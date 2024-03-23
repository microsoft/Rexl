// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Rexl.Kernel;

/// <summary>
/// Abstract class to publish error and data text.
/// </summary>
public abstract class Publisher
{
    /// <summary>
    /// Publish error information, given its plain and/or html representation.
    /// </summary>
    public abstract void PublishError(ExecuteMessage msg, string? plain, string? html = null);

    /// <summary>
    /// Publish data information, given its plain text representation.
    /// </summary>
    public abstract void PublishData(ExecuteMessage msg, string? plain, string? html = null);
}
