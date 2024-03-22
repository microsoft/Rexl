// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;

using Microsoft.Rexl;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RexlTest;

[TestClass]
public sealed class DTypeTests
{
    private static DType VerifySerialize(DType type)
    {
        string str = type.Serialize();
        Assert.IsTrue(DType.TryDeserialize(str, out var type2));
        Assert.IsTrue(type == type2);
        return type;
    }

    /// <summary>
    /// This calls DType.GetSuperType on both (type0, type1) and (type1, type0), and
    /// verifies that the results match, then verifies that the result accepts both 
    /// type0 and type1, then returns the result.
    /// </summary>
    private static DType VerifySuperType(DType type0, DType type1, bool union)
    {
        DType sup0 = DType.GetSuperType(type0, type1, union);
        DType sup1 = DType.GetSuperType(type1, type0, union);
        Assert.IsTrue(sup0 == sup1);
        Assert.IsTrue(sup0.Accepts(type0, union));
        Assert.IsTrue(sup0.Accepts(type1, union));

        DType sup2 = DType.GetSuperType(type0.ToSequence(), type1.ToSequence(), union);
        DType sup3 = DType.GetSuperType(type1.ToSequence(), type0.ToSequence(), union);
        Assert.IsTrue(sup2 == sup3);
        Assert.IsTrue(sup0.ToSequence() == sup2);

        return sup0;
    }

    [TestMethod]
    public void TestNames()
    {
        const string pre = "_X";

        string src;
        DName name;
        bool isMod;

        name = DName.MakeValid(src = " ab c\t", out isMod);
        Assert.AreEqual(src, name.Value);
        Assert.IsTrue(!isMod);
        Assert.IsTrue(DName.IsValidDName(src));

        name = DName.MakeValid(src = " \t", out isMod);
        Assert.AreEqual(pre + src, name.Value);
        Assert.IsTrue(isMod);
        Assert.IsTrue(!DName.IsValidDName(src));

        name = DName.MakeValid(src = "x\ry", out isMod);
        Assert.AreEqual("xy", name.Value);
        Assert.IsTrue(isMod);
        Assert.IsTrue(!DName.IsValidDName(src));

        name = DName.MakeValid(src = "\nxy\r", out isMod);
        Assert.AreEqual("xy", name.Value);
        Assert.IsTrue(isMod);
        Assert.IsTrue(!DName.IsValidDName(src));

        name = DName.MakeValid(src = null, out isMod);
        Assert.AreEqual(pre, name.Value);
        Assert.IsTrue(isMod);
        Assert.IsTrue(!DName.IsValidDName(src));

        name = DName.MakeValid(src = "", out isMod);
        Assert.AreEqual(pre, name.Value);
        Assert.IsTrue(isMod);
        Assert.IsTrue(!DName.IsValidDName(src));
    }

    [TestMethod]
    public void BasicTypes()
    {
        var statics = new DType[] {
            DType.General, DType.Vac, DType.Null,
            DType.EmptyRecordReq, DType.EmptyRecordOpt, DType.EmptyTableReq, DType.EmptyTableOpt,
            DType.Text,
            DType.R8Req, DType.R8Opt, DType.R4Req, DType.R4Opt,
            DType.IAReq, DType.IAOpt,
            DType.I8Req, DType.I8Opt, DType.I4Req, DType.I4Opt, DType.I2Req, DType.I2Opt, DType.I1Req, DType.I1Opt,
            DType.U8Req, DType.U8Opt, DType.U4Req, DType.U4Opt, DType.U2Req, DType.U2Opt, DType.U1Req, DType.U1Opt,
            DType.BitReq, DType.BitOpt,
            DType.DateReq, DType.DateOpt, DType.TimeReq, DType.TimeOpt,
            DType.GuidReq, DType.GuidOpt,
            DType.UriGen,
            DType.CreateUriType(NPath.Root.Append(new DName("Image"))),
            DType.CreateUriType(NPath.Root.Append(new DName("Audio"))),
            DType.CreateTuple(false), DType.CreateTuple(true),
            DType.CreateTuple(false, DType.BitReq), DType.CreateTuple(true, DType.BitReq),
            DType.CreateTuple(false, DType.I4Req, DType.Text), DType.CreateTuple(true, DType.I4Req, DType.Text),
        };
        var kinds = new DKind[] {
            DKind.General, DKind.Vac, DKind.Vac,
            DKind.Record, DKind.Record, DKind.Sequence, DKind.Sequence,
            DKind.Text,
            DKind.R8, DKind.R8, DKind.R4, DKind.R4,
            DKind.IA, DKind.IA,
            DKind.I8, DKind.I8, DKind.I4, DKind.I4, DKind.I2, DKind.I2, DKind.I1, DKind.I1,
            DKind.U8, DKind.U8, DKind.U4, DKind.U4, DKind.U2, DKind.U2, DKind.U1, DKind.U1,
            DKind.Bit, DKind.Bit,
            DKind.Date, DKind.Date, DKind.Time, DKind.Time,
            DKind.Guid, DKind.Guid,
            DKind.Uri,
            DKind.Uri,
            DKind.Uri,
            DKind.Tuple, DKind.Tuple,
            DKind.Tuple, DKind.Tuple,
            DKind.Tuple, DKind.Tuple,
        };
        var opts = new bool[] {
            true, false, true,
            false, true, true, true,
            true,
            false, true, false, true,
            false, true,
            false, true, false, true, false, true, false, true,
            false, true, false, true, false, true, false, true,
            false, true,
            false, true, false, true,
            false, true,
            true,
            true,
            true,
            false, true,
            false, true,
            false, true,
        };
        var sizeNum = new int[] {
            -1, -1, -1,
            -1, -1, -1, -1,
            -1,
            +8, +8, +4, +4,
            1 << 29, 1 << 29,
            +8, +8, +4, +4, +2, +2, +1, +1,
            +8, +8, +4, +4, +2, +2, +1, +1,
            0, 0,
            -1, -1, -1, -1,
            -1, -1,
            -1,
            -1,
            -1,
            -1, -1,
            -1, -1,
            -1, -1,
        };
        var strs = new string[] {
            "g", "v", "o",
            "{}", "{}?", "{}*", "{}?*",
            "s",
            "r8", "r8?", "r4", "r4?",
            "i", "i?",
            "i8", "i8?", "i4", "i4?", "i2", "i2?", "i1", "i1?",
            "u8", "u8?", "u4", "u4?", "u2", "u2?", "u1", "u1?",
            "b", "b?",
            "d", "d?", "t", "t?",
            "G", "G?",
            "U<>",
            "U<Image>",
            "U<Audio>",
            "()", "()?",
            "(b)", "(b)?",
            "(i4, s)", "(i4, s)?",
        };
        Assert.AreEqual(statics.Length, kinds.Length);
        Assert.AreEqual(statics.Length, opts.Length);
        Assert.AreEqual(statics.Length, sizeNum.Length);
        Assert.AreEqual(statics.Length, strs.Length);

        // Kind, IsOpt, NumericSize.
        Assert.IsTrue(default(DType).Kind == 0);
        for (int i = 0; i < statics.Length; i++)
        {
            var type = statics[i];
            Assert.IsTrue(type.Kind == kinds[i]);
            Assert.IsTrue(type.ToSequence().Kind == DKind.Sequence);
            Assert.IsTrue(type.IsOpt == opts[i]);
            Assert.IsTrue(type.ToSequence().IsOpt);
            Assert.IsTrue(type.RootToOpt().IsOpt);
            Assert.IsTrue(type.RootToOpt().IsRootOpt);
            Assert.IsTrue(type.ToSequence().RootToOpt().IsRootOpt);
            Assert.IsTrue(type.ToSequence().RootToOpt() == type.RootToOpt().ToSequence());
            Assert.IsTrue(type.RootToReq().IsRootOpt ==
                (type.RootKind == DKind.General || type.RootKind == DKind.Text || type.RootKind == DKind.Uri));

            int size = type.Kind.NumericSize();
            Assert.AreEqual(sizeNum[i], size);

            // Verify serialization.
            VerifySerialize(type);
            VerifySerialize(type.RootToOpt());
            VerifySerialize(type.ToSequence());
            VerifySerialize(type.RootToOpt().ToSequence());
        }

        // ToString.
        Assert.IsTrue(default(DType).Serialize() == "x");
        for (int i = 0; i < statics.Length; i++)
        {
            Assert.AreEqual(strs[i], statics[i].Serialize());
            Assert.AreEqual(strs[i] + "*", statics[i].ToSequence().Serialize());
        }

        // Equality.
        for (int i = 0; i < statics.Length; i++)
        {
            var a = statics[i];
            for (int j = 0; j < statics.Length; j++)
            {
                var b = statics[j];
                Assert.IsTrue((a == b) == (i == j));
                Assert.IsTrue((a == b) == !(a != b));
                Assert.IsTrue((a == b) == a.Equals(b));
            }
        }

        // RootType and ItemTypeOrThis.
        foreach (var type in statics)
        {
            Assert.IsTrue(type.ToSequence() != type);
            Assert.IsTrue(type.ToSequence(0) == type);
            Assert.IsTrue(type.ToSequence(2) != type);
            Assert.IsTrue(type.ToSequence(2) != type.ToSequence(1));
            Assert.IsTrue(type.ToSequence(2).SeqCount == type.SeqCount + 2);
            Assert.IsTrue(type.ToSequence(50).SeqCount == type.SeqCount + 50);
            Assert.IsTrue(type.ToSequence().RootType == type.RootType);
            Assert.IsTrue(type.ToSequence(2).RootType == type.RootType);
            if (type.Kind != DKind.Sequence)
            {
                Assert.IsTrue(type.SeqCount == 0);
                Assert.IsTrue(type.RootType == type);
                Assert.IsTrue(type.ItemTypeOrThis == type);
            }
            else
            {
                Assert.IsTrue(type.SeqCount == 1);
                Assert.IsTrue(type.RootType != type);
                Assert.IsTrue(type.RootType == type.ItemTypeOrThis);
                Assert.IsTrue(type.RootType.SeqCount == 0);
            }
        }

        // Field count.
        Assert.AreEqual(-1, default(DType).FieldCount);
        foreach (var type in statics)
            Assert.IsTrue(type.FieldCount <= 0);

        // IsValid.
        Assert.IsTrue(!default(DType).IsValid);
        foreach (var type in statics)
        {
            Assert.IsTrue(type.IsValid);
            Assert.IsTrue(type.ToSequence().IsValid);
        }

        // IsSequence.
        foreach (var type in statics)
        {
            Assert.IsTrue(type.IsSequence == (type.Kind == DKind.Sequence));
            Assert.IsTrue(type.ToSequence().IsSequence);
        }

        // IsGeneral.
        foreach (var type in statics)
        {
            Assert.IsTrue(type.IsGeneral == (type.Kind == DKind.General));
            Assert.IsFalse(type.ToSequence().IsGeneral);
        }

        // HasGeneral, HasVac, HasUri, Flags.
        foreach (var type in statics)
        {
            Assert.AreEqual(type.Flags | DTypeFlags.HasSequence | DTypeFlags.HasOpt, type.ToSequence().Flags);

            var t2 = type.ToOpt();
            if (t2 == type)
            {
                Assert.IsTrue(type.IsOpt);
                Assert.IsTrue((type.Flags & DTypeFlags.HasOpt) != 0);
            }
            else
                Assert.AreEqual(type.Flags | DTypeFlags.HasOpt | DTypeFlags.HasRemovableOpt, type.ToOpt().Flags);

            // Note that these aren't universal invariants, but just reflect the types in "statics".
            Assert.AreEqual(type.Kind == DKind.General, type.HasGeneral);
            Assert.AreEqual(type.Kind == DKind.Vac, type.HasVac);
            Assert.AreEqual(type.Kind == DKind.Uri, type.HasUri);
        }

        // IsNull.
        foreach (var type in statics)
        {
            Assert.IsTrue(type.IsNull == (type.Kind == DKind.Vac && type.IsOpt));
            Assert.IsFalse(type.ToSequence().IsNull);
        }

        // IsRecord.
        foreach (var type in statics)
        {
            Assert.IsTrue(type.IsRecordXxx == (type.Kind == DKind.Record));
            Assert.IsTrue(type.IsRecordReq == (type.Kind == DKind.Record && !type.IsOpt));
            Assert.IsTrue(type.IsRecordOpt == (type.Kind == DKind.Record && type.IsOpt));
            Assert.IsFalse(type.ToSequence().IsRecordXxx);
            Assert.IsFalse(type.ToSequence().IsRecordReq);
            Assert.IsFalse(type.ToSequence().IsRecordOpt);
        }

        // IsTable.
        foreach (var type in statics)
        {
            Assert.IsTrue(type.IsTableXxx == (type.Kind == DKind.Sequence));
            Assert.IsTrue(type.IsTableReq == (type.Kind == DKind.Sequence && !type.IsRootOpt));
            Assert.IsTrue(type.IsTableOpt == (type.Kind == DKind.Sequence && type.IsRootOpt));
            Assert.IsTrue(type.ToSequence().IsTableXxx == (type.Kind == DKind.Record));
            Assert.IsTrue(type.ToSequence().IsTableReq == (type.Kind == DKind.Record && !type.IsOpt));
            Assert.IsTrue(type.ToSequence().IsTableOpt == (type.Kind == DKind.Record && type.IsOpt));
            Assert.IsFalse(type.ToSequence().ToSequence().IsTableXxx);
            Assert.IsFalse(type.ToSequence().ToSequence().IsTableReq);
            Assert.IsFalse(type.ToSequence().ToSequence().IsTableOpt);
            Assert.IsFalse(type.RootType.IsTableXxx);
            Assert.IsFalse(type.RootType.IsTableReq);
            Assert.IsFalse(type.RootType.IsTableOpt);
        }

        // IsPrimitive and IsNumeric.
        foreach (var type in statics)
        {
            switch (type.Kind)
            {
            case DKind.General:
            case DKind.Vac:
            case DKind.Record:
            case DKind.Tuple:
            case DKind.Sequence:
            case DKind.Uri:
                Assert.IsFalse(type.IsPrimitiveXxx);
                Assert.IsFalse(type.IsNumericXxx);
                break;

            case DKind.Text:
            case DKind.Date:
            case DKind.Time:
            case DKind.Guid:
                Assert.IsTrue(type.IsPrimitiveXxx);
                Assert.IsFalse(type.IsNumericXxx);
                break;

            case DKind.R8:
            case DKind.R4:
            case DKind.IA:
            case DKind.I8:
            case DKind.I4:
            case DKind.I2:
            case DKind.I1:
            case DKind.U8:
            case DKind.U4:
            case DKind.U2:
            case DKind.U1:
            case DKind.Bit:
                Assert.IsTrue(type.IsPrimitiveXxx);
                Assert.IsTrue(type.IsNumericXxx);
                break;

            default:
                Assert.IsTrue(false);
                break;
            }

            Assert.IsFalse(type.ToSequence().IsPrimitiveXxx);
            Assert.IsFalse(type.ToSequence().IsPrimitiveReq);
            Assert.IsFalse(type.ToSequence().IsPrimitiveOpt);
            Assert.IsFalse(type.ToSequence().IsNumericXxx);
            Assert.IsFalse(type.ToSequence().IsNumericReq);
            Assert.IsFalse(type.ToSequence().IsNumericOpt);
            Assert.IsTrue(type.IsPrimitiveReq == (type.IsPrimitiveXxx && !type.IsOpt));
            Assert.IsTrue(type.IsPrimitiveOpt == (type.IsPrimitiveXxx && type.IsOpt));
            Assert.IsTrue(type.IsNumericReq == (type.IsNumericXxx && !type.IsOpt));
            Assert.IsTrue(type.IsNumericOpt == (type.IsNumericXxx && type.IsOpt));
        }

        foreach (var union in new[] { false, true })
        {
            // Everything accepts itself.
            // A sequence of T accepts T iff T is null-like.
            foreach (var type in statics)
            {
                Assert.IsTrue(type.Accepts(type, union));
                Assert.IsTrue(type.ToSequence().Accepts(type.ToSequence(), union));
                Assert.IsTrue(type.ToSequence().Accepts(type, union) == type.IsVacXxx);
            }

            // S* accepts T* iff S accepts t.
            foreach (var type0 in statics)
            {
                foreach (var type1 in statics)
                    Assert.IsTrue(type0.Accepts(type1, union) == type0.ToSequence().Accepts(type1.ToSequence(), union));
            }

            // T accepts Null iff T.IsOpt.
            foreach (var type in statics)
            {
                Assert.IsTrue(type.Accepts(DType.Null, union) == type.IsOpt);
                Assert.IsTrue(type.ToSequence().Accepts(DType.Null, union));
                Assert.IsTrue(type.Accepts(DType.Null.ToSequence(), union) == (type.IsTableOpt || type.IsGeneral));
            }

            // General accepts everything.
            foreach (var type in statics)
            {
                Assert.IsTrue(DType.General.Accepts(type, union));
                Assert.IsTrue(DType.General.Accepts(type.ToSequence(), union));
                Assert.IsTrue(DType.General.ToSequence().Accepts(type, union) == (type.IsVacXxx || type.IsSequence));
            }

            // Vac accepts only Vac.
            foreach (var type in statics)
            {
                Assert.IsTrue(DType.Vac.Accepts(type, union) == type.IsVac);
                Assert.IsFalse(DType.Vac.Accepts(type.ToSequence(), union));
                Assert.IsTrue(DType.Vac.ToSequence().Accepts(type, union) == type.IsVacXxx);
            }

            // Null accepts only Vac and Null.
            foreach (var type in statics)
            {
                Assert.IsTrue(DType.Null.Accepts(type, union) == type.IsVacXxx);
                Assert.IsFalse(DType.Null.Accepts(type.ToSequence(), union));
                Assert.IsTrue(DType.Null.ToSequence().Accepts(type, union) == type.IsVacXxx);
            }

            // Record accepts only itself and vac.
            foreach (var type in statics)
            {
                Assert.IsTrue(DType.EmptyRecordReq.Accepts(type, union) == (type.IsVac || type.IsRecordReq));
                Assert.IsTrue(DType.EmptyRecordOpt.Accepts(type, union) == (type.IsVacXxx || type.IsRecordXxx));
                Assert.IsFalse(DType.EmptyRecordReq.Accepts(type.ToSequence(), union));
                Assert.IsFalse(DType.EmptyRecordOpt.Accepts(type.ToSequence(), union));
                Assert.IsTrue(DType.EmptyRecordReq.ToSequence().Accepts(type, union) == (type.IsVacXxx || type.IsTableReq));
                Assert.IsTrue(DType.EmptyRecordOpt.ToSequence().Accepts(type, union) == (type.IsVacXxx || type.IsTableXxx));
            }

            // Table accepts only itself and null-like.
            foreach (var type in statics)
            {
                Assert.IsTrue(DType.EmptyTableReq.Accepts(type, union) == (type.IsVacXxx || type.IsTableReq));
                Assert.IsTrue(DType.EmptyTableOpt.Accepts(type, union) == (type.IsVacXxx || type.IsTableXxx));
                Assert.IsTrue(DType.EmptyTableReq.Accepts(type.ToSequence(), union) == (type.IsVac || type.IsRecordReq));
                Assert.IsTrue(DType.EmptyTableOpt.Accepts(type.ToSequence(), union) == (type.IsVacXxx || type.IsRecordXxx));
                Assert.IsTrue(DType.EmptyTableReq.ToSequence().Accepts(type, union) == type.IsVacXxx);
                Assert.IsTrue(DType.EmptyTableOpt.ToSequence().Accepts(type, union) == type.IsVacXxx);
            }

            // These accept only themselves and vac.
            foreach (var left in new[] { DType.BitReq, DType.DateReq, DType.TimeReq, DType.GuidReq })
            {
                foreach (var type in statics)
                {
                    Assert.IsTrue(left.Accepts(type, union) == (type.IsVac || type == left));
                    Assert.IsFalse(left.Accepts(type.ToSequence(), union));
                    Assert.IsTrue(left.ToSequence().Accepts(type, union) == type.IsVacXxx);
                }
            }

            // These accept only themselves and possibly null-like.
            foreach (var left in new[] { DType.Text, DType.BitOpt, DType.DateOpt, DType.TimeOpt, DType.GuidOpt })
            {
                foreach (var type in statics)
                {
                    Assert.IsTrue(left.Accepts(type, union) == (type.IsVacXxx || type.Kind == left.Kind));
                    Assert.IsFalse(left.Accepts(type.ToSequence(), union));
                    Assert.IsTrue(left.ToSequence().Accepts(type, union) == type.IsVacXxx);
                }
            }

            // UriGen accepts any flavor of uri.
            foreach (var type in statics)
            {
                Assert.IsTrue(DType.UriGen.Accepts(type, union) == (type.IsVacXxx || type.Kind == DKind.Uri));
                NPath image = NPath.Root.Append(new DName("Image"));
                Assert.IsTrue(DType.CreateUriType(image).Accepts(type, union) == (type.IsVacXxx || type.Kind == DKind.Uri && type.GetRootUriFlavor() == image));
                NPath video = NPath.Root.Append(new DName("Video"));
                Assert.IsTrue(DType.CreateUriType(video).Accepts(type, union) == type.IsVacXxx);
            }

            // Number accepts numeric types, and optionally null.
            foreach (var type in statics)
            {
                Assert.IsTrue(DType.R8Req.Accepts(type, union) == (type.IsVac || type.IsNumericReq));
                Assert.IsTrue(DType.R8Opt.Accepts(type, union) == (type.IsVacXxx || type.IsNumericXxx));
                Assert.IsFalse(DType.R8Req.Accepts(type.ToSequence(), union));
                Assert.IsFalse(DType.R8Opt.Accepts(type.ToSequence(), union));
                Assert.IsTrue(DType.R8Req.ToSequence().Accepts(type, union) == type.IsVacXxx);
                Assert.IsTrue(DType.R8Opt.ToSequence().Accepts(type, union) == type.IsVacXxx);
            }

            foreach (var type0 in statics)
            {
                // Filter to numeric types.
                var kind0 = type0.Kind;
                int size0 = kind0.NumericSize();
                Assert.IsTrue(size0 >= -1);
                Assert.IsTrue((size0 >= 0) == kind0.IsNumeric());
                if (size0 < 0)
                    continue;

                // Indicates the size limits for when the other type is Fp, Si, and Ui respectively.
                int sizeFp = kind0.IsFractional() ? size0 : 0;
                int sizeSi = kind0.IsFractional() ? int.MaxValue : kind0.IsSignedIntegral() ? size0 : 0;
                int sizeUi = kind0.IsFractional() ? int.MaxValue : kind0.IsSignedIntegral() && size0 < 8 ? size0 / 2 : size0;

                foreach (var type1 in statics)
                {
                    var kind1 = type1.Kind;
                    var size1 = kind1.NumericSize();
                    Assert.IsTrue(size1 >= -1);
                    Assert.IsTrue((size1 >= 0) == kind1.IsNumeric());

                    bool res = type0.Accepts(type1, union);

                    if (!type0.IsOpt && type1.IsOpt)
                        Assert.IsTrue(!res);
                    else if (type1.IsVacXxx)
                        Assert.IsTrue(res);
                    else if (size1 == -1)
                        Assert.IsTrue(!res, "Types: {0}, {1}", type0, type1);
                    else if (kind1.IsFractional())
                        Assert.AreEqual(size1 <= sizeFp, res, "Failed for {0} and {1}", type0, type1);
                    else if (kind1.IsSignedIntegral())
                        Assert.IsTrue(res == (size1 <= sizeSi));
                    else
                    {
                        Assert.IsTrue(kind1.IsUx());
                        Assert.IsTrue(res == (size1 <= sizeUi));
                    }

                    Assert.IsFalse(type0.Accepts(type1.ToSequence(), union));
                    Assert.IsTrue(type0.ToSequence().Accepts(type1, union) == type1.IsVacXxx);
                    Assert.IsTrue(type0.ToSequence().Accepts(type1, union) == type1.IsVacXxx);
                }
            }

            // GetSuperType.
            for (int i = 0; i < statics.Length; i++)
            {
                DType type0 = statics[i];
                DType seq0 = type0.ToSequence();

                for (int j = 0; j < statics.Length; j++)
                {
                    DType type1 = statics[j];
                    DType seq1 = type1.ToSequence();

                    // Using type0/type1.
                    DType sup = VerifySuperType(type0, type1, union);
                    Assert.IsTrue(type0 != type1 || sup == type0);
                    if (type0 == type1)
                        Assert.IsTrue(sup == type0);
                    else if (type0.Accepts(type1, union))
                        Assert.AreEqual(type0, sup);
                    else if (type1.Accepts(type0, union))
                        Assert.IsTrue(sup == type1);
                    else if (!type0.IsOpt && type0.RootToOpt().Accepts(type1, union))
                        Assert.AreEqual(type0.RootToOpt(), sup);
                    else if (!type1.IsOpt && type1.RootToOpt().Accepts(type0, union))
                        Assert.IsTrue(sup == type1.RootToOpt());
                    else if (type0.Kind == DKind.Uri && type1.Kind == DKind.Uri)
                        Assert.IsTrue(sup.Kind == DKind.Uri);
                    else if (!type0.IsNumericXxx || !type1.IsNumericXxx)
                        Assert.IsTrue(sup.IsGeneral);
                    else
                    {
                        // Numeric types. Complex rules.
                        var kindA = type0.RootKind;
                        var kindB = type1.RootKind;
                        var kindSup = sup.RootKind;
                        if (kindA > kindB)
                        {
                            var tmp = kindA;
                            kindA = kindB;
                            kindB = tmp;
                        }
                        int sizeA = kindA.NumericSize();
                        int sizeB = kindB.NumericSize();
                        int sizeSup = kindSup.NumericSize();
                        int sizeMax = Math.Max(sizeA, sizeB);
                        Assert.IsTrue(sizeSup > sizeA || sizeSup == 8 && sizeA == 8);
                        Assert.IsTrue(sizeSup > sizeB || sizeSup == 8 && sizeB == 8);
                        if (kindA.IsFractional())
                        {
                            Assert.IsTrue(kindB.IsIntegral());
                            Assert.IsTrue(kindSup.IsFractional());
                            Assert.IsTrue(sizeSup == sizeMax << 1 || sizeMax == 8);
                        }
                        else
                        {
                            Assert.IsTrue(kindA.IsSignedIntegral());
                            Assert.IsTrue(kindB.IsUx());
                            Assert.IsTrue(kindSup.IsSignedIntegral());
                            Assert.IsTrue(sizeSup == sizeMax << 1 || sizeMax == 8);
                        }
                    }

                    // Using seq0, seq1.
                    // Note that VerifySuperType(S, T) checks that Super(S*, T*) == Super(S, T)*. Calling
                    // VerifySuperType(S*, T*) then verifies that Super(S**, T**) == Super(S*, T*)*. Thus, this
                    // test is mostly redundant, but doesn't hurt!
                    DType supSeq = VerifySuperType(seq0, seq1, union);
                    Assert.IsTrue(supSeq == sup.ToSequence());

                    // Using seq0/type1. No need to do type0, seq1, since that is just a permutation of this.
                    sup = VerifySuperType(seq0, type1, union);
                    if (type1.IsVacXxx)
                        Assert.IsTrue(sup == seq0);
                    else if (!type1.IsSequence)
                        Assert.IsTrue(sup.IsGeneral);
                    else if (type0.IsVac)
                        Assert.IsTrue(sup == type1);
                    else if (type0.IsNull)
                        Assert.IsTrue(sup == type1.RootToOpt());
                    else if (type0.IsRecordReq)
                        Assert.IsTrue(sup == type1);
                    else if (type0.IsRecordOpt)
                        Assert.IsTrue(sup == type1.RootToOpt());
                    else
                        Assert.IsTrue(sup.SeqCount == 1 & sup.RootType.IsGeneral);
                }
            }
        }
    }

    [TestMethod]
    public void PathType()
    {
        DType type;
        var A = new DName("A");
        var B = new DName("B");
        var C = new DName("C");
        var D = new DName("D");
        var E = new DName("E");

        var n = DType.R8Req;
        var b = DType.BitReq;
        var s = DType.Text;

        // Create a record.
        DType rec1 = VerifySerialize(
            DType.CreateRecord(
                false,
                new TypedName(A, n),
                new TypedName(B, b),
                new TypedName(C, s)));
        Assert.IsTrue(rec1.Kind == DKind.Record);
        Assert.IsTrue(rec1.IsRecordXxx);

        // Create a table from the record.
        DType tbl1 = rec1.ToSequence();
        Assert.IsTrue(tbl1.Kind == DKind.Sequence);
        Assert.IsTrue(tbl1.IsTableXxx);

        // Verify some ToString() functionality.
        Assert.AreEqual("{A:r8, B:b, C:s}", rec1.Serialize());
        Assert.AreEqual("{A:r8, B:b, C:s}*", tbl1.Serialize());

        Assert.IsTrue(rec1.TryGetNameType(B, out type) & type == b);
        Assert.IsTrue(tbl1.TryGetNameType(B, out type) & type == b);

        DType rec2 = rec1.SetNameType(D, rec1);
        Assert.AreEqual("{A:r8, B:b, C:s, D:{A:r8, B:b, C:s}}", rec2.Serialize());
        VerifySerialize(rec2);
        DType tbl2 = tbl1.SetNameType(D, rec1);
        Assert.AreEqual("{A:r8, B:b, C:s, D:{A:r8, B:b, C:s}}*", tbl2.Serialize());
        Assert.AreEqual(rec2.ToSequence(), tbl2);
        VerifySerialize(tbl2);

        DType rec3 = rec2.SetNameType(E, rec1.ToSequence());
        Assert.AreEqual("{A:r8, B:b, C:s, D:{A:r8, B:b, C:s}, E:{A:r8, B:b, C:s}*}", rec3.Serialize());
        VerifySerialize(rec3);
        DType tbl3 = tbl2.SetNameType(E, rec1.ToSequence());
        Assert.AreEqual("{A:r8, B:b, C:s, D:{A:r8, B:b, C:s}, E:{A:r8, B:b, C:s}*}*", tbl3.Serialize());
        Assert.AreEqual(rec3.ToSequence(), tbl3);
        VerifySerialize(tbl3);

        DType rec4 = rec3.SetNameType(E, rec3.GetNameTypeOrDefault(E).SetNameType(D, b));
        Assert.AreEqual("{A:r8, B:b, C:s, D:{A:r8, B:b, C:s}, E:{A:r8, B:b, C:s, D:b}*}", rec4.Serialize());
        VerifySerialize(rec4);
        DType tbl4 = tbl3.SetNameType(E, tbl3.GetNameTypeOrDefault(E).SetNameType(D, b));
        Assert.AreEqual("{A:r8, B:b, C:s, D:{A:r8, B:b, C:s}, E:{A:r8, B:b, C:s, D:b}*}*", tbl4.Serialize());
        Assert.AreEqual(rec4.ToSequence(), tbl4);
        VerifySerialize(tbl4);

        // Valid path.
        DType rec5 = rec4.SetNameType(D, rec4.GetNameTypeOrDefault(D).SetNameType(D, s));
        Assert.AreEqual("{A:r8, B:b, C:s, D:{A:r8, B:b, C:s, D:s}, E:{A:r8, B:b, C:s, D:b}*}", rec5.Serialize());
        DType tbl5 = tbl4.SetNameType(D, tbl4.GetNameTypeOrDefault(D).SetNameType(D, s));
        Assert.AreEqual("{A:r8, B:b, C:s, D:{A:r8, B:b, C:s, D:s}, E:{A:r8, B:b, C:s, D:b}*}*", tbl5.Serialize());
        Assert.AreEqual(rec5.ToSequence(), tbl5);
        VerifySerialize(tbl5);

        // Add another star to E.
        Assert.IsTrue(tbl5.TryGetNameType(E, out type));
        tbl5 = tbl5.SetNameType(E, type.ToSequence());
        Assert.AreEqual("{A:r8, B:b, C:s, D:{A:r8, B:b, C:s, D:s}, E:{A:r8, B:b, C:s, D:b}**}*", tbl5.Serialize());
        VerifySerialize(tbl5);
        // Now fetch and verify.
        Assert.IsTrue(tbl5.TryGetNameType(E, out var typeTmp));
        Assert.IsTrue(typeTmp == type.ToSequence());
    }

    [TestMethod]
    public void BasicRecord()
    {
        DType type;
        DType typeDefault = default;
        var A = new DName("A");
        var B = new DName("B");
        var C = new DName("C");
        var D = new DName("D");
        var E = new DName("E");

        var bools = new[] { false, true };
        foreach (var union in bools)
        {
            foreach (var opt in bools)
            {
                var suf = opt ? "?" : "";
                // This is for the opposite nullability.
                var suf2 = opt ? "" : "?";

                foreach (var optN in bools)
                {
                    var n = optN ? DType.R8Opt : DType.R8Req;
                    var sufN = optN ? "?" : "";

                    foreach (var optB in bools)
                    {
                        var b = optB ? DType.BitOpt : DType.BitReq;
                        var sufB = optB ? "?" : "";

                        // This has the opposite nullability from b.
                        var b2 = optB ? DType.BitReq : DType.BitOpt;
                        var sufB2 = optB ? "" : "?";

                        // Create a record.
                        DType rec1 = DType.CreateRecord(
                            opt,
                            new TypedName(B, b),
                            new TypedName(A, n),
                            new TypedName(C, DType.Text));
                        Assert.IsTrue(rec1.Kind == DKind.Record);
                        Assert.IsTrue(rec1.IsOpt == opt);
                        Assert.IsTrue(rec1.IsRecordXxx);
                        Assert.IsTrue(rec1.IsRecordReq == !opt);
                        Assert.IsTrue(rec1.IsRecordOpt == opt);
                        Assert.IsFalse(rec1.IsTableXxx);
                        Assert.IsFalse(rec1.IsTableReq);
                        Assert.IsFalse(rec1.IsTableOpt);
                        Assert.IsTrue(rec1.Equals(rec1));
                        Assert.IsTrue(rec1.Equals(rec1.DropName(E)));

                        var recTmp = DType.CreateRecord(opt, new[] { B }.Select(x => new TypedName(x, b)))
                            .AddNameType(C, DType.Text)
                            .AddNameType(A, n);
                        Assert.IsTrue(rec1.Equals(recTmp));
                        recTmp = DType.CreateRecord(
                            opt,
                            new[] { C, B, A, B },
                            new[] { DType.Text, DType.BitOpt, n, b });
                        Assert.IsTrue(rec1.Equals(recTmp));
                        VerifySerialize(rec1);

                        // Create a table from the record.
                        DType tbl1 = rec1.ToSequence();
                        Assert.IsTrue(tbl1.Kind == DKind.Sequence);
                        Assert.IsTrue(tbl1.IsTableXxx);
                        Assert.IsTrue(tbl1.IsTableReq == !opt);
                        Assert.IsTrue(tbl1.IsTableOpt == opt);
                        Assert.IsFalse(tbl1.IsRecordXxx);
                        Assert.IsTrue(tbl1.Equals(tbl1));
                        Assert.IsTrue(rec1 == tbl1.RootType);
                        Assert.IsTrue(rec1.ToSequence() == tbl1);
                        Assert.IsTrue(rec1 == tbl1.ToSequence().RootType);
                        VerifySerialize(tbl1);

                        // The record and table are not the same.
                        Assert.IsTrue(rec1 != tbl1);
                        Assert.IsTrue(tbl1 != rec1);
                        Assert.IsFalse(rec1 == tbl1);
                        Assert.IsFalse(tbl1 == rec1);

                        // The table should be equivalent to building it from scratch.
                        Assert.IsTrue(tbl1 ==
                            DType.CreateTable(
                                opt,
                                new TypedName(A, n),
                                new TypedName(B, b),
                                new TypedName(C, DType.Text)));
                        Assert.IsTrue(tbl1.Equals(
                            DType.CreateTable(opt, new[] { B }.Select(x => new TypedName(x, b)))
                                .AddNameType(C, DType.Text)
                                .AddNameType(A, n)));

                        // Create record / table with opposite nullability for the record.
                        DType rec2 = opt ? rec1.RootToReq() : rec1.RootToOpt();
                        Assert.IsTrue(rec2.Kind == DKind.Record);
                        Assert.IsTrue(rec2.IsOpt == !opt);
                        Assert.IsTrue(rec2.IsRecordXxx);
                        Assert.IsTrue(rec2.IsRecordReq == opt);
                        Assert.IsTrue(rec2.IsRecordOpt == !opt);
                        Assert.IsFalse(rec2.IsTableXxx);
                        Assert.IsFalse(rec2.IsTableReq);
                        Assert.IsFalse(rec2.IsTableOpt);
                        Assert.IsTrue(rec2.Equals(rec2));
                        Assert.IsTrue(rec2 != rec1);
                        VerifySerialize(rec2);

                        DType tbl2 = opt ? tbl1.RootToReq() : tbl1.RootToOpt();
                        Assert.IsTrue(tbl2.IsTableXxx);
                        Assert.IsTrue(tbl2.IsTableReq == opt);
                        Assert.IsTrue(tbl2.IsTableOpt == !opt);
                        Assert.IsFalse(tbl2.IsRecordXxx);
                        Assert.IsTrue(tbl2.Equals(tbl2));
                        Assert.IsTrue(rec2 == tbl2.RootType);
                        Assert.IsTrue(rec2.ToSequence() == tbl2);
                        Assert.IsTrue(rec2 == tbl2.ToSequence().RootType);
                        Assert.IsTrue(tbl2 != tbl1);
                        VerifySerialize(tbl2);

                        // Create record / table with opposite nullability for B.
                        DType rec3 = rec1.SetNameType(B, b2);
                        Assert.IsTrue(rec3.Kind == DKind.Record);
                        Assert.IsTrue(rec3.IsOpt == opt);
                        Assert.IsTrue(rec3.IsRecordXxx);
                        Assert.IsTrue(rec3.IsRecordReq == !opt);
                        Assert.IsTrue(rec3.IsRecordOpt == opt);
                        Assert.IsFalse(rec3.IsTableXxx);
                        Assert.IsFalse(rec3.IsTableReq);
                        Assert.IsFalse(rec3.IsTableOpt);
                        Assert.IsTrue(rec3.Equals(rec3));
                        Assert.IsTrue(rec3 != rec1);
                        VerifySerialize(rec3);

                        DType tbl3 = tbl1.SetNameType(B, b2);
                        Assert.IsTrue(tbl3.IsTableXxx);
                        Assert.IsTrue(tbl3.IsTableReq == !opt);
                        Assert.IsTrue(tbl3.IsTableOpt == opt);
                        Assert.IsFalse(tbl3.IsRecordXxx);
                        Assert.IsTrue(tbl3.Equals(tbl3));
                        Assert.IsTrue(rec3 == tbl3.RootType);
                        Assert.IsTrue(rec3.ToSequence() == tbl3);
                        Assert.IsTrue(rec3 == tbl3.ToSequence().RootType);
                        Assert.IsTrue(tbl3 != tbl1);
                        VerifySerialize(tbl3);

                        // Verify some ToString() functionality.
                        Assert.AreEqual(string.Format("{{A:r8{1}, B:b{2}, C:s}}{0}", suf, sufN, sufB), rec1.Serialize());
                        Assert.AreEqual(string.Format("{{A:r8{1}, B:b{2}, C:s}}{0}", suf2, sufN, sufB), rec2.Serialize());
                        Assert.AreEqual(string.Format("{{A:r8{1}, B:b{2}, C:s}}{0}", suf, sufN, sufB2), rec3.Serialize());
                        Assert.AreEqual(string.Format("{{A:r8{1}, B:b{2}, C:s}}{0}*", suf, sufN, sufB), tbl1.Serialize());
                        Assert.AreEqual(string.Format("{{A:r8{1}, B:b{2}, C:s}}{0}*", suf2, sufN, sufB), tbl2.Serialize());
                        Assert.AreEqual(string.Format("{{A:r8{1}, B:b{2}, C:s}}{0}*", suf, sufN, sufB2), tbl3.Serialize());
                        Assert.AreEqual(string.Format("{{A:r8{1}, B:b{2}, C:s}}{0}**", suf, sufN, sufB), tbl1.ToSequence().Serialize());
                        Assert.AreEqual(string.Format("{{A:r8{1}, B:b{2}, C:s}}{0}**", suf2, sufN, sufB), tbl2.ToSequence().Serialize());
                        Assert.AreEqual(string.Format("{{A:r8{1}, B:b{2}, C:s}}{0}**", suf, sufN, sufB2), tbl3.ToSequence().Serialize());

                        // Verify some field information.
                        Assert.IsTrue(rec1.FieldCount == 3);
                        Assert.IsTrue(tbl1.FieldCount == 3);
                        Assert.IsTrue(rec2.FieldCount == 3);
                        Assert.IsTrue(tbl2.FieldCount == 3);
                        Assert.IsTrue(rec3.FieldCount == 3);
                        Assert.IsTrue(tbl3.FieldCount == 3);

                        Assert.IsTrue(rec1.GetNames().Count() == 3);
                        Assert.IsTrue(tbl1.GetNames().Count() == 3);

                        Assert.IsTrue(rec1.Contains(A));
                        Assert.IsTrue(tbl1.Contains(A));
                        Assert.IsFalse(DType.General.Contains(A));
                        Assert.IsFalse(DType.R8Req.Contains(A));

                        Assert.IsTrue(rec1.TryGetNameType(A, out type) & type == n);
                        Assert.IsTrue(tbl1.TryGetNameType(A, out type) & type == n);

                        Assert.IsTrue(rec1.TryGetNameType(B, out type) & type == b);
                        Assert.IsTrue(tbl1.TryGetNameType(B, out type) & type == b);
                        Assert.IsTrue(rec3.TryGetNameType(B, out type) & type == b2);
                        Assert.IsTrue(tbl3.TryGetNameType(B, out type) & type == b2);

                        Assert.IsTrue(!rec1.TryGetNameType(D, out type) & type == typeDefault);
                        Assert.IsTrue(!tbl1.TryGetNameType(D, out type) & type == typeDefault);

                        Assert.IsTrue(!rec1.TryGetNameType(default, out type) & type == typeDefault);
                        Assert.IsTrue(!tbl1.TryGetNameType(default, out type) & type == typeDefault);

                        // Acceptance and GetSuperType.
                        Assert.IsTrue(rec1.Accepts(rec2, union) == opt);
                        Assert.IsTrue(tbl1.Accepts(tbl2, union) == opt);
                        Assert.IsTrue(rec2.Accepts(rec1, union) == !opt);
                        Assert.IsTrue(tbl2.Accepts(tbl1, union) == !opt);
                        DType recSup = VerifySuperType(rec1, rec2, union);
                        Assert.IsTrue(recSup == (opt ? rec1 : rec2));
                        DType tblSup = VerifySuperType(tbl1, tbl2, union);
                        Assert.IsTrue(tblSup == (opt ? tbl1 : tbl2));

                        // Acceptance and GetSuperType.
                        Assert.IsTrue(rec1.Accepts(rec3, union) == optB);
                        Assert.IsTrue(tbl1.Accepts(tbl3, union) == optB);
                        Assert.IsTrue(rec3.Accepts(rec1, union) == !optB);
                        Assert.IsTrue(tbl3.Accepts(tbl1, union) == !optB);
                        recSup = VerifySuperType(rec1, rec3, union);
                        Assert.IsTrue(recSup == (optB ? rec1 : rec3));
                        tblSup = VerifySuperType(tbl1, tbl3, union);
                        Assert.IsTrue(tblSup == (optB ? tbl1 : tbl3));

                        foreach (var optD in bools)
                        {
                            var d = optD ? DType.IAOpt : DType.IAReq;
                            var sufD = optD ? "?" : "";

                            DType rec4 = rec1.AddNameType(D, d);
                            Assert.AreEqual(string.Format("{{A:r8{1}, B:b{2}, C:s, D:i{3}}}{0}", suf, sufN, sufB, sufD), rec4.Serialize());
                            Assert.AreEqual(!union, rec1.Accepts(rec4, union));
                            Assert.AreEqual(union && optD, rec4.Accepts(rec1, union));
                            Assert.IsTrue(!rec4.HasGeneral);
                            VerifySerialize(rec4);

                            DType rec5 = rec1.AddNameType(D, n);
                            Assert.AreEqual(string.Format("{{A:r8{1}, B:b{2}, C:s, D:r8{3}}}{0}", suf, sufN, sufB, sufN), rec5.Serialize());
                            Assert.IsTrue(rec5.Accepts(rec4, union) == (n.IsOpt || !d.IsOpt));
                            Assert.IsFalse(rec4.Accepts(rec5, union));
                            Assert.IsTrue(!rec5.HasGeneral);
                            DType typeSup = VerifySuperType(rec4, rec5, union);
                            Assert.AreEqual(string.Format("{{A:r8{1}, B:b{2}, C:s, D:r8{3}}}{0}", suf, sufN, sufB, optN || optD ? "?" : ""), typeSup.Serialize());
                            VerifySerialize(rec5);

                            DType rec6 = rec4.AddNameType(E, DType.General);
                            Assert.AreEqual(string.Format("{{A:r8{1}, B:b{2}, C:s, D:i{3}, E:g}}{0}", suf, sufN, sufB, sufD), rec6.Serialize());
                            Assert.AreEqual(!union, rec1.Accepts(rec6, union));
                            Assert.AreEqual(union && optD, rec6.Accepts(rec1, union));
                            Assert.AreEqual(!union, rec4.Accepts(rec6, union));
                            Assert.AreEqual(union, rec6.Accepts(rec4, union));
                            Assert.IsTrue(rec6.HasGeneral);
                            Assert.IsTrue(rec6.ToSequence().HasGeneral);
                            VerifySerialize(rec6);
                        }
                    }
                }
            }
        }
    }

    [TestMethod]
    public void CompoundRecord()
    {
        var A = new DName("A");
        var B = new DName("B");
        var C = new DName("C");

        var bools = new[] { false, true };
        foreach (var opt in bools)
        {
            var close = opt ? "}?" : "}";
            var mark = "}";

            DType recBase = opt ? DType.EmptyRecordOpt : DType.EmptyRecordReq;
            DType tblBase = recBase.ToSequence();

            DType rec = recBase
                .Add(new TypedName(A, DType.R8Req))
                .SetNameType(B, DType.BitReq)
                .SetNameType(C, recBase.SetNameType(A, DType.Text).AddNameType(B, DType.BitReq));
            Assert.AreEqual("{A:r8, B:b, C:{A:s, B:b}}".Replace(mark, close), rec.Serialize());
            VerifySerialize(rec);

            DType tbl = rec
                .SetNameType(C, rec.GetNameTypeOrDefault(C).AddNameType(C, tblBase)).ToSequence();
            Assert.AreEqual("{A:r8, B:b, C:{A:s, B:b, C:{}*}}*".Replace(mark, close), tbl.Serialize());
            VerifySerialize(tbl);
        }
    }

    [TestMethod]
    public void OddFieldNames()
    {
        var A = new DName("  A ");
        var B = new DName("min");
        var C = new DName("C'DE'F'");

        var bools = new[] { false, true };
        foreach (var opt in bools)
        {
            var close = opt ? "}?" : "}";
            var mark = "}";

            DType recBase = opt ? DType.EmptyRecordOpt : DType.EmptyRecordReq;
            DType tblBase = recBase.ToSequence();

            DType rec = recBase
                .Add(new TypedName(A, DType.R8Req))
                .SetNameType(B, DType.BitReq)
                .SetNameType(C, recBase.SetNameType(A, DType.Text).AddNameType(B, DType.BitReq));
            Assert.AreEqual("{'  A ':r8, 'C''DE''F''':{'  A ':s, min:b}, min:b}".Replace(mark, close), rec.Serialize());
            VerifySerialize(rec);

            DType tbl = rec
                .SetNameType(C, rec.GetNameTypeOrDefault(C).AddNameType(C, tblBase)).ToSequence();
            Assert.AreEqual("{'  A ':r8, 'C''DE''F''':{'  A ':s, 'C''DE''F''':{}*, min:b}, min:b}*".Replace(mark, close), tbl.Serialize());
            VerifySerialize(tbl);
        }
    }

    [TestMethod]
    public void BasicTensor()
    {
        var bools = new[] { false, true };
        foreach (var union in bools)
        {
            foreach (var opt in bools)
            {
                var close = opt ? "]?" : "]";
                var mark = "]";

                DType type1 = DType.DateReq.ToTensor(opt, 0);
                Assert.AreEqual("d[]".Replace(mark, close), type1.Serialize());
                Assert.IsTrue(type1.Equals(type1));
                Assert.IsTrue(type1 == DType.DateReq.ToTensor(opt, 0));
                Assert.IsFalse(type1 != DType.DateReq.ToTensor(opt, 0));
                Assert.IsTrue(type1.Accepts(type1, union));
                Assert.IsTrue(type1.Accepts(DType.DateReq.ToTensor(opt, 0), union));
                // REVIEW: Should a rank zero tensor accept a scalar and vice-versa?
                Assert.IsFalse(DType.DateReq.Accepts(type1, union));
                Assert.IsFalse(type1.Accepts(DType.DateReq, union));
                Assert.IsFalse(type1.HasGeneral);
                VerifySerialize(type1);

                // Test nullable tensor against non-nullable.
                Assert.IsTrue(type1.Accepts(DType.Null, union) == opt);
                DType tmp = opt ? type1.RootToReq() : type1.RootToOpt();
                Assert.IsTrue(tmp != type1);
                Assert.IsTrue(type1.Accepts(tmp, union) == opt);
                Assert.IsTrue(tmp.Accepts(type1, union) == !opt);
                VerifySerialize(tmp);

                // Test nullable item type against non-nullable.
                tmp = DType.DateOpt.ToTensor(opt, 0);
                Assert.IsTrue(tmp != type1);
                Assert.IsFalse(type1.Accepts(tmp, union));
                Assert.IsTrue(tmp.Accepts(type1, union));
                VerifySerialize(tmp);

                DType type2 = DType.R8Req.ToTensor(opt, 0);
                Assert.AreEqual("r8[]".Replace(mark, close), type2.Serialize());
                Assert.IsTrue(type2.Equals(type2));
                Assert.IsFalse(type2.Equals(type1));
                Assert.IsFalse(type2 == type1);
                Assert.IsFalse(type1 == type2);
                Assert.IsTrue(type2 != type1);
                Assert.IsTrue(type1 != type2);
                Assert.IsTrue(type2.Accepts(type2, union));
                Assert.IsFalse(type1.Accepts(type2, union));
                Assert.IsFalse(type2.Accepts(type1, union));
                VerifySerialize(type2);

                // Make type3 a rank-3 tensor.
                DType type3 = DType.R8Req.ToTensor(opt, 3);
                Assert.AreEqual("r8[*,*,*]".Replace(mark, close), type3.Serialize());
                Assert.IsTrue(type3.Equals(type3));
                Assert.IsFalse(type3 == type2);
                Assert.IsFalse(type2 == type3);
                Assert.IsFalse(type3.Accepts(type2, union));
                Assert.IsFalse(type2.Accepts(type3, union));
                VerifySerialize(type3);

                // Make type4 a rank-3 tensor with u1 item type.
                DType type4 = DType.U1Req.ToTensor(opt, 3);
                Assert.AreEqual("u1[*,*,*]".Replace(mark, close), type4.Serialize());
                Assert.IsTrue(type3.Accepts(type4, union));
                Assert.IsFalse(type4.Accepts(type3, union));
                VerifySerialize(type4);

                // Now "lift" to be sequences.
                DType type3s = type3.ToSequence();
                Assert.IsTrue(type3s.Equals(type3s));
                Assert.IsFalse(type3.Equals(type3s));
                Assert.IsFalse(type3s.Equals(type3));
                Assert.IsFalse(type3.Accepts(type3s, union));
                Assert.IsFalse(type3s.Accepts(type3, union));
                VerifySerialize(type3s);

                DType type4s = type4.ToSequence();
                Assert.IsTrue(type3s.Accepts(type4s, union));
                Assert.IsFalse(type4s.Accepts(type3s, union));
                VerifySerialize(type4s);

                // Esoteric.
                Assert.IsTrue(VerifySerialize(DType.General.ToTensor(opt, 1)).HasGeneral);
                Assert.IsTrue(VerifySerialize(DType.EmptyRecordReq.AddNameType(new DName("A"), DType.General).ToSequence().ToTensor(opt, 1)).HasGeneral);
                Assert.IsTrue(VerifySerialize(DType.EmptyRecordReq.AddNameType(new DName("A"), DType.General).ToSequence().ToTensor(opt, 1).ToSequence()).HasGeneral);
                Assert.IsTrue(!VerifySerialize(DType.EmptyRecordReq.AddNameType(new DName("A"), DType.R8Req).ToSequence().ToTensor(opt, 1)).HasGeneral);
                Assert.IsTrue(!VerifySerialize(DType.EmptyRecordReq.AddNameType(new DName("A"), DType.R8Req).ToSequence().ToTensor(opt, 1).ToSequence()).HasGeneral);

                // Tensor of Tensor.
                DType type5 = type3.ToTensor(opt, 2);
                Assert.AreEqual("r8[*,*,*][*,*]".Replace(mark, close), type5.Serialize());
                VerifySerialize(type5);
            }
        }
    }

    private static void DoSuper(bool union, DType type0, DType type1, string res)
    {
        DType sup = VerifySuperType(type0, type1, union);
        var tmp = sup.Serialize();
        if (res != tmp)
            Assert.AreEqual(res, tmp);
        VerifySerialize(type0);
        VerifySerialize(type1);
    }

    [TestMethod]
    public void TestSuperType()
    {
        var bools = new[] { false, true };
        foreach (var union in bools)
        {
            foreach (var opt0 in bools)
            {
                foreach (var opt1 in bools)
                {
                    var close = opt0 | opt1 ? "}?" : "}";
                    var mark = "|";

                    DoSuper(union,
                        DType.CreateRecord(opt0, new TypedName("A", DType.R8Req)),
                        DType.CreateRecord(opt1, new TypedName("A", DType.R8Req)),
                        "{A:r8|".Replace(mark, close));

                    DoSuper(union,
                        DType.CreateRecord(opt0, new TypedName("A", DType.R8Req)),
                        DType.CreateRecord(opt1, new TypedName("A", DType.R8Opt)),
                        "{A:r8?|".Replace(mark, close));

                    DoSuper(union,
                        DType.CreateRecord(opt0, new TypedName("A", DType.R8Opt)),
                        DType.CreateRecord(opt1, new TypedName("A", DType.R8Opt)),
                        "{A:r8?|".Replace(mark, close));

                    DoSuper(union,
                        DType.CreateRecord(opt0, new TypedName("A", DType.R8Req)),
                        DType.CreateTable(opt1, new TypedName("A", DType.R8Req)),
                        "g");

                    DoSuper(union,
                        DType.CreateRecord(opt0, new TypedName("A", DType.R8Req)).ToSequence(),
                        DType.CreateTable(opt1, new TypedName("A", DType.R8Req)).ToSequence(),
                        "g*");

                    DoSuper(union,
                        DType.CreateRecord(opt0, new TypedName("A", DType.R8Opt)),
                        DType.CreateRecord(opt1, new TypedName("A", DType.Null)),
                        "{A:r8?|".Replace(mark, close));

                    DoSuper(union,
                        DType.CreateRecord(opt0, new TypedName("A", DType.Null)),
                        DType.CreateRecord(opt1, new TypedName("A", DType.R8Req)),
                        "{A:r8?|".Replace(mark, close));

                    // String is nullable.
                    DoSuper(union,
                        DType.CreateRecord(opt0, new TypedName("A", DType.Text)),
                        DType.CreateRecord(opt1, new TypedName("A", DType.Null)),
                        "{A:s|".Replace(mark, close));

                    DoSuper(union,
                        DType.CreateRecord(opt0, new TypedName("A", DType.Null)),
                        DType.CreateRecord(opt1, new TypedName("A", DType.Text)),
                        "{A:s|".Replace(mark, close));

                    // Mismatch becomes general.
                    DoSuper(union,
                        DType.CreateRecord(opt0, new TypedName("A", DType.Text)),
                        DType.CreateRecord(opt1, new TypedName("A", DType.R8Opt)),
                        "{A:g|".Replace(mark, close));

                    DoSuper(union,
                        DType.CreateRecord(opt0, new TypedName("A", DType.Text)),
                        DType.CreateRecord(opt1, new TypedName("A", DType.EmptyRecordReq)),
                        "{A:g|".Replace(mark, close));

                    DoSuper(union,
                        DType.CreateRecord(opt0, new TypedName("A", DType.General)),
                        DType.CreateRecord(opt1, new TypedName("A", DType.R8Req)),
                        "{A:g|".Replace(mark, close));

                    DoSuper(union,
                        DType.CreateRecord(opt0, new TypedName("A", DType.General)),
                        DType.CreateRecord(opt1, new TypedName("A", DType.General)),
                        "{A:g|".Replace(mark, close));

                    DoSuper(union,
                        DType.CreateRecord(opt0, new TypedName("A", DType.R8Req)),
                        DType.CreateRecord(opt1, new TypedName("A", DType.IAReq)),
                        "{A:r8|".Replace(mark, close));

                    // Different names.
                    DoSuper(union,
                        DType.CreateRecord(opt0, new TypedName("A", DType.R8Req)),
                        DType.CreateRecord(opt1, new TypedName("B", DType.R8Req)),
                        (union ? "{A:r8?, B:r8?|" : "{|").Replace(mark, close));

                    DoSuper(union,
                        DType.CreateRecord(opt0, new TypedName("A", DType.R8Req), new TypedName("B", DType.IAReq)),
                        DType.CreateRecord(opt1, new TypedName("B", DType.R8Req)),
                        (union ? "{A:r8?, B:r8|" : "{B:r8|").Replace(mark, close));

                    DoSuper(union,
                        DType.CreateRecord(opt0, new TypedName("A", DType.R8Req), new TypedName("B", DType.IAOpt)),
                        DType.CreateRecord(opt1, new TypedName("B", DType.R8Req)),
                        (union ? "{A:r8?, B:r8?|" : "{B:r8?|").Replace(mark, close));

                    DoSuper(union,
                        DType.CreateRecord(opt0, new TypedName("A", DType.R8Req), new TypedName("B", DType.IAReq)),
                        DType.CreateRecord(opt1, new TypedName("B", DType.R8Opt)),
                        (union ? "{A:r8?, B:r8?|" : "{B:r8?|").Replace(mark, close));

                    DoSuper(union,
                        DType.CreateRecord(opt0, new TypedName("A", DType.R8Req), new TypedName("B", DType.IAOpt)),
                        DType.CreateRecord(opt1, new TypedName("B", DType.R8Opt)),
                        (union ? "{A:r8?, B:r8?|" : "{B:r8?|").Replace(mark, close));

                    DoSuper(union,
                        DType.CreateRecord(opt0, new TypedName("A", DType.R8Req.ToSequence())),
                        DType.CreateRecord(opt1, new TypedName("A", DType.R8Req.ToSequence())),
                        "{A:r8*|".Replace(mark, close));

                    DoSuper(union,
                        DType.CreateRecord(opt0, new TypedName("A", DType.R8Opt.ToSequence())),
                        DType.CreateRecord(opt1, new TypedName("A", DType.R8Req.ToSequence())),
                        "{A:r8?*|".Replace(mark, close));

                    DoSuper(union,
                        DType.CreateRecord(opt0, new TypedName("A", DType.R8Req.ToSequence().ToSequence())),
                        DType.CreateRecord(opt1, new TypedName("A", DType.R8Req.ToSequence().ToSequence())),
                        "{A:r8**|".Replace(mark, close));

                    DoSuper(union,
                        DType.CreateRecord(opt0, new TypedName("A", DType.R8Req.ToSequence())),
                        DType.CreateRecord(opt1, new TypedName("A", DType.R8Req.ToSequence().ToSequence())),
                        "{A:g*|".Replace(mark, close));

                    DoSuper(union,
                        // [A:r8,B:s,C:b,D:r8]*
                        DType.CreateTable(
                            opt0,
                            new TypedName("A", DType.R8Req),
                            new TypedName("B", DType.Text),
                            new TypedName("C", DType.BitReq),
                            new TypedName("D", DType.R8Req)),
                        // [A:r8,B:s,C:r8,D:r8]*
                        DType.CreateTable(
                            opt1,
                            new TypedName("A", DType.R8Req),
                            new TypedName("B", DType.Text),
                            new TypedName("C", DType.R8Req),
                            new TypedName("D", DType.R8Req)),
                        "{A:r8, B:s, C:r8, D:r8|*".Replace(mark, close));

                    DoSuper(union,
                        // [A:r8,B:s,C:d,D:r8]*
                        DType.CreateTable(
                            opt0,
                            new TypedName("A", DType.R8Req),
                            new TypedName("B", DType.Text),
                            new TypedName("C", DType.DateReq),
                            new TypedName("D", DType.R8Req)),
                        // [A:r8,B:s,C:r8,D:r8]*
                        DType.CreateTable(
                            opt1,
                            new TypedName("A", DType.R8Req),
                            new TypedName("B", DType.Text),
                            new TypedName("C", DType.R8Req),
                            new TypedName("D", DType.R8Req)),
                        "{A:r8, B:s, C:g, D:r8|*".Replace(mark, close));

                    DoSuper(union,
                        // [A:r8,B:s,C:[D:r8,F:i]*]*
                        DType.CreateTable(
                            opt0,
                            new TypedName("A", DType.R8Req),
                            new TypedName("B", DType.Text),
                            new TypedName("C", DType.CreateTable(
                                opt0,
                                new TypedName("D", DType.R8Req),
                                new TypedName("F", DType.IAReq)))),
                        // [A:r8,B:s,C:[D:r8,F:s]*]*
                        DType.CreateTable(
                            opt1,
                            new TypedName("A", DType.R8Req),
                            new TypedName("B", DType.Text),
                            new TypedName("C", DType.CreateTable(
                                opt1,
                                new TypedName("D", DType.R8Req),
                                new TypedName("F", DType.Text)))),
                        "{A:r8, B:s, C:{D:r8, F:g|*|*".Replace(mark, close));

                    DoSuper(union,
                        // [A:r8,B:s,C:[D:r8,F:i]*]*
                        DType.CreateTable(
                            opt0,
                            new TypedName("A", DType.R8Req),
                            new TypedName("B", DType.Text),
                            new TypedName("C", DType.CreateTable(
                                opt1,
                                new TypedName("D", DType.R8Req),
                                new TypedName("F", DType.IAReq)))),
                        // [A:r8,B:s,C:[D:r8,F:s]*]*
                        DType.CreateTable(
                            opt1,
                            new TypedName("A", DType.R8Req),
                            new TypedName("B", DType.Text),
                            new TypedName("C", DType.CreateTable(
                                opt0,
                                new TypedName("D", DType.R8Req),
                                new TypedName("F", DType.Text)))),
                        "{A:r8, B:s, C:{D:r8, F:g|*|*".Replace(mark, close));

                    DoSuper(union,
                        // [A:r8,B:s,C:[D:r8,F:i]*]*
                        DType.CreateTable(
                            opt0,
                            new TypedName("A", DType.R8Req),
                            new TypedName("B", DType.Text),
                            new TypedName("C", DType.CreateTable(
                                opt0,
                                new TypedName("D", DType.R8Req),
                                new TypedName("F", DType.IAReq)))),
                        // [A:r8,B:s,C:[D:r8,F:s]*]*
                        DType.CreateTable(
                            opt1,
                            new TypedName("A", DType.R8Req),
                            new TypedName("B", DType.Text),
                            new TypedName("C", DType.CreateTable(
                                opt0,
                                new TypedName("D", DType.R8Req),
                                new TypedName("F", DType.Text)))),
                        "{A:r8, B:s, C:{D:r8, F:g/*|*".Replace("/", opt0 ? "}?" : "}").Replace(mark, close));

                    DoSuper(union,
                        // [A:r8,B:s,C:[D:r8,F:i]]
                        DType.CreateRecord(
                            opt0,
                            new TypedName("A", DType.R8Req),
                            new TypedName("B", DType.Text),
                            new TypedName("C", DType.CreateRecord(
                                opt0,
                                new TypedName("D", DType.R8Req),
                                new TypedName("F", DType.IAReq)))),
                        // [A:r8,B:s,C:[D:r8,F:s]]
                        DType.CreateRecord(
                            opt1,
                            new TypedName("A", DType.R8Req),
                            new TypedName("B", DType.Text),
                            new TypedName("C", DType.CreateRecord(
                                opt1,
                                new TypedName("D", DType.R8Req),
                                new TypedName("F", DType.Text)))),
                        "{A:r8, B:s, C:{D:r8, F:g||".Replace(mark, close));

                    DoSuper(union,
                        // [A:r4, B:s]
                        DType.CreateRecord(
                            opt0,
                            new TypedName("A", DType.R4Req),
                            new TypedName("B", DType.Text)),
                        // [A:i, B:b]
                        DType.CreateRecord(
                            opt1,
                            new TypedName("A", DType.IAReq),
                            new TypedName("B", DType.BitReq)),
                        "{A:r4, B:g|".Replace(mark, close));

                    DoSuper(union,
                        // [A:r8,B:s,C:[D:r8,F:d]]
                        DType.CreateRecord(
                            opt0,
                            new TypedName("A", DType.R8Req),
                            new TypedName("B", DType.Text),
                            new TypedName("C", DType.CreateRecord(
                                opt0,
                                new TypedName("D", DType.R8Req),
                                new TypedName("F", DType.DateReq)))),
                        // [A:r8,B:s,C:[D:b,F:s]]
                        DType.CreateRecord(
                            opt1,
                            new TypedName("A", DType.R8Req),
                            new TypedName("B", DType.Text),
                            new TypedName("C", DType.CreateRecord(
                                opt1,
                                new TypedName("D", DType.BitReq),
                                new TypedName("F", DType.Text)))),
                        "{A:r8, B:s, C:{D:r8, F:g||".Replace(mark, close));

                    DoSuper(union,
                        // [A:r8,B:s,C:[D:r8,F:d]]
                        DType.CreateRecord(
                            opt0,
                            new TypedName("A", DType.R8Req),
                            new TypedName("B", DType.Text),
                            new TypedName("C", DType.CreateRecord(
                                opt0,
                                new TypedName("D", DType.R8Req),
                                new TypedName("F", DType.DateReq)))),
                        // [A:r8,B:s,C:[D:d,F:s]]
                        DType.CreateRecord(
                            opt1,
                            new TypedName("A", DType.R8Req),
                            new TypedName("B", DType.Text),
                            new TypedName("C", DType.CreateRecord(
                                opt1,
                                new TypedName("D", DType.DateReq),
                                new TypedName("F", DType.Text)))),
                        "{A:r8, B:s, C:{D:g, F:g||".Replace(mark, close));

                    DoSuper(union,
                        // [A:r8,B:s,C:[D:r8,F:o]]
                        DType.CreateRecord(
                            opt0,
                            new TypedName("A", DType.R8Req),
                            new TypedName("B", DType.Text),
                            new TypedName("C", DType.CreateRecord(
                                opt0,
                                new TypedName("D", DType.R8Req),
                                new TypedName("F", DType.Null)))),
                        // [A:r8,B:s,C:[D:d,F:s]]
                        DType.CreateRecord(
                            opt1,
                            new TypedName("A", DType.R8Req),
                            new TypedName("B", DType.Text),
                            new TypedName("C", DType.CreateRecord(
                                opt1,
                                new TypedName("D", DType.DateReq),
                                new TypedName("F", DType.Text)))),
                        "{A:r8, B:s, C:{D:g, F:s||".Replace(mark, close));

                    DoSuper(union,
                        // [D:r8,E:s,F:[G:r8,H:o]]
                        DType.CreateRecord(
                            opt0,
                            new TypedName("D", DType.R8Req),
                            new TypedName("E", DType.Text),
                            new TypedName("F", DType.CreateRecord(
                                opt0,
                                new TypedName("G", DType.R8Req),
                                new TypedName("H", DType.Null)))),
                        // [E:s]
                        DType.CreateRecord(opt1, new TypedName("E", DType.Text)),
                        (union ? "{D:r8?, E:s, F:{G:r8, H:o}?|" : "{E:s|").Replace(mark, close));
                }
            }
        }
    }

    /// <summary>
    /// This test is for code coverage of the DType deserialization code.
    /// </summary>
    [TestMethod]
    public void TestDTypeParsing()
    {
        DType type;

        // Good.
        Assert.IsTrue(DType.TryDeserialize("n", out type) & type == DType.R8Req);
        Assert.IsTrue(DType.TryDeserialize("r8", out type) & type == DType.R8Req);
        Assert.IsTrue(DType.TryDeserialize("i", out type) & type == DType.IAReq);
        Assert.IsTrue(DType.TryDeserialize("i?", out type) & type == DType.IAOpt);
        Assert.IsTrue(DType.TryDeserialize("i8", out type) & type == DType.I8Req);
        Assert.IsTrue(DType.TryDeserialize("   i8***  ", out type) & type.RootType == DType.I8Req & type.SeqCount == 3);
        Assert.IsTrue(DType.TryDeserialize("n[ * , 3-10 ]", out type) & type.Kind == DKind.Tensor & type.TensorRank == 2);
        Assert.IsTrue(DType.TryDeserialize("n[*,3-10]", out type) & type.Kind == DKind.Tensor & type.TensorRank == 2);
        Assert.IsTrue(DType.TryDeserialize("n[2147483648]", out type) & type.Kind == DKind.Tensor & type.TensorRank == 1);
        Assert.IsTrue(DType.TryDeserialize("n[12345-3000000000]", out type) & type.Kind == DKind.Tensor & type.TensorRank == 1);
        Assert.IsTrue(DType.TryDeserialize("n[100-12345678901234567890]", out type) & type.Kind == DKind.Tensor & type.TensorRank == 1);
        Assert.IsTrue(DType.TryDeserialize("n[12345678901234567890]", out type) & type.Kind == DKind.Tensor & type.TensorRank == 1);
        Assert.IsTrue(DType.TryDeserialize("(i4,s,t)", out type) & type.Kind == DKind.Tuple & type.TupleArity == 3);
        Assert.IsTrue(DType.TryDeserialize("()", out type) & type.Kind == DKind.Tuple & type.TupleArity == 0);
        Assert.IsTrue(DType.TryDeserialize("( i4  )", out type) & type.Kind == DKind.Tuple & type.TupleArity == 1);
        Assert.IsTrue(DType.TryDeserialize("(( ) ,  G)", out type) & type.Kind == DKind.Tuple & type.TupleArity == 2);

        Assert.IsTrue(DType.TryDeserialize("{a :n}", out type) & type.IsRecordReq & type.GetNames().Count() == 1);
        Assert.IsTrue(DType.TryDeserialize("{'a b':n}", out type) & type.IsRecordReq & type.GetNames().Count() == 1);
        Assert.IsTrue(DType.TryDeserialize("{'a b *+!''%':n}", out type) & type.IsRecordReq & type.GetNames().Count() == 1);
        Assert.IsTrue(DType.TryDeserialize("{'a:b:c':n}", out type) & type.IsRecordReq & type.GetNames().Count() == 1);

        Assert.IsTrue(DType.TryDeserialize("U<>", out type) & type == DType.UriGen);
        Assert.IsTrue(DType.TryDeserialize("U<>?", out type) & type == DType.UriGen);
        Assert.IsTrue(DType.TryDeserialize("U<>**", out type) & type.RootType == DType.UriGen & type.SeqCount == 2);
        Assert.IsTrue(DType.TryDeserialize("U<>?**", out type) & type.RootType == DType.UriGen & type.SeqCount == 2);
        Assert.IsTrue(DType.TryDeserialize("U<Image>", out type) & type.Kind == DKind.Uri &
            type.GetRootUriFlavor() == NPath.Root.Append(new DName("Image")));
        Assert.IsTrue(DType.TryDeserialize("U<Image>?", out type) & type.Kind == DKind.Uri &
            type.GetRootUriFlavor() == NPath.Root.Append(new DName("Image")));
        Assert.IsTrue(DType.TryDeserialize("U<Image>*", out type) & type.RootKind == DKind.Uri &
            type.GetRootUriFlavor() == NPath.Root.Append(new DName("Image")) & type.SeqCount == 1);
        Assert.IsTrue(DType.TryDeserialize("U<Image>?*", out type) & type.RootKind == DKind.Uri &
            type.GetRootUriFlavor() == NPath.Root.Append(new DName("Image")) & type.SeqCount == 1);
        Assert.IsTrue(DType.TryDeserialize("U<'  Image'>", out type) & type.Kind == DKind.Uri &
            type.GetRootUriFlavor() == NPath.Root.Append(new DName("  Image")));
        Assert.IsTrue(DType.TryDeserialize("U<' A Flavor   '>", out type) & type.Kind == DKind.Uri &
            type.GetRootUriFlavor() == NPath.Root.Append(new DName(" A Flavor   ")));
        Assert.IsTrue(DType.TryDeserialize("U<' A Flavor ''><  '>", out type) & type.Kind == DKind.Uri &
            type.GetRootUriFlavor() == NPath.Root.Append(new DName(" A Flavor '><  ")));
        Assert.IsTrue(DType.TryDeserialize("U<Image.Jpeg>", out type) & type.Kind == DKind.Uri &
            type.GetRootUriFlavor() == NPath.Root.Append(new DName("Image")).Append(new DName("Jpeg")));
        Assert.IsTrue(DType.TryDeserialize("U<' A Flavor.NotPath ''><  '>", out type) & type.Kind == DKind.Uri &
            type.GetRootUriFlavor() == NPath.Root.Append(new DName(" A Flavor.NotPath '><  ")));

        // Should remove "Flavor" suffix.
        NPath ABC = NPath.Root.Append(new DName("A")).Append(new DName("B")).Append(new DName("C"));
        Assert.IsTrue(DType.TryDeserialize("U<A.B.C>", out type) & type.Kind == DKind.Uri & type.GetRootUriFlavor() == ABC);
        Assert.IsTrue(DType.TryDeserialize("U<A.B.CFlavor>", out type) & type.Kind == DKind.Uri & type.GetRootUriFlavor() == ABC);
        Assert.IsTrue(DType.TryDeserialize("U<A.BFlavor.C>", out type) & type.Kind == DKind.Uri & type.GetRootUriFlavor() == ABC);
        Assert.IsTrue(DType.TryDeserialize("U<A.BFlavor.CFlavor>", out type) & type.Kind == DKind.Uri & type.GetRootUriFlavor() == ABC);
        Assert.IsTrue(DType.TryDeserialize("U<AFlavor.B.C>", out type) & type.Kind == DKind.Uri & type.GetRootUriFlavor() == ABC);
        Assert.IsTrue(DType.TryDeserialize("U<AFlavor.B.CFlavor>", out type) & type.Kind == DKind.Uri & type.GetRootUriFlavor() == ABC);
        Assert.IsTrue(DType.TryDeserialize("U<AFlavor.BFlavor.C>", out type) & type.Kind == DKind.Uri & type.GetRootUriFlavor() == ABC);
        Assert.IsTrue(DType.TryDeserialize("U<AFlavor.BFlavor.CFlavor>", out type) & type.Kind == DKind.Uri & type.GetRootUriFlavor() == ABC);
        Assert.IsTrue(DType.TryDeserialize("U<' A Flavor'>", out type) & type.Kind == DKind.Uri &
            type.GetRootUriFlavor() == NPath.Root.Append(new DName(" A ")));
        Assert.IsTrue(DType.TryDeserialize("U<'   Flavor'>", out type) & type.Kind == DKind.Uri &
            type.GetRootUriFlavor() == NPath.Root.Append(new DName("   Flavor")));

        // Errors.
        Assert.IsTrue(!DType.TryDeserialize(null, out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("   i8* **  ", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("  ", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("j", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("i7", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("r3", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("u", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("u ", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("u5", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("{x ", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("{x:n", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("{x:n y:b}", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("n*?", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("n[*-,3]", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("n[ ", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("n[ * 3-10]", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("n[*,", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("n[x]", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("n[+]", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("n[12345", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("n[12345-", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("n[12345-3000]", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("{'a b *+!'%':n}", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("{hello+:n}", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("{' ':n}", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("{A:n,A:b}", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("{A:x}", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("U", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("U<", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("U< >", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("U <>", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("U< Image>", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("U<Image  >", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("U< 'Image  '>", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("U< A Flavor   >", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("U<Blah.", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("(,)", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("(i4,)", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("(", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("(b s", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("(b,", out type) & !type.IsValid);
        Assert.IsTrue(!DType.TryDeserialize("(b,  ", out type) & !type.IsValid);
    }

    /// <summary>
    /// This test is for code coverage of the DType deserialization code that supports alternatives.
    /// </summary>
    [TestMethod]
    public void TestDTypeParsingWithAlts()
    {
        DType type;

        // Good.
        Assert.IsTrue(DType.TryDeserializeWithAlts("b", true, out type) & type == DType.BitReq);
        Assert.IsTrue(DType.TryDeserializeWithAlts("o", true, out type) & type == DType.Null);
        Assert.IsTrue(DType.TryDeserializeWithAlts("(r8|r4)", true, out type) & type == DType.R8Req);
        Assert.IsTrue(DType.TryDeserializeWithAlts("(ia|r8|o)", true, out type) & type == DType.R8Opt);
        Assert.IsTrue(DType.TryDeserializeWithAlts("({A:i4, B:b}|{A:i8, C:s})", true, out type));
        Assert.AreEqual("{A:i8, B:b?, C:s}", type.Serialize());
        Assert.IsTrue(DType.TryDeserializeWithAlts("( { A:i4, B:b} |  { A:i8, C:s}  )", true, out type));
        Assert.AreEqual("{A:i8, B:b?, C:s}", type.Serialize());

        // Bad.
        Assert.IsFalse(DType.TryDeserializeWithAlts(null, true, out type));
        Assert.IsFalse(DType.TryDeserializeWithAlts("  x ", true, out type) & !type.IsValid);
        Assert.IsFalse(DType.TryDeserializeWithAlts("({A:i4, B:b}|{A:i8, C:s}", true, out type));
        Assert.IsFalse(DType.TryDeserializeWithAlts("({A:i4, B:b}  ", true, out type));
        Assert.IsFalse(DType.TryDeserialize("({A:i4, B:b} | { })", out type));
        Assert.IsFalse(DType.TryDeserializeWithAlts("({A:i4, B:b} | { ", true, out type));
        Assert.IsFalse(DType.TryDeserializeWithAlts("({A:i4, B:b} | { }, { })", true, out type));
    }
}

[TestClass]
public class DTypeBaseLineTests : RexlTestsBaseText<bool>
{
    [TestMethod]
    public void TestIncludes()
    {
        int count = DoBaselineTests(
            Run, @"DType/Includes.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, bool opts)
        {
            void Yep(string a, string b)
            {
                Assert.IsTrue(DType.TryDeserialize(a, out var t1));
                Assert.IsTrue(DType.TryDeserialize(b, out var t2));
                Assert.IsTrue(t1.Includes(t2));
                Sink.WriteLine("          {0} inc {1}", t1.Serialize(), t2.Serialize());
            }
            void Not(string a, string b)
            {
                Assert.IsTrue(DType.TryDeserialize(a, out var t1));
                Assert.IsTrue(DType.TryDeserialize(b, out var t2));
                Assert.IsFalse(t1.Includes(t2));
                Sink.WriteLine("!!False!! {0} inc {1}", t1.Serialize(), t2.Serialize());
            }

            // Basic.
            Yep("i4", "i4");
            Not("i8", "i4");
            Yep("g", "i4");
            Yep("g", "i4*");
            Yep("g*", "i4*");
            Not("g*", "i4");
            Yep("g*", "i4**");
            Yep("g**", "i4**");
            Not("g**", "i4*");

            // Tensor.
            Yep("g", "(s,i4)[*,*]");
            Not("g[*]", "i4");
            Yep("g[*]", "i4[*]");
            Not("g[*]", "i4[*,*]");
            Yep("g[*,*]", "i4[*,*]");
            Yep("(g,i4)[*,*]", "(s,i4)[*,*]");

            // Tuple.
            Yep("g", "(i4*,s,b)");
            Yep("(g)", "(i4)");
            Yep("(g)*", "(i4)*");
            Not("(g)", "(i4)*");
            Not("(g)*", "(i4)");
            Not("(i8)", "(i4)");
            Not("(g)", "(i4,s)");
            Not("(g,i8)", "({A:s})");
            Yep("(g,i8)", "({A:s},i8)");
            Not("(g,i8)", "({A:s},i4)");

            // Record.
            Yep("g", "{A:s,B:i4}");
            Not("{A:g}", "i4");
            Yep("{A:g}", "{A:i4}");
            Yep("{A:g}***", "{A:i4}***");
            Not("{A:g}**", "{A:i4}***");
            Not("{A:g}***", "{A:i4}**");
            Not("{A:g}", "{B:i4}");
            Not("{A:g}", "{A:i4,B:i4}");
            Not("{A:g,B:i4}", "{B:i4}");
            Yep("{A:g,B:i4}", "{A:s,B:i4}");
            Not("{A:g,B:i8}", "{A:s,B:i4}");

            // Opt.
            Yep("g", "{A:i4}?");
            Yep("{A:g}?", "{A:i4}?");
            Yep("{A:g}?**", "{A:i4}?**");
            Not("{A:g}?", "{A:i4}");
            Not("{A:g}", "{A:i4}?");
            Yep("(g)?", "({A:i4}?)?");
            Not("(g)?", "({A:i4}?)");
        }
    }

    [TestMethod]
    public void TestGetIncluded()
    {
        int count = DoBaselineTests(
            Run, @"DType/GetIncluded.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, bool opts)
        {
            void Do(string a, string b)
            {
                Assert.IsTrue(DType.TryDeserialize(a, out var t1));
                Assert.IsTrue(DType.TryDeserialize(b, out var t2));

                var res = t1.GetIncludedType(t2);
                Assert.IsTrue(t1.Includes(res));

                Sink.WriteLine("{0} <<< {1}", t1.Serialize(), t2.Serialize());
                Sink.WriteLine(res.Serialize());
                Sink.WriteLine("###");
            }

            // Basic.
            Do("i4", "i4");
            Do("i8", "i4");
            Do("g", "i4");
            Do("g", "i4*");
            Do("g", "(i4*,s,b)");
            Do("g*", "i4*");
            Do("g*", "i4");
            Do("g*", "i4**");
            Do("g**", "i4**");
            Do("g**", "i4*");

            // Tensor.
            Do("g[*]", "i4");
            Do("g[*]", "i4[*]");
            Do("g[*]", "i4[*,*]");

            // Record.
            Do("{A:g}", "i4");
            Do("{A:g}", "{A:i4}");
            Do("{A:g}", "{B:i4}");
            Do("{A:g}", "{A:i4,B:i4}");
            Do("{A:g,B:i4}", "{B:i4}");
            Do("{A:g,B:i4}", "{A:s,B:i4}");
            Do("{A:g,B:i8}", "{A:s,B:i4}");
            Do("{A:g,B:i8}", "{A:s,B:i4}*");
            Do("{A:g}", "s");

            // Opt.
            Do("{A:g}?", "{A:i4}?");
            Do("{A:g}?", "{A:i4}");
            Do("{A:g}", "{A:i4}?");

            // Tuple.
            Do("(g)", "(i4)");
            Do("(i8)", "(i4)");
            Do("(g)", "(i4,s)");
            Do("(g,i8)", "({A:s})");
            Do("(g,i8)", "(i4,i8)");
            Do("(g,i8)", "(i4,i4)");
            Do("(g,i8)", "i4");
            Do("(i8,g)", "i4");
        }
    }
}
