// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Rexl;

namespace Microsoft.Rexl.Solve;

using VarID = System.Int32;

/// <summary>
/// Solutions for SatSolver models
/// </summary>
public class SatSolution
{
    private Immutable.Array<Literal> _rglit;
    private int[] _rgilitGuess;
    private int _cvRestart;
    private int _cvBackTrack;
    private int _cclLearned;
    private int _clitLearned;
    private TimeSpan _ts;

    /// <summary>
    /// Create a solution object
    /// </summary>
    public SatSolution(Immutable.Array<Literal> rglit, int[] rgilitGuess, int cvRestart, int cvBackTrack, int cclLearned, int learnedLiteralCount, TimeSpan ts)
    {
        _rglit = rglit;
        _rgilitGuess = rgilitGuess;
        _cvRestart = cvRestart;
        _cvBackTrack = cvBackTrack;
        _cclLearned = cclLearned;
        _clitLearned = learnedLiteralCount;
        _ts = ts;
    }
    /// <summary>
    /// Get the number of restarts during search
    /// </summary>
    public int RestartCount { get { return _cvRestart; } }
    /// <summary>
    /// Get the number of backtracks during search
    /// </summary>
    public int BackTrackCount { get { return _cvBackTrack; } }
    /// <summary>
    /// Get the number of learned clauses during search
    /// </summary>
    public int LearnedClauseCount { get { return _cclLearned; } }
    /// <summary>
    /// Get the number of learned literals during search
    /// </summary>
    public int LearnedLiteralCount { get { return _clitLearned; } }
    /// <summary>
    /// Get the amount of time spent in search
    /// </summary>
    public TimeSpan Time { get { return _ts; } }

    /// <summary>
    /// Get all literals.
    /// </summary>
    public Immutable.Array<Literal> Literals { get { return _rglit; } }

    /// <summary>
    /// Get all positive literals
    /// </summary>
    public IEnumerable<VarID> Pos
    {
        get
        {
            foreach (Literal lit in _rglit)
            {
                if (lit.Sense)
                    yield return lit.Var;
            }
        }
    }
    /// <summary>
    /// Get all negative literals
    /// </summary>
    public IEnumerable<VarID> Neg
    {
        get
        {
            foreach (Literal lit in _rglit)
            {
                if (!lit.Sense)
                    yield return lit.Var;
            }
        }
    }
    /// <summary>
    /// Get all choice literals
    /// </summary>
    public IEnumerable<Literal> Guesses
    {
        get
        {
            foreach (int ilit in _rgilitGuess)
                yield return _rglit[ilit];
        }
    }
    /// <summary>
    /// Get all choice literals that are positive
    /// </summary>
    public IEnumerable<VarID> PosGuess
    {
        get
        {
            foreach (int ilit in _rgilitGuess)
            {
                Literal lit = _rglit[ilit];
                if (lit.Sense)
                    yield return lit.Var;
            }
        }
    }
    /// <summary>
    /// Get all choice literals that are negative
    /// </summary>
    public IEnumerable<VarID> NegGuess
    {
        get
        {
            foreach (int ilit in _rgilitGuess)
            {
                Literal lit = _rglit[ilit];
                if (!lit.Sense)
                    yield return lit.Var;
            }
        }
    }

    /// <summary>
    /// Get the string representation of the solution
    /// </summary>
    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        bool fFirst = true;
        sb.Append('{');
        foreach (Literal lit in _rglit)
        {
            if (!lit.Sense)
                continue;
            if (!fFirst)
                sb.Append(',');
            sb.Append(lit);
            fFirst = false;
        }
        sb.Append('}');
        return sb.ToString();
    }
}
