// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Bind;

/// <summary>
/// This contains rexl language semantic definition functionality that is relevant
/// at both bind/reduce time and code gen time.
/// </summary>
public static class BindUtil
{
    public static readonly NPath DateNs = NPath.Root.Append(new DName("Date"));
    public static readonly NPath TimeNs = NPath.Root.Append(new DName("Time"));
    public static readonly NPath IntNs = NPath.Root.Append(new DName("Int"));
    public static readonly NPath FloatNs = NPath.Root.Append(new DName("Float"));
    public static readonly NPath TextNs = NPath.Root.Append(new DName("Text"));
    public static readonly NPath TupleNs = NPath.Root.Append(new DName("Tuple"));
    public static readonly NPath TensorNs = NPath.Root.Append(new DName("Tensor"));
    public static readonly NPath LinkNs = NPath.Root.Append(new DName("Link"));
    public static readonly NPath GuidNs = NPath.Root.Append(new DName("Guid"));
    public static readonly NPath ModuleNs = NPath.Root.Append(new DName("Module"));

    /// <summary>
    /// This clips the bitwise shift amount when shifting big integer values.
    /// We don't support arbitrarily large 63 bit shift amounts since we would
    /// quickly run out of memory and System.Numerics.BigInteger doesn't directly
    /// support them.
    /// </summary>
    public static int ClipShift(long amt)
    {
        Validation.Assert(amt > 0);
        // REVIEW: Should this "and" or "min"? C# uses "and".
        // REVIEW: Should we use something smaller than int.MaxValue, eg, 0xFFFF?
        return (int)Math.Min(amt, int.MaxValue);
    }

    /// <summary>
    /// Map from <see cref="DType"/> to function namespace, if there is one.
    /// </summary>
    public static NPath GetFuncNSForType(DType typeSrc)
    {
        // REVIEW: Generalize this somehow? This should really be specified by the client,
        // not hard-coded in the binder. Perhaps the _getFuncInfo needs to be generalized to be
        // a "host" object that provides all the various information:
        // * Functions
        // * Property namespaces
        // * Globals
        // * Global namespaces
        // Etc.
        switch (typeSrc.RootKind)
        {
        default: return default;
        case DKind.Date: return DateNs;
        case DKind.Time: return TimeNs;

        case DKind.IA:
        case DKind.I8:
        case DKind.I4:
        case DKind.I2:
        case DKind.I1:
        case DKind.U8:
        case DKind.U4:
        case DKind.U2:
        case DKind.U1:
        case DKind.Bit:
            return IntNs;
        case DKind.R8:
        case DKind.R4:
            return FloatNs;

        case DKind.Text: return TextNs;
        case DKind.Tuple: return TupleNs;
        case DKind.Tensor: return TensorNs;
        case DKind.Uri: return LinkNs;
        case DKind.Guid: return GuidNs;
        case DKind.Module: return ModuleNs;
        }
    }
}
