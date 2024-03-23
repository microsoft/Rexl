// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Rexl;

/// <summary>
/// Indicates the kind of a scope parameter in a function call.
/// </summary>
public enum ScopeKind
{
    /// <summary>
    /// Not applicable, since there are no scopes.
    /// </summary>
    None,

    /// <summary>
    /// The value of the scope is the value of the arg.
    /// </summary>
    With,

    /// <summary>
    /// The value of the scope is the non-null value of the arg, with execution
    /// of nested args bypassed if the arg value is null.
    /// </summary>
    Guard,

    /// <summary>
    /// The value of the scope is an item of the arg, which must be a sequence.
    /// </summary>
    SeqItem,

    /// <summary>
    /// The value of the scope is an item of the arg, which must be a tensor.
    /// </summary>
    TenItem,

    /// <summary>
    /// The value of the scope is an item initialized by the arg. The actual value in any
    /// referencing (nested) arg may vary.
    /// </summary>
    Iter,

    /// <summary>
    /// The value of the scope always corresponds to the i8 value of its index.
    /// The arg specifies the lim value.
    /// REVIEW: SeqItem scopes initialized by Range(N) could be reduced to this.
    /// </summary>
    Range,

    /// <summary>
    /// An index associated with sequence iteration.
    /// </summary>
    SeqIndex,

    _Lim
}

/// <summary>
/// Extension methods for ScopeKind.
/// </summary>
public static class ScopeKindUtil
{
    /// <summary>
    /// Returns true if the given kind is in scope iff we are inside a loop body.
    /// </summary>
    public static bool IsLoopScope(this ScopeKind kind)
    {
        switch (kind)
        {
        case ScopeKind.SeqItem:
        case ScopeKind.TenItem:
        case ScopeKind.SeqIndex:
        case ScopeKind.Iter:
        case ScopeKind.Range:
            return true;
        }
        return false;
    }
}
