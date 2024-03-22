// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text;

using Microsoft.Rexl;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;

namespace RexlBench;

using Context = MainForm.Context;

sealed class SinkImpl : FlushEvalSink
{
    private readonly Context _context;
    private readonly ValueWriterConfig _config;
    private readonly StdValueWriter _valueWriter;

    protected override bool ShowDiagBanners => true;

    public SinkImpl(Context context, TypeManager tm)
        : base()
    {
        Validation.AssertValue(context);
        Validation.AssertValue(tm);

        _context = context;
        _config = new ValueWriterConfig();
        _valueWriter = new StdValueWriter(_config, this, tm);
    }

    protected override void Dump(StringBuilder sb)
    {
        Validation.AssertValue(sb);
        _context.Flush(sb);
    }

    protected override void PostDiagnosticCore(DiagSource src, BaseDiagnostic diag, RexlNode? nodeCtx = null)
    {
        Validation.AssertValue(diag);
        WriteDiag(src, diag);
    }

    protected override void PostValueCore(DType type, object value)
    {
        Validation.Assert(type.IsValid);
        WriteValue(type, value);
    }

    protected override void WriteValueCore(DType type, object? value, int max)
    {
        Validation.Assert(type.IsValid);
        _config.Max = max;
        _config.ShowHex = _context.ShowHex;
        _config.ShowTerseTensor = _context.ShowTerseTensor;
        _valueWriter.WriteValue(type, value);
    }
}

sealed class Operations : OperationRegistry
{
    public Operations()
        : base(BuiltinFunctions.Instance, BuiltinProcedures.Instance, FlowProcs.Instance)
    {
#if WITH_SOLVE
        AddParent(Solve.SolverFunctions.Instance);
#endif
#if WITH_ONNX
        AddParent(Onnx.ModelFunctions.Instance);
#endif
        AddOne(WrapFunc.Instance);
    }
}

sealed class Generators : GeneratorRegistry
{
    public Generators()
        : base(BuiltinGenerators.Instance)
    {
#if WITH_SOLVE
        AddParent(Solve.SolverGenerators.Instance);
#endif
#if WITH_ONNX
        AddParent(Onnx.ModelFuncGenerators.Instance);
#endif
        Add(WrapFunc.MakeGenerator());
    }
}

/// <summary>
/// This function is for wrapping a literal so it doesn't look like a constant to the binder.
/// It helps with testing various scenarios, such as Abs(Wrap([-1, 2, -5])).
/// </summary>
internal sealed class WrapFunc : RexlOper
{
    public static readonly WrapFunc Instance = new WrapFunc();

    private WrapFunc()
        : base(isFunc: true, new DName("Wrap"), 1, 1)
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

        var type = info.Args[0].Type;
        return (type, Immutable.Array.Create(type));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        var args = call.Args;
        if (type != args[0].Type)
            return false;
        return true;
    }

    public static Gen MakeGenerator() => new Gen();

    public sealed class Gen : RexlOperationGenerator<WrapFunc>
    {
        protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts,
            out Type stRet, out SeqWrapKind wrap)
        {
            Validation.Assert(IsValidCall(call, true));

            // Nothing to do.
            wrap = SeqWrapKind.DontWrap;
            stRet = sts[0];
            return true;
        }
    }
}
