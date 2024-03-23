// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

partial class Tensor
{
    /// <summary>
    /// Implements point-wise functions, which support broadcasting, but otherwise
    /// operate element by element, with the inputs being coerced to be the same shape
    /// (up to broadcasting).
    /// 
    /// REVIEW: When the inputs have common "expansion" dimensions, this can be
    /// optimized to do fewer computations and require a smaller output buffer. For example
    /// if both inputs have zero stride for the first dimension, then the output should also.
    /// To do this we just need the first half of <see cref="GetCompressedShapeAndStrides(ref Shape, ref Shape, int)"/>
    /// that uses two sets of strides, removing dimensions where both strides are zero.
    /// </summary>
    private partial struct PointWise
    {
        /// <summary>
        /// Information about one of the arguments.
        /// </summary>
        public struct Arg
        {
            /// <summary>
            /// The tensor (untyped).
            /// </summary>
            public readonly Tensor Ten;

            /// <summary>
            /// Whether any of the dimensions was shrunk to match the other arg.
            /// </summary>
            public readonly bool Shrunk;

            /// <summary>
            /// Whether this arg is being broadcast across any dimensions.
            /// </summary>
            public readonly bool Broadcast;

            public Arg(Tensor ten, bool shrunk, bool broadcast)
            {
                Ten = ten;
                Shrunk = shrunk;
                Broadcast = broadcast;
            }

            /// <summary>
            /// Whether the layout for this arg is "regular" meaning the offset between consecutive
            /// cells is a constant, namely Ten._delta. Note that a constant tensor is a special case
            /// with delta being zero.
            /// </summary>
            public bool Regular => Ten._regular && (Ten._delta == 0 || !Shrunk && !Broadcast);
        }

        /// <summary>
        /// The left argument.
        /// </summary>
        public readonly Arg Arg0;

        /// <summary>
        /// The right argument.
        /// </summary>
        public readonly Arg Arg1;

        /// <summary>
        /// Used when there are more than two arguments.
        /// </summary>
        public readonly Arg[] Args;

        /// <summary>
        /// The result shape.
        /// </summary>
        public readonly Shape ShapeRes;

        /// <summary>
        /// The number of cells in the result shape.
        /// </summary>
        public readonly long CountRes;

        /// <summary>
        /// The dimensions for which one of the inputs was shrunk to match the other input.
        /// </summary>
        public readonly BitSet Diffs;

        public PointWise(Tensor ten0, Tensor ten1)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var shape0 = ten0.Shape;
            var shape1 = ten1.Shape;

            Diffs = default;
            Args = null;

            if (shape0 == shape1)
            {
                Validation.Assert(ten0.Count == ten1.Count);
                Arg0 = new Arg(ten0, false, false);
                Arg1 = new Arg(ten1, false, false);
                ShapeRes = shape0;
                CountRes = ten0.Count;
                return;
            }

            int rank0 = ten0.Rank;
            int rank1 = ten1.Rank;
            int rankRes = Math.Max(rank0, rank1);
            int dr0 = rankRes - rank0;
            int dr1 = rankRes - rank1;
            Validation.Assert(dr0 == 0 || dr1 == 0);

            var shr0 = false;
            var shr1 = false;
            var bc0 = false;
            var bc1 = false;

            // REVIEW: Should avoid allocating this builder when it isn't needed.
            var bldrRes = Shape.CreateBuilder(rankRes);

            for (int d = 0; d < rankRes; d++)
            {
                long dim0 = d < dr0 ? 1 : shape0[d - dr0];
                long dim1 = d < dr1 ? 1 : shape1[d - dr1];
                long dim;
                if (dim0 == dim1)
                    dim = dim0;
                else
                {
                    Diffs = Diffs.SetBit(d);
                    if (dim0 == 1)
                    {
                        bc0 = true;
                        dim = dim1;
                    }
                    else if (dim1 == 1)
                    {
                        bc1 = true;
                        dim = dim0;
                    }
                    else if (dim0 < dim1)
                    {
                        dim = dim0;
                        shr1 = true;
                    }
                    else
                    {
                        dim = dim1;
                        shr0 = true;
                    }
                }
                bldrRes[d] = dim;
            }
            ShapeRes = bldrRes.ToImmutable();
            if (ShapeRes == shape0)
                ShapeRes = shape0;
            else if (ShapeRes == shape1)
                ShapeRes = shape1;

            // REVIEW: Could fail because of broadcasting. What should we do?
            if (!ShapeRes.TryGetCount(out CountRes))
                throw new InvalidOperationException("Tensor too large");

            Arg0 = new Arg(ten0, shr0, bc0);
            Arg1 = new Arg(ten1, shr1, bc1);
        }

        public PointWise(params Tensor[] tens)
        {
            Validation.AssertValue(tens);
            Validation.Assert(tens.Length > 2);

            Diffs = default;
            Arg0 = default;
            Arg1 = default;

            int num = tens.Length;
            var shape = tens[0].Shape;
            var count = tens[0].Count;
            bool same = true;
            for (int i = 1; i < num; i++)
            {
                Validation.AssertValue(tens[i]);
                var shapeCur = tens[i].Shape;
                var countCur = tens[i].Count;

                if (shape == shapeCur)
                {
                    Validation.Assert(count == countCur);
                    continue;
                }

                same = false;
                if (shape.Rank < shapeCur.Rank || count < countCur && shape.Rank == shapeCur.Rank)
                {
                    shape = shapeCur;
                    count = countCur;
                }
            }

            Args = new Arg[num];
            if (same)
            {
                ShapeRes = shape;
                CountRes = count;
                for (int i = 0; i < num; i++)
                    Args[i] = new Arg(tens[i], false, false);
                return;
            }

            int rankRes = shape.Rank;
            BitSet br = default;
            BitSet shr = default;
            // REVIEW: Should avoid allocating this builder when it isn't needed.
            var bldrRes = Shape.CreateBuilder(rankRes);
            for (int d = 0; d < rankRes; d++)
            {
                // Find the smallest non-one value, since 1 values broadcast.
                long dim = -1;
                bool diff = false;
                BitSet ones = default;
                for (int i = 0; i < num; i++)
                {
                    int dr = rankRes - tens[i].Rank;
                    long dimCur = d < dr ? 1 : tens[i].Shape[d - dr];
                    Validation.Assert(dimCur >= 0);
                    if (dimCur == 1)
                    {
                        ones = ones.SetBit(i);
                        continue;
                    }
                    if (dim < 0)
                        dim = dimCur;
                    else if (dim != dimCur)
                    {
                        diff = true;
                        if (dim > dimCur)
                            dim = dimCur;
                    }
                }

                if (dim < 0)
                {
                    // All are one.
                    Validation.Assert(ones == BitSet.GetMask(num));
                    Validation.Assert(!Diffs.TestBit(d));
                    bldrRes[d] = 1;
                    continue;
                }

                br |= ones;
                Validation.Assert(dim != 1);
                bldrRes[d] = dim;
                if (!diff)
                    continue;

                Diffs = Diffs.SetBit(d);
                for (int i = 0; i < num; i++)
                {
                    int dr = rankRes - tens[i].Rank;
                    long dimCur = d < dr ? 1 : tens[i].Shape[d - dr];
                    Validation.Assert(dimCur >= 0);
                    if (dimCur == dim)
                        continue;
                    if (dimCur == 1)
                        continue;
                    Validation.Assert(dimCur > dim);
                    shr = shr.SetBit(i);
                }
            }

            ShapeRes = bldrRes.ToImmutable();
            // Use an existing shape if there is a match.
            for (int i = 0; i < num; i++)
            {
                var ten = tens[i];
                if (ShapeRes == ten.Shape)
                    ShapeRes = ten.Shape;
                Args[i] = new Arg(ten, shr.TestBit(i), br.TestBit(i));
            }

            // REVIEW: Could fail because of broadcasting. What should we do?
            if (!ShapeRes.TryGetCount(out CountRes))
                throw new InvalidOperationException("Tensor too large");
        }

        /// <summary>
        /// Prefix the given shape with <paramref name="dr"/> slots containing one. Returns a builder.
        /// </summary>
        private static Immutable.Array<long>.Builder MakePrefix(Shape shape, int dr)
        {
            Validation.Assert(dr > 0);
            var bldr = Shape.CreateBuilder(dr + shape.Rank);
            for (int i = 0; i < dr; i++)
                bldr[i] = 1;
            for (int i = 0; i < shape.Rank; i++)
                bldr[dr + i] = shape[i];
            return bldr;
        }

        /// <summary>
        /// Create a tensor with <see cref="ShapeRes"/>, given <paramref name="strides"/> and
        /// <paramref name="buf"/>.
        /// </summary>
        public Tensor<T> Create<T>(Shape strides, Buffer<T> buf, long root)
        {
            Validation.Assert(strides.Rank == ShapeRes.Rank);
            return Tensor<T>._CreateRaw(ShapeRes, strides, buf, root);
        }

        /// <summary>
        /// Create an empty result (when one of the args is empty).
        /// </summary>
        public Tensor<T> CreateEmpty<T>()
        {
            Validation.Assert(CountRes == 0);
            return Tensor<T>._CreateRaw(ShapeRes, Shape.CreateZero(ShapeRes.Rank), Array.Empty<T>(), 0);
        }

        /// <summary>
        /// Create a constant result. This is used when both inputs are constant or when one input
        /// is a constant "sink" value for the operator, eg, when one is zero for integer multiplication.
        /// </summary>
        public Tensor<T> CreateConstant<T>(T val)
        {
            return Tensor<T>._CreateRaw(ShapeRes, Shape.CreateZero(ShapeRes.Rank), new T[] { val }, 0);
        }

        /// <summary>
        /// Create a "full" result tensor, with no cell sharing (no zero strides).
        /// </summary>
        public Tensor<T> CreateFull<T>(Buffer<T> buf)
        {
            Validation.AssertValue(buf);
            Validation.Assert(buf.Length == CountRes);

            long count = 1;
            ShapeRes.TryMakeStrides(ref count, out var strides).Verify();
            Validation.Assert(count == CountRes);
            if (Args != null)
            {
                for (int i = 0; i < Args.Length; i++)
                {
                    if (strides == Args[i].Ten._strides)
                    {
                        strides = Args[i].Ten._strides;
                        break;
                    }
                }
            }
            else if (strides == Arg0.Ten._strides)
                strides = Arg0.Ten._strides;
            else if (strides == Arg1.Ten._strides)
                strides = Arg1.Ten._strides;
            return Tensor<T>._CreateRaw(ShapeRes, strides, buf, 0);
        }

        /// <summary>
        /// If the shape was augmented with leading ones, then the stride needs
        /// the correct number of leading zeros.
        /// </summary>
        private Shape FixStrides(Shape strides)
        {
            int rank = ShapeRes.Rank;
            int dr = rank - strides.Rank;
            Validation.Assert(dr >= 0);
            if (dr <= 0)
                return strides;
            var bldr = Shape.CreateBuilder(rank);
            for (int i = 0; i < strides.Rank; i++)
                bldr[i + dr] = strides[i];
            return bldr.ToImmutable();
        }

        /// <summary>
        /// Returns the tensor in the arg with shape adjusted to <see cref="ShapeRes"/>.
        /// This uses the same underlying storage, but may have different shape and strides,
        /// eg, more dimensions with zero strides or dims with size one (and zero stride)
        /// expanded to have more items, with shared underlying values.
        /// </summary>
        private Tensor<T> FixShape<T>(in Arg arg, Buffer<T> buf)
        {
            var ten = arg.Ten;
            if (ten._shape == ShapeRes)
                return (Tensor<T>)ten;
            return Create<T>(FixStrides(ten._strides), buf, ten._root);
        }

        /// <summary>
        /// Iterate over the values of the given input according to the output shape.
        /// This yields <see cref="CountRes"/> values.
        /// </summary>
        private IEnumerable<T> GetValues<T>(in Arg arg, Buffer<T> buf)
        {
            var strides = arg.Ten._strides;
            if (arg.Ten._shape != ShapeRes)
                strides = FixStrides(strides);
            return IterateValues(buf, CountRes, ShapeRes, strides, arg.Ten._root);
        }
    }
}
