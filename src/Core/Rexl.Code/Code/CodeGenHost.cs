// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Rexl.Code;

/// <summary>
/// The code gen host serves as an interface between the client and the code gen process.
/// </summary>
public abstract class CodeGenHost
{
    /// <summary>
    /// Creates a bare code gen host. This is the host used if one is not provided.
    /// </summary>
    public static CodeGenHost CreateBare()
    {
        return BareImpl.Instance;
    }

    private sealed class BareImpl : CodeGenHost
    {
        public static readonly BareImpl Instance = new BareImpl();

        private BareImpl()
        {
        }
    }
}
