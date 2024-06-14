// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.Rexl;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Private;

namespace RexlTest;

internal sealed class TestMultiFormProcGen : TestMultiFormOperGen<TestMultiFormProc, TestMultiFormProc.ExecutionOper>
{
    public TestMultiFormProcGen()
    {
    }

    protected override void GenCoreCode(ICodeGen codeGen, TestMultiFormProc.ExecutionOper exec, DType typeIn, DType typeOut, Type stIn, Type stOut)
    {
        base.GenCoreCode(codeGen, exec, typeIn, typeOut, stIn, stOut);

        MethodInfo meth;
        if (exec.Merges)
        {
            Type stDst = codeGen.GetSystemType(exec.TypeDst);
            Type stSrc = codeGen.GetSystemType(exec.TypeSrc);
            meth = new Action<Runner<object, object, object, object>>(Visit)
                .Method
                .GetGenericMethodDefinition()
                .MakeGenericMethod(stIn, stSrc, stDst, stOut);
        }
        else
        {
            meth = new Action<Runner<object, object>>(Visit)
                .Method
                .GetGenericMethodDefinition()
                .MakeGenericMethod(stIn, stOut);
        }

        codeGen.Writer
            .Ldarg(1)
            .Call(meth);
    }

    protected override void GenCreateRunner<TIn, TDst>(ICodeGen codeGen, TestMultiFormProc.ExecutionOper exec)
    {
        var meth = new Func<TIn, Func<ActionRunner, TIn, TDst>, TestMultiFormProc.ExecutionOper, ActionHost, ExecCtx, ActionRunner>(CreateRunner).Method;
        codeGen.Writer.Call(meth);
    }

    protected override void GenCreateRunner<TIn, TSrc, TDst, TOut>(ICodeGen codeGen, TestMultiFormProc.ExecutionOper exec)
    {
        var meth = new Func<TIn, TSrc, Func<ActionRunner, TIn, TSrc, (TDst, TOut)>, TestMultiFormProc.ExecutionOper, ActionHost, ExecCtx, ActionRunner>(CreateRunner).Method;
        codeGen.Writer.Call(meth);
    }

    private static ActionRunner CreateRunner<TIn, TDst>(TIn input, Func<ActionRunner, TIn, TDst> funcCore, TestMultiFormProc.ExecutionOper exec, ActionHost host, ExecCtx ctx)
    {
        return new Runner<TIn, TDst>(input, funcCore, exec.TypeDst, host);
    }

    private static ActionRunner CreateRunner<TIn, TSrc, TDst, TOut>(TIn input, TSrc src, Func<ActionRunner, TIn, TSrc, (TDst, TOut)> funcCore, TestMultiFormProc.ExecutionOper exec, ActionHost host, ExecCtx ctx)
    {
        return new Runner<TIn, TSrc, TDst, TOut>(input, src, funcCore, exec.TypeDst, exec.TypeOut, host);
    }

    private static void Visit<TIn, TOut>(Runner<TIn, TOut> runner)
    {
        runner.Visit();
    }

    private static void Visit<TIn, TSrc, TDst, TOut>(Runner<TIn, TSrc, TDst, TOut> runner)
    {
        runner.Visit();
    }

    internal sealed class Runner<TIn, TDst> : ThreadActionRunner
    {
        private readonly Func<ActionRunner, TIn, TDst> _funcCore; // Func for core function.
        private readonly TIn _in;
        private readonly DType _typeDst;
        private readonly ActionHost _host;

        private TDst _dst;
        private bool _visited;

        public Runner(TIn input, Func<ActionRunner, TIn, TDst> funcCore, DType typeDst, ActionHost host)
        {
            Validation.AssertValue(funcCore);
            Validation.Assert(typeDst.IsValid);
            Validation.AssertValue(host);
            _in = input;
            _funcCore = funcCore;
            _typeDst = typeDst;
            _host = host;
        }

        protected override object GetResultValueCore(ResultInfo info)
        {
            switch (info.Name.ToString())
            {
            case "Result":
                return _dst;
            case "Visited":
                return _visited;
            default:
                Validation.Assert(false);
                return null;
            }
        }

        protected override Task RunCoreAsync()
        {
            _dst = _funcCore(this, _in);
            AddResult("Result", _typeDst, isPrimary: true);
            return Task.CompletedTask;
        }

        public void Visit()
        {
            _visited = true;
            AddResult("Visited", DType.BitReq);
        }
    }

    internal sealed class Runner<TIn, TSrc, TDst, TOut> : ThreadActionRunner
    {
        private readonly Func<ActionRunner, TIn, TSrc, (TDst, TOut)> _funcCore; // Func for core function.
        private readonly TIn _in;
        private readonly TSrc _src;
        private readonly DType _typeDst;
        private readonly DType _typeOut;
        private readonly ActionHost _host;

        private TDst _dst;
        private TOut _out;
        private bool _visited;

        public Runner(TIn input, TSrc src, Func<ActionRunner, TIn, TSrc, (TDst, TOut)> funcCore, DType typeDst, DType typeOut, ActionHost host)
        {
            Validation.AssertValue(funcCore);
            Validation.Assert(typeDst.IsValid);
            Validation.Assert(typeOut.IsValid);
            Validation.AssertValue(host);
            _in = input;
            _src = src;
            _funcCore = funcCore;
            _typeDst = typeDst;
            _typeOut = typeOut;
            _host = host;
        }

        protected override object GetResultValueCore(ResultInfo info)
        {
            switch (info.Name.ToString())
            {
            case "Result":
                return _dst;
            case "PreMerge":
                return _out;
            case "Visited":
                return _visited;
            default:
                Validation.Assert(false);
                return null;
            }
        }

        protected override Task RunCoreAsync()
        {
            (_dst, _out) = _funcCore(this, _in, _src);
            AddResult("Result", _typeDst, isPrimary: true);
            AddResult("PreMerge", _typeOut);
            return Task.CompletedTask;
        }

        public void Visit()
        {
            _visited = true;
            AddResult("Visited", DType.BitReq);
        }
    }
}
