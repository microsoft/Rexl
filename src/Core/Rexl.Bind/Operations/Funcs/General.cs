// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

/// <summary>
/// Produces a default value of a type derived from the type of the argument.
/// </summary>
public sealed partial class DefaultValueFunc : RexlOper
{
    private enum Kind : byte
    {
        Def,
        Req,
        Opt,
    }

    public static readonly DefaultValueFunc Def = new DefaultValueFunc("Def", Kind.Def, item: false);
    public static readonly DefaultValueFunc DefReq = new DefaultValueFunc("DefReq", Kind.Req, item: false);
    public static readonly DefaultValueFunc DefOpt = new DefaultValueFunc("DefOpt", Kind.Opt, item: false);
    public static readonly DefaultValueFunc DefItem = new DefaultValueFunc("DefItem", Kind.Def, item: true);
    public static readonly DefaultValueFunc DefItemReq = new DefaultValueFunc("DefItemReq", Kind.Req, item: true);
    public static readonly DefaultValueFunc DefItemOpt = new DefaultValueFunc("DefItemOpt", Kind.Opt, item: true);

    private readonly Kind _kind;
    private readonly bool _item;

    private DefaultValueFunc(string name, Kind kind, bool item)
        : base(isFunc: true, new DName(name), 1, 1)
    {
        _kind = kind;
        _item = item;
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        return ArgTraitsSimple.Create(this, eager: false, carg);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var typeArg = info.Args[0].Type;
        var typeRes = typeArg;
        if (_item)
        {
            typeRes = typeArg.ItemTypeOrThis;
            typeArg = typeRes.ToSequence();
        }
        switch (_kind)
        {
        case Kind.Opt:
            typeRes = typeRes.ToOpt();
            break;
        case Kind.Req:
            typeRes = typeRes.ToReq();
            if (typeRes.IsOpt)
            {
                info.PostDiagnostic(RexlDiagnostic.Warning(info.GetParseArg(0),
                    ErrorStrings.WrnTypeNoReq_Type, typeRes));
            }
            break;
        default:
            Validation.Assert(_kind == Kind.Def);
            break;
        }

        return (typeRes, Immutable.Array.Create(typeArg));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Args[0].Type;
        if (_item)
        {
            if (!type.IsSequence)
                return false;
            type = type.ItemTypeOrThis;
        }
        switch (_kind)
        {
        case Kind.Opt:
            type = type.ToOpt();
            break;
        case Kind.Req:
            type = type.ToReq();
            break;
        default:
            Validation.Assert(_kind == Kind.Def);
            break;
        }

        if (call.Type != type)
            return false;

        // This should always reduce, so is never used for code gen.
        full = false;

        return true;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        return BndDefaultNode.Create(call.Type);
    }
}

/// <summary>
/// Converts to the associated optional type.
/// </summary>
public sealed partial class OptFunc : RexlOper
{
    public static readonly OptFunc Instance = new OptFunc();

    private OptFunc()
        : base(isFunc: true, new DName("Opt"), 1, 1)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        return ArgTraitsSimple.Create(this, eager: false, carg);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var typeArg = info.Args[0].Type;
        if (typeArg.IsOpt)
        {
            info.PostDiagnostic(RexlDiagnostic.Warning(info.GetParseArg(0),
                ErrorStrings.WrnTypeAlreadyOpt_Type, typeArg));
        }
        return (typeArg.ToOpt(), Immutable.Array.Create(typeArg));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Args[0].Type;
        if (call.Type != type.ToOpt())
            return false;

        // This should always reduce, so is never used for code gen.
        full = false;

        return true;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        var arg = call.Args[0];
        if (arg.Type.IsOpt)
            return arg;
        return BndCastOptNode.Create(arg);
    }
}

/// <summary>
/// Tests whether a value is null. If the argument is a sequence, this is the same
/// as testing for empty.
/// </summary>
public sealed partial class IsNullFunc : RexlOper
{
    public static readonly IsNullFunc Instance = new IsNullFunc();

    private IsNullFunc()
        : base(isFunc: true, new DName("IsNull"), 1, 1)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        return ArgTraitsSimple.Create(this, eager: true, carg);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(info.Arity == 1);

        // REVIEW: Should we emit a warning if type isn't opt?
        var type = info.Args[0].Type;
        return (DType.BitReq, Immutable.Array.Create(type));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Type != DType.BitReq)
            return false;
        // When the arg is a sequence it should be reduced to IsEmpty.
        if (call.Args[0].Type.IsSequence)
            full = false;
        return true;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        var args = call.Args;
        var arg = args[0];
        if (arg.Type.IsNull)
            return BndIntNode.True;
        if (arg.Type.IsSequence)
            return reducer.Reduce(BndCallNode.Create(IsEmptyFunc.Instance, DType.BitReq, args));
        if (arg.Type.IsVac)
            return call;
        if (arg.IsConstant)
            return BndIntNode.CreateBit(arg.IsNullValue);
        if (!arg.Type.IsOpt)
        {
            // The type says it can't be null.
            return BndIntNode.False;
        }
        return call;
    }
}

/// <summary>
/// Tests whether a sequence or string is empty.
/// </summary>
public sealed partial class IsEmptyFunc : RexlOper
{
    public static readonly IsEmptyFunc Instance = new IsEmptyFunc();

    private IsEmptyFunc()
        : base(isFunc: true, new DName("IsEmpty"), 1, 1)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        return ArgTraitsSimple.Create(this, eager: true, carg);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(info.Arity == 1);

        DType type = info.Args[0].Type;
        if (!type.IsSequence && type != DType.Text && !type.IsNull)
            EnsureTypeSeq(ref type);
        return (DType.BitReq, Immutable.Array.Create(type));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Type != DType.BitReq)
            return false;
        var typeSrc = call.Args[0].Type;
        if (!typeSrc.IsSequence && typeSrc != DType.Text && typeSrc != DType.Null)
            return false;
        return true;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        var arg = call.Args[0];
        Validation.Assert(arg.Type.IsSequence || arg.Type == DType.Text || arg.Type.IsNull);

        if (arg.TryGetString(out string str))
            return BndIntNode.CreateBit(string.IsNullOrEmpty(str));
        if (arg.IsKnownNull)
            return BndIntNode.True;
        if (arg.Type.IsSequence)
        {
            var (min, max) = arg.GetItemCountRange();
            // Note that IsKnownNull should have caught max == 0, but doesn't hurt to have it here
            // in case that changes.
            if (max == 0)
                return BndIntNode.True;
            if (min > 0)
                return BndIntNode.False;
        }
        return call;
    }
}

public sealed class WithFunc : RexlOper
{
    public static readonly WithFunc With = new WithFunc(mapsNull: false, lifts: false, name: "With");
    public static readonly WithFunc Guard = new WithFunc(mapsNull: true, lifts: false, name: "Guard");
    public static readonly WithFunc WithMap = new WithFunc(mapsNull: false, lifts: true, name: "WithMap", reduction: With);
    public static readonly WithFunc GuardMap = new WithFunc(mapsNull: true, lifts: true, name: "GuardMap", reduction: Guard);

    public override RexlOper ReduceTo { get; }

    public bool MapsNull { get; }
    public bool Lifts { get; }

    private WithFunc(bool mapsNull, bool lifts, string name, WithFunc reduction = null)
        : base(isFunc: true, new DName(name), 2, int.MaxValue)
    {
        Validation.Assert(lifts == (reduction != null));
        MapsNull = mapsNull;
        Lifts = lifts;
        ReduceTo = reduction;
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.BugCheckParam(SupportsArity(carg), nameof(carg));
        return ArgTraitsWith.Create(this, carg, MapsNull, Lifts);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);

        var types = info.GetArgTypes();
        Validation.Assert(types != null);
        Validation.Assert(SupportsArity(types.Count));
        Validation.Assert(info.Scopes.Length == types.Count - 1);

        var scopes = info.Scopes;
        DType typeDst = types[scopes.Length];
        if (!typeDst.IsNull && MapsNull)
        {
            for (int slot = 0; slot < scopes.Length; slot++)
            {
                if (scopes[slot].Kind == ScopeKind.Guard)
                {
                    typeDst = typeDst.ToOpt();
                    break;
                }
            }
        }
        return (typeDst, types.ToImmutable());
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var args = call.Args;
        var scopes = call.Scopes;
        Validation.Assert(scopes.Length == args.Length - 1);
        DType typeDst = args[scopes.Length].Type;
        if (!typeDst.IsOpt && MapsNull)
        {
            for (int slot = 0; slot < scopes.Length; slot++)
            {
                if (scopes[slot].Kind == ScopeKind.Guard)
                {
                    typeDst = typeDst.ToOpt();
                    break;
                }
            }
        }
        if (call.Type != typeDst)
            return false;
        return true;
    }

    protected override (long min, long max) GetItemCountRangeCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));
        Validation.Assert(call.Type.IsSequence);

        var (min, max) = call.Args[call.Scopes.Length].GetItemCountRange();
        return MapsNull ? (0, max) : (min, max);
    }

    protected override PullWithFlags GetPullWithFlagsCore(BndCallNode call, int iarg)
    {
        Validation.Assert(IsValidCall(call));
        Validation.Assert(!call.Traits.IsRepeated(iarg));

        // Don't pull with/guard out of with/guard! Makes no sense and would
        // cause stack overflow.
        return 0;
    }

    private BndCallNode Flatten(BndCallNode call)
    {
        Validation.AssertValue(call);
        Validation.Assert(call.Oper == this);

        Immutable.Array<BoundNode>.Builder bldr = null;
        Immutable.Array<ArgScope>.Builder bldrScopes = null;
        var args = call.Args;
        var scopes = call.Scopes;
        Validation.Assert(scopes.Length == args.Length - 1);
        int ivDst = 0;
        for (int ivSrc = 0; ivSrc < args.Length; ivSrc++)
        {
            var arg = args[ivSrc];
            var scope = ivSrc < scopes.Length ? scopes[ivSrc] : null;
            if (arg is BndCallNode bcn && bcn.Oper is WithFunc func && (scope == null || !func.MapsNull || scope.Kind == ScopeKind.Guard))
            {
                bldr ??= args.ToBuilder();
                bldrScopes ??= scopes.ToBuilder();

                bldr.RemoveAt(ivDst);
                bldr.InsertRange(ivDst, bcn.Args);
                bldrScopes.InsertRange(ivDst, bcn.Scopes);
                ivDst += bcn.Args.Length;
                if (scope != null)
                {
                    arg = bldr[ivDst - 1];
                    Validation.Assert(scope == bldrScopes[ivDst - 1]);
                    if (!scope.IsValidArg(arg))
                    {
                        Validation.Assert(scope.Kind == ScopeKind.Guard);
                        Validation.Assert(!arg.Type.IsOpt);
                        Validation.Assert(arg.Type == scope.Type);
                        bldr[ivDst - 1] = BndCastOptNode.Create(arg);
                    }
                }
            }
            else
                ivDst++;
        }

        if (bldr == null)
        {
            Validation.Assert(bldrScopes == null);
            Validation.Assert(ivDst == args.Length);
            return call;
        }

        Validation.Assert(bldrScopes != null);
        Validation.Assert(bldr.Count == ivDst);
        Validation.Assert(bldrScopes.Count == ivDst - 1);

        args = bldr.ToImmutable();
        scopes = bldrScopes.ToImmutable();
        var guard = scopes.Any(s => s.Kind == ScopeKind.Guard);
        Validation.Assert(!guard || call.Type.IsOpt);
        return BndCallNode.Create(guard ? Guard : With, call.Type, args, scopes);
    }

    /// <summary>
    /// This performs multiple optimizations:
    /// * Merge equivalent scope args.
    /// * Reduce Guard scopes to With when they are known to be non-null.
    /// * Remove With scopes whose value is just a scope ref.
    /// * Removed unused scopes.
    /// * Substitute constant scope values.
    /// * Substitute with scope values that are cheap.
    /// * Substitute With scopes that are used only in one place (not in a loop).
    /// </summary>
    private struct Trimmer
    {
        private readonly WithFunc _func;
        private readonly IReducer _reducer;
        private readonly BndCallNode _call;
        private readonly Immutable.Array<ArgScope> _scopes;
        private readonly Immutable.Array<BoundNode> _args;

        private BitSet _toss;
        private Dictionary<ArgScope, ArgScope> _map;
        private Immutable.Array<BoundNode>.Builder _bldr;
        private Immutable.Array<ArgScope>.Builder _bldrScopes;

        private Trimmer(WithFunc func, IReducer reducer, BndCallNode call)
        {
            Validation.AssertValue(func);
            Validation.AssertValue(reducer);
            Validation.Assert(func.IsValidCall(call));

            _func = func;
            _reducer = reducer;
            _call = call;
            _scopes = _call.Scopes;
            Validation.Assert(_func.MapsNull || !_scopes.Any(s => s.Kind == ScopeKind.Guard));
            _args = _call.Args;
            Validation.Assert(_args.Length == _scopes.Length + 1);

            _toss = 0;
            _map = null;
            _bldr = null;
            _bldrScopes = null;
        }

        /// <summary>
        /// Merge equivalent scopes, remove unused ones, substitute those used only once.
        /// </summary>
        public static BoundNode Trim(WithFunc func, IReducer reducer, BndCallNode call)
        {
            var trimmer = new Trimmer(func, reducer, call);
            return trimmer.Run();
        }

        private BoundNode Run()
        {
            for (; ; )
            {
                if (_call.Type.IsNull || IsNull())
                    return BndNullNode.Create(_call.Type);

                // Merge equivalent scopes.
                MergeEquiv();

                // Change Guard with With when possible and deal with With scopes that are just scope references.
                // If any arguments change, go around again.
                if (TryReduceScopes())
                    continue;

                // Removed unused scopes.
                TossUnused();

                // Apply any scope mapping to the remaining args.
                ApplyMap();

                // Substitute in constants. If any arguments change, go around again, since optimization may have
                // removed uses of scopes (eg, multiply by zero).
                if (TrySubstituteConstants())
                    continue;

                // Substitute single use scopes. If any arguments change, go around again.
                if (!TrySubstituteSingles())
                    break;
            }

            return Finish();
        }

        private bool IsTossed(int i)
        {
            Validation.AssertIndex(i, _args.Length);
            return _toss.TestBit(i);
        }

        private ArgScope GetScope(int i)
        {
            Validation.Assert(_bldrScopes == null || _bldrScopes.Count <= _scopes.Length);
            Validation.AssertIndex(i, _bldrScopes != null ? _bldrScopes.Count : _scopes.Length);
            Validation.Assert(!IsTossed(i));
            return _bldrScopes != null ? _bldrScopes[i] : _scopes[i];
        }

        private BoundNode GetArg(int i)
        {
            Validation.Assert(_bldr == null || _bldr.Count <= _args.Length);
            Validation.AssertIndex(i, _bldr != null ? _bldr.Count : _args.Length);
            Validation.Assert(!IsTossed(i));
            return _bldr != null ? _bldr[i] : _args[i];
        }

        private void TossScope(int iscope, ArgScope scopeMap)
        {
            Validation.Assert(_bldrScopes == null || _bldrScopes.Count == _scopes.Length);
            Validation.AssertIndex(iscope, _scopes.Length);
            Validation.Assert(!IsTossed(iscope));
            Validation.AssertValueOrNull(scopeMap);

            var scopeSrc = GetScope(iscope);
            Validation.Assert(scopeMap != scopeSrc);
            _toss = _toss.SetBit(iscope);
            if (scopeMap != null)
                Util.Add(ref _map, scopeSrc, scopeMap);
        }

        private void ChangeScope(int iscope, ArgScope scopeNew)
        {
            Validation.Assert(_bldrScopes == null || _bldrScopes.Count == _scopes.Length);
            Validation.AssertIndex(iscope, _scopes.Length);
            Validation.Assert(!IsTossed(iscope));
            Validation.AssertValue(scopeNew);

            var scopeSrc = GetScope(iscope);
            _bldrScopes ??= _scopes.ToBuilder();
            _bldrScopes[iscope] = scopeNew;
            Util.Add(ref _map, scopeSrc, scopeNew);
        }

        private void ChangeArg(int iarg, BoundNode bnd)
        {
            Validation.Assert(_bldr == null || _bldr.Count == _args.Length);
            Validation.Assert(!IsTossed(iarg));
            Validation.AssertValue(bnd);

            _bldr ??= _args.ToBuilder();
            _bldr[iarg] = bnd;
        }

        /// <summary>
        /// Do easy checks for null, like any guards that are known to be null.
        /// </summary>
        private bool IsNull()
        {
            if (GetArg(_scopes.Length).IsKnownNull)
                return true;

            if (!_func.MapsNull)
                return false;

            // See if any guards are known to be null.
            for (int i = 0; i < _scopes.Length; i++)
            {
                if (IsTossed(i))
                    continue;
                if (GetScope(i).Kind != ScopeKind.Guard)
                    continue;
                var arg = GetArg(i);
                if (arg.IsKnownNull)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Merge equivalent scopes.
        /// </summary>
        private void MergeEquiv()
        {
            for (int i = 1; i < _scopes.Length; i++)
            {
                if (IsTossed(i))
                    continue;

                var arg = GetArg(i);
                if (arg.IsImpure)
                    continue;

                var kind = GetScope(i).Kind;
                for (int j = 0; j < i; j++)
                {
                    if (IsTossed(j))
                        continue;

                    if (GetScope(j).Kind != kind)
                        continue;
                    var other = GetArg(j);
                    if (!other.IsImpure && arg.Equivalent(other, _map))
                    {
                        TossScope(i, GetScope(j));
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Change Guard to With when possible. Also toss any With scopes that map directly to another scope.
        /// Returns true if some values changed, indicating that reductions should restart.
        /// </summary>
        private bool TryReduceScopes()
        {
            bool argsChanged = false;

            for (int i = _scopes.Length; --i >= 0;)
            {
                if (IsTossed(i))
                    continue;

                var scope = GetScope(i);
                var arg = GetArg(i);

                if (scope.Kind == ScopeKind.Guard)
                {
                    // Other code should have already handled guard scopes with known null value.
                    Validation.Assert(!arg.IsKnownNull);

                    if (arg is BndCastOptNode bco)
                    {
                        // Change this to a With.
                        Validation.Assert(bco.Child.Type == scope.Type);
                        ChangeArg(i, bco.Child);
                        argsChanged = true;
                    }
                    else if (arg is BndCastRefNode bcr && !bcr.Child.Type.IsOpt)
                    {
                        // Change this to a With.
                        var argNew = bcr.Child;
                        if (argNew.Type != scope.Type)
                            argNew = BndCastRefNode.Create(argNew, scope.Type);
                        ChangeArg(i, argNew);
                        argsChanged = true;
                    }
                    else if (arg.IsConstant)
                    {
                        // Non-null constant of opt type, like a string, so change to a With.
                        Validation.Assert(arg.Type.IsOpt);
                        Validation.Assert(!arg.Type.HasReq);
                    }
                    else if (arg.Type.IsSequence)
                    {
                        var (min, max) = arg.GetItemCountRange();
                        Validation.Assert(max > 0);
                        if (min == 0)
                            continue;
                        // Non-empty sequence, so Guard can change to With.
                    }
                    else
                        continue;

                    scope = ArgScope.Create(ScopeKind.With, scope.Type);
                    ChangeScope(i, scope);
                }

                Validation.Assert(scope.Kind == ScopeKind.With);
                if (arg is BndScopeRefNode bsrn)
                    TossScope(i, bsrn.Scope);
            }

            return argsChanged;
        }

        /// <summary>
        /// Look for unused scopes.
        /// </summary>
        private void TossUnused()
        {
            for (int i = _scopes.Length; --i >= 0;)
            {
                if (IsTossed(i))
                    continue;

                var scope = GetScope(i);
                var arg = GetArg(i);

                if (scope.Kind == ScopeKind.Guard)
                {
                    Validation.Assert(!arg.IsConstant);
                    Validation.Assert(!arg.IsKnownNull);
                    // Doesn't matter if it is actually used, we need it for null testing.
                    continue;
                }

                Validation.Assert(scope.Kind == ScopeKind.With);
                Validation.Assert(!(arg is BndScopeRefNode));

                bool used = false;
                for (int j = i + 1; j < _args.Length; j++)
                {
                    if (IsTossed(j))
                        continue;
                    if (ScopeCounter.Any(GetArg(j), scope, _map))
                    {
                        used = true;
                        break;
                    }
                }

                if (!used)
                    TossScope(i, null);
            }
        }

        /// <summary>
        /// Do any scope mapping.
        /// </summary>
        private void ApplyMap()
        {
            if (_map == null)
                return;

            for (int i = 1; i < _args.Length; i++)
            {
                if (IsTossed(i))
                    continue;
                var arg = GetArg(i);
                var argNew = _reducer.MapScopes(arg, _map);
                if (argNew != arg)
                    ChangeArg(i, argNew);
            }
            _map = null;
        }

        /// <summary>
        /// Substitute constant scopes. Returns true if something was reduced, in which case,
        /// everything should restart.
        /// </summary>
        private bool TrySubstituteConstants()
        {
            Validation.Assert(_map == null);

            bool argsChanged = false;
            for (int i = 0; i < _scopes.Length; i++)
            {
                if (IsTossed(i))
                    continue;

                var arg = GetArg(i);
                var scope = GetScope(i);
                if (scope.Kind == ScopeKind.Guard)
                {
                    if (!arg.IsConstant)
                        continue;

                    // A constant With scope could cause this to be reduced to a null.
                    if (arg.IsKnownNull)
                    {
                        Validation.Assert(argsChanged);
                        return true;
                    }

                    if (arg is BndCastOptNode bco)
                    {
                        Validation.Assert(scope.Type == bco.Child.Type);
                        arg = bco.Child;
                    }
                    else if (arg is BndCastRefNode bcr && !bcr.Child.Type.IsOpt)
                    {
                        // REVIEW: Can this case happen? Perhaps for compound constants, so future
                        // symbolic compute scenarios will hit this.
                        var typeSrc = bcr.Child.Type;
                        if (typeSrc == scope.Type)
                            arg = bcr.Child;
                        else
                            arg = BndCastRefNode.Create(bcr.Child, scope.Type);
                    }

                    if (arg.Type != scope.Type)
                    {
                        Validation.Assert(false);
                        continue;
                    }
                }
                else
                {
                    Validation.Assert(scope.Kind == ScopeKind.With);
                    if (!arg.IsCheap)
                        continue;
                }

                // Replace in all the later args.
                for (int j = i + 1; j < _args.Length; j++)
                {
                    if (IsTossed(j))
                        continue;

                    var argCur = GetArg(j);
                    var (x, num) = _reducer.ReplaceScope(argCur, scope, arg);
                    if (num > 0)
                    {
                        // REVIEW: At this point, the new arg could become just about anything.
                        // Consequently, this should really restart the whole process.
                        Validation.Assert(x != argCur);
                        ChangeArg(j, _reducer.Reduce(x));
                        argsChanged = true;
                    }
                }

                TossScope(i, null);
            }

            return argsChanged;
        }

        /// <summary>
        /// Substitute values for With scopes that are used only once.
        /// </summary>
        private bool TrySubstituteSingles()
        {
            Validation.Assert(_map == null);

            bool argsChanged = false;
            for (int i = 0; i < _scopes.Length; i++)
            {
                if (IsTossed(i))
                    continue;

                var scope = GetScope(i);
                if (scope.Kind == ScopeKind.Guard)
                {
                    // Need to keep the arg for null testing even if it is not used.
                    continue;
                }

                int useCount = 0;
                int jUse = -1;
                for (int j = i + 1; j < _args.Length; j++)
                {
                    if (IsTossed(j))
                        continue;

                    int num = ScopeCounter.Count(GetArg(j), scope);
                    if (num > 0)
                    {
                        useCount += num;
                        if (useCount > 1)
                            break;
                        jUse = j;
                    }
                }

                // The TossUnused method should have dealt with any that aren't used before entering this method,
                // but it is possible that ReplaceScope removes usage of scope in a previous iteration of this loop.
                Validation.Assert(useCount > 0 || argsChanged);

                if (useCount <= 1)
                {
                    // As noted above, this condition should always be true, but it's safer (given the complexity of
                    // the code that ensures the invariant) to keep this test.
                    if (useCount > 0)
                    {
                        var (x, num) = _reducer.ReplaceScope(GetArg(jUse), scope, GetArg(i));
                        Validation.Assert(num == 1);
                        ChangeArg(jUse, _reducer.Reduce(x));
                        argsChanged = true;
                    }

                    // Toss the scope. This comes after the block above so GetArg(i) is still legal there.
                    TossScope(i, null);
                }
            }

            return argsChanged;
        }

        private BoundNode Finish()
        {
            Validation.Assert(_map == null);

            if (!_toss.IsEmpty)
            {
                Validation.Assert(!_toss.TestAtOrAbove(_scopes.Length));
                if (_toss == BitSet.GetMask(_scopes.Length))
                {
                    // No scopes are needed.
                    var arg = GetArg(_scopes.Length);
                    if (arg.Type != _call.Type)
                    {
                        Validation.Assert(arg.Type.ToOpt() == _call.Type);
                        arg = BndCastOptNode.Create(arg);
                    }
                    return arg;
                }

                // Build the reduced invocation.
                _bldr ??= _args.ToBuilder();
                _bldrScopes ??= _scopes.ToBuilder();
                int ivDst = 0;
                int ivSrc = 0;
                for (; ivSrc < _bldrScopes.Count; ivSrc++)
                {
                    if (_toss.TestBit(ivSrc))
                        continue;
                    if (ivDst < ivSrc)
                    {
                        _bldr[ivDst] = _bldr[ivSrc];
                        _bldrScopes[ivDst] = _bldrScopes[ivSrc];
                    }
                    ivDst++;
                }
                _bldrScopes.RemoveTail(ivDst);
                _bldr[ivDst++] = _bldr[ivSrc];
                _bldr.RemoveTail(ivDst);

                // Clear _toss.
                _toss = 0;
            }

            // Trim un-needed tail scopes.
            int len = _bldr != null ? _bldr.Count : _args.Length;
            if (_bldr == null && _args[len - 1] is BndScopeRefNode srnTmp && srnTmp.Scope == _scopes[len - 2])
                _bldr = _args.ToBuilder();

            // If any scopes have changed, then some args should have also changed.
            // If this assert triggers - we need to understand why. However, we still handle this case.
            Validation.Assert(_bldrScopes == null || _bldr != null);
            if (_bldrScopes != null && _bldr == null)
                _bldr = _args.ToBuilder();

            BoundNode res = _call;
            if (_bldr != null)
            {
                Validation.Assert(len == _bldr.Count);
                while (len > 1 && _bldr[len - 1] is BndScopeRefNode srn && srn.Scope == GetScope(len - 2))
                    len--;

                if (len == 1)
                    res = _bldr[0];
                else
                {
                    if (len < _bldr.Count)
                    {
                        _bldr.RemoveTail(len);
                        _bldrScopes ??= _scopes.ToBuilder();
                        _bldrScopes.RemoveTail(len - 1);
                    }

                    var args = _bldr.ToImmutable();
                    var scopes = _bldrScopes != null ? _bldrScopes.ToImmutable() : _scopes;

                    var typeSel = args[args.Length - 1].Type;
                    bool anyGuard = scopes.Any(s => s.Kind == ScopeKind.Guard);
                    res = BndCallNode.Create(anyGuard ? Guard : With, anyGuard ? typeSel.ToOpt() : typeSel, args, scopes);
                }
            }

            return res;
        }
    }

    private BndCallNode InlineRecords(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        Immutable.Array<BoundNode>.Builder bldr = null;
        Immutable.Array<ArgScope>.Builder bldrScopes = null;
        var args = call.Args;
        var scopes = call.Scopes;
        Validation.Assert(scopes.Length == args.Length - 1);
        int ivDst = 0;
        for (int ivSrc = 0; ivSrc < scopes.Length; ivSrc++)
        {
            var arg = bldr != null ? bldr[ivDst] : args[ivSrc];
            var scope = scopes[ivSrc];
            var brn = arg as BndRecordNode;
            var btn = arg as BndTupleNode;
            if (brn == null && btn == null)
            {
                ivDst++;
                continue;
            }

            // Used with ScopeFinder to count the number of scope references
            // and the number of such references that are NOT field/slot retrievals.
            // Also records the field/slot usage.
            var items = default(BitSet);
            HashSet<DName> fields = null;
            HashSet<int> slots = null;
            int all = 0;
            int non = 0;
            void HandleRef(ArgScope s, BndScopeRefNode b, ScopeFinder.IContext ctx)
            {
                if (s != scope)
                    return;
                all++;
                if (ctx.Depth == 0)
                {
                    non++;
                    return;
                }

                var parent = ctx[0];
                if (parent is BndGetFieldNode gf)
                    Util.Add(ref fields, gf.Name);
                else if (parent is BndGetSlotNode gs)
                    Util.Add(ref slots, gs.Slot);
                else
                    non++;
            }
            Action<ArgScope, BndScopeRefNode, ScopeFinder.IContext> act = HandleRef;

            for (int i = ivSrc + 1; i < args.Length; i++)
            {
                Validation.Assert(non == 0);
                int prev = all;
                ScopeFinder.Run(args[i], act);
                Validation.AssertIndexInclusive(non, all - prev);
                if (non > 0)
                    break;
                // Remember whether this item needs to be mapped.
                if (all > prev)
                    items = items.SetBit(i - ivSrc - 1);
            }

            if (non > 0)
            {
                // Can't reduce.
                ivDst++;
                continue;
            }

            Validation.Assert((fields == null) ^ (slots == null));
            int countNew = fields != null ? fields.Count : slots.Count;
            Validation.Assert(countNew > 0);

            bldr ??= args.ToBuilder();
            bldrScopes ??= scopes.ToBuilder();

            Dictionary<DName, ArgScope> fieldMap = null;
            Dictionary<int, ArgScope> slotMap = null;

            var valuesNew = new BoundNode[countNew];
            var scopesNew = new ArgScope[countNew];
            int index = 0;
            if (fields != null)
            {
                fieldMap = new Dictionary<DName, ArgScope>(fields.Count);
                foreach (var name in fields.OrderBy(n => n.Value, StringComparer.Ordinal))
                {
                    arg.Type.TryGetNameType(name, out var type).Verify();
                    fieldMap.Add(name, scopesNew[index] = ArgScope.Create(ScopeKind.With, type));
                    if (!brn.Items.TryGetValue(name, out valuesNew[index]))
                        valuesNew[index] = BndDefaultNode.Create(type);
                    index++;
                }
            }
            else
            {
                Validation.Assert(slots != null);
                slotMap = new Dictionary<int, ArgScope>(slots.Count);
                var types = arg.Type.GetTupleSlotTypes();
                foreach (var slot in slots.OrderBy(s => s))
                {
                    slotMap.Add(slot, scopesNew[index] = ArgScope.Create(ScopeKind.With, types[slot]));
                    valuesNew[index] = btn.Items[slot];
                    index++;
                }
            }
            Validation.Assert(index == countNew);

            foreach (var i in items)
            {
                int iv = ivDst + 1 + i;
                Validation.AssertIndex(iv, bldr.Count);
                var dst = reducer.MapScopeFields(bldr[iv], scope, fieldMap, slotMap);
                Validation.Assert(dst != bldr[iv]);
                bldr[iv] = dst;
            }

            if (countNew == 1)
            {
                bldr[ivDst] = valuesNew[0];
                bldrScopes[ivDst] = scopesNew[0];
            }
            else
            {
                bldr.RemoveAt(ivDst);
                bldrScopes.RemoveAt(ivDst);
                bldr.InsertRange(ivDst, valuesNew);
                bldrScopes.InsertRange(ivDst, scopesNew);
            }
            ivDst += countNew;
        }

        if (bldr == null)
        {
            Validation.Assert(bldrScopes == null);
            Validation.Assert(ivDst == scopes.Length);
            return call;
        }

        Validation.Assert(bldrScopes != null);
        Validation.Assert(bldr.Count == ivDst + 1);
        Validation.Assert(bldrScopes.Count == ivDst);
        return BndCallNode.Create(MapsNull ? Guard : With, call.Type, bldr.ToImmutable(), bldrScopes.ToImmutable());
    }

    private static BoundNode ReduceCore(WithFunc func, IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(func.IsValidCall(call));

        int phaseCur = 0;
        int phaseChanged = 0;
        const int phases = 3;
        for (; ; )
        {
            BndCallNode prev;

            // Flatten.
            func = call.Oper as WithFunc;
            if (func == null || phaseCur - phaseChanged >= phases)
                return call;
            call = func.Flatten(prev = call);
            Validation.Assert(call.Type == prev.Type);
            phaseCur++;
            if (call != prev)
                phaseChanged = phaseCur;

            // Merge equivalent scopes, remove unused scopes, substitute constant scopes,
            // substitute scopes used only once.
            func = call.Oper as WithFunc;
            if (func == null || phaseCur - phaseChanged >= phases)
                return call;
            var node = Trimmer.Trim(func, reducer, call);
            Validation.Assert(node.Type == prev.Type || node.Type.ToOpt() == prev.Type);
            phaseCur++;
            if (node != prev)
                phaseChanged = phaseCur;
            if ((call = node as BndCallNode) == null)
                return node;

            // Expand explicit records/tuples where only the fields/slots are used.
            func = call.Oper as WithFunc;
            if (func == null || phaseCur - phaseChanged >= phases)
                return call;
            call = func.InlineRecords(reducer, prev = call);
            Validation.Assert(call.Type == prev.Type);
            phaseCur++;
            if (call != prev)
                phaseChanged = phaseCur;
            Validation.Assert(phaseCur % phases == 0);
        }
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode bnd)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(bnd));

        var arg = bnd.Args[bnd.Args.Length - 1];
        if (arg.IsKnownNull)
            return BndNullNode.Create(bnd.Type);

        var res = ReduceCore(this, reducer, bnd);
        if (res.Type != bnd.Type)
        {
            Validation.Assert(res.Type.ToOpt() == bnd.Type);
            res = BndCastOptNode.Create(res);
        }

        return res;
    }
}

/// <summary>
/// Short-ciruiting "if" function. This handles any number of conditions and the "else" value
/// is optional, defaulting to "null". For example, in 'If(a, b, c, d, e)', a and c are boolean
/// conditions while b, d, and e are promoted to the same type, which is the result type. When
/// the number of args is even, the "else" value is null, forcing the result type to be opt.
/// 
/// REVIEW: Consider making the implicit "else" value 'default' instead.
/// </summary>
public sealed partial class IfFunc : RexlOper
{
    public static readonly IfFunc Instance = new IfFunc();

    private IfFunc()
        : base(isFunc: true, new DName("If"), 2, int.MaxValue)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        return ArgTraitsSimple.Create(this, eager: false, carg);
    }

    protected override bool SupportsDirectiveCore(ArgTraits traits, int slot, Directive dir)
    {
        Validation.AssertValue(traits);
        Validation.Assert(traits.Oper == this);
        Validation.AssertIndex(slot, traits.SlotCount);
        Validation.Assert(dir != Directive.None);

        int cslot = traits.SlotCount;
        switch (dir)
        {
        case Directive.If:
            return (slot & 1) == 0 && slot < cslot - 1;
        case Directive.Else:
            return (slot & 1) == 0 && slot == cslot - 1;
        }
        return false;
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var types = info.GetArgTypes();
        int carg = types.Count;
        var typeRes = (carg & 1) == 0 ? DType.Null : types[carg - 1];
        for (int iarg = 1; iarg < carg; iarg += 2)
            typeRes = DType.GetSuperType(typeRes, types[iarg], AcceptUseUnion);
        for (int iarg = 1; iarg < carg; iarg += 2)
        {
            types[iarg - 1] = DType.BitReq;
            types[iarg] = typeRes;
        }
        types[carg - 1] = typeRes;

        return (typeRes, types.ToImmutable());
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        var args = call.Args;
        for (int slot = 0; slot < args.Length; slot++)
        {
            var typeCur = (slot & 1) == 0 && slot < args.Length - 1 ? DType.BitReq : type;
            if (args[slot].Type != typeCur)
                return false;
        }

        full = false; // Should always reduce to `if` expression.
        return true;
    }

    protected override (long min, long max) GetItemCountRangeCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));
        Validation.Assert(call.Type.IsSequence);

        var args = call.Args;
        int carg = args.Length;

        // Process initial constant predicates.
        bool some = false;
        long min = default;
        long max = default;
        long minCur, maxCur;
        for (int iarg = 1; ; iarg += 2)
        {
            Validation.Assert((iarg & 1) != 0);
            bool isConst;
            if (iarg >= carg)
            {
                // Get the counts for the final "else" value.
                if (iarg == carg)
                    (minCur, maxCur) = args[iarg - 1].GetItemCountRange();
                else
                    minCur = maxCur = 0;
                // Don't look for additional values.
                isConst = true;
            }
            else
            {
                isConst = args[iarg - 1].TryGetBool(out var cond);
                if (isConst && !cond)
                {
                    // The predicate is constant false, so the current value is never used.
                    continue;
                }
                (minCur, maxCur) = args[iarg].GetItemCountRange();
            }

            // Fold in the counts for the current value.
            if (!some)
            {
                min = minCur;
                max = maxCur;
                some = true;
            }
            else
            {
                min = Math.Min(min, minCur);
                max = Math.Max(max, maxCur);
            }

            // If the predicate is constant true, no need to look at more values.
            if (isConst)
                return (min, max);
        }
    }

    protected override PullWithFlags GetPullWithFlagsCore(BndCallNode call, int iarg)
    {
        Validation.Assert(IsValidCall(call));
        Validation.Assert(!call.Traits.IsRepeated(iarg));
        Validation.Coverage(iarg == 0 ? 0 : 1);

        // If is "strict" only for the first arg.
        return iarg == 0 ? PullWithFlags.With : 0;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        var type = call.Type;
        var args = call.Args;
        int carg = args.Length;

        int iargSrc = 0;
        int iargDst = 0;
        Immutable.Array<BoundNode>.Builder bldr = null;
        for (; iargSrc < carg - 1; iargSrc += 2)
        {
            Validation.Assert((iargSrc & 1) == 0);
            Validation.Assert((iargDst & 1) == 0);
            Validation.Assert(iargDst <= iargSrc);
            Validation.Assert(args[iargSrc].Type == DType.BitReq);
            Validation.Assert(args[iargSrc + 1].Type == type);

            if (args[iargSrc].TryGetBool(out bool cond))
            {
                // The condition is constant, so can be dropped.
                if (cond)
                {
                    // Later values aren't used.
                    var val = args[iargSrc + 1];
                    if (iargDst == 0)
                        return val;
                    bldr ??= args.ToBuilder();
                    if (!val.IsKnownNull)
                        bldr[iargDst++] = val;
                    bldr.RemoveTail(iargDst);
                    return CreateIf(type, bldr);
                }
                // The condition and value are dropped.
            }
            else
            {
                // Need to keep this condition and value.
                if (iargDst < iargSrc)
                {
                    bldr ??= args.ToBuilder();
                    bldr[iargDst] = args[iargSrc];
                    bldr[iargDst + 1] = args[iargSrc + 1];
                }
                iargDst += 2;
            }
        }

        Validation.Assert(iargSrc == (carg & ~1));
        Validation.Assert((iargDst & 1) == 0);
        Validation.Assert(iargDst <= iargSrc);

        if (iargDst == iargSrc)
        {
            Validation.Assert(bldr == null);
            return CreateIf(type, args, carg);
        }

        if (iargDst == 0)
            return iargSrc < carg ? args[iargSrc] : BndNullNode.Create(type);

        bldr ??= args.ToBuilder();
        if (iargSrc < carg && !args[iargSrc].IsNullValue)
            bldr[iargDst++] = args[iargSrc];
        bldr.RemoveTail(iargDst);
        return CreateIf(type, bldr);
    }

    private BoundNode CreateIf(DType type, Immutable.Array<BoundNode>.Builder args)
    {
        Validation.Assert(args.Count >= 2);
        var odd = (args.Count & 1) != 0;
        var node = odd ?
            BndIfNode.Create(args[^3], args[^2], args[^1]) :
            BndIfNode.Create(args[^2], args[^1], BndNullNode.Create(type));
        for (int iarg = args.Count - (odd ? 4 : 3); iarg > 0; iarg -= 2)
            node = BndIfNode.Create(args[iarg - 1], args[iarg], node);
        return node;
    }

    private BoundNode CreateIf(DType type, Immutable.Array<BoundNode> args, int lim)
    {
        Validation.Assert(lim <= args.Length);
        Validation.Assert(lim >= 2);
        var odd = (lim & 1) != 0;
        var node = odd ?
            BndIfNode.Create(args[lim - 3], args[lim - 2], args[lim - 1]) :
            BndIfNode.Create(args[lim - 2], args[lim - 1], BndNullNode.Create(type));
        for (int iarg = lim - (odd ? 4 : 3); iarg > 0; iarg -= 2)
            node = BndIfNode.Create(args[iarg - 1], args[iarg], node);
        return node;
    }
}
