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
        public static Tensor<T> Add(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Add(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Add(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(buf0[Arg0.Ten._root] + val)); // op
                    if (T.IsNaN(val)) // sink
                        return CreateConstant<T>(val);
                    if (val == default) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Add(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (T.IsNaN(val)) // sink
                        return CreateConstant<T>(val);
                    if (val == default) // ident
                        return FixShape<T>(in Arg1, buf1);
                    return Add(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = (T)(buf0[off0] + buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(ator0.Current + ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Add(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(T.IsNaN(a))); // sink
                Validation.Assert(a != default); // ident
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(a + src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(a + val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Sub(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Sub(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Sub(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(buf0[Arg0.Ten._root] - val)); // op
                    if (T.IsNaN(val)) // sink
                        return CreateConstant<T>(val);
                    if (val == default) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Add((T)(0 - val), in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (T.IsNaN(val)) // sink
                        return CreateConstant<T>(val);
                    return Sub(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = (T)(buf0[off0] - buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(ator0.Current - ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Sub(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(T.IsNaN(a))); // sink
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(a - src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(a - val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Mul(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Mul(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Mul(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(buf0[Arg0.Ten._root] * val)); // op
                    if (T.IsNaN(val)) // sink
                        return CreateConstant<T>(val);
                    if (val == (T)1) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Mul(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (T.IsNaN(val)) // sink
                        return CreateConstant<T>(val);
                    if (val == (T)1) // ident
                        return FixShape<T>(in Arg1, buf1);
                    return Mul(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = (T)(buf0[off0] * buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(ator0.Current * ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Mul(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(T.IsNaN(a))); // sink
                Validation.Assert(a != (T)1); // ident
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(a * src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(a * val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Div(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Div(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Div(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(buf0[Arg0.Ten._root] / val)); // op
                    if (T.IsNaN(val)) // sink
                        return CreateConstant<T>(val);
                    if (val == (T)1) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Div(in Arg0, buf0, val);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (T.IsNaN(val)) // sink
                        return CreateConstant<T>(val);
                    return Div(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = (T)(buf0[off0] / buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(ator0.Current / ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Div(in Arg arg, Buffer<T> src, T b)
            {
                Validation.Assert(!T.IsNaN(b)); // sink
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(src[off] / b); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(val / b); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Div(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(T.IsNaN(a))); // sink
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(a / src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(a / val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Min(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Min(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Min(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>(Math.Min(buf0[Arg0.Ten._root], val)); // op
                    if (T.IsNaN(val)) // sink
                        return CreateConstant<T>(val);
                    return Min(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (T.IsNaN(val)) // sink
                        return CreateConstant<T>(val);
                    return Min(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = Math.Min(buf0[off0], buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Min(ator0.Current, ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Min(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(T.IsNaN(a))); // sink
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = Math.Min(a, src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Min(a, val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Max(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Max(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Max(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>(Math.Max(buf0[Arg0.Ten._root], val)); // op
                    if (T.IsNaN(val)) // sink
                        return CreateConstant<T>(val);
                    return Max(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (T.IsNaN(val)) // sink
                        return CreateConstant<T>(val);
                    return Max(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = Math.Max(buf0[off0], buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Max(ator0.Current, ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Max(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(T.IsNaN(a))); // sink
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = Math.Max(a, src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Max(a, val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
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
        public static Tensor<T> Add(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Add(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Add(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(buf0[Arg0.Ten._root] + val)); // op
                    if (T.IsNaN(val)) // sink
                        return CreateConstant<T>(val);
                    if (val == default) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Add(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (T.IsNaN(val)) // sink
                        return CreateConstant<T>(val);
                    if (val == default) // ident
                        return FixShape<T>(in Arg1, buf1);
                    return Add(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = (T)(buf0[off0] + buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(ator0.Current + ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Add(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(T.IsNaN(a))); // sink
                Validation.Assert(a != default); // ident
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(a + src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(a + val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Sub(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Sub(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Sub(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(buf0[Arg0.Ten._root] - val)); // op
                    if (T.IsNaN(val)) // sink
                        return CreateConstant<T>(val);
                    if (val == default) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Add((T)(0 - val), in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (T.IsNaN(val)) // sink
                        return CreateConstant<T>(val);
                    return Sub(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = (T)(buf0[off0] - buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(ator0.Current - ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Sub(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(T.IsNaN(a))); // sink
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(a - src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(a - val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Mul(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Mul(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Mul(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(buf0[Arg0.Ten._root] * val)); // op
                    if (T.IsNaN(val)) // sink
                        return CreateConstant<T>(val);
                    if (val == (T)1) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Mul(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (T.IsNaN(val)) // sink
                        return CreateConstant<T>(val);
                    if (val == (T)1) // ident
                        return FixShape<T>(in Arg1, buf1);
                    return Mul(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = (T)(buf0[off0] * buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(ator0.Current * ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Mul(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(T.IsNaN(a))); // sink
                Validation.Assert(a != (T)1); // ident
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(a * src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(a * val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Div(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Div(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Div(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(buf0[Arg0.Ten._root] / val)); // op
                    if (T.IsNaN(val)) // sink
                        return CreateConstant<T>(val);
                    if (val == (T)1) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Div(in Arg0, buf0, val);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (T.IsNaN(val)) // sink
                        return CreateConstant<T>(val);
                    return Div(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = (T)(buf0[off0] / buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(ator0.Current / ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Div(in Arg arg, Buffer<T> src, T b)
            {
                Validation.Assert(!T.IsNaN(b)); // sink
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(src[off] / b); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(val / b); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Div(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(T.IsNaN(a))); // sink
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(a / src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(a / val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Min(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Min(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Min(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>(Math.Min(buf0[Arg0.Ten._root], val)); // op
                    if (T.IsNaN(val)) // sink
                        return CreateConstant<T>(val);
                    return Min(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (T.IsNaN(val)) // sink
                        return CreateConstant<T>(val);
                    return Min(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = Math.Min(buf0[off0], buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Min(ator0.Current, ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Min(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(T.IsNaN(a))); // sink
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = Math.Min(a, src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Min(a, val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Max(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Max(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Max(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>(Math.Max(buf0[Arg0.Ten._root], val)); // op
                    if (T.IsNaN(val)) // sink
                        return CreateConstant<T>(val);
                    return Max(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (T.IsNaN(val)) // sink
                        return CreateConstant<T>(val);
                    return Max(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = Math.Max(buf0[off0], buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Max(ator0.Current, ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Max(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(T.IsNaN(a))); // sink
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = Math.Max(a, src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Max(a, val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
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
        public static Tensor<T> Add(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Add(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Add(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(buf0[Arg0.Ten._root] + val)); // op
                    if (val == default) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Add(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // ident
                        return FixShape<T>(in Arg1, buf1);
                    return Add(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = (T)(buf0[off0] + buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(ator0.Current + ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Add(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(a != default); // ident
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(a + src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(a + val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Sub(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Sub(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Sub(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(buf0[Arg0.Ten._root] - val)); // op
                    if (val == default) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Add((T)(0 - val), in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    return Sub(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = (T)(buf0[off0] - buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(ator0.Current - ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Sub(T a, in Arg arg, Buffer<T> src)
            {
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(a - src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(a - val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Mul(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Mul(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Mul(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(buf0[Arg0.Ten._root] * val)); // op
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    if (val == (T)1) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Mul(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    if (val == (T)1) // ident
                        return FixShape<T>(in Arg1, buf1);
                    return Mul(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = (T)(buf0[off0] * buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(ator0.Current * ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Mul(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(a == default)); // sink
                Validation.Assert(a != (T)1); // ident
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(a * src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(a * val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Div(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Div(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            private static T DivBop(T a, T b)
            {
                if (b == default)
                    return default;
                return (T)(a / b);
            }

            public Tensor<T> Div(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>(DivBop(buf0[Arg0.Ten._root], val)); // op
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    if (val == (T)1) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Div(in Arg0, buf0, val);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    return Div(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = DivBop(buf0[off0], buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = DivBop(ator0.Current, ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Div(in Arg arg, Buffer<T> src, T b)
            {
                Validation.Assert(b != default); // sink
                Validation.Assert(b != (T)1); // ident
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(src[off] / b); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(val / b); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Div(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(a == default)); // sink
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = DivBop(a, src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = DivBop(a, val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Min(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Min(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Min(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>(T.Min(buf0[Arg0.Ten._root], val)); // op
                    return Min(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    return Min(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = T.Min(buf0[off0], buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = T.Min(ator0.Current, ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Min(T a, in Arg arg, Buffer<T> src)
            {
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = T.Min(a, src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = T.Min(a, val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Max(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Max(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Max(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>(T.Max(buf0[Arg0.Ten._root], val)); // op
                    return Max(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    return Max(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = T.Max(buf0[off0], buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = T.Max(ator0.Current, ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Max(T a, in Arg arg, Buffer<T> src)
            {
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = T.Max(a, src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = T.Max(a, val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
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
        public static Tensor<T> Add(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Add(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Add(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(buf0[Arg0.Ten._root] + val)); // op
                    if (val == default) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Add(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // ident
                        return FixShape<T>(in Arg1, buf1);
                    return Add(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = (T)(buf0[off0] + buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(ator0.Current + ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Add(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(a != default); // ident
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(a + src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(a + val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Sub(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Sub(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Sub(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(buf0[Arg0.Ten._root] - val)); // op
                    if (val == default) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Add((T)(0 - val), in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    return Sub(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = (T)(buf0[off0] - buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(ator0.Current - ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Sub(T a, in Arg arg, Buffer<T> src)
            {
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(a - src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(a - val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Mul(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Mul(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Mul(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(buf0[Arg0.Ten._root] * val)); // op
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    if (val == (T)1) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Mul(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    if (val == (T)1) // ident
                        return FixShape<T>(in Arg1, buf1);
                    return Mul(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = (T)(buf0[off0] * buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(ator0.Current * ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Mul(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(a == default)); // sink
                Validation.Assert(a != (T)1); // ident
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(a * src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(a * val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Div(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Div(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            private static T DivBop(T a, T b)
            {
                if (b == default)
                    return default;
                if (b == (T)(-1))
                    return (T)(-a);
                return (T)(a / b);
            }

            public Tensor<T> Div(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>(DivBop(buf0[Arg0.Ten._root], val)); // op
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    if (val == (T)1) // ident
                        return FixShape<T>(in Arg0, buf0);
                    // REVIEW: Perhaps negating should use a map buffer.
                    if (val == (T)(-1))
                        return Sub(default, in Arg0, buf0);
                    return Div(in Arg0, buf0, val);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    return Div(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = DivBop(buf0[off0], buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = DivBop(ator0.Current, ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Div(in Arg arg, Buffer<T> src, T b)
            {
                Validation.Assert(b != default); // sink
                Validation.Assert(b != (T)1); // ident
                Validation.Assert(b != (T)(-1)); // negate
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(src[off] / b); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(val / b); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Div(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(a == default)); // sink
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = DivBop(a, src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = DivBop(a, val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Min(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Min(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Min(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>(Math.Min(buf0[Arg0.Ten._root], val)); // op
                    if (val == T.MinValue) // sink
                        return CreateConstant<T>(val);
                    return Min(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == T.MinValue) // sink
                        return CreateConstant<T>(val);
                    return Min(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = Math.Min(buf0[off0], buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Min(ator0.Current, ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Min(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(a == T.MinValue)); // sink
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = Math.Min(a, src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Min(a, val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Max(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Max(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Max(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>(Math.Max(buf0[Arg0.Ten._root], val)); // op
                    if (val == T.MaxValue) // sink
                        return CreateConstant<T>(val);
                    return Max(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == T.MaxValue) // sink
                        return CreateConstant<T>(val);
                    return Max(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = Math.Max(buf0[off0], buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Max(ator0.Current, ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Max(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(a == T.MaxValue)); // sink
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = Math.Max(a, src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Max(a, val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
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
        public static Tensor<T> Add(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Add(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Add(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(buf0[Arg0.Ten._root] + val)); // op
                    if (val == default) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Add(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // ident
                        return FixShape<T>(in Arg1, buf1);
                    return Add(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = (T)(buf0[off0] + buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(ator0.Current + ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Add(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(a != default); // ident
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(a + src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(a + val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Sub(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Sub(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Sub(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(buf0[Arg0.Ten._root] - val)); // op
                    if (val == default) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Add((T)(0 - val), in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    return Sub(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = (T)(buf0[off0] - buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(ator0.Current - ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Sub(T a, in Arg arg, Buffer<T> src)
            {
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(a - src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(a - val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Mul(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Mul(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Mul(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(buf0[Arg0.Ten._root] * val)); // op
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    if (val == (T)1) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Mul(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    if (val == (T)1) // ident
                        return FixShape<T>(in Arg1, buf1);
                    return Mul(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = (T)(buf0[off0] * buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(ator0.Current * ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Mul(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(a == default)); // sink
                Validation.Assert(a != (T)1); // ident
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(a * src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(a * val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Div(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Div(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            private static T DivBop(T a, T b)
            {
                if (b == default)
                    return default;
                if (b == (T)(-1))
                    return (T)(-a);
                return (T)(a / b);
            }

            public Tensor<T> Div(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>(DivBop(buf0[Arg0.Ten._root], val)); // op
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    if (val == (T)1) // ident
                        return FixShape<T>(in Arg0, buf0);
                    // REVIEW: Perhaps negating should use a map buffer.
                    if (val == (T)(-1))
                        return Sub(default, in Arg0, buf0);
                    return Div(in Arg0, buf0, val);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    return Div(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = DivBop(buf0[off0], buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = DivBop(ator0.Current, ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Div(in Arg arg, Buffer<T> src, T b)
            {
                Validation.Assert(b != default); // sink
                Validation.Assert(b != (T)1); // ident
                Validation.Assert(b != (T)(-1)); // negate
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(src[off] / b); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(val / b); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Div(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(a == default)); // sink
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = DivBop(a, src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = DivBop(a, val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Min(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Min(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Min(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>(Math.Min(buf0[Arg0.Ten._root], val)); // op
                    if (val == T.MinValue) // sink
                        return CreateConstant<T>(val);
                    return Min(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == T.MinValue) // sink
                        return CreateConstant<T>(val);
                    return Min(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = Math.Min(buf0[off0], buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Min(ator0.Current, ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Min(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(a == T.MinValue)); // sink
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = Math.Min(a, src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Min(a, val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Max(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Max(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Max(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>(Math.Max(buf0[Arg0.Ten._root], val)); // op
                    if (val == T.MaxValue) // sink
                        return CreateConstant<T>(val);
                    return Max(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == T.MaxValue) // sink
                        return CreateConstant<T>(val);
                    return Max(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = Math.Max(buf0[off0], buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Max(ator0.Current, ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Max(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(a == T.MaxValue)); // sink
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = Math.Max(a, src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Max(a, val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
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
        public static Tensor<T> Add(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Add(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Add(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(buf0[Arg0.Ten._root] + val)); // op
                    if (val == default) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Add(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // ident
                        return FixShape<T>(in Arg1, buf1);
                    return Add(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = (T)(buf0[off0] + buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(ator0.Current + ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Add(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(a != default); // ident
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(a + src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(a + val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Sub(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Sub(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Sub(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(buf0[Arg0.Ten._root] - val)); // op
                    if (val == default) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Add((T)(0 - val), in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    return Sub(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = (T)(buf0[off0] - buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(ator0.Current - ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Sub(T a, in Arg arg, Buffer<T> src)
            {
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(a - src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(a - val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Mul(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Mul(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Mul(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(buf0[Arg0.Ten._root] * val)); // op
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    if (val == (T)1) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Mul(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    if (val == (T)1) // ident
                        return FixShape<T>(in Arg1, buf1);
                    return Mul(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = (T)(buf0[off0] * buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(ator0.Current * ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Mul(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(a == default)); // sink
                Validation.Assert(a != (T)1); // ident
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(a * src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(a * val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Div(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Div(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            private static T DivBop(T a, T b)
            {
                if (b == default)
                    return default;
                if (b == (T)(-1))
                    return (T)(-a);
                return (T)(a / b);
            }

            public Tensor<T> Div(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>(DivBop(buf0[Arg0.Ten._root], val)); // op
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    if (val == (T)1) // ident
                        return FixShape<T>(in Arg0, buf0);
                    // REVIEW: Perhaps negating should use a map buffer.
                    if (val == (T)(-1))
                        return Sub(default, in Arg0, buf0);
                    return Div(in Arg0, buf0, val);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    return Div(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = DivBop(buf0[off0], buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = DivBop(ator0.Current, ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Div(in Arg arg, Buffer<T> src, T b)
            {
                Validation.Assert(b != default); // sink
                Validation.Assert(b != (T)1); // ident
                Validation.Assert(b != (T)(-1)); // negate
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(src[off] / b); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(val / b); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Div(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(a == default)); // sink
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = DivBop(a, src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = DivBop(a, val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Min(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Min(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Min(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>(Math.Min(buf0[Arg0.Ten._root], val)); // op
                    if (val == T.MinValue) // sink
                        return CreateConstant<T>(val);
                    return Min(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == T.MinValue) // sink
                        return CreateConstant<T>(val);
                    return Min(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = Math.Min(buf0[off0], buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Min(ator0.Current, ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Min(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(a == T.MinValue)); // sink
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = Math.Min(a, src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Min(a, val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Max(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Max(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Max(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>(Math.Max(buf0[Arg0.Ten._root], val)); // op
                    if (val == T.MaxValue) // sink
                        return CreateConstant<T>(val);
                    return Max(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == T.MaxValue) // sink
                        return CreateConstant<T>(val);
                    return Max(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = Math.Max(buf0[off0], buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Max(ator0.Current, ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Max(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(a == T.MaxValue)); // sink
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = Math.Max(a, src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Max(a, val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
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
        public static Tensor<T> Add(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Add(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Add(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(buf0[Arg0.Ten._root] + val)); // op
                    if (val == default) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Add(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // ident
                        return FixShape<T>(in Arg1, buf1);
                    return Add(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = (T)(buf0[off0] + buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(ator0.Current + ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Add(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(a != default); // ident
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(a + src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(a + val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Sub(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Sub(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Sub(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(buf0[Arg0.Ten._root] - val)); // op
                    if (val == default) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Add((T)(0 - val), in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    return Sub(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = (T)(buf0[off0] - buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(ator0.Current - ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Sub(T a, in Arg arg, Buffer<T> src)
            {
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(a - src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(a - val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Mul(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Mul(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Mul(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(buf0[Arg0.Ten._root] * val)); // op
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    if (val == (T)1) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Mul(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    if (val == (T)1) // ident
                        return FixShape<T>(in Arg1, buf1);
                    return Mul(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = (T)(buf0[off0] * buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(ator0.Current * ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Mul(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(a == default)); // sink
                Validation.Assert(a != (T)1); // ident
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(a * src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(a * val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Div(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Div(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            private static T DivBop(T a, T b)
            {
                if (b == default)
                    return default;
                if (b == (T)(-1))
                    return (T)(-a);
                return (T)(a / b);
            }

            public Tensor<T> Div(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>(DivBop(buf0[Arg0.Ten._root], val)); // op
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    if (val == (T)1) // ident
                        return FixShape<T>(in Arg0, buf0);
                    // REVIEW: Perhaps negating should use a map buffer.
                    if (val == (T)(-1))
                        return Sub(default, in Arg0, buf0);
                    return Div(in Arg0, buf0, val);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    return Div(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = DivBop(buf0[off0], buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = DivBop(ator0.Current, ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Div(in Arg arg, Buffer<T> src, T b)
            {
                Validation.Assert(b != default); // sink
                Validation.Assert(b != (T)1); // ident
                Validation.Assert(b != (T)(-1)); // negate
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(src[off] / b); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(val / b); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Div(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(a == default)); // sink
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = DivBop(a, src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = DivBop(a, val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Min(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Min(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Min(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>(Math.Min(buf0[Arg0.Ten._root], val)); // op
                    if (val == T.MinValue) // sink
                        return CreateConstant<T>(val);
                    return Min(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == T.MinValue) // sink
                        return CreateConstant<T>(val);
                    return Min(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = Math.Min(buf0[off0], buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Min(ator0.Current, ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Min(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(a == T.MinValue)); // sink
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = Math.Min(a, src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Min(a, val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Max(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Max(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Max(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>(Math.Max(buf0[Arg0.Ten._root], val)); // op
                    if (val == T.MaxValue) // sink
                        return CreateConstant<T>(val);
                    return Max(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == T.MaxValue) // sink
                        return CreateConstant<T>(val);
                    return Max(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = Math.Max(buf0[off0], buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Max(ator0.Current, ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Max(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(a == T.MaxValue)); // sink
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = Math.Max(a, src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Max(a, val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
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
        public static Tensor<T> Add(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Add(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Add(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(buf0[Arg0.Ten._root] + val)); // op
                    if (val == default) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Add(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // ident
                        return FixShape<T>(in Arg1, buf1);
                    return Add(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = (T)(buf0[off0] + buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(ator0.Current + ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Add(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(a != default); // ident
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(a + src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(a + val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Sub(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Sub(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Sub(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(buf0[Arg0.Ten._root] - val)); // op
                    if (val == default) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Add((T)(0 - val), in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    return Sub(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = (T)(buf0[off0] - buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(ator0.Current - ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Sub(T a, in Arg arg, Buffer<T> src)
            {
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(a - src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(a - val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Mul(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Mul(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Mul(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(buf0[Arg0.Ten._root] * val)); // op
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    if (val == (T)1) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Mul(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    if (val == (T)1) // ident
                        return FixShape<T>(in Arg1, buf1);
                    return Mul(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = (T)(buf0[off0] * buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(ator0.Current * ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Mul(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(a == default)); // sink
                Validation.Assert(a != (T)1); // ident
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(a * src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(a * val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Div(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Div(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            private static T DivBop(T a, T b)
            {
                if (b == default)
                    return default;
                return (T)(a / b);
            }

            public Tensor<T> Div(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>(DivBop(buf0[Arg0.Ten._root], val)); // op
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    if (val == (T)1) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Div(in Arg0, buf0, val);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    return Div(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = DivBop(buf0[off0], buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = DivBop(ator0.Current, ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Div(in Arg arg, Buffer<T> src, T b)
            {
                Validation.Assert(b != default); // sink
                Validation.Assert(b != (T)1); // ident
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(src[off] / b); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(val / b); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Div(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(a == default)); // sink
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = DivBop(a, src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = DivBop(a, val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Min(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Min(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Min(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>(Math.Min(buf0[Arg0.Ten._root], val)); // op
                    if (val == T.MinValue) // sink
                        return CreateConstant<T>(val);
                    return Min(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == T.MinValue) // sink
                        return CreateConstant<T>(val);
                    return Min(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = Math.Min(buf0[off0], buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Min(ator0.Current, ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Min(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(a == T.MinValue)); // sink
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = Math.Min(a, src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Min(a, val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Max(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Max(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Max(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>(Math.Max(buf0[Arg0.Ten._root], val)); // op
                    if (val == T.MaxValue) // sink
                        return CreateConstant<T>(val);
                    return Max(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == T.MaxValue) // sink
                        return CreateConstant<T>(val);
                    return Max(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = Math.Max(buf0[off0], buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Max(ator0.Current, ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Max(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(a == T.MaxValue)); // sink
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = Math.Max(a, src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Max(a, val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
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
        public static Tensor<T> Add(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Add(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Add(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(buf0[Arg0.Ten._root] + val)); // op
                    if (val == default) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Add(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // ident
                        return FixShape<T>(in Arg1, buf1);
                    return Add(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = (T)(buf0[off0] + buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(ator0.Current + ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Add(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(a != default); // ident
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(a + src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(a + val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Sub(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Sub(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Sub(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(buf0[Arg0.Ten._root] - val)); // op
                    if (val == default) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Add((T)(0 - val), in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    return Sub(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = (T)(buf0[off0] - buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(ator0.Current - ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Sub(T a, in Arg arg, Buffer<T> src)
            {
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(a - src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(a - val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Mul(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Mul(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Mul(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(buf0[Arg0.Ten._root] * val)); // op
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    if (val == (T)1) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Mul(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    if (val == (T)1) // ident
                        return FixShape<T>(in Arg1, buf1);
                    return Mul(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = (T)(buf0[off0] * buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(ator0.Current * ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Mul(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(a == default)); // sink
                Validation.Assert(a != (T)1); // ident
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(a * src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(a * val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Div(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Div(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            private static T DivBop(T a, T b)
            {
                if (b == default)
                    return default;
                return (T)(a / b);
            }

            public Tensor<T> Div(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>(DivBop(buf0[Arg0.Ten._root], val)); // op
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    if (val == (T)1) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Div(in Arg0, buf0, val);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    return Div(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = DivBop(buf0[off0], buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = DivBop(ator0.Current, ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Div(in Arg arg, Buffer<T> src, T b)
            {
                Validation.Assert(b != default); // sink
                Validation.Assert(b != (T)1); // ident
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(src[off] / b); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(val / b); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Div(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(a == default)); // sink
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = DivBop(a, src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = DivBop(a, val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Min(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Min(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Min(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>(Math.Min(buf0[Arg0.Ten._root], val)); // op
                    if (val == T.MinValue) // sink
                        return CreateConstant<T>(val);
                    return Min(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == T.MinValue) // sink
                        return CreateConstant<T>(val);
                    return Min(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = Math.Min(buf0[off0], buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Min(ator0.Current, ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Min(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(a == T.MinValue)); // sink
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = Math.Min(a, src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Min(a, val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Max(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Max(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Max(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>(Math.Max(buf0[Arg0.Ten._root], val)); // op
                    if (val == T.MaxValue) // sink
                        return CreateConstant<T>(val);
                    return Max(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == T.MaxValue) // sink
                        return CreateConstant<T>(val);
                    return Max(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = Math.Max(buf0[off0], buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Max(ator0.Current, ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Max(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(a == T.MaxValue)); // sink
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = Math.Max(a, src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Max(a, val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
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
        public static Tensor<T> Add(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Add(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Add(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(buf0[Arg0.Ten._root] + val)); // op
                    if (val == default) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Add(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // ident
                        return FixShape<T>(in Arg1, buf1);
                    return Add(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = (T)(buf0[off0] + buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(ator0.Current + ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Add(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(a != default); // ident
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(a + src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(a + val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Sub(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Sub(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Sub(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(buf0[Arg0.Ten._root] - val)); // op
                    if (val == default) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Add((T)(0 - val), in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    return Sub(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = (T)(buf0[off0] - buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(ator0.Current - ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Sub(T a, in Arg arg, Buffer<T> src)
            {
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(a - src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(a - val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Mul(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Mul(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Mul(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(buf0[Arg0.Ten._root] * val)); // op
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    if (val == (T)1) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Mul(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    if (val == (T)1) // ident
                        return FixShape<T>(in Arg1, buf1);
                    return Mul(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = (T)(buf0[off0] * buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(ator0.Current * ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Mul(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(a == default)); // sink
                Validation.Assert(a != (T)1); // ident
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(a * src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(a * val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Div(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Div(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            private static T DivBop(T a, T b)
            {
                if (b == default)
                    return default;
                return (T)(a / b);
            }

            public Tensor<T> Div(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>(DivBop(buf0[Arg0.Ten._root], val)); // op
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    if (val == (T)1) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Div(in Arg0, buf0, val);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    return Div(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = DivBop(buf0[off0], buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = DivBop(ator0.Current, ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Div(in Arg arg, Buffer<T> src, T b)
            {
                Validation.Assert(b != default); // sink
                Validation.Assert(b != (T)1); // ident
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(src[off] / b); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(val / b); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Div(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(a == default)); // sink
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = DivBop(a, src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = DivBop(a, val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Min(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Min(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Min(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>(Math.Min(buf0[Arg0.Ten._root], val)); // op
                    if (val == T.MinValue) // sink
                        return CreateConstant<T>(val);
                    return Min(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == T.MinValue) // sink
                        return CreateConstant<T>(val);
                    return Min(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = Math.Min(buf0[off0], buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Min(ator0.Current, ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Min(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(a == T.MinValue)); // sink
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = Math.Min(a, src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Min(a, val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Max(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Max(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Max(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>(Math.Max(buf0[Arg0.Ten._root], val)); // op
                    if (val == T.MaxValue) // sink
                        return CreateConstant<T>(val);
                    return Max(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == T.MaxValue) // sink
                        return CreateConstant<T>(val);
                    return Max(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = Math.Max(buf0[off0], buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Max(ator0.Current, ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Max(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(a == T.MaxValue)); // sink
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = Math.Max(a, src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Max(a, val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
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
        public static Tensor<T> Add(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Add(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Add(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(buf0[Arg0.Ten._root] + val)); // op
                    if (val == default) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Add(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // ident
                        return FixShape<T>(in Arg1, buf1);
                    return Add(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = (T)(buf0[off0] + buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(ator0.Current + ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Add(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(a != default); // ident
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(a + src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(a + val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Sub(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Sub(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Sub(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(buf0[Arg0.Ten._root] - val)); // op
                    if (val == default) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Add((T)(0 - val), in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    return Sub(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = (T)(buf0[off0] - buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(ator0.Current - ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Sub(T a, in Arg arg, Buffer<T> src)
            {
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(a - src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(a - val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Mul(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Mul(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Mul(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(buf0[Arg0.Ten._root] * val)); // op
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    if (val == (T)1) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Mul(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    if (val == (T)1) // ident
                        return FixShape<T>(in Arg1, buf1);
                    return Mul(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = (T)(buf0[off0] * buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(ator0.Current * ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Mul(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(a == default)); // sink
                Validation.Assert(a != (T)1); // ident
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(a * src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(a * val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Div(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Div(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            private static T DivBop(T a, T b)
            {
                if (b == default)
                    return default;
                return (T)(a / b);
            }

            public Tensor<T> Div(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>(DivBop(buf0[Arg0.Ten._root], val)); // op
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    if (val == (T)1) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return Div(in Arg0, buf0, val);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    return Div(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = DivBop(buf0[off0], buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = DivBop(ator0.Current, ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Div(in Arg arg, Buffer<T> src, T b)
            {
                Validation.Assert(b != default); // sink
                Validation.Assert(b != (T)1); // ident
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(src[off] / b); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(val / b); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Div(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(a == default)); // sink
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = DivBop(a, src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = DivBop(a, val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Min(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Min(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Min(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>(Math.Min(buf0[Arg0.Ten._root], val)); // op
                    if (val == T.MinValue) // sink
                        return CreateConstant<T>(val);
                    return Min(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == T.MinValue) // sink
                        return CreateConstant<T>(val);
                    return Min(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = Math.Min(buf0[off0], buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Min(ator0.Current, ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Min(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(a == T.MinValue)); // sink
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = Math.Min(a, src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Min(a, val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> Max(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Max(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Max(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>(Math.Max(buf0[Arg0.Ten._root], val)); // op
                    if (val == T.MaxValue) // sink
                        return CreateConstant<T>(val);
                    return Max(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == T.MaxValue) // sink
                        return CreateConstant<T>(val);
                    return Max(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = Math.Max(buf0[off0], buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Max(ator0.Current, ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Max(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(!(a == T.MaxValue)); // sink
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = Math.Max(a, src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = Math.Max(a, val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
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
        public static Tensor<T> AddSub(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.AddSub(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> AddSub(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(buf0[Arg0.Ten._root] ^ val)); // op
                    if (val == default) // ident
                        return FixShape<T>(in Arg0, buf0);
                    return AddSub(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // ident
                        return FixShape<T>(in Arg1, buf1);
                    return AddSub(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = (T)(buf0[off0] ^ buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(ator0.Current ^ ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> AddSub(T a, in Arg arg, Buffer<T> src)
            {
                Validation.Assert(a != default); // ident
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(a ^ src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(a ^ val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

        public static Tensor<T> MulDivMin(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.MulDivMin(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> MulDivMin(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(buf0[Arg0.Ten._root] & val)); // op
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    else // ident
                        return FixShape<T>(in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    if (val == default) // sink
                        return CreateConstant<T>(val);
                    else // ident
                        return FixShape<T>(in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = (T)(buf0[off0] & buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(ator0.Current & ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

        }

        public static Tensor<T> Max(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk)
        {
            Validation.AssertValue(ten0);
            Validation.AssertValue(ten1);

            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<T>();
            return pw.Max(ten0._buf, ten1._buf);
        }

        partial struct PointWise
        {
            public Tensor<T> Max(Buffer<T> buf0, Buffer<T> buf1)
            {
                bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
                bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

                if (con1)
                {
                    var val = buf1[Arg1.Ten._root];
                    if (con0)
                        return CreateConstant<T>((T)(buf0[Arg0.Ten._root] | val)); // op
                    return Max(val, in Arg0, buf0);
                }
                if (con0)
                {
                    var val = buf0[Arg0.Ten._root];
                    return Max(val, in Arg1, buf1);
                }

                var count = CountRes;
                var dst = new T[count];
                if (Arg0.Regular && Arg1.Regular)
                {
                    // The simple case: both are regular.
                    long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                    long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                    for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                        dst[i] = (T)(buf0[off0] | buf1[off1]); // op
                }
                else
                {
                    long i = 0;
                    using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                    using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                    while (ator0.MoveNext() && ator1.MoveNext())
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(ator0.Current | ator1.Current); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }

            private Tensor<T> Max(T a, in Arg arg, Buffer<T> src)
            {
                // REVIEW: When the input shares cells, the output could as well.
                var count = CountRes;
                var dst = new T[count];
                if (arg.Regular)
                {
                    Validation.Assert(arg.Ten._delta != 0);
                    var off = arg.Ten._root;
                    var delta = arg.Ten._delta;
                    for (long i = 0; i < count; i++, off += delta)
                        dst[i] = (T)(a | src[off]); // op
                }
                else
                {
                    long i = 0;
                    foreach (var val in GetValues(in arg, src))
                    {
                        Validation.AssertIndex(i, count);
                        dst[i++] = (T)(a | val); // op
                    }
                    Validation.Assert(i == count);
                }
                return CreateFull<T>(dst);
            }
        }

    }
}

