// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Bind;

/// <summary>
/// The kind of name in a <see cref="NameInfo"/>.
/// </summary>
public enum NameKind : int
{
    Namespace,
    Function,

    Global,

    Field,

    ScopeName,
    ScopeField,
}

/// <summary>
/// Information about a name generated for intellisense by the <see cref="BoundFormula"/>.
/// </summary>
public struct NameInfo
{
    // The nesting values to use for non-scoped things.
    private const int NestingNamespace = -1;
    private const int NestingFunction = -1;
    private const int NestingField = -1;

    // This is just some arbitrary, but large value, since globals come last in lookup.
    private const int NestingGlobal = ushort.MaxValue;

    /// <summary>
    /// The name, fully escaped, as the user would type it. Note that it is not fully qualified, so
    /// may be hidden by other names. The additional information should make it clear how the name can
    /// be qualified.
    /// </summary>
    public readonly string Name;

    /// <summary>
    /// The kind of this name.
    /// </summary>
    public readonly NameKind Kind;

    /// <summary>
    /// The scope nesting level. Generally, sorting (increasing) by this will put the names
    /// in binding order.
    /// </summary>
    public readonly int Nesting;

    private NameInfo(DName name, NameKind kind, int nesting)
    {
        Validation.Assert(name.IsValid);
        Validation.Assert(NameKind.Function <= kind && kind <= NameKind.ScopeField);
        Name = name.Escape();
        Kind = kind;
        Nesting = nesting;
    }

    private NameInfo(NPath name, NameKind kind, int nesting)
    {
        Validation.Assert(name != default);
        Validation.Assert(NameKind.Function <= kind && kind <= NameKind.ScopeField);
        Name = name.ToDottedSyntax();
        Kind = kind;
        Nesting = nesting;
    }

    /// <summary>
    /// Create a <see cref="NameInfo"/> for a namespace.
    /// </summary>
    public static NameInfo Namespace(NPath path)
    {
        return new NameInfo(path, NameKind.Namespace, NestingNamespace);
    }

    /// <summary>
    /// Create a <see cref="NameInfo"/> for a function.
    /// </summary>
    public static NameInfo Function(NPath name)
    {
        return new NameInfo(name, NameKind.Function, NestingFunction);
    }

    /// <summary>
    /// Create a <see cref="NameInfo"/> for a global.
    /// </summary>
    public static NameInfo Global(NPath name)
    {
        return new NameInfo(name, NameKind.Global, NestingGlobal);
    }

    /// <summary>
    /// Create a <see cref="NameInfo"/> for a field.
    /// </summary>
    public static NameInfo Field(DName name)
    {
        return new NameInfo(name, NameKind.Field, NestingField);
    }

    /// <summary>
    /// Create a <see cref="NameInfo"/> for a scope (variable) name.
    /// </summary>
    public static NameInfo ScopeName(DName name, int nesting)
    {
        Validation.Assert(nesting >= 0);
        return new NameInfo(name, NameKind.ScopeName, nesting);
    }

    /// <summary>
    /// Create a <see cref="NameInfo"/> for a scope field.
    /// </summary>
    public static NameInfo ScopeField(DName name, int nesting)
    {
        Validation.Assert(nesting >= 0);
        return new NameInfo(name, NameKind.ScopeField, nesting);
    }

    public override string ToString()
    {
        switch (Kind)
        {
        case NameKind.Function:
            return $"Function {Name}";
        case NameKind.Global:
            return $"Global {Name}";
        case NameKind.Field:
            return $"Field {Name}";
        case NameKind.ScopeName:
            return $"Scope<{Nesting}> {Name}";
        case NameKind.ScopeField:
            return $"Scope<{Nesting}> Field: {Name}";
        default:
            Validation.Assert(false);
            return "";
        }
    }
}

/// <summary>
/// Information about a (potentially nested) scope. The <see cref="TryGetScopeInfo"/> function
/// produces an array of these given a parse node.
/// </summary>
public struct NestedScopeInfo
{
    /// <summary>
    /// The type of the scope. If the type's root type is a record type, then field names
    /// of the type are in scope.
    /// </summary>
    public readonly DType Type;

    /// <summary>
    /// An optional alias for the scope item. If valid, the alias name is in scope.
    /// </summary>
    public readonly DName Alias;

    /// <summary>
    /// Whether the alias is implicit.
    /// </summary>
    public readonly bool AliasIsImplicit;

    internal NestedScopeInfo(DType type, DName alias = default(DName), bool aliasIsImplicit = false)
    {
        Validation.Assert(type.IsValid);
        Validation.Assert(!aliasIsImplicit || alias.IsValid);
        Type = type;
        Alias = alias;
        AliasIsImplicit = aliasIsImplicit;
    }

    /// <summary>
    /// Returns whether the given name is within scope as a variable.
    ///
    /// Note that this should match the binder name lookup rules. If we tweak
    /// the rules for looking up fields in nested record types, this will need
    /// to be updated as well.
    /// </summary>
    public bool ContainsName(DName name)
    {
        if (Alias == name)
            return true;

        if (Type.RootKind == DKind.Record && Type.TryGetNameType(name, out _))
            return true;

        return false;
    }
}

/// <summary>
/// This encapsulates a bound formula, with references to the rexl formula, bound tree,
/// errors, etc.
/// </summary>
public sealed partial class BoundFormula
{
    /// <summary>
    /// This maps from parse node to inferred info about it, including its type and scope-chain information.
    /// This is useful for intellisense features like suggestion generation. Note that some parse nodes, like ExprListNode
    /// and the record node in an augmenting projection node, won't have type information.
    /// </summary>
    private readonly NodeToInfo _nodeToInfo;

    /// <summary>
    /// This maps from call node to a RexlFunc. Useful for intellisense features
    /// like signature help generation.
    /// </summary>
    private readonly CallToOper _callNodeToOper;

    /// <summary>
    /// RexlOperation instances referenced by this formula. Useful for telemetry and smart intellisense.
    /// </summary>
    private readonly Immutable.Array<OperInfo> _opers;

    /// <summary>
    /// Gets the source formula.
    /// </summary>
    public RexlFormula Formula { get; }

    /// <summary>
    /// Gets the <see cref="BindHostInfo"/> provided by the <see cref="BindHost"/>. May be null.
    /// </summary>
    public BindHostInfo HostInfo { get; }

    /// <summary>
    /// Gets the bound tree.
    /// </summary>
    public BoundNode BoundTree { get; }

    /// <summary>
    /// Gets the unreduced and unoptimized bound tree.
    /// </summary>
    public BoundNode BoundTreeRaw { get; }

    /// <summary>
    /// Gets whether there are binding errors or warnings.
    /// </summary>
    public bool HasDiagnostics { get { return Diagnostics.Length > 0; } }

    /// <summary>
    /// Gets the (possibly empty) array of binding errors.
    /// </summary>
    public Immutable.Array<BaseDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Gets whether there are binding errors.
    /// </summary>
    public bool HasErrors { get { return Errors.Length > 0; } }

    /// <summary>
    /// Gets the (possibly empty) array of binding errors.
    /// </summary>
    public Immutable.Array<BaseDiagnostic> Errors { get; }

    /// <summary>
    /// Gets whether there are binding warnings.
    /// </summary>
    public bool HasWarnings { get { return Warnings.Length > 0; } }

    /// <summary>
    /// Gets the (possibly empty) array of binding warnings.
    /// </summary>
    public Immutable.Array<BaseDiagnostic> Warnings { get; }

    /// <summary>
    /// Gets the (possibly empty) array of binding diagnostics that include guesses/changes.
    /// These are sorted by min with tie-breaker lim.
    /// </summary>
    public Immutable.Array<RexlDiagnostic> Suggestions { get; }

    /// <summary>
    /// If there are any diagnostics with suggested/guessed changes, this is the result of applying those changes.
    /// Otherwise, this is null.
    /// </summary>
    public string CorrectedText { get; }

    /// <summary>
    /// Indicates if there are no binding and parsing errors.
    /// </summary>
    public bool IsGood { get { return !HasErrors && !Formula.HasErrors; } }

    private BoundFormula(
        RexlFormula fma, BindHostInfo hostInfo, BoundNode boundTree, BoundNode boundTreeRaw, Immutable.Array<BaseDiagnostic> diagnostics,
        NodeToInfo nodeToInfo, CallToOper callNodeToOper)
    {
        Validation.AssertValue(fma);
        Validation.AssertValueOrNull(hostInfo);
        Validation.AssertValue(boundTree);
        Validation.AssertValue(boundTreeRaw);
        Validation.Assert(boundTree.Type == boundTreeRaw.Type);
        Validation.Assert(!diagnostics.IsDefault);
        Validation.AssertValue(nodeToInfo);
        Validation.AssertValue(callNodeToOper);

        Formula = fma;
        HostInfo = hostInfo;
        BoundTree = boundTree;
        BoundTreeRaw = boundTreeRaw;
        Diagnostics = diagnostics;

        (Errors, Warnings, Suggestions) = RexlDiagnostic.Partition(diagnostics);

        if (Suggestions.Length > 0)
            CorrectedText = fma.ApplyChanges(Suggestions);

        _nodeToInfo = nodeToInfo;
        _callNodeToOper = callNodeToOper;
        _opers = callNodeToOper.GetValues()
            .Select(val => val.info).Where(info => info is not null)
            .Distinct().ToImmutableArray();
    }

    /// <summary>
    /// Bind the given formula and return the result.
    /// </summary>
    /// <param name="fma">The formula to bind</param>
    /// <param name="host">The binding host, that knows about the available globals, namespaces, and functions</param>
    /// <param name="options">The <see cref="BindOptions"/>, such as whether to optimize, whether procedures are allowed, etc.</param>
    /// <param name="typeThis">The type of the "this" value, if there is one, otherwise default</param>
    /// <param name="scopes">Additional outer scopes and optional names in which this formula should be bound</param>
    public static BoundFormula Create(
        RexlFormula fma, BindHost host, BindOptions options = default, DType typeThis = default,
        Immutable.Array<(ArgScope scope, string name)> scopes = default)
    {
        Validation.BugCheckValue(fma, nameof(fma));
        Validation.BugCheckValue(host, nameof(host));

        var bnd = Binder.Run(
            fma.ParseTree, host, options, typeThis, scopes,
            out var bndRaw, out var diagnostics, out var nodeToInfo, out var callNodeToOper);
        return new BoundFormula(fma, host.GetInfo(bnd), bnd, bndRaw, diagnostics, nodeToInfo, callNodeToOper);
    }

    /// <summary>
    /// Return a new <see cref="BoundFormula"/> that wraps the current <see cref="BoundTree"/> casted to the
    /// given type. If the current type is not accepted by the given <paramref name="typeDst"/>, then the
    /// resulting bound tree is a <see cref="BndErrorNode"/> and the diagnostics are augmented.
    /// </summary>
    public BoundFormula CastTo(DType typeDst, RexlDiagnostic err = null)
    {
        Validation.BugCheckParam(typeDst.IsValid, nameof(typeDst));

        var bndSrc = BoundTree;
        var typeSrc = bndSrc.Type;

        if (typeDst == typeSrc)
        {
            Validation.Assert(err == null);
            return this;
        }

        var diags = Diagnostics;
        BoundNode bndDst;
        BoundNode bndDstRaw;
        if (err == null && typeDst.Accepts(typeSrc, DType.UseUnionDefault))
        {
            bndDst = Conversion.CastBnd(NoopReducerHost.Instance, bndSrc, typeDst, DType.UseUnionDefault);
            bndDstRaw = Conversion.CastBnd(NoopReducerHost.Instance, BoundTreeRaw, typeDst, DType.UseUnionDefault);
        }
        else
        {
            err ??= RexlDiagnostic.Error(Formula.ParseTree, ErrorStrings.ErrBadType_Src_Dst, typeSrc, typeDst);
            bndDst = BndErrorNode.Create(err, typeDst);
            bndDstRaw = bndDst;
            diags = diags.Add(err);
        }

        // REVIEW: Should we worry about updating the node mapping, etc?
        return new BoundFormula(Formula, HostInfo, bndDst, bndDstRaw, diags, _nodeToInfo, _callNodeToOper);
    }

    /// <summary>
    /// Whether the node is used as an implicit name. This may happen when an identifier is an
    /// argument to a scoped function slot, or is used within record literal syntax without a variable
    /// declaration.
    ///
    /// Note that a node may form an implicit name without being used, in which case this will return false.
    /// In the formula Guard(N, N*N), the first N is used as an implicit name in the following expression,
    /// but in Guard(N, it*it) N is not despite forming one.
    /// </summary>
    public bool IsUsedImplicitName(RexlNode nameNode)
    {
        if (!_nodeToInfo.TryGetValue(nameNode, out var info))
            return false;

        Validation.AssertValue(info);
        Validation.Assert(!info.IsUsedImplicitName || nameNode is FirstNameNode);
        return info.IsUsedImplicitName;
    }

    /// <summary>
    /// Whether or not the node refers to a namespace. If so, sets <paramref name="ns"/> to
    /// the full path of the namespace.
    /// </summary>
    public bool TryGetNamespace(RexlNode nameNode, out NPath ns)
    {
        if (_nodeToInfo.TryGetValue(nameNode, out var info) && info.VerifyValue().BoundNode is BndNamespaceNode bnn)
        {
            ns = bnn.Path;
            return true;
        }

        ns = default;
        return false;
    }

    /// <summary>
    /// Adds any binding warnings to <paramref name="warnings"/> and returns whether there were
    /// any binding warnings.
    /// </summary>
    public bool GetWarnings(ref List<BaseDiagnostic> warnings)
    {
        if (Warnings.Length == 0)
            return false;

        if (warnings == null)
            warnings = new List<BaseDiagnostic>();
        warnings.AddRange(Warnings);
        return true;
    }

    /// <summary>
    /// Adds any binding errors to <paramref name="errors"/> and returns whether there were
    /// any binding errors.
    /// </summary>
    public bool GetErrors(ref List<BaseDiagnostic> errors)
    {
        if (Errors.Length == 0)
            return false;

        if (errors == null)
            errors = new List<BaseDiagnostic>();
        errors.AddRange(Errors);
        return true;
    }

    /// <summary>
    /// Adds any binding diagnostics to <paramref name="diagnostics"/> and returns whether there were
    /// any binding diagnostics.
    /// </summary>
    public bool GetDiagnostics(ref List<BaseDiagnostic> diagnostics)
    {
        if (Diagnostics.Length == 0)
            return false;

        if (diagnostics == null)
            diagnostics = new List<BaseDiagnostic>();
        diagnostics.AddRange(Diagnostics);
        return true;
    }

    /// <summary>
    /// Get field names of the given RexlNode, if any, adding them to the <paramref name="names"/> list.
    /// </summary>
    public void GetFieldNames(ref List<NameInfo> names, RexlNode node)
    {
        Validation.BugCheckValueOrNull(names);
        Validation.BugCheckValue(node, nameof(node));

        if (TryGetNodeType(node, out var typeLeft))
        {
            if (typeLeft.RootKind == DKind.Record)
            {
                foreach (var tn in typeLeft.GetNames())
                    Util.Add(ref names, NameInfo.Field(tn.Name));
            }
        }
    }

    /// <summary>
    /// Get scope items for the scope information associated with the given node, adding them to
    /// the <paramref name="names"/> list.
    /// </summary>
    public void GetScopeItemNames(ref List<NameInfo> names, RexlNode node)
    {
        Validation.BugCheckValueOrNull(names);
        Validation.BugCheckValue(node, nameof(node));

        if (TryGetNodeScopeInfo(node, out var scopes))
        {
            for (int iscope = 0; iscope < scopes.Length; iscope++)
            {
                var scope = scopes[iscope];
                if (scope.Alias.IsValid)
                    Util.Add(ref names, NameInfo.ScopeName(scope.Alias, iscope));
                foreach (var tn in scope.Type.GetNames())
                    Util.Add(ref names, NameInfo.ScopeField(tn.Name, iscope));
            }
        }
    }

    /// <summary>
    /// Retrieves the inferred DType of a parse node, i.e., RexlNode.
    /// Returns true if the node type is inferred by the binder.
    /// Returns false if the node type is not inferred (for example, ExprList),
    /// and in this case type is assigned to default DType.
    /// The input node should not be null.
    /// </summary>
    public bool TryGetNodeType(RexlNode node, out DType type)
    {
        Validation.BugCheckValue(node, nameof(node));
        bool res = _nodeToInfo.TryGetValue(node, out var info);
        type = info.Type;
        return res && type.IsValid;
    }

    /// <summary>
    /// Retrieves the <see cref="RexlOper"/> for the given <see cref="CallNode"/>.
    /// </summary>
    public bool TryGetOper(CallNode node, out RexlOper oper)
    {
        Validation.BugCheckValue(node, nameof(node));
        if (_callNodeToOper.TryGetValue(node, out var value))
        {
            oper = value.traits.Oper;
            return true;
        }
        oper = null;
        return false;
    }

    /// <summary>
    /// Retrieves the <see cref="OperInfo"/> for the given <see cref="CallNode"/>.
    /// </summary>
    public bool TryGetOperInfo(CallNode node, out OperInfo info)
    {
        Validation.BugCheckValue(node, nameof(node));
        if (_callNodeToOper.TryGetValue(node, out var value) && value.info is not null)
        {
            info = value.info;
            return true;
        }
        info = null;
        return false;
    }

    /// <summary>
    /// Retrieves the <see cref="OperInfo"/> and <see cref="ArgTraits"/> for the given
    /// <see cref="CallNode"/>.
    /// </summary>
    public bool TryGetOper(CallNode node, out OperInfo info, out ArgTraits traits)
    {
        Validation.BugCheckValue(node, nameof(node));
        if (_callNodeToOper.TryGetValue(node, out var value))
        {
            info = value.info;
            traits = value.traits;
            return true;
        }
        info = null;
        traits = null;
        return false;
    }

    /// <summary>
    /// Retrieves the nested scope information for the given parse node. If the parse node is not
    /// contained in any nested scopes, returns false and sets scopes to null. Otherwise, scopes
    /// is an array containing information for the scopes containing the parse node, in inner to outer
    /// order, so the 0th slot is the inner-most scope.
    /// </summary>
    public bool TryGetNodeScopeInfo(RexlNode node, out NestedScopeInfo[] scopes)
    {
        Validation.BugCheckValue(node, nameof(node));

        if (!_nodeToInfo.TryGetValue(node, out var info) || info.ScopeInfo == null)
        {
            scopes = null;
            return false;
        }

        var scope = info.ScopeInfo;
        scopes = new NestedScopeInfo[scope.Depth];
        for (int i = 0; i < scopes.Length; i++)
        {
            Validation.Assert(scope != null);
            scopes[i] = new NestedScopeInfo(scope.Type, scope.Alias, scope.AliasIsImplicit);
            scope = scope.Outer;
        }
        Validation.Assert(scope == null);

        return true;
    }

    /// <summary>
    /// Retrieves all <see cref="OperInfo"/> instances referenced by this formula.
    /// Note that this will not include <c>null</c>, even when some invocations were un-resolved.
    /// </summary>
    public Immutable.Array<OperInfo> GetAllOperations()
    {
        return _opers;
    }

    /// <summary>
    /// Returns enumeration of CallNodes together with the CallNode's oper info.
    /// The oper info will be <c>null</c> if the call node wasn't resolved to an operation.
    /// </summary>
    public IEnumerable<KeyValuePair<CallNode, OperInfo>> GetAllCallNodes()
    {
        foreach (var kvp in _callNodeToOper.GetPairs())
            yield return new KeyValuePair<CallNode, OperInfo>(kvp.key, kvp.val.info);
    }
}
