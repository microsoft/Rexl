// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Rexl;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Harness;

// This partial handles task related functionality.
partial class SimpleHarnessBase
{
    protected override Task<(Link full, Stream stream)> LoadStreamForTaskAsync(SourceContext sctx, Link link)
    {
        Validation.AssertValue(sctx);
        Validation.AssertValueOrNull(link);

        return _storage.LoadStreamAsync(sctx.LinkCtx, link);
    }

    protected override Task<(Link full, Stream stream)> CreateStreamForTaskAsync(SourceContext sctx, Link link,
        StreamOptions options = default)
    {
        Validation.AssertValue(sctx);
        Validation.AssertValueOrNull(link);

        return _storage.CreateStreamAsync(sctx.LinkCtx, link, options);
    }

    protected override IEnumerable<Link> GetFilesForTask(SourceContext sctx, Link linkDir, out Link full)
    {
        Validation.AssertValue(sctx);
        Validation.AssertValueOrNull(linkDir);

        IEnumerable<Link> seq;
        (full, seq) = _storage.GetFiles(sctx.LinkCtx, linkDir);
        return seq;
    }
}
