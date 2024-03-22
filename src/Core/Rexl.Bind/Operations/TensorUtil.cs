// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Rexl;

/// <summary>
/// Tensor related utilities, including for common representations of "pixels".
/// </summary>
public static class TensorUtil
{
    /// <summary>
    /// Type for rank-one tensor of byte.
    /// </summary>
    public static readonly DType TypeBytes = DType.U1Req.ToTensor(opt: false, rank: 1);

    /// <summary>
    /// Type for rank-three tensor of byte, which is one way to encode pixels.
    /// </summary>
    public static readonly DType TypePixU1 = DType.U1Req.ToTensor(opt: false, rank: 3);

    /// <summary>
    /// Type for rank-two tensor of uint, which is one way to encode pixels.
    /// </summary>
    public static readonly DType TypePixU4 = DType.U4Req.ToTensor(opt: false, rank: 2);

    /// <summary>
    /// Type for rank-one tensor of float.
    /// </summary>
    public static readonly DType TypeFloatVector = DType.R4Req.ToTensor(opt: false, rank: 1);

    /// <summary>
    /// Returns whether the type is a required (non-opt) pixel tensor type.
    /// </summary>
    public static bool IsPixTypeReq(DType type)
    {
        if (type == TypePixU1)
            return true;
        if (type == TypePixU4)
            return true;
        return false;
    }

    /// <summary>
    /// Returns whether the type is an optional pixel tensor type.
    /// </summary>
    public static bool IsPixTypeOpt(DType type)
    {
        if (type == TypePixU1.ToOpt())
            return true;
        if (type == TypePixU4.ToOpt())
            return true;
        return false;
    }

    /// <summary>
    /// Given a "source" type, determine the pixel type that should be assumed. Typically used in
    /// specialize types functionality.
    /// </summary>
    public static DType InferPixType(DType typeSrc)
    {
        if (!typeSrc.IsTensorXxx)
            return TensorUtil.TypePixU1;

        var typeCell = typeSrc.GetTensorItemType();
        int rank = typeSrc.TensorRank;
        switch (typeCell.RootKind)
        {
        case DKind.U1:
            return TensorUtil.TypePixU1;
        case DKind.U4:
            return TensorUtil.TypePixU4;
        default:
            switch (rank)
            {
            case 2:
                return TensorUtil.TypePixU4;
            case 3:
                return TensorUtil.TypePixU1;
            }
            return TensorUtil.TypePixU1;
        }
    }
}
