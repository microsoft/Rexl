// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Linq;

using Microsoft.Rexl;

namespace RexlTest;

internal sealed class TestMultiFormProc : TestMultiFormOper
{
    // These names have the form Multi[D|_][G][S|s|R|r|f][M|_][A|B|C]... with:
    // * [D|_] indicating whether the direct simple form is supported: F(<arg>).
    // * [G] indicating that there is a field G in the mis.
    // * [S|s|R|r|f] indicating whether the input type is a sequence or record. Little s/r indicates that form 3/5 should be used
    //     instead of form 4/6. Little f indicates that form 2 should be used.
    // * [M|_] indicating whether merging is supported.
    // * [A|B|C] indicating whether the mis type is { A:s }* or { A:s, B:s }* or { A:s, C:{ D:s, E:s }* }*.
    // * [|a|b|c|ab] indicating that the given fields are required (not optional).
    // * [|W] indicating that mos is dst or that mos is wrapped in a dst record, together with the settings (echoed).
    public static readonly TestMultiFormProc MultiDSMA = Create(nameof(MultiDSMA), new Settings { dir = Y, mrg = Y, withB = n, withC = n, withS1S2 = 0, outRec = n });
    public static readonly TestMultiFormProc MultiDSMB = Create(nameof(MultiDSMB), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0, outRec = n });
    public static readonly TestMultiFormProc MultiDsMB = Create(nameof(MultiDsMB), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0, outRec = n, expSel = n });
    public static readonly TestMultiFormProc MultiDS_A = Create(nameof(MultiDS_A), new Settings { dir = Y, mrg = n, withB = n, withC = n, withS1S2 = 0, outRec = n });
    public static readonly TestMultiFormProc MultiDS_B = Create(nameof(MultiDS_B), new Settings { dir = Y, mrg = n, withB = Y, withC = n, withS1S2 = 0, outRec = n });
    public static readonly TestMultiFormProc Multi_SMB = Create(nameof(Multi_SMB), new Settings { dir = n, mrg = Y, withB = Y, withC = n, withS1S2 = 0, outRec = n });
    public static readonly TestMultiFormProc MultiDSMBb = Create(nameof(MultiDSMBb), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0, outRec = n, hasDefB = n });
    public static readonly TestMultiFormProc MultiDSMBab = Create(nameof(MultiDSMBab), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0, outRec = n, hasDefA = n, hasDefB = n });
    public static readonly TestMultiFormProc MultiDSMC = Create(nameof(MultiDSMC), new Settings { dir = Y, mrg = Y, withB = n, withC = Y, withS1S2 = 0, outRec = n, hasDefB = n });
    public static readonly TestMultiFormProc MultiDSMCc = Create(nameof(MultiDSMCc), new Settings { dir = Y, mrg = Y, withB = n, withC = Y, withS1S2 = 0, outRec = n, hasDefB = n, hasDefC = n });
    public static readonly TestMultiFormProc MultiDRMB = Create(nameof(MultiDRMB), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x3, outRec = n });
    public static readonly TestMultiFormProc MultiDrMB = Create(nameof(MultiDrMB), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x3, outRec = n, expSel = n });
    public static readonly TestMultiFormProc MultiDfMB = Create(nameof(MultiDfMB), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x3, outRec = n, inFld = Y });
    public static readonly TestMultiFormProc MultiDSMBW = Create(nameof(MultiDSMBW), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0, outRec = Y });
    public static readonly TestMultiFormProc MultiDRMBW = Create(nameof(MultiDRMBW), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x3, outRec = Y });

    public static readonly TestMultiFormProc MultiDGSMA = Create(nameof(MultiDGSMA), new Settings { dir = Y, mrg = Y, withB = n, withC = n, withS1S2 = 0, outRec = n, withG = Y });
    public static readonly TestMultiFormProc MultiDGSMB = Create(nameof(MultiDGSMB), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0, outRec = n, withG = Y });
    public static readonly TestMultiFormProc MultiDGsMB = Create(nameof(MultiDGsMB), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0, outRec = n, withG = Y, expSel = n });
    public static readonly TestMultiFormProc MultiDGS_A = Create(nameof(MultiDGS_A), new Settings { dir = Y, mrg = n, withB = n, withC = n, withS1S2 = 0, outRec = n, withG = Y });
    public static readonly TestMultiFormProc MultiDGS_B = Create(nameof(MultiDGS_B), new Settings { dir = Y, mrg = n, withB = Y, withC = n, withS1S2 = 0, outRec = n, withG = Y });
    public static readonly TestMultiFormProc Multi_GSMB = Create(nameof(Multi_GSMB), new Settings { dir = n, mrg = Y, withB = Y, withC = n, withS1S2 = 0, outRec = n, withG = Y });
    public static readonly TestMultiFormProc MultiDGSMBb = Create(nameof(MultiDGSMBb), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0, outRec = n, withG = Y, hasDefB = n });
    public static readonly TestMultiFormProc MultiDGSMBab = Create(nameof(MultiDGSMBab), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0, outRec = n, withG = Y, hasDefA = n, hasDefB = n });
    public static readonly TestMultiFormProc MultiDGSMC = Create(nameof(MultiDGSMC), new Settings { dir = Y, mrg = Y, withB = n, withC = Y, withS1S2 = 0, outRec = n, withG = Y, hasDefB = n });
    public static readonly TestMultiFormProc MultiDGSMCc = Create(nameof(MultiDGSMCc), new Settings { dir = Y, mrg = Y, withB = n, withC = Y, withS1S2 = 0, outRec = n, withG = Y, hasDefB = n, hasDefC = n });
    public static readonly TestMultiFormProc MultiDGRMB = Create(nameof(MultiDGRMB), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x3, outRec = n, withG = Y });
    public static readonly TestMultiFormProc MultiDGrMB = Create(nameof(MultiDGrMB), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x3, outRec = n, withG = Y, expSel = n });
    public static readonly TestMultiFormProc MultiDGfMB = Create(nameof(MultiDGfMB), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x3, outRec = n, withG = Y, inFld = Y });
    public static readonly TestMultiFormProc MultiDGSMBW = Create(nameof(MultiDGSMBW), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0, outRec = Y, withG = Y });
    public static readonly TestMultiFormProc MultiDGRMBW = Create(nameof(MultiDGRMBW), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x3, outRec = Y, withG = Y });

    // These names have the form MultiS_[*]_[A][B][C][G][D][M][1][2].
    // When each flag is set:
    // A - allow single to sequence on the A field when it is a slot and increases its seq count by 1.
    // B - allow single to sequence on the B field when it is a slot and increases its seq count by 1.
    // C - allow single to sequence on the C field when it is a slot and increases its seq count by 1.
    // G - allow single to sequence on the G field when it is a slot and increases its seq count by 1.
    // D - allow single to sequence on the singular argument in the direct form (1).
    // M - allow single to sequence on the singular selector argument in forms (3) and (5).
    // 1 - allow single to sequence on the S1 setting when it is a slot and increases its seq count by 1.
    // 2 - allow single to sequence on the S2 setting when it is a slot and increases its seq count by 1.
    // Otherwise the function has the same semantics as indicated by above within the [*].
    public static readonly TestMultiFormProc MultiS_DSMA_AD = Create(nameof(MultiS_DSMA_AD), new Settings { dir = Y, mrg = Y, withB = n, withC = n, withS1S2 = 0, outRec = n, singleA = Y, singleDir = Y });
    public static readonly TestMultiFormProc MultiS_DSMB_ABD = Create(nameof(MultiS_DSMB_ABD), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0, outRec = n, singleA = Y, singleB = Y, singleDir = Y });
    public static readonly TestMultiFormProc MultiS_DSMC_ACD = Create(nameof(MultiS_DSMC_ACD), new Settings { dir = Y, mrg = Y, withB = n, withC = Y, withS1S2 = 0, outRec = n, hasDefB = n, singleA = Y, singleC = Y, singleDir = Y });
    public static readonly TestMultiFormProc MultiS_DRMB_ABD12 = Create(nameof(MultiS_DRMB_ABD12), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x3, outRec = n, singleA = Y, singleB = Y, singleS1 = Y, singleS2 = Y });
    public static readonly TestMultiFormProc MultiS_DfMB_ABDM12 = Create(nameof(MultiS_DfMB_ABDM12), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x3, outRec = n, inFld = Y, singleA = Y, singleB = Y, singleSel = Y, singleS1 = Y, singleS2 = Y });
    public static readonly TestMultiFormProc MultiS_DsMB_ABDM = Create(nameof(MultiS_DsMB_ABDM), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0, outRec = n, expSel = n, singleA = Y, singleB = Y, singleDir = Y, singleSel = Y });
    public static readonly TestMultiFormProc MultiS_DrMB_ABDM12 = Create(nameof(MultiS_DrMB_ABDM12), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x3, outRec = n, expSel = n, singleA = Y, singleB = Y, singleDir = Y, singleSel = Y, singleS1 = Y, singleS2 = Y });

    public static readonly TestMultiFormProc MultiS_DGSMA_AGD = Create(nameof(MultiS_DGSMA_AGD), new Settings { dir = Y, mrg = Y, withB = n, withC = n, withS1S2 = 0, outRec = n, withG = Y, singleA = Y, singleG = Y, singleDir = Y });
    public static readonly TestMultiFormProc MultiS_DGSMB_ABGD = Create(nameof(MultiS_DGSMB_ABGD), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0, outRec = n, withG = Y, singleA = Y, singleB = Y, singleG = Y, singleDir = Y });
    public static readonly TestMultiFormProc MultiS_DGRMB_ABGD12 = Create(nameof(MultiS_DGRMB_ABGD12), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x3, outRec = n, withG = Y, singleA = Y, singleB = Y, singleG = Y, singleS1 = Y, singleS2 = Y });
    public static readonly TestMultiFormProc MultiS_DGfMB_ABGDM12 = Create(nameof(MultiS_DGfMB_ABGDM12), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x3, outRec = n, withG = Y, inFld = Y, singleA = Y, singleB = Y, singleG = Y, singleSel = Y, singleS1 = Y, singleS2 = Y });
    public static readonly TestMultiFormProc MultiS_DGsMB_ABGDM = Create(nameof(MultiS_DGsMB_ABGDM), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0, outRec = n, withG = Y, expSel = n, singleA = Y, singleB = Y, singleG = Y, singleDir = Y, singleSel = Y });
    public static readonly TestMultiFormProc MultiS_DGrMB_ABGDM12 = Create(nameof(MultiS_DGrMB_ABGDM12), new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x3, outRec = n, withG = Y, expSel = n, singleA = Y, singleB = Y, singleG = Y, singleDir = Y, singleSel = Y, singleS1 = Y, singleS2 = Y });

    // Functions with multiple overloads of the form ManyXXXX, where XXXX is a common setting
    // shared by all of the overloads.
    public static readonly TestMultiFormProc ManyDRMB = Create(nameof(ManyDRMB),
        new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x0, outRec = n, hasDefS1 = n, hasDefS2 = n },
        new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x1, outRec = n, hasDefS1 = n, hasDefS2 = n },
        new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x2, outRec = n, hasDefS1 = n, hasDefS2 = n },
        new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x3, outRec = n, hasDefS1 = n, hasDefS2 = n },
        new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x3, outRec = n, s2First = Y, hasDefS1 = n, hasDefS2 = n });
    public static readonly TestMultiFormProc ManyDRM = Create(nameof(ManyDRM),
        new Settings { dir = Y, mrg = Y, withB = n, withC = n, withS1S2 = 0x0, outRec = n, hasDefS1 = n, hasDefS2 = n },
        new Settings { dir = Y, mrg = Y, withB = n, withC = n, withS1S2 = 0x1, outRec = n, hasDefS1 = n, hasDefS2 = n },
        new Settings { dir = Y, mrg = Y, withB = n, withC = n, withS1S2 = 0x2, outRec = n, hasDefS1 = n, hasDefS2 = n },
        new Settings { dir = Y, mrg = Y, withB = n, withC = n, withS1S2 = 0x3, outRec = n, hasDefS1 = n, hasDefS2 = n },
        new Settings { dir = Y, mrg = Y, withB = n, withC = n, withS1S2 = 0x3, outRec = n, s2First = Y, hasDefS1 = n, hasDefS2 = n },
        new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x0, outRec = n, hasDefB = n, hasDefS1 = n, hasDefS2 = n },
        new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x1, outRec = n, hasDefB = n, hasDefS1 = n, hasDefS2 = n },
        new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x2, outRec = n, hasDefB = n, hasDefS1 = n, hasDefS2 = n },
        new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x3, outRec = n, hasDefB = n, hasDefS1 = n, hasDefS2 = n },
        new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x3, outRec = n, s2First = Y, hasDefB = n, hasDefS1 = n, hasDefS2 = n });
    // Same as above but testing default values.
    public static readonly TestMultiFormProc ManyDRM_Def = Create(nameof(ManyDRM_Def),
        new Settings { dir = Y, mrg = Y, withB = n, withC = n, withS1S2 = 0x3, outRec = n },
        new Settings { dir = Y, mrg = Y, withB = Y, withC = n, withS1S2 = 0x3, outRec = n });

    internal TestMultiFormProc(DName name, NPath ns, int arityMin, int arityMax, Immutable.Array<InvocationForm> forms)
        : base(isFunc: false, name, ns, arityMin, arityMax, forms)
    {
    }

    private static TestMultiFormProc Create(string name, params Settings[] settings)
    {
        var bldrForms = Immutable.Array.CreateBuilder<InvocationForm>();
        foreach (var setting in settings)
            AddForms(bldrForms, setting);
        return CreateCore(new DName(name), bldrForms.ToImmutable());
    }

    private static TestMultiFormProc CreateCore(DName name, Immutable.Array<InvocationForm> forms)
    {
        int arityMin = forms.Min(form => form.ArityMin);
        int arityMax = forms.Max(form => form.ArityMax);
        return new TestMultiFormProc(name, NPath.Root.Append(new DName("Test")).Append(new DName("Proc")), arityMin, arityMax, forms);
    }
}
