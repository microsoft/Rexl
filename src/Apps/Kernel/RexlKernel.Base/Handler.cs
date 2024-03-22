// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Kernel;

/// <summary>
/// Represents a language handler/executor. Note that this is distinct from a <see cref="Command"/>.
/// </summary>
public abstract partial class Handler
{
    protected readonly Logger _logger;
    protected readonly Publisher _pub;

    /// <summary>
    /// The dictionary of commands.
    /// </summary>
    protected readonly Dictionary<string, Command> _commands;

    // The composite kernel can directly set this. No other code should.
    private Composite _parent;

    /// <summary>
    /// The parent composite handler, if there is one. Typically, there is at most one composite handler
    /// that contains one or more simple handlers, with one being the default.
    /// </summary>
    public Composite Parent => _parent;

    /// <summary>
    /// A publisher for this handler.
    /// </summary>
    public Publisher Publisher => _pub;

    protected Handler(Logger logger, Publisher pub)
    {
        Validation.AssertValue(logger);
        Validation.AssertValue(pub);

        _logger = logger;
        _pub = pub;
        _commands = new();
    }

    /// <summary>
    /// Adds a command to this handler. If the name is not specified, the command's default name is used.
    /// </summary>
    public void AddCmd(Command cmd, string name = null)
    {
        Validation.AssertValue(cmd);
        Validation.AssertNonEmptyOrNull(name);

        _commands[name ?? cmd.Name] = cmd;
    }

    /// <summary>
    /// Execute the given <see cref="ExecuteMessage"/>.
    /// </summary>
    public abstract Task ExecAsync(ExecuteMessage msg);

    public abstract Task CleanupAsync();

    /// <summary>
    /// Fetch the command with the given name, if there is one.
    /// </summary>
    public virtual bool TryGetCommand(string name, out Command command)
    {
        return _commands.TryGetValue(name, out command);
    }

    /// <summary>
    /// Get the commands and their names, sorted by name.
    /// </summary>
    public virtual IEnumerable<(string name, Command cmd)> GetCommands()
    {
        return _commands.Select(kvp => (kvp.Key, kvp.Value)).OrderBy(k => k.Key);
    }
}

// This partial contains the Simple handler definition.
partial class Handler
{
    /// <summary>
    /// A simple handler does not contain other handlers.
    /// </summary>
    public abstract class Simple : Handler
    {
        /// <summary>
        /// The default name of this simple handler.
        /// </summary>
        public string Name { get; }

        protected Simple(Logger logger, Publisher pub, string name)
            : base(logger, pub)
        {
            Validation.AssertNonEmpty(name);
            Name = name;
        }

        public sealed override Task ExecAsync(ExecuteMessage msg)
        {
            return ExecAsync(msg, 0, msg.Request.Code.Length);
        }

        /// <summary>
        /// Execute the indicated portion of the text in the given <see cref="ExecuteMessage"/>.
        /// </summary>
        public abstract Task ExecAsync(ExecuteMessage msg, int min, int lim);
    }
}

// This partial contains the Composite handler definition.
partial class Handler
{
    /// <summary>
    /// A composite handler contains one or more simple handlers. One is the "default".
    /// </summary>
    public sealed class Composite : Handler
    {
        /// <summary>
        /// The dictionary of simple handlers.
        /// </summary>
        private readonly Dictionary<string, Simple> _map;

        /// <summary>
        /// The default simple handler.
        /// </summary>
        public Simple Default => _map[""];

        public Composite(Logger logger, Simple handler, string name = null)
            : base(logger, handler._pub)
        {
            Validation.AssertValue(handler);
            Validation.Assert(handler._parent is null);
            Validation.AssertNonEmptyOrNull(name);

            _map = new();
            _map.Add("", handler);
            _map.Add(name ?? handler.Name, handler);
            handler._parent = this;
        }

        /// <summary>
        /// Map the given name to the given simple handler. When <paramref name="name"/> is <c>null</c>, uses
        /// the default name of <paramref name="handler"/>.
        /// </summary>
        public void SetHandler(Simple handler, string name = null)
        {
            Validation.AssertValue(handler);
            Validation.Assert(handler._parent is null || handler._parent == this);
            Validation.AssertNonEmptyOrNull(name);

            _map[name ?? handler.Name] = handler;
            handler._parent = this;
        }

        /// <summary>
        /// Get the child handlers and their names, sorted by name.
        /// </summary>
        public IEnumerable<(string name, Simple child)> GetHandlers()
        {
            return _map.Select(kvp => (kvp.Key, kvp.Value)).OrderBy(k => k.Key);
        }

        public override Task ExecAsync(ExecuteMessage msg)
        {
            // Tokenize the code.
            var toks = CellLexer.Run(msg.Request.Code);

            // If there are no tokens, there is nothing to do.
            if (toks.Count == 0)
                return Task.CompletedTask;

            // If there is a single code token, directly invoke the default simple handler.
            if (toks.Count == 1 && toks[0] is CellLexer.CodeToken ct)
                return Default.ExecAsync(msg);

            // Handle multiple tokens or single cmd token.
            return ExecMultiAsync(msg, toks);
        }

        private async Task ExecMultiAsync(ExecuteMessage msg, IReadOnlyList<CellLexer.Token> toks)
        {
            var simple = Default;
            for (int itok = 0; itok < toks.Count; itok++)
            {
                switch (toks[itok])
                {
                case CellLexer.CodeToken codeTok:
                    await simple.ExecAsync(msg, codeTok.Min, codeTok.Lim).ConfigureAwait(false);
                    break;
                case CellLexer.CmdToken cmdTok:
                    CellLexer.ArgsToken? argsTok = null;
                    if (itok + 1 < toks.Count && toks[itok + 1] is CellLexer.ArgsToken tmp)
                    {
                        argsTok = tmp;
                        itok++;
                    }
                    if (_map.TryGetValue(cmdTok.Name, out var simpleNew))
                    {
                        simple = simpleNew;
                        if (argsTok is null)
                            break;
                    }
                    if (simple.TryGetCommand(cmdTok.Name, out var cmd) ||
                        this.TryGetCommand(cmdTok.Name, out cmd))
                    {
                        await cmd.ExecAsync(msg, argsTok?.Value).ConfigureAwait(false);
                    }
                    else
                        _pub.PublishError(msg, $"Unknown command: '{cmdTok.Name}'");
                    break;
                }
            }
        }

        public override async Task CleanupAsync()
        {
            // Cleanup all the simple handlers. Since there can be aliases, track the handlers that
            // have already been processed.
            var done = new HashSet<Simple>();
            foreach (var smp in _map.Values)
            {
                if (done.Add(smp))
                    await smp.CleanupAsync().ConfigureAwait(false);
            }
        }
    }
}
