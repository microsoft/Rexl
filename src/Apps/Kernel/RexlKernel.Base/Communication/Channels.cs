// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

using Microsoft.Rexl.Private;

using NetMQ;

namespace Microsoft.Rexl.Kernel;

/// <summary>
/// Common utilities and constants.
/// </summary>
public static class Common
{
    public const string JupyterWireVersion = "5.3";

    public static readonly JsonSerializerOptions SerializerOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var res = new JsonSerializerOptions(JsonSerializerDefaults.General)
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowNamedFloatingPointLiterals,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        //SerializerOptions.Converters.Add(new DataDictionaryConverter());
        //SerializerOptions.Converters.Add(new BareObjectConverter());

        return res;
    }
}

/// <summary>
/// Host for a channel.
/// </summary>
public abstract class ChannelHost
{
    public abstract string Session { get; }
    public abstract string User { get; }
    public abstract Logger Logger { get; }
    public abstract SignatureHandler Sig { get; }

    protected ChannelHost()
    {
    }
}

/// <summary>
/// Base class for a communication channel.
/// </summary>
public abstract class Channel : IDisposable
{
    protected readonly ChannelHost _host;

    // REVIEW: Less than ideal factoring, since HeartBeat doesn't need _sig and _enc.
    // On the other hand, Pub doesn't need its own thread. Here's a classic instance where
    // multiple inheritance would be useful.

    protected readonly SignatureHandler _sig;
    protected readonly Encoding _enc;

    private readonly object _lock;

    private volatile bool _disposed;

    protected Channel(ChannelHost host)
    {
        Validation.AssertValue(host);

        _host = host;
        _enc = _host.Sig?.Enc;
        _lock = new object();
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (!_disposed)
            {
                _disposed = true;
                DisposeCore();
            }
        }

        GC.SuppressFinalize(this);
    }

    protected abstract void DisposeCore();

    /// <summary>
    /// Create a message with a parent message.
    /// </summary>
    protected Message CreateMessage(Message parent, MessageContent content)
    {
        Validation.AssertValue(content);
        Validation.AssertValue(parent);

        return new Message(
            CreateHeader(content.GetKind()), content,
            parent.Header, identities: parent.Identities);
    }

    /// <summary>
    /// Create a message with no parent message.
    /// </summary>
    protected Message CreateMessage(MessageContent content)
    {
        Validation.AssertValue(content);

        return new Message(CreateHeader(content.GetKind()), content);
    }

    protected MsgHeader CreateHeader(string kind)
    {
        var hdr = new MsgHeader(
            messageId: Guid.NewGuid().ToString(),
            session: _host.Session,
            username: _host.User,
            date: DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            messageType: kind,
            version: Common.JupyterWireVersion);

        return hdr;
    }

    protected byte[] Encode(object val)
    {
        Validation.AssertValue(_enc);
        string str = JsonSerializer.Serialize(val, Common.SerializerOptions);
        return _enc.GetBytes(str);
    }

    protected void Send(NetMQSocket sock, Message msg)
    {
        Validation.AssertValue(_host.Sig);

        byte[] hdr = Encode(msg.Header);
        byte[] par = Encode(msg.ParentHeader);
        byte[] md = Encode(msg.MetaData);
        byte[] cnt = Encode(msg.Content);

        string hmac = _host.Sig.CreateSignature(hdr, par, md, cnt);

        lock (_lock)
        {
            if (_disposed)
                return;

            if (msg.Identities != null)
            {
                foreach (var ident in msg.Identities)
                    sock.SendFrame(ident, true);
            }

            sock.SendFrame("<IDS|MSG>", true);
            sock.SendFrame(hmac, true);
            sock.SendFrame(hdr, true);
            sock.SendFrame(par, true);
            sock.SendFrame(md, true);
            sock.SendFrame(cnt, false);
        }
    }
}

/// <summary>
/// Base for HeartBeat and Shell / Control. These all have their own thread.
/// </summary>
public abstract class ThreadedChannel : Channel
{
    protected readonly Thread _thread;
    protected volatile bool _stop;

    public abstract string Name { get; }

    protected ThreadedChannel(ChannelHost host)
        : base(host)
    {
        _thread = new Thread(Loop);
    }

    public Thread Thread { get { return _thread; } }

    public void Start()
    {
        Validation.Assert(!_stop);
        _thread.Start();
    }

    public void Stop()
    {
        _stop = true;
    }

    private void Loop()
    {
        try
        {
            Init();
            _host.Logger.LogTop("{0}: Inited thread", Name);
            while (!_stop)
                DoOne();
            _host.Logger.LogTop("{0}: Stopping thread", Name);
        }
        finally
        {
            _host.Logger.LogTop("{0}: Leaving thread", Name);
        }
    }

    protected abstract void Init();
    protected abstract void DoOne();
}
