// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Code;

using RngTuple = Immutable.Array<SlotRange>;

/// <summary>
/// Indicates whether and how a sequence result from a function should be "wrapped".
/// </summary>
public enum SeqWrapKind : byte
{
    /// <summary>
    /// Indicates the code generator's choice is fine.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Indicates that no wrapping is needed because the result came from a previously
    /// computed value. For example, <c>If(cond, A, B)</c> can safely use this, as
    /// <c>A</c> and <c>B</c> are sequences handed to the <c>If</c> function rather than
    /// constructed by it.
    /// </summary>
    DontWrap = 1,

    /// <summary>
    /// The sequence must be ensured to be "caching" in the sense that individual items
    /// should not be computed more than once. This is typically because a selector
    /// or predicate is volatile.
    /// </summary>
    MustCache = 2,
}

/// <summary>
/// Base class for a rexl operation code generator that handles type <typeparamref name="TChild"/>
/// as a "child" of <typeparamref name="TParent"/>. Note that <see cref="Handles(RexlOper)"/> returns
/// true for a child, not for the parent.
/// </summary>
public abstract class RexlOperationGenerator<TParent, TChild> : RexlOperationGenerator
    where TParent : RexlOper
    where TChild : RexlOper
{
    public sealed override bool Handles(RexlOper oper)
    {
        if (oper is not TChild)
            return false;
        if (oper.Parent is not TParent)
            return false;
        return true;
    }

    protected TChild GetChild(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call, full: true));
        Validation.Assert(call.Oper is TChild);
        return (TChild)call.Oper;
    }

    protected TParent GetParent(BndCallNode call)
    {
        return GetParent(GetChild(call));
    }

    protected TParent GetParent(TChild child)
    {
        Validation.AssertValue(child);
        Validation.Assert(child.Parent is TParent);
        return (TParent)child.Parent;
    }
}

/// <summary>
/// Base class for a rexl operation code generator that handles a particular subclass
/// of <see cref="RexlOper"/>.
/// </summary>
public abstract class RexlOperationGenerator<TOper> : RexlOperationGenerator
    where TOper : RexlOper
{
    public sealed override bool Handles(RexlOper oper)
    {
        if (oper is not TOper)
            return false;
        return true;
    }

    protected TOper GetOper(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call, full: true));
        Validation.Assert(call.Oper is TOper);
        return (TOper)call.Oper;
    }
}

/// <summary>
/// Base generator class where the subclass looks up the method from the call.
/// </summary>
public abstract class GetMethGen<TOper> : RexlOperationGenerator<TOper>
    where TOper : RexlOper
{
    protected abstract bool TryGetMeth(ICodeGen codeGen, BndCallNode call, out MethodInfo meth);

    protected sealed override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));

        if (!TryGetMeth(codeGen, call, out var meth))
        {
            Validation.Assert(false);
            stRet = null;
            return false;
        }

        stRet = GenCall(codeGen, meth, sts);
        return true;
    }
}

/// <summary>
/// Base generator class for arity based method lookup in a readonly array.
/// </summary>
public abstract class MethArityGen<TOper> : GetMethGen<TOper>
    where TOper : RexlOper
{
    protected abstract ReadOnly.Array<MethodInfo> Meths { get; }
    protected abstract int ArityMin { get; }

    protected override bool TryGetMeth(ICodeGen codeGen, BndCallNode call, out MethodInfo meth)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));

        int index = call.Args.Length - ArityMin;
        var meths = Meths;
        if (Validation.IsValidIndex(index, meths.Length))
        {
            meth = meths[index];
            return true;
        }

        meth = null;
        return false;
    }
}

/// <summary>
/// Base class for a rexl operation code generator.
/// </summary>
public abstract class RexlOperationGenerator
{
    /// <summary>
    /// Return whether this operation code generator can handle the given operation.
    /// </summary>
    public abstract bool Handles(RexlOper oper);

    /// <summary>
    /// Return whether the given call is valid and can be handled by this operation code generator.
    /// </summary>
    public bool IsValidCall(BndCallNode call, bool full)
    {
        if (call is null)
            return false;
        if (!Handles(call.Oper))
            return false;
        return call.Oper.IsValidCall(call, full);
    }

    /// <summary>
    /// Return whether this call needs access to an execution context.
    /// </summary>
    public bool NeedsExecCtx(BndCallNode call)
    {
        Validation.BugCheckParam(IsValidCall(call, true), nameof(call));
        return NeedsExecCtxCore(call);
    }

    protected virtual bool NeedsExecCtxCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call, true));
        // The default is that unbounded operations use the exec context (to ping).
        return call.Oper.IsUnbounded(call);
    }

    /// <summary>
    /// Optionally generate code for the entire call, including args. That is, this is
    /// invoked before the args are code-gened. This allows short-circuiting logic.
    /// </summary>
    public bool TryGenSpecial(ICodeGen codeGen, BndCallNode call, int idx, out Type stRet, out SeqWrapKind wrap)
    {
        Validation.BugCheckValue(codeGen, nameof(codeGen));
        Validation.BugCheckParam(IsValidCall(call, true), nameof(call));
        return TryGenSpecialCore(codeGen, call, idx, out stRet, out wrap);
    }

    protected virtual bool TryGenSpecialCore(ICodeGen codeGen, BndCallNode call, int idx,
        out Type stRet, out SeqWrapKind wrap)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        stRet = default;
        wrap = default;
        return false;
    }

    /// <summary>
    /// A function may require that some slot ranges be bundled as arrays. This function
    /// produces those ranges. The args for any such range must be of the same type.
    /// </summary>
    public RngTuple GetArrayRanges(BndCallNode call)
    {
        Validation.BugCheckParam(IsValidCall(call, true), nameof(call));

        var res = GetArrayRangesCore(call);
        Validation.Assert(IsValidArrayRanges(call, res));
        return res;
    }

    protected virtual RngTuple GetArrayRangesCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call, true));
        return RngTuple.Empty;
    }

    /// <summary>
    /// Returns true if the ranges are valid. This is typically used in
    /// asserts and bug-checks.
    /// </summary>
    protected bool IsValidArrayRanges(BndCallNode call, RngTuple rngs)
    {
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(!rngs.IsDefault);

        // The ranges must be in order and contain args of the same type.
        int lim = 0;
        for (int irng = 0; irng < rngs.Length; irng++)
        {
            var rng = rngs[irng];
            if (rng.Count == 0)
                return false;
            if (lim > rng.Min)
                return false;
            lim = rng.Lim;
            if (lim > call.Args.Length)
                return false;

            // The args must be of the same type.
            for (int i = rng.Min + 1; i < lim; i++)
            {
                if (call.Args[i].Type != call.Args[i - 1].Type)
                    return false;
            }
        }

        return IsValidArrayRangesCore(call, rngs);
    }

    protected virtual bool IsValidArrayRangesCore(BndCallNode call, RngTuple ranges)
    {
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(!ranges.IsDefault);
        return ranges.Length == 0;
    }

    /// <summary>
    /// Returns true if code gen should include a parameter for the given index when generating a
    /// delegate for the given nested arg, even if that index has not been referenced within any args.
    /// This allows functions to avoid a combinatorial explosion of possible signatures with indexed
    /// variants if they accept many selectors and have multiple indices, e.g. <see cref="KeyJoinFunc"/>.
    /// </summary>
    public bool NeedsIndexParam(BndCallNode call, int slot, int iidx)
    {
        Validation.BugCheck(IsValidCall(call, true), nameof(call));
        Validation.BugCheckIndex(slot, call.Args.Length, nameof(slot));
        Validation.BugCheck(call.Traits.IsNested(slot), nameof(slot));
        Validation.BugCheckIndex(iidx, call.Indices.Length, nameof(iidx));
        return NeedsIndexParamCore(call, slot, iidx);
    }

    protected virtual bool NeedsIndexParamCore(BndCallNode call, int slot, int iidx)
    {
        Validation.Assert(IsValidCall(call, true));
        Validation.AssertIndex(slot, call.Args.Length);
        Validation.Assert(call.Traits.IsNested(slot));
        Validation.AssertIndex(iidx, call.Indices.Length);
        return false;
    }

    /// <summary>
    /// Try to generate code for a <see cref="BndCallNode"/>.
    /// </summary>
    public bool TryGenCode(ICodeGen codeGen, BndCallNode call, RngTuple rngs, ReadOnly.Array<Type> sts,
        out Type stRet, out SeqWrapKind wrap)
    {
        Validation.BugCheckValue(codeGen, nameof(codeGen));
        Validation.BugCheckParam(IsValidCall(call, true), nameof(call));
        Validation.BugCheckParam(IsValidArrayRanges(call, rngs), nameof(rngs));
        return TryGenCodeCore(codeGen, call, rngs, sts, out stRet, out wrap);
    }

    /// <summary>
    /// Try to generate code for a <see cref="BndCallNode"/>.
    /// </summary>
    protected virtual bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, RngTuple rngs,
        ReadOnly.Array<Type> sts, out Type stRet, out SeqWrapKind wrap)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(IsValidArrayRanges(call, rngs));

        if (!rngs.IsDefaultOrEmpty)
        {
            wrap = default;
            stRet = null;
            return false;
        }

        Validation.BugCheckParam(sts.Length == call.Args.Length, nameof(sts));
        return TryGenCodeCore(codeGen, call, sts, out stRet, out wrap);
    }

    /// <summary>
    /// Try to generate code for a <see cref="BndCallNode"/>.
    /// </summary>
    protected virtual bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts,
        out Type stRet, out SeqWrapKind wrap)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);
        wrap = default;
        return TryGenCodeCore(codeGen, call, sts, out stRet);
    }

    /// <summary>
    /// Try to generate code for a <see cref="BndCallNode"/>.
    /// </summary>
    protected virtual bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts,
        out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        stRet = null;
        return false;
    }

    /// <summary>
    /// Return whether the method parameter types are compatible with the given system types.
    /// </summary>
    protected static bool AreCompatible(MethodInfo meth, ReadOnly.Array<Type> sts)
    {
        Validation.AssertValue(meth);
        Validation.Assert(meth.IsStatic);

        // Validate the parameters.
        var parms = meth.GetParameters();
        if (parms.Length != sts.Length)
            return false;

        for (int i = 0; i < parms.Length; i++)
        {
            var stArg = sts[i];
            var stPrm = parms[i].ParameterType;
            if (!stPrm.IsAssignableFrom(stArg))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Return whether the method parameter types are compatible with the given system types.
    /// </summary>
    protected static bool AreCompatible(MethodInfo meth, ReadOnly.Array<Type> sts, params Type[] more)
    {
        Validation.AssertValue(meth);
        Validation.Assert(meth.IsStatic);

        // Validate the parameters.
        var parms = meth.GetParameters();
        if (parms.Length != sts.Length + Util.Size(more))
            return false;

        for (int i = 0; i < parms.Length; i++)
        {
            var stArg = i < sts.Length ? sts[i] : more[i - sts.Length];
            var stPrm = parms[i].ParameterType;
            if (!stPrm.IsAssignableFrom(stArg))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Generate a call of the given static method info.
    /// Assert that the method is compatible with the given parameter types.
    /// </summary>
    protected static Type GenCall(ICodeGen codeGen, MethodInfo meth, ReadOnly.Array<Type> sts)
    {
        Validation.AssertValue(codeGen);
        Validation.AssertValue(meth);
        Validation.Assert(AreCompatible(meth, sts));

        codeGen.Writer.Call(meth);
        return meth.ReturnType;
    }

    /// <summary>
    /// Generate a call of the given static method info after pushing the default value for the given
    /// <paramref name="type"/>.
    /// Assert that the method is compatible with the given parameter types.
    /// </summary>
    protected static Type GenCallDefault(ICodeGen codeGen, MethodInfo meth, ReadOnly.Array<Type> sts, DType type)
    {
        Validation.AssertValue(codeGen);
        Validation.AssertValue(meth);
        Validation.Assert(type.IsValid);

        var st = codeGen.GetSystemType(type);
        Validation.Assert(AreCompatible(meth, sts, st));

        codeGen.GenLoadDefault(type);
        codeGen.Writer.Call(meth);
        return meth.ReturnType;
    }

    /// <summary>
    /// Generate a call of the given static method info after pushing the given "extra" value.
    /// Assert that the method is compatible with the given parameter types.
    /// </summary>
    protected static Type GenCallExtra<T>(ICodeGen codeGen, MethodInfo meth, ReadOnly.Array<Type> sts, T extra)
        where T : class
    {
        Validation.AssertValue(codeGen);
        Validation.AssertValue(meth);
        Validation.AssertValue(extra);
        Validation.Assert(AreCompatible(meth, sts, extra.GetType()));

        codeGen.GenLoadConst(extra);
        codeGen.Writer.Call(meth);
        return meth.ReturnType;
    }

    /// <summary>
    /// Generate a call of the given static method info after pushing the execution context.
    /// Assert that the method is compatible with the given parameter types.
    /// </summary>
    protected static Type GenCallCtx(ICodeGen codeGen, MethodInfo meth, ReadOnly.Array<Type> sts)
    {
        Validation.AssertValue(codeGen);
        Validation.AssertValue(meth);
        Validation.Assert(AreCompatible(meth, sts, typeof(ExecCtx)));

        codeGen.GenLoadExecCtx();
        codeGen.Writer.Call(meth);
        return meth.ReturnType;
    }

    /// <summary>
    /// Generate a call of the given static method info after pushing the given "extra" value and the exec ctx.
    /// Assert that the method is compatible with the given parameter types.
    /// </summary>
    protected static Type GenCallExtraCtx<T>(ICodeGen codeGen, MethodInfo meth, ReadOnly.Array<Type> sts, T extra)
        where T : class
    {
        Validation.AssertValue(codeGen);
        Validation.AssertValue(meth);
        Validation.AssertValue(extra);
        Validation.Assert(AreCompatible(meth, sts, extra.GetType(), typeof(ExecCtx)));

        codeGen.GenLoadConst(extra);
        codeGen.GenLoadExecCtx();
        codeGen.Writer.Call(meth);
        return meth.ReturnType;
    }

    /// <summary>
    /// Generate a call of the given static method info after pushing the exec ctx and and an id for
    /// <paramref name="bnd"/>.
    /// Assert that the method is compatible with the given parameter types.
    /// </summary>
    protected static Type GenCallCtxId(ICodeGen codeGen, MethodInfo meth, ReadOnly.Array<Type> sts, BoundNode bnd, int count = 1)
    {
        Validation.AssertValue(codeGen);
        Validation.AssertValue(meth);
        Validation.AssertValue(bnd);
        Validation.Assert(AreCompatible(meth, sts, typeof(ExecCtx), typeof(int)));

        codeGen.GenLoadExecCtxAndId(bnd, count);
        codeGen.Writer.Call(meth);
        return meth.ReturnType;
    }

    /// <summary>
    /// Generate a call of the given static method info after pushing the the default value for the given
    /// <paramref name="type"/> and the exec ctx and an id for <paramref name="bnd"/>.
    /// Assert that the method is compatible with the given parameter types.
    /// </summary>
    protected static Type GenCallDefaultCtxId(ICodeGen codeGen, MethodInfo meth, ReadOnly.Array<Type> sts, DType type, BoundNode bnd, int count = 1)
    {
        Validation.AssertValue(codeGen);
        Validation.AssertValue(meth);
        Validation.Assert(type.IsValid);
        Validation.AssertValue(bnd);

        var st = codeGen.GetSystemType(type);
        Validation.Assert(AreCompatible(meth, sts, st, typeof(ExecCtx), typeof(int)));

        codeGen.GenLoadDefault(type);
        codeGen.GenLoadExecCtxAndId(bnd, count);
        codeGen.Writer.Call(meth);
        return meth.ReturnType;
    }

    /// <summary>
    /// Generate a call of the given static method info after pushing the given "extra" value and the exec ctx
    /// and an id for <paramref name="bnd"/>.
    /// Assert that the method is compatible with the given parameter types.
    /// </summary>
    protected static Type GenCallExtraCtxId<T>(ICodeGen codeGen, MethodInfo meth, ReadOnly.Array<Type> sts, T extra, BoundNode bnd, int count = 1)
        where T : class
    {
        Validation.AssertValue(codeGen);
        Validation.AssertValue(meth);
        Validation.AssertValue(extra);
        Validation.AssertValue(bnd);
        Validation.Assert(AreCompatible(meth, sts, extra.GetType(), typeof(ExecCtx), typeof(int)));

        codeGen.GenLoadConst(extra);
        codeGen.GenLoadExecCtxAndId(bnd, count);
        codeGen.Writer.Call(meth);
        return meth.ReturnType;
    }
}
