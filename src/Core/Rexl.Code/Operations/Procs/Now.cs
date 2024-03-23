// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Code;

public sealed class NowGen : RexlOperationGenerator<NowProc>
{
    public static readonly NowGen Instance = new NowGen();

    private readonly MethodInfo _meth;

    private NowGen()
    {
        _meth = new Func<NowProc.NowKind, ActionHost, ActionRunner>(Exec).Method;
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        var proc = GetOper(call);

        codeGen.Writer.Ldc_U4((byte)proc.Kind);
        codeGen.GenLoadActionHost();
        codeGen.Writer.Call(_meth);

        stRet = _meth.ReturnType;
        return true;
    }

    private static ActionRunner Exec(NowProc.NowKind kind, ActionHost host)
    {
        return new Runner(kind, host);
    }

    private sealed class Runner : SyncActionRunner
    {
        private const string k_nameUtc = "Utc";
        private const string k_nameLoc = "Local";
        private const string k_nameOff = "Offset";

        private readonly NowProc.NowKind _kind;
        private readonly ActionHost _host;

        private DateTimeOffset _dto;

        public Runner(NowProc.NowKind kind, ActionHost host)
        {
            Validation.BugCheckValue(host, nameof(host));
            _kind = kind;
            _host = host;
        }

        protected override object GetResultValueCore(ResultInfo info)
        {
            Validation.AssertValue(info);
            switch ((NowProc.NowKind)info.Index)
            {
            case NowProc.NowKind.Utc:
                return RDate.FromTicks(_dto.UtcTicks);
            case NowProc.NowKind.Local:
                // dto.LocalDateTime doesn't seem to use the offset in the DateTimeOffset! Instead it uses the
                // current ("local") time zone. We want to use the offset in dto. Also, dto.LocalDateTime or
                // dto.DateTime can throw if the utc value is too close to the range end points.
                return RDate.FromTicks(_dto.UtcTicks + _dto.Offset.Ticks);
            default:
                Validation.Assert(info.Index == (int)NowProc.NowKind.Offset);
                return _dto.Offset;
            }
        }

        protected override void AbortCore()
        {
        }

        protected override void RunCore()
        {
            _dto = _host.Now();

            // Publish the results.
            Validation.Verify(AddResult("Utc", DType.DateReq, isPrimary: _kind == NowProc.NowKind.Utc).Index == (int)NowProc.NowKind.Utc);
            Validation.Verify(AddResult("Local", DType.DateReq, isPrimary: _kind == NowProc.NowKind.Local).Index == (int)NowProc.NowKind.Local);
            Validation.Verify(AddResult("Offset", DType.TimeReq, isPrimary: _kind == NowProc.NowKind.Offset).Index == (int)NowProc.NowKind.Offset);
        }
    }
}
