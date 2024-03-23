// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

/// <summary>
/// These are currently used both in tests and in product for easily experimenting with
/// procedure piping. There are mostly for testing though and should perhaps be moved
/// to only tests at some point.
/// </summary>
public sealed class FlowProcs : OperationRegistry
{
    public static readonly FlowProcs Instance = new FlowProcs();

    private FlowProcs()
        : base()
    {
        AddBoth(EchoProc.Instance);
        AddBoth(SyncProc.Instance);
        AddBoth(ThreadProc.Instance);
        AddBoth(PipeProc.Pipe);
        AddBoth(PipeProc.Step);
        AddBoth(FailProc.Instance);
    }

    /// <summary>
    /// Add both in the Test namespace and in the root namespace.
    /// </summary>
    private void AddBoth(RexlOper proc)
    {
        // Add it both with and without namespace, if the name without isn't already taken.
        AddOne(proc);
        if (proc.Path.NameCount > 1)
        {
            var pathShort = NPath.Root.Append(proc.Name);
            if (GetOper(pathShort) == null)
                AddOne(proc, pathShort);
        }
    }
}

/// <summary>
/// Base class for flow procedures. Puts the procedure in the "Test" namespace.
/// </summary>
public abstract class TestProc : RexlOper
{
    // Set the namespace to "Test".
    private protected TestProc(string name, int arityMin, int arityMax)
        : base(isFunc: false, name: new DName(name), ns: NPath.Root.Append(new DName("Test")), arityMin: arityMin, arityMax: arityMax)
    {
    }

    // Set the namespace to "Test".
    private protected TestProc(string name, bool union, int arityMin, int arityMax)
        : base(isFunc: false, new DName(name), NPath.Root.Append(new DName("Test")), union, arityMin, arityMax, null)
    {
    }

    protected abstract override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info);
}

/// <summary>
/// This procedure echos an input.
/// </summary>
public sealed partial class EchoProc : TestProc
{
    public static readonly EchoProc Instance = new EchoProc();

    private EchoProc()
        : base("Echo", 1, 1)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        return ArgTraitsSimple.Create(this, eager: false, carg);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(info.Arity == 1);

        return (DType.General, Immutable.Array.Create(info.Args[0].Type));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        if (type != DType.General)
            return false;
        return true;
    }
}

/// <summary>
/// A test proc that uses the <see cref="SyncActionRunner"/>. This accepts two "time" arguments.
/// The first specifies how long to "run" for. The second specifies how long to wait when
/// <see cref="SyncActionRunner.AbortCore"/> is called. These allow testing for various
/// flow paths through <see cref="SyncActionRunner"/>.
/// </summary>
public sealed partial class SyncProc : TestProc
{
    public static readonly SyncProc Instance = new SyncProc();

    private SyncProc()
        : base("SyncProc", 2, 2)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        return ArgTraitsSimple.Create(this, eager: false, carg);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(info.Arity == 2);

        return (DType.General, Immutable.Array.Create(DType.TimeReq, DType.TimeReq));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        var args = call.Args;
        if (type != DType.General)
            return false;
        if (args[0].Type != DType.TimeReq)
            return false;
        if (args[1].Type != DType.TimeReq)
            return false;
        return true;
    }
}

/// <summary>
/// A test proc that uses the <see cref="ThreadActionRunner"/>. This accepts a "time" argument
/// and a "limit" argument. The first specifies how long to "run" between yields. The second
/// specifies how many times to yield. These allow testing for various flow paths through
/// <see cref="ThreadActionRunner"/>.
/// </summary>
public sealed partial class ThreadProc : TestProc
{
    public static readonly ThreadProc Instance = new ThreadProc();

    private ThreadProc()
        : base("ThreadProc", 2, 2)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        return ArgTraitsSimple.Create(this, eager: false, carg);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(info.Arity == 2);

        return (DType.General, Immutable.Array.Create(DType.TimeReq, DType.I8Req));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        var args = call.Args;
        if (type != DType.General)
            return false;
        if (args[0].Type != DType.TimeReq)
            return false;
        if (args[1].Type != DType.I8Req)
            return false;
        return true;
    }
}

/// <summary>
/// This is used for testing task piping. Pipe takes one or two sequence args (of the
/// same item type). StepPipe takes a sequence arg and an optional count of "poke"
/// operations needed to release all remaining input items. Each poke before the limit
/// releases one input item.
/// </summary>
public sealed class PipeProc : TestProc
{
    public static readonly PipeProc Pipe = new PipeProc(step: false);
    public static readonly PipeProc Step = new PipeProc(step: true);

    public bool IsStep { get; }

    private PipeProc(bool step)
        : base(step ? "StepPipe" : "Pipe", 1, 2)
    {
        IsStep = step;
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        return ArgTraitsSimple.Create(this, eager: false, carg);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var type0 = info.Args[0].Type;
        if (info.Arity == 1)
        {
            EnsureTypeSeq(ref type0);
            return (DType.General, Immutable.Array.Create(type0));
        }

        if (IsStep)
        {
            EnsureTypeSeq(ref type0);
            return (DType.General, Immutable.Array.Create(type0, DType.I8Req));
        }

        var type1 = info.Args[1].Type;
        var type = DType.GetSuperType(type0.ItemTypeOrThis, type1.ItemTypeOrThis, DType.UseUnionDefault)
            .ToSequence();
        return (DType.General, Immutable.Array.Create(type, type1));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        var args = call.Args;
        if (type != DType.General)
            return false;
        if (!args[0].Type.IsSequence)
            return false;
        if (args.Length > 1)
        {
            if (IsStep)
            {
                if (args[1].Type != DType.I8Req)
                    return false;
            }
            else
            {
                if (args[1].Type != args[0].Type)
                    return false;
            }
        }
        return true;
    }
}

/// <summary>
/// Throws when the proc is executed (doesn't create an action runner).
/// </summary>
public sealed class FailProc : TestProc
{
    public static readonly FailProc Instance = new FailProc();

    private FailProc()
        : base("FailProc", 1, 1)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        return ArgTraitsSimple.Create(this, eager: false, carg);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        return (DType.General, Immutable.Array.Create(DType.I8Req));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Type != DType.General)
            return false;
        if (call.Args[0].Type != DType.I8Req)
            return false;
        return true;
    }
}
