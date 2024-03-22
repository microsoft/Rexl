// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Rexl.Lex;

/// <summary>
/// Extension methods for <see cref="TokKind"/>.
/// </summary>
public static class TokKindExtensions
{
    private const TokKind _kwdMin = TokKind.Box;
    private const TokKind _kwdMax = _ktxMin - 1;

    private const TokKind _ktxMin = TokKind.KtxFunc;
    private const TokKind _ktxMax = _dirMax - 1;

    private const TokKind _ktxOperMin = TokKind.KtxOr;
    private const TokKind _ktxOperMax = TokKind.KtxShru;

    private const TokKind _dirMin = TokKind.DirCi;
    private const TokKind _dirMax = TokKind._Lim - 1;

    /// <summary>
    /// Returns whether the given <paramref name="kind"/> is valid.
    /// </summary>
    public static bool IsValid(this TokKind kind)
    {
        return TokKind.None < kind && kind < TokKind._Lim;
    }

    /// <summary>
    /// Returns whether the given <paramref name="kind"/> is a directive.
    /// </summary>
    public static bool IsDirective(this TokKind kind)
    {
        return _dirMin <= kind && kind <= _dirMax;
    }

    /// <summary>
    /// Returns whether the given <paramref name="kind"/> is a reserved keyword (as opposed to contextual).
    /// </summary>
    public static bool IsReservedKwd(this TokKind kind)
    {
        return _kwdMin <= kind && kind <= _kwdMax;
    }

    /// <summary>
    /// Returns whether the given <paramref name="kind"/> is a contextual keyword, meaning that
    /// it can also be used as an identifier without quoting. Whether it is an identifier is
    /// determined by syntactic context.
    /// </summary>
    public static bool IsContextualKwd(this TokKind kind)
    {
        return _ktxMin <= kind && kind <= _ktxMax;
    }

    /// <summary>
    /// Returns whether the given <paramref name="kind"/> is a keyword, either reserved or contextual.
    /// </summary>
    public static bool IsKeyword(this TokKind kind)
    {
        return _kwdMin <= kind && kind <= _kwdMax || _ktxMin <= kind && kind <= _ktxMax;
    }

    /// <summary>
    /// Returns whether the given <paramref name="kind"/> is a contextual keyword based operator.
    /// </summary>
    public static bool IsKtxOperator(this TokKind kind)
    {
        return _ktxOperMin <= kind && kind <= _ktxOperMax;
    }

    /// <summary>
    /// Whether this is a task modifier.
    /// </summary>
    public static bool IsTaskModifier(this TokKind kind)
    {
        switch (kind)
        {
        case TokKind.KtxTask:
        case TokKind.KtxPrime:
        case TokKind.KtxPlay:
        case TokKind.KtxPause:
        case TokKind.KtxPoke:
        case TokKind.KtxPoll:
        case TokKind.KtxFinish:
        case TokKind.KtxAbort:
            return true;
        }

        return false;
    }

    /// <summary>
    /// Whether this is a task "command". This is true for all task modifiers except <c>task</c>.
    /// </summary>
    public static bool IsTaskCmd(this TokKind kind)
    {
        switch (kind)
        {
        case TokKind.KtxPrime:
        case TokKind.KtxPlay:
        case TokKind.KtxPause:
        case TokKind.KtxPoke:
        case TokKind.KtxPoll:
        case TokKind.KtxFinish:
        case TokKind.KtxAbort:
            return true;
        }

        return false;
    }
}
