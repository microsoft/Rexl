// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Rexl;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RexlTest;

public abstract class RexlTestsBaseType<TOpts> : RexlTestsBase<SbSysTypeSink, TOpts>
{
    private readonly SbSysTypeSink _sink = new SbSysTypeSink();

    protected sealed override SbSysTypeSink Sink => _sink;

    protected sealed override string GetTextAndReset()
    {
        var res = _sink.Builder.ToString();
        _sink.Builder.Clear();
        return res;
    }

    protected RexlTestsBaseType() : base()
    {
    }

    #region Link construction for convenience

    public static readonly NPath GenFlavor = NPath.Root;
    public static readonly NPath FileFlavor = NPath.Root.Append(new DName("File"));
    public static readonly NPath ImageFlavor = NPath.Root.Append(new DName("Image"));
    public static readonly NPath JpegFlavor = ImageFlavor.Append(new DName("Jpeg"));
    public static readonly NPath XrayFlavor = JpegFlavor.Append(new DName("Xray"));
    public static readonly NPath PdfFlavor = NPath.Root.Append(new DName("Pdf"));

    protected Link MakeLink(string value, NPath flavor)
    {
        return Link.CreateGeneric(flavor.ToDottedSyntax() + "/" + value);
    }

    #endregion Link construction for convenience
}
