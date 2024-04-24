// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

/// <summary>
/// Information about an operation entry. For use in an operation registry and by the binder.
/// </summary>
public sealed class OperInfo
{
    /// <summary>
    /// Path for this operation.
    /// </summary>
    public NPath Path { get; }

    /// <summary>
    /// The operation object.
    /// </summary>
    public RexlOper Oper { get; }

    /// <summary>
    /// Whether this entry is hidden.
    /// </summary>
    public bool Hidden { get; }

    /// <summary>
    /// Whether this entry is deprecated.
    /// </summary>
    public bool Deprecated { get; }

    /// <summary>
    /// When this is deprecated, this is an optional alternative to point the user to.
    /// </summary>
    public NPath PathAlt { get; }

    private readonly Immutable.Array<Signature> _sigs;

    /// <summary>
    /// Get the signatures (if any) from the <paramref name="oper"/>.
    /// </summary>
    public OperInfo(NPath path, RexlOper oper,
        bool hidden = false, bool deprecated = false, NPath pathAlt = default)
    {
        Validation.BugCheckParam(!path.IsRoot, nameof(path));
        Validation.BugCheckValue(oper, nameof(oper));
        Path = path;
        Oper = oper;
        Hidden = hidden;
        Deprecated = deprecated;
        PathAlt = pathAlt;
        _sigs = oper is IHaveSignatures have ? have.GetSignatures() : Immutable.Array<Signature>.Empty;
    }

    /// <summary>
    /// With one signature.
    /// </summary>
    public OperInfo(NPath path, RexlOper oper, Signature sig,
        bool hidden = false, bool deprecated = false, NPath pathAlt = default)
    {
        Validation.BugCheckParam(!path.IsRoot, nameof(path));
        Validation.BugCheckValue(oper, nameof(oper));
        Validation.BugCheckValue(sig, nameof(sig));

        Path = path;
        Oper = oper;
        Hidden = hidden;
        Deprecated = deprecated;
        PathAlt = pathAlt;
        _sigs = Immutable.Array.Create(sig);
    }

    /// <summary>
    /// With given signatures.
    /// </summary>
    public OperInfo(NPath path, RexlOper oper, Immutable.Array<Signature> sigs,
        bool hidden = false, bool deprecated = false, NPath pathAlt = default)
    {
        Validation.BugCheckParam(!path.IsRoot, nameof(path));
        Validation.BugCheckValue(oper, nameof(oper));
        Validation.BugCheckParam(!sigs.IsDefault, nameof(sigs));

        Path = path;
        Oper = oper;
        Hidden = hidden;
        Deprecated = deprecated;
        PathAlt = pathAlt;
        _sigs = sigs;
    }

    /// <summary>
    /// Get the signatures.
    /// </summary>
    public Immutable.Array<Signature> GetSignatures()
    {
        return _sigs;
    }
}

/// <summary>
/// Abstract base class for a registry of rexl operations (functions and procedures).
/// NOTE: This is not thread safe while additions are being made. The derived class and its clients
/// must ensure that additions are not being made while lookups may be happening on a different thread.
/// A simple way to ensure this is to make mutations only in the constructor.
/// </summary>
public abstract class OperationRegistry : RegistryBase<OperationRegistry, NPath, OperInfo>
{
    /// <summary>
    /// Ctor when there are no parent registries.
    /// </summary>
    protected OperationRegistry()
        : base()
    {
    }

    /// <summary>
    /// Ctor when there is at most one parent registry.
    /// </summary>
    protected OperationRegistry(OperationRegistry parent)
        : base(parent)
    {
    }

    /// <summary>
    /// Ctor for any number of parent registries.
    /// </summary>
    protected OperationRegistry(params OperationRegistry[] parents)
        : base(parents)
    {
    }

    /// <summary>
    /// Ctor for any number of parent registries.
    /// </summary>
    protected OperationRegistry(IEnumerable<OperationRegistry> parents)
        : base(parents)
    {
    }

    /// <summary>
    /// Adds an entry to the registry.
    /// This bug checks if the path has already been set by this registry, but not when it overrides
    /// one inherited from a parent.
    /// </summary>
    protected void AddOne(OperInfo info)
    {
        Validation.BugCheckValue(info, nameof(info));

        AddItem(info.Path, info);
    }

    /// <summary>
    /// Create an <see cref="OperInfo"/> for the given information.
    /// </summary>
    protected OperInfo CreateInfo(NPath path, RexlOper sym)
    {
        return new OperInfo(path, sym);
    }

    /// <summary>
    /// Create an <see cref="OperInfo"/> for the given information.
    /// </summary>
    protected OperInfo CreateInfo(NPath path, RexlOper sym, Signature sig,
        bool hidden = false, bool deprecated = false, NPath pathAlt = default)
    {
        return new OperInfo(path, sym, sig, hidden, deprecated, pathAlt);
    }

    /// <summary>
    /// Create an <see cref="OperInfo"/> for the given information.
    /// </summary>
    protected OperInfo CreateInfo(NPath path, RexlOper sym,
        bool hidden = false, bool deprecated = false, NPath pathAlt = default)
    {
        return new OperInfo(path, sym, hidden, deprecated, pathAlt);
    }

    /// <summary>
    /// Create an <see cref="OperInfo"/> for the given information.
    /// </summary>
    protected OperInfo CreateInfo(NPath path, RexlOper sym, Immutable.Array<Signature> sigs,
        bool hidden = false, bool deprecated = false, NPath pathAlt = default)
    {
        return new OperInfo(path, sym, sigs, hidden, deprecated, pathAlt);
    }

    /// <summary>
    /// Adds a symbol with its declared path.
    /// This bug checks if the path has already been set by this registry, but not when it overrides
    /// one inherited from a parent.
    /// </summary>
    protected void AddOne(RexlOper sym)
    {
        Validation.BugCheckValue(sym, nameof(sym));
        AddOne(CreateInfo(sym.Path, sym));
    }

    /// <summary>
    /// Adds a symbol with its declared path and given signature.
    /// This bug checks if the path has already been set by this registry, but not when it overrides
    /// one inherited from a parent.
    /// </summary>
    protected void AddOne(RexlOper sym, Signature sig,
        bool hidden = false, bool deprecated = false, NPath pathAlt = default)
    {
        Validation.BugCheckValue(sym, nameof(sym));
        Validation.BugCheckValue(sig, nameof(sig));
        AddOne(CreateInfo(sym.Path, sym, sig, hidden, deprecated, pathAlt));
    }

    /// <summary>
    /// Adds a symbol with its declared path and given signature.
    /// This bug checks if the path has already been set by this registry, but not when it overrides
    /// one inherited from a parent.
    /// </summary>
    protected void AddOne(RexlOper sym, Immutable.Array<Signature> sigs,
        bool hidden = false, bool deprecated = false, NPath pathAlt = default)
    {
        Validation.BugCheckValue(sym, nameof(sym));
        Validation.BugCheckParam(!sigs.IsDefault, nameof(sigs));
        AddOne(CreateInfo(sym.Path, sym, sigs, hidden, deprecated, pathAlt));
    }

    /// <summary>
    /// Adds a symbol with its declared path.
    /// This bug checks if the path has already been set by this registry, but not when it overrides
    /// one inherited from a parent.
    /// </summary>
    protected void AddOne(RexlOper sym,
        bool hidden = false, bool deprecated = false, NPath pathAlt = default)
    {
        Validation.BugCheckValue(sym, nameof(sym));
        AddOne(CreateInfo(sym.Path, sym, hidden, deprecated, pathAlt));
    }

    /// <summary>
    /// Adds a symbol with the given name in the symbol's declared namespace.
    /// This bug checks if the path has already been set by this registry, but not when it overrides
    /// one inherited from a parent.
    /// </summary>
    protected void AddOne(RexlOper sym, string alias,
        bool hidden = false, bool deprecated = false, string nameAlt = null, NPath pathAlt = default)
    {
        Validation.BugCheckValue(sym, nameof(sym));
        Validation.BugCheckParam(DName.TryWrap(alias, out var dn), nameof(alias));
        Validation.BugCheckParam(TryGetItem(sym.Path, out var info), nameof(sym));

        AddOne(CreateInfo(sym.Namespace.Append(dn), sym, info.GetSignatures(), hidden,
            deprecated, string.IsNullOrEmpty(nameAlt) ? pathAlt : sym.Namespace.Append(new DName(nameAlt))));
    }

    /// <summary>
    /// Add a deprecated symbol name for the given (non-deprecated) symbol.
    /// </summary>
    protected void AddOneDep(RexlOper sym, string name)
    {
        AddOne(sym, name, deprecated: true, pathAlt: sym.Path);
    }

    /// <summary>
    /// Adds a symbol with the given path, getting the signatures from the declared path.
    /// This bug checks if the path has already been set by this registry, but not when it overrides
    /// one inherited from a parent.
    /// </summary>
    protected void AddOne(RexlOper sym, NPath alias, bool hidden = false, bool deprecated = false, NPath pathAlt = default)
    {
        Validation.BugCheckValue(sym, nameof(sym));
        Validation.BugCheckParam(alias.NameCount > 0, nameof(alias));
        Validation.BugCheckParam(TryGetItem(sym.Path, out var info), nameof(sym));

        AddOne(CreateInfo(alias, sym, info.GetSignatures(), hidden, deprecated, pathAlt));
    }

    /// <summary>
    /// Add a deprecated symbol name for the given (non-deprecated) symbol.
    /// </summary>
    protected void AddOneDep(RexlOper sym, NPath name)
    {
        AddOne(sym, name, deprecated: true, pathAlt: sym.Path);
    }

    /// <summary>
    /// Adds a symbol with its declared name in the given namespace.
    /// This bug checks if the path has already been set by this registry, but not when it overrides
    /// one inherited from a parent.
    /// </summary>
    protected void AddOneToNs(RexlOper sym, string ns)
    {
        Validation.AssertValueOrNull(sym);
        Validation.BugCheckParam(DName.TryWrap(ns, out var dn), nameof(ns));
        AddOne(CreateInfo(NPath.Root.Append(dn).Append(sym.Name), sym));
    }

    /// <summary>
    /// Looks up an operation entry by path. Returns null if there isn't one.
    /// </summary>
    public OperInfo GetInfo(NPath path)
    {
        var info = GetItem(path);
        if (info is null)
            return null;

        Validation.Assert(info.Path == path);

        // If the entry is just meant to hide things, return null.
        if (info.Oper == null)
            return null;
        return info;
    }

    /// <summary>
    /// Looks up a symbol by path. Returns null if there isn't one.
    /// </summary>
    public RexlOper GetOper(NPath path)
    {
        if (!TryGetItem(path, out var info))
            return null;

        Validation.Assert(info.Path == path);
        return info.Oper;
    }

    /// <summary>
    /// Yields the symbol bindings in the registry. Note that a symbol object may be returned more than once,
    /// if it is bound to multiple paths.
    /// </summary>
    public IEnumerable<OperInfo> GetInfos(bool includeHidden = false, bool includeDeprecated = false, Func<OperInfo, bool> fnFilter = null)
    {
        foreach (var (path, info) in GetItems())
        {
            Validation.Assert(info.Path == path);
            if (info.Oper != null &&
                (!info.Hidden || includeHidden) &&
                (!info.Deprecated || includeDeprecated) &&
                (fnFilter == null || fnFilter(info)))
            {
                yield return info;
            }
        }
    }
}

/// <summary>
/// An operation registry that simply aggregates its parents.
/// </summary>
public sealed class AggregateOperationRegistry : OperationRegistry
{
    public AggregateOperationRegistry(params OperationRegistry[] parents)
        : base(parents)
    {
    }

    public AggregateOperationRegistry(IEnumerable<OperationRegistry> parents)
        : base(parents)
    {
    }
}
