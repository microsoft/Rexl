// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Symbolic;

/// <summary>
/// These are used to represent various "selection" operations in bound trees used in modules.
/// They do not participate in code generation. That is, they should be replaced, typically by
/// the <see cref="SymbolReducer"/>, before code generation is invoked.
/// 
/// Note also that these are not intended to be used in REXL code, but only at the "bound tree"
/// level, in <see cref="BndCallNode"/> instances.
/// </summary>
public sealed partial class SelectFunc : RexlOper
{
    /// <summary>
    /// The kind of selection function. See the descriptions of the individual function
    /// instances for details.
    /// </summary>
    public enum SelKind
    {
        One,
        Opt,
        Seq,
    }

    /// <summary>
    /// This form takes two parallel sequences as parameters:
    /// * The sequence of bit values indicating which item/key is selected.
    ///   Exactly one of these is expected to be true.
    /// * The sequence of items/keys, corresponding to the bit values.
    /// Its "return value" is the item/key selected according to the bit values.
    /// </summary>
    public static readonly SelectFunc Req = new SelectFunc("SelectOne", SelKind.One);

    /// <summary>
    /// This form, like <see cref="Req"/>, takes two parallel sequences as parameters.
    /// The difference is that it is legal for all bit values to be false, in which case
    /// the "return value" is null. The parameters are:
    /// * The sequence of bit values indicating which item/key is selected.
    ///   At most one of these is expected to be true.
    /// * The sequence of items/keys, corresponding to the bit values.
    /// Its "return value" is the item/key selected according to the bit values, if
    /// they are not all false, and null if they are all false.
    /// </summary>
    public static readonly SelectFunc Opt = new SelectFunc("SelectOpt", SelKind.Opt);

    /// <summary>
    /// This form takes two parallel sequences as parameters:
    /// * The sequence of bit values indicating which items/keys are selected.
    ///   Any subset of these can be true.
    /// * The sequence of items/keys, corresponding to the bit values.
    /// Its "return value" is the sub-sequence items/keys selected according to the bit values.
    /// </summary>
    public static readonly SelectFunc Seq = new SelectFunc("SelectSeq", SelKind.Seq);

    /// <summary>
    /// The kind of selection function this is.
    /// </summary>
    public readonly SelKind Kind;

    private SelectFunc(string name, SelKind kind)
        : base(isFunc: true, new DName(name), 2, 2)
    {
        Kind = kind;
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        return ArgTraitsSimple.Create(this, eager: false, carg);
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        var args = call.Args;
        Validation.Assert(args.Length == 2);

        var inds = args[0];
        if (inds.Type != DType.BitReq.ToSequence())
            return false;

        int cind;
        if (inds is BndSequenceNode bsn)
            cind = bsn.Items.Length;
        else if (inds is BndArrConstNode bacn)
            cind = bacn.Length;
        else
            return false;

        var vals = args[1];
        int cval;
        if (vals is BndArrConstNode arr)
            cval = arr.Length;
        else if (vals is BndSequenceNode bsnVals)
            cval = bsnVals.Items.Length;
        else
            return false;

        Validation.Assert(vals.Type.IsSequence);

        if (cind != cval)
            return false;

        var typeVal = vals.Type.ItemTypeOrThis;
        switch (Kind)
        {
        case SelKind.One:
            if (type != typeVal)
                return false;
            break;

        case SelKind.Opt:
            if (type != typeVal.ToOpt())
                return false;
            break;

        case SelKind.Seq:
            if (type != typeVal.ToSequence())
                return false;
            break;
        }

        full = false;
        return true;
    }
}
