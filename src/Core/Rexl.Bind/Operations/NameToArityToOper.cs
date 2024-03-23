// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Rexl;

/// <summary>
/// Persistent dictionary from <see cref="NPath"/> to <see cref="ArityToOper"/>.
/// REVIEW: Nothing in Rexl uses this. It could be moved to SE code.
/// </summary>
public sealed class NameToArityToOper : NPathRedBlackTree<NameToArityToOper, ArityToOper>
{
    public static readonly NameToArityToOper Empty = new NameToArityToOper(null);

    private NameToArityToOper(Node root)
        : base(root)
    {
    }

    protected override NameToArityToOper Wrap(Node root)
    {
        return root == _root ? this : root != null ? new NameToArityToOper(root) : Empty;
    }

    protected override bool KeyIsValid(NPath key)
    {
        return key != default;
    }

    protected override bool ValIsValid(ArityToOper val)
    {
        return val != null && val.Count > 0;
    }

    protected override bool ValEquals(ArityToOper val0, ArityToOper val1)
    {
        return val0 == val1;
    }

    protected override int ValHash(ArityToOper val)
    {
        return val.GetHashCode();
    }
}

/// <summary>
/// Persistent dictionary from arity to <see cref="OperInfo"/>.
/// </summary>
public sealed class ArityToOper : IntRedBlackTree<ArityToOper, OperInfo>
{
    public static readonly ArityToOper Empty = new ArityToOper(null);

    private ArityToOper(Node root)
        : base(root)
    {
    }

    protected override ArityToOper Wrap(Node root)
    {
        return root == _root ? this : root != null ? new ArityToOper(root) : Empty;
    }

    protected override bool KeyIsValid(int key)
    {
        return key >= 0;
    }

    protected override bool ValEquals(OperInfo val0, OperInfo val1)
    {
        return val0 == val1;
    }

    protected override int ValHash(OperInfo val)
    {
        return val != null ? val.GetHashCode() : 0;
    }

    protected override bool ValIsValid(OperInfo val)
    {
        return val != null;
    }
}
