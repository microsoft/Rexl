// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Bind;

/// <summary>
/// This is information that the <see cref="BindHost"/> decides to attach to a <see cref="BoundFormula"/>.
/// </summary>
public abstract class BindHostInfo
{
    protected BindHostInfo()
    {
    }
}

/// <summary>
/// Provided by the client of the binder to do lookup of various things by name. Supports approximate
/// matching via the "Fuzzy" variants. The "Fuzzy" variants will only be called after the normal variants
/// have failed. If a "Fuzzy" variant is used, an error is still recorded.
/// </summary>
public abstract class BindHost
{
    /// <summary>
    /// Produces the <see cref="BindHostInfo"/> that should be attached to the <see cref="BoundFormula"/>
    /// being produced. That is, after binding with this host, this method is called and the result is
    /// bundled with the bound node into a bound formula. The <see cref="ReferenceBindHost"/> bundles
    /// information about name and function lookups performed. This can be used for dependency analysis.
    /// </summary>
    public abstract BindHostInfo GetInfo(BoundNode bnd);

    /// <summary>
    /// Get the type for <c>this</c>. Returns <c>false</c> to indicate that <c>this</c> is illegal.
    /// </summary>
    public abstract bool TryGetThisType(out DType type);

    /// <summary>
    /// Return true if <paramref name="a"/> and <paramref name="b"/> should be considered a "fuzzy" match
    /// (for error recovery). This is used, for example, when matching to a scope name.
    /// </summary>
    public abstract bool IsFuzzyMatch(string a, string b);

    /// <summary>
    /// Try to look up a name and produce information about what it references. If the name is
    /// not found, returns false. Otherwise, sets <paramref name="path"/> to the actual full path
    /// of the item, sets <paramref name="type"/> to the type of the referenced global (if there
    /// is one) or to <c>default</c> if the referenced item is a namespace, and sets
    /// <paramref name="isStream"/> to true if the referenced global is a "streaming" result
    /// of a task.
    /// </summary>
    public abstract bool TryFindName(ExprNode ctx, DName name, bool isRooted,
        out NPath path, out DType type, out bool isStream);

    /// <summary>
    /// Try to look up a name allowing a fuzzy match and produce information about what it references.
    /// If the name is not found, returns false. Otherwise, sets <paramref name="nameGuess"/> to the
    /// actual name, sets <paramref name="path"/> to the actual full path of the item, sets
    /// <paramref name="type"/> to the type of the referenced global (if there is one) or to
    /// <c>default</c> if the referenced item is a namespace, and sets <paramref name="isStream"/>
    /// to true if the referenced global is a "streaming" result of a task.
    /// </summary>
    public abstract bool TryFindNameFuzzy(ExprNode ctx, DName name, bool isRooted,
        out DName nameGuess, out NPath path, out DType type, out bool isStream);

    /// <summary>
    /// Looks for an item with the given <paramref name="name"/> in the given namespace. If there is a
    /// global or namespaces, sets the <paramref name="path"/> accordingly. For a namespace, the
    /// <paramref name="type"/> is set to <c>default</c> (invalid) and <paramref name="isStream"/>
    /// is set to true if the referenced global is a "streaming" result of a task.
    /// </summary>
    public abstract bool TryFindNamespaceItem(ExprNode? ctx, NPath ns, DName name,
        out NPath path, out DType type, out bool isStream);

    /// <summary>
    /// Similar to <see cref="TryFindNamespaceItem(ExprNode, NPath, DName, out NPath, out DType, out bool)"/>
    /// except the <paramref name="name"/> is allowed to be a "fuzzy" match. Sets <paramref name="nameGuess"/>
    /// to the matched name.
    /// </summary>
    public abstract bool TryFindNamespaceItemFuzzy(ExprNode? ctx, NPath ns, DName name,
        out DName nameGuess, out DType type, out bool isStream);

    /// <summary>
    /// Determine if the given name is a namespace.
    /// </summary>
    public abstract bool IsNamespace(NPath name);

    /// <summary>
    /// Determine if there is an operation with the given name and provide its <see cref="OperInfo"/>.
    /// This searches namespaces as appropriate. If there is no operation that supports the given
    /// <paramref name="arity"/> a best guess is provided. When <paramref name="fuzzy"/> is <c>true</c>,
    /// this allows non-exact name matches.
    /// </summary>
    public abstract bool TryGetOperInfo(NPath name, bool isRooted, NPath nsType, bool fuzzy, int arity, out OperInfo info);

    /// <summary>
    /// Look for an operation with the given name. This does not do any namespace search. That is it looks
    /// only for a match with the given <paramref name="name"/>. The <paramref name="user"/> parameter
    /// indicates whether the operation should be user defined. The <paramref name="fuzzy"/> parameter
    /// indicates whether name can be a fuzzy (non-exact) match.
    /// </summary>
    public abstract bool TryGetOperInfoOne(NPath name, bool user, bool fuzzy, int arity, out OperInfo info);

    /// <summary>
    /// Look for a "meta property" with the given <paramref name="name"/> associated with the namespace
    /// indicated by <paramref name="path"/>. A "meta property" produces a <see cref="BoundNode"/>.
    /// </summary>
    public abstract bool TryGetMetaProp(NPath path, DName name, out BoundNode bnd);
}

/// <summary>
/// An <c>abstract</c> bind host that provides common implementation suitable for most needs.
///
/// This supports the concept of namespaces. There is a current namespace and root namespace. The former
/// must be contained within (or equal to) the latter. This also supports the concept of "withs", which
/// is an ordered sequence of namespaces that are searched for globals before the normal namespace
/// search. All the "with" namespaces must be contained within (or equal to) the root namespace. If
/// namespace functionality is not desired, set both current and root namespace to <see cref="NPath.Root"/>
/// and return <c>null</c> for the <see cref="Withs"/> property.
/// </summary>
public abstract class BindHostStd : BindHost
{
    /// <summary>
    /// The root namespace. Everything found, except core (non user-defined) functions, must be within this.
    /// </summary>
    protected abstract NPath NsRoot { get; }

    /// <summary>
    /// The current namespace. This must be within <see cref="NsRoot"/>.
    /// </summary>
    protected abstract NPath NsCur { get; }

    /// <summary>
    /// The current namespace relative to <see cref="NsRoot"/>. This is used for core function search.
    /// </summary>
    protected abstract NPath NsRel { get; }

    /// <summary>
    /// The active "with" namespaces. Note that all of these <i>must</i> be within <see cref="NsRoot"/>.
    /// </summary>
    protected abstract IEnumerable<NPath> Withs { get; }

    protected BindHostStd()
    {
    }

    public override BindHostInfo GetInfo(BoundNode bnd)
    {
        return null;
    }

    public override bool TryGetThisType(out DType type)
    {
        type = default;
        return false;
    }

    public override bool IsFuzzyMatch(string a, string b)
    {
        return StrComparer.EqCi(a, b);
    }

    public sealed override bool TryFindName(ExprNode ctx, DName name, bool isRooted,
        out NPath path, out DType type, out bool isStream)
    {
        Validation.AssertValueOrNull(ctx);
        Validation.Assert(name.IsValid);

        var nsRoot = NsRoot;

        // First search "withs". This search does NOT find namespaces, only globals.
        IEnumerable<NPath> withs;
        if (!isRooted && (withs = Withs) != null)
        {
            foreach (var ns in withs)
            {
                Validation.Assert(ns.StartsWith(nsRoot));

                if (TryFindNamespaceItem(ctx, ns, name, out path, out type, out isStream) && type.IsValid)
                    return true;
            }
        }

        for (var ns = !isRooted ? NsCur : nsRoot; ; ns = ns.Parent)
        {
            if (TryFindNamespaceItem(ctx, ns, name, out path, out type, out isStream))
                return true;

            if (ns.NameCount <= nsRoot.NameCount)
            {
                path = default;
                type = default;
                isStream = false;
                return false;
            }
        }
    }

    public sealed override bool TryFindNameFuzzy(ExprNode ctx, DName name, bool isRooted,
        out DName nameGuess, out NPath path, out DType type, out bool isStream)
    {
        Validation.AssertValueOrNull(ctx);
        Validation.Assert(name.IsValid);

        var nsRoot = NsRoot;

        // First search "withs". This search does NOT find namespaces, only globals.
        IEnumerable<NPath> withs;
        if (!isRooted && (withs = Withs) != null)
        {
            foreach (var ns in withs)
            {
                Validation.Assert(ns.StartsWith(nsRoot));

                if (TryFindNamespaceItemFuzzy(ctx, ns, name, out nameGuess, out type, out isStream) && type.IsValid)
                {
                    path = ns.Append(nameGuess);
                    return true;
                }
            }
        }

        for (var ns = !isRooted ? NsCur : nsRoot; ; ns = ns.Parent)
        {
            if (TryFindNamespaceItemFuzzy(ctx, ns, name, out nameGuess, out type, out isStream))
            {
                path = ns.Append(nameGuess);
                return true;
            }

            if (ns.NameCount <= nsRoot.NameCount)
            {
                nameGuess = default;
                path = default;
                type = default;
                isStream = false;
                return false;
            }
        }
    }

    public override bool TryFindNamespaceItemFuzzy(ExprNode ctx, NPath ns, DName name,
        out DName nameGuess, out DType type, out bool isStream)
    {
        nameGuess = default;
        type = default;
        isStream = default;
        return false;
    }

    /// <summary>
    /// Returns true if <paramref name="info0"/> is a worse candidate than <paramref name="info1"/>
    /// for the given <paramref name="arity"/>.
    /// REVIEW: Should this be <c>protected virtual</c>?
    /// </summary>
    private static bool IsWorse(OperInfo info0, OperInfo info1, int arity)
    {
        Validation.AssertValueOrNull(info0);
        Validation.AssertValueOrNull(info1);

        if (info0 == info1)
            return false;
        if (info1 == null)
            return false;
        if (info0 == null)
            return true;

        var op0 = info0.Oper;
        var op1 = info1.Oper;

        if (op0.SupportsArity(arity))
            return false;
        if (op1.SupportsArity(arity))
            return true;

        if (op0.ArityMax < arity)
            return op0.ArityMax < op1.ArityMax;
        Validation.Assert(arity <= op0.ArityMax);

        if (op1.ArityMax < arity)
            return false;
        Validation.Assert(arity <= op1.ArityMax);

        int arityMin = Math.Min(op0.ArityMin, op1.ArityMin);
        if (arity < arityMin)
            arity = arityMin;

        for (; ; )
        {
            if (op0.SupportsArity(arity))
                return false;
            if (op1.SupportsArity(arity))
                return true;

            if (op1.ArityMax < arity)
                return false;
            if (op0.ArityMax < arity)
                return true;

            // Target the next larger arity (supposing that the user under shot the arity).
            arity++;
        }
    }

    /// <summary>
    /// If <paramref name="infoBest"/> is worse than <paramref name="info"/>, sets <paramref name="infoBest"/>
    /// to <paramref name="info"/>.
    private static void UpdateBest(ref OperInfo infoBest, OperInfo info, int arity)
    {
        if (IsWorse(infoBest, info, arity))
            infoBest = info;
    }

    // REVIEW: Should this be unsealed?
    public sealed override bool TryGetOperInfo(NPath name, bool isRooted, NPath nsType, bool fuzzy, int arity, out OperInfo info)
    {
        Validation.Assert(!name.IsRoot);
        Validation.Assert(arity >= 0);

        var nsRoot = NsRoot;

        // Tracks the best match so far (based on arity).
        OperInfo infoBest = null;

        NPath nsCore;
        NPath nsUser;

        if (!isRooted)
        {
            // First try within the type namespace, if given.
            if (!nsType.IsRoot)
            {
                var full = nsType.AppendPath(name);
                if (TryGetOperInfoOne(full, user: false, fuzzy, arity, out var infoCore) &&
                    infoCore.Oper.SupportsArity(arity) &&
                    infoCore.Oper.Path.StartsWith(nsType))
                {
                    info = infoCore;
                    return true;
                }

                if (!nsRoot.IsRoot)
                    full = nsRoot.AppendPath(full);
                if (TryGetOperInfoOne(full, user: true, fuzzy, arity, out var infoUser) &&
                    infoUser.Oper.SupportsArity(arity) &&
                    infoUser.Oper.Path.StartsWith(nsRoot.AppendPath(nsType)))
                {
                    info = infoUser;
                    return true;
                }

                UpdateBest(ref infoBest, infoCore, arity);
                UpdateBest(ref infoBest, infoUser, arity);
            }

            // Now try withs.
            IEnumerable<NPath> withs = Withs;
            if (withs != null)
            {
                foreach (var ns in withs)
                {
                    Validation.Assert(ns.StartsWith(nsRoot));

                    nsCore = nsRoot.IsRoot ? ns : NPath.Root.AppendPartial(ns, nsRoot.NameCount);
                    nsUser = ns;

                    var full = nsCore.AppendPath(name);
                    if (TryGetOperInfoOne(full, user: false, fuzzy, arity, out var infoCore) &&
                        infoCore.Oper.SupportsArity(arity))
                    {
                        info = infoCore;
                        return true;
                    }

                    if (!nsRoot.IsRoot)
                        full = nsUser.AppendPath(name);
                    if (TryGetOperInfoOne(full, user: true, fuzzy, arity, out var infoUser) &&
                        infoUser.Oper.SupportsArity(arity))
                    {
                        info = infoUser;
                        return true;
                    }

                    UpdateBest(ref infoBest, infoCore, arity);
                    UpdateBest(ref infoBest, infoUser, arity);
                }
            }
        }

        // Now go up the namespace hierarchy.
        nsCore = isRooted ? NPath.Root : NsRel;
        nsUser = isRooted ? nsRoot : NsCur;
        for (; ; nsCore = nsCore.Parent, nsUser = nsUser.Parent)
        {
            if (TryGetOperInfoOne(nsCore.AppendPath(name), user: false, fuzzy, arity, out var infoCore) &&
                infoCore.Oper.SupportsArity(arity))
            {
                info = infoCore;
                return true;
            }

            if (TryGetOperInfoOne(nsUser.AppendPath(name), user: true, fuzzy, arity, out var infoUser) &&
                infoUser.Oper.SupportsArity(arity))
            {
                info = infoUser;
                return true;
            }

            UpdateBest(ref infoBest, infoCore, arity);
            UpdateBest(ref infoBest, infoUser, arity);

            if (nsCore.IsRoot)
                break;
        }

        info = infoBest;
        return info != null;
    }

    public override bool TryGetMetaProp(NPath path, DName name, out BoundNode bnd)
    {
        bnd = null;
        return false;
    }
}

/// <summary>
/// This is an <c>abstract</c> <see cref="BindHost"/> with the namespaces fixed (stored as fields).
/// The namespaces are parameters to the constructor.
/// </summary>
public abstract class FixedNamespaceBindHost : BindHostStd
{
    protected sealed override NPath NsRoot { get; }
    protected sealed override NPath NsCur { get; }
    protected sealed override NPath NsRel { get; }
    protected sealed override IEnumerable<NPath> Withs { get; }

    protected FixedNamespaceBindHost(NPath nsCur, NPath nsRoot, IEnumerable<NPath> withs = null)
        : base()
    {
        Validation.BugCheckParam(nsCur.StartsWith(nsRoot), nameof(nsCur));
        Validation.BugCheckValueOrNull(withs);

        NsRoot = nsRoot;
        NsCur = nsCur;
        NsRel = NPath.Root.AppendPartial(nsCur, nsRoot.NameCount);

        // Snapshot the withs to guarantee that they don't change.
        Withs = withs?.ToImmutableArray();
    }
}

/// <summary>
/// This is a minimal <c>abstract</c> <see cref="FixedNamespaceBindHost"/> that knows nothing.
/// Subclasses can override to include some items (globals, namespaces, functions, procedures).
/// </summary>
public abstract class MinBindHost : FixedNamespaceBindHost
{
    protected MinBindHost()
        : base(NPath.Root, NPath.Root)
    {
    }

    protected MinBindHost(NPath nsCur, NPath nsRoot)
        : base(nsCur, nsRoot)
    {
    }

    public override bool IsNamespace(NPath name)
    {
        return name.IsRoot;
    }

    public override bool TryFindNamespaceItem(ExprNode ctx, NPath ns, DName name, out NPath path, out DType type, out bool isStream)
    {
        Validation.AssertValueOrNull(ctx);
        Validation.Assert(name.IsValid);

        path = default;
        type = default;
        isStream = false;
        return false;
    }

    public override bool TryGetOperInfoOne(NPath name, bool user, bool fuzzy, int arity, out OperInfo info)
    {
        Validation.Assert(!name.IsRoot);
        info = null;
        return false;
    }
}
