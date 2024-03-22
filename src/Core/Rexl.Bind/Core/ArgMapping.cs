// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Lex;

namespace Microsoft.Rexl.Sink;

/// <summary>
/// Utilities for message arg mapping. Used by <see cref="TypeSink"/> and diagnostics.
/// </summary>
public static class ArgMapping
{
    /// <summary>
    /// The default formatted message arg mapper.
    /// </summary>
    public static readonly Func<object?, object?> DefaultArgMapper = MapArg;

    /// <summary>
    /// The default formatted message arg mapper.
    /// REVIEW: Could use a better scheme where these values aren't realized as their own
    /// string but are written directly to the sink.
    /// </summary>
    public static object? MapArg(object? arg)
    {
        switch (arg)
        {
        case string str:
            return str;
        case Token tok:
            {
                string res = tok.Range.GetFragment();
                if (string.IsNullOrEmpty(res))
                    res = tok.GetStdString();
                return res;
            }
        case TokKind tid:
            {
                if (RexlLexer.Instance.TryGetFixedText(tid, out var res))
                    return res;
                switch (tid)
                {
                case TokKind.Ident: return "<identifier>";
                default: return arg;
                }
            }
        case DName name:
            if (name.IsValid)
                return name.Escape();
            return "<blank>";
        case NPath path:
            if (!path.IsRoot)
                return path.ToDottedSyntax();
            return "<root>";
        case BoundNode bnd:
            return BndNodePrinter.Run(bnd, BndNodePrinter.Verbosity.Terse);
#if DEBUG
        case DType type: break;
        case long i8: break;
        case int i4: break;

        default:
            // This is useful for code coverage, so we can easily see whether there are types
            // that we don't have cases for.
            break;
#endif
        }
        return arg;
    }

    /// <summary>
    /// Maps an args array to a new one, using the given <paramref name="argMap"/>. If <paramref name="argMap"/>
    /// is null, uses <see cref="DefaultArgMapper"/>.
    /// REVIEW: The current functionality in RC uses <see cref="IFormatProvider"/>. Should we use that
    /// here? The fill-ins currently don't use any format specification, so not sure if using that mechanism
    /// is justified for this.
    /// </summary>
    public static object?[] MapArgs(object?[] args, Func<object?, object?>? argMap)
    {
        return TextSinkUtil.MapArgs(args, argMap ?? DefaultArgMapper);
    }
}
