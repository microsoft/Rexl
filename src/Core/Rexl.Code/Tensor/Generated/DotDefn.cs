// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// WARNING: This .cs file is generated from the corresponding .tt file. DO NOT edit this .cs directly.

using System;

using Microsoft.Rexl.Private;

// Note: don't use new style namespace decl since we need the T using alias.
namespace Microsoft.Rexl
{
    using T = Double;

    partial class Tensor
    {
        public static Tensor<T> Dot(Tensor<T> ten0, Tensor<T> ten1, int d0, int d1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var dot = new Dotter(ten0, ten1, d0, d1);
            shrunk = dot.Arg0.Shrunk | dot.Arg1.Shrunk;
            if (dot.CountLog == 0)
                return dot.CreateEmpty<T>();
            if (dot.DimSum == 0)
                return dot.CreateConstant<T>(default(T));
            Validation.Assert(ten0.Count > 0);
            Validation.Assert(ten1.Count > 0);
            return dot.Dot(ten0._buf, ten1._buf);
        }

        partial struct Dotter
        {
            public Tensor<T> Dot(Buffer<T> buf0, Buffer<T> buf1)
            {
                Validation.Assert(CountLog > 0);
                Validation.Assert(buf0.Length > 0);
                Validation.Assert(buf1.Length > 0);
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(val * buf0[Arg0.Ten._root] * (T)DimSum));
                    if (T.IsNaN(val)) // sink
                        return CreateConstant<T>(val);
                    // REVIEW: Optimize?
                    // return Dot(in Arg0, buf0, val);
                }
                else if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (T.IsNaN(val)) // sink
                        return CreateConstant<T>(val);
                    // REVIEW: Optimize?
                    // return Dot(in Arg1, buf1, val);
                }

                var count = CountRaw;
                var dst = new T[count];

                var s0 = Arg0.StrideSum;
                var s1 = Arg1.StrideSum;
                var dim = DimSum;
                long i = 0;
                foreach (long off0 in Arg0.GetIndices())
                {
                    foreach (long off1 in Arg1.GetIndices())
                    {
                        T sum = default;
                        for (long j = 0, tmp0 = off0, tmp1 = off1; j < dim; j++, tmp0 += s0, tmp1 += s1)
                            sum += (T)(buf0[tmp0] * buf1[tmp1]);
                        dst[i++] = sum;
                    }
                }
                Validation.Assert(i == count);

                return CreateRes(dst, 0);
            }
        }
    }
}

// Note: don't use new style namespace decl since we need the T using alias.
namespace Microsoft.Rexl
{
    using T = Single;

    partial class Tensor
    {
        public static Tensor<T> Dot(Tensor<T> ten0, Tensor<T> ten1, int d0, int d1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var dot = new Dotter(ten0, ten1, d0, d1);
            shrunk = dot.Arg0.Shrunk | dot.Arg1.Shrunk;
            if (dot.CountLog == 0)
                return dot.CreateEmpty<T>();
            if (dot.DimSum == 0)
                return dot.CreateConstant<T>(default(T));
            Validation.Assert(ten0.Count > 0);
            Validation.Assert(ten1.Count > 0);
            return dot.Dot(ten0._buf, ten1._buf);
        }

        partial struct Dotter
        {
            public Tensor<T> Dot(Buffer<T> buf0, Buffer<T> buf1)
            {
                Validation.Assert(CountLog > 0);
                Validation.Assert(buf0.Length > 0);
                Validation.Assert(buf1.Length > 0);
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(val * buf0[Arg0.Ten._root] * (T)DimSum));
                    if (T.IsNaN(val)) // sink
                        return CreateConstant<T>(val);
                    // REVIEW: Optimize?
                    // return Dot(in Arg0, buf0, val);
                }
                else if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (T.IsNaN(val)) // sink
                        return CreateConstant<T>(val);
                    // REVIEW: Optimize?
                    // return Dot(in Arg1, buf1, val);
                }

                var count = CountRaw;
                var dst = new T[count];

                var s0 = Arg0.StrideSum;
                var s1 = Arg1.StrideSum;
                var dim = DimSum;
                long i = 0;
                foreach (long off0 in Arg0.GetIndices())
                {
                    foreach (long off1 in Arg1.GetIndices())
                    {
                        T sum = default;
                        for (long j = 0, tmp0 = off0, tmp1 = off1; j < dim; j++, tmp0 += s0, tmp1 += s1)
                            sum += (T)(buf0[tmp0] * buf1[tmp1]);
                        dst[i++] = sum;
                    }
                }
                Validation.Assert(i == count);

                return CreateRes(dst, 0);
            }
        }
    }
}

// Note: don't use new style namespace decl since we need the T using alias.
namespace Microsoft.Rexl
{
    using T = System.Numerics.BigInteger;

    partial class Tensor
    {
        public static Tensor<T> Dot(Tensor<T> ten0, Tensor<T> ten1, int d0, int d1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var dot = new Dotter(ten0, ten1, d0, d1);
            shrunk = dot.Arg0.Shrunk | dot.Arg1.Shrunk;
            if (dot.CountLog == 0)
                return dot.CreateEmpty<T>();
            if (dot.DimSum == 0)
                return dot.CreateConstant<T>(default(T));
            Validation.Assert(ten0.Count > 0);
            Validation.Assert(ten1.Count > 0);
            return dot.Dot(ten0._buf, ten1._buf);
        }

        partial struct Dotter
        {
            public Tensor<T> Dot(Buffer<T> buf0, Buffer<T> buf1)
            {
                Validation.Assert(CountLog > 0);
                Validation.Assert(buf0.Length > 0);
                Validation.Assert(buf1.Length > 0);
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(val * buf0[Arg0.Ten._root] * (T)DimSum));
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    // REVIEW: Optimize?
                    // return Dot(in Arg0, buf0, val);
                }
                else if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    // REVIEW: Optimize?
                    // return Dot(in Arg1, buf1, val);
                }

                var count = CountRaw;
                var dst = new T[count];

                var s0 = Arg0.StrideSum;
                var s1 = Arg1.StrideSum;
                var dim = DimSum;
                long i = 0;
                foreach (long off0 in Arg0.GetIndices())
                {
                    foreach (long off1 in Arg1.GetIndices())
                    {
                        T sum = default;
                        for (long j = 0, tmp0 = off0, tmp1 = off1; j < dim; j++, tmp0 += s0, tmp1 += s1)
                            sum += (T)(buf0[tmp0] * buf1[tmp1]);
                        dst[i++] = sum;
                    }
                }
                Validation.Assert(i == count);

                return CreateRes(dst, 0);
            }
        }
    }
}

// Note: don't use new style namespace decl since we need the T using alias.
namespace Microsoft.Rexl
{
    using T = Int64;

    partial class Tensor
    {
        public static Tensor<T> Dot(Tensor<T> ten0, Tensor<T> ten1, int d0, int d1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var dot = new Dotter(ten0, ten1, d0, d1);
            shrunk = dot.Arg0.Shrunk | dot.Arg1.Shrunk;
            if (dot.CountLog == 0)
                return dot.CreateEmpty<T>();
            if (dot.DimSum == 0)
                return dot.CreateConstant<T>(default(T));
            Validation.Assert(ten0.Count > 0);
            Validation.Assert(ten1.Count > 0);
            return dot.Dot(ten0._buf, ten1._buf);
        }

        partial struct Dotter
        {
            public Tensor<T> Dot(Buffer<T> buf0, Buffer<T> buf1)
            {
                Validation.Assert(CountLog > 0);
                Validation.Assert(buf0.Length > 0);
                Validation.Assert(buf1.Length > 0);
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(val * buf0[Arg0.Ten._root] * (T)DimSum));
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    // REVIEW: Optimize?
                    // return Dot(in Arg0, buf0, val);
                }
                else if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    // REVIEW: Optimize?
                    // return Dot(in Arg1, buf1, val);
                }

                var count = CountRaw;
                var dst = new T[count];

                var s0 = Arg0.StrideSum;
                var s1 = Arg1.StrideSum;
                var dim = DimSum;
                long i = 0;
                foreach (long off0 in Arg0.GetIndices())
                {
                    foreach (long off1 in Arg1.GetIndices())
                    {
                        T sum = default;
                        for (long j = 0, tmp0 = off0, tmp1 = off1; j < dim; j++, tmp0 += s0, tmp1 += s1)
                            sum += (T)(buf0[tmp0] * buf1[tmp1]);
                        dst[i++] = sum;
                    }
                }
                Validation.Assert(i == count);

                return CreateRes(dst, 0);
            }
        }
    }
}

// Note: don't use new style namespace decl since we need the T using alias.
namespace Microsoft.Rexl
{
    using T = Int32;

    partial class Tensor
    {
        public static Tensor<T> Dot(Tensor<T> ten0, Tensor<T> ten1, int d0, int d1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var dot = new Dotter(ten0, ten1, d0, d1);
            shrunk = dot.Arg0.Shrunk | dot.Arg1.Shrunk;
            if (dot.CountLog == 0)
                return dot.CreateEmpty<T>();
            if (dot.DimSum == 0)
                return dot.CreateConstant<T>(default(T));
            Validation.Assert(ten0.Count > 0);
            Validation.Assert(ten1.Count > 0);
            return dot.Dot(ten0._buf, ten1._buf);
        }

        partial struct Dotter
        {
            public Tensor<T> Dot(Buffer<T> buf0, Buffer<T> buf1)
            {
                Validation.Assert(CountLog > 0);
                Validation.Assert(buf0.Length > 0);
                Validation.Assert(buf1.Length > 0);
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(val * buf0[Arg0.Ten._root] * (T)DimSum));
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    // REVIEW: Optimize?
                    // return Dot(in Arg0, buf0, val);
                }
                else if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    // REVIEW: Optimize?
                    // return Dot(in Arg1, buf1, val);
                }

                var count = CountRaw;
                var dst = new T[count];

                var s0 = Arg0.StrideSum;
                var s1 = Arg1.StrideSum;
                var dim = DimSum;
                long i = 0;
                foreach (long off0 in Arg0.GetIndices())
                {
                    foreach (long off1 in Arg1.GetIndices())
                    {
                        T sum = default;
                        for (long j = 0, tmp0 = off0, tmp1 = off1; j < dim; j++, tmp0 += s0, tmp1 += s1)
                            sum += (T)(buf0[tmp0] * buf1[tmp1]);
                        dst[i++] = sum;
                    }
                }
                Validation.Assert(i == count);

                return CreateRes(dst, 0);
            }
        }
    }
}

// Note: don't use new style namespace decl since we need the T using alias.
namespace Microsoft.Rexl
{
    using T = Int16;

    partial class Tensor
    {
        public static Tensor<T> Dot(Tensor<T> ten0, Tensor<T> ten1, int d0, int d1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var dot = new Dotter(ten0, ten1, d0, d1);
            shrunk = dot.Arg0.Shrunk | dot.Arg1.Shrunk;
            if (dot.CountLog == 0)
                return dot.CreateEmpty<T>();
            if (dot.DimSum == 0)
                return dot.CreateConstant<T>(default(T));
            Validation.Assert(ten0.Count > 0);
            Validation.Assert(ten1.Count > 0);
            return dot.Dot(ten0._buf, ten1._buf);
        }

        partial struct Dotter
        {
            public Tensor<T> Dot(Buffer<T> buf0, Buffer<T> buf1)
            {
                Validation.Assert(CountLog > 0);
                Validation.Assert(buf0.Length > 0);
                Validation.Assert(buf1.Length > 0);
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(val * buf0[Arg0.Ten._root] * (T)DimSum));
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    // REVIEW: Optimize?
                    // return Dot(in Arg0, buf0, val);
                }
                else if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    // REVIEW: Optimize?
                    // return Dot(in Arg1, buf1, val);
                }

                var count = CountRaw;
                var dst = new T[count];

                var s0 = Arg0.StrideSum;
                var s1 = Arg1.StrideSum;
                var dim = DimSum;
                long i = 0;
                foreach (long off0 in Arg0.GetIndices())
                {
                    foreach (long off1 in Arg1.GetIndices())
                    {
                        T sum = default;
                        for (long j = 0, tmp0 = off0, tmp1 = off1; j < dim; j++, tmp0 += s0, tmp1 += s1)
                            sum += (T)(buf0[tmp0] * buf1[tmp1]);
                        dst[i++] = sum;
                    }
                }
                Validation.Assert(i == count);

                return CreateRes(dst, 0);
            }
        }
    }
}

// Note: don't use new style namespace decl since we need the T using alias.
namespace Microsoft.Rexl
{
    using T = SByte;

    partial class Tensor
    {
        public static Tensor<T> Dot(Tensor<T> ten0, Tensor<T> ten1, int d0, int d1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var dot = new Dotter(ten0, ten1, d0, d1);
            shrunk = dot.Arg0.Shrunk | dot.Arg1.Shrunk;
            if (dot.CountLog == 0)
                return dot.CreateEmpty<T>();
            if (dot.DimSum == 0)
                return dot.CreateConstant<T>(default(T));
            Validation.Assert(ten0.Count > 0);
            Validation.Assert(ten1.Count > 0);
            return dot.Dot(ten0._buf, ten1._buf);
        }

        partial struct Dotter
        {
            public Tensor<T> Dot(Buffer<T> buf0, Buffer<T> buf1)
            {
                Validation.Assert(CountLog > 0);
                Validation.Assert(buf0.Length > 0);
                Validation.Assert(buf1.Length > 0);
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(val * buf0[Arg0.Ten._root] * (T)DimSum));
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    // REVIEW: Optimize?
                    // return Dot(in Arg0, buf0, val);
                }
                else if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    // REVIEW: Optimize?
                    // return Dot(in Arg1, buf1, val);
                }

                var count = CountRaw;
                var dst = new T[count];

                var s0 = Arg0.StrideSum;
                var s1 = Arg1.StrideSum;
                var dim = DimSum;
                long i = 0;
                foreach (long off0 in Arg0.GetIndices())
                {
                    foreach (long off1 in Arg1.GetIndices())
                    {
                        T sum = default;
                        for (long j = 0, tmp0 = off0, tmp1 = off1; j < dim; j++, tmp0 += s0, tmp1 += s1)
                            sum += (T)(buf0[tmp0] * buf1[tmp1]);
                        dst[i++] = sum;
                    }
                }
                Validation.Assert(i == count);

                return CreateRes(dst, 0);
            }
        }
    }
}

// Note: don't use new style namespace decl since we need the T using alias.
namespace Microsoft.Rexl
{
    using T = UInt64;

    partial class Tensor
    {
        public static Tensor<T> Dot(Tensor<T> ten0, Tensor<T> ten1, int d0, int d1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var dot = new Dotter(ten0, ten1, d0, d1);
            shrunk = dot.Arg0.Shrunk | dot.Arg1.Shrunk;
            if (dot.CountLog == 0)
                return dot.CreateEmpty<T>();
            if (dot.DimSum == 0)
                return dot.CreateConstant<T>(default(T));
            Validation.Assert(ten0.Count > 0);
            Validation.Assert(ten1.Count > 0);
            return dot.Dot(ten0._buf, ten1._buf);
        }

        partial struct Dotter
        {
            public Tensor<T> Dot(Buffer<T> buf0, Buffer<T> buf1)
            {
                Validation.Assert(CountLog > 0);
                Validation.Assert(buf0.Length > 0);
                Validation.Assert(buf1.Length > 0);
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(val * buf0[Arg0.Ten._root] * (T)DimSum));
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    // REVIEW: Optimize?
                    // return Dot(in Arg0, buf0, val);
                }
                else if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    // REVIEW: Optimize?
                    // return Dot(in Arg1, buf1, val);
                }

                var count = CountRaw;
                var dst = new T[count];

                var s0 = Arg0.StrideSum;
                var s1 = Arg1.StrideSum;
                var dim = DimSum;
                long i = 0;
                foreach (long off0 in Arg0.GetIndices())
                {
                    foreach (long off1 in Arg1.GetIndices())
                    {
                        T sum = default;
                        for (long j = 0, tmp0 = off0, tmp1 = off1; j < dim; j++, tmp0 += s0, tmp1 += s1)
                            sum += (T)(buf0[tmp0] * buf1[tmp1]);
                        dst[i++] = sum;
                    }
                }
                Validation.Assert(i == count);

                return CreateRes(dst, 0);
            }
        }
    }
}

// Note: don't use new style namespace decl since we need the T using alias.
namespace Microsoft.Rexl
{
    using T = UInt32;

    partial class Tensor
    {
        public static Tensor<T> Dot(Tensor<T> ten0, Tensor<T> ten1, int d0, int d1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var dot = new Dotter(ten0, ten1, d0, d1);
            shrunk = dot.Arg0.Shrunk | dot.Arg1.Shrunk;
            if (dot.CountLog == 0)
                return dot.CreateEmpty<T>();
            if (dot.DimSum == 0)
                return dot.CreateConstant<T>(default(T));
            Validation.Assert(ten0.Count > 0);
            Validation.Assert(ten1.Count > 0);
            return dot.Dot(ten0._buf, ten1._buf);
        }

        partial struct Dotter
        {
            public Tensor<T> Dot(Buffer<T> buf0, Buffer<T> buf1)
            {
                Validation.Assert(CountLog > 0);
                Validation.Assert(buf0.Length > 0);
                Validation.Assert(buf1.Length > 0);
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(val * buf0[Arg0.Ten._root] * (T)DimSum));
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    // REVIEW: Optimize?
                    // return Dot(in Arg0, buf0, val);
                }
                else if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    // REVIEW: Optimize?
                    // return Dot(in Arg1, buf1, val);
                }

                var count = CountRaw;
                var dst = new T[count];

                var s0 = Arg0.StrideSum;
                var s1 = Arg1.StrideSum;
                var dim = DimSum;
                long i = 0;
                foreach (long off0 in Arg0.GetIndices())
                {
                    foreach (long off1 in Arg1.GetIndices())
                    {
                        T sum = default;
                        for (long j = 0, tmp0 = off0, tmp1 = off1; j < dim; j++, tmp0 += s0, tmp1 += s1)
                            sum += (T)(buf0[tmp0] * buf1[tmp1]);
                        dst[i++] = sum;
                    }
                }
                Validation.Assert(i == count);

                return CreateRes(dst, 0);
            }
        }
    }
}

// Note: don't use new style namespace decl since we need the T using alias.
namespace Microsoft.Rexl
{
    using T = UInt16;

    partial class Tensor
    {
        public static Tensor<T> Dot(Tensor<T> ten0, Tensor<T> ten1, int d0, int d1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var dot = new Dotter(ten0, ten1, d0, d1);
            shrunk = dot.Arg0.Shrunk | dot.Arg1.Shrunk;
            if (dot.CountLog == 0)
                return dot.CreateEmpty<T>();
            if (dot.DimSum == 0)
                return dot.CreateConstant<T>(default(T));
            Validation.Assert(ten0.Count > 0);
            Validation.Assert(ten1.Count > 0);
            return dot.Dot(ten0._buf, ten1._buf);
        }

        partial struct Dotter
        {
            public Tensor<T> Dot(Buffer<T> buf0, Buffer<T> buf1)
            {
                Validation.Assert(CountLog > 0);
                Validation.Assert(buf0.Length > 0);
                Validation.Assert(buf1.Length > 0);
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(val * buf0[Arg0.Ten._root] * (T)DimSum));
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    // REVIEW: Optimize?
                    // return Dot(in Arg0, buf0, val);
                }
                else if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    // REVIEW: Optimize?
                    // return Dot(in Arg1, buf1, val);
                }

                var count = CountRaw;
                var dst = new T[count];

                var s0 = Arg0.StrideSum;
                var s1 = Arg1.StrideSum;
                var dim = DimSum;
                long i = 0;
                foreach (long off0 in Arg0.GetIndices())
                {
                    foreach (long off1 in Arg1.GetIndices())
                    {
                        T sum = default;
                        for (long j = 0, tmp0 = off0, tmp1 = off1; j < dim; j++, tmp0 += s0, tmp1 += s1)
                            sum += (T)(buf0[tmp0] * buf1[tmp1]);
                        dst[i++] = sum;
                    }
                }
                Validation.Assert(i == count);

                return CreateRes(dst, 0);
            }
        }
    }
}

// Note: don't use new style namespace decl since we need the T using alias.
namespace Microsoft.Rexl
{
    using T = Byte;

    partial class Tensor
    {
        public static Tensor<T> Dot(Tensor<T> ten0, Tensor<T> ten1, int d0, int d1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var dot = new Dotter(ten0, ten1, d0, d1);
            shrunk = dot.Arg0.Shrunk | dot.Arg1.Shrunk;
            if (dot.CountLog == 0)
                return dot.CreateEmpty<T>();
            if (dot.DimSum == 0)
                return dot.CreateConstant<T>(default(T));
            Validation.Assert(ten0.Count > 0);
            Validation.Assert(ten1.Count > 0);
            return dot.Dot(ten0._buf, ten1._buf);
        }

        partial struct Dotter
        {
            public Tensor<T> Dot(Buffer<T> buf0, Buffer<T> buf1)
            {
                Validation.Assert(CountLog > 0);
                Validation.Assert(buf0.Length > 0);
                Validation.Assert(buf1.Length > 0);
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(val * buf0[Arg0.Ten._root] * (T)DimSum));
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    // REVIEW: Optimize?
                    // return Dot(in Arg0, buf0, val);
                }
                else if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    // REVIEW: Optimize?
                    // return Dot(in Arg1, buf1, val);
                }

                var count = CountRaw;
                var dst = new T[count];

                var s0 = Arg0.StrideSum;
                var s1 = Arg1.StrideSum;
                var dim = DimSum;
                long i = 0;
                foreach (long off0 in Arg0.GetIndices())
                {
                    foreach (long off1 in Arg1.GetIndices())
                    {
                        T sum = default;
                        for (long j = 0, tmp0 = off0, tmp1 = off1; j < dim; j++, tmp0 += s0, tmp1 += s1)
                            sum += (T)(buf0[tmp0] * buf1[tmp1]);
                        dst[i++] = sum;
                    }
                }
                Validation.Assert(i == count);

                return CreateRes(dst, 0);
            }
        }
    }
}

// Note: don't use new style namespace decl since we need the T using alias.
namespace Microsoft.Rexl
{
    using T = Boolean;

    partial class Tensor
    {
        public static Tensor<T> Dot(Tensor<T> ten0, Tensor<T> ten1, int d0, int d1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var dot = new Dotter(ten0, ten1, d0, d1);
            shrunk = dot.Arg0.Shrunk | dot.Arg1.Shrunk;
            if (dot.CountLog == 0)
                return dot.CreateEmpty<T>();
            if (dot.DimSum == 0)
                return dot.CreateConstant<T>(default(T));
            Validation.Assert(ten0.Count > 0);
            Validation.Assert(ten1.Count > 0);
            return dot.Dot(ten0._buf, ten1._buf);
        }

        partial struct Dotter
        {
            public Tensor<T> Dot(Buffer<T> buf0, Buffer<T> buf1)
            {
                Validation.Assert(CountLog > 0);
                Validation.Assert(buf0.Length > 0);
                Validation.Assert(buf1.Length > 0);
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(val & buf0[Arg0.Ten._root] & (DimSum & 1) != 0));
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    // REVIEW: Optimize?
                    // return Dot(in Arg0, buf0, val);
                }
                else if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    // REVIEW: Optimize?
                    // return Dot(in Arg1, buf1, val);
                }

                var count = CountRaw;
                var dst = new T[count];

                var s0 = Arg0.StrideSum;
                var s1 = Arg1.StrideSum;
                var dim = DimSum;
                long i = 0;
                foreach (long off0 in Arg0.GetIndices())
                {
                    foreach (long off1 in Arg1.GetIndices())
                    {
                        T sum = default;
                        for (long j = 0, tmp0 = off0, tmp1 = off1; j < dim; j++, tmp0 += s0, tmp1 += s1)
                            sum ^= (T)(buf0[tmp0] & buf1[tmp1]);
                        dst[i++] = sum;
                    }
                }
                Validation.Assert(i == count);

                return CreateRes(dst, 0);
            }
        }
    }
}

