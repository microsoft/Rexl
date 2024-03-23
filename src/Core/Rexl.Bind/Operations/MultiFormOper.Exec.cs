// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

using TypeTuple = Immutable.Array<DType>;

// This partial contains execution opers.
partial class MultiFormOper<TCookie>
{
    /// <summary>
    /// Interface for post-reduction functions or procedures. Implementors
    /// should inherit from <see cref="RexlOper"/>.
    /// </summary>
    public abstract partial class ExecutionOper : RexlOper
    {
        /// <summary>
        /// The parent rexl operation.
        /// </summary>
        public sealed override MultiFormOper<TCookie> Parent { get; }

        /// <summary>
        /// The invocation form that this exec function is based on. WARNING: Some properties of the
        /// invocation form may be misleading. For example the <see cref="InvocationForm.TypeIn"/>
        /// and <see cref="InvocationForm.TypeMis"/> properties may not match this exec func, because
        /// of "wild carding".
        /// </summary>
        public InvocationForm Form { get; }

        /// <summary>
        /// Instances of this class have fixed arity.
        /// </summary>
        public int Arity => ArityMin;

        /// <summary>
        /// Whether this exec func does merging.
        /// </summary>
        public bool Merges => MergeInfo is not null;

        /// <summary>
        /// Gets the merge information for this exec, if there is one, otherwise null.
        /// </summary>
        public abstract MergeInfo MergeInfo { get; }

        /// <summary>
        /// This is the input type for this execution. Note that this may be a specialization of the
        /// <see cref="InvocationForm.TypeIn"/> property of <see cref="Form"/>. That is, if the latter
        /// includes any uses of the general type, those may be replaced with different types. An
        /// occurence of the general type is thus considered a "wild card".
        /// REVIEW: Perhaps empty record should be a wild card for "any record type"? Alternatively,
        /// we could invent more complex "templating" or "polymorphism" schemes.
        /// </summary>
        public DType TypeIn { get; }

        /// <summary>
        /// The type of the execution output, before merging.
        /// </summary>
        public DType TypeOut => Form.TypeOut;

        /// <summary>
        /// The type of the source sequence, if it exists and merges.
        /// </summary>
        public DType TypeSrc => MergeInfo is not null ? MergeInfo.TypeSrc : default;

        /// <summary>
        /// The type of the final output, after merging and record creation. If this
        /// class is a func, then it is also the type of the BndCallNode.
        /// </summary>
        public DType TypeDst { get; }

        /// <summary>
        /// The type of the bound node for binding purposes. This should be General for
        /// procedures and <see cref="TypeDst"/> otherwise.
        /// </summary>
        public DType TypeCall => IsFunc ? TypeDst : DType.General;

        /// <summary>
        /// The DTypes for the <see cref="BndCallNode"/>s using this op.
        /// </summary>
        public TypeTuple ArgTypes { get; }

        /// <summary>
        /// This is for convenience, to use the standard exec funcs. If custom exec funcs are needed,
        /// this should not be called.
        /// </summary>
        public static ExecutionOper Create(MultiFormOper<TCookie> parent, InvocationForm form, DType typeIn,
            MergeInfo merge = null)
        {
            Validation.AssertValue(parent);
            Validation.AssertValue(form);
            Validation.Assert(form.TypeIn.Includes(typeIn) || form.AllowSingleIn && form.TypeIn.ItemTypeOrThis.Includes(typeIn));
            Validation.AssertValueOrNull(merge);

            if (merge == null)
                return SimpleExecOper.Create(parent, form, typeIn);

            Validation.Assert(form.CanMerge);

            if (form.TypeMis == form.TypeIn)
                return MergeSeqExecOper.Create(parent, form, typeIn, merge);

            if (form.TypeIn.IsRecordReq)
                return MergeRecExecOper.Create(parent, form, typeIn, merge);

            throw Validation.BugExcept("Unexpected form");
        }

        protected ExecutionOper(
                MultiFormOper<TCookie> parent, InvocationForm form, DType typeIn,
                DName name, NPath ns, DType typeRet, TypeTuple types)
            : base(isFunc: parent.VerifyValue().IsFunc, name, ns, types.Length, types.Length)
        {
            Validation.AssertValue(form);
            Validation.Assert(form.TypeIn.Includes(typeIn) || form.AllowSingleIn && form.TypeIn.ItemTypeOrThis.Includes(typeIn));
            Validation.Assert(typeRet.IsValid);
            Validation.Assert(ArityMin == ArityMax);
            Validation.Assert(ArityMin >= 1);
            Validation.Assert(ArityMin == types.Length);

            Parent = parent;
            Form = form;
            TypeIn = typeIn;
            TypeDst = typeRet;
            ArgTypes = types;
        }

        protected override (DType typeRes, TypeTuple typesArg) SpecializeTypesCore(InvocationInfo info)
        {
            Validation.Assert(SupportsArity(info.Arity));
            return (TypeDst, ArgTypes);
        }

        protected override bool CertifyCore(BndCallNode call, ref bool full)
        {
            if (call.Type != TypeCall)
                return false;
            var args = call.Args;
            if (args.Length != ArgTypes.Length)
                return false;
            for (int slot = 0; slot < ArgTypes.Length; slot++)
            {
                if (args[slot].Type != ArgTypes[slot])
                    return false;
            }
            return true;
        }

        protected override bool IsUnboundedCore(BndCallNode call)
        {
            Validation.Assert(IsValidCall(call, true));
            return true;
        }
    }

    /// <summary>
    /// For simple 1-arg exec form with no merging.
    /// </summary>
    protected abstract partial class SimpleExecBase : ExecutionOper
    {
        public sealed override MergeInfo MergeInfo => null;

        protected SimpleExecBase(
                MultiFormOper<TCookie> parent, InvocationForm form, DType typeIn,
                DName name, NPath ns, DType typeRet, TypeTuple types)
            : base(parent, form, typeIn, name, ns, typeRet, types)
        {
        }
    }

    /// <summary>
    /// For simple 1-arg exec form with no merging.
    /// </summary>
    protected sealed partial class SimpleExecOper : SimpleExecBase
    {
        public static SimpleExecOper Create(MultiFormOper<TCookie> parent, InvocationForm form, DType typeIn)
        {
            Validation.AssertValue(parent);
            Validation.AssertValue(form);
            Validation.Assert(form.TypeIn.Includes(typeIn) || form.AllowSingleIn && form.TypeIn.ItemTypeOrThis.Includes(typeIn));

            return new SimpleExecOper(parent, form, typeIn, new DName("Simple"), parent.Path, form.TypeOut, TypeTuple.Create(typeIn));
        }

        private SimpleExecOper(
                MultiFormOper<TCookie> parent, InvocationForm form, DType typeIn,
                DName name, NPath ns, DType typeRet, TypeTuple types)
            : base(parent, form, typeIn, name, ns, typeRet, types)
        {
        }

        protected override ArgTraits GetArgTraitsCore(int carg)
        {
            Validation.Assert(SupportsArity(carg));
            // REVIEW: Whether this processes the sequence lazily depends on the function semantics.
            return ArgTraitsSimple.Create(this, eager: true, carg);
        }
    }

    /// <summary>
    /// Base class for exec with mapping and merging.
    /// </summary>
    protected abstract class MergeExecBase : ExecutionOper
    {
        public sealed override MergeInfo MergeInfo { get; }

        protected MergeExecBase(
                MultiFormOper<TCookie> parent, InvocationForm form, DType typeIn, MergeInfo merge,
                DName name, NPath ns, DType typeRet, TypeTuple types)
            : base(parent, form, typeIn, name, ns, typeRet, types)
        {
            Validation.BugCheckValue(merge, nameof(merge));
            MergeInfo = merge;
        }
    }

    /// <summary>
    /// For sequence based mapping with merging.
    /// </summary>
    protected abstract class MergeSeqExecBase : MergeExecBase
    {
        protected MergeSeqExecBase(
                MultiFormOper<TCookie> parent, InvocationForm form, DType typeIn, MergeInfo merge,
                DName name, NPath ns, DType typeRet, TypeTuple types)
            : base(parent, form, typeIn, merge, name, ns, typeRet, types)
        {
        }
    }

    /// <summary>
    /// For sequence based mapping with merging.
    /// </summary>
    protected sealed class MergeSeqExecOper : MergeSeqExecBase
    {
        new public static MergeSeqExecOper Create(MultiFormOper<TCookie> parent, InvocationForm form, DType typeIn, MergeInfo merge)
        {
            Validation.AssertValue(parent);
            Validation.AssertValue(form);
            Validation.Assert(form.TypeIn.Includes(typeIn) || form.AllowSingleIn && form.TypeIn.ItemTypeOrThis.Includes(typeIn));
            Validation.AssertValue(merge);
            Validation.Assert(form.TypeIn == form.TypeMis);

            return new MergeSeqExecOper(parent, form, typeIn, merge, new DName("MergeSeq"), parent.Path, merge.TypeDst,
                TypeTuple.Create(merge.TypeSrc, typeIn, merge.TypeSrc));
        }

        private MergeSeqExecOper(
                MultiFormOper<TCookie> parent, InvocationForm form, DType typeIn, MergeInfo merge,
                DName name, NPath ns, DType typeRet, TypeTuple types)
            : base(parent, form, typeIn, merge, name, ns, typeRet, types)
        {
        }

        protected override ArgTraits GetArgTraitsCore(int carg)
        {
            Validation.Assert(SupportsArity(carg));
            return ArgTraitsExecMerge.Create(this, carg);
        }
    }

    /// <summary>
    /// For top level record based mapping with merging.
    /// </summary>
    protected abstract class MergeRecExecBase : MergeExecBase
    {
        protected MergeRecExecBase(
                MultiFormOper<TCookie> parent, InvocationForm form, DType typeIn, MergeInfo merge,
                DName name, NPath ns, DType typeRet, TypeTuple types)
            : base(parent, form, typeIn, merge, name, ns, typeRet, types)
        {
        }
    }

    /// <summary>
    /// For top level record based mapping with merging.
    /// </summary>
    protected sealed class MergeRecExecOper : MergeRecExecBase
    {
        new public static MergeRecExecOper Create(MultiFormOper<TCookie> parent, InvocationForm form, DType typeIn, MergeInfo merge)
        {
            Validation.AssertValue(parent);
            Validation.AssertValue(form);
            Validation.Assert(form.TypeIn.Includes(typeIn));
            Validation.AssertValue(merge);
            Validation.Assert(form.TypeIn.IsRecordReq);

            return new MergeRecExecOper(
                parent, form, typeIn, merge,
                new DName("MergeRec"), parent.Path, merge.TypeDst, TypeTuple.Create(merge.TypeSrc, typeIn, merge.TypeSrc));
        }

        private MergeRecExecOper(
                MultiFormOper<TCookie> parent, InvocationForm form, DType typeIn, MergeInfo merge,
                DName name, NPath ns, DType typeRet, TypeTuple types)
            : base(parent, form, typeIn, merge, name, ns, typeRet, types)
        {
        }

        protected override ArgTraits GetArgTraitsCore(int carg)
        {
            Validation.Assert(SupportsArity(carg));
            return ArgTraitsExecMerge.Create(this, carg);
        }
    }
}
