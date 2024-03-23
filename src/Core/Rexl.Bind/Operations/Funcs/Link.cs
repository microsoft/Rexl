// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

/// <summary>
/// Common uri flavors.
/// </summary>
public static class UriFlavors
{
    public static readonly NPath Text = NPath.Root.Append(new DName("Text"));
    public static readonly NPath Data = NPath.Root.Append(new DName("Data"));
    public static readonly NPath Image = NPath.Root.Append(new DName("Image"));
    public static readonly NPath Audio = NPath.Root.Append(new DName("Audio"));
    public static readonly NPath Video = NPath.Root.Append(new DName("Video"));
    public static readonly NPath Document = NPath.Root.Append(new DName("Document"));

    public static readonly DType UriText = DType.CreateUriType(Text);
    public static readonly DType UriData = DType.CreateUriType(Data);
    public static readonly DType UriImage = DType.CreateUriType(Image);
    public static readonly DType UriAudio = DType.CreateUriType(Audio);
    public static readonly DType UriVideo = DType.CreateUriType(Video);
    public static readonly DType UriDocument = DType.CreateUriType(Document);
}

/// <summary>
/// Rexl functions to create <see cref="Link"/> objects.
/// REVIEW: We should also add functions to get the components from a <see cref="Link"/> object.
/// </summary>
public sealed class LinkFunc : RexlOper
{
    // REVIEW: Note that we don't want functions for LinkKind.Temporary, or LinkKind.Invalid,
    // although we may need something special for Temporary.

    private static readonly Immutable.Array<LinkFunc> _funcs = MakeFuncs();

    private static Immutable.Array<LinkFunc> MakeFuncs()
    {
        var flavors = new[]
        {
            (UriFlavors.UriText, "Text"),
            (UriFlavors.UriData, "Data"),
            (UriFlavors.UriImage, "Image"),
            (UriFlavors.UriAudio, "Audio"),
            (UriFlavors.UriVideo, "Video"),
            (UriFlavors.UriDocument, "PDF"),
        };

        var kinds = new[]
        {
            // REVIEW: Is temp needed?
            // (LinkKind.Temporary, "Temp"),
            (LinkKind.Generic, "Local"),
            (LinkKind.Http, "Web"),
            (LinkKind.AzureBlob, "Blob"),
            (LinkKind.AzureDataLake, "LegacyDataLake"),
            (LinkKind.AzureDataLakeGen2, "DataLake"),
        };

        int linkFuncCount = flavors.Length * kinds.Length + kinds.Length;
        var bldr = Immutable.Array<LinkFunc>.CreateBuilder(linkFuncCount, init: true);
        int i = 0;
        foreach (var (kind, strKind) in kinds)
        {
            var strKindLong = kind == LinkKind.AzureBlob ? "BlobStorage" : strKind;
            bool isTemp = kind == LinkKind.Temporary;
            bool hasLong = !isTemp && kind != LinkKind.Generic;

            var carg = kind.NeedsAccount() ? 2 : 1;
            foreach (var (type, strFlav) in flavors)
            {
                bldr[i++] = new LinkFunc(
                    strKind + strFlav, hasLong ? "LinkTo" + strKindLong + strFlav : null,
                    kind, carg, type, isTemp);
            }

            // Add link without flavor.
            bldr[i++] = new LinkFunc(strKind, null, kind, carg + 1, isTemp);
        }
        Validation.Assert(i == linkFuncCount);
        return bldr.ToImmutable();
    }

    /// <summary>
    /// All the link generation functions.
    /// </summary>
    public static Immutable.Array<LinkFunc> Funcs => _funcs;

    public DType TypeRes { get; }

    public bool NeedsFlavor => !TypeRes.IsValid;

    public int Arity { get; }

    /// <summary>
    /// The legacy name for this function.
    /// </summary>
    public NPath LegacyName { get; }

    /// <summary>
    /// Whether this function should be hidden.
    /// </summary>
    public bool IsHidden { get; }

    /// <summary>
    /// The kind of link for this function.
    /// </summary>
    public LinkKind Kind { get; }

    private LinkFunc(string name, string nameLeg, LinkKind kind, int arity, DType type, bool hidden)
        : base(isFunc: true, new DName(name), BindUtil.LinkNs, arity, arity)
    {
        Validation.Assert(arity == (kind.NeedsAccount() ? 2 : 1));
        Validation.Assert(type.IsUri);

        LegacyName = string.IsNullOrEmpty(nameLeg) ? NPath.Root : NPath.Root.Append(new DName(nameLeg));
        IsHidden = hidden;

        Kind = kind;
        TypeRes = type;
        Arity = arity;
    }

    private LinkFunc(string name, string nameLeg, LinkKind kind, int arity, bool hidden)
        : base(isFunc: true, new DName(name), BindUtil.LinkNs, arity, arity)
    {
        Validation.Assert(arity == (kind.NeedsAccount() ? 3 : 2));

        LegacyName = string.IsNullOrEmpty(nameLeg) ? NPath.Root : NPath.Root.Append(new DName(nameLeg));
        IsHidden = hidden;

        Kind = kind;
        TypeRes = default;
        Arity = arity;
    }

    /// <summary>
    /// Return the func that corresponds to the given <paramref name="kind"/> and <paramref name="flavor"/>.
    /// Returns <c>null</c> if there isn't one.
    /// </summary>
    public static LinkFunc GetLinkFunc(LinkKind kind, NPath flavor)
    {
        foreach (var func in _funcs)
        {
            if (func.Kind == kind && func.TypeRes.GetRootUriFlavor() == flavor)
                return func;
        }
        return null;
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        return ArgTraitsSimple.Create(this, eager: false, carg);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var bldr = Immutable.Array.CreateBuilder<DType>(info.Args.Length, init: true);
        for (int i = 0; i < bldr.Count; i++)
            bldr[i] = DType.Text;

        var typeRes = TypeRes;
        if (NeedsFlavor)
        {
            var flavor = info.Args[0];
            var flavorString = string.Empty;
            if (flavor.Kind != BndNodeKind.Null && !flavor.TryGetString(out flavorString))
            {
                info.PostDiagnostic(RexlDiagnostic.Error(info.GetParseArg(0),
                    ErrorStrings.ErrNonTextLiteralFlavor));
                typeRes = DType.UriGen;
            }
            else if (!TryParseFlavor(flavorString, out typeRes))
            {
                info.PostDiagnostic(RexlDiagnostic.Error(info.GetParseArg(0),
                    ErrorStrings.ErrInvalidFlavor_Flavor,
                    LexUtils.GetTextLiteral(flavorString)));
                typeRes = DType.UriGen;
            }
        }
        Validation.Assert(typeRes.IsUri);
        return (typeRes, bldr.ToImmutable());
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        Validation.Assert(SupportsArity(call.Args.Length));
        if (!NeedsFlavor)
        {
            Validation.Assert(TypeRes.IsUri);
            if (call.Type != TypeRes)
                return false;
        }

        var args = call.Args;
        for (int slot = 0; slot < args.Length; slot++)
        {
            if (args[slot].Type != DType.Text)
                return false;
        }

        if (NeedsFlavor)
        {
            if (!args[0].TryGetString(out var flavor))
                full = false;
            else if (!TryParseFlavor(flavor, out var typeRes))
                full = false;
            else if (call.Type != typeRes)
                full = false;
        }
        return true;
    }

    private bool TryParseFlavor(string flavor, out DType type)
    {
        var full = $"U<{flavor}>";
        return DType.TryDeserialize(full, out type);
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));
        Validation.Assert(call.Type.IsUri);

        var args = call.Args;
        // Flavor can be null/empty string, no need to validate flavor.
        for (int i = NeedsFlavor.ToNum(); i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.TryGetString(out var val) && string.IsNullOrEmpty(val))
                return BndNullNode.Create(call.Type);
        }

        return call;
    }
}

/// <summary>
/// Rexl properties to get information from links.
/// </summary>
public sealed class LinkProp : RexlOper
{
    public enum PropKind : byte
    {
        Kind,
        Acct,
        Path,
    }

    public static readonly LinkProp FuncKind = new LinkProp("Kind", RexlStrings.AboutLinkKind, PropKind.Kind);
    public static readonly LinkProp FuncAccount = new LinkProp("Account", RexlStrings.AboutLinkAccount, PropKind.Acct);
    public static readonly LinkProp FuncPath = new LinkProp("Path", RexlStrings.AboutLinkPath, PropKind.Path);

    public PropKind Kind { get; }

    private readonly StringId _sidAbout;

    private LinkProp(string name, StringId about, PropKind kind)
        : base(isFunc: true, new DName(name), BindUtil.LinkNs, 1, 1)
    {
        _sidAbout = about;
        Kind = kind;
    }

    public override bool IsProperty(DType typeThis)
    {
        return typeThis.IsUri;
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.BugCheckParam(SupportsArity(carg), nameof(carg));
        Validation.Assert(carg == 1);
        var maskAll = BitSet.GetMask(carg);
        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: maskAll, maskLiftTen: maskAll);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));
        Validation.Assert(info.Arity == 1);

        var type = info.Args[0].Type;
        if (!type.IsUri)
            type = DType.UriGen;
        return (DType.Text, Immutable.Array.Create(type));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Type != DType.Text)
            return false;
        if (!call.Args[0].Type.IsUri)
            return false;
        return true;
    }
}
