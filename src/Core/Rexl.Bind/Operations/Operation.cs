// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

using Conditional = System.Diagnostics.ConditionalAttribute;
using DirTuple = Immutable.Array<Directive>;
using NameTuple = Immutable.Array<DName>;
using TypeTuple = Immutable.Array<DType>;

/// <summary>
/// Base class for functions and procedures, which are named entities that take arguments.
/// </summary>
public abstract partial class RexlOper
{
    /// <summary>
    /// Whether this is a function, as opposed to procedure.
    /// </summary>
    public bool IsFunc { get; }

    /// <summary>
    /// Whether this is a procedure, as opposed to function. A procedure differs from a function in that:
    /// <list type="bullet">
    /// <item>Execution is not "pure". That is, it may cause side effects, may not be deterministic,
    ///   may depend on state outside of the arguments.</item>
    /// <item>There is not a single "result" value. There may be many or none.</item>
    /// <item>The number and types of any results may not be known before execution.</item>
    /// <item>The result of "running" an invocation of a procedure is not a rexl value, but is
    ///   an <see cref="Code.ActionRunner"/>. The action runner provides control over the execution,
    ///   monitoring of progress, and the number, types and values of the results.</item>
    /// </list>
    /// </summary>
    public bool IsProc => !IsFunc;

    /// <summary>
    /// If this operation has a natural "parent" operation, this returns it. Otherwise,
    /// returns <c>null</c>. An example is when an invocation of a "public" operation reduces
    /// to an invocation of a less visible "implementation" operation.
    /// </summary>
    public virtual RexlOper Parent => null;

    /// <summary>
    /// The name of the operation.
    /// </summary>
    public DName Name { get; }

    /// <summary>
    /// The namespace of the operation.
    /// </summary>
    public NPath Namespace { get; }

    /// <summary>
    /// The namespace and name combined into a single path.
    /// </summary>
    public NPath Path { get; }

    /// <summary>
    /// The version of the function. Should be changed when the function is changed.
    /// This can be null, indicating an initial version.
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// The minimum number of arguments accepted.
    /// </summary>
    public int ArityMin { get; }

    /// <summary>
    /// The maximum number of arguments accepted.
    /// </summary>
    public int ArityMax { get; }

    /// <summary>
    /// Whether this function supports implicit names coming from dotted names.
    /// </summary>
    public virtual bool SupportsImplicitDotted => false;

    /// <summary>
    /// This returns non-null if any <see cref="BndCallNode"/> for this operation should use
    /// a different operation instead.
    /// </summary>
    public virtual RexlOper ReduceTo => null;

    /// <summary>
    /// The main constructor. All others invoke this one.
    /// </summary>
    protected RexlOper(bool isFunc, DName name, NPath ns, bool union, int arityMin, int arityMax, string version)
    {
        Validation.BugCheckParam(name.IsValid, nameof(name));
        Validation.BugCheckValueOrNull(version);
        Validation.BugCheck(0 <= arityMin & arityMin <= arityMax);

        IsFunc = isFunc;
        Name = name;
        Namespace = ns;
        Path = Namespace.Append(name);
        Version = version;

        ArityMin = arityMin;
        ArityMax = arityMax;
        AcceptUseUnion = union;
    }

    /// <summary>
    /// Ctor with no version.
    /// </summary>
    protected RexlOper(bool isFunc, DName name, NPath ns, int arityMin, int arityMax)
        : this(isFunc, name, ns, DType.UseUnionFunc, arityMin, arityMax, null)
    {
    }

    /// <summary>
    /// Ctor with no namespace.
    /// </summary>
    protected RexlOper(bool isFunc, DName name, bool union, int arityMin, int arityMax)
        : this(isFunc, name, NPath.Root, union, arityMin, arityMax, null)
    {
    }

    /// <summary>
    /// Ctor with no namespace or union flag.
    /// </summary>
    protected RexlOper(bool isFunc, DName name, int arityMin, int arityMax)
        : this(isFunc, name, NPath.Root, DType.UseUnionFunc, arityMin, arityMax, null)
    {
    }

    /// <summary>
    /// Whether this operation supports the given arity.
    /// </summary>
    public virtual bool SupportsArity(int arity)
    {
        return ArityMin <= arity && arity <= ArityMax;
    }

    /// <summary>
    /// Whether this operation supports the given arity. If so, sets arityUse to arity. If not, sets
    /// arityUse to a suggested arity for (binder) error recovery.
    /// </summary>
    public virtual bool SupportsArity(int arity, out int arityUse)
    {
        if (SupportsArity(arity))
        {
            arityUse = arity;
            return true;
        }

        if (arity <= ArityMin)
            arity = ArityMin;
        else if (arity >= ArityMax)
            arity = ArityMax;
        else
        {
            // Look for a good arity between arity and ArityMax.
            while (++arity < ArityMax && !SupportsArity(arity))
            {
            }
        }
        Validation.Assert(ArityMin <= arity & arity <= ArityMax);

        // If this assert goes off, the operation's SupportArity(int) function isn't consistent with
        // ArityMin or ArityMax.
        Validation.Assert(SupportsArity(arity));

        arityUse = arity;
        return false;
    }

    /// <summary>
    /// Return the ArgTraits for the given number of args, names, and directives.
    /// <paramref name="implicitNames"/> signifies which names are "implicit", i.e. come from
    /// a single identifier or dotted name.
    /// Note that <paramref name="names"/> and <paramref name="dirs"/> come from the parse
    /// nodes only and may not match what is kept in the final bound node, since the ArgTraits
    /// may affect which names and directives are valid.
    /// </summary>
    public ArgTraits GetArgTraits(int carg, NameTuple names, BitSet implicitNames, DirTuple dirs)
    {
        Validation.BugCheckParam(SupportsArity(carg), nameof(carg));
        Validation.BugCheckParam(names.IsDefault || names.Length == carg, nameof(carg));
        Validation.BugCheckParam(names.IsDefault && implicitNames.IsEmpty || implicitNames.SlotMax < names.Length, nameof(implicitNames));
        Validation.BugCheckParam(dirs.IsDefault || dirs.Length == carg, nameof(carg));

        var traits = GetArgTraitsCore(carg, names, implicitNames, dirs);
        AssertValidArgTraits(traits);
        return traits;
    }

    protected virtual ArgTraits GetArgTraitsCore(int carg, NameTuple names, BitSet implicitNames, DirTuple dirs)
    {
        Validation.Assert(SupportsArity(carg));
        Validation.Assert(names.IsDefault || names.Length == carg);
        Validation.Assert(names.IsDefault && implicitNames.IsEmpty || implicitNames.SlotMax < names.Length);
        Validation.Assert(dirs.IsDefault || dirs.Length == carg);
        return GetArgTraitsCore(carg);
    }

    protected abstract ArgTraits GetArgTraitsCore(int carg);

    /// <summary>
    /// <see cref="ArgTraits"/> has invariants that are hard to test internally to that class,
    /// so we test them here. For example, due to the separation of <see cref="ArgTraits.GetScopeKind(int)"/>
    /// and <see cref="ArgTraits.IsScope(int, out int, out int, out bool)"/>, the traits cannot
    /// easily verify that the collective invariants of scopes are valid at construction time.
    /// </summary>
    [Conditional("DEBUG")]
    private static void AssertValidArgTraits(ArgTraits traits)
    {
#if DEBUG
        Validation.AssertValue(traits);
        Validation.BugCheck(traits.AreEquivalent(traits), nameof(ArgTraits.AreEquivalent) + " failed reflexivity");

        int cscope = 0;
        int cscopePushed = 0;
        int cnested = 0;
        int cindex = 0;

        for (int slot = 0; slot < traits.SlotCount; slot++)
        {
            var scopeKind = traits.GetScopeKind(slot);
            bool isScope = traits.IsScope(slot, out int iscope, out int iidx, out bool firstForIdx);
            if (!(isScope ^ scopeKind == ScopeKind.None))
            {
                throw Validation.BugExcept("{0} returned {1} but {2} returned {3}",
                    nameof(ArgTraits.IsScope), isScope, nameof(ArgTraits.GetScopeKind), scopeKind);
            }

            if (isScope)
            {
                cscope++;
                cscopePushed++;
                if (iidx >= 0)
                {
                    if (!(scopeKind == ScopeKind.SeqItem || scopeKind == ScopeKind.Range))
                    {
                        throw Validation.BugExcept("{0} gave a non-negative iidx, but the scope kind {1} is not indexable",
                            nameof(ArgTraits.IsScope), scopeKind);
                    }

                    // If this assert goes off, ensure the binder's index scope logic has been updated.
                    // Any subsequent index scopes with this iidx should be mapped to the Range scope.
                    // Once added, an additional check can be added to ensure that any Range scopes with
                    // a non-negative iidx share their iidx with some other indexable scope.
                    Validation.Assert(scopeKind != ScopeKind.Range);

                    bool isNewIidx = iidx == cindex;
                    if (!(isNewIidx || iidx == cindex - 1))
                    {
                        throw Validation.BugExcept("{0}iidx of {1} given for slot {2}; valid iidxes must only monotonically increase contiguously",
                            isNewIidx ? "new " : "", iidx, slot);
                    }
                    if (isNewIidx != firstForIdx)
                    {
                        throw Validation.BugExcept("{0}iidx of {1} given for slot {2}; firstForIdx should be {3} but was {4}",
                            isNewIidx ? "new " : "", iidx, slot, isNewIidx, firstForIdx);
                    }

                    if (isNewIidx)
                        cindex++;
                }
            }
            else
            {
                if (iscope >= 0)
                {
                    throw Validation.BugExcept("{0} returned false for slot {1}; iscope should be negative but was {2}",
                        nameof(ArgTraits.IsScope), slot, iscope);
                }
                if (iidx >= 0)
                {
                    throw Validation.BugExcept("{0} returned false for slot {1}; iidx should be negative but was {2}",
                        nameof(ArgTraits.IsScope), slot, iidx);
                }
                if (firstForIdx)
                {
                    throw Validation.BugExcept("{0} returned false for slot {1}; firstForIdx should be false but was true",
                        nameof(ArgTraits.IsScope), slot);
                }
            }

            bool isNested = traits.IsNested(slot);
            if (isNested)
            {
                cnested++;
                bool hasActiveScope = false;
                for (int upCount = 0; upCount < cscopePushed; upCount++)
                {
                    if (traits.IsScopeActive(slot, upCount))
                    {
                        hasActiveScope = true;
                        break;
                    }
                }

                if (!hasActiveScope)
                {
                    throw Validation.BugExcept("{0} returned true for slot {1} but no active scope was found",
                        nameof(ArgTraits.IsNested), slot);
                }

                for (int upCount = cscopePushed; upCount < cscope; upCount++)
                {
                    if (traits.IsScopeActive(slot, upCount))
                    {
                        throw Validation.BugExcept("{0} returned true for slot {1} and upCount {2}, but upCount refers to an unpushed scope",
                            nameof(ArgTraits.IsScopeActive), slot, upCount);
                    }
                }
            }

            if (traits.IsNestedTail(slot))
            {
                cscopePushed = 0;
                if (!isNested)
                {
                    throw Validation.BugExcept("{0} returned true for slot {1}; {2} should be true but was false",
                        nameof(ArgTraits.IsNestedTail), slot, nameof(ArgTraits.IsNested));
                }
            }

            Validation.BugCheck(traits.IsScope(slot) == isScope, "Inconsistent overloading of " + nameof(ArgTraits.IsScope));
            Validation.BugCheck(traits.IsScope(slot, out int iscope2) == isScope, "Inconsistent overloading of " + nameof(ArgTraits.IsScope));
            Validation.BugCheck(iscope2 == iscope, "Inconsistent overloading of " + nameof(ArgTraits.IsScope));
        }

        if (cscope != traits.ScopeCount)
        {
            throw Validation.BugExcept("{0} mismatch; traits specify {1} but found {2}",
                    nameof(ArgTraits.ScopeCount), traits.ScopeCount, cscope);
        }
        if (cindex != traits.ScopeIndexCount)
        {
            throw Validation.BugExcept("{0} mismatch; traits specify {1} but found {2}",
                nameof(ArgTraits.ScopeIndexCount), traits.ScopeIndexCount, cindex);
        }
        if (cnested != traits.NestedCount)
        {
            throw Validation.BugExcept("{0} mismatch; traits specify {1} but found {2}",
                nameof(ArgTraits.NestedCount), traits.NestedCount, cnested);
        }
#endif
    }

    /// <summary>
    /// Whether the function supports the given directive in the indicated slot.
    /// </summary>
    public bool SupportsDirective(ArgTraits traits, int slot, Directive dir)
    {
        Validation.BugCheckValue(traits, nameof(traits));
        Validation.BugCheckParam(traits.Oper == this, nameof(traits));
        Validation.BugCheckIndex(slot, traits.SlotCount, nameof(slot));
        Validation.BugCheckParam(dir != Directive.None, nameof(dir));
        return SupportsDirectiveCore(traits, slot, dir);
    }

    /// <summary>
    /// Whether the function supports a directive in the indicated slot.
    /// </summary>
    protected virtual bool SupportsDirectiveCore(ArgTraits traits, int slot, Directive dir)
    {
        Validation.AssertValue(traits);
        Validation.Assert(traits.Oper == this);
        Validation.AssertIndex(slot, traits.SlotCount);
        Validation.Assert(dir != Directive.None);
        return false;
    }

    /// <summary>
    /// Whether, by default, argument conversion should use union/superset acceptance, not
    /// intersection/subset acceptance. Note that the bool output from
    /// <see cref="SpecializeTypes(InvocationInfo, out ArgTraits)"/>
    /// may override this.
    /// </summary>
    public bool AcceptUseUnion { get; }

    /// <summary>
    /// Called by the binder to allow functions to specify a better type for a scope arg. Scope args are handled specially
    /// since the types provided by <see cref="SpecializeTypes(InvocationInfo, out ArgTraits)"/>
    /// generally can't be applied by binding, since nested args have already been bound with the original type.
    /// </summary>
    internal DType GetScopeArgType(ArgTraits traits, int slot, DType type)
    {
        Validation.AssertValue(traits);
        Validation.Assert(SupportsArity(traits.SlotCount));
        Validation.AssertIndex(slot, traits.SlotCount);
        Validation.Assert(traits.IsScope(slot));
        Validation.Assert(type.IsValid);

        return GetScopeArgTypeCore(traits, slot, type);
    }

    /// <summary>
    /// Called by the binder to allow functions to specify a better type for a scope arg. Scope args
    /// are handled specially since the types provided by
    /// <see cref="SpecializeTypes(InvocationInfo, out ArgTraits)"/> generally
    /// can't be applied by binding, since nested args have already been bound with the original type.
    /// </summary>
    protected virtual DType GetScopeArgTypeCore(ArgTraits traits, int iarg, DType type)
    {
        Validation.AssertValue(traits);
        Validation.Assert(SupportsArity(traits.SlotCount));
        Validation.AssertIndex(iarg, traits.SlotCount);
        Validation.Assert(traits.IsScope(iarg));
        Validation.Assert(type.IsValid);

        return type;
    }

    /// <summary>
    /// Given the invocation information, return the return type, argument types, and acceptance variant to use.
    /// If <paramref name="traitsChange"/> is non-null, then it indicates a new <see cref="ArgTraits"/> that
    /// it requests the binder to use in another binding loop. The binder is not guaranteed to use it, e.g.
    /// if it hits the limit on the number of rebinds or is called in a non-rebinding context.
    /// </summary>
    internal (DType typeRes, TypeTuple typesArg, bool union) SpecializeTypes(
        InvocationInfo info, out ArgTraits traitsChange)
    {
        // This does validation on the info, then calls SpecializeTypesCore.
        Validation.BugCheckValue(info, nameof(info));
        Validation.BugCheckParam(info.Oper == this, nameof(info));
        Validation.BugCheckParam(SupportsArity(info.Arity), nameof(info));
        Validation.Assert(!info.Args.IsDefault);
        Validation.Assert(info.Args.Length == info.Arity);
        Validation.Assert(!info.Scopes.IsDefault);

        var res = SpecializeTypesCore(info, out bool union, out traitsChange);
        return (res.typeRes, res.typesArg, union);
    }

    /// <summary>
    /// Override this form to specify non-default acceptance variant or modified arg traits.
    /// </summary>
    protected virtual (DType typeRes, TypeTuple typesArg) SpecializeTypesCore(
        InvocationInfo info, out bool union, out ArgTraits traitsChange)
    {
        Validation.AssertValue(info);
        traitsChange = null;
        union = AcceptUseUnion;
        return SpecializeTypesCore(info);
    }

    /// <summary>
    /// Override this form to use standard acceptance and arg traits.
    /// </summary>
    protected virtual (DType typeRes, TypeTuple typesArg) SpecializeTypesCore(InvocationInfo info)
    {
        // The operation isn't fully implemented if we get here.
        Validation.Assert(false);

        Validation.Assert(SupportsArity(info.Arity));
        return (DType.Vac, TypeTuple.Fill(DType.General, info.Arity));
    }

    /// <summary>
    /// If the type in the indicated slot is not a sequence type, makes it one.
    /// </summary>
    protected static void EnsureTypeSeq(TypeTuple.Builder types, int slot)
    {
        Validation.BugCheckValue(types, nameof(types));
        Validation.BugCheckIndex(slot, types.Count, nameof(slot));

        if (types[slot].SeqCount == 0)
            types[slot] = types[slot].ToSequence();
    }

    /// <summary>
    /// If the type in the indicated slot is not a sequence type, makes it one.
    /// </summary>
    public static void EnsureTypeSeq(ref DType type)
    {
        Validation.BugCheckParam(type.IsValid, nameof(type));

        if (type.SeqCount == 0)
            type = type.ToSequence();
    }

    /// <summary>
    /// If the type in the indicated slot is not a req tensor type, makes it one.
    /// </summary>
    protected static void EnsureTypeTen(TypeTuple.Builder types, int slot)
    {
        Validation.BugCheckValue(types, nameof(types));
        Validation.BugCheckIndex(slot, types.Count, nameof(slot));

        if (!types[slot].IsTensorReq)
            types[slot] = types[slot].ToTensor(opt: false, 0);
    }

    /// <summary>
    /// If the type is not a req tensor type, makes it one.
    /// </summary>
    protected static void EnsureTypeTen(ref DType type)
    {
        Validation.BugCheckParam(type.IsValid, nameof(type));

        if (!type.IsTensorReq)
            type = type.ToTensor(opt: false, 0);
    }

    /// <summary>
    /// If the type in the indicated slot is not a tensor type, makes it one.
    /// </summary>
    protected static void EnsureTypeTenXxx(TypeTuple.Builder types, int slot)
    {
        Validation.BugCheckValue(types, nameof(types));
        Validation.BugCheckIndex(slot, types.Count, nameof(slot));

        if (!types[slot].IsTensorXxx)
            types[slot] = types[slot].ToTensor(opt: false, 0);
    }

    /// <summary>
    /// If the type is not a tensor type, makes it one.
    /// </summary>
    protected static void EnsureTypeTenXxx(ref DType type)
    {
        Validation.BugCheckParam(type.IsValid, nameof(type));

        if (!type.IsTensorXxx)
            type = type.ToTensor(opt: false, 0);
    }

    /// <summary>
    /// If the type in the indicated slot is not text or a uri type, makes it one.
    /// </summary>
    protected static void EnsureTextOrUri(TypeTuple.Builder types, int slot)
    {
        Validation.BugCheckValue(types, nameof(types));
        Validation.BugCheckIndex(slot, types.Count, nameof(slot));
        var type = types[slot];
        if (type == DType.Text)
            return;
        if (!type.IsUri)
            types[slot] = DType.UriGen;
    }

    /// <summary>
    /// If the type is not text or a uri type, makes it one.
    /// </summary>
    protected static void EnsureTextOrUri(ref DType type)
    {
        Validation.BugCheckParam(type.IsValid, nameof(type));
        Validation.BugCheckParam(type.IsValid, nameof(type));

        if (type == DType.Text)
            return;
        if (!type.IsUri)
            type = DType.UriGen;
    }

    /// <summary>
    /// If the type in the indicated slot is not text or a uri type with the given flavor,
    /// makes it one.
    /// </summary>
    protected static void EnsureTextOrUri(TypeTuple.Builder types, int slot, DType typeUriBase)
    {
        Validation.BugCheckValue(types, nameof(types));
        Validation.BugCheckIndex(slot, types.Count, nameof(slot));
        Validation.BugCheckParam(typeUriBase.IsUri, nameof(typeUriBase));

        var type = types[slot];
        if (type == DType.Text)
            return;
        if (!type.IsUri || !typeUriBase.Accepts(type, union: true))
            types[slot] = typeUriBase;
    }

    /// <summary>
    /// If the type is not text or a uri type with the given flavor, makes it one.
    /// </summary>
    protected static void EnsureTextOrUri(ref DType type, DType typeUriBase)
    {
        Validation.BugCheckParam(type.IsValid, nameof(type));
        Validation.BugCheckParam(typeUriBase.IsUri, nameof(typeUriBase));

        if (type == DType.Text)
            return;
        if (!type.IsUri || !typeUriBase.Accepts(type, union: true))
            type = typeUriBase;
    }

    /// <summary>
    /// Returns true if <paramref name="type"/> is text or a uri type.
    /// </summary>
    protected static bool CertifyTextOrUri(DType type)
    {
        if (type == DType.Text)
            return true;
        if (!type.IsUri)
            return false;
        return true;
    }

    /// <summary>
    /// Returns true if <paramref name="type"/> is text or a uri type accepted by
    /// <paramref name="typeUriBase"/>.
    /// </summary>
    protected static bool CertifyTextOrUri(DType type, DType typeUriBase)
    {
        if (type == DType.Text)
            return true;
        if (!type.IsUri)
            return false;
        if (!typeUriBase.Accepts(type, true))
            return false;
        return true;
    }

    /// <summary>
    /// Called only by the <see cref="BndCallNode"/> constructor! Returns <c>true</c> if <paramref name="call"/>
    /// is a valid invocation of this function. If it is valid invocation for binding but not for code gen,
    /// this sets <paramref name="full"/> to <c>false</c>. These results are recorded in the
    /// <see cref="BndCallNode.Certified"/> and <see cref="BndCallNode.CertifiedFull"/> properties of the
    /// <see cref="BndCallNode"/>. If this returns false, the constructor throws so in theory no "active"
    /// <see cref="BndCallNode"/> instance should have <see cref="BndCallNode.Certified"/> be false.
    /// Of course, a misbehaving implementation of <see cref="CertifyCore(BndCallNode, ref bool)"/> could
    /// squirrel away the call and try to do something with it. The <see cref="IsValidCall(BndCallNode, bool)"/>
    /// function would return false for such instances, so it should be faithfully called at appropriate "gates".
    /// </summary>
    internal bool Certify(BndCallNode call, out bool full)
    {
        // This should only be called by the BndCallNode ctor!
        Validation.AssertValue(call);
        Validation.Assert(!call.Certified);

        full = false;
        if (call.Oper != this)
        {
            // Should never get here.
            Validation.Assert(false);
            return false;
        }

        int arity = call.Args.Length;
        var traits = call.Traits;

        // These should be guaranteed by BndCallNode construction.
        Validation.Assert(SupportsArity(arity));
        Validation.AssertValue(traits);
        Validation.Assert(traits.Oper == this);
        Validation.Assert(arity == traits.SlotCount);
        Validation.Assert(call.Scopes.Length == traits.ScopeCount);
        Validation.Assert(call.Indices.Length == traits.ScopeIndexCount);

        // Validate names. Names should only exist on args that accept them and must exist
        // on args that require them. The actual values of the names are not considered.
        var names = call.Names;
        if (!names.IsDefault)
        {
            Validation.Assert(names.Length == arity);
            for (int i = 0; i < arity; i++)
            {
                var name = names[i];
                if (name.IsValid)
                {
                    if (!traits.SupportsName(i))
                        return false;
                    // Shouldn't include a name for a scope when it isn't required.
                    if (!traits.RequiresName(i) && traits.IsScope(i))
                        return false;
                }
                else if (traits.RequiresName(i))
                    return false;
            }
        }
        else
        {
            // No names provided, ensure that no args require a name.
            for (int i = 0; i < arity; i++)
            {
                if (traits.RequiresName(i))
                    return false;
            }
        }

        // Validate directives.
        var dirs = call.Directives;
        if (!dirs.IsDefault)
        {
            Validation.Assert(dirs.Length == arity);
            for (int i = 0; i < arity; i++)
            {
                var dir = dirs[i];
                if (dir != Directive.None && !SupportsDirective(traits, i, dir))
                    return false;
            }
        }

        // BndCallNode create method should use ReduceTo if it is present.
        if (ReduceTo != null && ReduceTo != this)
            return false;

        // Full defaults to true.
        full = true;
        if (CertifyCore(call, ref full))
            return true;

        // Set full to false and return false.
        full = false;
        return false;
    }

    /// <summary>
    /// Sub-classes must implement this. The public <see cref="Certify(BndCallNode, out bool)"/>
    /// method calls this after performing standard validation. This can assume that the standard
    /// validation has been done. The standard validation includes:
    /// <list type="bullet">
    /// <item>Func: <c>call.Func == this</c>.</item>
    /// <item>Traits: correct arity, scope count, etc.</item>
    /// <item>Names: args that can't have a name don't and args that require a name have one.</item>
    /// <item>Directives: any directives are "approved" by <see cref="SupportsDirective(ArgTraits, int, Directive)"/>.</item>
    /// </list>
    /// This may also assume invariants guaranteed by <see cref="BndCallNode"/>, such as:
    /// <list type="bullet">
    /// <item>The number of scopes and indices matches what the traits say.</item>
    /// <item>The scope kinds are correct (although a "guard" may be replaced by a "with" scope).</item>
    /// <item>The arguments corresponding to the scopes have compatible types.</item>
    /// </list>
    /// </summary>
    protected abstract bool CertifyCore(BndCallNode call, ref bool full);

    /// <summary>
    /// Called to allow the operation to rewrite/simplify the invocation. This enables optimization
    /// such as constant folding, but can be used for other "rewrites" (that are needed for other
    /// reasons).
    /// 
    /// If this is a procedure, the result will also be a call of a procedure.
    /// REVIEW: Make a DType for "task" so this can be relaxed a bit to the result type not changing.
    /// </summary>
    public BoundNode Reduce(IReducer reducer, BndCallNode call)
    {
        Validation.BugCheckValue(reducer, nameof(reducer));
        Validation.BugCheckParam(IsValidCall(call), nameof(call));

        var ret = ReduceCore(reducer, call);
        Validation.BugCheck(ret.Type == call.Type);

        if (!IsProc)
            return ret;

        var callNew = ret as BndCallNode;
        Validation.BugCheck(callNew is not null);
        Validation.BugCheck(callNew.Oper.IsProc);
        return callNew;
    }

    protected virtual BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        return call;
    }

    /// <summary>
    /// Return whether the <see cref="BndCallNode"/> is a proper invocation of this operation.
    /// If <paramref name="full"/> is false, permits conditions that may be produced by binding
    /// but are not valid for code generation. For example, if code gen require additional
    /// invariants or reduction, the <see cref="BndCallNode.CertifiedFull"/> property should
    /// be <c>false</c>.
    /// </summary>
    public bool IsValidCall(BndCallNode call, bool full = false)
    {
        if (call == null)
            return false;
        if (call.Oper != this)
            return false;
        if (full && call.HasErrors)
            return false;
        if (full ? !call.CertifiedFull : !call.Certified)
            return false;
        return true;
    }

    /// <summary>
    /// Returns <c>true</c> if any slots of the call are volatile and not allowed to be.
    /// If so, sets <paramref name="slot"/> to the first such slot.
    /// </summary>
    public virtual bool HasBadVolatile(BndCallNode call, out int slot)
    {
        Validation.Assert(IsValidCall(call));
        slot = -1;
        return false;
    }

    /// <summary>
    /// Returns whether evaluation of the invocation may be "unbounded" in the sense that it
    /// may process an arbitrary number of items before "yielding" an item. For example,
    /// <c>Sum</c>, <c>Sort</c>, <c>TakeIf</c> should return <c>true</c>, while <c>ForEach</c>
    /// with no predicate or with a "while" predicate should return <c>false</c>.
    /// REVIEW: Better name? Also move this into the "bind" portion when it is fixed
    /// to be used by NeedsExecCtx.
    /// </summary>
    public bool IsUnbounded(BndCallNode call)
    {
        Validation.BugCheckParam(IsValidCall(call), nameof(call));
        return IsUnboundedCore(call);
    }

    protected virtual bool IsUnboundedCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));
        return false;
    }
}
