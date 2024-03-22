// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

namespace Microsoft.Rexl.Kernel;

public sealed partial class RexlProgram : Program
{
    private readonly Handler.Composite _handler;
    private readonly Handler.Simple _rexl;
    private readonly Handler.Simple _pfx;

    public override string User => "rexl_user";

    public static void Main(string[] args)
    {
        try
        {
            var prog = new RexlProgram(args);
            prog._logger.LogTop("Finished ctor");
            prog.Run();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Aborted: {0}", ex.Message);
        }
    }

    private RexlProgram(string[] args, LogLevel lvl = LogLevel.All)
        : base(args, lvl)
    {
        // REVIEW: Set the default via command line.
        _rexl = new RexlHandler(_logger, _publisher);
        _handler = new Handler.Composite(_logger, _rexl);
        _handler.AddCmd(new LsMagicCmd(_handler));
        _handler.AddCmd(new TimeCmd(_handler));

        _pfx = new PfxHandler(_logger, _publisher);
        _handler.SetHandler(_pfx);
    }

    protected override KernelInfoReply GetKernelInfo()
    {
        // REVIEW: Fix this.
        var lang = new LanguageInfo("Rexl", "0.22", "text/x-rexl", ".rexl", "rexl");
        return new KernelInfoReply(Common.JupyterWireVersion, "irexl", "0.1", lang, "rexl");
    }

    protected override Handler GetHandler()
    {
        return _handler;
    }
}
