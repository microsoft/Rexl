// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// WARNING: This .cs file is generated from the corresponding .tt file. DO NOT edit this .cs directly.

namespace Microsoft.Rexl.Lex;

internal static class RexlKeyWords
{
    internal static (string kwd, TokKind tokKind)[] Keywords = new (string, TokKind)[]
    {
        ("_", TokKind.Box),
        ("if", TokKind.KwdIf),
        ("then", TokKind.KwdThen),
        ("else", TokKind.KwdElse),
        ("goto", TokKind.KwdGoto),
        ("while", TokKind.KwdWhile),
        ("for", TokKind.KwdFor),
        ("break", TokKind.KwdBreak),
        ("continue", TokKind.KwdContinue),
        ("not", TokKind.KwdNot),
        ("bnot", TokKind.KwdBnot),
        ("true", TokKind.KwdTrue),
        ("false", TokKind.KwdFalse),
        ("null", TokKind.KwdNull),
        ("this", TokKind.KwdThis),
        ("it", TokKind.KwdIt),
        ("in", TokKind.KwdIn),
        ("has", TokKind.KwdHas),
        ("as", TokKind.KwdAs),
        ("is", TokKind.KwdIs),
        ("from", TokKind.KwdFrom),
        ("to", TokKind.KwdTo),
        ("with", TokKind.KwdWith),
        ("import", TokKind.KwdImport),
        ("execute", TokKind.KwdExecute),
        ("namespace", TokKind.KwdNamespace),
    };

    internal static bool IsKeyword(string keyword)
    {
        switch (keyword)
        {
        case "_":
        case "if":
        case "then":
        case "else":
        case "goto":
        case "while":
        case "for":
        case "break":
        case "continue":
        case "not":
        case "bnot":
        case "true":
        case "false":
        case "null":
        case "this":
        case "it":
        case "in":
        case "has":
        case "as":
        case "is":
        case "from":
        case "to":
        case "with":
        case "import":
        case "execute":
        case "namespace":
            return true;
        default:
            return false;
        }
    }
}
