// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;

// REVIEW: Need to figure out Opt and auto-lifting over Opt, etc.
namespace Microsoft.Rexl;

using ArgTuple = Immutable.Array<BoundNode>;
using DirTuple = Immutable.Array<Directive>;
using NameTuple = Immutable.Array<DName>;
using ScopeTuple = Immutable.Array<ArgScope>;
using TypeTuple = Immutable.Array<DType>;

/// <summary>
/// Base class supporting multiple invocation forms. This includes a set of "standard" forms/patterns but also allows
/// additional custom patterns (specified by the sub-class). The conceptual model is that this wraps some underlying
/// "core" function that takes a single input and produces a single output. When there are naturally multiple inputs,
/// those inputs are bundled into a single input record.
/// 
/// Each form rewrites itself to match an execution function form, which is used for code gen. In the descriptions below,
/// F means this function and G or H means the execution function, aka rewrite function.
/// 
/// Several forms support the notion of a "main input sequence", known as "mis". The mis can either be the entire input type
/// or, when the input type is a record type, a field of the input record. In this discussion, we'll assume the field
/// name is "mis". When there is a mis, whether as the entire input type or as a field of the record input type, the first
/// arg is a sequence, known as the "source sequence", notated src. The src has a "map" scope and is followed by one or more
/// additional "selector" args, which are nested in the map scope. The selectors are used to construct the mis from the src via
/// mapping.
/// 
/// REVIEW: generalize the notion of merging.
/// Several forms also support "item merging", where there is a mis and also a main output sequence, aka "mos". Items
/// of the mis and mos are in one-to-one correspondence. Hence items of the src are also in one-to-one correspondence with
/// items of the mos. In this case, each item of src is "merged" with the corresponding mos item to produce the final
/// "destination" item. When there is merging, we use H to represent the execution function and it receives both the
/// normal "input" as well as src. When there is no merging, we use G to represent the execution function which receives
/// just the "input".
/// 
/// The most common form of merging (and the only currently implemented) applies when src is a sequence of record and the
/// mos is also a sequence of record, and corresponding records are "unioned", with field renaming when there are collisions.
/// 
/// The H forms involve creating a "With" scope in the first argument to avoid repeating computation of src while keeping H at the top level.
/// The output value of H is either the merged sequence or a record containing that merge. In the former case, a null input produces a null output
/// while in the latter a null input results in a null merged sequence but the result record is not null. In the latter case, the "core"
/// function (H) is still invoked.
/// 
/// If the op is a function, then as an optimization the H form will insert a Guard on the src if needed.
/// This is not possible if op is a procedure because reduction requires another proc at the top level.
/// 
/// REVIEW: The code generator does not allow the H forms to see the first slot due to
/// their With scope, so it needs to be repeated again as an argument in the merge cases.
/// 
/// 1a) F(arg) => G(arg'): one argument, nothing fancy going on. Although arg is checked against the expected input
///     type, it's not converted to it until reduction time. The ' refers to converting arg to the correct DType,
///     since we can't do that at SpecializeTypes time for the merge case.
/// 1b) F(arg) => H(x:arg, x', x): The merge version. Same note for x.
/// 
/// 2) F([n1:] a1, [n2:] a2, ...) => G({n1: a1, n2: a2, ...}): multiple args that are optionally named and are the
///    fields of a record type. When a name is omitted, the arg is positional. When a field has a default value, it
///    may be omitted.
/// 
/// 3a) F(src, sel) => G(Map(src, sel)): Inject Map.
/// 3b) F(src, sel) =>               H(x:src, Map(x, sel), x): The merge version.
/// 3c) F(src, sel) => Guard(y: src, H(x:y  , Map(x, sel), x): The merge version with Guard.
/// 
/// 4a) F(src, [m1:] s1, [m2:] s2, ...) => G(Map(src, {m1: s1, m2: s2, ...})): Inject Map and record construction.
/// 4b) F(src, [m1:] s1, [m2:] s2, ...) =>           H(x:src, Map(x, {m1: s1, m2: s2, ...}), x): The merge version.
/// 4c) F(src, [m1:] s1, [m2:] s2, ...) => Guard(y:x, H(x:src, Map(x, {m1: s1, m2: s2, ...}), x): The merge version with Guard.
/// 
/// 5a) F(src, sel, [n1:] a1, [n2:] a2, ...) => G({ mis: Map(src, sel), n1: a1, n2: a2, ... }): Inject Map and record.
/// 5b) F(src, sel, [n1:] a1, [n2:] a2, ...) =>            H(x:src, { mis: Map(x, sel), n1: a1, n2: a2, ... }, x): The merge version.
/// 5c) F(src, sel, [n1:] a1, [n2:] a2, ...) => Guard(y:x, H(x:src, { mis: Map(x, sel), n1: a1, n2: a2, ... }, x): The merge version with Guard.
/// 
/// 6a) F(src, [m1:] s1, [m2:] s2, ..., [n1:] a1, [n2:] a2, ...) => G({ mis: Map(src, { m1: s1, m2: s2, ... }), n1: a1, n2: a2, ... }):
///     Inject Map and record.
/// 6b) F(src, [m1:] s1, [m2:] s2, ..., [n1:] a1, [n2:] a2, ...) =>            H(x:src, { mis: Map(x, { m1: s1, m2: s2, ... }), n1: a1, n2: a2, ... }, x):
///     The merge version.
/// 6c) F(src, [m1:] s1, [m2:] s2, ..., [n1:] a1, [n2:] a2, ...) => Guard(y:x, H(x:src, { mis: Map(x, { m1: s1, m2: s2, ... }), n1: a1, n2: a2, ... }, x):
///     The merge version with Guard.
/// 
/// REVIEW: Figure out how best to support opt item types.
/// </summary>
public abstract partial class MultiFormOper<TCookie> : RexlOper
{
    protected readonly Immutable.Array<InvocationForm> _forms;
    private readonly BitSet _maskArities;

    /// <summary>
    /// Cache of execution operations. The key consists of the <see cref="InvocationForm"/> together with the input type and,
    /// when merging is active, the data source type. The input type might differ from <see cref="InvocationForm.TypeIn"/>
    /// when that type includes usage of 'g', which is considered to be a "template" or "wildcard" indicating that it
    /// should match any type, and NOT that the type should actually be 'g'.
    /// </summary>
    private readonly ConcurrentDictionary<(InvocationForm form, DType typeIn, DType typeSrc), ExecutionOper> _cacheExec;

    protected MultiFormOper(
            bool isFunc, DName name, NPath ns, int arityMin, int arityMax,
            Immutable.Array<InvocationForm> forms, string version)
        // REVIEW: Should "union: false" be changed to default to true?
        : base(isFunc, name, ns, union: false, arityMin, arityMax, version)
    {
        Validation.BugCheckParam(forms.Length > 0, nameof(forms));
        Validation.BugCheckParam(ArityMin == forms.Min(form => form.ArityMin), nameof(arityMin));
        Validation.BugCheckParam(ArityMax == forms.Max(form => form.ArityMax), nameof(arityMax));

        // Compute the mask for accepted arities.
        var maskAll = BitSet.GetMask(0);
        foreach (var form in forms)
        {
            Validation.Assert(form.ArityMin >= ArityMin);
            Validation.Assert(form.ArityMax <= ArityMax);
            if (form.ArityMax > 0)
            {
                // GetMask sets all bits at positions strictly less than the given arity. Since we want the bit
                // at ArityMax to be set, we need to add 1.
                var maskCur = BitSet.GetMask(form.ArityMin, form.ArityMax + 1);
                maskAll |= maskCur;
            }
        }
        _maskArities = maskAll;

        _forms = forms;
        _cacheExec = new ConcurrentDictionary<(InvocationForm form, DType typeIn, DType typeSrc), ExecutionOper>();
    }

    protected override bool SupportsDirectiveCore(ArgTraits traits, int slot, Directive dir)
    {
        var traits1 = traits as ArgTraitsMff;
        Validation.AssertValue(traits1);
        Validation.Assert(dir != Directive.None);

        return dir == Directive.Top && slot > traits1.FormCur.CountSlotsMisMin;
    }

    public override bool SupportsArity(int arity)
    {
        if (!base.SupportsArity(arity))
            return false;
        return _maskArities.TestBit(arity);
    }

    /// <summary>
    /// Select a subset of forms based on the given parse level information.
    /// </summary>
    private Immutable.Array<InvocationForm> SelectForms(int arity, NameTuple names, BitSet implicitNames, DirTuple dirs, out bool err)
    {
        Validation.Assert(SupportsArity(arity));
        Validation.Assert(names.IsDefault || names.Length == arity);
        Validation.Assert(!implicitNames.TestAtOrAbove(names.Length));
        Validation.Assert(dirs.Length <= arity);

        err = false;
        int limSel = GetLimSel(arity, dirs);
        var forms = Immutable.Array.CreateBuilder<InvocationForm>();
        foreach (var form in _forms)
        {
            if (!form.SupportsArity(arity))
                continue;
            if (Matches(form, arity, names, implicitNames, dirs, limSel))
                forms.Add(form);
        }

        if (forms.Count == 0)
        {
            err = true;
            InvocationForm formBest = null;
            // If forms is empty, then the names and directives are invalid and we will always error.
            // Just find one suitable form to format error messages in SpecializeTypes.
            foreach (var form in _forms)
            {
                if (form.SupportsArity(arity))
                {
                    if (formBest == null)
                    {
                        formBest = form;
                        continue;
                    }
                    formBest = Compare(form, formBest) > 0 ? form : formBest;
                }
            }
            forms.Add(formBest);
        }

        static int Compare(InvocationForm formA, InvocationForm formB)
        {
            // REVIEW: Determine which is better somehow! Currently we prefer in order:
            // 1. Larger number of mis selectors (0 if no mis)
            // 2. Smaller arity range
            // 3. Smaller ArityMax
            int key = formB.CountSlotsMisMax - formA.CountSlotsMisMax;
            if (key == 0)
                key = (formA.ArityMax - formA.ArityMin) - (formB.ArityMax - formB.ArityMin);
            if (key == 0)
                key = formA.ArityMax - formB.ArityMax;
            return key;
        }

        // If this assert fires, the SupportsArity implementation is flawed.
        Validation.Assert(forms.Count > 0);
        forms.QuadSort(Compare);
        return forms.ToImmutable();
    }

    /// <summary>
    /// Checks if <paramref name="form"/> matches the given parse level information.
    /// </summary>
    private bool Matches(InvocationForm form, int arity, NameTuple names, BitSet implicitNames, DirTuple dirs, int limSel)
    {
        Validation.AssertValue(form);
        Validation.Assert(SupportsArity(arity));
        Validation.Assert(names.IsDefault || names.Length == arity);
        Validation.Assert(!implicitNames.TestAtOrAbove(names.Length));
        Validation.Assert(dirs.Length <= arity);

        // The first time this method is called is in the initial
        // GetArgTraits, where limSel should be max. Then if
        // SpecializeTypes fails the binder should rebind with
        // an ArgTraits with a smaller number of selectors, making
        // limSel monotonically decreasing.
        // This means that if there are optional mis slots, such that
        // the effective number of mis slots is between limSel and
        // form.CountSlotsMisMin, then it would have been considered
        // in a previous rebind iteration with a larger limSel and
        // promptly rejected.
        if (form.CountSlotsMisMin >= limSel)
            return false;

        if (names.IsDefault)
            return true;

        // Check for duplicate names or non-named args after named ones.
        // Note that a name doesn't count as "seen" if it matches the positional
        // slot's name.
        // REVIEW: Perhaps support a "loose" mode that tolerates fuzzy
        // matches when none satisfy the naming requirements.
        var nameToSlot = new Dictionary<string, int>();
        var slots = form.GetSlots().ToList();
        HashSet<DName> namesSlots = null;
        Validation.AssertIndexInclusive(arity, slots.Count);
        bool seenName = false;
        int islot = 0;
        for (int iname = 0; islot < arity && islot < slots.Count; iname++, islot++)
        {
            // Ignore the MIS source slot.
            if (form.HasSelector && islot == 0)
                continue;

            var name = names[iname];
            if (iname == limSel && limSel < form.CountSlotsMisMax + 1)
            {
                // If we see the directive, skip the rest of the mis slots.
                var islotNew = form.CountSlotsMisMax + 1;
                if (islotNew >= slots.Count)
                    return false;

                // If we don't have a name, then skipping over
                // any mis slots is an error. If we do, then
                // we check optionality at the bottom of this method.
                if (!name.IsValid || implicitNames.TestBit(iname))
                    return false;
                islot = islotNew;
            }

            var nameSlot = slots[islot].Name;
            DName nameFld;
            if (name.IsValid && !implicitNames.TestBit(iname))
            {
                namesSlots ??= new HashSet<DName>(slots.Select(s => s.Name));
                if (!namesSlots.Contains(name))
                    return false;
                if (nameSlot != name)
                    seenName = true;
                nameFld = name;
            }
            else
            {
                if (seenName)
                    return false;
                nameFld = nameSlot;
            }

            if (nameToSlot.ContainsKey(nameFld))
                return false;
            nameToSlot.Add(nameFld, iname);
        }

        // Check that all required parameters are satisfied.
        foreach (var slot in slots)
        {
            if (slot.Name.IsValid && !slot.IsOptional && !nameToSlot.ContainsKey(slot.Name))
                return false;
        }

        return true;
    }

    private bool Matches(InvocationForm form, InvocationInfo info, out (DType typeRes, TypeTuple typesArg) pair)
    {
        Validation.AssertValue(form);
        Validation.AssertValue(info);
        var err = false;
        // REVIEW: Seems like this shouldn't post the diagnostic until/unless this overload is selected.
        pair = form.SpecializeTypes(this, info, diag => { err = true; info.PostDiagnostic(diag); });
        if (err)
            return false;
        var args = info.Args;
        var typesIn = pair.typesArg;
        Validation.Assert(typesIn.Length == args.Length);
        for (int iarg = 0; iarg < args.Length; iarg++)
        {
            if (!typesIn[iarg].Accepts(args[iarg].Type, Union(form)))
                return false;
        }
        return true;
    }

    protected override ArgTraits GetArgTraitsCore(int carg, NameTuple names, BitSet implicitNames, DirTuple dirs)
    {
        Validation.Assert(SupportsArity(carg));

        var forms = SelectForms(carg, names, implicitNames, dirs, out bool err);
        if (forms.Length > 0)
            return forms[0].GetArgTraits(this, carg, names, implicitNames, dirs, forms, 0, err);

        Validation.Assert(false);
        return ArgTraitsSimple.Create(this, eager: true, carg);
    }

    protected sealed override ArgTraits GetArgTraitsCore(int carg)
    {
        // Delegate to the one with names.
        Validation.Assert(SupportsArity(carg));
        return GetArgTraitsCore(carg, default, default, default);
    }

    protected override (DType typeRes, TypeTuple typesArg) SpecializeTypesCore(InvocationInfo info,
        out bool union, out ArgTraits traitsChange)
    {
        Validation.AssertValue(info);

        traitsChange = null;
        var traits = info.Traits as ArgTraitsMff;
        Validation.AssertValue(traits);
        Validation.Assert(traits.Candidates.Length > 0);

        // Check for bad directives first.
        for (int i = 0; i < info.Dirs.Length; i++)
        {
            Validation.AssertIndex(i, info.ParseArity);
            var dir = info.Dirs[i];
            if (dir != Directive.None && dir != Directive.Top)
                info.PostDiagnostic(RexlDiagnostic.Error(info.ParseArgs[i], ErrorStrings.ErrBadDirective));
        }

        if (traits.ParseError)
            info.PostDiagnostic(RexlDiagnostic.Error(info.ParseNode, ErrorStrings.ErrNoOverload));

        var form = traits.FormCur;
        if (!Matches(form, info, out var pair))
        {
            if (traits.IndexForm < traits.Candidates.Length - 1)
            {
                // Choose another form to try rebinding.
                // We still return the original specialized types
                // to trigger failure and rebinding.
                Validation.Assert(!traits.ParseError);
                var formChange = traits.Candidates[traits.IndexForm + 1];
                traitsChange = formChange.GetArgTraits(this, info.Arity, info.Names, default, info.Dirs, traits.Candidates, traits.IndexForm + 1, false);
            }
        }

        union = Union(form);
        if (!IsProc)
            return pair;

        return (DType.General, pair.typesArg);
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        // Disallow this op from code gen. It should only
        // be allowed in the execution ops.
        full = false;

        if (call.Traits is not ArgTraitsMff traits)
            return false;

        var form = traits.FormCur;

        // REVIEW: This is somewhat sleazy. The WrapInvocationInfo has no parse args, so any
        // diagnostic construction in the specialize types call would throw. This working depends
        // on all such construction being guarded by a test for the error sink being non-null.
        var (typeRet, typesArg) = form.SpecializeTypes(this, new WrapInvocationInfo(call), null);

        for (int iarg = 0; iarg < typesArg.Length; iarg++)
        {
            if (call.Args[iarg].Type != typesArg[iarg])
                return false;
        }

        if (call.Oper.IsProc)
        {
            if (!call.Type.IsGeneral)
                return false;
        }
        else if (typeRet != call.Type)
            return false;

        return true;
    }

    // Use union except for the direct forms (1 and 2).
    // REVIEW: Should this change to always use union?
    private static bool Union(InvocationForm form) => !(form is SimpleFormBase || form is RecFieldForm);

    /// <summary>
    /// Specializes the type of a single slot by modifying <paramref name="typeSlot"/>.
    /// This includes filling in wildcard types indicated by `g` and, if <paramref name="allowSingle"/>
    /// is true, allowing a lower seq count when <paramref name="typeArg"/> has a lower one than <paramref name="typeSlot"/>.
    /// </summary>
    private static void SpecializeType(ref DType typeSlot, DType typeArg, bool allowSingle)
    {
        if (allowSingle && typeSlot.SeqCount > typeArg.SeqCount && !typeArg.RootType.IsNull)
            typeSlot = typeSlot.ItemTypeOrThis;
        typeSlot = typeSlot.GetIncludedType(typeArg);
    }

    /// <summary>
    /// If <paramref name="allowSingle"/> is true and
    /// <paramref name="typeArg"/> is the item type of <paramref name="typeSlot"/>,
    /// then increases <paramref name="typeArg"/>'s seq count and returns true. Otherwise returns false.
    /// Note that this method does not cover inclusion and acceptance, only seq count. So this is expected
    /// to be called after SpecializeTypes has completed, during reduction.
    /// Because of that fact, it also does not need to worry that <paramref name="typeArg"/>
    /// is the null type.
    /// </summary>
    private static bool IsSingleOf(DType typeSlot, ref DType typeArg, bool allowSingle)
    {
        var isSingle = allowSingle && typeArg.SeqCount < typeSlot.SeqCount;
        if (isSingle)
            typeArg = typeArg.ToSequence();
        return isSingle;
    }

    private static int GetLimSel(int limSel, DirTuple dirs)
    {
        if (!dirs.IsDefault)
        {
            var idir = dirs.IndexOf(Directive.Top);
            if (idir < limSel)
                limSel = idir;
        }

        return limSel;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        var traits = call.Traits as ArgTraitsMff;
        Validation.AssertValue(traits);
        if (traits.FormCur != null)
            return traits.FormCur.Reduce(this, reducer, call).VerifyValue();

        // None are applicable.
        return call;
    }

    protected ExecutionOper EnsureExec(InvocationForm form, DType typeIn, MergeInfo merge = null)
    {
        Validation.AssertValue(form);
        Validation.Assert(form.TypeIn.Includes(typeIn) || form.AllowSingleIn && form.TypeIn.ItemTypeOrThis.Includes(typeIn));
        Validation.AssertValueOrNull(merge);

        // Since merge is determined purely by form and merge.TypeSrc, we don't need to use other information in
        // merge as part of the key.
        var key = (form, typeIn, merge != null ? merge.TypeSrc : default);
        if (!_cacheExec.TryGetValue(key, out var exec))
            exec = _cacheExec.GetOrAdd(key, CreateExec(form, typeIn, merge));

        return exec;
    }

    /// <summary>
    /// For custom forms, this may need to be overridden.
    /// </summary>
    protected virtual ExecutionOper CreateExec(InvocationForm form, DType typeIn, MergeInfo merge = null)
    {
        Validation.AssertValue(form);
        Validation.Assert(form.TypeIn.Includes(typeIn) || form.AllowSingleIn && form.TypeIn.ItemTypeOrThis.Includes(typeIn));
        Validation.AssertValueOrNull(merge);

        return ExecutionOper.Create(this, form, typeIn, merge);
    }

    /// <summary>
    /// Gets the merge info given the invocation form and source type. The base implementation supports
    /// record merging with output field renaming on collision.
    /// </summary>
    public virtual MergeInfo GetMergeInfo(InvocationForm form, DType typeSrc)
    {
        Validation.AssertValue(form);

        if (!form.CanMerge)
            return null;

        // The default merging functionality only handles record merging with mos field renaming.
        // REVIEW: Perhaps generalize this?
        if (!typeSrc.IsTableReq || !form.HasMos || !form.TypeMos.IsTableReq)
            return null;

        Dictionary<DName, DName> renames = null;
        DType typeMos = form.TypeMos;
        DType typeMrg = typeSrc;
        foreach (var field in typeMos.GetNames())
        {
            if (!typeMrg.TryGetNameType(field.Name, out var typeCur))
            {
                typeMrg = typeMrg.Add(field);
                continue;
            }

            var count = 2;
            for (; ; )
            {
                // REVIEW: Do we want a different disambiguation strategy? Perhaps we should rename the src
                // fields instead of mos fields?
                var name = new DName($"{field.Name}{count++}");
                if (!typeMrg.TryGetNameType(name, out _))
                {
                    renames = renames ?? new Dictionary<DName, DName>();
                    renames.Add(field.Name, name);
                    typeMrg = typeMrg.AddNameType(name, field.Type);
                    break;
                }
            }
        }

        DType typeDst;
        if (!form.NameMos.IsValid)
        {
            Validation.Assert(form.TypeOut == form.TypeMos);
            typeDst = typeMrg;
        }
        else
        {
            Validation.Assert(form.TypeOut.GetNameTypeOrDefault(form.NameMos) == form.TypeMos);
            typeDst = form.TypeOut.SetNameType(form.NameMos, typeMrg);
        }

        return new RecordMergeInfo(typeSrc, typeMrg, typeDst, renames);
    }
}

partial class MultiFormOper<TCookie>
{
    // See the top of this file for a catalog of the forms.

    /// <summary>
    /// Represents an invocation form or pattern. This should be immutable and pure functional. In particular,
    /// the property return values should not vary.
    /// </summary>
    public abstract class InvocationForm
    {
        /// <summary>
        /// The cookie value for this usage.
        /// </summary>
        public readonly TCookie Cookie;

        /// <summary>
        /// The core function input type. Multiple inputs are assumed to be wrapped in a record.
        /// </summary>
        public readonly DType TypeIn;

        /// <summary>
        /// The core function output type. Multiple outputs are assumed to be wrapped in a record.
        /// </summary>
        public readonly DType TypeOut;

        /// <summary>
        /// Whether there is a "main input sequence", i.e. a sequence such that the output is based
        /// off its individual items.
        /// </summary>
        public virtual bool HasMis => false;

        /// <summary>
        /// Whether this form uses selectors on a data source to create the MIS.
        /// Should only be true if <see cref="HasMis"/> is true.
        /// </summary>
        public virtual bool HasSelector => HasMis;

        /// <summary>
        /// The type of the mis, returning default(DType) if <see cref="HasMis"/> is false.
        /// </summary>
        public virtual DType TypeMis { get { Validation.Assert(!HasMis); return default; } }

        /// <summary>
        /// The name of the field for the mis, if <see cref="HasMis"/> is true and <see cref="TypeIn"/> is a record.
        /// </summary>
        public virtual DName NameMis { get { Validation.Assert(!HasMis || TypeMis == TypeIn); return default; } }

        /// <summary>
        /// The max number of selector slots corresponding to the fields of the mis. Returns 0 if <see cref="HasMis"/>
        /// is false.
        /// </summary>
        public abstract int CountSlotsMisMax { get; }

        /// <summary>
        /// The min number of selector slots corresponding to the fields of the mis. Returns 0 if <see cref="HasMis"/>
        /// is false.
        /// This is unequal to <see cref="CountSlotsMisMax"/> when there are optional mis slots.
        /// </summary>
        public virtual int CountSlotsMisMin => CountSlotsMisMax;

        /// <summary>
        /// Whether there is a "main output sequence", and if so what kind. Should only be
        /// <see cref="MosKind.None"/> unless there is also a mis.
        /// </summary>
        public virtual MosKind MosKind => MosKind.None;

        /// <summary>
        /// Whether there is a "main output sequence". Should only be true if there is also a mis.
        /// </summary>
        public bool HasMos => MosKind.HasMos();

        /// <summary>
        /// The type of the mos, returning default(DType) if <see cref="HasMos"/> is false.
        /// </summary>
        public virtual DType TypeMos { get { Validation.Assert(!HasMos); return default; } }

        /// <summary>
        /// The name of the field for the mos, if <see cref="HasMos"/> is true and <see cref="TypeOut"/> is a record.
        /// </summary>
        public virtual DName NameMos { get { Validation.Assert(!HasMos || TypeMos == TypeOut); return default; } }

        /// <summary>
        /// Whether <see cref="TypeIn"/> can be a singular value which will be wrapped in
        /// a singleton sequence.
        /// </summary>
        public virtual bool AllowSingleIn => false;

        /// <summary>
        /// Whether merging is a possibility. This is just an alias for <see cref="HasMos"/>.
        /// REVIEW: Perhaps we should generalize to other kinds of merging than item based.
        /// </summary>
        public bool CanMerge { get { return HasMos; } }

        /// <summary>
        /// The min arity of this form.
        /// </summary>
        public abstract int ArityMin { get; }

        /// <summary>
        /// The max arity of this form.
        /// </summary>
        public abstract int ArityMax { get; }

        protected InvocationForm(TCookie cookie, DType typeIn, DType typeOut)
        {
            Validation.BugCheckParam(typeIn.IsValid, nameof(typeIn));
            Validation.BugCheckParam(typeOut.IsValid, nameof(typeOut));

            Cookie = cookie;
            TypeIn = typeIn;
            TypeOut = typeOut;
        }

        protected static ReadOnly.HashSet<DName> GetAllowSingle(Immutable.Array<SlotInfo> slots)
        {
            HashSet<DName> allowSingle = null;
            foreach (var slot in slots)
            {
                if (slot.AllowSingle)
                {
                    allowSingle ??= new HashSet<DName>();
                    allowSingle.Add(slot.Name);
                }
            }
            return allowSingle;
        }

        protected void CheckSlots(DType typeRec, Immutable.Array<SlotInfo> slots, string name)
        {
            Validation.BugCheckParam(!slots.IsDefault, name);
            Validation.BugCheckParam(slots.Length > 0, name);

            bool seenOpt = false;
            foreach (var slot in slots)
            {
                Validation.BugCheckParam(typeRec.TryGetNameType(slot.Name, out DType typeSlot), name, "Bad slot name");
                Validation.BugCheckParam(typeSlot == slot.Type, name, "Bad slot type");
                Validation.BugCheckParam(!seenOpt || slot.IsOptional, name, "Non-opt after opt slot");
                seenOpt = slot.IsOptional;
            }
        }

        protected DType CheckMos(MosKind mosKind, DName nameMos = default)
        {
            if (mosKind == MosKind.None)
            {
                Validation.BugCheckParam(!nameMos.IsValid, nameof(nameMos));
                return default;
            }

            if (!nameMos.IsValid)
            {
                Validation.BugCheckParam(TypeOut.IsSequence, nameof(mosKind));
                return TypeOut;
            }

            Validation.BugCheckParam(TypeOut.IsRecordReq, nameof(nameMos));
            Validation.BugCheckParam(TypeOut.TryGetNameType(nameMos, out DType typeMos), nameof(nameMos));
            Validation.BugCheckParam(typeMos.IsSequence, nameof(nameMos));
            return typeMos;
        }

        /// <summary>
        /// Whether this form supports the indicated arity.
        /// Note that sub-classes can override this so the supported arities need not be contiguous.
        /// </summary>
        public virtual bool SupportsArity(int arity) { return ArityMin <= arity & arity <= ArityMax; }

        public ArgTraitsMff GetArgTraits(MultiFormOper<TCookie> parent, int carg, NameTuple names, BitSet implicitNames, DirTuple dirs,
            Immutable.Array<InvocationForm> forms, int ind, bool err)
        {
            Validation.Assert(forms[ind] == this);
            return ArgTraitsMff.Create(GetArgTraitsCore(parent, carg, names, implicitNames, dirs), forms, ind, err);
        }

        internal protected abstract ArgTraits GetArgTraitsCore(MultiFormOper<TCookie> parent, int carg, NameTuple names, BitSet implicitNames, DirTuple dirs);

        internal protected abstract IEnumerable<SlotInfo> GetSlots();

        internal protected (DType typeRes, TypeTuple typesArg) SpecializeTypes(
            MultiFormOper<TCookie> parent, InvocationInfo info, Action<BaseDiagnostic> errorSink)
        {
            var types = SpecializeTypesCore(parent, info, errorSink);
            // REVIEW: It would be nice if we could easily cache the MergeInfo.
            DType typeRet = GetReturnType(parent, types[0], out _);
            return (typeRet, types);
        }

        protected abstract TypeTuple SpecializeTypesCore(MultiFormOper<TCookie> parent, InvocationInfo info, Action<BaseDiagnostic> errorSink);

        internal protected BoundNode Reduce(MultiFormOper<TCookie> parent, IReducer reducer, BndCallNode call)
        {
            Validation.AssertValue(parent);
            Validation.AssertValue(reducer);
            Validation.AssertValue(call);
            Validation.Assert(parent.IsValidCall(call));

            DType typeRet = GetReturnType(parent, call.Args[0].Type, out var merge);
            Validation.Assert(CanMerge || merge == null);
            Validation.Assert(typeRet == TypeOut || merge != null);

            return ReduceCore(parent, reducer, call, merge).VerifyValue();
        }

        protected abstract BoundNode ReduceCore(MultiFormOper<TCookie> parent, IReducer reducer, BndCallNode call, MergeInfo merge);

        /// <summary>
        /// Gets the return type, together with the optional <see cref="MergeInfo"/>, given the type of the
        /// first argument. This supports item merging as described above. It does not assume record merging.
        /// </summary>
        internal protected DType GetReturnType(MultiFormOper<TCookie> parent, DType typeSrc, out MergeInfo merge)
        {
            Validation.AssertValue(parent);

            if (!CanMerge)
            {
                merge = null;
                return TypeOut;
            }

            // Defer to the parent to get the specifics of the merging.
            merge = parent.GetMergeInfo(this, typeSrc);
            return merge == null ? TypeOut : merge.TypeDst;
        }

        protected void SpecializeTypesSlots(
            MultiFormOper<TCookie> parent, Action<BaseDiagnostic> errorSink,
            DType typeRec, Immutable.Array<SlotInfo> slots, ReadOnly.HashSet<DName> allowSingle,
            int iargLim, InvocationInfo info, ref bool seenName,
            TypeTuple.Builder bldr)
        {
            Validation.AssertValue(parent);
            Validation.AssertValueOrNull(errorSink);
            Validation.Assert(SupportsArity(info.Arity));
            Validation.Assert(typeRec.IsRecordXxx);
            Validation.Assert(slots.Length > 0);
            Validation.Assert(info.Names.IsDefault || info.Names.Length == info.Args.Length);
            Validation.Assert(bldr.Count <= iargLim);
            Validation.Assert(iargLim <= info.Args.Length);
            Validation.Assert(iargLim - bldr.Count <= slots.Length);

            bool seenError = false;

            var nameToSlot = new Dictionary<string, int>();
            int iargBase = bldr.Count;
            Validation.Assert(iargLim - iargBase <= slots.Length);

            for (int iarg = iargBase; iarg < iargLim; iarg++)
            {
                var nameSlot = slots[iarg - iargBase].Name;
                bool hasError = false;
                var name = info.Names.GetItemOrDefault(iarg);
                var tokName = info.NameTokens.GetItemOrDefault(iarg);
                Validation.Assert(name.IsValid | (tokName == null));
                if (name.IsValid)
                {
                    if (!typeRec.Contains(name))
                    {
                        errorSink?.Invoke(RexlDiagnostic.Error(tokName, ErrorStrings.ErrFieldDoesNotExist_Type, typeRec));
                        hasError = true;
                        name = default;
                        tokName = null;
                    }
                    else if (nameSlot != name)
                        seenName = true;
                }
                else if (seenName)
                {
                    // We should use a name for all the arguments after the first named one (that didn't match its positional name).
                    errorSink?.Invoke(RexlDiagnostic.Error(info.GetParseArg(iarg), ErrorStrings.ErrNeedFieldName_Slot_Func, iarg + 1, parent.Name));
                    hasError = true;
                }

                var nameFld = name.IsValid ? name : slots[iarg - iargBase].Name;
                typeRec.TryGetNameType(nameFld, out DType typeFld).Verify();
                var arg = info.Args[iarg];
                SpecializeType(ref typeFld, arg.Type, allowSingle.Contains(nameFld));
                bldr.Add(typeFld);

                if (!nameToSlot.TryGetValue(nameFld, out int slot))
                    nameToSlot.Add(nameFld, iarg);
                else if (!hasError)
                {
                    // If ident is not valid but we have a duplicate, then nameFld came from a positional argument and
                    // a previous argument was named the same. But then hasError would be true.
                    Validation.Assert(tokName != null || errorSink == null);
                    Validation.Assert(name.IsValid);
                    errorSink?.Invoke(RexlDiagnostic.Error(tokName, ErrorStrings.ErrDuplicateParamName_Name_Diff, nameFld, iarg - slot));
                    hasError = true;
                }

                seenError |= hasError;
            }

            Validation.Assert(bldr.Count == iargLim);

            Validation.Assert(nameToSlot.Count <= iargLim - iargBase);
            if (!seenError && nameToSlot.Count < slots.Length)
            {
                foreach (var slot in slots)
                {
                    if (!slot.IsOptional && !nameToSlot.ContainsKey(slot.Name))
                        errorSink?.Invoke(RexlDiagnostic.Error(info.GetParseArg(info.Args.Length - 1), ErrorStrings.ErrNeedArg_Name, slot.Name));
                }
            }
        }

        protected bool TryCreateRecord(out BoundNode rec, ArgTuple args, Immutable.Array<DName> names,
            int iargMin, int iargLim, DType typeRec, Immutable.Array<SlotInfo> slots, ReadOnly.HashSet<DName> allowSingle, NamedItems items = null)
        {
            Validation.Assert(typeRec.IsRecordReq);
            Validation.Assert(slots.Length > 0);

            items ??= NamedItems.Empty;
            var initCount = items.Count;
            var ifield = 0;

            for (int iarg = iargMin; iarg < iargLim; iarg++, ifield++)
            {
                var name = !names.IsDefault && names[iarg].IsValid ? names[iarg] : slots[ifield].Name;
                if (items.TryGetValue(name, out _))
                {
                    rec = null;
                    return false;
                }

                var typeFld = typeRec.GetNameTypeOrDefault(name);
                var typeInc = args[iarg].Type;
                var arg = args[iarg];
                if (typeInc != typeFld)
                {
                    if (!typeFld.IsValid)
                    {
                        rec = null;
                        return false;
                    }

                    if (IsSingleOf(typeFld, ref typeInc, allowSingle.Contains(name)))
                        arg = BndSequenceNode.Create(typeInc, ArgTuple.Create(arg));

                    if (!typeFld.Includes(typeInc))
                    {
                        rec = null;
                        return false;
                    }
                    typeRec = typeRec.SetNameType(name, typeInc);
                }
                items = items.SetItem(name, arg);
            }

            foreach (var slot in slots)
            {
                var typeFld = typeRec.GetNameTypeOrDefault(slot.Name);
                if (!typeFld.IsValid || !slot.Type.Includes(typeFld) && !(slot.AllowSingle && slot.Type.ItemTypeOrThis.Includes(typeFld)))
                {
                    Validation.Assert(false, "inconsistent slot information");
                    rec = null;
                    return false;
                }

                if (!items.ContainsKey(slot.Name))
                {
                    if (!slot.IsOptional)
                    {
                        rec = null;
                        return false;
                    }
                    items = items.SetItem(slot.Name, CreateConstant(slot));
                }
            }
            Validation.Assert(items.Count == slots.Length + initCount);
            Validation.Assert(items.Count <= typeRec.FieldCount);

            rec = BndRecordNode.Create(typeRec, items);
            return true;
        }

        protected BoundNode CreateConstant(SlotInfo slot)
        {
            Validation.AssertValue(slot);
            Validation.Assert(slot.IsOptional);

            object value = slot.DefaultValue;
            if (value == null)
            {
                Validation.Assert(slot.Type.IsOpt);
                return BndNullNode.Create(slot.Type);
            }

            BoundNode bnd;
            switch (slot.Type.Kind)
            {
            default:
                throw Validation.BugExcept("Unsupported default type");

            case DKind.Text:
                Validation.Assert(value is string);
                return BndStrNode.Create((string)value);

            case DKind.Bit:
                Validation.Assert(value is bool);
                bnd = BndIntNode.CreateBit((bool)value);
                break;
            case DKind.R8:
                Validation.Assert(value is double);
                bnd = BndFltNode.CreateR8((double)value);
                break;
            case DKind.R4:
                Validation.Assert(value is float);
                bnd = BndFltNode.CreateR4((float)value);
                break;
            case DKind.IA:
                Validation.Assert(value is BigInteger);
                bnd = BndIntNode.CreateI((BigInteger)value);
                break;
            case DKind.I8:
                Validation.Assert(value is long);
                bnd = BndIntNode.CreateI8((long)value);
                break;
            case DKind.I4:
                Validation.Assert(value is int);
                bnd = BndIntNode.CreateI4((int)value);
                break;
            case DKind.I2:
                Validation.Assert(value is short);
                bnd = BndIntNode.CreateI2((short)value);
                break;
            case DKind.I1:
                Validation.Assert(value is sbyte);
                bnd = BndIntNode.CreateI1((sbyte)value);
                break;
            case DKind.U8:
                Validation.Assert(value is ulong);
                bnd = BndIntNode.CreateU8((ulong)value);
                break;
            case DKind.U4:
                Validation.Assert(value is uint);
                bnd = BndIntNode.CreateU4((uint)value);
                break;
            case DKind.U2:
                Validation.Assert(value is ushort);
                bnd = BndIntNode.CreateU2((ushort)value);
                break;
            case DKind.U1:
                Validation.Assert(value is byte);
                bnd = BndIntNode.CreateU1((byte)value);
                break;
            }

            if (slot.Type.HasReq)
                bnd = BndCastOptNode.Create(bnd);

            return bnd;
        }
    }

    /// <summary>
    /// Handles cases (1), namely F(arg).
    /// TypeIn: T.
    /// </summary>
    protected abstract class SimpleFormBase : InvocationForm
    {
        public override bool HasSelector => false;
        public override int ArityMin => 1;
        public override int ArityMax => 1;
        public override bool AllowSingleIn => AllowSingle;
        public override int CountSlotsMisMax => 0;

        public override bool HasMis { get; }
        public override DType TypeMis { get; }
        public override MosKind MosKind { get; }
        public override DType TypeMos { get; }
        public override DName NameMos { get; }

        private SlotInfo _slot;

        /// <summary>
        /// Whether the only argument allows a transformation from a single value
        /// to a singleton sequence.
        /// </summary>
        public bool AllowSingle { get; }

        protected SimpleFormBase(TCookie cookie, DType typeIn, DType typeOut, bool hasMis, MosKind mosKind, bool allowSingle, DName nameMos)
            : base(cookie, typeIn, typeOut)
        {
            Validation.BugCheckParam(!allowSingle || typeIn.IsSequence, nameof(allowSingle));
            HasMis = hasMis && typeIn.IsSequence;
            TypeMis = hasMis ? typeIn : default;
            MosKind = !HasMis ? MosKind.None : mosKind;
            NameMos = MosKind.HasMos() ? nameMos : default;
            TypeMos = CheckMos(MosKind, NameMos);
            AllowSingle = allowSingle;
            _slot = SlotInfo.Create(TypeIn, default, false, null, allowSingle);
        }

        internal protected override ArgTraits GetArgTraitsCore(MultiFormOper<TCookie> parent, int carg, NameTuple names, BitSet implicitNames, DirTuple dirs)
        {
            Validation.Assert(carg == 1);
            // REVIEW: Whether this processes the sequence lazily depends on the function semantics.
            return ArgTraitsSimple.Create(parent, eager: true, carg);
        }

        internal protected override IEnumerable<SlotInfo> GetSlots()
        {
            yield return _slot;
        }

        protected override TypeTuple SpecializeTypesCore(MultiFormOper<TCookie> parent, InvocationInfo info, Action<BaseDiagnostic> errorSink)
        {
            Validation.Assert(info.Arity == 1);
            var typeIn = TypeIn;
            var typeArg = info.Args[0].Type;
            SpecializeType(ref typeIn, typeArg, AllowSingle);
            if (typeIn.Accepts(typeArg, Union(this)))
                return TypeTuple.Create(typeArg);
            return TypeTuple.Create(typeIn);
        }

        protected override BoundNode ReduceCore(MultiFormOper<TCookie> parent, IReducer reducer, BndCallNode call, MergeInfo merge)
        {
            Validation.AssertValue(parent);
            Validation.AssertValue(call);
            Validation.Assert(parent.IsValidCall(call));

            var typeIn = TypeIn;
            var typeArg = call.Args[0].Type;
            SpecializeType(ref typeIn, typeArg, AllowSingle);
            var typeInRaw = typeIn;
            var isSingle = IsSingleOf(TypeIn, ref typeIn, AllowSingle);

            var exec = parent.EnsureExec(this, typeIn, merge);
            Validation.Assert(exec.Form == this);
            if (call.Type != exec.TypeCall)
            {
                Validation.Assert(false, "bad call");
                return call;
            }

            var arg = call.Args[0];
            var union = Union(this);
            Validation.Assert(typeInRaw.Accepts(arg.Type, union));

            if (!exec.Merges || isSingle)
            {
                // Case 1a: F(arg) => G(arg).
                var argExec = Conversion.CastBnd(NoopReducerHost.Instance, arg, typeInRaw, union);
                if (isSingle)
                    argExec = BndSequenceNode.Create(typeIn, ArgTuple.Create(argExec));
                var g = BndCallNode.Create(exec, exec.TypeCall, ArgTuple.Create(argExec));
                return reducer.Reduce(g);
            }
            else
            {
                // Case 1b: F(arg) => H(x: arg, x1, x).
                var scopeArg = ArgScope.Create(ScopeKind.With, typeArg);
                var argExec = Conversion.CastBnd(NoopReducerHost.Instance, BndScopeRefNode.Create(scopeArg), typeInRaw, union);
                var h = BndCallNode.Create(exec, exec.TypeCall,
                    ArgTuple.Create(arg, argExec, BndScopeRefNode.Create(scopeArg)), ScopeTuple.Create(scopeArg));
                return reducer.Reduce(h);
            }
        }
    }

    /// <summary>
    /// Handles cases (1), namely F(arg).
    /// TypeIn: T.
    /// </summary>
    protected sealed class SimpleForm : SimpleFormBase
    {
        public SimpleForm(TCookie cookie, DType typeIn, DType typeOut, bool hasMis, MosKind mosKind, bool allowSingle, DName nameMos)
            : base(cookie, typeIn, typeOut, hasMis, mosKind, allowSingle, nameMos)
        {
        }
    }

    protected abstract class SeqFormBase : InvocationForm
    {
        public override bool HasMis => true;
        public override DType TypeMis => TypeIn;
        public override MosKind MosKind { get; }
        public override DType TypeMos { get; }
        public override DName NameMos { get; }

        protected SlotInfo _slotSrc;

        protected SeqFormBase(TCookie cookie, DType typeIn, DType typeOut,
            MosKind mosKind, DName nameMos = default)
            : base(cookie, typeIn, typeOut)
        {
            Validation.BugCheckParam(TypeIn.IsSequence, nameof(typeIn));

            MosKind = mosKind;
            NameMos = nameMos;
            TypeMos = CheckMos(mosKind, nameMos);
            _slotSrc = SlotInfo.Create(default, default, false, null, false);
        }
    }

    /// <summary>
    /// Handles (3a) and (3b), namely F(src, sel).
    /// TypeIn: T*.
    /// </summary>
    protected sealed class SeqForm : SeqFormBase
    {
        public override int ArityMin => 2;
        public override int ArityMax => 2;
        public override int CountSlotsMisMax => 1;

        private SlotInfo _slotSel;

        /// <summary>
        /// Whether the selector argument allows a transformation from a single value
        /// to a singleton sequence.
        /// </summary>
        public bool AllowSingleSel { get; }

        public SeqForm(TCookie cookie, DType typeIn, DType typeOut, MosKind mosKind, bool allowSingleSel, DName nameMos = default)
            : base(cookie, typeIn, typeOut, mosKind, nameMos)
        {
            AllowSingleSel = allowSingleSel;
            _slotSel = SlotInfo.Create(typeIn.ItemTypeOrThis, default, false, null, allowSingleSel);
        }

        internal protected override ArgTraits GetArgTraitsCore(MultiFormOper<TCookie> parent, int carg, NameTuple names, BitSet implicitNames, DirTuple dirs)
        {
            Validation.Assert(carg == 2);
            return ArgTraitsZip.Create(parent, indexed: false, eager: true, carg, seqCount: 1);
        }

        internal protected override IEnumerable<SlotInfo> GetSlots()
        {
            yield return _slotSrc;
            yield return _slotSel;
        }

        protected override TypeTuple SpecializeTypesCore(MultiFormOper<TCookie> parent, InvocationInfo info, Action<BaseDiagnostic> errorSink)
        {
            Validation.Assert(info.Arity == 2);

            var typeSrc = info.Args[0].Type;
            RexlOper.EnsureTypeSeq(ref typeSrc);

            var typeSel = TypeIn.ItemTypeOrThis;
            SpecializeType(ref typeSel, info.Args[1].Type, AllowSingleSel);
            return TypeTuple.Create(typeSrc, typeSel);
        }

        protected override BoundNode ReduceCore(MultiFormOper<TCookie> parent, IReducer reducer, BndCallNode call, MergeInfo merge)
        {
            Validation.AssertValue(parent);
            Validation.AssertValue(reducer);
            Validation.AssertValue(call);
            Validation.Assert(parent.IsValidCall(call));
            Validation.AssertValueOrNull(merge);

            var typeIn = call.Args[1].Type.ToSequence();
            var isSingle = IsSingleOf(TypeIn, ref typeIn, AllowSingleSel);

            var exec = parent.EnsureExec(this, typeIn, merge);
            Validation.Assert(exec.Form == this);
            Validation.Assert(exec.Merges == (merge != null));
            if (call.Type != exec.TypeCall)
            {
                Validation.Assert(false, "bad call");
                return call;
            }

            ArgScope scopeSrc = null;
            ArgTuple argsMap = call.Args;
            if (exec.Merges)
            {
                Validation.Assert(call.Args[0].Type == exec.TypeSrc);
                scopeSrc = ArgScope.Create(ScopeKind.With, exec.TypeSrc);
                argsMap = ArgTuple.Create(BndScopeRefNode.Create(scopeSrc), call.Args[1]);
            }

            if (isSingle)
                argsMap = ArgTuple.Create(argsMap[0], BndSequenceNode.Create(typeIn.ItemTypeOrThis, argsMap.RemoveAt(0)));
            var map = BndCallNode.Create(ForEachFunc.ForEach, typeIn, argsMap, call.Scopes);

            if (!exec.Merges)
            {
                // Case 3a: F(src, sel) => G(Map(src, sel)).
                return reducer.Reduce(BndCallNode.Create(exec, exec.TypeCall, ArgTuple.Create(map)));
            }
            else if (exec.IsFunc && exec.MergeInfo.TypeMrg == exec.TypeDst)
            {
                // Case 3c: Guard(y: src, H(x:y, Map(x, sel), x).
                var scopeGuard = ArgScope.Create(ScopeKind.Guard, exec.TypeSrc);
                var h = BndCallNode.Create(exec, exec.TypeCall,
                    ArgTuple.Create(BndScopeRefNode.Create(scopeGuard), map, BndScopeRefNode.Create(scopeSrc)), ScopeTuple.Create(scopeSrc));
                var g = BndCallNode.Create(WithFunc.Guard, exec.TypeCall,
                    ArgTuple.Create(call.Args[0], h), ScopeTuple.Create(scopeGuard));
                return reducer.Reduce(g);
            }
            else
            {
                // Case 3b: F(src, sel) => H(x: src, Map(x, sel), x).
                var h = BndCallNode.Create(exec, exec.TypeCall,
                    ArgTuple.Create(call.Args[0], map, BndScopeRefNode.Create(scopeSrc)), ScopeTuple.Create(scopeSrc));
                return reducer.Reduce(h);
            }
        }
    }

    /// <summary>
    /// Handles (4a) and (4b), namely F(src, [m1:] s1, [m2:] s2, ...).
    /// TypeIn: { m1: T1, m2: T2, ... }*.
    /// </summary>
    protected sealed class SeqRecForm : SeqFormBase
    {
        private readonly Immutable.Array<SlotInfo> _slotsMis;
        private readonly ReadOnly.HashSet<DName> _allowSingleMis;

        public override int ArityMin { get; }
        public override int ArityMax { get; }
        public override int CountSlotsMisMax { get; }
        public override int CountSlotsMisMin { get; }

        public SeqRecForm(TCookie cookie, DType typeIn, DType typeOut, Immutable.Array<SlotInfo> slotsMis,
            MosKind mosKind, DName nameMos = default)
            : base(cookie, typeIn, typeOut, mosKind, nameMos)
        {
            Validation.BugCheckParam(TypeMis.IsTableReq, nameof(typeIn));
            CheckSlots(TypeMis, slotsMis, nameof(slotsMis));

            _slotsMis = slotsMis;
            _allowSingleMis = GetAllowSingle(slotsMis);

            // Need at least one to get the map scope for rewriting.
            int arityMinMis = Math.Max(1, _slotsMis.Count(slot => !slot.IsOptional));

            // Args start with src.
            ArityMin = 1 + arityMinMis;
            ArityMax = 1 + _slotsMis.Length;
            CountSlotsMisMax = _slotsMis.Length;

            int count = 0;
            foreach (var slot in _slotsMis)
            {
                if (slot.IsOptional)
                    break;
                count++;
            }
            CountSlotsMisMin = count;
        }

        internal protected override ArgTraits GetArgTraitsCore(MultiFormOper<TCookie> parent, int carg, NameTuple names, BitSet implicitNames, DirTuple dirs)
        {
            Validation.Assert(SupportsArity(carg));
            var maskFlds = BitSet.GetMask(1, carg);
            // REVIEW: Whether this processes the sequence lazily depends on the function semantics.
            return ArgTraits.CreateGeneral(parent, carg,
                scopeKind: ScopeKind.SeqItem, maskScope: 0x1, maskNested: maskFlds,
                maskName: maskFlds, maskNameExp: maskFlds, maskLazySeq: 0x0);
        }

        internal protected override IEnumerable<SlotInfo> GetSlots()
        {
            yield return _slotSrc;
            foreach (var slot in _slotsMis)
                yield return slot;
        }

        protected override TypeTuple SpecializeTypesCore(MultiFormOper<TCookie> parent, InvocationInfo info, Action<BaseDiagnostic> errorSink)
        {
            Validation.Assert(SupportsArity(info.Arity));

            var bldr = Immutable.Array.CreateBuilder<DType>(info.Arity);
            DType typeSrc = info.Args[0].Type;
            RexlOper.EnsureTypeSeq(ref typeSrc);
            bldr.Add(typeSrc);
            bool seenName = false;
            SpecializeTypesSlots(parent, errorSink, TypeMis.ItemTypeOrThis, _slotsMis, _allowSingleMis, info.Arity, info, ref seenName, bldr);
            return bldr.ToImmutable();
        }

        protected override BoundNode ReduceCore(MultiFormOper<TCookie> parent, IReducer reducer, BndCallNode call, MergeInfo merge)
        {
            Validation.AssertValue(parent);
            Validation.AssertValue(reducer);
            Validation.AssertValue(call);
            Validation.Assert(parent.IsValidCall(call));
            Validation.AssertValueOrNull(merge);

            if (!TryCreateRecord(out var rec, call.Args, call.Names, 1, call.Args.Length, TypeMis.ItemTypeOrThis, _slotsMis, _allowSingleMis))
                return call;

            var typeIn = rec.Type.ToSequence();
            if (!TypeIn.Includes(typeIn))
            {
                Validation.Assert(false, "bad call");
                return call;
            }

            var exec = parent.EnsureExec(this, typeIn, merge);
            Validation.Assert(exec.Form == this);
            Validation.Assert(exec.Merges == (merge != null));
            if (call.Type != exec.TypeCall)
            {
                Validation.Assert(false, "bad call");
                return call;
            }

            ArgScope scopeSrc = null;
            BoundNode seq;
            if (exec.Merges)
            {
                Validation.Assert(call.Args[0].Type == exec.TypeSrc);
                scopeSrc = ArgScope.Create(ScopeKind.With, exec.TypeSrc);
                seq = BndScopeRefNode.Create(scopeSrc);
            }
            else
                seq = call.Args[0];

            var map = BndCallNode.Create(ForEachFunc.ForEach, rec.Type.ToSequence(), ArgTuple.Create(seq, rec), call.Scopes);

            if (!exec.Merges)
            {
                // Case 4a: F(src, [m1:] s1, [m2:] s2, ...) => G(Map(src, {m1: s1, m2: s2, ...})).
                return reducer.Reduce(BndCallNode.Create(exec, exec.TypeCall, ArgTuple.Create(map)));
            }
            else if (exec.IsFunc && exec.MergeInfo.TypeMrg == exec.TypeDst)
            {
                // Case 4c: Guard(y: src, H(x: y, Map(x, {m1: s1, m2: s2, ...}, x).
                var scopeGuard = ArgScope.Create(ScopeKind.Guard, exec.TypeSrc);
                var h = BndCallNode.Create(exec, exec.TypeCall,
                    ArgTuple.Create(BndScopeRefNode.Create(scopeGuard), map, BndScopeRefNode.Create(scopeSrc)), ScopeTuple.Create(scopeSrc));
                var g = BndCallNode.Create(WithFunc.Guard, exec.TypeCall,
                    ArgTuple.Create(call.Args[0], h), ScopeTuple.Create(scopeGuard));
                return reducer.Reduce(g);
            }
            else
            {
                // Case 4b: F(src, [m1:] s1, [m2:] s2, ...) => H(x: src, Map(x, {m1: s1, m2: s2, ...}, x).
                var h = BndCallNode.Create(exec, exec.TypeCall,
                    ArgTuple.Create(call.Args[0], map, BndScopeRefNode.Create(scopeSrc)), ScopeTuple.Create(scopeSrc));
                return reducer.Reduce(h);
            }
        }
    }

    /// <summary>
    /// Base form for when the top-most input type is a record.
    /// TypeIn: { [mis: T*,] n1: T1, n2: T2, ...}.
    /// </summary>
    protected abstract class RecFormBase : InvocationForm
    {
        protected readonly Immutable.Array<SlotInfo> _slotsTop;
        protected readonly ReadOnly.HashSet<DName> _allowSingleTop;
        protected readonly int _arityMinTop;

        protected RecFormBase(TCookie cookie, DType typeIn, DType typeOut, Immutable.Array<SlotInfo> slotsTop)
            : base(cookie, typeIn, typeOut)
        {
            Validation.BugCheckParam(TypeIn.IsRecordReq, nameof(typeIn));
            CheckSlots(TypeIn, slotsTop, nameof(slotsTop));

            _slotsTop = slotsTop;
            _allowSingleTop = GetAllowSingle(slotsTop);
            _arityMinTop = slotsTop.Count(slot => !slot.IsOptional);
        }
    }

    /// <summary>
    /// Handles (2), namely F([n1:] a1, [n2:] a2, ...).
    /// TypeIn: { n1: T1, n2: T2, ...}.
    /// </summary>
    protected sealed class RecFieldForm : RecFormBase
    {
        private readonly int _arityMin;
        private readonly int _arityMax;

        public override int ArityMin => _arityMin;
        public override int ArityMax => _arityMax;
        public override int CountSlotsMisMax => 0;

        public RecFieldForm(TCookie cookie, DType typeIn, DType typeOut, Immutable.Array<SlotInfo> slotsTop)
            : base(cookie, typeIn, typeOut, slotsTop)
        {
            _arityMin = _arityMinTop;
            _arityMax = slotsTop.Length;
        }

        internal protected override ArgTraits GetArgTraitsCore(MultiFormOper<TCookie> parent, int carg, NameTuple names, BitSet implicitNames, DirTuple dirs)
        {
            var maskAll = BitSet.GetMask(carg);
            return ArgTraitsNamed.Create(parent, carg, maskName: maskAll, maskNameExp: maskAll);
        }

        internal protected override IEnumerable<SlotInfo> GetSlots()
        {
            foreach (var slot in _slotsTop)
                yield return slot;
        }

        protected override Immutable.Array<DType> SpecializeTypesCore(MultiFormOper<TCookie> parent, InvocationInfo info, Action<BaseDiagnostic> errorSink)
        {
            Validation.Assert(SupportsArity(info.Arity));

            var bldr = Immutable.Array.CreateBuilder<DType>(info.Arity);
            bool seenName = false;
            SpecializeTypesSlots(parent, errorSink, TypeIn, _slotsTop, _allowSingleTop, info.Arity, info, ref seenName, bldr);
            return bldr.ToImmutable();
        }

        protected override BoundNode ReduceCore(MultiFormOper<TCookie> parent, IReducer reducer, BndCallNode call, MergeInfo merge)
        {
            Validation.AssertValue(parent);
            Validation.AssertValue(reducer);
            Validation.AssertValue(call);
            Validation.Assert(parent.IsValidCall(call));
            Validation.Assert(parent._forms.Contains(this));
            Validation.Assert(merge == null);

            // Case 2: F([n1:] a1, [n2:] a2, ...) => G({n1: a1, n2: a2, ...}).
            if (!TryCreateRecord(out var rec, call.Args, call.Names, 0, call.Args.Length, TypeIn, _slotsTop, _allowSingleTop))
                return call;

            var typeIn = rec.Type;
            var exec = parent.EnsureExec(this, typeIn, merge);
            Validation.Assert(exec.Form == this);
            Validation.Assert(!exec.Merges);
            if (call.Type != exec.TypeCall)
            {
                Validation.Assert(false, "bad call");
                return call;
            }

            return reducer.Reduce(BndCallNode.Create(exec, exec.TypeCall, ArgTuple.Create(rec)));
        }
    }

    /// <summary>
    /// Base class for top level record with a mis.
    /// </summary>
    protected abstract class RecSeqFormBase : RecFormBase
    {
        public override bool HasMis => true;
        public override DType TypeMis { get; }
        public override DName NameMis { get; }

        public override MosKind MosKind { get; }
        public override DType TypeMos { get; }
        public override DName NameMos { get; }

        protected readonly SlotInfo _slotSrc;

        protected RecSeqFormBase(TCookie cookie, DType typeIn, DType typeOut, DName nameMis, Immutable.Array<SlotInfo> slotsTop,
                MosKind mosKind, DName nameMos = default)
            : base(cookie, typeIn, typeOut, slotsTop)
        {
            Validation.BugCheckParam(nameMis.IsValid, nameof(nameMis));
            Validation.BugCheckParam(TypeIn.TryGetNameType(nameMis, out DType typeMis), nameof(nameMis));
            Validation.BugCheckParam(typeMis.IsSequence, nameof(nameMis));
            Validation.BugCheckParam(_slotsTop.All(slot => slot.Name != nameMis), nameof(slotsTop));

            NameMis = nameMis;
            TypeMis = typeMis;

            MosKind = mosKind;
            NameMos = nameMos;
            TypeMos = CheckMos(mosKind, nameMos);

            _slotSrc = SlotInfo.Create(default, default, false, null, false);
        }
    }

    /// <summary>
    /// Handles case (5a) and (5b), namely F(src, sel, [n1:] a1, [n2:] a2, ...).
    /// TypeIn: { mis: T*, n1: T1, n2, T2, ... }
    /// </summary>
    protected sealed class RecSeqForm : RecSeqFormBase
    {
        public override int ArityMin { get; }
        public override int ArityMax { get; }
        public override int CountSlotsMisMax => 1;

        public bool AllowSingleSel { get; }

        private readonly SlotInfo _slotSel;

        public RecSeqForm(TCookie cookie, DType typeIn, DType typeOut, DName nameMis, Immutable.Array<SlotInfo> slotsTop,
                MosKind mosKind, bool allowSingleSel, DName nameMos = default)
            : base(cookie, typeIn, typeOut, nameMis, slotsTop, mosKind, nameMos)
        {
            // Args start with src, sel.
            ArityMin = 2 + _arityMinTop;
            ArityMax = 2 + _slotsTop.Length;
            AllowSingleSel = allowSingleSel;
            _slotSel = SlotInfo.Create(TypeMis.ItemTypeOrThis, default, false, null, allowSingleSel);
        }

        internal protected override ArgTraits GetArgTraitsCore(MultiFormOper<TCookie> parent, int carg, NameTuple names, BitSet implicitNames, DirTuple dirs)
        {
            Validation.Assert(SupportsArity(carg));
            var maskName = BitSet.GetMask(2, carg);
            // REVIEW: Whether this processes the sequence lazily depends on the function semantics.
            return ArgTraits.CreateGeneral(parent, carg,
                scopeKind: ScopeKind.SeqItem, maskScope: 0x1, maskNested: 0x2,
                maskName: maskName, maskNameExp: maskName, maskLazySeq: 0x0);
        }

        internal protected override IEnumerable<SlotInfo> GetSlots()
        {
            yield return _slotSrc;
            yield return _slotSel;
            foreach (var slot in _slotsTop)
                yield return slot;
        }

        protected override Immutable.Array<DType> SpecializeTypesCore(MultiFormOper<TCookie> parent, InvocationInfo info, Action<BaseDiagnostic> errorSink)
        {
            Validation.Assert(SupportsArity(info.Arity));

            var bldr = Immutable.Array.CreateBuilder<DType>(info.Arity);
            DType typeSrc = info.Args[0].Type;
            RexlOper.EnsureTypeSeq(ref typeSrc);
            bldr.Add(typeSrc);

            var typeSel = TypeMis.ItemTypeOrThis;
            SpecializeType(ref typeSel, info.Args[1].Type, AllowSingleSel);
            bldr.Add(typeSel);
            bool seenName = false;
            SpecializeTypesSlots(parent, errorSink, TypeIn, _slotsTop, _allowSingleTop, info.Arity, info, ref seenName, bldr);
            return bldr.ToImmutable();
        }

        protected override BoundNode ReduceCore(MultiFormOper<TCookie> parent, IReducer reducer, BndCallNode call, MergeInfo merge)
        {
            Validation.AssertValue(parent);
            Validation.AssertValue(reducer);
            Validation.AssertValue(call);
            Validation.Assert(parent.IsValidCall(call));
            Validation.AssertValueOrNull(merge);

            var typeMis = call.Args[1].Type.ToSequence();
            var isSingle = IsSingleOf(TypeMis, ref typeMis, AllowSingleSel);

            var typeIn = TypeIn;
            if (TypeMis != typeMis)
                typeIn = typeIn.SetNameType(NameMis, typeMis);

            ArgScope scopeSrc = null;
            BoundNode seq;
            if (merge != null)
            {
                Validation.Assert(call.Args[0].Type == merge.TypeSrc);
                scopeSrc = ArgScope.Create(ScopeKind.With, merge.TypeSrc);
                seq = BndScopeRefNode.Create(scopeSrc);
            }
            else
                seq = call.Args[0];

            var sel = call.Args[1];
            if (isSingle)
                sel = BndSequenceNode.Create(typeMis.ItemTypeOrThis, ArgTuple.Create(sel));
            var map = BndCallNode.Create(ForEachFunc.ForEach, typeMis, ArgTuple.Create(seq, sel), call.Scopes);

            var items = NamedItems.Empty.SetItem(NameMis, map);
            if (!TryCreateRecord(out var rec, call.Args, call.Names, 2, call.Args.Length, typeIn, _slotsTop, _allowSingleTop, items))
                return call;

            typeIn = rec.Type;
            if (!TypeIn.Includes(typeIn))
            {
                Validation.Assert(false, "bad call");
                return call;
            }

            var exec = parent.EnsureExec(this, typeIn, merge);
            Validation.Assert(exec.Form == this);
            Validation.Assert(exec.Merges == (merge != null));
            if (call.Type != exec.TypeCall)
            {
                Validation.Assert(false, "bad call");
                return call;
            }

            if (!exec.Merges)
            {
                // Case 5a: F(src, sel, [n1:] a1, [n2:] a2, ...) => G({ mis: Map(src, sel), n1: a1, n2: a2, ... }).
                // No merging, just need the record.
                return reducer.Reduce(BndCallNode.Create(exec, exec.TypeCall, ArgTuple.Create(rec)));
            }
            else if (exec.IsFunc && exec.MergeInfo.TypeMrg == exec.TypeDst)
            {
                // Case 5c: Guard(y: src, H(x:y, { mis: Map(x, sel), n1: a1, n2: a2, ... }, x).
                var scopeGuard = ArgScope.Create(ScopeKind.Guard, exec.TypeSrc);
                var h = BndCallNode.Create(exec, exec.TypeCall,
                    ArgTuple.Create(BndScopeRefNode.Create(scopeGuard), rec, BndScopeRefNode.Create(scopeSrc)), ScopeTuple.Create(scopeSrc));
                var g = BndCallNode.Create(WithFunc.Guard, exec.TypeCall,
                    ArgTuple.Create(call.Args[0], h), ScopeTuple.Create(scopeGuard));
                return reducer.Reduce(g);
            }
            else
            {
                // Case 5b: F(src, sel, [n1:] a1, [n2:] a2, ...) => H(x:src, { mis: Map(x, sel), n1: a1, n2: a2, ... }, x).
                var h = BndCallNode.Create(exec, exec.TypeCall,
                    ArgTuple.Create(call.Args[0], rec, BndScopeRefNode.Create(scopeSrc)), ScopeTuple.Create(scopeSrc));
                return reducer.Reduce(h);
            }
        }
    }

    /// <summary>
    /// Handles case (6a) and (6b), namely: F(src, [m1:] s1, [m2:] s2, ..., [n1:] a1, [n2:] a2, ...).
    /// TypeIn: { mis: { m1: S1, m2: S2, ... }*, n1: T1, n2: T2, ... }
    /// </summary>
    protected sealed class RecSeqRecForm : RecSeqFormBase
    {
        private readonly Immutable.Array<SlotInfo> _slotsMis;
        private readonly ReadOnly.HashSet<DName> _allowSingleMis;

        public override int ArityMin { get; }
        public override int ArityMax { get; }
        public override int CountSlotsMisMax { get; }
        public override int CountSlotsMisMin { get; }

        public RecSeqRecForm(TCookie cookie, DType typeIn, DType typeOut, DName nameMis, Immutable.Array<SlotInfo> slotsMis,
            Immutable.Array<SlotInfo> slotsTop, MosKind mosKind, DName nameMos = default)
            : base(cookie, typeIn, typeOut, nameMis, slotsTop, mosKind, nameMos)
        {
            Validation.BugCheckParam(TypeMis.IsTableReq, nameof(nameMis));
            CheckSlots(TypeMis, slotsMis, nameof(slotsMis));

            _slotsMis = slotsMis;
            _allowSingleMis = GetAllowSingle(slotsMis);

            // If all of the "top" slots are optional, then some of the mis slots can be also.
            // REVIEW: Is there a way to allow some of the mis slots to be optional even if not all top slots are optional?
            int arityMinMis = Math.Max(1, _arityMinTop == 0 ? _slotsMis.Count(slot => !slot.IsOptional) : _slotsMis.Length);

            // Args start with mis src and selectors.
            ArityMin = 1 + arityMinMis + _arityMinTop;
            ArityMax = 1 + _slotsMis.Length + _slotsTop.Length;
            CountSlotsMisMax = _slotsMis.Length;

            int count = 0;
            foreach (var slot in _slotsMis)
            {
                if (slot.IsOptional)
                    break;
                count++;
            }
            CountSlotsMisMin = count;
        }

        internal protected override ArgTraits GetArgTraitsCore(MultiFormOper<TCookie> parent, int carg, NameTuple names, BitSet implicitNames, DirTuple dirs)
        {
            Validation.Assert(SupportsArity(carg));
            Validation.Assert(names.IsDefault || names.Length == carg);
            Validation.Assert(!implicitNames.TestAtOrAbove(names.Length));
            Validation.Assert(dirs.IsDefault || dirs.Length == carg);

            int limSel = GetLimSel(1 + _slotsMis.Length, dirs);
            // This form requires at least one selector.
            if (limSel < 2)
                limSel = 2;
            var maskAll = BitSet.GetMask(carg);
            var maskMis = maskAll & BitSet.GetMask(1, limSel);
            var maskTop = maskAll & BitSet.GetMask(limSel, limSel + _slotsTop.Length);

            // REVIEW: Should names be allowed on the mis selector slots?
            var maskName = maskTop | maskMis;

            // REVIEW: Whether this processes the sequence lazily depends on the function semantics.
            return ArgTraits.CreateGeneral(parent, carg,
                scopeKind: ScopeKind.SeqItem, maskScope: 0x1, maskNested: maskMis,
                maskName: maskName, maskNameExp: maskName, maskLazySeq: 0x0);
        }

        internal protected override IEnumerable<SlotInfo> GetSlots()
        {
            yield return _slotSrc;
            foreach (var slot in _slotsMis)
                yield return slot;
            foreach (var slot in _slotsTop)
                yield return slot;
        }

        protected override Immutable.Array<DType> SpecializeTypesCore(MultiFormOper<TCookie> parent, InvocationInfo info, Action<BaseDiagnostic> errorSink)
        {
            Validation.Assert(SupportsArity(info.Arity));

            var bldr = Immutable.Array.CreateBuilder<DType>(info.Arity);
            DType typeSrc = info.Args[0].Type;
            RexlOper.EnsureTypeSeq(ref typeSrc);
            bldr.Add(typeSrc);

            bool seenName = false;
            int limSel = GetLimSel(Math.Min(1 + _slotsMis.Length, info.Arity), info.Dirs);
            // This form requires at least one selector.
            if (limSel < 2)
                limSel = 2;
            SpecializeTypesSlots(parent, errorSink, TypeMis.ItemTypeOrThis, _slotsMis, _allowSingleMis, limSel, info, ref seenName, bldr);
            SpecializeTypesSlots(parent, errorSink, TypeIn, _slotsTop, _allowSingleTop, info.Arity, info, ref seenName, bldr);
            return bldr.ToImmutable();
        }

        protected override BoundNode ReduceCore(MultiFormOper<TCookie> parent, IReducer reducer, BndCallNode call, MergeInfo merge)
        {
            Validation.AssertValue(parent);
            Validation.AssertValue(reducer);
            Validation.AssertValue(call);
            Validation.Assert(parent.IsValidCall(call));
            Validation.AssertValueOrNull(merge);

            // REVIEW: For now, we just assume that the selector part has a full set of args. Eventually, it might be nice
            // to figure out how to relax that, perhaps via a syntax like: F(src, s1, s2, ... | a1, a2, ...), where "|" or ";"
            // or some other reasonable token is used to separate the two arg ranges.
            int iargLimSel = Math.Min(call.Args.Length, 1 + _slotsMis.Length);
            if (!TryCreateRecord(out var selector, call.Args, call.Names, 1, iargLimSel, TypeMis.ItemTypeOrThis, _slotsMis, _allowSingleMis))
                return call;

            var typeMis = selector.Type.ToSequence();
            if (!TypeMis.Includes(typeMis))
            {
                Validation.Assert(false, "bad call");
                return call;
            }

            var typeIn = TypeIn;
            if (TypeMis != typeMis)
                typeIn = typeIn.SetNameType(NameMis, typeMis);

            ArgScope scopeSrc = null;
            BoundNode seq;
            if (merge != null)
            {
                Validation.Assert(call.Args[0].Type == merge.TypeSrc);
                scopeSrc = ArgScope.Create(ScopeKind.With, merge.TypeSrc);
                seq = BndScopeRefNode.Create(scopeSrc);
            }
            else
                seq = call.Args[0];

            var map = BndCallNode.Create(ForEachFunc.ForEach, typeMis, ArgTuple.Create(seq, selector), call.Scopes);
            var items = NamedItems.Empty.SetItem(NameMis, map);

            if (!TryCreateRecord(out var rec, call.Args, call.Names, iargLimSel, call.Args.Length, typeIn, _slotsTop, _allowSingleTop, items))
                return call;

            typeIn = rec.Type;
            if (!TypeIn.Includes(typeIn))
            {
                Validation.Assert(false, "bad call");
                return call;
            }

            var exec = parent.EnsureExec(this, typeIn, merge);
            Validation.Assert(exec.Form == this);
            Validation.Assert(exec.Merges == (merge != null));
            if (call.Type != exec.TypeCall)
            {
                Validation.Assert(false, "bad call");
                return call;
            }

            if (!exec.Merges)
            {
                // Case 6a: F(src, [m1:] s1, [m2:] s2, ..., [n1:] a1, [n2:] a2, ...) => G({ mis: Map(src, { m1: s1, m2: s2, ... }), n1: a1, n2: a2, ... }).
                return reducer.Reduce(BndCallNode.Create(exec, call.Type, ArgTuple.Create(rec)));
            }
            else if (exec.IsFunc && exec.MergeInfo.TypeMrg == exec.TypeDst)
            {
                // Case 6c: Guard(y: src, H(x:y, { mis: Map(x, { m1: s1, m2: s2, ... }), n1: a1, n2: a2, ... }, x).
                var scopeGuard = ArgScope.Create(ScopeKind.Guard, exec.TypeSrc);
                var h = BndCallNode.Create(exec, exec.TypeCall,
                    ArgTuple.Create(BndScopeRefNode.Create(scopeGuard), rec, BndScopeRefNode.Create(scopeSrc)), ScopeTuple.Create(scopeSrc));
                var g = BndCallNode.Create(WithFunc.Guard, exec.TypeCall,
                    ArgTuple.Create(call.Args[0], h), ScopeTuple.Create(scopeGuard));
                return reducer.Reduce(g);
            }
            else
            {
                // Case 6b: F(src, [m1:] s1, [m2:] s2, ..., [n1:] a1, [n2:] a2, ...) => H(x:src, { mis: Map(x, { m1: s1, m2: s2, ... }), n1: a1, n2: a2, ... }, x).
                var h = BndCallNode.Create(exec, exec.TypeCall,
                    ArgTuple.Create(call.Args[0], rec, BndScopeRefNode.Create(scopeSrc)), ScopeTuple.Create(scopeSrc));
                return reducer.Reduce(h);
            }
        }
    }

    /// <summary>
    /// Specifies information for a "slot" in an expanded <see cref="InvocationForm"/>. Contains the name and type of the
    /// slot, as well as whether the slot is optional, and, if so, what its default value is. Whether a slot is optional
    /// is independent of whether its <see cref="DType"/> is an opt type.
    /// </summary>
    public abstract class SlotInfo
    {
        // Cache the MethodInfo for CreateOpt<>.
        private static readonly MethodInfo MethCreateOpt = new Func<DType, DName, object, bool, SlotInfo>(CreateOpt)
            .Method.GetGenericMethodDefinition();

        protected static readonly ReadOnly.Dictionary<DKind, Type> _mapKindToType =
            new Dictionary<DKind, Type>()
            {
                { DKind.Text, typeof(string) },
                { DKind.Bit, typeof(bool) },
                { DKind.R8, typeof(double) },
                { DKind.R4, typeof(float) },
                { DKind.IA, typeof(BigInteger) },
                { DKind.I8, typeof(long) },
                { DKind.I4, typeof(int) },
                { DKind.I2, typeof(short) },
                { DKind.I1, typeof(sbyte) },
                { DKind.U8, typeof(ulong) },
                { DKind.U4, typeof(uint) },
                { DKind.U2, typeof(ushort) },
                { DKind.U1, typeof(byte) },
            };

        /// <summary>
        /// The type of the slot.
        /// </summary>
        public DType Type { get; }

        /// <summary>
        /// The name of the slot. May be default.
        /// </summary>
        public DName Name { get; }

        /// <summary>
        /// Whether the slot allows a single value to sequence transformation.
        /// </summary>
        public bool AllowSingle { get; }

        /// <summary>
        /// Whether the slot is optional.
        /// </summary>
        public abstract bool IsOptional { get; }

        /// <summary>
        /// If the slot is optional, the default value used when unspecified.
        /// </summary>
        public abstract object DefaultValue { get; }

        protected SlotInfo(DType type, DName name, bool allowSingle)
        {
            Validation.Assert(!allowSingle || type.IsSequence);
            Type = type;
            Name = name;
            AllowSingle = allowSingle;
        }

        public static SlotInfo Create(DType type, DName name, bool isOptional, object defaultValue, bool allowSingle)
        {
            Validation.BugCheckParam(!allowSingle || type.IsSequence, nameof(allowSingle));
            if (!isOptional)
            {
                Validation.Assert(defaultValue == null);
                return new SlotInfoReq(type, name, allowSingle);
            }

            Validation.BugCheck(defaultValue != null || type.IsOpt, "Non-opt type can't have null default value");

            if (defaultValue == null)
                return new SlotInfoOptNull(type, name, allowSingle);

            // REVIEW: Ideally we'd have a TypeManager for this, but rexl functions aren't supposed to be
            // tied to a particular type manager instance....
            Validation.BugCheck(_mapKindToType.TryGetValue(type.Kind, out Type st), "Unsupported type for default value");
            if (type.HasReq)
                st = typeof(Nullable<>).MakeGenericType(st);

            Validation.BugCheckParam(st.IsAssignableFrom(defaultValue.GetType()), nameof(defaultValue),
                "Default value is of the wrong type");

            var meth = MethCreateOpt.MakeGenericMethod(st);
            var ret = (SlotInfo)meth.Invoke(null, new object[] { type, name, defaultValue, allowSingle });
            return ret;
        }

        private static SlotInfo CreateOpt<T>(DType type, DName name, T defaultValue, bool allowSingle)
        {
            return new SlotInfoOptNonNull<T>(type, name, defaultValue, allowSingle);
        }
    }

    /// <summary>
    /// Concrete type for a required (non-optional) slot. Whether a slot is optional is independent
    /// of whether its _type_ is an opt type.
    /// </summary>
    protected sealed class SlotInfoReq : SlotInfo
    {
        public override bool IsOptional => false;
        public override object DefaultValue => null;

        public SlotInfoReq(DType type, DName name, bool allowSingle)
            : base(type, name, allowSingle)
        {
        }
    }

    /// <summary>
    /// Abstract type for an optional slot. Whether a slot is optional is independent of whether its
    /// _type_ is an opt type.
    /// </summary>
    protected abstract class SlotInfoOpt : SlotInfo
    {
        public override bool IsOptional => true;

        protected SlotInfoOpt(DType type, DName name, bool allowSingle)
            : base(type, name, allowSingle)
        {
        }
    }

    /// <summary>
    /// Concrete type for an optional slot with a null default value.
    /// </summary>
    protected sealed class SlotInfoOptNull : SlotInfoOpt
    {
        public override object DefaultValue => null;

        public SlotInfoOptNull(DType type, DName name, bool allowSingle)
            : base(type, name, allowSingle)
        {
            Validation.BugCheck(type.IsOpt);
        }
    }

    /// <summary>
    /// Concrete type for an optional slot with a nonnull default value.
    /// </summary>
    protected sealed class SlotInfoOptNonNull<T> : SlotInfoOpt
    {
        public readonly T DefValue;
        public override object DefaultValue => DefValue;

        public SlotInfoOptNonNull(DType type, DName name, T defaultValue, bool allowSingle)
            : base(type, name, allowSingle)
        {
            Validation.BugCheck(defaultValue != null);

            Validation.BugCheck(_mapKindToType.TryGetValue(Type.Kind, out Type st));
            if (Type.HasReq)
                st = typeof(Nullable<>).MakeGenericType(st);
            Validation.BugCheck(typeof(T) == st);

            DefValue = defaultValue;
        }
    }

    /// <summary>
    /// Base class for merging related information.
    /// REVIEW: For now, this assumes "item merging", rather than any more general notion.
    /// REVIEW: Should this contain a back reference to the form or the Mis and Mos information?
    /// </summary>
    public abstract partial class MergeInfo
    {
        /// <summary>
        /// The source sequence type.
        /// </summary>
        public readonly DType TypeSrc;

        /// <summary>
        /// The merged sequence type.
        /// </summary>
        public readonly DType TypeMrg;

        /// <summary>
        /// The final return type.
        /// </summary>
        public readonly DType TypeDst;

        protected MergeInfo(DType typeSrc, DType typeMrg, DType typeDst)
        {
            Validation.Assert(typeSrc.IsSequence);
            Validation.Assert(typeMrg.IsValid);
            Validation.Assert(typeDst.IsValid);

            TypeSrc = typeSrc;
            TypeMrg = typeMrg;
            TypeDst = typeDst;
        }
    }

    /// <summary>
    /// Merges src and mos records by unioning the records into a single record. The renames dictionary
    /// indicates mos fields that are renamed. Requires that all fields in typeSrc are in typeMrg.
    /// </summary>
    public sealed partial class RecordMergeInfo : MergeInfo
    {
        private readonly ReadOnly.Dictionary<DName, DName> _renames;

        internal RecordMergeInfo(DType typeSrc, DType typeMrg, DType typeDst, Dictionary<DName, DName> renames)
            : base(typeSrc, typeMrg, typeDst)
        {
            Validation.Assert(typeSrc.IsTableReq);
            Validation.Assert(typeMrg.IsTableReq);
            Validation.Assert(typeSrc.GetNames().All(tn => tn.Type == typeMrg.GetNameTypeOrDefault(tn.Name)));
            Validation.Assert(typeDst == typeMrg || typeDst.IsRecordReq);
            Validation.AssertValueOrNull(renames);
            _renames = renames;
        }

        public DName MosToMrgName(DName name)
        {
            if (!_renames.IsDefault && _renames.TryGetValue(name, out DName nameMrg))
                return nameMrg;
            return name;
        }
    }

    /// <summary>
    /// An <see cref="ArgTraits"/> wrapper which additionally contains information about
    /// which <see cref="InvocationForm"/> produced it.
    /// </summary>
    public class ArgTraitsMff : ArgTraits
    {
        /// <summary>
        /// The actual <see cref="ArgTraits"/> produced by the form.
        /// </summary>
        public ArgTraits Inner { get; }

        /// <summary>
        /// Potential forms which may match a given argument list.
        /// </summary>
        public Immutable.Array<InvocationForm> Candidates { get; }

        /// <summary>
        /// The index of the specific form in question.
        /// </summary>
        public int IndexForm { get; }

        /// <summary>
        /// The current form in question.
        /// </summary>
        public InvocationForm FormCur => Candidates[IndexForm];

        /// <summary>
        /// Whether there was an error detected at the parse level,
        /// meaning that the provided names and directives do not
        /// allow for any matching forms. In which case, <see cref="Candidates"/>
        /// will only have one element which at least supports the arity
        /// of the provided argument list, to use for error messages.
        /// </summary>
        public bool ParseError { get; }

        public override int ScopeCount => Inner.ScopeCount;

        public override BitSet MaskLiftSeq => Inner.MaskLiftSeq;

        public override BitSet MaskLiftTen => Inner.MaskLiftTen;

        public override BitSet MaskLiftOpt => Inner.MaskLiftOpt;

        public override int NestedCount => Inner.NestedCount;

        public override bool SupportDottedImplicitNames => Inner.SupportDottedImplicitNames;

        public override ScopeKind GetScopeKind(int slot) => Inner.GetScopeKind(slot);

        public override bool IsNested(int slot) => Inner.IsNested(slot);

        public override bool IsScope(int slot) => Inner.IsScope(slot);

        public override bool IsScope(int slot, out int iscope) => Inner.IsScope(slot, out iscope);

        public override bool IsScopeActive(int slot, int upCount) => Inner.IsScopeActive(slot, upCount);

        public override bool IsEagerSeq(int slot)
        {
            return Inner.IsEagerSeq(slot);
        }

        public override bool RequiresName(int slot) => Inner.RequiresName(slot);

        public override bool SupportsImplicitName(int slot) => Inner.SupportsImplicitName(slot);

        public override bool SupportsName(int slot) => Inner.SupportsName(slot);

        private ArgTraitsMff(ArgTraits inner, Immutable.Array<InvocationForm> candidates, int ind, bool err)
            : base(inner.VerifyValue().Oper, inner.SlotCount)
        {
            Validation.AssertIndex(ind, candidates.Length);
            Inner = inner;
            Candidates = candidates;
            IndexForm = ind;
            ParseError = err;
        }

        public static ArgTraitsMff Create(ArgTraits traits, Immutable.Array<InvocationForm> candidates, int ind, bool err)
        {
            Validation.CheckValue(traits, nameof(traits));
            Validation.Check(candidates.Length > 0, nameof(candidates));
            Validation.CheckIndex(ind, candidates.Length, nameof(ind));
            return new ArgTraitsMff(traits, candidates, ind, err);
        }

        public override bool AreEquivalent(ArgTraits cmp)
        {
            if (this == cmp)
                return true;

            if (!(cmp is ArgTraitsMff cmpMff))
                return false;
            if (ParseError != cmpMff.ParseError)
                return false;
            if (IndexForm != cmpMff.IndexForm)
                return false;
            if (Candidates.Length == cmpMff.Candidates.Length)
                return false;
            for (int i = 0; i < Candidates.Length; i++)
            {
                if (Candidates[i] != cmpMff.Candidates[i])
                    return false;
            }

            if (!Inner.AreEquivalent(cmpMff.Inner))
                return false;

            return true;
        }

    }

    /// <summary>
    /// An <see cref="ArgTraits"/> for <see cref="ExecutionOper"/> implementations
    /// that merge. The first slot is a With scope for the source sequence, and
    /// the rest are nested in it.
    /// </summary>
    protected sealed class ArgTraitsExecMerge : ArgTraits
    {
        public override int ScopeCount => 1;

        public override BitSet MaskLiftSeq => default;

        public override BitSet MaskLiftTen => default;

        public override BitSet MaskLiftOpt => BitSet.GetMask(1);

        public override int NestedCount => SlotCount - 1;

        public override bool SupportDottedImplicitNames => false;

        private ArgTraitsExecMerge(ExecutionOper exec, int arity)
            : base(exec, arity)
        {
            Validation.AssertValue(exec);
            Validation.Assert(exec is ExecutionOper);
            Validation.Assert(arity >= 2);
        }

        public static ArgTraitsExecMerge Create(ExecutionOper exec, int arity)
        {
            Validation.BugCheckValue(exec, nameof(exec));
            Validation.BugCheck(arity >= 2, nameof(arity));
            return new ArgTraitsExecMerge(exec, arity);
        }

        public override bool AreEquivalent(ArgTraits cmp)
        {
            if (this == cmp)
                return true;
            if (cmp is not ArgTraitsExecMerge)
                return false;
            if (Oper != cmp.Oper)
                return false;
            if (SlotCount != cmp.SlotCount)
                return false;
            return true;
        }

        public override ScopeKind GetScopeKind(int slot)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
            return slot == 0 ? ScopeKind.With : ScopeKind.None;
        }

        public override bool IsEagerSeq(int slot)
        {
            // REVIEW: Whether this processes the sequence lazily depends on the function semantics.
            return true;
        }

        public override bool IsNested(int slot)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
            return slot > 0;
        }

        public override bool IsScope(int slot)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
            return slot == 0;
        }

        public override bool IsScope(int slot, out int iscope)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
            if (slot == 0)
            {
                iscope = 0;
                return true;
            }
            iscope = -1;
            return false;
        }

        public override bool IsScopeActive(int slot, int upCount)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
            return true;
        }

        public override bool RequiresName(int slot)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
            return false;
        }

        public override bool SupportsImplicitName(int slot)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
            return false;
        }

        public override bool SupportsName(int slot)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
            return false;
        }
    }
}

public enum MosKind
{
    /// <summary>
    /// No mos.
    /// </summary>
    None,

    /// <summary>
    /// Every output in the mos is in correspondence with the items in the mis.
    /// I.E. they are exactly parallel sequences.
    /// </summary>
    OneOne,

    /// <summary>
    /// Every input item in the mis can produce multiple (or 0) output items.
    /// When merging, inputs with 0 output items will not appear in the final merged output.
    /// Hence an "inner join" merge.
    /// </summary>
    OneManyInner,

    /// <summary>
    /// Every input item in the mis can produce multiple (or 0) output items.
    /// When merging, inputs with 0 output items will be merged with a default value.
    /// Hence an "outer join" merge.
    /// </summary>
    OneManyOuter
}

public static class MosKindUtil
{
    public static bool HasMos(this MosKind kind)
    {
        return kind != MosKind.None;
    }

    public static bool IsOneMany(this MosKind kind)
    {
        return kind == MosKind.OneManyInner || kind == MosKind.OneManyOuter;
    }
}
