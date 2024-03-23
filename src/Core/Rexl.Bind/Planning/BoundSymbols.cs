// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Bind;

/// <summary>
/// Information for a symbol declaration. This is information produced by binding a module
/// expression.
/// </summary>
public abstract class ModSym
{
    /// <summary>
    /// The index in the owning module.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// The kind of symbol, as explained in the documentation for <see cref="ModSymKind"/>.
    /// </summary>
    public ModSymKind SymKind { get; }

    /// <summary>
    /// The name of this symbol.
    /// </summary>
    public DName Name { get; }

    /// <summary>
    /// The type of this symbol.
    /// </summary>
    public DType Type { get; }

    /// <summary>
    /// The formula index for the "value" of the symbol.
    /// </summary>
    public int IfmaValue { get; }

    /// <summary>
    /// The number of formulas for this symbol. This is 1 for most symbol kinds, everything except
    /// free variables, where it may be up to 3.
    /// </summary>
    public abstract int FmaCount { get; }

    /// <summary>
    /// Returns true for parameters and computed constants.
    /// </summary>
    public bool IsConstantSym => SymKind.IsConstant();

    /// <summary>
    /// Returns true for free variables and computed variables.
    /// </summary>
    public bool IsVariableSym => SymKind.IsVariable();

    /// <summary>
    /// Returns true for computed variables, which includes the let, measure, and constraint
    /// symbol kinds.
    /// </summary>
    public bool IsComputedVarSym => SymKind.IsComputedVariable();

    public bool IsFreeVarSym => SymKind == ModSymKind.FreeVariable;
    public bool IsConstraintSym => SymKind == ModSymKind.Constraint;
    public bool IsMeasureSym => SymKind == ModSymKind.Measure;

    private protected ModSym(int index, ModSymKind kind, DName name, DType type, int ifmaVal)
    {
        Validation.Assert(kind != ModSymKind.None);
        Validation.Assert(name.IsValid);
        Validation.Assert(type.IsValid);
        Validation.Assert(ifmaVal >= 0);

        Index = index;
        SymKind = kind;
        Name = name;
        Type = type;
        IfmaValue = ifmaVal;
    }
}

/// <summary>
/// Information for a free variable declaration.
/// </summary>
public abstract class ModFreeVar : ModSym
{
    public abstract bool IsOpt { get; }

    /// <summary>
    /// The default value formula index. Always valid.
    /// </summary>
    public int FormulaDefault => IfmaValue;

    private protected ModFreeVar(int index, DName name, DType type, int ifma)
        : base(index, ModSymKind.FreeVariable, name, type, ifma)
    {
    }
}

/// <summary>
/// Information for a simple variable.
/// </summary>
public sealed partial class ModSimpleVar : ModFreeVar
{
    public override bool IsOpt => Type.IsOpt;

    public override int FmaCount { get; }

    /// <summary>
    /// The min value formula index or -1.
    /// </summary>
    public int FormulaFrom { get; }

    /// <summary>
    /// The max value formula index or -1.
    /// </summary>
    public int FormulaTo { get; }

    internal static ModSimpleVar Create(int index, DName name, DType type,
        int ibndMin, int ibndMax, int ibndVal)
    {
        Validation.Assert(ibndMin >= -1);
        Validation.Assert(ibndMax >= -1);
        Validation.Assert(ibndVal >= 0);

        return new ModSimpleVar(index, name, type, ibndMin, ibndMax, ibndVal);
    }

    private ModSimpleVar(int index, DName name, DType type,
            int ifmaFrom, int ifmaTo, int ifmaVal)
        : base(index, name, type, ifmaVal)
    {
        FmaCount = Util.ToNum(ifmaFrom >= 0) + Util.ToNum(ifmaTo >= 0) + 1;
        FormulaFrom = ifmaFrom;
        FormulaTo = ifmaTo;
    }
}

/// <summary>
/// Information for a variable that is an item from a sequence.
/// </summary>
public sealed partial class ModItemVar : ModFreeVar
{
    public override bool IsOpt { get; }

    public override int FmaCount => 2;

    /// <summary>
    /// The domain sequence formula index.
    /// </summary>
    public int FormulaIn { get; }

    internal static ModItemVar Create(int index, DName name, DType type,
        int ifmaSeq, int ifmaVal, bool isOpt)
    {
        Validation.Assert(ifmaSeq >= 0);
        Validation.Assert(ifmaVal >= 0);

        return new ModItemVar(index, name, type, ifmaSeq, ifmaVal, isOpt);
    }

    private ModItemVar(int index, DName name, DType type,
            int ifmaIn, int ifmaVal, bool isOpt)
        : base(index, name, type, ifmaVal)
    {
        Validation.Assert(ifmaIn >= 0);
        Validation.Assert(!isOpt || Type.IsOpt);

        FormulaIn = ifmaIn;
        IsOpt = isOpt;
    }
}

/// <summary>
/// Information for a parameter, computed constant, or computed variable. These all have
/// a single formula, namely the value/default.
/// </summary>
public abstract class ModFmaSym : ModSym
{
    public override int FmaCount => 1;

    private protected ModFmaSym(int index, ModSymKind kind, DName name, DType type, int ifmaVal)
        : base(index, kind, name, type, ifmaVal)
    {
    }
}

/// <summary>
/// Information for a parameter or computed constant.
/// </summary>
public sealed partial class ModConstant : ModFmaSym
{
    internal ModConstant(int index, DName name, DType type, int ifmaVal, bool isParm)
        : base(index, isParm ? ModSymKind.Parameter : ModSymKind.Constant, name, type, ifmaVal)
    {
    }
}

/// <summary>
/// Information for a computed variable, which can be a "let", "measure", or "constraint".
/// </summary>
public sealed partial class ModComputedVar : ModFmaSym
{
    internal ModComputedVar(int index, DName name, DType type, int ifmaVal, bool isMeasure, bool isConstraint)
        : base(index, isMeasure ? ModSymKind.Measure : isConstraint ? ModSymKind.Constraint : ModSymKind.Let,
            name, type, ifmaVal)
    {
        Validation.Assert(!isMeasure || !isConstraint);
        Validation.Assert(!isConstraint || Type.RootType == DType.BitReq);
    }
}
