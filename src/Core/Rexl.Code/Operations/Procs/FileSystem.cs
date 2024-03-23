// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Code;

public sealed class ListFilesProcGen : RexlOperationGenerator<ListFilesProc>
{
    public static readonly ListFilesProcGen Instance = new ListFilesProcGen();

    private ListFilesProcGen()
    {
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        var ilw = codeGen.Writer;

        if (call.Args.Length > 0)
        {
            // Convert string to Link.
            var typeSrc = call.Args[0].Type;
            Validation.Assert(typeSrc.Kind == DKind.Text | typeSrc.Kind == DKind.Uri);
            if (typeSrc.Kind == DKind.Text)
                ilw.Call(LinkHelpers.MethLinkFromPath);
        }

        codeGen.GenLoadActionHost();

        var meth = call.Args.Length == 0 ?
            new Func<ActionHost, ActionRunner>(Exec).Method :
            new Func<Link, ActionHost, ActionRunner>(Exec).Method;
        ilw.Call(meth);

        stRet = meth.ReturnType;
        return true;
    }

    private static ActionRunner Exec(ActionHost host)
    {
        return new ThreadRunner(host, null);
    }

    private static ActionRunner Exec(Link link, ActionHost host)
    {
        return new ThreadRunner(host, link);
    }

    private sealed class ThreadRunner : ThreadActionRunner
    {
        private const int k_idLink = 0;
        private const int k_idFull = 1;
        private const int k_idData = 2;

        private readonly ActionHost _host;
        private readonly Link _link;

        // These are set when priming.
        private Link _full;
        private IEnumerable<Link> _seq;

        public ThreadRunner(ActionHost host, Link link)
            : base()
        {
            Validation.BugCheckValue(host, nameof(host));
            Validation.BugCheckValueOrNull(link);

            _host = host;
            _link = link;

            Validation.Verify(AddStableResult("Link", UriFlavors.UriData).Index == k_idLink);
        }

        protected override Task PrimeCoreAsync()
        {
            Validation.Assert(!IsPrimed);

            _seq = _host.GetFiles(_link, out _full);
            if (_full == null)
                _full = _link;

            Validation.Verify(AddStableResult("FullLink", UriFlavors.UriData).Index == k_idFull);
            return Task.CompletedTask;
        }

        protected override Task RunCoreAsync()
        {
            Validation.Verify(AddResult("Data", DType.UriGen.ToSequence(), isPrimary: true).Index == k_idData);
            return Task.CompletedTask;
        }

        protected override object GetResultValueCore(ResultInfo info)
        {
            Validation.AssertValue(info);
            switch (info.Index)
            {
            case k_idLink:
                return _link;
            case k_idFull:
                return _full;
            case k_idData:
                return _seq;
            default:
                throw Validation.BugExceptParam(nameof(info));
            }
        }
    }
}
