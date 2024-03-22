// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Microsoft.Rexl.Kernel;

/// <summary>
/// The handler for Rexl.
/// </summary>
public sealed partial class RexlHandler : Handler.Simple
{
    private readonly Executor _exec;

    public RexlHandler(Logger logger, Publisher pub)
        : base(logger, pub, "rexl")
    {
        _exec = Executor.Create(_logger, pub);

        AddCmd(new GlobalsCmd(this));
        AddCmd(new TasksCmd(this));
        AddCmd(new ShowHideILCmd(this, true));
        AddCmd(new ShowHideILCmd(this, false));
        AddCmd(new RecoverCmd(this));
        AddCmd(new RecoverCmd(this, true));
        AddCmd(new RecoverCmd(this, false));
        AddCmd(new VerboseCmd(this));
        AddCmd(new VerboseCmd(this, true));
        AddCmd(new VerboseCmd(this, false));
    }

    public override Task ExecAsync(ExecuteMessage msg, int min, int lim)
    {
        return _exec.ExecAsync(msg, min, lim);
    }

    public override Task CleanupAsync()
    {
        return _exec.CleanupAsync();
    }
}

// Contains the rexl-based commands.
partial class RexlHandler
{
    /// <summary>
    /// Base class for rexl commands.
    /// </summary>
    private abstract class RexlCmd : Command<RexlHandler>
    {
        protected RexlCmd(RexlHandler parent, string name, string helpText)
            : base(parent, name, helpText)
        {
        }
    }

    /// <summary>
    /// List the globals and their types.
    /// </summary>
    private sealed class GlobalsCmd : RexlCmd
    {
        public GlobalsCmd(RexlHandler handler)
            : base(handler, "globals", "Display the names and types of the globals.")
        {
        }

        public override Task ExecAsync(ExecuteMessage msg, string? args)
        {
            ErrorOnNonEmpty(msg, args);

            var globals = _parent._exec.GetGlobalInfos()
                .Where(pair => !pair.name.IsRoot)
                .Select(pair => (pair.name.ToDottedSyntax(), pair.type.Serialize()));
            var plain = string.Join("\n", globals.Select(pair => pair.Item1 + " : " + pair.Item2));

            // Generate html.
            var html = new StringBuilder();
            html.Append("<div><table>")
                .Append("<thead><tr><th>Variable</th><th>Type</th></tr></thead>")
                .Append("<tbody>");
            foreach (var (name, type) in globals)
            {
                html.Append("<tr>")
                    .Append("<td>").Append(HttpUtility.HtmlEncode(name)).Append("</td>")
                    .Append("<td>").Append(HttpUtility.HtmlEncode(type)).Append("</td>")
                    .Append("</tr>");
            }
            html.Append("</tbody></table></div>");

            _parent._pub.PublishData(msg, plain, html.ToString());
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// List the tasks and their states.
    /// </summary>
    private sealed class TasksCmd : RexlCmd
    {
        public TasksCmd(RexlHandler handler)
            : base(handler, "tasks", "Display the names and states of the tasks.")
        {
        }

        public override Task ExecAsync(ExecuteMessage msg, string? args)
        {
            ErrorOnNonEmpty(msg, args);

            var tasks = _parent._exec.GetTaskInfos()
                .Where(pair => !pair.name.IsRoot)
                .Select(pair => (pair.name.ToDottedSyntax(), pair.state.ToString()));
            var plain = string.Join("\n", tasks.Select(pair => pair.Item1 + " : " + pair.Item2));

            // Generate html.
            var html = new StringBuilder();
            html.Append("<div><table>")
                .Append("<thead><tr><th>Task</th><th>State</th></tr></thead>")
                .Append("<tbody>");
            foreach (var (name, state) in tasks)
            {
                html.Append("<tr>")
                    .Append("<td>").Append(HttpUtility.HtmlEncode(name)).Append("</td>")
                    .Append("<td>").Append(HttpUtility.HtmlEncode(state)).Append("</td>")
                    .Append("</tr>");
            }
            html.Append("</tbody></table></div>");

            _parent._pub.PublishData(msg, plain, html.ToString());
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Show or hide generated IL.
    /// </summary>
    private sealed class ShowHideILCmd : RexlCmd
    {
        public bool Show { get; }

        public ShowHideILCmd(RexlHandler handler, bool show)
            : base(handler,
                show ? "show-il" : "hide-il",
                show ? "Show generated IL." : "Don't show generated IL.")
        {
            Show = show;
        }

        public override Task ExecAsync(ExecuteMessage msg, string? args)
        {
            var flag = Show;
            switch (args?.Trim())
            {
            case "--on":
            case "-on":
                break;
            case "--off":
            case "-off":
                flag = !flag;
                break;
            default:
                ErrorOnNonEmpty(msg, args);
                break;
            }

            _parent._exec.Config.SetShowIL(flag);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Base class for toggle commands, where there are (up to) three variations.
    /// </summary>
    private abstract class ToggleCmd : RexlCmd
    {
        public bool? On { get; }

        public ToggleCmd(RexlHandler handler, bool? on, string name, string help)
            : base(handler, name, help)
        {
            On = on;
        }

        public sealed override Task ExecAsync(ExecuteMessage msg, string? args)
        {
            var flag = On ?? true;
            switch (args?.Trim())
            {
            case "--on":
            case "-on":
                flag = true;
                break;
            case "--off":
            case "-off":
                flag = false;
                break;
            default:
                ErrorOnNonEmpty(msg, args);
                break;
            }

            return ExecCoreAsync(msg, flag);
        }

        protected abstract Task ExecCoreAsync(ExecuteMessage msg, bool flag);
    }

    /// <summary>
    /// Turn recover mode on or off.
    /// </summary>
    private sealed class RecoverCmd : ToggleCmd
    {
        public RecoverCmd(RexlHandler handler)
            : base(handler, null, "recover", "Turn recover mode on or off.")
        {
        }

        public RecoverCmd(RexlHandler handler, bool on)
            : base(handler, on,
                on ? "recover-on" : "recover-off",
                on ?
                    "Turn on recover mode so cell execution continues when there is an error." :
                    "Turn off recover mode so cell execution stops when there is an error.")
        {
        }

        protected override Task ExecCoreAsync(ExecuteMessage msg, bool flag)
        {
            _parent._exec.Config.SetShouldContinue(flag);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Turn verbose mode on or off.
    /// </summary>
    private sealed class VerboseCmd : ToggleCmd
    {
        public VerboseCmd(RexlHandler handler)
            : base(handler, null, "verbose", "Turn verbose mode on or off.")
        {
        }

        public VerboseCmd(RexlHandler handler, bool on)
            : base(handler, on,
                on ? "verbose-on" : "verbose-off",
                on == true ? "Turn on verbose mode." : "Turn off verbose mode.")
        {
        }

        protected override Task ExecCoreAsync(ExecuteMessage msg, bool flag)
        {
            _parent._exec.Config.SetVerbose(flag);
            return Task.CompletedTask;
        }
    }
}
