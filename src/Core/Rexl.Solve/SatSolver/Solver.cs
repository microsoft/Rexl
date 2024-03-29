// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using Microsoft.Rexl;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Solve;

using Stopwatch = System.Diagnostics.Stopwatch;
using VarID = System.Int32;

/// <summary>
/// Class for processing SatSolver parameters
/// </summary>
public class SatSolverParams
{
    private bool _fAbort; // REVIEW: shall we use volatile for _fAbort?
    private bool _fBiased;
    private bool _fInitialGuess;
    private double _dblRandVarProb;
    private double _dblRandSenseProb;
    private int _cvBackTrackMaxInit;
    private readonly int _seed;
    private readonly Func<bool> _fnQueryAbort;

    /// <summary>
    /// Create default parameter set
    /// </summary>
    public SatSolverParams(int seed) : this(seed, null) { }

    /// <summary>
    /// Create default parameter set with a callback function to indicate when to stop
    /// </summary>
    /// <param name="fnQueryAbort"></param>
    public SatSolverParams(int seed, Func<bool> fnQueryAbort)
    {
        _dblRandVarProb = 0.02;
        _dblRandSenseProb = 0.1;
        _cvBackTrackMaxInit = 50;

        _seed = seed;
        _fnQueryAbort = fnQueryAbort;
    }

    /// <summary>
    /// The random seed.
    /// </summary>
    public int Seed { get { return _seed; } }

    /// <summary>
    /// Get/Set the abort flag to stop the solver
    /// </summary>
    public virtual bool Abort
    {
        get { return _fAbort || (_fnQueryAbort != null && _fnQueryAbort()); }
        set { _fAbort = value; }
    }
    /// <summary>
    /// Whether the value choice is biased in the search
    /// </summary>
    public virtual bool Biased
    {
        get { return _fBiased; }
        set { _fBiased = value; }
    }
    /// <summary>
    /// If Biased is true, this indicates which direction will be tried first.
    /// </summary>
    public virtual bool InitialGuess
    {
        get { return _fInitialGuess; }
        set { _fInitialGuess = value; }
    }
    /// <summary>
    /// Probability that a variable is chosen at random.
    /// </summary>
    public virtual double RandVarProb
    {
        get { return _dblRandVarProb; }
        set { _dblRandVarProb = value; }
    }
    /// <summary>
    /// Probability that a sense is chosen at random.
    /// </summary>
    public virtual double RandSenseProb
    {
        get { return _dblRandSenseProb; }
        set { _dblRandSenseProb = value; }
    }
    /// <summary>
    /// Initial number of back tracks that triggers a restart.
    /// </summary>
    public virtual int BackTrackCountLimit
    {
        get { return _cvBackTrackMaxInit; }
        set { _cvBackTrackMaxInit = Math.Max(10, value); }
    }
}

/// <summary>
/// Class for SatSolver
/// </summary>
public class SatSolver
{
    /// <summary>
    /// This one uses heuristics to decide which boolean value to try first.
    /// </summary>
    public static IEnumerable<SatSolution> Solve(SatSolverParams prm, int varLim, IEnumerable<Literal[]> rgcl)
    {
        return SolveCore(prm, varLim, rgcl);
    }

    /* protected */
    internal static IEnumerable<SatSolution> SolveCore(SatSolverParams prm, int varLim, IEnumerable<Literal[]> rgcl)
    {
        SatSolver solver = new SatSolver();
        if (!solver.Init(prm, varLim, rgcl))
            return Array.Empty<SatSolution>();
        return solver.GetSolutions();
    }

    /* protected */
    internal enum VarVal : int
    {
        False = -1,
        Undefined = 0, // Should be 0, ie, default(VarVal).
        True = 1,
    }

    // VarID decay is really multiplied times new increments so is greater than one.
    // This is more effecient than multiplying all the current values by a number less than one.
    const double kactVarDecay = 1.0 / 0.95;

    private SatSolverParams _prm; // Parameters.
    private Random _rand; // Random number generator.

    // Note: We used to use a Clause type, but the memory and GC overhead was affecting
    // perf significantly. Now clauses are represented in one of two ways.
    // * An empty clause forces an automatic failure so is not added to the clause bank.
    // * A singleton clause forces a variable assignment so is not added to the clause bank.
    // * A binary clause is added to the binary watch lists as the other literal.
    // * A clause with more than two literals is represented as a Literal[] and added to
    //   the general watch lists.
    // Logical clauses are often referenced using the hungarian cl even though their type is
    // simply Literal[]. I'd _really_ like to be using a typedef for clauses, but sadly C#
    // doesn't yet support them.
    private List<Literal[]> _rgclLearned; // The learned clauses.
    private List<Literal[]> _rgclSolution; // The clauses that prohibit previous solutions.
    private int _clitLearned; // Total number of literal in the learned clauses.
    private int _cclRaw; // Number of raw clauses.

    private int _varLim; // The count (limit) of variables.
    private VarVal[] _mplitval; // The value of each literal.
    private int[] _mpvarlev; // The level of each assigned variable.
    private Literal[][] _mpvarclReason; // For each variable, the clause that implied its value.
    private Literal[] _mpvarlitReason; // For each variable, the other part of the binary clause that implied its value.

    // For each literal, this stores the other half of the binary clauses that contain
    // the negation of the literal.
    private WatchList<Literal>[] _mplitwlBinary;
    // For each literal, the clauses (with more than two literals) watching the literal.
    private WatchList<Literal[]>[] _mplitwlGeneral;

    // The assignments in chronological order.
    private List<Literal> _rglitAssign;

    // Maps from level number (lev) to the first index into _rglitAssign
    // for that level. This makes it easy to unassign variables back to a certain level.
    // The first variable for any level (other than level 0) should be a choice.
    // The remaining ones come from unit propogation.
    private List<int> _mplevilitAssign;

    // An index into _rglitAssign. This is the next literal to apply propogation to.
    private int _ilitPropogate;

    // Activity tracking.
    private double[] _mpvaract; // The activity measure of each variable.
    private int[] _mplitccl; // The number of clauses containing this literal.
    private double _dactVar; // VarID activity delta.
    private double _actVarDecay; // VarID activity decay factor.

    private int _cvBackTrack; // Back track count.

    // Dynamic variable ordering.
    private IndexedHeap _heap;

    // Previous solution.
    private SatSolution _solPrev;

    // Stopwatch for timing.
    private Stopwatch _sw;

    // Scratch space for Analyze.
    private bool[] _mpvarfSeen;
    private List<Literal> _rglitLearned;

    private SatSolver()
    {
    }

    /// <summary>
    /// Initializes the data structures to represent the given set of clauses.
    /// REVIEW: Perhaps change to take an Immutable.Array of Literal, so the arrays
    /// don't have to be cloned.
    /// </summary>
    protected bool Init(SatSolverParams prm, int varLim, IEnumerable<Literal[]> rgcl)
    {
        _rand = new Random(prm.Seed);

        _rgclLearned = new List<Literal[]>();
        _rgclSolution = new List<Literal[]>();
        _clitLearned = 0;

        _prm = prm;
        _varLim = varLim;
        _mplitval = new VarVal[varLim * 2];
        _mpvarlev = new int[varLim];
        _mpvarclReason = new Literal[varLim][];
        _mpvarlitReason = new Literal[varLim];

        _mplitwlBinary = new WatchList<Literal>[varLim * 2];
        _mplitwlGeneral = new WatchList<Literal[]>[varLim * 2];

        _rglitAssign = new List<Literal>();
        _mplevilitAssign = new List<int>();
        _ilitPropogate = 0;

        _mpvaract = new double[varLim];
        _mplitccl = new int[varLim * 2];
        _dactVar = 1.0;
        _actVarDecay = kactVarDecay;
        _cvBackTrack = 0;

        _heap = new IndexedHeap(varLim, (var1, var2) => _mpvaract[var1] < _mpvaract[var2]);

        _mpvarfSeen = new bool[varLim];
        _rglitLearned = new List<Literal>();

        _sw = new Stopwatch();

        foreach (Literal[] cl in rgcl)
        {
            if (!AddMainClause(cl) || _prm.Abort)
                return false;
        }

        // Do the first round of unit propogation.
        Literal[] clBad = PropogateUnitClauses();
        if (clBad != null)
            return false;
#if false
        // Now simplify the database. Eliminating all known variables from clauses
        // and remove satisfied clauses from all watch lists.
        SimplifyClauses();
#endif

        // Count the number of clauses that use each variable and literal.
        InitVarActivity();

        // Now build the heap. Ignore variables with a zero activity count.
        // They are either already set by the first pass of unit propogation
        // or not used in any active clauses.
        for (VarID var = _mpvaract.Length; --var >= 0;)
        {
            if (_mpvaract[var] > 0.0)
                _heap.Add(var);
        }

        return true;
    }

    /* protected */
    internal void AddToWatchLists(Literal[] cl)
    {
        Validation.Assert(cl.Length >= 2);
        if (cl.Length > 2)
        {
            _mplitwlGeneral[(~cl[0]).Id].Add(cl);
            _mplitwlGeneral[(~cl[1]).Id].Add(cl);
        }
        else
        {
            _mplitwlBinary[(~cl[0]).Id].Add(cl[1]);
            _mplitwlBinary[(~cl[1]).Id].Add(cl[0]);
        }
    }

    /* protected */
    internal bool AddMainClause(Literal[] cl)
    {
        _cclRaw++;

        // An empty clause is never satisfied....
        if (cl.Length == 0)
            return false;

        // Look for initial units.
        if (cl.Length == 1)
            return EnqueueLit(cl[0], null, Literal.Nil);

        if (cl.Length > 2)
        {
            // Clone any arrays that we need to hold onto.
            cl = (Literal[])cl.Clone();
            AddToWatchLists(cl);
            AdjustVarActivity(cl);
        }
        else
            AddToWatchLists(cl);
        return true;
    }

    /* protected */
    internal void AdjustVarActivity(Literal[] cl)
    {
        foreach (Literal lit in cl)
        {
            _mplitccl[lit.Id]++;
            _mpvaract[lit.Var] += _dactVar;
        }
    }

    /* protected */
    internal void InitVarActivity()
    {
        for (int id = _mplitwlBinary.Length; --id >= 0;)
        {
            int ccl = _mplitwlBinary[id ^ 1].Count;
            _mplitccl[id] += ccl;
            _mpvaract[id >> 1] += ccl * _dactVar;
        }
    }

    /* protected */
    internal void BumpActivity(VarID var)
    {
        _mpvaract[var] += _dactVar;
        if (_mpvaract[var] >= 1e100)
            RescaleVarActivity();
        if (_heap.InHeap(var))
            _heap.MoveUp(var);
    }

    /* protected */
    internal void RescaleVarActivity()
    {
        for (VarID var = 0; var < _varLim; var++)
            _mpvaract[var] *= 1e-100;
        _dactVar *= 1e-100;
    }

    /* protected */
    internal void DecayVarActivity()
    {
        _dactVar *= _actVarDecay;
    }

    /// <summary>
    /// Backtrack to the given level
    /// </summary>
    /* public */
    internal void UndoToLevel(int lev)
    {
        int litCount = _mplevilitAssign[lev];
        Validation.Assert(litCount <= _rglitAssign.Count);
        while (_rglitAssign.Count > litCount)
            UndoAssign();
        Util.TrimList(_mplevilitAssign, lev);
    }

    /* protected */
    internal bool ResetFromPreviousSolution()
    {
        if (LevelCur == 0)
            return false;

        Literal[] cl = new Literal[LevelCur];
        for (int lev = 0; lev < LevelCur; lev++)
            cl[lev] = ~_rglitAssign[_mplevilitAssign[lev]];

        UndoToLevel(0);
        if (cl.Length == 1)
        {
            bool fTmp = EnqueueLit(cl[0], null, Literal.Nil);
            Validation.Assert(fTmp);
        }
        else
        {
            AdjustVarActivity(cl);
            AddToWatchLists(cl);
            _rgclSolution.Add(cl);
        }

        return true;
    }

    // Init must be called (and be successful) before calling this.
    /* protected */
    internal IEnumerable<SatSolution> GetSolutions()
    {
        SatSolution sol;
        while ((sol = GetNextSolution()) != null)
            yield return sol;
    }

    // The main event. This gets called for each solution search. It returns
    // null to signal no more solutions.
    /* protected */
    internal SatSolution GetNextSolution()
    {
        if (_solPrev != null && !ResetFromPreviousSolution())
            return null;

        // Implements restarts. Search returns false on an abort due
        // to excessive backtracking.
        int cvBackTrackMax = _prm.BackTrackCountLimit;
        int cvRestart = 0;
        _sw.Stop();
        _sw.Reset();
        _sw.Start();
        for (; ; )
        {
            if (_prm.Abort)
            {
                _solPrev = null;
                break;
            }
            if (Search(cvBackTrackMax, cvRestart, out _solPrev))
                break;
            cvBackTrackMax += Math.Max(5, cvBackTrackMax / 10);
            cvRestart++;
        }
        _sw.Stop();

        return _solPrev;
    }

    // Pick the next literal to "guess" with. This uses random guessing with probability
    // kdblRandom. Otherwise it uses VSIDS. Random guessing does the fast thing and picks
    // uniformly over [0, _varLim). If the chosen variable happens to already be assigned,
    // we go to the VSIDS heap. Because of this, the effective probability of using a random
    // choice decreases as more variables are assigned.
    // If all variables (that are used in clauses) are assigned, this returns Literal.Nil.
    /* protected */
    internal Literal ChooseNext()
    {
        Literal lit;

        // REVIEW: We should probably pick randomly among unassigned variables, not
        // among all!
        if (_rand.NextDouble() < _prm.RandVarProb &&
          GetLitVal(lit = new Literal(_rand.Next(2 * _varLim))) == VarVal.Undefined)
        {
            if (_prm.Biased)
                return new Literal(lit.Var, _prm.InitialGuess);
            return lit;
        }

        for (; ; )
        {
            if (_heap.Count == 0)
                return Literal.Nil;
            lit = new Literal(_heap.Pop(), false);
            if (GetLitVal(lit) == VarVal.Undefined)
                break;
        }

        if (_prm.Biased)
            return new Literal(lit.Var, _prm.InitialGuess);

        Literal litOpp = ~lit;
        int dccl = _mplitccl[lit.Id] - _mplitccl[litOpp.Id];
        if (dccl == 0 || _rand.NextDouble() < _prm.RandSenseProb)
            return _rand.Next(2) == 0 ? lit : litOpp;
        return dccl > 0 ? lit : litOpp;
    }

    // Returns false if searching was interrupted due to excessive backtracking.
    /* protected */
    internal bool Search(int cvBackTrackMax, int cvRestart, out SatSolution sol)
    {
        _cvBackTrack = 0;

        for (; ; )
        {
            Literal litNew;
            Literal[] clBad = PropogateUnitClauses();

            if (clBad == null)
            {
                // No conflict, so choose the next literal to try.
                litNew = ChooseNext();
                if (litNew.IsNil)
                {
                    // Have a solution.
                    _sw.Stop();
                    sol = new SatSolution(_rglitAssign.ToImmutableArray(), _mplevilitAssign.ToArray(), cvRestart,
                      _cvBackTrack, _rgclLearned.Count, _clitLearned, _sw.Elapsed);
                    return true;
                }
                // Start a new level.
                _mplevilitAssign.Add(_rglitAssign.Count);

                // Assume the new literal.
                bool fTmp = EnqueueLit(litNew, null, Literal.Nil);
                Validation.Assert(fTmp);
            }
            else
            {
                // Conflict.
                if (LevelCur == 0)
                {
                    sol = null;
                    return true;
                }

                Literal[] clLearn;
                int ilitMaxLev;
                Analyze(clBad, out clLearn, out ilitMaxLev);
                _cvBackTrack++;

                // Go back to the beginning of lev.
                if (ilitMaxLev >= 0)
                {
                    Validation.Assert(ilitMaxLev > 0 && _mpvarlev[clLearn[ilitMaxLev].Var] > 0);
                    UndoToLevel(_mpvarlev[clLearn[ilitMaxLev].Var]);
                }
                else
                    UndoToLevel(0);

                if (clLearn.Length > 1)
                {
                    Literal litMax = clLearn[ilitMaxLev];
                    clLearn[ilitMaxLev] = clLearn[1];
                    clLearn[1] = litMax;
                    _rgclLearned.Add(clLearn);
                    _clitLearned += clLearn.Length;
                    AddToWatchLists(clLearn);
                    DecayVarActivity();
                }

                // Now take the asserting literal as fact (since the asserting literal is unit).
                EnqueueLit(clLearn[0], clLearn, Literal.Nil).Verify();

                if (++_cvBackTrack >= cvBackTrackMax || _prm.Abort)
                {
                    // Backtracked too many times.
                    if (LevelCur > 0)
                        UndoToLevel(0);
                    sol = null;
                    return false;
                }
            }
        }
    }

    // Perform unit propogation. This is where most of the CPU time is spent.
    /* protected */
    internal Literal[] PropogateUnitClauses()
    {
        // _ilitPropogate is essentially the head of the queue of new literals
        // to be processed.
        while (_ilitPropogate < _rglitAssign.Count)
        {
            Literal litCur = _rglitAssign[_ilitPropogate++];
            Literal litOpp = ~litCur;

            // First process binary clauses. For these, either the clause is satisfied,
            // conflicting or unit. There's no need to move between watch lists.
            int litCount = _mplitwlBinary[litCur.Id].Count;
            Literal[] rglitBinary = _mplitwlBinary[litCur.Id].Elements;
            for (int ilit = litCount; --ilit >= 0;)
            {
                // If the literal is already true, EnqueueLit is a no-op.
                if (!EnqueueLit(rglitBinary[ilit], null, litOpp))
                {
                    // Conflict!
                    return new Literal[] { rglitBinary[ilit], litOpp };
                }
            }

            // Now process general clauses.
            int iclSrc = 0;
            int iclDst = 0;
            int ccl = _mplitwlGeneral[litCur.Id].Count;
            Literal[][] rgcl = _mplitwlGeneral[litCur.Id].Elements;

            for (; iclSrc < ccl; iclSrc++)
            {
                Literal[] cl = rgcl[iclSrc];

                // Make sure the false literal is rglit[1].
                Validation.Assert(litOpp == cl[0] || litOpp == cl[1]);
                if (litOpp == cl[0])
                {
                    cl[0] = cl[1];
                    cl[1] = litOpp;
                }

                // If 0th is true, then clause is already satisfied.
                if (_mplitval[cl[0].Id] != VarVal.True)
                {
                    // Look for a new literal to watch.
                    int ilit = 2;
                    while (ilit < cl.Length && _mplitval[cl[ilit].Id] == VarVal.False)
                        ilit++;
                    if (ilit < cl.Length)
                    {
                        // Move it to the new watch list.
                        cl[1] = cl[ilit];
                        cl[ilit] = litOpp;
                        _mplitwlGeneral[(~cl[1]).Id].Add(cl);
                        continue;
                    }

                    // Clause is unit. Keep it in this watch list.
                    if (!EnqueueLit(cl[0], cl, Literal.Nil))
                    {
                        // Conflict! Copy the remaining clauses in the watch list, including this one.
                        if (iclDst < iclSrc)
                        {
                            while (iclSrc < ccl)
                            {
                                rgcl[iclDst++] = rgcl[iclSrc++];
                            }
                            _mplitwlGeneral[litCur.Id].Count = iclDst;
                        }
                        return cl;
                    }
                }

                // Keep it in the watch list.
                if (iclDst < iclSrc)
                    rgcl[iclDst] = rgcl[iclSrc];
                iclDst++;
            }
            Validation.Assert(iclSrc == ccl);
            _mplitwlGeneral[litCur.Id].Count = iclDst;
        }

        // No conflicts.
        return null;
    }

    // Derive a learned clause.
    /* protected */
    internal void Analyze(Literal[] clReason, out Literal[] clOut, out int ilitMaxLev)
    {
        // _mpvarfSeen is scratch space for Analyze, but allocated only once for
        // efficiency. Same for _rglitLearned.
#if DEBUG
        for (VarID var = 0; var < _varLim; var++)
            Validation.Assert(!_mpvarfSeen[var]);
#endif
        _rglitLearned.Clear();

        // If one of the reason clauses below is binary, we used the same array
        // repeatedly.
        Literal[] clBinaryReason = null;

        int cvarSeenThisLevel = 0;
        Literal lit = Literal.Nil;
        int ilitLim = _rglitAssign.Count;
        int levMax = 0;
        ilitMaxLev = -1;

        // Leave room for the asserting literal.
        _rglitLearned.Add(Literal.Nil);

        for (; ; )
        {
            Validation.Assert(lit == Literal.Nil || lit == clReason[0]);
            // REVIEW: If we implement learned clause trimming, here's the place to bump
            // the clause activity.
            // BumpClause(clReason);

            for (int ilit = 0; ilit < clReason.Length; ilit++)
            {
                Literal litCur = clReason[ilit];
                if (litCur.Id == lit.Id)
                    continue;

                VarID var = litCur.Var;
                int levVar;
                if (!_mpvarfSeen[var] && (levVar = _mpvarlev[var]) > 0)
                {
                    BumpActivity(var);
                    _mpvarfSeen[var] = true;
                    if (levVar == LevelCur)
                        cvarSeenThisLevel++;
                    else
                    {
                        if (levMax < levVar)
                        {
                            levMax = levVar;
                            ilitMaxLev = _rglitLearned.Count;
                        }
                        _rglitLearned.Add(litCur);
                    }
                }
            }

            while (!_mpvarfSeen[_rglitAssign[--ilitLim].Var])
                ;

            lit = _rglitAssign[ilitLim];
            _mpvarfSeen[lit.Var] = false;
            if (--cvarSeenThisLevel <= 0)
                break;

            clReason = _mpvarclReason[lit.Var];
            if (clReason == null)
            {
                Validation.Assert(!_mpvarlitReason[lit.Var].IsNil);
                if (clBinaryReason == null)
                    clBinaryReason = new Literal[2];
                clBinaryReason[0] = lit;
                clBinaryReason[1] = _mpvarlitReason[lit.Var];
                clReason = clBinaryReason;
            }
        }

        _rglitLearned[0] = ~lit;
        clOut = _rglitLearned.ToArray();
        foreach (Literal litTmp in clOut)
            _mpvarfSeen[litTmp.Var] = false;

#if DEBUG
        for (VarID var = 0; var < _varLim; var++)
            Validation.Assert(!_mpvarfSeen[var]);
#endif
    }

    /* protected */
    internal void UndoAssign()
    {
        Literal lit = Util.PopList(_rglitAssign);
        Validation.Assert(GetLitVal(lit) != VarVal.Undefined);
        SetLitVal(lit, VarVal.Undefined);
        VarID var = lit.Var;
        if (!_heap.InHeap(var))
            _heap.Add(var);
        if (_ilitPropogate > _rglitAssign.Count)
            _ilitPropogate = _rglitAssign.Count;
    }

    #region Helpers

    /* protected */
    internal VarVal GetLitVal(Literal lit) { return _mplitval[lit.Id]; }
    /* protected */
    internal void SetLitVal(Literal lit, VarVal val)
    {
        _mplitval[lit.Id] = val;
        _mplitval[lit.Id ^ 1] = (VarVal)(-(sbyte)val);
    }
    /* protected */
    internal int LevelCur { get { return _mplevilitAssign.Count; } }

    /* protected */
    internal bool EnqueueLit(Literal lit, Literal[] clFrom, Literal litFrom)
    {
        switch (GetLitVal(lit))
        {
        case VarVal.True:
            // Already assigned.
            return true;
        case VarVal.False:
            // Conflicting assignment.
            return false;
        default:
            Validation.Assert(GetLitVal(lit) == VarVal.Undefined);
            SetLitVal(lit, VarVal.True);
            VarID var = lit.Var;
            _mpvarlev[var] = LevelCur;
            _mpvarclReason[var] = clFrom;
            _mpvarlitReason[var] = litFrom;
            _rglitAssign.Add(lit);
            return true;
        }
    }

    #endregion Helpers

    /* protected */
    internal struct WatchList<T>
    {
        private int _cv;
        private T[] _rgv;

        public int Count
        {
            get { return _cv; }
            set { _cv = value; }
        }

        public T[] Elements
        {
            get { return _rgv; }
        }

        public void Add(T cl)
        {
            if (_rgv == null)
            {
                _rgv = new T[8];
            }
            else if (_cv == _rgv.Length)
            {
                Array.Resize(ref _rgv, _rgv.Length * 2);
            }
            _rgv[_cv++] = cl;
        }
    }

#if false
// Simplify the database. Eliminate all known variables from clauses
// and remove satisfied clauses from the watch lists.
// This also initializes the variable activity values and sets
// all variables that don't appear in any clauses.
protected void SimplifyClauses() {
  if (_rglitAssign.Count == 0)
    return;

  // There was some initial unit propogation, so remove all false literals
  // and remove satisfied clauses.
  int iclDst = 0;
  for (int iclSrc = 0; iclSrc < _rgclMain.Count; ) {
    Clause cl = _rgclMain[iclSrc++];
    bool fHasFalse = false;
    foreach (Literal lit in cl.Literals) {
      switch (GetLitVal(lit)) {
      case VarVal.True:
        // Clause is already satisfied. Remove it from the watch lists.
        RemoveFromWatchLists(cl);
        goto LNextClause;
      case VarVal.False:
        fHasFalse = true;
        break;
      }
    }

    Debug.Assert(cl.Literals.Length >= 2);
    if (fHasFalse) {
      // Build a new clause with minimal values.
      RemoveFromWatchLists(cl);

      List<Literal> list = new List<Literal>();
      foreach (Literal lit in cl.Literals) {
        if (GetVarVal(lit.Var) == VarVal.Undefined)
          list.Add(lit);
      }
      Debug.Assert(list.Count >= 2);

      cl = new SimplifiedClause(list.ToArray());
      AddToWatchLists(cl);
    }
    _rgclMain[iclDst++] = cl;
  LNextClause:
    ;
  }

  Utils.TrimList(_rgclMain, iclDst);
}
#endif
}
