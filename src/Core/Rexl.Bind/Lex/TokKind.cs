// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// WARNING: This .cs file is generated from the corresponding .tt file. DO NOT edit this .cs directly.

namespace Microsoft.Rexl.Lex;

/// <summary>
/// Token kinds.
/// </summary>
public enum TokKind
{
    // Critical that this is zero.
    None = 0,

    // Miscellaneous.
    Eof,
    Error,

    // Identifiers and literals.
    Ident,
    IntLit,
    FltLit,
    TxtLit,

    // Special.
    ItSlot,
    HashSlot,

    // Comments.
    CommentLine,
    CommentBlock,

    // Punctuators.
    ParenOpen,
    ParenClose,
    CurlyOpen,
    CurlyClose,
    SquareOpen,
    SquareClose,

    Dot,
    Bng,
    Tld,
    Comma,
    Semi,
    Colon,
    At,
    Dol,
    Hash,

    Add,
    Sub,
    Mul,
    Div,
    Per,
    Bar,
    Amp,
    Car,
    AddAdd,
    MulMul,
    BarBar,
    AmpAmp,
    CarCar,

    Equ,
    Lss,
    LssEqu,
    GrtEqu,
    Grt,

    LssLss,
    GrtGrt,
    GrtGrtGrt,

    SubGrt,
    AddGrt,
    EquGrt,
    ColEqu,
    Que,
    QueQue,

    // Reserved Keywords.
    Box,
    KwdIf,
    KwdThen,
    KwdElse,
    KwdGoto,
    KwdWhile,
    KwdFor,
    KwdBreak,
    KwdContinue,
    KwdNot,
    KwdBnot,
    KwdTrue,
    KwdFalse,
    KwdNull,
    KwdThis,
    KwdIt,
    KwdIn,
    KwdHas,
    KwdAs,
    KwdIs,
    KwdFrom,
    KwdTo,
    KwdWith,
    KwdImport,
    KwdExecute,
    KwdNamespace,

    // Contextual Keywords.

    // For user defined callable things.
    KtxFunc,
    // REVIEW: "prop" and "proc" are very similar. Will this be too error-prone?
    KtxProp,
    KtxProc,

    // Operators.
    KtxOr,
    KtxXor,
    KtxAnd,
    KtxDiv,
    KtxMod,
    KtxMin,
    KtxMax,
    KtxBor,
    KtxBxor,
    KtxBand,
    KtxShl,
    KtxShr,
    KtxShri,
    KtxShru,

    // For modules
    KtxModule,

    // For module symbols.
    KtxParam,
    KtxConst,
    KtxVar,
    KtxLet,
    KtxCon,
    KtxMsr,

    // For module symbol domains.
    KtxOpt,
    KtxReq,
    KtxDef,

    // For tasks.
    KtxTask,
    KtxPrime,
    KtxPlay,
    KtxPause,
    KtxPoke,
    KtxPoll,
    KtxFinish,
    KtxAbort,

    // For task bodies.
    KtxPublish,
    KtxPrimary,
    KtxStream,

    // Directives.

    // For sorting.
    DirCi,
    DirUp,
    DirDown,
    DirUpCi,
    DirDownCi,

    // For keys.
    DirEq,
    DirEqCi,
    DirKey,

    // For GroupBy.
    DirAgg,
    DirMap,
    DirAuto,

    // For Guard.
    DirWith,
    DirGuard,

    // General.
    DirIf,
    DirWhile,
    DirElse,

    // For MultiFormFunc.
    DirTop,

    _Lim
}
