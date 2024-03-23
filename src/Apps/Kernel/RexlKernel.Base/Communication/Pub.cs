// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

using Microsoft.Rexl.Private;

using NetMQ.Sockets;

namespace Microsoft.Rexl.Kernel;

/// <summary>
/// The publish channel.
/// </summary>
public sealed class Pub : Channel
{
    private readonly string _addr;
    private readonly PublisherSocket _sock;

    public Pub(ChannelHost host, string addr)
        : base(host)
    {
        Validation.AssertNonEmpty(addr);

        _addr = addr;
        _sock = new PublisherSocket();
        _sock.Bind(_addr);
    }

    protected override void DisposeCore()
    {
        if (_sock != null)
            _sock.Dispose();
    }

    public struct BusyToken : IDisposable
    {
        private readonly Pub _pub;
        private readonly string _name;
        private readonly Message _msg;

        public BusyToken(Pub pub, string name, Message msg)
        {
            Validation.AssertValue(pub);
            Validation.AssertNonEmpty(name);
            Validation.AssertValue(msg);
            _pub = pub;
            _name = name;
            _msg = msg;

            _pub._host.Logger.LogLow("{0}: Busy", _name);
            _pub.Send(_pub._sock, _pub.CreateMessage(_msg, StatusContent.Busy));
        }

        public void Dispose()
        {
            _pub._host.Logger.LogLow("{0}: Idle", _name);
            _pub.Send(_pub._sock, _pub.CreateMessage(_msg, StatusContent.Idle));
        }
    }

    public BusyToken Busy(string name, Message parent)
    {
        return new BusyToken(this, name, parent);
    }

    public void SendStarting()
    {
        _host.Logger.LogLow("Starting");
        Send(_sock, CreateMessage(StatusContent.Starting));
    }

    public void SendMsg(Message parent, MessageContent content)
    {
        Validation.AssertValue(content);

        _host.Logger.LogLow("*Pub*: Sending {0}", content.GetKind());
        Send(_sock, CreateMessage(parent, content));
    }
}
