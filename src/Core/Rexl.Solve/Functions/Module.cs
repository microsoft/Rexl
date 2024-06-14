// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

public sealed partial class ModuleOptimizeFunc : RexlOper
{
    public static readonly NPath ModuleNs = NPath.Root.Append(new DName("Module"));

    /// <summary>
    /// The kind of func, whether minimize, maximize, optimize (with an extra isMax arg), or
    /// the <see cref="Opt"/> private instance used for code gen.
    /// </summary>
    public enum OptKind : byte
    {
        Minimize,
        Maximize,

        /// <summary>
        /// This one takes a bool parameter (in the third slot) with true meaning maximize
        /// and false meaning minimize.
        /// REVIEW: Perhaps the max/min bool should have the module in scope?
        /// </summary>
        Optimize,

        /// <summary>
        /// This one is used for code gen. It should not be in the func registry. It doesn't have a "with" scope
        /// for the module parameter and use a string/text value for the measure parameter, rather than a
        /// "lambda" expression.
        /// </summary>
        Opt,
    }

    public static readonly ModuleOptimizeFunc Minimize = new ModuleOptimizeFunc("Minimize", OptKind.Minimize, 2);
    public static readonly ModuleOptimizeFunc Maximize = new ModuleOptimizeFunc("Maximize", OptKind.Maximize, 2);
    public static readonly ModuleOptimizeFunc Optimize = new ModuleOptimizeFunc("Optimize", OptKind.Optimize, 3);

    // This one is used for code gen. It uses a text value for the measure rather than a lambda.
    private static readonly ModuleOptimizeFunc Raw = new ModuleOptimizeFunc("Opt", OptKind.Opt, 3);

    public OptKind Kind { get; }

    // REVIEW: Add support for a solver options record (eventually).
    private ModuleOptimizeFunc(string name, OptKind kind, int arityMin)
        : base(isFunc: true, new DName(name), BindUtil.ModuleNs, arityMin, arityMin + 1)
    {
        Kind = kind;
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));

        if (Kind == OptKind.Opt)
            return ArgTraitsSimple.Create(this, eager: true, carg);

        // The public ones have a "with" scope for the module arg that is active in the measure arg.
        // REVIEW: Might it be worth supporting a more general expression rather than requiring
        // just a measure reference?
        return new Traits(this, carg);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));
        Validation.Assert(Kind != OptKind.Opt);

        var types = info.GetArgTypes();
        var typeMod = types[0];
        if (!typeMod.IsModuleReq)
        {
            info.PostDiagnostic(RexlDiagnostic.Error(info.GetParseArg(0), ErrorStrings.ErrNotModule));
            typeMod = DType.EmptyModuleReq;
        }
        else if (info.Args[1] is not BndGetFieldNode bgf || bgf.Record is not BndModToRecNode bmtr ||
            bmtr.Child is not BndScopeRefNode bsr || bsr.Scope != info.Scopes[0])
        {
            // The measure arg needs to be just a reference to a measure symbol in the module. Otherwise,
            // we can't (currently) do code gen.
            info.PostDiagnostic(MessageDiag.Error(ErrorStrings.ErrModuleOptNeedMeasure));
        }
        else if (!typeMod.TryGetSymbolNameType(bgf.Name, out _, out var sk) || sk != ModSymKind.Measure)
        {
            // REVIEW: Perhaps use a different error for no such symbol vs symbol is not a measure.
            info.PostDiagnostic(MessageDiag.Error(ErrorStrings.ErrModuleUnknownMeasure_Name, bgf.Name));
        }

        types[0] = typeMod;
        if (!types[1].IsNumericXxx)
            types[1] = DType.R8Req;
        if (ArityMin == 3)
            types[2] = DType.BitReq;
        if (types.Count > ArityMin)
            types[ArityMin] = DType.Text;

        return (typeMod.ToOpt(), types.ToImmutable());
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var typeMod = call.Type;

        if (!typeMod.IsModuleOpt)
            return false;

        // Because slot 0 has a scope, the binder can't cast it, so we can't insist on the first
        // arg having module type.
        var typeSrc = call.Args[0].Type;
        if (!typeSrc.IsModuleReq)
            full = false;
        else if (typeSrc.ToOpt() != typeMod)
            return false;

        if (ArityMin == 3 && call.Args[2].Type != DType.BitReq)
            return false;
        if (call.Args.Length > ArityMin && call.Args[ArityMin].Type != DType.Text)
            return false;

        // ReduceCore validates the measure parameter and if it is good, translates to an invocation of
        // the raw instance. Non-raw instances should not be used for code gen.
        if (Kind != OptKind.Opt)
            full = false;
        else if (call.Args[1] is not BndStrNode bsn || !DName.TryWrap(bsn.Value, out var name) ||
            !typeMod.TryGetSymbolNameType(name, out var typeMsr, out var sk) || sk != ModSymKind.Measure)
        {
            return false;
        }

        return true;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        if (Kind == OptKind.Opt)
            return call;

        // For the public instances, the module arg has a "with" scope that is active in the
        // measure slot. If the measure expr/lambda is a reference to a measure in the module,
        // we reduce to an invocation of the "Opt" instance, with the measure name as a text
        // value. This simplifies code gen.
        Validation.Assert(call.Scopes.Length == 1);

        var args = call.Args;
        var typeSrc = args[0].Type;
        if (!typeSrc.IsModuleReq)
            return call;
        Validation.Assert(typeSrc.ToOpt() == call.Type);

        // If the measure slot is not a reference to a measure symbol in the module, we can't reduce.
        if (args[1] is not BndGetFieldNode bgf || bgf.Record is not BndModToRecNode bmtr ||
            bmtr.Child is not BndScopeRefNode bsr || bsr.Scope != call.Scopes[0] ||
            !typeSrc.TryGetSymbolNameType(bgf.Name, out _, out var sk) || sk != ModSymKind.Measure)
        {
            return call;
        }

        int dc = Raw.ArityMin - ArityMin;
        Validation.Assert(0 <= dc & dc <= 1);
        int carg = args.Length + dc;
        var bldr = Immutable.Array<BoundNode>.CreateBuilder(carg, init: true);
        bldr[0] = args[0];
        // For the measure, use the name of the symbol.
        bldr[1] = BndStrNode.Create(bgf.Name.Value);

        // Include a bool isMax arg.
        switch (Kind)
        {
        case OptKind.Optimize:
            bldr[2] = args[2];
            break;
        case OptKind.Minimize:
            bldr[2] = BndIntNode.False;
            break;
        case OptKind.Maximize:
            bldr[2] = BndIntNode.True;
            break;
        default:
            Validation.Assert(false);
            return call;
        }
        Validation.Assert(bldr[2].Type == DType.BitReq);

        // Copy over any extra args. Today that is just an optional solver name. Soon we'll also
        // support an optional solver options record.
        for (int i = carg; --i >= 3;)
            bldr[i] = args[i - dc];

        return BndCallNode.Create(Raw, call.Type, bldr.ToImmutable());
    }

    protected override PullWithFlags GetPullWithFlagsCore(BndCallNode call, int iarg)
    {
        Validation.Assert(IsValidCall(call));

        // Don't pull a "With" out of the measure arg.
        return iarg == 1 ? PullWithFlags.None : PullWithFlags.With;
    }

    private sealed class Traits : ArgTraitsBare
    {
        public Traits(ModuleOptimizeFunc parent, int carg)
            : base(parent, carg)
        {
        }

        // Lift the module arg over opt.
        public override BitSet MaskLiftOpt => 0x01;

        public override int ScopeCount => 1;

        public override int NestedCount => 1;

        public override bool AreEquivalent(ArgTraits cmp)
        {
            if (this == cmp)
                return true;
            if (cmp is not Traits other)
                return false;
            if (Oper != other.Oper)
                return false;
            if (SlotCount != other.SlotCount)
                return false;
            return true;
        }

        public override ScopeKind GetScopeKind(int slot)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
            return slot == 0 ? ScopeKind.With : ScopeKind.None;
        }

        public override bool IsNested(int slot)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
            return slot == 1;
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
            Validation.BugCheckIndex(upCount, ScopeCount, nameof(upCount));

            if (slot == 1 && upCount == 0)
                return true;
            return false;
        }
    }

    protected override bool IsUnboundedCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));
        return true;
    }
}
