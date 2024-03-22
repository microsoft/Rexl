// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Rexl;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RexlTest;

[TestClass]
public partial class RestrictedArrayTests
{
    // REVIEW: This doesn't fully cover ReadOnly.Array, but should.
    [TestMethod]
    public void ReadOnlyArrayCoverageTest()
    {
        {
            // Default untyped array.
            var xa = default(ReadOnly.Array);
            Assert.IsTrue(xa.IsDefault);
            Assert.IsTrue(xa.IsDefaultOrEmpty);
            Assert.AreEqual(0, xa.Length);

            // Can cast to anything.
            Assert.IsTrue(xa.TryCast<int>(out _));
            Assert.IsTrue(xa.TryCast<string>(out _));

            // Untyped from null - same as default.
            Array raw = null;
            xa = Immutable.Array.Create(raw);
            Assert.IsTrue(xa.IsDefault);
            Assert.IsTrue(xa.IsDefaultOrEmpty);
            Assert.AreEqual(0, xa.Length);
            Assert.AreEqual(xa.Length, xa.Count());
        }

        {
            // Default string array.
            var sa = default(ReadOnly.Array<string>);
            Assert.IsTrue(sa.IsDefault);
            Assert.IsTrue(sa.IsDefaultOrEmpty);
            Assert.AreEqual(0, sa.Length);

            Assert.IsNull(sa.GetItemOrDefault(3));

            // ToArray.
            var a = sa.ToArray();
            Assert.IsNull(a);

            // Can cast to anything.
            Assert.IsTrue(sa.TryCast<int>(out _));
        }

        {
            // Empty string array.
            var raw = new string[0];
            ReadOnly.Array<string> sa = raw;
            Assert.IsFalse(sa.IsDefault);
            Assert.IsTrue(sa.IsDefaultOrEmpty);
            Assert.AreEqual(raw.Length, sa.Length);

            // ToArray.
            var a = sa.ToArray();
            Assert.IsNotNull(a);
            Assert.AreEqual(0, a.Length);

            // Bad cast.
            Assert.IsFalse(sa.TryCast<int>(out _));
        }

        {
            var raw = new string[] { "a", "b", "c" };
            ReadOnly.Array<string> sa = raw;
            Assert.IsFalse(sa.IsDefault);
            Assert.IsFalse(sa.IsDefaultOrEmpty);
            Assert.AreEqual(raw.Length, sa.Length);
            Assert.AreSame(raw[0], sa[0]);
            Assert.AreSame(raw[0], sa.GetItemOrDefault(0));

            // Basics.
            Assert.IsFalse(sa.IsDefault);
            Assert.IsFalse(sa.IsDefaultOrEmpty);
            Assert.AreEqual(raw.Length, sa.Length);
            Assert.AreSame(raw[0], sa[0]);
            Assert.AreEqual(sa.Length, sa.Count());

            // Casting to sub-class.
            Assert.IsTrue(sa.TryCast<object>(out var oa));
            Assert.AreEqual(sa.Length, oa.Length);
            Assert.AreSame(sa[0], oa[0]);

            // Casting back.
            Assert.IsTrue(oa.TryCast<string>(out var tmp));
            Assert.AreEqual(sa.Length, tmp.Length);
            Assert.AreSame(sa[0], tmp[0]);

            // Bad cast.
            Assert.IsFalse(oa.TryCast<int>(out _));

            // ToArray.
            var a = sa.ToArray();
            Assert.AreEqual(sa.Length, a.Length);
            Assert.AreSame(sa[0], a[0]);

            // To untyped.
            ReadOnly.Array xa = sa;
            Assert.IsFalse(xa.IsDefault);
            Assert.IsFalse(xa.IsDefaultOrEmpty);
            Assert.AreEqual(sa.Length, xa.Length);
            Assert.AreSame(sa[0], xa[0]);
            Assert.AreEqual(xa.Length, xa.Count());

            // Span.
            var span = sa.AsSpan();
            Assert.AreEqual(sa.Length, span.Length);
            Assert.AreSame(sa[0], span[0]);

            span = sa.AsSpan(1, 2);
            Assert.AreEqual(2, span.Length);
            Assert.AreSame(sa[1], span[0]);

            span = sa;
            Assert.AreEqual(sa.Length, span.Length);
            Assert.AreSame(sa[0], span[0]);

            // ToImmutable.
            var ia = sa.ToImmutableArray();
            Assert.AreEqual(sa.Length, ia.Length);
            Assert.AreSame(sa[0], ia[0]);
        }
    }

    // REVIEW: This doesn't fully cover Immutable.Array, but should.
    [TestMethod]
    public void ImmutableOnlyArrayCoverageTest()
    {
        {
            // Default untyped array.
            var xa = default(Immutable.Array);
            Assert.IsTrue(xa.IsDefault);
            Assert.IsTrue(xa.IsDefaultOrEmpty);
            Assert.AreEqual(0, xa.Length);

            // Can cast to anything.
            Assert.IsTrue(xa.TryCast<int>(out _));
            Assert.IsTrue(xa.TryCast<string>(out _));

            // Untyped from null - same as default.
            Array raw = null;
            xa = Immutable.Array.Create(raw);
            Assert.IsTrue(xa.IsDefault);
            Assert.IsTrue(xa.IsDefaultOrEmpty);
            Assert.AreEqual(0, xa.Length);
        }

        {
            // Default string array.
            var sa = default(Immutable.Array<string>);
            Assert.IsTrue(sa.IsDefault);
            Assert.IsTrue(sa.IsDefaultOrEmpty);
            Assert.AreEqual(0, sa.Length);

            // ToArray.
            var a = sa.ToArray();
            Assert.IsNull(a);

            // Can cast to anything.
            Assert.IsTrue(sa.TryCast<int>(out _));
        }

        {
            // Empty string array.
            var raw = new string[0];
            var sa = Immutable.Array<string>.Create(raw);

            // Basics.
            Assert.IsFalse(sa.IsDefault);
            Assert.IsTrue(sa.IsDefaultOrEmpty);
            Assert.AreEqual(0, sa.Length);

            // ToArray.
            var a = sa.ToArray();
            Assert.IsNotNull(a);
            Assert.AreEqual(0, a.Length);

            // Bad cast.
            Assert.IsFalse(sa.TryCast<int>(out _));

            // Un-typed
            Immutable.Array xa = sa;
            Assert.AreEqual(sa.Length, xa.Length);
            Assert.AreEqual(xa.Length, xa.Count());

            // Cast back.
            Assert.IsTrue(xa.TryCast<string>(out var sa2));
            Assert.IsTrue(sa.AreIdentical(sa2));

            // Bad cast.
            Assert.IsFalse(xa.TryCast<int>(out _));

            var raw2 = new string[] { "a", "b" };
            sa2 = raw2.ToImmutableArray();
            var oa2 = Immutable.Array<object>.Cast(sa2);
            var oa = Immutable.Array<object>.Cast(sa);

            // AddRange to empty array.
            var sa3 = sa.AddRange(sa2);
            Assert.AreEqual(sa2, sa3);

            // AddRange from empty array.
            sa3 = sa2.AddRange(sa);
            Assert.AreEqual(sa2, sa3);

            // AddRange with cast to empty array.
            var oa3 = oa.AddRange(sa2);
            Assert.IsTrue(oa3.TryCast<string>(out var tmp));
            Assert.IsTrue(sa2.AreIdentical(tmp));

            // AddRange with cast from empty array.
            oa3 = oa2.AddRange(sa);
            Assert.IsTrue(oa2.AreIdentical(oa3));

            // From null source array should produce empty.
            raw = null;
            sa = Immutable.Array<string>.Create(raw);
            Assert.IsFalse(sa.IsDefault);
            Assert.IsTrue(sa.IsDefaultOrEmpty);
            Assert.AreEqual(0, sa.Length);

            // From null List<T>.
            sa = Immutable.Array<string>.Create(default(List<string>));
            Assert.IsFalse(sa.IsDefault);
            Assert.IsTrue(sa.IsDefaultOrEmpty);
            Assert.AreEqual(0, sa.Length);

            // From null IEnumerable<T>.
            sa = Immutable.Array<string>.Create(default(IEnumerable<string>));
            Assert.IsFalse(sa.IsDefault);
            Assert.IsTrue(sa.IsDefaultOrEmpty);
            Assert.AreEqual(0, sa.Length);

            // From empty List<T>.
            sa = Immutable.Array<string>.Create(new List<string>());
            Assert.IsFalse(sa.IsDefault);
            Assert.IsTrue(sa.IsDefaultOrEmpty);
            Assert.AreEqual(0, sa.Length);
        }

        {
            // String array.
            var raw = new string[] { "a", "b", "c" };
            var sa = raw.ToImmutableArray();

            // Basics.
            Assert.IsFalse(sa.IsDefault);
            Assert.IsFalse(sa.IsDefaultOrEmpty);
            Assert.AreEqual(raw.Length, sa.Length);
            Assert.AreSame(raw[0], sa[0]);

            // Casting to sub-class using TryCast.
            Assert.IsTrue(sa.TryCast<object>(out var oa));
            Assert.AreEqual(sa.Length, oa.Length);
            Assert.AreSame(sa[0], oa[0]);

            // Casting to sub-class using Cast.
            oa = Immutable.Array<object>.Cast(sa);
            Assert.AreEqual(sa.Length, oa.Length);
            Assert.AreSame(sa[0], oa[0]);

            // Casting back.
            Assert.IsTrue(oa.TryCast<string>(out var tmp));
            Assert.AreEqual(sa.Length, tmp.Length);
            Assert.AreSame(sa[0], tmp[0]);

            // Bad cast.
            Assert.IsFalse(oa.TryCast<int>(out _));

            // Add.
            var obj = new object();
            var oa2 = oa.Add(obj);
            Assert.AreEqual(oa.Length + 1, oa2.Length);

            // AddRange.
            oa2 = oa2.AddRange(oa);
            Assert.IsTrue(oa2.SequenceEqual(new object[] { "a", "b", "c", obj, "a", "b", "c" }));

            // AddRange with cast.
            oa2 = oa2.AddRange(sa);
            Assert.IsTrue(oa2.SequenceEqual(new object[] { "a", "b", "c", obj, "a", "b", "c", "a", "b", "c" }));

            // ToArray.
            var a = sa.ToArray();
            Assert.AreEqual(sa.Length, a.Length);
            Assert.AreSame(sa[0], a[0]);

            // To ReadOnly.
            ReadOnly.Array<string> rsa = sa;
            Assert.IsFalse(rsa.IsDefault);
            Assert.IsFalse(rsa.IsDefaultOrEmpty);
            Assert.AreEqual(sa.Length, rsa.Length);
            Assert.AreSame(sa[0], rsa[0]);

            ReadOnly.Array rxa = sa;
            Assert.IsFalse(rxa.IsDefault);
            Assert.IsFalse(rxa.IsDefaultOrEmpty);
            Assert.AreEqual(sa.Length, rxa.Length);
            Assert.AreSame(sa[0], rxa[0]);

            // Span.
            var span = sa.AsSpan();
            Assert.AreEqual(sa.Length, span.Length);
            Assert.AreSame(sa[0], span[0]);

            span = sa.AsSpan(1, 2);
            Assert.AreEqual(2, span.Length);
            Assert.AreSame(sa[1], span[0]);

            span = sa;
            Assert.AreEqual(sa.Length, span.Length);
            Assert.AreSame(sa[0], span[0]);

            // Range.
            var sa2 = sa.GetRange(1, 2);
            Assert.AreEqual(2, sa2.Length);
            Assert.AreSame(sa[1], sa2[0]);

            sa2 = sa.GetMinLim(1, 3);
            Assert.AreEqual(2, sa2.Length);
            Assert.AreSame(sa[1], sa2[0]);

            sa2 = sa.GetMinLim(3, 3);
            Assert.AreEqual(0, sa2.Length);

            sa2 = sa.GetMinLim(0, 3);
            Assert.IsTrue(sa.AreIdentical(sa2));
        }

        {
            // Untyped string array.
            Array raw = new string[] { "a", "b", "c" };
            var xa = Immutable.Array.Create(raw);

            // Basics.
            Assert.IsFalse(xa.IsDefault);
            Assert.IsFalse(xa.IsDefaultOrEmpty);
            Assert.AreEqual(raw.Length, xa.Length);
            Assert.AreSame(raw.GetValue(0), xa[0]);
            Assert.AreEqual(xa.Length, xa.Count());

            // Casting back.
            Assert.IsTrue(xa.TryCast<string>(out var sa));
            Assert.AreEqual(raw.Length, sa.Length);
            Assert.AreSame(raw.GetValue(0), sa[0]);

            // To ReadOnly.
            ReadOnly.Array rxs = xa;
            Assert.IsFalse(rxs.IsDefault);
            Assert.IsFalse(rxs.IsDefaultOrEmpty);
            Assert.AreEqual(xa.Length, rxs.Length);
            Assert.AreSame(xa[0], rxs[0]);

            raw = new string[0];
            xa = Immutable.Array.Create(raw);
            Assert.IsFalse(xa.IsDefault);
            Assert.IsTrue(xa.IsDefaultOrEmpty);
            Assert.AreEqual(raw.Length, xa.Length);
        }

        {
            // Object array - created from strings preserves type.
            var raw = new string[] { "a", "b", "c" };
            var oa = Immutable.Array.Create<object>(raw);

            // Basics.
            Assert.IsFalse(oa.IsDefault);
            Assert.IsFalse(oa.IsDefaultOrEmpty);
            Assert.AreEqual(raw.Length, oa.Length);
            Assert.AreSame(raw[0], oa[0]);
            Assert.AreEqual(oa.Length, oa.Count());

            // Casting to string succeeds.
            Assert.IsTrue(oa.TryCast<string>(out var sa));
            Assert.AreEqual(raw.Length, sa.Length);
            Assert.AreSame(raw[0], sa[0]);
        }
    }
}
