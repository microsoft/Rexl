// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

partial class Tensor
{
    /// <summary>
    /// Implements the "Dot" function, which performs a dot product over an indicated dimension
    /// of two tensors, preserving all other dimensions in order.
    /// </summary>
    private protected partial struct Dotter
    {
        /// <summary>
        /// Information about one of the arguments.
        /// </summary>
        public partial struct Arg
        {
            /// <summary>
            /// The tensor (untyped).
            /// </summary>
            public readonly Tensor Ten;

            /// <summary>
            /// Whether the summing dimension was shrunken to match the other.
            /// </summary>
            public readonly bool Shrunk;

            /// <summary>
            /// The stride for the summing dimension.
            /// </summary>
            public readonly long StrideSum;

            // The shape and stride to use for iteration. The summing slot is not included.
            // Compatible slots have also been combined, to reduce the number of slots.

            /// <summary>
            /// The shape to use for computation/iteration. The summing slot is not included. This
            /// leverages any zero strides in an argument to reduce the amount of computation and
            /// storage for the result. This also combines adjacent slots with compatible strides
            /// to reduce the iteration complexity.
            /// </summary>
            public readonly Shape ShapeCore;

            /// <summary>
            /// The strides to use for computation/iteration.
            /// </summary>
            public readonly Shape StridesCore;

            /// <summary>
            /// The number of distinct cells produced by the computation.
            /// </summary>
            public readonly long CountCore;

            public Arg(Tensor ten, int slot, long dim)
            {
                Validation.AssertValue(ten);

                var shape = ten._shape;
                Validation.AssertIndex(slot, shape.Rank);
                Validation.AssertIndexInclusive(dim, shape[slot]);

                Ten = ten;
                Shrunk = dim < shape[slot];
                StrideSum = Ten._strides[slot];

                // Drop the summing slot and stride zero slots.
                if (shape.Rank <= 1)
                {
                    ShapeCore = Shape.Scalar;
                    StridesCore = Shape.Scalar;
                    CountCore = 1;
                }
                else
                {
                    var strides = ten._strides;
                    GetCompressedShapeAndStrides(ref shape, ref strides, slot);

                    ShapeCore = shape;
                    StridesCore = strides;
                    if (!ShapeCore.TryGetCount(out CountCore))
                        throw new InvalidOperationException("Tensor too large");
                }

                Validation.Assert(ShapeCore.Rank == StridesCore.Rank);
            }

            /// <summary>
            /// Iterate over non-summing indices into the underlying <see cref="Buffer{T}"/>.
            /// REVIEW: For the inner argument, we should probably use an array rather than recomputing.
            /// </summary>
            public IEnumerable<long> GetIndices() => IterateIndices(CountCore, ShapeCore, StridesCore, Ten._root);
        }

        /// <summary>
        /// The left arg.
        /// </summary>
        public readonly Arg Arg0;

        /// <summary>
        /// The right arg.
        /// </summary>
        public readonly Arg Arg1;

        /// <summary>
        /// The result shape, with the summing dimensions dropped and all others preserved in order.
        /// </summary>
        public readonly Shape ShapeRes;

        /// <summary>
        /// The strides of the result. Note that this shares cells when they are shared in an input.
        /// </summary>
        public readonly Shape StridesRes;

        /// <summary>
        /// The total logical number of cells of the result (product of dimensions in the shape).
        /// </summary>
        public readonly long CountLog;

        /// <summary>
        /// The number of distinct cells computed.
        /// </summary>
        public readonly long CountRaw;

        /// <summary>
        /// The size of the summing/dotting dimension.
        /// </summary>
        public readonly long DimSum;

        /// <summary>
        /// Constructs the <see cref="Dotter"/> given the arguments and which of their slots should
        /// be dotted/summed.
        /// </summary>
        public Dotter(Tensor ten0, Tensor ten1, int d0, int d1)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);
            Validation.AssertIndex(d0, ten0.Rank);
            Validation.AssertIndex(d1, ten1.Rank);

            var shape0 = ten0._shape;
            var shape1 = ten1._shape;

            var dim0 = shape0[d0];
            var dim1 = shape1[d1];
            DimSum = Math.Min(dim0, dim1);

            Arg0 = new Arg(ten0, d0, DimSum);
            Arg1 = new Arg(ten1, d1, DimSum);

            // REVIEW: Optimize?
            int rank0 = shape0.Rank;
            int rank1 = shape1.Rank;
            int rankRes = rank0 + rank1 - 2;

            var strides0 = ten0._strides;
            var strides1 = ten1._strides;

            // Build the resulting shape and strides.
            var bldrSh = Shape.CreateBuilder(rankRes);
            var bldrSt = Shape.CreateBuilder(rankRes);
            int d = 0;
            for (int i = 0; i < d0; d++, i++)
            { bldrSh[d] = shape0[i]; bldrSt[d] = strides0[i]; }
            for (int i = d0 + 1; i < rank0; d++, i++)
            { bldrSh[d] = shape0[i]; bldrSt[d] = strides0[i]; }
            for (int i = 0; i < d1; d++, i++)
            { bldrSh[d] = shape1[i]; bldrSt[d] = strides1[i]; }
            for (int i = d1 + 1; i < rank1; d++, i++)
            { bldrSh[d] = shape1[i]; bldrSt[d] = strides1[i]; }
            Validation.Assert(d == rankRes);

            var shape = new Shape(bldrSh.ToImmutable());
            if (shape == shape0)
                shape = shape0;
            else if (shape == shape1)
                shape = shape1;

            var strides = new Shape(bldrSt.ToImmutable());

            // REVIEW: What should we do if this fails?
            if (!shape.TryMakeStridesLike(ref strides, out long countRaw))
                throw new InvalidOperationException("Tensor too large");

            if (strides == strides0)
                strides = strides0;
            else if (strides == strides1)
                strides = strides1;

            ShapeRes = shape;
            StridesRes = strides;
            CountRaw = countRaw;

            // REVIEW: What should we do if these fail?
            if (!ShapeRes.TryGetCount(out CountLog))
                throw new InvalidOperationException("Tensor too large");
        }

        /// <summary>
        /// Create an empty result.
        /// </summary>
        public Tensor<T> CreateEmpty<T>()
        {
            Validation.Assert(CountLog == 0);
            return Tensor<T>._CreateRaw(ShapeRes, Shape.CreateZero(ShapeRes.Rank), Array.Empty<T>(), 0);
        }

        /// <summary>
        /// Create a constant result.
        /// </summary>
        public Tensor<T> CreateConstant<T>(T val)
        {
            return Tensor<T>._CreateRaw(ShapeRes, Shape.CreateZero(ShapeRes.Rank), new T[] { val }, 0);
        }

        /// <summary>
        /// Create the result with the given buffer.
        /// </summary>
        public Tensor<T> CreateRes<T>(T[] buf, long root)
        {
            Validation.Assert(buf.Length == CountRaw);
            return Tensor<T>._CreateRaw(ShapeRes, StridesRes, buf, root);
        }
    }
}
