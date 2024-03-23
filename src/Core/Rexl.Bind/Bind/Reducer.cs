// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Utility;

namespace Microsoft.Rexl.Bind;

using ArgTuple = Immutable.Array<BoundNode>;
using Integer = System.Numerics.BigInteger;
using NullTo = BndCompareNode.NullTo;
using ScopeFieldMap = ReadOnly.Dictionary<DName, ArgScope>;
using ScopeMap = ReadOnly.Dictionary<ArgScope, ArgScope>;
using ScopeSlotMap = ReadOnly.Dictionary<int, ArgScope>;
using ScpTuple = Immutable.Array<ArgScope>;

/// <summary>
/// Indicates whether "with"/"guard" can be pulled from an argument.
/// </summary>
[Flags]
public enum PullWithFlags
{
    None = 0x00,
    With = 0x01,
    Guard = 0x02,

    Both = With | Guard,
}

/// <summary>
/// Interface for a reducer, passed to <see cref="RexlOper.Reduce(IReducer, BndCallNode)"/>.
/// </summary>
public interface IReducer
{
    /// <summary>
    /// Reduce the given node and return the result.
    /// </summary>
    BoundNode Reduce(BoundNode bnd);

    /// <summary>
    /// Return the <paramref name="src"/> converted to the given <paramref name="typeDst"/>.
    /// This checks that <paramref name="typeDst"/> accepts the type of <paramref name="src"/>.
    /// </summary>
    BoundNode Convert(BoundNode src, DType typeDst, bool union);

    /// <summary>
    /// Replace occurrences in <paramref name="bnd"/> of <paramref name="src"/> with <paramref name="dst"/>
    /// and return the result. If there are no such occurrences, <paramref name="bnd"/> is returned.
    /// </summary>
    BoundNode MapScope(BoundNode bnd, ArgScope src, ArgScope dst);

    /// <summary>
    /// Replace occurrences in <paramref name="bnd"/> of keys in <paramref name="scopeMap"/> with their
    /// associated values and return the result. If there are no such occurrences, <paramref name="bnd"/>
    /// is returned.
    /// </summary>
    BoundNode MapScopes(BoundNode bnd, ScopeMap scopeMap);

    /// <summary>
    /// Replace occurrences in <paramref name="bnd"/> of fields of <paramref name="src"/> that are in
    /// <paramref name="scopeFieldMap"/> with their associated new scope and return the result.
    /// If there are no such occurrences, <paramref name="bnd"/> is returned.
    /// </summary>
    BoundNode MapScopeFields(BoundNode bnd, ArgScope src, ScopeFieldMap mapFld, ScopeSlotMap mapSlot);

    /// <summary>
    /// Replace occurrences in <paramref name="bnd"/> of <paramref name="src"/> with the node
    /// <paramref name="dst"/>. Note that this is only safe to do if either <paramref name="bnd"/>
    /// contains at most one occurrence or <paramref name="dst"/> is a constant. This returns
    /// the result as well as the number of substitutions, so this can be verified by calling code.
    /// </summary>
    (BoundNode res, int count) ReplaceScope(BoundNode bnd, ArgScope src, BoundNode dst);

    /// <summary>
    /// Invoked to report a warning situation (appropriate for an end user).
    /// </summary>
    void Warn(BoundNode bnd, StringId msg);
}

/// <summary>
/// The visitor. This is separate from <see cref="Reducer"/> so the visit methods aren't visible
/// to clients of the <see cref="Reducer"/>.
/// </summary>
public abstract class ReducerVisitor : ReducerBase
{
    protected interface IRetire
    {
        void Retire();
    }

    /// <summary>
    /// This implements the public <see cref="IReducer"/> interface which simply delegates to
    /// the visitor. This is a separate class so receivers can't get access to the full public
    /// API of the visitor.
    /// </summary>
    protected abstract class Interface<TVtor> : IReducer, IRetire
        where TVtor : ReducerVisitor
    {
        protected TVtor _vtor { get; private set; }

        protected Interface(TVtor vtor)
        {
            Validation.AssertValue(vtor);
            _vtor = vtor;
        }

        /// <summary>
        /// Retire this reducer wrapper.
        /// </summary>
        public void Retire()
        {
            _vtor = null;
        }

        /// <summary>
        /// Implementation of <see cref="IReducer.Reduce(BoundNode)"/>.
        /// </summary>
        public virtual BoundNode Reduce(BoundNode bnd)
        {
            Validation.BugCheck(_vtor != null);
            return _vtor.ReducePub(bnd);
        }

        public virtual BoundNode Convert(BoundNode src, DType typeDst, bool union)
        {
            Validation.BugCheck(_vtor != null);
            return _vtor.Convert(src, typeDst, union);
        }

        /// <summary>
        /// Implementation of <see cref="IReducer.MapScope(BoundNode, ArgScope, ArgScope)"/>.
        /// </summary>
        public virtual BoundNode MapScope(BoundNode bnd, ArgScope src, ArgScope dst)
        {
            Validation.BugCheck(_vtor != null);
            return _vtor.MapScope(bnd, src, dst);
        }

        /// <summary>
        /// Implementation of <see cref="IReducer.MapScopes(BoundNode, ScopeMap)"/>.
        /// </summary>
        public virtual BoundNode MapScopes(BoundNode bnd, ScopeMap scopeMap)
        {
            Validation.BugCheck(_vtor != null);
            return _vtor.MapScopes(bnd, scopeMap);
        }

        public virtual BoundNode MapScopeFields(BoundNode bnd, ArgScope src, ScopeFieldMap mapFld, ScopeSlotMap mapSlot)
        {
            Validation.BugCheck(_vtor != null);
            return _vtor.MapScopeFields(bnd, src, mapFld, mapSlot);
        }

        /// <summary>
        /// Implementation of <see cref="IReducer.ReplaceScope(BoundNode, ArgScope, BoundNode)"/>.
        /// </summary>
        public virtual (BoundNode res, int count) ReplaceScope(BoundNode bnd, ArgScope src, BoundNode dst)
        {
            Validation.BugCheck(_vtor != null);
            return _vtor.ReplaceScope(bnd, src, dst);
        }

        /// <summary>
        /// Implementation of <see cref="IReducer.Warn(BoundNode, StringId)"/>.
        /// </summary>
        public virtual void Warn(BoundNode bnd, StringId msg)
        {
            Validation.BugCheck(_vtor != null);
            _vtor.Warn(bnd, msg);
        }
    }

    private sealed class InterfaceDefault : Interface<ReducerVisitor>
    {
        public InterfaceDefault(ReducerVisitor vtor)
            : base(vtor)
        {
        }
    }

    /// <summary>
    /// The wrapper implementation of <see cref="IReducer"/>.
    /// </summary>
    private IReducer _intf;

    // This is used for substitution of "with" scopes when the corresponding value is "cheap".
    private readonly Dictionary<ArgScope, (BoundNode, RexlOper)> _scopeToValue;

    protected ReducerVisitor(IReducerHost host, bool memoize, Func<ReducerVisitor, IReducer> creator = null)
        : base(host, memoize)
    {
        _scopeToValue = new();
        _intf = creator != null ? creator(this) : new InterfaceDefault(this);
    }

    protected virtual void Retire()
    {
        if (_intf is IRetire ret)
            ret.Retire();
        _intf = null;
    }

    protected IReducer ReducerIface => _intf;

    #region IReducer API

    public BoundNode ReducePub(BoundNode bnd)
    {
        Validation.BugCheckValue(bnd, nameof(bnd));
        int cur = 0;
        return Reduce(bnd, ref cur);
    }

    public BoundNode Convert(BoundNode src, DType typeDst, bool union)
    {
        int cur = 0;
        return Reduce(Conversion.CastBnd(_host, src, typeDst, union), ref cur);
    }

    public BoundNode MapScope(BoundNode bnd, ArgScope src, ArgScope dst)
    {
        int cur = 0;
        return Reduce(ScopeMapper.Run(_host, bnd, src, dst), ref cur);
    }

    public BoundNode MapScopes(BoundNode bnd, ScopeMap scopeMap)
    {
        int cur = 0;
        return Reduce(ScopeMapper.Run(_host, bnd, scopeMap), ref cur);
    }

    public BoundNode MapScopeFields(BoundNode bnd, ArgScope src, ScopeFieldMap mapFld, ScopeSlotMap mapSlot)
    {
        int cur = 0;
        return Reduce(ScopeItemMapper.Run(_host, bnd, src, mapFld, mapSlot), ref cur);
    }

    public (BoundNode res, int count) ReplaceScope(BoundNode bnd, ArgScope src, BoundNode dst)
    {
        var (res, count) = ScopeReplacer.Run(_host, bnd, src, dst);
        int cur = 0;
        return (Reduce(res, ref cur), count);
    }

    public void Warn(BoundNode bnd, StringId msg)
    {
        _host.Warn(bnd, msg);
    }

    #endregion IReducer API

    /// <summary>
    /// Returns <c>true</c> if <paramref name="val"/> references any of the scopes in
    /// <paramref name="scopes"/>.
    /// </summary>
    private static bool UsesScopes(BoundNode val, ScpTuple scopes)
    {
        for (int i = 0; i < scopes.Length; i++)
        {
            var scope = scopes[i];
            Validation.AssertValue(scope);
            if (ScopeCounter.Any(val, scope))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns <c>true</c> if <paramref name="val"/> references any of the scopes in
    /// <paramref name="scopes"/> whose corresponding bit is set in <paramref name="left"/>.
    /// </summary>
    private static bool UsesScopes(BoundNode val, ScpTuple scopes, BitSet left)
    {
        foreach (int i in left)
        {
            Validation.AssertIndex(i, scopes.Length);
            // Caller shouldn't set a bit for a null scope.
            Validation.Assert(scopes[i] != null);
            if (ScopeCounter.Any(val, scopes[i]))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Information for pulling with/guard constructs out of arguments.
    /// </summary>
    protected struct WithInfo
    {
        private readonly ReducerVisitor _vtor;

        /// <summary>
        /// This is invocation that is being reduced, when there is one (may be null).
        /// </summary>
        private readonly BndCallNode _ctx;

        /// <summary>
        /// Whether the <see cref="_ctx"/> declares non-loop scopes.
        /// </summary>
        private readonly bool _ctxHasNonLoopScopes;

        /// <summary>
        /// When <see cref="_ctx"/> is non-null, this is its <see cref="BndCallNode.Oper"/> value.
        /// </summary>
        private readonly RexlOper _func;

        /// <summary>
        /// Set to true if with/guard values should <i>not</i> be pulled.
        /// </summary>
        private readonly bool _disabled;

        /// <summary>
        /// This holds the arguments for a new (wrapping) invocation of with/guard. Allocated lazily so
        /// may be null.
        /// </summary>
        private ArgTuple.Builder _args;

        /// <summary>
        /// This holds the scopes for a new (wrapping) invocation of with/guard. Allocated lazily so
        /// may be null.
        /// </summary>
        private ScpTuple.Builder _scopes;

        /// <summary>
        /// Whether any guard scopes have been placed in <see cref="_scopes"/>.
        /// </summary>
        private bool _guard;

        /// <summary>
        /// Constructor for a non-call scenario, eg, an operator.
        /// </summary>
        public WithInfo(ReducerVisitor vtor)
            : this()
        {
            Validation.AssertValue(vtor);
            _vtor = vtor;
        }

        /// <summary>
        /// Constructor to use when pulling may be disabled. For example, a comparison chain disables pulling
        /// when there are more than two operands.
        /// </summary>
        public WithInfo(ReducerVisitor vtor, bool disabled)
            : this(vtor)
        {
            Validation.Assert(_vtor == vtor);
            _disabled = disabled;
        }

        /// <summary>
        /// Constructor for a function/procedure call.
        /// </summary>
        public WithInfo(ReducerVisitor vtor, BndCallNode ctx)
            : this(vtor)
        {
            Validation.Assert(_vtor == vtor);
            Validation.AssertValue(ctx);
            _ctx = ctx;

            // Determine whether the call has any non-loop scopes.
            for (int i = 0; i < ctx.Scopes.Length; i++)
            {
                if (!ctx.Scopes[i].Kind.IsLoopScope())
                {
                    _ctxHasNonLoopScopes = true;
                    break;
                }
            }

            _func = _ctx.Oper;
            _disabled = _func == null;
        }

        /// <summary>
        /// Whether any guard values have been pulled.
        /// </summary>
        public bool HasGuard => _guard;

        /// <summary>
        /// Whether any values have been pulled.
        /// </summary>
        public bool HasAny => _args != null;

        /// <summary>
        /// The given value/scope is being pulled. Record that fact.
        /// </summary>
        private void Add(BoundNode val, ArgScope scope)
        {
            Validation.AssertValue(_vtor);
            Validation.AssertValue(val);
            Validation.AssertValue(scope);
            Validation.Assert(scope.Kind == ScopeKind.With | scope.Kind == ScopeKind.Guard);

            (_args ??= ArgTuple.CreateBuilder()).Add(val);
            (_scopes ??= ScpTuple.CreateBuilder()).Add(scope);
            _guard |= scope.Kind == ScopeKind.Guard;
        }

        /// <summary>
        /// Wrap <paramref name="bnd"/> as the selector in an invocation of With/Guard containing the pulled
        /// scopes and values. If <see cref="HasAny"/> is <c>false</c>, this just returns <paramref name="bnd"/>.
        /// When this returns, this <see cref="WithInfo"/> is blanked out (unusable).
        /// </summary>
        public BoundNode Apply(BoundNode bnd)
        {
            Validation.AssertValue(_vtor);

            if (_args == null)
            {
                Validation.Assert(_scopes == null);
                Validation.Assert(!_guard);
                this = default;
                return bnd;
            }

            Validation.AssertValue(_scopes);
            Validation.Assert(_args.Count > 0);
            Validation.Assert(_args.Count == _scopes.Count);

            _args.Add(bnd);
            bnd = BndCallNode.Create(_guard ? WithFunc.Guard : WithFunc.With, bnd.Type,
                _args.ToImmutable(), _scopes.ToImmutable());
            var vtor = _vtor;
            this = default;

            int tmp = 0;
            return vtor.Reduce(bnd, ref tmp);
        }

        /// <summary>
        /// Process the given <paramref name="args"/>, in a non-call case. Processing includes reducing
        /// as well as pulling with/guard (when appropriate). If any of the args change, returns a non-null
        /// builder containing the complete args to use (some may be unmodified and others modified).
        /// </summary>
        public ArgTuple.Builder Process(ArgTuple args, ref int cur)
        {
            Validation.AssertValue(_vtor);
            Validation.Assert(_ctx == null);
            Validation.Assert(!args.IsDefault);

            ArgTuple.Builder bldr = null;
            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                var res = Process(arg, ref cur);
                if (res != arg)
                {
                    bldr ??= args.ToBuilder();
                    bldr[i] = res;
                }
            }

            return bldr;
        }

        /// <summary>
        /// Process the given <paramref name="arg"/>, in a non-call case. Processing includes reducing
        /// as well as pulling with/guard (when appropriate). If <paramref name="scopeBad"/> is referenced
        /// by a with/guard value, pulling for that value is inhibited.
        /// </summary>
        public BoundNode Process(BoundNode arg, ref int cur, ArgScope scopeBad = null)
        {
            Validation.AssertValue(_vtor);
            Validation.Assert(_ctx == null);
            Validation.AssertValue(arg);

            // scopeBad shouldn't be a loop scope. This should not be called if arg is nested in a
            // looping scope.
            Validation.Assert(scopeBad == null || !scopeBad.Kind.IsLoopScope());

            var res = Pre(arg, ref cur);

            if (!_disabled && res is BndCallNode call && call.Oper is WithFunc)
                res = Core(call, PullWithFlags.With, default, scopeBad);

            return Post(arg, res);
        }

        /// <summary>
        /// Process the given <paramref name="arg"/>, in a call case. <paramref name="iarg"/> is the index of the
        /// arg in the call. Processing includes reducing as well as pulling with/guard (when appropriate).
        /// </summary>
        public BoundNode Process(BoundNode arg, ref int cur, int iarg)
        {
            Validation.AssertValue(_vtor);
            Validation.Assert(_ctx != null);
            Validation.AssertValue(arg);
            Validation.AssertIndex(iarg, _ctx.Args.Length);

            var res = Pre(arg, ref cur);

            // Pulling is disabled for procedure calls.
            Validation.Assert(_func != null | _disabled);
            PullWithFlags flags;
            if (!_disabled && res is BndCallNode call && call.Oper is WithFunc &&
                ((flags = _func.GetPullWithFlags(_ctx, iarg)) & PullWithFlags.With) != 0)
            {
                // We don't need to test for use of any looping scopes (including index scopes) since
                // the flags should be zero for repeated args.
                Validation.Assert(!_ctx.Traits.IsRepeated(iarg));
                res = Core(call, flags, _ctxHasNonLoopScopes ? _ctx.Scopes : default);
            }

            return Post(arg, res);
        }

        /// <summary>
        /// This is the first step of processing - to reduce the <paramref name="arg"/>.
        /// </summary>
        private BoundNode Pre(BoundNode arg, ref int cur)
        {
            cur = arg.Accept(_vtor, cur);
            var res = _vtor.Pop();
            Validation.AssertValue(res);
            Validation.Assert(res.Type == arg.Type);
            return res;
        }

        /// <summary>
        /// This is the final step of processing - to validate the final value, <paramref name="res"/>.
        /// </summary>
        private BoundNode Post(BoundNode arg, BoundNode res)
        {
            Validation.Assert(res.Type == arg.Type);
            if (res == arg)
                return arg;
            Validation.Assert(!res.Equivalent(arg));
            return res;
        }

        /// <summary>
        /// This is the pulling portion of "process". Pulling is inhibited if a value references any of
        /// the "bad" scopes specified.
        /// </summary>
        private BoundNode Core(BndCallNode call, PullWithFlags flags,
            ScpTuple scopesBad, ArgScope scopeBad = null)
        {
            Validation.Assert(!_disabled);
            Validation.Assert(call.Oper is WithFunc);
            Validation.Assert((flags & PullWithFlags.With) != 0);

            // Move With/Guard "out", when possible.
            bool doGuard = (flags & PullWithFlags.Guard) != 0;
            var ss = call.Scopes;
            BitSet left = default;

            // Pulled values can come from multiple operands. Different operands may declare/use the same
            // ArgScope instance. To avoid improper nesting of ArgScope declarations, we map a pulled scope
            // to a new ArgScope instance.
            // REVIEW: Is there a better (more efficient) way to deal with this issue?
            Dictionary<ArgScope, ArgScope> map = null;
            ArgTuple.Builder argsMapped = null;

            for (int j = 0; j < ss.Length; j++)
            {
                var val = call.Args[j];
                var scp = call.Scopes[j];
                Validation.Assert(map == null || !map.ContainsKey(scp));
                if (map != null)
                    val = ScopeMapper.Run(_vtor._host, val, map);

                if (scp.Kind == ScopeKind.Guard && !doGuard)
                {
                    left = left.SetBit(j);
                    if (val != call.Args[j])
                        (argsMapped ??= call.Args.ToBuilder())[j] = val;
                    continue;
                }

                if ((val.AllKinds & (BndNodeKindMask.ArgScopeRef | BndNodeKindMask.IndScopeRef)) != 0)
                {
                    if (scopeBad != null && ScopeCounter.Any(val, scopeBad) ||
                        !scopesBad.IsDefaultOrEmpty && UsesScopes(val, scopesBad))
                    {
                        left = left.SetBit(j);
                        if (val != call.Args[j])
                            (argsMapped ??= call.Args.ToBuilder())[j] = val;
                        continue;
                    }
                    if (!left.IsEmpty && UsesScopes(val, ss, left))
                    {
                        left = left.SetBit(j);
                        if (val != call.Args[j])
                            (argsMapped ??= call.Args.ToBuilder())[j] = val;
                        continue;
                    }
                }

                var scpNew = ArgScope.Create(scp.Kind, scp.Type);
                (map ??= new()).Add(scp, scpNew);
                Add(val, scpNew);
            }

            BoundNode res = call;
            if (left != BitSet.GetMask(ss.Length))
            {
                // Pulled some out.
                Validation.Assert(map != null);
                res = call.Args[ss.Length];
                res = ScopeMapper.Run(_vtor._host, res, map);

                if (!left.IsEmpty)
                {
                    // Didn't pull out everything.
                    int num = left.Count;
                    var argsRem = ArgTuple.CreateBuilder(num + 1, init: true);
                    var scpsRem = ScpTuple.CreateBuilder(num, init: true);
                    int k = 0;
                    bool anyGuard = false;
                    for (int j = 0; j < ss.Length; j++)
                    {
                        if (left.TestBit(j))
                        {
                            Validation.Assert(k < num);
                            anyGuard |= call.Scopes[j].Kind == ScopeKind.Guard;
                            argsRem[k] = argsMapped != null ? argsMapped[j] : call.Args[j];
                            scpsRem[k] = call.Scopes[j];
                            k++;
                        }
                    }
                    Validation.Assert(k == num);
                    argsRem[num] = res;
                    res = BndCallNode.Create(anyGuard ? WithFunc.Guard : WithFunc.With, call.Type,
                        argsRem.ToImmutable(), scpsRem.ToImmutable());
                }
            }

            return res;
        }
    }

    protected override bool PreVisitImpl(BndBinaryOpNode bnd, int idx)
    {
        Validation.AssertValue(bnd);

        int cur = idx + 1;
        var with = new WithInfo(this);
        var arg0 = with.Process(bnd.Arg0, ref cur);
        var arg1 = bnd.Arg1;

        switch (bnd.Op)
        {
        case BinaryOp.Coalesce:
            // Not strict in the 2nd arg.
            ReduceChild(ref arg1, ref cur);
            break;
        default:
            arg1 = with.Process(arg1, ref cur);
            break;
        }
        Validation.Assert(cur == idx + bnd.NodeCount);

        BoundNode result;
        switch (bnd.Op)
        {
        case BinaryOp.IntDiv:
        case BinaryOp.IntMod:
            Validation.Coverage(arg0 == bnd.Arg0 ? 0 : 1);
            Validation.Coverage(arg1 == bnd.Arg1 ? 0 : 1);
            result = ReduceDivModInt(bnd, arg0, arg1);
            break;

        case BinaryOp.Shl:
        case BinaryOp.Shri:
        case BinaryOp.Shru:
            Validation.Coverage(arg0 == bnd.Arg0 ? 0 : 1);
            Validation.Coverage(arg1 == bnd.Arg1 ? 0 : 1);
            result = ReduceShift(bnd, arg0, arg1);
            break;

        case BinaryOp.Power:
            Validation.Coverage(arg0 == bnd.Arg0 ? 0 : 1);
            Validation.Coverage(arg1 == bnd.Arg1 ? 0 : 1);
            result = ReducePower(bnd, arg0, arg1);
            break;

        case BinaryOp.Min:
        case BinaryOp.Max:
            Validation.Coverage(arg0 == bnd.Arg0 ? 0 : 1);
            Validation.Coverage(arg1 == bnd.Arg1 ? 0 : 1);
            result = ReduceMinMax(bnd, arg0, arg1);
            break;

        case BinaryOp.Coalesce:
            Validation.Coverage(arg0 == bnd.Arg0 ? 0 : 1);
            Validation.Coverage(arg1 == bnd.Arg1 ? 0 : 1);
            result = ReduceCoalesce(bnd, arg0, arg1);
            break;

        case BinaryOp.Has:
        case BinaryOp.HasNot:
        case BinaryOp.HasCi:
        case BinaryOp.HasCiNot:
            Validation.Coverage(arg0 == bnd.Arg0 ? 0 : 1);
            Validation.Coverage(arg1 == bnd.Arg1 ? 0 : 1);
            result = ReduceHas(bnd, arg0, arg1);
            break;

        default:
            Validation.Coverage(arg0 == bnd.Arg0 ? 0 : 1);
            Validation.Coverage(arg1 == bnd.Arg1 ? 0 : 1);
            result = bnd;
            if (arg0 != bnd.Arg0 || arg1 != bnd.Arg1)
                result = BndBinaryOpNode.Create(bnd.Type, bnd.Op, arg0, arg1);
            break;
        }

        Validation.Coverage(with.HasAny ? 0 : 1);
        Push(bnd, with.Apply(result));
        return false;
    }

    protected override bool PreVisitImpl(BndVariadicOpNode bnd, int idx)
    {
        Validation.AssertValue(bnd);

        bool canPull = true;
        switch (bnd.Op)
        {
        case BinaryOp.And:
        case BinaryOp.Or:
            canPull = false;
            break;

        case BinaryOp.Xor:
        case BinaryOp.BitOr:
        case BinaryOp.BitXor:
        case BinaryOp.BitAnd:
        case BinaryOp.StrConcat:
        case BinaryOp.TupleConcat:
        case BinaryOp.RecordConcat:
        case BinaryOp.SeqConcat:
        case BinaryOp.Add:
        case BinaryOp.Mul:
            break;

        default:
            Validation.Assert(false);
            break;
        }

        int cur = idx + 1;
        var with = new WithInfo(this, disabled: !canPull);
        var bldr = with.Process(bnd.Args, ref cur);
        Validation.Assert(cur == idx + bnd.NodeCount);
        Validation.Assert(!with.HasGuard);

        BoundNode result;
        switch (bnd.Op)
        {
        case BinaryOp.And:
        case BinaryOp.Or:
            Validation.Coverage(bldr == null ? 0 : 1);
            result = ReduceOrAnd(bnd, bldr);
            break;

        case BinaryOp.Add:
            if (bnd.Type.IsIntegralReq)
            {
                Validation.Coverage(bldr == null ? 0 : 1);
                result = ReduceAssociative(AddIntBop.FromKind(bnd.Type.RootKind), bnd, bldr);
            }
            else
            {
                Validation.Coverage(bldr == null ? 0 : 1);
                result = ReduceFractionalAdd(FracOps.FromKind(bnd.Type.RootKind), bnd, bldr);
            }
            break;
        case BinaryOp.Mul:
            if (bnd.Type.IsIntegralReq)
            {
                Validation.Coverage(bldr == null ? 0 : 1);
                result = ReduceAssociative(MulIntBop.FromKind(bnd.Type.RootKind), bnd, bldr);
            }
            else
            {
                Validation.Coverage(bldr == null ? 0 : 1);
                result = ReduceFractionalMul(FracOps.FromKind(bnd.Type.RootKind), bnd, bldr);
            }
            break;
        case BinaryOp.BitOr:
            Validation.Coverage(bldr == null ? 0 : 1);
            result = ReduceAssociative(OrIntBop.FromKind(bnd.Type.RootKind), bnd, bldr);
            break;
        case BinaryOp.BitXor:
            Validation.Coverage(bldr == null ? 0 : 1);
            result = ReduceAssociative(XorIntBop.FromKind(bnd.Type.RootKind), bnd, bldr);
            break;
        case BinaryOp.BitAnd:
            Validation.Coverage(bldr == null ? 0 : 1);
            result = ReduceAssociative(AndIntBop.FromKind(bnd.Type.RootKind), bnd, bldr);
            break;

        case BinaryOp.Xor:
            Validation.Coverage(bldr == null ? 0 : 1);
            result = ReduceAssociative(XorBoolBop.Instance, bnd, bldr);
            break;

        case BinaryOp.StrConcat:
            Validation.Coverage(bldr == null ? 0 : 1);
            result = ReduceStrConcat(bnd, bldr);
            break;
        case BinaryOp.TupleConcat:
            Validation.Coverage(bldr == null ? 0 : 1);
            result = ReduceTupleConcat(bnd, bldr);
            break;
        case BinaryOp.RecordConcat:
            Validation.Coverage(bldr == null ? 0 : 1);
            result = ReduceRecordConcat(bnd, bldr);
            break;
        case BinaryOp.SeqConcat:
            Validation.Coverage(bldr == null ? 0 : 1);
            result = ReduceSeqConcat(bnd, bldr);
            break;

        default:
            result = bnd;
            if (bldr != null)
                result = BndVariadicOpNode.Create(bnd.Type, bnd.Op, bldr.ToImmutable(), bnd.Inverted);
            break;
        }
        Validation.Assert(result.Type == bnd.Type);

        Validation.Coverage(with.HasAny ? 0 : 1);
        Push(bnd, with.Apply(result));
        return false;
    }

    protected override bool PreVisitImpl(BndCompareNode bnd, int idx)
    {
        Validation.AssertValue(bnd);
        Push(bnd, ReduceCompare(bnd, idx));
        return false;
    }

    protected override bool PreVisitImpl(BndCallNode bnd, int idx)
    {
        Validation.AssertValue(bnd);

        // Can't just call with.Process(args) because we also need to push the scope mapping items.
        int cur = idx + 1;
        var with = new WithInfo(this, bnd);
        ArgTuple.Builder bldr = null;
        var args = bnd.Args;
        int iscope = 0;
        var scopes = bnd.Scopes;
        for (int iarg = 0; iarg < args.Length; iarg++)
        {
            var arg = args[iarg];
            var res = with.Process(arg, ref cur, iarg);
            Validation.AssertValue(res);
            Validation.Assert(res.Type == arg.Type);

            if (res != arg)
            {
                bldr ??= args.ToBuilder();
                bldr[iarg] = res;
            }

            if (bnd.Traits.IsScope(iarg))
            {
                Validation.AssertIndex(iscope, scopes.Length);
                var scope = scopes[iscope++];
                Validation.Assert(!_scopeToValue.ContainsKey(scope));
                // Record the scope value for constant scope replacement.
                _scopeToValue.Add(scope, (res, bnd.Oper));
            }
        }
        Validation.Assert(iscope == scopes.Length);
        Validation.Assert(cur == idx + bnd.NodeCount);

        // Erase the scope mapping items.
        for (int i = 0; i < scopes.Length; i++)
            _scopeToValue.Remove(scopes[i]).Verify();

        var call = bnd;
        if (bldr != null)
            call = call.SetArgs(bldr.ToImmutable());

        var result = call.Oper.Reduce(_intf, call);

        // REVIEW: Should this apply further reductions to the result (assuming it's not
        // the same as call)? Currently it assumes that the function reduced fully, which seems
        // like a questionable assumption.

        Validation.Coverage(with.HasAny ? 0 : 1);
        Push(bnd, with.Apply(result));
        return false;
    }

    protected override bool PreVisitImpl(BndSetFieldsNode bnd, int idx)
    {
        Validation.AssertValue(bnd);
        BoundNode result = bnd;

        int cur = idx + 1;
        var with = new WithInfo(this);
        var scope = bnd.Scope;
        var source = with.Process(bnd.Source, ref cur);

        bool changes = source != bnd.Source;
        var adds = bnd.Additions;
        Validation.Assert(adds.NeedName);
        Validation.Assert(adds.NeedNode);
        if (adds.Count > 0)
        {
            Validation.AssertValue(scope);
            Validation.Assert(scope.Kind == ScopeKind.With);

            Validation.Assert(!_scopeToValue.ContainsKey(scope));
            _scopeToValue.Add(scope, (source, null));

            foreach (var (name, val) in adds.GetPairs())
            {
                var res = with.Process(val, ref cur, scope);
                if (val != res)
                {
                    Validation.Assert(!val.Equivalent(res));
                    changes = true;
                    adds = adds.SetItem(name, res);
                }
            }

            changes |= DropSelfFields(bnd.Type, source, scope, ref adds);

            _scopeToValue.Remove(scope).Verify();
        }
        Validation.Assert(cur == idx + bnd.NodeCount);

        if (changes)
            result = BndSetFieldsNode.Create(bnd.Type, source, scope, adds, bnd.NameHints);
        Validation.Coverage(with.HasAny ? 0 : 1);
        Push(bnd, with.Apply(result));
        return false;
    }

    protected override bool PreVisitImpl(BndIfNode bnd, int idx)
    {
        Validation.AssertValue(bnd);

        int cur = idx + 1;
        var with = new WithInfo(this);
        var condValue = with.Process(bnd.CondValue, ref cur);

        // REVIEW: When the condition is a constant, should we still reduce both sides (for warnings)?
        var trueValue = Reduce(bnd.TrueValue, ref cur);
        var falseValue = Reduce(bnd.FalseValue, ref cur);
        Validation.Assert(cur == idx + bnd.NodeCount);

        BoundNode result;
        if (condValue.TryGetBool(out bool cond))
            result = cond ? trueValue : falseValue;
        else if (trueValue.Equivalent(falseValue))
            result = trueValue;
        else if (condValue != bnd.CondValue || trueValue != bnd.TrueValue || falseValue != bnd.FalseValue)
            result = BndIfNode.Create(condValue, trueValue, falseValue);
        else
            result = bnd;

        Validation.Coverage(with.HasAny ? 0 : 1);
        Push(bnd, with.Apply(result));
        return false;
    }

    protected override void VisitImpl(BndScopeRefNode bnd, int idx)
    {
        Validation.AssertValue(bnd);
        Validation.AssertValue(bnd.Scope);
        Validation.Assert(bnd.Type == bnd.Scope.Type);

        var scope = bnd.Scope;
        if (_scopeToValue.TryGetValue(scope, out var pair))
        {
            var (value, func) = pair;
            switch (scope.Kind)
            {
            case ScopeKind.With:
                if (value.IsConstant || value.IsCheap && func is WithFunc)
                {
                    Push(bnd, value);
                    return;
                }
                break;

            case ScopeKind.Guard:
                if (value.IsNonNullConstant)
                {
                    if (value is BndCastOptNode bco)
                    {
                        Validation.Assert(scope.Type == bco.Child.Type);
                        value = bco.Child;
                    }
                    else if (value is BndCastRefNode bcr && !bcr.Child.Type.IsOpt)
                    {
                        // REVIEW: Can this case happen? Perhaps for compound constants, so future
                        // symbolic compute scenarios will hit this.
                        var typeSrc = bcr.Child.Type;
                        if (typeSrc == scope.Type)
                            value = bcr.Child;
                        else
                            value = BndCastRefNode.Create(bcr.Child, scope.Type);
                    }

                    if (value.Type == scope.Type)
                    {
                        Push(bnd, value);
                        return;
                    }

                    // How did we get here?
                    Validation.Assert(false);
                }
                break;
            }
        }

        base.VisitImpl(bnd, idx);
    }

    protected override bool PreVisitImpl(BndGetFieldNode bnd, int idx)
    {
        Validation.AssertValue(bnd);
        BoundNode result = bnd;

        int cur = idx + 1;
        var with = new WithInfo(this);
        var rec = with.Process(bnd.Record, ref cur);
        Validation.Assert(cur == idx + bnd.NodeCount);
        if (rec != bnd.Record)
            result = BndGetFieldNode.Create(bnd.Name, rec);
        Validation.Coverage(with.HasAny ? 0 : 1);
        Push(bnd, with.Apply(result));
        return false;
    }

    protected override bool PreVisitImpl(BndGetSlotNode bnd, int idx)
    {
        Validation.AssertValue(bnd);
        BoundNode result = bnd;

        int cur = idx + 1;
        var with = new WithInfo(this);
        var tup = with.Process(bnd.Tuple, ref cur);
        if (tup != bnd.Tuple)
            result = BndGetSlotNode.Create(bnd.Slot, tup);
        Validation.Assert(cur == idx + bnd.NodeCount);
        Validation.Coverage(with.HasAny ? 0 : 1);
        Push(bnd, with.Apply(result));
        return false;
    }

    protected override bool PreVisitImpl(BndIdxTextNode bnd, int idx)
    {
        Validation.AssertValue(bnd);
        BoundNode result = bnd;

        int cur = idx + 1;
        var with = new WithInfo(this);
        var txt = with.Process(bnd.Text, ref cur);
        var ind = with.Process(bnd.Index, ref cur);
        Validation.Assert(cur == idx + bnd.NodeCount);
        Validation.Coverage(txt == bnd.Text ? 0 : 1);
        Validation.Coverage(ind == bnd.Index ? 0 : 1);
        if (txt != bnd.Text || ind != bnd.Index)
            result = BndIdxTextNode.Create(txt, ind, bnd.Modifier);
        Validation.Coverage(with.HasAny ? 0 : 1);
        Push(bnd, with.Apply(result));
        return false;
    }

    protected override bool PreVisitImpl(BndIdxTensorNode bnd, int idx)
    {
        Validation.AssertValue(bnd);

        int cur = idx + 1;
        var with = new WithInfo(this);
        var tensor = with.Process(bnd.Tensor, ref cur);
        var inds = with.Process(bnd.Indices, ref cur);
        Validation.Assert(cur == idx + bnd.NodeCount);

        var result = bnd;
        if (inds != null)
            result = bnd.SetChildren(tensor, inds.ToImmutable());
        else if (tensor != bnd.Tensor)
            result = bnd.SetChildren(tensor, bnd.Indices);
        else
            Validation.Assert(!with.HasAny);

        if (tensor is BndTensorNode btn)
        {
            // The tensor node is a BndTensorNode so see if we can reduce. Note that
            // a call to Tensor.From on BndSequenceNode is reduced by Tensor.From to
            // a BndTensorNode.
            // REVIEW: Also handle a "constant" tensor.
            Shape shape = btn.Shape;
            var items = btn.Items;
            int rank = shape.Rank;
            Validation.Assert(rank == result.Indices.Length);

            long index = 0;
            bool good = true;
            for (int i = 0; i < rank; i++)
            {
                if (!result.Indices[i].TryGetIntegral(out var ind))
                {
                    good = false;
                    continue;
                }
                if (ind < 0)
                    ind += shape[i];
                if (ind < 0 || ind >= shape[i])
                {
                    // Reduce to the default value.
                    Warn(bnd, ErrorStrings.WrnTensorIndexOutOfRange);
                    Push(bnd, BndDefaultNode.Create(result.Type));
                    return false;
                }
                index = index * shape[i] + (long)ind;
            }

            if (good)
            {
                // No need to further reduce.
                Validation.AssertIndex(index, items.Length);
                Push(bnd, items[(int)index]);
                return false;
            }
        }

        Validation.Coverage(with.HasAny ? 0 : 1);
        Push(bnd, with.Apply(result));
        return false;
    }

    protected override bool PreVisitImpl(BndIdxHomTupNode bnd, int idx)
    {
        Validation.AssertValue(bnd);
        BoundNode result = bnd;

        int cur = idx + 1;
        var with = new WithInfo(this);
        var tup = with.Process(bnd.Tuple, ref cur);
        var ind = with.Process(bnd.Index, ref cur);
        Validation.Assert(cur == idx + bnd.NodeCount);
        Validation.Coverage(tup == bnd.Tuple ? 0 : 1);
        Validation.Coverage(ind == bnd.Index ? 0 : 1);
        if (tup != bnd.Tuple || ind != bnd.Index)
        {
            result = BndIdxHomTupNode.Create(tup, ind, bnd.Modifier, out bool oor);
            if (oor)
                _host.Warn(bnd, ErrorStrings.WrnHomTupleIndexOutOfRange);
        }
        Validation.Coverage(with.HasAny ? 0 : 1);
        Push(bnd, with.Apply(result));
        return false;
    }

    protected override bool PreVisitImpl(BndTextSliceNode bnd, int idx)
    {
        // REVIEW: Handle pulling with.
        return base.PreVisitImpl(bnd, idx);
    }

    protected override bool PreVisitCastCore(BndCastNode bnd, int idx)
    {
        Validation.AssertValue(bnd);

        int cur = idx + 1;
        var with = new WithInfo(this);
        var child = with.Process(bnd.Child, ref cur);
        Validation.Assert(cur == idx + bnd.NodeCount);
        var result = bnd.SetChild(child, _host);
        Validation.Coverage(with.HasAny ? 0 : 1);
        Push(bnd, with.Apply(result));
        return false;
    }

    protected override bool PreVisitImpl(BndCastVacNode bnd, int idx)
    {
        Validation.AssertValue(bnd);

        Push(bnd, BndDefaultNode.Create(bnd.Type));
        return false;
    }

    protected override bool PreVisitImpl(BndRecordNode bnd, int idx)
    {
        Validation.AssertValue(bnd);
        BoundNode result = bnd;

        int cur = idx + 1;
        var with = new WithInfo(this);
        var items = bnd.Items;
        Validation.Assert(items.NeedName);
        Validation.Assert(items.NeedNode);
        foreach (var (name, val) in items.GetPairs())
        {
            var res = with.Process(val, ref cur);
            if (val != res)
            {
                Validation.Assert(!val.Equivalent(res));
                items = items.SetItem(name, res);
            }
        }
        Validation.Assert(cur == idx + bnd.NodeCount);
        if (items != bnd.Items)
            result = BndRecordNode.Create(bnd.Type, items, bnd.NameHints);
        Validation.Coverage(with.HasAny ? 0 : 1);
        Push(bnd, with.Apply(result));
        return false;
    }

    protected override bool PreVisitImpl(BndSequenceNode bnd, int idx)
    {
        Validation.AssertValue(bnd);
        BoundNode result = bnd;

        int cur = idx + 1;
        var with = new WithInfo(this);
        var items = with.Process(bnd.Items, ref cur);
        Validation.AssertValue(bnd);
        if (items != null)
        {
            Validation.Assert(items.Count == bnd.Items.Length);
            result = BndSequenceNode.Create(bnd.Type, items.ToImmutable());
        }
        Validation.Coverage(with.HasAny ? 0 : 1);
        Push(bnd, with.Apply(result));
        return false;
    }

    protected override bool PreVisitImpl(BndTensorNode bnd, int idx)
    {
        Validation.AssertValue(bnd);
        BoundNode result = bnd;

        int cur = idx + 1;
        var with = new WithInfo(this);
        var items = with.Process(bnd.Items, ref cur);
        Validation.Assert(cur == idx + bnd.NodeCount);
        if (items != null)
        {
            Validation.Assert(items.Count == bnd.Items.Length);
            result = BndTensorNode.Create(bnd.Type, items.ToImmutable(), bnd.Shape);
        }
        Validation.Coverage(with.HasAny ? 0 : 1);
        Push(bnd, with.Apply(result));
        return false;
    }

    protected override bool PreVisitImpl(BndTensorSliceNode bnd, int idx)
    {
        // REVIEW: Handle pulling with.
        return base.PreVisitImpl(bnd, idx);
    }

    protected override bool PreVisitImpl(BndTupleNode bnd, int idx)
    {
        Validation.AssertValue(bnd);
        BoundNode result = bnd;

        int cur = idx + 1;
        var with = new WithInfo(this);
        var items = with.Process(bnd.Items, ref cur);
        Validation.AssertValue(bnd);
        if (items != null)
        {
            Validation.Assert(items.Count == bnd.Items.Length);
            result = BndTupleNode.Create(items.ToImmutable(), bnd.Type);
        }
        Validation.Coverage(with.HasAny ? 0 : 1);
        Push(bnd, with.Apply(result));
        return false;
    }

    protected override bool PreVisitImpl(BndTupleSliceNode bnd, int idx)
    {
        // REVIEW: Handle pulling with.
        return base.PreVisitImpl(bnd, idx);
    }

    protected override bool PreVisitImpl(BndGroupByNode bnd, int idx)
    {
        Validation.AssertValue(bnd);
        BoundNode result = bnd;

        int cur = idx + 1;
        var with = new WithInfo(this);
        var source = with.Process(bnd.Source, ref cur);

        var keysPure = bnd.PureKeys;
        var keysKeep = bnd.KeepKeys;
        var maps = bnd.MapItems;
        var aggs = bnd.AggItems;
        if (ReduceChildren(ref keysPure, ref cur) |
            ReduceChildren(ref keysKeep, ref cur) |
            ReduceChildren(ref maps, ref cur) |
            ReduceChildren(ref aggs, ref cur) |
            source != bnd.Source)
        {
            result = BndGroupByNode.Create(
                bnd.Type, source,
                bnd.ScopeForKeys, bnd.IndexForKeys, keysPure, keysKeep, bnd.KeysCi,
                bnd.ScopeForMaps, bnd.IndexForMaps, maps,
                bnd.ScopeForAggs, aggs);
        }
        Validation.Assert(cur == idx + bnd.NodeCount);
        Validation.Coverage(with.HasAny ? 0 : 1);
        Push(bnd, with.Apply(result));
        return false;
    }

    /// <summary>
    /// If bnd is a negation, represented as an add with all inv bits set, or a mul with last operand -1,
    /// this strips off the negation and returns true. Otherwise, it leaves bnd alone and returns false.
    /// </summary>
    private bool TryStripNegation(ref BoundNode bnd)
    {
        Validation.AssertValue(bnd);
        Validation.Assert(bnd.Type.IsNumericReq);

        // See if this is an add/sub with the zeroth bit set.
        if (BndVariadicOpNode.TryGetArgs(BinaryOp.Add, bnd, out var args, out var invs))
        {
            if (!invs.TestBit(0))
                return false;
            if (args.Length == 1)
                bnd = args[0];
            else
                bnd = BndVariadicOpNode.Create(bnd.Type, BinaryOp.Add, args, BitSet.GetMask(args.Length) - invs);
            return true;
        }

        // See if this is a mul/div with the last arg being -1.
        if (BndVariadicOpNode.TryGetArgs(BinaryOp.Mul, bnd, out args, out invs))
        {
            if (args.Length < 2)
                return false;

            int index = args.Length - 1;
            if (args[index].TryGetFractional(out var dbl))
            {
                // Have to write it this way instead of "if (dbl != -1.0)" because of NaN.
                if (!(dbl == -1))
                    return false;
                invs = invs.ClearBit(index);
            }
            else if (args[index].TryGetIntegral(out var val))
            {
                if (val != -1)
                    return false;
            }
            else
                return false;

            if (args.Length == 2 && !invs.TestBit(0))
            {
                bnd = args[0];
                return true;
            }

            bnd = BndVariadicOpNode.Create(bnd.Type, BinaryOp.Mul, args.RemoveAt(index), invs);
            return true;
        }

        return false;
    }

    /// <summary>
    /// This tries to apply negation to a factor in a mul/div node. It assumes that the node has been
    /// reduced and has had negation stripped. That is, TryStripNegation should have been called first.
    /// </summary>
    private bool TryNegateFactor(ref BoundNode bnd)
    {
        Validation.AssertValue(bnd);
        Validation.Assert(bnd.Type.IsFractionalReq);

        // If constant, negate.
        if (bnd.TryGetFractional(out var val))
        {
            if (!double.IsNaN(val))
                bnd = BndFltNode.Create(bnd.Type, -val);
            return true;
        }

        // See if the arg is a mul/div.
        if (BndVariadicOpNode.TryGetArgs(BinaryOp.Mul, bnd, out var args, out var invs))
        {
            // Find the last constant arg, if there is one, and fold the negation into it.
            for (int i = args.Length; --i >= 0;)
            {
                var arg = args[i];
                if (arg.TryGetFractional(out val))
                {
                    // We have the last constant arg. If the constant is NaN, negation has no effect.
                    if (!double.IsNaN(val))
                    {
                        // Negate this arg. This assert assumes that -1 values have been stripped.
                        Validation.Assert(val != -1);
                        args = args.SetItem(i, BndFltNode.Create(bnd.Type, -val));
                        bnd = BndVariadicOpNode.Create(bnd.Type, BinaryOp.Mul, args, invs);
                    }
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// This applies negation to a numeric integral node.
    /// </summary>
    private BoundNode NegateIntegral(BoundNode bnd)
    {
        Validation.AssertValue(bnd);
        Validation.Assert(bnd.Type.IsIntegralReq);
        Validation.Assert(!bnd.IsConstant);

        // If this is a add/sub, invert all the inv bits.
        if (BndVariadicOpNode.TryGetArgs(BinaryOp.Add, bnd, out var args, out var invs))
        {
            if (args.Length == 1 && invs.TestBit(0))
                return args[0];
            return BndVariadicOpNode.Create(bnd.Type, BinaryOp.Add, args, invs: BitSet.GetMask(args.Length) - invs);
        }

        if (TryStripNegation(ref bnd))
            return bnd;

        return BndVariadicOpNode.Create(bnd.Type, BinaryOp.Add, ArgTuple.Create(bnd), invs: 0x1);
    }

    /// <summary>
    /// This applies negation to a numeric fractional node.
    /// </summary>
    private BoundNode NegateFractional(BoundNode bnd)
    {
        // REVIEW: Currently this is only called from one place and in that case, bnd can be assumed to have had
        // TryStripNegation already called on it. Consequently, code coverage is not complete. Should we simplify this
        // leveraging the invariants from that one call site, or make it fully general?

        Validation.AssertValue(bnd);
        Validation.Assert(!bnd.IsConstant);

        DType type = bnd.Type;
        Validation.Assert(type.IsFractionalReq);

        // If this is a add/sub, invert all the inv bits.
        if (BndVariadicOpNode.TryGetArgs(BinaryOp.Add, bnd, out var args, out var invs))
        {
            if (args.Length == 1 && invs.TestBit(0))
                return args[0];
            int tmp = 0;
            return Reduce(BndVariadicOpNode.Create(
                bnd.Type, BinaryOp.Add, args, invs: BitSet.GetMask(args.Length) - invs), ref tmp);
        }

        // If this is a mul/div, see if one of the args is easily negatable.
        if (BndVariadicOpNode.TryGetArgs(BinaryOp.Mul, bnd, out args, out invs))
        {
            for (int i = args.Length; --i >= 0;)
            {
                var arg = args[i];
                if (TryStripNegation(ref arg) || TryNegateFactor(ref arg))
                {
                    args = args.SetItem(i, arg);
                    return BndVariadicOpNode.Create(type, BinaryOp.Mul, args, invs);
                }
                Validation.Assert(arg == args[i]);
            }

            // We're in a mul/div with no constant term, so just tack on -1.
            // No Reduce call necessary because TryStripNegation was called before entering this method.
            return BndVariadicOpNode.Create(type, BinaryOp.Mul, args.Add(BndFltNode.Create(type, -1)), invs);
        }

        // No Reduce call necessary because TryStripNegation was called before entering this method.
        return BndVariadicOpNode.Create(type, BinaryOp.Add, ArgTuple.Create(bnd), invs: 0x1);
    }

    /// <summary>
    /// Reduce a variadic op node for an associative commutative binary operator.
    /// </summary>
    private BoundNode ReduceAssociative<T>(AssociativeBop<T> bop, BndVariadicOpNode bnd, ArgTuple.Builder bldr)
    {
        // REVIEW: Should we optimize when multiple args are equivalent? Eg, for bitwise
        // or/and, the dups can just be dropped, for bitwise xor, two equivalent args result in
        // zero. For add/sub we have the obvious reduction. Of course, then we'd be tempted to
        // optimize 3*x - 5*x, etc. We might need this level of reduction for the module/optimization
        // functionality, but this could also be done by a separate reducer. Note that module/optimization
        // will probably also need to "cheat" at times and pretend that floating point arithmetic
        // is associative.

        Validation.AssertValue(bop);
        Validation.AssertValue(bnd);
        Validation.AssertValueOrNull(bldr);

        Validation.Assert(bop.Op == bnd.Op);
        var type = bnd.Type;
        Validation.Assert(!type.HasReq);
        Validation.Assert(!type.IsSequence);
        Validation.Assert(bop.Kind == type.RootKind);

        var args = bnd.Args;
        Validation.Assert(args.Length > 0);
        Validation.Assert(args.All(a => a.Type == type));
        Validation.Assert(bldr == null || bldr.Count == args.Length);
        Validation.Assert(bldr == null || bldr.All(a => a.Type == type));

        var invs = bnd.Inverted;
        Validation.Assert(!invs.TestAtOrAbove(args.Length));

        // Fully associative and commutative so flatten and combine all the constants.
        if (bldr != null)
            BndVariadicOpNode.Flatten(bnd.Op, ref bldr, ref invs);

        // Track the combined constant value.
        bool invTail = false;
        T valTail = bop.Identity;
        BoundNode tail = null;

        // For signed multiplication, we gather/consolidate negation.
        bool isSMul = bop.Op == BinaryOp.Mul && type.RootKind.IsSignedIntegral();
        bool negate = false;

        int lenSrc = bldr != null ? bldr.Count : args.Length;
        int ivDst = 0;
        for (int ivSrc = 0; ivSrc < lenSrc; ivSrc++)
        {
            var arg = bldr != null ? bldr[ivSrc] : args[ivSrc];
            var inv = invs.TestBit(ivSrc);
            if (bop.TryGetConst(arg, out var val))
            {
                if (bop.IsSink(val))
                    return arg;
                if (bop.IsIdentity(val))
                    continue;
                if (bop.IsIdentity(valTail))
                {
                    invTail = inv;
                    valTail = val;
                    tail = arg;
                }
                else
                {
                    if (invTail)
                    {
                        var valCur = valTail;
                        valTail = bop.Identity;
                        bop.Apply(ref valTail, valCur, invTail);
                        invTail = false;
                    }
                    bop.Apply(ref valTail, val, inv);
                    if (bop.IsSink(valTail))
                        return bop.CreateConst(valTail);
                    tail = null;
                }
                continue;
            }

            if (isSMul && TryStripNegation(ref arg))
            {
                negate = !negate;
                bldr ??= args.ToBuilder();
                bldr[ivDst] = arg;
            }

            if (ivDst < ivSrc)
            {
                invs = inv ? invs.SetBit(ivDst) : invs.ClearBit(ivDst);
                bldr ??= args.ToBuilder();
                bldr[ivDst] = arg;
            }
            ivDst++;
        }
        Validation.Assert(tail == null || !bop.IsIdentity(valTail));

        if (negate)
        {
            Validation.Assert(isSMul);
            valTail = bop.Negate(valTail);
            tail = null;
            negate = false;
        }
        Validation.Assert(!negate);

        // Normalize the constant value. Inv is ok when ivDst > 0.
        // REVIEW: Consider issuing a warning for add/sub if the type is unsigned and all
        // args, including a non-zero tail, end up with inv set. In such a case, computing the
        // result will always involve overflow.
        if (bop.Trim(ref valTail, ref invTail, invOk: ivDst > 0, _host, bnd))
            tail = null;

        if (bop.IsSink(valTail))
            return bop.CreateConst(valTail);

        if (ivDst == 0)
        {
            Validation.Assert(!invTail);
            return tail ?? bop.CreateConst(valTail);
        }

        if (!bop.IsIdentity(valTail))
        {
            // If valTail is already at the end, no need to change anything.
            if (bldr == null && tail != null && ivDst == lenSrc - 1 && tail == args[lenSrc - 1] && invs == bnd.Inverted)
                return bnd;

            if (isSMul && bop.IsIdentity(bop.Negate(valTail)))
                negate = true;
            else
            {
                invs = invTail ? invs.SetBit(ivDst) : invs.ClearBit(ivDst);
                tail ??= bop.CreateConst(valTail);
                bldr ??= args.ToBuilder();
                Validation.Assert(bldr.Count == lenSrc);
                Validation.Assert(ivDst <= lenSrc);
                if (ivDst < lenSrc)
                    bldr[ivDst++] = tail;
                else
                {
                    Validation.Assert(false); // Can we get here? If so, extend tests for coverage.
                    bldr.Add(tail);
                    ivDst = bldr.Count;
                }
            }
        }

        Validation.Assert(!negate | isSMul);
        if (ivDst == 1)
        {
            var arg = bldr != null ? bldr[0] : args[0];
            if (!invs.TestBit(0))
                return negate ? NegateIntegral(arg) : arg;

            // Negation in a sum.
            Validation.Assert(bop.Op == BinaryOp.Add);
            if (TryStripNegation(ref arg))
                return arg;
        }

        if (ivDst < lenSrc)
        {
            invs = invs.ClearAtAndAbove(ivDst);
            bldr ??= args.ToBuilder();
            bldr.RemoveTail(ivDst);
        }

        if (bldr == null)
        {
            Validation.Assert(!negate);
            return bnd;
        }

        Validation.Assert(!AreEquiv(bldr, bnd.Args));
        int tmp = 0;
        var res = Reduce(BndVariadicOpNode.Create(bnd.Type, bnd.Op, bldr.ToImmutable(), invs), ref tmp);
        return negate ? NegateIntegral(res) : res;
    }

    /// <summary>
    /// Reduce a variadic node for floating point add/sub.
    /// </summary>
    protected virtual BoundNode ReduceFractionalAdd(FracOps frops, BndVariadicOpNode bnd, ArgTuple.Builder bldr)
    {
        Validation.AssertValue(frops);
        Validation.AssertValue(bnd);
        Validation.Assert(bnd.Op == BinaryOp.Add);
        Validation.Assert(bnd.Type.IsFractionalReq);
        Validation.Assert(frops.Kind == bnd.Type.RootKind);
        Validation.AssertValueOrNull(bldr);

        var args = bnd.Args;
        Validation.Assert(args.Length > 0);
        Validation.Assert(args.All(a => a.Type == bnd.Type));
        Validation.Assert(bldr == null || bldr.Count == args.Length);
        Validation.Assert(bldr == null || bldr.All(a => a.Type == bnd.Type));

        var invs = bnd.Inverted;
        Validation.Assert(!invs.TestAtOrAbove(args.Length));

        // Combine leading constants. Note that addition is tricky because of negative zero issues.
        // In particular, we can't just drop zeros, but we can combine (signed) zeroes with other
        // leading constants into a single leading constant.

        // REVIEW: We could do:
        // * If we have any non-zero constants, we could drop all zeros.
        // * If we have any positive zeros, we could drop all but one positive zeros and all negative zeros.

        // REVIEW: Should we always fold an inv bit into a constant? It doesn't really save
        // anything, but would "normalize".

        int lenSrc = args.Length;
        var arg = bldr != null ? bldr[0] : args[0];
        if (frops.TryGetConst(arg, out var valFirst))
        {
            double valHead;
            if (!invs.TestBit(0))
                valHead = valFirst;
            else
            {
                valHead = -valFirst;
                invs = invs.ClearBit(0);
            }

            int ivSrc;
            for (ivSrc = 1; ivSrc < lenSrc; ivSrc++)
            {
                arg = bldr != null ? bldr[ivSrc] : args[ivSrc];
                if (!frops.TryGetConst(arg, out var val))
                    break;
                frops.Add(ref valHead, val, invs.TestBit(ivSrc));
            }
            Validation.Assert(ivSrc >= 1);

            if (ivSrc > 1 || valHead.ToBits() != valFirst.ToBits())
            {
                var res = frops.CreateConst(valHead);
                if (ivSrc >= lenSrc)
                    return res;
                bldr ??= args.ToBuilder();
                bldr[0] = res;
                invs = invs.ClearBit(0);

                if (ivSrc > 1)
                {
                    bldr.RemoveMinLim(1, ivSrc);

                    // REVIEW: Need a better way to remove a range of bits (sliding higher down).
                    int ivDst = 1;
                    for (; ivSrc < lenSrc; ivSrc++, ivDst++)
                        invs = invs.TestBit(ivSrc) ? invs.SetBit(ivDst) : invs.ClearBit(ivDst);
                    invs = invs.ClearAtAndAbove(ivDst);

                    Validation.Assert(ivSrc == lenSrc);
                    Validation.Assert(ivDst == bldr.Count);
                }
            }
        }

        int lenDst = bldr != null ? bldr.Count : lenSrc;
        if (lenDst == 1)
        {
            arg = bldr != null ? bldr[0] : args[0];
            if (!invs.TestBit(0))
                return arg;
            if (TryStripNegation(ref arg) || TryNegateFactor(ref arg))
                return arg;
        }

        if (bldr == null)
        {
            if (invs == bnd.Inverted)
                return bnd;
            // REVIEW: This case isn't currently covered by tests and it probably won't ever be
            // unless -NaN ends up with the same bits as NaN. That is currently not the case (the sign
            // bit is inverted), but might depend on the version of the CLR. The test case "-(0/0) + r8"
            // will hit this if/when -NaN has the same bits as NaN.
            return BndVariadicOpNode.Create(bnd.Type, bnd.Op, args, invs);
        }

        Validation.Assert(!AreEquiv(bldr, bnd.Args));
        return BndVariadicOpNode.Create(bnd.Type, bnd.Op, bldr.ToImmutable(), invs);
    }

    /// <summary>
    /// Reduce a variadic node for floating point mul/div.
    /// </summary>
    protected virtual BoundNode ReduceFractionalMul(FracOps frops, BndVariadicOpNode bnd, ArgTuple.Builder bldr)
    {
        Validation.AssertValue(frops);
        Validation.AssertValue(bnd);
        Validation.Assert(bnd.Op == BinaryOp.Mul);
        Validation.Assert(bnd.Type.IsFractionalReq);
        Validation.Assert(frops.Kind == bnd.Type.RootKind);
        Validation.AssertValueOrNull(bldr);

        var args = bnd.Args;
        Validation.Assert(args.Length > 0);
        Validation.Assert(args.All(a => a.Type == bnd.Type));
        Validation.Assert(bldr == null || bldr.Count == args.Length);
        Validation.Assert(bldr == null || bldr.All(a => a.Type == bnd.Type));

        var invs = bnd.Inverted;
        Validation.Assert(!invs.TestAtOrAbove(args.Length));

        // Combine leading constants.
        bool negate = false;
        int ivSrc = 0;
        int ivDst = 0;
        int lenSrc = args.Length;
        var arg = bldr != null ? bldr[ivSrc] : args[ivSrc];
        if (frops.TryGetConst(arg, out var valFirst))
        {
            double valHead;
            if (!invs.TestBit(ivSrc))
                valHead = valFirst;
            else
            {
                // Note: this is difficult (impossible?) to hit through the binder, but when we
                // implement partial evaluation (beta reduction), it will certainly be possible.
                valHead = 1d;
                frops.Mul(ref valHead, valFirst, true);
                invs = invs.ClearBit(0);
            }

            for (ivSrc = 1; ivSrc < lenSrc; ivSrc++)
            {
                arg = bldr != null ? bldr[ivSrc] : args[ivSrc];
                if (!frops.TryGetConst(arg, out var val))
                    break;
                frops.Mul(ref valHead, val, invs.TestBit(ivSrc));
            }

            if (valHead == +1)
            {
            }
            else if (valHead == -1)
                negate = true;
            else if (ivSrc > 1 || valHead.ToBits() != valFirst.ToBits())
            {
                var res = frops.CreateConst(valHead);
                if (ivSrc >= lenSrc)
                    return res;
                bldr ??= args.ToBuilder();
                invs = invs.ClearBit(ivDst);
                bldr[ivDst++] = res;
            }
            else
                ivDst = 1;
            Validation.Assert(ivSrc >= 1);
        }
        Validation.Assert(ivDst <= 1);

        // Track the index of the last constant.
        int ivDstConst = ivDst - 1;

        // Consolidate negation and drop identities.
        for (; ivSrc < lenSrc; ivSrc++)
        {
            arg = bldr != null ? bldr[ivSrc] : args[ivSrc];
            var old = arg;
            if (frops.TryGetConst(arg, out var val))
            {
                if (val == +1d)
                    continue;
                if (val == -1d)
                {
                    negate = !negate;
                    continue;
                }
                ivDstConst = ivDst;
            }
            else
                negate ^= TryStripNegation(ref arg);

            if (old != arg || ivDst < ivSrc)
            {
                bldr ??= args.ToBuilder();
                bldr[ivDst] = arg;
                if (ivDst < ivSrc)
                    invs = invs.TestBit(ivSrc) ? invs.SetBit(ivDst) : invs.ClearBit(ivDst);
            }
            ivDst++;
        }
        Validation.Assert(-1 <= ivDstConst & ivDstConst < ivDst);

        if (ivDst == 0)
            return frops.CreateConst(negate ? -1d : +1d);
        if (ivDst == 1)
        {
            arg = bldr != null ? bldr[0] : args[0];
            if (!invs.TestBit(0))
                return negate ? NegateFractional(arg) : arg;
        }
        if (negate)
        {
            double val;
            if (ivDstConst >= 0)
            {
                bldr ??= args.ToBuilder();
                bldr[ivDstConst].TryGetFractional(out val).Verify();
                bldr[ivDstConst] = frops.CreateConst(-val);
            }
            else if (ivDst == lenSrc - 1 &&
                (bldr != null ? bldr[ivDst] : args[ivDst]).TryGetFractional(out val) &&
                val == -1.0)
            {
                invs = invs.ClearBit(ivDst);
                ivDst++;
            }
            else
            {
                bldr ??= args.ToBuilder();
                invs = invs.ClearBit(ivDst);
                Validation.Assert(bldr.Count == lenSrc);
                Validation.Assert(ivDst <= lenSrc);
                var tail = frops.CreateConst(-1);
                if (ivDst < bldr.Count)
                    bldr[ivDst++] = tail;
                else
                {
                    bldr.Add(tail);
                    ivDst = bldr.Count;
                }
            }
        }

        if (ivDst < lenSrc)
        {
            bldr ??= args.ToBuilder();
            bldr.RemoveTail(ivDst);
            invs = invs.ClearAtAndAbove(ivDst);
        }

        if (bldr == null)
        {
            if (invs == bnd.Inverted)
                return bnd;
            return BndVariadicOpNode.Create(bnd.Type, bnd.Op, args, invs);
        }

        Validation.Assert(!AreEquiv(bldr, bnd.Args));
        return BndVariadicOpNode.Create(bnd.Type, bnd.Op, bldr.ToImmutable(), invs);
    }

    /// <summary>
    /// Reduce a string concat node. If <paramref name="bldr"/> is not null, it contains
    /// the reduced args (some of them are different than in <paramref name="bnd"/>).
    /// </summary>
    private BoundNode ReduceStrConcat(BndVariadicOpNode bnd, ArgTuple.Builder bldr)
    {
        Validation.AssertValue(bnd);
        Validation.Assert(bnd.Op == BinaryOp.StrConcat);
        Validation.Assert(bnd.Inverted.IsEmpty);
        Validation.Assert(bnd.Type == DType.Text);

        var args = bnd.Args;
        Validation.Assert(args.Length >= 1);
        Validation.Assert(args.All(a => a.Type == DType.Text));
        Validation.Assert(bldr == null || bldr.Count == args.Length);
        Validation.Assert(bldr == null || bldr.All(a => a.Type == DType.Text));

        // Fully associative so flatten.
        BitSet invs = default;
        if (bldr != null)
            BndVariadicOpNode.Flatten(bnd.Op, ref bldr, ref invs);
        Validation.Assert(invs.IsEmpty);

        // Combine adjacent constants.
        int lenSrc = bldr != null ? bldr.Count : args.Length;
        int ivDst = 0;

        // Track the previous constant. We use a string builder to avoid O(n*n) perf.
        StringBuilder sbPrev = null;
        string valPrev = default;
        bool prevConst = false;

        for (int ivSrc = 0; ivSrc < lenSrc; ivSrc++)
        {
            var arg = bldr != null ? bldr[ivSrc] : args[ivSrc];
            if (arg.TryGetString(out var val))
            {
                if (string.IsNullOrEmpty(val))
                    continue;
                if (prevConst)
                {
                    // Combine with the previous value (in the string builder).
                    Validation.Assert(ivDst > 0);
                    sbPrev ??= new StringBuilder();
                    if (valPrev != null)
                    {
                        sbPrev.Append(valPrev);
                        valPrev = null;
                    }
                    sbPrev.Append(val);
                    bldr ??= args.ToBuilder();
                    bldr[ivDst - 1] = null; // Filled in later.
                    continue;
                }
                valPrev = val;
                prevConst = true;
            }
            else
            {
                if (prevConst && bldr != null && bldr[ivDst - 1] == null)
                {
                    // The previous value is in the string builder.
                    Validation.Assert(sbPrev != null);
                    bldr[ivDst - 1] = BndStrNode.Create(sbPrev.ToString());
                    sbPrev.Clear();
                }
                valPrev = null;
                prevConst = false;
            }
            Validation.Assert(sbPrev == null || sbPrev.Length == 0);

            if (ivDst < ivSrc)
            {
                bldr ??= args.ToBuilder();
                bldr[ivDst] = arg;
            }
            ivDst++;
        }

        if (ivDst == 0)
        {
            Validation.Assert(!prevConst);
            Validation.Assert(sbPrev == null);
            return BndStrNode.Create("");
        }

        if (prevConst && bldr != null && bldr[ivDst - 1] == null)
        {
            // The last value is in the string builder.
            Validation.Assert(sbPrev != null);
            bldr[ivDst - 1] = BndStrNode.Create(sbPrev.ToString());
            sbPrev.Clear();
        }
        Validation.Assert(sbPrev == null || sbPrev.Length == 0);

        if (ivDst == 1)
        {
            var arg = bldr != null ? bldr[0] : args[0];
            if (arg.IsConstant)
                return arg;
            // REVIEW: If there are other known non-null forms, we could test for them
            // and return "arg" here. We can't always return arg, because concat should
            // produce non-null.
        }

        if (ivDst < lenSrc)
        {
            bldr ??= args.ToBuilder();
            bldr.RemoveTail(ivDst);
        }

        if (bldr == null)
            return bnd;
        Validation.Assert(!AreEquiv(bldr, bnd.Args));
        return BndVariadicOpNode.Create(bnd.Type, bnd.Op, bldr.ToImmutable(), default);
    }

    /// <summary>
    /// Reduce a tuple concat node. If <paramref name="bldr"/> is not null, it contains
    /// the reduced args (some of them are different than in <paramref name="bnd"/>).
    /// </summary>
    private BoundNode ReduceTupleConcat(BndVariadicOpNode bnd, ArgTuple.Builder bldr)
    {
        Validation.AssertValue(bnd);
        Validation.Assert(bnd.Op == BinaryOp.TupleConcat);
        Validation.Assert(bnd.Inverted.IsEmpty);
        Validation.Assert(bnd.Type.IsTupleReq);

        var args = bnd.Args;
        Validation.Assert(args.Length >= 2);
        Validation.Assert(args.All(a => a.Type.IsTupleReq));
        Validation.Assert(bldr == null || bldr.Count == args.Length);
        Validation.Assert(bldr == null || bldr.All(a => a.Type.IsTupleReq));

        // Fully associative so flatten.
        BitSet invs = default;
        if (bldr != null)
            BndVariadicOpNode.Flatten(bnd.Op, ref bldr, ref invs);
        Validation.Assert(invs.IsEmpty);

        // Combine adjacent BndTupleNodes.
        int lenSrc = bldr != null ? bldr.Count : args.Length;
        int ivDst = 0;

        // Track the previous btn.
        BndTupleNode btnPrev = null;
        ArgTuple.Builder bldrPrev = null;
        BndTupleNode btnEmpty = null;
        for (int ivSrc = 0; ivSrc < lenSrc; ivSrc++)
        {
            var arg = bldr != null ? bldr[ivSrc] : args[ivSrc];

            if (arg is BndTupleNode btn)
            {
                if (btn.Items.Length == 0)
                {
                    btnEmpty = btn;
                    continue;
                }

                if (btnPrev != null)
                {
                    // Combine with the previous value (in bldrPrev).
                    Validation.Assert(ivDst > 0);
                    bldrPrev ??= btnPrev.Items.ToBuilder();
                    bldrPrev.AddRange(btn.Items);
                    bldr ??= args.ToBuilder();
                    bldr[ivDst - 1] = null; // Filled in later.
                    continue;
                }
                btnPrev = btn;
            }
            else
            {
                // Drop empties.
                if (arg.Type.TupleArity == 0)
                    continue;

                if (bldrPrev != null)
                {
                    // The previous value is in bldrPrev.
                    Validation.Assert(bldr != null && bldr[ivDst - 1] == null);
                    bldr[ivDst - 1] = BndTupleNode.Create(bldrPrev.ToImmutable());
                    bldrPrev = null;
                }
                btnPrev = null;
            }
            Validation.Assert(bldrPrev == null);

            if (ivDst < ivSrc)
            {
                bldr ??= args.ToBuilder();
                bldr[ivDst] = arg;
            }
            ivDst++;
        }

        if (ivDst == 0)
        {
            Validation.Assert(btnPrev == null);
            Validation.Assert(bldrPrev == null);
            return btnEmpty ?? BndTupleNode.Create(ArgTuple.Empty);
        }

        if (bldrPrev != null)
        {
            // The last value is in bldrPrev.
            Validation.Assert(bldr != null && bldr[ivDst - 1] == null);
            bldr[ivDst - 1] = BndTupleNode.Create(bldrPrev.ToImmutable());
        }

        if (ivDst == 1)
            return bldr != null ? bldr[0] : args[0];

        if (ivDst < lenSrc)
        {
            bldr ??= args.ToBuilder();
            bldr.RemoveTail(ivDst);
        }

        if (bldr == null)
            return bnd;
        Validation.Assert(!AreEquiv(bldr, bnd.Args));
        return BndVariadicOpNode.Create(bnd.Type, bnd.Op, bldr.ToImmutable(), default);
    }

    /// <summary>
    /// Reduce a record concat node. If <paramref name="bldr"/> is not null, it contains
    /// the reduced args (some of them are different than in <paramref name="bnd"/>).
    /// </summary>
    private BoundNode ReduceRecordConcat(BndVariadicOpNode bnd, ArgTuple.Builder bldr)
    {
        Validation.AssertValue(bnd);
        Validation.Assert(bnd.Op == BinaryOp.RecordConcat);
        Validation.Assert(bnd.Inverted.IsEmpty);
        Validation.Assert(bnd.Type.IsRecordReq);

        var args = bnd.Args;
        Validation.Assert(args.Length >= 2);
        Validation.Assert(args.All(a => a.Type.IsRecordReq));
        Validation.Assert(bldr == null || bldr.Count == args.Length);
        Validation.Assert(bldr == null || bldr.All(a => a.Type.IsRecordReq));

        // Fully associative so flatten.
        BitSet invs = default;
        if (bldr != null)
            BndVariadicOpNode.Flatten(bnd.Op, ref bldr, ref invs);
        Validation.Assert(invs.IsEmpty);

        // First build the map from field to "slot".
        int lenSrc = bldr != null ? bldr.Count : args.Length;
        var fieldMap = new Dictionary<DName, int>(bnd.Type.FieldCount);
        for (int slotSrc = 0; slotSrc < lenSrc; slotSrc++)
        {
            var arg = bldr != null ? bldr[slotSrc] : args[slotSrc];
            foreach (var tn in arg.Type.GetNames())
                fieldMap[tn.Name] = slotSrc;
        }
        Validation.Assert(fieldMap.Count == bnd.Type.FieldCount);

        // Determine how many fields each slot contributes.
        var counts = new int[lenSrc];
        foreach (var slot in fieldMap.Values)
        {
            Validation.AssertIndex(slot, lenSrc);
            counts[slot]++;
        }

        // Drop slots that don't contribute any fields and combine adjacent BndRecordNodes.
        int slotDst = 0;

        // Track the previous brn.
        BndRecordNode brnPrev = null;
        DType typePrev = DType.EmptyRecordReq;
        var itemsPrev = NamedItems.Empty;
        Immutable.Array<DName>.Builder namesPrev = null;
        BndRecordNode brnEmpty = null;
        for (int slotSrc = 0; slotSrc < lenSrc; slotSrc++)
        {
            Validation.Assert(typePrev.IsRecordReq);
            Validation.Assert((brnPrev != null) == (typePrev.FieldCount > 0));
            Validation.Assert(itemsPrev.Count <= typePrev.FieldCount);
            Validation.Assert(namesPrev == null || brnPrev != null);

            var arg = bldr != null ? bldr[slotSrc] : args[slotSrc];
            Validation.AssertIndexInclusive(counts[slotSrc], arg.Type.FieldCount);

            // Get the number of fields that this slot contributes.
            int count = counts[slotSrc];
            if (arg is BndRecordNode brn)
            {
                if (count == 0)
                {
                    if (arg.Type.FieldCount == 0)
                        brnEmpty = brn;
                    continue;
                }

                if (brnPrev != null || count < arg.Type.FieldCount)
                {
                    // Handle name hints.
                    if (brnPrev == null)
                        namesPrev = Immutable.Array<DName>.CreateBuilder();
                    else if (namesPrev == null)
                    {
                        // Initialize name hints to reflect brnPrev.
                        namesPrev = brnPrev.NameHints.ToBuilder();
                        int dst = 0;
                        for (int src = 0; src < namesPrev.Count; src++)
                        {
                            if (!typePrev.Contains(namesPrev[src]))
                                continue;
                            if (dst < src)
                                namesPrev[dst] = namesPrev[src];
                            dst++;
                        }
                        if (dst < namesPrev.Count)
                            namesPrev.RemoveTail(dst);
                    }

                    // Add our name hints.
                    foreach (var name in brn.NameHints)
                    {
                        if (fieldMap[name] == slotSrc)
                            namesPrev.Add(name);
                    }

                    // Update the type.
                    foreach (var tn in brn.Type.GetNames())
                    {
                        if (fieldMap[tn.Name] == slotSrc)
                            typePrev = typePrev.Add(tn);
                    }

                    // Update the items.
                    foreach (var pair in brn.Items.GetPairs())
                    {
                        if (fieldMap[pair.key] == slotSrc)
                            itemsPrev = itemsPrev.SetItem(pair.key, pair.val);
                    }

                    if (brnPrev == null)
                    {
                        Validation.Assert(typePrev.FieldCount == count);
                        brnPrev = brn;
                        slotDst++;
                    }
                    else
                    {
                        // Combining with some previous brns.
                        Validation.Assert(slotDst > 0);
                        Validation.Assert(typePrev.FieldCount > count);
                    }
                    bldr ??= args.ToBuilder();
                    bldr[slotDst - 1] = null; // Filled in later.
                    continue;
                }

                Validation.Assert(typePrev.FieldCount == 0);
                Validation.Assert(itemsPrev.Count == 0);
                brnPrev = brn;
                typePrev = brn.Type;
                itemsPrev = brn.Items;
            }
            else
            {
                // Drop if it doesn't contribute.
                if (count == 0)
                    continue;

                if (slotDst > 0 && bldr != null && bldr[slotDst - 1] == null)
                {
                    // The previous item needs to be built.
                    Validation.Assert(brnPrev != null);
                    Validation.Assert(typePrev.FieldCount > 0);
                    bldr[slotDst - 1] = BndRecordNode.Create(typePrev, itemsPrev, namesPrev != null ? namesPrev.ToImmutable() : default);
                    typePrev = DType.EmptyRecordReq;
                    itemsPrev = default;
                    namesPrev = null;
                }
                brnPrev = null;
                typePrev = DType.EmptyRecordReq;
                itemsPrev = NamedItems.Empty;
            }

            if (slotDst < slotSrc)
            {
                bldr ??= args.ToBuilder();
                bldr[slotDst] = arg;
            }
            slotDst++;
        }

        if (slotDst == 0)
        {
            Validation.Assert(brnPrev == null);
            Validation.Assert(typePrev.FieldCount == 0);
            return brnEmpty ?? BndRecordNode.Create(DType.EmptyRecordReq, NamedItems.Empty);
        }

        if (bldr != null && bldr[slotDst - 1] == null)
        {
            // The previous item needs to be built.
            Validation.Assert(brnPrev != null);
            Validation.Assert(typePrev.FieldCount > 0);
            bldr[slotDst - 1] = BndRecordNode.Create(typePrev, itemsPrev, namesPrev != null ? namesPrev.ToImmutable() : default);
        }

        if (slotDst == 1)
            return bldr != null ? bldr[0] : args[0];

        if (slotDst < lenSrc)
        {
            bldr ??= args.ToBuilder();
            bldr.RemoveTail(slotDst);
        }

        if (bldr == null)
            return bnd;
        Validation.Assert(!AreEquiv(bldr, bnd.Args));
        return BndVariadicOpNode.Create(bnd.Type, bnd.Op, bldr.ToImmutable(), default);
    }

    /// <summary>
    /// Reduce a sequence concat node. If <paramref name="bldr"/> is not null, it contains
    /// the reduced args (some of them are different than in <paramref name="bnd"/>).
    /// </summary>
    private BoundNode ReduceSeqConcat(BndVariadicOpNode bnd, ArgTuple.Builder bldr)
    {
        Validation.AssertValue(bnd);
        Validation.Assert(bnd.Op == BinaryOp.SeqConcat);
        Validation.Assert(bnd.Inverted.IsEmpty);
        var type = bnd.Type;
        Validation.Assert(type.IsSequence);

        var args = bnd.Args;
        Validation.Assert(args.Length >= 1);
        Validation.Assert(args.All(a => a.Type == type));
        Validation.Assert(bldr == null || bldr.Count == args.Length);
        Validation.Assert(bldr == null || bldr.All(a => a.Type == type));

        // Fully associative so flatten.
        BitSet invs = default;
        if (bldr != null)
            BndVariadicOpNode.Flatten(bnd.Op, ref bldr, ref invs);
        Validation.Assert(invs.IsEmpty);

        // REVIEW: Should we combine adjacent sequence literals?
        int lenSrc = bldr != null ? bldr.Count : args.Length;
        int ivDst = 0;
        for (int ivSrc = 0; ivSrc < lenSrc; ivSrc++)
        {
            var arg = bldr != null ? bldr[ivSrc] : args[ivSrc];
            Validation.Assert(arg.Type == type);

            var (min, max) = arg.GetItemCountRange();
            Validation.AssertIndexInclusive(min, max);
            if (max == 0)
                continue;

            if (ivDst < ivSrc)
            {
                bldr ??= args.ToBuilder();
                bldr[ivDst] = arg;
            }
            ivDst++;
        }

        if (ivDst == 0)
            return BndSequenceNode.CreateEmpty(type);

        if (ivDst == 1)
        {
            var arg = bldr != null ? bldr[0] : args[0];
            return arg;
        }

        if (ivDst < lenSrc)
        {
            bldr ??= args.ToBuilder();
            bldr.RemoveTail(ivDst);
        }

        if (bldr == null)
            return bnd;
        Validation.Assert(!AreEquiv(bldr, bnd.Args));
        return BndVariadicOpNode.Create(bnd.Type, bnd.Op, bldr.ToImmutable(), default);
    }

    /// <summary>
    /// Reduce a bool "or" or "and" node. If <paramref name="bldr"/> is not null, it contains
    /// the reduced args (some of them are different than in <paramref name="bnd"/>).
    /// </summary>
    private BoundNode ReduceOrAnd(BndVariadicOpNode bnd, ArgTuple.Builder bldr)
    {
        Validation.AssertValue(bnd);
        Validation.Assert(bnd.Op == BinaryOp.Or | bnd.Op == BinaryOp.And);
        Validation.Assert(bnd.Type.Kind == DKind.Bit);
        bool isOpt = bnd.Type.IsOpt;

        var args = bnd.Args;
        Validation.Assert(args.Length >= 2);
        Validation.Assert(args.All(a => a.Type.Kind == DKind.Bit));
        Validation.Assert(bldr == null || bldr.Count == args.Length);
        Validation.Assert(bldr == null || bldr.All(a => a.Type.Kind == DKind.Bit));

        // Fully associative and commutative so flatten and combine all the constants.
        BitSet invs = default;
        if (bldr != null)
            BndVariadicOpNode.Flatten(bnd.Op, ref bldr, ref invs);
        Validation.Assert(invs.IsEmpty);
        bool sink = bnd.Op == BinaryOp.Or;
        bool identity = !sink;

        // The tail, when it exists, will always be a constant null node.
        BoundNode tailNull = null;
        int lenSrc = bldr != null ? bldr.Count : args.Length;
        int ivDst = 0;
        for (int ivSrc = 0; ivSrc < lenSrc; ivSrc++)
        {
            var arg = bldr != null ? bldr[ivSrc] : args[ivSrc];
            Validation.Assert(arg.Type.Kind == DKind.Bit);
            Validation.Assert(isOpt | !arg.Type.IsOpt);

            if (arg.TryGetBoolOpt(out var val))
            {
                if (val == sink)
                    return arg.Type == bnd.Type ? arg : BndCastOptNode.Create(arg);
                if (val == null)
                    tailNull = arg;
                continue;
            }

            if (ivDst < ivSrc)
            {
                bldr ??= args.ToBuilder();
                bldr[ivDst] = arg;
            }
            ivDst++;
        }
        Validation.Assert(tailNull == null || tailNull.IsNullValue && tailNull.Type == DType.BitOpt);

        if (ivDst == 0)
        {
            if (tailNull != null)
                return tailNull;
            BoundNode res = BndIntNode.CreateBit(identity);
            return isOpt ? BndCastOptNode.Create(res) : res;
        }

        if (tailNull != null)
        {
            Validation.Assert(ivDst < lenSrc);
            // If valTail is already at the end, no need to change anything.
            if (bldr == null && ivDst == lenSrc - 1 && tailNull == args[lenSrc - 1])
                return bnd;
            bldr ??= args.ToBuilder();
            bldr[ivDst++] = tailNull;
        }

        if (ivDst == 1)
        {
            var res = bldr != null ? bldr[0] : args[0];
            Validation.Assert(res.Type.Kind == DKind.Bit);
            return isOpt && !res.Type.IsOpt ? BndCastOptNode.Create(res) : res;
        }

        if (ivDst < lenSrc)
        {
            bldr ??= args.ToBuilder();
            bldr.RemoveTail(ivDst);
        }

        if (bldr == null)
            return bnd;
        Validation.Assert(!AreEquiv(bldr, bnd.Args));
        return BndVariadicOpNode.Create(bnd.Type, bnd.Op, bldr.ToImmutable(), default);
    }

    /// <summary>
    /// Reduce a bitwise shift node. The <paramref name="arg0"/> and <paramref name="arg1"/>
    /// values are the reduced operands.
    /// </summary>
    private BoundNode ReduceShift(BndBinaryOpNode bnd, BoundNode arg0, BoundNode arg1)
    {
        Validation.AssertValue(bnd);
        Validation.Assert(bnd.Type.IsIntegralReq);
        Validation.Assert(bnd.Op == BinaryOp.Shl | bnd.Op == BinaryOp.Shri | bnd.Op == BinaryOp.Shru);
        Validation.AssertValue(arg0);
        Validation.AssertValue(arg1);
        Validation.Assert(arg0.Type == bnd.Type);
        Validation.Assert(arg1.Type == DType.I8Req);

        var type = bnd.Type;

        // Special cases:
        // * Shri on bit type: val (no-op)
        // * Val is 0: val (zero)
        // * Shri with val all ones: val (all ones)
        // * Amt is not positive: val
        // * Shift by too many bits:
        //   * Not shri: 0
        //   * Shri: high bit propagated through all (shri cbit - 1)

        // Shri on bit is a no-op.
        var kind = type.RootKind;
        bool oneFill = bnd.Op == BinaryOp.Shri;
        if (kind == DKind.Bit && oneFill)
            return arg0;

        // Any shift on a zero value is zero. Shri on all ones is all ones.
        bool c0 = arg0.TryGetIntegral(out var val);
        if (c0 && (val.IsZero || oneFill && val == BndIntNode.AllOnes(kind)))
            return arg0;

        if (arg1.TryGetIntegral(out var amtRaw))
        {
            // The amount is an i8.
            Validation.Assert(long.MinValue <= amtRaw);
            Validation.Assert(amtRaw <= long.MaxValue);
            long amt = (long)amtRaw;

            if (amt <= 0)
                return arg0;
            Validation.Assert(amt > 0);

            bool left = bnd.Op == BinaryOp.Shl;
            int cbRes = kind.NumericSize();
            int cbitRes = Math.Max(1, cbRes * 8);
            Validation.Assert(cbitRes > 0);
            Validation.Assert((cbitRes & (cbitRes - 1)) == 0);

            if (kind != DKind.IA && amt >= cbitRes)
            {
                // Shift is at least the number of bits, so the result is either 0 or all ones.
                if (!oneFill)
                    return BndIntNode.Create(type, 0);

                Validation.Assert(!left);
                Validation.Assert(cbitRes > 1);

                // Result is the high bit of arg0 propagated right (0 or all ones).
                if (c0)
                {
                    val = val.TestBit(cbitRes - 1) ? BndIntNode.AllOnes(kind) : 0;
                    return BndIntNode.Create(type, val);
                }

                // REVIEW: Is there a better way to optimize this?
                arg1 = BndIntNode.Create(DType.I8Req, cbitRes - 1);
                return BndBinaryOpNode.Create(type, bnd.Op, arg0, arg1);
            }
            Validation.Assert(kind == DKind.IA || amt < cbitRes);

            if (c0)
            {
                if (left)
                    val <<= BindUtil.ClipShift(amt);
                else
                {
                    if (kind != DKind.IA)
                    {
                        if (!oneFill && val.Sign < 0)
                        {
                            // Need to clear the high bits.
                            // REVIEW: Is there a better way to do this?
                            Validation.Assert(kind.IsIx());
                            val &= ((1UL << (cbitRes - 1) << 1) - 1);
                        }
                        else if (oneFill && val.Sign >= 0 && val.TestBit(cbitRes - 1))
                        {
                            // Need to set the high bits.
                            // REVIEW: Is there a better way to do this?
                            Validation.Assert(kind.IsUx());
                            val |= Integer.MinusOne << cbitRes;
                        }
                    }
                    val >>= BindUtil.ClipShift(amt);
                }

                // The result might be incorrect for the type, so we use CreateCast. Overflow doesn't matter.
                return BndIntNode.CreateCast(type, val, out _);
            }
        }

        if (arg0 == bnd.Arg0 && arg1 == bnd.Arg1)
            return bnd;
        return BndBinaryOpNode.Create(type, bnd.Op, arg0, arg1);
    }

    /// <summary>
    /// Reduce an integer div or mod node. The <paramref name="arg0"/> and <paramref name="arg1"/>
    /// values are the reduced operands.
    /// </summary>
    private BoundNode ReduceDivModInt(BndBinaryOpNode bnd, BoundNode arg0, BoundNode arg1)
    {
        Validation.AssertValue(bnd);
        Validation.Assert(bnd.Type.IsIntegralReq);
        Validation.Assert(bnd.Type.RootKind.NumericSize() >= 4);
        Validation.Assert(bnd.Op == BinaryOp.IntDiv | bnd.Op == BinaryOp.IntMod);
        Validation.AssertValue(arg0);
        Validation.AssertValue(arg1);
        Validation.Assert(arg0.Type == bnd.Type);
        Validation.Assert(arg1.Type == bnd.Type);

        bool div = bnd.Op == BinaryOp.IntDiv;
        var type = bnd.Type;

        bool c0 = arg0.TryGetIntegral(out var val0);
        if (c0 && val0.IsZero)
            return arg0;

        bool c1 = arg1.TryGetIntegral(out var val1);
        if (c1)
        {
            if (val1.IsZero)
            {
                _host.Warn(bnd, ErrorStrings.WrnIntDivZero);
                return arg1;
            }
            if (val1 == 1)
                return div ? arg0 : BndIntNode.Create(type, 0);
            if (val1 == -1)
            {
                if (!div)
                    return BndIntNode.Create(type, 0);

                // Negate the value.
                Validation.Assert(type.RootKind.IsSignedIntegral());
                if (c0)
                {
                    var res = BndIntNode.CreateCast(bnd.Type, -val0, out bool overflow);
                    if (overflow)
                        _host.Warn(bnd, ErrorStrings.WrnIntOverflow);
                    return res;
                }
                return NegateIntegral(arg0);
            }
            if (c0)
                return BndIntNode.Create(type, div ? val0 / val1 : val0 % val1);
        }

        if (arg0 == bnd.Arg0 && arg1 == bnd.Arg1)
            return bnd;
        return BndBinaryOpNode.Create(type, bnd.Op, arg0, arg1);
    }

    /// <summary>
    /// Reduce a power node. The <paramref name="arg0"/> and <paramref name="arg1"/>
    /// values are the reduced operands.
    /// </summary>
    private BoundNode ReducePower(BndBinaryOpNode bnd, BoundNode arg0, BoundNode arg1)
    {
        Validation.AssertValue(bnd);
        Validation.Assert(bnd.Type.IsNumericReq);
        Validation.Assert(bnd.Op == BinaryOp.Power);
        Validation.AssertValue(arg0);
        Validation.AssertValue(arg1);
        Validation.Assert(arg0.Type == bnd.Type);
        Validation.Assert(arg1.Type.IsNumericReq);

        var typeRes = bnd.Type;
        var kindRes = typeRes.RootKind;
        var typeExp = arg1.Type;
        var kindExp = typeExp.RootKind;
        switch (kindRes)
        {
        case DKind.I8:
        case DKind.U8:
            Validation.Assert(kindExp == DKind.I8 || kindExp == DKind.U8);
            if (arg1.TryGetIntegral(out var expInt))
            {
                if (expInt == 1)
                    return arg0;
                if (expInt <= 0)
                    return BndIntNode.Create(typeRes, 1);
                if (arg0.TryGetIntegral(out var val0))
                {
                    var res = kindRes == DKind.I8 ?
                        (Integer)NumUtil.IntPow((long)val0, (ulong)expInt) :
                        (Integer)NumUtil.IntPow((ulong)val0, (ulong)expInt);
                    return BndIntNode.Create(typeRes, res);
                }
            }
            break;

        case DKind.R8:
            Validation.Assert(kindExp == DKind.R8);
            if (arg1.TryGetFractional(out var val1))
            {
                if (val1 == 1)
                    return arg0;
                if (arg0.TryGetFractional(out var val0))
                    return BndFltNode.CreateCast(typeRes, Math.Pow(val0, val1));
                // REVIEW: Should we reduce x**-1 to Mul([/] x)? Values might be slightly different.
            }
            break;

        default:
            Validation.Assert(false);
            break;
        }


        if (arg0 == bnd.Arg0 && arg1 == bnd.Arg1)
            return bnd;
        return BndBinaryOpNode.Create(bnd.Type, bnd.Op, arg0, arg1);
    }

    /// <summary>
    /// Reduce a min or max node. The <paramref name="arg0"/> and <paramref name="arg1"/>
    /// values are the reduced operands.
    /// </summary>
    private BoundNode ReduceMinMax(BndBinaryOpNode bnd, BoundNode arg0, BoundNode arg1)
    {
        Validation.AssertValue(bnd);
        Validation.Assert(!bnd.Type.IsSequence);
        Validation.Assert(!bnd.Type.HasReq);
        Validation.Assert(bnd.Type.IsComparable);
        Validation.Assert(bnd.Op == BinaryOp.Min | bnd.Op == BinaryOp.Max);
        Validation.AssertValue(arg0);
        Validation.AssertValue(arg1);
        Validation.Assert(arg0.Type == bnd.Type);
        Validation.Assert(arg1.Type == bnd.Type);

        bool min = bnd.Op == BinaryOp.Min;
        if (arg0.TryGetIntegral(out var i0) && arg1.TryGetIntegral(out var i1))
        {
            var val = min ? Integer.Min(i0, i1) : Integer.Max(i0, i1);
            Validation.Assert(val == i0 || val == i1);
            return val == i0 ? arg0 : arg1;
        }
        if (arg0.TryGetFractional(out var d0) && arg1.TryGetFractional(out var d1))
        {
            var val = min ? Math.Min(d0, d1) : Math.Max(d0, d1);
            Validation.Assert(val.ToBits() == d0.ToBits() || val.ToBits() == d1.ToBits());
            return val.ToBits() == d0.ToBits() ? arg0 : arg1;
        }
        if (arg0.TryGetString(out var s0) && arg1.TryGetString(out var s1))
        {
            var val = min ? StrComparer.Min(s0, s1) : StrComparer.Max(s0, s1);
            Validation.Assert(val == s0 || val == s1);
            return val == s0 ? arg0 : arg1;
        }

        if (arg0 == bnd.Arg0 && arg1 == bnd.Arg1)
            return bnd;
        return BndBinaryOpNode.Create(bnd.Type, bnd.Op, arg0, arg1);
    }

    /// <summary>
    /// Reduce a coalesce node. The <paramref name="arg0"/> and <paramref name="arg1"/>
    /// values are the reduced operands.
    /// </summary>
    private BoundNode ReduceCoalesce(BndBinaryOpNode bnd, BoundNode arg0, BoundNode arg1)
    {
        Validation.AssertValue(bnd);
        Validation.Assert(bnd.Op == BinaryOp.Coalesce);
        Validation.AssertValue(arg0);
        Validation.AssertValue(arg1);
        Validation.Assert(arg0.Type == bnd.Type || arg0.Type == bnd.Type.ToOpt());
        Validation.Assert(arg1.Type == bnd.Type);

        var type = bnd.Type;
        if (arg0.IsConstant)
        {
            if (arg0.IsNullValue)
                return arg1;

            if (type == arg0.Type)
                return arg0;

            Validation.Assert(type == arg0.Type.ToReq());
            if (arg0 is BndCastOptNode bon)
            {
                Validation.Assert(bon.Child.Type == type);
                return bon.Child;
            }

            if (arg0 is BndCastRefNode brn && !brn.Child.Type.IsOpt)
            {
                var typeSrc = brn.Child.Type;
                if (typeSrc == type)
                    return brn.Child;
                Validation.Assert(Conversion.IsRefConv(typeSrc, type));
                return BndCastRefNode.Create(brn.Child, type);
            }

            // I don't think this can happen. If it does, just fall through.
            Validation.Assert(false);
        }

        if (arg1.IsNullValue)
        {
            Validation.Assert(arg0.Type == type);
            return arg0;
        }

        if (arg0 is BndCastOptNode cop)
        {
            Validation.Assert(!cop.Child.Type.IsOpt);
            var res = cop.Child;
            if (type == res.Type)
                return res;
            Validation.Assert(type == cop.Type);
            return cop;
        }

        if (arg0 == bnd.Arg0 && arg1 == bnd.Arg1)
            return bnd;
        return BndBinaryOpNode.Create(type, bnd.Op, arg0, arg1);
    }

    /// <summary>
    /// Reduce a has node. The <paramref name="arg0"/> and <paramref name="arg1"/>
    /// values are the reduced operands.
    /// </summary>
    private BoundNode ReduceHas(BndBinaryOpNode bnd, BoundNode arg0, BoundNode arg1)
    {
        Validation.AssertValue(bnd);
        Validation.Assert(bnd.Type == DType.BitReq);
        Validation.Assert(bnd.Op == BinaryOp.Has | bnd.Op == BinaryOp.HasCi | bnd.Op == BinaryOp.HasNot | bnd.Op == BinaryOp.HasCiNot);
        Validation.AssertValue(arg0);
        Validation.AssertValue(arg1);
        Validation.Assert(arg0.Type == DType.Text);
        Validation.Assert(arg1.Type == DType.Text);

        if (arg1.TryGetString(out var s1))
        {
            bool not = bnd.Op == BinaryOp.HasNot || bnd.Op == BinaryOp.HasCiNot;
            bool ci = bnd.Op == BinaryOp.HasCi || bnd.Op == BinaryOp.HasCiNot;

            if (string.IsNullOrEmpty(s1))
            {
                // s1 is empty, so the answer is true.
                return BndIntNode.CreateBit(true ^ not);
            }

            if (arg0.TryGetString(out var s0))
            {
                // The cases when s1 is null or empty are handled above.
                Validation.Assert(!string.IsNullOrEmpty(s1));

                bool res;

                if (string.IsNullOrEmpty(s0))
                    res = false;
                else
                    res = s0.IndexOf(s1, ci ? StringComparison.InvariantCultureIgnoreCase : StringComparison.Ordinal) >= 0;
                return BndIntNode.CreateBit(res ^ not);
            }
        }

        // Note that we can't do any reduction if arg0 is constant but arg1 is not.
        // The best we could do is reduce 's has x' to 'x = "" or x = null' when s is null or empty.

        if (arg0 == bnd.Arg0 && arg1 == bnd.Arg1)
            return bnd;
        return BndBinaryOpNode.Create(bnd.Type, bnd.Op, arg0, arg1);
    }

    /// <summary>
    /// Reduce a compare node. The operands have not been reduced so this method must do that.
    /// </summary>
    private BoundNode ReduceCompare(BndCompareNode bnd, int idx)
    {
        Validation.AssertValue(bnd);
        Validation.Assert(bnd.Type == DType.BitReq);

        // REVIEW: We should be able to reduce many more expressions, for example if x:i4, then x == 3.5
        // should reduce to false. Note that C# doesn't do this. Another example: when a type has a max or min
        // value, x > max and x < min could reduce to false. We currently only do this kind of reduction for
        // floating point.

        // REVIEW: We can also do many reductions involving index scopes. These reductions would give
        // better reduction behavior for Take/Drop and related. For example (also consider negations):
        //   # <  (non-positive): false
        //   # >  (negative)    : true
        //   # =  (negative)    : false
        //   # <= long.MaxValue : true
        //
        // More reductions are possible when considering the bounds on the iteration that the index applies to.
        // For example, let S be a sequence that introduces an index scope. Then, S.GetItemCountRange() can be
        // used to determine the min and max length of S. This info allows stronger reductions like
        //   # >= c >= max       : false
        //   # <  c <= min       : true
        //
        // It would also be helpful for this class of reductions to reduce 'not X op Y' to 'X !op Y'.

        var args = bnd.Args;
        var ops = bnd.Ops;
        Validation.Assert(args.Length >= 2);
        Validation.Assert(ops.Length == args.Length - 1);

        Func<BoundNode, (bool, object)> getConst;
        Func<CompareOp, object, object, bool> cmpValues;
        Validation.Assert(bnd.ArgType.IsEquatable);
        switch (bnd.ArgKind)
        {
        case DKind.Text:
            getConst = GetStringObj;
            cmpValues = CmpStringObj;
            break;
        case DKind.R8:
        case DKind.R4:
            getConst = GetFractionalObj;
            cmpValues = CmpFractionalObj;
            break;
        case DKind.IA:
        case DKind.I8:
        case DKind.I4:
        case DKind.I2:
        case DKind.I1:
        case DKind.U8:
        case DKind.U4:
        case DKind.U2:
        case DKind.U1:
        case DKind.Bit:
            getConst = GetIntegralObj;
            cmpValues = CmpValObj<Integer>;
            break;
        case DKind.Date:
            // The value is the number of ticks.
            getConst = GetValObj<long>;
            cmpValues = CmpValObj<long>;
            break;
        case DKind.Time:
            // The value is the number of ticks.
            getConst = GetValObj<long>;
            cmpValues = CmpValObj<long>;
            break;
        case DKind.Guid:
            getConst = GetValObj<Guid>;
            cmpValues = CmpValObj<Guid>;
            break;
        default:
            // These handle null values.
            // REVIEW: Could drill into BndRecordNode and BndTupleNode.
            // REVIEW: Could also handle BndDefaultNode.
            getConst = GetXxxObj;
            cmpValues = CmpXxxObj;
            break;
        }

        // We reduce all, even if we detect early that the result is false, so all warnings are generated.
        int cur = idx + 1;
        int len = args.Length;

        // Pull withs when there are only two args.
        var with = new WithInfo(this, disabled: len != 2);
        var bldr = with.Process(args, ref cur);
        Validation.Assert(bldr == null || bldr.Count == len);
        Validation.Assert(cur == idx + bnd.NodeCount);

        var nullTos = bnd.NullToFlags;
        var isOpt = bnd.IsOpt;
        if (!bnd.ArgType.IsOpt && !isOpt.IsEmpty)
        {
            // Remove any CastOpt nodes, then recompute the NullTo values.
            bool any = false;
            for (int i = 0; i < args.Length; i++)
            {
                var a = bldr != null ? bldr[i] : args[i];
                Validation.Assert(isOpt.TestBit(i) == a.Type.IsOpt);
                if (a is BndCastOptNode bco)
                {
                    Validation.Assert(isOpt.TestBit(i));
                    Validation.Assert(!bco.Child.Type.IsOpt);
                    (bldr ??= args.ToBuilder())[i] = bco.Child;
                    any = true;
                    isOpt = isOpt.ClearBit(i);
                }
            }
            if (any)
                nullTos = BndCompareNode.ComputeNullTos(ops, isOpt);
        }

        Immutable.Array<CompareOp>.Builder bldrOps = null;
        var arg = bldr != null ? bldr[0] : args[0];
        var nullToCur = nullTos[0];
        (bool isConstCur, object valueCur) = getConst(arg);
        if ((nullToCur & NullTo.False) != 0 && isConstCur && valueCur is null)
            return BndIntNode.False;

        for (int i = 1; i < len; i++)
        {
            var prev = arg;
            var (nullToPrev, isConstPrev, valuePrev) = (nullToCur, isConstCur, valueCur);

            arg = bldr != null ? bldr[i] : args[i];
            nullToCur = nullTos[i];
            (isConstCur, valueCur) = getConst(arg);
            if ((nullToCur & NullTo.False) != 0 && isConstCur && valueCur is null)
                return BndIntNode.False;

            var op = ops[i - 1];
            if (op == CompareOp.None)
                continue;

            Validation.Assert(!op.IsOrdered || bnd.ArgType.IsComparable);

            // REVIEW: The binder should have simplified for type, but a hand-crafted BndCompareNode
            // could still come through here.
            var opNew = op.SimplifyForType(prev.Type, arg.Type);
            if (opNew != op)
                (bldrOps ??= ops.ToBuilder())[i - 1] = op = opNew;

            // Try to simplify. False is universal and true can be dropped.
            // REVIEW: Should we handle more types that have min/max values, eg bool?
            bool drop = false;
            if (isConstCur)
            {
                if (isConstPrev)
                {
                    // Both constant so determine the value.
                    if (!CmpObj(op, valuePrev, valueCur, cmpValues))
                        return BndIntNode.False;
                    drop = true;
                }
                else if (valueCur is null)
                {
                    Validation.Assert(isOpt.TestBit(i));
                    // NullTo.False should have been handled above.
                    Validation.Assert((nullToCur & NullTo.False) == 0);
                    if ((nullToCur & NullTo.TrueRightAbs) != 0)
                        drop = true;
                }
                else if (valueCur is double v)
                {
                    if (double.IsNaN(v))
                    {
                        if (op.IsStrict)
                        {
                            if (!op.IsNot)
                                return BndIntNode.False;
                            if (!isOpt.TestBit(i - 1) || (nullToPrev & NullTo.False) == 0)
                                drop = true;
                        }
                        else if (!isOpt.TestBit(i - 1))
                        {
                            Validation.Assert(!op.IsNot || op.Root == CompareRoot.Equal);
                            switch (op.Root)
                            {
                            case CompareRoot.Less:
                                return BndIntNode.False;
                            case CompareRoot.GreaterEqual:
                                drop = true;
                                break;
                            }
                        }
                    }
                    else if (double.IsPositiveInfinity(v))
                    {
                        switch (op.Root)
                        {
                        case CompareRoot.Greater:
                            // x [$]> inf is false.
                            if (!op.IsNot)
                                return BndIntNode.False;
                            drop = true;
                            break;
                        case CompareRoot.LessEqual:
                            drop = !op.IsStrict;
                            break;
                        }
                    }
                    else if (double.IsNegativeInfinity(v) && op.IsStrict)
                    {
                        switch (op.Root)
                        {
                        case CompareRoot.Less:
                            // x $< -inf is false.
                            if (!op.IsNot)
                                return BndIntNode.False;
                            drop = true;
                            break;
                        }
                    }
                }
            }
            else if (isConstPrev)
            {
                if (valuePrev is null)
                {
                    Validation.Assert(isOpt.TestBit(i - 1));
                    // NullTo.False should have been handled above.
                    Validation.Assert((nullToPrev & NullTo.False) == 0);
                    if ((nullToPrev & NullTo.TrueLeftAbs) != 0)
                        drop = true;
                }
                else if (valuePrev is double v)
                {
                    if (double.IsNaN(v))
                    {
                        if (op.IsStrict)
                        {
                            if (!op.IsNot)
                                return BndIntNode.False;
                            if (!isOpt.TestBit(i) || (nullToCur & NullTo.False) == 0)
                                drop = true;
                        }
                        else if (!isOpt.TestBit(i))
                        {
                            Validation.Assert(!op.IsNot || op.Root == CompareRoot.Equal);
                            switch (op.Root)
                            {
                            case CompareRoot.Greater:
                                return BndIntNode.False;
                            case CompareRoot.LessEqual:
                                drop = true;
                                break;
                            }
                        }
                    }
                    else if (double.IsPositiveInfinity(v))
                    {
                        switch (op.Root)
                        {
                        case CompareRoot.Less:
                            // inf [$]< x is false.
                            if (!op.IsNot)
                                return BndIntNode.False;
                            drop = true;
                            break;
                        case CompareRoot.GreaterEqual:
                            drop = !op.IsStrict;
                            break;
                        }
                    }
                }
            }

            if (drop)
            {
                // Known true, so set the operator to none.
                (bldrOps ??= ops.ToBuilder())[i - 1] = CompareOp.None;
            }
        }

        // Drop any args that have None on both sides.
        bool isNoneBefore = true;
        nullToCur = NullTo.None;
        for (int i = len; --i >= 0;)
        {
            bool isNoneAfter = isNoneBefore;
            isNoneBefore = i == 0 || (bldrOps != null ? bldrOps[i - 1] : ops[i - 1]) == CompareOp.None;
            var nullToNew = nullTos[i];
            if (isNoneAfter && isNoneBefore)
            {
                bldr ??= args.ToBuilder();
#if DEBUG
                // We shouldn't be throwing away an operand that sends null to false unless it is a
                // non-null constant.
                var a = bldr[i];
                if (a.Type.IsOpt && (nullToNew & NullTo.False) != 0)
                    Validation.Assert(a.IsNonNullConstant);
#endif
                bldr.RemoveAt(i);
                if (bldr.Count <= 1)
                {
                    // The entire thing is true.
                    // We shouldn't be throwing away an operand that sends null to false.
#if DEBUG
                    var nullTo = (i == 0) ? nullToCur : nullTos[0];
                    var b = bldr[0];
                    if (b.Type.IsOpt && (nullTo & NullTo.False) != 0)
                        Validation.Assert(b.IsNonNullConstant);
#endif
                    return BndIntNode.True;
                }
                bldrOps ??= ops.ToBuilder();
                bldrOps.RemoveAt(Math.Min(i, bldrOps.Count - 1));
                Validation.Assert(bldr.Count == bldrOps.Count + 1);
            }
            else
                nullToCur = nullToNew;
        }

        BndCompareNode ret;
        if (bldrOps == null)
            ret = bldr == null ? bnd : BndCompareNode.Create(ops, bldr.ToImmutable());
        else if (bldr == null)
            ret = BndCompareNode.Create(bldrOps.ToImmutable(), args);
        else
        {
            Validation.Assert(bldr.Count >= 2);
            ret = BndCompareNode.Create(bldrOps.ToImmutable(), bldr.ToImmutable());
        }

        Validation.Coverage(with.HasAny ? 0 : 1);
        return with.Apply(ret);
    }

    #region Comparison functions, strongly typed.

    /// <summary>
    /// Compare double values. Note that this follows IEEE 754 semantics, where NaN compared with
    /// anything is false, positive and negative zero are considered equal, etc. When one of the values
    /// is NaN, the only operation that produces true is "None", which means, don't compare the values.
    /// </summary>
    private static bool Cmp(CompareOp op, double v0, double v1)
    {
        var (root, mods) = op.GetParts();
        bool strict = mods.IsStrict();
        bool res;
        switch (root)
        {
        default:
            Validation.Assert(root == CompareRoot.None);
            return true;
        case CompareRoot.Equal: res = strict ? v0 == v1 : NumUtil.Eq(v0, v1); break;
        case CompareRoot.Less: res = strict ? v0 < v1 : NumUtil.Lt(v0, v1); break;
        case CompareRoot.LessEqual: res = strict ? v0 <= v1 : NumUtil.Le(v0, v1); break;
        case CompareRoot.Greater: res = strict ? v0 > v1 : NumUtil.Gt(v0, v1); break;
        case CompareRoot.GreaterEqual: res = strict ? v0 >= v1 : NumUtil.Ge(v0, v1); break;
        }
        if (mods.IsNot())
            res = !res;
        return res;
    }

    /// <summary>
    /// Compare values of type <typeparamref name="T"/>. This ignores strictness (caller should
    /// make sure it isn't an issue).
    /// </summary>
    private static bool Cmp<T>(CompareOp op, T v0, T v1)
        where T : struct, IComparable<T>, IEquatable<T>
    {
        var (root, mods) = op.GetParts();
        bool res;
        switch (root)
        {
        default:
            Validation.Assert(root == CompareRoot.None);
            return true;
        case CompareRoot.Equal: res = v0.Equals(v1); break;
        case CompareRoot.Less: res = v0.CompareTo(v1) < 0; break;
        case CompareRoot.LessEqual: res = v0.CompareTo(v1) <= 0; break;
        case CompareRoot.Greater: res = v0.CompareTo(v1) > 0; break;
        case CompareRoot.GreaterEqual: res = v0.CompareTo(v1) >= 0; break;
        }
        if (mods.IsNot())
            res = !res;
        return res;
    }

    /// <summary>
    /// Compare string values. The caller has already handled null values so strictness
    /// is inconsequential.
    /// </summary>
    private static bool Cmp(CompareOp op, string v0, string v1)
    {
        Validation.AssertValue(v0);
        Validation.AssertValue(v1);
        var (root, mods) = op.GetParts();
        bool res;
        if (mods.IsCi())
        {
            switch (root)
            {
            default:
                Validation.Assert(root == CompareRoot.None);
                return true;
            case CompareRoot.Equal: res = StrComparer.EqCi(v0, v1); break;
            case CompareRoot.Less: res = StrComparer.LtCi(v0, v1); break;
            case CompareRoot.LessEqual: res = StrComparer.LeCi(v0, v1); break;
            case CompareRoot.Greater: res = StrComparer.GtCi(v0, v1); break;
            case CompareRoot.GreaterEqual: res = StrComparer.GeCi(v0, v1); break;
            }
        }
        else
        {
            switch (root)
            {
            default:
                Validation.Assert(root == CompareRoot.None);
                return true;
            case CompareRoot.Equal: res = string.Equals(v0, v1); break;
            case CompareRoot.Less: res = StrComparer.Lt(v0, v1); break;
            case CompareRoot.LessEqual: res = StrComparer.Le(v0, v1); break;
            case CompareRoot.Greater: res = StrComparer.Gt(v0, v1); break;
            case CompareRoot.GreaterEqual: res = StrComparer.Ge(v0, v1); break;
            }
        }
        if (mods.IsNot())
            res = !res;
        return res;
    }

    #endregion Comparison functions, strongly typed.

    #region Comparison methods, object-based.

    private bool CmpObj(CompareOp op, object v0, object v1, Func<CompareOp, object, object, bool> cmpCore)
    {
        if (op == CompareOp.None)
            return true;

        if (op.IsStrict)
        {
            if (v0 is null || v1 is null)
                return op.IsNot;
        }
        else
        {
            bool not = op.IsNot;
            if (v0 is null)
            {
                switch (op.Root)
                {
                case CompareRoot.Equal:
                case CompareRoot.GreaterEqual:
                    return (v1 is null) ^ not;
                case CompareRoot.Less:
                    return (v1 is not null) ^ not;
                case CompareRoot.LessEqual:
                    return true ^ not;
                case CompareRoot.Greater:
                    return false ^ not;
                }
                Validation.Assert(false);
            }
            if (v1 is null)
            {
                switch (op.Root)
                {
                case CompareRoot.Equal:
                case CompareRoot.LessEqual:
                    return (v0 is null) ^ not;
                case CompareRoot.Greater:
                    return (v0 is not null) ^ not;
                case CompareRoot.GreaterEqual:
                    return true ^ not;
                case CompareRoot.Less:
                    return false ^ not;
                }
                Validation.Assert(false);
            }
        }

        Validation.Assert(v0 is not null);
        Validation.Assert(v1 is not null);
        return cmpCore(op, v0, v1);
    }

    private bool CmpFractionalObj(CompareOp op, object v0, object v1)
    {
        Validation.Assert(op.Root != CompareRoot.None);
        Validation.Assert(!op.IsCi);
        Validation.Assert(v0 is double);
        Validation.Assert(v1 is double);
        return Cmp(op, (double)v0, (double)v1);
    }

    private bool CmpValObj<T>(CompareOp op, object v0, object v1)
        where T : struct, IComparable<T>, IEquatable<T>
    {
        Validation.Assert(op.Root != CompareRoot.None);
        Validation.Assert(!op.IsCi);
        Validation.Assert(v0 is T);
        Validation.Assert(v1 is T);
        return Cmp(op, (T)v0, (T)v1);
    }

    private bool CmpStringObj(CompareOp op, object v0, object v1)
    {
        Validation.Assert(op.Root != CompareRoot.None);
        Validation.Assert(v0 is string);
        Validation.Assert(v1 is string);
        return Cmp(op, (string)v0, (string)v1);
    }

    private bool CmpXxxObj(CompareOp op, object v0, object v1)
    {
        // Can't get here. The caller handles null values and the corresponding GetXxxObj
        // only fetches null values.
        Validation.Assert(v0 is not null);
        Validation.Assert(v1 is not null);
        Validation.Assert(false);
        return false;
    }

    #endregion Comparison methods, object-based.

    #region Object-based get-constant methods.

    // These are helpers for CompareNode reduction. They return a bool indicating whether
    // the BoundNode is constant (of the correct kind(s)) and, if so, the boxed value.

    private (bool, object) GetFractionalObj(BoundNode bnd)
    {
        Validation.AssertValue(bnd);
        Validation.Assert(bnd.Type.Kind.IsFractional());
        return (bnd.TryGetFractionalOpt(out var value), value);
    }

    private (bool, object) GetIntegralObj(BoundNode bnd)
    {
        Validation.AssertValue(bnd);
        Validation.Assert(bnd.Type.Kind.IsIntegral());
        return (bnd.TryGetIntegralOpt(out var value), value);
    }

    private (bool, object) GetValObj<T>(BoundNode bnd)
        where T : struct
    {
        Validation.AssertValue(bnd);
        if (bnd.IsKnownNull)
            return (true, null);
        if (bnd is BndDefaultNode)
            return (true, default(T));
        return (false, null);
    }

    private (bool, object) GetStringObj(BoundNode bnd)
    {
        Validation.AssertValue(bnd);
        Validation.Assert(bnd.Type.Kind == DKind.Text);
        return (bnd.TryGetString(out var value), value);
    }

    // For kinds that don't have a literal form as a bound node.
    private (bool, object) GetXxxObj(BoundNode bnd)
    {
        Validation.AssertValue(bnd);
        return (bnd.IsKnownNull, null);
    }

    #endregion Object-based get-constant methods.

    #region Binary Operator Helpers

    /// <summary>
    /// Details of an associative commutative binary operator whose representation uses
    /// <see cref="BndVariadicOpNode"/>. Each such operator has an identity value. Some
    /// operators have an "inverse" operation, eg, subtraction for addition. Note that
    /// floating point operations are not associative, so this is not used for them.
    /// </summary>
    /// <typeparam name="T">The system type of the constant representation, eg, bool, double, Integer.</typeparam>
    private abstract class AssociativeBop<T>
    {
        protected AssociativeBop(BinaryOp bop, DKind kind, T identity)
        {
            Op = bop;
            Kind = kind;
            Identity = identity;
        }

        /// <summary>The operator.</summary>
        public BinaryOp Op { get; }

        /// <summary>The kind of operand.</summary>
        public DKind Kind { get; }

        /// <summary>The identity value.</summary>
        public T Identity { get; }

        /// <summary>
        /// Apply the operator (or its inverse, if indicated) to <paramref name="a"/> and <paramref name="b"/>
        /// and store the result in <paramref name="a"/>.
        /// </summary>
        public abstract void Apply(ref T a, T b, bool inv);

        /// <summary>
        /// Trim the value in <paramref name="a"/> to be within the range of the <see cref="Kind"/> and
        /// store the result in <paramref name="a"/>. If the value changes, this returns true after
        /// possibly generating a warning (sending it to <paramref name="host"/>).
        /// </summary>
        public abstract bool Trim(ref T a, ref bool inv, bool invOk, IReducerHost host, BoundNode src);

        /// <summary>
        /// For a numeric type, return the negation. For other types, throw.
        /// </summary>
        public abstract T Negate(T a);

        /// <summary>
        /// Return whether <paramref name="a"/> is the identity value.
        /// </summary>
        public abstract bool IsIdentity(T a);

        /// <summary>
        /// Return whether <paramref name="a"/> is the sink value (if there is one). For example,
        /// zero is a sink for integral multiplication, zero is a sink for bitwise-and, and all ones
        /// is a sink for bitwise-or.
        /// </summary>
        public abstract bool IsSink(T a);

        /// <summary>
        /// If <paramref name="bnd"/> is a constant, set <paramref name="val"/> to its value and
        /// return true.
        /// </summary>
        public abstract bool TryGetConst(BoundNode bnd, out T val);

        /// <summary>
        /// Create a constant node with the given value.
        /// </summary>
        public abstract BoundNode CreateConst(T val);
    }

    /// <summary>
    /// Base class for integral associative commutative operators.
    /// </summary>
    private abstract class IntBop : AssociativeBop<Integer>
    {
        protected readonly DType _type;

        protected IntBop(BinaryOp bop, DKind kind, Integer identity)
            : base(bop, kind, identity)
        {
            _type = DType.GetNumericType(Kind);
            Validation.Assert(Cast(0) == 0);
        }

        /// <summary>
        /// Asserts that <paramref name="a"/> fits in the <see cref="Kind"/>.
        /// </summary>
        protected Integer V(Integer a)
        {
            Validation.Assert(Cast(a) == a);
            return a;
        }

        /// <summary>
        /// Casts <paramref name="a"/> to a value that fits in <see cref="Kind"/>.
        /// </summary>
        protected Integer Cast(Integer a)
        {
            switch (Kind)
            {
            case DKind.I1: return NumUtil.CastI1(a);
            case DKind.I2: return NumUtil.CastI2(a);
            case DKind.I4: return NumUtil.CastI4(a);
            case DKind.I8: return NumUtil.CastI8(a);
            case DKind.Bit: return NumUtil.CastBit(a);
            case DKind.U1: return NumUtil.CastU1(a);
            case DKind.U2: return NumUtil.CastU2(a);
            case DKind.U4: return NumUtil.CastU4(a);
            case DKind.U8: return NumUtil.CastU8(a);
            default:
                Validation.Assert(Kind == DKind.IA);
                return a;
            }
        }

        public override bool Trim(ref Integer a, ref bool inv, bool invOk, IReducerHost host, BoundNode src)
        {
            bool changed = false;
            if (inv && !invOk)
            {
                Validation.Assert(Op == BinaryOp.Add);
                a = -a;
                inv = false;
                changed = true;
            }

            var res = Cast(a);
            if (res == a)
                return changed;

            if (invOk && Op == BinaryOp.Add && a < 0)
            {
                // See if the absolute value is in range.
                var res2 = Cast(-a);
                if (res2 == -a)
                {
                    a = -a;
                    inv = !inv;
                    return true;
                }
            }

            host.Warn(src, ErrorStrings.WrnIntOverflow);
            a = res;
            return true;
        }

        public override Integer Negate(Integer a)
        {
            // Should only be called for multiplication.
            Validation.Assert(Op == BinaryOp.Mul);
            return -a;
        }

        public override bool TryGetConst(BoundNode bnd, out Integer val)
        {
            Validation.AssertValue(bnd);
            Validation.Assert(bnd.Type == _type);
            if (!bnd.TryGetIntegral(out val))
                return false;
            Validation.Assert(Cast(val) == val);
            return true;
        }

        public override BoundNode CreateConst(Integer val)
        {
            return BndIntNode.Create(_type, V(val));
        }
    }

    /// <summary>
    /// Addition and subtraction.
    /// </summary>
    private sealed class AddIntBop : IntBop
    {
        private static readonly AddIntBop _i8 = new AddIntBop(DKind.I8);
        private static readonly AddIntBop _u8 = new AddIntBop(DKind.U8);
        private static readonly AddIntBop _ia = new AddIntBop(DKind.IA);

        private AddIntBop(DKind kind)
            : base(BinaryOp.Add, kind, 0)
        {
        }

        /// <summary>
        /// Get the operator instance for the given <paramref name="kind"/>.
        /// </summary>
        public static AddIntBop FromKind(DKind kind)
        {
            switch (kind)
            {
            case DKind.I8: return _i8;
            case DKind.U8: return _u8;
            case DKind.IA: return _ia;
            }
            throw Validation.BugExcept();
        }

        public override void Apply(ref Integer a, Integer b, bool inv)
        {
            if (inv)
                a -= V(b);
            else
                a += V(b);
        }

        public override bool IsIdentity(Integer a) => a == 0;
        public override bool IsSink(Integer a) => false;
    }

    /// <summary>
    /// Multiplication (no inverse).
    /// </summary>
    private sealed class MulIntBop : IntBop
    {
        private static readonly MulIntBop _i8 = new MulIntBop(DKind.I8);
        private static readonly MulIntBop _u8 = new MulIntBop(DKind.U8);
        private static readonly MulIntBop _ia = new MulIntBop(DKind.IA);

        private MulIntBop(DKind kind)
            : base(BinaryOp.Mul, kind, 1)
        {
        }

        /// <summary>
        /// Get the operator instance for the given <paramref name="kind"/>.
        /// </summary>
        public static MulIntBop FromKind(DKind kind)
        {
            switch (kind)
            {
            case DKind.I8: return _i8;
            case DKind.U8: return _u8;
            case DKind.IA: return _ia;
            }
            throw Validation.BugExcept();
        }

        public override void Apply(ref Integer a, Integer b, bool inv)
        {
            Validation.Assert(!inv);
            a *= V(b);
        }

        public override bool IsIdentity(Integer a) => a == 1;
        public override bool IsSink(Integer a) => a == 0;
    }

    /// <summary>
    /// Bitwise-or (no inverse).
    /// </summary>
    private sealed class OrIntBop : IntBop
    {
        private static readonly OrIntBop _ia = new OrIntBop(DKind.IA);
        private static readonly OrIntBop _i8 = new OrIntBop(DKind.I8);
        private static readonly OrIntBop _i4 = new OrIntBop(DKind.I4);
        private static readonly OrIntBop _i2 = new OrIntBop(DKind.I2);
        private static readonly OrIntBop _i1 = new OrIntBop(DKind.I1);
        private static readonly OrIntBop _u8 = new OrIntBop(DKind.U8);
        private static readonly OrIntBop _u4 = new OrIntBop(DKind.U4);
        private static readonly OrIntBop _u2 = new OrIntBop(DKind.U2);
        private static readonly OrIntBop _u1 = new OrIntBop(DKind.U1);
        private static readonly OrIntBop _b = new OrIntBop(DKind.Bit);

        private OrIntBop(DKind kind)
            : base(BinaryOp.BitOr, kind, 0)
        {
        }

        /// <summary>
        /// Get the operator instance for the given <paramref name="kind"/>.
        /// </summary>
        public static OrIntBop FromKind(DKind kind)
        {
            switch (kind)
            {
            case DKind.IA: return _ia;
            case DKind.I8: return _i8;
            case DKind.I4: return _i4;
            case DKind.I2: return _i2;
            case DKind.I1: return _i1;
            case DKind.U8: return _u8;
            case DKind.U4: return _u4;
            case DKind.U2: return _u2;
            case DKind.U1: return _u1;
            case DKind.Bit: return _b;
            }
            throw Validation.BugExcept();
        }

        public override void Apply(ref Integer a, Integer b, bool inv)
        {
            Validation.Assert(!inv);
            a |= b;
        }

        public override bool IsIdentity(Integer a) => a == 0;
        public override bool IsSink(Integer a) => a == BndIntNode.AllOnes(Kind);
    }

    /// <summary>
    /// Bitwise-and (no inverse).
    /// </summary>
    private sealed class AndIntBop : IntBop
    {
        private static readonly AndIntBop _ia = new AndIntBop(DKind.IA);
        private static readonly AndIntBop _i8 = new AndIntBop(DKind.I8);
        private static readonly AndIntBop _i4 = new AndIntBop(DKind.I4);
        private static readonly AndIntBop _i2 = new AndIntBop(DKind.I2);
        private static readonly AndIntBop _i1 = new AndIntBop(DKind.I1);
        private static readonly AndIntBop _u8 = new AndIntBop(DKind.U8);
        private static readonly AndIntBop _u4 = new AndIntBop(DKind.U4);
        private static readonly AndIntBop _u2 = new AndIntBop(DKind.U2);
        private static readonly AndIntBop _u1 = new AndIntBop(DKind.U1);
        private static readonly AndIntBop _b = new AndIntBop(DKind.Bit);

        private AndIntBop(DKind kind)
            : base(BinaryOp.BitAnd, kind, BndIntNode.AllOnes(kind))
        {
        }

        /// <summary>
        /// Get the operator instance for the given <paramref name="kind"/>.
        /// </summary>
        public static AndIntBop FromKind(DKind kind)
        {
            switch (kind)
            {
            case DKind.IA: return _ia;
            case DKind.I8: return _i8;
            case DKind.I4: return _i4;
            case DKind.I2: return _i2;
            case DKind.I1: return _i1;
            case DKind.U8: return _u8;
            case DKind.U4: return _u4;
            case DKind.U2: return _u2;
            case DKind.U1: return _u1;
            case DKind.Bit: return _b;
            }
            throw Validation.BugExcept();
        }

        public override void Apply(ref Integer a, Integer b, bool inv)
        {
            Validation.Assert(!inv);
            a &= b;
        }

        public override bool IsIdentity(Integer a) => a == BndIntNode.AllOnes(Kind);
        public override bool IsSink(Integer a) => a == 0;
    }

    /// <summary>
    /// Bitwise-xor (no inverse).
    /// </summary>
    private sealed class XorIntBop : IntBop
    {
        private static readonly XorIntBop _ia = new XorIntBop(DKind.IA);
        private static readonly XorIntBop _i8 = new XorIntBop(DKind.I8);
        private static readonly XorIntBop _i4 = new XorIntBop(DKind.I4);
        private static readonly XorIntBop _i2 = new XorIntBop(DKind.I2);
        private static readonly XorIntBop _i1 = new XorIntBop(DKind.I1);
        private static readonly XorIntBop _u8 = new XorIntBop(DKind.U8);
        private static readonly XorIntBop _u4 = new XorIntBop(DKind.U4);
        private static readonly XorIntBop _u2 = new XorIntBop(DKind.U2);
        private static readonly XorIntBop _u1 = new XorIntBop(DKind.U1);
        private static readonly XorIntBop _b = new XorIntBop(DKind.Bit);

        private XorIntBop(DKind kind)
            : base(BinaryOp.BitXor, kind, 0)
        {
        }

        /// <summary>
        /// Get the operator instance for the given <paramref name="kind"/>.
        /// </summary>
        public static XorIntBop FromKind(DKind kind)
        {
            switch (kind)
            {
            case DKind.IA: return _ia;
            case DKind.I8: return _i8;
            case DKind.I4: return _i4;
            case DKind.I2: return _i2;
            case DKind.I1: return _i1;
            case DKind.U8: return _u8;
            case DKind.U4: return _u4;
            case DKind.U2: return _u2;
            case DKind.U1: return _u1;
            case DKind.Bit: return _b;
            }
            throw Validation.BugExcept();
        }

        public override void Apply(ref Integer a, Integer b, bool inv)
        {
            Validation.Assert(!inv);
            a ^= b;
        }

        public override bool IsIdentity(Integer a) => a == 0;
        public override bool IsSink(Integer a) => false;
    }

    /// <summary>
    /// Bool xor (no inverse).
    /// </summary>
    private sealed class XorBoolBop : AssociativeBop<bool>
    {
        public static readonly XorBoolBop Instance = new XorBoolBop();

        private XorBoolBop()
            : base(BinaryOp.Xor, DKind.Bit, false)
        {
        }

        public override void Apply(ref bool a, bool b, bool inv)
        {
            Validation.Assert(!inv);
            a ^= b;
        }

        public override bool IsIdentity(bool a) => !a;

        public override bool IsSink(bool a) => false;

        public override bool Trim(ref bool a, ref bool inv, bool invOk, IReducerHost host, BoundNode src)
        {
            Validation.Assert(!inv);
            return false;
        }

        public override bool Negate(bool a)
        {
            Validation.Assert(false);
            return a;
        }

        public override bool TryGetConst(BoundNode bnd, out bool val)
        {
            Validation.AssertValue(bnd);
            Validation.Assert(bnd.Type == DType.BitReq);
            return bnd.TryGetBool(out val);
        }

        public override BoundNode CreateConst(bool val)
        {
            return BndIntNode.CreateBit(val);
        }
    }

    /// <summary>
    /// Details of a floating point binary operator whose representation uses
    /// <see cref="BndVariadicOpNode"/>. There are two representations, add/sub and mul/div.
    /// This is similar to <see cref="AssociativeBop{T}"/>, but for non-associative
    /// floating point operators.
    /// </summary>
    protected sealed class FracOps
    {
        private static readonly FracOps _r4 = new FracOps(DType.R4Req);
        private static readonly FracOps _r8 = new FracOps(DType.R8Req);

        private readonly DType _type;
        private readonly bool _isFloat;

        public DKind Kind => _type.RootKind;

        private FracOps(DType type)
        {
            Validation.Assert(type.IsFractionalReq);
            _type = type;
            _isFloat = Kind == DKind.R4;
        }

        /// <summary>
        /// Get the instance for the given <paramref name="kind"/>.
        /// </summary>
        public static FracOps FromKind(DKind kind)
        {
            switch (kind)
            {
            case DKind.R4: return _r4;
            case DKind.R8: return _r8;
            }
            throw Validation.BugExcept();
        }

        /// <summary>
        /// Convert to float. This asserts that the round trip (back to double) is lossless.
        /// </summary>
        private float F(double val)
        {
            Validation.Assert(_isFloat);
            var res = (float)val;
            Validation.Assert(((double)res).ToBits() == val.ToBits());
            return res;
        }

        /// <summary>
        /// Verify that the value is valid and return the value.
        /// </summary>
        private double V(double val)
        {
            Validation.Assert(!_isFloat || ((double)(float)val).ToBits() == val.ToBits());
            return val;
        }

        public void Add(ref double a, double b, bool inv)
        {
            if (_isFloat)
                a = inv ? F(a) - F(b) : F(a) + F(b);
            else
                a = inv ? a - b : a + b;
        }

        public void Mul(ref double a, double b, bool inv)
        {
            if (_isFloat)
                a = V(inv ? F(a) / F(b) : F(a) * F(b));
            else
                a = inv ? a / b : a * b;
        }

        /// <summary>
        /// If <paramref name="bnd"/> is a constant, set <paramref name="val"/> to its value and
        /// return true.
        /// </summary>
        public bool TryGetConst(BoundNode bnd, out double val)
        {
            Validation.AssertValue(bnd);
            Validation.Assert(bnd.Type == _type);
            if (!bnd.TryGetFractional(out val))
                return false;
            V(val);
            return true;
        }

        /// <summary>
        /// Create a constant node with the given value.
        /// </summary>
        public BndFltNode CreateConst(double val)
        {
            return BndFltNode.Create(_type, V(val));
        }
    }

    #endregion Binary Operator Helpers
}

/// <summary>
/// This is the main bound tree (IR) reducer (optimizer). It should always be called before
/// invoking an IL-based code generator. It may be useful for other code generators as well, once
/// we have some. It can generate warnings, but not errors. The warnings are sent to the
/// <see cref="IReducerHost"/> passed to <see cref="Run(IReducerHost, BoundNode)"/>.
/// Replacement notifications are also sent to the <see cref="IReducerHost"/>.
/// </summary>
public static class Reducer
{
    /// <summary>
    /// Run the reducer and return the resulting <see cref="BoundNode"/>, which may be the
    /// same as <paramref name="bnd"/>. Warnings and substitutions are sent to the
    /// <paramref name="host"/>.
    /// </summary>
    public static BoundNode Run(IReducerHost host, BoundNode bnd)
    {
        return Visitor.Run(host, bnd);
    }

    /// <summary>
    /// The visitor. This is separate from <see cref="Reducer"/> so the visit methods aren't visible
    /// to clients of the <see cref="Reducer"/>.
    /// </summary>
    private sealed class Visitor : ReducerVisitor
    {
        private Visitor(IReducerHost host)
            : base(host, memoize: true)
        {
        }

        public static BoundNode Run(IReducerHost host, BoundNode bnd)
        {
            var vtor = new Visitor(host);
            int num = bnd.Accept(vtor, 0);
            Validation.Assert(num == bnd.NodeCount);
            Validation.Assert(vtor.StackDepth == 1);
            return vtor.Pop();
        }
    }
}
