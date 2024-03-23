// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

/// <summary>
/// Base class for t-tests.
/// REVIEW: Yuen's trimmed means variants are not currently implemented.
/// REVIEW: Summarized variants? i.e. just from a mean and stdev per sample.
/// REVIEW: We currently specialize sample values into R8s. What about R4s and integers?
/// Though Math.NET only operates on doubles, we can still do better on the paired selector variant.
/// </summary>
public abstract partial class TTestBaseFunc : RexlOper
{
    private static readonly DName NameTTest = new DName("TTest");
    private static readonly NPath NsTTest = NPath.Root.Append(NameTTest);

    protected static readonly TypedName FieldPL = new TypedName("PL", DType.R8Req);
    protected static readonly TypedName FieldPR = new TypedName("PR", DType.R8Req);
    protected static readonly TypedName FieldP2 = new TypedName("P2", DType.R8Req);
    protected static readonly TypedName FieldDof = new TypedName("Dof", DType.R8Req);
    protected static readonly TypedName FieldStderr = new TypedName("Stderr", DType.R8Req);
    protected static readonly TypedName FieldT = new TypedName("T", DType.R8Req);

    private static readonly Immutable.Array<TypedName> FieldsBase = Immutable.Array<TypedName>.Create(
        FieldPL, FieldPR, FieldP2, FieldDof, FieldStderr, FieldT);

    protected static readonly TypedName FieldCount = new TypedName("Count", DType.I8Req);
    protected static readonly TypedName FieldMean = new TypedName("Mean", DType.R8Req);
    protected static readonly TypedName FieldVariance = new TypedName("Var", DType.R8Req);

    /// <summary>
    /// Additional fields to include in the output record when performing a test against a single sample.
    /// Note this also applies in the paired case, since it is equivalent to having a single sample of the
    /// paired differences.
    /// </summary>
    protected static readonly Immutable.Array<TypedName> FieldsExtraSingle = Immutable.Array<TypedName>.Create(
        FieldCount, FieldMean, FieldVariance);

    public Immutable.Array<TypedName> FieldsRes { get; }

    public DType TypeRes { get; }

    protected TTestBaseFunc(string name, int arityMin, int arityMax, Immutable.Array<TypedName> fieldsExtra)
        : base(isFunc: true, new DName(name), NsTTest, arityMin, arityMax)
    {
        FieldsRes = FieldsBase.AddRange(fieldsExtra);
        TypeRes = DType.CreateRecord(opt: false, FieldsRes);
    }

    protected override bool IsUnboundedCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));
        return true;
    }
}

/// <summary>
/// One-sample t-test. The first argument is the sample. The optional second argument is
/// the hypothesized population mean, which defaults to 0.
/// </summary>
public sealed partial class TTestOneSampleFunc : TTestBaseFunc
{
    public static readonly TTestOneSampleFunc Instance = new TTestOneSampleFunc();

    private TTestOneSampleFunc()
        : base("OneSample", 1, 2, FieldsExtraSingle)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: 0x1, maskLiftNeedsSeq: 0x1);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var type = (info.Args[0].Type.RootType.IsOpt ? DType.R8Opt : DType.R8Req).ToSequence();
        if (info.Arity == 1)
            return (TypeRes, Immutable.Array.Create(type));
        return (TypeRes, Immutable.Array.Create(type, DType.R8Req));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Type != TypeRes)
            return false;
        if (call.Args[0].Type.SeqCount != 1 || call.Args[0].Type.RootKind != DKind.R8)
            return false;
        if (call.Args.Length > 1 && call.Args[1].Type != DType.R8Req)
            return false;
        return true;
    }
}

/// <summary>
/// Two sample t-test, aka the independent sample t-test. The first two arguments are the samples.
/// 
/// The optional third argument specifies whether to assume equal population variances for the samples,
/// which affects the type of test performed:
///   * True- Student
///   * False- Welch
/// Defaults to false. This is because the loss in power is minimal for Welch when the variance is equal,
/// but this is not the case for Student with unequal variances. See the following paper for more info:
/// West RM. Best practice in statistics: Use the Welch t-test when testing the difference between two groups.
/// Annals of Clinical Biochemistry. 2021;58(4):267-269. doi:10.1177/0004563221992088
/// </summary>
public sealed partial class TTestTwoSampleFunc : TTestBaseFunc
{
    private static readonly string NameArgEqualVar = RexlStrings.TTestTwoSample_EqualVar.GetString();

    private static readonly TypedName FieldCountX = new TypedName("CountX", DType.I8Req);
    private static readonly TypedName FieldCountY = new TypedName("CountY", DType.I8Req);
    private static readonly TypedName FieldMeanX = new TypedName("MeanX", DType.R8Req);
    private static readonly TypedName FieldMeanY = new TypedName("MeanY", DType.R8Req);
    private static readonly TypedName FieldVarianceX = new TypedName("VarX", DType.R8Req);
    private static readonly TypedName FieldVarianceY = new TypedName("VarY", DType.R8Req);

    private static readonly Immutable.Array<TypedName> FieldsExtraTwoSample = Immutable.Array<TypedName>.Create(
        FieldCountX, FieldCountY, FieldMeanX, FieldMeanY, FieldVarianceX, FieldVarianceY);

    public static readonly TTestTwoSampleFunc Instance = new TTestTwoSampleFunc();

    private TTestTwoSampleFunc()
        : base("TwoSample", 2, 3, FieldsExtraTwoSample)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        return new ArgTraitsImpl(this, carg);
    }

    /// <summary>
    /// Lifts over sequence for the samples, leaving a final sequence count of one.
    /// Optionally allows a name for equal_var.
    /// REVIEW: The latter is not necessary if named argument mechanism
    /// limits concern of ArgTraits to positional arguments.
    /// </summary>
    private sealed class ArgTraitsImpl : ArgTraitsNoScopesBare
    {
        public override BitSet MaskLiftSeq => 0x3;

        public override BitSet MaskLiftNeedsSeq => 0x3;

        public ArgTraitsImpl(TTestTwoSampleFunc func, int carg)
            : base(func, carg)
        {
            Validation.Assert(func.SupportsArity(carg));
        }

        public override bool AreEquivalent(ArgTraits cmp)
        {
            if (cmp is not ArgTraitsImpl cmpImpl)
                return false;
            if (SlotCount != cmpImpl.SlotCount)
                return false;
            return true;
        }

        public override bool SupportsName(int slot)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
            return slot == 2;
        }
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);

        var types = info.GetArgTypes();
        Validation.Assert(SupportsArity(types.Count));

        types[0] = (types[0].RootType.IsOpt ? DType.R8Opt : DType.R8Req).ToSequence();
        types[1] = (types[1].RootType.IsOpt ? DType.R8Opt : DType.R8Req).ToSequence();

        if (types.Count > 2)
        {
            types[2] = DType.BitReq;

            // REVIEW: Motivates a more generalized named argument mechanism.
            // This will likely change to require the name.
            var name = info.Names.GetItemOrDefault(2);
            if (name.IsValid && name != NameArgEqualVar)
            {
                var tokName = info.NameTokens.GetItemOrDefault(2);
                info.PostDiagnostic(RexlDiagnostic.ErrorGuess(tokName, info.GetParseArg(2),
                    ErrorStrings.ErrBadName_Guess, NameArgEqualVar, tokName.Range, NameArgEqualVar));
            }
        }

        return (TypeRes, types.ToImmutable());
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Type != TypeRes)
            return false;

        var type0 = call.Args[0].Type;
        var type1 = call.Args[1].Type;

        if (type0.SeqCount != 1 || type0.RootKind != DKind.R8)
            return false;
        if (type1.SeqCount != 1 || type1.RootKind != DKind.R8)
            return false;
        if (call.Args.Length > 2 && call.Args[2].Type != DType.BitReq)
            return false;
        return true;
    }
}

/// <summary>
/// Paired t-test. Either accepts two samples, or a sequence with two selectors for values of each sample.
/// </summary>
public sealed partial class TTestPairedFunc : TTestBaseFunc
{
    public static readonly TTestPairedFunc Instance = new TTestPairedFunc();

    private TTestPairedFunc()
        : base("Paired", 2, 3, FieldsExtraSingle)
    {
    }


    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        if (carg == 2)
            return ArgTraitsLifting.Create(this, carg, maskLiftSeq: 0x3, maskLiftNeedsSeq: 0x3);

        Validation.Assert(carg == 3);
        return new ArgTraitsSel(this, carg);
    }

    private sealed class ArgTraitsSel : ArgTraitsSeq
    {
        public override int NestedCount => 2;

        public ArgTraitsSel(TTestPairedFunc func, int slotCount)
            : base(func, parallel: true, indexed: true, slotCount, seqCount: 1)
        {
            Validation.Assert(slotCount == 3);
        }

        public override bool AreEquivalent(ArgTraits cmp)
        {
            return cmp is ArgTraitsSel;
        }

        public override bool IsNested(int slot)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
            return slot >= ScopeCount;
        }

        public override bool IsScopeActive(int slot, int upCount)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
            Validation.BugCheckIndex(upCount, ScopeCount, nameof(upCount));
            return slot >= ScopeCount;
        }
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var types = info.GetArgTypes();
        if (types.Count == 2)
        {
            types[0] = (types[0].RootType.IsOpt ? DType.R8Opt : DType.R8Req).ToSequence();
            types[1] = (types[1].RootType.IsOpt ? DType.R8Opt : DType.R8Req).ToSequence();
        }
        else
        {
            EnsureTypeSeq(types, 0);
            types[1] = types[1].IsOpt ? DType.R8Opt : DType.R8Req;
            types[2] = types[2].IsOpt ? DType.R8Opt : DType.R8Req;
        }

        return (TypeRes, types.ToImmutable());
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Type != TypeRes)
            return false;

        var type0 = call.Args[0].Type;
        var type1 = call.Args[1].Type;

        int cscopeExp;
        if (call.Args.Length == 2)
        {
            if (type0.SeqCount != 1 || type0.RootKind != DKind.R8)
                return false;
            if (type1.SeqCount != 1 || type1.RootKind != DKind.R8)
                return false;
            cscopeExp = 0;
        }
        else
        {
            Validation.Assert(call.Args.Length == 3);
            if (!type0.IsSequence)
                return false;
            if (type1 != DType.R8Req && type1 != DType.R8Opt)
                return false;
            var type2 = call.Args[2].Type;
            if (type2 != DType.R8Req && type2 != DType.R8Opt)
                return false;
            cscopeExp = 1;
        }

        if (call.Scopes.Length != cscopeExp)
            return false;
        if (call.Indices.Length != cscopeExp)
            return false;

        return true;
    }
}
