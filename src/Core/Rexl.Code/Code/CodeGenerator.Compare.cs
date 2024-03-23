// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Reflection;
using System.Reflection.Emit;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Code;

using LocEnt = MethodGenerator.Local;
using NullTo = BndCompareNode.NullTo;

partial class CodeGeneratorBase
{
    partial class Impl
    {
        protected override bool PreVisitImpl(BndCompareNode bnd, int idx)
        {
            AssertIdx(bnd, idx);

            // These are guaranteed by BndCompareNode.
            Validation.Assert(bnd.Args.Length >= 2);
            Validation.Assert(bnd.Args.Length == bnd.Ops.Length + 1);
            Validation.Assert(bnd.Ops[0] != CompareOp.None);
            Validation.Assert(bnd.Ops[bnd.Ops.Length - 1] != CompareOp.None);

            // REVIEW: Consider supporting a target label and sense rather than generating
            // the bool value.
            int depthInit = _frameCur.StackDepth;

            if (bnd.ArgType.IsAggXxx)
                GenCmpAggType(bnd, idx);
            else if (bnd.ArgKind.IsReferenceFriendly() || !bnd.HasOpt)
                GenCmpSimple(bnd, idx);
            else
                CmpGenValType.Gen(this, bnd, idx);

            // Stack state should be a single bool.
            Validation.Assert(_frameCur.StackDepth == depthInit + 1);
            PeekType(typeof(bool));

            return false;
        }

        protected override void PostVisitImpl(BndCompareNode bnd, int idx)
        {
            // Should be handled in PreVisit.
            throw Unexpected();
        }

        /// <summary>
        /// For generating comparison chains for "simple" types. These consist of text, uri, and
        /// non-opt value types. Aggregate types, like records and tuples, and opt value types
        /// require special handling.
        /// </summary>
        private void GenCmpSimple(BndCompareNode bnd, int idx)
        {
            AssertIdx(bnd, idx);

            // The types that come through here are value types (non-opt) and text and uri.
            // None of these has an associated required type.
            Validation.Assert(!bnd.ArgType.HasReq);
            Validation.Assert(!bnd.ArgType.IsAggXxx);

            var typeCmp = bnd.ArgType;
            var stCmp = GetSysType(typeCmp);

            Label labFalse = default;
            Loc loc = default;
            try
            {
                int cur = idx + 1;
                for (int iop = 0; ;)
                {
                    var op = bnd.Ops[iop];
                    bool isLast = ++iop >= bnd.Ops.Length;
                    if (op == CompareOp.None)
                    {
                        Validation.Assert(!isLast);
                        Validation.Assert(!loc.IsActive);
                        continue;
                    }

                    if (loc.IsActive)
                    {
                        loc.Push(this);
                        loc.Dispose();
                        Validation.Assert(!loc.IsActive);
                    }
                    else
                        cur = bnd.Args[iop - 1].Accept(this, cur);
                    PeekType(stCmp);

                    if (!isLast && bnd.Ops[iop] != CompareOp.None)
                        loc.EnsureLocOrArg(this, bnd.Args[iop], ref cur, stCmp, load: true);
                    else
                        cur = bnd.Args[iop].Accept(this, cur);
                    PeekType(stCmp);

                    if (isLast)
                    {
                        GenCmp(op, typeCmp);
                        PeekType(typeof(bool));
                        break;
                    }
                    GenCmpBr(op, typeCmp, ref labFalse);
                }
                Validation.Assert(cur == idx + bnd.NodeCount);
            }
            finally
            {
                loc.Dispose();
            }

            // Generate false section and labels.
            Label labDone = default;
            if (IL.IsLabelUsed(labFalse))
            {
                IL
                    .Br(ref labDone)
                    .MarkLabel(labFalse)
                    .Ldc_I4(0);
            }
            IL.MarkLabelIfUsed(labDone);
        }

        /// <summary>
        /// For generating comparison chains for agg types like records and tuples.
        /// </summary>
        private void GenCmpAggType(BndCompareNode bnd, int idx)
        {
            AssertIdx(bnd, idx);
            Validation.Assert(bnd.ArgType.IsAggReq);
            Validation.Assert(bnd.ArgType.IsEquatable);
            Validation.Assert(!bnd.ArgType.IsComparable);

            // These are guaranteed by BndCompareNode.
            Validation.Assert(!bnd.HasOrdered);
            Validation.Assert(bnd.Args.Length >= 2);
            Validation.Assert(bnd.Args.Length == bnd.Ops.Length + 1);
            Validation.Assert(bnd.Ops[0] != CompareOp.None);
            Validation.Assert(bnd.Ops[bnd.Ops.Length - 1] != CompareOp.None);

            var typeCmp = bnd.ArgType;
            var methEq = GetAggEquals2(typeCmp, out Type stCmp);
            EqCmpInfo info = default;

            Label labFalse = default;
            Loc loc = default;
            try
            {
                int cur = idx + 1;
                for (int iop = 0; ;)
                {
                    var op = bnd.Ops[iop];
                    bool isLast = ++iop >= bnd.Ops.Length;
                    if (op == CompareOp.None)
                    {
                        Validation.Assert(!isLast);
                        Validation.Assert(!loc.IsActive);
                        continue;
                    }

                    MethodInfo methSpec = null;
                    bool ci = op.IsCi;
                    bool strict = op.IsStrict;
                    if (ci | strict)
                    {
                        if (info.StItem is null)
                            GetAggEqInfo(typeCmp, out info);
                        Validation.Assert(info.StItem == stCmp);
                        Validation.Assert(info.Eq is not null);
                        Validation.Assert(info.MethEquals is not null);
                        IEqualityComparer eq;

                        if (ci & strict)
                            eq = info.EqTiCi ?? info.EqCi ?? info.EqTi;
                        else if (ci)
                            eq = info.EqCi;
                        else
                            eq = info.EqTi;

                        if (eq is not null)
                        {
                            GenLoadConstCore(eq, info.StEq);
                            PushType(info.StEq);
                            methSpec = info.MethEquals;
                        }
                        else
                        {
                            // REVIEW: Should this ever happen?
                            Validation.Assert(false);
                        }
                    }

                    if (loc.IsActive)
                    {
                        loc.Push(this);
                        loc.Dispose();
                        Validation.Assert(!loc.IsActive);
                    }
                    else
                        cur = bnd.Args[iop - 1].Accept(this, cur);
                    PeekType(stCmp);

                    if (!isLast && bnd.Ops[iop] != CompareOp.None)
                        loc.EnsureLocOrArg(this, bnd.Args[iop], ref cur, stCmp, load: true);
                    else
                        cur = bnd.Args[iop].Accept(this, cur);
                    PeekType(stCmp);

                    if (methSpec is not null)
                        EmitCallVirt(methSpec);
                    else
                        EmitCall(methEq);

                    if (isLast)
                    {
                        if (op.IsNot)
                            IL.Ldc_I4(0).Ceq();
                        PeekType(typeof(bool));
                        break;
                    }
                    else if (op.IsNot)
                        IL.Brtrue(ref labFalse);
                    else
                        IL.Brfalse(ref labFalse);
                    PopType(typeof(bool));
                }
                Validation.Assert(cur == idx + bnd.NodeCount);
            }
            finally
            {
                loc.Dispose();
            }

            // Generate false section and labels.
            Label labDone = default;
            if (IL.IsLabelUsed(labFalse))
            {
                IL
                    .Br(ref labDone)
                    .MarkLabel(labFalse)
                    .Ldc_I4(0);
            }
            IL.MarkLabelIfUsed(labDone);
        }

        protected MethodInfo GetAggEquals2(DType typeAgg, out Type stAgg)
        {
            Validation.Assert(typeAgg.IsAggXxx);

            if (_typeManager.TryGetAggEquals2(typeAgg, out stAgg, out var methEq))
            {
                Validation.Assert(stAgg.IsClass);
                Validation.Assert(GetSysType(typeAgg.ToOpt()) == stAgg);
                Validation.Assert(methEq.IsStatic);
                return methEq;
            }

            if (stAgg is null)
                throw Unexpected("Failed to create system type for agg type");
            Validation.Assert(methEq is null);
            throw Unexpected("Missing Equals2 method for equatable agg type");
        }

        protected void GetAggEqInfo(DType typeAgg, out EqCmpInfo info)
        {
            Validation.Assert(typeAgg.IsAggXxx);
            Validation.Assert(typeAgg.IsEquatable);

            if (!_typeManager.TryGetAggEqCmp(typeAgg, out info))
                throw Unexpected();
        }

        protected void GenLoadAggEqCmpCore(DType typeAgg, bool ti, bool ci, out EqCmpInfo info)
        {
            GetAggEqInfo(typeAgg, out info);

            // REVIEW: Currently ti is always false (the only use is for GroupBy which
            // never uses strict).
            IEqualityComparer eq;
            if (ti & ci)
                eq = info.EqTiCi ?? info.EqCi ?? info.EqTi;
            else if (ti)
                eq = info.EqTi;
            else if (ci)
                eq = info.EqCi;
            else
            {
                Validation.Assert(info.MethGetDefault != null);
                Validation.Assert(info.MethGetDefault.ReturnType == info.StEq);
                IL.Call(info.MethGetDefault);
                return;
            }

            Validation.Assert(info.Eq != null);
            GenLoadConstCore(eq ?? info.Eq, info.StEq);
        }

        /// <summary>
        /// Generates a test and branch, with the branch taken when the logical value of the test matches
        /// <paramref name="sense"/>, which defaults to false.
        /// </summary>
        protected void GenCmpBr(CompareOp op, DType type, ref Label lab, bool sense = false)
        {
            Validation.Assert(type.IsValid);

            DKind kindCmp = type.Kind;

            op = op.SimplifyForType(type, type);
            var (root, mods) = op.GetParts();

            // Reverse the sense for a Not variant.
            if (mods.IsNot())
                sense = !sense;

            MethodInfo meth;
            switch (kindCmp)
            {
            default:
                throw Unexpected();

            case DKind.I8:
            case DKind.I4:
            case DKind.I2:
            case DKind.I1:
                Validation.Assert(!type.IsOpt);
                switch (root)
                {
                case CompareRoot.Equal:
                    if (sense)
                        IL.Beq(ref lab);
                    else
                        IL.Bne_Un(ref lab);
                    break;
                case CompareRoot.Less:
                    if (sense)
                        IL.Blt(ref lab);
                    else
                        IL.Bge(ref lab);
                    break;
                case CompareRoot.Greater:
                    if (sense)
                        IL.Bgt(ref lab);
                    else
                        IL.Ble(ref lab);
                    break;
                case CompareRoot.LessEqual:
                    if (sense)
                        IL.Ble(ref lab);
                    else
                        IL.Bgt(ref lab);
                    break;
                case CompareRoot.GreaterEqual:
                    if (sense)
                        IL.Bge(ref lab);
                    else
                        IL.Blt(ref lab);
                    break;
                default:
                    throw Unexpected();
                }
                PopType(type);
                PopType(type);
                return;

            case DKind.U8:
            case DKind.U4:
            case DKind.U2:
            case DKind.U1:
            case DKind.Bit:
                Validation.Assert(!type.IsOpt);
                switch (root)
                {
                case CompareRoot.Equal:
                    if (sense)
                        IL.Beq(ref lab);
                    else
                        IL.Bne_Un(ref lab);
                    break;
                case CompareRoot.Less:
                    if (sense)
                        IL.Blt_Un(ref lab);
                    else
                        IL.Bge_Un(ref lab);
                    break;
                case CompareRoot.Greater:
                    if (sense)
                        IL.Bgt_Un(ref lab);
                    else
                        IL.Ble_Un(ref lab);
                    break;
                case CompareRoot.LessEqual:
                    if (sense)
                        IL.Ble_Un(ref lab);
                    else
                        IL.Bgt_Un(ref lab);
                    break;
                case CompareRoot.GreaterEqual:
                    if (sense)
                        IL.Bge_Un(ref lab);
                    else
                        IL.Blt_Un(ref lab);
                    break;
                default:
                    throw Unexpected();
                }
                PopType(type);
                PopType(type);
                return;

            case DKind.R8:
            case DKind.R4:
                Validation.Assert(!type.IsOpt);
                if (mods.IsStrict())
                {
                    // The strict case uses the normal IL instructions.
                    switch (root)
                    {
                    case CompareRoot.Equal:
                        if (sense)
                            IL.Beq(ref lab);
                        else
                            IL.Bne_Un(ref lab);
                        break;
                    case CompareRoot.Less:
                        if (sense)
                            IL.Blt(ref lab);
                        else
                            IL.Bge_Un(ref lab);
                        break;
                    case CompareRoot.Greater:
                        if (sense)
                            IL.Bgt(ref lab);
                        else
                            IL.Ble_Un(ref lab);
                        break;
                    case CompareRoot.LessEqual:
                        if (sense)
                            IL.Ble(ref lab);
                        else
                            IL.Bgt_Un(ref lab);
                        break;
                    case CompareRoot.GreaterEqual:
                        if (sense)
                            IL.Bge(ref lab);
                        else
                            IL.Blt_Un(ref lab);
                        break;
                    default:
                        throw Unexpected();
                    }
                    PopType(type);
                    PopType(type);
                }
                else
                {
                    if (kindCmp == DKind.R8)
                    {
                        switch (root)
                        {
                        case CompareRoot.Equal: meth = CodeGenUtil.R8Eq; break;
                        case CompareRoot.Less: meth = CodeGenUtil.R8Lt; break;
                        case CompareRoot.Greater: meth = CodeGenUtil.R8Gt; break;
                        case CompareRoot.LessEqual: meth = CodeGenUtil.R8Le; break;
                        case CompareRoot.GreaterEqual: meth = CodeGenUtil.R8Ge; break;
                        default: throw Unexpected();
                        }
                    }
                    else
                    {
                        Validation.Assert(kindCmp == DKind.R4);
                        switch (root)
                        {
                        case CompareRoot.Equal: meth = CodeGenUtil.R4Eq; break;
                        case CompareRoot.Less: meth = CodeGenUtil.R4Lt; break;
                        case CompareRoot.Greater: meth = CodeGenUtil.R4Gt; break;
                        case CompareRoot.LessEqual: meth = CodeGenUtil.R4Le; break;
                        case CompareRoot.GreaterEqual: meth = CodeGenUtil.R4Ge; break;
                        default: throw Unexpected();
                        }
                    }
                    EmitCall(meth);
                    if (sense)
                        IL.Brtrue(ref lab);
                    else
                        IL.Brfalse(ref lab);
                    PopType(typeof(bool));
                }
                return;

            case DKind.IA:
                Validation.Assert(!type.IsOpt);
                switch (root)
                {
                case CompareRoot.Equal: meth = CodeGenUtil.IntEq; break;
                case CompareRoot.Greater: meth = CodeGenUtil.IntGt; break;
                case CompareRoot.GreaterEqual: meth = CodeGenUtil.IntGe; break;
                case CompareRoot.Less: meth = CodeGenUtil.IntLt; break;
                case CompareRoot.LessEqual: meth = CodeGenUtil.IntLe; break;
                default: throw Unexpected();
                }
                EmitCall(meth);
                break;

            case DKind.Text:
                EmitCall(CodeGenUtil.GetStrCmpOp(op));
                break;

            case DKind.Date:
                Validation.Assert(!type.IsOpt);
                switch (root)
                {
                case CompareRoot.Equal: meth = CodeGenUtil.DateEq; break;
                case CompareRoot.Greater: meth = CodeGenUtil.DateGt; break;
                case CompareRoot.GreaterEqual: meth = CodeGenUtil.DateGe; break;
                case CompareRoot.Less: meth = CodeGenUtil.DateLt; break;
                case CompareRoot.LessEqual: meth = CodeGenUtil.DateLe; break;
                default: throw Unexpected();
                }
                EmitCall(meth);
                break;

            case DKind.Time:
                Validation.Assert(!type.IsOpt);
                switch (root)
                {
                case CompareRoot.Equal: meth = CodeGenUtil.TimeEq; break;
                case CompareRoot.Greater: meth = CodeGenUtil.TimeGt; break;
                case CompareRoot.GreaterEqual: meth = CodeGenUtil.TimeGe; break;
                case CompareRoot.Less: meth = CodeGenUtil.TimeLt; break;
                case CompareRoot.LessEqual: meth = CodeGenUtil.TimeLe; break;
                default: throw Unexpected();
                }
                EmitCall(meth);
                break;

            case DKind.Guid:
                Validation.Assert(!type.IsOpt);
                switch (root)
                {
                case CompareRoot.Equal:
                    EmitCall(CodeGenUtil.EquatableEqualsVal(typeof(Guid)));
                    break;
                case CompareRoot.Greater:
                    EmitCall(CodeGenUtil.ComparableCmpVal(typeof(Guid)));
                    IL.Ldc_I4(0);
                    if (sense)
                        IL.Bgt(ref lab);
                    else
                        IL.Ble(ref lab);
                    PopType(typeof(int));
                    return;
                case CompareRoot.GreaterEqual:
                    EmitCall(CodeGenUtil.ComparableCmpVal(typeof(Guid)));
                    IL.Ldc_I4(0);
                    if (sense)
                        IL.Bge(ref lab);
                    else
                        IL.Blt(ref lab);
                    PopType(typeof(int));
                    return;
                case CompareRoot.Less:
                    EmitCall(CodeGenUtil.ComparableCmpVal(typeof(Guid)));
                    IL.Ldc_I4(0);
                    if (sense)
                        IL.Blt(ref lab);
                    else
                        IL.Bge(ref lab);
                    PopType(typeof(int));
                    return;
                case CompareRoot.LessEqual:
                    EmitCall(CodeGenUtil.ComparableCmpVal(typeof(Guid)));
                    IL.Ldc_I4(0);
                    if (sense)
                        IL.Ble(ref lab);
                    else
                        IL.Bgt(ref lab);
                    PopType(typeof(int));
                    return;
                default:
                    throw Unexpected();
                }
                Validation.Assert(!root.IsOrdered());
                break;

            case DKind.Uri:
                Validation.Assert(root == CompareRoot.Equal);
                EmitCall(CodeGenUtil.EquatableEqualsOpt(typeof(Link), mods.IsStrict()));
                break;

            case DKind.Record:
            case DKind.Tuple:
                throw Unexpected();
            }

            if (sense)
                IL.Brtrue(ref lab);
            else
                IL.Brfalse(ref lab);
            PopType(typeof(bool));
        }

        protected void GenCmp(CompareOp op, DType type)
        {
            Validation.Assert(type.IsValid);
            DKind kindCmp = type.Kind;

            op = op.SimplifyForType(type, type);

            // In the cases below, `break` means apply the final inversion if `not` is true,
            // while cases that are handled completely `return` (rather than `break`).
            var (root, mods) = op.GetParts();
            bool not = mods.IsNot();
            MethodInfo meth;
            switch (kindCmp)
            {
            default:
                throw Unexpected();

            case DKind.I8:
            case DKind.I4:
            case DKind.I2:
            case DKind.I1:
                Validation.Assert(!type.IsOpt);
                switch (root)
                {
                case CompareRoot.Equal:
                    IL.Ceq();
                    break;
                case CompareRoot.Less:
                    IL.Clt();
                    break;
                case CompareRoot.Greater:
                    IL.Cgt();
                    break;
                case CompareRoot.LessEqual:
                    IL.Cgt();
                    not = !not;
                    break;
                case CompareRoot.GreaterEqual:
                    IL.Clt();
                    not = !not;
                    break;
                default:
                    throw Unexpected();
                }
                PopType(type);
                PopType(type);
                PushType(typeof(bool));
                break;

            case DKind.U8:
            case DKind.U4:
            case DKind.U2:
            case DKind.U1:
            case DKind.Bit:
                Validation.Assert(!type.IsOpt);
                PopType(type);
                PopType(type);
                PushType(typeof(bool));
                switch (root)
                {
                case CompareRoot.Equal:
                    if (not && type.RootKind == DKind.Bit)
                    {
                        IL.Xor();
                        return;
                    }
                    IL.Ceq();
                    break;
                case CompareRoot.Less:
                    IL.Clt_Un();
                    break;
                case CompareRoot.Greater:
                    IL.Cgt_Un();
                    break;
                case CompareRoot.LessEqual:
                    IL.Cgt_Un();
                    not = !not;
                    break;
                case CompareRoot.GreaterEqual:
                    IL.Clt_Un();
                    not = !not;
                    break;

                default:
                    throw Unexpected();
                }
                break;

            case DKind.R8:
            case DKind.R4:
                Validation.Assert(!type.IsOpt);
                if (mods.IsStrict())
                {
                    switch (root)
                    {
                    case CompareRoot.Equal:
                        IL.Ceq();
                        break;
                    case CompareRoot.Less:
                        IL.Clt();
                        break;
                    case CompareRoot.Greater:
                        IL.Cgt();
                        break;
                    case CompareRoot.LessEqual:
                        // Use cgt_un, before negating. This gives the correct NaN behavior.
                        IL.Cgt_Un();
                        not = !not;
                        break;
                    case CompareRoot.GreaterEqual:
                        // Use clt_un, before negating. This gives the correct NaN behavior.
                        IL.Clt_Un();
                        not = !not;
                        break;
                    default:
                        throw Unexpected();
                    }
                    PopType(type);
                    PopType(type);
                    PushType(typeof(bool));
                }
                else
                {
                    if (kindCmp == DKind.R8)
                    {
                        switch (root)
                        {
                        case CompareRoot.Equal: meth = CodeGenUtil.R8Eq; break;
                        case CompareRoot.Less: meth = CodeGenUtil.R8Lt; break;
                        case CompareRoot.Greater: meth = CodeGenUtil.R8Gt; break;
                        case CompareRoot.LessEqual: meth = CodeGenUtil.R8Le; break;
                        case CompareRoot.GreaterEqual: meth = CodeGenUtil.R8Ge; break;
                        default: throw Unexpected();
                        }
                    }
                    else
                    {
                        Validation.Assert(kindCmp == DKind.R4);
                        switch (root)
                        {
                        case CompareRoot.Equal: meth = CodeGenUtil.R4Eq; break;
                        case CompareRoot.Less: meth = CodeGenUtil.R4Lt; break;
                        case CompareRoot.Greater: meth = CodeGenUtil.R4Gt; break;
                        case CompareRoot.LessEqual: meth = CodeGenUtil.R4Le; break;
                        case CompareRoot.GreaterEqual: meth = CodeGenUtil.R4Ge; break;
                        default: throw Unexpected();
                        }
                    }
                    EmitCall(meth);
                }
                break;

            case DKind.IA:
                Validation.Assert(!type.IsOpt);
                switch (root)
                {
                case CompareRoot.Equal:
                    meth = not ? CodeGenUtil.IntNe : CodeGenUtil.IntEq;
                    not = false;
                    break;
                case CompareRoot.Greater: meth = CodeGenUtil.IntGt; break;
                case CompareRoot.GreaterEqual: meth = CodeGenUtil.IntGe; break;
                case CompareRoot.Less: meth = CodeGenUtil.IntLt; break;
                case CompareRoot.LessEqual: meth = CodeGenUtil.IntLe; break;
                default: throw Unexpected();
                }
                EmitCall(meth);
                break;

            case DKind.Text:
                EmitCall(CodeGenUtil.GetStrCmpOp(op));
                break;

            case DKind.Date:
                Validation.Assert(!type.IsOpt);
                switch (root)
                {
                case CompareRoot.Equal:
                    meth = not ? CodeGenUtil.DateNe : CodeGenUtil.DateEq;
                    not = false;
                    break;
                case CompareRoot.Greater: meth = CodeGenUtil.DateGt; break;
                case CompareRoot.GreaterEqual: meth = CodeGenUtil.DateGe; break;
                case CompareRoot.Less: meth = CodeGenUtil.DateLt; break;
                case CompareRoot.LessEqual: meth = CodeGenUtil.DateLe; break;
                default: throw Unexpected();
                }
                EmitCall(meth);
                break;

            case DKind.Time:
                Validation.Assert(!type.IsOpt);
                switch (root)
                {
                case CompareRoot.Equal:
                    meth = not ? CodeGenUtil.TimeNe : CodeGenUtil.TimeEq;
                    not = false;
                    break;
                case CompareRoot.Greater: meth = CodeGenUtil.TimeGt; break;
                case CompareRoot.GreaterEqual: meth = CodeGenUtil.TimeGe; break;
                case CompareRoot.Less: meth = CodeGenUtil.TimeLt; break;
                case CompareRoot.LessEqual: meth = CodeGenUtil.TimeLe; break;
                default: throw Unexpected();
                }
                EmitCall(meth);
                break;

            case DKind.Guid:
                bool gt;
                switch (root)
                {
                case CompareRoot.Equal:
                    EmitCall(CodeGenUtil.EquatableEqualsVal(typeof(Guid)));
                    if (not)
                        IL.Ldc_I4(0).Ceq();
                    return;
                case CompareRoot.Greater:
                    gt = true;
                    break;
                case CompareRoot.Less:
                    gt = false;
                    break;
                case CompareRoot.GreaterEqual:
                    gt = false;
                    not = !not;
                    break;
                case CompareRoot.LessEqual:
                    gt = true;
                    not = !not;
                    break;
                default:
                    throw Unexpected();
                }

                EmitCall(CodeGenUtil.ComparableCmpVal(typeof(Guid)));
                IL.Ldc_I4(0);
                if (gt)
                    IL.Cgt();
                else
                    IL.Clt();
                PopType(typeof(int));
                PushType(typeof(bool));
                break;

            case DKind.Uri:
                EmitCall(CodeGenUtil.EquatableEqualsOpt(typeof(Link), mods.IsStrict()));
                break;

            case DKind.Record:
            case DKind.Tuple:
                throw Unexpected();
            }

            if (not)
                IL.Ldc_I4(0).Ceq();
        }

        /// <summary>
        /// Encapsulates an operand that lives in a local or arg.
        /// </summary>
        private struct Loc
        {
            /// <summary>
            /// If we had to allocate a local, this is that local.
            /// </summary>
            private LocEnt _ent;

            /// <summary>
            /// This may be active when <see cref="_ent"/> isn't. This is the local or arg
            /// that contains the operand.
            /// </summary>
            private LocArgInfo _lai;

            /// <summary>
            /// Returns whether this is "active" (contains an operand).
            /// </summary>
            public bool IsActive => _lai.IsActive;

            /// <summary>
            /// The system type, when active.
            /// </summary>
            public Type Type => _lai.SysType;

            /// <summary>
            /// Ensures that <paramref name="arg"/> is in a local or argument, generating the value and
            /// storing in an allocated local when needed. If <paramref name="load"/> is true, also
            /// places the value on the stack.
            /// </summary>
            public void EnsureLocOrArg(Impl impl, BoundNode arg, ref int cur, Type st, bool load)
            {
                Validation.Assert(!IsActive);
                _ent = impl.EnsureLocOrArg(arg, ref cur, st, out _lai, load: load);
                if (load)
                    impl.PushType(st);
            }

            /// <summary>
            /// Invoke the HasValue property on this (nullable) local.
            /// </summary>
            public void GenHasValue(Impl impl, DType typeOpt)
            {
                Validation.Assert(typeOpt.HasReq);
                impl._typeManager.GenHasValue(impl.MethCur, typeOpt, in _lai);
                impl.PushType(typeof(bool));
            }

            /// <summary>
            /// Invoke the GetValueOrDefault() method on this (nullable) local.
            /// </summary>
            public void GenGetValue(Impl impl, DType typeOpt)
            {
                Validation.Assert(typeOpt.HasReq);
                var st = impl._typeManager.GenGetValueOrDefault(impl.MethCur, typeOpt, in _lai);
                impl.PushType(st);
            }

            /// <summary>
            /// Invoke the GetValueOrDefault() method on this (nullable) local and change this local
            /// to a req local containing that value.
            /// </summary>
            public void GenToReq(Impl impl, DType typeOpt)
            {
                Validation.Assert(typeOpt.HasReq);
                var st = impl._typeManager.GenGetValueOrDefault(impl.MethCur, typeOpt, in _lai);
                Dispose();
                _ent = impl.MethCur.AcquireLocal(st);
                impl.IL.Stloc(_ent);
                _lai = _ent;
            }

            /// <summary>
            /// Pushes the value of this local on the stack.
            /// </summary>
            public void Push(Impl impl)
            {
                Validation.Assert(IsActive);
                impl.IL.LdLocArg(in _lai);
                impl.PushType(_lai.SysType);
            }

            /// <summary>
            /// Disposes this local (if it has an allocated local).
            /// </summary>
            public void Dispose()
            {
                _ent.Dispose();
                _ent = default;
                _lai = default;
            }
        }

        /// <summary>
        /// For generating comparison chains when the comparison type is a value type and some of the
        /// operands are opt (nullable).
        /// REVIEW: Improve this for args that are opt fields of records.
        /// </summary>
        private struct CmpGenValType
        {
            // Readonly state.
            private readonly Impl _impl;
            private readonly BndCompareNode _bnd;
            private readonly DType _typeReq;
            private readonly DType _typeOpt;
            private readonly Type _stReq;
            private readonly Type _stOpt;
            private readonly Immutable.Array<NullTo> _nullTo;
            private readonly int _iopLim;
            // The expected end node index.
            private readonly int _end;

            // The following state tracks the current "clause".

            // Current arg index.
            private int _iargCur;
            // Whether this is the last clause.
            private bool _isLast;
            // Whether we'll need the right operand again, for the next clause.
            private bool _again;
            // The current operator.
            private CompareOp _opCur;

            // Expected node indices.
            private int _idx0; // Starting index for left.
            private int _idx1; // Starting index for right.
            private int _idx2; // Ending index for right.

            // These track the opt-ness of the operands for the current clause.
            // 0 means left, 1 means right.
            private bool _isOpt0;
            private bool _isOpt1;

            // These track what to do with a null operand for the current clause.
            // 0 means left, 1 means right.
            private NullTo _nullTo0;
            private NullTo _nullTo1;

            // The locals for the current operands, 0 for left, 1 for right.
            private Loc _loc0;
            private Loc _loc1;

            // Labels for generating a final false or true value.
            private Label _labFalse;
            private Label _labTrue;

            /// <summary>
            /// The main entry point.
            /// </summary>
            public static void Gen(Impl impl, BndCompareNode bnd, int idx)
            {
                var gen = new CmpGenValType(impl, bnd, idx);
                gen.Run();
            }

            private CmpGenValType(Impl impl, BndCompareNode bnd, int idx)
            {
                Validation.AssertValue(impl);
                impl.AssertIdx(bnd, idx);

                // If none are opt, we shouldn't get here. The code in that case is much simpler.
                Validation.Assert(!bnd.IsOpt.IsEmpty);

                // These are guaranteed by BndCompareNode.
                Validation.Assert(bnd.Args.Length >= 2);
                Validation.Assert(bnd.Args.Length == bnd.Ops.Length + 1);
                Validation.Assert(bnd.Ops[0] != CompareOp.None);
                Validation.Assert(bnd.Ops[bnd.Ops.Length - 1] != CompareOp.None);

                _impl = impl;
                _bnd = bnd;
                _end = idx + bnd.NodeCount;
                _nullTo = bnd.NullToFlags;

                _iopLim = _bnd.Ops.Length;

                // All operand types should be the same or differ only in opt-ness.
                _typeReq = bnd.ArgType;
                _typeOpt = _typeReq.ToOpt();
                Validation.Assert(_typeReq.IsEquatable);

                _stReq = _impl.GetSysType(_typeReq);
                Validation.Assert(_stReq.IsValueType);
                _stOpt = _impl.GetSysType(_typeOpt);
                Validation.Assert(_impl.TypeManager.IsNullableType(_stOpt));

                // Initialize things for the first loop iteration in Run.
                _iargCur = 0;
                _isLast = false;
                _again = false;
                _opCur = CompareOp.None;
                _isOpt0 = false;
                _isOpt1 = _bnd.IsOpt.TestBit(_iargCur);
                _idx0 = -1;
                _idx1 = idx + 1;
                _idx2 = _idx1 + _bnd.Args[0].NodeCount;

                _nullTo0 = NullTo.None;
                _nullTo1 = _nullTo[0];

                _loc0 = default;
                _loc1 = default;

                _labFalse = default;
                _labTrue = default;
            }

            /// <summary>
            /// Generate the code for all the clauses. This is the main entry point.
            /// </summary>
            private void Run()
            {
                int depthInit = _impl._frameCur.StackDepth;

                try
                {
                    while (++_iargCur <= _iopLim)
                    {
                        // Shift, skipping over ops that are CompareOp.None.
                        _idx0 = _idx1;
                        _idx1 = _idx2;
                        _idx2 += _bnd.Args[_iargCur].NodeCount;

                        _isOpt0 = _isOpt1;
                        _isOpt1 = _bnd.IsOpt.TestBit(_iargCur);
                        _nullTo0 = _nullTo1;
                        _nullTo1 = _nullTo[_iargCur];

                        _loc0.Dispose();
                        _loc0 = _loc1;
                        _loc1 = default;

                        _opCur = _bnd.Ops[_iargCur - 1];
                        if (_opCur == CompareOp.None)
                            continue;

                        _isLast = _iargCur >= _iopLim;
                        _again = !_isLast && _bnd.Ops[_iargCur] != CompareOp.None;

                        // Simplify the op, give the types.
                        _opCur = _opCur.SimplifyForType(
                            _isOpt0 ? _typeOpt : _typeReq, _isOpt1 ? _typeOpt : _typeReq);

                        Validation.Assert(!_isLast | !_again);
                        Validation.Assert(!_loc1.IsActive);
                        GenOne();
                    }
                }
                finally
                {
                    _loc0.Dispose();
                    _loc1.Dispose();
                }

                // Shouldn't have any args left over.
                Validation.Assert(_isLast);
                Validation.Assert(!_again);
                Validation.Assert(_idx2 == _end);

                // Stack state should be a single bool.
                Validation.Assert(_impl._frameCur.StackDepth == depthInit + 1);
                _impl.PeekType(typeof(bool));

                // Generate true/false sections and labels.
                Label labDone = default;
                if (_impl.IL.IsLabelUsed(_labTrue))
                {
                    _impl.IL
                        .Br(ref labDone)
                        .MarkLabel(_labTrue)
                        .Ldc_I4(1);
                }
                if (_impl.IL.IsLabelUsed(_labFalse))
                {
                    _impl.IL
                        .Br(ref labDone)
                        .MarkLabel(_labFalse)
                        .Ldc_I4(0);
                }
                _impl.IL.MarkLabelIfUsed(labDone);
            }

            /// <summary>
            /// Generate the code for the current clause.
            /// </summary>
            private void GenOne()
            {
                Label labNext = default;

                // Deal with nullable. Note that we need to deal with mapping null => false before
                // mapping null => true. It is possible that one operand says null => false while the
                // other specifies null => true.
                if (!_isOpt0)
                {
                    if (_isOpt1)
                    {
                        Validation.Assert((_nullTo1 & NullTo.Right) != 0);
                        NullRightToDst(ref labNext);
                    }
                }
                else if (!_isOpt1)
                {
                    Validation.Assert((_nullTo0 & NullTo.Left) != 0);
                    NullLeftToDst(ref labNext);
                }
                // From here on, both operands are opt.
                else if ((_nullTo0 & NullTo.False) != 0)
                {
                    // Left null => false.
                    Validation.Assert((_nullTo1 & NullTo.Right) != 0);
                    NullLeftToFalse();
                    NullRightToDst(ref labNext);
                }
                else if ((_nullTo1 & NullTo.False) != 0)
                {
                    // Right null => false.
                    Validation.Assert((_nullTo0 & NullTo.Left) != 0);
                    NullRightToFalse();
                    NullLeftToDst(ref labNext);
                }
                else if ((_nullTo1 & NullTo.TrueRight) != 0)
                {
                    // Right null => true.
                    // Note that this goes goes before "left null => true" since right may need to for the next
                    // clause (when _again is true). Also, this case handles when both map null to true.
                    NullRightToDst(ref labNext);
                    EnsureLeftLocal();
                    if ((_nullTo0 & NullTo.TrueLeft) != 0)
                    {
                        // Left null => true.
                        NullToTrue(in _loc0, ref labNext);
                    }
                    else
                    {
                        // Left null => false, in the context of right being non-null.
                        NullToFalse(in _loc0);
                    }
                }
                else if ((_nullTo0 & NullTo.TrueLeft) != 0)
                {
                    // Left null => true.
                    Validation.Assert((_nullTo1 & NullTo.Right) == 0);
                    NullLeftToDst(ref labNext);
                    EnsureRightLocal();
                    // Right null => false, in the context of left being non-null.
                    NullToFalse(in _loc1);
                }
                else
                {
                    // The only cases left are non-strict = and !=.
                    Validation.Assert(!_opCur.IsStrict);
                    Validation.Assert(_opCur.Root == CompareRoot.Equal);

                    // For pos, null-ness and value must match.
                    // For not, null-ness or value must differ.
                    EnsureLeftLocal();
                    EnsureRightLocal();

                    GenHasValue(in _loc0);
                    GenHasValue(in _loc1);

                    if (!_opCur.IsNot)
                        _impl.IL.Bne_Un(ref _labFalse);
                    else if (_isLast)
                        _impl.IL.Bne_Un(ref _labTrue);
                    else
                        _impl.IL.Bne_Un(ref labNext);
                    _impl.PopType(typeof(bool));
                    _impl.PopType(typeof(bool));
                }

                // Put the req values on the stack and compare.
                EnsureReqLeftStack();
                EnsureReqRightStack();

                if (_isLast)
                    _impl.GenCmp(_opCur, _typeReq);
                else
                    _impl.GenCmpBr(_opCur, _typeReq, ref _labFalse);

                _impl.IL.MarkLabelIfUsed(labNext);
            }

            /// <summary>
            /// Ensure that the current left arg is in <see cref="_loc0"/>.
            /// </summary>
            private void EnsureLeftLocal()
            {
                Validation.Assert(_isOpt0);
                if (!_loc0.IsActive)
                {
                    int cur = _idx0;
                    GenToLocal(_iargCur - 1, ref cur, _isOpt0, ref _loc0);
                    Validation.Assert(cur == _idx1);
                }
                Validation.Assert(_loc0.Type == _stOpt);
            }

            /// <summary>
            /// Ensure that the current left arg is in <see cref="_loc1"/>.
            /// </summary>
            private void EnsureRightLocal(bool load = false)
            {
                if (!_loc1.IsActive)
                {
                    int cur = _idx1;
                    GenToLocal(_iargCur, ref cur, _isOpt1, ref _loc1, load);
                    Validation.Assert(cur == _idx2);
                }
                else if (load)
                    _loc1.Push(_impl);
                Validation.Assert(_loc1.Type == (_isOpt1 ? _stOpt : _stReq));
            }

            private void EnsureReqLeftStack()
            {
                if (!_isOpt0)
                {
                    int cur = _idx0;
                    GenReqToStack(_iargCur - 1, ref cur, ref _loc0);
                    Validation.Assert(cur == _idx1);
                }
                else
                {
                    // Should have dealt with null-ness, so must already be in its local.
                    GenGetValue(in _loc0);
                    _loc0.Dispose();
                }
                _impl.PeekType(_stReq);
            }

            private void EnsureReqRightStack()
            {
                if (!_isOpt1)
                {
                    if (_again)
                        EnsureRightLocal(load: true);
                    else
                    {
                        int cur = _idx1;
                        GenReqToStack(_iargCur, ref cur, ref _loc1);
                        Validation.Assert(cur == _idx2);
                    }
                }
                else
                {
                    // Should have dealt with null-ness, so must already be in its local.
                    GenGetValue(in _loc1);
                    if (!_again)
                        _loc1.Dispose();
                    else
                    {
                        // If a null check were needed, it should have morphed the local to req.
                        Validation.Assert((_nullTo1 & NullTo.False) == 0);
                    }
                }
                _impl.PeekType(_stReq);
            }

            /// <summary>
            /// Generate the indicated arg to a <see cref="Loc"/>.
            /// </summary>
            private void GenToLocal(int iarg, ref int cur, bool opt, ref Loc loc, bool load = false)
            {
                Validation.AssertIndexInclusive(iarg, _iopLim);
                Validation.Assert(!loc.IsActive);

                Type st = opt ? _stOpt : _stReq;
                loc.EnsureLocOrArg(_impl, _bnd.Args[iarg], ref cur, st, load: load);

                Validation.Assert(loc.Type == st);
                if (load)
                    _impl.PeekType(st);
            }

            /// <summary>
            /// Generate the indicated req arg to the stack. The value may already be in the given
            /// <paramref name="loc"/>, in which case, the <paramref name="loc"/> is pushed and disposed.
            /// </summary>
            private void GenReqToStack(int iarg, ref int cur, ref Loc loc)
            {
                Validation.AssertIndexInclusive(iarg, _iopLim);

                if (!loc.IsActive)
                {
                    // Generate.
                    Validation.Assert(_bnd.Args[iarg].Type == _typeReq);
                    cur = _bnd.Args[iarg].Accept(_impl, cur);
                }
                else
                {
                    Validation.Assert(loc.Type == _stReq);
                    loc.Push(_impl);
                    loc.Dispose();
                    cur += _bnd.Args[iarg].NodeCount;
                }
                Validation.Assert(!loc.IsActive);
                _impl.PeekType(_stReq);
            }

            #region Nullable handling

            /// <summary>
            /// Invoke the HasValue property on the nullable local and leave the value
            /// on the stack.
            /// </summary>
            private void GenHasValue(in Loc loc)
            {
                Validation.Assert(loc.Type == _stOpt);
                loc.GenHasValue(_impl, _typeOpt);
            }

            /// <summary>
            /// Branch to false if the nullable local contains null.
            /// </summary>
            private void NullToFalse(in Loc loc)
            {
                GenHasValue(in loc);
                _impl.IL.Brfalse(ref _labFalse);
                _impl.PopType(typeof(bool));
            }

            /// <summary>
            /// Branch to true/skip if the nullable local contains null.
            /// </summary>
            private void NullToTrue(in Loc loc, ref Label labNext)
            {
                if (_isLast)
                {
                    GenHasValue(in loc);
                    _impl.IL.Brfalse(ref _labTrue);
                }
                else
                {
                    GenHasValue(in loc);
                    _impl.IL.Brfalse(ref labNext);
                }
                _impl.PopType(typeof(bool));
            }

            /// <summary>
            /// If left is null, branch to false.
            /// </summary>
            private void NullLeftToFalse()
            {
                Validation.Assert(_isOpt0);
                Validation.Assert((_nullTo0 & NullTo.False) != 0);
                EnsureLeftLocal();
                NullToFalse(in _loc0);
            }

            /// <summary>
            /// If right is null, branch to false. If <see cref="_again"/> is true, morph <see cref="_loc1"/>
            /// to contain the req value.
            /// </summary>
            private void NullRightToFalse()
            {
                Validation.Assert(_isOpt1);
                Validation.Assert((_nullTo1 & NullTo.False) != 0);
                EnsureRightLocal();
                NullToFalse(in _loc1);
                if (_again)
                {
                    _loc1.GenToReq(_impl, _typeOpt);
                    _isOpt1 = false;
                }
            }

            /// <summary>
            /// If left is null, branch to the correct destination based on <see cref="_nullTo0"/>.
            /// If <see cref="_again"/> is true, and destination is "true" this also ensures that
            /// right is in its local.
            /// </summary>
            private void NullLeftToDst(ref Label labNext)
            {
                Validation.Assert((_nullTo0 & NullTo.Left) != 0);

                if ((_nullTo0 & NullTo.False) != 0)
                    NullLeftToFalse();
                else
                {
                    EnsureLeftLocal();
                    if (_again && !_loc1.IsActive)
                    {
                        // The null test for right should be done before this!
                        Validation.Assert(!_isOpt1 || (_nullTo1 & NullTo.False) == 0);
                        EnsureRightLocal();
                    }
                    NullToTrue(in _loc0, ref labNext);
                }
            }

            private void NullRightToDst(ref Label labNext)
            {
                Validation.Assert((_nullTo1 & NullTo.Right) != 0);

                if ((_nullTo1 & NullTo.False) != 0)
                    NullRightToFalse();
                else
                {
                    EnsureRightLocal();
                    NullToTrue(in _loc1, ref labNext);
                }
            }

            /// <summary>
            /// Invoke the GetValueOrDefault() method on the nullable local and leave the (req) value on the stack.
            /// </summary>
            private void GenGetValue(in Loc loc)
            {
                Validation.Assert(loc.Type == _stOpt);
                loc.GenGetValue(_impl, _typeOpt);
            }

            #endregion Nullable handling
        }
    }
}
