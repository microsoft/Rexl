// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

/// <summary>
/// Represents a structural type. These are immutable. The possibilities are:
/// * The default value, default(DType), which is invalid (.IsValid return false).
/// * A primitive type, like text, r8, i8, date, time, etc.
/// * A record type, which is a set of (field-name, type) pairs.
/// * A tuple type, which has zero or more slot types.
/// * A tensor type, which has an item type and rank.
/// * A uri type, which has a flavor.
/// * A nullable primitive, record, tuple, tensor, or uri type.
/// * A sequence type, which has an item type, but no indication of number of items.
/// </summary>
partial struct DType
{
    /// <summary>
    /// Create a Uri DType of the given flavor. The flavor may be NPath.Root, indicating the super-type
    /// of all Uri types. The DType system doesn't understand the semantics of the "flavor", other than
    /// that sub-path is equivalent to super-type.
    /// </summary>
    public static DType CreateUriType(NPath flavor)
    {
        flavor = CleanseFlavor(flavor);
        return new DType(DKind.Uri, opt: true, 0, flavor.GetBlob(), DTypeFlags.HasUri);
    }

    /// <summary>
    /// This removes "Flavor" suffixes from all elements of a flavor path.
    /// REVIEW: This is a temporary hack to help client code transition to uri paths that don't use "Flavor"
    /// as a suffix. When should it be removed?
    /// </summary>
    private static NPath CleanseFlavor(NPath flavor)
    {
        const string tag = "Flavor";

        string name;
        switch (flavor.NameCount)
        {
        case 0:
            return flavor;
        case 1:
            name = flavor.Leaf.Value;
            if (name.Length > tag.Length && name.EndsWith(tag) && DName.TryWrap(name.Substring(0, name.Length - tag.Length), out var dn))
                return NPath.Root.Append(dn);
            return flavor;
        }

        Immutable.Array<DName>.Builder? bldr = null;
        NPath prefix = flavor;
        for (var path = flavor; !path.IsRoot;)
        {
            path = path.PopOne(out var cur);
            name = cur.Value;
            if (name.Length > tag.Length && name.EndsWith(tag) && DName.TryWrap(name.Substring(0, name.Length - tag.Length), out var dn))
            {
                bldr ??= flavor.ToNames();
                bldr[path.NameCount] = dn;
                prefix = path;
            }
        }

        if (bldr == null)
            return flavor;

        Validation.Assert(bldr.Count == flavor.NameCount);
        var res = prefix;
        for (int i = prefix.NameCount; i < bldr.Count; i++)
            res = res.Append(bldr[i]);
        Validation.Assert(res.NameCount == flavor.NameCount);
        return res;
    }

    /// <summary>
    /// Checks that the root is a Uri type and returns the flavor as an <see cref="NPath"/>. Note that
    /// the general Uri type returns <see cref="NPath.Root"/>.
    /// </summary>
    public NPath GetRootUriFlavor()
    {
        Validation.BugCheck(_kind == DKind.Uri, "Wrong kind");
        AssertValid();
        return NPath.CreateFromBlob(_detail);
    }

    /// <summary>
    /// If <c>this</c> is a uri type or sequence of uri, strip the flavor. Otherwise, return <c>this</c>.
    /// </summary>
    public DType StripFlavor()
    {
        if (_kind != DKind.Uri)
            return this;

        AssertValid();
        if (_detail == null)
            return this;
        return new DType(DKind.Uri, _opt, _seqCount, null);
    }

    private static DType _GetUriSuperType(NPath flavor1, NPath flavor2, int seqCount)
    {
        NPath flavor = flavor1.GetCommonPrefix(flavor2);
        return DType.CreateUriType(flavor).ToSequence(seqCount);
    }
}
