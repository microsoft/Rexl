// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Bind;

/// <summary>
/// This <see cref="BindHost"/> keeps track of name lookups (globals and namespaces) and
/// operation (function/procedure) lookups. Its <see cref="GetInfo(BoundNode)"/> method wraps that
/// information in a <see cref="ReferenceBindInfo"/> instance, or returns <c>null</c> if there were
/// no lookups.
/// </summary>
public abstract class ReferenceBindHost : FixedNamespaceBindHost
{
    /// <summary>
    /// The result of a name lookup (not func/proc).
    /// </summary>
    protected struct PathResult
    {
        public readonly NPath Resolved;
        public readonly DType Type;
        public readonly bool Found;
        public readonly bool Namespace;
        public readonly bool Stream;
        public readonly List<RexlNode> Nodes;

        public PathResult(NPath resolved, DType type, bool stream)
        {
            Validation.Assert(!resolved.IsRoot | !type.IsValid);
            Validation.Assert(!stream | type.IsSequence);

            Resolved = resolved;
            Type = type;
            Found = !resolved.IsRoot;
            Stream = stream;
            Namespace = !resolved.IsRoot && !type.IsValid;
            Nodes = new List<RexlNode>();
        }
    }

    /// <summary>
    /// The full paths/names tested for while binding, mapping each to whether it was found, its type (if a global)
    /// and list of referencing parse nodes. Note that there may be entries here that are not directly referenced by the
    /// bound tree, because of constant folding, as well as namespace prefixes. If any symbols are introduced that
    /// hide such full names, the binding will change. Also, for globals that are reduced by constant folding, the types
    /// will typically influence the binding, so such globals should still be considered "referenced" for dependency
    /// analysis. For example, if "G" has type {A:i4} and the script is "0 * G.A", the binder will likely simplify
    /// to just the constant 0 of type i4, so the bound tree won't directly reference G.
    /// Note: This is not captured in a check-point.
    /// </summary>
    protected Dictionary<NPath, PathResult> _pathsTested;

    /// <summary>
    /// The operation path/arity/flags combinations that were tested for and the result.
    /// </summary>
    protected NameArityToOperOpt _operPathsTested;

    protected ReferenceBindHost(NPath nsCur, NPath nsRoot)
        : base(nsCur, nsRoot)
    {
        _operPathsTested = NameArityToOperOpt.Empty;
    }

    public override BindHostInfo GetInfo(BoundNode bnd)
    {
        var info = Info.Create(this, bnd);
        _pathsTested = null;
        _operPathsTested = NameArityToOperOpt.Empty;
        return info;
    }

    /// <summary>
    /// This is the default bind info result wrapper.
    /// </summary>
    private sealed class Info : ReferenceBindInfo
    {
        public static Info Create(ReferenceBindHost host, BoundNode bnd)
        {
            Dictionary<NPath, PathInfo> paths = null;

            if (Util.Size(host._pathsTested) > 0)
            {
                paths = new Dictionary<NPath, PathInfo>(host._pathsTested.Count);
                foreach (var kvp in host._pathsTested)
                {
                    // De-dup and sort the nodes.
                    var nodes = kvp.Value.Nodes.Distinct().OrderBy(n => n.Token.Range.Min).ToImmutableArray();
                    paths.Add(kvp.Key, new PathInfo(kvp.Value.Type, kvp.Value.Found, kvp.Value.Stream, nodes));
                }
            }

            if (paths == null && host._operPathsTested.Count == 0)
                return null;

            return new Info(bnd.IsProcCall, paths, host._operPathsTested);
        }

        private Info(bool isProc, ReadOnly.Dictionary<NPath, PathInfo> pathsTested, NameArityToOperOpt operPathsTested)
            : base(isProc, pathsTested, operPathsTested)
        {
        }
    }

    public sealed override bool TryFindNamespaceItem(ExprNode ctx, NPath ns, DName name, out NPath path, out DType type, out bool isStream)
    {
        Validation.CheckValueOrNull(ctx);

        var key = ns.Append(name);
        if (!Util.TryGetValue(_pathsTested, key, out var res))
        {
            if (TryFindNamespaceItemCore(ns, name, out var pathTmp, out var typeTmp, out var isStreamTmp))
            {
                Validation.Assert(!pathTmp.IsRoot);
                if (pathTmp == key)
                    pathTmp = key;
                res = new PathResult(pathTmp, typeTmp, isStreamTmp);
            }
            else
                res = new PathResult(default, default, false);

            Util.Add(ref _pathsTested, key, res);
        }
        Validation.Assert(res.Nodes != null);
        if (ctx != null)
            res.Nodes.Add(ctx);

        path = res.Resolved;
        type = res.Type;
        isStream = res.Stream;
        return res.Found;
    }

    protected abstract bool TryFindNamespaceItemCore(NPath ns, DName name, out NPath path, out DType type, out bool isStream);

    public sealed override bool TryGetOperInfoOne(NPath name, bool user, bool fuzzy, int arity, out OperInfo info)
    {
        var flags = NameArityToOperOpt.ExtraFlags.None;
        if (user)
            flags |= NameArityToOperOpt.ExtraFlags.User;
        if (fuzzy)
            flags |= NameArityToOperOpt.ExtraFlags.Fuzzy;

        var key = (name, Util.MakeULong((int)flags, arity));
        if (_operPathsTested.TryGetValue(key, out var oi))
            return (info = oi as OperInfo) != null;
        bool res = TryGetOperInfoCore(name, user, fuzzy, arity, out info);
        Validation.Assert(res == (info != null));
        _operPathsTested = _operPathsTested.SetItem(key, info);
        return res;
    }

    protected abstract bool TryGetOperInfoCore(NPath name, bool user, bool fuzzy, int arity, out OperInfo? info);
}

/// <summary>
/// The <see cref="ReferenceBindHost"/> wraps its binding information in an instance of this class.
/// It includes information about name lookups and func/proc lookups.
/// </summary>
public abstract class ReferenceBindInfo : BindHostInfo
{
    /// <summary>
    /// The result of looking up a path.
    /// </summary>
    protected struct PathInfo
    {
        public readonly DType Type;
        public readonly bool Found;
        public readonly bool Stream;
        public readonly bool Namespace;
        public readonly Immutable.Array<RexlNode> Nodes;

        public PathInfo(DType type, bool found, bool stream, Immutable.Array<RexlNode> nodes)
        {
            Validation.Assert(found | !type.IsValid);
            Validation.Assert(!stream | type.IsSequence);

            // Note this can be empty if only given null ctx nodes when looking up the corresponding path.
            Validation.Assert(!nodes.IsDefault);

            Type = type;
            Found = found;
            Stream = stream;
            Namespace = found && !type.IsValid;
            Nodes = nodes;
        }
    }

    /// <summary>
    /// The full paths/names tested for while binding, mapping each to whether it was found, its type (if a global) and
    /// list of referencing parse nodes in token order. Note that there may be entries here that are not directly referenced
    /// by the bound tree, because of reduction, as well as namespace prefixes. If any symbols are introduced that
    /// hide such full names, the binding will change. Also, for globals that are eliminated by reduction, the types
    /// will typically influence the binding, so such globals should still be considered "referenced" for dependency
    /// analysis. For example, if "G" has type {A:i4} and the script is "0 * G.A", the binder will likely simplify
    /// to just the constant 0 of type i4, so the bound tree won't directly reference G.
    /// </summary>
    protected readonly ReadOnly.Dictionary<NPath, PathInfo> _pathsTested;

    /// <summary>
    /// The full paths/names+arity of functions tested for while binding, mapping each to the resulting function info,
    /// or null if not found. Note that there may be entries here that are not directly referenced by the bound tree,
    /// because of reduction, as well as namespace prefixes. If any functions are introduced that hide such full names,
    /// the binding will change. Also, for calls that are eliminated by reduction, the types will typically influence
    /// the binding, so such functions should still be considered "referenced" for dependency analysis.
    /// </summary>
    public NameArityToOperOpt FuncPathsTested { get; }

    protected readonly bool _isProc;

    protected ReferenceBindInfo(bool isProc, ReadOnly.Dictionary<NPath, PathInfo> pathsTested, NameArityToOperOpt operPathsTested)
    {
        Validation.AssertValue(operPathsTested);

        _isProc = isProc;
        _pathsTested = pathsTested;
        FuncPathsTested = operPathsTested;
    }

    /// <summary>
    /// Get the parse nodes that bound to the given full name.
    /// </summary>
    public Immutable.Array<RexlNode> GetParseNodesForName(NPath name)
    {
        if (_pathsTested.TryGetValue(name, out var pair))
            return pair.Nodes;
        return Immutable.Array<RexlNode>.Empty;
    }

    /// <summary>
    /// Returns the sequence of referenced globals together with their parse nodes, in no particular order.
    /// </summary>
    public IEnumerable<(NPath, Immutable.Array<RexlNode>)> GetReferencedGlobals()
    {
        foreach (var kvp in _pathsTested)
        {
            if (!kvp.Value.Type.IsValid)
                continue;

            Validation.Assert(kvp.Value.Found);
            Validation.Assert(!kvp.Value.Nodes.IsDefaultOrEmpty);
            yield return (kvp.Key, kvp.Value.Nodes);
        }
    }

    /// <summary>
    /// Returns the sequence of full names (together with the binding information) tested for while binding,
    /// in no particular order.
    /// </summary>
    public IEnumerable<GlobalPathInfo> GetTestedGlobalPaths()
    {
        foreach (var kvp in _pathsTested)
        {
            yield return new GlobalPathInfo(kvp.Key, kvp.Value.Type, kvp.Value.Found,
                kvp.Value.Stream & _isProc, kvp.Value.Stream & !_isProc, kvp.Value.Nodes);
        }
    }

    /// <summary>
    /// Returns whether there were any unsuccessful name lookups during binding. Note that this doesn't necessary
    /// mean there were errors. For example, if the formula was bound in namespace N and there is a global named
    /// X but there is not an N.X, then a use of "X" will register as a miss for "N.X".
    /// </summary>
    public bool HasNameMisses()
    {
        foreach (var kvp in _pathsTested)
        {
            if (!kvp.Value.Found)
                return true;
        }
        return false;
    }
}

/// <summary>
/// Information for a global (full) path tested while binding.
/// </summary>
public struct GlobalPathInfo
{
    /// <summary>
    /// The path tested.
    /// </summary>
    public NPath Path { get; }

    /// <summary>
    /// The type of the found global. When no global was found, this is <c>default</c>,
    /// which is an invalid <see cref="DType"/>. This can be tested with <c>Type.IsValid</c>.
    /// </summary>
    public DType Type { get; }

    /// <summary>
    /// Whether the path was found.
    /// </summary>
    public bool Found { get; }

    /// <summary>
    /// Whether the path was found to be a namespace.
    /// </summary>
    public bool Namespace { get; }

    /// <summary>
    /// Whether the path was found to be a streaming sequence global. This will only be true when
    /// the top level is a call to a procedure.
    /// </summary>
    public bool Stream { get; }

    /// <summary>
    /// Whether the path was found to be a streaming sequence global that should be snapshotted. This
    /// will only be true when the expression is "pure".
    /// </summary>
    public bool Snapshot { get; }

    /// <summary>
    /// The nodes where this path was tested.
    /// </summary>
    public Immutable.Array<RexlNode> Nodes;

    public GlobalPathInfo(NPath path, DType type, bool found, bool stream, bool snapshot, Immutable.Array<RexlNode> nodes)
    {
        Validation.BugCheckParam(!path.IsRoot, nameof(path));
        Validation.BugCheckParam(found | !type.IsValid, nameof(type));
        Validation.BugCheckParam(!stream | type.IsSequence, nameof(stream));
        Validation.BugCheckParam(!snapshot | type.IsSequence, nameof(snapshot));
        Validation.BugCheckParam(!stream | !snapshot, nameof(snapshot));
        Validation.BugCheckParam(!nodes.IsDefaultOrEmpty, nameof(nodes));

        Path = path;
        Type = type;
        Found = found;
        Namespace = found && !type.IsValid;
        Stream = stream;
        Snapshot = snapshot;
        Nodes = nodes;
    }
}
