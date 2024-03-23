// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text;

using Microsoft.Rexl.Private;

using NetMQ;
using NetMQ.Sockets;

namespace Microsoft.Rexl.Kernel;

public sealed class HeartBeat : ThreadedChannel
{
    private readonly string _addr;
    private readonly ResponseSocket _sock;

    public override string Name => "Heart";

    public HeartBeat(ChannelHost host, string addr)
        : base(host)
    {
        Validation.AssertNonEmpty(addr);

        _addr = addr;
        _sock = new ResponseSocket();
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
        if (_sock.TryReceiveFrameBytes(new TimeSpan(10000), out byte[] data))
        {
            _host.Logger.LogMid(Encoding.Default.GetString(data));
            _sock.TrySendFrame(data);
        }
    }
}
