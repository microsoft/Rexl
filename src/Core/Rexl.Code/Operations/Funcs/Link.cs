// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Code;

public sealed class LinkGen : RexlOperationGenerator<LinkFunc>
{
    public static readonly LinkGen Instance = new LinkGen();

    private readonly MethodInfo _meth1;
    private readonly MethodInfo _meth2;
    private readonly MethodInfo _methFlav2;
    private readonly MethodInfo _methFlav3;

    private LinkGen()
    {
        _meth1 = new Func<string, LinkFunc, Link>(Exec).Method;
        _meth2 = new Func<string, string, LinkFunc, Link>(Exec).Method;

        _methFlav2 = new Func<string, string, LinkFunc, Link>(ExecWithFlavor).Method;
        _methFlav3 = new Func<string, string, string, LinkFunc, Link>(ExecWithFlavor).Method;
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));

        var func = GetOper(call);

        MethodInfo meth;
        if (func.NeedsFlavor)
        {
            Validation.Assert(func.Arity == 2 | func.Arity == 3);
            meth = func.Arity == 2 ? _methFlav2 : _methFlav3;
        }
        else
        {
            Validation.Assert(func.Arity == 1 | func.Arity == 2);
            meth = func.Arity == 1 ? _meth1 : _meth2;
        }

        stRet = GenCallExtra(codeGen, meth, sts, func);
        return true;
    }

    private static Link Exec(string path, LinkFunc func)
    {
        Validation.AssertValue(func, nameof(func));
        if (string.IsNullOrEmpty(path))
            return null;
        return Link.Create(func.Kind, null, path);
    }

    private static Link ExecWithFlavor(string flavor, string path, LinkFunc func)
    {
        return Exec(path, func);
    }

    private static Link Exec(string acct, string path, LinkFunc func)
    {
        Validation.AssertValue(func, nameof(func));
        if (string.IsNullOrEmpty(acct))
            return null;
        if (string.IsNullOrEmpty(path))
            return null;
        return Link.Create(func.Kind, acct, path);
    }

    private static Link ExecWithFlavor(string flavor, string acct, string path, LinkFunc func)
    {
        return Exec(acct, path, func);
    }
}

public sealed class LinkPropGen : GetMethGen<LinkProp>
{
    public static readonly LinkPropGen Instance = new LinkPropGen();

    private readonly MethodInfo _methKind;
    private readonly MethodInfo _methAcct;
    private readonly MethodInfo _methPath;

    private LinkPropGen()
    {
        _methKind = new Func<Link, string>(ExecKind).Method;
        _methAcct = new Func<Link, string>(ExecAccount).Method;
        _methPath = new Func<Link, string>(ExecPath).Method;
    }

    protected override bool TryGetMeth(ICodeGen codeGen, BndCallNode call, out MethodInfo meth)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));

        var prop = GetOper(call);

        switch (prop.Kind)
        {
        case LinkProp.PropKind.Kind: meth = _methKind; return true;
        case LinkProp.PropKind.Acct: meth = _methAcct; return true;
        case LinkProp.PropKind.Path: meth = _methPath; return true;
        }

        meth = null;
        return false;
    }

    private static string ExecKind(Link link)
    {
        if (link == null)
            return null;

        switch (link.Kind)
        {
        case LinkKind.Http: return "Web";
        case LinkKind.AzureDataLake: return "LegacyDataLake";
        case LinkKind.AzureDataLakeGen2: return "DataLake";
        case LinkKind.AzureBlob: return "Blob";
        case LinkKind.Generic: return "Local";
        case LinkKind.Temporary: return "Temp";

        default:
            return null;
        }
    }

    private static string ExecAccount(Link link)
    {
        return link?.AccountId;
    }

    private static string ExecPath(Link link)
    {
        return link?.Path;
    }
}
