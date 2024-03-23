// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Rexl;

/// <summary>
/// Indicates the kind of symbol in a module. The kinds are:
/// * Parameter: a constant that can be "set" from outside and whose default is determined by evaluating its formula.
/// * Computed Constant: a constant whose value is determined by evaluating its formula. It is not "settable".
/// * Free Variable: a variable that can be "set" from the outside and whose default is determined by its formulas.
///   There are several forms of free variables. The forms indicate the kind of variable (item in a sequence,
///   subsequence, standard value, etc) and formulas that indicate their domain and default value.
/// * Computed variables: a variable computed from other symbols. There are three forms of these: measure,
///   constraint, and "let" (neither measure nor constraint).
/// </summary>
public enum ModSymKind : byte
{
    /// <summary>
    /// Not a valid kind.
    /// </summary>
    None,

    /// <summary>
    /// Parameter, meaning it is a "constant" that is customizable. That is,
    /// its value can either be dictacted from the outside or the result of
    /// evaluating its (default) formula.
    /// </summary>
    Parameter,

    /// <summary>
    /// A pure formula based constant that is NOT customizable. That is, its
    /// value is always from evaluating its formula.
    /// </summary>
    Constant,

    /// <summary>
    /// A free variable, whose value may be from its default, or settable from outside,
    /// or the result of automatic optimization. There are many forms of free variables.
    /// </summary>
    FreeVariable,

    /// <summary>
    /// A computed variable that is not identified as a measure or constraint. All computed
    /// variables get their value from evaluation of their formula.
    /// </summary>
    Let,

    /// <summary>
    /// A computed variable that is identified as a measure. Such a computed variable
    /// can be used as an optimization objective.
    /// </summary>
    Measure,

    /// <summary>
    /// A computed variable that is identified as a constraint. Such a computed variable
    /// can be used as a requirement when optimizing. Its root type is generally boolean.
    /// </summary>
    Constraint,
}

/// <summary>
/// Utilities for the <see cref="ModSymKind"/> enum.
/// </summary>
public static class ModSymKindUtil
{
    /// <summary>
    /// Returns whether the value is a valid module symbol kind.
    /// </summary>
    public static bool IsValid(this ModSymKind value)
    {
        switch (value)
        {
        case ModSymKind.Parameter:
        case ModSymKind.Constant:
        case ModSymKind.FreeVariable:
        case ModSymKind.Let:
        case ModSymKind.Measure:
        case ModSymKind.Constraint:
            return true;
        }
        return false;
    }

    /// <summary>
    /// Returns whether the module symbol kind is a variable kind.
    /// </summary>
    public static bool IsVariable(this ModSymKind value)
    {
        switch (value)
        {
        case ModSymKind.FreeVariable:
        case ModSymKind.Let:
        case ModSymKind.Measure:
        case ModSymKind.Constraint:
            return true;
        }
        return false;
    }

    /// <summary>
    /// Returns whether the module symbol kind is a computed variable kind.
    /// </summary>
    public static bool IsComputedVariable(this ModSymKind value)
    {
        switch (value)
        {
        case ModSymKind.Let:
        case ModSymKind.Measure:
        case ModSymKind.Constraint:
            return true;
        }
        return false;
    }

    /// <summary>
    /// Returns whether the module symbol kind is a constant kind.
    /// </summary>
    public static bool IsConstant(this ModSymKind value)
    {
        switch (value)
        {
        case ModSymKind.Parameter:
        case ModSymKind.Constant:
            return true;
        }
        return false;
    }

    /// <summary>
    /// Returns whether the module symbol kind is settable, which is true for
    /// parameters and free variables.
    /// </summary>
    public static bool IsSettable(this ModSymKind value)
    {
        switch (value)
        {
        case ModSymKind.Parameter:
        case ModSymKind.FreeVariable:
            return true;
        }
        return false;
    }

    /// <summary>
    /// Maps from <see cref="ModSymKind"/> to "verbose" string.
    /// </summary>
    public static string ToStr(this ModSymKind value)
    {
        switch (value)
        {
        case ModSymKind.Parameter: return "param";
        case ModSymKind.Constant: return "const";
        case ModSymKind.FreeVariable: return "var";
        case ModSymKind.Let: return "let";
        case ModSymKind.Measure: return "msr";
        case ModSymKind.Constraint: return "con";
        }
        return "<invalid>";
    }

    /// <summary>
    /// Maps from <see cref="ModSymKind"/> to one short "code" for embedding in <see cref="DType"/>
    /// string encodings.
    /// </summary>
    public static string ToCode(this ModSymKind value)
    {
        switch (value)
        {
        case ModSymKind.Parameter: return "P";
        case ModSymKind.Constant: return "K";
        case ModSymKind.FreeVariable: return "V";
        case ModSymKind.Let: return "L";
        case ModSymKind.Measure: return "M";
        case ModSymKind.Constraint: return "C";
        }
        return "_";
    }
}
