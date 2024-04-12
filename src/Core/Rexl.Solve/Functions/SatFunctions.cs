// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Solve;

/// <summary>
/// Function to solve a sat problem. The invocation is:
///   Sat.Solve(count: i8, clauses: i8**, [cslnMax: i8]): i8**
/// where:
/// * count is the number of literals, which come in pairs as a variable followed by its logical inverse.
/// * clauses is a sequence of clauses.
/// * each clause is a sequence of literals.
/// * cslnMax is the maximum number of solutions to produce, defaulting to 100.
/// * the return value is a sequence of solutions.
/// * each solution is the set of true literals.
/// produces a null sequence if the inputs are invalid.
/// </summary>
public sealed partial class SatSolveFunc : RexlOper
{
    public static readonly SatSolveFunc Instance = new SatSolveFunc();

    private SatSolveFunc()
        : base(isFunc: true, new DName("Solve"), NPath.Root.Append(new DName("Sat")), 2, 3)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        return ArgTraitsSimple.Create(this, eager: true, carg);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var typeSeq = DType.I8Req.ToSequence(2);
        if (info.Arity == 2)
            return (typeSeq, Immutable.Array.Create(DType.I8Req, typeSeq));
        return (typeSeq, Immutable.Array.Create(DType.I8Req, typeSeq, DType.I8Req));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Type != DType.I8Req.ToSequence(2))
            return false;
        var args = call.Args;
        if (args[0].Type != DType.I8Req)
            return false;
        if (args[1].Type != DType.I8Req.ToSequence(2))
            return false;
        if (args.Length > 2 && args[2].Type != DType.I8Req)
            return false;
        return true;
    }

    public static Gen MakeGen() => new Gen();

    public sealed class Gen : RexlOperationGenerator<SatSolveFunc>
    {
        private readonly MethodInfo _meth2 = new Func<long, IEnumerable<IEnumerable<long>>, IEnumerable<long[]>>(Exec).Method;
        private readonly MethodInfo _meth3 = new Func<long, IEnumerable<IEnumerable<long>>, long, IEnumerable<long[]>>(Exec).Method;

        protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
        {
            Validation.AssertValue(codeGen);
            Validation.Assert(IsValidCall(call, true));

            var meth = call.Args.Length == 2 ? _meth2 : _meth3;
            stRet = GenCall(codeGen, meth, sts);
            return true;
        }

        public static IEnumerable<long[]> Exec(long count, IEnumerable<IEnumerable<long>> clauses)
        {
            // REVIEW: What should we use as the default cslnMax value?
            return Exec(count, clauses, cslnMax: 100);
        }

        public static IEnumerable<long[]> Exec(long count, IEnumerable<IEnumerable<long>> clauses, long cslnMax)
        {
            // count should be an even positive integer.
            if (count <= 0 || (count & 1) != 0 || count > int.MaxValue || clauses == null || cslnMax <= 0)
                return null;

            var cls = new List<Literal[]>();
            var idsCur = new HashSet<int>();
            var clCur = new List<Literal>();
            foreach (var clause in clauses)
            {
                if (clause == null)
                    continue;
                idsCur.Clear();
                clCur.Clear();
                foreach (var id in clause)
                {
                    if (id < 0 || id >= count)
                        return null;
                    if (idsCur.Add((int)id))
                        clCur.Add(new Literal((int)id));
                }
                if (clCur.Count == 0)
                    continue;
                cls.Add(clCur.ToArray());
            }

            var prm = new SatSolverParams(42);

            return Translate(SatSolver.Solve(prm, (int)count / 2, cls), (int)Math.Min(int.MaxValue, cslnMax));
        }

        private static IEnumerable<long[]> Translate(IEnumerable<SatSolution> solutions, int cslnMax)
        {
            Validation.AssertValue(solutions);
            Validation.Assert(cslnMax > 0);

            int csln = 0;
            foreach (var sln in solutions)
            {
                // REVIEW: This is slightly bogus if the code generator wants to wrap! Each solution should
                // be wrapped, but there's not a good way to do that. Since we're creating an array, it works for
                // all current code generators.
                var lits = sln.Literals;
                var ids = new long[lits.Length];
                for (int i = 0; i < ids.Length; i++)
                    ids[i] = lits[i].Id;
                yield return ids;
                if (++csln >= cslnMax)
                    break;
            }
        }
    }
}

/// <summary>
/// Function to construct clauses for "at most one" of the given literals.
/// REVIEW: Should we keep this? The same functionality can be handled generally by using the MakePairs function.
/// </summary>
public sealed partial class SatAtMostOneFunc : RexlOper
{
    public static readonly SatAtMostOneFunc Instance = new SatAtMostOneFunc();

    private SatAtMostOneFunc()
        : base(isFunc: true, new DName("AtMostOne"), NPath.Root.Append(new DName("Sat")), 1, 1)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        // REVIEW: This could be made non-eager but the current implementation is eager.
        return ArgTraitsSimple.Create(this, eager: true, carg);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        return (DType.I8Req.ToSequence(2), Immutable.Array.Create(DType.I8Req.ToSequence(1)));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Type != DType.I8Req.ToSequence(2))
            return false;
        if (call.Args[0].Type != DType.I8Req.ToSequence())
            return false;
        return true;
    }

    public static Gen MakeGen() => new Gen();

    public sealed class Gen : RexlOperationGenerator<SatAtMostOneFunc>
    {
        private readonly MethodInfo _meth = new Func<IEnumerable<long>, IEnumerable<long[]>>(Exec).Method;

        protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
        {
            Validation.AssertValue(codeGen);
            Validation.Assert(IsValidCall(call, true));

            stRet = GenCall(codeGen, _meth, sts);
            return true;
        }

        public static IEnumerable<long[]> Exec(IEnumerable<long> lits)
        {
            if (lits == null)
                return null;

            return ExecCore(lits as long[] ?? lits.ToArray());
        }

        private static IEnumerable<long[]> ExecCore(long[] lits)
        {
            Validation.AssertValue(lits);

            // Need to construct all pairs of negations.
            int count = lits.Length;
            for (int i = 0; i < count - 1; i++)
            {
                for (int j = i + 1; j < count; j++)
                    yield return new long[] { lits[i] ^ 1, lits[j] ^ 1 };
            }
        }
    }
}

/// <summary>
/// Function to negate a SAT literal. This simply xor's with 1.
/// </summary>
public sealed partial class SatNotFunc : RexlOper
{
    public static readonly SatNotFunc Instance = new SatNotFunc();

    private SatNotFunc()
        : base(isFunc: true, new DName("Not"), NPath.Root.Append(new DName("Sat")), 1, 1)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.BugCheckParam(SupportsArity(carg), nameof(carg));
        var maskAll = BitSet.GetMask(carg);
        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: maskAll, maskLiftTen: maskAll, maskLiftOpt: maskAll);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        return (DType.I8Req, Immutable.Array.Create(DType.I8Req));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Type != DType.I8Req)
            return false;
        if (call.Args[0].Type != DType.I8Req)
            return false;
        return true;
    }

    public static Gen MakeGen() => new Gen();

    public sealed class Gen : RexlOperationGenerator<SatNotFunc>
    {
        protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
        {
            Validation.AssertValue(codeGen);
            Validation.Assert(IsValidCall(call, true));
            Validation.Assert(sts.Length == call.Args.Length);
            Validation.Assert(sts[0] == typeof(long));

            codeGen.Writer
                .Ldc_I8(1)
                .Xor();

            stRet = typeof(long);
            return true;
        }
    }
}
