// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Web;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Kernel;

using Stopwatch = System.Diagnostics.Stopwatch;

/// <summary>
/// Top level (global) command to list all languages and commands.
/// </summary>
public sealed class LsMagicCmd : Command<Handler.Composite>
{
    public LsMagicCmd(Handler.Composite parent)
        : base(parent, "lsmagic", "List the available magic commands / directives.")
    {
    }

    public override Task ExecAsync(ExecuteMessage msg, string? args)
    {
        ErrorOnNonEmpty(msg, args);

        var sb = new StringBuilder();
        var htm = new StringBuilder();

        AddSome(sb, htm, "Global", _parent.GetCommands(), _parent.GetHandlers());

        _parent.Publisher.PublishData(msg, sb.ToString(), htm.ToString());

        return Task.CompletedTask;

        static void AddSome(StringBuilder plain, StringBuilder html, string owner,
            IEnumerable<(string name, Command cmd)>? cmdsEnum,
            IEnumerable<(string name, Handler.Simple handler)>? hndlrsEnum)
        {
            var hndlrs = hndlrsEnum?.ToArray() ?? Array.Empty<(string name, Handler.Simple handler)>();
            var cmds = cmdsEnum?.ToArray() ?? Array.Empty<(string name, Command cmd)>();

            if (cmds.Length == 0 && hndlrs.Length == 0)
                return;

            if (plain.Length > 0)
                plain.AppendLine();
            plain.Append(owner).Append(" magics").AppendLine();

            html.Append("<div>")
                .Append("<h3>").Append(HttpUtility.HtmlEncode(owner)).Append(" magics</h3>")
                .Append("<div>");

            foreach (var (name, hndlr) in hndlrs)
            {
                if (!string.IsNullOrWhiteSpace(name))
                    AddOne(name, $"Run the remaining code in the cell using the {name} language.", plain, html);
            }

            foreach (var (name, cmd) in cmds)
                AddOne(name, cmd.HelpText, plain, html);

            foreach (var (name, hndlr) in hndlrs)
            {
                if (!string.IsNullOrEmpty(name))
                    AddSome(plain, html, name, hndlr.GetCommands(), null);
            }
        }

        static void AddOne(string name, string help, StringBuilder plain, StringBuilder html)
        {
            plain.AppendFormat("  {0}: {1}", name, help).AppendLine();

            html
                .Append("<div style=\"text-indent:1.5em\">")
                    .Append("<pre><span style=\"text-indent:1.5em; color:#512bd4\">")
                        .Append("#!").Append(HttpUtility.HtmlEncode(name))
                        .Append("</span></pre>")
                    .Append("<div style=\"text-indent:3em\">")
                        .Append(HttpUtility.HtmlEncode(help))
                        .Append("</div>")
                    .Append("</div>");
        }
    }
}

/// <summary>
/// Top level (global) command to time the remainder of the cell execution.
/// </summary>
public sealed class TimeCmd : Command<Handler.Composite>
{
    public TimeCmd(Handler.Composite parent)
        : base(parent, "time", "Time the execution of the remaining code in the cell.")
    {
    }

    public override Task ExecAsync(ExecuteMessage msg, string? args)
    {
        ErrorOnNonEmpty(msg, args);

        var timer = Stopwatch.StartNew();
        msg.OnComplete(() =>
        {
            var elapsed = timer.Elapsed;
            _parent.Publisher.PublishData(msg, $"Wall time: {elapsed.TotalMilliseconds}ms");
        });

        return Task.CompletedTask;
    }
}
