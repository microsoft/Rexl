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

internal sealed partial class TestOperations : OperationRegistry
{
    public static readonly TestOperations Instance = new TestOperations();

    private TestOperations()
        : base(BuiltinFunctions.Instance, BuiltinProcedures.Instance, FlowProcs.Instance)
    {
#if WITH_ONNX
        AddParent(Microsoft.Rexl.Onnx.ModelFunctions.Instance);
#endif

        AddBoth(CastGenFunc.Raw);
        AddBoth(CastGenFunc.Lift);

        AddBoth(WrapFunc.Instance);
        AddBoth(WrapFunc.NYI);
        AddBoth(WrapLogFunc.Instance);
        AddBoth(WrapCallCtxFunc.Instance);
        AddBoth(WrapCallSvcFromHostFunc.Instance);

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

        // For testing MultiFormFunc.
        AddBoth(TestMultiFormFunc.MultiDSMA);
        AddBoth(TestMultiFormFunc.MultiDSMB);
        AddBoth(TestMultiFormFunc.MultiDsMB);
        AddBoth(TestMultiFormFunc.MultiDS_A);
        AddBoth(TestMultiFormFunc.MultiDS_B);
        AddBoth(TestMultiFormFunc.Multi_SMB);
        AddBoth(TestMultiFormFunc.MultiDSMBab);
        AddBoth(TestMultiFormFunc.MultiDS_Bab);
        AddBoth(TestMultiFormFunc.MultiDSMC);
        AddBoth(TestMultiFormFunc.MultiDSMCc);
        AddBoth(TestMultiFormFunc.MultiDSMBb);
        AddBoth(TestMultiFormFunc.MultiDRMB);
        AddBoth(TestMultiFormFunc.MultiDrMB);
        AddBoth(TestMultiFormFunc.MultiDfMB);
        AddBoth(TestMultiFormFunc.MultiDSMBW);
        AddBoth(TestMultiFormFunc.MultiDS_BW);
        AddBoth(TestMultiFormFunc.MultiDRMBW);
        AddBoth(TestMultiFormFunc.MultiDGSMA);
        AddBoth(TestMultiFormFunc.MultiDGSMB);
        AddBoth(TestMultiFormFunc.MultiDGsMB);
        AddBoth(TestMultiFormFunc.MultiDGS_A);
        AddBoth(TestMultiFormFunc.MultiDGS_B);
        AddBoth(TestMultiFormFunc.Multi_GSMB);
        AddBoth(TestMultiFormFunc.MultiDGSMBab);
        AddBoth(TestMultiFormFunc.MultiDGS_Bab);
        AddBoth(TestMultiFormFunc.MultiDGSMC);
        AddBoth(TestMultiFormFunc.MultiDGSMCc);
        AddBoth(TestMultiFormFunc.MultiDGSMBb);
        AddBoth(TestMultiFormFunc.MultiDGRMB);
        AddBoth(TestMultiFormFunc.MultiDGrMB);
        AddBoth(TestMultiFormFunc.MultiDGfMB);
        AddBoth(TestMultiFormFunc.MultiDGSMBW);
        AddBoth(TestMultiFormFunc.MultiDGS_BW);
        AddBoth(TestMultiFormFunc.MultiDGRMBW);
        AddBoth(TestMultiFormFunc.MultiS_DSMA_AD);
        AddBoth(TestMultiFormFunc.MultiS_DSMB_ABD);
        AddBoth(TestMultiFormFunc.MultiS_DSMC_ACD);
        AddBoth(TestMultiFormFunc.MultiS_DRMB_ABD12);
        AddBoth(TestMultiFormFunc.MultiS_DfMB_ABDM12);
        AddBoth(TestMultiFormFunc.MultiS_DsMB_ABDM);
        AddBoth(TestMultiFormFunc.MultiS_DrMB_ABDM12);
        AddBoth(TestMultiFormFunc.MultiS_DGSMA_AGD);
        AddBoth(TestMultiFormFunc.MultiS_DGSMB_ABGD);
        AddBoth(TestMultiFormFunc.MultiS_DGRMB_ABGD12);
        AddBoth(TestMultiFormFunc.MultiS_DGfMB_ABGDM12);
        AddBoth(TestMultiFormFunc.MultiS_DGsMB_ABGDM);
        AddBoth(TestMultiFormFunc.MultiS_DGrMB_ABGDM12);
        AddBoth(TestMultiFormFunc.ManyDRMB);
        AddBoth(TestMultiFormFunc.ManyDRM);
        AddBoth(TestMultiFormFunc.ManyDRM_Def);
        AddBoth(TestMultiFormFunc.MultiDSMAM);
        AddBoth(TestMultiFormFunc.MultiDSMAm);
        AddBoth(TestMultiFormFunc.MultiDGSMAM);
        AddBoth(TestMultiFormFunc.MultiDGSMAm);

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

        // For testing MultiFormProc.
        AddBoth(TestMultiFormProc.MultiDSMA);
        AddBoth(TestMultiFormProc.MultiDSMB);
        AddBoth(TestMultiFormProc.MultiDsMB);
        AddBoth(TestMultiFormProc.MultiDS_A);
        AddBoth(TestMultiFormProc.MultiDS_B);
        AddBoth(TestMultiFormProc.Multi_SMB);
        AddBoth(TestMultiFormProc.MultiDSMBab);
        AddBoth(TestMultiFormProc.MultiDSMC);
        AddBoth(TestMultiFormProc.MultiDSMCc);
        AddBoth(TestMultiFormProc.MultiDSMBb);
        AddBoth(TestMultiFormProc.MultiDRMB);
        AddBoth(TestMultiFormProc.MultiDrMB);
        AddBoth(TestMultiFormProc.MultiDfMB);
        AddBoth(TestMultiFormProc.MultiDSMBW);
        AddBoth(TestMultiFormProc.MultiDRMBW);
        AddBoth(TestMultiFormProc.MultiDGSMA);
        AddBoth(TestMultiFormProc.MultiDGSMB);
        AddBoth(TestMultiFormProc.MultiDGsMB);
        AddBoth(TestMultiFormProc.MultiDGS_A);
        AddBoth(TestMultiFormProc.MultiDGS_B);
        AddBoth(TestMultiFormProc.Multi_GSMB);
        AddBoth(TestMultiFormProc.MultiDGSMBab);
        AddBoth(TestMultiFormProc.MultiDGSMC);
        AddBoth(TestMultiFormProc.MultiDGSMCc);
        AddBoth(TestMultiFormProc.MultiDGSMBb);
        AddBoth(TestMultiFormProc.MultiDGRMB);
        AddBoth(TestMultiFormProc.MultiDGrMB);
        AddBoth(TestMultiFormProc.MultiDGfMB);
        AddBoth(TestMultiFormProc.MultiDGSMBW);
        AddBoth(TestMultiFormProc.MultiDGRMBW);
        AddBoth(TestMultiFormProc.MultiS_DSMA_AD);
        AddBoth(TestMultiFormProc.MultiS_DSMB_ABD);
        AddBoth(TestMultiFormProc.MultiS_DSMC_ACD);
        AddBoth(TestMultiFormProc.MultiS_DRMB_ABD12);
        AddBoth(TestMultiFormProc.MultiS_DfMB_ABDM12);
        AddBoth(TestMultiFormProc.MultiS_DsMB_ABDM);
        AddBoth(TestMultiFormProc.MultiS_DrMB_ABDM12);
        AddBoth(TestMultiFormProc.MultiS_DGSMA_AGD);
        AddBoth(TestMultiFormProc.MultiS_DGSMB_ABGD);
        AddBoth(TestMultiFormProc.MultiS_DGRMB_ABGD12);
        AddBoth(TestMultiFormProc.MultiS_DGfMB_ABGDM12);
        AddBoth(TestMultiFormProc.MultiS_DGsMB_ABGDM);
        AddBoth(TestMultiFormProc.MultiS_DGrMB_ABGDM12);
        AddBoth(TestMultiFormProc.ManyDRMB);
        AddBoth(TestMultiFormProc.ManyDRM);
        AddBoth(TestMultiFormProc.ManyDRM_Def);
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
internal abstract class TestFunc : RexlOper
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
internal sealed class CastGenFunc : TestFunc
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
internal sealed class WrapFunc : TestFunc
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
internal sealed class WrapLogFunc : TestFunc
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
internal sealed class WrapCallCtxFunc : TestFunc
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
/// Issues a call to an <see cref="IService"/> loaded from the code gen host. Returns the input value.
/// </summary>
internal sealed class WrapCallSvcFromHostFunc : TestFunc
{
    public interface IService
    {
#if WITH_CODE
        void Call<T>(T value, Microsoft.Rexl.Code.ExecCtx ctx, int id);
#endif
    }

    public static readonly WrapCallSvcFromHostFunc Instance = new WrapCallSvcFromHostFunc();

    private WrapCallSvcFromHostFunc()
        : base("WrapCallSvcFromHost", 1, 1)
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
internal sealed partial class FirstNRevFunc : TestFunc
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
internal sealed partial class DblMapFunc : TestFunc
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
/// This function is used (via script/baseline tests) to exercise the MultiFormFunc base class. Its various
/// rexl func instances satisfy the following:
/// 
/// * Take a main input sequence (mis) of table type with field A:s and perhaps B:s or C:{ D:s, E:s }*.
/// * Produce a main output sequence (mos) of type { B:s* }*.
/// * Some take settings of type { S1:s, S2:i8 }. Those that don't use values "" for S1 and long.MaxValue for S2.
/// 
/// They map over the mis, processing the fields of the mis item into an s* value, which is then wrapped in a record.
/// Funcs vary in that they:
/// * May or may not support the direct single-arg invocation form F(input).
/// * May or may not have default values for the fields of the mis.
/// * May have input type the same as the mis type or may have an input record containing mis and settings.
/// * May or may not support merging the mis and mos.
/// 
/// The actual "core item computation" isn't really important, but here's what it does:
/// * If either B or C are included, define a new variable X which is B or C's length (as a string) if the other is not included,
///   or their concatenation if both are.
/// * For either [A] or [A, X], take the field value, truncate it to at most S2 characters,
///   append the string S1, and prepend the field name.
/// * Wrap those values in a sequence.
/// * Wrap that sequence in a record, with field name "B".
/// 
/// See the MultiFormFunc.txt scripts and baselines for the test cases and results.
/// 
/// REVIEW: This class should be split up. It's too large now.
/// </summary>
internal abstract partial class TestMultiFormOper : MultiFormOper<MultiFormFuncTypes>
{
    public static readonly DName _nameA = new DName("A");
    public static readonly DName _nameB = new DName("B");
    public static readonly DName _nameC = new DName("C");
    public static readonly DName _nameG = new DName("G");
    public static readonly DName _nameD = new DName("D");
    public static readonly DName _nameE = new DName("E");
    public static readonly DName _nameMis = new DName("Mis");
    public static readonly DName _nameMos = new DName("Mos");
    public static readonly DName _nameS1 = new DName("S1");
    public static readonly DName _nameS2 = new DName("S2");

    public static readonly DType _typeABase = DType.Text;
    public static readonly DType _typeBBase = DType.Text;
    public static readonly DType _typeCBase = DType.CreateRecord(false, new TypedName(_nameD, DType.Text), new TypedName(_nameE, DType.Text)).ToSequence();
    public static readonly DType _typeGBase = DType.General;
    public static readonly DType _typeS1Base = DType.Text;
    public static readonly DType _typeS2Base = DType.I8Req;

    protected const bool Y = true;
    protected const bool n = false;

    protected sealed class Settings
    {
        public bool dir;
        public bool mrg;
        public bool withB;
        public bool withC;
        public uint withS1S2;
        public bool outRec;

        public bool withG = false;
        public bool expSel = true;
        public bool inFld = false;
        public bool hasDefA = true;
        public bool hasDefB = true;
        public bool hasDefC = true;
        public bool hasDefS1 = true;
        public bool hasDefS2 = true;

        public bool singleA = false;
        public bool singleB = false;
        public bool singleC = false;
        public bool singleG = false;
        public bool singleDir = false;
        public bool singleSel = false;
        public bool singleS1 = false;
        public bool singleS2 = false;

        public bool s2First = false;

        public bool oneMany = false;
        public bool mosUseDef = false;
    }

    protected TestMultiFormOper(bool isFunc, DName name, NPath ns, int arityMin, int arityMax, Immutable.Array<InvocationForm> forms)
        : base(isFunc, name, ns, arityMin, arityMax, forms, null)
    {
    }
}

internal sealed class TestMultiFormFunc : TestMultiFormOper
{
    // These names have the form Multi[D|_][G][S|s|R|r|f][M|_][A|B|C]... with:
    // * [D|_] indicating whether the direct simple form is supported: F(<arg>).
    // * [G] indicating that there is a field G in the mis.
    // * [S|s|R|r|f] indicating whether the input type is a sequence or record. Little s/r indicates that form 3/5 should be used
    //     instead of form 4/6. Little f indicates that form 2 should be used.
    // * [M|_] indicating whether merging is supported.
    // * [A|B|C] indicating whether the mis type is { A:s }* or { A:s, B:s }* or { A:s, C:{ D:s, E:s }* }*.
    // * [|a|b|c|ab] indicating that the given fields are required (not optional).
    // * [|W] indicating that mos is dst or that mos is wrapped in a dst record, together with the settings (echoed).
    // * [M|m|_] a second time indicating if it's one to many and if so, whether merging is inner (no default value)
    //   or outer (with default value) on empty outputs.
    public static readonly TestMultiFormFunc MultiDSMA = Create(nameof(MultiDSMA), new Settings { dir = Y, mrg = Y, withB = n, withC = n, withS1S2 = 0, outRec = n });
    public static readonly TestMultiFormFunc MultiDSMB = Create(nameof(MultiDSMB), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0, outRec = n });
    public static readonly TestMultiFormFunc MultiDsMB = Create(nameof(MultiDsMB), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0, outRec = n, expSel = n });
    public static readonly TestMultiFormFunc MultiDS_A = Create(nameof(MultiDS_A), new Settings { dir = Y, mrg = n, withB = n, withC = n, withS1S2 = 0, outRec = n });
    public static readonly TestMultiFormFunc MultiDS_B = Create(nameof(MultiDS_B), new Settings { dir = Y, mrg = n, withB = Y, withC = n, withS1S2 = 0, outRec = n });
    public static readonly TestMultiFormFunc Multi_SMB = Create(nameof(Multi_SMB), new Settings { dir = n, mrg = Y, withB = Y, withC = n, withS1S2 = 0, outRec = n });
    public static readonly TestMultiFormFunc MultiDSMBb = Create(nameof(MultiDSMBb), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0, outRec = n, hasDefB = n });
    public static readonly TestMultiFormFunc MultiDSMBab = Create(nameof(MultiDSMBab), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0, outRec = n, hasDefA = n, hasDefB = n });
    public static readonly TestMultiFormFunc MultiDS_Bab = Create(nameof(MultiDS_Bab), new Settings { dir = Y, mrg = n, withB = Y, withC = n, withS1S2 = 0, outRec = n, hasDefA = n, hasDefB = n });
    public static readonly TestMultiFormFunc MultiDSMC = Create(nameof(MultiDSMC), new Settings { dir = Y, mrg = Y, withB = n, withC = Y, withS1S2 = 0, outRec = n, hasDefB = n });
    public static readonly TestMultiFormFunc MultiDSMCc = Create(nameof(MultiDSMCc), new Settings { dir = Y, mrg = Y, withB = n, withC = Y, withS1S2 = 0, outRec = n, hasDefB = n, hasDefC = n });
    public static readonly TestMultiFormFunc MultiDRMB = Create(nameof(MultiDRMB), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x3, outRec = n });
    public static readonly TestMultiFormFunc MultiDrMB = Create(nameof(MultiDrMB), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x3, outRec = n, expSel = n });
    public static readonly TestMultiFormFunc MultiDfMB = Create(nameof(MultiDfMB), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x3, outRec = n, inFld = Y });
    public static readonly TestMultiFormFunc MultiDSMBW = Create(nameof(MultiDSMBW), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0, outRec = Y });
    public static readonly TestMultiFormFunc MultiDS_BW = Create(nameof(MultiDS_BW), new Settings { dir = Y, mrg = n, withB = Y, withC = n, withS1S2 = 0, outRec = Y });
    public static readonly TestMultiFormFunc MultiDRMBW = Create(nameof(MultiDRMBW), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x3, outRec = Y });

    public static readonly TestMultiFormFunc MultiDGSMA = Create(nameof(MultiDGSMA), new Settings { dir = Y, mrg = Y, withB = n, withC = n, withS1S2 = 0, outRec = n, withG = Y });
    public static readonly TestMultiFormFunc MultiDGSMB = Create(nameof(MultiDGSMB), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0, outRec = n, withG = Y });
    public static readonly TestMultiFormFunc MultiDGsMB = Create(nameof(MultiDGsMB), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0, outRec = n, withG = Y, expSel = n });
    public static readonly TestMultiFormFunc MultiDGS_A = Create(nameof(MultiDGS_A), new Settings { dir = Y, mrg = n, withB = n, withC = n, withS1S2 = 0, outRec = n, withG = Y });
    public static readonly TestMultiFormFunc MultiDGS_B = Create(nameof(MultiDGS_B), new Settings { dir = Y, mrg = n, withB = Y, withC = n, withS1S2 = 0, outRec = n, withG = Y });
    public static readonly TestMultiFormFunc Multi_GSMB = Create(nameof(Multi_GSMB), new Settings { dir = n, mrg = Y, withB = Y, withC = n, withS1S2 = 0, outRec = n, withG = Y });
    public static readonly TestMultiFormFunc MultiDGSMBb = Create(nameof(MultiDGSMBb), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0, outRec = n, withG = Y, hasDefB = n });
    public static readonly TestMultiFormFunc MultiDGSMBab = Create(nameof(MultiDGSMBab), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0, outRec = n, withG = Y, hasDefA = n, hasDefB = n });
    public static readonly TestMultiFormFunc MultiDGS_Bab = Create(nameof(MultiDGS_Bab), new Settings { dir = Y, mrg = n, withB = Y, withC = n, withS1S2 = 0, outRec = n, withG = Y, hasDefA = n, hasDefB = n });
    public static readonly TestMultiFormFunc MultiDGSMC = Create(nameof(MultiDGSMC), new Settings { dir = Y, mrg = Y, withB = n, withC = Y, withS1S2 = 0, outRec = n, withG = Y, hasDefB = n });
    public static readonly TestMultiFormFunc MultiDGSMCc = Create(nameof(MultiDGSMCc), new Settings { dir = Y, mrg = Y, withB = n, withC = Y, withS1S2 = 0, outRec = n, withG = Y, hasDefB = n, hasDefC = n });
    public static readonly TestMultiFormFunc MultiDGRMB = Create(nameof(MultiDGRMB), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x3, outRec = n, withG = Y });
    public static readonly TestMultiFormFunc MultiDGrMB = Create(nameof(MultiDGrMB), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x3, outRec = n, withG = Y, expSel = n });
    public static readonly TestMultiFormFunc MultiDGfMB = Create(nameof(MultiDGfMB), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x3, outRec = n, withG = Y, inFld = Y });
    public static readonly TestMultiFormFunc MultiDGSMBW = Create(nameof(MultiDGSMBW), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0, outRec = Y, withG = Y });
    public static readonly TestMultiFormFunc MultiDGS_BW = Create(nameof(MultiDGS_BW), new Settings { dir = Y, mrg = n, withB = Y, withC = n, withS1S2 = 0, outRec = Y, withG = Y });
    public static readonly TestMultiFormFunc MultiDGRMBW = Create(nameof(MultiDGRMBW), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x3, outRec = Y, withG = Y });

    public static readonly TestMultiFormFunc MultiDSMAM = Create(nameof(MultiDSMAM), new Settings { dir = Y, mrg = Y, withB = n, withC = n, withS1S2 = 0, outRec = n, oneMany = Y, mosUseDef = Y });
    public static readonly TestMultiFormFunc MultiDSMAm = Create(nameof(MultiDSMAm), new Settings { dir = Y, mrg = Y, withB = n, withC = n, withS1S2 = 0, outRec = n, oneMany = Y, mosUseDef = n });
    public static readonly TestMultiFormFunc MultiDGSMAM = Create(nameof(MultiDGSMAM), new Settings { dir = Y, mrg = Y, withB = n, withC = n, withS1S2 = 0, outRec = n, oneMany = Y, mosUseDef = Y, withG = Y });
    public static readonly TestMultiFormFunc MultiDGSMAm = Create(nameof(MultiDGSMAm), new Settings { dir = Y, mrg = Y, withB = n, withC = n, withS1S2 = 0, outRec = n, oneMany = Y, mosUseDef = n, withG = Y });

    // These names have the form MultiS_[*]_[A][B][C][G][D][M][1][2].
    // When each flag is set:
    // A - allow single to sequence on the A field when it is a slot and increases its seq count by 1.
    // B - allow single to sequence on the B field when it is a slot and increases its seq count by 1.
    // C - allow single to sequence on the C field when it is a slot and increases its seq count by 1.
    // G - allow single to sequence on the G field when it is a slot and increases its seq count by 1.
    // D - allow single to sequence on the singular argument in the direct form (1).
    // M - allow single to sequence on the singular selector argument in forms (3) and (5).
    // 1 - allow single to sequence on the S1 setting when it is a slot and increases its seq count by 1.
    // 2 - allow single to sequence on the S2 setting when it is a slot and increases its seq count by 1.
    // Otherwise the function has the same semantics as indicated by above within the [*].
    public static readonly TestMultiFormFunc MultiS_DSMA_AD = Create(nameof(MultiS_DSMA_AD), new Settings { dir = Y, mrg = Y, withB = n, withC = n, withS1S2 = 0, outRec = n, singleA = Y, singleDir = Y });
    public static readonly TestMultiFormFunc MultiS_DSMB_ABD = Create(nameof(MultiS_DSMB_ABD), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0, outRec = n, singleA = Y, singleB = Y, singleDir = Y });
    public static readonly TestMultiFormFunc MultiS_DSMC_ACD = Create(nameof(MultiS_DSMC_ACD), new Settings { dir = Y, mrg = Y, withB = n, withC = Y, withS1S2 = 0, outRec = n, hasDefB = n, singleA = Y, singleC = Y, singleDir = Y });
    public static readonly TestMultiFormFunc MultiS_DRMB_ABD12 = Create(nameof(MultiS_DRMB_ABD12), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x3, outRec = n, singleA = Y, singleB = Y, singleS1 = Y, singleS2 = Y });
    public static readonly TestMultiFormFunc MultiS_DfMB_ABDM12 = Create(nameof(MultiS_DfMB_ABDM12), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x3, outRec = n, inFld = Y, singleA = Y, singleB = Y, singleSel = Y, singleS1 = Y, singleS2 = Y });
    public static readonly TestMultiFormFunc MultiS_DsMB_ABDM = Create(nameof(MultiS_DsMB_ABDM), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0, outRec = n, expSel = n, singleA = Y, singleB = Y, singleDir = Y, singleSel = Y });
    public static readonly TestMultiFormFunc MultiS_DrMB_ABDM12 = Create(nameof(MultiS_DrMB_ABDM12), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x3, outRec = n, expSel = n, singleA = Y, singleB = Y, singleDir = Y, singleSel = Y, singleS1 = Y, singleS2 = Y });

    public static readonly TestMultiFormFunc MultiS_DGSMA_AGD = Create(nameof(MultiS_DGSMA_AGD), new Settings { dir = Y, mrg = Y, withB = n, withC = n, withS1S2 = 0, outRec = n, withG = Y, singleA = Y, singleG = Y, singleDir = Y });
    public static readonly TestMultiFormFunc MultiS_DGSMB_ABGD = Create(nameof(MultiS_DGSMB_ABGD), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0, outRec = n, withG = Y, singleA = Y, singleB = Y, singleG = Y, singleDir = Y });
    public static readonly TestMultiFormFunc MultiS_DGRMB_ABGD12 = Create(nameof(MultiS_DGRMB_ABGD12), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x3, outRec = n, withG = Y, singleA = Y, singleB = Y, singleG = Y, singleS1 = Y, singleS2 = Y });
    public static readonly TestMultiFormFunc MultiS_DGfMB_ABGDM12 = Create(nameof(MultiS_DGfMB_ABGDM12), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x3, outRec = n, withG = Y, inFld = Y, singleA = Y, singleB = Y, singleG = Y, singleSel = Y, singleS1 = Y, singleS2 = Y });
    public static readonly TestMultiFormFunc MultiS_DGsMB_ABGDM = Create(nameof(MultiS_DGsMB_ABGDM), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0, outRec = n, withG = Y, expSel = n, singleA = Y, singleB = Y, singleG = Y, singleDir = Y, singleSel = Y });
    public static readonly TestMultiFormFunc MultiS_DGrMB_ABGDM12 = Create(nameof(MultiS_DGrMB_ABGDM12), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x3, outRec = n, withG = Y, expSel = n, singleA = Y, singleB = Y, singleG = Y, singleDir = Y, singleSel = Y, singleS1 = Y, singleS2 = Y });

    // Functions with multiple overloads of the form ManyXXXX, where XXXX is a common setting
    // shared by all of the overloads.
    public static readonly TestMultiFormFunc ManyDRMB = Create(nameof(ManyDRMB),
        new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x0, outRec = n, hasDefS1 = n, hasDefS2 = n },
        new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x1, outRec = n, hasDefS1 = n, hasDefS2 = n },
        new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x2, outRec = n, hasDefS1 = n, hasDefS2 = n },
        new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x3, outRec = n, hasDefS1 = n, hasDefS2 = n },
        new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x3, outRec = n, s2First = Y, hasDefS1 = n, hasDefS2 = n });
    public static readonly TestMultiFormFunc ManyDRM = Create(nameof(ManyDRM),
        new Settings { dir = Y, mrg = Y, withB = n, withC = n, withS1S2 = 0x0, outRec = n, hasDefS1 = n, hasDefS2 = n },
        new Settings { dir = Y, mrg = Y, withB = n, withC = n, withS1S2 = 0x1, outRec = n, hasDefS1 = n, hasDefS2 = n },
        new Settings { dir = Y, mrg = Y, withB = n, withC = n, withS1S2 = 0x2, outRec = n, hasDefS1 = n, hasDefS2 = n },
        new Settings { dir = Y, mrg = Y, withB = n, withC = n, withS1S2 = 0x3, outRec = n, hasDefS1 = n, hasDefS2 = n },
        new Settings { dir = Y, mrg = Y, withB = n, withC = n, withS1S2 = 0x3, outRec = n, s2First = Y, hasDefS1 = n, hasDefS2 = n },
        new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x0, outRec = n, hasDefB = n, hasDefS1 = n, hasDefS2 = n },
        new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x1, outRec = n, hasDefB = n, hasDefS1 = n, hasDefS2 = n },
        new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x2, outRec = n, hasDefB = n, hasDefS1 = n, hasDefS2 = n },
        new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x3, outRec = n, hasDefB = n, hasDefS1 = n, hasDefS2 = n },
        new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x3, outRec = n, s2First = Y, hasDefB = n, hasDefS1 = n, hasDefS2 = n });
    // Same as above but testing default values.
    public static readonly TestMultiFormFunc ManyDRM_Def = Create(nameof(ManyDRM_Def),
        new Settings { dir = Y, mrg = Y, withB = n, withC = n, withS1S2 = 0x3, outRec = n },
        new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x3, outRec = n });

    internal TestMultiFormFunc(DName name, NPath ns, int arityMin, int arityMax, Immutable.Array<InvocationForm> forms)
        : base(isFunc: true, name, ns, arityMin, arityMax, forms)
    {
    }

    private static TestMultiFormFunc Create(string name, params Settings[] settings)
    {
        var bldrForms = Immutable.Array.CreateBuilder<InvocationForm>();
        foreach (var setting in settings)
            AddForms(bldrForms, setting);
        return CreateCore(new DName(name), bldrForms.ToImmutable());
    }

    private static TestMultiFormFunc CreateCore(DName name, Immutable.Array<InvocationForm> forms)
    {
        int arityMin = forms.Min(form => form.ArityMin);
        int arityMax = forms.Max(form => form.ArityMax);
        return new TestMultiFormFunc(name, NPath.Root.Append(new DName("Test")), arityMin, arityMax, forms);
    }
}

partial class TestMultiFormOper
{
    protected static void AddForms(Immutable.Array<InvocationForm>.Builder bldrForms, Settings settings)
    {
        var typeA = settings.singleA ? _typeABase.ToSequence() : _typeABase;
        var typeB = settings.singleB ? _typeBBase.ToSequence() : _typeBBase;
        var typeC = settings.singleC ? _typeCBase.ToSequence() : _typeCBase;
        var typeG = settings.singleG ? _typeGBase.ToSequence() : _typeGBase;
        var typeS1 = settings.singleS1 ? _typeS1Base.ToSequence() : _typeS1Base;
        var typeS2 = settings.singleS2 ? _typeS2Base.ToSequence() : _typeS2Base;
        var types = new MultiFormFuncTypes(typeA, typeB, typeC, typeS1, typeS2);

        DType typeMis = DType.CreateTable(false, new TypedName(_nameA, typeA));
        if (settings.withB)
            typeMis = typeMis.AddNameType(_nameB, typeB);
        if (settings.withC)
            typeMis = typeMis.AddNameType(_nameC, typeC);
        if (settings.withG)
            typeMis = typeMis.AddNameType(_nameG, typeG);

        var bldrSlots = Immutable.Array.CreateBuilder<SlotInfo>();

        DType typeMos = DType.CreateTable(false, new TypedName(_nameB, _typeBBase.ToSequence()));
        DType typeIn;
        Immutable.Array<SlotInfo> slotsTop;
        var inRec = settings.withS1S2 > 0;
        var withS1 = (settings.withS1S2 & 0x1) > 0;
        var withS2 = (settings.withS1S2 & 0x2) > 0;
        if (!inRec)
        {
            typeIn = typeMis;
            slotsTop = default;
        }
        else
        {
            var tns = new List<TypedName>();
            tns.Add(new TypedName(_nameMis, settings.singleSel && !settings.expSel ? typeMis.ToSequence() : typeMis));
            if (withS1)
                tns.Add(new TypedName(_nameS1, typeS1));
            if (withS2)
                tns.Add(new TypedName(_nameS2, typeS2));
            typeIn = DType.CreateRecord(false, tns.ToArray());
            bldrSlots.Clear();
            if (settings.inFld)
                bldrSlots.Add(new SlotInfoReq(typeMis, _nameMis, settings.singleSel));

            SlotInfo slotS1;
            if (!settings.hasDefS1)
                slotS1 = new SlotInfoReq(settings.singleS1 ? DType.Text.ToSequence() : DType.Text, _nameS1, settings.singleS1);
            else
                slotS1 = settings.singleS1 ? (SlotInfo)new SlotInfoOptNull(DType.Text.ToSequence(), _nameS1, true) : (SlotInfo)new SlotInfoOptNonNull<string>(DType.Text, _nameS1, "/def", false);
            SlotInfo slotS2;
            if (!settings.hasDefS2)
                slotS2 = new SlotInfoReq(settings.singleS2 ? DType.I8Req.ToSequence() : DType.I8Req, _nameS2, settings.singleS2);
            else
                slotS2 = settings.singleS2 ? (SlotInfo)new SlotInfoOptNull(DType.I8Req.ToSequence(), _nameS2, true) : new SlotInfoOptNonNull<long>(DType.I8Req, _nameS2, 3, settings.singleS2);

            if (settings.s2First)
            {
                if (withS2)
                    bldrSlots.Add(slotS2);
                if (withS1)
                    bldrSlots.Add(slotS1);
            }
            else
            {
                if (withS1)
                    bldrSlots.Add(slotS1);
                if (withS2)
                    bldrSlots.Add(slotS2);
            }
            slotsTop = bldrSlots.ToImmutable();
        }

        DType typeOut;
        DName nameMos;
        if (settings.outRec)
        {
            nameMos = settings.mrg ? _nameMos : default;
            typeOut = DType.CreateRecord(false,
                new TypedName(_nameMos, typeMos),
                new TypedName(_nameS1, typeS1),
                new TypedName(_nameS2, typeS2));
        }
        else
        {
            nameMos = default;
            typeOut = typeMos;
        }

        MosKind mosKind;
        if (!settings.mrg)
            mosKind = MosKind.None;
        else if (!settings.oneMany)
            mosKind = MosKind.OneOne;
        else if (settings.mosUseDef)
            mosKind = MosKind.OneManyOuter;
        else
            mosKind = MosKind.OneManyInner;

        var hasMis = !(settings.inFld && inRec);

        if (settings.dir)
            bldrForms.Add(new SimpleForm(types, typeIn, typeOut, hasMis, mosKind, typeIn.IsSequence ? settings.singleDir : false, nameMos));
        if (!hasMis)
            bldrForms.Add(new RecFieldForm(types, typeIn, typeOut, slotsTop));
        else if (!settings.expSel)
        {
            if (!inRec)
                bldrForms.Add(new SeqForm(types, settings.singleSel ? typeIn.ToSequence() : typeIn, typeOut,
                    mosKind, settings.singleSel, nameMos));
            else
                bldrForms.Add(new RecSeqForm(types, typeIn, typeOut, _nameMis, slotsTop,
                    mosKind, settings.singleSel, nameMos));
        }
        else
        {
            bldrSlots.Clear();
            if (settings.withG)
                bldrSlots.Add(SlotInfo.Create(typeG, _nameG, false, null, settings.singleG));
            if (settings.withB)
                bldrSlots.Add(SlotInfo.Create(typeB, _nameB, settings.hasDefB, settings.hasDefB && !settings.singleB ? "B def" : null, settings.singleB));
            if (settings.withC)
                bldrSlots.Add(SlotInfo.Create(typeC, _nameC, settings.hasDefC, null, settings.singleC));
            bldrSlots.Add(settings.hasDefA ? !settings.singleA ? (SlotInfo)new SlotInfoOptNonNull<string>(typeA, _nameA, "A's default", settings.singleA) : new SlotInfoOptNull(typeA, _nameA, settings.singleA) : new SlotInfoReq(typeA, _nameA, settings.singleA));
            var slotsMis = bldrSlots.ToImmutable();
            if (!inRec)
                bldrForms.Add(new SeqRecForm(types, typeIn, typeOut, slotsMis, mosKind, nameMos));
            else
                bldrForms.Add(new RecSeqRecForm(types, typeIn, typeOut, _nameMis, slotsMis, slotsTop, mosKind, nameMos));
        }
    }
}

internal sealed class MultiFormFuncTypes
{
    public DType TypeA { get; }
    public DType TypeB { get; }
    public DType TypeC { get; }
    public DType TypeS1 { get; }
    public DType TypeS2 { get; }

    public MultiFormFuncTypes(DType typeA, DType typeB, DType typeC, DType typeS1, DType typeS2)
    {
        TypeA = typeA;
        TypeB = typeB;
        TypeC = typeC;
        TypeS1 = typeS1;
        TypeS2 = typeS2;
    }
}

/// <summary>
/// Wraps a sequence as an <see cref="IIndexedEnumerable{T}"/>.
/// </summary>
internal sealed partial class TestWrapSeqFunc : TestFunc
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
/// <item><see cref="WrapColl"/>: Wraps a sequence in a wrapper that only implements <see cref="ICollection{T}"/>.</item>
/// <item><see cref="WrapArr"/>: Wraps a sequence in an array.</item>
/// <item><see cref="WrapCurs"/>: Wraps a sequence in a wrapper that only implements <see cref="ICursorable{T}"/>.</item>
/// </list>
/// </summary>
internal sealed partial class TestWrapCollFunc : TestFunc
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
internal sealed partial class TestRngSeqFunc : TestFunc
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
internal sealed class ArityTestFunc : RexlOper
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
internal sealed class TestWith : TestFunc
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
internal sealed class PingFunc : TestFunc
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
internal sealed class ThrowFunc : TestFunc
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
