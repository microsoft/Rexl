// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// WARNING: This .cs file is generated from the corresponding .tt file. DO NOT edit this .cs directly.

namespace Microsoft.Rexl.Types;

internal static class RexlKeyWords
{
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
