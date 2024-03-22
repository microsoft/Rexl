// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

/// <summary>
/// Procedure to "list" files. Takes zero or one arguments. The arg can be a link
/// or text value.
/// REVIEW: Currently the uri type must have flavor `Data`. Is that appropriate?
/// Perhaps allow any flavor? Or have a flavor for directories?
/// </summary>
public sealed partial class ListFilesProc : DataProc
{
    public static readonly ListFilesProc Instance = new ListFilesProc();

    private ListFilesProc()
        : base(new DName("ListFiles"), DType.UriGen, 0, 1)
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

        if (info.Arity == 0)
            return (DType.General, Immutable.Array<DType>.Empty);
        var type = info.Args[0].Type;
        EnsureTextOrUri(ref type);
        return (DType.General, Immutable.Array.Create(type));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Args.Length > 0 && !CertifyTextOrUri(call.Args[0].Type))
            return false;
        return true;
    }
}
