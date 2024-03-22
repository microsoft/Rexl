// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Reflection;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Code;

public abstract class ReadFromStreamGen<TOper> : RexlOperationGenerator<TOper>
    where TOper : ReadFromStreamFunc
{
    public static Stream LoadStream(Link link, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(link);
        Validation.AssertValue(ctx);

        return ctx.LoadStream(link, id);
    }
}

public sealed class ReadBytesGen : ReadFromStreamGen<ReadBytesFunc>
{
    public static readonly ReadBytesGen Instance = new ReadBytesGen();

    private readonly MethodInfo _meth;

    private ReadBytesGen()
    {
        _meth = new Func<Link, ExecCtx, int, Tensor<byte>>(Exec).Method;
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        var ilw = codeGen.Writer;

        // Convert string to Link.
        var typeSrc = call.Args[0].Type;
        Validation.Assert(typeSrc.Kind == DKind.Text | typeSrc.Kind == DKind.Uri);
        if (typeSrc.Kind == DKind.Text)
            ilw.Call(LinkHelpers.MethLinkFromPath);

        stRet = GenCallCtxId(codeGen, _meth, LinkHelpers.StsLink, call);
        return true;
    }

    private static Tensor<byte> Exec(Link link, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(link);
        Validation.AssertValue(ctx);

        try
        {
            using var stream = LoadStream(link, ctx, id);
            if (stream is null)
                return null;

            var res = Tensor.ReadAllBytes(stream);
            return res;
        }
        catch
        {
            // REVIEW: Log the exception!
            return null;
        }
    }
}
