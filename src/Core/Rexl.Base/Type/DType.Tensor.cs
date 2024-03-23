// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;

namespace Microsoft.Rexl;

partial struct DType
{
    /// <summary>
    /// The detail information for a tensor type. Consists of the item type and rank.
    /// </summary>
    private sealed class TensorInfo : IEquatable<TensorInfo>
    {
        public readonly DType ItemType;
        public readonly int Rank;

        public TensorInfo(DType typeItem, int rank)
        {
            typeItem.AssertValid();
            Validation.BugCheckParam(rank >= 0, nameof(rank));
            ItemType = typeItem;
            Rank = rank;
        }

        public void AppendShape(TextSink sink)
        {
            sink.Write('[');
            string strPre = "";
            for (int idim = 0; idim < Rank; idim++)
            {
                sink.Write(strPre);
                strPre = ",";
                sink.Write('*');
            }
            sink.Write(']');
        }

        public bool Equals(TensorInfo? other)
        {
            if (object.ReferenceEquals(this, other))
                return true;
            if (other is null)
                return false;
            if (Rank != other.Rank)
                return false;
            if (ItemType != other.ItemType)
                return false;
            return true;
        }

        public static bool operator ==(TensorInfo? a, TensorInfo? b)
        {
            if (a is null)
                return b is null;
            return a.Equals(b);
        }

        public static bool operator !=(TensorInfo? a, TensorInfo? b)
        {
            if (a is null)
                return b is not null;
            return !a.Equals(b);
        }

        public override bool Equals(object? obj)
        {
            if (obj is not TensorInfo other)
                return false;
            return Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ItemType, Rank);
        }
    }

    private static TensorInfo _GetTensorInfo(object? detail)
    {
        Validation.Assert(detail is TensorInfo);
        return (TensorInfo)detail;
    }

    private TensorInfo _GetTensorInfo()
    {
        Validation.Assert(_kind == DKind.Tensor);
        Validation.Assert(_detail is TensorInfo);
        return (TensorInfo)_detail;
    }

    /// <summary>
    /// Return a Tensor type with this type as its item type and the given rank.
    /// </summary>
    public DType ToTensor(bool opt, int rank)
    {
        BugCheckValid();
        return new DType(DKind.Tensor, opt, 0, new TensorInfo(this, rank), WithSpecToRaw(_flags) | DTypeFlags.HasTensor);
    }

    /// <summary>
    /// Checks that the root is a Tensor type and returns the item type.
    /// </summary>
    public DType GetTensorItemType()
    {
        Validation.BugCheck(_kind == DKind.Tensor, "Wrong kind");
        AssertValid();
        return _GetTensorInfo().ItemType;
    }

    /// <summary>
    /// Checks that the root is a Tensor type and returns the rank.
    /// </summary>
    public int GetTensorRank()
    {
        Validation.BugCheck(_kind == DKind.Tensor, "Wrong kind");
        AssertValid();
        return _GetTensorInfo().Rank;
    }

    /// <summary>
    /// If the root is a Tensor type, returns the rank, otherwise returns -1.
    /// </summary>
    public int TensorRank
    {
        get
        {
            AssertValidOrDefault();
            return _kind == DKind.Tensor ? _GetTensorInfo().Rank : -1;
        }
    }

    /// <summary>
    /// Implements acceptance for tensor types.
    /// </summary>
    private static bool _TensorAccepts(TensorInfo infoDst, TensorInfo infoSrc, bool union)
    {
        Validation.AssertValue(infoDst);
        Validation.AssertValue(infoSrc);

        int rank = infoDst.Rank;
        if (rank != infoSrc.Rank)
            return false;
        if (!infoDst.ItemType.Accepts(infoSrc.ItemType, union))
            return false;
        return true;
    }

    private static DType _GetTensorSuperType(TensorInfo info1, TensorInfo info2, bool opt, int seqCount, bool union, ref bool toGen)
    {
        Validation.AssertValue(info1);
        Validation.AssertValue(info2);

        if (info1.Rank != info2.Rank)
        {
            toGen = true;
            return _MakeGeneral(seqCount);
        }

        var itemType = DType.GetSuperTypeCore(info1.ItemType, info2.ItemType, union, ref toGen);
        return itemType.ToTensor(opt, info1.Rank).ToSequence(seqCount);
    }
}
