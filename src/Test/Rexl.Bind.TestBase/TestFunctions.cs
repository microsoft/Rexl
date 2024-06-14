// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Rexl;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;

namespace RexlTest;

public sealed partial class TestFunctions : OperationRegistry
{
    public static readonly TestFunctions Instance = new TestFunctions();

    private TestFunctions()
        : base(BuiltinFunctions.Instance, BuiltinProcedures.Instance, FlowProcs.Instance)
    {
        AddBoth(CastGenFunc.Raw);
        AddBoth(CastGenFunc.Lift);

        AddBoth(WrapFunc.Instance);
        AddBoth(WrapFunc.NYI);
        AddBoth(WrapLogFunc.Instance);
        AddBoth(WrapCallCtxFunc.Instance);

        // For testing deprecation messages.
        AddOne(WrapFunc.Instance, "Old", deprecated: true);
        AddOneDep(WrapFunc.Instance, "OldAlt");

        // For testing fuzzy matching with escaping.
        AddOne(WrapFunc.Instance, "Wrap One");
        AddOne(new OperInfo(NPath.Root.Append(new DName("Wrap One")), WrapFunc.Instance));

        // For testing various ArgTrait patterns.
        AddBoth(FirstNRevFunc.Instance);
        AddBoth(DblMapFunc.Instance);

        // For testing range scope interaction with other loop scopes.
        AddBoth(TestRngSeqFunc.Instance);

        // For wrapping a sequence as IndexedSequence<T>.
        AddBoth(TestWrapSeqFunc.Instance);

        // For testing sequence functions.
        AddBoth(TestWrapCollFunc.CantCount);
        AddBoth(TestWrapCollFunc.LazyCount);
        AddBoth(TestWrapCollFunc.WrapList);
        AddBoth(TestWrapCollFunc.WrapArr);
        AddBoth(TestWrapCollFunc.WrapColl);
        AddBoth(TestWrapCollFunc.WrapCurs);

        // For testing arity based function overload resolution.
        AddOne(ArityTestFunc.Arity_1_7);
        AddOne(ArityTestFunc.Arity_2_6);

        AddOne(TestWith.Instance);
        AddBoth(PingFunc.Instance);

        AddBoth(ThrowFunc.Instance);
    }

    /// <summary>
    /// Add both with its standard namespace (the Test namespace) and with dropping the first namespace name.
    /// </summary>
    private void AddBoth(RexlOper oper)
    {
        // Add it both with and without first namespace component, if the name without isn't already taken.
        AddOne(oper);
        if (oper.Path.NameCount > 1)
        {
            var pathShort = NPath.Root.AppendPartial(oper.Path, 1);
            if (GetOper(pathShort) is null)
                AddOne(oper, pathShort);
        }
    }
}

/// <summary>
/// Base class for Test functions. Puts the function in the "Test" namespace.
/// </summary>
public abstract class TestFunc : RexlOper
{
    // Set the namespace to "Test".
    private protected TestFunc(string name, int arityMin, int arityMax)
        : base(isFunc: true, new DName(name), NPath.Root.Append(new DName("Test")), arityMin, arityMax)
    {
    }

    // Set the namespace to "Test".
    private protected TestFunc(string name, bool union, int arityMin, int arityMax)
        : base(isFunc: true, new DName(name), NPath.Root.Append(new DName("Test")), union, arityMin, arityMax, null)
    {
    }

    protected abstract override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info);
}

/// <summary>
/// These function are for casting a value to the general type, without and with lifting.
/// They are used to help test proper handling of general throughout the system.
/// </summary>
public sealed class CastGenFunc : TestFunc
{
    public static readonly CastGenFunc Raw = new(lift: false);
    public static readonly CastGenFunc Lift = new(lift: true);

    private readonly bool _lift;

    private CastGenFunc(bool lift)
        : base(lift ? "CastGenLift" : "CastGen", 1, 1)
    {
        _lift = lift;
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        return _lift ?
            ArgTraitsLifting.Create(this, carg, maskLiftSeq: 0x1) :
            ArgTraitsSimple.Create(this, eager: false, carg);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(info.Arity == 1);

        var type = info.Args[0].Type;
        return (DType.General, Immutable.Array.Create(type));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Type != DType.General)
            return false;
        return true;
    }
}

/// <summary>
/// This function is for wrapping a literal so it doesn't look like a constant to the binder.
/// It helps with testing various scenarios, such as Abs(Wrap([-1, 2, -5])).
/// </summary>
public sealed class WrapFunc : TestFunc
{
    public static readonly WrapFunc Instance = new WrapFunc(nyi: false);
    public static readonly WrapFunc NYI = new WrapFunc(nyi: true);

    public bool IsNYI { get; }

    private WrapFunc(bool nyi)
        : base(nyi ? "WrapNYI" : "Wrap", 1, 1)
    {
        IsNYI = nyi;
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        return ArgTraitsSimple.Create(this, eager: false, carg);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(info.Arity == 1);

        var type = info.Args[0].Type;
        return (type, Immutable.Array.Create(type));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        var args = call.Args;
        if (type != args[0].Type)
            return false;
        return true;
    }
}

/// <summary>
/// Wraps a value so it doesn't look like a constant to the binder. Also logs the value
/// at run time, effectively testing the execution context mechanism.
/// </summary>
public sealed class WrapLogFunc : TestFunc
{
    public static readonly WrapLogFunc Instance = new WrapLogFunc();

    private WrapLogFunc()
        : base("WrapLog", 1, 1)
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

        var type = info.Args[0].Type;
        return (type, Immutable.Array.Create(type));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        var args = call.Args;
        if (type != args[0].Type)
            return false;
        return true;
    }
}

/// <summary>
/// This function calls the <see cref="ExecCtx"/>'s methods.
/// This facilitates coverage of calling those methods, in particular when passed with no ID.
/// </summary>
public sealed class WrapCallCtxFunc : TestFunc
{
    public static readonly WrapCallCtxFunc Instance = new WrapCallCtxFunc();

    private WrapCallCtxFunc()
        : base("WrapCallCtx", 1, 1)
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
        Validation.Assert(info.Arity == 1);

        var type = info.Args[0].Type;
        return (type, Immutable.Array.Create(type));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        var args = call.Args;
        if (type != args[0].Type)
            return false;
        return true;
    }
}

/// <summary>
/// Like Take but requires the predicate and has the predicate before the count.
/// </summary>
public sealed partial class FirstNRevFunc : TestFunc
{
    public static readonly FirstNRevFunc Instance = new FirstNRevFunc();

    private FirstNRevFunc()
        : base(new DName("FirstNRev"), 3, 3)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(carg == 3);
        // Only the predicate is nested, not the count, and the count comes after the predicate.
        return ArgTraitsZip.Create(this, indexed: false, eager: false, carg, seqCount: 1, nonPost: 1);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(info.Arity == 3);

        var type = info.Args[0].Type;
        EnsureTypeSeq(ref type);
        return (type, Immutable.Array.Create(type, DType.BitReq, DType.I8Req));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        var args = call.Args;
        if (!type.IsSequence)
            return false;
        if (args[0].Type != type)
            return false;
        if (args[1].Type != DType.BitReq)
            return false;
        if (args[2].Type != DType.I8Req)
            return false;
        return true;
    }
}

/// <summary>
/// Map over two sequences and weave them together.
/// </summary>
public sealed partial class DblMapFunc : TestFunc
{
    public static readonly DblMapFunc Instance = new DblMapFunc();

    private DblMapFunc()
        // Use operator acceptance to mimic ++.
        : base(new DName("DblMap"), DType.UseUnionOper, 4, 4)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.BugCheckParam(carg == 4, nameof(carg));
        return ArgTraits.CreateGeneral(this, 4, scopeKind: ScopeKind.SeqItem, maskScope: 0x5, maskNested: 0xA,
            maskScopeInactive: ArgTraits.MakeScopeInactive(2, (3, 1)), maskLazySeq: 0xF);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var types = info.GetArgTypes();
        Validation.Assert(types.Count == 4);

        DType type = DType.GetSuperType(types[1], types[3], AcceptUseUnion);
        EnsureTypeSeq(types, 0);
        EnsureTypeSeq(types, 2);
        types[1] = type;
        types[3] = type;

        return (type.ToSequence(), types.ToImmutable());
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        if (!type.IsSequence)
            return false;
        var typeItem = type.ItemTypeOrThis;
        var args = call.Args;
        if (!args[0].Type.IsSequence)
            return false;
        if (args[1].Type != typeItem)
            return false;
        if (!args[2].Type.IsSequence)
            return false;
        if (args[3].Type != typeItem)
            return false;
        return true;
    }
}

/// <summary>
/// Wraps a sequence as an <see cref="IIndexedEnumerable{T}"/>.
/// </summary>
public sealed partial class TestWrapSeqFunc : TestFunc
{
    public static readonly TestWrapSeqFunc Instance = new TestWrapSeqFunc();

    private TestWrapSeqFunc()
        : base("WrapSeq", 1, 1)
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

        var type = info.Args[0].Type;
        EnsureTypeSeq(ref type);
        return (type, Immutable.Array.Create(type));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        if (!type.IsSequence)
            return false;
        if (call.Args[0].Type != type)
            return false;
        return true;
    }
}

/// <summary>
/// Test functions to wrap sequences in wrappers exposing various collection functionality.
/// This facilitates getting code coverage for sequence functions.
/// <list>
/// <item><see cref="CantCount"/>: Wraps a sequence in a wrapper that doesn't implement <see cref="ICanCount"/>
/// but does implement <see cref="ICachingEnumerable"/>.</item>
/// <item><see cref="LazyCount"/>: Wraps a sequence in a wrapper that only implements <see cref="ICanCount"/>
/// and doesn't ever cache its count. This helps with coverage for <see cref="ICanCount.GetCount(Action)"/> when
/// <see cref="ICanCount.TryGetCount(out long)"/> fails.</item>
/// <item><see cref="WrapList"/>: Wraps a sequence in a wrapper that implements <see cref="IList{T}"/>.</item>
/// <item><see cref="WrapColl"/>: Wraps a sequence in a wrapper that only implements <see cref="IReadOnlyCollection{T}"/>.</item>
/// <item><see cref="WrapArr"/>: Wraps a sequence in an array.</item>
/// <item><see cref="WrapCurs"/>: Wraps a sequence in a wrapper that only implements <see cref="ICursorable{T}"/>.</item>
/// </list>
/// </summary>
public sealed partial class TestWrapCollFunc : TestFunc
{
    public static readonly TestWrapCollFunc CantCount = new TestWrapCollFunc("CantCount");
    public static readonly TestWrapCollFunc LazyCount = new TestWrapCollFunc("LazyCount");
    public static readonly TestWrapCollFunc WrapList = new TestWrapCollFunc("WrapList");
    public static readonly TestWrapCollFunc WrapColl = new TestWrapCollFunc("WrapColl");
    public static readonly TestWrapCollFunc WrapArr = new TestWrapCollFunc("WrapArr");
    public static readonly TestWrapCollFunc WrapCurs = new TestWrapCollFunc("WrapCurs");

    private TestWrapCollFunc(string name)
        : base(name, 1, 1)
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

        var type = info.Args[0].Type;
        EnsureTypeSeq(ref type);
        return (type, Immutable.Array.Create(type));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        if (!type.IsSequence)
            return false;
        if (call.Args[0].Type != type)
            return false;
        return true;
    }
}

/// <summary>
/// Performs three-level nested iteration with each level being defined respectively by a
/// <see cref="ScopeKind.Range"/>, a <see cref="ScopeKind.SeqItem"/>, and a <see cref="ScopeKind.Range"/>
/// scope. Returns the sequence whose items are produced by the inner selector.
/// This facilitates getting coverage for range scope index behavior, since none of the
/// scopes' indices can be mapped to each other.
/// REVIEW: This says it is "unbounded" but really shouldn't be. Should we change it?
/// </summary>
public sealed partial class TestRngSeqFunc : TestFunc
{
    public static readonly TestRngSeqFunc Instance = new TestRngSeqFunc();

    private TestRngSeqFunc()
        : base("RngSeq", 4, 4)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        return new ArgTraitsImpl(this);
    }

    private sealed class ArgTraitsImpl : ArgTraitsBare
    {
        public override int ScopeCount => 3;

        public override int ScopeIndexCount => 1;

        public override int NestedCount => 1;

        public ArgTraitsImpl(TestRngSeqFunc func)
            : base(func, 4)
        {
        }

        public override bool AreEquivalent(ArgTraits cmp)
        {
            return cmp is ArgTraitsImpl;
        }

        public override ScopeKind GetScopeKind(int slot)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
            return (slot switch
            {
                0 => ScopeKind.Range,
                1 => ScopeKind.SeqItem,
                2 => ScopeKind.Range,
                _ => ScopeKind.None,
            });
        }

        public override bool IsNested(int slot)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
            return slot == 3;
        }

        public override bool IsScope(int slot)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
            return slot <= 2;
        }

        public override bool IsScope(int slot, out int iscope)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
            if (slot <= 2)
            {
                iscope = slot;
                return true;
            }
            iscope = -1;
            return false;
        }

        public override bool IsScope(int slot, out int iscope, out int iidx, out bool firstForIdx)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));

            switch (slot)
            {
            case 0:
            case 2:
                iscope = slot;
                iidx = -1;
                firstForIdx = false;
                return true;

            case 1:
                iscope = 1;
                iidx = 0;
                firstForIdx = true;
                return true;

            default:
                iscope = -1;
                iidx = -1;
                firstForIdx = false;
                return false;
            }
        }

        public override bool IsScopeActive(int slot, int upCount)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
            Validation.BugCheckIndex(upCount, ScopeCount, nameof(upCount));
            return slot == 3;
        }
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var types = info.GetArgTypes();
        types[0] = DType.I8Req;
        EnsureTypeSeq(types, 1);
        types[2] = DType.I8Req;
        return (types[3].ToSequence(), types.ToImmutable());
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        Validation.AssertValue(call);
        if (call.Args.Length != 4)
            return false;
        if (call.Args[0].Type != DType.I8Req)
            return false;
        if (!call.Args[1].Type.IsSequence)
            return false;
        if (call.Args[2].Type != DType.I8Req)
            return false;
        if (!call.Type.IsSequence)
            return false;
        if (call.Type != call.Args[3].Type.ToSequence())
            return false;
        if (call.Scopes.Length != 3)
            return false;
        if (call.Indices.Length != 1)
            return false;
        return true;
    }

    protected override bool IsUnboundedCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));
        return true;
    }
}

/// <summary>
/// These functions are for testing arity-based prioritization of method overloads.
/// </summary>
public sealed class ArityTestFunc : RexlOper
{
    public static readonly ArityTestFunc Arity_1_7 = new ArityTestFunc("N17", 1, 7);
    public static readonly ArityTestFunc Arity_2_6 = new ArityTestFunc("N26", 2, 6);

    private readonly int[] _arities;

    private ArityTestFunc(string ns, params int[] arities)
        : base(isFunc: true, new DName("Arity"), NPath.Root.Append(new DName(ns)), arities[0], arities[^1])
    {
        _arities = arities;
    }

    public override bool SupportsArity(int arity)
    {
        return _arities.Contains(arity);
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

        return (DType.Text, info.GetArgTypes().ToImmutable());
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        Validation.AssertValue(call);
        if (call.Type != DType.Text)
            return false;
        full = false;
        return true;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));
        return BndStrNode.Create(Path.ToDottedSyntax());
    }
}

/// <summary>
/// This is for testing "pulling withs" during reduction. It is like `With` but not special
/// and doesn't support code gen.
/// </summary>
public sealed class TestWith : TestFunc
{
    public static readonly TestWith Instance = new TestWith();

    private TestWith()
        : base("With", 2, int.MaxValue)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        return ArgTraitsWith.Create(this, carg, false, false);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var types = info.GetArgTypes();
        return (types[types.Count - 1], types.ToImmutable());
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Type != call.Args[call.Args.Length - 1].Type)
            return false;
        // Doesn't implement code gen - no need at this point.
        full = false;
        return true;
    }
}

/// <summary>
/// Volatile function that causes a ping and returns the current ping count.
/// </summary>
public sealed class PingFunc : TestFunc
{
    public static readonly PingFunc Instance = new PingFunc();

    private PingFunc()
        : base("Ping", 0, 0)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        return ArgTraitsSimple.Create(this, false, 0);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(info.Arity == 0);

        return (DType.I8Req, Immutable.Array<DType>.Empty);
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Type != DType.I8Req)
            return false;
        return true;
    }

    public override bool IsVolatile(ArgTraits traits, DType type,
        Immutable.Array<BoundNode> args, Immutable.Array<ArgScope> scopes, Immutable.Array<ArgScope> indices,
        Immutable.Array<Directive> dirs, Immutable.Array<DName> names)
    {
        return true;
    }
}

/// <summary>
/// Function that throws an exception when executed.
/// </summary>
public sealed class ThrowFunc : TestFunc
{
    public static readonly ThrowFunc Instance = new ThrowFunc();

    private ThrowFunc()
        : base(new DName("Throw"), 0, 0)
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
        Validation.Assert(info.Arity == 0);

        return (DType.Vac, Immutable.Array<DType>.Empty);
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Type != DType.Vac)
            return false;
        return true;
    }
}
