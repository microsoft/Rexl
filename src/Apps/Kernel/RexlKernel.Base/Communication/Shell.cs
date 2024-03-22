// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.Json;

using Microsoft.Rexl.Private;

using NetMQ;
using NetMQ.Sockets;

namespace Microsoft.Rexl.Kernel;

/// <summary>
/// This serves both the shell and control channels. Each run on their own thread.
/// They are differentiated only by their handlers.
/// </summary>
public sealed class Shell : ThreadedChannel
{
    private readonly string _addr;
    private readonly IReadOnlyDictionary<string, Action<Shell, Message>> _handlers;
    private readonly byte[] _delim;

    private readonly RouterSocket _sock;

    public override string Name { get; }

    public Shell(ChannelHost host, string name, string addr,
            IReadOnlyDictionary<string, Action<Shell, Message>> handlers)
        : base(host)
    {
        Validation.AssertNonEmpty(name);
        Validation.AssertNonEmpty(addr);
        Validation.AssertNonEmpty(handlers);

        _addr = addr;
        _handlers = handlers;
        _delim = _enc.GetBytes("<IDS|MSG>");

        _sock = new RouterSocket();

        Name = name;
    }

    protected override void DisposeCore()
    {
        if (_sock != null)
            _sock.Dispose();
    }

    protected override void Init()
    {
        _sock.Bind(_addr);
    }

    protected override void DoOne()
    {
        if (!TryGetMessage(out Message msg))
            return;

        string kind = msg.Header.MessageType;
        if (!_handlers.TryGetValue(kind, out var fn))
        {
            _host.Logger.LogTop("{0}: No message handler for kind: '{1}'", Name, kind);
            return;
        }

        _host.Logger.LogMid("{0}: Received {1}", Name, kind);
        fn(this, msg);
    }

    private bool BytesEqual(byte[] a0, byte[] a1)
    {
        Validation.AssertValue(a0);
        Validation.AssertValue(a1);
        if (a0.Length != a1.Length)
            return false;
        for (int i = 0; i < a0.Length; i++)
        {
            if (a0[i] != a1[i])
                return false;
        }
        return true;
    }

    private static bool IsEmptyJson(string json)
    {
        if (json is null)
            return true;
        int cch = json.Length;
        if (cch == 0)
            return true;

        int ich = 0;
        for (; ; ich++)
        {
            if (ich >= cch)
                return true;
            var ch = json[ich];
            if (ch == '{')
                break;
            if (!char.IsWhiteSpace(ch))
                return false;
        }
        Validation.AssertIndex(ich, cch);
        Validation.Assert(json[ich] == '{');
        for (ich++; ; ich++)
        {
            if (ich >= cch)
                return false;
            var ch = json[ich];
            if (ch == '}')
                break;
            if (!char.IsWhiteSpace(ch))
                return false;
        }
        Validation.AssertIndex(ich, cch);
        Validation.Assert(json[ich] == '}');
        for (ich++; ; ich++)
        {
            if (ich >= cch)
                return true;
            var ch = json[ich];
            if (!char.IsWhiteSpace(ch))
                return false;
        }
    }

    private static T FromJson<T>(string json)
    {
        if (IsEmptyJson(json))
            return default;
        return JsonSerializer.Deserialize<T>(json, Common.SerializerOptions);
    }

    private bool TryGetMessage(out Message msg)
    {
        // See http://ipython.org/ipython-doc/dev/development/messaging.html#the-wire-protocol for details.

        msg = null;

        List<byte[]> frames = null;
        if (!_sock.TryReceiveMultipartBytes(new TimeSpan(10000), ref frames, 7))
        {
            msg = null;
            return false;
        }
        Validation.AssertValue(frames);

        LogLevel lvl = LogLevel.Low;
        _host.Logger.Log(lvl, "{0}: Message frames: {1}", Name, frames.Count);

        int iframe = 0;
        List<byte[]> identities = null;
        while (true)
        {
            if (iframe >= frames.Count)
            {
                _host.Logger.LogTop("{0}: Missing <IDS|MSG>!", Name);
                return false;
            }
            if (BytesEqual(frames[iframe], _delim))
            {
                iframe++;
                break;
            }
            if (identities is null)
                identities = new List<byte[]>();
            identities.Add(frames[iframe++]);
        }

        if (iframe > frames.Count - 5)
        {
            _host.Logger.LogTop("{0}: Malformed message: iframe: {1}, frames.Count: {2}!", Name, iframe, frames.Count);
            return false;
        }

        try
        {
            // REVIEW: Validate the signature? Or put it in the msg?
            string sig = _enc.GetString(frames[iframe++]);

            var hdr = FromJson<MsgHeader>(_enc.GetString(frames[iframe++]));
            var hdrPar = FromJson<MsgHeader>(_enc.GetString(frames[iframe++]));
            var metaData = MetadataFromJson(_enc.GetString(frames[iframe++]));
            var content = MessageContent.FromJson(hdr.MessageType, _enc.GetString(frames[iframe++]));

            var buffers = new List<byte[]>();
            while (iframe < frames.Count)
                buffers.Add(frames[iframe++]);

            msg = new Message(hdr, content, hdrPar, metaData, identities, buffers);
            return true;
        }
        catch (Exception ex)
        {
            _host.Logger.LogTop("{0}: Exception while parsing message: frames.Count: {1}, Ex: {2}", Name, frames.Count, ex);
            for (int i = 0; i < frames.Count; i++)
                _host.Logger.LogTop("{0}: Frame {1} ({2}): {3}", Name, i, frames[i].Length, _enc.GetString(frames[i]));
            return false;
        }
    }

    private static Dictionary<string, object> MetadataFromJson(string json)
    {
        var metadata = new Dictionary<string, object>();
        var doc = JsonDocument.Parse(json);
        foreach (var property in doc.RootElement.EnumerateObject())
            metadata[property.Name] = property.Value.ToObject();

        return metadata;
    }

    public void SendMain(Message parent, MessageContent content)
    {
        Validation.AssertValue(content);

        _host.Logger.LogTop("{0}: Sending {1}", Name, content.GetKind());
        Send(_sock, CreateMessage(parent, content));
    }
}

/// <summary>
/// Extension functions to convert <see cref="JsonElement"/> to normal .net objects.
/// </summary>
public static class JsonElementExtensions
{
    public static object ToObject(this JsonElement source)
    {
        return source.ValueKind switch
        {
            JsonValueKind.String => source.GetString(),
            JsonValueKind.False => false,
            JsonValueKind.True => true,
            // REVIEW: Should we try to use integer types?
            JsonValueKind.Number => source.GetDouble(),
            JsonValueKind.Object => source.ToDictionary(),
            JsonValueKind.Array => source.ToArray(),
            _ => null
        };
    }

    public static IDictionary<string, object> ToDictionary(this JsonElement source)
    {
        var ret = new Dictionary<string, object>();
        foreach (var value in source.EnumerateObject())
        {
            ret[value.Name] = value.Value.ToObject();
        }
        return ret;
    }

    public static object[] ToArray(this JsonElement source)
    {
        var ret = new List<object>();
        foreach (var value in source.EnumerateArray())
        {
            ret.Add(value.ToObject());
        }
        return ret.ToArray();
    }
}
