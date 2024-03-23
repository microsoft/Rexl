// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;

using Microsoft.Rexl;
using Microsoft.Rexl.Code;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RexlTest;

using Integer = System.Numerics.BigInteger;

[TestClass]
public sealed class TypeManagerTests
{
    [TestMethod]
    public void TryEnsureSysTypeNameLength()
    {
        DType dtType = DType.EmptyRecordReq;
        for (int i = 0; i < 300; i++)
            dtType = dtType.AddNameType(new DName($"a{i}"), DType.Text);

        var tm1 = new TestEnumTypeManager();
        bool tmp1 = tm1.TryEnsureSysType(dtType, out var stType1);
        Assert.IsTrue(tmp1);

        var tm2 = new TestEnumTypeManager();
        bool tmp2 = tm2.TryEnsureSysType(dtType, out var stType2);
        Assert.IsTrue(tmp2);

        Assert.IsTrue(stType1.Name == stType2.Name);
    }

    private static bool TryGetStrictEqCmp(TypeManager tm, DType type, out IEqualityComparer ec, bool ci = false)
    {
        return tm.TryEnsureEqCmp(type, strict: true, ci: ci, ec: out ec, stEq: out var stEq, stItem: out var stItem);
    }

    [TestMethod]
    public void TestStrictEqCmp_General()
    {
        var tm = new TestEnumTypeManager();

        IEqualityComparer ec;
        IEqualityComparer ecOpt;

        // Most non-nullable value types can't be bad keys.
        Assert.IsTrue(TryGetStrictEqCmp(tm, DType.I4Req, out ec) & ec == EqualityComparer<int>.Default);
        Assert.IsTrue(TryGetStrictEqCmp(tm, DType.U1Req, out ec) & ec == EqualityComparer<byte>.Default);
        Assert.IsTrue(TryGetStrictEqCmp(tm, DType.IAReq, out ec) & ec == EqualityComparer<Integer>.Default);
        Assert.IsTrue(TryGetStrictEqCmp(tm, DType.BitReq, out ec) & ec == EqualityComparer<bool>.Default);
        Assert.IsTrue(TryGetStrictEqCmp(tm, DType.DateReq, out ec) & ec == EqualityComparer<RDate>.Default);
        Assert.IsTrue(TryGetStrictEqCmp(tm, DType.TimeReq, out ec) & ec == EqualityComparer<TimeSpan>.Default);

        // Vac/null are not equatable.
        Assert.IsTrue(!TryGetStrictEqCmp(tm, DType.Vac, out ec) & ec is null);
        Assert.IsTrue(!TryGetStrictEqCmp(tm, DType.Null, out ec) & ec is null);

        // Non-equatable types should fail.
        Assert.IsTrue(!TryGetStrictEqCmp(tm, DType.General, out ec) & ec is null);
        Assert.IsTrue(!TryGetStrictEqCmp(tm, DType.I4Req.ToSequence(), out ec) & ec is null);
        Assert.IsTrue(!TryGetStrictEqCmp(tm, DType.U1Req.ToTensor(false, 3), out ec) & ec is null);
        Assert.IsTrue(!TryGetStrictEqCmp(tm, DType.CreateTuple(false, DType.I4Req.ToSequence()), out ec) & ec is null);
        Assert.IsTrue(!TryGetStrictEqCmp(tm, DType.CreateTuple(false, DType.CreateTuple(false, DType.I4Req.ToSequence())), out ec) & ec is null);

        // For reference types, we share the equality comparer between opt and req.
        Assert.IsTrue(TryGetStrictEqCmp(tm, DType.EmptyTupleReq, out ec) & ec is not null);
        Assert.IsTrue(TryGetStrictEqCmp(tm, DType.EmptyTupleOpt, out ecOpt) & ecOpt is not null);
        Assert.AreSame(ec, ecOpt);
        Assert.IsTrue(TryGetStrictEqCmp(tm, DType.EmptyRecordReq, out ec) & ec is not null);
        Assert.IsTrue(TryGetStrictEqCmp(tm, DType.EmptyRecordOpt, out ecOpt) & ecOpt is not null);
        Assert.AreSame(ec, ecOpt);

        // Opt reports null as bad.
        Assert.IsTrue(TryGetStrictEqCmp(tm, DType.I4Opt, out ec) & ec is not null);
        {
            Assert.IsFalse(ec.Equals(null, null));
            Assert.ThrowsException<ArgumentException>(() => ec.Equals(null, true));
            Assert.IsTrue(ec.Equals(3, 3));

            var eq = ec as EqualityComparer<int?>;
            Assert.IsNotNull(eq);
            Assert.IsTrue(eq.Equals(0, 0));
            Assert.IsTrue(eq.Equals(1, 1));
            Assert.IsTrue(eq.Equals(int.MaxValue, int.MaxValue));
            Assert.IsTrue(eq.Equals(int.MinValue, int.MinValue));
            Assert.IsFalse(eq.Equals(null, null));
        }

        // Floating point types report NaN as bad.
        Assert.IsTrue(TryGetStrictEqCmp(tm, DType.R8Req, out ec) & ec is not null);
        {
            Assert.ThrowsException<ArgumentException>(() => ec.Equals(null, null));
            Assert.ThrowsException<ArgumentException>(() => ec.Equals(false, true));
            Assert.IsTrue(ec.Equals(3.5, 3.5));
            Assert.IsFalse(ec.Equals(double.NaN, double.NaN));

            var eq = ec as EqualityComparer<double>;
            Assert.IsNotNull(eq);
            Assert.IsTrue(eq.Equals(0.0, 0.0));
            Assert.IsTrue(eq.Equals(-0.0, -0.0));
            Assert.IsTrue(eq.Equals(3.5, 3.5));
            Assert.IsTrue(eq.Equals(double.PositiveInfinity, double.PositiveInfinity));
            Assert.IsTrue(eq.Equals(double.NegativeInfinity, double.NegativeInfinity));
            Assert.IsFalse(eq.Equals(double.NaN, double.NaN));
        }
        Assert.IsTrue(TryGetStrictEqCmp(tm, DType.R4Req, out ec) & ec is not null);
        {
            Assert.ThrowsException<ArgumentException>(() => ec.Equals(null, null));
            Assert.ThrowsException<ArgumentException>(() => ec.Equals(false, true));

            var eq = ec as EqualityComparer<float>;
            Assert.IsNotNull(eq);
            Assert.IsTrue(eq.Equals(0.0f, 0.0f));
            Assert.IsTrue(eq.Equals(-0.0f, -0.0f));
            Assert.IsTrue(eq.Equals(3.5f, 3.5f));
            Assert.IsTrue(eq.Equals(float.PositiveInfinity, float.PositiveInfinity));
            Assert.IsTrue(eq.Equals(float.NegativeInfinity, float.NegativeInfinity));
            Assert.IsFalse(eq.Equals(float.NaN, float.NaN));
        }
        Assert.IsTrue(TryGetStrictEqCmp(tm, DType.R8Opt, out ec) & ec is not null);
        {
            Assert.IsFalse(ec.Equals(null, null));
            Assert.ThrowsException<ArgumentException>(() => ec.Equals(null, true));

            var eq = ec as EqualityComparer<double?>;
            Assert.IsNotNull(eq);
            Assert.IsTrue(eq.Equals(0.0, 0.0));
            Assert.IsTrue(eq.Equals(-0.0, -0.0));
            Assert.IsTrue(eq.Equals(3.5, 3.5));
            Assert.IsTrue(eq.Equals(double.PositiveInfinity, double.PositiveInfinity));
            Assert.IsTrue(eq.Equals(double.NegativeInfinity, double.NegativeInfinity));
            Assert.IsFalse(eq.Equals(double.NaN, double.NaN));
            Assert.IsFalse(eq.Equals(null, null));
            Assert.IsFalse(eq.Equals(0.0, null));
        }
        Assert.IsTrue(TryGetStrictEqCmp(tm, DType.R4Opt, out ec) & ec is not null);
        {
            Assert.IsFalse(ec.Equals(null, null));
            Assert.ThrowsException<ArgumentException>(() => ec.Equals(null, true));

            var eq = ec as EqualityComparer<float?>;
            Assert.IsNotNull(eq);
            Assert.IsTrue(eq.Equals(0.0f, 0.0f));
            Assert.IsTrue(eq.Equals(-0.0f, -0.0f));
            Assert.IsTrue(eq.Equals(3.5f, 3.5f));
            Assert.IsTrue(eq.Equals(float.PositiveInfinity, float.PositiveInfinity));
            Assert.IsTrue(eq.Equals(float.NegativeInfinity, float.NegativeInfinity));
            Assert.IsFalse(eq.Equals(float.NaN, float.NaN));
            Assert.IsFalse(eq.Equals(null, null));
            Assert.IsFalse(eq.Equals(0.0f, null));
        }

        // Text types report null as bad.
        Assert.IsTrue(TryGetStrictEqCmp(tm, DType.Text, out ec) & ec is not null);
        {
            Assert.IsFalse(ec.Equals(null, null));
            Assert.IsFalse(ec.Equals("hi", null));
            Assert.ThrowsException<ArgumentException>(() => ec.Equals(null, true));

            var eq = ec as EqualityComparer<string>;
            Assert.IsNotNull(eq);
            Assert.IsTrue(eq.Equals("hi", "hi"));
            Assert.IsFalse(eq.Equals("hi", "HI"));
            Assert.IsTrue(eq.Equals("", ""));
            Assert.IsFalse(eq.Equals("hi", null));
            Assert.IsFalse(eq.Equals(null, null));
        }
        Assert.IsTrue(TryGetStrictEqCmp(tm, DType.Text, out ec, ci: true) & ec is not null);
        {
            Assert.IsFalse(ec.Equals(null, null));
            Assert.IsFalse(ec.Equals("hi", null));
            Assert.ThrowsException<ArgumentException>(() => ec.Equals(null, true));

            var eq = ec as EqualityComparer<string>;
            Assert.IsNotNull(eq);
            Assert.IsTrue(eq.Equals("hi", "hi"));
            Assert.IsTrue(eq.Equals("hi", "HI"));
            Assert.IsTrue(eq.Equals("", ""));
            Assert.IsFalse(eq.Equals("hi", null));
            Assert.IsFalse(eq.Equals(null, null));
        }

        // Uri types report null as bad.
        Assert.IsTrue(TryGetStrictEqCmp(tm, DType.UriGen, out ec) & ec is not null);
        {
            Assert.IsFalse(ec.Equals(null, null));
            Assert.ThrowsException<ArgumentException>(() => ec.Equals(null, true));

            var eq = ec as EqualityComparer<Link>;
            Assert.IsNotNull(eq);
            var link = Link.CreateGeneric("blah");
            Assert.IsTrue(eq.Equals(link, link));
            Assert.IsFalse(eq.Equals(null, null));
            Assert.IsFalse(eq.Equals(link, null));
        }
    }

    [TestMethod]
    public void TestStrictEqCmp_Tuple()
    {
        var tm = new TestEnumTypeManager();

        IEqualityComparer ec;

        // Tuple types should succeed, as long as all item types are good.
        Assert.IsTrue(TryGetStrictEqCmp(tm, DType.EmptyTupleReq, out ec) & ec is not null);
        {
            Assert.IsFalse(ec.Equals(null, null));
            Assert.ThrowsException<ArgumentException>(() => ec.Equals(null, true));

            var eq = ec as EqualityComparer<TupleImpl>;
            Assert.IsNotNull(eq);
            var tup = new TupleImpl();
            Assert.IsTrue(eq.Equals(tup, tup));
            Assert.IsTrue(eq.Equals(tup, new TupleImpl()));
            Assert.IsFalse(eq.Equals(null, null));
            Assert.IsFalse(eq.Equals(null, tup));
        }
        Assert.IsTrue(TryGetStrictEqCmp(tm, DType.CreateTuple(false, DType.I4Req), out ec) & ec is not null);
        {
            Assert.IsFalse(ec.Equals(null, null));
            Assert.ThrowsException<ArgumentException>(() => ec.Equals(null, true));

            var eq = ec as EqualityComparer<TupleImpl<int>>;
            Assert.IsNotNull(eq);
            var tup = new TupleImpl<int>();
            Assert.IsTrue(eq.Equals(tup, tup));
            Assert.IsFalse(eq.Equals(null, null));
        }
        Assert.IsTrue(TryGetStrictEqCmp(tm, DType.CreateTuple(false, DType.I4Req, DType.BitReq), out ec) & ec is not null);
        {
            var eq = ec as EqualityComparer<TupleImpl<int, bool>>;
            Assert.IsNotNull(eq);
            var tup = new TupleImpl<int, bool>();
            Assert.IsTrue(eq.Equals(tup, tup));
            Assert.IsTrue(eq.Equals(tup, new TupleImpl<int, bool>()));
            Assert.IsFalse(eq.Equals(null, null));
        }
        Assert.IsTrue(TryGetStrictEqCmp(tm, DType.CreateTuple(false, DType.I4Req, DType.BitReq, DType.IAReq), out ec) & ec is not null);
        {
            var eq = ec as EqualityComparer<TupleImpl<int, bool, Integer>>;
            Assert.IsNotNull(eq);
            var tup = new TupleImpl<int, bool, Integer>();
            tup._F1 = true;
            Assert.IsTrue(eq.Equals(tup, tup));
            Assert.IsFalse(eq.Equals(tup, new TupleImpl<int, bool, Integer>()));
            Assert.IsFalse(eq.Equals(null, null));
        }
        Assert.IsTrue(TryGetStrictEqCmp(tm, DType.CreateTuple(false, DType.I4Req, DType.BitReq, DType.IAReq, DType.U1Req), out ec) & ec is not null);
        {
            var eq = ec as EqualityComparer<TupleImpl<int, bool, Integer, byte>>;
            Assert.IsNotNull(eq);
            var tup = new TupleImpl<int, bool, Integer, byte>();
            Assert.IsTrue(eq.Equals(tup, tup));
            Assert.IsFalse(eq.Equals(null, null));
        }
        Assert.IsTrue(TryGetStrictEqCmp(tm, DType.CreateTuple(false, DType.I4Opt), out ec) & ec is not null);
        {
            var eq = ec as EqualityComparer<TupleImpl<int?>>;
            Assert.IsNotNull(eq);
            var tup = new TupleImpl<int?>() { _F0 = 0 };
            Assert.IsTrue(eq.Equals(tup, tup));
            Assert.IsTrue(eq.Equals(tup, new TupleImpl<int?>() { _F0 = 0 }));
            Assert.IsFalse(eq.Equals(tup = new TupleImpl<int?>() { _F0 = null }, tup));
            Assert.IsFalse(eq.Equals(tup, new TupleImpl<int?>() { _F0 = null }));
            Assert.IsFalse(eq.Equals(tup = new TupleImpl<int?>(), tup));
            Assert.IsFalse(eq.Equals(tup, new TupleImpl<int?>()));
            Assert.IsFalse(eq.Equals(null, tup));
            Assert.IsFalse(eq.Equals(null, null));
        }
        Assert.IsTrue(TryGetStrictEqCmp(tm, DType.CreateTuple(false, DType.R8Req), out ec) & ec is not null);
        {
            var eq = ec as EqualityComparer<TupleImpl<double>>;
            Assert.IsNotNull(eq);
            var tup = new TupleImpl<double>() { _F0 = 3.5 };
            Assert.IsTrue(eq.Equals(tup, tup));
            Assert.IsTrue(eq.Equals(tup = new TupleImpl<double>() { _F0 = 0 }, tup));
            Assert.IsFalse(eq.Equals(tup = new TupleImpl<double>() { _F0 = double.NaN }, tup));
            Assert.IsTrue(eq.Equals(tup = new TupleImpl<double>(), tup));
            Assert.IsFalse(eq.Equals(null));
        }
        Assert.IsTrue(TryGetStrictEqCmp(tm, DType.CreateTuple(false, DType.R8Opt), out ec) & ec is not null);
        {
            var eq = ec as EqualityComparer<TupleImpl<double?>>;
            Assert.IsNotNull(eq);
            var tup = new TupleImpl<double?>() { _F0 = 3.5 };
            Assert.IsTrue(eq.Equals(tup, tup));
            Assert.IsTrue(eq.Equals(tup = new TupleImpl<double?>() { _F0 = 0 }, tup));
            Assert.IsFalse(eq.Equals(tup = new TupleImpl<double?>() { _F0 = double.NaN }, tup));
            Assert.IsFalse(eq.Equals(tup = new TupleImpl<double?>() { _F0 = null }, tup));
            Assert.IsFalse(eq.Equals(tup = new TupleImpl<double?>(), tup));
            Assert.IsFalse(eq.Equals(null));
        }
        Assert.IsTrue(TryGetStrictEqCmp(tm, DType.CreateTuple(false, DType.Text), out ec) & ec is not null);
        {
            var eq = ec as EqualityComparer<TupleImpl<string>>;
            Assert.IsNotNull(eq);
            var tup = new TupleImpl<string>() { _F0 = "hello" };
            Assert.IsTrue(eq.Equals(tup, tup));
            Assert.IsTrue(eq.Equals(tup = new TupleImpl<string>() { _F0 = "" }, tup));
            Assert.IsFalse(eq.Equals(tup = new TupleImpl<string>() { _F0 = null }, tup));
            Assert.IsFalse(eq.Equals(tup = new TupleImpl<string>(), tup));
            Assert.IsFalse(eq.Equals(null));
        }
        Assert.IsTrue(TryGetStrictEqCmp(tm, DType.CreateTuple(false, DType.CreateTuple(false, DType.R8Opt)), out ec) & ec is not null);
        {
            var eq = ec as EqualityComparer<TupleImpl<TupleImpl<double?>>>;
            Assert.IsNotNull(eq);
            var tup = new TupleImpl<TupleImpl<double?>>() { _F0 = new TupleImpl<double?>() { _F0 = 0 } };
            Assert.IsTrue(eq.Equals(tup, tup));
            Assert.IsFalse(eq.Equals(tup = new TupleImpl<TupleImpl<double?>>() { _F0 = new TupleImpl<double?>() { _F0 = double.NaN } }, tup));
            Assert.IsFalse(eq.Equals(tup = new TupleImpl<TupleImpl<double?>>() { _F0 = new TupleImpl<double?>() { _F0 = null } }, tup));
            Assert.IsFalse(eq.Equals(tup = new TupleImpl<TupleImpl<double?>>() { _F0 = new TupleImpl<double?>() }, tup));
            Assert.IsFalse(eq.Equals(tup = new TupleImpl<TupleImpl<double?>>() { _F0 = null }, tup));
            Assert.IsFalse(eq.Equals(tup = new TupleImpl<TupleImpl<double?>>(), tup));
            Assert.IsFalse(eq.Equals(tup = null, tup));
        }
    }

    [TestMethod]
    public void TestStrictEqCmp_Record()
    {
        var tm = new TestEnumTypeManager();

        // Record with Text, Uri, and nullable should report null as bad.
        {
            DType typeRec = DType.CreateRecord(true,
                new TypedName("A", DType.Text),
                new TypedName("B", DType.UriGen),
                new TypedName("C", DType.I8Opt));
            Assert.IsTrue(tm.TryEnsureSysType(typeRec, out Type stRec));
            Assert.IsTrue(TryGetStrictEqCmp(tm, typeRec, out var ec) & ec is not null);
            Assert.IsTrue(typeof(IEqualityComparer<>).MakeGenericType(stRec).IsAssignableFrom(ec.GetType()));

            bool good = ec.Equals(null, null);
            Assert.IsFalse(good);

            var fact = tm.CreateRecordFactory(typeRec);
            var setA = fact.GetFieldSetter<string>(new DName("A"));
            var setB = fact.GetFieldSetter<Link>(new DName("B"));
            var setC = fact.GetFieldSetter<long?>(new DName("C"));

            // No nulls.
            var bldr = fact.Create().Open();
            setA(bldr, "hi");
            setB(bldr, Link.CreateGeneric("Blah"));
            setC(bldr, 0);
            var rec = bldr.Close();
            good = ec.Equals(rec, rec);
            Assert.IsTrue(good);

            // No nulls, separate instance.
            bldr = fact.Create().Open();
            setA(bldr, "hi");
            setB(bldr, Link.CreateGeneric("Blah"));
            setC(bldr, 0);
            var rec2 = bldr.Close();
            good = ec.Equals(rec, rec2);
            Assert.IsTrue(good);

            // Set A to null.
            bldr = fact.Create().Open();
            setA(bldr, null);
            setB(bldr, Link.CreateGeneric("Blah"));
            setC(bldr, 3);
            rec = bldr.Close();
            good = ec.Equals(rec, rec);
            Assert.IsFalse(good);

            // Leave A as null.
            bldr = fact.Create().Open();
            setB(bldr, Link.CreateGeneric("Blah"));
            setC(bldr, 3);
            rec = bldr.Close();
            good = ec.Equals(rec, rec);
            Assert.IsFalse(good);

            // Set B to null.
            bldr = fact.Create().Open();
            setA(bldr, "hi");
            setB(bldr, null);
            setC(bldr, 3);
            rec = bldr.Close();
            good = ec.Equals(rec, rec);
            Assert.IsFalse(good);

            // Leave B as null.
            bldr = fact.Create().Open();
            setA(bldr, "hi");
            setC(bldr, 3);
            rec = bldr.Close();
            good = ec.Equals(rec, rec);
            Assert.IsFalse(good);

            // Set C as null.
            bldr = fact.Create().Open();
            setA(bldr, "hi");
            setB(bldr, Link.CreateGeneric("Blah"));
            setC(bldr, null);
            rec = bldr.Close();
            good = ec.Equals(rec, rec);
            Assert.IsFalse(good);

            // Leave C as null.
            bldr = fact.Create().Open();
            setA(bldr, "hi");
            setB(bldr, Link.CreateGeneric("Blah"));
            rec = bldr.Close();
            good = ec.Equals(rec, rec);
            Assert.IsFalse(good);
        }
    }

    [TestMethod]
    public void TupleSizes()
    {
        var tm = new TestEnumTypeManager();

        var bldr = Immutable.Array<DType>.CreateBuilder(TupleBase.ArityMax, init: true);
        for (int i = 0; i < bldr.Count; i++)
            bldr[i] = DType.I4Req;
        var typeTup = DType.CreateTuple(false, bldr.ToImmutableCopy());

        Assert.IsTrue(tm.TryEnsureSysType(typeTup, out var stTup));
        Assert.IsTrue(stTup.IsSubclassOf(typeof(TupleBase)));
        Assert.IsTrue(stTup.IsGenericType);
        var tas = stTup.GetGenericArguments();
        Assert.AreEqual(bldr.Count, tas.Length);

        foreach (var ts in tas)
            Assert.AreEqual(ts, typeof(int));

        bldr.Add(DType.I4Req);
        typeTup = DType.CreateTuple(false, bldr.ToImmutable());
        Assert.IsFalse(tm.TryEnsureSysType(typeTup, out stTup));
        Assert.IsNull(stTup);
    }

    [TestMethod]
    public void RecordSizes()
    {
        var tm = new TestEnumTypeManager();

        var bldr = Immutable.Array<TypedName>.CreateBuilder(RecordBase.ArityMax, init: true);
        for (int i = 0; i < bldr.Count; i++)
            bldr[i] = new TypedName(new DName(i.ToString()), DType.I4Req);
        var typeRec = DType.CreateRecord(false, bldr.ToImmutableCopy());

        Assert.IsTrue(tm.TryEnsureSysType(typeRec, out var stRec));
        Assert.IsTrue(stRec.IsSubclassOf(typeof(RecordBase)));
        Assert.IsTrue(stRec.IsGenericType);
        var tas = stRec.GetGenericArguments();
        Assert.AreEqual(bldr.Count, tas.Length);

        foreach (var ts in tas)
            Assert.AreEqual(ts, typeof(int));

        bldr.Add(new TypedName(new DName("X"), DType.I4Req));
        typeRec = DType.CreateRecord(false, bldr.ToImmutable());
        Assert.IsFalse(tm.TryEnsureSysType(typeRec, out stRec));
        Assert.IsNull(stRec);
    }

    [TestMethod]
    public void TupleDefault()
    {
        var tm = new TestEnumTypeManager();

        var typeTupInner = DType.CreateTuple(false, DType.I4Req, DType.Text);
        var typeTupOuter = DType.CreateTuple(false, DType.R4Req, typeTupInner);

        Assert.IsTrue(tm.TryEnsureDefaultValue(typeTupOuter, out var entry));
        Assert.IsTrue(entry.special);

        var tupOuter = entry.value;
        Assert.IsNotNull(tupOuter);

        var stOuter = tupOuter.GetType();
        Assert.AreEqual(tm.GetSysTypeOrNull(typeTupOuter), stOuter);
        Assert.IsTrue(stOuter.IsGenericType);

        var types = stOuter.GetGenericArguments();
        Assert.AreEqual(2, types.Length);

        Assert.AreEqual(typeof(float), types[0]);
        Assert.AreEqual(default(float), stOuter.GetField("_F0").GetValue(tupOuter));

        var stInner = types[1];
        Assert.AreEqual(tm.GetSysTypeOrNull(typeTupInner), stInner);
        Assert.IsTrue(stInner.IsGenericType);

        types = stInner.GetGenericArguments();
        Assert.AreEqual(2, types.Length);

        var tupInner = stOuter.GetField("_F1").GetValue(tupOuter);
        Assert.IsNotNull(tupInner);
        Assert.AreEqual(stInner, tupInner.GetType());

        Assert.AreEqual(typeof(int), types[0]);
        Assert.AreEqual(default(int), stInner.GetField("_F0").GetValue(tupInner));
        Assert.AreEqual(typeof(string), types[1]);
        Assert.IsNull(stInner.GetField("_F1").GetValue(tupInner));
    }

    [TestMethod]
    public void TensorDefault()
    {
        var tm = new TestEnumTypeManager();
        var typeTens = DType.I8Req.ToTensor(false, 3);

        Assert.IsTrue(tm.TryEnsureDefaultValue(typeTens, out var entry));
        Assert.IsTrue(entry.special);

        Assert.IsTrue(entry.value is Tensor<long>);
        var tens = (Tensor<long>)entry.value;
        Assert.AreEqual(3, tens.Rank);
        Assert.AreEqual(0, tens.Count);
        Assert.AreEqual(3, tens.Shape.Rank);
        for (var i = 0; i < tens.Shape.Rank; i++)
            Assert.AreEqual(0, tens.Shape[i]);
    }
}
