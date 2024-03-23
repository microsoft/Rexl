// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Code;

using GlobalFunc = Func<object[], object>;
using GlobalTuple = Immutable.Array<GlobalInfo>;
using RunnerFunc = Func<object[], ActionHost, ActionRunner>;

/// <summary>
/// The output from a code generator being run on a bound tree. Exactly one of <see cref="Func"/> and
/// <see cref="CreateRunnerFunc"/> is non-null. The former is for pure expression evaluation. The latter
/// is for an action.
/// </summary>
public sealed class CodeGenResult
{
    /// <summary>
    /// The compiled function delegate for the bound tree in the non-action case. The delegate is of type
    /// <see cref="Func{T, TResult}"/> where <c>T</c> is <c>object[]</c> and <c>TResult</c> is <c>object</c>.
    /// Executing this produces a rexl value of the <see cref="DType"/> given in the <see cref="BoundTree"/>.
    /// </summary>
    public GlobalFunc Func { get; }

    /// <summary>
    /// The compiled function delegate for the bound tree in the action case. The delegate is of type
    /// <see cref="Func{T, TResult}"/> where <c>T</c> is <c>object[]</c> and <c>TResult</c> is <c>ActionRunner</c>.
    /// Executing this produces an action runner. The <see cref="DType"/> of the <see cref="BoundTree"/> is
    /// irrelevant.
    /// </summary>
    public RunnerFunc CreateRunnerFunc { get; }

    /// <summary>
    /// The global argument information. The values of the global argument information indicates which named
    /// global values should be passed and their order. Note that one of the arguments needed may be an
    /// execution context. That context instance should be specific to a particular invocation of the
    /// function delegate.
    /// </summary>
    public GlobalTuple Globals { get; }

    /// <summary>
    /// The <see cref="Code.IdBndMap"/> used for the code gen process. This pairs bound nodes in the
    /// bound tree with their IDs used by the compiled function delegate when invoked.
    /// </summary>
    public IdBndMap IdBndMap { get; }

    /// <summary>
    /// The code generator instance used to produce this result.
    /// </summary>
    public CodeGeneratorBase Generator { get; }

    /// <summary>
    /// The bound tree used as input to produce this result.
    /// </summary>
    public BoundNode BoundTree { get; }

    /// <summary>
    /// The <see cref="DType"/> of the result.
    /// </summary>
    public DType Type => BoundTree.Type;

    /// <summary>
    /// The system type of the result.
    /// </summary>
    public Type SysType { get; }

    internal CodeGenResult(Delegate fn, GlobalTuple globals, IdBndMap idBndMap, CodeGeneratorBase gen,
        BoundNode tree, Type stRet)
    {
        Validation.BugCheckValue(fn, nameof(fn));
        Validation.BugCheckValue(idBndMap, nameof(idBndMap));
        Validation.BugCheckValue(gen, nameof(gen));
        Validation.BugCheckValue(tree, nameof(tree));
        Validation.BugCheckValue(stRet, nameof(stRet));

        if (tree is BndCallNode bcn && bcn.Oper.IsProc)
        {
            CreateRunnerFunc = fn as RunnerFunc;
            Validation.BugCheckParam(CreateRunnerFunc != null, nameof(fn));
        }
        else
        {
            Func = fn as GlobalFunc;
            Validation.BugCheckParam(Func != null, nameof(fn));
        }

        Globals = globals.Length > 0 ? globals : GlobalTuple.Empty;
        IdBndMap = idBndMap;
        Generator = gen;
        BoundTree = tree;
        SysType = stRet;
    }
}

/// <summary>
/// Information about a global needed to invoke a generated function.
/// </summary>
public sealed class GlobalInfo
{
    /// <summary>
    /// The singleton instance for exec context.
    /// </summary>
    private static readonly GlobalInfo CtxInstance = new GlobalInfo();

    /// <summary>
    /// The argument slot.
    /// </summary>
    public int Slot { get; }

    /// <summary>
    /// The name of the global. If this is <see cref="NPath.Root"/>, the argument is either
    /// <c>this</c> or the <see cref="ExecCtx"/>.
    /// </summary>
    public NPath Name { get; }

    /// <summary>
    /// The <see cref="DType"/> of the argument. This is valid iff <see cref="IsCtx"/> is false.
    /// </summary>
    public DType Type { get; }

    /// <summary>
    /// The system type of the argument. This is valid even when <see cref="IsCtx"/> is true.
    /// </summary>
    public Type SysType { get; }

    /// <summary>
    /// Whether this slot is the <see cref="ExecCtx"/>. If so, <see cref="Name"/> will be
    /// <see cref="NPath.Root"/> and <see cref="Type"/> will be invalid.
    /// </summary>
    public bool IsCtx { get; }

    /// <summary>
    /// Whether this slot is <c>this</c>. If so, <see cref="Name"/> will be <see cref="NPath.Root"/>.
    /// </summary>
    public bool IsThis { get; }

    /// <summary>
    /// The constructor for the <see cref="ExecCtx"/> argument.
    /// </summary>
    private GlobalInfo()
    {
        // Exec ctx has root name, invalid type, and slot 0.
        IsCtx = true;
        SysType = typeof(ExecCtx);
    }

    /// <summary>
    /// The constructor for a non-ctx / value argument. The type should always be valid.
    /// The <c>this</c> argument (when present) is indicated with a <paramref name="name"/>
    /// that is <see cref="NPath.Root"/>.
    /// </summary>
    private GlobalInfo(int slot, NPath name, DType type, Type st)
    {
        Validation.Assert(slot >= 0);
        Validation.Assert(type.IsValid);
        Validation.AssertValue(st);

        Slot = slot;
        Name = name;
        Type = type;
        SysType = st;
        IsThis = name.IsRoot;
    }

    /// <summary>
    /// Create a <see cref="GlobalInfo"/> for the execution context. Note that this always has slot 0.
    /// </summary>
    public static GlobalInfo CreateCtx()
    {
        return CtxInstance;
    }

    /// <summary>
    /// Create a <see cref="GlobalInfo"/> for the given name and type.
    /// </summary>
    public static GlobalInfo Create(int slot, NPath name, DType type, ICodeGen codeGen)
    {
        Validation.BugCheckParam(slot >= 0, nameof(slot));
        Validation.BugCheckParam(type.IsValid, nameof(type));
        Validation.BugCheckValue(codeGen, nameof(codeGen));
        return new GlobalInfo(slot, name, type, codeGen.GetSystemType(type));
    }
}
