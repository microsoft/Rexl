// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

/// <summary>
/// Represents a range of slots (or indices).
/// </summary>
public struct SlotRange
{
    public readonly int Min;
    public readonly int Lim;

    public int Count => Lim - Min;

    public SlotRange(int min, int lim)
    {
        Validation.BugCheckParam(min >= 0, nameof(min));
        Validation.BugCheckParam(lim >= min, nameof(lim));

        Min = min;
        Lim = lim;
    }
}
