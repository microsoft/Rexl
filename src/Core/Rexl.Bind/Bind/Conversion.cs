// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Bind;

using ArgTuple = Immutable.Array<BoundNode>;
using ScopeTuple = Immutable.Array<ArgScope>;

public static class Conversion
{
    /// <summary>
    /// Cast the given <paramref name="src"/> to the given <paramref name="typeDst"/>.
    /// This asserts that the destination type accepts the source type. It does not generate
    /// any errors, but may generate warnings.
    /// </summary>
    public static BoundNode CastBnd(IReducerHost host, BoundNode src, DType typeDst, bool union)
    {
        Validation.BugCheckValue(host, nameof(host));
        Validation.BugCheckValue(src, nameof(src));
        Validation.BugCheckParam(typeDst.IsValid, nameof(typeDst));

        DType typeSrc = src.Type;

        // This should only be called for legal conversions.
        Validation.BugCheckParam(typeDst.Accepts(typeSrc, union), nameof(typeDst));

        // See if we need to do anything.
        if (typeDst == typeSrc)
            return src;

        // Handle conversion to general type.
        if (typeDst.IsGeneral)
            return host.Associate(src, BndCastNode.CastGeneral(src));

        // Handle null value and the Null type as source.
        if (src.IsKnownNull)
            return host.Associate(src, BndNullNode.Create(typeDst));

        // Handle vac type as source.
        if (typeSrc.IsVac)
            return host.Associate(src, BndCastVacNode.Create(src, typeDst));

        // Handle reference conversion.
        if (IsRefConv(typeSrc, typeDst))
            return BndCastRefNode.Create(src, typeDst);

        // Handle conversion to a sequence type.
        if (typeDst.SeqCount > 0)
            return CastSequence(host, src, typeDst, union);

        // Now that the destination is not a sequence or general, the source can't be a sequence.
        Validation.Assert(typeSrc.SeqCount == 0);

        // Handle conversion to an opt (non-sequence, non-record) type.
        if (typeDst.IsOpt)
        {
            // Since the only thing accepted by string is vac/null and those are handled above,
            // the destination type should have a Req form.
            Validation.Assert(typeDst.HasReq);
            DType typeReq = typeDst.ToReq();
            Validation.BugCheck(typeReq != typeDst);

            if (typeSrc.IsOpt)
            {
                // Map null to null, so Guard over the non-opt conversion.
                var scope = ArgScope.Create(ScopeKind.Guard, typeSrc.ToReq());
                var scopeRef = host.Associate(src, BndScopeRefNode.Create(scope));
                return host.Associate(src,
                    BndCallNode.Create(WithFunc.Guard, typeDst,
                        ArgTuple.Create(src, CastBnd(host, scopeRef, typeReq, union)), ScopeTuple.Create(scope)));
            }

            // First cast to the non-nullable.
            var tmp = CastBnd(host, src, typeReq, union);
            Validation.Assert(tmp.Type == typeReq);

            // Now convert to opt.
            tmp = host.Associate(src, BndCastOptNode.Create(tmp));
            Validation.Assert(tmp.Type == typeDst);
            return tmp;
        }

        // Only non-opts make it here.
        Validation.Assert(!typeDst.IsOpt);
        Validation.Assert(!typeSrc.IsOpt);

        // Handle conversion of special types.
        switch (typeSrc.RootKind)
        {
        case DKind.Module:
            Validation.Assert(typeDst.IsRecordReq);
            src = host.Associate(src, BndModToRecNode.Create(src));
            if (src.Type == typeDst)
                return src;
            return CastRecord(host, src, typeDst, union);
        case DKind.Record:
            return CastRecord(host, src, typeDst, union);
        case DKind.Tuple:
            return CastTuple(host, src, typeDst, union);
        case DKind.Tensor:
            return CastTensor(host, src, typeDst, union);
        }

        // Numeric conversion.
        if (typeDst.IsNumericReq)
        {
            var res = BndCastNumNode.Create(src, typeDst, host);
            if (host != null && typeDst.RootKind == DKind.I8 && src.Type.RootKind == DKind.U8 &&
                res is BndCastNumNode bcn && bcn.Child == src)
            {
                host.Warn(src, ErrorStrings.WrnU8ToI8);
            }
            return host.Associate(src, res);
        }

        // REVIEW: Can we get here?
        throw Validation.BugExcept("Unhandled conversion");
    }

    private static BoundNode CastSequence(IReducerHost host, BoundNode src, DType typeDst, bool union)
    {
        Validation.AssertValue(host);
        Validation.AssertValue(src);
        Validation.Assert(typeDst.SeqCount > 0);

        DType typeSrc = src.Type;

        // Caller should have ensured these.
        Validation.Assert(typeDst.Accepts(typeSrc, union));
        Validation.Assert(typeDst != typeSrc);
        Validation.Assert(!src.IsNullValue);
        Validation.Assert(!typeSrc.IsVacXxx);
        Validation.Assert(typeSrc.SeqCount > 0);
        Validation.Assert(!IsRefConv(typeSrc, typeDst));

        // This one is implied !IsRefConv.
        Validation.Assert(typeDst.SeqCount >= typeSrc.SeqCount);

        DType typeItemDst = typeDst.ItemTypeOrThis;

        // If the source is a literal sequence, map the conversion through it.
        if (src is BndSequenceNode bsn)
        {
            // Map through the sequence literal.
            var bldr = bsn.Items.ToBuilder();
            for (int i = 0; i < bldr.Count; i++)
            {
                // Recursive call.
                bldr[i] = CastBnd(host, bldr[i], typeItemDst, union);
            }
            return host.Associate(src, BndSequenceNode.Create(typeDst, bldr.ToImmutable()));
        }

        {
            // Lift the conversion over seq using ForEach.
            var scope = ArgScope.Create(ScopeKind.SeqItem, typeSrc.ItemTypeOrThis);
            var scopeRef = host.Associate(src, BndScopeRefNode.Create(scope));
            return host.Associate(src, BndCallNode.Create(
                ForEachFunc.ForEach, typeDst,
                ArgTuple.Create(src, CastBnd(host, scopeRef, typeItemDst, union)),
                ScopeTuple.Create(scope)));
        }
    }

    private static BoundNode CastTensor(IReducerHost host, BoundNode src, DType typeDst, bool union)
    {
        Validation.AssertValue(host);
        Validation.AssertValue(src);
        Validation.Assert(typeDst.IsTensorReq);

        DType typeSrc = src.Type;

        // Caller should have ensured these.
        Validation.Assert(typeDst.Accepts(typeSrc, union));
        Validation.Assert(typeDst != typeSrc);
        Validation.Assert(!typeSrc.IsVacXxx);
        Validation.Assert(typeSrc.IsTensorReq);
        Validation.Assert(!IsRefConv(typeSrc, typeDst));

        // REVIEW: Perhaps there should be a BndCastTensorNode and these reductions should be done by the reducer?

        var typeItemSrc = typeSrc.GetTensorItemType();
        var typeItemDst = typeDst.GetTensorItemType();

        // If the source is a standard tensor construction, map the conversion through it.
        if (src is BndCallNode call)
        {
            if (call.Oper == TensorFillFunc.Instance)
            {
                var val = call.Args[0];
                Validation.Assert(val.Type == typeItemSrc);
                var args = call.Args.SetItem(0, CastBnd(host, val, typeItemDst, union));
                return host.Associate(src, BndCallNode.Create(call.Oper, typeDst, args));
            }
            if (call.Oper == TensorFromFunc.Instance)
            {
                var val = call.Args[0];
                Validation.Assert(val.Type == typeItemSrc.ToSequence());
                var args = call.Args.SetItem(0, CastBnd(host, call.Args[0], typeItemDst.ToSequence(), union));
                return host.Associate(src, BndCallNode.Create(call.Oper, typeDst, args));
            }

            // REVIEW: Are there others that we should handle?
        }

        // If the source is a literal tensor, map the conversion through it.
        if (src is BndTensorNode btn)
        {
            // Map through the tensor literal.
            var bldr = btn.Items.ToBuilder();
            for (int i = 0; i < bldr.Count; i++)
            {
                // Recursive call.
                bldr[i] = CastBnd(host, bldr[i], typeItemDst, union);
            }
            return host.Associate(src, BndTensorNode.Create(typeDst, bldr.ToImmutable(), btn.Shape));
        }

        {
            // Lift the conversion over tensor.
            var scope = ArgScope.Create(ScopeKind.TenItem, typeItemSrc);
            var scopeRef = host.Associate(src, BndScopeRefNode.Create(scope));
            return host.Associate(src, BndCallNode.Create(
                TensorForEachFunc.Lazy, typeDst,
                ArgTuple.Create(src, CastBnd(host, scopeRef, typeItemDst, union)),
                ScopeTuple.Create(scope)));
        }
    }

    /// <summary>
    /// Cast a (non-opt) record to another record type.
    /// </summary>
    private static BoundNode CastRecord(IReducerHost host, BoundNode src, DType typeDst, bool union)
    {
        Validation.AssertValue(host);
        Validation.AssertValue(src);
        Validation.Assert(typeDst.IsRecordReq);

        DType typeSrc = src.Type;

        // Caller should have ensured these.
        Validation.Assert(typeDst.Accepts(typeSrc, union));
        Validation.Assert(typeDst != typeSrc);
        Validation.Assert(typeSrc.IsRecordReq);
        Validation.Assert(!IsRefConv(typeSrc, typeDst));
        Validation.Assert(!typeDst.SameFieldReqs(typeSrc));

        // REVIEW: Perhaps there should be a BndCastRecordNode and these reductions should be done by the reducer?

        // Percolate the casts through to the fields.
        var items = NamedItems.Empty;
        if (src is BndRecordNode brn)
        {
            foreach (var (name, value) in brn.Items.GetPairs())
            {
                if (!typeDst.TryGetNameType(name, out DType typeFldDst))
                    continue;
                items = items.SetItem(name, CastBnd(host, value, typeFldDst, union));
            }
            return host.Associate(src, BndRecordNode.Create(typeDst, items, brn.NameHints));
        }

        var scope = ArgScope.Create(ScopeKind.With, typeSrc);
        foreach (var tn in typeDst.GetNames())
        {
            if (typeSrc.TryGetNameType(tn.Name, out var typeFld))
            {
                var tmp = host.Associate(src, BndGetFieldNode.Create(tn.Name, host.Associate(src, BndScopeRefNode.Create(scope))));
                Validation.Assert(tmp.Type == typeFld);
                items = items.SetItem(tn.Name, CastBnd(host, tmp, tn.Type, union));
            }
            else
            {
                Validation.Assert(union);
                Validation.Assert(tn.Type.IsOpt);
                items = items.SetItem(tn.Name, host.Associate(src, BndNullNode.Create(tn.Type)));
            }
        }
        var res = host.Associate(src, BndRecordNode.Create(typeDst, items));

        // Wrap in a With.
        return host.Associate(src, BndCallNode.Create(WithFunc.With, typeDst, ArgTuple.Create(src, res), ScopeTuple.Create(scope)));
    }

    /// <summary>
    /// Cast a (non-opt) tuple to another tuple type.
    /// </summary>
    private static BoundNode CastTuple(IReducerHost host, BoundNode bnd, DType typeDst, bool union)
    {
        Validation.AssertValue(host);
        Validation.AssertValue(bnd);
        Validation.Assert(typeDst.IsTupleReq);

        DType typeSrc = bnd.Type;

        // Caller should have ensured these.
        Validation.Assert(typeDst.Accepts(typeSrc, union));
        Validation.Assert(typeDst != typeSrc);
        Validation.Assert(typeSrc.IsTupleReq);
        Validation.Assert(!IsRefConv(typeSrc, typeDst));

        // REVIEW: Perhaps there should be a BndCastTupleNode and these reduction should be done by the reducer?

        // Percolate the casts through to the fields.
        var typesSrc = typeSrc.GetTupleSlotTypes();
        var typesDst = typeDst.GetTupleSlotTypes();
        ArgTuple.Builder bldr = ArgTuple.CreateBuilder(typesDst.Length, init: true);
        if (bnd is BndTupleNode btn)
        {
            Validation.Assert(typesDst.Length == btn.Items.Length);
            for (int i = 0; i < typesDst.Length; i++)
                bldr[i] = CastBnd(host, btn.Items[i], typesDst[i], union);
            return host.Associate(bnd, BndTupleNode.Create(bldr.ToImmutable(), typeDst));
        }

        var scope = ArgScope.Create(ScopeKind.With, typeSrc);
        for (int i = 0; i < typesDst.Length; i++)
        {
            var tmp = host.Associate(bnd, BndGetSlotNode.Create(i, host.Associate(bnd, BndScopeRefNode.Create(scope))));
            Validation.Assert(tmp.Type == typesSrc[i]);
            bldr[i] = CastBnd(host, tmp, typesDst[i], union);
        }
        var res = host.Associate(bnd, BndTupleNode.Create(bldr.ToImmutable(), typeDst));

        // Wrap in a With.
        return host.Associate(bnd, BndCallNode.Create(WithFunc.With, typeDst, ArgTuple.Create(bnd, res), ScopeTuple.Create(scope)));
    }

    /// <summary>
    /// Determines whether converting from <paramref name="typeSrc"/> to <paramref name="typeDst"/> is a reference
    /// conversion, so can be represented by <see cref="BndCastRefNode"/>. If <paramref name="allowCovariance"/>
    /// is false, the expected systems types need to match exactly. Effectively this means that there is an
    /// "outer" system type that is not covariant. Note that <see cref="System.Collections.Generic.IEnumerable{T}"/>
    /// is convariant, so this code assumes that sequences support covariance (unless <paramref name="allowCovariance"/>
    /// is false).
    /// </summary>
    public static bool IsRefConv(DType typeSrc, DType typeDst, bool allowCovariance = true)
    {
        Validation.Assert(typeSrc.IsValid);
        Validation.Assert(typeDst.IsValid);

        if (typeDst == typeSrc)
            return false;
        if (!typeDst.Accepts(typeSrc, DType.UseUnionDefault))
            return false;
        if (typeSrc.IsVacXxx)
            return false;

        var kindDst = typeDst.RootKind;
        int dseq = typeDst.SeqCount - typeSrc.SeqCount;
        if (dseq != 0)
            return dseq < 0 && allowCovariance && kindDst == DKind.General;

        var kindSrc = typeSrc.RootKind;
        if (!kindSrc.IsReferenceFriendly())
            return false;
        if (kindDst != kindSrc)
            return allowCovariance && kindDst == DKind.General;

        switch (kindDst)
        {
        case DKind.Uri:
            return true;
        case DKind.Record:
            return IsRecordRefConv(typeSrc.RootType, typeDst.RootType);
        case DKind.Tuple:
            return IsTupleRefConv(typeSrc.RootType, typeDst.RootType);
        case DKind.Tensor:
            return IsTensorRefConv(typeSrc.RootType, typeDst.RootType);
        }

        return false;
    }

    /// <summary>
    /// Return whether the conversion from record type typeSrc to record type typeDst is a reference conversion.
    /// Effectively, this must return false if there is a chance that a reasonably implemented TypeManager might
    /// use a different system type for the two record types.
    /// </summary>
    private static bool IsRecordRefConv(DType typeSrc, DType typeDst)
    {
        Validation.Assert(typeSrc.IsRecordXxx);
        Validation.Assert(typeDst.IsRecordXxx);
        Validation.Assert(typeSrc != typeDst);
        Validation.Assert(typeDst.Accepts(typeSrc, DType.UseUnionDefault));

        // REVIEW: This assumes that the type manager's record system type doesn't vary when only opt-ness
        // of field types vary. This is currently the case for the standard type managers, but this behavior may
        // need to be optional in the future.
        if (typeDst.SameFieldReqs(typeSrc))
            return true;

        // This case is included in the above test.
        // if (typeSrc.ToOpt() == typeDst)
        //     return true;

        // REVIEW: In the presence of nesting, it is possible to do better. For example, with the
        // SameFieldReqs test above, an instance of {A:{X:T1}} should be useable as an instance of {A:{X:T1?}?}?.
        // That is, if all needed field conversions are ref conversions with the same system type, the top level
        // record conversion could also be a reference conversion. Unfortunately, the current record DType
        // representation doesn't directly support testing for this.
        //
        // For now, we're stuck with saying no here.
        return false;
    }

    /// <summary>
    /// Return whether the conversion from tuple type typeSrc to tuple type typeDst is a reference conversion.
    /// Effectively, this must return false if there is a chance that a reasonably implemented TypeManager might
    /// use a different system type for the two record types.
    /// </summary>
    private static bool IsTupleRefConv(DType typeSrc, DType typeDst)
    {
        Validation.Assert(typeSrc.IsTupleXxx);
        Validation.Assert(typeDst.IsTupleXxx);
        Validation.Assert(typeSrc != typeDst);

        if (typeSrc.ToOpt() == typeDst)
            return true;

        // REVIEW: It is possible to do better, but see the comment in the record method. Similar issues
        // apply here.
        return false;
    }

    /// <summary>
    /// Return whether the conversion from tensor type typeSrc to tensor type typeDst is a reference conversion.
    /// Effectively, this must return false if there is a chance that a reasonably implemented TypeManager might
    /// use a different system type for the two record types.
    /// </summary>
    private static bool IsTensorRefConv(DType typeSrc, DType typeDst)
    {
        Validation.Assert(typeSrc.IsTensorXxx);
        Validation.Assert(typeDst.IsTensorXxx);
        Validation.Assert(typeSrc != typeDst);

        if (typeSrc.ToOpt() == typeDst)
            return true;

        var typeItemSrc = typeSrc.GetTensorItemType();
        var typeItemDst = typeDst.GetTensorItemType();

        // Note that Tensor<T> is not covariant, so the system types need to match exactly.
        if (!IsRefConv(typeItemSrc, typeItemDst, allowCovariance: false))
            return false;

        return true;
    }
}
