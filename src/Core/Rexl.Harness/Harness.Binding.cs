// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.Rexl;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Harness;

using UserOperTuple = Immutable.Array<UserOper>;

// This partial is for binding related functionality. This includes tracking namespaces, the operation
// registry, the udo registry, and the bind host.
partial class HarnessBase
{
    protected readonly OperationRegistry _opers;

    // The standard namespaces (not modules or tasks) currently in use and the names within them.
    // The hash set contains global, task, and module names, but not function names.
    private readonly Dictionary<NPath, HashSet<DName>> _namespaces;

    // Map from name/path to udf/udp overloads, sorted by arity.
    private readonly Dictionary<NPath, UserOperTuple> _nameToUserOpers;

    // Case insensitive map from udf/udp path to actual path, created lazily.
    private Dictionary<NPath, NPath> _fuzzyUserOperPathToPath;

    // The binding host mostly delegates to the harness.
    private readonly BindHostImpl _host;

    /// <summary>
    /// Binds an expression. Calls <see cref="HandleBoundFormula(BoundFormula)"/> for diagnostic reporting.
    /// Returns true and sets <paramref name="bnd"/> if there are no errors.
    /// </summary>
    protected bool TryBind(RexlFormula fma, BindOptions options, out BoundNode bnd)
    {
        // Binding.
        if (!_config.Optimize)
            options |= BindOptions.DontOptimize;
        var bfma = BoundFormula.Create(fma, _host, options);
        Validation.Assert(bfma.Formula == fma);
        HandleBoundFormula(bfma);

        if (bfma.IsGood)
        {
            bnd = bfma.BoundTree;
            Validation.Assert(!bnd.HasErrors);
            return true;
        }

        bnd = null;
        return false;
    }

    /// <summary>
    /// Returns whether there is a standard namespace with the given name (not module or task).
    /// </summary>
    protected bool HasStandardNamespace(NPath name) => _namespaces.ContainsKey(name);

    /// <summary>
    /// Whether the given <paramref name="name"/> is a generalized namespace (standard namespace,
    /// module, or task).
    /// </summary>
    protected bool IsGeneralNamespace(NPath name)
    {
        return HasStandardNamespace(name) || HasTask(name);
    }

    /// <summary>
    /// Ensure that the namespace map includes the function and procedure namespaces.
    /// </summary>
    protected void InitNamespaces()
    {
        foreach (var info in _opers.GetInfos())
        {
            if (info.Path.NameCount > 1)
                UpdateNamespaces(info.Path);
        }
    }

    /// <summary>
    /// Update the namespaces dictionary given the <paramref name="name"/> of a new item.
    /// </summary>
    protected void UpdateNamespaces(NPath name)
    {
        for (var parent = name; ;)
        {
            parent = parent.PopOne(out var child);
            if (!_namespaces.TryGetValue(parent, out var names))
                _namespaces.Add(parent, names = new HashSet<DName>());
            if (!names.Add(child))
                break;
            if (parent.IsRoot)
                break;
        }
    }

    /// <summary>
    /// Record the udf/udp implementation in the udf/udp map, according to the given
    /// <paramref name="udo"/>. The out parameter <paramref name="prev"/> is set to
    /// the previous implementation for that name and arity. The out parameter
    /// <paramref name="cur"/> is set to the new implementation for the name and arity.
    /// Note that <paramref name="cur"/> will be <c>null</c> when the body of
    /// <paramref name="udo"/> is just an underscore. This indicates that the udo
    /// should be "removed".
    /// </summary>
    protected void SetUserOper(UserOper udo, out UserOper prev, out UserOper cur)
    {
        Validation.Assert(udo.IsValid);

        var name = udo.Oper.Path;
        if (!_nameToUserOpers.TryGetValue(name, out var udos))
        {
            udos = UserOperTuple.Empty;
            UpdateNamespaces(name);
        }

        int arity = udo.Arity;
        bool have = TryFindUserOperArity(udos, arity, out int index);
        prev = have ? udos[index] : default;

        // A user func with body being just box (_) means remove it.
        cur = udo.AsFunc?.Formula.ParseTree is BoxNode ? default : udo;

        if (cur.IsValid)
        {
            udos = have ? udos.SetItem(index, cur) : udos.Insert(index, cur);
            _nameToUserOpers[name] = udos;
            if (_fuzzyUserOperPathToPath != null)
                _fuzzyUserOperPathToPath[cur.Oper.Path] = cur.Oper.Path;
        }
        else if (have)
        {
            if (udos.Length > 1)
                _nameToUserOpers[name] = udos.RemoveAt(index);
            else
            {
                _nameToUserOpers.Remove(name);
                _fuzzyUserOperPathToPath = null;
            }
        }
    }

    /// <summary>
    /// Static method to find a udo overload from the given <paramref name="overloads"/> given
    /// the <paramref name="arity"/>. The overloads are assumed to be sorted by arity. The
    /// <paramref name="index"/> is set to where the overload belongs even when there is no
    /// match.
    /// </summary>
    protected static bool TryFindUserOperArity(UserOperTuple overloads, int arity, out int index)
    {
        Validation.Assert(!overloads.IsDefault);

        int min = 0;
        int lim = overloads.Length;
        while (min < lim)
        {
            int mid = (int)((uint)(min + lim) >> 1);
            if (arity <= overloads[mid].Arity)
                lim = mid;
            else
                min = mid + 1;
        }
        Validation.Assert(min == lim);
        Validation.AssertIndexInclusive(min, overloads.Length);
        Validation.Assert(min == overloads.Length || arity <= overloads[min].Arity);
        Validation.Assert(min == 0 || arity > overloads[min - 1].Arity);

        index = min;
        return min < overloads.Length && overloads[min].Arity == arity;
    }

    private Dictionary<NPath, NPath> EnsureUserOperFuzzyMap()
    {
        var map = _fuzzyUserOperPathToPath;
        if (map == null)
        {
            map = new Dictionary<NPath, NPath>(_nameToUserOpers.Count, CiNPathComparer.Instance);
            foreach (var path in _nameToUserOpers.Keys)
                map[path] = path;
            _fuzzyUserOperPathToPath = map;
        }
        return map;
    }

    private bool TryGetOperInfoOne(NPath name, bool user, int arity, out OperInfo info)
    {
        Validation.Assert(!name.IsRoot);

        if (!user)
        {
            info = _opers.GetInfo(name);
            Validation.Assert(info is null || info.Oper is not null);
            return info != null;
        }

        // REVIEW: Should UDFs be exposed in modules?

        // Don't find UDFs that are outside NsRoot.
        info = null;
        if (!name.Parent.StartsWith(NsRoot))
            return false;

        if (!_nameToUserOpers.TryGetValue(name, out var udos))
            return false;
        Validation.Assert(!udos.IsDefaultOrEmpty);

        // Note that the first two cases could be combined, but we keep them split for ease of debugging.
        UserOper udo;
        if (TryFindUserOperArity(udos, arity, out int index))
            udo = udos[index];
        else if (index < udos.Length)
            udo = udos[index];
        else
            udo = udos[index - 1];

        info = new OperInfo(udo.Oper.Path, udo.Oper);
        return true;
    }

    /// <summary>
    /// Callback from bind host. Looks for a "global value" with the given <paramref name="name"/> in a
    /// namespace, module, or task with name <paramref name="ns"/>. If found, sets <paramref name="type"/>
    /// and <paramref name="isStream"/> accordingly and returns <c>true</c>.
    /// </summary>
    protected bool TryGetGlobalInfo(NPath ns, DName name, out DType type, out bool isStream)
    {
        isStream = false;

        if (TryGetStdGlobalType(ns, name, out type))
        {
            Validation.Assert(type.IsValid);
            return true;
        }

        if (TryGetTaskItemType(ns, name, out type, out isStream))
        {
            Validation.Assert(type.IsValid);
            return true;
        }

        type = default;
        return false;
    }

    /// <summary>
    /// Callback from bind host. Looks for a "global value" with a fuzzy match of the given
    /// <paramref name="name"/> in a namespace, module, or task with name <paramref name="ns"/>. If there
    /// is one, sets <paramref name="nameGuess"/>, <paramref name="type"/> and <paramref name="isStream"/>
    /// accordingly and returns <c>true</c>.
    /// </summary>
    protected bool TryGetGlobalInfoFuzzy(NPath ns, DName name,
        out DName nameGuess, out DType type, out bool isStream)
    {
        isStream = false;

        if (TryGetStdGlobalTypeFuzzy(ns, name, out nameGuess, out type))
        {
            Validation.Assert(type.IsValid);
            return true;
        }

        if (TryGetTaskItemTypeFuzzy(ns, name, out nameGuess, out type, out isStream))
        {
            Validation.Assert(type.IsValid);
            return true;
        }

        nameGuess = default;
        type = default;
        return false;
    }

    /// <summary>
    /// Gets the type of an item in a standard namespace, if there is one.
    /// </summary>
    protected bool TryGetStdGlobalType(NPath ns, DName name, out DType type)
    {
        if (_namespaces.TryGetValue(ns, out var names) && names.Contains(name) &&
            TryGetGlobalType(ns.Append(name), out type))
        {
            Validation.Assert(type.IsValid);
            return true;
        }

        type = default;
        return false;
    }

    /// <summary>
    /// Looks for an an item in a standard namespace whose name is a fuzzy match for <paramref name="name"/>.
    /// If found, sets <paramref name="nameGuess"/> and <paramref name="type"/> and returns <c>true</c>.
    /// </summary>
    protected bool TryGetStdGlobalTypeFuzzy(NPath ns, DName name, out DName nameGuess, out DType type)
    {
        if (_namespaces.TryGetValue(ns, out var names))
        {
            foreach (var n in names)
            {
                if (IsFuzzyMatch(n, name) && TryGetGlobalType(ns.Append(n), out type))
                {
                    Validation.Assert(type.IsValid);
                    nameGuess = n;
                    return true;
                }
            }
        }

        nameGuess = default;
        type = default;
        return false;
    }

    /// <summary>
    /// Looks for a namespace with name <paramref name="ns"/> and a sub-namespace in it whose name is a fuzzy
    /// match for <paramref name="name"/>. If found, sets <paramref name="nameGuess"/> accordingly.
    /// </summary>
    protected bool TryGetNamespaceFuzzy(NPath ns, DName name, out DName nameGuess)
    {
        if (_namespaces.TryGetValue(ns, out var names))
        {
            foreach (var n in names)
            {
                if (IsFuzzyMatch(n, name))
                {
                    var path = ns.Append(n);
                    Validation.Assert(!HasGlobal(path));
                    if (IsGeneralNamespace(path))
                    {
                        nameGuess = n;
                        return true;
                    }
                }
            }
        }

        nameGuess = default;
        return false;
    }

    /// <summary>
    /// Tests two names to see if they are a "fuzzy" match.
    /// </summary>
    protected virtual bool IsFuzzyMatch(DName a, DName b)
    {
        return StrComparer.EqCi(a, b);
    }

    /// <summary>
    /// Base class for binding expressions and modules.
    /// </summary>
    private abstract class BindHostImplBase : BindHostStd
    {
        protected readonly HarnessBase _parent;

        // Does not include UDFs, since they can come and go.
        // REVIEW: Perhaps this should use a different scheme?
        protected readonly Dictionary<NPath, OperInfo> _fuzzyPathToInfo;

        protected sealed override NPath NsRoot => _parent.NsRoot;
        protected sealed override NPath NsCur => _parent.NsCur;
        protected sealed override NPath NsRel => _parent.NsRel;

        public BindHostImplBase(HarnessBase parent)
            : base()
        {
            Validation.AssertValue(parent);
            _parent = parent;

            _fuzzyPathToInfo = new Dictionary<NPath, OperInfo>(CiNPathComparer.Instance);
            foreach (var info in _parent._opers.GetInfos(includeHidden: true, includeDeprecated: true))
                _fuzzyPathToInfo[info.Path] = info;
        }

        public override bool TryGetOperInfoOne(NPath name, bool user, bool fuzzy, int arity, out OperInfo info)
        {
            info = null;
            if (name.IsRoot)
                return false;

            if (!fuzzy)
                return _parent.TryGetOperInfoOne(name, user, arity, out info);

            // REVIEW: Use a different mechanism for fuzzy match?
            if (!user)
                return _fuzzyPathToInfo.TryGetValue(name, out info);

            // REVIEW: Should UDOs be exposed in modules?

            var mapUdo = _parent.EnsureUserOperFuzzyMap();
            if (!mapUdo.TryGetValue(name, out var path))
                return false;

            // Don't find UDOs that are outside NsRoot.
            if (!path.Parent.StartsWith(NsRoot))
                return false;

            if (!_parent._nameToUserOpers.TryGetValue(path, out var udos))
                return false;
            Validation.Assert(!udos.IsDefaultOrEmpty);

            // Note that the first two cases could be combined, but we keep them split for ease of debugging.
            UserOper udo;
            if (TryFindUserOperArity(udos, arity, out int index))
                udo = udos[index];
            else if (index < udos.Length)
                udo = udos[index];
            else
                udo = udos[index - 1];

            info = new OperInfo(udo.Oper.Path, udo.Oper);
            return true;
        }
    }

    /// <summary>
    /// The bind host for expression binding.
    /// </summary>
    private sealed class BindHostImpl : BindHostImplBase
    {
        protected override IEnumerable<NPath> Withs => _parent._interp.Withs;

        public BindHostImpl(HarnessBase parent)
            : base(parent)
        {
        }

        public override bool TryGetThisType(out DType type)
        {
            return _parent.TryGetThisType(out type);
        }

        public override bool TryFindNamespaceItem(ExprNode ctx, NPath ns, DName name,
            out NPath path, out DType type, out bool isStream)
        {
            Validation.Assert(name.IsValid);

            // The namespace should always start with NsRoot.
            if (!ns.StartsWith(NsRoot))
            {
                Validation.Assert(false);
                path = default;
                type = default;
                isStream = false;
                return false;
            }

            if (_parent.TryGetGlobalInfo(ns, name, out type, out isStream))
            {
                path = ns.Append(name);
                return true;
            }

            type = default;
            isStream = false;
            if (IsNamespace(path = ns.Append(name)))
                return true;

            path = default;
            return false;
        }

        public override bool TryFindNamespaceItemFuzzy(ExprNode ctx, NPath ns, DName name,
            out DName nameGuess, out DType type, out bool isStream)
        {
            Validation.Assert(name.IsValid);

            // The namespace should always start with NsRoot.
            if (!ns.StartsWith(NsRoot))
            {
                Validation.Assert(false);
                nameGuess = default;
                type = default;
                isStream = false;
                return false;
            }

            if (_parent.TryGetGlobalInfoFuzzy(ns, name, out nameGuess, out type, out isStream))
                return true;

            type = default;
            isStream = false;
            if (_parent.TryGetNamespaceFuzzy(ns, name, out nameGuess))
                return true;

            nameGuess = default;
            return false;
        }

        public override bool IsNamespace(NPath name)
        {
            return _parent.IsGeneralNamespace(name);
        }

        public override bool TryGetMetaProp(NPath ns, DName name, out BoundNode bnd)
        {
            // This shouldn't be called when binding modules, but just to make that clear....
            // Also, the namespace should always start with NsRoot.
            if (!ns.StartsWith(NsRoot))
            {
                Validation.Assert(false);
                bnd = default;
                return false;
            }

            return _parent.TryGetMetaProp(ns, name, out bnd);
        }
    }
}
