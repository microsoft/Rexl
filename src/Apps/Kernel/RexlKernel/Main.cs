// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Rexl.Kernel;

public sealed partial class RexlProgram : Program
{
    private readonly Handler.Composite _handler;
    private readonly Handler.Simple _rexl;
    private readonly Handler.Simple _pfx;

    // The default kernel/language.
    private readonly string _kernDef;

    public override string User => "rexl_user";

    public static void Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return;
            }

            var cmd = args[0].ToLowerInvariant();
            string kernDef;
            switch (cmd)
            {
            default:
                PrintUsage();
                return;

            case "pre-register":
                Register(full: false, rexl: true, pfx: true);
                return;
            case "register":
            case "register-rexl":
                Register(full: true, rexl: true, pfx: false);
                return;
            case "register-pfx":
                Register(full: true, rexl: false, pfx: true);
                return;
            case "register-all":
                Register(full: true, rexl: true, pfx: true);
                return;
            case "rexl":
            case "pfx":
                if (args.Length <= 1)
                    goto default;
                kernDef = cmd;
                break;
            }

            var prog = new RexlProgram(kernDef, args.AsSpan(1));
            prog._logger.LogTop("Finished ctor");
            prog.Run();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Aborted: {0}", ex.Message);
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine($@"Usage:

    {Path.GetFileNameWithoutExtension(typeof(RexlProgram).Assembly.Location)} <cmd>

    where <cmd> is one of:
        help: Display this usage information.
        pre-register: Create the kernel spec directories (.krn-rexl and .krn-pfx).
        register: Register Rexl with Jupyter.
        register-pfx: Register Power Fx with Jupyter.
        register-all: Register Rexl and Power Fx with Jupyter.
        [rexl|pfx] <connection-file>: typically invoked by Jupyter, not directly.
");
    }

    private static void Register(bool full, bool rexl, bool pfx)
    {
        var loc = typeof(RexlProgram).Assembly.Location;
        var dirBase = Path.GetDirectoryName(loc);
        var app = Path.Combine(dirBase, Path.GetFileNameWithoutExtension(loc));

        if (rexl)
            RegisterCore(full, dirBase, app, "Rexl", "Rexl");
        if (pfx)
            RegisterCore(full, dirBase, app, "Pfx", "Power Fx");
    }

    private static void RegisterCore(bool full, string dirBase, string app, string lang, string disp)
    {
        var name = lang.ToLowerInvariant();
        var dir = Path.Combine(dirBase, $".krn-{name}");
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, "kernel.json");
        File.WriteAllText(path, $@"{{
    ""argv"": [""{app.Replace('\\', '/')}"", ""{name}"", ""{{connection_file}}""],
    ""display_name"": ""{disp}"",
    ""language"": ""{lang}""
}}
");

        if (!full)
            return;

        var proc = new System.Diagnostics.Process()
        {
            StartInfo =
                {
                    FileName = "jupyter",
                    Arguments = $"kernelspec install \"{dir.Replace('\\', '/')}\" --user --name={name}",
                    RedirectStandardError = false,
                    RedirectStandardOutput = false,
                    RedirectStandardInput = false,
                }
        };

        proc.Start();
        proc.WaitForExit();
    }

    private RexlProgram(string kernDef, ReadOnlySpan<string> args, LogLevel lvl = LogLevel.All)
        : base(args, lvl)
    {
        _rexl = new RexlHandler(_logger, _publisher);
        _pfx = new PfxHandler(_logger, _publisher);

        switch (kernDef)
        {
        case "pfx":
            _handler = new Handler.Composite(_logger, _pfx);
            _handler.SetHandler(_rexl);
            _kernDef = kernDef;
            break;

        default:
            _handler = new Handler.Composite(_logger, _rexl);
            _handler.SetHandler(_pfx);
            _kernDef = "rexl";
            break;
        }

        // Add magics.
        _handler.AddCmd(new LsMagicCmd(_handler));
        _handler.AddCmd(new TimeCmd(_handler));
    }

    protected override KernelInfoReply GetKernelInfo()
    {
        LanguageInfo lang;
        string banner;
        switch (_kernDef)
        {
        case "pfx":
            lang = new LanguageInfo("PowerFx", "1.1", "text/x-pfx", ".pfx", "pfx");
            banner = "PowerFx";
            break;
        default:
            lang = new LanguageInfo("Rexl", "1.0", "text/x-rexl", ".rexl", "rexl");
            banner = "Rexl";
            break;
        }

        // REVIEW: Current Jupyter seems unable to connect to these links. It shows an error like
        // "github.com refused to connect".
        List<WebLink> links = new List<WebLink>() {
            new WebLink { Text="Rexl Project", Url="https://github.com/microsoft/Rexl" },
            new WebLink { Text="Rexl User Guide", Url="https://github.com/microsoft/Rexl/blob/main/docs/UserGuide/RexlUserGuide.md" },
            new WebLink { Text="Power Fx Project", Url="https://github.com/microsoft/Power-Fx" },
            new WebLink { Text="Power Fx Reference", Url="https://learn.microsoft.com/en-us/power-platform/power-fx/overview" },
        };
        return new KernelInfoReply(Common.JupyterWireVersion, "RexlKern", "1.0", lang, banner, links);
    }

    protected override Handler GetHandler()
    {
        return _handler;
    }
}
