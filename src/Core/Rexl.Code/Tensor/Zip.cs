// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

partial class Tensor
{
    partial struct PointWise
    {
        public static Tensor<U> Zip<T0, T1, U>(Tensor<T0> ten0, Tensor<T1> ten1, Func<T0, T1, U> map, out bool shrunk)
        {
            var pw = new PointWise(ten0, ten1);
            shrunk = pw.Arg0.Shrunk | pw.Arg1.Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<U>();
            return pw.ZipCore(ten0._buf, ten1._buf, map);
        }

        private Tensor<U> ZipCore<T0, T1, U>(Buffer<T0> buf0, Buffer<T1> buf1, Func<T0, T1, U> map)
        {
            bool con0 = Arg0.Ten._delta == 0 && Arg0.Ten._regular;
            bool con1 = Arg1.Ten._delta == 0 && Arg1.Ten._regular;

            if (con1)
            {
                var val1 = buf1[Arg1.Ten._root];
                if (con0)
                    return CreateConstant<U>(map(buf0[Arg0.Ten._root], val1));
                return ZipCore(buf0, val1, map);
            }
            if (con0)
            {
                var val0 = buf0[Arg0.Ten._root];
                return ZipCore(val0, buf1, map);
            }

            // REVIEW: When both inputs have zero stride, the output could as well, reducing computation.
            var count = CountRes;
            var dst = new U[count];
            if (Arg0.Regular && Arg1.Regular)
            {
                // The simple case: both are regular.
                long off0 = Arg0.Ten._root, off1 = Arg1.Ten._root;
                long delta0 = Arg0.Ten._delta, delta1 = Arg1.Ten._delta;
                for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1)
                    dst[i] = map(buf0[off0], buf1[off1]);
            }
            else
            {
                long i = 0;
                using var ator0 = GetValues(in Arg0, buf0).GetEnumerator();
                using var ator1 = GetValues(in Arg1, buf1).GetEnumerator();
                while (ator0.MoveNext() && ator1.MoveNext())
                {
                    Validation.AssertIndex(i, count);
                    dst[i++] = map(ator0.Current, ator1.Current);
                }
                Validation.Assert(i == count);
            }
            return CreateFull<U>(dst);
        }

        private Tensor<U> ZipCore<T0, T1, U>(Buffer<T0> buf0, T1 val1, Func<T0, T1, U> map)
        {
            // REVIEW: When the input shares cells, the output could as well.
            var count = CountRes;
            var dst = new U[count];
            if (Arg0.Regular)
            {
                Validation.Assert(Arg0.Ten._delta != 0);
                var off = Arg0.Ten._root;
                var delta = Arg0.Ten._delta;
                for (long i = 0; i < count; i++, off += delta)
                    dst[i] = map(buf0[off], val1);
            }
            else
            {
                long i = 0;
                foreach (var val0 in GetValues(in Arg0, buf0))
                {
                    Validation.AssertIndex(i, count);
                    dst[i++] = map(val0, val1);
                }
                Validation.Assert(i == count);
            }
            return CreateFull<U>(dst);
        }

        private Tensor<U> ZipCore<T0, T1, U>(T0 val0, Buffer<T1> buf1, Func<T0, T1, U> map)
        {
            // REVIEW: When the input shares cells, the output could as well.
            var count = CountRes;
            var dst = new U[count];
            if (Arg1.Regular)
            {
                Validation.Assert(Arg1.Ten._delta != 0);
                var off = Arg1.Ten._root;
                var delta = Arg1.Ten._delta;
                for (long i = 0; i < count; i++, off += delta)
                    dst[i] = map(val0, buf1[off]);
            }
            else
            {
                long i = 0;
                foreach (var val1 in GetValues(in Arg1, buf1))
                {
                    Validation.AssertIndex(i, count);
                    dst[i++] = map(val0, val1);
                }
                Validation.Assert(i == count);
            }
            return CreateFull<U>(dst);
        }
    }

    partial struct PointWise
    {
        public static Tensor<U> Zip<T0, T1, T2, U>(Tensor<T0> ten0, Tensor<T1> ten1, Tensor<T2> ten2, Func<T0, T1, T2, U> map, out bool shrunk)
        {
            var pw = new PointWise(ten0, ten1, ten2);
            shrunk = pw.Args[0].Shrunk | pw.Args[1].Shrunk | pw.Args[2].Shrunk;
            if (pw.CountRes == 0)
                return pw.CreateEmpty<U>();
            return pw.ZipCore(ten0._buf, ten1._buf, ten2._buf, map);
        }

        private Tensor<U> ZipCore<T0, T1, T2, U>(Buffer<T0> buf0, Buffer<T1> buf1, Buffer<T2> buf2, Func<T0, T1, T2, U> map)
        {
            bool con0 = Args[0].Ten._delta == 0 && Args[0].Ten._regular;
            bool con1 = Args[1].Ten._delta == 0 && Args[1].Ten._regular;
            bool con2 = Args[2].Ten._delta == 0 && Args[2].Ten._regular;
            bool any = con0 | con1 | con2;

            if (any)
            {
                if (con0 & con1 & con2)
                    return CreateConstant<U>(map(buf0[Args[0].Ten._root], buf1[Args[1].Ten._root], buf2[Args[2].Ten._root]));
                // REVIEW: Optimize other constant cases?
            }

            // REVIEW: When all inputs have zero stride, the output could as well, reducing computation.
            var count = CountRes;
            var dst = new U[count];
            if (Args[0].Regular && Args[1].Regular && Args[2].Regular)
            {
                // The simple case: all are regular.
                long off0 = Args[0].Ten._root, off1 = Args[1].Ten._root, off2 = Args[2].Ten._root;
                long delta0 = Args[0].Ten._delta, delta1 = Args[1].Ten._delta, delta2 = Args[2].Ten._delta;
                for (long i = 0; i < count; i++, off0 += delta0, off1 += delta1, off2 += delta2)
                    dst[i] = map(buf0[off0], buf1[off1], buf2[off2]);
            }
            else
            {
                long i = 0;
                using var ator0 = GetValues(in Args[0], buf0).GetEnumerator();
                using var ator1 = GetValues(in Args[1], buf1).GetEnumerator();
                using var ator2 = GetValues(in Args[2], buf2).GetEnumerator();
                while (ator0.MoveNext() && ator1.MoveNext() && ator2.MoveNext())
                {
                    Validation.AssertIndex(i, count);
                    dst[i++] = map(ator0.Current, ator1.Current, ator2.Current);
                }
                Validation.Assert(i == count);
            }
            return CreateFull<U>(dst);
        }
    }
}
