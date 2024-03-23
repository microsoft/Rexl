// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

using Microsoft.Rexl.Lex;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Parse;

using Conditional = System.Diagnostics.ConditionalAttribute;
using ExprTuple = Immutable.Array<ExprNode>;
using IdentTuple = Immutable.Array<Identifier>;
using NameTuple = Immutable.Array<DName>;
using SliceTuple = Immutable.Array<SliceItemNode>;
using StmtTuple = Immutable.Array<StmtNode>;

/// <summary>
/// Root comparison operator. The variations come via <see cref="CompareModifiers"/>.
/// This is a component of a <see cref="CompareOp"/>.
/// </summary>
public enum CompareRoot : byte
{
    None,

    Equal = 1,
    Less = 2,
    LessEqual = 3,
    Greater = 4,
    GreaterEqual = 5,
}

/// <summary>
/// Modifiers for comparison operators representing variations of a root comparison operator.
/// This is a component of a <see cref="CompareOp"/> value, together with a <see cref="CompareRoot"/>.
/// </summary>
[Flags]
public enum CompareModifiers : byte
{
    None = 0x00,
    Not = 0x01,
    Ci = 0x02,
    Strict = 0x04,
}

/// <summary>
/// This has a <see cref="CompareRoot"/> component and a <see cref="CompareModifiers"/> component. When
/// the root is <see cref="CompareRoot.None"/>, the modifiers must be <see cref="CompareModifiers.None"/>.
/// </summary>
public struct CompareOp
{
    /// <summary>
    /// Represents the lack of a comparison operator.
    /// </summary>
    public static CompareOp None => default;

    /// <summary>
    /// The equality operator with no modifiers.
    /// </summary>
    public static readonly CompareOp Equal = new CompareOp(CompareRoot.Equal, default);

    /// <summary>
    /// Maps from encoded bits to name and symbolic representations. Used by <see cref="GetStr"/> and
    /// <see cref="ToString"/>.
    /// </summary>
    private static readonly Dictionary<byte, (string name, string sym)> _bitsToStrs = BuildStrMap();

    /// <summary>
    /// Builds the map from bit representation to name and symbolic representations.
    /// </summary>
    private static Dictionary<byte, (string name, string sym)> BuildStrMap()
    {
        var map = new List<(uint bits, string name, string sym)>();

        // Non-strict.
        map.Add(((uint)CompareRoot.Equal, "Equal", "@="));
        map.Add(((uint)CompareRoot.Less, "Less", "@<"));
        map.Add(((uint)CompareRoot.LessEqual, "LessEqual", "@<="));
        map.Add(((uint)CompareRoot.Greater, "Greater", "@>"));
        map.Add(((uint)CompareRoot.GreaterEqual, "GreaterEqual", "@>="));

        // Strict.
        uint hi = (uint)CompareModifiers.Strict << 4;
        map.Add((hi | (uint)CompareRoot.Equal, "StrictEqual", "$="));
        map.Add((hi | (uint)CompareRoot.Less, "StrictLess", "$<"));
        map.Add((hi | (uint)CompareRoot.LessEqual, "StrictLessEqual", "$<="));
        map.Add((hi | (uint)CompareRoot.Greater, "StrictGreater", "$>"));
        map.Add((hi | (uint)CompareRoot.GreaterEqual, "StrictGreaterEqual", "$>="));

        // Case insensitive.
        int count = map.Count;
        hi = (uint)CompareModifiers.Ci << 4;
        for (int i = 0; i < count; i++)
        {
            var (bits, name, sym) = map[i];
            map.Add((bits | hi, name + "Ci", "~" + sym));
        }

        // Not.
        count = map.Count;
        hi = (uint)CompareModifiers.Not << 4;
        for (int i = 0; i < count; i++)
        {
            var (bits, name, sym) = map[i];
            map.Add((bits | hi, "Not" + name, "!" + sym));
        }

        return map.ToDictionary(t => (byte)t.bits, t => (t.name, t.sym));
    }

    // The one and only instance field. The low nibble is the root and high nibble is
    // the modifiers.
    private readonly byte _bits;

    /// <summary>
    /// The internal constructor. Other assemblies use <see cref="FromParts(CompareRoot, CompareModifiers)"/>.
    /// </summary>
    internal CompareOp(CompareRoot root, CompareModifiers mods)
    {
        Validation.Assert(root.IsValid());
        Validation.Assert(mods.IsValid());
        Validation.Assert(mods == default || root != default);
        _bits = (byte)((uint)root | (uint)mods << 4);
        AssertValid();
    }

    [Conditional("DEBUG")]
    private void AssertValid()
    {
#if DEBUG
        Validation.Assert(((CompareRoot)(_bits & 0x0F)).IsValid());
        Validation.Assert(((CompareModifiers)(_bits >> 4)).IsValid());
        Validation.Assert(_bits == 0 || (_bits & 0x0F) != 0);
#endif
    }

    /// <summary>
    /// Assemble a <see cref="CompareOp"/> from the given <paramref name="root"/> and
    /// <paramref name="mods"/>. This requires that <paramref name="root"/> is not "none"
    /// unless <paramref name="mods"/> is also "none".
    /// </summary>
    public static CompareOp FromParts(CompareRoot root, CompareModifiers mods)
    {
        Validation.BugCheckParam(root.IsValid(), nameof(root));
        Validation.BugCheckParam(mods.IsValid(), nameof(mods));
        Validation.BugCheckParam(root != default || mods == default, nameof(root));
        return new CompareOp(root, mods);
    }

    /// <summary>
    /// The root component.
    /// </summary>
    public CompareRoot Root => (CompareRoot)(_bits & 0x0F);

    /// <summary>
    /// The modifiers component.
    /// </summary>
    public CompareModifiers Mods => (CompareModifiers)(_bits >> 4);

    /// <summary>
    /// Returns whether the "not" flag is set.
    /// </summary>
    public bool IsNot => Mods.IsNot();

    /// <summary>
    /// Returns whether the "case insensitive" flag is set.
    /// </summary>
    public bool IsCi => Mods.IsCi();

    /// <summary>
    /// Returns whether the "strict" flag is set.
    /// </summary>
    public bool IsStrict => Mods.IsStrict();

    /// <summary>
    /// Whether the root operator is equal and the not flag is claear.
    /// </summary>
    public bool IsEqualPos => Root == CompareRoot.Equal && !IsNot;

    /// <summary>
    /// Whether the root operator is equal and the not flag is set.
    /// </summary>
    public bool IsEqualNeg => Root == CompareRoot.Equal && IsNot;

    /// <summary>
    /// Whether the root operator is orderd (not equal or none).
    /// </summary>
    public bool IsOrdered => Root.IsOrdered();

    /// <summary>
    /// Whether the root operator ir ordered and the not flag is clear.
    /// </summary>
    public bool IsOrderedPos => Root.IsOrdered() && !Mods.IsNot();

    /// <summary>
    /// Whether the root operator is ordered and the not flag is set.
    /// </summary>
    public bool IsOrderedNeg => Root.IsOrdered() && Mods.IsNot();

    /// <summary>
    /// Get the two components: root and modifiers.
    /// </summary>
    public (CompareRoot root, CompareModifiers mods) GetParts() => (Root, Mods);

    /// <summary>
    /// Return the variant with the same root and modifiers except with the case insensitive
    /// flag clear.
    /// </summary>
    public CompareOp ClearCi() => new CompareOp(Root, Mods & ~CompareModifiers.Ci);

    /// <summary>
    /// Return the variant with the same root and modifiers except with the strict
    /// flag clear.
    /// </summary>
    public CompareOp ClearStrict() => new CompareOp(Root, Mods & ~CompareModifiers.Strict);

    /// <summary>
    /// Return the simplified operator given the operand types. The two types may differ in
    /// optness but otherwise must be the same. If the type doesn't contain text, clears
    /// the ci modifier. Similarly clears the strict modifier if appropriate. If the not
    /// modifier is present but strict is not, and the root operator is ordered, invokes
    /// <see cref="SimplifyForTotalOrder"/>.
    /// </summary>
    public CompareOp SimplifyForType(DType type0, DType type1)
    {
        Validation.Assert(type0.ToOpt() == type1.ToOpt());

        if (Root == CompareRoot.None)
            return None;

        var op = this;
        if (op.IsCi && !type0.HasText)
            op = op.ClearCi();
        if (op.IsStrict && !type0.HasFloat)
        {
            // NaN isn't an issue, but null may be.
            bool hasOpt0 = type0.HasOpt;
            bool hasOpt1 = type1.HasOpt;
            if (hasOpt0)
            {
                if (!hasOpt1 && !op.IsOrdered)
                    op = op.ClearStrict();
            }
            else if (!hasOpt1 || !IsOrdered)
                op = op.ClearStrict();
        }

        if (!op.IsStrict)
            op = op.SimplifyForTotalOrder();
        return op;
    }

    /// <summary>
    /// For a totally ordered type, simplify. In particular, the result will have
    /// <see cref="CompareOp.IsOrderedNeg"/> be false. For example, this maps "not less"
    /// to "greater or equal". Note that this is quite different than clearing the
    /// <see cref="CompareModifiers.Not"/> flag.
    /// </summary>
    public CompareOp SimplifyForTotalOrder()
    {
        var mods = Mods;
        Validation.BugCheck(!mods.IsStrict());

        if (!mods.IsNot())
            return this;

        var root = Root;
        switch (root)
        {
        default:
            return this;

        case CompareRoot.Less: root = CompareRoot.GreaterEqual; break;
        case CompareRoot.LessEqual: root = CompareRoot.Greater; break;
        case CompareRoot.Greater: root = CompareRoot.LessEqual; break;
        case CompareRoot.GreaterEqual: root = CompareRoot.Less; break;
        }

        mods &= ~CompareModifiers.Not;
        return new CompareOp(root, mods);
    }

    /// <summary>
    /// Return the operator that is equivalent when the argument order is reversed. For example,
    /// "less" maps to "greater" (with the same modifiers).
    /// </summary>
    public CompareOp GetReverse()
    {
        var (root, mods) = GetParts();
        switch (root)
        {
        case CompareRoot.Less: root = CompareRoot.Greater; break;
        case CompareRoot.LessEqual: root = CompareRoot.GreaterEqual; break;
        case CompareRoot.Greater: root = CompareRoot.Less; break;
        case CompareRoot.GreaterEqual: root = CompareRoot.LessEqual; break;
        case CompareRoot.Equal: return this;
        default:
            Validation.Assert(_bits == 0);
            return this;
        }

        return new CompareOp(root, mods);
    }

    /// <summary>
    /// Get the symbolic operator representation, for example "!~$<".
    /// </summary>
    public string GetStr()
    {
        AssertValid();

        if (_bitsToStrs.TryGetValue(_bits, out var pair))
            return pair.sym;
        Validation.Assert(_bits == 0);
        return "&&";
    }

    /// <summary>
    /// Get the name representation, for example "NotLessStrictCi".
    /// </summary>
    public override string ToString()
    {
        AssertValid();

        if (_bitsToStrs.TryGetValue(_bits, out var pair))
            return pair.name;
        Validation.Assert(_bits == 0);
        return "None";
    }

    public static bool operator ==(CompareOp a, CompareOp b)
    {
        a.AssertValid();
        b.AssertValid();
        return a._bits == b._bits;
    }

    public static bool operator !=(CompareOp a, CompareOp b)
    {
        a.AssertValid();
        b.AssertValid();
        return a._bits != b._bits;
    }

    public override int GetHashCode()
    {
        return _bits;
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        if (obj is not CompareOp op)
            return false;
        return _bits == op._bits;
    }
}

/// <summary>
/// Internal extension method helpers for comparison ops.
/// </summary>
public static class CompareOpUtil
{
    /// <summary>
    /// Returns whether the <paramref name="root"/> is a valid value. Note that
    /// <see cref="CompareRoot.None"/> is considered valid.
    /// </summary>
    public static bool IsValid(this CompareRoot root)
    {
        switch (root)
        {
        case CompareRoot.None:
        case CompareRoot.Equal:
        case CompareRoot.Less:
        case CompareRoot.LessEqual:
        case CompareRoot.Greater:
        case CompareRoot.GreaterEqual:
            return true;
        }
        return false;
    }

    /// <summary>
    /// Returns whether <paramref name="root"/> is an ordered comparison root, that is, neither
    /// <see cref="CompareRoot.Equal"/> nor <see cref="CompareRoot.None"/>.
    /// </summary>
    public static bool IsOrdered(this CompareRoot root)
    {
        Validation.Assert(root.IsValid());
        switch (root)
        {
        case CompareRoot.Less:
        case CompareRoot.LessEqual:
        case CompareRoot.Greater:
        case CompareRoot.GreaterEqual:
            return true;
        }
        return false;
    }

    /// <summary>
    /// Returns whether <paramref name="mods"/> is a valid value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValid(this CompareModifiers mods)
    {
        return (uint)mods < 0x08;
    }

    /// <summary>
    /// Returns whether <paramref name="mods"/> includes the <see cref="CompareModifiers.Not"/> flag.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNot(this CompareModifiers mods)
    {
        Validation.Assert(mods.IsValid());
        return (mods & CompareModifiers.Not) != 0;
    }

    /// <summary>
    /// Returns whether <paramref name="mods"/> includes the <see cref="CompareModifiers.Ci"/> flag.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsCi(this CompareModifiers mods)
    {
        Validation.Assert(mods.IsValid());
        return (mods & CompareModifiers.Ci) != 0;
    }

    /// <summary>
    /// Returns whether <paramref name="mods"/> includes the <see cref="CompareModifiers.Strict"/> flag.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsStrict(this CompareModifiers mods)
    {
        Validation.Assert(mods.IsValid());
        return (mods & CompareModifiers.Strict) != 0;
    }
}

/// <summary>
/// Binary operators.
/// </summary>
public enum BinaryOp
{
    // These are allowed in variadic bound nodes.
    Or,
    Xor,
    And,
    BitOr,
    BitXor,
    BitAnd,
    StrConcat,
    TupleConcat,
    RecordConcat,
    SeqConcat,
    Add,
    Mul,

    // From here on, these are NOT allowed in variadic bound nodes.

    // In BndBinaryOpNode.
    IntDiv,
    IntMod,
    Shl,
    Shri,
    Shru,
    Power,
    In,
    InNot,
    InCi,
    InCiNot,
    Has,
    HasNot,
    HasCi,
    HasCiNot,
    Coalesce,
    ChronoAdd,
    ChronoSub,
    ChronoMul,
    ChronoDiv,
    ChronoMod,
    // REVIEW: Should these be variadic?
    Min,
    Max,

    // Eliminated by binder.
    Sub, // Binder folds into Variadic with inv bit.
    Div, // Binder folds into Variadic with inv bit.
    Pipe, // Binder substitutes.
    Shr, // Binder resolves to Shri or Shru.
    GenConcat, // Binder resolves to StrConcat, TupleConcat, or record concat.

    Error,
}

internal static class BinaryOpUtil
{
    public static bool IsAssociative(this BinaryOp op)
    {
        switch (op)
        {
        case BinaryOp.Or:
        case BinaryOp.Xor:
        case BinaryOp.And:
        case BinaryOp.BitOr:
        case BinaryOp.BitXor:
        case BinaryOp.BitAnd:
        case BinaryOp.StrConcat:
        case BinaryOp.TupleConcat:
        case BinaryOp.RecordConcat:
        case BinaryOp.SeqConcat:
        case BinaryOp.Add:
        case BinaryOp.Mul:
        case BinaryOp.Min:
        case BinaryOp.Max:
            return true;
        }

        return false;
    }

    /// <summary>
    /// Given a <see cref="BinaryOp"/> <paramref name="op"/> returns true
    /// if it signifies a binary operator containing In or Has.
    /// </summary>
    public static bool IsInHas(this BinaryOp op)
    {
        switch (op)
        {
        case BinaryOp.In:
        case BinaryOp.InNot:
        case BinaryOp.InCi:
        case BinaryOp.InCiNot:
        case BinaryOp.Has:
        case BinaryOp.HasNot:
        case BinaryOp.HasCi:
        case BinaryOp.HasCiNot:
            return true;
        }
        return false;
    }

    /// <summary>
    /// Given a <see cref="BinaryOp"/> <paramref name="op"/> where <see cref="IsInHas(BinaryOp)"/> is true,
    /// returns true if it signifies a case-insensitive operator.
    /// </summary>
    public static bool IsInHasCi(this BinaryOp op)
    {
        Validation.Assert(op.IsInHas());

        switch (op)
        {
        case BinaryOp.InCi:
        case BinaryOp.InCiNot:
        case BinaryOp.HasCi:
        case BinaryOp.HasCiNot:
            return true;
        }
        return false;
    }

    /// <summary>
    /// Given a <see cref="BinaryOp"/> <paramref name="op"/> where <see cref="IsInHas(BinaryOp)"/> is true,
    /// returns true if it signifies a positive (without Not) operator.
    /// </summary>
    public static bool IsInHasPos(this BinaryOp op)
    {
        Validation.Assert(op.IsInHas());

        switch (op)
        {
        case BinaryOp.In:
        case BinaryOp.InCi:
        case BinaryOp.Has:
        case BinaryOp.HasCi:
            return true;
        }
        return false;
    }

    /// <summary>
    /// Given a <see cref="BinaryOp"/> <paramref name="op"/> where <see cref="IsInHas(BinaryOp)"/> is true 
    /// and <see cref="IsInHasCi(BinaryOp)"/> is false, returns its case-insensitive counterpart.
    /// </summary>
    public static BinaryOp GetInHasCi(this BinaryOp op)
    {
        Validation.Assert(op.IsInHas());
        Validation.Assert(!op.IsInHasCi());
        op += (BinaryOp.HasCi - BinaryOp.Has);
        Validation.Assert(op.IsInHasCi());
        return op;
    }

    /// <summary>
    /// Given a <see cref="BinaryOp"/> <paramref name="op"/> where <see cref="IsInHasCi(BinaryOp)"/> is true,
    /// returns its case-sensitive counterpart.
    /// </summary>
    public static BinaryOp GetInHasCs(this BinaryOp op)
    {
        Validation.Assert(op.IsInHasCi());
        op += (BinaryOp.Has - BinaryOp.HasCi);
        Validation.Assert(op.IsInHas() && !op.IsInHasCi());
        return op;
    }

    /// <summary>
    /// Given a <see cref="BinaryOp"/> <paramref name="op"/> where <see cref="IsInHasPos(BinaryOp)"/> is true,
    /// returns its negated counterpart.
    /// </summary>
    public static BinaryOp GetInHasNeg(this BinaryOp op)
    {
        Validation.Assert(op.IsInHas());
        Validation.Assert(op.IsInHasPos());
        op += (BinaryOp.HasNot - BinaryOp.Has);
        Validation.Assert(op.IsInHas() && !op.IsInHasPos());
        return op;
    }
}

/// <summary>
/// Unary operators, both prefix and postfix.
/// </summary>
public enum UnaryOp
{
    // Prefix.
    Not,
    BitNot,
    Negate,
    Posate,

    // Postfix.
    Percent,
}

/// <summary>
/// The kind of definition. <see cref="None"/> indicates standard. The rest are
/// for task bodies. The parser has this baked into it.
/// </summary>
public enum DefnKind
{
    None,

    // In task bodies.
    Publish,
    Primary,
    Stream,

    // For assigning to "this".
    This,
}

/// <summary>
/// Parse node precedence.
/// </summary>
public enum Precedence : byte
{
    StmtList,
    Stmt,

    SymList, // List of symbol declarations for module.
    ExprList, // For ExprList, ie, invocation arguments.

    // Putting Error first forces an error to be parenthesized when used as a child.
    Error,
    Expr,

    Pipe,
    If,
    Coalesce,
    Or,
    Xor,
    And,
    Not,
    Compare,
    InHas,

    // REVIEW: Not clear where some of these should go.
    Concat,

    MinMax,

    BitOr,
    BitXor,
    BitAnd,
    BitNot,

    Shift,
    Add,
    Mul,

    PrefixUnary,
    Power,
    PostfixUnary,
    Primary,
    Atomic,

    Lim,
}

public static class PrecedenceUtil
{
    public static Precedence ToPrecedence(this BinaryOp op)
    {
        return op switch
        {
            BinaryOp.Or => Precedence.Or,
            BinaryOp.Xor => Precedence.Xor,
            BinaryOp.And => Precedence.And,
            BinaryOp.BitOr => Precedence.BitOr,
            BinaryOp.BitXor => Precedence.BitXor,
            BinaryOp.BitAnd => Precedence.BitAnd,
            BinaryOp.StrConcat or
            BinaryOp.TupleConcat or
            BinaryOp.RecordConcat or
            BinaryOp.SeqConcat or
            BinaryOp.GenConcat => Precedence.Concat,
            BinaryOp.Add or
            BinaryOp.Sub => Precedence.Add,
            BinaryOp.Mul or
            BinaryOp.IntDiv or
            BinaryOp.IntMod or
            BinaryOp.Div => Precedence.Mul,
            BinaryOp.Shl or
            BinaryOp.Shri or
            BinaryOp.Shru or
            BinaryOp.Shr => Precedence.Shift,
            BinaryOp.Power => Precedence.Power,
            BinaryOp.In or
            BinaryOp.InNot or
            BinaryOp.InCi or
            BinaryOp.InCiNot or
            BinaryOp.Has or
            BinaryOp.HasCi or
            BinaryOp.HasCiNot or
            BinaryOp.HasNot => Precedence.InHas,
            BinaryOp.Min or
            BinaryOp.Max => Precedence.MinMax,
            BinaryOp.Error => Precedence.Error,
            BinaryOp.Coalesce => Precedence.Coalesce,
            BinaryOp.ChronoAdd or
            BinaryOp.ChronoSub or
            BinaryOp.ChronoMul or
            BinaryOp.ChronoDiv or
            BinaryOp.ChronoMod => throw Validation.Except(), // Never created by the parser.
            BinaryOp.Pipe => Precedence.Pipe
        };
    }

    public static Precedence ToPrecedence(this UnaryOp op)
    {
        return op switch
        {
            UnaryOp.Not => Precedence.Not,
            UnaryOp.BitNot or
            UnaryOp.Negate or
            UnaryOp.Posate => Precedence.PrefixUnary,
            UnaryOp.Percent => Precedence.PostfixUnary
        };
    }
}

/// <summary>
/// Directive kinds.
/// </summary>
public enum Directive
{
    None = 0,

    // For sorting.
    Ci,
    Up,
    Down,
    UpCi,
    DownCi,

    // For keys.
    Eq,
    EqCi,
    Key,

    // For GroupBy.
    Agg,
    Map,
    Auto,

    // For Guard.
    With,
    Guard,

    // General.
    If,
    While,
    Else,

    // For MultiFormFunc.
    Top
}

/// <summary>
/// Extension method helpers for directives.
/// </summary>
public static class DirectiveUtil
{
    /// <summary>
    /// Map from token kind to corresponding directive.
    /// </summary>
    public static Directive ToDir(this TokKind tid)
    {
        switch (tid)
        {
        case TokKind.DirCi: return Directive.Ci;
        case TokKind.DirUp: return Directive.Up;
        case TokKind.DirDown: return Directive.Down;
        case TokKind.DirUpCi: return Directive.UpCi;
        case TokKind.DirDownCi: return Directive.DownCi;
        case TokKind.DirEq: return Directive.Eq;
        case TokKind.DirEqCi: return Directive.EqCi;
        case TokKind.DirKey: return Directive.Key;
        case TokKind.DirAgg: return Directive.Agg;
        case TokKind.DirMap: return Directive.Map;
        case TokKind.DirAuto: return Directive.Auto;
        case TokKind.DirGuard: return Directive.Guard;
        case TokKind.DirWith: return Directive.With;
        case TokKind.DirIf: return Directive.If;
        case TokKind.DirWhile: return Directive.While;
        case TokKind.DirElse: return Directive.Else;
        case TokKind.DirTop: return Directive.Top;
        }

        return Directive.None;
    }

    /// <summary>
    /// Map from directive to a corresponding token kind.
    /// </summary>
    public static TokKind ToTokKind(this Directive dir)
    {
        return dir switch
        {
            Directive.None => TokKind.None,
            Directive.Ci => TokKind.DirCi,
            Directive.Up => TokKind.DirUp,
            Directive.Down => TokKind.DirDown,
            Directive.UpCi => TokKind.DirUpCi,
            Directive.DownCi => TokKind.DirDownCi,
            Directive.Eq => TokKind.DirEq,
            Directive.EqCi => TokKind.DirEqCi,
            Directive.Key => TokKind.DirKey,
            Directive.Agg => TokKind.DirAgg,
            Directive.Map => TokKind.DirMap,
            Directive.Auto => TokKind.DirAuto,
            Directive.Guard => TokKind.DirGuard,
            Directive.With => TokKind.DirWith,
            Directive.If => TokKind.DirIf,
            Directive.While => TokKind.DirWhile,
            Directive.Else => TokKind.DirElse,
            Directive.Top => TokKind.DirTop,
        };
    }

    /// <summary>
    /// Map from directive to preferred rexl source text.
    /// </summary>
    public static string ToSrcText(this Directive dir)
    {
        var tid = dir.ToTokKind();
        if (tid == TokKind.None)
            return null;
        return RexlLexer.Instance.GetFixedText(tid);
    }

    /// <summary>
    /// Given a valid sort directive <paramref name="dir"/>, sets the bool variables
    /// <paramref name="ci"/> and <paramref name="isDown"/> appropriately. For
    /// <paramref name="isDown"/>, this will either override its original value or
    /// leave it unchanged.
    /// </summary>
    public static void SetSortFlags(this Directive dir, out bool ci, ref bool isDown)
    {
        ci = false;
        switch (dir)
        {
        case Directive.None:
            break;
        case Directive.Ci:
            ci = true;
            break;
        case Directive.Up:
            isDown = false;
            break;
        case Directive.UpCi:
            ci = true;
            isDown = false;
            break;
        case Directive.Down:
            isDown = true;
            break;
        case Directive.DownCi:
            ci = true;
            isDown = true;
            break;
        default:
            throw Validation.BugExcept("Bad sort directive");
        }
    }

    /// <summary>
    /// Return whether the directive indicates case insensitive.
    /// </summary>
    public static bool IsCi(this Directive dir)
    {
        switch (dir)
        {
        case Directive.Ci:
        case Directive.EqCi:
        case Directive.UpCi:
        case Directive.DownCi:
            return true;
        }
        return false;
    }

    /// <summary>
    /// Return whether the directive indicates equality vs more strict matching.
    /// </summary>
    public static bool IsEq(this Directive dir)
    {
        switch (dir)
        {
        case Directive.Eq:
        case Directive.EqCi:
            return true;
        }
        return false;
    }
}

/// <summary>
/// For RexlNode extension methods and utilities. The set of CastXxx methods may grow over time.
/// If you find yourself using an explicit cast, eg (RecordNode)node, don't! Instead either use
/// the generic Cast method or add the appropriate method here.
/// </summary>
public static class RexlNodeUtil
{
    public static T Cast<T>(this RexlNode node)
        where T : RexlNode
    {
        Validation.Assert(node is T);
        return (T)node;
    }
}

/// <summary>
/// Base class for all parse nodes. All instances are created by the parser, so the constructors
/// are all internal.
/// </summary>
public abstract class RexlNode
{
    /// <summary>
    /// Interlock-incremented to assign <see cref="Ordinal"/>.
    /// </summary>
    private static long _ordAuto;

    /// <summary>
    /// This is unique across the process, so nodes can be used as keys in red-black-trees.
    /// REVIEW: Is there a better way?
    /// </summary>
    internal long Ordinal { get; }

    /// <summary>
    /// The id of this node. Internal as it should only be used in this assembly.
    /// REVIEW: This is only really used for test base-line display. Should we toss it?
    /// </summary>
    internal int Id { get; }

    /// <summary>
    /// This is a token associated with the node. It is never null. It is typically a token that
    /// distinguishes this node kind. For example, for a CallNode, it is the open paren.
    /// </summary>
    public Token Token { get; }

    /// <summary>
    /// The depth of the tree under this node. Internal until we know we need it publicly.
    /// </summary>
    internal int TreeDepth { get; }

    private protected static int GetDepth(RexlNode child)
    {
        return child?.TreeDepth ?? 0;
    }

    private protected static int GetDepth(RexlNode child0, RexlNode child1)
    {
        return Math.Max(child0?.TreeDepth ?? 0, child1?.TreeDepth ?? 0);
    }

    private protected static int GetDepth<TChild>(Immutable.Array<TChild> children)
        where TChild : RexlNode
    {
        if (children.IsDefault)
            return 0;

        int depth = 0;
        foreach (var child in children)
        {
            int cur = child?.TreeDepth ?? 0;
            if (depth < cur)
                depth = cur;
        }
        return depth;
    }

    private protected static int GetDepth(params RexlNode[] children)
    {
        int depth = 0;
        foreach (var child in children)
        {
            int cur = child?.TreeDepth ?? 0;
            if (depth < cur)
                depth = cur;
        }
        return depth;
    }

    private protected RexlNode(ref int idNext, Token tok, int depth)
    {
        Validation.Assert(idNext >= 0);
        Validation.AssertValue(tok);
        Validation.Assert(depth >= 1);

        Ordinal = Interlocked.Increment(ref _ordAuto);
        Id = idNext++;
        Token = tok;
        TreeDepth = depth;
    }

    /// <summary>
    /// Helper to get an array of <see cref="DName"/> from an array of <see cref="Identifier"/>.
    /// </summary>
    private protected static NameTuple GetNamesFromIdents(IdentTuple idents)
    {
        Validation.Assert(!idents.IsDefault);

        switch (idents.Length)
        {
        case 0:
            return NameTuple.Empty;
        case 1:
            return NameTuple.Create(idents[0].Name);
        default:
            var bldr = NameTuple.CreateBuilder(idents.Length, init: true);
            for (int i = 0; i < idents.Length; i++)
                bldr[i] = idents[i].Name;
            return bldr.ToImmutable();
        }
    }

    public abstract void Accept(RexlTreeVisitor visitor);

    private protected void AcceptChild(RexlTreeVisitor visitor, RexlNode child)
    {
        Validation.AssertValue(visitor);
        Validation.AssertValue(child);
        child.Accept(visitor);
    }

    private protected void AcceptChildOpt(RexlTreeVisitor visitor, RexlNode child)
    {
        Validation.AssertValue(visitor);
        Validation.AssertValueOrNull(child);
        if (child != null)
            child.Accept(visitor);
    }

    private protected void AcceptChildren<TChild>(RexlTreeVisitor visitor, Immutable.Array<TChild> children)
        where TChild : RexlNode
    {
        Validation.AssertValue(visitor);
        if (!children.IsDefault)
        {
            foreach (var child in children)
                AcceptChild(visitor, child);
        }
    }

    /// <summary>
    /// A range or source character positions associated with this node.
    /// </summary>
    public virtual SourceRange GetRange()
    {
        return Token.Range;
    }

    /// <summary>
    /// The complete range of source characters that were used to build this node.
    /// </summary>
    public abstract SourceRange GetFullRange();

    /// <summary>
    /// The kind of this node. Useful for switch statements over node kinds.
    /// </summary>
    public abstract NodeKind Kind { get; }

    public override string ToString()
    {
        return RexlPrettyPrinter.Print(this);
    }
}

/// <summary>
/// Abstract base class for all statement node types.
/// </summary>
public abstract class StmtNode : RexlNode
{
    /// <summary>
    /// Whether this statement should be separated from a subsequent statement with a semi-colon.
    /// </summary>
    public abstract bool NeedsSemi { get; }

    private protected StmtNode(ref int idNext, Token tok, int depth)
        : base(ref idNext, tok, depth)
    {
    }
}

/// <summary>
/// Abstract base class for all expression node types.
/// </summary>
public abstract class ExprNode : RexlNode
{
    private protected ExprNode(ref int idNext, Token tok, int depth)
        : base(ref idNext, tok, depth)
    {
    }
}

/// <summary>
/// Represents a block of statements. This is itself a <see cref="StmtNode"/>. It consists of
/// an open curly brace followed by nested statements and a closing curly. In error conditions,
/// the closing curly may be missing.
/// </summary>
public sealed partial class BlockStmtNode : StmtNode
{
    public override bool NeedsSemi => false;

    /// <summary>
    /// The nested statements.
    /// </summary>
    public StmtListNode Statements { get; }

    /// <summary>
    /// The close curly brace, when present.
    /// </summary>
    public Token TokClose { get; }

    internal BlockStmtNode(ref int idNext, Token tok, StmtListNode stmts, Token tokClose)
        : base(ref idNext, tok, GetDepth(stmts) + 1)
    {
        Validation.AssertValue(stmts);
        Validation.AssertValueOrNull(tokClose);

        Statements = stmts;
        TokClose = tokClose;
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        if (visitor.PreVisit(this))
        {
            AcceptChild(visitor, Statements);
            visitor.PostVisit(this);
        }
    }

    public override SourceRange GetFullRange()
    {
        return Token.Range.Union(TokClose?.Range ?? Statements.GetFullRange());
    }
}

/// <summary>
/// Information about specification of a namespace. Typically `namespace` can be followed by
/// an <see cref="IdentPath"/> or just an `@` token or nothing at all. This wraps those forms.
/// </summary>
public sealed partial class NamespaceSpec
{
    /// <summary>
    /// The <c>namespace</c> token.
    /// </summary>
    public Token Token { get; }

    /// <summary>
    /// The identifier path, which may be null or rooted.
    /// </summary>
    public IdentPath IdentPath { get; }

    /// <summary>
    /// Whether this namespace statement is "rooted", meaning that the namespace path
    /// is prefixed with '@', either as part of <see cref="IdentPath"/> or as a lone token.
    /// </summary>
    public bool IsRooted { get; }

    /// <summary>
    /// The '@' token when it is present and there is no <see cref="IdentPath"/>.
    /// </summary>
    public Token TokAt { get; }

    internal NamespaceSpec(Token tok, IdentPath idents)
    {
        Validation.AssertValue(tok);
        Validation.AssertValue(idents);
        Token = tok;
        IdentPath = idents;
        IsRooted = idents.IsRooted;
    }

    internal NamespaceSpec(Token tok, Token tokAt)
    {
        Validation.AssertValue(tok);
        Validation.AssertValue(tokAt);
        Token = tok;
        TokAt = tokAt;
        IsRooted = true;
    }

    internal NamespaceSpec(Token tok)
    {
        Validation.AssertValue(tok);
        Token = tok;
    }

    /// <summary>
    /// Returns the range, including the `namespace` keyword.
    /// </summary>
    public SourceRange GetRange()
    {
        if (IdentPath != null)
            return Token.Range.Union(IdentPath.Range);
        if (TokAt != null)
            return Token.Range.Union(TokAt.Range);
        return Token.Range;
    }
}

/// <summary>
/// A namespace statement.
/// </summary>
public sealed partial class NamespaceStmtNode : StmtNode
{
    public override bool NeedsSemi => Block == null;

    /// <summary>
    /// The namespace specification.
    /// </summary>
    public NamespaceSpec NsSpec { get; }

    /// <summary>
    /// The statement block, if present.
    /// </summary>
    public BlockStmtNode Block { get; }

    internal NamespaceStmtNode(ref int idNext, NamespaceSpec nss, BlockStmtNode block)
        : base(ref idNext, nss.VerifyValue().Token, GetDepth(block.VerifyValue()) + 1)
    {
        NsSpec = nss;
        Block = block;
    }

    internal NamespaceStmtNode(ref int idNext, NamespaceSpec nss)
        : base(ref idNext, nss.VerifyValue().Token, 1)
    {
        NsSpec = nss;
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
        {
            AcceptChildOpt(visitor, Block);
            visitor.PostVisit(this);
        }
    }

    public override SourceRange GetFullRange()
    {
        if (Block != null)
            return Token.Range.Union(Block.GetFullRange());
        return NsSpec.GetRange();
    }
}

/// <summary>
/// A with statement.
/// </summary>
public sealed partial class WithStmtNode : StmtNode
{
    public override bool NeedsSemi => Block == null;

    /// <summary>
    /// The identifier paths.
    /// </summary>
    public Immutable.Array<IdentPath> IdentPaths { get; }

    /// <summary>
    /// The statement block, if present.
    /// </summary>
    public BlockStmtNode Block { get; }

    internal WithStmtNode(ref int idNext, Token tok, Immutable.Array<IdentPath> paths, BlockStmtNode block)
        : base(ref idNext, tok, GetDepth(block.VerifyValue()) + 1)
    {
        Validation.Assert(!paths.IsDefault);
        IdentPaths = paths;
        Block = block;
    }

    internal WithStmtNode(ref int idNext, Token tok, Immutable.Array<IdentPath> paths)
        : base(ref idNext, tok, 1)
    {
        Validation.Assert(!paths.IsDefaultOrEmpty);
        IdentPaths = paths;
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
        {
            AcceptChildOpt(visitor, Block);
            visitor.PostVisit(this);
        }
    }

    public override SourceRange GetFullRange()
    {
        var rng = Token.Range;
        if (Block != null)
            return rng.Union(Block.GetFullRange());
        if (IdentPaths.Length > 0)
            return rng.Union(IdentPaths[^1].Range);
        return rng;
    }
}

/// <summary>
/// A <c>goto</c> statement. Contains the target label name.
/// </summary>
public sealed partial class GotoStmtNode : StmtNode
{
    public override bool NeedsSemi => true;

    /// <summary>
    /// The target label name.
    /// </summary>
    public Identifier Label { get; }

    internal GotoStmtNode(ref int idNext, Token tok, Identifier label)
        : base(ref idNext, tok, 1)
    {
        Validation.AssertValue(label);
        Label = label;
    }

    public override SourceRange GetFullRange()
    {
        return Token.Range.Union(Label.Range);
    }
}

/// <summary>
/// A label statement. Note that this does <i>not</i> wrap the subsequent statement, but
/// just the label name.
/// </summary>
public sealed partial class LabelStmtNode : StmtNode
{
    public override bool NeedsSemi => false;

    /// <summary>
    /// The label name.
    /// </summary>
    public Identifier Label { get; }

    internal LabelStmtNode(ref int idNext, Token tok, Identifier label)
        : base(ref idNext, tok, 1)
    {
        Validation.AssertValue(label);
        Label = label;
    }

    public override SourceRange GetFullRange()
    {
        return Label.Range.Union(Token.Range);
    }
}

/// <summary>
/// Represents an `if` statement. Syntax is similar to C#.
/// </summary>
public sealed partial class IfStmtNode : StmtNode
{
    public override bool NeedsSemi => (Else ?? Then) is not BlockStmtNode;

    /// <summary>
    /// The condition. Typically a <see cref="ParenNode"/> which includes the enclosing parentheses.
    /// </summary>
    public ExprNode Condition { get; }

    /// <summary>
    /// The statement (possibly block) to execute if the condition is true.
    /// </summary>
    public StmtNode Then { get; }

    /// <summary>
    /// If there is an <c>else</c> and the <see cref="Then"/> statement is not a block, this is the
    /// trailing semi-colon of the "then", if present.
    /// </summary>
    public Token TokSemi { get; }

    /// <summary>
    /// The <c>else</c> token, if present.
    /// </summary>
    public Token TokElse { get; }

    /// <summary>
    /// Optional. The statement (possibly block) to execute if the condition is false.
    /// </summary>
    public StmtNode Else { get; }

    internal IfStmtNode(ref int idNext, Token tok, ExprNode cond,
            StmtNode stmtThen, Token tokSemi, Token tokElse, StmtNode stmtElse)
        : base(ref idNext, tok, GetDepth(cond, stmtThen, stmtElse) + 1)
    {
        Validation.AssertValue(cond);
        Validation.AssertValue(stmtThen);
        Validation.Assert(tokSemi == null || tokElse != null);
        Validation.Assert((tokElse != null) == (stmtElse != null));

        Condition = cond;
        Then = stmtThen;
        TokSemi = tokSemi;
        TokElse = tokElse;
        Else = stmtElse;
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
        {
            AcceptChild(visitor, Condition);
            AcceptChild(visitor, Then);
            AcceptChildOpt(visitor, Else);
            visitor.PostVisit(this);
        }
    }

    public override SourceRange GetFullRange()
    {
        return Token.Range.Union(Else?.GetFullRange() ?? Then.GetFullRange());
    }
}

/// <summary>
/// Represents a `while` statement. Syntax is similar to C#.
/// </summary>
public sealed partial class WhileStmtNode : StmtNode
{
    public override bool NeedsSemi => Body is not BlockStmtNode;

    /// <summary>
    /// The condition. Typically a <see cref="ParenNode"/> which includes the enclosing parentheses.
    /// </summary>
    public ExprNode Condition { get; }

    /// <summary>
    /// The statement (possibly block) to execute if the condition is true.
    /// </summary>
    public StmtNode Body { get; }

    internal WhileStmtNode(ref int idNext, Token tok, ExprNode cond, StmtNode body)
        : base(ref idNext, tok, GetDepth(cond, body) + 1)
    {
        Validation.AssertValue(cond);
        Validation.AssertValue(body);

        Condition = cond;
        Body = body;
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
        {
            AcceptChild(visitor, Condition);
            AcceptChild(visitor, Body);
            visitor.PostVisit(this);
        }
    }

    public override SourceRange GetFullRange()
    {
        return Token.Range.Union(Body.GetFullRange());
    }
}

/// <summary>
/// Base for variadic non-Expr nodes, namely, StmtList and ExprList.
/// </summary>
public abstract class VariadicBase<TChild> : RexlNode
    where TChild : RexlNode
{
    public Immutable.Array<TChild> Children { get; }

    private protected VariadicBase(ref int idNext, Token tok, Immutable.Array<TChild> children, int depthMin = 1)
        : base(ref idNext, tok, Math.Max(depthMin, GetDepth(children) + 1))
    {
        Children = children;
    }

    /// <summary>
    /// Gets the number of children in the <see cref="Children"/> array.
    /// </summary>
    public int Count { get { return Children.Length; } }

    public override SourceRange GetFullRange()
    {
        Validation.Assert(!Children.IsDefault);
        switch (Children.Length)
        {
        case 0:
            return Token.Range;
        case 1:
            return Token.Range.Union(Children[0].GetFullRange());
        default:
            return Token.Range.Union(Children[0].GetFullRange()).Union(Children[Children.Length - 1].GetFullRange());
        }
    }
}

/// <summary>
/// Base for variadic Expr nodes, like RecordNode and CompareNode.
/// </summary>
public abstract class VariadicExprBase<TChild> : ExprNode
    where TChild : ExprNode
{
    // REVIEW: TChild is currently always ExprNode, so perhaps this shouldn't be generic?

    public Immutable.Array<TChild> Children { get; }

    private protected VariadicExprBase(ref int idNext, Token tok, Immutable.Array<TChild> children, int depthMin = 1)
        : base(ref idNext, tok, Math.Max(depthMin, GetDepth(children) + 1))
    {
        Children = children;
    }

    /// <summary>
    /// Gets the number of children in the <see cref="Children"/> array.
    /// </summary>
    public int Count { get { return Children.Length; } }

    public override SourceRange GetFullRange()
    {
        Validation.Assert(!Children.IsDefault);
        switch (Children.Length)
        {
        case 0:
            return Token.Range;
        case 1:
            return Token.Range.Union(Children[0].GetFullRange());
        default:
            return Token.Range.Union(Children[0].GetFullRange()).Union(Children[Children.Length - 1].GetFullRange());
        }
    }
}

/// <summary>
/// Encapsulates a list of statements, possibly empty.
/// </summary>
public sealed partial class StmtListNode : VariadicBase<StmtNode>
{
    /// <summary>
    /// An optional leading semicolon.
    /// </summary>
    public Token TokOpen { get; }

    /// <summary>
    /// An optional trailing semicolon.
    /// </summary>
    public Token TokClose { get; }

    internal StmtListNode(ref int idNext, Token tok, StmtTuple stmts, Token tokOpen = null, Token tokClose = null)
        : base(ref idNext, tok, stmts)
    {
        TokOpen = tokOpen;
        TokClose = tokClose;
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
        {
            AcceptChildren(visitor, Children);
            visitor.PostVisit(this);
        }
    }

    public override SourceRange GetFullRange()
    {
        return base.GetFullRange().Union(TokOpen?.Range).Union(TokClose?.Range);
    }
}

/// <summary>
/// A list of <see cref="ExprNode"/>, typically comma separated, but not always.
/// Does NOT reference the separator tokens.
/// </summary>
public sealed partial class ExprListNode : VariadicBase<ExprNode>
{
    internal ExprListNode(ref int idNext, Token tok, ExprTuple exprs)
        : base(ref idNext, tok, exprs)
    {
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
        {
            AcceptChildren(visitor, Children);
            visitor.PostVisit(this);
        }
    }
}

/// <summary>
/// A list of <see cref="SymbolDeclNode"/>. Does NOT reference the separator tokens.
/// </summary>
public sealed partial class SymListNode : VariadicBase<SymbolDeclNode>
{
    internal SymListNode(ref int idNext, Token tok, Immutable.Array<SymbolDeclNode> exprs)
        : base(ref idNext, tok, exprs)
    {
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
        {
            AcceptChildren(visitor, Children);
            visitor.PostVisit(this);
        }
    }
}

/// <summary>
/// A list of <see cref="ExprNode"/>, typically comma separated, but not always.
/// Does NOT reference the separator tokens.
/// </summary>
public sealed partial class SliceListNode : VariadicBase<SliceItemNode>
{
    /// <summary>
    /// Whether all slice items are simple.
    /// </summary>
    public bool AllSimple { get; }

    internal SliceListNode(ref int idNext, Token tok, SliceTuple items)
        : base(ref idNext, tok, items)
    {
        bool simple = true;
        foreach (var item in items)
            simple &= item.IsSimple;
        AllSimple = simple;
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
        {
            AcceptChildren(visitor, Children);
            visitor.PostVisit(this);
        }
    }
}

/// <summary>
/// Base statement node that contains a single value expression.
/// </summary>
public abstract class ValueStmtNode : StmtNode
{
    /// <summary>
    /// The contained <see cref="ExprNode"/>.
    /// </summary>
    public ExprNode Value { get; }

    internal ValueStmtNode(ref int idNext, Token tok, ExprNode value)
        : base(ref idNext, tok, GetDepth(value) + 1)
    {
        Validation.AssertValue(value);
        Value = value;
    }
}

/// <summary>
/// A command statement consisting of a command keyword followed by a single expression,
/// optionally followed by <c>in namespace ...</c>.
/// Use <see cref="Kind"/> to determine which command.
/// </summary>
public abstract partial class CmdStmtNode : ValueStmtNode
{
    public override bool NeedsSemi => true;

    /// <summary>
    /// The namespace specification, if present.
    /// </summary>
    public NamespaceSpec Namespace { get; }

    private protected CmdStmtNode(ref int idNext, Token tok, ExprNode value, NamespaceSpec nss)
        : base(ref idNext, tok, value)
    {
        Validation.Assert(tok.Kind == TokKind.KwdImport | tok.Kind == TokKind.KwdExecute);
        Validation.AssertValueOrNull(nss);

        Namespace = nss;
    }

    internal static CmdStmtNode Create(ref int idNext, Token tok, ExprNode value, NamespaceSpec nss)
    {
        Validation.AssertValue(tok);
        Validation.Assert(tok.Kind == TokKind.KwdImport | tok.Kind == TokKind.KwdExecute);
        Validation.AssertValue(value);
        Validation.AssertValueOrNull(nss);

        if (tok.Kind == TokKind.KwdImport)
            return new ImportStmtNode(ref idNext, tok, value, nss);
        return new ExecuteStmtNode(ref idNext, tok, value, nss);
    }

    public override SourceRange GetFullRange()
    {
        return Token.Range.Union(Value.GetFullRange());
    }
}

public sealed partial class ExecuteStmtNode : CmdStmtNode
{
    internal ExecuteStmtNode(ref int idNext, Token tok, ExprNode value, NamespaceSpec nss)
        : base(ref idNext, tok, value, nss)
    {
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
        {
            AcceptChild(visitor, Value);
            visitor.PostVisit(this);
        }
    }
}

public sealed partial class ImportStmtNode : CmdStmtNode
{
    internal ImportStmtNode(ref int idNext, Token tok, ExprNode value, NamespaceSpec nss)
        : base(ref idNext, tok, value, nss)
    {
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
        {
            AcceptChild(visitor, Value);
            visitor.PostVisit(this);
        }
    }
}

/// <summary>
/// Statement wrapper around an expr.
/// </summary>
public sealed partial class ExprStmtNode : ValueStmtNode
{
    public override bool NeedsSemi => true;

    internal ExprStmtNode(ref int idNext, ExprNode value)
        : base(ref idNext, value.VerifyValue().Token, value)
    {
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
        {
            AcceptChild(visitor, Value);
            visitor.PostVisit(this);
        }
    }

    public override SourceRange GetFullRange()
    {
        return Value.GetFullRange();
    }
}

/// <summary>
/// A definition statement: <c>[kind] name := value</c>. The <c>kind</c> is an optional <see cref="DefnKind"/>
/// used in task and module contexts. When <c>name</c> is <c>this</c> or absent, the <see cref="IdentPath"/>
/// is <c>null</c> and the <see cref="DefnKind"/> is <see cref="DefnKind.This"/>.
/// </summary>
public sealed partial class DefinitionStmtNode : ValueStmtNode
{
    public override bool NeedsSemi => true;

    /// <summary>
    /// The identifier path on the lhs, with <c>null</c> meaning assign to <c>this</c>.
    /// </summary>
    public IdentPath IdentPath { get; }

    /// <summary>
    /// The path corresponding to the <see cref="IdentPath"/>. When <see cref="IdentPath"/> is <c>null</c>,
    /// this is the root path.
    /// </summary>
    public NPath FullName { get; }

    /// <summary>
    /// Whether this is an assignment to <c>this</c>.
    /// </summary>
    public bool ForThis => IdentPath == null;

    /// <summary>
    /// The definition kind.
    /// </summary>
    public DefnKind DefnKind { get; }

    /// <summary>
    /// The token for the definition kind (may be null).
    /// </summary>
    public Token TokDefnKind { get; }

    internal DefinitionStmtNode(ref int idNext, Token tok, IdentPath idents, ExprNode value, Token tokKind = null)
        : base(ref idNext, tok, value)
    {
        Validation.AssertValueOrNull(idents);
        IdentPath = idents;
        FullName = idents?.FullName ?? default;

        TokDefnKind = tokKind;
        if (tokKind == null)
        {
            DefnKind = DefnKind.None;
            if (idents == null)
                DefnKind = DefnKind.This;
        }
        else
        {
            switch (tokKind.Kind)
            {
            case TokKind.KtxPublish: DefnKind = DefnKind.Publish; break;
            case TokKind.KtxPrimary: DefnKind = DefnKind.Primary; break;
            case TokKind.KtxStream: DefnKind = DefnKind.Stream; break;

            case TokKind.KwdThis: DefnKind = DefnKind.This; break;

            default:
                Validation.Assert(false);
                DefnKind = DefnKind.None;
                break;
            }
        }

        Validation.Assert((FullName.NameCount == 0) == (DefnKind == DefnKind.This));
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
        {
            AcceptChild(visitor, Value);
            visitor.PostVisit(this);
        }
    }

    public override SourceRange GetFullRange()
    {
        return Token.Range
            .Union(TokDefnKind?.Range)
            .Union(IdentPath?.Range)
            .Union(Value.GetFullRange());
    }
}

/// <summary>
/// Represents a parsed func declaration.
/// </summary>
public sealed partial class FuncStmtNode : StmtNode
{
    public override bool NeedsSemi => true;

    /// <summary>
    /// The identifier path of the function.
    /// </summary>
    public IdentPath IdentPath { get; }

    /// <summary>
    /// The open paren token.
    /// </summary>
    public Token TokOpen { get; }

    /// <summary>
    /// The parameter names.
    /// </summary>
    public IdentTuple ParamIdents { get; }

    /// <summary>
    /// The parameter names.
    /// </summary>
    public NameTuple ParamNames { get; }

    /// <summary>
    /// Whether this is a property declaration.
    /// </summary>
    public bool IsProp => Token.TokenAlt.Kind == TokKind.KtxProp && ParamNames.Length == 1;

    /// <summary>
    /// The close paren, possibly null (when missing).
    /// </summary>
    public Token TokClose { get; }

    /// <summary>
    /// The := token, Possibly null.
    /// </summary>
    public Token TokColEqu { get; }

    /// <summary>
    /// The body of the function declaration.
    /// </summary>
    public ExprNode Value { get; }

    internal FuncStmtNode(ref int idNext,
            Token tok, IdentPath path,
            Token tokOpen, IdentTuple paramIdents, Token tokClose,
            Token tokColEqu, ExprNode value)
        : base(ref idNext, tok, GetDepth(value) + 1)
    {
        Validation.AssertValue(path);
        Validation.AssertValue(tokOpen);
        Validation.AssertValueOrNull(tokClose);
        Validation.AssertValueOrNull(tokColEqu);
        Validation.AssertValue(value);

        IdentPath = path;
        TokOpen = tokOpen;
        ParamIdents = paramIdents;
        ParamNames = GetNamesFromIdents(paramIdents);

        TokClose = tokClose;
        TokColEqu = tokColEqu;
        Value = value;
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
        {
            AcceptChild(visitor, Value);
            visitor.PostVisit(this);
        }
    }

    public override SourceRange GetFullRange()
    {
        return Token.Range.Union(Value.GetFullRange());
    }
}

/// <summary>
/// An error as an <see cref="ExprNode"/>.
/// </summary>
public sealed partial class ErrorNode : ExprNode
{
    public RexlDiagnostic Error { get; }

    internal ErrorNode(ref int idNext, Token tok, RexlDiagnostic error)
        : base(ref idNext, tok, 1)
    {
        Validation.AssertValue(error);
        Validation.Assert(error.IsError);
        Error = error;
    }

    public override SourceRange GetFullRange()
    {
        // REVIEW: This should really know about all the tokens involved.
        return GetRange();
    }

    public string Format()
    {
        return Error.ToString();
    }
}

/// <summary>
/// A missing value <see cref="ExprNode"/>.
/// </summary>
public sealed partial class MissingValueNode : ExprNode
{
    internal MissingValueNode(ref int idNext, Token tok)
        : base(ref idNext, tok, 1)
    {
    }

    public string Format()
    {
        return "<missing>";
    }

    public override SourceRange GetFullRange() => GetRange();
}

/// <summary>
/// Represents a parenthesized expression. Semantically, this isn't needed. However, it records
/// the source code / token extents, which is useful for correlating source code to parse nodes.
/// The static Wrap method ensures that the child will never be a ParenNode, instead we create a
/// new "wider" ParenNode around the child.
/// </summary>
public sealed partial class ParenNode : ExprNode
{
    public readonly ExprNode Child;

    /// <summary>
    /// The open paren. Will never be null.
    /// </summary>
    public Token TokOpen { get; }

    /// <summary>
    /// The close paren, which may be null if missing.
    /// </summary>
    public Token TokClose { get; }

    /// <summary>
    /// Wrap an ExprNode in a ParenNode.
    /// </summary>
    internal static ParenNode Wrap(ref int idNext, Token tokOpen, ExprNode child, Token tokClose)
    {
        Validation.AssertValue(tokOpen);
        Validation.Assert(tokOpen.Kind == TokKind.ParenOpen);
        Validation.AssertValue(child);

        // Don't double wrap.
        var paren = child as ParenNode;
        return new ParenNode(ref idNext, tokOpen, paren?.Child ?? child, tokClose ?? paren?.TokClose);
    }

    // Private so client code must use the static Wrap method.
    private ParenNode(ref int idNext, Token tokOpen, ExprNode child, Token tokClose)
        : base(ref idNext, tokOpen, GetDepth(child) + 1)
    {
        Validation.Assert(Token.Kind == TokKind.ParenOpen);
        Validation.AssertValue(child);

        // Parens around parens should be combined by Wrap method above.
        Validation.Assert(child.Kind != NodeKind.Paren);

        Validation.AssertValueOrNull(tokClose);

        Child = child;
        TokOpen = Token;
        TokClose = tokClose;
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
        {
            AcceptChild(visitor, Child);
            visitor.PostVisit(this);
        }
    }

    public override SourceRange GetFullRange()
    {
        return Child.GetFullRange().Union(TokOpen.Range).Union(TokClose?.Range);
    }
}

/// <summary>
/// A null literal.
/// </summary>
public sealed partial class NullLitNode : ExprNode
{
    internal NullLitNode(ref int idNext, Token tok)
        : base(ref idNext, tok, 1)
    {
    }

    public override SourceRange GetFullRange() => GetRange();
}

/// <summary>
/// A boolean literal.
/// </summary>
public sealed partial class BoolLitNode : ExprNode
{
    /// <summary>
    /// Gets the value of the literal.
    /// </summary>
    public bool Value { get { return Token.Kind == TokKind.KwdTrue; } }

    internal BoolLitNode(ref int idNext, Token tok)
        : base(ref idNext, tok, 1)
    {
        Validation.AssertValue(tok);
        Validation.Assert(tok.Kind == TokKind.KwdTrue || tok.Kind == TokKind.KwdFalse);
    }

    public override SourceRange GetFullRange() => GetRange();
}

/// <summary>
/// A numeric literal.
/// </summary>
public sealed partial class NumLitNode : ExprNode
{
    /// <summary>
    /// The numeric literal token.
    /// </summary>
    public NumLitToken Value { get; }

    internal NumLitNode(ref int idNext, NumLitToken tok)
        : base(ref idNext, tok, 1)
    {
        Value = tok;
    }

    public override SourceRange GetFullRange() => GetRange();
}

/// <summary>
/// A text literal.
/// </summary>
public sealed partial class TextLitNode : ExprNode
{
    public string Value { get; }

    internal TextLitNode(ref int idNext, TextLitToken tok)
        : base(ref idNext, tok, 1)
    {
        Value = tok.Value;
        Validation.AssertValue(Value);
    }

    public override SourceRange GetFullRange() => GetRange();
}

/// <summary>
/// A "box", representing target location of the pipe operator.
/// Note that the grammar doesn't ensure a 1:1 match between pipe operator and box.
/// In fact, we may decide to support multiple targets, eg, (x + y) => (_ * _).
/// The complexity will be in the Binder, not Parser.
/// </summary>
public sealed partial class BoxNode : ExprNode
{
    internal BoxNode(ref int idNext, KeyToken tok)
        : base(ref idNext, tok, 1)
    {
        Validation.Assert(tok.Kind == TokKind.Box);
    }

    public override SourceRange GetFullRange() => GetRange();
}

/// <summary>
/// Represents 'it[$slot]'.
/// </summary>
public sealed partial class ItNameNode : ExprNode
{
    /// <summary>
    /// The number of times to walk up the scope chain from 'it'.
    /// </summary>
    public int UpCount { get; }

    internal ItNameNode(ref int idNext, ItSlotToken tokItSlot)
        : base(ref idNext, tokItSlot, 1)
    {
        Validation.AssertValue(tokItSlot);
        Validation.Assert(tokItSlot.Kind == TokKind.ItSlot);
        Validation.Assert(tokItSlot.Slot >= 0);

        UpCount = tokItSlot.Slot;
    }

    internal ItNameNode(ref int idNext, KeyToken tokIt)
        : base(ref idNext, tokIt, 1)
    {
        Validation.AssertValue(tokIt);
        Validation.Assert(tokIt.Kind == TokKind.KwdIt);

        UpCount = 0;
    }

    public override SourceRange GetFullRange() => GetRange();
}

/// <summary>
/// Represents an identifier. Note that this is NOT a <see cref="RexlNode"/>.
/// </summary>
public sealed partial class Identifier
{
    /// <summary>
    /// The token for this identifier.
    /// </summary>
    public IdentToken Token { get; }

    /// <summary>
    /// The name for this identifier.
    /// </summary>
    public DName Name { get; }

    /// <summary>
    /// Non-null when parsed from the @xyz form.
    /// </summary>
    public Token AtToken { get; }

    /// <summary>
    /// The range for this identifier, including the optional AtToken.
    /// </summary>
    public SourceRange Range { get; }

    /// <summary>
    /// Whether this identifier is considered a "global", because it has an AtToken.
    /// </summary>
    public bool IsGlobal { get { return AtToken != null; } }

    internal Identifier(IdentToken tok, Token tokAt = null)
    {
        Validation.AssertValue(tok);
        Validation.Assert(tok.Name.IsValid);
        Validation.AssertValueOrNull(tokAt);

        Token = tok;
        Name = tok.Name;
        AtToken = tokAt;
        Range = Token.Range.Union(tokAt?.Range);
    }
}

/// <summary>
/// Represents one or more identifiers separated by dot. Note that this is NOT a <see cref="RexlNode"/>.
/// Each <see cref="Identifier"/> may include an '@' token, but an error is reported if any but the
/// first does so. The <see cref="IsRooted"/> property reflects whether the first includes '@'.
/// </summary>
public sealed partial class IdentPath
{
    /// <summary>
    /// The identifiers (at least one).
    /// </summary>
    public IdentTuple Idents { get; }

    /// <summary>
    /// The number of identifiers in the path.
    /// </summary>
    public int Length => Idents.Length;

    /// <summary>
    /// The first identifier.
    /// </summary>
    public Identifier First => Idents[0];

    /// <summary>
    /// The last identifier.
    /// </summary>
    public Identifier Last => Idents[Idents.Length - 1];

    /// <summary>
    /// Whether the path is "rooted", that is, preceeded by '@'.
    /// </summary>
    public bool IsRooted => First.IsGlobal;

    /// <summary>
    /// The <see cref="NPath"/> corresponding to the identifiers.
    /// </summary>
    public NPath FullName { get; }

    /// <summary>
    /// The range for this identifier path, including the optional AtToken.
    /// </summary>
    public SourceRange Range { get; }

    internal static IdentPath Create(Identifier ident)
    {
        Validation.AssertValue(ident);
        return new IdentPath(Immutable.Array.Create(ident.VerifyValue()));
    }

    internal IdentPath(IdentTuple idents)
    {
        Validation.Assert(!idents.IsDefaultOrEmpty);
        Validation.AssertAllValues(idents);

        Idents = idents;

        var full = NPath.Root;
        foreach (var name in idents)
            full = full.Append(name.Name);
        FullName = full;

        Range = Idents[0].Range.Union(Idents[Idents.Length - 1].Range);
    }

    /// <summary>
    /// Combine this with <paramref name="ns"/> if this is not rooted and with <paramref name="nsRoot"/>
    /// if this is rooted.
    /// </summary>
    public NPath Combine(NPath ns, NPath nsRoot)
    {
        var nsBase = IsRooted ? nsRoot : ns;
        return nsBase.AppendPath(FullName);
    }

    public override string ToString()
    {
        var res = FullName.ToDottedSyntax();
        if (IsRooted)
            res = "@" + res;
        return res;
    }
}

/// <summary>
/// The "this" name.
/// </summary>
public sealed partial class ThisNameNode : ExprNode
{
    internal ThisNameNode(ref int idNext, Token tok)
        : base(ref idNext, tok, 1)
    {
    }

    public override SourceRange GetFullRange() => GetRange();
}

/// <summary>
/// A name represented as a single identifier.
/// </summary>
public sealed partial class FirstNameNode : ExprNode
{
    public Identifier Ident { get; }

    internal FirstNameNode(ref int idNext, Identifier ident)
        : base(ref idNext, ident.VerifyValue().Token, 1)
    {
        Validation.AssertValue(ident);
        Ident = ident;
    }

    public override SourceRange GetFullRange()
    {
        return Ident.Range;
    }
}

/// <summary>
/// An identifier path followed by a dollar '$' followed by an identifier.
/// </summary>
public sealed partial class MetaPropNode : ExprNode
{
    /// <summary>
    /// The ident path to the left of the dollar.
    /// </summary>
    public IdentPath Left { get; }

    /// <summary>
    /// The identifier to the right of the dollar.
    /// </summary>
    public Identifier Right { get; }

    internal MetaPropNode(ref int idNext, Token tok, IdentPath left, Identifier right)
        : base(ref idNext, tok, 1)
    {
        Validation.AssertValue(tok);
        Validation.Assert(tok.Kind == TokKind.Dol);
        Validation.AssertValue(left);
        Validation.AssertValue(right);

        Left = left;
        Right = right;
    }

    public override SourceRange GetFullRange()
    {
        Validation.AssertValue(Token);
        Validation.AssertValue(Right);
        Validation.AssertValue(Right.Token);

        return Left.Range.Union(Right.Range);
    }
}

/// <summary>
/// An ExprNode followed by a dot '.' followed by an identifier. Note that the left-most (root) node
/// is not necessarily a <see cref="FirstNameNode"/>.
/// </summary>
public sealed partial class DottedNameNode : ExprNode
{
    // REVIEW: Consider making this variadic, rather than a long chain?

    /// <summary>
    /// The beginning of the DottedNameNode chain. If <see cref="Left"/> is a DottedNameNode, this
    /// is the same as Left.Root. Otherwise, it is the same as <see cref="Left"/>.
    /// </summary>
    public ExprNode Root { get; }

    /// <summary>
    /// The expression to the left of the dot.
    /// </summary>
    public ExprNode Left { get; }

    /// <summary>
    /// The identifier to the right of the dot.
    /// </summary>
    public Identifier Right { get; }

    internal DottedNameNode(ref int idNext, Token tok, ExprNode left, Identifier right)
        : base(ref idNext, tok, GetDepth(left) + 1)
    {
        Validation.AssertValue(tok);
        Validation.AssertValue(left);
        Validation.AssertValue(right);

        Left = left;
        Right = right;
        Root = (left as DottedNameNode)?.Root ?? left;
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
        {
            AcceptChild(visitor, Left);
            visitor.PostVisit(this);
        }
    }

    /// <summary>
    /// Returns the identifiers in this DottedNameNode, including Root, when it is a FirstNameNode.
    /// </summary>
    public IdentPath ToIdents()
    {
        // Walk the DottedNameNode list, adding to the builder. This adds them in reverse order, so we
        // call Reverse below.
        var bldr = Immutable.Array.CreateBuilder<Identifier>(TreeDepth + 1);
        for (DottedNameNode dotted = this; dotted != null; dotted = dotted.Left as DottedNameNode)
        {
            Validation.Assert(dotted.Root == Root);
            Validation.Assert(dotted.Left is DottedNameNode | dotted.Root == dotted.Left);
            bldr.Add(dotted.Right);
        }

        if (Root is FirstNameNode first)
            bldr.Add(first.Ident);

        // Reverse the names.
        bldr.Reverse();

        return new IdentPath(bldr.ToImmutable());
    }

    public override SourceRange GetFullRange()
    {
        Validation.AssertValue(Token);
        Validation.AssertValue(Right);
        Validation.AssertValue(Right.Token);

        return Root.GetFullRange().Union(Right.Range);
    }
}

/// <summary>
/// Parse node for getting an index for a map scope. Exactly one of <see cref="ItChild"/>, <see cref="NameChild"/>,
/// <see cref="Slot"/> is specified (non-null / non-negative).
/// </summary>
public sealed partial class GetIndexNode : ExprNode
{
    /// <summary>
    /// The it name after #. May be null.
    /// </summary>
    public ItNameNode ItChild { get; }

    /// <summary>
    /// The identifier after #. May be null.
    /// </summary>
    public FirstNameNode NameChild { get; }

    /// <summary>
    /// The slot number. Negative for not specified.
    /// </summary>
    public int Slot { get; }

    /// <summary>
    /// Constructor that takes an it name.
    /// </summary>
    internal GetIndexNode(ref int idNext, Token tok, ItNameNode it)
        : base(ref idNext, tok, it.VerifyValue().TreeDepth + 1)
    {
        ItChild = it;
        Slot = -1;
    }

    /// <summary>
    /// Constructor that takes a first name node.
    /// </summary>
    internal GetIndexNode(ref int idNext, Token tok, FirstNameNode name)
        : base(ref idNext, tok, name.VerifyValue().TreeDepth + 1)
    {
        NameChild = name;
        Slot = -1;
    }

    /// <summary>
    /// Constructor that takes a slot.
    /// </summary>
    internal GetIndexNode(ref int idNext, Token tok, int slot)
        : base(ref idNext, tok, 1)
    {
        Validation.Assert(slot >= 0);
        Slot = slot;
    }

    /// <summary>
    /// Constructor that takes a <see cref="HashSlotToken"/>.
    /// </summary>
    internal GetIndexNode(ref int idNext, HashSlotToken tok)
        : base(ref idNext, tok, 1)
    {
        Validation.Assert(tok.Slot >= 0);
        Slot = tok.Slot;
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
        {
            AcceptChildOpt(visitor, ItChild);
            AcceptChildOpt(visitor, NameChild);
            visitor.PostVisit(this);
        }
    }

    public override SourceRange GetFullRange()
    {
        return Token.Range.Union(ItChild?.GetFullRange()).Union(NameChild?.GetFullRange());
    }
}

/// <summary>
/// Represents a unary operator and its operand.
/// </summary>
public sealed partial class UnaryOpNode : ExprNode
{
    /// <summary>
    /// The unary operator.
    /// </summary>
    public UnaryOp Op { get; }

    /// <summary>
    /// The operand.
    /// </summary>
    public ExprNode Arg { get; }

    internal UnaryOpNode(ref int idNext, Token tok, UnaryOp op, ExprNode child)
        : base(ref idNext, tok, GetDepth(child) + 1)
    {
        Validation.AssertValue(child);

        Arg = child;
        Op = op;
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
        {
            AcceptChild(visitor, Arg);
            visitor.PostVisit(this);
        }
    }

    public override SourceRange GetFullRange()
    {
        return Token.Range.Union(Arg.GetFullRange());
    }
}

/// <summary>
/// Represents a binary operator and its operands.
/// </summary>
public sealed partial class BinaryOpNode : ExprNode
{
    /// <summary>
    /// The binary operator.
    /// </summary>
    public BinaryOp Op { get; }

    /// <summary>
    /// The left operand.
    /// </summary>
    public ExprNode Arg0 { get; }

    /// <summary>
    /// The right operand.
    /// </summary>
    public ExprNode Arg1 { get; }

    internal BinaryOpNode(ref int idNext, Token tok, BinaryOp op, ExprNode left, ExprNode right)
        : base(ref idNext, tok, GetDepth(left, right) + 1)
    {
        Validation.AssertValue(left);
        Validation.AssertValue(right);

        Arg0 = left;
        Arg1 = right;
        Op = op;
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
        {
            AcceptChild(visitor, Arg0);
            AcceptChild(visitor, Arg1);
            visitor.PostVisit(this);
        }
    }

    public override SourceRange GetFullRange()
    {
        return Arg0.GetFullRange().Union(Arg1.GetFullRange());
    }
}

/// <summary>
/// Represents a binary operator containing In or Has and its operands.
/// </summary>
public sealed partial class InHasNode : ExprNode
{
    /// <summary>
    /// The binary operator containing In or Has.
    /// </summary>
    public BinaryOp Op { get; }

    /// <summary>
    /// The left operand.
    /// </summary>
    public ExprNode Arg0 { get; }

    /// <summary>
    /// The right operand.
    /// </summary>
    public ExprNode Arg1 { get; }

    /// <summary>
    /// The <c>!</c> or <c>not</c> token when present.
    /// </summary>
    public Token Not { get; }

    /// <summary>
    /// The <c>~</c> token when present.
    /// </summary>
    public Token Tld { get; }

    internal InHasNode(ref int idNext, Token tok, BinaryOp op, ExprNode left, ExprNode right,
            Token not, Token tld)
        : base(ref idNext, tok, GetDepth(left, right) + 1)
    {
        Validation.AssertValue(left);
        Validation.AssertValue(right);
        Validation.Assert(op.IsInHas());
        Validation.Assert(not == null || not.Kind == TokKind.KwdNot || not.Kind == TokKind.Bng);
        Validation.Assert(tld == null || tld.Kind == TokKind.Tld);

        Arg0 = left;
        Arg1 = right;
        Op = op;
        Not = not;
        Tld = tld;
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
        {
            AcceptChild(visitor, Arg0);
            AcceptChild(visitor, Arg1);
            visitor.PostVisit(this);
        }
    }

    public override SourceRange GetFullRange()
    {
        return Arg0.GetFullRange().Union(Arg1.GetFullRange());
    }
}

/// <summary>
/// Variadic comparison chain. We use python semantics, NOT Tangram or Mathematica.
/// </summary>
public sealed partial class CompareNode : VariadicExprBase<ExprNode>
{
    /// <summary>
    /// The comparison operators.
    /// </summary>
    public Immutable.Array<CompareOp> Operators { get; }

    /// <summary>
    /// The operator tokens corresponding to <see cref="Operators"/>. Any of these will be null when not provided.
    /// Op can be null only in error cases where at least one of the other tokens is not null. The <c>Str</c>
    /// token is either <c>$</c> or <c>@</c>, with the former meaning strict and the latter meaning not strict.
    /// </summary>
    public Immutable.Array<(Token Op, Token Not, Token Tld, Token Str)> Tokens { get; }

    internal CompareNode(ref int idNext, Token tok, ExprTuple exprs, Immutable.Array<CompareOp> cops,
            Immutable.Array<(Token Op, Token Not, Token Tld, Token Str)> toks)
        : base(ref idNext, tok, exprs)
    {
        Validation.Assert(Children.Length >= 2);
        Validation.Assert(!cops.IsDefault);
        Validation.Assert(cops.Length == Children.Length - 1);
        Validation.Assert(!toks.IsDefault);
        Validation.Assert(toks.Length == Children.Length - 1);
        Validation.Assert(toks.All(t => t.Op != null || t.Not != null || t.Tld != null || t.Str != null));
        Operators = cops;
        Tokens = toks;
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
        {
            AcceptChildren(visitor, Children);
            visitor.PostVisit(this);
        }
    }
}

/// <summary>
/// A function or procedure call. The operation path is an <see cref="IdentPath"/>.
/// This node supports the concept of a prefix token that indicates special handling.
/// For example, this is used to support the "run" and "start" indications when invoking
/// a procedure.
/// </summary>
public sealed partial class CallNode : ExprNode
{
    /// <summary>
    /// The identifiers in the function path, including any namespace indication. The first
    /// identifier may include '@' indicating that the path is "rooted".
    /// </summary>
    public IdentPath IdentPath { get; }

    /// <summary>
    /// The function arguments.
    /// </summary>
    public ExprListNode Args { get; }

    /// <summary>
    /// The pipe to call token, when present.
    /// </summary>
    public Token TokPipe { get; }

    /// <summary>
    /// The open paren. It may be null if <see cref="TokPipe"/> is null.
    /// </summary>
    public Token TokOpen { get; }

    /// <summary>
    /// The closing paren, when present.
    /// </summary>
    public Token TokClose { get; }

    private CallNode(ref int idNext, Token tok, IdentPath idents, ExprListNode args,
            Token tokPipe, Token tokOpen, Token tokClose)
        : base(ref idNext, tok, GetDepth(args) + 1)
    {
        Validation.AssertValue(idents);
        Validation.AssertValue(args);
        Validation.Assert((tokPipe != null && tok == tokPipe) || (tokPipe == null && tok == tokOpen));
        Validation.AssertValueOrNull(tokClose);

        IdentPath = idents;

        Args = args;
        TokPipe = tokPipe;
        TokOpen = tokOpen;
        TokClose = tokClose;
    }

    internal static CallNode CreatePiped(ref int idNext, Token tokPipe, IdentPath idents,
        Token tokOpen, ExprListNode args, Token tokClose)
    {
        Validation.AssertValue(tokPipe);
        Validation.Assert(args.Count > 0);
        return new CallNode(ref idNext, tokPipe, idents, args, tokPipe, tokOpen, tokClose);
    }

    internal static CallNode Create(ref int idNext, IdentPath idents,
        Token tokOpen, ExprListNode args, Token tokClose)
    {
        Validation.AssertValue(tokOpen);
        return new CallNode(ref idNext, tokOpen, idents, args, null, tokOpen, tokClose);
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
        {
            AcceptChild(visitor, Args);
            visitor.PostVisit(this);
        }
    }

    public override SourceRange GetFullRange()
    {
        if (TokPipe != null)
            return Args.GetFullRange().Union(TokClose?.Range);
        return IdentPath.Range.Union(TokClose?.Range ?? Args.GetFullRange());
    }
}

/// <summary>
/// Represents an item in a <see cref="SliceListNode"/>, which is expected to be an index as an integer value,
/// a range via multiple optional integer values seperated by colons, or a range via a tuple value. In general,
/// the parser cannot distinguish between an index and a range specified as a tuple, since the type of <c>x</c>
/// is not apparent in <c>T[x]</c>.
/// 
/// The "simple" case is when <see cref="Colon1"/> is <c>null</c>. The simple case has two sub-cases, determined
/// by the binder, namely the "index" case, and the "tuple range" case, depending on whether <see cref="Start"/>
/// binds to an I8 value vs a tuple value. For the simple case, the <see cref="Start"/> value is not <c>null</c>,
/// and its modifiers <see cref="StartBack"/> and <see cref="StartEdge"/> may be non-<c>null</c>. All other node and
/// token properties will be <c>null</c>.
/// 
/// The "compound" case is when <see cref="Colon1"/> is not <c>null</c>. In the compound case, this specifies an
/// explicit range via multiple optional values separated by colons. For the compound case, the <see cref="Start"/>,
/// <see cref="Stop"/> and <see cref="Step"/> values are all optional.
/// 
/// If <see cref="Colon2"/> is <c>null</c>, then <see cref="Step"/> will also be <c>null</c>. Theses can only be
/// non-<c>null</c> for the compound case.
/// </summary>
public sealed partial class SliceItemNode : RexlNode
{
    /// <summary>
    /// Indicates whether this is the "simple" case of either an "index" or "tuple range", as opposed to the
    /// "compound" case of an explicit "range".
    /// </summary>
    public bool IsSimple => Colon1 == null;

    /// <summary>
    /// The optional <c>^</c> token for the <see cref="Start"/> value.
    /// If <see cref="Start"/> is <c>null</c>, this is also <c>null</c>.
    /// When this is <c>null</c>, <see cref="Start"/> is treated as a positive offset from zero.
    /// When this is not <c>null</c>, <see cref="Start"/> is treated as a negative offset from the size.
    /// </summary>
    public Token StartBack { get; }

    /// <summary>
    /// The optional <c>%</c> or <c>&amp;</c> token for the <see cref="Start"/> value.
    /// In the compound case, this will be <c>null</c>.
    /// This affects how an "out of bounds" index is treated (when the size is not zero).
    /// When this is <c>null</c>, an out of bounds index value is not adjusted.
    /// When this is <c>%</c>, an out of bounds index value is "wrapped" (reduced modulo the size).
    /// When this is <c>&amp;</c>, an out of bounds index value is "clipped" to be in range.
    /// The binder generates an error in the "tuple range" case if this is not <c>null</c>.
    /// </summary>
    public Token StartEdge { get; }

    /// <summary>
    /// When <see cref="IsSimple"/>, this value and its modifying tokens determine the index value.
    /// Otherwise, this value is optional and determines the start value of the range.
    /// </summary>
    public ExprNode Start { get; }

    /// <summary>
    /// The first colon token, when present.
    /// </summary>
    public Token Colon1 { get; }

    /// <summary>
    /// The optional <c>^</c> token for the <see cref="Stop"/> value.
    /// If <see cref="Stop"/> is <c>null</c>, this is also <c>null</c>.
    /// When this is <c>null</c>, <see cref="Stop"/> is treated as a positive offset from zero.
    /// When this is not <c>null</c>, <see cref="Stop"/> is treated as a negative offset from the size.
    /// See <see cref="StopStar"/> for details on how this interacts with it.
    /// </summary>
    public Token StopBack { get; }

    /// <summary>
    /// The optional <c>*</c> token for the <see cref="Stop"/> value.
    /// If <see cref="Stop"/> is <c>null</c>, this is also <c>null</c>.
    /// When this is <c>null</c>, <see cref="Stop"/> determines a stop position.
    /// When this is not <c>null</c>, <see cref="Stop"/> determines an item count rather than stop position.
    /// When both this and <see cref="StopBack"/> are not <c>null</c>, the normal count of items is reduced
    /// by the <see cref="Stop"/> value.
    /// </summary>
    public Token StopStar { get; }

    /// <summary>
    /// When <see cref="IsSimple"/>, this is <c>null</c>. Otherwise, this value is optional and determines the
    /// stop position or stop count of the range, depending on whether <see cref="StopStar"/> is <c>null</c>.
    /// </summary>
    public ExprNode Stop { get; }

    /// <summary>
    /// The second colon token, when present. If <see cref="Colon1"/> is <c>null</c> (the simple case),
    /// this is also <c>null</c>.
    /// </summary>
    public Token Colon2 { get; }

    /// <summary>
    /// When <see cref="Colon2"/> is null, this is null. When <see cref="Colon2"/> is not <c>null</c>, this is
    /// the optional step value.
    /// </summary>
    public ExprNode Step { get; }

    /// <summary>
    /// The number of values in this slice item. This can be between zero and three inclusive.
    /// </summary>
    public int ValueCount
    {
        get
        {
            if (IsSimple)
                return 1;
            return Util.ToNum(Start != null) + Util.ToNum(Stop != null) + Util.ToNum(Step != null);
        }
    }

    private SliceItemNode(ref int idNext, Token backStart, Token edge, ExprNode start,
            Token col1, Token backStop, Token starStop, ExprNode stop, Token col2, ExprNode step)
        : base(ref idNext, col1 ?? start.VerifyValue().Token, GetDepth(start, stop, step) + 1)
    {
        Validation.Assert(backStart == null || backStart.Kind == TokKind.Car);
        Validation.Assert(backStart == null || start != null);
        Validation.Assert(edge == null || start != null);

        Validation.Assert(col1 == null || col1.Kind == TokKind.Colon);
        Validation.Assert(col1 != null || start != null);
        Validation.Assert(edge == null || col1 == null);

        Validation.Assert(backStop == null || backStop.Kind == TokKind.Car);
        Validation.Assert(backStop == null || stop != null);
        Validation.Assert(starStop == null || starStop.Kind == TokKind.Mul);
        Validation.Assert(starStop == null || stop != null);

        Validation.Assert(col1 != null || stop == null);
        Validation.Assert(col2 == null || col2.Kind == TokKind.Colon);
        Validation.Assert(col2 == null || col1 != null);
        Validation.Assert(step == null || col2 != null);

        StartBack = backStart;
        StartEdge = edge;
        Start = start;
        Colon1 = col1;
        StopBack = backStop;
        StopStar = starStop;
        Stop = stop;
        Colon2 = col2;
        Step = step;
    }

    /// <summary>
    /// Create a simple <see cref="SliceItemNode"/>, where there are no colons.
    /// </summary>
    internal static SliceItemNode Create(ref int idNext, Token back, Token edge, ExprNode start)
    {
        Validation.AssertValue(start);
        return new SliceItemNode(ref idNext, back, edge, start, null, null, null, null, null, null);
    }

    /// <summary>
    /// Create a <see cref="SliceItemNode"/> with one colon and optional <paramref name="start"/> and
    /// <paramref name="stop"/> values.
    /// </summary>
    internal static SliceItemNode Create(ref int idNext, Token backStart, ExprNode start,
        Token col1, Token backStop, Token starStop, ExprNode stop)
    {
        Validation.AssertValue(col1);
        Validation.Assert(col1.Kind == TokKind.Colon);
        return new SliceItemNode(ref idNext, backStart, null, start, col1, backStop, starStop, stop, null, null);
    }

    /// <summary>
    /// Create a <see cref="SliceItemNode"/> with two colons, optional <paramref name="start"/> and
    /// <paramref name="stop"/> values and required (non-null) <paramref name="step"/> value.
    /// </summary>
    internal static SliceItemNode Create(ref int idNext, Token backStart, ExprNode start,
        Token col1, Token backStop, Token starStop, ExprNode stop, Token col2, ExprNode step)
    {
        Validation.AssertValue(col1);
        Validation.Assert(col1.Kind == TokKind.Colon);
        Validation.AssertValue(col2);
        Validation.Assert(col2.Kind == TokKind.Colon);
        return new SliceItemNode(ref idNext, backStart, null, start, col1, backStop, starStop, stop, col2, step);
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        if (visitor.PreVisit(this))
        {
            Start?.Accept(visitor);
            Stop?.Accept(visitor);
            Step?.Accept(visitor);
            visitor.PostVisit(this);
        }
    }

    public override SourceRange GetFullRange()
    {
        Validation.Assert(Start != null || Colon1 != null);

        SourceRange rng;
        if (Start != null)
            rng = Start.GetFullRange();
        else
            rng = Colon1.Range;

        if (StartBack != null)
            rng = rng.Union(StartBack.Range);
        if (StartEdge != null)
            rng = rng.Union(StartEdge.Range);
        if (Colon1 == null)
            return rng;

        return rng.Union(Step?.GetFullRange() ?? Colon2?.Range ?? Stop?.GetFullRange());
    }
}

/// <summary>
/// An indexing expression.
/// </summary>
public sealed partial class IndexingNode : ExprNode
{
    /// <summary>
    /// The value being indexed.
    /// </summary>
    public ExprNode Child { get; }

    /// <summary>
    /// The indexing (slice item) arguments.
    /// </summary>
    public SliceListNode Items { get; }

    /// <summary>
    /// The open square bracket.
    /// </summary>
    public Token TokOpen => Token;

    /// <summary>
    /// The closing square bracket, when present.
    /// </summary>
    public Token TokClose { get; }

    private IndexingNode(ref int idNext, Token tokOpen, ExprNode child, SliceListNode items, Token tokClose)
        : base(ref idNext, tokOpen, GetDepth(child, items) + 1)
    {
        Validation.AssertValue(child);
        Validation.AssertValue(items);
        Validation.AssertValueOrNull(tokClose);

        Child = child;
        Items = items;
        TokClose = tokClose;
    }

    internal static IndexingNode Create(ref int idNext, Token tokOpen, ExprNode child, SliceListNode items, Token tokClose)
    {
        Validation.AssertValue(tokOpen);
        Validation.AssertValue(child);
        Validation.AssertValue(items);
        return new IndexingNode(ref idNext, tokOpen, child, items, tokClose);
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
        {
            AcceptChild(visitor, Child);
            AcceptChild(visitor, Items);
            visitor.PostVisit(this);
        }
    }

    public override SourceRange GetFullRange()
    {
        return Child.GetFullRange().Union(TokClose?.Range ?? Items.GetFullRange());
    }
}

/// <summary>
/// A variable declaration, which consists of 'name : value' or 'value as name'. The name can either be an
/// identifier or a "box".
/// </summary>
public sealed partial class VariableDeclNode : ExprNode
{
    /// <summary>
    /// The variable name. Note this is non-null iff <see cref="Box"/> is null.
    /// </summary>
    public Identifier Variable { get; }

    /// <summary>
    /// Either this or <see cref="Variable"/> is non-null. If this is, then this came from
    /// parsing "_: ...", rather than "name: ...".
    /// </summary>
    public KeyToken Box { get; }

    /// <summary>
    /// The value after the colon.
    /// </summary>
    public ExprNode Value { get; }

    internal VariableDeclNode(ref int idNext, Token tok, Identifier variable, ExprNode value)
        : base(ref idNext, tok, GetDepth(value) + 1)
    {
        Validation.AssertValue(variable);
        Validation.AssertValue(value);
        Variable = variable;
        Value = value;
    }

    internal VariableDeclNode(ref int idNext, Token tok, KeyToken box, ExprNode value)
        : base(ref idNext, tok, GetDepth(value) + 1)
    {
        Validation.AssertValue(box);
        Validation.Assert(box.Kind == TokKind.Box);
        Validation.AssertValue(value);
        Box = box;
        Value = value;
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
        {
            AcceptChild(visitor, Value);
            visitor.PostVisit(this);
        }
    }

    public override SourceRange GetFullRange()
    {
        return Value.GetFullRange().Union(Variable?.Range ?? Box.Range);
    }
}

/// <summary>
/// A directive token followed by an expression. For example, the second argument of Sort(T, [<] F).
/// </summary>
public sealed partial class DirectiveNode : ExprNode
{
    /// <summary>
    /// The directive.
    /// </summary>
    public Directive Directive { get; }

    /// <summary>
    /// The directive token.
    /// </summary>
    public Token DirToken => Token;

    /// <summary>
    /// The value after the directive.
    /// </summary>
    public ExprNode Value { get; }

    internal DirectiveNode(ref int idNext, Token tok, Directive dir, ExprNode value)
        : base(ref idNext, tok, GetDepth(value) + 1)
    {
        Validation.Assert(dir != default);
        Validation.AssertValue(value);
        Validation.Assert(value.Kind != NodeKind.Directive, "Unexpected nested directive");
        Directive = dir;
        Value = value;
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
        {
            AcceptChild(visitor, Value);
            visitor.PostVisit(this);
        }
    }

    public override SourceRange GetFullRange()
    {
        return DirToken.Range.Union(Value.GetFullRange());
    }
}

/// <summary>
/// An "if" node is: node if node else node.
/// </summary>
public sealed partial class IfNode : ExprNode
{
    /// <summary>
    /// The result value when the condition is true.
    /// </summary>
    public ExprNode TrueValue { get; }

    /// <summary>
    /// The condition.
    /// </summary>
    public ExprNode Condition { get; }

    /// <summary>
    /// The result value when the condition is false.
    /// </summary>
    public ExprNode FalseValue { get; }

    /// <summary>
    /// The "else" token, which may be missing.
    /// </summary>
    public Token TokElse { get; }

    internal IfNode(ref int idNext, Token tok, ExprNode trueValue, ExprNode cond, Token tokElse, ExprNode falseValue)
        : base(ref idNext, tok, GetDepth(trueValue, cond, falseValue) + 1)
    {
        Validation.AssertValue(trueValue);
        Validation.AssertValue(cond);
        Validation.AssertValueOrNull(tokElse);
        Validation.AssertValue(falseValue);

        TrueValue = trueValue;
        Condition = cond;
        FalseValue = falseValue;
        TokElse = tokElse;
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
        {
            AcceptChild(visitor, TrueValue);
            AcceptChild(visitor, Condition);
            AcceptChild(visitor, FalseValue);
            visitor.PostVisit(this);
        }
    }

    public override SourceRange GetFullRange()
    {
        return TrueValue.GetFullRange().Union(FalseValue.GetFullRange());
    }
}

/// <summary>
/// A record: { field-defns }.
/// </summary>
public sealed partial class RecordNode : ExprNode
{
    /// <summary>
    /// The items. Each item is either a VariableDeclNode or a FirstNameNode.
    /// </summary>
    public ExprListNode Items { get; }

    /// <summary>
    /// The open curly brace.
    /// </summary>
    public Token TokOpen { get { return Token; } }

    /// <summary>
    /// The close curly token, when present.
    /// </summary>
    public Token TokClose { get; }

    internal RecordNode(ref int idNext, Token tok, ExprListNode items, Token tokClose)
        : base(ref idNext, tok, GetDepth(items) + 1)
    {
        Validation.AssertValue(items);
        Validation.AssertValueOrNull(tokClose);

#if DEBUG
        for (int i = 0; i < items.Children.Length; i++)
        {
            var value = items.Children[i];
            Validation.Assert(
                value is VariableDeclNode || value is FirstNameNode || value is DottedNameNode,
                "Each record item should be a VariableDeclNode or FirstNameNode or DottedNameNode");
        }
#endif

        Items = items;
        TokClose = tokClose;
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
        {
            AcceptChild(visitor, Items);
            visitor.PostVisit(this);
        }
    }

    public override SourceRange GetFullRange()
    {
        return TokOpen.Range.Union(TokClose?.Range ?? Items.GetFullRange());
    }
}

/// <summary>
/// A sequence literal.
/// </summary>
public sealed partial class SequenceNode : ExprNode
{
    /// <summary>
    /// The items.
    /// </summary>
    public ExprListNode Items { get; }

    /// <summary>
    /// The open square brace.
    /// </summary>
    public Token TokOpen { get { return Token; } }

    /// <summary>
    /// The close square brace, when present, or trailing comma, if present.
    /// </summary>
    public Token TokClose { get; }

    internal SequenceNode(ref int idNext, Token tok, ExprListNode items, Token tokClose)
        : base(ref idNext, tok, GetDepth(items) + 1)
    {
        Validation.AssertValue(items);
        Validation.AssertValueOrNull(tokClose);

        Items = items;
        TokClose = tokClose;
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
        {
            AcceptChild(visitor, Items);
            visitor.PostVisit(this);
        }
    }

    public override SourceRange GetFullRange()
    {
        return TokOpen.Range.Union(TokClose?.Range ?? Items.GetFullRange());
    }
}

/// <summary>
/// A tuple literal.
/// </summary>
public sealed partial class TupleNode : ExprNode
{
    /// <summary>
    /// The items.
    /// </summary>
    public ExprListNode Items { get; }

    /// <summary>
    /// The open paren.
    /// </summary>
    public Token TokOpen { get { return Token; } }

    /// <summary>
    /// The close paren, when present, or trailing comma, if present.
    /// </summary>
    public Token TokClose { get; }

    internal TupleNode(ref int idNext, Token tok, ExprListNode items, Token tokClose)
        : base(ref idNext, tok, GetDepth(items) + 1)
    {
        Validation.AssertValue(items);
        Validation.AssertValueOrNull(tokClose);

        Items = items;
        TokClose = tokClose;
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
        {
            AcceptChild(visitor, Items);
            visitor.PostVisit(this);
        }
    }

    public override SourceRange GetFullRange()
    {
        return TokOpen.Range.Union(TokClose?.Range ?? Items.GetFullRange());
    }
}

/// <summary>
/// A module symbol declaration.
/// </summary>
public abstract class SymbolDeclNode : ExprNode
{
    /// <summary>
    /// The kind of symbol.
    /// </summary>
    public ModSymKind SymKind { get; }

    /// <summary>
    /// The token determining the symbol kind, possibly null.
    /// </summary>
    public Token KindToken { get; }

    /// <summary>
    /// The symbol name (non-null).
    /// </summary>
    public Identifier Name { get; }

    private protected SymbolDeclNode(ref int idNext,
            Token tok, Token tokKind, ModSymKind sk, Identifier name, int depth)
        : base(ref idNext, tok, depth)
    {
        Validation.Assert(sk.IsValid());
        Validation.AssertValueOrNull(tokKind);
        Validation.AssertValue(name);

        SymKind = sk;
        KindToken = tokKind;
        Name = name;
    }
}

/// <summary>
/// A module symbol declaration, which consists of 'kind name := value' or 'kind name default value'.
/// </summary>
public sealed partial class ValueSymDeclNode : SymbolDeclNode
{
    /// <summary>
    /// The value after the <c>:=</c>.
    /// </summary>
    public ExprNode Value { get; }

    internal ValueSymDeclNode(ref int idNext, Token tok, Token tokKind, ModSymKind sk, Identifier name, ExprNode value)
        : base(ref idNext, tok, tokKind, sk, name, GetDepth(value) + 1)
    {
        Validation.Assert(sk.IsValid());
        Validation.Assert(sk != ModSymKind.FreeVariable);
        Validation.AssertValue(value);

        Value = value;
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
        {
            AcceptChild(visitor, Value);
            visitor.PostVisit(this);
        }
    }

    public override SourceRange GetFullRange()
    {
        return Name.Range.Union(Value.GetFullRange());
    }
}

/// <summary>
/// A free variable definition statment.
/// </summary>
public sealed partial class FreeVarDeclNode : SymbolDeclNode
{
    /// <summary>
    /// The token for the 'in' clause. May be null. If the 'in' clause is non-null then both
    /// 'from' and 'to' will be null.
    /// </summary>
    public Token TokIn { get; }

    /// <summary>
    /// The value for the 'in' clause. May be null.
    /// </summary>
    public ExprNode ValueIn { get; }

    /// <summary>
    /// The token for the 'from' clause. May be null.
    /// </summary>
    public Token TokFrom { get; }

    /// <summary>
    /// The value for the 'from' clause. May be null.
    /// </summary>
    public ExprNode ValueFrom { get; }

    /// <summary>
    /// The token for the 'to' clause. May be null.
    /// </summary>
    public Token TokTo { get; }

    /// <summary>
    /// The value for the 'to' clause. May be null.
    /// </summary>
    public ExprNode ValueTo { get; }

    /// <summary>
    /// The token for the 'default' clause. May be null.
    /// </summary>
    public Token TokDef { get; }

    /// <summary>
    /// The value for the 'default' clause. May be null.
    /// </summary>
    public ExprNode ValueDef { get; }

    /// <summary>
    /// The token for the optional/required. May be null.
    /// </summary>
    public Token TokOptReq { get; }

    internal FreeVarDeclNode(
            ref int idNext, Token tok, Token tokKind, Identifier name,
            Token tokIn, ExprNode valIn,
            Token tokFr, ExprNode valFr, Token tokTo, ExprNode valTo,
            Token tokDef, ExprNode valDef, Token tokOptReq)
        : base(ref idNext, tok, tokKind, ModSymKind.FreeVariable, name,
              Math.Max(GetDepth(valIn), Math.Max(GetDepth(valFr), GetDepth(valTo))) + 1)
    {
        Validation.AssertValue(name);
        Validation.Assert(tokIn is null || valIn is not null);
        Validation.Assert(tokFr is null || valFr is not null);
        Validation.Assert(tokTo is null || valTo is not null);
        Validation.Assert(tokDef is null || valDef is not null);
        Validation.Assert(tokIn is null || tokFr is null);
        Validation.Assert(tokIn is null || tokTo is null);
        Validation.Assert(tokOptReq == null || tokOptReq.Kind == TokKind.KtxOpt || tokOptReq.Kind == TokKind.KtxReq);

        TokIn = tokIn;
        ValueIn = valIn;
        TokFrom = tokFr;
        ValueFrom = valFr;
        TokTo = tokTo;
        ValueTo = valTo;
        TokDef = tokDef;
        ValueDef = valDef;
        TokOptReq = tokOptReq;
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
        {
            AcceptChildOpt(visitor, ValueIn);
            AcceptChildOpt(visitor, ValueFrom);
            AcceptChildOpt(visitor, ValueTo);
            AcceptChildOpt(visitor, ValueDef);
            visitor.PostVisit(this);
        }
    }

    public int ChildCount
    {
        get
        {
            return
                Util.ToNum(ValueIn != null) +
                Util.ToNum(ValueFrom != null) +
                Util.ToNum(ValueTo != null) +
                Util.ToNum(ValueDef != null);
        }
    }

    public override SourceRange GetFullRange()
    {
        return Token.Range
            .Union(ValueIn?.GetFullRange())
            .Union(ValueFrom?.GetFullRange())
            .Union(ValueTo?.GetFullRange())
            .Union(TokOptReq?.Range);
    }
}

/// <summary>
/// A module expression.
/// </summary>
public sealed partial class ModuleNode : ExprNode
{
    /// <summary>
    /// The symbol definitions of the module.
    /// </summary>
    public SymListNode Definitions { get; }

    /// <summary>
    /// The open curly brace, when present.
    /// </summary>
    public Token TokOpen { get; }

    /// <summary>
    /// The close curly brace, when present.
    /// </summary>
    public Token TokClose { get; }

    internal ModuleNode(ref int idNext, Token tok, Token tokOpen, SymListNode defns, Token tokClose)
        : base(ref idNext, tok, GetDepth(defns) + 1)
    {
        Validation.AssertValueOrNull(tokOpen);
        Validation.AssertValue(defns);
        Validation.AssertValueOrNull(tokClose);

        Definitions = defns;
        TokOpen = tokOpen;
        TokClose = tokClose;
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
        {
            AcceptChild(visitor, Definitions);
            visitor.PostVisit(this);
        }
    }

    public override SourceRange GetFullRange()
    {
        return Token.Range.Union(TokClose?.Range ?? Definitions.GetFullRange());
    }
}

/// <summary>
/// A command (contextual keyword) followed by an <see cref="IdentPath"/>, as a <see cref="StmtNode"/>.
/// </summary>
public sealed partial class TaskCmdStmtNode : StmtNode
{
    public override bool NeedsSemi => true;

    /// <summary>
    /// The command as a <see cref="TokKind"/>.
    /// </summary>
    public TokKind Cmd => Token.TokenAlt.Kind;

    /// <summary>
    /// The name as an <see cref="IdentPath"/>.
    /// </summary>
    public IdentPath Name { get; }

    internal TaskCmdStmtNode(ref int idNext, Token tok, IdentPath name)
        : base(ref idNext, tok, 1)
    {
        Validation.Assert(Token.Kind.IsTaskCmd());
        Validation.AssertValue(name);
        Name = name;
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
            visitor.PostVisit(this);
    }

    public override SourceRange GetFullRange()
    {
        return Token.Range.Union(Name.Range);
    }
}

/// <summary>
/// A task definition from a procedure invocation: cmd [name as] proc-invocation.
/// </summary>
public sealed partial class TaskProcStmtNode : ValueStmtNode
{
    public override bool NeedsSemi => true;

    /// <summary>
    /// The modifier, <c>task</c>, <c>play</c>, etc, as a <see cref="TokKind"/>.
    /// </summary>
    public TokKind Modifier => Token.TokenAlt.Kind;

    /// <summary>
    /// The identifier path on the lhs (non null/empty).
    /// </summary>
    public IdentPath IdentPath { get; }

    /// <summary>
    /// The <c>as</c> token, when present.
    /// </summary>
    public Token TokAs { get; }

    /// <summary>
    /// Constructor for when there is no name.
    /// </summary>
    internal TaskProcStmtNode(ref int idNext, Token tok, ExprNode value)
        : base(ref idNext, tok, value)
    {
        Validation.Assert(Token.Kind.IsTaskModifier());
    }

    /// <summary>
    /// Constructor for when there is a name.
    /// </summary>
    internal TaskProcStmtNode(ref int idNext, Token tok, IdentPath idents, Token tokAs, ExprNode value)
        : base(ref idNext, tok, value)
    {
        Validation.Assert(Token.Kind.IsTaskModifier());
        Validation.AssertValue(idents);
        Validation.AssertValueOrNull(tokAs);
        IdentPath = idents;
        TokAs = tokAs;
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
        {
            AcceptChild(visitor, Value);
            visitor.PostVisit(this);
        }
    }

    public override SourceRange GetFullRange()
    {
        return Token.Range.Union(Value.GetFullRange());
    }
}

/// <summary>
/// A task definition from a block of statements:
/// <c>cmd name [with rec] [prime { ... }] as { ... }</c>.
/// </summary>
public sealed partial class TaskBlockStmtNode : StmtNode
{
    public override bool NeedsSemi => false;

    /// <summary>
    /// The modifier, <c>task</c>, <c>play</c>, etc, as a <see cref="TokKind"/>.
    /// </summary>
    public TokKind Modifier => Token.TokenAlt.Kind;

    /// <summary>
    /// The identifier path (non null/empty).
    /// </summary>
    public IdentPath IdentPath { get; }

    /// <summary>
    /// The full name (path).
    /// </summary>
    public NPath FullName => IdentPath.FullName;

    /// <summary>
    /// The optional <c>with</c> token.
    /// </summary>
    public Token TokWith { get; }

    /// <summary>
    /// The expression following <c>with</c>, when present. Should bind to a record. The fields
    /// become globals in the task body / script.
    /// </summary>
    public ExprNode With { get; }

    /// <summary>
    /// The optional <c>prime</c> token.
    /// </summary>
    public Token TokPrime { get; }

    /// <summary>
    /// The block following <c>prime</c>, when present.
    /// </summary>
    public BlockStmtNode Prime { get; }

    /// <summary>
    /// The <c>as</c> token, when present.
    /// </summary>
    public Token TokAs { get; }

    /// <summary>
    /// The body (statements) of the task.
    /// </summary>
    public BlockStmtNode Body { get; }

    internal TaskBlockStmtNode(ref int idNext, Token tok, IdentPath idents, Token tokWith, ExprNode with,
            Token tokPrime, BlockStmtNode prime, Token tokAs, BlockStmtNode body)
        : base(ref idNext, tok, GetDepth(with, prime, body) + 1)
    {
        Validation.Assert(Token.Kind.IsTaskModifier());
        Validation.AssertValue(idents);
        Validation.AssertValueOrNull(tokWith);
        Validation.AssertValueOrNull(with);
        Validation.Assert((tokWith == null) == (with == null));
        Validation.AssertValueOrNull(tokPrime);
        Validation.AssertValueOrNull(prime);
        Validation.Assert((tokPrime == null) == (prime == null));
        Validation.AssertValueOrNull(tokAs);
        Validation.AssertValue(body);

        IdentPath = idents;
        TokWith = tokWith;
        With = with;
        TokPrime = tokPrime;
        Prime = prime;
        TokAs = tokAs;
        Body = body;
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
        {
            AcceptChildOpt(visitor, With);
            AcceptChildOpt(visitor, Prime);
            AcceptChild(visitor, Body);
            visitor.PostVisit(this);
        }
    }

    public override SourceRange GetFullRange()
    {
        return Token.Range.Union(Body.GetFullRange());
    }
}

/// <summary>
/// A procedure definition from a block of statements:
/// <c>proc name(A, B, ...) [prime { ... }] as { ... }</c>.
/// </summary>
public sealed partial class UserProcStmtNode : StmtNode
{
    public override bool NeedsSemi => false;

    /// <summary>
    /// The identifier path of the function.
    /// </summary>
    public IdentPath IdentPath { get; }

    /// <summary>
    /// The open paren token.
    /// </summary>
    public Token TokOpen { get; }

    /// <summary>
    /// The parameter names.
    /// </summary>
    public IdentTuple ParamIdents { get; }

    /// <summary>
    /// The parameter names.
    /// </summary>
    public NameTuple ParamNames { get; }

    /// <summary>
    /// The close paren, possibly null (when missing).
    /// </summary>
    public Token TokClose { get; }

    /// <summary>
    /// The optional <c>prime</c> token.
    /// </summary>
    public Token TokPrime { get; }

    /// <summary>
    /// The block following <c>prime</c>, when present.
    /// </summary>
    public BlockStmtNode Prime { get; }

    /// <summary>
    /// The <c>play</c> or <c>as</c> token, when present.
    /// </summary>
    public Token TokPlay { get; }

    /// <summary>
    /// The block following <c>play</c>, defining the main body of the task.
    /// </summary>
    public BlockStmtNode Play { get; }

    internal UserProcStmtNode(ref int idNext, Token tok, IdentPath path,
            Token tokOpen, IdentTuple paramIdents, Token tokClose,
            Token tokPrime, BlockStmtNode prime, Token tokPlay, BlockStmtNode play)
        : base(ref idNext, tok, GetDepth(prime, play) + 1)
    {
        Validation.AssertValue(path);
        Validation.AssertValue(tokOpen);
        Validation.AssertValueOrNull(tokClose);
        Validation.AssertValueOrNull(tokPrime);
        Validation.AssertValueOrNull(prime);
        Validation.Assert((tokPrime == null) == (prime == null));
        Validation.AssertValueOrNull(tokPlay);
        Validation.AssertValue(play);

        IdentPath = path;
        TokOpen = tokOpen;
        ParamIdents = paramIdents;
        ParamNames = GetNamesFromIdents(paramIdents);
        TokClose = tokClose;
        TokPrime = tokPrime;
        Prime = prime;
        TokPlay = tokPlay;
        Play = play;
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
        {
            AcceptChildOpt(visitor, Prime);
            AcceptChild(visitor, Play);
            visitor.PostVisit(this);
        }
    }

    public override SourceRange GetFullRange()
    {
        return Token.Range.Union(Play.GetFullRange());
    }
}


/// <summary>
/// Projection.
/// </summary>
public abstract class ProjectionNode : ExprNode
{
    /// <summary>
    /// The source value.
    /// </summary>
    public ExprNode Source { get; }

    /// <summary>
    /// The projection value.
    /// </summary>
    public ExprNode Value { get; }

    internal ProjectionNode(ref int idNext, Token tok, ExprNode src, ExprNode value)
        : base(ref idNext, tok, GetDepth(src, value) + 1)
    {
        Validation.AssertValue(src);
        Validation.AssertValue(value);

        Source = src;
        Value = value;
    }

    public sealed override SourceRange GetFullRange()
    {
        return Source.GetFullRange().Union(Value.GetFullRange());
    }
}

/// <summary>
/// Record projection, syntax: source->{field-defns}.
/// </summary>
public sealed partial class RecordProjectionNode : ProjectionNode
{
    /// <summary>
    /// The projection record definition.
    /// </summary>
    public RecordNode Record { get; }

    /// <summary>
    /// Whether this is a "concat" projection, meaning that the source must be record and this
    /// does a concatenation rather than just compute.
    /// </summary>
    public bool IsConcat { get; }

    internal RecordProjectionNode(ref int idNext, Token tok, ExprNode src, bool isConcat, RecordNode rec)
        : base(ref idNext, tok, src, rec)
    {
        Validation.AssertValue(rec);
        Record = rec;
        IsConcat = isConcat;
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
        {
            AcceptChild(visitor, Source);
            AcceptChild(visitor, Record);
            visitor.PostVisit(this);
        }
    }
}

/// <summary>
/// Module projection, syntax: source=>{field-defns} or source=>(expr). Note that <see cref="Record"/> is not
/// necessarily a <see cref="RecordNode"/>.
/// </summary>
public sealed partial class ModuleProjectionNode : ProjectionNode
{
    /// <summary>
    /// The projection record definition.
    /// </summary>
    public ExprNode Record { get; }

    internal ModuleProjectionNode(ref int idNext, Token tok, ExprNode src, ExprNode rec)
        : base(ref idNext, tok, src, rec)
    {
        Validation.AssertValue(rec);
        Record = rec;
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
        {
            AcceptChild(visitor, Source);
            AcceptChild(visitor, Record);
            visitor.PostVisit(this);
        }
    }
}

/// <summary>
/// Tuple projection, syntax: source->(values).
/// </summary>
public sealed partial class TupleProjectionNode : ProjectionNode
{
    /// <summary>
    /// The projection tuple definition.
    /// </summary>
    public TupleNode Tuple { get; }

    /// <summary>
    /// Whether this is a "concat" projection, meaning that the source must be tuple and this
    /// does a concatenation rather than just compute.
    /// </summary>
    public bool IsConcat { get; }

    internal TupleProjectionNode(ref int idNext, Token tok, ExprNode src, bool isConcat, TupleNode tup)
        : base(ref idNext, tok, src, tup)
    {
        Validation.AssertValue(tup);
        Tuple = tup;
        IsConcat = isConcat;
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
        {
            AcceptChild(visitor, Source);
            AcceptChild(visitor, Tuple);
            visitor.PostVisit(this);
        }
    }
}

/// <summary>
/// Value projection, syntax: source->(value).
/// </summary>
public sealed partial class ValueProjectionNode : ProjectionNode
{
    internal ValueProjectionNode(ref int idNext, Token tok, ExprNode src, ExprNode value)
        : base(ref idNext, tok, src, value)
    {
    }

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        if (visitor.PreVisit(this))
        {
            AcceptChild(visitor, Source);
            AcceptChild(visitor, Value);
            visitor.PostVisit(this);
        }
    }
}

/// <summary>
/// Abstract base class for parse node visitors.
/// </summary>
public abstract partial class RexlTreeVisitor
{
    /// <summary>
    /// This tracks the rexl nodes being processed by depth, so tracks ancestry.
    /// </summary>
    private readonly List<RexlNode> _srcs;

    protected RexlTreeVisitor()
    {
        _srcs = new List<RexlNode>();
        _srcs.Add(null);
    }

    private void Enter(RexlNode node)
    {
        Validation.AssertValue(node);
        EnterCore(node);

        Validation.Assert(!_srcs.Contains(node));
        _srcs.Add(node);
    }

    private void Leave(RexlNode node)
    {
        Validation.AssertValue(node);
        Validation.Assert(_srcs.Peek() == node);
        _srcs.Pop();

        LeaveCore(node);
    }

    protected virtual void EnterCore(RexlNode node) { }
    protected virtual void LeaveCore(RexlNode node) { }

    protected int Depth => _srcs.Count - 1;

    protected RexlNode PeekSrc(int count = 0)
    {
        Validation.AssertIndex(count, _srcs.Count);
        return _srcs[_srcs.Count - count - 1];
    }

    protected virtual bool PreVisitCore(RexlNode node) { return true; }
}

/// <summary>
/// Abstract base class for visitors that do nothing for many node types.
/// </summary>
public abstract partial class NoopTreeVisitor : RexlTreeVisitor
{
    protected virtual void VisitCore(RexlNode node) { }

    protected virtual void PostVisitCore(RexlNode node) { }
}

/// <summary>
/// Base class for a persistent map from <see cref="RexlNode"/> (or sub-class) to something.
/// This disallows null node. Note that a sub-class can override methods appropriately to
/// handle null if it needs to.
/// </summary>
public abstract class RexlNodeMapping<TTree, TKey, TVal> : RedBlackTree<TTree, TKey, TVal>
    where TTree : RexlNodeMapping<TTree, TKey, TVal>
    where TKey : RexlNode
{
    protected RexlNodeMapping(Node root)
        : base(root)
    {
    }

    protected override bool KeyIsValid(TKey key)
    {
        return key != null;
    }

    protected override int KeyCompare(TKey key0, TKey key1)
    {
        Validation.AssertValue(key0);
        Validation.AssertValue(key1);
        Validation.Assert(key0.Ordinal != key1.Ordinal || key0 == key1);
        return key0 == key1 ? 0 : key0.Ordinal < key1.Ordinal ? -1 : +1;
    }

    protected override bool KeyEquals(TKey key0, TKey key1)
    {
        Validation.AssertValue(key0);
        Validation.AssertValue(key1);
        Validation.Assert(key0.Ordinal != key1.Ordinal || key0 == key1);
        return key1 == key0;
    }

    protected override int KeyHash(TKey key)
    {
        Validation.AssertValue(key);
        return key.GetHashCode();
    }
}
