// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Onnx;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Solve;

namespace Microsoft.Rexl.Kernel;

internal sealed class KernelBuiltins : OperationRegistry
{
    public KernelBuiltins(Func<bool, bool> setShowIL = null)
        : base(BuiltinFunctions.Instance, BuiltinProcedures.Instance, FlowProcs.Instance,
            SolverFunctions.Instance, ModelFunctions.Instance)
    {
        if (setShowIL != null)
            AddOne(new ShowILFunc(setShowIL));
    }
}

internal sealed class KernelGenerators : GeneratorRegistry
{
    public KernelGenerators()
        : base(BuiltinGenerators.Instance, SolverGenerators.Instance, ModelFuncGenerators.Instance)
    {
        Add(ShowILFunc.MakeGen());
    }
}

/// <summary>
/// This function is for turning IL dumping on or off.
/// </summary>
public sealed class ShowILFunc : RexlOper
{
    public Func<bool, bool> SetShowIL { get; }

    public ShowILFunc(Func<bool, bool> setShowIL)
        : base(isFunc: true, new DName("ShowIL"), 1, 1)
    {
        Validation.AssertValue(setShowIL);
        SetShowIL = setShowIL;
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

        return (DType.BitReq, Immutable.Array.Create(DType.BitReq));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Type != DType.BitReq)
            return false;
        if (call.Args[0].Type != DType.BitReq)
            return false;
        return true;
    }

    public static Gen MakeGen() => new Gen();

    public sealed class Gen : RexlOperationGenerator<ShowILFunc>
    {
        private readonly MethodInfo _meth;

        public Gen()
        {
            _meth = new Func<bool, ShowILFunc, bool>(Exec).Method;
        }

        protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
        {
            Validation.AssertValue(codeGen);
            Validation.Assert(IsValidCall(call, true));

            var func = GetOper(call);

            stRet = GenCallExtra(codeGen, _meth, sts, func);
            return true;
        }

        public static bool Exec(bool value, ShowILFunc func)
        {
            return func.SetShowIL(value);
        }
    }
}
