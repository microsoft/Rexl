// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using Microsoft.Rexl.Lex;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Bind;

using ArgTuple = Immutable.Array<BoundNode>;
using Conditional = System.Diagnostics.ConditionalAttribute;
using DirTuple = Immutable.Array<Directive>;
using NameTuple = Immutable.Array<DName>;
using NodeTuple = Immutable.Array<ExprNode>;
using ScopeTuple = Immutable.Array<ArgScope>;
using TokenTuple = Immutable.Array<Token>;

partial class BoundFormula
{
    partial class Binder
    {
        /// <summary>
        /// This handles binding <see cref="CallNode"/> and other parse structures that should resolve to
        /// a function/operation call, for example, record projection. Note that this implements
        /// <see cref="InvocationInfo"/> defined by the rexl operation code.
        /// </summary>
        private sealed class CallInfo : InvocationInfo
        {
            #region State set by the ctor

            private readonly Binder _vtor;
            private readonly ScopeWrapper _scopeBase;

            public override ExprNode ParseNode { get; }
            public override NodeTuple ParseArgs { get; }

            /// <summary>
            /// Info is null when an <see cref="UnknownFunc"/> is bound.
            /// </summary>
            public override OperInfo Info { get; }
            public override RexlOper Oper { get; }

            // The first bound node, when there is one.
            public BoundNode First { get; }

            public RexlDiagnostic ErrRoot { get; }

            #endregion State set by the ctor

            #region State filled in while binding.

            private BitSet _maskLiftable;

            // Note that this state should all be set by the time SpecializeTypes is called.
            private ArgTuple _args;
            private ScopeTuple _scopes;
            private ScopeTuple _indices;
            private NameTuple _names;
            private TokenTuple _nameTokens;
            private DirTuple _dirs;
            private ArgTraits _traits;

            public override ArgTuple Args => _args;
            public override ScopeTuple Scopes => _scopes;
            public override ScopeTuple Indices => _indices;
            public override NameTuple Names => _names;
            public override TokenTuple NameTokens => _nameTokens;
            public override DirTuple Dirs => _dirs;
            public override ArgTraits Traits => _traits;

            #endregion State filled in while binding.

            // For re-binding when a scope type needs to be promoted. For example,
            //   'Fold(x:Range(1, 11), y:1, x * y)'.
            // The 'y:1' parameter needs to be promoted to i8, from its natural i4, since its type must
            // match the type of the 'x * y' parameter.
            public Dictionary<int, DType> _scopeTypeChanges;
            public Dictionary<int, DType> _applyTypeChanges;
            public ArgTraits _traitsChange;

            private CallInfo(Binder vtor, ExprNode node, NodeTuple pargs, BoundNode first,
                    OperInfo info, RexlOper oper,
                    int arity, RexlDiagnostic errRoot)
                : base()
            {
                Validation.AssertValue(vtor);
                Validation.AssertValue(node);
                Validation.Assert(!pargs.IsDefault);
                Validation.AssertValue(oper);
                Validation.Assert(oper.SupportsArity(arity));
                Validation.AssertValueOrNull(first);

                _vtor = vtor;
                _scopeBase = _vtor._scopeCur;
                ParseNode = node;
                ParseArgs = pargs;
                First = first;

                Info = info;
                Oper = oper;

                GetNamesDirs(vtor, pargs, arity, isPipe: node is CallNode cn && cn.TokPipe != null,
                    dotted: oper.SupportsImplicitDotted, out var names, out var implicitNames, out var dirs);
                _traits = Oper.GetArgTraits(arity, names, implicitNames, dirs);
                Validation.Assert(_traits.SlotCount == arity);
                Validation.Assert(_traits.Oper == Oper);

                // No point in having scopes with fewer than two args!
                Validation.Assert(arity > 1 || _traits.ScopeCount == 0);

                ErrRoot = errRoot;

                AssertValid(false);
            }

            /// <summary>
            /// The <paramref name="nsType"/> comes from the pipe case, where the namespace may match the type. Eg, "Hello"->IndexOf(...)
            /// rather than Text.IndexOf("Hello", ...).
            /// </summary>
            private static (OperInfo info, bool fuzzy) GetOperInfo(Binder vtor, NPath nsType, IdentPath idents, int arity)
            {
                Validation.AssertValue(vtor);
                Validation.AssertValue(idents);

                bool rooted = idents.IsRooted;
                var name = idents.FullName;

                if (vtor._host.TryGetOperInfo(name, rooted, nsType, fuzzy: false, arity, out var info))
                {
                    Validation.Assert(info != null && info.Oper != null);
                    return (info, false);
                }

                // Didn't find it, so try fuzzy.
                if (vtor._host.TryGetOperInfo(name, rooted, nsType, fuzzy: true, arity, out info))
                {
                    Validation.Assert(info != null && info.Oper != null);
                    return (info, true);
                }

                return default;
            }

            /// <summary>
            /// Create from <see cref="CallNode"/>.
            /// </summary>
            public static CallInfo Create(Binder vtor, CallNode node)
            {
                Validation.AssertValue(vtor);
                Validation.AssertValue(node);

                bool allowProc = (vtor._options & BindOptions.AllowProc) != 0 && node == vtor._nodeTop;

                RexlDiagnostic errRoot = null;
                BoundNode first = null;
                NPath nsFunc = default;

                // Bind the first arg, if there is one, so we can use it for special cases, such as using it to
                // determine a namespace, or lifting over sequence on a first slot that introduces a With/Guard scope.
                if (node.Args.Count > 0)
                {
                    node.Args.Children[0].Accept(vtor);
                    first = vtor.Pop();
                    if (node.TokPipe != null && !node.IdentPath.IsRooted)
                        nsFunc = BindUtil.GetFuncNSForType(first.Type);
                }

                RexlOper oper;
                var (info, fuzzy) = GetOperInfo(vtor, nsFunc, node.IdentPath, node.Args.Count);

                if (info != null)
                {
                    Validation.Assert(info.Oper != null);
                    if (!allowProc && info.Oper.IsProc)
                        errRoot = vtor.Error(node, ErrorStrings.ErrUnknownFunction_Proc, info.Path);
                    else if (fuzzy)
                    {
                        Validation.Assert(info.Path != node.IdentPath.FullName);
                        Validation.Assert(info.Path != nsFunc.AppendPath(node.IdentPath.FullName));
                        string strGuess = info.Path.ToDottedSyntax();
                        if (node.IdentPath.IsRooted)
                            strGuess = "@" + strGuess;
                        Validation.Coverage(strGuess.Contains('\'') ? 1 : 0);
                        errRoot = vtor.ErrorGuess(node, ErrorStrings.ErrUnknownFunction_Guess, strGuess, node.IdentPath.Range,
                            info.Path.NameCount > 1 ? strGuess : info.Path.Leaf);
                    }
                    oper = info.Oper;
                }
                else
                {
                    // The Error is reported here, and stashed for use in the error node.
                    errRoot = vtor.Error(node, ErrorStrings.ErrUnknownFunction);
                    oper = UnknownFunc.Instance;
                }
                Validation.AssertValue(oper);

                int parseArity = node.Args.Count;
                if (!oper.SupportsArity(parseArity, out int arity))
                {
                    Validation.Assert(arity != parseArity);
                    if (arity > parseArity)
                        errRoot = vtor.Error(node, ErrorStrings.ErrArityTooSmall_Path_Num, oper.Path, arity - parseArity);
                    else
                        errRoot = vtor.Error(node.Args.Children[arity], ErrorStrings.ErrArityTooBig_Path_Num, oper.Path, parseArity - arity);
                }
                else if (info != null && info.Deprecated)
                {
                    if (info.PathAlt.IsRoot)
                        vtor.Warning(node, ErrorStrings.WrnDeprecatedFunction);
                    else
                    {
                        string sugg = info.PathAlt.ToDottedSyntax();
                        vtor.WarningGuess(node, ErrorStrings.WrnDeprecatedFunction_Alt, sugg, node.IdentPath.Range, sugg);
                    }
                }

                return new CallInfo(vtor, node, node.Args.Children, first, info, oper, arity, errRoot);
            }

            /// <summary>
            /// Create from other forms, eg, record projection.
            /// </summary>
            public static CallInfo Create(Binder vtor, ExprNode node, NodeTuple pargs, RexlOper oper, BoundNode first = null)
            {
                Validation.AssertValue(vtor);
                Validation.AssertValue(node);
                Validation.Assert(!pargs.IsDefault);
                Validation.AssertValue(oper);

                // Bind the first arg, if there is one, so we can use it for special cases, such as lifting over
                // sequence on a first slot that introduces a With/Guard scope.
                if (pargs.Length > 0 && first == null)
                {
                    pargs[0].Accept(vtor);
                    first = vtor.Pop();
                }
                return new CallInfo(vtor, node, pargs, first, null, oper, pargs.Length, null);
            }

            private static void GetNamesDirs(Binder vtor, NodeTuple pargs, int arity, bool isPipe, bool dotted, out NameTuple names, out BitSet implicitNames, out DirTuple dirs)
            {
                NameTuple.Builder bldrNames = null;
                BitSet implicits = default;
                DirTuple.Builder bldrDirs = null;
                var lim = arity;
                if (pargs.Length < arity)
                    lim = pargs.Length;
                for (int iarg = 0; iarg < lim; iarg++)
                {
                    var parg = pargs[iarg];
                    if (parg is DirectiveNode dn)
                    {
                        var dir = dn.Directive;
                        if (dir != Directive.None)
                        {
                            bldrDirs ??= DirTuple.CreateBuilder(arity, init: true);
                            bldrDirs[iarg] = dir;
                        }
                        parg = dn.Value;
                    }

                    if (parg is VariableDeclNode vdn)
                    {
                        var ident = vdn.Variable;
                        if (ident != null)
                        {
                            bldrNames ??= NameTuple.CreateBuilder(arity, init: true);
                            bldrNames[iarg] = ident.Name;
                        }
                    }
                    else if (!(iarg == 0 && isPipe) && vtor.IsImplicitName(parg, dotted, out var ident))
                    {
                        // Record an implicit name.
                        bldrNames ??= NameTuple.CreateBuilder(arity, init: true);
                        bldrNames[iarg] = ident.Name;
                        implicits = implicits.SetBit(iarg);
                    }
                }

                names = bldrNames == null ? default : bldrNames.ToImmutable();
                implicitNames = implicits;
                dirs = bldrDirs == null ? default : bldrDirs.ToImmutable();
            }

            [Conditional("DEBUG")]
            private void AssertValid(bool full)
            {
#if DEBUG
                Validation.Assert(_vtor != null);
                Validation.Assert(_scopeBase != null);
                Validation.Assert(ParseNode != null);
                Validation.Assert(!ParseArgs.IsDefault);
                Validation.Assert(!(Oper is UnknownFunc) | ErrRoot != null);
                Validation.Assert(Traits != null);
                Validation.Assert(Oper.SupportsArity(Traits.SlotCount));
                Validation.Assert(Traits.SlotCount == ParseArgs.Length | ErrRoot != null);

                if (full)
                {
                    Validation.Assert(!_args.IsDefault);
                    Validation.Assert(_args.Length == Traits.SlotCount);
                    Validation.Assert(!_scopes.IsDefault);
                    Validation.Assert(_scopes.Length == Traits.ScopeCount);
                    Validation.Assert(!_indices.IsDefault);
                    Validation.Assert(_indices.Length == Traits.ScopeIndexCount);
                    Validation.Assert(_names.IsDefault || _names.Length == _args.Length);
                    Validation.Assert(_nameTokens.IsDefault == _names.IsDefault);
                    Validation.Assert(_nameTokens.Length == _names.Length);
                    Validation.Assert(_dirs.IsDefault || _dirs.Length == _args.Length);
                }
#endif
            }

            public override void PostDiagnostic(BaseDiagnostic diag)
            {
                Validation.BugCheckValue(diag, nameof(diag));
                _vtor.AddDiagnostic(diag);
            }

            private (ExprNode, BoundNode) BindArg(int iarg)
            {
                Validation.AssertIndex(iarg, Arity);

                if (iarg >= ParseArity)
                {
                    Validation.Assert(ErrRoot != null);
                    Validation.Assert(iarg > 0 || First == null);
                    // REVIEW: Perhaps this should use the arg list node?
                    return (null, _vtor.Associate(ParseNode, BndMissingValueNode.Create(DType.Vac)));
                }

                var parg = ParseArgs[iarg];
                Validation.Assert(parg != null);

                var scopeTmp = _vtor._scopeCur;
                BoundNode arg;
                if (iarg == 0 && First != null)
                    arg = First;
                else
                {
                    parg.Accept(_vtor);
                    arg = _vtor.Pop();

                    // Accept should leave the scope as it found it.
                    Validation.Assert(_vtor._scopeCur == scopeTmp);

                    if (_applyTypeChanges != null && _applyTypeChanges.TryGetValue(iarg, out var typeNew))
                    {
                        Validation.Assert(Traits.GetScopeKind(iarg) == ScopeKind.Iter);
                        _vtor.CheckGeneralType(parg, ref arg, typeNew, DType.UseUnionDefault);
                    }
                }

                // Bind any extra/left-over args. We do this here, so relevant scopes are still active.
                if (iarg == Arity - 1 && Arity < ParseArity)
                {
                    Validation.Assert(ErrRoot != null);
                    for (int i = iarg + 1; i < ParseArity; i++)
                    {
                        ParseArgs[i].Accept(_vtor);
                        _vtor.Pop();
                    }
                }

                // Accept should leave the scope as it found it.
                Validation.Assert(_vtor._scopeCur == scopeTmp);

                return (parg, arg);
            }

            public void Visit()
            {
                AssertValid(false);

                // This special case is for functions that lift over sequence in the first slot and also introduce a
                // guard/with scope in the first slot. This covers GuardMap, WithMap, SetFields, etc.
                ScopeKind kind;
                if (First != null && Traits.MaskLiftSeq == 0x01 && First.Type.IsSequence && ParseArity >= 2 &&
                    ((kind = Traits.GetScopeKind(0)) == ScopeKind.Guard || kind == ScopeKind.With) && Oper.IsFunc)
                {
                    // Lift the first arg over sequence.
                    // REVIEW: Is there a better way? We need to do this before binding the other args,
                    // particularly for scope indexing.
                    _vtor.LiftUnary(LiftKinds.Seq, ParseNode, First,
                        (n, src) => Create(_vtor, n, ParseArgs, Oper, src).Visit());
                    return;
                }

                _vtor.Capture(out var cp);

                for (int count = 0; ;)
                {
                    VisitCore();

                    // REVIEW: How many times should we retry? A linear recurrence of
                    // order N can need N retries.
                    const int MaxRetryCount = 3;
                    if (_traitsChange == null && (_scopeTypeChanges == null || ++count > MaxRetryCount))
                        return;

                    // An (at least one) Iter scope needs to be promoted, or we need to readjust based
                    // on new ArgTraits, or both.
                    // Record and go around again.
                    Validation.Assert(_traitsChange != null || _scopeTypeChanges != null && _scopeTypeChanges.Count > 0);
                    Validation.Assert(_traitsChange != null || Traits.ScopeCount > 0);

                    _vtor.Pop();
                    _vtor.Rewind(in cp);

                    if (_traitsChange != null)
                    {
                        _traits = _traitsChange;
                        _traitsChange = null;
                    }
                    if (_scopeTypeChanges != null)
                    {
                        if (_applyTypeChanges == null)
                            _applyTypeChanges = _scopeTypeChanges;
                        else
                        {
                            foreach (var kvp in _scopeTypeChanges)
                                _applyTypeChanges[kvp.Key] = kvp.Value;
                        }
                        _scopeTypeChanges = null;
                    }
                }
            }

            private void VisitCore()
            {
                // Handle functions that introduce one or more scopes.
                var scopeCur = _scopeBase;

                // Immutable.Array builders.
                var bldrIndices = Traits.ScopeIndexCount > 0 ? ScopeTuple.CreateBuilder(Traits.ScopeIndexCount) : null;
                var bldrScopes = Traits.ScopeCount > 0 ? ScopeTuple.CreateBuilder(Traits.ScopeCount) : null;
                var bldrArgs = ArgTuple.CreateBuilder(Arity);
                DirTuple.Builder bldrDirs = null;
                NameTuple.Builder bldrNames = null;
                TokenTuple.Builder bldrNameToks = null;

                _maskLiftable = Oper.IsFunc ? Traits.MaskLiftSeq | Traits.MaskLiftTen | Traits.MaskLiftOpt : default;

                // Process the args.
                for (int iarg = 0; iarg < Arity; iarg++)
                {
                    // If non-nested follow immediately after some scopes, don't use the scopes when accepting.
                    if (Traits.IsNested(iarg))
                    {
                        Validation.Assert(iarg > 0);
                        _vtor._scopeCur = scopeCur;
                        int upCount = 0;
                        for (var scope = scopeCur; scope != _scopeBase; scope = scope.Outer)
                            scope.Enabled = Traits.IsScopeActive(iarg, upCount++);
                    }
                    else
                    {
                        _vtor._scopeCur = _scopeBase;
                        for (var scope = scopeCur; scope != _scopeBase; scope = scope.Outer)
                            scope.Enabled = false;
                    }

                    var (parg, arg) = BindArg(iarg);
                    Validation.Assert((parg != null) == (iarg < ParseArity));

                    // Unwind scopes if we're at the tail of a nested arg run.
                    if (Traits.IsNestedTail(iarg))
                    {
                        Validation.Assert(_vtor._scopeCur == scopeCur);
                        Validation.Assert(!Traits.IsScope(iarg));

                        int iscopeTmp = bldrScopes.Count;
                        while (_vtor._scopeCur != _scopeBase)
                        {
                            --iscopeTmp;
                            var sw = _vtor.PopScope();
                            Validation.Assert(sw.Scope == bldrScopes[iscopeTmp]);
                        }
                        scopeCur = _vtor._scopeCur;
                    }

                    // Handle directives.
                    bool unGuard = false;
                    if (parg is DirectiveNode dn)
                    {
                        var dir = dn.Directive;
                        Validation.Assert(dir != Directive.None);
                        if (!Oper.SupportsDirective(Traits, iarg, dir))
                        {
                            // For guard scopes, we allow [with] or [guard] to tweak scope kind.
                            if ((dir == Directive.Guard || dir == Directive.With) && Traits.GetScopeKind(iarg) == ScopeKind.Guard)
                                unGuard = dn.Directive == Directive.With;
                            else
                                _vtor.Error(dn.DirToken, dn, ErrorStrings.ErrBadDirective);
                        }
                        else
                        {
                            if (bldrDirs == null)
                                bldrDirs = DirTuple.CreateBuilder(Arity, init: true);
                            bldrDirs[iarg] = dir;
                        }
                        parg = dn.Value;
                    }

                    DType typeArg = arg.Type;
                    if (Traits.IsScope(iarg))
                    {
                        // For recording lifting. The first int is the kind of lifting and the second int depends on the
                        // kind of lifting:
                        // * 0: sequence, 2nd arg: count
                        // * 1: opt tensor, 2nd arg: rank
                        // * 2: req tensor, 2nd arg: rank
                        // * 3: opt, 2nd arg zero
                        List<(int, int)> lifts = null;

                        if (_maskLiftable.TestBit(iarg))
                        {
                            Validation.Assert(Oper.IsFunc);

                            // We need to determine the correct scope type. This involves analyzing lifting.
                            // We need to record the lifting that occurs so we can convert the arg if needed below.
                            for (; ; )
                            {
                                int cseqFinal = Traits.MaskLiftNeedsSeq.TestBit(iarg).ToNum();
                                if (Traits.LiftsOverSeq(iarg) && typeArg.SeqCount > cseqFinal && Oper.VerifyLiftOverSeq(Traits, iarg, typeArg))
                                {
                                    Util.Add(ref lifts, (0, typeArg.SeqCount - cseqFinal));
                                    typeArg = typeArg.RootType.ToSequence(cseqFinal);
                                }
                                else if (Traits.LiftsOverTen(iarg) && typeArg.IsTensorXxx && Oper.VerifyLiftOverTen(Traits, iarg, typeArg))
                                {
                                    Util.Add(ref lifts, (typeArg.IsOpt ? 1 : 2, typeArg.TensorRank));
                                    typeArg = typeArg.GetTensorItemType();
                                }
                                else if (Traits.LiftsOverOpt(iarg) && typeArg.HasReq && Oper.VerifyLiftOverOpt(Traits, iarg, typeArg))
                                {
                                    Util.Add(ref lifts, (3, 0));
                                    typeArg = typeArg.ToReq();
                                }
                                else
                                    break;
                            }
                        }

                        // Determine whether the scope arg needs to be converted.
                        var typeInner = typeArg;
                        typeArg = Oper.GetScopeArgType(Traits, iarg, typeInner);
                        if (Traits.GetScopeKind(iarg) == ScopeKind.SeqItem && !typeArg.IsSequence)
                            typeArg = typeArg.ToSequence();

                        if (typeArg != typeInner)
                        {
                            var typeSrc = typeArg;
                            if (lifts != null)
                            {
                                // Lifting was applied, so undo it at the type level (in reverse order).
                                while (lifts.Count > 0)
                                {
                                    var (kind, n) = lifts.Pop();
                                    switch (kind)
                                    {
                                    case 0:
                                        Validation.Assert(n > 0);
                                        typeSrc = typeSrc.ToSequence(n);
                                        break;
                                    case 1:
                                        Validation.Assert(n >= 0);
                                        typeSrc = typeSrc.ToTensor(true, n);
                                        break;
                                    case 2:
                                        Validation.Assert(n >= 0);
                                        typeSrc = typeSrc.ToTensor(false, n);
                                        break;
                                    default:
                                        Validation.Assert(kind == 3);
                                        Validation.Assert(n == 0);
                                        typeSrc = typeSrc.ToOpt();
                                        break;
                                    }
                                }
                            }

                            // Convert the arg.
                            var p = parg is VariableDeclNode vd ? vd.Value : parg ?? ParseNode;
                            _vtor.CheckGeneralType(p, ref arg, typeSrc, DType.UseUnionDefault);
                        }
                    }

                    // Handle names.
                    string variable = null;
                    ExprNode impName = null;

                    Identifier identAdd = null;
                    if (parg is VariableDeclNode vdn)
                    {
                        var ident = vdn.Variable;
                        if (ident != null)
                        {
                            // Record names for non-scope slots and for slots which require a name.
                            if (!Traits.SupportsName(iarg))
                                _vtor.Error(ident.Token, parg, ErrorStrings.ErrBadName);
                            else if (!Traits.IsScope(iarg) || Traits.RequiresName(iarg))
                                identAdd = ident;
                            variable = ident.Name.Value;
                        }
                        parg = vdn.Value;
                    }
                    else if (Traits.SupportsImplicitName(iarg) && _vtor.IsImplicitName(parg, Traits.SupportDottedImplicitNames, out var ident) &&
                        !(iarg == 0 && ParseNode is CallNode cn && cn.TokPipe != null))
                    {
                        // Record an implicit name. Note that piped first arguments do not form implicit names. Also, for
                        // with/guard scopes, we use implicit naming just for the scope name. We don't record it.
                        if (!Traits.IsScope(iarg))
                            identAdd = ident;
                        else
                        {
                            Validation.Assert(Traits.GetScopeKind(iarg) == ScopeKind.With || Traits.GetScopeKind(iarg) == ScopeKind.Guard);
                            // If there was lifting over sequence or tensor, don't use an implicit name, since that can be confusing,
                            // but DO use implicit naming for opt lifting.
                            if (arg.Type == typeArg || arg.Type == typeArg.ToOpt())
                            {
                                variable = ident.Name;
                                impName = parg;
                            }
                        }
                    }

                    if (identAdd != null || Traits.RequiresName(iarg))
                    {
                        // Record a name.
                        if (identAdd == null)
                        {
                            Validation.Assert(iarg < ParseArity || ErrRoot != null);
                            if (iarg < ParseArity && !arg.HasErrors)
                                _vtor.Error(parg, ErrorStrings.ErrNeedName_Slot_Func, iarg + 1, Oper.Name);
                        }
                        if (bldrNames == null)
                        {
                            bldrNames = NameTuple.CreateBuilder(Arity, init: true);
                            bldrNameToks = TokenTuple.CreateBuilder(Arity, init: true);
                        }
                        bldrNames[iarg] = identAdd != null ? identAdd.Name : new DName("_");
                        bldrNameToks[iarg] = identAdd?.Token;
                    }

                    if (Traits.IsScope(iarg, out int iscope, out int iidx, out _))
                    {
                        ArgScope indexScope = null;

                        // Scopes can't end a nesting run.
                        Validation.Assert(!Traits.IsNestedTail(iarg));

                        // The iscope should match.
                        Validation.Assert(iscope == bldrScopes.Count);

                        // Validate iidx.
                        Validation.Assert(iidx < 0 || bldrIndices != null && iidx <= bldrIndices.Count);

                        // Create a scope.
                        DType typeScope;
                        var kind = Traits.GetScopeKind(iarg);
                        Validation.Assert(iidx < 0 || kind == ScopeKind.SeqItem);
                        switch (kind)
                        {
                        case ScopeKind.SeqItem:
                            // Map scopes can't lift over sequence, it makes no sense.
                            Validation.Assert(!Traits.LiftsOverSeq(iarg));

                            // The scope is a map, so typeArg needs to be a sequence.
                            if (typeArg.SeqCount == 0)
                            {
                                // Since there is an error, don't do any lifting.
                                _maskLiftable = _maskLiftable.ClearBit(iarg);
                                typeArg = arg.Type.ToSequence();
                                if (iarg < ParseArity)
                                {
                                    arg = _vtor.CreateBndError(parg, typeArg,
                                        ErrorStrings.ErrNeedSequence_Slot_Func, iarg + 1, Oper.Name);
                                }
                                else
                                {
                                    // Already reported an error.
                                    Validation.Assert(ErrRoot != null);
                                    Validation.Assert(iarg >= ParseArity);
                                    Validation.Assert(arg.Type.IsVac);
                                    arg = _vtor.Associate(parg ?? ParseNode, BndCastVacNode.Create(arg, typeArg));
                                }
                            }
                            if (iidx >= 0)
                            {
                                if (iidx == bldrIndices.Count)
                                {
                                    indexScope = ArgScope.CreateIndex();
                                    bldrIndices.Add(indexScope);
                                }
                                else
                                    indexScope = bldrIndices[iidx];
                            }
                            typeScope = typeArg.ItemTypeOrThis;
                            break;

                        case ScopeKind.TenItem:
                            // Tensor scopes can't lift over tensor, it makes no sense.
                            Validation.Assert(!Traits.LiftsOverTen(iarg));

                            // The scope is a tensor item, so typeArg needs to be a tensor.
                            if (!typeArg.IsTensorXxx)
                            {
                                // Since there is an error, don't do any lifting.
                                _maskLiftable = _maskLiftable.ClearBit(iarg);
                                typeArg = arg.Type.ToTensor(false, 0);
                                if (iarg < ParseArity)
                                {
                                    arg = _vtor.CreateBndError(parg, typeArg,
                                        ErrorStrings.ErrNeedTensor_Slot_Func, iarg + 1, Oper.Name);
                                }
                                else
                                {
                                    // Already reported an error.
                                    Validation.Assert(ErrRoot != null);
                                    Validation.Assert(iarg >= ParseArity);
                                    Validation.Assert(arg.Type.IsVac);
                                    arg = _vtor.Associate(parg ?? ParseNode, BndCastVacNode.Create(arg, typeArg));
                                }
                            }
                            typeScope = typeArg.GetTensorItemType();
                            break;

                        case ScopeKind.Guard:
                            Validation.Assert(!Traits.LiftsOverOpt(iarg));
                            if (unGuard)
                            {
                                // There is a [with] attribute, so convert this to a "with" scope.
                                typeScope = typeArg;
                                kind = ScopeKind.With;
                            }
                            else if (typeArg.HasReq)
                            {
                                // Adjust the scope type.
                                Validation.Assert(!typeArg.IsSequence);
                                typeScope = typeArg.ToReq();
                            }
                            else if (!typeArg.IsOpt)
                            {
                                // No guard needed.
                                Validation.Assert(!typeArg.IsSequence);
                                typeScope = typeArg;
                                kind = ScopeKind.With;
                            }
                            else
                                typeScope = typeArg;
                            break;

                        case ScopeKind.Iter:
                        case ScopeKind.With:
                            typeScope = typeArg;
                            break;

                        case ScopeKind.Range:
                            Validation.Assert(typeArg == DType.I8Req);
                            typeScope = DType.I8Req;
                            break;

                        default:
                            Validation.Assert(false);
                            kind = ScopeKind.With;
                            typeScope = typeArg;
                            break;
                        }

                        scopeCur = ScopeWrapper.Create(scopeCur, kind, typeScope, indexScope, variable, impName);
                        bldrScopes.Add(scopeCur.Scope);
                    }

                    bldrArgs.Add(arg);
                }
                Validation.Assert(_vtor._scopeCur == _scopeBase);
                _scopes = bldrScopes != null ? bldrScopes.ToImmutable() : ScopeTuple.Empty;
                Validation.Assert(Scopes.Length == Traits.ScopeCount);
                _indices = bldrIndices != null ? bldrIndices.ToImmutable() : ScopeTuple.Empty;

                // Bind any extra/left-over args. Note that if Arity > 0, this was done in the last
                // call to BindArg, so is not needed.
                if (Arity == 0 && ParseArity > 0)
                {
                    Validation.Assert(ErrRoot != null);
                    for (int i = First == null ? 0 : 1; i < ParseArity; i++)
                    {
                        ParseArgs[i].Accept(_vtor);
                        _vtor.Pop();
                    }
                }

                _args = bldrArgs.ToImmutable();
                Validation.Assert(_args.Length == Arity);

                if (bldrDirs != null)
                    _dirs = bldrDirs.ToImmutable();
                if (bldrNames != null)
                {
                    _names = bldrNames.ToImmutable();
                    _nameTokens = bldrNameToks.ToImmutable();
                }

                AssertValid(true);

                Lift();
                AssertValid(true);
            }

            private void Lift()
            {
                if (_maskLiftable.IsEmpty)
                {
                    Visit3();
                    return;
                }

                GetLiftSlots(_args, out var slotsSeq, out var slotsTen, out var slotsOpt, Traits.MaskLiftNeedsSeq);
                slotsSeq &= Traits.MaskLiftSeq & _maskLiftable;
                slotsTen &= Traits.MaskLiftTen & _maskLiftable;
                slotsOpt &= Traits.MaskLiftOpt & _maskLiftable;

                BitSet slots;
                Action<ExprNode, ArgTuple, BitSet, Action<ExprNode, ArgTuple>> lifter;
                if (!(slots = slotsSeq).IsEmpty)
                    lifter = LiftOverSeq;
                else if (!(slots = slotsTen).IsEmpty)
                {
                    if (!(slotsOpt &= slotsTen).IsEmpty)
                    {
                        slots = slotsOpt;
                        lifter = _vtor.LiftOverOpt;
                    }
                    else
                        lifter = _vtor.LiftOverTen;
                }
                else if (!(slots = slotsOpt).IsEmpty)
                    lifter = _vtor.LiftOverOpt;
                else
                {
                    Visit3();
                    return;
                }

                lifter(ParseNode, _args, slots, (n, bs) =>
                {
                    Validation.Assert(n == ParseNode);
                    Validation.Assert(bs.Length == _args.Length);
                    _args = bs;
                    Lift();
                });
            }

            /// <summary>
            /// Lifts an operation over sequence via (repeated) Map/Zip on the slots indicated by
            /// <paramref name="slotsLift"/>. The slots that should end with a sequence count of
            /// one are indicated by this info's <see cref="ArgTraits.MaskLiftNeedsSeq"/>. Note
            /// that any slots in that mask that are not in <paramref name="slotsLift"/> are ignored.
            /// </summary>
            private void LiftOverSeq(ExprNode node, ArgTuple args, BitSet slotsLift,
                Action<ExprNode, ArgTuple> action)
            {
                _vtor.LiftOverSeq(node, args, slotsLift, action, Traits.MaskLiftNeedsSeq);
            }

            /// <summary>
            /// We're done with all the scoping and lifting logic, so now just cast the args and build
            /// the <see cref="BndCallNode"/>.
            /// </summary>
            private void Visit3()
            {
                AssertValid(true);

                if (Oper is UserFunc udf)
                {
                    _vtor.HandleUdf(ParseNode, udf, _args);
                    return;
                }

                // Resolve types.
                var (typeRet, argTypes, union) = Oper.SpecializeTypes(this, out _traitsChange);
                Validation.Assert(_traitsChange != _traits);
                // REVIEW: Should we just require the length to be correct?
                Validation.Assert(argTypes.Length >= Math.Min(1, _args.Length));

                ArgTuple.Builder bldrArgs = null;
                for (int iarg = 0; iarg < _args.Length; iarg++)
                {
                    var arg = _args[iarg];
                    var typeSrc = arg.Type;
                    var typeDst = argTypes[Math.Min(iarg, argTypes.Length - 1)];

                    if (typeSrc == typeDst)
                        continue;

                    if (Traits.IsScope(iarg))
                    {
                        // Generally, we can't cast a scope slot, since anything that depends on it needs
                        // to be rebound. At the outer-most level, we implement a rewind process for casting
                        // Iter scopes. This commonly happens with Fold/Scan/Generate.
                        if (!arg.HasErrors)
                        {
                            _vtor.Error(GetParseArgInner(iarg),
                                ErrorStrings.ErrBadTypeScope_Src_Dst, typeSrc, typeDst);
                            // REVIEW: Does this need to restrict to non-lifted slots?
                            if (Traits.GetScopeKind(iarg) == ScopeKind.Iter && !Traits.LiftsOverOpt(iarg) &&
                                !Traits.LiftsOverSeq(iarg) && !Traits.LiftsOverTen(iarg))
                            {
                                Util.Add(ref _scopeTypeChanges, iarg, typeDst);
                            }
                        }
                        continue;
                    }

                    _vtor.CheckGeneralType(GetParseArgInner(iarg), ref arg, typeDst, union);
                    (bldrArgs ??= _args.ToBuilder())[iarg] = arg;
                }
                if (bldrArgs != null)
                    _args = bldrArgs.ToImmutable();

                AssertValid(true);

                var call = BndCallNode.Create(Oper, typeRet, _args, _scopes, _indices, _dirs, _names, Traits);

                switch (call.Kind)
                {
                case BndNodeKind.CallVolatile:
                    if ((_vtor._options & BindOptions.AllowVolatile) == 0)
                        _vtor.Error(ParseNode, ErrorStrings.ErrBadVolatileCall);
                    break;
                case BndNodeKind.CallProcedure:
                    // If this is an error it was reported when the particular procedure was found.
                    break;
                default:
                    Validation.Assert(call.Kind == BndNodeKind.Call);
                    break;
                }

                // Test for whether volatile is allowed slot-by-slot. If volatile isn't allowed at all,
                // don't bother since we already reported errors on any volatile calls.
                if (call.HasVolatile && call.Oper.HasBadVolatile(call, out int slot) &&
                    (_vtor._options & BindOptions.AllowVolatile) != 0)
                {
                    _vtor.Error(GetParseArgInner(slot), ErrorStrings.ErrBadVolatileArg_Slot, slot + 1);
                }

                _vtor.Push(ParseNode, call);
            }
        }

        /// <summary>
        /// Bind an invocation of a UDF. This binds the body of the UDF using separate binder and host instances.
        /// The special host does not expose "globals", but does expose UDFs. The special host stores the set of
        /// UDFs currently being bound so we can detect recursion, which is illegal.
        /// </summary>
        private void HandleUdf(RexlNode node, UserFunc udf, ArgTuple args)
        {
            // Get the set of UDFs currently being bound and test for recursion.
            // REVIEW: Should we also limit the call depth to avoid stack overflow?
            var udfs = NameToArities.Empty;
            if (_host is UdfHost uh)
            {
                udfs = uh.UdfSet;
                if (udfs.Contains(udf.Path, udf.Arity))
                {
                    // Recursion. Resolve the whole thing to an error.
                    PushError(node, null, ErrorStrings.ErrRecursiveUdf);
                    return;
                }
            }

            // Create the special host, with the current udf added to the set.
            var host = new UdfHost(_host, udf, udfs.Add(udf.Path, udf.Arity));

            // Create scopes for the parameters.
            // If there are duplicates among the param names, the latter ones will win. In such cases,
            // the parser should have produced an error, so we don't need to do anything special here.
            ScopeTuple scopes;
            Immutable.Array<(ArgScope scope, string name)> scopeMap;
            if (args.Length > 0)
            {
                var bldr = ScopeTuple.CreateBuilder(args.Length, init: true);
                var bldrMap = Immutable.Array<(ArgScope scope, string name)>.CreateBuilder(
                    args.Length, init: true);
                for (int i = 0; i < bldr.Count; i++)
                {
                    var scope = ArgScope.Create(ScopeKind.With, args[i].Type);
                    bldr[i] = scope;
                    bldrMap[i] = (scope, udf.ParamNames[i].Value);
                }
                scopes = bldr.ToImmutable();
                scopeMap = bldrMap.ToImmutable();
            }
            else
            {
                scopes = ScopeTuple.Empty;
                scopeMap = Immutable.Array<(ArgScope scope, string name)>.Empty;
            }

            // Bind the UDF body, so the UDF gets expanded "inline". Reduction and optimization will
            // happen all at once at the end, if requested, so don't do it here.
            BindOptions options = BindOptions.DontReduce | BindOptions.DontOptimize;
            if ((_options & BindOptions.AllowVolatile) != 0)
                options |= BindOptions.AllowVolatile;
            if ((_options & BindOptions.ProhibitModule) != 0)
                options |= BindOptions.ProhibitModule;
            var bfma = BoundFormula.Create(udf.Formula, host, options, default, scopeMap);

            // Copy over any diagnostics. Note that the diagnostics contain their associated source context,
            // so this should be "safe". In particular, client code should not assume that diagnostics point
            // into the same source context.
            bfma.GetDiagnostics(ref _diagnostics);

            // Wrap in an invocation of With if needed.
            var bnd = bfma.BoundTree;
            if (scopes.Length > 0)
                bnd = BndCallNode.Create(WithFunc.With, bnd.Type, args.Add(bnd), scopes);
            Push(node, bnd);
        }

        /// <summary>
        /// The <see cref="BindHost"/> to use when binding a UDF. It hides all globals.
        /// It also contains the set of udfs being bound, used to detect recursion.
        /// </summary>
        private sealed class UdfHost : MinBindHost
        {
            private readonly BindHost _host;
            private readonly UserFunc _udf;

            /// <summary>
            /// The set uf UDFs currently being bound. This is tracked so we can detect recursion (and
            /// not get a stack overflow exception).
            /// </summary>
            public readonly NameToArities UdfSet;

            public UdfHost(BindHost host, UserFunc udf, NameToArities udfs)
                : base(udf.VerifyValue().Namespace, udf.RootNamespace)
            {
                Validation.AssertValue(host);
                Validation.AssertValue(udf);
                Validation.AssertValue(udfs);
                Validation.Assert(udfs.ContainsKey(udf.Path));

                _host = host;
                _udf = udf;
                UdfSet = udfs;
            }

            public override bool IsFuzzyMatch(string a, string b)
            {
                // Don't fuzzy match fields.
                return false;
            }

            public override bool TryGetOperInfoOne(NPath name, bool user, bool fuzzy, int arity, out OperInfo info)
            {
                return _host.TryGetOperInfoOne(name, user, fuzzy, arity, out info);
            }
        }

        /// <summary>
        /// Persistent dictionary from <see cref="NPath"/> to the set of arities used for that path,
        /// represented as a <see cref="BitSet"/>.
        /// </summary>
        private sealed class NameToArities : NPathRedBlackTree<NameToArities, BitSet>
        {
            public static readonly NameToArities Empty = new NameToArities(null);

            private NameToArities(Node root)
                : base(root)
            {
            }

            protected override NameToArities Wrap(Node root)
            {
                return root == _root ? this : root != null ? new NameToArities(root) : Empty;
            }

            protected override bool KeyIsValid(NPath key) => !key.IsRoot;
            protected override bool ValIsValid(BitSet val) => !val.IsEmpty;
            protected override bool ValEquals(BitSet val0, BitSet val1) => val0 == val1;
            protected override int ValHash(BitSet val) => val.GetHashCode();

            /// <summary>
            /// Returns whether this contains the given <paramref name="path"/> and the
            /// given <paramref name="arity"/> within it.
            /// </summary>
            public bool Contains(NPath path, int arity)
            {
                Validation.Assert(!path.IsRoot);
                Validation.Assert(arity >= 0);
                return TryGetValue(path, out var bits) && bits.TestBit(arity);
            }

            /// <summary>
            /// Adds the given <paramref name="path"/> and <paramref name="arity"/>. Doesn't care
            /// if it is already there.
            /// </summary>
            public NameToArities Add(NPath path, int arity)
            {
                Validation.Assert(!path.IsRoot);
                Validation.Assert(arity >= 0);
                TryGetValue(path, out var bits);
                return SetItem(path, bits.SetBit(arity));
            }
        }

        /// <summary>
        /// This handles binding <see cref="DottedNameNode"/> on non-record types, where there is a
        /// "property function" for the lhs of the correct type, for example '"Hello".Len'. This also
        /// handles property access of a scope value, for example, 'With("Hello", Len)'.
        /// This implements <see cref="InvocationInfo"/> defined by the rexl function code.
        /// </summary>
        private sealed class PropInvokeInfo : InvocationInfo
        {
            #region State set by the ctor

            private readonly Binder _vtor;

            public override ExprNode ParseNode { get; }
            public override NodeTuple ParseArgs { get; }
            /// <summary>
            /// Info should not be null.
            /// </summary>
            public override OperInfo Info { get; }
            public override RexlOper Oper => Func;
            public override ArgTraits Traits { get; }

            public RexlOper Func { get; }

            #endregion State set by the ctor

            #region State filled in while binding.

            private ArgTuple _args;

            public override ArgTuple Args => _args;
            public override ScopeTuple Scopes => ScopeTuple.Empty;
            public override ScopeTuple Indices => ScopeTuple.Empty;
            public override NameTuple Names => default;
            public override TokenTuple NameTokens => default;
            public override DirTuple Dirs => default;

            #endregion State filled in while binding.

            public static OperInfo GetPropFunc(Binder vtor, DName name, DType typeSrc, bool guess)
            {
                Validation.AssertValue(vtor);
                Validation.Assert(name.IsValid);
                Validation.Assert(typeSrc.IsValid);

                var nsType = BindUtil.GetFuncNSForType(typeSrc);
                if (nsType.IsRoot)
                    return null;

                var path = NPath.Root.Append(name);
                if (!vtor._host.TryGetOperInfo(path, isRooted: false, nsType, guess, 1, out var info))
                    return null;
                if (info.Path.Parent != nsType)
                    return null;

                var oper = info.Oper;
                if (!oper.IsFunc)
                    return null;

                if (oper.ArityMin != 1 || oper.ArityMax != 1)
                    return null;
                if (!oper.IsProperty(typeSrc.RootType.ToReq()))
                    return null;

                Validation.Assert(!guess || info.Path != path);
                return info;
            }

            public static bool TryRun(Binder vtor, ExprNode node, Identifier ident, DType typeSrc, BoundNode arg)
            {
                Validation.Assert(typeSrc.SeqCount == 0);
                Validation.Assert(!typeSrc.HasReq);

                DName name = ident.Name;
                OperInfo info;
                if ((info = GetPropFunc(vtor, name, typeSrc, guess: false)) != null)
                    Validation.Assert(info.Path.Leaf == name);
                else if ((info = GetPropFunc(vtor, name, typeSrc, guess: true)) != null)
                {
                    Validation.Assert(info.Path.Leaf != name);
                    string strGuess = info.Path.Leaf.Escape();
                    Validation.Coverage(strGuess.Contains('\'') ? 1 : 0);
                    vtor.ErrorGuess(node, ErrorStrings.ErrUnknownProp_Guess, strGuess, ident.Range, info.Path.Leaf);
                }
                else
                    return false;

                var pii = new PropInvokeInfo(vtor, node, arg, info);
                pii.Resolve();
                return true;
            }

            private PropInvokeInfo(Binder vtor, ExprNode node, BoundNode arg, OperInfo info)
                : base()
            {
                Validation.AssertValue(vtor);
                Validation.AssertValue(node);
                Validation.AssertValue(arg);
                Validation.AssertValue(info);

                Info = info;
                Func = info.Oper;
                Validation.AssertValue(Func);
                Validation.Assert(Func.IsFunc);
                Validation.Assert(Func.SupportsArity(1));

                _vtor = vtor;
                ParseNode = node;

                var nodeArg = node is DottedNameNode dnn ? dnn.Left : node;
                ParseArgs = NodeTuple.Create(nodeArg);

                _args = ArgTuple.Create(arg);

                if (info.Deprecated)
                {
                    if (info.PathAlt.IsRoot)
                        vtor.Warning(node, ErrorStrings.WrnDeprecatedFunction);
                    else
                        vtor.Warning(node, ErrorStrings.WrnDeprecatedFunction_Alt, info.PathAlt);
                }

                Traits = Func.GetArgTraits(1, default, default, default);
                Validation.Assert(Traits.SlotCount == 1);
                Validation.Assert(Traits.Oper == Func);
                Validation.Assert(Traits.ScopeCount == 0);

                AssertValid();
            }

            [Conditional("DEBUG")]
            private void AssertValid()
            {
#if DEBUG
                Validation.Assert(_vtor != null);
                Validation.Assert(ParseNode != null);
                Validation.Assert(ParseArgs.Length == 1);
                Validation.Assert(Traits != null);
                Validation.Assert(Func.SupportsArity(Traits.SlotCount));
                Validation.Assert(Args.Length == 1);
#endif
            }

            public override void PostDiagnostic(BaseDiagnostic diag)
            {
                Validation.BugCheckValue(diag, nameof(diag));
                _vtor.AddDiagnostic(diag);
            }

            private void Resolve()
            {
                AssertValid();

                if (Func is UserFunc udf)
                {
                    _vtor.HandleUdf(ParseNode, udf, _args);
                    return;
                }

                // Resolve types.
                var (typeRet, argTypes, union) = Func.SpecializeTypes(this, out _);
                Validation.Assert(argTypes.Length == 1);

                var arg = _args[0];
                var typeSrc = arg.Type;
                var typeDst = argTypes[0];
                if (typeSrc != typeDst)
                {
                    _vtor.CheckGeneralType(GetParseArg(0), ref arg, typeDst, union);
                    _args = ArgTuple.Create(arg);
                }

                AssertValid();

                var call = BndCallNode.Create(Func, typeRet, _args, Scopes, Traits);
                _vtor.Push(ParseNode, call);
            }
        }
    }
}
