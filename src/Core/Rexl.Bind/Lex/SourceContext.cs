// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Globalization;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

/// <summary>
/// Context information for rexl source code.
/// </summary>
public sealed class SourceContext
{
    /// <summary>
    /// The source text. Never null, but may be empty.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Get a <see cref="SourceRange"/> for the entirety of this <see cref="SourceContext"/>.
    /// </summary>
    public SourceRange RangeAll => new SourceRange(this, 0, Text.Length);

    /// <summary>
    /// This is a context location used for resolving relative access to files. It is not
    /// necessarily a link to a file containing the <see cref="Text"/>. This may be null.
    /// </summary>
    public Link LinkCtx { get; }

    /// <summary>
    /// This is a full link to the file containing this script. It may be null.
    /// </summary>
    public Link LinkFull { get; }

    /// <summary>
    /// This is the path used to access the file containing this script. It may be null.
    /// REVIEW: Should this be a <see cref="Link"/>?
    /// </summary>
    public string PathTail { get; }

    /// <summary>
    /// This is the character position of line starts, plus one extra that is one more
    /// than the length of the text. It is used to map from character position to line/col.
    /// It is created lazily, when first needed.
    /// </summary>
    private Immutable.Array<int> _lineStarts;

    /// <summary>
    /// This is the character position of LF characters that are preceeded by CR, plus 0 at the beginning
    /// and one more than the length of the text at the end. It is used to map from character
    /// position to "clean" character position. It is created lazily, when first needed.
    /// </summary>
    private Immutable.Array<int> _posAdjustments;

    private SourceContext(Link linkCtx, Link linkFull, string pathTail, string text)
    {
        Validation.BugCheckValueOrNull(linkCtx);
        Validation.BugCheckValueOrNull(linkFull);
        Validation.BugCheckValueOrNull(pathTail);
        Validation.BugCheckValue(text, nameof(text));

        Text = text;
        LinkCtx = linkCtx;
        LinkFull = linkFull;
        if (pathTail == "")
            pathTail = null;
        PathTail = pathTail;
    }

    /// <summary>
    /// Create an instance with no location information.
    /// </summary>
    public static SourceContext Create(string text)
    {
        return new SourceContext(null, null, null, text);
    }

    /// <summary>
    /// Create an instance with the context link the same as the full link.
    /// </summary>
    public static SourceContext Create(Link linkFull, string pathTail, string text)
    {
        return new SourceContext(linkFull, linkFull, pathTail, text);
    }

    /// <summary>
    /// Create an instance from all properties.
    /// </summary>
    public static SourceContext Create(Link linkCtx, Link linkFull, string pathTail, string text)
    {
        return new SourceContext(linkCtx, linkFull, pathTail, text);
    }

    private void EnsureLineStarts()
    {
        if (!_lineStarts.IsDefault)
            return;

        var bldr = Immutable.Array<int>.CreateBuilder();
        bldr.Add(0);
        var text = Text.VerifyValue();
        for (int ich = 0; ich < text.Length; ich++)
        {
            if (text[ich] == '\n')
                bldr.Add(ich + 1);
        }
        bldr.Add(text.Length + 1);

        _lineStarts = bldr.ToImmutable();
    }

    private void EnsurePosAdjustments()
    {
        if (!_posAdjustments.IsDefault)
            return;

        var bldr = Immutable.Array<int>.CreateBuilder();
        bldr.Add(0);
        var text = Text.VerifyValue();
        for (int ich = 1; ich < text.Length; ich++)
        {
            if (text[ich - 1] == '\r' && text[ich] == '\n')
                bldr.Add(ich);
        }
        bldr.Add(text.Length + 1);

        _posAdjustments = bldr.ToImmutable();
    }

    private static int FindPos(int ich, Immutable.Array<int> poss)
    {
        Validation.Assert(poss.Length >= 2);
        Validation.AssertIndex(ich, poss[poss.Length - 1]);

        int min = 1;
        int lim = poss.Length - 1;
        while (min < lim)
        {
            int mid = (int)((uint)(min + lim) >> 1);
            if (ich < poss[mid])
                lim = mid;
            else
                min = mid + 1;
        }
        Validation.Assert(min == lim);
        Validation.Assert(0 < min & min < poss.Length);
        Validation.Assert(ich < poss[min]);
        Validation.Assert(ich >= poss[min - 1]);

        return min;
    }

    /// <summary>
    /// Returns the zero-based line number and column number of the given position in the source text.
    /// If <paramref name="end"/> is true and the position is at the end of a line (right after the new line),
    /// this returns the position on the previous line.
    /// </summary>
    public (int line, int col) GetLineCol(int ich, bool end = false)
    {
        Validation.BugCheckIndexInclusive(ich, Text.Length, nameof(ich));

        EnsureLineStarts();

        int loc = FindPos(ich, _lineStarts);

        Validation.Assert(0 < loc & loc < _lineStarts.Length);
        Validation.Assert(ich < _lineStarts[loc]);
        Validation.Assert(ich >= _lineStarts[loc - 1]);

        int line = loc - 1;
        if (end && line > 0 && ich == _lineStarts[line])
            line--;
        return (line, ich - _lineStarts[line]);
    }

    /// <summary>
    /// Adjust the position for the number of two-character line endings that preceed it.
    /// </summary>
    public int GetCleanPosition(int ich)
    {
        Validation.BugCheckIndexInclusive(ich, Text.Length, nameof(ich));

        EnsurePosAdjustments();

        int loc = FindPos(ich, _posAdjustments);

        Validation.Assert(0 < loc & loc < _posAdjustments.Length);
        Validation.Assert(ich < _posAdjustments[loc]);
        Validation.Assert(ich >= _posAdjustments[loc - 1]);

        int delta = loc - 1;
        Validation.Assert(delta >= 0);
        Validation.Assert(ich >= 2 * delta - 1);
        return ich - delta;
    }
}

/// <summary>
/// Represents an index range in a <see cref="SourceContext"/>.
/// </summary>
public struct SourceRange
{
    /// <summary>
    /// The source context.
    /// </summary>
    public readonly SourceContext Source;

    /// <summary>
    /// The minimum index of the range.
    /// </summary>
    public readonly int Min;

    /// <summary>
    /// The limit index of the range. Note that the Lim is not included in the range,
    /// so can equal the length of the source text.
    /// </summary>
    public readonly int Lim;

    public SourceRange(SourceContext source, int ichMin, int ichLim)
    {
        Validation.BugCheckValue(source, nameof(source));
        Validation.BugCheckIndexInclusive(ichLim, source.Text.Length, nameof(ichLim));
        Validation.BugCheckIndexInclusive(ichMin, ichLim, nameof(ichMin));
        Source = source;
        Min = ichMin;
        Lim = ichLim;
    }

    /// <summary>
    /// Compute and return the union of this range with the given range.
    /// </summary>
    public SourceRange Union(SourceRange rng)
    {
        Validation.BugCheck(Source != null);
        Validation.BugCheckParam(rng.Source == Source, nameof(rng));
        return new SourceRange(Source, Math.Min(Min, rng.Min), Math.Max(Lim, rng.Lim));
    }

    /// <summary>
    /// Compute and return the union of this range with the given range, if non-null.
    /// </summary>
    public SourceRange Union(SourceRange? rng)
    {
        Validation.BugCheck(Source != null);
        return rng == null ? this : Union(rng.Value);
    }

    /// <summary>
    /// Return the portion of the given source text specified by the range. BugChecks that the
    /// range lies within the text.
    /// </summary>
    public string GetFragment()
    {
        Validation.BugCheck(Source != null);
        return Source.Text.Substring(Min, Lim - Min);
    }

    public override string ToString()
    {
        Validation.BugCheck(Source != null);
        return string.Format(CultureInfo.InvariantCulture, "({0},{1})", Min, Lim);
    }

    public void Deconstruct(out int min, out int lim)
    {
        min = Min;
        lim = Lim;
    }

    public void Deconstruct(out SourceContext source, out int min, out int lim)
    {
        source = Source;
        min = Min;
        lim = Lim;
    }
}
