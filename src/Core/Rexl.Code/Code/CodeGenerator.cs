// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sequence;

namespace Microsoft.Rexl.Code;

using ArgTuple = Immutable.Array<BoundNode>;
using BndTuple = Immutable.Array<BoundNode>;
using Conditional = System.Diagnostics.ConditionalAttribute;
using Date = RDate;
using GlobalFunc = Func<object[], object>;
using GlobalTuple = Immutable.Array<GlobalInfo>;
using IE = IEnumerable<object>;
using Integer = System.Numerics.BigInteger;
using LocEnt = MethodGenerator.Local;
using RngTuple = Immutable.Array<SlotRange>;
using RunnerFunc = Func<object[], ActionHost, ActionRunner>;
using ScopeMap = Dictionary<int, LocArgInfo>;
using Time = TimeSpan;

/// <summary>
/// IL code generator.
/// </summary>
public abstract partial class CodeGeneratorBase
{
    private readonly GeneratorRegistry _gens;

    public TypeManager TypeManager { get; }

    protected CodeGeneratorBase(TypeManager typeManager, GeneratorRegistry gens)
    {
        Validation.BugCheckValue(typeManager, nameof(typeManager));
        Validation.BugCheckValue(gens, nameof(gens));

        TypeManager = typeManager;
        _gens = gens;
    }

    // REVIEW: No good way to attach the BoundFormula to CodeGenResult without having
    // a form that takes a BoundFormula as input. Augment the API with a new method accepting one?
    /// <summary>
    /// Runs the code generator on the given bound tree. Returns a <see cref="CodeGenResult"/>
    /// containing the output of the code generation process.
    /// </summary>
    public abstract CodeGenResult Run(BoundNode tree, CodeGenHost? host = null,
        Action<string> ilSink = null, ILLogKind logKind = ILLogKind.None);

    /// <summary>
    /// Get the code generator for the given operation, or <c>null</c> if there isn't one.
    /// </summary>
    public virtual RexlOperationGenerator GetOperGenerator(RexlOper oper)
    {
        Validation.AssertValue(oper);

        if (_gens.TryGet(oper, out var gen))
        {
            Validation.AssertValue(gen);
            return gen;
        }

        return null;
    }

    /// <summary>
    /// Returns whether the indicated <paramref name="call"/> needs the
    /// execution context.
    /// </summary>
    public virtual bool NeedsExecCtx(BndCallNode call)
    {
        var gen = GetOperGenerator(call.Oper);
        if (gen is not null)
            return gen.NeedsExecCtx(call);

        switch (call.Oper)
        {
        case SortFunc: return true;

        // REVIEW: Perhaps we should change the binder to produce specialized nodes for
        // With/Guard invocations.
        case WithFunc: return false;

        // These shouldn't be in a BndCallNode. The binder produces specialized
        // nodes for them.
        case GroupByFunc: return false;
        case SetFieldsFunc: return false;
        }

        return false;
    }

    private protected abstract partial class Impl : ScopedBoundTreeVisitor, ICodeGen
    {
        /// <summary>
        /// This class represents a dynamic method that is being generated. There are two kinds of frames,
        /// the "root" frame and "child" frames. Code gen for globals and constants depends on the kind
        /// of frame.
        /// 
        /// Frames are processed in bound tree traversal order, so multiple of these can be active simultaneously
        /// and are tracked via a stack. The top of the stack is stored in <see cref="_frameCur"/>.
        /// 
        /// The parent frame handles the inner one as a delegate object that wraps a "this" value and the inner
        /// frame's dynamic method. Note that the inner/child dynamic method is "constant" (the IL doesn't
        /// depend on the parameters to the top-level delegate).
        /// 
        /// In some cases, the "this" value for the inner delegate object is also constant, in which case the
        /// entire child delegate object is constant so the delegate object is created at code gen time and
        /// stored in the parent's constants array.
        /// 
        /// In other cases, the "this" value for the inner delegate must include some "curried" state, like the
        /// <see cref="ExecCtx"/>, or globals, or outer locals/parameters. In that case, the parent code
        /// "assembles" the "this" object and creates a delegate that wraps the assembled "this" object together
        /// with the (constant) dynamic method (which is loaded from the parent's constants array).
        /// 
        /// For the root frame:
        /// <list type="bullet">
        /// <item>"constants" are loaded from an object[] that is the 0th arg (the "this" value) of the
        ///   generated method.</item>
        /// <item>Globals and <see cref="ExecCtx"/> are loaded directly from an arg with index >= 1.</item>
        /// </list>
        /// 
        /// For child frames that don't need globals, external scopes/locals, or an execution context, the 0th
        /// arg is the constants object array (same as for root frames). Otherwise, the 0th arg is object[] with
        /// between 2 and 4 slots depending on what is needed (with unneeded slots null or missing):
        /// <list type="bullet">
        /// <item>[0] contains the constants array</item>
        /// <item>[1] contains the execution context</item>
        /// <item>[2] contains the curried globals record object</item>
        /// <item>[3] contains the curried scopes record object</item>
        /// </list>
        /// </summary>
        protected abstract class FrameInfo
        {
            /// <summary>
            /// The assigned id number, used for asserts.
            /// </summary>
            public readonly int Id;

            /// <summary>
            /// Whether this is the outer-most frame.
            /// </summary>
            public abstract bool IsRoot { get; }

            /// <summary>
            /// Whether this is for a procedure, rather than a pure expression. This is only true when
            /// <see cref="IsRoot"/> is also true.
            /// </summary>
            public abstract bool IsProc { get; }

            /// <summary>
            /// Whether this frame needs globals, including "this" but NOT including "execution context".
            /// </summary>
            public abstract bool UsesGlobals { get; }

            /// <summary>
            /// Whether this frame needs the execution context. When this is true, <see cref="UsesGlobals"/>
            /// is also true. That is, the execution context counts as a "global".
            /// </summary>
            public abstract bool UsesExecCtx { get; }

            /// <summary>
            /// Whether this frame needs any external scope values.
            /// </summary>
            public abstract bool UsesExtScopes { get; }

            /// <summary>
            /// Returns true if the constants array is the "this" value of the method being generated.
            /// This is true if this is a root frame or the method needs no globals, exec ctx or external
            /// scopes.
            /// </summary>
            public abstract bool SimpleConsts { get; }

            /// <summary>
            /// The top scope level when this frame is "created" / "entered". Used for asserts.
            /// </summary>
            public readonly RefMaps.ScopeInfo TopScope;

            /// <summary>
            /// The method generator for this frame.
            /// </summary>
            public readonly MethodGenerator Meth;

            /// <summary>
            /// The return system type.
            /// </summary>
            public readonly Type RetSysType;

            /// <summary>
            /// The resulting delegate type.
            /// </summary>
            public readonly Type DelegateType;

            /// <summary>
            /// Maps from scope to local/argument info. Note that this dictionary can't be readonly,
            /// because it can be modified by non-map scopes, for example, SetFields and With. Also
            /// note that it is created lazily.
            /// </summary>
            public ScopeMap ScopeMap;

            /// <summary>
            /// Tracks the contents of the execution stack.
            /// </summary>
            private readonly List<Type> _stack;

            /// <summary>
            /// This is the list of pre-constructed objects that are referenced by the generated frame IL.
            /// For example, some of these will be delegates, or DynamicMethods, or IEqualityComparer instances,
            /// boxed <see cref="Integer"/> values, child constants arrays, etc.
            /// </summary>
            private List<object> _consts;

            /// <summary>
            /// Maps from value to the constant slot used to store the value.
            /// </summary>
            private Dictionary<object, int> _valToConstSlot;

            /// <summary>
            /// Stack of null-handling Labels for code gen of Guard.
            /// </summary>
            public List<(BoundNode owner, Label label)> NullLabels;

            protected FrameInfo(Impl impl, string name, ILLogKind logKind, Type stRet, ReadOnly.Array<Type> stsArgs)
            {
                Validation.AssertValue(impl);
                Validation.AssertNonEmpty(name);
                Validation.AssertValue(stRet);
                Validation.AssertAllValues(stsArgs);

                _stack = new List<Type>();

                Id = impl._idFrameNext++;
                TopScope = impl.GetTopScope();

                Type[] stsMeth = new Type[1 + stsArgs.Length];
                stsArgs.Copy(0, stsArgs.Length, stsMeth, 1);
                stsMeth[0] = typeof(object[]);
                Meth = new MethodGenerator(name, logKind, stRet, stsMeth);
                RetSysType = stRet;
                DelegateType = GetFuncType(stRet, stsArgs);
            }

            /// <summary>
            /// Lookup or allocate a slot for the given constant object in the constants array.
            /// </summary>
            public int EnsureConstSlot(object value)
            {
                Validation.Assert(Util.Size(_valToConstSlot) == Util.Size(_consts));

                // Null shouldn't be passed here. The IL should directly load null.
                Validation.AssertValue(value);

                // See if the same object is already in the constants list.
                if (_valToConstSlot == null || !_valToConstSlot.TryGetValue(value, out int slot))
                {
                    slot = Util.Size(_consts);
                    Util.Add(ref _consts, value);
                    Util.Add(ref _valToConstSlot, value, slot);
                    Validation.Assert(_valToConstSlot.Count == _consts.Count);
                }
                Validation.AssertIndex(slot, Util.Size(_consts));
                Validation.Assert(_consts[slot].Equals(value));
                Validation.Assert(_valToConstSlot.ContainsKey(value) && _valToConstSlot[value] == slot);
                return slot;
            }

            /// <summary>
            /// Lookup or allocate a slot for the given <see cref="Integer"/> value.
            /// </summary>
            public int EnsureIntegerSlot(Integer value)
            {
                // REVIEW: It would be nice to avoid boxing for the lookup, but it's probably not
                // worth having a separate dictionary for that purpose.
                return EnsureConstSlot((object)value);
            }

            /// <summary>
            /// Return the constants array. The result is null if there are no associated constants.
            /// </summary>
            public object[] GetConstants()
            {
                if (Util.Size(_consts) > 0)
                    return _consts.ToArray();
                return null;
            }

            /// <summary>
            /// Get the <see cref="GlobalInfo"/> instance and caching local for the indicated global.
            /// This should only be called on a root frame.
            /// </summary>
            public abstract (GlobalInfo glob, LocalBuilder loc) GetGlobalInfo(NPath name);

            /// <summary>
            /// Get the <see cref="GlobalInfo"/> instance and caching local for the execution context.
            /// This should only be called on a root frame.
            /// </summary>
            public abstract (GlobalInfo glob, LocalBuilder loc) GetExecCtxInfo();

            public void Push(Type st)
            {
                Validation.AssertValue(st);
                _stack.Add(st);
            }

            public Type Pop()
            {
                Validation.Assert(_stack.Count > 0);
                return _stack.Pop();
            }

            public Type Peek()
            {
                Validation.Assert(_stack.Count > 0);
                return _stack.Peek();
            }

            public int StackDepth => _stack.Count;
        }

        protected sealed class RootFrameInfo : FrameInfo
        {
            public override bool IsRoot => true;
            public override bool IsProc { get; }
            public override bool UsesGlobals => _globalMap != null;
            public override bool UsesExecCtx => _globCtx != null;
            public override bool UsesExtScopes => false;
            public override bool SimpleConsts => true;

            /// <summary>
            /// Maps from global name to <see cref="GlobalInfo"/> and <see cref="LocalBuilder"/>.
            /// </summary>
            private readonly Dictionary<NPath, (GlobalInfo glob, LocalBuilder loc)> _globalMap;

            /// <summary>
            /// The execution context info, if one is used.
            /// </summary>
            private readonly GlobalInfo _globCtx;
            private readonly LocalBuilder _locCtx;

            /// <summary>
            /// The constructor for the top level frame info.
            /// </summary>
            public RootFrameInfo(Impl impl, string name, ILLogKind logKind, GlobalTuple globals, bool isProc)
                : base(impl, name, logKind,
                      isProc ? typeof(ActionRunner) : typeof(object),
                      isProc ? new[] { typeof(object[]), typeof(ActionHost) } : new[] { typeof(object[]) })
            {
                Validation.Assert(!globals.IsDefault);

                IsProc = isProc;

                for (int slot = 0; slot < globals.Length; slot++)
                {
                    var glob = globals[slot];
                    Validation.Assert(glob.Slot == slot);
                    var loc = Meth.Il.DeclareLocalRaw(glob.SysType);
                    if (glob.IsCtx)
                    {
                        Validation.Assert(_globCtx == null);
                        Validation.Assert(glob.Name.IsRoot);
                        Validation.Assert(!glob.Type.IsValid);
                        _globCtx = glob;
                        _locCtx = loc;
                    }
                    else
                    {
                        Validation.Assert(glob.Type.IsValid);
                        Validation.Assert(_globalMap == null || !_globalMap.ContainsKey(glob.Name));
                        Util.Add(ref _globalMap, glob.Name, (glob, loc));
                    }
                }
            }

            public override (GlobalInfo glob, LocalBuilder loc) GetGlobalInfo(NPath name)
            {
                Validation.Assert(_globalMap != null);
                Validation.Assert(_globalMap.ContainsKey(name));
                var res = _globalMap[name];
                Validation.Assert(res.glob.Name == name);
                Validation.Assert(res.glob.Type.IsValid);
                return res;
            }

            public override (GlobalInfo glob, LocalBuilder loc) GetExecCtxInfo()
            {
                Validation.Assert(_globCtx != null);
                return (_globCtx, _locCtx);
            }
        }

        protected sealed class ChildFrameInfo : FrameInfo
        {
            // The slots in the 0th arg array, when more than just constants are needed.
            public const int SlotConst = 0;
            public const int SlotExCtx = 1;
            public const int SlotGlobs = 2;
            public const int SlotScope = 3;

            public override bool IsRoot => false;
            public override bool IsProc => false;
            public override bool UsesGlobals { get; }
            public override bool UsesExecCtx { get; }
            public override bool UsesExtScopes => ExtScopeTup.TupleArity > 0;
            public override bool SimpleConsts => !UsesGlobals && !UsesExecCtx && !UsesExtScopes;

            /// <summary>
            /// The external scopes needed at this level and their assigned slot in the <see cref="ExtScopeTup"/>.
            /// The key is a scope info index.
            /// </summary>
            public readonly ReadOnly.Dictionary<int, int> ExtScopeToSlot;

            /// <summary>
            /// The external scope tuple <see cref="DType"/>, as provided by the caller.
            /// The names of the fields are string encodings of the scope depth. This is
            /// the empty tuple type if <see cref="UsesExtScopes"/> is false.
            /// </summary>
            public readonly DType ExtScopeTup;

            /// <summary>
            /// The system type corresponding to <see cref="ExtScopeTup"/>. This is null if
            /// <see cref="UsesExtScopes"/> is false.
            /// </summary>
            public readonly Type ExtScopeTupSysType;

            public ChildFrameInfo(
                    Impl impl, string name, ILLogKind logKind, Type stRet, ReadOnly.Array<Type> stsArgs,
                    bool usesGlobals = false, bool usesExec = false,
                    ScopeMap scopeMap = null, Dictionary<int, RefMaps.ScopeInfo> extScopes = null)
                : base(impl, name, logKind, stRet, stsArgs)
            {
                Validation.AssertValueOrNull(scopeMap);
                Validation.AssertValueOrNull(extScopes);

                UsesGlobals = usesGlobals;
                UsesExecCtx = usesExec;
                ScopeMap = scopeMap;

                // Get the tuple type for curried scopes.
                if (Util.Size(extScopes) > 0)
                {
                    var scopeToSlot = new Dictionary<int, int>(extScopes.Count);
                    var types = Immutable.Array<DType>.CreateBuilder(extScopes.Count, init: true);
                    foreach (var kvp in extScopes)
                    {
                        Validation.Assert(!scopeToSlot.ContainsKey(kvp.Key));
                        int slot = scopeToSlot.Count;
                        scopeToSlot.Add(kvp.Key, slot);
                        types[slot] = kvp.Value.Scope.Type;
                    }

                    ExtScopeToSlot = scopeToSlot;
                    ExtScopeTup = DType.CreateTuple(opt: false, types.ToImmutable());
                    ExtScopeTupSysType = impl.GetSysType(ExtScopeTup);
                }
                else
                    ExtScopeTup = DType.EmptyTupleReq;
            }

            public override (GlobalInfo glob, LocalBuilder loc) GetGlobalInfo(NPath name)
            {
                throw Validation.BugExcept();
            }

            public override (GlobalInfo glob, LocalBuilder loc) GetExecCtxInfo()
            {
                throw Validation.BugExcept();
            }
        }

        /// <summary>
        /// The reference information. See <see cref="RefMaps"/> for a list of the available information.
        /// </summary>
        protected RefMaps _refMaps;

        /// <summary>
        /// Maps from generated ID to bound node. IDs for a bound node must be contiguous.
        /// </summary>
        protected BndTuple.Builder _idToBnd;

        /// <summary>
        /// Maps from bound nodes to generated ID ranges.
        /// </summary>
        protected Dictionary<BoundNode, SlotRange> _bndToIds;

        /// <summary>
        /// The top scope information.
        /// </summary>
        private RefMaps.ScopeInfo _scopeCur;

        protected RefMaps.ScopeInfo GetTopScope() { return _scopeCur; }

        /// <summary>
        /// The top nested arg information.
        /// </summary>
        private RefMaps.NestedArg _nestedCur;

        protected RefMaps.NestedArg GetTopNestedArg() { return _nestedCur; }

        protected Action<string> _ilSink;
        protected ILLogKind _logKind;

        // Curried globals are stored in a tuple. This is the type for that tuple, the associated
        // top-level local, and the mapping from global path to slot.
        protected DType _typeGlobalCurry;
        protected Type _stGlobalCurry;
        protected LocalBuilder _locGlobalCurryTop;
        protected Dictionary<NPath, int> _globalPathToSlot;

        protected readonly TypeManager _typeManager;
        protected readonly CodeGeneratorBase _codeGen;

        protected readonly CodeGenHost _host;

        // Stack of method generators.
        protected readonly List<FrameInfo> _frames;
        protected int _idFrameNext;

        // The current method generator / frame.
        protected FrameInfo _frameCur;

        // The locals for non-map scopes.
        protected List<(ArgScope, LocEnt)> _nonMapLocals;

        // For allocating cache slots.
        protected int _cacheSlotNext;

        // The stack of call array ranges.
        protected readonly List<(BndCallNode call, RngTuple rngs)> _arrayRanges;

        /// <summary>
        /// Get the current frame as a <see cref="ChildFrameInfo"/>.
        /// </summary>
        protected ChildFrameInfo ChildFrame
        {
            get
            {
                Validation.Assert(!_frameCur.IsRoot == (_frameCur is ChildFrameInfo));
                return _frameCur as ChildFrameInfo;
            }
        }

        /// <summary>
        /// The MethodGenerator for the current frame.
        /// </summary>
        protected MethodGenerator MethCur { get { return _frameCur.Meth; } }

        /// <summary>
        /// The <see cref="ILWriter"/> for the current frame.
        /// </summary>
        protected ILWriter IL { get { return _frameCur.Meth.Il; } }

        protected Impl(CodeGeneratorBase codeGen, CodeGenHost host)
        {
            Validation.AssertValue(codeGen);
            Validation.AssertValue(codeGen.TypeManager);
            Validation.AssertValueOrNull(host);

            _frames = new List<FrameInfo>();
            _typeManager = codeGen.TypeManager;
            _host = host ?? CodeGenHost.CreateBare();
            _codeGen = codeGen;
            _arrayRanges = new List<(BndCallNode call, RngTuple rngs)>();
        }

        public CodeGenResult Translate(BoundNode tree, Action<string> ilSink = null, ILLogKind logKind = ILLogKind.None)
        {
            Validation.AssertValue(tree);
            Validation.Assert(!tree.HasErrors);
            Validation.AssertValueOrNull(ilSink);

            if (ilSink != null)
            {
                _ilSink = ilSink;
                _logKind = logKind != ILLogKind.None ? logKind : ILLogKind.Position;
            }

            Type stRet = GetSysType(tree.Type);

            // Initialize ID <=> bound node mappings.
            _idToBnd = BndTuple.CreateBuilder();
            _bndToIds = new Dictionary<BoundNode, SlotRange>();

            // Get the reference information.
            _refMaps = RefMaps.Run(tree, _codeGen);
            Validation.Assert(_refMaps.NodeCount == tree.NodeCount);
            Validation.Assert(_refMaps.ScopeInfoCount > 0);
            Validation.Assert(_refMaps.NestedArgCount > 0);

            // Initialize the current scope.
            _scopeCur = _refMaps.TopScopeInfo;
            Validation.Assert(_scopeCur.Outer == null);
            Validation.Assert(_scopeCur.Scope == null);

            // Initialize the current nested arg.
            _nestedCur = _refMaps.TopNestedArg;
            Validation.Assert(_nestedCur.Outer == null);
            Validation.Assert(_nestedCur.Owner == null);

            // Determine whether an execution context is needed.
            bool usesExecCtx = _refMaps.ExecRefCount > 0;

            // Parameters consist of the execution context (if needed), followed by the referenced globals (if any).
            // For convenience, we add the execution context to the "global tuple" with default name and default dtype.
            // This is how we communicate to callers that the execution context is needed (and which slot it goes in).

            // Build a "signature" string when logging.
            var sbSig = _ilSink != null ? new StringBuilder() : null;
            if (sbSig != null)
                sbSig.Append("// (");

            // Build the global tuple and curried globals record type.
            int num = usesExecCtx.ToNum() + _refMaps.GlobalCount;
            GlobalTuple globals;
            if (num > 0)
            {
                var bldrGlobs = GlobalTuple.CreateBuilder(num, init: true);
                int slot = 0;
                if (usesExecCtx)
                {
                    // Execution context use slot 0.
                    if (sbSig != null)
                        sbSig.Append("<ctx>");
                    bldrGlobs[slot] = GlobalInfo.CreateCtx();
                    slot++;
                }

                Immutable.Array<DType>.Builder typesCurry = null;
                if (_refMaps.GlobalCount > 0)
                {
                    foreach (var (name, type, refs) in _refMaps.GetGlobalInfos())
                    {
                        Validation.AssertIndex(slot, num);
                        Validation.Assert(bldrGlobs[slot] == null);

                        if (sbSig != null)
                            sbSig.Append(slot > 0 ? ", " : "").AppendFormat("{0}:{1}", name, type);
                        bldrGlobs[slot] = GlobalInfo.Create(slot, name, type, this);

                        // Determine whether the global needs to be curried.
                        bool curry = false;
                        foreach (var idx in refs)
                        {
                            Validation.Assert(_refMaps.GetNode(idx).Type == type);
                            for (var nested = _refMaps.GetGlobalRefCtx(idx); !curry && nested != null; nested = nested.Outer)
                            {
                                if (nested.NeedsDelegate)
                                    curry = true;
                            }
                        }

                        if (curry)
                        {
                            _globalPathToSlot ??= new Dictionary<NPath, int>();
                            typesCurry ??= Immutable.Array<DType>.CreateBuilder();
                            int slotCur = _globalPathToSlot.Count;
                            Validation.Assert(typesCurry.Count == slotCur);
                            _globalPathToSlot.Add(name, slotCur);
                            typesCurry.Add(type);
                        }

                        slot++;
                    }
                }
                Validation.Assert(slot == num);
                Validation.Assert((typesCurry?.Count ?? 0) == Util.Size(_globalPathToSlot));
                _typeGlobalCurry = typesCurry != null ?
                    DType.CreateTuple(opt: false, typesCurry.ToImmutable()) :
                    DType.EmptyTupleReq;
                globals = bldrGlobs.ToImmutable();
            }
            else
            {
                _typeGlobalCurry = DType.EmptyTupleReq;
                globals = GlobalTuple.Empty;
            }

            if (sbSig != null)
                _ilSink(sbSig.AppendFormat(") : {0}", tree.Type).ToString());

            // Start the function.
            bool isProc = tree is BndCallNode bcn && bcn.Oper.IsProc;
            _frameCur = new RootFrameInfo(this, "top", _logKind, globals, isProc);
            Validation.Assert(_frameCur.UsesExecCtx == usesExecCtx);

            // Generate code to copy globals (including ctx) from the argument array to their locals.
            // Note that the ActionHost (when isProc is true) is already in arg 2, so is not copied.
            for (int slot = 0; slot < globals.Length; slot++)
            {
                var (glob, loc) = globals[slot].IsCtx ? _frameCur.GetExecCtxInfo() : _frameCur.GetGlobalInfo(globals[slot].Name);
                Validation.Assert(glob == globals[slot]);
                Validation.Assert(glob.SysType == loc.LocalType);

                Type st = glob.SysType;
                MethodInfo meth;
                if (st.IsClass | st.IsInterface)
                    meth = CodeGenUtil.ToRef(st);
                else if (st.IsGenericType && st.GetGenericTypeDefinition() == typeof(Nullable<>))
                    meth = CodeGenUtil.ToOpt(st.GetGenericArguments()[0]);
                else
                    meth = CodeGenUtil.ToVal(st);

                IL
                    .Ldarg(1)
                    .Ldc_I4(slot)
                    .Ldelem_Ref();
                PushType(typeof(object));
                EmitCall(meth);
                IL.Stloc(loc);
                PopType(loc.LocalType);
            }

            // Create and fill in the global curry tuple.
            if (_typeGlobalCurry.TupleArity > 0)
            {
                _stGlobalCurry = GetSysType(_typeGlobalCurry);
                _locGlobalCurryTop = IL.DeclareLocalRaw(_stGlobalCurry);

                GenCreateTuple(_typeGlobalCurry, _stGlobalCurry, withType: false);
                IL.Stloc(_locGlobalCurryTop);

                foreach (var kvp in _globalPathToSlot)
                {
                    var (glob, loc) = _frameCur.GetGlobalInfo(kvp.Key);
                    Validation.AssertIndex(kvp.Value, _typeGlobalCurry.TupleArity);
                    Validation.Assert(_typeGlobalCurry.GetTupleSlotTypes()[kvp.Value] == glob.Type);
                    IL
                        .Ldloc(_locGlobalCurryTop)
                        .Ldloc(loc);
                    GenStoreSlot(_typeGlobalCurry, _stGlobalCurry, kvp.Value, glob.Type);
                }
            }

            // Generate the remaining code for the function.
            Go(tree);

            Validation.Assert(_frames.Count == 0);
            Validation.Assert(_frameCur.IsRoot);

            if (stRet.IsValueType)
            {
                IL.Box(stRet);
                PopType(stRet);
                PushType(typeof(object));
            }

            // Complete the function.
            (_, var fn) = EndFunctionToDelegate(topFrame: true);
            Validation.Assert(!isProc == (fn is GlobalFunc));
            Validation.Assert(isProc == (fn is RunnerFunc));

            Validation.Assert(_nestedCur.Outer == null);
            Validation.Assert(GetTopScope().Outer == null);

            var idBndMap = new IdBndMap(_idToBnd.ToImmutable(), _bndToIds);
            return new CodeGenResult(fn, globals, idBndMap, _codeGen, tree, stRet);
        }

        protected static Type GetFuncType(Type stRet, ReadOnly.Array<Type> sts)
        {
            var arr = new Type[sts.Length + 1];
            sts.AsSpan().CopyTo(arr);
            arr[arr.Length - 1] = stRet;

            switch (arr.Length)
            {
            case 1:
                return typeof(Func<>).MakeGenericType(arr);
            case 2:
                return typeof(Func<,>).MakeGenericType(arr);
            case 3:
                return typeof(Func<,,>).MakeGenericType(arr);
            case 4:
                return typeof(Func<,,,>).MakeGenericType(arr);
            case 5:
                return typeof(Func<,,,,>).MakeGenericType(arr);
            case 6:
                return typeof(Func<,,,,,>).MakeGenericType(arr);
            case 7:
                return typeof(Func<,,,,,,>).MakeGenericType(arr);
            case 8:
                return typeof(Func<,,,,,,,>).MakeGenericType(arr);
            case 9:
                return typeof(Func<,,,,,,,,>).MakeGenericType(arr);
            case 10:
                return typeof(Func<,,,,,,,,,>).MakeGenericType(arr);
            case 11:
                return typeof(Func<,,,,,,,,,,>).MakeGenericType(arr);
            case 12:
                return typeof(Func<,,,,,,,,,,,>).MakeGenericType(arr);
            case 13:
                return typeof(Func<,,,,,,,,,,,,>).MakeGenericType(arr);
            case 14:
                return typeof(Func<,,,,,,,,,,,,,>).MakeGenericType(arr);
            case 15:
                return typeof(Func<,,,,,,,,,,,,,,>).MakeGenericType(arr);
            case 16:
                return typeof(Func<,,,,,,,,,,,,,,,>).MakeGenericType(arr);
            case 17:
                return typeof(Func<,,,,,,,,,,,,,,,,>).MakeGenericType(arr);
            }

            // REVIEW: What should we do if they use more than 16 parameters? We could package things
            // in structs.
            throw NYI("More than 16 parameters used");
        }

        protected Type GetSysType(DType type)
        {
            Validation.Assert(type.IsValid);

            if (!_typeManager.TryEnsureSysType(type, out Type st))
            {
                // REVIEW: Eventually, this should be Unexpected instead of NYI.
                throw NYI($"Type not yet supported: {type.Serialize(compact: true)}");
            }

#if DEBUG
            // For most of the standard types, this code generator assumes certain system types, so verify.
            bool tmp = _typeManager.TryEnsureSysType(type.RootType, out Type stRoot);
            Validation.Assert(tmp);

            bool isOpt = type.IsRootOpt;
            Type stStd = null;
            switch (type.RootKind)
            {
            case DKind.General:
                Validation.Assert(isOpt);
                stStd = typeof(object);
                break;
            case DKind.Vac:
                // REVIEW: There is no good system type for Vac and Null. Can't use System.Void since we
                // need to be able to create arrays and enumerables with this type as item type. I suppose we could use
                // some little value type, V, like bool, or an empty struct, but then one would expect Null to have system
                // type Nullable<V>, which we don't want.
                stStd = typeof(object);
                break;
            case DKind.Text:
                Validation.Assert(isOpt);
                stStd = typeof(string);
                break;
            case DKind.Bit:
                stStd = isOpt ? typeof(bool?) : typeof(bool);
                break;

            case DKind.R8:
                stStd = isOpt ? typeof(double?) : typeof(double);
                break;
            case DKind.R4:
                stStd = isOpt ? typeof(float?) : typeof(float);
                break;
            case DKind.IA:
                stStd = isOpt ? typeof(Integer?) : typeof(Integer);
                break;
            case DKind.I8:
                stStd = isOpt ? typeof(long?) : typeof(long);
                break;
            case DKind.I4:
                stStd = isOpt ? typeof(int?) : typeof(int);
                break;
            case DKind.I2:
                stStd = isOpt ? typeof(short?) : typeof(short);
                break;
            case DKind.I1:
                stStd = isOpt ? typeof(sbyte?) : typeof(sbyte);
                break;
            case DKind.U8:
                stStd = isOpt ? typeof(ulong?) : typeof(ulong);
                break;
            case DKind.U4:
                stStd = isOpt ? typeof(uint?) : typeof(uint);
                break;
            case DKind.U2:
                stStd = isOpt ? typeof(ushort?) : typeof(ushort);
                break;
            case DKind.U1:
                stStd = isOpt ? typeof(byte?) : typeof(byte);
                break;

            case DKind.Date:
                stStd = isOpt ? typeof(Date?) : typeof(Date);
                break;
            case DKind.Time:
                stStd = isOpt ? typeof(Time?) : typeof(Time);
                break;
            case DKind.Guid:
                stStd = isOpt ? typeof(Guid?) : typeof(Guid);
                break;
            }
            Validation.Assert(stStd == null || stRoot == stStd);
#endif

            return st;
        }

        protected static Exception NYI(string msg)
        {
            Validation.AssertValue(msg);
            throw new NotImplementedException(msg);
        }

        protected Exception Unexpected()
        {
            Validation.Assert(false);
            throw new InvalidOperationException("Internal code-gen error");
        }

        protected Exception Unexpected(string msg)
        {
            Validation.AssertValue(msg);
            Validation.Assert(false);
            throw new InvalidOperationException(msg);
        }

        protected Exception Unsupported(string msg)
        {
            Validation.AssertValue(msg);
            throw new NotSupportedException(msg);
        }

        /// <summary>
        /// Return whether the given bound node maps to a local or arg. If so, sets <paramref name="lai"/>
        /// accordingly.
        /// </summary>
        private bool IsLocOrArg(BoundNode arg, int idx, out LocArgInfo lai)
        {
            if (arg != null)
            {
                AssertIdx(arg, idx);
                if (arg is BndScopeRefNode srn)
                {
                    var info = _refMaps.GetScopeInfoFromRefNode(idx);
                    if (Util.TryGetValue(_frameCur.ScopeMap, info.Index, out lai))
                        return true;
                }
                else if (_frameCur.IsRoot && arg is BndGlobalBaseNode gbn)
                {
                    Validation.Assert(_frames.Count == 0);
                    var (glob, loc) = _frameCur.GetGlobalInfo(gbn.FullName);
                    Validation.Assert(glob.Type == arg.Type);
                    Validation.Assert(glob.SysType == loc.LocalType);
                    lai = loc;
                    return true;
                }
            }

            lai = default;
            return false;
        }

        /// <summary>
        /// If <paramref name="bnd"/> is a local or arg, sets the <paramref name="lai"/> to the
        /// loc/arg info. Otherwise, "accepts" the <paramref name="bnd"/> (to load it on the top
        /// of the stack) and stores the value in a new local, sets the <paramref name="lai"/>
        /// and returns the local entry (for proper disposal by caller). If <paramref name="load"/>
        /// is true, leaves the value on the top of the stack.
        /// 
        /// Does NOT adjust the type stack. Note that when <paramref name="load"/> is false it
        /// has no effect on the execution stack, but when <paramref name="load"/> is true it
        /// loads the value on the execution stack. That (possible) load is NOT reflected in the
        /// type stack.
        /// </summary>
        private LocEnt EnsureLocOrArg(BoundNode bnd, ref int cur, Type st, out LocArgInfo lai, bool load)
        {
            Validation.AssertValueOrNull(bnd);

            if (IsLocOrArg(bnd, cur, out lai))
            {
                Validation.Assert(bnd.NodeCount == 1);
                cur += bnd.NodeCount;
                if (load)
                    IL.LdLocArg(in lai);
                return default;
            }

            var loc = MethCur.AcquireLocal(st);
            lai = loc;
            cur = bnd.Accept(this, cur);
            PopType(st);
            if (load)
                IL.Dup();
            IL.Stloc(loc);
            return loc;
        }

        [Conditional("DEBUG")]
        protected void AssertIdx(BoundNode bnd, int idx)
        {
            _refMaps.AssertIdx(bnd, idx);
        }

        protected override bool Enter(BoundNode bnd, int idx)
        {
            AssertIdx(bnd, idx);
            return base.Enter(bnd, idx);
        }

        protected override void Leave(BoundNode bnd, int idx)
        {
            AssertIdx(bnd, idx);
            base.Leave(bnd, idx);
        }

        protected override void InitScope(ArgScope scope, BndScopeOwnerNode owner, int idx, int slot, bool isArgValid)
        {
            Validation.BugCheck(isArgValid);
            Validation.AssertValue(scope);
            AssertIdx(owner, idx);

            var info = _refMaps.GetScopeInfoFromOwner(owner, idx, scope);
            Validation.Assert(info.Slot == slot);

            base.InitScope(scope, owner, idx, slot, isArgValid);
            InitScopeCore(info);
        }

        protected override void PushScope(ArgScope scope, BndScopeOwnerNode owner, int idx, int slot, bool isArgValid)
        {
            Validation.BugCheck(isArgValid);
            Validation.Assert(_scopeCur.Depth == ScopeDepth);
            Validation.AssertValue(scope);
            AssertIdx(owner, idx);

            var info = _refMaps.GetScopeInfoFromOwner(owner, idx, scope);
            Validation.Assert(info.Slot == slot);

            Validation.Assert(_scopeCur == info.Outer);
            base.PushScope(scope, owner, idx, slot, isArgValid);
            Validation.Assert(_scopeCur == info.Outer);

            // Update the current scope.
            _scopeCur = info;
            Validation.Assert(_scopeCur.Depth == ScopeDepth);

            PushScopeCore(_scopeCur);
        }

        protected override void PopScope(ArgScope scope)
        {
            Validation.AssertValue(_scopeCur);
            Validation.AssertValue(_scopeCur.Outer);
            Validation.Assert(_scopeCur.Depth == ScopeDepth);
            Validation.AssertValue(scope);
            Validation.Assert(_scopeCur.Scope == scope);

            PopScopeCore(_scopeCur);

            _scopeCur = _scopeCur.Outer;

            base.PopScope(scope);

            Validation.AssertValue(_scopeCur);
            Validation.Assert(_scopeCur.Depth == ScopeDepth);
        }

        protected override void DisposeScope(ArgScope scope, BndScopeOwnerNode owner, int idx, int slot)
        {
            Validation.AssertValue(scope);
            AssertIdx(owner, idx);

            var info = _refMaps.GetScopeInfoFromOwner(owner, idx, scope);
            Validation.Assert(info.Slot == slot);

            DisposeScopeCore(info);

            base.DisposeScope(scope, owner, idx, slot);
        }

        protected virtual void InitScopeCore(RefMaps.ScopeInfo info)
        {
            Validation.AssertValue(info);
            Validation.Assert(!Util.ContainsKey(_frameCur.ScopeMap, info.Index));

            // REVIEW: This assumes/asserts that a Guard or With scope has had its arg
            // reduced properly to an item, and, for Guard, that item is already guarded.

            BoundNode arg = null;
            int cur = -1;
            if (info.Owner is BndCallNode bcn && info.Slot >= 0 && !info.IsIndex)
            {
                Validation.BugCheck(info.Slot < bcn.Args.Length);
                arg = bcn.Args[info.Slot];
                Validation.BugCheck(info.Scope.IsValidArg(arg));
                cur = GetArgNodeIndex(info.OwnerIdx + 1, bcn.Args, info.Slot);
                AssertIdx(arg, cur);
            }
            else if (info.Owner is BndSetFieldsNode bsf)
            {
                Validation.BugCheck(info.Slot == -1);
                arg = bsf.Source;
                Validation.BugCheck(info.Scope.IsValidArg(arg));
                cur = info.OwnerIdx + 1;
                AssertIdx(arg, cur);
            }
            else if (info.Owner is BndModuleProjectionNode bmp)
            {
                Validation.BugCheck(info.Slot == -1);
                arg = bmp.Module;
                Validation.BugCheck(info.Scope.IsValidArg(arg));
                cur = info.OwnerIdx + 1;
                AssertIdx(arg, cur);
            }

            switch (info.Scope.Kind)
            {
            default:
                Validation.Assert(info.Scope.Kind.IsLoopScope());
                break;

            case ScopeKind.With:
                Validation.Assert(arg == null || info.Scope.Type == arg.Type);
                if (IsLocOrArg(arg, cur, out var laiWith))
                {
                    // The value is NOT on the stack.
                    // Share the indicated loc/arg - no need for a new one.
                    Util.Add(ref _nonMapLocals, (info.Scope, default));
                    Util.Add(ref _frameCur.ScopeMap, info.Index, laiWith);
                }
                else
                {
                    // The value IS on the stack.

                    // For indexed GroupBy, the items in the value for the agg scope may need to be unwrapped
                    // to get the type to match first. This unwrapping is only necessary when the *map* index
                    // is referenced, because otherwise the index is unused after key generation, and the
                    // element selector does the unwrapping before this point.
                    if (info.Owner is BndGroupByNode bgb && bgb.IndexForMaps != null && info.Scope == bgb.ScopeForAggs)
                    {
                        // The value is a sequence of wrapped pairs of items with their indices.
                        // Unwrap the items before proceeding.
                        Validation.Assert(info.Scope.Type.IsSequence);
                        var stItem = GetSysType(info.Scope.Type.ItemTypeOrThis);
                        EmitCall(CodeGenUtil.UnwrapIndPairsToItems(stItem));
                    }

                    // Allocate a local, add it to the scope map, and store the value in the local.
                    var st = GetSysType(info.Scope.Type);
                    var loc = MethCur.AcquireLocal(st);
                    Util.Add(ref _nonMapLocals, (info.Scope, loc));
                    Util.Add(ref _frameCur.ScopeMap, info.Index, loc);
                    IL.Stloc(loc);
                    PopType(st);
                }
                break;

            case ScopeKind.Guard:
                Validation.Assert(arg == null || info.Scope.Type.ToOpt() == arg.Type);
                {
                    DType typeReq = info.Scope.Type;
                    DType typeOpt = typeReq.ToOpt();
                    Type stReq = GetSysType(typeReq);
                    Type stOpt = GetSysType(typeOpt);

                    // Implement the null check.
                    Validation.Assert(_frameCur.NullLabels?.Count > 0);
                    (var _, var labNull) = Util.Peek(_frameCur.NullLabels);
                    Validation.Assert(labNull != default);

                    if (_typeManager.IsNullableType(stOpt))
                    {
                        Validation.Assert(stOpt != stReq);

                        // Allocate a local and add it to the scope map.
                        var loc = MethCur.AcquireLocal(stReq);
                        Util.Add(ref _nonMapLocals, (info.Scope, loc));
                        Util.Add(ref _frameCur.ScopeMap, info.Index, loc);

                        if (arg is BndGetFieldNode bgfn)
                        {
                            // The record value is on the stack, NOT the field value.
                            var rec = bgfn.Record;
                            var typeRec = rec.Type;
                            Validation.Assert(typeRec.IsRecordReq);
                            var stRec = GetSysType(typeRec);
                            bool isLoc = IsLocOrArg(rec, cur + 1, out var laiRec);
                            using var entRec = !isLoc ? MethCur.AcquireLocal(stRec) : default;
                            if (!isLoc)
                            {
                                IL.Stloc(entRec);
                                laiRec = entRec;
                                IL.LdLocArg(in laiRec);
                            }

                            // Do the null test.
                            PopType(stRec);
                            _typeManager.GenLoadFieldBit(MethCur, typeRec, stRec, bgfn.Name, typeOpt);
                            IL.Brfalse_Non(labNull);

                            // Get the unwrapped (req) value.
                            IL.LdLocArg(in laiRec);
                            _typeManager.GenLoadFieldReq(MethCur, typeRec, stRec, bgfn.Name, typeReq);
                        }
                        else
                        {
                            bool isLoc = IsLocOrArg(arg, cur, out var laiSrc);
                            LocEnt entSrc = !isLoc ? MethCur.AcquireLocal(stOpt) : default;
                            if (!isLoc)
                            {
                                // The value IS on the stack.
                                PopType(stOpt);
                                IL.Stloc(entSrc);
                                laiSrc = entSrc;
                            }

                            // Do the null test.
                            _typeManager.GenHasValue(MethCur, typeOpt, in laiSrc);
                            IL.Brfalse_Non(labNull);

                            // Get the unwrapped (req) value.
                            _typeManager.GenGetValueOrDefault(MethCur, typeOpt, in laiSrc);
                        }

                        // Store the unwrapped value to the local.
                        IL.Stloc(loc);
                    }
                    else
                    {
                        Validation.Assert(stOpt.IsClass | stOpt.IsInterface);
                        Validation.Assert(stOpt == GetSysType(info.Scope.Type));

                        if (IsLocOrArg(arg, cur, out var lai))
                        {
                            // The value is NOT on the stack.
                            // Share the indicated loc/arg - no need for a new one.
                            Util.Add(ref _nonMapLocals, (info.Scope, default));
                            Util.Add(ref _frameCur.ScopeMap, info.Index, lai);
                            IL.LdLocArg(in lai);
                        }
                        else
                        {
                            // The value IS on the stack.
                            PopType(stOpt);

                            // Allocate a local, add it to the scope map, and store the value in the local.
                            var loc = MethCur.AcquireLocal(GetSysType(info.Scope.Type));
                            Util.Add(ref _nonMapLocals, (info.Scope, loc));
                            Util.Add(ref _frameCur.ScopeMap, info.Index, loc);
                            IL.Dup().Stloc(loc);
                        }

                        if (typeOpt.IsSequence)
                        {
                            var typeItem = typeOpt.ItemTypeOrThis;
                            Type stItem = GetSysType(typeItem);
                            var meth = IsEmptyGen.Instance.MethIsEmptyEnum.MakeGenericMethod(stItem);
                            IL
                                .Call(meth)
                                .Brtrue_Non(labNull);
                        }
                        else
                            IL.Brfalse_Non(labNull);
                    }
                }
                break;
            }
        }

        protected virtual void PushScopeCore(RefMaps.ScopeInfo info)
        {
            Validation.AssertValue(info);

            switch (info.Scope.Kind)
            {
            default:
                Validation.Assert(info.Scope.Kind.IsLoopScope());
                break;

            case ScopeKind.With:
            case ScopeKind.Guard:
                Validation.Assert(Util.ContainsKey(_frameCur.ScopeMap, info.Index));
                break;
            }
        }

        protected virtual void PopScopeCore(RefMaps.ScopeInfo info)
        {
            Validation.AssertValue(info);

            switch (info.Scope.Kind)
            {
            default:
                Validation.Assert(info.Scope.Kind.IsLoopScope());
                break;

            case ScopeKind.With:
            case ScopeKind.Guard:
                Validation.Assert(Util.ContainsKey(_frameCur.ScopeMap, info.Index));
                Validation.Assert(Util.Size(_nonMapLocals) > 0);
                break;
            }
        }

        protected virtual void DisposeScopeCore(RefMaps.ScopeInfo info)
        {
            Validation.AssertValue(info);

            switch (info.Scope.Kind)
            {
            default:
                Validation.Assert(info.Scope.Kind.IsLoopScope());
                break;

            case ScopeKind.With:
            case ScopeKind.Guard:
                Validation.Assert(Util.ContainsKey(_frameCur.ScopeMap, info.Index));
                Validation.Assert(Util.Size(_nonMapLocals) > 0);

                (var sc, var loc) = Util.Pop(_nonMapLocals);
                Validation.Assert(sc == info.Scope);

                // Dispose the local cache entry.
                if (loc.IsActive)
                {
                    Validation.Assert(_frameCur.ScopeMap[info.Index].Index == loc.Index);
                    Validation.Assert(_frameCur.ScopeMap[info.Index].SysType == loc.Type);
                    loc.Dispose();
                }
                else
                    Validation.Assert(_frameCur.ScopeMap[info.Index].SysType == _typeManager.GetSysTypeOrNull(info.Scope.Type));

                // Remove our local from the scope map.
                _frameCur.ScopeMap.Remove(info.Index);
                break;
            }
        }

        protected override void PushNestedArg(BndScopeOwnerNode owner, int idx, int slot, bool needsDelegate)
        {
            AssertIdx(owner, idx);
            Validation.AssertValue(_nestedCur);
            Validation.Assert(_nestedCur.Depth == NestedArgDepth);

            base.PushNestedArg(owner, idx, slot, needsDelegate);

            var nested = _refMaps.GetNestedArg(owner, idx, slot);
            Validation.AssertValue(nested);
            Validation.Assert(nested.NeedsDelegate == needsDelegate);
            Validation.Assert(nested.Outer == _nestedCur);
            _nestedCur = nested;

            Validation.Assert(_nestedCur.Depth == NestedArgDepth);
        }

        protected override void PopNestedArg()
        {
            Validation.AssertValue(_nestedCur);
            Validation.AssertValue(_nestedCur.Outer);
            Validation.Assert(_nestedCur.Depth == NestedArgDepth);

            _nestedCur = _nestedCur.Outer;

            base.PopNestedArg();

            Validation.Assert(_nestedCur.Depth == NestedArgDepth);
        }

        /// <summary>
        /// Determine whether the given NestedArg uses globals.
        /// </summary>
        protected bool UsesGlobals(RefMaps.NestedArg nested)
        {
            Validation.AssertValue(nested);
            Validation.Assert(nested.Outer != null);

            return UsesGlobals(nested.Index, nested.Index + 1);
        }

        /// <summary>
        /// Determine whether the given range of NestedArg indices uses globals.
        /// </summary>
        protected bool UsesGlobals(int inaMin, int inaLim)
        {
            if (!_refMaps.UsesGlobals(inaMin, inaLim, out int min, out int lim))
                return false;

#if DEBUG
            // Verify that all of them are in the curried globals.
            Validation.Assert(_frameCur.UsesGlobals);
            for (int i = min; i < lim; i++)
            {
                var (bgn, idx) = _refMaps.GetGlobalRef(i);
                AssertIdx(bgn, idx);
                _globalPathToSlot.TryGetValue(bgn.FullName, out int slot).Verify();
                Validation.AssertIndex(slot, _typeGlobalCurry.TupleArity);
                Validation.Assert(_typeGlobalCurry.GetTupleSlotTypes()[slot] == bgn.Type);
            }
#endif

            return true;
        }

        /// <summary>
        /// Determine whether the given NestedArg uses the exec ctx.
        /// </summary>
        protected bool UsesExecCtx(RefMaps.NestedArg nested)
        {
            Validation.AssertValue(nested);
            Validation.Assert(nested.Outer != null);

            return UsesExecCtx(nested.Index, nested.Index + 1);
        }

        /// <summary>
        /// Determine whether the given range of NestedArg indices uses the exec ctx.
        /// </summary>
        protected bool UsesExecCtx(int inaMin, int inaLim)
        {
            return _refMaps.UsesExecCtx(inaMin, inaLim);
        }

        /// <summary>
        /// Build the set of external scopes for the given NestedArg. Returns null if there aren't any.
        /// </summary>
        protected Dictionary<int, RefMaps.ScopeInfo> FindExtScopes(RefMaps.ScopeInfo scopeBase, RefMaps.NestedArg nested)
        {
            Validation.AssertValue(nested);
            Validation.Assert(nested.Outer != null);

            // Get the set of external scopes that are referenced.
            return FindExtScopes(scopeBase, nested.Index, nested.Index + 1);
        }

        /// <summary>
        /// Build the set of external scopes for the given range of NestedArg indices. Returns null if there aren't any.
        /// </summary>
        protected Dictionary<int, RefMaps.ScopeInfo> FindExtScopes(RefMaps.ScopeInfo scopeBase, int inaMin, int inaLim)
        {
            return _refMaps.FindExtScopes(scopeBase, inaMin, inaLim);
        }

        /// <summary>
        /// Starts creation of a function, associated with the top NestedArg.
        /// The scopes dictionary maps from ArgScope object to parameter index.
        /// </summary>
        protected FrameInfo StartFunctionCore(
            string name, ScopeMap scopeMap, Dictionary<int, RefMaps.ScopeInfo> extScopes,
            bool usesGlobals, bool usesExec, Type stRet, params Type[] stsArgs)
        {
            Validation.AssertNonEmpty(name);
            Validation.AssertValueOrNull(scopeMap);
            Validation.Assert(Util.Size(scopeMap) <= Util.Size(stsArgs));
            Validation.AssertValueOrNull(extScopes);
            Validation.Assert(!usesGlobals | _frameCur.UsesGlobals);
            Validation.Assert(!usesExec | _frameCur.UsesExecCtx);

            _frames.Add(_frameCur);
            return _frameCur = new ChildFrameInfo(this, name, _logKind, stRet, stsArgs, usesGlobals, usesExec, scopeMap, extScopes);
        }

        /// <summary>
        /// Starts creation of a function that is disjoint from the bound tree, in the sense that
        /// there are no external dependencies for the function.
        /// </summary>
        protected FrameInfo StartFunctionDisjoint(string name, Type stRet, params Type[] stsArgs)
        {
            return StartFunctionDisjoint(name, stRet, new ReadOnly.Array<Type>(stsArgs));
        }

        /// <summary>
        /// Starts creation of a function that is disjoint from the bound tree, in the sense that
        /// there are no external dependencies for the function.
        /// </summary>
        protected FrameInfo StartFunctionDisjoint(string name, Type stRet, ReadOnly.Array<Type> stsArgs)
        {
            _frames.Add(_frameCur);
            return _frameCur = new ChildFrameInfo(this, name, _logKind, stRet, stsArgs);
        }

        /// <summary>
        /// Starts creation of a function which accepts a single argument of type
        /// <see cref="ValueTuple{T1,T2}"/>, given by <paramref name="stSysValTup"/>.
        /// The <c>Item1</c> and <c>Item2</c> fields of this tuple correspond to
        /// <paramref name="scopeItem1"/> and <paramref name="scopeItem2"/> respectively
        /// (note the types of the scopes are expected to match <c>T1</c> and <c>T2</c>
        /// when given). The function starts by mapping the scopes to locals containing
        /// the unpacked items, if the item's respective scope is non-null. Otherwise,
        /// it is assumed the respective item is unused within the function and the item
        /// is not unpacked.
        /// </summary>
        protected FrameInfo StartUnpackFunction(string name,
            Dictionary<int, RefMaps.ScopeInfo> extScopes, bool usesGlobals, bool usesExec,
            Type stRet, Type stSysValTup,
            RefMaps.ScopeInfo scopeItem1, RefMaps.ScopeInfo scopeItem2)
        {
            Validation.AssertNonEmpty(name);
            Validation.AssertValueOrNull(extScopes);
            Validation.Assert(!usesGlobals | _frameCur.UsesGlobals);
            Validation.Assert(!usesExec | _frameCur.UsesExecCtx);
            Validation.AssertValue(stSysValTup);
            Validation.Assert(stSysValTup.IsConstructedGenericType);
            Validation.Assert(stSysValTup.GetGenericTypeDefinition() == typeof(ValueTuple<,>));

            Validation.AssertValueOrNull(scopeItem1);
            var stItem1 = stSysValTup.GenericTypeArguments[0];
            Validation.Assert(scopeItem1 == null || GetSysType(scopeItem1.Scope.Type) == stItem1);

            Validation.AssertValueOrNull(scopeItem2);
            var stItem2 = stSysValTup.GenericTypeArguments[1];
            Validation.Assert(scopeItem2 == null || GetSysType(scopeItem2.Scope.Type) == stItem2);

            var scopeMap = new ScopeMap();

            var frameFn = StartFunctionCore(name,
                scopeMap, extScopes, usesGlobals, usesExec,
                stRet, stSysValTup);

            bool needItem1 = scopeItem1 != null;
            bool needItem2 = scopeItem2 != null;

            if (needItem1)
            {
                Validation.Coverage(needItem2 ? 1 : 0);
                var finItem1 = stSysValTup.GetField(CodeGenUtil.SysValTupItem1).VerifyValue();
                Validation.Assert(finItem1.FieldType == stItem1);

                // We don't dispose the local in this method, so we use the lower level call.
                var locItem1 = IL.DeclareLocalRaw(stItem1);
                scopeMap.Add(scopeItem1.Index, new LocArgInfo(locItem1));
                IL
                    .Ldarg(1, stSysValTup)
                    .Ldfld(finItem1)
                    .Stloc(locItem1);
            }

            if (needItem2)
            {
                Validation.Coverage(needItem1 ? 1 : 0);
                var finItem2 = stSysValTup.GetField(CodeGenUtil.SysValTupItem2).VerifyValue();
                Validation.Assert(finItem2.FieldType == stItem2);

                // We don't dispose the local in this method, so we use the lower level call.
                var locItem2 = IL.DeclareLocalRaw(stItem2);
                scopeMap.Add(scopeItem2.Index, new LocArgInfo(locItem2));
                IL
                    .Ldarg(1, stSysValTup)
                    .Ldfld(finItem2)
                    .Stloc(locItem2);
            }

            Validation.Assert(_frameCur == frameFn);
            return frameFn;
        }

        protected void DumpIL()
        {
            if (_ilSink != null)
            {
                _ilSink(_frameCur.Meth.GetHeader());
                var lines = _frameCur.Meth.Il.ResetLines();
                foreach (var line in lines)
                    _ilSink("  " + line);
                _ilSink("");
            }
        }

        protected static bool IsAssignableFrom(Type stDst, Type stSrc)
        {
            Validation.AssertValue(stDst);
            Validation.AssertValue(stSrc);
            if (stDst.IsAssignableFrom(stSrc))
                return true;
            if (stSrc.IsEnum && stDst.IsAssignableFrom(stSrc.GetEnumUnderlyingType()))
                return true;
            return false;
        }

        protected void PushArrRanges(BndCallNode call, RngTuple rngs)
        {
            Validation.AssertValue(call);
            Validation.Assert(!rngs.IsDefault);
            _arrayRanges.Add((call, rngs));
        }

        protected (BndCallNode call, RngTuple rngs) PopArrRanges()
        {
            Validation.Assert(_arrayRanges.Count > 0);
            return _arrayRanges.Pop();
        }

        /// <summary>
        /// Duplicate the top item of the system type stack.
        /// </summary>
        protected void DupType()
        {
            _frameCur.Push(_frameCur.Peek());
        }

        /// <summary>
        /// Push <paramref name="st"/> on the system type stack.
        /// </summary>
        protected void PushType(Type st)
        {
            _frameCur.Push(st);
        }

        /// <summary>
        /// Push the system type for <paramref name="type"/> on the system type stack.
        /// </summary>
        protected void PushType(DType type)
        {
            PushType(GetSysType(type));
        }

        /// <summary>
        /// Pops and returns the top of the system type stack, verifying that it is assignable to
        /// the given <paramref name="st"/>.
        /// </summary>
        protected Type PopType(Type st)
        {
            var stTop = _frameCur.Pop();
            Validation.Assert(IsAssignableFrom(st, stTop));
            return stTop;
        }

        /// <summary>
        /// Pops and returns the top of the system type stack, verifying that it is assignable to
        /// the system type for the given <paramref name="type"/>.
        /// </summary>
        protected Type PopType(DType type)
        {
            return PopType(GetSysType(type));
        }

        /// <summary>
        /// Asserts that the top of the system type stack is assignable to the given <paramref name="st"/>.
        /// </summary>
        [Conditional("DEBUG")]
        protected void PeekType(Type st)
        {
#if DEBUG
            var stHave = _frameCur.Peek();
            Validation.Assert(IsAssignableFrom(st, stHave));
#endif
        }

        /// <summary>
        /// Asserts that the top of the system type stack is assignable to the system type for <paramref name="type"/>.
        /// </summary>
        [Conditional("DEBUG")]
        protected void PeekType(DType type)
        {
            PeekType(GetSysType(type));
        }

        /// <summary>
        /// Emits a call to the given static method and validates and updates the type stack accordingly.
        /// </summary>
        protected void EmitCall(MethodInfo meth)
        {
            Validation.AssertValue(meth);
            Validation.Assert(meth.IsStatic);
            var pars = meth.GetParameters();
            for (int i = pars.Length; --i >= 0;)
                PopType(pars[i].ParameterType);
            IL.Call(meth);
            PushType(meth.ReturnType);
        }

        /// <summary>
        /// Emits a call to the given instance method and validates and updates the type stack accordingly.
        /// </summary>
        protected void EmitCallVirt(MethodInfo meth)
        {
            Validation.AssertValue(meth);
            Validation.Assert(!meth.IsStatic);
            var pars = meth.GetParameters();
            for (int i = pars.Length; --i >= 0;)
                PopType(pars[i].ParameterType);
            PopType(meth.DeclaringType);
            IL.Callvirt(meth);
            PushType(meth.ReturnType);
        }

        protected (Type, Delegate) EndFunctionToDelegate(bool tracksTypes = true, bool topFrame = false)
        {
            Validation.Assert(_frameCur.IsRoot | !_frameCur.UsesGlobals);
            Validation.Assert(_frameCur.IsRoot | !_frameCur.UsesExecCtx);
            Validation.Assert(!_frameCur.UsesExtScopes);
            Validation.Assert(topFrame == _frameCur.IsRoot);

            // Complete the function.
            IL.Ret();

            if (tracksTypes)
            {
                // This frame tracks system types.
                Type stRet = _frameCur.Pop();
                Validation.Assert(IsAssignableFrom(_frameCur.RetSysType, stRet));
            }
            Validation.Assert(_frameCur.StackDepth == 0);

            Type stDel = _frameCur.DelegateType;
            var objArr = _frameCur.GetConstants();

            DumpIL();

            Validation.Assert(_frameCur.SimpleConsts);

            // Get the delegate, with the object array as the "this" object.
            var res = MethCur.CreateInstanceDelegate(stDel, objArr);

            if (!topFrame)
                _frameCur = _frames.Pop();

            return (stDel, res);
        }

        protected void EndFunctionCore()
        {
            Validation.Assert(!_frameCur.IsRoot);

            // Complete the function.
            IL.Ret();

            Type stRet = _frameCur.Pop();
            Validation.Assert(_frameCur.StackDepth == 0);
            Validation.Assert(IsAssignableFrom(_frameCur.RetSysType, stRet));

            Type stDel = _frameCur.DelegateType;
            var objArr = _frameCur.GetConstants();

            DumpIL();

            if (_frameCur.SimpleConsts)
            {
                // Get the delegate, with the constant object array as the "this" object.
                var fn = MethCur.CreateInstanceDelegate(stDel, objArr);

                // Pop to the parent frame.
                _frameCur = _frames.Pop();

                // Now generate the code to load the delegate from the frame object array.
                GenLoadConstCore(fn, stDel);
                PushType(stDel);
                return;
            }

            // This form creates the delegate from the dynamic method object stored in the object array.

            // Pop to the parent frame.
            var frameInner = ChildFrame.VerifyValue();
            _frameCur = _frames.Pop();

            // Load the MethodInfo and delegate type.
            var meth = frameInner.Meth.GetMethodInfo();
            GenLoadConstCore(meth, typeof(MethodInfo));
            IL.Ldtoken(stDel);
            IL.Call(CodeGenUtil.GetMethodInfo1<RuntimeTypeHandle, Type>(Type.GetTypeFromHandle));

            // Create the "this" object, which is an object[] of size 2, 3, or 4, depending on what is needed:
            // [0] contains the constants
            // [1] contains the curried globals
            // [2] contains the curried scopes
            // [3] contains the execution context
            // REVIEW: When there are no curried scopes, construction of this could, in theory,
            // be lifted to the "top" level and the delegate stuffed into a delegate table.
            // Additionally, the execution context, curried globals, and delegate table could be bundled
            // together into a single slot, so this array only needs three slots: (0) constants,
            // (1) global stuff, (2) curried scopes.
            int size = 0;
            if (objArr != null)
                size = Math.Max(size, ChildFrameInfo.SlotConst + 1);
            if (frameInner.UsesGlobals)
                size = Math.Max(size, ChildFrameInfo.SlotGlobs + 1);
            if (frameInner.UsesExtScopes)
                size = Math.Max(size, ChildFrameInfo.SlotScope + 1);
            if (frameInner.UsesExecCtx)
                size = Math.Max(size, ChildFrameInfo.SlotExCtx + 1);
            Validation.Assert(2 <= size & size <= 4);

            IL.Ldc_I4(size);
            IL.Newarr(typeof(object));

            // Add the constants array.
            if (objArr != null)
            {
                Validation.Assert(ChildFrameInfo.SlotConst < size);
                IL.Dup().Ldc_I4(ChildFrameInfo.SlotConst);
                GenLoadConstCore(objArr, typeof(object[]));
                IL.Stelem(typeof(object[]));
            }

            // Add the curried globals.
            if (frameInner.UsesGlobals)
            {
                Validation.Assert(ChildFrameInfo.SlotGlobs < size);
                IL.Dup().Ldc_I4(ChildFrameInfo.SlotGlobs);
                GenLoadCurriedGlobals();
                IL.Stelem(_stGlobalCurry);
            }

            // Add the external scopes.
            if (frameInner.UsesExtScopes)
            {
                Validation.Assert(frameInner.ExtScopeTupSysType != null);
                Validation.Assert(frameInner.ExtScopeTup.IsTupleReq);
                Validation.Assert(frameInner.ExtScopeTup.TupleArity > 0);

                // Prepare to stash the curry tuple.
                Validation.Assert(ChildFrameInfo.SlotScope < size);
                IL.Dup().Ldc_I4(ChildFrameInfo.SlotScope);

                // Create the curry tuple.
                GenCreateTuple(frameInner.ExtScopeTup, frameInner.ExtScopeTupSysType, withType: false);

                // Fill in the curry tuple.
                foreach (var (iscp, slot) in frameInner.ExtScopeToSlot)
                {
                    Validation.AssertIndex(slot, frameInner.ExtScopeTup.TupleArity);
                    var info = _refMaps.GetScopeInfo(iscp);
                    Validation.Assert(frameInner.ExtScopeTup.GetTupleSlotTypes()[slot] == info.Scope.Type);
                    IL.Dup();
                    GenScopeRef(info);
                    GenStoreSlot(frameInner.ExtScopeTup, frameInner.ExtScopeTupSysType, slot, info.Scope.Type);
                }
                IL.Stelem(frameInner.ExtScopeTupSysType);
            }

            // Add the execution context.
            if (frameInner.UsesExecCtx)
            {
                Validation.Assert(ChildFrameInfo.SlotExCtx < size);
                IL.Dup().Ldc_I4(ChildFrameInfo.SlotExCtx);
                GenLoadExecCtxCore(withType: false);
                IL.Stelem(typeof(ExecCtx));
            }

            // Invoke CreateDelegate. Note that "meth" is loaded on the exec stack (and poked into the constants array) above.
            IL.Callvirt(CodeGenUtil.GetMethodInfo2<Type, object, Delegate>(meth.CreateDelegate));

            PushType(stDel);
        }

        protected void GenLoadConstArray()
        {
            if (_frameCur.SimpleConsts)
                IL.Ldarg(0);
            else
            {
                // The "this" object is an object[] with the constants array in the appropriate slot.
                Validation.Assert(!_frameCur.IsRoot);
                IL.Ldarg(0).Ldc_I4(ChildFrameInfo.SlotConst).Ldelem(typeof(object[]));
            }
        }

        protected void GenLoadConstFromSlot(int slot, Type st)
        {
            Validation.Assert(!st.IsValueType);
            GenLoadConstArray();
            IL.Ldc_I4(slot).Ldelem(st);
        }

        protected void GenLoadNull(DType type)
        {
            Validation.Assert(type.IsOpt);
            _typeManager.GenNull(MethCur, type);
        }

        protected void GenLoadCurriedGlobals()
        {
            Validation.Assert(_frameCur.UsesGlobals);

            if (_frameCur.IsRoot)
            {
                Validation.Assert(_locGlobalCurryTop != null);
                IL.Ldloc(_locGlobalCurryTop);
            }
            else
            {
                // The "this" object is an object[] with the curried globas in the appropriate slot.
                IL.Ldarg(0).Ldc_I4(ChildFrameInfo.SlotGlobs).Ldelem(_stGlobalCurry);
            }
        }

        protected void GenLoadCurriedScopes()
        {
            Validation.Assert(_frameCur.UsesExtScopes);
            var frame = ChildFrame.VerifyValue();

            // The "this" object is an object[] with the ext scopes in the appropriate slot.
            IL.Ldarg(0).Ldc_I4(ChildFrameInfo.SlotScope).Ldelem(frame.ExtScopeTupSysType);
        }

        public virtual void GenLoadActionHost()
        {
            Validation.BugCheck(_frameCur.IsProc, "Action host is not available");
            IL.Ldarg(2);
        }

        public virtual void GenLoadExecCtx()
        {
            GenLoadExecCtxCore(withType: false);
        }

        protected virtual void GenLoadExecCtxCore(bool withType)
        {
            Validation.BugCheck(_frameCur.UsesExecCtx);

            if (_frameCur.IsRoot)
            {
                Validation.Assert(_frames.Count == 0);
                var (glob, loc) = _frameCur.GetExecCtxInfo();
                Validation.Assert(loc != null);
                IL.Ldloc(loc);
                if (withType)
                    PushType(loc.LocalType);
            }
            else
            {
                // The "this" object is an object[] with the exec ctx in the appropriate slot.
                IL.Ldarg(0).Ldc_I4(ChildFrameInfo.SlotExCtx).Ldelem(typeof(ExecCtx));
                if (withType)
                    PushType(typeof(ExecCtx));
            }
        }

        public virtual int EnsureIdRange(BoundNode bnd, int count)
        {
            Validation.BugCheckValue(bnd, nameof(bnd));
            Validation.BugCheckParam(count >= 1, nameof(count));
            Validation.Assert(_idToBnd.Count == _bndToIds.Values.Sum(rng => rng.Count));

            if (_bndToIds.TryGetValue(bnd, out var rng))
            {
                Validation.BugCheck(rng.Count == count, nameof(count));
#if DEBUG
                for (int id = rng.Min; id < rng.Lim; id++)
                    Validation.Assert(_idToBnd[id] == bnd);
#endif
                return rng.Min;
            }

            int idMin = _idToBnd.Count;
            _bndToIds[bnd] = new SlotRange(idMin, idMin + count);
            for (int i = 0; i < count; i++)
                _idToBnd.Add(bnd);

            return idMin;
        }

        protected void GenLoadField(DType typeRec, Type stRec, DName name, DType typeFld)
        {
            Validation.Assert(typeRec.IsRecordReq);
            Validation.AssertValue(stRec);
            Validation.Assert(name.IsValid);
            Validation.Assert(typeFld.IsValid);
            _typeManager.GenLoadField(MethCur, typeRec, stRec, name, typeFld);
        }

        protected void GenLoadField(DType typeRec, DName name, DType typeFld)
        {
            Validation.Assert(typeRec.IsRecordReq);
            Validation.Assert(name.IsValid);
            Validation.Assert(typeFld.IsValid);
            _typeManager.GenLoadField(MethCur, typeRec, GetSysType(typeRec), name, typeFld);
        }

        protected virtual void GenMapNullRecordToNullRecord(Label labDone, DType typeSrc, Type stSrc, DType typeDst, Type stDst)
        {
            Validation.Assert(typeSrc.IsRecordOpt);
            Validation.AssertValue(stSrc);
            Validation.Assert(typeDst.IsRecordOpt);
            Validation.AssertValue(stDst);
            _typeManager.GenMapNullRecordToNullRecord(MethCur, labDone, typeSrc, stSrc, typeDst, stDst);
        }

        protected void GenLoadSlot(DType typeTup, Type stTup, int slot, DType typeDst)
        {
            Validation.Assert(typeTup.IsTupleReq);
            Validation.AssertValue(stTup);
            Validation.AssertIndex(slot, typeTup.TupleArity);
            Validation.Assert(typeDst.IsValid);
            _typeManager.GenLoadSlot(MethCur, typeTup, stTup, slot, typeDst);
        }

        protected void GenLoadSlot(DType typeTup, int slot, DType typeDst)
        {
            Validation.Assert(typeTup.IsTupleReq);
            Validation.AssertIndex(slot, typeTup.TupleArity);
            Validation.Assert(typeDst.IsValid);
            _typeManager.GenLoadSlot(MethCur, typeTup, GetSysType(typeTup), slot, typeDst);
        }

        protected virtual void GenStoreSlot(DType typeTup, Type stTup, int slot, DType typeFld)
        {
            Validation.Assert(typeTup.IsTupleReq);
            Validation.AssertValue(stTup);
            Validation.AssertIndex(slot, typeTup.TupleArity);
            Validation.Assert(typeFld.IsValid);
            _typeManager.GenStoreSlot(MethCur, typeTup, stTup, slot, typeFld);
        }

        protected virtual void GenCreateTuple(DType typeTup, Type stTup, bool withType)
        {
            Validation.Assert(typeTup.IsTupleReq);
            Validation.AssertValue(stTup);
            _typeManager.GenCreateTuple(MethCur, typeTup, stTup);
            if (withType)
                PushType(stTup);
        }

        protected override void VisitImpl(BndErrorNode bnd, int idx)
        {
            throw Unexpected("Shouldn't code gen a tree with errors");
        }

        protected override void VisitImpl(BndNamespaceNode bnd, int idx)
        {
            throw Unexpected("Shouldn't code gen a tree with namespace value");
        }

        protected override void VisitImpl(BndMissingValueNode bnd, int idx)
        {
            throw Unexpected("Shouldn't code gen a tree with errors");
        }

        protected override void VisitImpl(BndNullNode bnd, int idx)
        {
            Validation.Assert(bnd.Type.IsOpt);
            GenLoadNull(bnd.Type);
            PushType(bnd.Type);
        }

        protected override void VisitImpl(BndDefaultNode bnd, int idx)
        {
            Validation.Assert(!bnd.Type.IsOpt);
            GenLoadDefault(bnd.Type);
            PushType(bnd.Type);
        }

        protected void GenIntConst(DKind kind, Integer val)
        {
            switch (kind)
            {
            case DKind.I1:
            case DKind.I2:
            case DKind.I4:
                IL.Ldc_I4((int)val);
                break;
            case DKind.I8:
                IL.Ldc_I8((long)val);
                break;
            case DKind.Bit:
            case DKind.U1:
            case DKind.U2:
            case DKind.U4:
                IL.Ldc_U4((uint)val);
                break;
            case DKind.U8:
                IL.Ldc_U8((ulong)val);
                break;
            case DKind.IA:
                GenLoadInteger(val);
                break;

            default:
                throw Unexpected();
            }
        }

        protected void GenFltConst(DKind kind, double val)
        {
            switch (kind)
            {
            case DKind.R4:
                IL.Ldc_R4((float)val);
                break;
            case DKind.R8:
                IL.Ldc_R8(val);
                break;
            default:
                throw Unexpected();
            }
        }

        protected override void VisitImpl(BndIntNode bnd, int idx)
        {
            AssertIdx(bnd, idx);
            Validation.Assert(bnd.Type.IsIntegralReq);
            GenIntConst(bnd.Type.RootKind, bnd.Value);
            PushType(bnd.Type);
        }

        protected override void VisitImpl(BndFltNode bnd, int idx)
        {
            AssertIdx(bnd, idx);
            Validation.Assert(bnd.Type.IsFractionalReq);
            GenFltConst(bnd.Type.RootKind, bnd.Value);
            PushType(bnd.Type);
        }

        protected override void VisitImpl(BndStrNode bnd, int idx)
        {
            AssertIdx(bnd, idx);
            if (bnd.Value == null)
                IL.Ldnull();
            else
                IL.Ldstr(bnd.Value);
            PushType(bnd.Type);
        }

        protected override void VisitImpl(BndCmpConstNode bnd, int idx)
        {
            AssertIdx(bnd, idx);
            Type st = GetSystemType(bnd.Type);

            if (st.IsValueType)
                throw Unexpected("Compound constant expected to be a reference type");
            if (bnd.Value == null)
                throw Unexpected("Compound constant expected to be non-null");
            if (!st.IsAssignableFrom(bnd.Value.GetType()))
                throw Unexpected("Compound constant value of wrong type");

            GenLoadConstCore(bnd.Value, st);
            PushType(bnd.Type);
        }

        protected override void VisitImpl(BndThisNode bnd, int idx)
        {
            AssertIdx(bnd, idx);
            GenGlobal(bnd);
            PushType(bnd.Type);
        }

        protected override void VisitImpl(BndGlobalNode bnd, int idx)
        {
            AssertIdx(bnd, idx);
            GenGlobal(bnd);
            PushType(bnd.Type);
        }

        protected override void VisitImpl(BndFreeVarNode bnd, int idx)
        {
            throw Unexpected("Free variable not supported by code gen");
        }

        protected virtual void GenGlobal(BndGlobalBaseNode bnd)
        {
            Validation.AssertValue(bnd);
            Validation.Assert(_frameCur.UsesGlobals);

            var name = bnd.FullName;

            if (_frameCur.IsRoot)
            {
                Validation.Assert(_frames.Count == 0);
                var (glob, loc) = _frameCur.GetGlobalInfo(name);
                Validation.Assert(glob.Type == bnd.Type);
                Validation.Assert(glob.SysType == loc.LocalType);
                IL.Ldloc(loc);
            }
            else
            {
                GenLoadCurriedGlobals();
                _globalPathToSlot.TryGetValue(name, out int slot).Verify();
                GenLoadSlot(_typeGlobalCurry, _stGlobalCurry, slot, bnd.Type);
            }
        }

        protected override void VisitImpl(BndScopeRefNode bnd, int idx)
        {
            AssertIdx(bnd, idx);
            var info = _refMaps.GetScopeInfoFromRefNode(idx);
            GenScopeRef(info);
            PushType(bnd.Type);
        }

        protected override void VisitCore(BndScopeRefNode bnd, int idx, PushedScope scope)
        {
            // Shouldn't get here since we override VisitImpl.
            Validation.Assert(false);
        }

        protected void GenScopeRef(RefMaps.ScopeInfo info)
        {
            if (Util.TryGetValue(_frameCur.ScopeMap, info.Index, out var lai))
            {
                // This is a local or arg. Note that the 0th parameter is the frame object, which will
                // never be represented by a scope (and can't be represented in a LocArgInfo).
                IL.LdLocArg(in lai);
                return;
            }

            if (_frameCur.UsesExtScopes)
            {
                Validation.Assert(!_frameCur.IsRoot);
                var frame = ChildFrame.VerifyValue();

                if (frame.ExtScopeToSlot.TryGetValue(info.Index, out int slot))
                {
                    Validation.AssertIndex(slot, frame.ExtScopeTup.TupleArity);
                    Validation.Assert(frame.ExtScopeTup.GetTupleSlotTypes()[slot] == info.Scope.Type);
                    GenLoadCurriedScopes();
                    GenLoadSlot(frame.ExtScopeTup, frame.ExtScopeTupSysType, slot, info.Scope.Type);
                    return;
                }
            }

            throw Unexpected();
        }

        protected override void PostVisitImpl(BndGetFieldNode bnd, int idx)
        {
            AssertIdx(bnd, idx);
            Validation.Assert(bnd.Record.Type.IsRecordReq);
            DType typeSrc = bnd.Record.Type;
            DType typeDst = bnd.Type;

            PopType(typeSrc);
            GenLoadField(typeSrc, bnd.Name, typeDst);
            PushType(typeDst);
        }

        protected override void PostVisitImpl(BndGetSlotNode bnd, int idx)
        {
            AssertIdx(bnd, idx);
            Validation.Assert(bnd.Tuple.Type.IsTupleReq);
            DType typeSrc = bnd.Tuple.Type;
            DType typeDst = bnd.Type;
            PopType(typeSrc);
            GenLoadSlot(typeSrc, bnd.Slot, typeDst);
            PushType(typeDst);
        }

        protected override bool PreVisitImpl(BndIdxTextNode bnd, int idx)
        {
            AssertIdx(bnd, idx);
            Validation.Assert(bnd.Type == DType.U2Req);

            int cur = idx + 1;
            cur = bnd.Text.Accept(this, cur);
            cur = bnd.Index.Accept(this, cur);
            Validation.Assert(cur == idx + bnd.NodeCount);

            IL.Ldc_I4((int)bnd.Modifier);
            PushType(typeof(IndexFlags));
            EmitCall(CodeGenUtil.TextIndex);

            return false;
        }

        protected override void PostVisitImpl(BndIdxTextNode bnd, int idx)
        {
            throw Unexpected();
        }

        protected override bool PreVisitImpl(BndIdxTensorNode bnd, int idx)
        {
            AssertIdx(bnd, idx);

            int cur = idx + 1;
            var typeTen = bnd.Tensor.Type;
            int rank = typeTen.GetTensorRank();
            Validation.Assert(bnd.Indices.Length == rank);

            if (!_typeManager.TryEnsureSysType(typeTen, out Type stTen))
                throw Unsupported("Unsupported tensor type");

            DType typeItem = typeTen.GetTensorItemType();
            Type stItem = GetSysType(typeItem);
            if (!_typeManager.TryEnsureDefaultValue(typeItem, out var entry))
                throw Unsupported("Unsupported default value");

            // Generate the tensor.
            if (rank != 1)
                cur = bnd.Tensor.Accept(this, cur);

            var ilw = IL;
            if (rank == 0)
            {
                // This one is super easy.
                var meth = CodeGenUtil.TenGetFirst;
                Validation.Assert(meth.IsGenericMethodDefinition);
                meth = meth.MakeGenericMethod(stItem);
                Validation.Assert(meth.ReturnType == stItem);
                EmitCall(meth);
                Validation.Assert(cur == idx + bnd.NodeCount);
                return false;
            }

            Label labDone = default;

            var methGetAt = rank switch
            {
                1 => CodeGenUtil.TenGetItem1,
                2 => CodeGenUtil.TenGetItem2,
                3 => CodeGenUtil.TenGetItem3,
                4 => CodeGenUtil.TenGetItem4,
                _ => CodeGenUtil.TenGetItemArr,
            };
            Validation.Assert(methGetAt.IsGenericMethodDefinition);
            methGetAt = methGetAt.MakeGenericMethod(stItem);
            Validation.Assert(methGetAt.ReturnType == stItem);

            LocArgInfo laiTen = default; // Only used for rank 1.
            using var locCntSrc = rank == 1 ?
                EnsureLocOrArg(bnd.Tensor, ref cur, stTen, out laiTen, load: true) :
                MethCur.AcquireLocal(typeof(Shape));
            var methGetItem = CodeGenUtil.TenShapeItem;
            var arr = rank > 4;
            var labBads = !arr ? new Label[rank] : null;
            Label labBad = default; // Only used for array version.

            if (rank == 1)
                PushType(stTen);
            else
            {
                ilw.Dup(); // Dup the tensor.
                DupType();
                EmitCall(CodeGenUtil.TenGetShape);
                ilw.Stloc(locCntSrc);
                PopType(typeof(Shape));
            }

            if (arr)
            {
                // Make the index array.
                ilw
                    .Ldc_I4(rank)
                    .Newarr(typeof(long));
                PushType(typeof(long[]));
            }

            // Generate the indices and range test them.
            // REVIEW: Should this inline everything (as it currently does) or should it leverage
            // a SliceIndex type?
            for (int i = 0; i < rank; i++)
            {
                if (arr)
                {
                    // Prepare to store in the array.
                    ilw.Dup().Ldc_I4(i);
                }

                var arg = bnd.Indices[i];
                var mod = bnd.Modifiers[i];
                Validation.Assert(mod.IsValid());

                // Generate index.
                cur = arg.Accept(this, cur);
                if (arr)
                    PopType(DType.I8Req);

                // Generate count/shape access.
                void LoadCount()
                {
                    if (rank == 1)
                    {
                        ilw.LdLocArg(in laiTen);
                        PushType(stTen);
                        EmitCall(CodeGenUtil.TenGetCount);
                        PopType(typeof(long));
                    }
                    else
                    {
                        ilw
                            .Ldloca(locCntSrc)
                            .Ldc_I4(i)
                            .Call(methGetItem);
                    }
                }

                // Range test.
                if (!mod.HasIndexMods())
                {
                    ilw.Dup();
                    LoadCount();
                }
                else
                {
                    using var locSize = MethCur.AcquireLocal(typeof(long));
                    LoadCount();
                    ilw.Stloc(locSize);

                    GenAdjustIndex(mod, ilw => ilw.Ldloc(locSize));
                    ilw
                        .Dup()
                        .Ldloc(locSize);
                }

                if (arr)
                    ilw.Bge_Un(ref labBad).Stelem_I8();
                else
                    ilw.Bge_Un(ref labBads[i]);
            }
            Validation.Assert(cur == idx + bnd.NodeCount);

            // Make the call and branch to done.
            EmitCall(methGetAt);
            ilw.Br(ref labDone);

            // Cleanup.
            if (arr)
            {
                ilw
                    .MarkLabel(labBad)
                    .Pop() // index
                    .Pop() // dim number
                    .Pop() // array for store
                    .Pop(); // array for call
            }
            else
            {
                for (int i = rank; --i >= 0;)
                    ilw.MarkLabel(labBads[i]).Pop();
            }

            ilw.Pop(); // tensor.
            GenLoadDefaultCore(stItem, entry.value);
            ilw.MarkLabel(labDone);
            return false;
        }

        /// <summary>
        /// Generate code to adjust an index according to the given <see cref="IndexFlags"/>.
        /// The <paramref name="genLoadSize"/> action is expected to emit code to load the size.
        /// This asserts that <paramref name="flags"/> is not <see cref="IndexFlags.None"/>. Note
        /// that even with a kind that wraps or clips, the resulting index will be "out of bounds"
        /// when the size is zero, so be careful about omitting a bounds test on the result.
        /// </summary>
        protected void GenAdjustIndex(IndexFlags flags, Action<ILWriter> genLoadSize)
        {
            Validation.Assert(flags.HasIndexMods());

            var ilw = IL;

            if ((flags & IndexFlags.Back) != 0)
            {
                ilw.Neg();
                genLoadSize(ilw);
                ilw.Add();
                flags &= ~IndexFlags.Back;
            }

            switch (flags)
            {
            default:
                Validation.Assert(false);
                break;

            case 0:
                break;

            case IndexFlags.Wrap:
                // When size is zero, add one so we can mod.
                genLoadSize(ilw);
                ilw.Dup().Ldc_I8(0).Ceq().Add();

                // Mod and if the result is < 0 (can happen when idx < 0), add size. When size == 0,
                // this results in 0, which is out of bounds.
                ilw.Rem().Dup().Ldc_I8(0).Clt().Neg();
                genLoadSize(ilw);
                ilw.And().Add();
                break;

            case IndexFlags.Clip:
                // idx &= -(idx > 0) sets to zero if it is less than zero.
                ilw
                    .Dup().Ldc_I8(0).Cgt().Neg()
                    .And();
                // If idx >= size, set to size - 1. When size == 0, this results in -1, which is out
                // of bounds.
                {
                    Label labOk = default;
                    ilw.Dup();
                    genLoadSize(ilw);
                    ilw
                        .Blt(ref labOk)
                        .Pop();
                    genLoadSize(ilw);
                    ilw
                        .Ldc_I8(1).Sub()
                        .MarkLabel(labOk);
                }
                break;
            }
        }

        protected override void PostVisitImpl(BndIdxTensorNode bnd, int idx)
        {
            throw Unexpected();
        }

        protected override bool PreVisitImpl(BndIdxHomTupNode bnd, int idx)
        {
            AssertIdx(bnd, idx);
            var typeTup = bnd.Tuple.Type;
            Validation.Assert(typeTup.IsHomTuple());
            DType typeSlot = typeTup.GetHomTupleSlotType();

            int cur = idx + 1;
            int arity = typeTup.TupleArity;
            cur = bnd.Tuple.Accept(this, cur);
            cur = bnd.Index.Accept(this, cur);
            Validation.Assert(cur == idx + bnd.NodeCount);
            if (bnd.Modifier.HasIndexMods())
                GenAdjustIndex(bnd.Modifier, ilw => ilw.Ldc_I8(arity));

            GenLoadDefault(typeSlot);
            PushType(typeSlot);
            if (!_typeManager.TryEnsureGetSlotDynamic(arity, typeSlot, out var getSlotDyn))
                throw NYI(string.Format("Could not get GetSlotDynamic for type: {0}", typeSlot));
            EmitCall(getSlotDyn);
            return false;
        }

        protected override void PostVisitImpl(BndIdxHomTupNode bnd, int idx)
        {
            throw Unexpected();
        }

        protected override bool PreVisitImpl(BndTextSliceNode bnd, int idx)
        {
            AssertIdx(bnd, idx);
            Validation.Assert(bnd.Type == DType.Text);

            int cur = idx + 1;

            // Generate the source text value.
            cur = bnd.Text.Accept(this, cur);

            int ival = 0;
            Validation.Assert(!bnd.Item.IsIndex());
            GenSlice(bnd.Item, bnd.Values, ref ival, ref cur);
            EmitCall(CodeGenUtil.TextSlice);
            Validation.Assert(cur == idx + bnd.NodeCount);
            Validation.Assert(ival == bnd.Values.Length);

            return false;
        }

        protected override void PostVisitImpl(BndTextSliceNode bnd, int idx)
        {
            throw Unexpected();
        }

        protected override bool PreVisitImpl(BndTensorSliceNode bnd, int idx)
        {
            AssertIdx(bnd, idx);
            var typeTen = bnd.Tensor.Type;
            int rank = typeTen.GetTensorRank();
            Validation.Assert(bnd.Items.Length == rank);
            Validation.Assert(bnd.Items.Any(item => !item.IsIndex()));
            Validation.Assert(bnd.Type.GetTensorRank() > 0);

            if (!_typeManager.TryEnsureSysType(typeTen, out Type stTen))
                throw Unsupported("Unsupported tensor type");

            DType typeItem = typeTen.GetTensorItemType();
            Type stItem = GetSysType(typeItem);

            var needDef = bnd.Type.GetTensorRank() != typeTen.GetTensorRank();
#if DEBUG
            var needDef1 = bnd.Items.Any(item => item.IsIndex());
            Validation.Assert(needDef == needDef1);
#endif
            object defaultValue = null;
            if (needDef)
            {
                if (!_typeManager.TryEnsureDefaultValue(typeItem, out var entry))
                    throw Unsupported("Unhandled default value");
                defaultValue = entry.value;
            }

            MethodInfo methSlice;
            bool arr = false;
            switch (rank)
            {
            case 1:
                methSlice = CodeGenUtil.TenSlice1;
                break;
            case 2:
                methSlice = CodeGenUtil.TenSlice2;
                break;
            case 3:
                methSlice = CodeGenUtil.TenSlice3;
                break;
            case 4:
                methSlice = CodeGenUtil.TenSlice4;
                break;
            case 5:
                methSlice = CodeGenUtil.TenSlice5;
                break;
            default:
                methSlice = CodeGenUtil.TenSliceArr;
                arr = true;
                break;
            }

            Validation.Assert(methSlice.IsGenericMethodDefinition);
            methSlice = methSlice.MakeGenericMethod(stItem);
            Validation.Assert(methSlice.ReturnType == stTen);

            int cur = idx + 1;

            // Generate the tensor.
            cur = bnd.Tensor.Accept(this, cur);

            var ilw = IL;

            // Generate the default value.
            PushType(stItem);
            GenLoadDefaultCore(stItem, defaultValue);

            if (arr)
            {
                ilw.Ldc_I4(rank).Newarr(typeof(SliceItem));
                PushType(typeof(SliceItem[]));
            }

            int ival = 0;
            for (int i = 0; i < rank; i++)
            {
                if (arr)
                    ilw.Dup().Ldc_I4(i);

                var item = bnd.Items[i];
                GenSlice(bnd.Items[i], bnd.Values, ref ival, ref cur);

                if (arr)
                {
                    ilw.Stelem(typeof(SliceItem));
                    PopType(typeof(SliceItem));
                }
            }
            Validation.Assert(cur == idx + bnd.NodeCount);
            Validation.Assert(ival == bnd.Values.Length);

            EmitCall(methSlice);
            return false;
        }

        protected override void PostVisitImpl(BndTensorSliceNode bnd, int idx)
        {
            throw Unexpected();
        }

        protected override bool PreVisitImpl(BndTupleSliceNode bnd, int idx)
        {
            AssertIdx(bnd, idx);
            Validation.AssertValue(bnd.Tuple);
            var tuple = bnd.Tuple;
            var typeSrc = tuple.Type;
            var typeDst = bnd.Type;
            var typesSrc = typeSrc.GetTupleSlotTypes();
            var typesDst = typeDst.GetTupleSlotTypes();
            var stSrc = GetSysType(typeSrc);
            var stDst = GetSysType(typeDst);

            GenCreateTuple(typeDst, stDst, withType: true);

            int cur = idx + 1;
            using var locSrc = EnsureLocOrArg(tuple, ref cur, stSrc, out var laiSrc, load: false);
            Validation.Assert(cur == idx + bnd.NodeCount);

            int slotSrc = bnd.Start;
            int step = bnd.Step;
            int count = bnd.Count;
            for (int slotDst = 0; slotDst < count; slotDst++, slotSrc += step)
            {
                Validation.Assert(typesSrc[slotSrc] == typesDst[slotDst]);
                IL
                    .Dup()
                    .LdLocArg(in laiSrc);
                GenLoadSlot(typeSrc, slotSrc, typesDst[slotDst]);
                GenStoreSlot(typeDst, stDst, slotDst, typesDst[slotDst]);
            }
            return false;
        }

        protected override void PostVisitImpl(BndTupleSliceNode bnd, int idx)
        {
            throw Unexpected();
        }

        /// <summary>
        /// Generate code to create a <see cref="SliceItem"/>.
        /// </summary>
        protected void GenSlice(SliceItemFlags flags, ArgTuple values, ref int ival, ref int cur)
        {
            if (flags.IsRangeSlice())
            {
                Validation.Assert(ival <= values.Length - flags.GetCount());
                var start = (flags & SliceItemFlags.Start) != 0 ? values[ival++] : null;
                var stop = (flags & SliceItemFlags.Stop) != 0 ? values[ival++] : null;
                var step = (flags & SliceItemFlags.Step) != 0 ? values[ival++] : null;
                cur = GenSliceRange(flags, start, stop, step, cur);
            }
            else if (flags.IsTupleSlice())
            {
                Validation.Assert(ival < values.Length);
                cur = GenTupleSlice(flags, values[ival++], cur);
            }
            else
            {
                Validation.Assert(flags.IsIndex());
                Validation.Assert(ival < values.Length);
                cur = GenSimpleIndex(flags.ToIndexFlags(), values[ival++], cur);
            }
        }

        protected int GenSimpleIndex(IndexFlags flags, BoundNode bnd, int cur)
        {
            Validation.Assert(flags.IsValid());
            Validation.AssertValue(bnd);

            cur = bnd.Accept(this, cur);
            IL.Ldc_I4((int)flags);
            PushType(typeof(IndexFlags));
            EmitCall(CodeGenUtil.SliceCreateInd);
            return cur;
        }

        protected int GenTupleSlice(SliceItemFlags flags, BoundNode bnd, int cur)
        {
            Validation.Assert(flags.IsTupleSlice());
            Validation.AssertValue(bnd);

            var type = bnd.Type;
            Validation.Assert(type.IsTupleReq);

            var slots = type.GetTupleSlotTypes();
            bool withBacks = type.TupleArity > 3;
            Validation.Assert(type.TupleArity == (withBacks ? 5 : 3));

            if (bnd is BndTupleNode btn)
            {
                if (!withBacks)
                {
                    Validation.Assert(btn.Items.Length == 3);
                    return GenSliceRange(SliceItemFlags.Range, btn.Items[0], btn.Items[1], btn.Items[2], cur + 1);
                }

                Validation.Assert(btn.Items.Length == 5);
                if (btn.Items[1].TryGetBool(out bool backStart) && btn.Items[3].TryGetBool(out bool backStop))
                {
                    SliceItemFlags flagsNew = SliceItemFlags.Range;
                    if (backStart)
                        flagsNew |= SliceItemFlags.StartBack;
                    if (backStop)
                        flagsNew |= SliceItemFlags.StopBack;
                    return GenSliceRange(flagsNew, btn.Items[0], btn.Items[1], btn.Items[2], cur + 1);
                }
            }

            var tm = _typeManager;
            var ilw = IL;

            int slot = 0;
            var typeStart = slots[slot++];
            if (withBacks)
            {
                Validation.Assert(slots[slot] == DType.BitReq);
                slot++;
            }
            var typeStop = slots[slot++];
            if (withBacks)
            {
                Validation.Assert(slots[slot] == DType.BitReq);
                slot++;
            }
            var typeStep = slots[slot++];
            Validation.Assert(slot == slots.Length);

            if (!tm.TryEnsureSysType(type, out Type stTup))
                throw Unsupported("Unsupported tuple type");
            if (!tm.TryEnsureSysType(typeStep, out Type stStep))
                throw Unsupported("Unsupported index type");

            using (var _ = EnsureLocOrArg(bnd, ref cur, stTup, out var laiTup, load: true))
            {
                slot = 0;
                tm.GenLoadSlot(MethCur, type, stTup, slot++, typeStart);
                PushType(typeStart);
                if (withBacks)
                    slot++;

                ilw.LdLocArg(in laiTup);
                tm.GenLoadSlot(MethCur, type, stTup, slot++, typeStop);
                PushType(typeStop);
                if (withBacks)
                    slot++;

                ilw.LdLocArg(in laiTup);
                tm.GenLoadSlot(MethCur, type, stTup, slot++, typeStep);

                Validation.Assert(slot == slots.Length);

                if (typeStep.IsOpt)
                {
                    using var loc = MethCur.AcquireLocal(stStep);
                    ilw.Stloc(loc);
                    tm.GenGetValueOrDefault(MethCur, typeStep, loc);
                    PushType(typeof(long));
                }
                else
                    PushType(typeStep);

                // Push the flags. If there are "back" flags in the tuple, they require extra work.
                ilw.Ldc_I4((byte)(SliceItemFlags.Range |
                    SliceItemFlags.Start | SliceItemFlags.Stop | SliceItemFlags.Step));
                if (withBacks)
                {
                    // This code depends on these specific values.
                    Validation.Assert((byte)SliceItemFlags.StartBack == 1);
                    Validation.Assert((byte)SliceItemFlags.StopBack == 2);
                    ilw.LdLocArg(in laiTup);
                    tm.GenLoadSlot(MethCur, type, stTup, slot - 4, DType.BitReq);
                    ilw.Or().LdLocArg(in laiTup);
                    tm.GenLoadSlot(MethCur, type, stTup, slot - 2, DType.BitReq);
                    ilw.Ldc_I4(1).Shl().Or();
                }
                PushType(typeof(SliceItemFlags));
            }

            MethodInfo meth;
            if (typeStart.IsOpt && typeStop.IsOpt)
                meth = CodeGenUtil.SliceCreateOO;
            else if (typeStop.IsOpt)
                meth = CodeGenUtil.SliceCreateRO;
            else if (typeStart.IsOpt)
                meth = CodeGenUtil.SliceCreateOR;
            else
                meth = CodeGenUtil.SliceCreateRR;

            EmitCall(meth);
            return cur;
        }

        protected int GenSliceRange(SliceItemFlags flags,
            BoundNode bndStart, BoundNode bndStop, BoundNode bndStep, int cur)
        {
            Validation.Assert(flags.IsRangeSlice());

            Validation.Assert(bndStart == null || bndStart.Type.Kind == DKind.I8);
            Validation.Assert(bndStop == null || bndStop.Type.Kind == DKind.I8);
            Validation.Assert(bndStep == null || bndStep.Type.Kind == DKind.I8);

            var ilw = IL;
            var tm = _typeManager;

            static bool PushArg(Impl impl, BoundNode arg, ref int cur)
            {
                if (arg == null)
                    return false;
                if (arg.IsNullValue)
                {
                    cur += arg.NodeCount;
                    return false;
                }
                cur = arg.Accept(impl, cur);
                return true;
            }

            MethodInfo meth;
            if (PushArg(this, bndStart, ref cur))
            {
                bool opt = bndStart.Type.IsOpt;
                if (PushArg(this, bndStop, ref cur))
                {
                    if (bndStop.Type.IsOpt)
                    {
                        if (opt)
                            meth = CodeGenUtil.SliceCreateOO;
                        else
                            meth = CodeGenUtil.SliceCreateRO;
                    }
                    else
                    {
                        if (opt)
                            meth = CodeGenUtil.SliceCreateOR;
                        else
                            meth = CodeGenUtil.SliceCreateRR;
                    }
                }
                else
                {
                    flags &= ~(SliceItemFlags.Stop | SliceItemFlags.StopBack | SliceItemFlags.StopStar);
                    if (opt)
                        meth = CodeGenUtil.SliceCreateO_;
                    else
                        meth = CodeGenUtil.SliceCreateR_;
                }
            }
            else
            {
                flags &= ~(SliceItemFlags.Start | SliceItemFlags.StartBack);
                if (PushArg(this, bndStop, ref cur))
                    meth = bndStop.Type.IsOpt ? CodeGenUtil.SliceCreate_O : CodeGenUtil.SliceCreate_R;
                else
                {
                    flags &= ~(SliceItemFlags.Stop | SliceItemFlags.StopBack | SliceItemFlags.StopStar);
                    meth = CodeGenUtil.SliceCreate__;
                }
            }

            if (bndStep == null)
            {
                ilw.Ldc_I8(0);
                PushType(typeof(long));
            }
            else if (bndStep.IsNullValue)
            {
                cur += bndStep.NodeCount;
                ilw.Ldc_I8(0);
                PushType(typeof(long));
            }
            else if (bndStep.Type.IsOpt)
            {
                var typeStep = bndStep.Type;
                tm.TryEnsureSysType(bndStep.Type, out var stStep).Verify();
                using var _ = EnsureLocOrArg(bndStep, ref cur, stStep, out var lai, load: false);
                tm.GenGetValueOrDefault(MethCur, typeStep, in lai);
                PushType(typeof(long));
            }
            else
                cur = bndStep.Accept(this, cur);

            if ((flags & (SliceItemFlags.Start | SliceItemFlags.Stop)) != 0)
            {
                flags |= SliceItemFlags.Step;
                IL.Ldc_I4((int)flags);
                PushType(typeof(SliceItemFlags));
            }

            EmitCall(meth);
            return cur;
        }

        protected override void PostVisitImpl(BndCastNumNode bnd, int idx)
        {
            AssertIdx(bnd, idx);
            DType typeSrc = bnd.Child.Type;
            DType typeDst = bnd.Type;

            PopType(typeSrc);
            _typeManager.GenCastNum(MethCur, typeSrc, typeDst);
            PushType(typeDst);
        }

        protected override void PostVisitImpl(BndCastRefNode bnd, int idx)
        {
            AssertIdx(bnd, idx);
            DType typeSrc = bnd.Child.Type;
            DType typeDst = bnd.Type;

            // Any record types involved should have exactly the same set of fields, so acceptance
            // should be true under both modes.
            Validation.Assert(typeDst.Accepts(typeSrc, union: true));
            Validation.Assert(typeDst.Accepts(typeSrc, union: false));
            Validation.Assert(typeSrc != typeDst);

            Type stSrc = GetSysType(typeSrc);
            Type stDst = GetSysType(typeDst);

            Validation.Assert(_typeManager.IsRefType(stSrc));
            Validation.Assert(_typeManager.IsRefType(stDst));
            Validation.Assert(stDst.IsAssignableFrom(stSrc));

            // Reference conversions shouldn't need any code.
            PopType(stSrc);
            PushType(stDst);
        }

        protected override void PostVisitImpl(BndCastBoxNode bnd, int idx)
        {
            AssertIdx(bnd, idx);
            DType typeSrc = bnd.Child.Type;
            DType typeDst = bnd.Type;

            Validation.Assert(typeSrc.IsValid);
            Validation.Assert(typeSrc.SeqCount == 0);
            Validation.Assert(!typeSrc.Kind.IsReferenceFriendly());
            Validation.Assert(!typeSrc.IsVacXxx);
            Validation.Assert(typeDst == DType.General);

            Type stSrc = GetSysType(typeSrc);
            Type stDst = GetSysType(typeDst);

            Validation.Assert(stDst == typeof(object));

            PopType(stSrc);
            if (!_typeManager.IsRefType(stSrc))
            {
                Validation.Assert(stSrc.IsValueType);
                IL.Box(stSrc);
            }
            PushType(stDst);
        }

        protected override void PostVisitImpl(BndCastOptNode bnd, int idx)
        {
            AssertIdx(bnd, idx);
            DType typeSrc = bnd.Child.Type;
            DType typeDst = bnd.Type;

            Validation.Assert(!typeSrc.IsOpt);
            Validation.Assert(typeDst.IsOpt);
            Validation.Assert(typeDst == typeSrc.ToOpt());

            PopType(typeSrc);
            _typeManager.GenWrapOpt(MethCur, typeSrc, typeDst);
            PushType(typeDst);
        }

        protected override void PostVisitImpl(BndCastVacNode bnd, int idx)
        {
            // Vacuous cast should have been reduced to default value.
            throw Unexpected();
        }

        private void GenCoalescePre(BndBinaryOpNode bnd, int idx)
        {
            AssertIdx(bnd, idx);
            Validation.Assert(bnd.Op == BinaryOp.Coalesce);
            Validation.Assert(bnd.Type == bnd.Arg1.Type);

            DType type0 = bnd.Arg0.Type;
            Validation.Assert(bnd.Type == type0 || bnd.Type == type0.ToReq());
            Type st0 = GetSysType(type0);

            int cur = idx + 1;
            Label labDone = default;
            if (_typeManager.IsRefType(st0))
            {
                Validation.Assert(GetSysType(bnd.Type) == st0);
                cur = bnd.Arg0.Accept(this, cur);
                PopType(st0);
                IL
                    .Dup()
                    .Brtrue(ref labDone)
                    .Pop();
            }
            else if (_typeManager.IsNullableType(st0))
            {
                using var _ = EnsureLocOrArg(bnd.Arg0, ref cur, st0, out var lai, load: false);
                Label labOther = default;

                _typeManager.GenHasValue(MethCur, type0, in lai);
                IL.Brfalse(ref labOther);

                if (bnd.Type == type0)
                    IL.LdLocArg(in lai);
                else
                {
                    Validation.Assert(bnd.Type == type0.ToReq());
                    _typeManager.GenGetValueOrDefault(MethCur, type0, in lai);
                }
                IL
                    .Br(ref labDone)
                    .MarkLabel(labOther);
            }
            else
                throw Unexpected("Unexpected system type in coalesce");

            cur = bnd.Arg1.Accept(this, cur);
            Validation.Assert(cur == idx + bnd.NodeCount);

            // This verifies that the arg1 type is assignable to the result type.
            PopType(bnd.Type);

            // Put the pure result type on the stack.
            PushType(bnd.Type);

            IL.MarkLabel(labDone);
        }

        private void GenShiftPre(BndBinaryOpNode bnd, int idx)
        {
            AssertIdx(bnd, idx);
            Validation.Assert(bnd.Op == BinaryOp.Shl | bnd.Op == BinaryOp.Shri | bnd.Op == BinaryOp.Shru);
            Validation.Assert(bnd.Type == bnd.Arg0.Type);
            Validation.Assert(bnd.Type.IsIntegralReq);
            Validation.Assert(bnd.Arg1.Type == DType.I8Req);

            var kind = bnd.Type.RootKind;
            bool unsSrc = kind.IsUx();
            bool left = bnd.Op == BinaryOp.Shl;
            bool oneFill = bnd.Op == BinaryOp.Shri;
            bool big = kind == DKind.IA;
            bool bit = kind == DKind.Bit;

            int cbSrc = kind.NumericSize();
            Validation.Assert(cbSrc >= 0);
            Validation.Assert((cbSrc == 0) == bit);
            Validation.Assert((cbSrc > 8) == big);
            int cbit = big ? -1 : bit ? 1 : cbSrc * 8;

            int cur = idx + 1;

            // REVIEW: The reducer should ensure the bounds in this test, but we don't count on them....
            if (bnd.Arg1.TryGetIntegral(out var amt) && amt > 0 && (amt < cbit || big && amt <= int.MaxValue))
            {
                Validation.Assert(!bit);

                cur = bnd.Arg0.Accept(this, cur);
                if (cbSrc < 4 && !left && oneFill == unsSrc)
                    IL.Conv_XX(cbSrc, uns: !oneFill);
                IL.Ldc_I4((int)amt);
                if (big)
                {
                    PushType(typeof(int));
                    EmitCall(left ? CodeGenUtil.IntShl : CodeGenUtil.IntShr);
                }
                else
                    IL.Shift(left, uns: !oneFill);
                if (cbSrc < 4 && left)
                    IL.Conv_XX(cbSrc, unsSrc);
                cur += bnd.Arg1.NodeCount;
            }
            else if (big)
            {
                Label labDone = default;
                cur = bnd.Arg0.Accept(this, cur);
                using var _ = EnsureLocOrArg(bnd.Arg1, ref cur, typeof(long), out var laiAmt, load: true);
                IL
                    .Ldc_I8(0).Ble(ref labDone)
                    .LdLocArg(in laiAmt)
                    .Call(CodeGenUtil.ClipShift)
                    .Call(left ? CodeGenUtil.IntShl : CodeGenUtil.IntShr)
                    .MarkLabel(labDone);
            }
            else if (bit)
            {
                cur = bnd.Arg0.Accept(this, cur);
                if (!oneFill)
                {
                    Label labDone = default;

                    IL
                        .Dup()
                        .Brfalse(ref labDone);
                    cur = bnd.Arg1.Accept(this, cur);
                    PopType(typeof(long));
                    IL
                        .Ldc_I8(0)
                        .Cgt()
                        .Xor()
                        .MarkLabel(labDone);
                }
            }
            else if (!oneFill)
            {
                Label labDone = default;
                Label labTooBig = default;

                int cur0 = cur;
                int cur1 = cur + bnd.Arg0.NodeCount;
                cur = cur1;
                using var _ = EnsureLocOrArg(bnd.Arg1, ref cur, typeof(long), out var laiAmt, load: true);
                IL
                    .Ldc_I8(cbit).Bge(ref labTooBig);

                int tmp = bnd.Arg0.Accept(this, cur0);
                Validation.Assert(tmp == cur1);

                IL
                    .LdLocArg(in laiAmt)
                    .Ldc_I8(0).Ble(ref labDone);
                if (cbSrc < 4 && !left && !unsSrc)
                    IL.Conv_XX(cbSrc, uns: true);
                IL
                    .LdLocArg(in laiAmt).Conv_I4()
                    .Shift(left, uns: true);
                if (cbSrc < 4 && left)
                    IL.Conv_XX(cbSrc, unsSrc);
                IL
                    .Br(ref labDone)
                    .MarkLabel(labTooBig);
                // Need zero of the correct size.
                GenIntConst(kind, 0);
                IL.MarkLabel(labDone);
            }
            else
            {
                Validation.Assert(oneFill);
                Validation.Assert(!left);
                Label labDone = default;
                Label labNotBig = default;

                cur = bnd.Arg0.Accept(this, cur);
                using var _ = EnsureLocOrArg(bnd.Arg1, ref cur, typeof(long), out var laiAmt, load: true);
                IL
                    .Ldc_I8(0).Ble(ref labDone);
                if (cbSrc < 4 && unsSrc)
                    IL.Conv_XX(cbSrc, uns: false);
                IL
                    .LdLocArg(in laiAmt)
                    .Dup().Ldc_I8(cbit).Blt(ref labNotBig)
                    .Pop().Ldc_I8(cbit - 1)
                    .MarkLabel(labNotBig)
                    .Conv_I4()
                    .Shr()
                    .MarkLabel(labDone);
            }
            Validation.Assert(cur == idx + bnd.NodeCount);
        }

        private void GenPowerPre(BndBinaryOpNode bnd, int idx)
        {
            AssertIdx(bnd, idx);
            Validation.Assert(bnd.Type.IsNumericReq);
            Validation.Assert(bnd.Arg0.Type == bnd.Type);

            int cur = idx + 1;
            var kind = bnd.Type.RootKind;
            switch (kind)
            {
            case DKind.R8:
                Validation.Assert(bnd.Arg1.Type == bnd.Type);
                cur = bnd.Arg0.Accept(this, cur);
                cur = bnd.Arg1.Accept(this, cur);
                EmitCall(CodeGenUtil.PowDbl);
                break;

            case DKind.I8:
            case DKind.U8:
                Validation.Assert(bnd.Arg1.Type == DType.I8Req || bnd.Arg1.Type == DType.U8Req);
                if (bnd.Arg1.TryGetIntegral(out var exp))
                {
                    if (exp <= 0)
                    {
                        IL.Ldc_I8(1);
                        cur += bnd.Arg0.NodeCount;
                    }
                    else
                    {
                        cur = bnd.Arg0.Accept(this, cur);
                        IL.Ldc_U8((ulong)exp);
                        PushType(typeof(ulong));
                        EmitCall(kind == DKind.U8 ? CodeGenUtil.PowU8 : CodeGenUtil.PowI8);
                    }
                    cur += bnd.Arg1.NodeCount;
                }
                else if (bnd.Arg1.Type.RootKind != DKind.U8)
                {
                    Label labGen = default;
                    Label labDone = default;
                    int cur0 = cur;
                    int cur1 = cur + bnd.Arg0.NodeCount;
                    cur = cur1;
                    using var _ = EnsureLocOrArg(bnd.Arg1, ref cur, typeof(long), out var laiExp, true);
                    IL
                        .Ldc_I8(0)
                        .Bgt(ref labGen)
                        .Ldc_I8(1)
                        .Br(ref labDone)
                        .MarkLabel(labGen);
                    int tmp = bnd.Arg0.Accept(this, cur0);
                    Validation.Assert(tmp == cur1);
                    IL.LdLocArg(in laiExp);
                    // We know that the exp is positive, so it's ok to treat it as u8.
                    PushType(typeof(ulong));
                    EmitCall(kind == DKind.U8 ? CodeGenUtil.PowU8 : CodeGenUtil.PowI8);
                    IL.MarkLabel(labDone);
                }
                else
                {
                    cur = bnd.Arg0.Accept(this, cur);
                    cur = bnd.Arg1.Accept(this, cur);
                    EmitCall(kind == DKind.U8 ? CodeGenUtil.PowU8 : CodeGenUtil.PowI8);
                }
                return;

            default:
                throw Unexpected();
            }
            Validation.Assert(cur == idx + bnd.NodeCount);
        }

        private void GenDivModPre(BndBinaryOpNode bnd, int idx)
        {
            AssertIdx(bnd, idx);
            Validation.Assert(bnd.Op == BinaryOp.IntDiv || bnd.Op == BinaryOp.IntMod);

            var type = bnd.Type;
            var kind = type.RootKind;
            Validation.Assert(type.IsIntegralReq);
            Validation.Assert(bnd.Arg0.Type == type);
            Validation.Assert(bnd.Arg1.Type == type);

            int cur = idx + 1;
            bool div = bnd.Op == BinaryOp.IntDiv;
            if (kind == DKind.IA)
            {
                cur = bnd.Arg0.Accept(this, cur);
                cur = bnd.Arg1.Accept(this, cur);
                EmitCall(div ? DivGen.Instance.MethInt : ModGen.Instance.MethInt);
                Validation.Assert(cur == idx + bnd.NodeCount);
                return;
            }

            bool uns = kind.IsUx();

            // Note: we have to special case den being zero or -1, since either case may lead to an exception.
            // For -1, the exception comes when the numerator is the "evil value".
            if (bnd.Arg1.TryGetIntegral(out var den))
            {
                Validation.Assert(!uns | den.Sign >= 0);

                // Denominator is constant, so no need for special checks.
                // Binder should handle the special cases:
                Validation.Assert(!den.IsZero);
                Validation.Assert(!den.IsOne);
                Validation.Assert(den != -1);

                cur = bnd.Arg0.Accept(this, cur);
                PopType(type);
                GenIntConst(kind, den);
                IL.DivMod(uns, div);
                cur += bnd.Arg1.NodeCount;
            }
            else
            {
                Label labZero = default;
                Label labDone = default;

                var st = _typeManager.GetSysTypeOrNull(bnd.Type);
                int cur0 = cur;
                int cur1 = cur + bnd.Arg0.NodeCount;
                cur = cur1;
                using var _ = EnsureLocOrArg(bnd.Arg1, ref cur, st, out var laiDen, load: true);

                // Avoid division by zero and by -1.
                if (!div)
                {
                    // Send -1 (for signed), 1, and 0 all to 0.
                    GenIntConst(kind, 1);
                    if (!uns)
                    {
                        IL.Add();
                        GenIntConst(kind, 2);
                    }
                    IL.Ble_Un(ref labZero);
                }
                else
                {
                    // Send 0 to 0.
                    IL.Brfalse(ref labZero);
                }

                int tmp = bnd.Arg0.Accept(this, cur0);
                Validation.Assert(tmp == cur1);
                PopType(type);
                IL.LdLocArg(in laiDen);

                if (div && !uns)
                {
                    // Handle -1
                    Label labNotNeg1 = default;
                    IL
                        .Dup()
                        .Ldc_Ix(-1, kind == DKind.I8)
                        .Bne_Un(ref labNotNeg1)
                        .Pop()
                        .Neg()
                        .Br(ref labDone)
                        .MarkLabel(labNotNeg1);
                }

                IL
                    .DivMod(uns, div)
                    .Br(ref labDone)
                    .MarkLabel(labZero);
                GenIntConst(kind, 0);
                IL.MarkLabel(labDone);
            }
            Validation.Assert(cur == idx + bnd.NodeCount);
            PushType(type);
        }

        private void GenMinMaxPre(BndBinaryOpNode bnd, int idx)
        {
            AssertIdx(bnd, idx);
            Validation.Assert(bnd.Op == BinaryOp.Min || bnd.Op == BinaryOp.Max);

            var type = bnd.Type;
            Validation.Assert(type.IsComparable);
            Validation.Assert(type == bnd.Arg0.Type);
            Validation.Assert(type == bnd.Arg1.Type);
            Validation.Assert(!type.HasReq);

            var min = bnd.Op == BinaryOp.Min;
            var kind = type.Kind;

            int cur = idx + 1;
            cur = bnd.Arg0.Accept(this, cur);
            switch (kind)
            {
            case DKind.IA:
                cur = bnd.Arg1.Accept(this, cur);
                EmitCall(min ? CodeGenUtil.IntMin : CodeGenUtil.IntMax);
                break;
            case DKind.R4:
                cur = bnd.Arg1.Accept(this, cur);
                EmitCall(min ? CodeGenUtil.R4Min : CodeGenUtil.R4Max);
                break;
            case DKind.R8:
                cur = bnd.Arg1.Accept(this, cur);
                EmitCall(min ? CodeGenUtil.R8Min : CodeGenUtil.R8Max);
                break;
            case DKind.Text:
                cur = bnd.Arg1.Accept(this, cur);
                EmitCall(min ? CodeGenUtil.StrMin : CodeGenUtil.StrMax);
                break;
            case DKind.Bit:
                cur = bnd.Arg1.Accept(this, cur);
                PopType(typeof(bool));
                PopType(typeof(bool));
                if (min)
                    IL.And();
                else
                    IL.Or();
                PushType(typeof(bool));
                break;

            default:
                {
                    var st = GetSysType(type);
                    Label labDone = default;

                    IL.Dup();
                    using var _ = EnsureLocOrArg(bnd.Arg1, ref cur, st, out var lai, load: true);
                    PushType(st);

                    switch (kind)
                    {
                    case DKind.Date:
                        EmitCall(min ? CodeGenUtil.DateLe : CodeGenUtil.DateGe);
                        IL.Brtrue(ref labDone);
                        PopType(typeof(bool));
                        break;
                    case DKind.Time:
                        EmitCall(min ? CodeGenUtil.TimeLe : CodeGenUtil.TimeGe);
                        IL.Brtrue(ref labDone);
                        PopType(typeof(bool));
                        break;
                    default:
                        Validation.Assert(kind.IsIxOrUx());
                        PopType(st);
                        PopType(st);
                        if (kind.IsUx())
                        {
                            if (min)
                                IL.Ble_Un(ref labDone);
                            else
                                IL.Bge_Un(ref labDone);
                        }
                        else
                        {
                            if (min)
                                IL.Ble(ref labDone);
                            else
                                IL.Bge(ref labDone);
                        }
                        break;
                    }

                    IL
                        .Pop()
                        .LdLocArg(in lai)
                        .MarkLabel(labDone);

                    PushType(st);
                }
                break;
            }
            Validation.Assert(cur == idx + bnd.NodeCount);
        }

        private void GenChronoAddSubPost(BndBinaryOpNode bnd)
        {
            Validation.Assert(bnd.Op == BinaryOp.ChronoAdd || bnd.Op == BinaryOp.ChronoSub);
            Validation.Assert(!bnd.Arg0.Type.IsOpt);
            Validation.Assert(!bnd.Arg1.Type.IsOpt);
            Validation.Assert(!bnd.Arg0.Type.IsSequence);
            Validation.Assert(!bnd.Arg1.Type.IsSequence);

            bool sub = bnd.Op == BinaryOp.ChronoSub;
            var kind = bnd.Type.RootKind;
            var kind0 = bnd.Arg0.Type.RootKind;
            var kind1 = bnd.Arg1.Type.RootKind;

            Validation.Assert(kind.IsChrono());
            Validation.Assert(kind0.IsChrono());
            Validation.Assert(kind1.IsChrono());

            // Signatures:
            // * D + T => D
            // * D - T => D
            // * D - D => T
            // * T + T => T
            // * T - T => T

            MethodInfo meth;
            if (kind0 == DKind.Date)
            {
                // * D + T => D
                // * D - T => D
                // * D - D => T
                if (kind1 == DKind.Date)
                {
                    Validation.Assert(sub);
                    Validation.Assert(kind == DKind.Time);
                    meth = CodeGenUtil.DateSubDate;
                }
                else
                {
                    Validation.Assert(kind1 == DKind.Time);
                    meth = sub ? CodeGenUtil.DateSubTime : CodeGenUtil.DateTimeAdd;
                }
            }
            else
            {
                Validation.Assert(kind1 == DKind.Time);
                meth = sub ? CodeGenUtil.TimeSubTime : CodeGenUtil.TimeAddTime;
            }

            EmitCall(meth);
        }

        private void GenChronoMulDivModPost(BndBinaryOpNode bnd)
        {
            // REVIEW: Code gen could be inlined.
            Validation.Assert(bnd.Op == BinaryOp.ChronoMul | bnd.Op == BinaryOp.ChronoDiv | bnd.Op == BinaryOp.ChronoMod);
            Validation.BugCheck(bnd.Arg0.Type == DType.TimeReq);

            MethodInfo meth;
            var type1 = bnd.Arg1.Type;
            var typeRes = bnd.Type;

            switch (bnd.Op)
            {
            case BinaryOp.ChronoMul:
                Validation.BugCheck(typeRes == DType.TimeReq);
                Validation.BugCheck(type1 == DType.R8Req || type1 == DType.I8Req);
                meth = bnd.Arg1.Type == DType.R8Req ? CodeGenUtil.TimeMulR8 : CodeGenUtil.TimeMulI8;
                break;

            case BinaryOp.ChronoDiv:
                if (typeRes == DType.TimeReq)
                {
                    if (type1 == DType.R8Req)
                        meth = CodeGenUtil.TimeDivR8;
                    else if (type1 == DType.I8Req)
                        meth = CodeGenUtil.TimeIntDivI8;
                    else
                        throw Validation.BugExcept();
                }
                else if (typeRes == DType.R8Req)
                {
                    Validation.BugCheck(type1 == DType.TimeReq);
                    meth = CodeGenUtil.TimeDivTime;

                }
                else
                {
                    Validation.BugCheck(typeRes == DType.I8Req);
                    Validation.BugCheck(type1 == DType.TimeReq);
                    meth = CodeGenUtil.TimeIntDivTime;
                }
                break;

            default:
                Validation.Assert(bnd.Op == BinaryOp.ChronoMod);
                Validation.BugCheck(typeRes == DType.TimeReq);
                Validation.BugCheck(type1 == DType.TimeReq);
                meth = CodeGenUtil.TimeIntModTime;
                break;
            }

            EmitCall(meth);
        }

        protected abstract void GenInPost(BndBinaryOpNode bnd);

        protected override bool PreVisitImpl(BndBinaryOpNode bnd, int idx)
        {
            AssertIdx(bnd, idx);

            switch (bnd.Op)
            {
            default:
                return true;

            case BinaryOp.Coalesce:
                GenCoalescePre(bnd, idx);
                return false;

            case BinaryOp.Shl:
            case BinaryOp.Shri:
            case BinaryOp.Shru:
                GenShiftPre(bnd, idx);
                return false;

            case BinaryOp.Power:
                GenPowerPre(bnd, idx);
                return false;

            case BinaryOp.IntDiv:
            case BinaryOp.IntMod:
                GenDivModPre(bnd, idx);
                return false;

            case BinaryOp.Min:
            case BinaryOp.Max:
                GenMinMaxPre(bnd, idx);
                return false;
            }
        }

        protected override void PostVisitImpl(BndBinaryOpNode bnd, int idx)
        {
            AssertIdx(bnd, idx);

            var op = bnd.Op;
            switch (op)
            {
            default:
                throw NYI("bop");

            case BinaryOp.Coalesce:
            case BinaryOp.Shl:
            case BinaryOp.Shri:
            case BinaryOp.Shru:
            case BinaryOp.IntDiv:
            case BinaryOp.IntMod:
                throw Unexpected("post bop");

            case BinaryOp.Has:
            case BinaryOp.HasCi:
            case BinaryOp.HasNot:
            case BinaryOp.HasCiNot:
                Validation.Assert(bnd.Type == DType.BitReq);
                Validation.Assert(bnd.Arg0.Type == DType.Text);
                Validation.Assert(bnd.Arg1.Type == DType.Text);
                if (op == BinaryOp.HasCi || op == BinaryOp.HasCiNot)
                    EmitCall(CodeGenUtil.StrHasCi);
                else
                    EmitCall(CodeGenUtil.StrHas);
                if (op == BinaryOp.HasNot || op == BinaryOp.HasCiNot)
                    IL.Ldc_I4(0).Ceq();
                break;

            case BinaryOp.In:
            case BinaryOp.InNot:
            case BinaryOp.InCi:
            case BinaryOp.InCiNot:
                GenInPost(bnd);
                break;

            case BinaryOp.ChronoAdd:
            case BinaryOp.ChronoSub:
                GenChronoAddSubPost(bnd);
                break;
            case BinaryOp.ChronoMul:
            case BinaryOp.ChronoDiv:
            case BinaryOp.ChronoMod:
                GenChronoMulDivModPost(bnd);
                break;
            }
        }

        protected override bool PreVisitImpl(BndVariadicOpNode bnd, int idx)
        {
            AssertIdx(bnd, idx);

            var type = bnd.Type;
            var kind = type.Kind;

            var args = bnd.Args;
            Validation.Assert(args.All(x => x.Type.Kind == kind));
            Validation.Assert(args.Any(x => x.Type.IsOpt) == type.IsOpt);

            var invs = bnd.Inverted;
            Validation.Assert(!invs.TestAtOrAbove(args.Length));

            int cur;
            switch (bnd.Op)
            {
            case BinaryOp.Or:
                Validation.Assert(invs.IsEmpty);
                GenOrPre(bnd, idx);
                return false;
            case BinaryOp.And:
                Validation.Assert(invs.IsEmpty);
                GenAndPre(bnd, idx);
                return false;
            case BinaryOp.StrConcat:
                Validation.Assert(invs.IsEmpty);
                cur = GenStrConcatPre(args, idx + 1);
                Validation.Assert(cur == idx + bnd.NodeCount);
                return false;
            case BinaryOp.TupleConcat:
                Validation.Assert(invs.IsEmpty);
                cur = GenTupleConcatPre(bnd.Type, args, idx + 1);
                Validation.Assert(cur == idx + bnd.NodeCount);
                return false;
            case BinaryOp.RecordConcat:
                Validation.Assert(invs.IsEmpty);
                GenRecordConcatPre(bnd, idx);
                return false;
            case BinaryOp.SeqConcat:
                Validation.Assert(invs.IsEmpty);
                cur = GenSeqConcatPre(bnd.Type, args, idx + 1);
                return false;
            }

            // REVIEW: We should be able to assert !type.HasReq.
            if (type.HasReq)
                throw Unexpected("variadic opt");

            cur = idx + 1;
            int len = args.Length;
            Validation.Assert(len > 0);
            if (kind == DKind.IA)
            {
                MethodInfo meth;
                MethodInfo methEnd = null;
                Integer v;
                switch (bnd.Op)
                {
                default:
                    throw Unexpected();

                case BinaryOp.Add:
                    meth = CodeGenUtil.IntAdd;
                    var methInv = CodeGenUtil.IntSub;
                    cur = args[0].Accept(this, cur);
                    if (invs.TestBit(0))
                        EmitCall(CodeGenUtil.IntNeg);
                    for (int i = 1; i < len; i++)
                    {
                        cur = args[i].Accept(this, cur);
                        EmitCall(invs.TestBit(i) ? methInv : meth);
                    }
                    Validation.Assert(cur == idx + bnd.NodeCount);
                    return false;

                case BinaryOp.Mul:
                    meth = CodeGenUtil.IntMul;
                    // Optimize -1 to negate. Should always be tail.
                    if (len > 1 && args[len - 1].TryGetIntegral(out v) && v == -1)
                        methEnd = CodeGenUtil.IntNeg;
                    break;
                case BinaryOp.BitOr:
                    meth = CodeGenUtil.IntOr;
                    break;
                case BinaryOp.BitAnd:
                    meth = CodeGenUtil.IntAnd;
                    break;
                case BinaryOp.BitXor:
                    meth = CodeGenUtil.IntXor;
                    // Optimize -1 to use Not. Should always be tail.
                    if (len > 1 && args[len - 1].TryGetIntegral(out v) && v == -1)
                        methEnd = CodeGenUtil.IntNot;
                    break;
                }

                Validation.Assert(invs.IsEmpty);
                cur = args[0].Accept(this, cur);
                int lim = methEnd == null ? len : len - 1;
                Validation.Assert(lim > 0);
                for (int i = 1; i < lim; i++)
                {
                    cur = args[i].Accept(this, cur);
                    EmitCall(meth);
                }

                if (methEnd != null)
                {
                    EmitCall(methEnd);
                    cur += args[lim].NodeCount;
                }
                Validation.Assert(cur == idx + bnd.NodeCount);

                return false;
            }
            else
            {
                // The switch sets these and puts the first operand on the stack, including
                // applying "inv" when needed.
                OpCode opc;
                OpCode opcInv = default;
                bool specEnd = false;
                Type st = GetSysType(type);
                switch (bnd.Op)
                {
                default:
                    throw Unexpected();

                case BinaryOp.Xor:
                    Validation.Assert(invs.IsEmpty);
                    Validation.Assert(kind == DKind.Bit);
                    opc = OpCodes.Xor;
                    // Tweak Xor with true to use IL.Ldc_I4(0).Ceq(), which is the standard pattern C# uses.
                    { specEnd = len >= 2 && args[len - 1].TryGetBool(out var v) && v; }
                    cur = args[0].Accept(this, cur);
                    break;

                case BinaryOp.Add:
                    Validation.Assert(kind.IsNumeric());
                    opc = OpCodes.Add;
                    opcInv = OpCodes.Sub;
                    cur = args[0].Accept(this, cur);
                    if (invs.TestBit(0))
                        IL.Neg();
                    break;
                case BinaryOp.Mul:
                    Validation.Assert(kind.IsNumeric());
                    opc = OpCodes.Mul;
                    // Optimize -1 to use IL.Neg(). Should always be tail.
                    if (kind.IsFractional())
                    {
                        specEnd = len >= 2 && args[len - 1].TryGetFractional(out var v) && v == -1;
                        opcInv = OpCodes.Div;
                        if (invs.TestBit(0))
                        {
                            GenFltConst(kind, 1);
                            cur = args[0].Accept(this, cur);
                            IL.Emit(opcInv);
                        }
                        else
                            cur = args[0].Accept(this, cur);
                    }
                    else
                    {
                        Validation.Assert(invs.IsEmpty);
                        specEnd = len >= 2 && args[len - 1].TryGetIntegral(out var v) && v == BndIntNode.AllOnes(kind);
                        cur = args[0].Accept(this, cur);
                    }
                    break;
                case BinaryOp.BitOr:
                    Validation.Assert(invs.IsEmpty);
                    Validation.Assert(kind.IsIntegral());
                    opc = OpCodes.Or;
                    cur = args[0].Accept(this, cur);
                    break;
                case BinaryOp.BitXor:
                    Validation.Assert(invs.IsEmpty);
                    Validation.Assert(kind.IsIntegral());
                    opc = OpCodes.Xor;
                    // Optimize all ones to use IL.Not(), possibly followed by conv. Should always be tail.
                    Validation.Assert(kind.IsIxOrUx());
                    { specEnd = kind != DKind.Bit && len >= 2 && args[len - 1].TryGetIntegral(out var v) && v == BndIntNode.AllOnes(kind); }
                    cur = args[0].Accept(this, cur);
                    break;
                case BinaryOp.BitAnd:
                    Validation.Assert(invs.IsEmpty);
                    Validation.Assert(kind.IsIntegral());
                    opc = OpCodes.And;
                    cur = args[0].Accept(this, cur);
                    break;
                }
                PopType(st);

                Validation.Assert(opcInv != default || invs.IsEmpty);

                int lim = specEnd ? len - 1 : len;
                for (int i = 1; i < lim; i++)
                {
                    cur = args[i].Accept(this, cur);
                    PopType(st);
                    IL.Emit(invs.TestBit(i) ? opcInv : opc);
                }

                if (specEnd)
                {
                    cur += args[lim].NodeCount;
                    switch (bnd.Op)
                    {
                    case BinaryOp.Xor:
                        IL.Ldc_I4(0).Ceq();
                        break;
                    case BinaryOp.Mul:
                        IL.Neg();
                        break;
                    case BinaryOp.BitXor:
                        Validation.Assert(kind != DKind.Bit);
                        IL.Not();
                        if (kind == DKind.U2)
                            IL.Conv_U2();
                        else if (kind == DKind.U1)
                            IL.Conv_U1();
                        break;
                    default:
                        Validation.Assert(false);
                        break;
                    }
                }
                Validation.Assert(cur == idx + bnd.NodeCount);

                PushType(st);
                return false;
            }
        }

        protected override void PostVisitImpl(BndVariadicOpNode bnd, int idx)
        {
            // Should have been handled in PreVisit.
            throw Unexpected();
        }

        protected void GenOrPre(BndVariadicOpNode bnd, int idx)
        {
            AssertIdx(bnd, idx);
            Validation.Assert(bnd.Op == BinaryOp.Or);
            var type = bnd.Type;
            var args = bnd.Args;
            Validation.Assert(type.Kind == DKind.Bit);
            Validation.Assert(args.Length >= 2);
            Label labDone = default;

            int cur = idx + 1;
            if (!type.IsOpt)
            {
                Validation.Assert(args[0].Type == DType.BitReq);
                cur = args[0].Accept(this, cur);
                PopType(typeof(bool));
                IL.Dup().Brtrue(ref labDone).Pop();
                for (int i = 1; ;)
                {
                    Validation.Assert(args[i].Type == DType.BitReq);
                    cur = args[i].Accept(this, cur);
                    PopType(typeof(bool));
                    if (++i >= args.Length)
                        break;
                    IL.Dup().Brtrue(ref labDone).Pop();
                }
                IL.MarkLabel(labDone);
                PushType(typeof(bool));
            }
            else
            {
                // The args may be either Req or Opt, but at least one arg is Opt.
                // Process only the BitReq arguments in this loop.
                int argCountReq = 0;
                Label labWrap = default;
                int cur0 = cur;
                for (int i = 0; i < args.Length; i++)
                {
                    var arg = args[i];
                    Validation.Assert(arg.Type.Kind == DKind.Bit);
                    if (arg.Type.IsOpt)
                    {
                        Validation.Assert(arg.Type == DType.BitOpt);
                        cur += arg.NodeCount;
                        continue;
                    }

                    Validation.Assert(arg.Type == DType.BitReq);
                    argCountReq++;
                    cur = arg.Accept(this, cur);
                    PopType(typeof(bool));
                    IL
                        .Dup()
                        .Brtrue(ref labWrap)
                        .Pop();
                }
                Validation.Assert(argCountReq < args.Length);

                bool needWrap = argCountReq > 0;
                if (argCountReq == args.Length - 1)
                {
                    // Only one BitOpt argument.
                    for (int i = 0; i < args.Length; i++)
                    {
                        var arg = args[i];
                        if (arg.Type.IsOpt)
                        {
                            cur0 = arg.Accept(this, cur0);
                            PopType(typeof(bool?));
                            IL.Br(ref labDone);
                            break;
                        }
                        cur0 += arg.NodeCount;
                    }
                }
                else
                {
                    var stDst = GetSysType(type);
                    Label labUseCur = default;
                    using var locCur = MethCur.AcquireLocal(stDst);
                    using var locHasValue = MethCur.AcquireLocal(typeof(int));
                    IL.Ldc_I4(1).Stloc(locHasValue);
                    // Process only the BitOpt arguments in this loop.
                    int argCount = argCountReq;
                    for (int i = 0; i < args.Length; i++)
                    {
                        var arg = args[i];
                        if (!arg.Type.IsOpt)
                        {
                            cur0 += arg.NodeCount;
                            continue;
                        }

                        cur0 = arg.Accept(this, cur0);
                        PopType(typeof(bool?));
                        IL.Stloc(locCur);
                        _typeManager.GenGetValueOrDefault(MethCur, arg.Type, locCur);
                        IL.Brtrue(ref labUseCur);
                        if (++argCount >= args.Length)
                            break;

                        Label labNext = default;
                        if (argCount > argCountReq + 1)
                        {
                            // Only need to read the HasValue local starting from
                            // the second opt arg.
                            IL.Ldloc(locHasValue);
                            IL.Brfalse(ref labNext);
                        }

                        _typeManager.GenHasValue(MethCur, arg.Type, locCur);
                        IL.Stloc(locHasValue);
                        IL.MarkLabelIfUsed(labNext);
                    }
                    Validation.Assert(argCount == args.Length);

                    IL
                        .Ldloc(locHasValue)
                        .Brtrue(ref labUseCur);
                    _typeManager.GenSetNull(MethCur, type, locCur);
                    IL
                        .MarkLabel(labUseCur)
                        .Ldloc(locCur);
                    if (needWrap)
                        IL.Br(ref labDone);
                }
                Validation.Assert(cur == idx + bnd.NodeCount);

                if (needWrap)
                {
                    IL.MarkLabel(labWrap);
                    _typeManager.GenWrapOpt(MethCur, type.ToReq(), type);
                    IL.MarkLabel(labDone);
                }
                PushType(typeof(bool?));
            }
        }

        protected void GenAndPre(BndVariadicOpNode bnd, int idx)
        {
            AssertIdx(bnd, idx);
            Validation.Assert(bnd.Op == BinaryOp.And);
            var type = bnd.Type;
            var args = bnd.Args;
            Validation.Assert(type.Kind == DKind.Bit);
            Validation.Assert(args.Length >= 2);
            Label labDone = default;

            int cur = idx + 1;
            if (!type.IsOpt)
            {
                Validation.Assert(args[0].Type == DType.BitReq);
                cur = args[0].Accept(this, cur);
                PopType(typeof(bool));
                IL.Dup().Brfalse(ref labDone).Pop();
                for (int i = 1; ;)
                {
                    Validation.Assert(args[i].Type == DType.BitReq);
                    cur = args[i].Accept(this, cur);
                    PopType(typeof(bool));
                    if (++i >= args.Length)
                        break;
                    IL.Dup().Brfalse(ref labDone).Pop();
                }
                IL.MarkLabel(labDone);
                PushType(typeof(bool));
            }
            else
            {
                // The args may be either Req or Opt, but at least one arg is Opt.
                // Process only the BitReq arguments in this loop.
                int argCount = 0;
                Label labWrap = default;
                int cur0 = cur;
                for (int i = 0; i < args.Length; i++)
                {
                    var arg = args[i];
                    var argType = arg.Type;
                    Validation.Assert(argType.Kind == DKind.Bit);
                    if (argType.IsOpt)
                    {
                        Validation.Assert(argType == DType.BitOpt);
                        cur += arg.NodeCount;
                        continue;
                    }

                    Validation.Assert(argType == DType.BitReq);
                    argCount++;
                    cur = arg.Accept(this, cur);
                    PopType(typeof(bool));
                    IL
                        .Dup()
                        .Brfalse(ref labWrap)
                        .Pop();
                }
                Validation.Assert(argCount < args.Length);
                Validation.Assert(cur == idx + bnd.NodeCount);

                bool needWrap = argCount > 0;
                if (argCount == args.Length - 1)
                {
                    // Only one BitOpt argument.
                    for (int i = 0; i < args.Length; i++)
                    {
                        var arg = args[i];
                        if (!arg.Type.IsOpt)
                            cur0 += arg.NodeCount;
                        else
                        {
                            cur0 = arg.Accept(this, cur0);
                            PopType(typeof(bool?));
                            IL.Br(ref labDone);
                            break;
                        }
                    }
                }
                else
                {
                    var stDst = GetSysType(type);
                    Label labUseCur = default;
                    using var locCur = MethCur.AcquireLocal(stDst);
                    using var locHasValue = MethCur.AcquireLocal(typeof(int));
                    IL.Ldc_I4(1).Stloc(locHasValue);
                    // Process only the BitOpt arguments in this loop.
                    for (int i = 0; i < args.Length; i++)
                    {
                        var arg = args[i];
                        var argType = arg.Type;
                        if (!argType.IsOpt)
                        {
                            cur0 += arg.NodeCount;
                            continue;
                        }

                        cur0 = arg.Accept(this, cur0);
                        PopType(typeof(bool?));
                        IL.Stloc(locCur);
                        _typeManager.GenGetValueOrDefault(MethCur, argType, locCur);
                        if (++argCount >= args.Length)
                            break;

                        Label labNext = default;
                        IL.Brtrue(ref labNext);
                        _typeManager.GenHasValue(MethCur, argType, locCur);
                        IL
                            .Brtrue(ref labUseCur)
                            .Ldc_I4(0)
                            .Stloc(locHasValue)
                            .MarkLabel(labNext);
                    }
                    Validation.Assert(argCount == args.Length);

                    // Continue processing the last argument.
                    // Just called GetValueOrDefault on the last argument.
                    IL
                        .Brfalse(ref labUseCur)
                        .Ldloc(locHasValue)
                        .Brtrue(ref labUseCur);
                    // Result is null.
                    _typeManager.GenSetNull(MethCur, type, locCur);
                    IL
                        .MarkLabel(labUseCur)
                        .Ldloc(locCur);
                    if (needWrap)
                        IL.Br(ref labDone);
                }

                if (needWrap)
                {
                    Validation.Assert(IL.IsLabelUsed(labWrap));
                    Validation.Assert(IL.IsLabelUsed(labDone));
                    IL.MarkLabel(labWrap);
                    _typeManager.GenWrapOpt(MethCur, type.ToReq(), type);
                    IL.MarkLabel(labDone);
                }
                PushType(typeof(bool?));
            }
        }

        protected int GenStrConcatPre(ArgTuple args, int cur)
        {
            Validation.Assert(!args.IsDefault);
            Validation.Assert(args.Length >= 1);

            // Handle small arg counts.
            MethodInfo meth;
            switch (args.Length)
            {
            case 1:
                // Convert null to empty string.
                Label labDone = default;
                cur = args[0].Accept(this, cur);
                PopType(typeof(string));
                IL
                    .Dup()
                    .Brtrue(ref labDone)
                    .Pop()
                    .Ldstr("")
                    .MarkLabel(labDone);
                PushType(typeof(string));
                return cur;
            case 2:
                cur = args[0].Accept(this, cur);
                cur = args[1].Accept(this, cur);
                meth = CodeGenUtil.StrConcat2;
                break;
            case 3:
                cur = args[0].Accept(this, cur);
                cur = args[1].Accept(this, cur);
                cur = args[2].Accept(this, cur);
                meth = CodeGenUtil.StrConcat3;
                break;
            case 4:
                cur = args[0].Accept(this, cur);
                cur = args[1].Accept(this, cur);
                cur = args[2].Accept(this, cur);
                cur = args[3].Accept(this, cur);
                meth = CodeGenUtil.StrConcat4;
                break;

            default:
                // Create an array of strings and fill it up.
                IL.Ldc_I4(args.Length).Newarr(typeof(string));
                PushType(typeof(string[]));
                for (int i = 0; i < args.Length; i++)
                {
                    IL.Dup().Ldc_I4(i);
                    cur = args[i].Accept(this, cur);
                    IL.Stelem_Ref();
                    PopType(typeof(string));
                }
                meth = CodeGenUtil.StrConcatArr;
                break;
            }

            EmitCall(meth);
            return cur;
        }

        protected int GenTupleConcatPre(DType type, ArgTuple args, int cur)
        {
            Validation.Assert(type.IsTupleReq);
            Validation.Assert(!args.IsDefault);
            Validation.Assert(args.Length >= 2);

            Type st = GetSysType(type);
            GenCreateTuple(type, st, withType: true);

            var types = type.GetTupleSlotTypes();
            int slot = 0;
            foreach (var arg in args)
            {
                Validation.Assert(arg.Type.IsTupleReq);
                if (arg is BndTupleNode btn)
                {
                    cur = GenTupleSlotStores(type, st, types, ref slot, btn.Items, cur + 1);
                    continue;
                }

                var typesInner = arg.Type.GetTupleSlotTypes();
                Validation.AssertIndexInclusive(typesInner.Length, types.Length - slot);
                if (typesInner.Length == 0)
                    continue;

                if (typesInner.Length == 1)
                {
                    Validation.Assert(typesInner[0] == types[slot]);
                    IL.Dup();
                    cur = arg.Accept(this, cur);
                    PopType(arg.Type);
                    GenLoadSlot(arg.Type, 0, types[slot]);
                    GenStoreSlot(type, st, slot, types[slot]);
                    slot++;
                    continue;
                }

                var stInner = GetSysType(arg.Type);
                using var locInner = EnsureLocOrArg(arg, ref cur, stInner, out var laiInner, false);
                for (int slotInner = 0; slotInner < typesInner.Length; slotInner++, slot++)
                {
                    Validation.Assert(typesInner[slotInner] == types[slot]);
                    IL
                        .Dup()
                        .LdLocArg(in laiInner);
                    GenLoadSlot(arg.Type, slotInner, types[slot]);
                    GenStoreSlot(type, st, slot, types[slot]);
                }
            }
            Validation.Assert(slot == types.Length);
            return cur;
        }

        protected virtual void GenLoadRrti(RecordRuntimeTypeInfo rrti)
        {
            Validation.AssertValue(rrti);
            GenLoadConst(rrti);
        }

        public virtual TypeManager.RecordGenerator CreateRecordGenerator(DType type)
        {
            Validation.BugCheckParam(type.IsRecordXxx, nameof(type));
            return _typeManager.CreateRecordGenerator(MethCur, type, GenLoadRrti);
        }

        protected void GenRecordConcatPre(BndVariadicOpNode bnd, int idx)
        {
            AssertIdx(bnd, idx);
            Validation.Assert(bnd.Op == BinaryOp.RecordConcat);
            Validation.Assert(bnd.Type.IsRecordReq);

            var type = bnd.Type;
            var args = bnd.Args;
            Validation.Assert(args.Length >= 2);

            using var rg = CreateRecordGenerator(type);

            // We traverse backwards so the position computation is non-standard.
            int lim = idx + bnd.NodeCount;
            var fields = new HashSet<DName>();
            for (int i = args.Length; --i >= 0;)
            {
                var arg = args[i];
                Validation.Assert(arg.Type.IsRecordReq);
                var stInner = GetSysType(arg.Type);
                if (arg is BndRecordNode brn)
                {
                    int tmp = GenRecordFieldStores(rg, brn.Items, fields, lim - arg.NodeCount + 1);
                    Validation.Assert(tmp == lim);
                    lim -= arg.NodeCount;
                    continue;
                }

                LocArgInfo laiInner = default;
                LocEnt locInner = default;
                try
                {
                    foreach (var tn in arg.Type.GetNames())
                    {
                        if (!fields.Add(tn.Name))
                            continue;

                        Validation.Assert(type.GetNameTypeOrDefault(tn.Name) == tn.Type);
                        if (laiInner.SysType == null)
                        {
                            int tmp = lim - arg.NodeCount;
                            locInner = EnsureLocOrArg(arg, ref tmp, stInner, out laiInner, false);
                            Validation.Assert(tmp == lim);
                        }
                        rg.SetFromLocalField(tn.Name, arg.Type, tn.Name, in laiInner);
                    }
                    lim -= arg.NodeCount;
                }
                finally
                {
                    locInner.Dispose();
                }
            }
            Validation.Assert(fields.Count == type.FieldCount);
            Validation.Assert(lim == idx + 1);

            rg.Finish();
            PushType(type);
        }

        protected int GenSeqConcatPre(DType type, ArgTuple args, int cur)
        {
            Validation.Assert(type.IsSequence);
            Validation.Assert(args.Length >= 2);
            Validation.Assert(args.All(a => a.Type == type));

            // Handle small arg counts.
            Type st = GetSysType(type);
            Type stItem = GetSysType(type.ItemTypeOrThis);
            MethodInfo meth;
            switch (args.Length)
            {
            case 2:
                cur = args[0].Accept(this, cur);
                cur = args[1].Accept(this, cur);
                meth = CodeGenUtil.SeqConcat2(stItem);
                break;
            case 3:
                cur = args[0].Accept(this, cur);
                cur = args[1].Accept(this, cur);
                cur = args[2].Accept(this, cur);
                meth = CodeGenUtil.SeqConcat3(stItem);
                break;
            case 4:
                cur = args[0].Accept(this, cur);
                cur = args[1].Accept(this, cur);
                cur = args[2].Accept(this, cur);
                cur = args[3].Accept(this, cur);
                meth = CodeGenUtil.SeqConcat4(stItem);
                break;

            default:
                // Create an array of sequences and fill it up.
                IL.Ldc_I4(args.Length).Newarr(st);
                PushType(st.MakeArrayType());
                for (int i = 0; i < args.Length; i++)
                {
                    IL.Dup().Ldc_I4(i);
                    cur = args[i].Accept(this, cur);
                    IL.Stelem_Ref();
                    PopType(st);
                }
                meth = CodeGenUtil.SeqConcatArr(stItem);
                break;
            }

            EmitCall(meth);
            GenWrap(default, stItem);
            return cur;
        }

        private int VisitArgCore(BndCallNode call, int slot, BoundNode arg, int cur)
        {
            // InitScope coordinates with this behavior:
            // * If arg is the value of a "with" or "guard" scope and is just a "local" or "parameter",
            //   then defer loading the value. That is, don't generate the load.
            // * If arg is the value of a "guard" scope and is a field ref, only load the record, not
            //   the field value.

            if (call.Traits.IsScope(slot, out int iscope))
            {
                Validation.AssertIndex(iscope, call.Scopes.Length);
                var scope = call.Scopes[iscope];
                switch (scope.Kind)
                {
                case ScopeKind.Guard:
                    if (arg is BndGetFieldNode bgfn)
                    {
                        // For a nullable field, load the record, not the field. InitScope will test for null directly on
                        // the record, rather than load a Nullable<T> before testing.
                        if (_typeManager.IsNullableType(GetSysType(arg.Type)))
                        {
                            arg = bgfn.Record;
                            cur++;
                        }
                        break;
                    }
                    goto case ScopeKind.With;

                case ScopeKind.With:
                    if (!IsLocOrArg(arg, cur, out var lai))
                        break;

                    // Don't load the loc/arg.
                    Validation.Assert(_typeManager.GetSysTypeOrNull(arg.Type) == lai.SysType);
                    return cur + arg.NodeCount;
                }
            }

            return arg.Accept(this, cur);
        }

        /// <summary>
        /// If the indicated slot is in one of the slot ranges, return
        /// the index of the range. Otherwise, return -1.
        /// </summary>
        protected int FindRng(int slot, RngTuple rngs)
        {
            for (int irng = 0; irng < rngs.Length; irng++)
            {
                var rng = rngs[irng];
                if (slot < rng.Min)
                    return -1;
                if (slot < rng.Lim)
                    return irng;
            }
            return -1;
        }

        /// <summary>
        /// For a call arg that is in an array range, this is called before arg is "visited".
        /// Generates the necessary setup code to be able to store the value in the array.
        /// Must be properly balanced with <see cref="PostArrayArg(Type)"/>.
        /// </summary>
        protected void PreArrayArg(FrameInfo frame, int irng, RngTuple rngs, int slot, Type st)
        {
            Validation.AssertIndex(irng, rngs.Length);

            var rng = rngs[irng];
            Validation.Assert(rng.Min <= slot & slot < rng.Lim);

            // If this is the first item, create the array.
            if (slot == rng.Min)
            {
                frame.Meth.Il
                    .Ldc_I4(rng.Count)
                    .Newarr(st);
                frame.Push(st.MakeArrayType());
            }
            else
                Validation.Assert(st.MakeArrayType() == frame.Peek());

            frame.Meth.Il
                .Dup()
                .Ldc_I4(slot - rng.Min);
        }

        /// <summary>
        /// For a call arg that is in an array range this is called after the arg is "visited".
        /// Assumes that <see cref="PreArrayArg(FrameInfo, int, RngTuple, int, Type)"/> was called
        /// properly. Generates code to store the value in the array.
        /// </summary>
        protected void PostArrayArg(Type st)
        {
            IL.Stelem(st);
            PopType(st);
            Validation.Assert(_frameCur.Peek() == st.MakeArrayType());
        }

        protected override int VisitNonNestedCallArg(BndCallNode call, int slot, BoundNode arg, int cur)
        {
            var (c, rngs) = _arrayRanges.Peek();
            Validation.Assert(c == call);

            var st = GetSysType(arg.Type);
            int irng = FindRng(slot, rngs);

            if (irng >= 0)
                PreArrayArg(_frameCur, irng, rngs, slot, st);
            cur = VisitArgCore(call, slot, arg, cur);
            if (irng >= 0)
                PostArrayArg(st);
            return cur;
        }

        protected string GetNestedCallArgFnName(RexlOper oper, int slot)
        {
            return string.Format("{0}_{1}", oper.Name.Value, slot);
        }

        protected override int VisitNestedCallArg(BndCallNode call, int slot, BoundNode arg, int cur)
        {
            Validation.AssertValue(call);
            var traits = call.Traits;
            Validation.Assert(traits.IsNested(slot));

            // We should be the "top" nested arg.
            var nested = _nestedCur;
            _refMaps.AssertNestedArg(nested, call, slot);

            var (c, rngs) = _arrayRanges.Peek();
            Validation.Assert(c == call);

            var st = GetSysType(arg.Type);
            int irng = FindRng(slot, rngs);

            // There are two cases that this code handles:
            // * A delegate is not needed; all scope values are stored in locals. eg WithFunc.
            // * All scopes are parameters to the delegate that we generate here. eg Map, Zip, CrossJoin, Scan, etc.

            if (!nested.NeedsDelegate)
            {
                // The outer scopes should have been added to the current frame's ScopeMap,
                // so no need to do anything special!
                if (irng >= 0)
                    PreArrayArg(_frameCur, irng, rngs, slot, st);
                cur = VisitArgCore(call, slot, arg, cur);
                if (irng >= 0)
                    PostArrayArg(st);
                return cur;
            }

            // First generate the delegate type, which means determining the parameter types.
            // We already know the return type, namely st.

            // Build the parameter types in reverse order. For k parameters, the top k active scopes should
            // be owned by the BndCallNode!
            var scopeTop = GetTopScope();
            Validation.AssertValue(scopeTop);

            var sts = new List<Type>();
            var ownedScopes = new List<RefMaps.ScopeInfo>();
            var scopeCur = scopeTop;
            int upCount = 0;
#if DEBUG
            // Need to ensure we aren't adding more than one parameter per index.
            // This tracks the iidx's we have included.
            var includedIndices = new HashSet<int>();
#endif
            for (int slotCur = slot; --slotCur >= 0;)
            {
                if (traits.IsNestedTail(slotCur))
                    break;

                if (!traits.IsScope(slotCur, out int iscope, out int iidx, out bool firstForIdx))
                    continue;

                Validation.AssertIndex(iscope, call.Scopes.Length);
                Validation.Assert(call.Scopes[iscope] == scopeCur.Scope);
                Validation.Assert(scopeCur != _frameCur.TopScope);
                Validation.Assert(scopeCur.Owner == call);
                Validation.Assert(!scopeCur.IsIndex && scopeCur.Scope.Kind.IsLoopScope());
                Validation.Assert(scopeCur.Slot == slotCur);
#if DEBUG
                if (scopeCur.Outer.VerifyValue().IsIndex)
                {
                    Validation.AssertIndex(iidx, call.Indices.Length);
                    Validation.Assert(firstForIdx);
                    Validation.Assert(call.Indices[iidx] == scopeCur.Outer.Scope);
                }
#endif
                if (traits.IsScopeActive(slot, upCount))
                {
                    ownedScopes.Add(scopeCur);
                    sts.Add(GetSysType(scopeCur.Scope.Type));

                    if (firstForIdx)
                    {
                        Validation.Assert(iidx >= 0);
                        if (scopeCur.Outer.IsIndex)
                        {
                            var scopeIdx = scopeCur.Outer;
#if DEBUG
                            Validation.Assert(scopeIdx.Slot == slotCur);
                            Validation.Assert(!scopeIdx.Outer.IsIndex);
                            includedIndices.Add(iidx).Verify();
                            if (slotCur > 0)
                            {
                                traits.IsScope(slotCur - 1, out int iscopePrev, out int iidxPrev, out _);
                                Validation.Assert(iscopePrev < iscope);
                                Validation.Assert(iidxPrev < iidx);
                            }
#endif
                            ownedScopes.Add(scopeIdx);
                            sts.Add(typeof(long));
                        }
                        else if (_codeGen.GetOperGenerator(call.Oper)?.NeedsIndexParam(call, slot, iidx) ?? false)
                        {
#if DEBUG
                            includedIndices.Add(iidx).Verify();
#endif
                            // Don't need to remap this index scope; we just want to add the param.
                            ownedScopes.Add(null);
                            sts.Add(typeof(long));
                        }
                    }
                }

                upCount++;
                scopeCur = scopeCur.Outer;
                if (firstForIdx && call.Indices[iidx] != null)
                {
                    Validation.Assert(scopeCur.IsIndex);
                    Validation.Assert(call.Indices[iidx] == scopeCur.Scope);
                    Validation.Assert(scopeCur.Slot == slotCur);
                    scopeCur = scopeCur.Outer;
                }

                Validation.AssertValue(scopeCur);
            }

            sts.Reverse();
            ownedScopes.Reverse();

            // We should have digested at least one scope.
            Validation.Assert(sts.Count > 0);
            Validation.Assert(sts.Count == ownedScopes.Count);
            // We should have exhausted this call's scopes.
            Validation.Assert(scopeCur.Owner != call);

            // Build the scope map from the owned scopes list.
            ScopeMap scopeMap = null;
            for (int i = 0; i < ownedScopes.Count; i++)
            {
                var scope = ownedScopes[i];
                if (scope != null)
                    Util.Add(ref scopeMap, scope.Index, LocArgInfo.FromArg(i + 1, sts[i]));
            }

            var frameOuter = _frameCur;

            StartFunctionCore(
                GetNestedCallArgFnName(call.Oper, slot),
                scopeMap, FindExtScopes(scopeCur, nested), UsesGlobals(nested), UsesExecCtx(nested),
                st, sts.ToArray());

            var stDel = _frameCur.DelegateType;
            if (irng >= 0)
                PreArrayArg(frameOuter, irng, rngs, slot, stDel);

            // Generate the code for the arg.
            cur = arg.Accept(this, cur);

            EndFunctionCore();
            Validation.Assert(_frameCur == frameOuter);

            if (irng >= 0)
                PostArrayArg(stDel);

            return cur;
        }

        protected override bool PreVisitImpl(BndIfNode bnd, int idx)
        {
            AssertIdx(bnd, idx);
            Validation.Assert(bnd.CondValue.Type == DType.BitReq);
            Validation.Assert(bnd.TrueValue.Type == bnd.Type);
            Validation.Assert(bnd.FalseValue.Type == bnd.Type);

            DType type = bnd.Type;
            Label labFalse = default;
            Label labDone = default;

            int cur = idx + 1;

            cur = bnd.CondValue.Accept(this, cur);
            PopType(typeof(bool));
            IL.Brfalse(ref labFalse);

            cur = bnd.TrueValue.Accept(this, cur);
            PopType(type);
            IL.Br(ref labDone);

            IL.MarkLabel(labFalse);
            cur = bnd.FalseValue.Accept(this, cur);
            PopType(type);

            IL.MarkLabel(labDone);

            Validation.Assert(cur == idx + bnd.NodeCount);
            PushType(type);
            return false;
        }

        protected override void PostVisitImpl(BndIfNode bnd, int idx)
        {
            // Should be handled in PreVisit.
            throw Unexpected("post if");
        }

        protected override bool PreVisitImpl(BndRecordNode bnd, int idx)
        {
            AssertIdx(bnd, idx);

            DType type = bnd.Type;
            Validation.Assert(type.IsRecordReq);

            int cur = idx + 1;
            using var rg = CreateRecordGenerator(type);
            cur = GenRecordFieldStores(rg, bnd.Items, null, cur);
            Validation.Assert(cur == idx + bnd.NodeCount);
            rg.Finish();
            PushType(type);

            return false;
        }

        private int GenRecordFieldStores(
            TypeManager.RecordGenerator rg, NamedItems items, HashSet<DName> fields, int cur)
        {
            Validation.AssertValue(rg);
            Validation.AssertValueOrNull(fields);

            foreach (var (name, value) in items.GetPairs())
            {
                if (fields != null && !fields.Add(name))
                {
                    cur += value.NodeCount;
                    continue;
                }
                if (value.IsNullValue)
                {
                    cur += value.NodeCount;
                    continue;
                }
                if (value is BndCastOptNode bcon)
                {
                    Validation.Assert(bcon.Type.HasReq);
                    Validation.Assert(bcon.ChildCount == 1);
                    var val = bcon.Child;
                    Validation.Assert(val.Type == bcon.Type.ToReq());
                    rg.SetFromStackPre(name, bcon.Type, fromReq: true);
                    cur = val.Accept(this, cur + 1);
                    PopType(val.Type);
                    rg.SetFromStackPost();
                }
                else if (value.Type.HasReq && value is BndGetFieldNode bgfn && _typeManager.IsNullableType(GetSysType(value.Type)))
                {
                    var rec = bgfn.Record;
                    var stRec = GetSysType(rec.Type);
                    Validation.Assert(bgfn.ChildCount == 1);
                    cur++;
                    using var ent = EnsureLocOrArg(rec, ref cur, stRec, out var laiRec, load: false);
                    rg.SetFromLocalField(name, rec.Type, bgfn.Name, in laiRec);
                }
                else
                {
                    rg.SetFromStackPre(name, value.Type);
                    cur = value.Accept(this, cur);
                    PopType(value.Type);
                    rg.SetFromStackPost();
                }
            }
            return cur;
        }

        protected override void PostVisitImpl(BndRecordNode bnd, int idx)
        {
            // Should be handled in PreVisit.
            throw Unexpected("post rec");
        }

        protected override bool PreVisitImpl(BndTupleNode bnd, int idx)
        {
            AssertIdx(bnd, idx);

            DType type = bnd.Type;
            Validation.Assert(type.IsTupleReq);

            Type st = GetSysType(type);
            GenCreateTuple(type, st, withType: true);

            var types = type.GetTupleSlotTypes();
            Validation.Assert(types.Length == bnd.Items.Length);
            int slot = 0;
            int cur = GenTupleSlotStores(type, st, types, ref slot, bnd.Items, idx + 1);
            Validation.Assert(cur == idx + bnd.NodeCount);
            Validation.Assert(slot == types.Length);

            return false;
        }

        private int GenTupleSlotStores(DType typeDst, Type stDst, ReadOnly.Array<DType> typesDst,
            ref int slot, ArgTuple args, int cur)
        {
            Validation.Assert(typeDst.IsTupleReq);
            Validation.Assert(typeDst.TupleArity == typesDst.Length);
            Validation.AssertIndexInclusive(slot, typesDst.Length - args.Length);

            foreach (var arg in args)
            {
                Validation.Assert(typesDst[slot] == arg.Type);
                IL.Dup();
                cur = arg.Accept(this, cur);
                GenStoreSlot(typeDst, stDst, slot, typesDst[slot]);
                PopType(arg.Type);
                slot++;
            }
            return cur;
        }

        protected override void PostVisitImpl(BndTupleNode bnd, int idx)
        {
            // Should be handled in PreVisit.
            throw Unexpected("post tup");
        }

        protected override bool PreVisitSetFields(BndSetFieldsNode bsf, int idx)
        {
            AssertIdx(bsf, idx);
            Validation.Assert(!bsf.HasErrors);
            Validation.Assert(bsf.Type.IsRecordReq);

            var src = bsf.Source;
            DType typeSrc = src.Type;
            DType typeDst = bsf.Type;
            Validation.Assert(typeSrc.IsRecordReq);
            Validation.Assert(typeDst.IsRecordReq);

            var items = bsf.Additions;
            var scope = bsf.Scope;

            int cur = idx + 1;

            // For the no-additions case, we need to evaluate and put it in a local.
            LocArgInfo laiSrc = default;
            using var ent = scope == null ? EnsureLocOrArg(src, ref cur, GetSysType(typeSrc), out laiSrc, false) : default;

            if (scope != null)
            {
                Validation.Assert(scope.Kind == ScopeKind.With);
                Validation.Assert(items.Count > 0);

                if (!IsLocOrArg(src, cur, out laiSrc))
                    cur = src.Accept(this, cur);
                else
                    cur += src.NodeCount;

                // Init the scope.
                bool isValid = scope.IsValidArg(src);
                InitScope(scope, bsf, idx, -1, isValid);
                PushScope(scope, bsf, idx, -1, isValid);
                Validation.Assert(_scopeCur.Scope == scope);
                Util.TryGetValue(_frameCur.ScopeMap, _scopeCur.Index, out laiSrc).Verify();
            }

            // Create the destination record.
            using var rg = CreateRecordGenerator(typeDst);

            // First copy new fields into the destination record.
            HashSet<DName> names = null;
            if (items.Count > 0)
            {
                names = new HashSet<DName>(items.Count);
                int slot = 0;
                foreach (var (nameFld, arg) in items.GetPairs())
                {
                    Validation.Assert(nameFld.IsValid);
                    Validation.Assert(typeDst.TryGetNameType(nameFld, out DType typeFld) && typeFld == arg.Type);
                    names.Add(nameFld);

                    rg.SetFromStackPre(nameFld, arg.Type);

                    PushNestedArg(bsf, idx, slot, needsDelegate: false);
                    cur = arg.Accept(this, cur);
                    PopNestedArg();

                    rg.SetFromStackPost();
                    PopType(arg.Type);
                    slot++;
                }
                Validation.Assert(slot == items.Count);
            }
            Validation.Assert(cur == idx + bsf.NodeCount);

            // Copy remaining fields from the source.
            foreach (var tn in typeDst.GetNames())
            {
                if (names != null && names.Contains(tn.Name))
                    continue;

                Validation.BugCheck(typeSrc.TryGetNameType(tn.Name, out DType typeFld));
                Validation.BugCheck(typeFld == tn.Type);
                rg.SetFromLocalField(tn.Name, typeSrc, tn.Name, in laiSrc);
            }

            // Cleanup.
            if (scope != null)
            {
                PopScope(scope);
                DisposeScope(scope, bsf, idx, -1);
            }

            rg.Finish();
            PushType(typeDst);

            return false;
        }

        protected override void PostVisitImpl(BndSetFieldsNode bnd, int idx)
        {
            throw Unexpected("set-fields post");
        }

        protected override bool PreVisitCall(BndCallNode call, int idx)
        {
            AssertIdx(call, idx);

            // Should only code-gen fully certified calls.
            Validation.BugCheckParam(call.CertifiedFull, nameof(call));

            if (call.Oper.IsProc && !_frameCur.IsProc)
                throw Unexpected("Unexpected proc");

            if (call.Oper is GroupByFunc)
                throw Unexpected();
            if (call.Oper is SetFieldsFunc)
                throw Unexpected();

            // Get the operation generator, if there is one.
            var gener = _codeGen.GetOperGenerator(call.Oper);

            if (call.Scopes.Length == 0)
            {
                if (gener is not null)
                {
                    var ilwCur = MethCur.Il;
                    int posCur = ilwCur.Position;
                    bool wrote = gener.TryGenSpecial(this, call, idx, out Type stRet, out var wrap);
                    Validation.Assert(ilwCur == MethCur.Il);
                    if (wrote)
                    {
                        PushType(stRet);
                        Validation.Assert(ilwCur.Position >= posCur);
                        Validation.BugCheck(ilwCur.Position > posCur);
                        if (call.Type.SeqCount > 0)
                            GenWrap(wrap, call.Type.ItemTypeOrThis);
                        PeekType(call.Type);
                        return false;
                    }
                    Validation.Assert(posCur == ilwCur.Position);
                }
            }
            else if (call.Scopes.Any(s => s.Kind == ScopeKind.Guard))
            {
                // REVIEW: Generalize this to other possible patterns.
                if (!(call.Oper is WithFunc))
                    throw Unexpected("Unrecognized guard function");
                if (call.Scopes.Length != call.Args.Length - 1)
                    throw Unexpected("Unrecognized guard pattern");

                Validation.Assert(!call.Scopes.Any(s => !s.IsIndex && s.Kind.IsLoopScope()));
                Validation.Assert(call.Type.IsOpt);

                // We need a label for returning null.
                Util.Add(ref _frameCur.NullLabels, (call, IL.DefineLabel()));

                PushArrRanges(call, RngTuple.Empty);
                return true;
            }

            PushArrRanges(call, gener is not null ? gener.GetArrayRanges(call) : RngTuple.Empty);
            return base.PreVisitCall(call, idx);
        }

        protected override void PostVisitImpl(BndCallNode call, int idx)
        {
            AssertIdx(call, idx);

            var (callRng, rngs) = PopArrRanges();
            Validation.Assert(callRng == call);

            if (call.Scopes.Any(s => s.Kind == ScopeKind.Guard))
            {
                // REVIEW: Generalize this to other possible patterns.
                Validation.Assert(call.Oper is WithFunc);
                Validation.Assert(call.Scopes.Length == call.Args.Length - 1);
                Validation.Assert(!call.Scopes.Any(s => s.Kind.IsLoopScope()));
                Validation.Assert(call.Type.IsOpt);
                Validation.Assert(rngs.IsDefaultOrEmpty);

                // Generate the null handling code.
                Validation.Assert(Util.Size(_frameCur.NullLabels) > 0);
                (var owner, var labNull) = Util.Pop(_frameCur.NullLabels);
                Validation.Assert(call == owner);
                Validation.Assert(labNull != default);

                Label labDone = default;

                // Wrap the value on the stack.
                _typeManager.GenWrapOpt(MethCur, call.Args[call.Args.Length - 1].Type, call.Type);
                IL
                    .Br(ref labDone)
                    .MarkLabel(labNull);
                GenLoadNull(call.Type);
                IL.MarkLabel(labDone);
                return;
            }

            if (call.Oper is WithFunc)
            {
                // The value should be on the top of the execution stack. Nothing more to do.
                Validation.Assert(call.Type == call.Args[call.Args.Length - 1].Type);
                Validation.Assert(rngs.IsDefaultOrEmpty);
                return;
            }

            int carg = call.Args.Length;
            int cargArr = 0;
            int iarg = carg;
            for (int irng = rngs.Length; --irng >= 0;)
            {
                var rng = rngs[irng];
                Validation.Assert(rng.Count >= 2);
                Validation.Assert(rng.Lim <= iarg);
                cargArr += rng.Count;
                iarg = rng.Min;
            }

            var sts = new Type[carg - cargArr + rngs.Length];
            iarg = carg;
            int ist = sts.Length;
            for (int irng = rngs.Length; ;)
            {
                int lim = --irng < 0 ? 0 : rngs[irng].Lim;
                while (--iarg >= lim)
                {
                    if (call.Traits.GetScopeKind(iarg) == ScopeKind.With)
                        sts[--ist] = null;
                    else
                        sts[--ist] = _frameCur.Pop();
                }
                if (irng < 0)
                    break;
                var st = _frameCur.Pop();
                Validation.Assert(st.IsArray);
                sts[--ist] = st;
                iarg = rngs[irng].Min;
            }
            Validation.Assert(ist == 0);

            var gener = _codeGen.GetOperGenerator(call.Oper);

            if (gener is not null)
            {
                var ilwCur = MethCur.Il;
                int posCur = ilwCur.Position;
                bool wrote = gener.TryGenCode(this, call, rngs, sts, out Type stRet, out var wrap);
                Validation.Assert(ilwCur == MethCur.Il);

                if (wrote)
                {
                    PushType(stRet);
                    Validation.Assert(ilwCur.Position >= posCur);
                    if (ilwCur.Position == posCur)
                    {
                        // No code was written, so the src type had better be assignable to the dst type.
                        Validation.BugCheck(call.Args.Length == 1);
                        var typeSrc = call.Args[0].Type;
                        var typeDst = call.Type;
                        Validation.BugCheck(_typeManager.TryEnsureSysType(typeSrc, out Type stSrc));
                        Validation.BugCheck(_typeManager.TryEnsureSysType(typeDst, out Type stDst));
                        if (!stDst.IsAssignableFrom(stSrc))
                        {
                            // There are several numeric types that don't necessarily need IL to "convert" between, namely,
                            // ix and ux, as well as b.
                            Validation.BugCheck(typeDst.IsNumericReq & typeDst.RootKind.IsIxOrUx());
                            Validation.BugCheck(typeSrc.IsNumericReq & typeSrc.RootKind.IsIxOrUx() | typeSrc == DType.BitReq);
                        }
                    }
                    if (call.Type.SeqCount > 0)
                        GenWrap(wrap, call.Type.ItemTypeOrThis);
                    if (call.Oper.IsFunc)
                        PeekType(call.Type);
                    else
                        PeekType(typeof(ActionRunner));
                    return;
                }

                // TryGenCode shouldn't write code if it claims that it can't / didn't.
                Validation.BugCheck(ilwCur.Position == posCur);
            }

            // REVIEW: Add a way for the oper to supply a message.
            throw Unsupported($"Code generation for {call.Oper.Path.ToDottedSyntax()} failed");
        }

        #region ICodeGen implementation

        public virtual MethodGenerator Generator => MethCur;

        public virtual TypeManager TypeManager => _typeManager;

        public virtual CodeGenHost Host => _host;

        public virtual Type GetSystemType(DType type)
        {
            return GetSysType(type);
        }

        public virtual Type GenCode(BoundNode bnd, ref int cur)
        {
            Validation.BugCheckValue(bnd, nameof(bnd));
            Validation.BugCheckParam(_typeManager.TryEnsureSysType(bnd.Type, out Type stRet), nameof(bnd));

            int depth = _frameCur.StackDepth;
            cur = bnd.Accept(this, cur);
            Validation.Assert(_frameCur.StackDepth == depth + 1);
            return PopType(stRet);
        }

        public void GenDefValType(Type st)
        {
            Validation.BugCheckValue(st, nameof(st));
            Validation.BugCheck(st.IsValueType, nameof(st));

            GenDefValTypeCore(st);
        }

        protected void GenDefValTypeCore(Type st)
        {
            Validation.AssertValue(st);
            Validation.Assert(st.IsValueType);

            var ilw = IL;
            if (st == typeof(sbyte) || st == typeof(short) || st == typeof(int) ||
                st == typeof(bool) || st == typeof(byte) || st == typeof(ushort) || st == typeof(uint))
            {
                ilw.Ldc_I4(0);
                return;
            }

            if (st == typeof(long) || st == typeof(ulong))
            {
                ilw.Ldc_I8(0);
                return;
            }

            var meth = CodeGenUtil.GetDefault(st);
            ilw.Call(meth);
        }

        public void GenLoadDefault(DType type)
        {
            Validation.BugCheckParam(type.IsValid, nameof(type));
            if (!_typeManager.TryEnsureSysType(type, out var st) ||
                !_typeManager.TryEnsureDefaultValue(type, out var entry))
            {
                throw Unsupported("Unsupported default value");
            }
            GenLoadDefaultCore(st, entry.value);
        }

        protected void GenLoadDefaultCore(Type st, object defValue)
        {
            Validation.AssertValue(st);

            if (st.IsValueType)
                GenDefValTypeCore(st);
            else if (defValue == null)
                IL.Ldnull();
            else
                GenLoadConstCore(defValue, st);
        }

        public virtual Type GenSequenceWrap(Type stSrc, Type stItem)
        {
            Validation.BugCheckValue(stSrc, nameof(stSrc));
            Validation.BugCheckValue(stItem, nameof(stItem));
            return GenWrap(default, stSrc, stItem);
        }

        public void GenLoadConst<T>(T value)
            where T : class
        {
            GenLoadConst(value, typeof(T));
        }

        public virtual void GenLoadConst(object value, Type st)
        {
            Validation.BugCheckValueOrNull(value);
            Validation.BugCheckValue(st, nameof(st));

            if (value != null)
            {
                Validation.BugCheckParam(st.IsAssignableFrom(value.GetType()), nameof(value));
                GenLoadConstCore(value, st);
            }
            else if (_typeManager.IsRefType(st))
                IL.Ldnull();
            else
            {
                Validation.BugCheckParam(_typeManager.IsNullableType(st), nameof(value));
                GenDefValTypeCore(st);
            }
        }

        public virtual bool GenLoadEqCmpOrNull(DType type, bool ti, bool ci, out Type stEq, out Type stItem)
        {
            Validation.BugCheckParam(type.IsEquatable, nameof(type));

            ti &= type.HasFloat | type.HasOpt;
            ci &= type.HasText;
            if (!ti & !ci)
            {
                IL.Ldnull();
                stEq = null;
                stItem = null;
                return false;
            }

            // REVIEW: Also handle other known ones?
            if (type == DType.Text)
            {
                Validation.Assert(ti | ci);
                FieldInfo fld;
                if (ti & ci)
                    fld = EqCmpStrTiCi.FldInstance;
                else if (ti)
                    fld = EqCmpStrTi.FldInstance;
                else
                    fld = EqCmpStrCi.FldInstance;

                IL.Ldsfld(fld);
                stEq = fld.FieldType;
                stItem = typeof(string);
                return true;
            }

            if (_typeManager.TryEnsureEqCmp(type, ti, ci, out var eq, out stEq, out stItem))
            {
                GenLoadConstCore(eq, stEq);
                return true;
            }

            throw Unexpected("Unexpected ci type");
        }

        protected virtual void GenLoadConstCore(object value, Type st)
        {
            Validation.AssertValue(value);
            Validation.AssertValue(st);
            Validation.Assert(!st.IsValueType);
            Validation.Assert(st.IsAssignableFrom(value.GetType()));

            GenLoadConstFromSlot(_frameCur.EnsureConstSlot(value), st);
        }

        public virtual void GenLoadInteger(Integer value)
        {
            if (value <= int.MaxValue && value >= int.MinValue)
            {
                // The BigInteger conversion from int is efficient, so just use that.
                // REVIEW: When we start using the new Integer implementation,
                // we can use this technique for more values (any value that fits in
                // 64 bits (signed or unsigned).
                IL.Ldc_I4((int)value);
                IL.Newobj(CodeGenUtil.CtorIntFromI4);
            }
            else
            {
                // Load it from the constants array.
                int slot = _frameCur.EnsureIntegerSlot(value);
                GenLoadConstArray();
                IL.Ldc_I4(slot).Ldelem_Ref().Unbox_Any(typeof(Integer));
            }
        }

        public virtual int AllocCacheSlot()
        {
            return _cacheSlotNext++;
        }

        public int StartFunction(string name, Type stRet, params Type[] stsParams)
        {
            return StartFunction(name, stRet, new ReadOnly.Array<Type>(stsParams));
        }

        public virtual int StartFunction(string name, Type stRet, ReadOnly.Array<Type> stsParams)
        {
            Validation.BugCheckNonEmpty(name, nameof(name));
            Validation.BugCheckValue(stRet, nameof(stRet));

            StartFunctionDisjoint(name, stRet, stsParams);
            return _frameCur.Id;
        }

        public virtual (Type, Delegate) EndFunction(int cookie)
        {
            Validation.BugCheckParam(cookie == _frameCur.Id, nameof(cookie));
            return EndFunctionToDelegate(tracksTypes: false);
        }

#endregion ICodeGen implementation

        /// <summary>
        /// Generates code to wrap the top value as a standard sequence, and adjusts the system type stack.
        /// </summary>
        protected void GenWrap(SeqWrapKind wrap, DType typeItem)
        {
            Validation.Assert(typeItem.IsValid);
            if (wrap == SeqWrapKind.DontWrap)
                return;
            GenWrap(wrap, GetSysType(typeItem));
        }

        /// <summary>
        /// Generates code to wrap the top value as a standard sequence, and adjusts the system type stack.
        /// </summary>
        protected void GenWrap(SeqWrapKind wrap, Type stItem)
        {
            Validation.AssertValue(stItem);
            if (wrap == SeqWrapKind.DontWrap)
                return;
            var stDst = GenWrap(wrap, _frameCur.Pop(), stItem);
            PushType(stDst);
        }

        /// <summary>
        /// Given the source system type, generates code to wrap the top value as a standard sequence. Returns
        /// the resulting sequence system type.
        /// </summary>
        protected virtual Type GenWrap(SeqWrapKind wrap, Type stSrc, Type stItem)
        {
            return GenWrapCore(wrap, stSrc, stItem, defaultWrap: false);
        }

        /// <summary>
        /// Given the source system type, generates code to wrap the top value as a standard sequence. Returns
        /// the resulting sequence system type.
        /// </summary>
        protected Type GenWrapCore(SeqWrapKind wrap, Type stSrc, Type stItem, bool defaultWrap = false)
        {
            // The default is to not wrap.
            Type stSeq = _typeManager.MakeSequenceType(stItem);
            Validation.BugCheckParam(stSeq.IsAssignableFrom(stSrc), nameof(stSrc));

            switch (wrap)
            {
            default:
                Validation.Assert(wrap == SeqWrapKind.Default);
                if (!defaultWrap)
                {
                    // The default is to not wrap.
                    return stSrc;
                }
                break;

            case SeqWrapKind.DontWrap:
                return stSrc;

            case SeqWrapKind.MustCache:
                break;
            }

            // If the source is already an array, caching, or IList, no need to cache.
            if (stSrc.IsArray)
                return stSrc;
            if (typeof(ICachingEnumerable).IsAssignableFrom(stSrc))
                return stSrc;
            if (typeof(IList<>).MakeGenericType(stItem).IsAssignableFrom(stSrc))
                return stSrc;

            // We use a different entry point for "forced" to make the IL reflect the difference.
            // Functionally, they are the same.
            var meth = wrap == SeqWrapKind.MustCache ?
                CodeGenUtil.EnumerableToCachingForced(stItem) :
                CodeGenUtil.EnumerableToCaching(stItem);
            IL.Call(meth);
            return meth.ReturnType;
        }
    }
}

public abstract class EnumerableCodeGeneratorBase : CodeGeneratorBase
{
    new public EnumerableTypeManager TypeManager { get; }

    protected EnumerableCodeGeneratorBase(EnumerableTypeManager typeManager, GeneratorRegistry operGens)
        : base(typeManager, operGens)
    {
        Validation.BugCheckValue(typeManager, nameof(typeManager));
        TypeManager = typeManager;
    }

    new private protected abstract class Impl : CodeGeneratorBase.Impl
    {
        private protected Impl(EnumerableCodeGeneratorBase codeGen, CodeGenHost host)
            : base(codeGen, host)
        {
        }

        protected override bool PreVisitImpl(BndSequenceNode bnd, int idx)
        {
            AssertIdx(bnd, idx);

            DType typeSeq = bnd.Type;
            Validation.Assert(typeSeq.SeqCount > 0);
            DType typeItem = typeSeq.ItemTypeOrThis;

            Type stItem = GetSysType(typeItem);
            var items = bnd.Items;

            // Create the array of items and fill it up.
            int cur = idx + 1;
            IL.Ldc_I4(items.Length).Newarr(stItem);
            for (int i = 0; i < items.Length; i++)
            {
                IL.Dup().Ldc_I4(i);
                cur = items[i].Accept(this, cur);
                IL.Stelem(stItem);
                PopType(stItem);
            }
            Validation.Assert(cur == idx + bnd.NodeCount);

            PushType(stItem.MakeArrayType());
            GenWrap(default, stItem);

            PeekType(bnd.Type);
            return false;
        }

        protected override void PostVisitImpl(BndSequenceNode bnd, int idx)
        {
            throw Unexpected();
        }

        protected override bool PreVisitImpl(BndTensorNode bnd, int idx)
        {
            AssertIdx(bnd, idx);

            DType typeTen = bnd.Type;
            Validation.Assert(typeTen.IsTensorReq);
            DType typeItem = typeTen.GetTensorItemType();

            Type stItem = GetSysType(typeItem);
            var items = bnd.Items;

            // Create an array of items and fill it up.
            int cur = idx + 1;
            IL.Ldc_I4(items.Length).Newarr(stItem);
            for (int i = 0; i < items.Length; i++)
            {
                IL.Dup().Ldc_I4(i);
                cur = items[i].Accept(this, cur);
                IL.Stelem(stItem);
                PopType(stItem);
            }
            Validation.Assert(cur == idx + bnd.NodeCount);

            PushType(stItem.MakeArrayType());

            var shape = bnd.Shape;
            int rank = shape.Rank;
            Validation.Assert(rank == typeTen.TensorRank);
            MethodInfo meth;
            if (rank == 1)
            {
                meth = typeof(Tensor<>).MakeGenericType(stItem).GetMethod(
                    "CreateFrom", new[] { typeof(IEnumerable<>).MakeGenericType(stItem), typeof(long) }).VerifyValue();
                IL.Ldc_I8(shape[0]);
                PushType(typeof(long));
            }
            else
            {
                meth = typeof(Tensor<>).MakeGenericType(stItem).GetMethod(
                    "CreateFrom", new[] { typeof(IEnumerable<>).MakeGenericType(stItem), typeof(Shape) }).VerifyValue();
                GenLoadConstCore((object)shape, typeof(object));
                IL.Unbox_Any(typeof(Shape));
                PushType(typeof(Shape));
            }
            EmitCall(meth);

            PeekType(bnd.Type);
            return false;
        }

        protected override void PostVisitImpl(BndTensorNode bnd, int idx)
        {
            throw Unexpected();
        }

        protected override bool PreVisitCall(BndCallNode call, int idx)
        {
            AssertIdx(call, idx);

            if (call.Oper is SortFunc sort)
                return PreVisitSort(call, idx, sort);

            return base.PreVisitCall(call, idx);
        }

        protected bool PreVisitSort(BndCallNode call, int idx, SortFunc sort)
        {
            AssertIdx(call, idx);
            Validation.Assert(sort.IsValidCall(call, full: true));

            var stItem = GetSysType(call.Type.ItemTypeOrThis);
            var scopeBase = GetTopScope();
            var argSrc = call.Args[0];

            int cur = idx + 1;

            // Generate the code for the source arg.
            cur = argSrc.Accept(this, cur);
            Validation.Assert(GetTopScope() == scopeBase);

            // Dup the source for wrapping the result with the source count info.
            IL.Dup();
            DupType();

            // Wrap the source as a pinging enumerable.
            GenEnumerableToPinging(stItem, EnsureIdRange(call, 1));

            // Generate the OrderBy call.
            int carg = call.Args.Length;
            Validation.Assert(call.Scopes.Length == (carg == 1 ? 0 : 1));
            Validation.Assert(call.Indices.Length == (carg == 1 ? 0 : 1));
            if (carg == 1)
                GenSortNoSel(call, sort, stItem);
            else
            {
                var scopeSrc = call.Scopes[0];
                var indexSrc = call.Indices[0];

                bool isIndexed = indexSrc != null;
                if (isIndexed)
                {
                    InitScope(indexSrc, call, idx, 0, isArgValid: true);
                    PushScope(indexSrc, call, idx, 0, isArgValid: true);
                }

                bool isArgValid = scopeSrc.IsValidArg(argSrc);
                InitScope(scopeSrc, call, idx, 0, isArgValid);
                PushScope(scopeSrc, call, idx, 0, isArgValid);

                if (carg == 2)
                    cur = GenSortOneSel(call, idx, sort, stItem, scopeBase, cur);
                else
                    cur = GenSortMultipleSels(call, idx, sort, stItem, scopeBase, cur);

                PopScope(scopeSrc);
                DisposeScope(scopeSrc, call, idx, 0);
                if (isIndexed)
                {
                    PopScope(indexSrc);
                    DisposeScope(indexSrc, call, idx, 0);
                }
            }
            Validation.Assert(cur == idx + call.NodeCount);

            // Wrap.
            MethodInfo methWrapCtr = CodeGenUtil.WrapWithCounter(stItem, stItem);
            Validation.Assert(methWrapCtr.GetParameters().Length == 2);
            EmitCall(methWrapCtr);
            GenWrap(default, stItem);

            return false;
        }

        /// <summary>
        /// Generates the core code for a Sort call with no selector. The call has a single
        /// argument for the input sequence which may have a sort directive.
        /// </summary>
        protected void GenSortNoSel(BndCallNode call, SortFunc sort, Type stItem)
        {
            Validation.Assert(sort.IsValidCall(call, full: true));
            Validation.Assert(call.Args.Length == 1);
            Validation.Assert(call.Scopes.Length == 0);
            Validation.Assert(call.Indices.Length == 0);
            var typeItem = call.Type.ItemTypeOrThis;
            Validation.Assert(GetSysType(typeItem) == stItem);

            bool isDown = sort.IsDown(typeItem);
            call.GetDirective(0).SetSortFlags(out bool ci, ref isDown);
            GenLoadCompareFunc(typeItem, ci, isDown);

            EmitCall(CodeGenUtil.GetSortMeth(isIndexed: false, stItem));
        }

        /// <summary>
        /// Generates the core code for a Sort call with a single selector. The selector argument
        /// produces the key for each item and may have a sort directive.
        /// </summary>
        protected int GenSortOneSel(BndCallNode call, int idx, SortFunc sort,
            Type stItem, RefMaps.ScopeInfo scopeBase, int cur)
        {
            AssertIdx(call, idx);
            Validation.Assert(sort.IsValidCall(call, full: true));
            Validation.Assert(call.Args.Length == 2);
            Validation.Assert(call.Scopes.Length == 1);
            Validation.Assert(call.Indices.Length == 1);
            Validation.Assert(GetSysType(call.Type.ItemTypeOrThis) == stItem);

            var info = _refMaps.GetScopeInfoFromOwner(call, idx, call.Scopes[0]);
            Validation.Assert(info.Slot == 0);

            Type[] stsArgs;
            var scopeMap = new ScopeMap { { info.Index, LocArgInfo.FromArg(1, stItem) } };
            PushNestedArg(call, idx, 1, needsDelegate: true);
            var nested = GetTopNestedArg();

            bool isIndexed = call.Indices[0] != null;
            if (isIndexed)
            {
                info = _refMaps.GetScopeInfoFromOwner(call, idx, call.Indices[0]);
                Validation.Assert(info.Slot == 0);
                stsArgs = new Type[] { stItem, typeof(long) };
                scopeMap.Add(info.Index, LocArgInfo.FromArg(2, typeof(long)));
            }
            else
                stsArgs = new Type[] { stItem };

            var sel = call.Args[1];
            Validation.Assert(sel.Type.IsSortable);
            var stKey = GetSysType(sel.Type);

            StartFunctionCore(GetNestedCallArgFnName(sort, 1),
                scopeMap, FindExtScopes(scopeBase, nested), UsesGlobals(nested), UsesExecCtx(nested),
                stKey, stsArgs);
            cur = sel.Accept(this, cur);
            EndFunctionCore();
            PopNestedArg();

            bool isDown = sort.IsDown(sel.Type);
            call.GetDirective(1).SetSortFlags(out bool ci, ref isDown);
            GenLoadCompareFunc(sel.Type, ci, isDown);

            EmitCall(CodeGenUtil.GetSortMeth(isIndexed, stItem, stKey));

            return cur;
        }

        /// <summary>
        /// Generates the core code for a Sort call with more than one selector. Each selector becomes a subcomparison
        /// evaluated left-to-right in a short-circuiting comparison chain, which returns once a nonzero comparison
        /// result is found. Each selector may have a sort directive.
        /// </summary>
        protected int GenSortMultipleSels(BndCallNode call, int idx, SortFunc sort,
            Type stItem, RefMaps.ScopeInfo scopeBase, int cur)
        {
            AssertIdx(call, idx);
            Validation.Assert(sort.IsValidCall(call, full: true));
            Validation.Assert(call.Args.Length > 2);
            Validation.Assert(call.Scopes.Length == 1);
            Validation.Assert(call.Indices.Length == 1);
            Validation.Assert(GetSysType(call.Type.ItemTypeOrThis) == stItem);

            // Create the comparison function that sequentially evaluates the selectors.
            string fnName = sort.Name.Value + CodeGenUtil.GetCompareMethName(isDown: false);
            var nestedArgs = _refMaps.GetNestedArgList(call, idx);
            int inaMin = nestedArgs[0].Item1;
            int inaLim = nestedArgs[nestedArgs.Count - 1].Item2;
            bool isIndexed = call.Indices[0] != null;
            Type[] stsArgs;

            // The scope map is updated during function generation to map the
            // item scope/index to the function's args for each selector visit.
            var info = _refMaps.GetScopeInfoFromOwner(call, idx, call.Scopes[0]);
            Validation.Assert(info.Slot == 0);

            var mapArgX = new ScopeMap { { info.Index, LocArgInfo.FromArg(1, stItem) } };
            var mapArgY = new ScopeMap { { info.Index, LocArgInfo.FromArg(isIndexed ? 3 : 2, stItem) } };

            if (isIndexed)
            {
                stsArgs = new Type[] { stItem, typeof(long), stItem, typeof(long) };
                info = _refMaps.GetScopeInfoFromOwner(call, idx, call.Indices[0]);
                Validation.Assert(info.Slot == 0);
                mapArgX.Add(info.Index, LocArgInfo.FromArg(2, typeof(long)));
                mapArgY.Add(info.Index, LocArgInfo.FromArg(4, typeof(long)));
            }
            else
                stsArgs = new Type[] { stItem, stItem };

            StartFunctionCore(fnName, scopeMap: null,
                FindExtScopes(scopeBase, inaMin, inaLim), UsesGlobals(inaMin, inaLim), UsesExecCtx(inaMin, inaLim),
                typeof(int), stsArgs);

            Validation.Assert(_frameCur.ScopeMap == null);
            Label labRet = IL.DefineLabel();
            int csel = call.Args.Length - 1;

            // Pseudocode for each selector:
            // if ((res = compare(selector(a[, ia]), selector(b[, ib])) != 0) return res;
            for (int slot = 1; ; slot++)
            {
                Validation.AssertIndex(slot, call.Args.Length);

                var sel = call.Args[slot];
                DType typeSel = sel.Type;
                Validation.Assert(typeSel.IsSortable);
                Type stKey = GetSysType(typeSel);
                bool isDown = sort.IsDown(typeSel);
                call.GetDirective(slot).SetSortFlags(out bool ci, ref isDown);
                bool useSub = UsesILSubCmp(typeSel);

                // Selector transformation is inlined for each arg. Note we still need to set
                // needsDelegate to true when pushing the nested arg, even though we aren't
                // creating one here, since a SeqItem is in scope for the selector and it
                // would normally need to be a delegate parameter.
                PushNestedArg(call, idx, slot, needsDelegate: true);
                _frameCur.ScopeMap = isDown ? mapArgY : mapArgX;
                int tmp = sel.Accept(this, cur);
                if (useSub)
                    GenLdILSubConv(typeSel);

                _frameCur.ScopeMap = isDown ? mapArgX : mapArgY;
                cur = sel.Accept(this, cur);
                Validation.Assert(tmp == cur);
                if (useSub)
                    GenLdILSubConv(typeSel);

                PopNestedArg();
                Validation.Assert(_frameCur.ScopeMap.Count == (isIndexed ? 2 : 1));
                _frameCur.ScopeMap = null;

                // Do the comparison. We handle the type stack after all selectors are processed.
                if (useSub)
                {
                    PopType(stKey);
                    PopType(stKey);
                    GenILSubCmp(typeSel);
                }
                else
                {
                    // Since we are already swapping the args to get a descending comparison
                    // if needed, we always use the ascending compare method.
                    Validation.BugCheck(CodeGenUtil.TryGetCompareMeth(stKey, ci, isDown: false, out var methCmp));
                    EmitCall(methCmp);
                    PopType(typeof(int));
                }

                if (slot == csel)
                    break;
                IL
                    .Dup()
                    .Brtrue(ref labRet)
                    .Pop();
            }

            PushType(typeof(int));
            IL.MarkLabel(labRet);

            EndFunctionCore();
            EmitCall(CodeGenUtil.GetSortMeth(isIndexed, stItem));

            return cur;
        }

        protected void GenEnumerableToPinging(Type stItem, int id)
        {
            Validation.AssertValue(stItem);
            Validation.Assert(id >= 0);
            MethodInfo meth = CodeGenUtil.EnumerableToPinging(stItem);
            Validation.Assert(meth.GetParameters().Length == 3);

            GenLoadExecCtxCore(withType: true);
            IL.Ldc_I4(id);
            PushType(typeof(int));
            EmitCall(meth);
        }

        protected override void GenInPost(BndBinaryOpNode bnd)
        {
            Validation.Assert(bnd.Type == DType.BitReq);
            Validation.Assert(bnd.Arg0.Type.IsEquatable);
            Validation.Assert(bnd.Arg1.Type.IsSequence);
            Validation.Assert(bnd.Arg0.Type == bnd.Arg1.Type.ItemTypeOrThis);

            var op = bnd.Op;
            var typeItem = bnd.Arg0.Type;

            bool ci = false;
            switch (op)
            {
            case BinaryOp.InCi:
            case BinaryOp.InCiNot:
                ci = typeItem.HasText;
                break;
            }

            MethodInfo meth;
            if (ci)
            {
                Type stItem;
                if (typeItem.IsAggXxx)
                {
                    GetAggEqInfo(typeItem, out var info);
                    Validation.Assert(info.EqCi != null);
                    GenLoadConstCore(info.EqCi, info.StEq);
                    PushType(info.StEq);
                    stItem = info.StItem;
                }
                else
                {
                    Validation.Assert(typeItem == DType.Text);
                    var fld = EqCmpStrCi.FldInstance;
                    IL.Ldsfld(fld);
                    PushType(fld.FieldType);
                    stItem = typeof(string);
                }
                meth = CodeGenUtil.InEnumerable(stItem, withEqCmp: true);
            }
            else
                meth = CodeGenUtil.InEnumerable(GetSystemType(typeItem), withEqCmp: false);

            GenLoadExecCtxCore(withType: true);
            IL.Ldc_I4(EnsureIdRange(bnd, 1));
            PushType(typeof(int));

            EmitCall(meth);
            if (op == BinaryOp.InNot || op == BinaryOp.InCiNot)
                IL.Ldc_I4(0).Ceq();
        }

        /// <summary>
        /// Generates a call to <see cref="CodeGenUtil.WrapIndPairs{T}(IEnumerable{T})"/>
        /// to wrap each item in a sequence with its index. Populates <paramref name="stIndPair"/>
        /// with the type of the wrapped item pairs. This is a <see cref="ValueTuple{T1,T2}"/>,
        /// where <c>T1</c> is <paramref name="stItem"/> and <c>T2</c> is <c>long</c>.
        /// </summary>
        protected void GenWrapIndPairs(Type stItem, out Type stIndPair)
        {
            Validation.AssertValue(stItem);

            stIndPair = typeof(ValueTuple<,>).MakeGenericType(stItem, typeof(long));
            var meth = CodeGenUtil.WrapIndPairs(stItem);
            Validation.Assert(meth.ReturnType.IsAssignableFrom(typeof(IEnumerable<>).MakeGenericType(stIndPair)));

            EmitCall(meth);
        }

        protected void GenLoadCompareFunc(DType typeKey, bool ci, bool isDown)
        {
            Validation.Assert(typeKey.IsSortable);

            Type stFn;
            Delegate fn;
            Type stKey = GetSysType(typeKey);
            if (UsesILSubCmp(typeKey))
            {
                // REVIEW: When we need a delegate and aren't operating on bools, we don't need to do IL code gen.
                // We can instead invoke a delegate implemented in C#.
                StartFunctionDisjoint(CodeGenUtil.GetCompareMethName(isDown), typeof(int), new Type[] { stKey, stKey });

                IL.Ldarg(isDown ? 2 : 1);
                GenLdILSubConv(typeKey);
                IL.Ldarg(isDown ? 1 : 2);
                GenLdILSubConv(typeKey);
                GenILSubCmp(typeKey);

                (stFn, fn) = EndFunctionToDelegate(tracksTypes: false);
            }
            else
                Validation.BugCheck(CodeGenUtil.TryGetCompareDel(stKey, ci, isDown, out fn, out stFn));

            GenLoadConstCore(fn, stFn);
            PushType(stFn);
        }

        /// <summary>
        /// Required numeric types less than 8 bytes can be efficiently compared
        /// with inlined IL subtraction. Note trying to do this for booleans in C# runs
        /// into type safety issues.
        /// </summary>
        protected static bool UsesILSubCmp(DType typeKey)
        {
            if (!typeKey.IsOpt)
            {
                switch (typeKey.Kind)
                {
                case DKind.Bit:
                case DKind.I1:
                case DKind.U1:
                case DKind.I2:
                case DKind.U2:
                case DKind.I4:
                case DKind.U4:
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// I4 and U4 can be compared via IL subtraction, but first they must be
        /// converted to I8 and U8 respectively. The other IL subtraction types
        /// can be subtracted directly. This generates the proper conversion
        /// if <paramref name="typeKey"/> corresponds to either of these types.
        /// </summary>
        protected void GenLdILSubConv(DType typeKey)
        {
            Validation.Assert(UsesILSubCmp(typeKey));

            if (typeKey.Kind == DKind.I4)
                IL.Conv_I8();
            else if (typeKey.Kind == DKind.U4)
                IL.Conv_U8();
        }

        /// <summary>
        /// Generates the IL to perform a comparison via subtraction on the given <paramref name="typeKey"/>,
        /// which should return true for <see cref="UsesILSubCmp"/>.
        /// </summary>
        protected void GenILSubCmp(DType typeKey)
        {
            Validation.Assert(UsesILSubCmp(typeKey));
            IL.Sub();

            if (typeKey.Kind == DKind.I4 || typeKey.Kind == DKind.U4)
            {
                // The difference is in the range [-2^32+1, 2^32-1].
                // We need to get into the range for i4 and preserve the sign. A naive right
                // shift would change a 1 into a 0. So we mask the lower bit, shift it up, and
                // OR it with the difference to ensure the second bit is set if the first is.
                // This will allow us to right shift while preserving the sign.
                IL
                    .Dup()
                    .Ldc_I8(1).And().Ldc_I4(1).Shl()
                    .Or()
                    .Ldc_I4(1).Shr()
                    .Conv_I4();
            }
        }

        protected void GenLoadIdentityFunc(Type stItem)
        {
            Validation.AssertValue(stItem);

            Type stFn = typeof(Func<,>).MakeGenericType(stItem, stItem);
            Delegate fn = Delegate.CreateDelegate(stFn, null, CodeGenUtil.Identity(stItem));
            GenLoadConstCore(fn, stFn);
            PushType(stFn);
        }

        protected override bool PreVisitGroupBy(BndGroupByNode bgb, int idx)
        {
            AssertIdx(bgb, idx);
            Validation.Assert(bgb.PureKeys.Length + bgb.KeepKeys.Count > 0);

            // Depending on the situation, we use one of four overloads of Enumerable.GroupBy, namely the
            // following two plus the variations without an IEqualityComparer:
            //
            //   IEnumerable<TResult> GroupBy<TSource, TKey, TElement, TResult>(
            //       IEnumerable<TSource> source,
            //       Func<TSource, TKey> keySelector,
            //       Func<TSource, TElement> elementSelector,
            //       Func<TKey, IEnumerable<TElement>, TResult> resultSelector,
            //       IEqualityComparer<TKey> comparer);
            //
            //   IEnumerable<TResult> GroupBy<TSource, TKey, TResult>(
            //       IEnumerable<TSource> source,
            //       Func<TSource, TKey> keySelector,
            //       Func<TKey, IEnumerable<TSource>, TResult> resultSelector,
            //       IEqualityComparer<TKey> comparer);
            //
            // In the indexed case, we wrap items from the source sequence into indexed pairs and unpack the items
            // from these pairs as needed within the selectors.
            //
            // When the comments say we use the identity function as elementSelector, that means we use the simpler
            // overload that doesn't take an elementSelector.
            //
            // keySelector:
            //   * When any of the keys are marked as case insensitive, we'll use an equality comparer, either
            //     standard or custom depending on whether the ci indications are consistent across all keys.
            //   * When there is a single key, we use the primitive type as TKey, and either no equality comparer
            //     or the standard case insensitive comparer.
            //   * Otherwise, when all keys are fields of TSource and TSource is req, we'll use the identity key
            //     selector, so TKey is TSource. When not all fields of TSource are keys, or the ci option is not
            //     consistent across all key fields, we'll generate a custom IEqualityComparer that looks at only
            //     the key fields and handles ci properly. Otherwise, we either use no equality comparer or the
            //     standard ci comparer.
            //   * Otherwise, we construct a record type, {k01:x, k1:x, ...}, as TKey. For this, we use either no
            //     equality comparer or the standard ci comparer or a custom one depending on whether any keys are
            //     ci and whether all (that can be) are ci.
            // elemSelector:
            //   * If there is a single "map" and no aggs:
            //     * If specified as a computed value, generate it.
            //     * Otherwise, if some fields are dropped, copy fields into a sub-record.
            //     * Otherwise, use the identity, so TElement is TSource.
            //   * Otherwise, use the identity, so TElement is TSource, and do all the agg and map fields
            //     in resultSelector.
            //   When the resultSelector is the identity, we use the simpler GroupBy overload, as described above.
            // resultSelector:
            //   * If there is a single "map" and no aggs:
            //     * If non-record outer, return the IE<TElement>, wrapped appropriately.
            //     * If record outer, copy fields from the key and copy the IE<TElement>, wrapped appropriately.
            //   * Otherwise, if there are no maps or aggs and no named keys, the result item is just the group.
            //   * Otherwise, compute and store all the map and agg fields.

            bool anyNames =
                bgb.KeepKeys.Count > 0 ||
                bgb.AggItems.GetKeys().Any(x => x.IsValid) ||
                bgb.MapItems.GetKeys().Any(x => x.IsValid);

#if DEBUG
            if (!anyNames)
                Validation.Assert(bgb.AggItems.Count + bgb.MapItems.Count <= 1);
            else
            {
                // When the result is a record, every agg and map should have a valid name.
                Validation.Assert(bgb.AggItems.GetKeys().All(x => x.IsValid));
                Validation.Assert(bgb.MapItems.GetKeys().All(x => x.IsValid));
            }
#endif

            // The destination (result) type.
            DType typeDst = bgb.Type;
            Validation.Assert(typeDst.SeqCount > 0);
            DType typeItemDst = typeDst.ItemTypeOrThis;
            Type stItemDst = GetSysType(typeItemDst);

            // The source type.
            DType typeSrc = bgb.Source.Type;
            Validation.Assert(typeSrc.SeqCount > 0);
            DType typeItemSrc = typeSrc.ItemTypeOrThis;
            Type stItemSrc = GetSysType(typeItemSrc);

            // The current top scope.
            var scopeBase = GetTopScope();

            // *** source.
            // Generate the code for the "source" arg.
            var (min, lim) = bgb.Source.GetItemCountRange();
            if (lim == 0)
            {
                IL.Ldnull();
                PushType(typeDst);
                return false;
            }

            int cur = idx + 1;
            cur = bgb.Source.Accept(this, cur);
            Validation.Assert(GetTopScope() == scopeBase);

            Label labDone = default;
            if (min == 0)
            {
                // Do the null test.
                Label labNotNull = default;
                IL
                    .Dup()
                    // REVIEW: It's tempting to just do Brfalse(ref labDone) since when the branch would
                    // be taken, there would be a null on the stack for the final result. However, the null is
                    // almost certainly of the incorrect type. This likely only matters when an IL verifier is
                    // active, which doesn't seem to be the case for dynamic code like this. But rather than risk
                    // it, we bloat the IL slightly by popping the null and re-pushing an untyped null.
                    .Brtrue(ref labNotNull)
                    .Pop()
                    .Ldnull()
                    .Br(ref labDone)
                    .MarkLabel(labNotNull);
            }

            bool isIndexed = bgb.IndexForKeys != null || bgb.IndexForMaps != null;

            // Wrap src as a pinging enumerable.
            GenEnumerableToPinging(stItemSrc, EnsureIdRange(bgb, 1));

            Type stItemSrcInd = null;
            if (isIndexed)
                GenWrapIndPairs(stItemSrc, out stItemSrcInd);

            int ckey = bgb.PureKeys.Length + bgb.KeepKeys.Count;
            Validation.Assert(!bgb.KeysCi.TestAtOrAbove(ckey));
            var nestedArgs = _refMaps.GetNestedArgList(bgb, idx);
            Validation.Assert(nestedArgs.Count == ckey + bgb.MapItems.Count + bgb.AggItems.Count);

            // *** keySelector.
            // Generate the code for the "keySelector" arg.
            cur = GenGroupByKeySelector(bgb, idx, typeItemSrc, stItemSrc, stItemSrcInd, cur,
                out var typeKey, out var stKey, out var namesKey);
            Validation.Assert(typeKey.IsEquatable);

            // Handle elementSelector and resultSelector. There are two cases:
            // (1) when there is a single map and no aggs.
            // (2) otherwise.
            bool isIdentity; // Whether the elementSelector is the identity function, so is omitted.
            Type stItemInner;
            if (bgb.AggItems.Count == 0 && bgb.MapItems.Count == 1)
            {
                // The 1 map, 0 aggs case.

                // *** elementSelector.
                // Generate the code for the "elementSelector" arg.

                // Push the scopes for the elementSelector code.
                bool isValid = bgb.ScopeForMaps.IsValidArg(bgb.Source);
                if (bgb.IndexForMaps != null)
                {
                    InitScope(bgb.IndexForMaps, bgb, idx, -2, isValid);
                    PushScope(bgb.IndexForMaps, bgb, idx, -2, isValid);
                }
                InitScope(bgb.ScopeForMaps, bgb, idx, -2, isValid);
                PushScope(bgb.ScopeForMaps, bgb, idx, -2, isValid);
                var scopeCur = GetTopScope();

                var (nameInner, valueInner) = bgb.MapItems.GetPairs().FirstOrDefault();
                DType typeInner;
                DType typeItemInner;
                if (valueInner == null)
                {
                    // This is the "auto" case. The element is either the source or an auto-generated record type
                    // which is a sub-record of typeItemSrc.
                    Validation.Assert(anyNames);
                    Validation.Assert(nameInner.IsValid);
                    Validation.Assert(typeItemDst.IsRecordReq);

                    typeItemDst.TryGetNameType(nameInner, out typeInner).Verify();
                    typeItemInner = typeInner.ItemTypeOrThis;
                    stItemInner = GetSysType(typeItemInner);

                    if (typeItemInner == typeItemSrc)
                    {
                        // The nested item type and source type are the same.
                        // In the indexed case, unwrap the item from the tuple and discard the index.
                        // Otherwise, use the identity.
                        Validation.Assert(stItemSrc == stItemInner);
                        if (!isIndexed)
                            isIdentity = true;
                        else
                        {
                            StartFunctionDisjoint("elementSelector", stItemSrc, stItemSrcInd);
                            var finItem1 = stItemSrcInd.GetField(CodeGenUtil.SysValTupItem1).VerifyValue();
                            Validation.Assert(finItem1.FieldType == stItemSrc);
                            IL.Ldarg(1, stItemSrcInd).Ldfld(finItem1);
                            PushType(stItemSrc);
                            EndFunctionCore();
                            isIdentity = false;
                        }
                    }
                    else
                    {
                        // Copy fields from the source item to the inner element item.
                        Validation.Assert(typeItemSrc.IsRecordXxx);
                        Validation.Assert(typeItemInner.IsRecordXxx);
                        GenGroupByAutoFunc(bgb, "elementSelector", typeItemSrc, stItemSrc, stItemSrcInd, typeItemInner, stItemInner);
                        isIdentity = false;
                    }
                }
                else
                {
                    // The map element is computed, not auto-generated.
                    typeItemInner = valueInner.Type;
                    typeInner = typeItemInner.ToSequence();
                    stItemInner = GetSysType(typeItemInner);

                    PushNestedArg(bgb, idx, ckey, needsDelegate: true);
                    var nested = GetTopNestedArg();

                    var extScopes = FindExtScopes(scopeBase, nested);
                    bool usesGlobals = UsesGlobals(nested);
                    bool usesExecCtx = UsesExecCtx(nested);

                    var info = _refMaps.GetScopeInfoFromOwner(bgb, idx, bgb.ScopeForMaps);
                    Validation.Assert(info.Slot == -2);
                    if (isIndexed)
                    {
                        // Note we assume the scope for maps is used for simplicity.
                        Validation.Assert(bgb.IndexForMaps == null || ScopeCounter.Any(valueInner, bgb.IndexForMaps));
                        var infoInd = _refMaps.GetScopeInfoFromOwner(bgb, idx, bgb.IndexForMaps);
                        Validation.Assert(infoInd == null || infoInd.Slot == -2);

                        StartUnpackFunction("elementSelector",
                            extScopes, usesGlobals, usesExecCtx,
                            stItemInner, stItemSrcInd,
                            info, infoInd);
                    }
                    else
                    {
                        // The map scope is the first (non-frame) parameter.
                        var scopeMap = new ScopeMap { { info.Index, LocArgInfo.FromArg(1, stItemSrc) } };

                        StartFunctionCore(
                            "elementSelector",
                            scopeMap, extScopes, usesGlobals, usesExecCtx,
                            stItemInner, stItemSrc);
                    }

                    Validation.Assert(GetTopScope() == scopeCur);
                    cur = valueInner.Accept(this, cur);
                    Validation.Assert(GetTopScope() == scopeCur);

                    EndFunctionCore();

                    Validation.Assert(GetTopNestedArg() == nested);
                    PopNestedArg();

                    isIdentity = false;
                }

                Validation.Assert(GetTopScope() == scopeCur);

                // REVIEW: Should we wait to dispose the scopes until after PostVisit?
                PopScope(bgb.ScopeForMaps);
                DisposeScope(bgb.ScopeForMaps, bgb, idx, -2);
                if (bgb.IndexForMaps != null)
                {
                    PopScope(bgb.IndexForMaps);
                    DisposeScope(bgb.IndexForMaps, bgb, idx, -2);
                }

                // *** resultSelector.
                // Generate the code for the resultSelector arg.
                {
                    // Create the resultSelector function.
                    Type stEnumItemInner = typeof(IEnumerable<>).MakeGenericType(stItemInner);

                    StartFunctionDisjoint("resultSelector", stItemDst, stKey, stEnumItemInner);

                    if (!anyNames)
                    {
                        // Non-record result, so return the IEnumerable<TElement> wrapped appropriately.
                        Validation.Assert(!nameInner.IsValid);
                        Validation.Assert(typeItemDst == typeInner);
                        IL.Ldarg(2);
                        PushType(stEnumItemInner);
                        GenWrap(default, stItemInner);
                    }
                    else
                    {
                        // Record result.
                        Validation.Assert(typeItemDst.IsRecordReq);
                        Validation.Assert(nameInner.IsValid);
                        Validation.Assert(typeItemDst.GetNameTypeOrDefault(nameInner) == typeInner);

                        // Create the result record and copy in any named keys.
                        using var rg = GenGroupByCopyKeys(bgb, typeKey, stKey, namesKey, typeItemDst, stItemDst);

                        // Wrap the IE<TElement> and copy into the field.
                        rg.SetFromStackPre(nameInner, typeInner);
                        IL.Ldarg(2);
                        GenWrap(default, stEnumItemInner, stItemInner);
                        rg.SetFromStackPost();

                        rg.Finish();
                        PushType(typeItemDst);
                    }

                    EndFunctionCore();
                }
            }
            else
            {
                // Either no maps/aggs, or more than 1 map, or more than 0 aggs.

                // In the indexed case, if the map index is not used,
                // we unpack the items from the wrapped index pairs
                // as the element selector.
                //
                // Otherwise, we use the identity for elementSelector.
                //
                // Do all remaining work in the resultSelector.

                // *** elementSelector.
                bool isMapIndexed = bgb.IndexForMaps != null;
                if (isIndexed && !isMapIndexed)
                {
                    StartFunctionDisjoint("elementSelector", stItemSrc, stItemSrcInd);
                    IL.Ldarg(1, stItemSrcInd);
                    var finItem1 = stItemSrcInd.GetField(CodeGenUtil.SysValTupItem1).VerifyValue();
                    Validation.Assert(finItem1.FieldType == stItemSrc);
                    IL.Ldfld(finItem1);
                    PushType(stItemSrc);
                    var (stFn, fn) = EndFunctionToDelegate();
                    GenLoadConstCore(fn, stFn);
                    PushType(stFn);

                    stItemInner = stItemSrc;
                    isIdentity = false;
                }
                else
                {
                    stItemInner = isIndexed ? stItemSrcInd : stItemSrc;
                    isIdentity = true;
                }

                // *** resultSelector.
                // Generate the code for the resultSelector arg.
                // Func<TKey, IE<TElem>, TRes>. No scope map needed.
                Dictionary<int, RefMaps.ScopeInfo> scopesExt = null;
                bool usesGlobals = false;
                bool usesExec = false;
                if (ckey < nestedArgs.Count)
                {
                    int inaMin = nestedArgs[ckey].Item1;
                    int inaLim = nestedArgs[nestedArgs.Count - 1].Item1 + 1;
                    scopesExt = FindExtScopes(scopeBase, inaMin, inaLim);
                    usesGlobals = UsesGlobals(inaMin, inaLim);
                    usesExec = UsesExecCtx(inaMin, inaLim);
                }

                Type stEnumItemSrc = typeof(IEnumerable<>).MakeGenericType(
                    isMapIndexed ? stItemSrcInd : stItemSrc);
                StartFunctionCore("resultSelector", null, scopesExt, usesGlobals, usesExec,
                    stItemDst, stKey, stEnumItemSrc);

                // Create the result record (if there is one) and copy in any named keys.
                using var rg = anyNames ?
                    GenGroupByCopyKeys(bgb, typeKey, stKey, namesKey, typeItemDst, stItemDst) :
                    null;
#if DEBUG
                if (rg == null)
                {
                    Validation.Assert(bgb.MapItems.Count == 0);
                    if (bgb.AggItems.Count == 0)
                    {
                        // The group is the result.
                        Validation.Assert(typeItemDst == typeItemSrc.ToSequence());
                    }
                    else
                    {
                        // The agg value is the result.
                        Validation.Assert(bgb.AggItems.Count == 1);
                        Validation.Assert(!bgb.AggItems.FirstKey.IsValid);
                        Validation.Assert(typeItemDst == bgb.AggItems.FirstVal.Type);
                    }
                }
#endif
                // Load the IE<TSource> group and wrap it as a sequence.
                IL.Ldarg(2);
                PushType(stEnumItemSrc);
                var stSrc = GenWrap(default, stEnumItemSrc, isMapIndexed ? stItemSrcInd : stItemSrc);

                if (bgb.MapItems.Count > 0)
                {
                    Validation.Assert(anyNames);
                    cur = GenGroupByMapFields(rg, bgb, idx, typeItemSrc, stItemSrc,
                        isMapIndexed ? stItemSrcInd : null, cur);
                }

                if (bgb.AggItems.Count > 0)
                    cur = GenGroupByAggs(rg, bgb, idx, cur);
                else
                {
                    // The (wrapped) group is on top of the stack. We need it iff the group is the result,
                    // which is true iff the result is not a constructed record and there are no maps (or aggs).
                    if (bgb.MapItems.Count > 0 || anyNames)
                    {
                        // We don't need the group. The group has the same type as the src.
                        Validation.Assert(bgb.ScopeForAggs == null);
                        Validation.Coverage(isMapIndexed ? 1 : 0);
                        IL.Pop();
                        PopType(stSrc);
                    }
                    else
                        Validation.Assert(typeItemDst == typeItemSrc.ToSequence());
                }

                if (rg != null)
                {
                    PushType(rg.RecType);
                    rg.Finish();
                }

                EndFunctionCore();
            }

            MethodInfo methFinal;
            bool same = bgb.AreKeysSameCi(out bool ci);
            bool subset = ckey > 1 && namesKey.VerifyValue().Length != typeKey.FieldCount;
            if (subset || !same || ci)
            {
                // *** IEqualityComparer<TKey>.
                if (subset || !same)
                {
                    Validation.Assert(typeKey.IsRecordXxx);
                    Validation.Assert(namesKey.Length <= typeKey.FieldCount);
                    GenGroupByComparer(typeKey, stKey, namesKey, bgb.KeysCi);
                }
                else if (typeKey.IsAggXxx)
                {
                    Validation.Assert(ci);
                    GenLoadAggEqCmpCore(typeKey, ti: false, ci: ci, info: out var info);
                    Validation.Assert(stKey == info.StItem);
                    PushType(info.StEq);
                }
                else
                {
                    Validation.Assert(ci);
                    Validation.Assert(typeKey == DType.Text);
                    var fld = EqCmpStrCi.FldInstance;
                    IL.Ldsfld(fld);
                    PushType(fld.FieldType);
                }

                // Now call the appropriate overload of GroupBy that takes an IEqualityComparer. Whether it takes
                // an elementSelector depends on isIdentity.
                //
                //   IEnumerable<TResult> GroupBy<TSource, TKey, TElement, TResult>(
                //       IEnumerable<TSource> source,
                //       Func<TSource, TKey> keySelector,
                //       Func<TSource, TElement> elementSelector,
                //       Func<TKey, IEnumerable<TElement>, TResult> resultSelector,
                //       IEqualityComparer<TKey> comparer);
                if (!isIdentity)
                {
                    Validation.Coverage(isIndexed ? 1 : 0);
                    methFinal =
                        CodeGenUtil.GetMethodInfo5<
                                IE,
                                Func<object, object>,
                                Func<object, object>,
                                Func<object, IE, object>,
                                IEqualityComparer<object>,
                                IE>
                            (Enumerable.GroupBy, isIndexed ? stItemSrcInd : stItemSrc, stKey, stItemInner, stItemDst);
                }
                else
                {
                    Validation.Assert(stItemInner == (isIndexed ? stItemSrcInd : stItemSrc));
                    Validation.Coverage(isIndexed ? 1 : 0);
                    methFinal =
                        CodeGenUtil.GetMethodInfo4<
                                IE,
                                Func<object, object>,
                                Func<object, IE, object>,
                                IEqualityComparer<object>,
                                IE>
                            (Enumerable.GroupBy, isIndexed ? stItemSrcInd : stItemSrc, stKey, stItemDst);
                }
            }
            else
            {
                // Now call the appropriate overload of GroupBy that does not take an IEqualityComparer. Whether it takes
                // an elementSelector depends on isIdentity.
                //
                //   IEnumerable<TResult> GroupBy<TSource, TKey, TElement, TResult>(
                //       IEnumerable<TSource> source,
                //       Func<TSource, TKey> keySelector,
                //       Func<TSource, TElement> elementSelector,
                //       Func<TKey, IEnumerable<TElement>, TResult> resultSelector);
                if (!isIdentity)
                {
                    Validation.Coverage(isIndexed ? 1 : 0);
                    methFinal =
                        CodeGenUtil.GetMethodInfo4<
                                IE,
                                Func<object, object>,
                                Func<object, object>,
                                Func<object, IE, object>,
                                IE>
                            (Enumerable.GroupBy, isIndexed ? stItemSrcInd : stItemSrc, stKey, stItemInner, stItemDst);
                }
                else
                {
                    Validation.Assert(bgb.IndexForMaps != null || stItemInner == stItemSrc);
                    Validation.Coverage(isIndexed ? 1 : 0);
                    methFinal =
                        CodeGenUtil.GetMethodInfo3<
                                IE,
                                Func<object, object>,
                                Func<object, IE, object>,
                                IE>
                            (Enumerable.GroupBy, isIndexed ? stItemSrcInd : stItemSrc, stKey, stItemDst);
                }
            }

            EmitCall(methFinal);

            SeqWrapKind wrap = default;
            if (bgb.HasVolatile)
            {
                // Account for volatile map or agg items.
                if (bgb.MapItems.HasVolatile())
                    wrap = SeqWrapKind.MustCache;
                else if (bgb.AggItems.HasVolatile())
                    wrap = SeqWrapKind.MustCache;
            }

            GenWrap(wrap, stItemDst);
            IL.MarkLabelIfUsed(labDone);

            Validation.Assert(GetTopScope() == scopeBase);
            Validation.Assert(cur == idx + bgb.NodeCount);

            return false;
        }

        /// <summary>
        /// Generate the key selector for GroupBy. See the comments in PreVisitGroupBy for details.
        /// </summary>
        protected int GenGroupByKeySelector(
            BndGroupByNode bgb, int idx, DType typeItemSrc, Type stItemSrc, Type stItemSrcInd, int cur,
            out DType typeKey, out Type stKey, out DName[] namesKey)
        {
            AssertIdx(bgb, idx);

            bool isIndexed = stItemSrcInd != null;
            Validation.Assert(bgb.IndexForKeys == null || isIndexed);
            Validation.Assert(!isIndexed ||
                typeof(ValueTuple<,>).MakeGenericType(stItemSrc, typeof(long)).IsAssignableFrom(stItemSrcInd));

            // The current top scope.
            var scopeBase = GetTopScope();

            // Count the number of source fields, get the field names, both in src and in key,
            // and build the initial typeKey record type. Note that we may decide not to use this
            // typeKey record, instead using typeItemSrc (when all keys are fields of typeItemSrc),
            // or a single primitive type (when there is only a single key).
            int ckey = bgb.PureKeys.Length + bgb.KeepKeys.Count;
            int cfld = 0;
            var keys = new BoundNode[ckey];
            var namesKeySrc = new DName[ckey];
            var namesKeyTmp = new DName[ckey];
            typeKey = DType.EmptyRecordReq;
            int ikey = 0;

            // In the indexed case, need to unpack values from indexed pairs into locals. But we only
            // need to do this for the source item if the scope for the key is referenced somewhere.
            bool unpackItemSrc = false;

            for (; ikey < bgb.PureKeys.Length; ikey++)
            {
                var val = keys[ikey] = bgb.PureKeys[ikey];
                if (BndGetFieldNode.IsScopeField(val, bgb.ScopeForKeys, out namesKeySrc[ikey]))
                {
                    unpackItemSrc = true;
                    cfld++;
                }
                else if (!unpackItemSrc && isIndexed && ScopeCounter.Any(val, bgb.ScopeForKeys))
                    unpackItemSrc = true;

                namesKeyTmp[ikey] = new DName(string.Format("X{0}", ikey));
                typeKey = typeKey.AddNameType(namesKeyTmp[ikey], val.Type);
            }
            Validation.Assert(ikey == bgb.PureKeys.Length);
            foreach (var (name, val) in bgb.KeepKeys.GetPairs())
            {
                keys[ikey] = val;
                if (BndGetFieldNode.IsScopeField(val, bgb.ScopeForKeys, out namesKeySrc[ikey]))
                {
                    unpackItemSrc = true;
                    cfld++;
                }
                else if (!unpackItemSrc && isIndexed && ScopeCounter.Any(val, bgb.ScopeForKeys))
                    unpackItemSrc = true;

                namesKeyTmp[ikey] = new DName(string.Format("X{0}", ikey));
                typeKey = typeKey.AddNameType(namesKeyTmp[ikey], val.Type);
                ikey++;
            }
            Validation.Assert(ikey == ckey);
            Validation.Assert(typeKey.FieldCount == ckey);

            // Push the scopes for the keySelector code.
            if (bgb.IndexForKeys != null)
            {
                InitScope(bgb.IndexForKeys, bgb, idx, -1, isArgValid: true);
                PushScope(bgb.IndexForKeys, bgb, idx, -1, isArgValid: true);
            }
            bool isValid = bgb.ScopeForKeys.IsValidArg(bgb.Source);
            InitScope(bgb.ScopeForKeys, bgb, idx, -1, isValid);
            PushScope(bgb.ScopeForKeys, bgb, idx, -1, isValid);
            var scopeCur = GetTopScope();

            bool computeKey = ckey == 1 || cfld < ckey || typeItemSrc.IsRootOpt || isIndexed;
            if (computeKey)
            {
                // Key is not just a field of typeItemSrc, so we can't get away with using the identity.
                // If there is only one key, use the primitive value for the key, not an object.
                // This saves us from having to generate a comparer.

                if (ckey == 1)
                {
                    typeKey = typeKey.GetNameTypeOrDefault(namesKeyTmp[0]);
                    namesKey = null;
                }
                else
                    namesKey = namesKeyTmp;
                stKey = GetSysType(typeKey);

                var nestedArgs = _refMaps.GetNestedArgList(bgb, idx);
                Validation.Assert(nestedArgs.Count == ckey + bgb.MapItems.Count + bgb.AggItems.Count);

                int inaMin = nestedArgs[0].Item1;
                int inaLim = nestedArgs[ckey - 1].Item1 + 1;
                var extScopes = FindExtScopes(scopeBase, inaMin, inaLim);
                var usesGlobals = UsesGlobals(inaMin, inaLim);
                var usesExecCtx = UsesExecCtx(inaMin, inaLim);

                if (isIndexed)
                {
                    // The key selector accepts a (TSource, long).
                    // The items need to be unpacked and mapped to locals for the used scopes.
                    var info = unpackItemSrc ? _refMaps.GetScopeInfoFromOwner(bgb, idx, bgb.ScopeForKeys) : null;
                    Validation.Assert(info == null || info.Slot == -1);
                    var infoInd = _refMaps.GetScopeInfoFromOwner(bgb, idx, bgb.IndexForKeys);
                    Validation.Assert(infoInd == null || infoInd.Slot == -1);
                    StartUnpackFunction("keySelector",
                        extScopes, usesGlobals, usesExecCtx,
                        stKey, stItemSrcInd, info, infoInd);
                }
                else
                {
                    var info = _refMaps.GetScopeInfoFromOwner(bgb, idx, bgb.ScopeForKeys);
                    Validation.Assert(info.Slot == -1);
                    var scopeMap = new ScopeMap { { info.Index, LocArgInfo.FromArg(1, stItemSrc) } };
                    StartFunctionCore("keySelector",
                        scopeMap, extScopes, usesGlobals, usesExecCtx,
                        stKey, stItemSrc);
                }

                var rg = ckey > 1 ? CreateRecordGenerator(typeKey) : null;
                for (int i = 0; i < ckey; i++)
                {
                    PushNestedArg(bgb, idx, i, needsDelegate: true);
                    var nested = GetTopNestedArg();
                    var key = keys[i];

                    Validation.Assert(GetTopScope() == scopeCur);
                    if (rg == null)
                        cur = key.Accept(this, cur);
                    else
                    {
                        // REVIEW: Optimize when key is a BndCastOptNode?
                        rg.SetFromStackPre(namesKeyTmp[i], key.Type);
                        cur = key.Accept(this, cur);
                        PopType(key.Type);
                        rg.SetFromStackPost();
                    }
                    Validation.Assert(GetTopScope() == scopeCur);

                    Validation.Assert(GetTopNestedArg() == nested);
                    PopNestedArg();
                }
                if (rg != null)
                {
                    rg.Finish();
                    PushType(typeKey);
                }

                EndFunctionCore();
            }
            else
            {
                // Use the source item as key object, so keySelector is just the identity function.
                Validation.Assert(ckey > 1 & cfld == ckey);
                Validation.Assert(typeItemSrc.IsRecordReq);

                typeKey = typeItemSrc;
                stKey = stItemSrc;
                namesKey = namesKeySrc;

                // Load the identity keySelector delegate.
                GenLoadIdentityFunc(stItemSrc);

                // Still need to return the correct position.
                for (int i = 0; i < ckey; i++)
                    cur += keys[i].NodeCount;
            }
            Validation.Assert(ckey == 1 & typeKey.IsEquatable || ckey > 1 & typeKey.IsRecordReq);

            // REVIEW: Should we wait to dispose the scopes until after PostVisit?
            Validation.Assert(GetTopScope() == scopeCur);
            PopScope(bgb.ScopeForKeys);
            DisposeScope(bgb.ScopeForKeys, bgb, idx, -1);
            if (bgb.IndexForKeys != null)
            {
                PopScope(bgb.IndexForKeys);
                DisposeScope(bgb.IndexForKeys, bgb, idx, -1);
            }
            Validation.Assert(GetTopScope() == scopeBase);

            return cur;
        }

        /// <summary>
        /// Generates code to allocate the destination record and copy keep keys into it.
        /// </summary>
        protected TypeManager.RecordGenerator GenGroupByCopyKeys(BndGroupByNode bgb, DType typeKey, Type stKey, DName[] namesKey, DType typeItemDst, Type stItemDst)
        {
            Validation.AssertValue(bgb);
            Validation.Assert(typeItemDst.IsRecordReq);

            int ckey = bgb.PureKeys.Length + bgb.KeepKeys.Count;
            Validation.Assert((namesKey != null) == (ckey > 1));
            Validation.Assert(ckey == 1 || namesKey.Length == ckey);

            var rg = CreateRecordGenerator(typeItemDst);

            // Copy keys from typeKey to typeItemDst.
            int ikey = bgb.PureKeys.Length;
            var laiKey = LocArgInfo.FromArg(1, stKey);
            foreach (var (name, val) in bgb.KeepKeys.GetPairs())
            {
                Validation.Assert(name.IsValid);
                Validation.Assert(val != null);

                DType type = val.Type;

                if (ckey == 1)
                {
                    Validation.Assert(type == typeKey);
                    rg.SetFromLocal(name, in laiKey);
                }
                else
                {
                    DName nameKey = namesKey[ikey];
                    Validation.Assert(nameKey.IsValid);
                    Validation.Assert(type == typeKey.GetNameTypeOrDefault(nameKey));
                    rg.SetFromLocalField(name, typeKey, nameKey, in laiKey);
                }
                ikey++;
            }
            Validation.Assert(ikey == ckey);

            return rg;
        }

        /// <summary>
        /// Generate the function to construct the sub-record for "auto". The sub-record contains
        /// all fields that aren't keys for the GroupBy. The case when the sub-record contains all
        /// fields should be handled by the caller (this asserts).
        /// </summary>
        protected void GenGroupByAutoFunc(BndGroupByNode bgb, string nameFn,
            DType typeItemSrc, Type stItemSrc, Type stItemSrcInd,
            DType typeItemDst, Type stItemDst)
        {
            Validation.AssertNonEmpty(nameFn);
            Validation.AssertValue(bgb);
            Validation.Assert(typeItemSrc.IsRecordXxx);
            Validation.Assert(typeItemDst.IsRecordXxx);
            // typeItemSrc has some extra fields (the key fields).
            Validation.Assert(typeItemDst.Accepts(typeItemSrc, union: false));
            Validation.Assert(typeItemDst != typeItemSrc);

            bool isIndexed = stItemSrcInd != null;
            Validation.Assert(bgb.IndexForMaps == null || isIndexed);
            Validation.Assert(!isIndexed || typeof(ValueTuple<,>).MakeGenericType(stItemSrc, typeof(long)).IsAssignableFrom(stItemSrcInd));

            LocArgInfo laiSrc;
            if (isIndexed)
            {
                StartFunctionDisjoint(nameFn, stItemDst, stItemSrcInd);
                IL.Ldarg(1);
                var finItem1 = stItemSrcInd.GetField(CodeGenUtil.SysValTupItem1).VerifyValue();
                Validation.Assert(finItem1.FieldType == stItemSrc);
                IL.Ldfld(finItem1);

                // Note we don't dispose the local so laiSrc remains valid.
                var locSrc = IL.DeclareLocalRaw(stItemSrc);
                laiSrc = new LocArgInfo(locSrc);
                IL.Stloc(locSrc);
            }
            else
            {
                StartFunctionDisjoint(nameFn, stItemDst, stItemSrc);
                laiSrc = LocArgInfo.FromArg(1, stItemSrc);
            }

            bool isOpt = typeItemSrc.IsRootOpt;
            Validation.Assert(isOpt == typeItemDst.IsRootOpt);

            Label labDone = default;
            if (isOpt)
            {
                // Null maps to null.
                IL.LdLocArg(laiSrc); // Load the src item.
                GenMapNullRecordToNullRecord(IL.Ensure(ref labDone), typeItemSrc, stItemSrc, typeItemDst, stItemDst);
                IL.Pop(); // Pop the src item, we don't need it here.

                // From here on, the src and dst items are not null.
                typeItemSrc = typeItemSrc.ToReq();
                typeItemDst = typeItemDst.ToReq();
            }

            // Create the element object and copy fields.
            using var rg = CreateRecordGenerator(typeItemDst);

            foreach (var tn in typeItemDst.GetNames())
                rg.SetFromLocalField(tn.Name, typeItemSrc, tn.Name, in laiSrc);
            rg.Finish();
            PushType(typeItemDst);

            IL.MarkLabelIfUsed(labDone);
            EndFunctionCore();
        }

        /// <summary>
        /// Generate code to compute and store the "map" fields in the destination record. On entry, the
        /// wrapped group is on top of the stack. On exit, the same should be true.
        /// </summary>
        protected int GenGroupByMapFields(TypeManager.RecordGenerator rg, BndGroupByNode bgb, int idx,
            DType typeItemSrc, Type stItemSrc, Type stItemSrcInd, int cur)
        {
            Validation.AssertValue(rg);
            AssertIdx(bgb, idx);
            Validation.Assert(bgb.MapItems.Count > 0);
            Validation.Assert(bgb.ScopeForMaps != null);

            bool isMapIndexed = stItemSrcInd != null;
            Validation.Assert(isMapIndexed ^ bgb.IndexForMaps == null);
            Validation.Assert(!isMapIndexed || typeof(ValueTuple<,>).MakeGenericType(stItemSrc, typeof(long)).IsAssignableFrom(stItemSrcInd));

            int slotBase = bgb.PureKeys.Length + bgb.KeepKeys.Count;

            // The wrapped IE<TSource> for the group is on top. We need to leave the stack in this state. Store the IE<TSource> in a local.
            var stSrc = _typeManager.MakeSequenceType(isMapIndexed ? stItemSrcInd : stItemSrc);
            using var locGroup = MethCur.AcquireLocal(stSrc);
            IL.Stloc(locGroup);
            PopType(locGroup.Type);

            // The current top scope.
            var scopeBase = GetTopScope();

            // Push the scopes for the maps.
            if (bgb.IndexForMaps != null)
            {
                InitScope(bgb.IndexForMaps, bgb, idx, -2, isArgValid: true);
                PushScope(bgb.IndexForMaps, bgb, idx, -2, isArgValid: true);
            }
            bool isValid = bgb.ScopeForMaps.IsValidArg(bgb.Source);
            InitScope(bgb.ScopeForMaps, bgb, idx, -2, isValid);
            PushScope(bgb.ScopeForMaps, bgb, idx, -2, isValid);
            var scopeCur = GetTopScope();

            int i = -1;
            foreach (var (name, value) in bgb.MapItems.GetPairs())
            {
                Validation.Assert(name.IsValid);
                i++;

                string nameFn = string.Format("mapItem{0}", i);

                DType typeMap;
                if (value == null)
                {
                    // This is the "auto" case. The element is an auto-generated record type, which is just
                    // a sub-record of typeItemSrc.
                    rg.RecType.TryGetNameType(name, out typeMap).Verify();
                    Validation.Assert(typeMap.IsSequence);
                    var typeItem = typeMap.ItemTypeOrThis;
                    var stItem = GetSysType(typeItem);

                    // Prepare to store the field.
                    rg.SetFromStackPre(name, typeMap);

                    // Prepare to invoke Map/Select.
                    IL.Ldloc(locGroup);
                    PushType(locGroup.Type);

                    if (typeItem == typeItemSrc)
                    {
                        // The nested item type and source type are the same.
                        // In the non-indexed case, we use the identity, so no need to do the map/select!
                        // For indexed, unwrap the items from the indexed pairs.
                        Validation.Assert(stItemSrc == stItem);
                        if (isMapIndexed)
                            EmitCall(CodeGenUtil.UnwrapIndPairsToItems(stItemSrc));
                    }
                    else
                    {
                        Validation.Assert(typeMap.IsTableXxx);
                        Validation.Assert(typeItem.IsRecordXxx);

                        // Copy fields from the source item to the inner element item.
                        GenGroupByAutoFunc(bgb, nameFn, typeItemSrc, stItemSrc, stItemSrcInd, typeItem, stItem);

                        // Invoke Map/Select.
                        var meth = CodeGenUtil.GetMethodInfo2<IE, Func<object, object>, IE>(
                            Enumerable.Select, isMapIndexed ? stItemSrcInd : stItemSrc, stItem);
                        EmitCall(meth);
                        GenWrap(default, stItem);
                    }
                }
                else
                {
                    // The map element is computed, not auto-generated.
                    var typeItem = value.Type;
                    typeMap = typeItem.ToSequence();
                    var stItem = GetSysType(typeItem);

                    // Prepare to store the field.
                    rg.SetFromStackPre(name, typeMap);

                    // Prepare to invoke Map/Select.
                    IL.Ldloc(locGroup);
                    PushType(locGroup.Type);

                    // The map scope is the first (non-frame) parameter.
                    PushNestedArg(bgb, idx, slotBase + i, needsDelegate: true);
                    var nested = GetTopNestedArg();

                    var extScopes = FindExtScopes(scopeBase, nested);
                    bool usesGlobals = UsesGlobals(nested);
                    bool usesExecCtx = UsesExecCtx(nested);

                    var info = _refMaps.GetScopeInfoFromOwner(bgb, idx, bgb.ScopeForMaps);
                    Validation.Assert(info.Slot == -2);
                    if (isMapIndexed)
                    {
                        // The map item selectors accept a (TSource, long).
                        // Unpack the items from the tuple into locals and map the scopes to them.
                        // Note we assume the scope for maps is used for simplicity.
                        Validation.Assert(bgb.IndexForMaps != null);
                        var infoInd = ScopeCounter.Any(value, bgb.IndexForMaps) ?
                            _refMaps.GetScopeInfoFromOwner(bgb, idx, bgb.IndexForMaps) : null;
                        Validation.Assert(infoInd == null || infoInd.Slot == -2);
                        StartUnpackFunction(nameFn,
                            extScopes, usesGlobals, usesExecCtx,
                            stItem, stItemSrcInd,
                            info, infoInd);
                    }
                    else
                    {
                        var scopeMap = new ScopeMap { { info.Index, LocArgInfo.FromArg(1, stItemSrc) } };
                        StartFunctionCore(nameFn,
                            scopeMap, extScopes, usesGlobals, usesExecCtx,
                            stItem, stItemSrc);
                    }

                    Validation.Assert(GetTopScope() == scopeCur);
                    cur = value.Accept(this, cur);
                    Validation.Assert(GetTopScope() == scopeCur);

                    EndFunctionCore();

                    // Invoke Map/Select.
                    var meth = CodeGenUtil.GetMethodInfo2<IE, Func<object, object>, IE>(
                        Enumerable.Select, isMapIndexed ? stItemSrcInd : stItemSrc, stItem);
                    EmitCall(meth);
                    GenWrap(default, stItem);

                    Validation.Assert(GetTopNestedArg() == nested);
                    PopNestedArg();
                }

                // Store it in the field.
                Validation.Assert(rg.RecType.GetNameTypeOrDefault(name) == typeMap);
                rg.SetFromStackPost();
                PopType(typeMap);
            }

            // REVIEW: Should we wait to dispose the scopes until after PostVisit?
            Validation.Assert(GetTopScope() == scopeCur);
            PopScope(bgb.ScopeForMaps);
            DisposeScope(bgb.ScopeForMaps, bgb, idx, -2);
            if (isMapIndexed)
            {
                PopScope(bgb.IndexForMaps);
                DisposeScope(bgb.IndexForMaps, bgb, idx, -2);
            }
            Validation.Assert(GetTopScope() == scopeBase);

            // Need to leave the group on top of the stack.
            IL.Ldloc(locGroup);
            PushType(locGroup.Type);

            return cur;
        }

        /// <summary>
        /// Generate code to compute and add the aggs to the destination item. On entry the top stack
        /// value is the group sequence. On exit, the group sequence has been popped, and the top stack
        /// value is the destination item if <paramref name="rg"/> is null.
        /// </summary>
        protected int GenGroupByAggs(TypeManager.RecordGenerator rg, BndGroupByNode bgb, int idx, int cur)
        {
            Validation.AssertValueOrNull(rg);
            AssertIdx(bgb, idx);
            Validation.Assert(bgb.ScopeForAggs != null);

            // Push the scope for aggs. PushScope also stores the value in a local.
            bool isValid = bgb.ScopeForAggs.IsValidArg(bgb.Source);
            InitScope(bgb.ScopeForAggs, bgb, idx, -3, isValid);
            PushScope(bgb.ScopeForAggs, bgb, idx, -3, isValid);
            var scopeCur = GetTopScope();

            int slotBase = bgb.PureKeys.Length + bgb.KeepKeys.Count + bgb.MapItems.Count;
            int i = -1;
            foreach (var (name, value) in bgb.AggItems.GetPairs())
            {
                i++;
                if (rg != null)
                    rg.SetFromStackPre(name, value.Type);

                PushNestedArg(bgb, idx, slotBase + i, needsDelegate: true);
                var nested = GetTopNestedArg();

                Validation.Assert(GetTopScope() == scopeCur);
                cur = value.Accept(this, cur);
                Validation.Assert(GetTopScope() == scopeCur);

                Validation.Assert(GetTopNestedArg() == nested);
                PopNestedArg();

                if (rg != null)
                {
                    rg.SetFromStackPost();
                    PopType(value.Type);
                }
            }

            Validation.Assert(GetTopScope() == scopeCur);
            PopScope(bgb.ScopeForAggs);

            // REVIEW: Should we wait to dispose the scopes until after PostVisit?
            DisposeScope(bgb.ScopeForAggs, bgb, idx, -3);

            return cur;
        }

        /// <summary>
        /// Generates code for the IEqualityComparer for a GroupBy operation. Pushes the
        /// comparer on the stack.
        /// </summary>
        protected void GenGroupByComparer(DType typeKey, Type stKey, DName[] namesKey, BitSet keysCi)
        {
            // This handles the case when the input type to the comparer is a required record type.
            Validation.Assert(typeKey.IsRecordReq);
            int ckey = namesKey.Length;
            Validation.Assert(ckey > 1);
            Validation.Assert(ckey <= typeKey.FieldCount);

            // REVIEW: Perhaps keep a Cache of these?

            // Create the Equals function.
            Delegate fnEquals;
            {
                StartFunctionDisjoint("Equals", typeof(bool), stKey, stKey);

                Label labFalse = IL.DefineLabel();
                Label labRet = IL.DefineLabel();
                for (int ikey = 0; ;)
                {
                    DName name = namesKey[ikey];
                    Validation.Assert(name.IsValid);
                    bool ci = keysCi.TestBit(ikey);
                    bool isLast = ++ikey >= ckey;

                    typeKey.TryGetNameType(name, out DType typeFld).Verify();
                    Type stFld = GetSysType(typeFld);
                    bool isNullable = _typeManager.IsNullableType(stFld);
                    var stReq = isNullable ? GetSysType(typeFld.ToReq()) : stFld;

                    // For R8/R4, we use the strongly typed Equals method rather than inline equality testing
                    // so NaN compares equal to NaN.
                    MethodInfo methEq = null;
                    switch (typeFld.RootKind)
                    {
                    case DKind.R8:
                    case DKind.R4:
                        methEq = CodeGenUtil.EquatableEqualsVal(stReq);
                        Validation.Assert(methEq.IsStatic);
                        break;

                    case DKind.Text:
                        if (ci)
                        {
                            methEq = EqCmpStrCi.MethEqualsStatic;
                            Validation.Assert(methEq.IsStatic);
                        }
                        break;

                    case DKind.Record:
                    case DKind.Tuple:
                        if (ci)
                        {
                            GenLoadAggEqCmpCore(typeFld, ti: false, ci: ci, info: out var info);
                            Validation.Assert(stFld == info.StItem);
                            PushType(info.StEq);
                            methEq = info.MethEquals;
                            Validation.Assert(!methEq.IsStatic);
                        }
                        else
                        {
                            methEq = GetAggEquals2(typeFld, out Type stTmp);
                            Validation.Assert(stTmp == stFld);
                            Validation.Assert(methEq.IsStatic);
                        }
                        break;
                    }

                    Label labNext = default;
                    if (_typeManager.IsNullableType(stFld))
                    {
                        // Nullable field. Test the null bits separate from the values.
                        Label labGood1 = default;

                        IL.Ldarg(1);
                        _typeManager.GenLoadFieldBit(MethCur, typeKey, stKey, name, typeFld);
                        IL.Ldarg(2);
                        _typeManager.GenLoadFieldBit(MethCur, typeKey, stKey, name, typeFld);
                        IL.Brtrue(ref labGood1);

                        // Right is null. Result is true iff the left is also null.
                        if (isLast)
                            IL.Ldc_I4(0).Ceq().Br(ref labRet);
                        else
                            IL.Brtrue(ref labFalse).Br(ref labNext);

                        // Right is non-null.
                        IL.MarkLabel(labGood1);
                        IL.Brfalse(ref labFalse);

                        // Both are non-null.
                        typeFld = typeFld.ToReq();
                        IL.Ldarg(1);
                        _typeManager.GenLoadFieldReq(MethCur, typeKey, stKey, name, typeFld);
                        PushType(typeFld);
                        IL.Ldarg(2);
                        _typeManager.GenLoadFieldReq(MethCur, typeKey, stKey, name, typeFld);
                        PushType(typeFld);
                    }
                    else
                    {
                        IL.Ldarg(1);
                        GenLoadField(typeKey, stKey, name, typeFld);
                        PushType(typeFld);
                        IL.Ldarg(2);
                        GenLoadField(typeKey, stKey, name, typeFld);
                        PushType(typeFld);
                    }

                    if (methEq != null)
                    {
                        if (methEq.IsStatic)
                            EmitCall(methEq);
                        else
                            EmitCallVirt(methEq);
                        if (isLast)
                            break;
                        IL.Brfalse(ref labFalse);
                        PopType(typeof(bool));
                    }
                    else
                    {
                        if (isLast)
                        {
                            GenCmp(CompareOp.Equal, typeFld);
                            break;
                        }
                        GenCmpBr(CompareOp.Equal, typeFld, ref labFalse);
                    }

                    IL.MarkLabelIfUsed(labNext);
                }

                Validation.Assert(IL.IsLabelUsed(labFalse));
                IL
                    .Br(ref labRet)
                    .MarkLabel(labFalse)
                    .Ldc_I4(0)
                    .MarkLabel(labRet);

                PeekType(typeof(bool));
                (_, fnEquals) = EndFunctionToDelegate();
            }

            // Create the GetHashCode function.
            Delegate fnGetHash;
            {
                StartFunctionDisjoint("GetHashCode", typeof(int), stKey);

                int count = 0;
                Type[] stsKey = new Type[Math.Min(8, ckey)];
                for (int ikey = 0; ikey < ckey; ikey++)
                {
                    Validation.Assert(count < stsKey.Length);
                    DName name = namesKey[ikey];
                    typeKey.TryGetNameType(name, out DType typeFld).Verify();
                    bool ci = keysCi.TestBit(ikey);

                    MethodInfo methGetHash = null;
                    switch (typeFld.RootKind)
                    {
                    case DKind.Record:
                    case DKind.Tuple:
                        GenLoadAggEqCmpCore(typeFld, ti: false, ci: ci, info: out var info);
                        PushType(info.StEq);
                        methGetHash = info.MethGetHash;
                        Validation.Assert(!methGetHash.IsStatic);
                        break;

                    case DKind.Text:
                        if (ci)
                        {
                            methGetHash = EqCmpStrCi.MethGetHashStatic;
                            Validation.Assert(methGetHash.IsStatic);
                        }
                        break;

                    default:
                        // REVIEW: Improve this for nullables? Separate bit from value?
                        break;
                    }

                    IL.Ldarg(1);
                    GenLoadField(typeKey, stKey, name, typeFld);
                    PushType(typeFld);

                    if (methGetHash is not null)
                    {
                        Validation.Assert(methGetHash.ReturnType == typeof(int));
                        if (methGetHash.IsStatic)
                            EmitCall(methGetHash);
                        else
                            EmitCallVirt(methGetHash);
                        stsKey[count++] = typeof(int);
                    }
                    else
                        stsKey[count++] = GetSysType(typeFld);

                    if (count == 8)
                    {
                        EmitCall(CodeGenUtil.HashCombine8.MakeGenericMethod(stsKey));
                        stsKey[0] = typeof(int);
                        count = 1;
                    }
                }

                Validation.Assert(1 <= count & count < 8);
                if (count > 1)
                {
                    MethodInfo methComb = CodeGenUtil.GetHashCombine(count);
                    Array.Resize(ref stsKey, count);
                    EmitCall(methComb.MakeGenericMethod(stsKey));
                }

                (_, fnGetHash) = EndFunctionToDelegate();
            }

            // Create the IEqualityComparer and stash it in the frame object array.
            Type stCmp = typeof(GroupByComparer<>).MakeGenericType(stKey);
            var cmp = Activator.CreateInstance(stCmp, fnGetHash, fnEquals);

            GenLoadConstCore(cmp, stCmp);
            PushType(stCmp);
        }

        protected override void PostVisitImpl(BndGroupByNode bnd, int idx)
        {
            throw Unexpected("group-by post");
        }
    }
}

public sealed class EnumerableCodeGenerator : EnumerableCodeGeneratorBase
{
    public EnumerableCodeGenerator(EnumerableTypeManager typeManager, GeneratorRegistry operGens)
        : base(typeManager, operGens)
    {
    }

    public override CodeGenResult Run(BoundNode tree, CodeGenHost? host = null,
        Action<string> ilSink = null, ILLogKind logKind = ILLogKind.None)
    {
        var gen = new Impl(this, host);
        return gen.Translate(tree, ilSink, logKind);
    }

    new private sealed class Impl : EnumerableCodeGeneratorBase.Impl
    {
        public Impl(EnumerableCodeGenerator codeGen, CodeGenHost host)
            : base(codeGen, host)
        {
        }
    }
}

public sealed class CachingEnumerableCodeGenerator : EnumerableCodeGeneratorBase
{
    public CachingEnumerableCodeGenerator(EnumerableTypeManager typeManager, GeneratorRegistry operGens)
        : base(typeManager, operGens)
    {
    }

    public override CodeGenResult Run(BoundNode tree, CodeGenHost? host = null,
        Action<string> ilSink = null, ILLogKind logKind = ILLogKind.None)
    {
        var gen = new Impl(this, host);
        return gen.Translate(tree, ilSink, logKind);
    }

    new private sealed class Impl : EnumerableCodeGeneratorBase.Impl
    {
        public Impl(CachingEnumerableCodeGenerator codeGen, CodeGenHost host)
            : base(codeGen, host)
        {
        }

        protected override Type GenWrap(SeqWrapKind wrap, Type stSrc, Type stItem)
        {
            Validation.AssertValue(stItem);
            Validation.AssertValue(stSrc);

            // Default is to cache.
            return base.GenWrapCore(wrap, stSrc, stItem, defaultWrap: true);
        }
    }
}
