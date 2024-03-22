// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.Rexl;

/// <summary>
/// Stream capabilities requested in addition to the Read/Write capability when loading/creating
/// a stream.
/// </summary>
[Flags]
public enum StreamOptions
{
    None = 0x00,

    /// <summary>
    /// Set when seekability is needed.
    /// </summary>
    NeedSeek = 0x01,

    /// <summary>
    /// Set if an existing file should not be overwritten when creating. Such a request should
    /// throw if there is an existing file.
    /// </summary>
    DontOverwrite = 0x02,
}
