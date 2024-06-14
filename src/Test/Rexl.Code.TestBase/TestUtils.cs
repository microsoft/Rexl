// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Rexl;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;

namespace RexlTest;

public static class TestUtils
{
    private readonly static EnumerableCodeGeneratorBase _codeGen =
        new CachingEnumerableCodeGenerator(new TestEnumTypeManager(), TestGenerators.Instance);
    private readonly static BindHost _host = new BindHostLonerWithBuiltinFuncs();

    /// <summary>
    /// Utility function to parse, bind, codeGen and execute the given Rexl <paramref name="formula"/>.
    /// The <paramref name="formula"/> may refer to built in Rexl functions but not refer to any globals
    /// other that its execution context if any.
    /// Throws if the input formula is invalid.
    /// </summary>
    public static (DType, object) ExecuteFormula(string formula, EnumerableCodeGeneratorBase codeGen = null, Type stWant = null)
    {
        Validation.BugCheckValue(formula, nameof(formula));
        var fma = RexlFormula.Create(SourceContext.Create(formula));
        Validation.BugCheckParam(!fma.HasErrors, nameof(formula));
        var bfma = BoundFormula.Create(fma, _host);
        codeGen ??= _codeGen;
        if (stWant != null)
        {
            codeGen.TypeManager.TryEnsureSysType(bfma.BoundTree.Type, out var stActual);
            Validation.BugCheckParam(stWant.IsAssignableFrom(stActual), nameof(stWant));
        }
        Validation.BugCheckParam(!bfma.HasErrors, nameof(formula));
        var resCodeGen = codeGen.Run(bfma.BoundTree);
        var globals = resCodeGen.Globals;
        object[] args = null;
        if (!globals.IsDefaultOrEmpty)
        {
            Validation.BugCheck(globals.Length == 1);
            var glob = globals[0];
            Validation.BugCheck(glob.IsCtx);
            args = new object[] { ExecCtx.CreateBare() };
        }
        return (bfma.BoundTree.Type, resCodeGen.Func.Invoke(args));
    }

    /// <summary>
    /// Strongly typed version of <see cref="ExecuteFormula(string, Type)"/>.
    /// Throws if the input formula is invalid or if the result type is not of the specified type <typeparamref name="T"/>.
    /// </summary>
    public static (DType, T) ExecuteFormula<T>(string formula, EnumerableCodeGeneratorBase codeGen = null)
    {
        Validation.BugCheckValue(formula, nameof(formula));
        var (dtype, result) = ExecuteFormula(formula, codeGen, typeof(T));
        return (dtype, (T)result);
    }

    private sealed class BindHostLonerWithBuiltinFuncs : MinBindHost
    {
        private readonly OperationRegistry _opers;

        public BindHostLonerWithBuiltinFuncs()
            : base()
        {
            // REVIEW: Should this be generalized?
            _opers = TestFunctions.Instance;
        }

        public override bool TryGetOperInfoOne(NPath name, bool user, bool fuzzy, int arity, out OperInfo info)
        {
            if (user | fuzzy)
            {
                info = null;
                return false;
            }

            info = _opers.GetInfo(name);
            return info != null;
        }
    }
}
