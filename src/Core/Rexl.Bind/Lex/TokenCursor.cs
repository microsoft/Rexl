// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Lex;

using Conditional = System.Diagnostics.ConditionalAttribute;

/// <summary>
/// Token cursor, used only by the parser, so is not public.
/// REVIEW: Consider making the members protected rather than public, since the
/// Parser currently derives from this, rather than owning an instance of it.
/// </summary>
internal abstract class TokenCursor
{
    private readonly Immutable.Array<Token> _rgtok;
    private readonly int _ctok;

    private int _itokCur;
    private Token _tokCur;
    private TokKind _tidCur;

    protected TokenCursor(Immutable.Array<Token> rgtok)
    {
        Validation.Assert(!rgtok.IsDefault);
        Validation.Assert(rgtok.Length > 0 && rgtok[rgtok.Length - 1].Kind == TokKind.Eof);
        _rgtok = rgtok;
        _ctok = _rgtok.Length;

        _tokCur = _rgtok[0];
        _tidCur = _tokCur.Kind;
    }

    [Conditional("DEBUG")]
    private void AssertValid()
    {
        Validation.Assert(0 < _ctok && _ctok <= _rgtok.Length);
        Validation.Assert(_rgtok[_ctok - 1].Kind == TokKind.Eof);

        Validation.AssertIndex(_itokCur, _ctok);
        Validation.Assert(_tokCur == _rgtok[_itokCur]);
        Validation.Assert(_tidCur == _tokCur.Kind);
    }

    public int ItokLim
    {
        get
        {
            AssertValid();
            return _ctok;
        }
    }

    public int ItokCur
    {
        get
        {
            AssertValid();
            return _itokCur;
        }
    }

    public Token TokCur
    {
        get
        {
            AssertValid();
            return _tokCur;
        }
    }

    public TokKind TidCur
    {
        get
        {
            AssertValid();
            return _tidCur;
        }
    }

    public TokKind TidCurAlt
    {
        get
        {
            AssertValid();
            return _tokCur.TokenAlt.Kind;
        }
    }

    /// <summary>
    /// This is the only member that changes the "position" of the cursor.
    /// </summary>
    protected virtual void Advance()
    {
        AssertValid();
        Validation.AssertIndex(_itokCur + 1, _ctok);
        _itokCur++;
        _tokCur = _rgtok[_itokCur];
        _tidCur = _tokCur.Kind;
        AssertValid();
    }

    /// <summary>
    /// Asserts that the current token alt is of the indicated kind, then skips the token
    /// and returns the new current token kind.
    /// </summary>
    public TokKind TidSkip(TokKind tid)
    {
        Validation.Assert(TidCurAlt == tid);
        return TidSkip();
    }

    /// <summary>
    /// Skips the current token and returns the new current token kind.
    /// </summary>
    public TokKind TidSkip()
    {
        AssertValid();
        if (_tidCur != TokKind.Eof)
            Advance();
        return _tidCur;
    }

    public Token TokMoveRaw()
    {
        AssertValid();
        Token tok = _tokCur;
        if (_tidCur != TokKind.Eof)
            Advance();
        return tok;
    }

    public TokKind TidPeek()
    {
        AssertValid();
        int itok;
        if ((itok = _itokCur + 1) < _ctok)
            return _rgtok[itok].Kind;
        Validation.Assert(_tidCur == TokKind.Eof);
        return _tidCur;
    }

    public Token TokPeek()
    {
        AssertValid();
        int itok;
        if ((itok = _itokCur + 1) < _ctok)
            return _rgtok[itok];
        Validation.Assert(_tidCur == TokKind.Eof);
        return _tokCur;
    }

    private int ItokPeek(int ctok)
    {
        AssertValid();
        Validation.Assert(ctok >= 0);

        int itok = _itokCur + ctok;
        if ((uint)itok >= (uint)_ctok)
            itok = _ctok - 1;
        Validation.AssertIndex(itok, _ctok);
        return itok;
    }

    public TokKind TidPeek(int ctok, bool useAlt = false)
    {
        var tok = _rgtok[ItokPeek(ctok)];
        return useAlt ? tok.TokenAlt.Kind : tok.Kind;
    }

    public Token TokPeek(int ctok)
    {
        return _rgtok[ItokPeek(ctok)];
    }
}
