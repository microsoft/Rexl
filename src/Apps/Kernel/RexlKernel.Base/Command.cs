// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Kernel;

/// <summary>
/// A command, invoked via "#!name".
/// </summary>
public abstract class Command
{
    /// <summary>
    /// The name, not including "#!".
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Help text.
    /// </summary>
    public virtual string? HelpText { get; }

    protected Command(string name, string? helpText)
    {
        Validation.AssertNonEmpty(name);
        Validation.AssertNonEmptyOrNull(name);

        Name = name;
        HelpText = helpText;
    }

    /// <summary>
    /// Execute the command in the context of the given <see cref="ExecuteMessage"/> with the given arguments,
    /// encoded as raw text.
    /// </summary>
    public abstract Task ExecAsync(ExecuteMessage msg, string? args);
}

/// <summary>
/// Base command class with a strongly typed parent <see cref="Handler"/>.
/// </summary>
public abstract class Command<THandler> : Command
    where THandler : Handler
{
    protected readonly THandler _parent;

    protected Command(THandler parent, string name, string? helpText = null)
        : base(name, helpText)
    {
        Validation.AssertValue(parent);
        _parent = parent;
    }

    protected void ErrorOnNonEmpty(ExecuteMessage msg, string? args)
    {
        if (!string.IsNullOrWhiteSpace(args))
            _parent.Publisher.PublishError(msg, $"Unsupported args for '#!{Name}'");
    }
}
