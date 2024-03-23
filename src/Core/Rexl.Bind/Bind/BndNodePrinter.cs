// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text;

using Microsoft.Rexl.Lex;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Utility;

namespace Microsoft.Rexl.Bind;

/// <summary>
/// For rendering bound nodes. The primary reason for this to exist (as opposed to all bound node
/// classes implementing ToString() directly) is so scopes get numbered properly.
/// </summary>
public static class BndNodePrinter
{
    public enum Verbosity
    {
        Default,
        Terse
    }

    public static string Run(BoundNode bnd, Verbosity verbosity = Verbosity.Default)
    {
        switch (verbosity)
        {
        case Verbosity.Terse:
            return BndNodeTersePrinter.Run(bnd);
        default:
            Validation.Assert(verbosity == Verbosity.Default);
            return BndNodeDefaultPrinter.Run(bnd);
        }
    }

    private abstract class BndNodePrinterBase : NoopScopedBoundTreeVisitor
    {
        /// <summary>
        /// Information for an <see cref="ArgScope"/> encountered.
        /// </summary>
        protected sealed class ScopeStatus
        {
            public readonly ArgScope Scope;
            public readonly int Id;

            private int _depthTop;
            private List<int> _depths;

            bool _usedExt; // Whether used as an external scope.
            bool _usedInt; // Whether used as an internal (pushed) scope.
            string _num;

            public int DepthCount => _depthTop < 0 ? 0 : 1 + Util.Size(_depths);
            public int TopDepth => _depthTop;

            public ScopeStatus(ArgScope scope, int id)
            {
                Validation.AssertValue(scope);
                Validation.Assert(id >= 0);
                Scope = scope;
                Id = id;
                _num = id.ToString();
                _depthTop = -1;
            }

            public void Push(int depth)
            {
                Validation.Assert(depth > _depthTop);
                if (_depthTop >= 0)
                    Util.Add(ref _depths, _depthTop);
                _depthTop = depth;
                _usedInt = true;
            }

            public void Pop()
            {
                Validation.Assert(DepthCount > 0);
                if (!Util.TryPop(_depths, out _depthTop))
                    _depthTop = -1;
            }

            /// <summary>
            /// Produces the "current" name. This always starts with the id mapped to string.
            /// If the scope is not on the stack (or is inactive), appends 'x' for "external".
            /// If the scope is on the stack in multiple places, appends 'm' for "multiple".
            /// If the scope has been used both as external and internal, appends 'c' for "conflict".
            /// REVIEW: 'm' should no longer be possible. BoundNode construction should outlaw it.
            /// </summary>
            public string GetCurName(PushedScope scope)
            {
                var res = _num;
                if (scope == null)
                {
                    _usedExt = true;
                    res += "x";
                }
                else
                {
                    Validation.Assert(_usedInt);
                    Validation.Assert(_depthTop >= 0);
                    Validation.Assert(scope.Depth == _depthTop || _depths != null && _depths.Contains(scope.Depth));
                    if (Util.Size(_depths) > 0)
                        res += "m";
                }
                if (_usedInt & _usedExt)
                    res += "c";
                return res;
            }
        }

        protected readonly Dictionary<ArgScope, ScopeStatus> _scopeToStatus;
        protected readonly List<string> _stack;

        protected BndNodePrinterBase()
        {
            _scopeToStatus = new Dictionary<ArgScope, ScopeStatus>();
            _stack = new List<string>();
        }

        protected void Push(string str)
        {
            _stack.Add(str);
        }

        protected void Push(string fmt, params object[] args)
        {
            _stack.Add(string.Format(fmt, args));
        }

        protected string Pop()
        {
            return Util.Pop(_stack);
        }

        protected string[] PopArray(int count)
        {
            Validation.AssertIndexInclusive(count, _stack.Count);
            var items = new string[count];
            int min = _stack.Count - count;
            _stack.CopyTo(min, items, 0, count);
            _stack.RemoveRange(min, count);
            return items;
        }

        /// <summary>
        /// Reverse the top <paramref name="count"/> items on the stack.
        /// </summary>
        protected void Reverse(int count)
        {
            Validation.AssertIndexInclusive(count, _stack.Count);
            for (int min = _stack.Count - count, max = _stack.Count - 1; min < max; min++, max--)
            {
                var tmp = _stack[min];
                _stack[min] = _stack[max];
                _stack[max] = tmp;
            }
        }

        protected ScopeStatus EnsureScopeStatus(ArgScope scope)
        {
            if (!_scopeToStatus.TryGetValue(scope, out var status))
            {
                status = new ScopeStatus(scope, _scopeToStatus.Count + 1);
                _scopeToStatus.Add(scope, status);
            }
            return status;
        }

        protected abstract void PushScope(ScopeStatus status, PushedScope scope);

        protected override void VisitCore(BndScopeRefNode bnd, int idx, PushedScope scope)
        {
            Validation.AssertValue(bnd);
            Validation.AssertValueOrNull(scope);

            var status = EnsureScopeStatus(bnd.Scope);
            PushScope(status, scope);
        }

        // REVIEW: Can "isArgValid" ever be false? Currently it doesn't seem like there is a way.
        protected sealed override void PushScope(ArgScope scope, BndScopeOwnerNode owner, int idx, int slot, bool isArgValid)
        {
            Validation.AssertValue(scope);

            base.PushScope(scope, owner, idx, slot, isArgValid);
            Validation.Assert(ScopeDepth > 0);

            var status = EnsureScopeStatus(scope);
            status.Push(ScopeDepth - 1);
        }

        protected override void PopScope(ArgScope scope)
        {
            Validation.Assert(ScopeDepth > 0);
            Validation.AssertValue(scope);

            Validation.Assert(_scopeToStatus.ContainsKey(scope));
            if (_scopeToStatus.TryGetValue(scope, out var info))
            {
                Validation.Assert(info.TopDepth == ScopeDepth - 1);
                info.Pop();
            }

            base.PopScope(scope);
        }

        /// <summary>
        /// Append text to declare a scope.
        /// </summary>
        protected abstract void DeclScope(StringBuilder sb, ArgScope scope, BndNodeKind kind);

        protected void HandleSliceItem(StringBuilder sb, SliceItemFlags item)
        {
            if (item.IsIndex())
                HandleSliceIndex(sb, item);
            else if (item.IsTupleSlice())
                HandleSliceTuple(sb, item);
            else
                HandleSliceRange(sb, item);
        }

        protected void HandleSliceItems(StringBuilder sb, BndTensorSliceNode bnd)
        {
            string sep = "";
            for (int i = 0; i < bnd.Items.Length; i++)
            {
                sb.Append(sep);
                sep = ", ";
                HandleSliceItem(sb, bnd.Items[i]);
            }
        }

        protected abstract void HandleSliceIndex(StringBuilder sb, SliceItemFlags item);

        protected abstract void HandleSliceTuple(StringBuilder sb, SliceItemFlags item);

        protected abstract void HandleSliceRange(StringBuilder sb, SliceItemFlags item);

        protected void HandleSliceRange(StringBuilder sb, SliceItemFlags item, string fmt, string strNull = "")
        {
            Validation.AssertValue(sb);
            Validation.Assert(item.IsRangeSlice());
            Validation.AssertValue(fmt);
            Validation.AssertValue(strNull);

            string start = strNull;
            string stop = strNull;
            string step = strNull;

            if ((item & SliceItemFlags.Start) != 0)
            {
                start = Pop();
                if ((item & SliceItemFlags.StartBack) != 0)
                    start = "^(" + start + ")";
            }
            if ((item & SliceItemFlags.Stop) != 0)
            {
                stop = Pop();
                var mods = item & (SliceItemFlags.StopBack | SliceItemFlags.StopStar);
                switch (mods)
                {
                case SliceItemFlags.StopBack:
                    stop = "^(" + stop + ")";
                    break;
                case SliceItemFlags.StopStar:
                    stop = "*(" + stop + ")";
                    break;
                case SliceItemFlags.StopBack | SliceItemFlags.StopStar:
                    stop = "^*(" + stop + ")";
                    break;
                default:
                    Validation.Assert(mods == 0);
                    break;
                }
            }
            if ((item & SliceItemFlags.Step) != 0)
                step = Pop();

            sb.AppendFormat(fmt, start, stop, step);
        }

        protected sealed override void VisitCore(BndLeafNode node, int idx)
        {
            Push(node.ToString());
        }

        protected sealed override void PostVisitImpl(BndCastNumNode bnd, int idx)
        {
            Push("Num<{0}>({1})", bnd.Type, Pop());
        }

        protected sealed override void PostVisitImpl(BndCastRefNode bnd, int idx)
        {
            Push("Ref<{0}>({1})", bnd.Type, Pop());
        }

        protected sealed override void PostVisitImpl(BndCastBoxNode bnd, int idx)
        {
            Push("Box<{0}>({1})", bnd.Type, Pop());
        }

        protected sealed override void PostVisitImpl(BndCastOptNode bnd, int idx)
        {
            Push("Opt<{0}>({1})", bnd.Type, Pop());
        }

        protected sealed override void PostVisitImpl(BndCastVacNode bnd, int idx)
        {
            // REVIEW: Change this to "Vac" at some point (in an isolated PR).
            Push("Unit<{0}>({1})", bnd.Type, Pop());
        }

        protected sealed override void PostVisitImpl(BndModToRecNode bnd, int idx)
        {
            Push("ModToRec({0})", Pop());
        }

        protected sealed override void PostVisitImpl(BndBinaryOpNode bnd, int idx)
        {
            Push("{0}({2}, {1})", bnd.Op, Pop(), Pop());
        }

        protected sealed override void PostVisitImpl(BndVariadicOpNode bnd, int idx)
        {
            int len = bnd.Args.Length;
            Validation.Assert(len >= 1);

            string inv = "";
            if (!bnd.Inverted.IsEmpty)
            {
                switch (bnd.Op)
                {
                case BinaryOp.Add:
                    inv = "[-] ";
                    break;
                case BinaryOp.Mul:
                    inv = "[/] ";
                    break;
                default:
                    inv = "<bad>";
                    break;
                }
            }

            switch (len)
            {
            case 1:
                Push("{0}({2}{1})", bnd.Op, Pop(), bnd.Inverted.TestBit(0) ? inv : "");
                break;
            case 2:
                Push("{0}({3}{2}, {4}{1})", bnd.Op, Pop(), Pop(), bnd.Inverted.TestBit(0) ? inv : "", bnd.Inverted.TestBit(1) ? inv : "");
                break;
            default:
                Reverse(len);
                var sb = new StringBuilder().AppendFormat("{0}(", bnd.Op);
                string sep = "";
                for (int i = 0; i < len; i++)
                {
                    sb.Append(sep);
                    sep = ", ";
                    if (bnd.Inverted.TestBit(i))
                        sb.Append(inv);
                    sb.Append(Pop());
                }
                sb.Append(')');
                Push(sb.ToString());
                break;
            }
        }

        protected sealed override void PostVisitImpl(BndIfNode bnd, int idx)
        {
            Push("If({2}, {1}, {0})", Pop(), Pop(), Pop());
        }

        protected sealed override void PostVisitImpl(BndRecordNode bnd, int idx)
        {
            int len = bnd.Items.Count;
            Validation.Assert(len == bnd.ChildCount);
            Reverse(len);
            var sb = new StringBuilder("{");
            string sep = "";
            foreach (var (name, val) in bnd.Items.GetPairs())
            {
                sb.Append(sep).AppendFormat("{0}:{1}", name, Pop());
                sep = ", ";
            }

            if (bnd.Items.Count < bnd.Type.FieldCount)
            {
                // Print out implicit default values of missing fields.
                int count = 0;
                sep = "[, ";
                foreach (var kv in bnd.Type.GetNames())
                {
                    if (!bnd.Items.ContainsKey(kv.Name))
                    {
                        VisitCore(BndDefaultNode.Create(kv.Type), -1);
                        sb.Append(sep).AppendFormat("{0}:{1}", kv.Name, Pop());
                        sep = ", ";
                        count++;
                    }
                }
                Validation.Assert(bnd.Items.Count + count == bnd.Type.FieldCount);
                sb.Append(']');
            }

            sb.Append('}');
            Push(sb.ToString());
        }

        protected sealed override void PostVisitImpl(BndModuleNode bnd, int idx)
        {
            var items = PopArray(bnd.Items.Length);
            var exts = PopArray(bnd.Externals.Length);

            var sb = new StringBuilder("module[");
            if (exts.Length > 0)
            {
                sb.Append("ext");
                DeclScope(sb, bnd.ScopeExt, bnd.Kind);
                sb.Append('(');
                for (int i = 0; i < exts.Length; i++)
                    sb.Append(exts[i]).Append(',');
                sb.Append("), ");
            }

            sb.Append("items");
            DeclScope(sb, bnd.ScopeItems, bnd.Kind);
            sb.Append("]{ ");
            var sep = "";
            foreach (var sym in bnd.Symbols)
            {
                sb.Append(sep).Append(sym.SymKind.ToStr()).Append(' ').Append(sym.Name.Escape());

                switch (sym)
                {
                case ModFmaSym mfs:
                    Validation.Assert(mfs.FmaCount == 1);
                    Validation.AssertIndex(mfs.IfmaValue, items.Length);
                    sb.Append(" := ").Append(items[mfs.IfmaValue]);
                    break;
                case ModItemVar miv:
                    Validation.Assert(miv.FmaCount == 2);
                    Validation.AssertIndex(miv.FormulaIn, items.Length);
                    sb.Append(" in ").Append(items[miv.FormulaIn]);
                    Validation.AssertIndex(miv.FormulaDefault, items.Length);
                    sb.Append(" def ").Append(items[miv.FormulaDefault]);
                    if (miv.IsOpt)
                        sb.Append(" opt");
                    break;
                case ModSimpleVar msv:
                    Validation.Assert(msv.FmaCount <= 3);
                    if (msv.FormulaFrom >= 0)
                    {
                        Validation.AssertIndex(msv.FormulaFrom, items.Length);
                        sb.Append(" from ").Append(items[msv.FormulaFrom]);
                    }
                    if (msv.FormulaTo >= 0)
                    {
                        Validation.AssertIndex(msv.FormulaTo, items.Length);
                        sb.Append(" to ").Append(items[msv.FormulaTo]);
                    }
                    Validation.AssertIndex(msv.FormulaDefault, items.Length);
                    sb.Append(" def ").Append(items[msv.FormulaDefault]);
                    if (msv.IsOpt)
                        sb.Append(" opt");
                    break;
                default:
                    Validation.Assert(false);
                    break;
                }
                sep = "; ";
            }

            sb.Append(" }");
            Push(sb.ToString());
        }

        protected sealed override void PostVisitImpl(BndCallNode bnd, int idx)
        {
            var sb = new StringBuilder();
            FormatCall(sb, bnd);
            Push(sb.ToString());
        }

        protected virtual void FormatCall(StringBuilder sb, BndCallNode bnd)
        {
            int len = bnd.Args.Length;
            Reverse(len);
            AppendOperPath(sb, bnd);
            sb.Append('(');
            string pre = "";
            int iscope = 0;
            for (int iarg = 0; iarg < len; iarg++)
            {
                sb.Append(pre);
                if (bnd.Traits.IsScope(iarg))
                {
                    Validation.Assert(iscope < bnd.Scopes.Length);
                    var scope = bnd.Scopes[iscope++];
                    DeclScope(sb, scope, bnd.Kind);
                }
                var dir = bnd.GetDirective(iarg).ToSrcText();
                if (dir != null)
                    sb.AppendFormat("{0} ", dir);
                var name = bnd.GetName(iarg);
                if (name.IsValid)
                    sb.AppendFormat("{0} : ", name);
                sb.Append(Pop());
                pre = ", ";
            }
            Validation.Assert(iscope == bnd.Scopes.Length);
            sb.Append(')');
        }

        protected virtual void AppendOperPath(StringBuilder sb, BndCallNode call)
        {
            sb.Append(call.Oper.Path.ToDottedSyntax());
            if (!call.CertifiedFull && call.Oper is not UnknownFunc)
                sb.Append('*');
        }

        protected sealed override void PostVisitImpl(BndGroupByNode bnd, int idx)
        {
            Reverse(bnd.ChildCount);
            var sb = new StringBuilder("GroupBy(");
            DeclScope(sb, bnd.ScopeForKeys, bnd.Kind);
            sb.Append(Pop());
            int ikey = 0;
            for (int i = 0; i < bnd.PureKeys.Length; i++)
            {
                bool ci = bnd.KeysCi.TestBit(ikey++);
                sb.AppendFormat(", [{0}] ", ci ? "~" : "key");
                sb.Append(Pop());
            }
            Validation.Assert(ikey == bnd.PureKeys.Length);
            foreach (var (name, val) in bnd.KeepKeys.GetPairs())
            {
                Validation.Assert(val != null);
                Validation.Assert(name.IsValid);
                bool ci = bnd.KeysCi.TestBit(ikey++);
                sb.AppendFormat(", [{0}] {1}:", ci ? "~" : "key", name);
                sb.Append(Pop());
            }
            Validation.Assert(ikey == bnd.PureKeys.Length + bnd.KeepKeys.Count);
            foreach (var (name, val) in bnd.MapItems.GetPairs())
            {
                sb.Append(val != null ? ", [map] " : ", [auto] ");
                if (name.IsValid)
                    sb.AppendFormat("{0}:", name);
                sb.Append(val != null ? Pop() : "<auto>");
            }
            foreach (var (name, val) in bnd.AggItems.GetPairs())
            {
                Validation.Assert(val != null);
                sb.Append(", [agg] ");
                if (name.IsValid)
                    sb.AppendFormat("{0}:", name);
                sb.Append(Pop());
            }
            sb.Append(')');
            Push(sb.ToString());
        }

        protected sealed override void PostVisitImpl(BndSetFieldsNode bnd, int idx)
        {
            Reverse(bnd.ChildCount);
            var sb = new StringBuilder("SetFields(");
            if (bnd.Scope != null)
                DeclScope(sb, bnd.Scope, bnd.Kind);
            sb.Append(Pop());
            foreach (var (name, val) in bnd.Additions.GetPairs())
            {
                Validation.Assert(val != null);
                Validation.Assert(name.IsValid);
                sb.AppendFormat(", {0} : ", name);
                sb.Append(Pop());
            }
            sb.Append(')');
            Push(sb.ToString());
        }

        protected sealed override void PostVisitImpl(BndModuleProjectionNode bnd, int idx)
        {
            Validation.Assert(bnd.ChildCount == 2);
            Reverse(2);
            var sb = new StringBuilder("ModuleProjection(");
            DeclScope(sb, bnd.Scope, bnd.Kind);
            sb.Append(Pop());
            sb.Append(", ");
            sb.Append(Pop());
            sb.Append(')');
            Push(sb.ToString());
        }

        protected sealed override void PostVisitImpl(BndTensorNode bnd, int idx)
        {
            int len = bnd.Items.Length;
            Reverse(len);
            var sb = new StringBuilder("[! ");
            string sep = "";
            for (int i = 0; i < len; i++)
            {
                sb.Append(sep).Append(Pop());
                sep = ", ";
            }
            sb.AppendFormat(" !]:{0}[", bnd.Type.GetTensorItemType());
            var pre = "";
            var shape = bnd.Shape;
            for (int i = 0; i < shape.Rank; i++)
            {
                sb.Append(pre).AppendFormat("{0}", shape[i]);
                pre = ",";
            }
            sb.Append(']');
            Push(sb.ToString());
        }
    }

    /// <summary>
    /// For rendering bound nodes with a balance between detail and readability.
    /// </summary>
    private sealed class BndNodeDefaultPrinter : BndNodePrinterBase
    {
        private BndNodeDefaultPrinter()
            : base()
        {
        }

        public static string Run(BoundNode bnd)
        {
            var printer = new BndNodeDefaultPrinter();
            int num = bnd.Accept(printer, 0);
            Validation.Assert(printer._stack.Count == 1);
            Validation.Assert(num == bnd.NodeCount);
            return printer.Pop();
        }

        protected override void DeclScope(StringBuilder sb, ArgScope scope, BndNodeKind kind)
        {
            if (scope is null)
                return;
            if (!_scopeToStatus.TryGetValue(scope, out var info).Verify())
                return;
            var id = info.Id;
            if (kind == BndNodeKind.GroupBy)
                sb.AppendFormat("[scope:{0}] ", id);
            else
            {
                switch (scope.Kind)
                {
                case ScopeKind.SeqItem:
                    sb.AppendFormat("[map:{0}] ", id);
                    break;
                case ScopeKind.TenItem:
                    sb.AppendFormat("[ten:{0}] ", id);
                    break;
                case ScopeKind.Iter:
                    sb.AppendFormat("[iter:{0}] ", id);
                    break;
                case ScopeKind.Guard:
                    sb.AppendFormat("[guard:{0}] ", id);
                    break;
                case ScopeKind.With:
                    sb.AppendFormat("[with:{0}] ", id);
                    break;
                case ScopeKind.Range:
                    sb.AppendFormat("[rng:{0}] ", id);
                    break;
                default:
                    Validation.Assert(false);
                    break;
                }
            }
        }

        protected override void PushScope(ScopeStatus status, PushedScope scope)
        {
            Validation.AssertValue(status);
            Validation.AssertValueOrNull(scope);

            Push("Scope({0})", status.GetCurName(scope));
        }

        protected override void PostVisitImpl(BndGetFieldNode bnd, int idx)
        {
            Push("GetField({0}, {1})", Pop(), bnd.Name);
        }

        protected override void PostVisitImpl(BndGetSlotNode bnd, int idx)
        {
            Push("GetSlot({0}, {1})", Pop(), bnd.Slot);
        }

        protected override void PostVisitImpl(BndIdxTextNode bnd, int idx)
        {
            Reverse(2);
            if (!bnd.Modifier.HasIndexMods())
                Push("IdxTxt({0}, {1})", Pop(), Pop());
            else
            {
                var sb = new StringBuilder();
                sb.Append("IdxTxt(").Append(Pop()).Append(", ");
                HandleIndex(sb, bnd.Modifier);
                sb.Append(')');
                Push(sb.ToString());
            }
        }

        protected override void PostVisitImpl(BndIdxTensorNode bnd, int idx)
        {
            int rank = bnd.Indices.Length;
            Reverse(rank + 1);
            var sb = new StringBuilder("IdxTen(");
            sb.Append(Pop());
            string sep = ", ";
            for (int i = 0; i < rank; i++)
            {
                sb.Append(sep);
                HandleIndex(sb, bnd.Modifiers[i]);
            }
            sb.AppendFormat("):{0}", bnd.Type);
            Push(sb.ToString());
        }

        protected override void PostVisitImpl(BndIdxHomTupNode bnd, int idx)
        {
            Reverse(2);
            if (!bnd.Modifier.HasIndexMods())
                Push("IdxTup({0}, {1})", Pop(), Pop());
            else
            {
                var sb = new StringBuilder();
                sb.Append("IdxTup(").Append(Pop()).Append(", ");
                HandleIndex(sb, bnd.Modifier);
                sb.Append(')');
                Push(sb.ToString());
            }
        }

        private void HandleIndex(StringBuilder sb, IndexFlags flags)
        {
            if (!flags.HasIndexMods())
                sb.Append(Pop());
            else
                HandleSliceIndex(sb, flags.ToSliceFlags());
        }

        protected override void HandleSliceIndex(StringBuilder sb, SliceItemFlags item)
        {
            Validation.Assert(item.IsIndex());

            string mod;
            if (!item.HasIndexMods())
                mod = "None";
            else
            {
                mod = "";
                if ((item & SliceItemFlags.IndexBack) != 0)
                    mod = "Back";
                if ((item & SliceItemFlags.IndexWrap) != 0)
                    mod += "Wrap";
                if ((item & SliceItemFlags.IndexClip) != 0)
                    mod += "Clip";
            }
            sb.AppendFormat("Index({0}, {1})", Pop(), mod);
        }

        protected override void HandleSliceTuple(StringBuilder sb, SliceItemFlags item)
        {
            Validation.Assert(item.IsTupleSlice());
            sb.AppendFormat("Slice({0})", Pop());
        }

        protected override void HandleSliceRange(StringBuilder sb, SliceItemFlags item)
        {
            HandleSliceRange(sb, item, "Slice({0}:{1}:{2})", "null:i8?");
        }

        protected override void PostVisitImpl(BndTextSliceNode bnd, int idx)
        {
            Reverse(bnd.ChildCount);
            var sb = new StringBuilder();
            sb.Append("TextSlice(").Append(Pop()).Append(", ");
            HandleSliceItem(sb, bnd.Item);
            sb.AppendFormat("):{0}", bnd.Type);
            Push(sb.ToString());
        }

        protected override void PostVisitImpl(BndTensorSliceNode bnd, int idx)
        {
            Reverse(bnd.ChildCount);
            var sb = new StringBuilder();
            sb.Append("GetSlice(").Append(Pop()).Append(", ");
            HandleSliceItems(sb, bnd);
            sb.AppendFormat("):{0}", bnd.Type);
            Push(sb.ToString());
        }

        protected override void PostVisitImpl(BndTupleSliceNode bnd, int idx)
        {
            Validation.Assert(bnd.ChildCount == 1);
            var sb = new StringBuilder();
            sb.Append("GetSlice(").Append(Pop()).Append(", ");
            sb.Append(bnd.Start);
            sb.Append(':').Append(bnd.Start + bnd.Count * bnd.Step);
            sb.Append(':').Append(bnd.Step);
            sb.AppendFormat("):{0}", bnd.Type);
            Push(sb.ToString());
        }

        protected override void PostVisitImpl(BndCompareNode bnd, int idx)
        {
            int len = bnd.Args.Length;
            Validation.Assert(len >= 2);
            switch (len)
            {
            case 2:
                Push("Cmp({2} {0} {1})", bnd.Ops[0].GetStr(), Pop(), Pop());
                break;
            default:
                Reverse(len);
                var sb = new StringBuilder().Append("Cmp(");
                for (int i = 0; i < len; i++)
                {
                    if (i > 0)
                        sb.AppendFormat(" {0} ", bnd.Ops[i - 1].GetStr());
                    sb.Append(Pop());
                }
                sb.Append(')');
                Push(sb.ToString());
                break;
            }
        }

        protected override int VisitNestedCallArg(BndCallNode call, int slot, BoundNode arg, int cur)
        {
            return base.VisitNestedCallArg(call, slot, arg, cur);
        }

        protected override void FormatCall(StringBuilder sb, BndCallNode bnd)
        {
            switch (bnd.Kind)
            {
            case BndNodeKind.CallProcedure:
                sb.Append("CallProc(");
                break;
            case BndNodeKind.CallVolatile:
                sb.Append("CallVol(");
                break;
            default:
                Validation.Assert(bnd.Kind == BndNodeKind.Call);
                sb.Append("Call(");
                break;
            }

            base.FormatCall(sb, bnd);
            switch (bnd.Type.RootKind)
            {
            case DKind.Record:
                sb.Append(')');
                break;
            default:
                sb.AppendFormat(":{0})", bnd.Type);
                break;
            }
        }

        protected override void AppendOperPath(StringBuilder sb, BndCallNode call)
        {
            sb.Append(call.Oper.Path.ToString());
            if (!call.CertifiedFull && call.Oper is not UnknownFunc)
                sb.Append('*');
        }

        protected override void PostVisitImpl(BndSequenceNode bnd, int idx)
        {
            int len = bnd.Items.Length;
            Reverse(len);
            var sb = new StringBuilder("[");
            string sep = "";
            for (int i = 0; i < len; i++)
            {
                sb.Append(sep).Append(Pop());
                sep = ", ";
            }
            sb.AppendFormat("]:{0}", bnd.Type);
            Push(sb.ToString());
        }

        protected override void PostVisitImpl(BndTupleNode bnd, int idx)
        {
            int len = bnd.Items.Length;
            Reverse(len);
            var sb = new StringBuilder("(");
            string sep = "";
            for (int i = 0; i < len; i++)
            {
                sb.Append(sep).Append(Pop());
                sep = ", ";
            }
            if (len == 1)
                sb.Append(',');
            sb.AppendFormat("):{0}", bnd.Type);
            Push(sb.ToString());
        }
    }

    /// <summary>
    /// For rendering bound nodes with a smaller string for ease of reading.
    /// </summary>
    private sealed class BndNodeTersePrinter : BndNodePrinterBase
    {
        private BndNodeTersePrinter()
            : base()
        {
        }

        public static string Run(BoundNode bnd)
        {
            var printer = new BndNodeTersePrinter();
            int num = bnd.Accept(printer, 0);
            Validation.Assert(printer._stack.Count == 1);
            Validation.Assert(num == bnd.NodeCount);
            return printer.Pop();
        }

        protected override void DeclScope(StringBuilder sb, ArgScope scope, BndNodeKind kind)
        {
            if (scope is null)
                return;
            if (!_scopeToStatus.TryGetValue(scope, out var info).Verify())
                return;
            sb.Append(GetPrefix(scope));
            sb.Append(info.Id);
            if (kind != BndNodeKind.Module)
                sb.Append(": ");
        }

        protected override void VisitImpl(BndMissingValueNode bnd, int idx) { Push("<missing>"); }
        protected override void VisitImpl(BndNullNode bnd, int idx) { Push("null"); }
        protected override void VisitImpl(BndIntNode bnd, int idx)
        {
            if (bnd.TryGetBool(out var val))
                Push(val ? "true" : "false");
            else
                Push(bnd.Value.ToString());
        }
        protected override void VisitImpl(BndGlobalNode bnd, int idx) { Push(bnd.FullName.ToDottedSyntax()); }

        protected override void VisitImpl(BndStrNode bnd, int idx)
        {
            if (bnd.Value == null)
                Push("str(<null>)");
            else
            {
                var sb = new StringBuilder();
                RexlLexer.Instance.AppendTextLiteral(sb, bnd.Value);
                Push(sb.ToString());
            }
        }

        protected override void VisitImpl(BndFltNode bnd, int idx)
        {
            Push(bnd.Value.ToStr());
        }

        private string GetPrefix(ArgScope scope)
        {
            switch (scope.Kind)
            {
            case ScopeKind.SeqItem:
                return "*";
            case ScopeKind.TenItem:
                return "@";
            case ScopeKind.Iter:
                return "%";
            case ScopeKind.Guard:
                return "?";
            case ScopeKind.With:
                return "!";
            case ScopeKind.Range:
                return "^";
            case ScopeKind.SeqIndex:
                return "#";
            default:
                Validation.Assert(false);
                return "???";
            }
        }

        protected override void PushScope(ScopeStatus status, PushedScope scope)
        {
            Validation.AssertValue(status);
            Validation.AssertValueOrNull(scope);

            Push("{0}{1}", GetPrefix(status.Scope), status.GetCurName(scope));
        }

        protected override void PostVisitImpl(BndGetFieldNode bnd, int idx)
        {
            Push("{0}.{1}", Pop(), bnd.Name);
        }

        protected override void PostVisitImpl(BndGetSlotNode bnd, int idx)
        {
            Push("{0}.{1}", Pop(), bnd.Slot);
        }

        protected override void PostVisitImpl(BndIdxTextNode bnd, int idx)
        {
            Reverse(2);
            if (!bnd.Modifier.HasIndexMods())
                Push("{0}[{1}]", Pop(), Pop());
            else
            {
                var sb = new StringBuilder();
                sb.Append(Pop()).Append('[');
                HandleSliceIndex(sb, bnd.Modifier.ToSliceFlags());
                sb.Append(']');
                Push(sb.ToString());
            }
        }

        protected override void PostVisitImpl(BndIdxTensorNode bnd, int idx)
        {
            int rank = bnd.Indices.Length;
            Reverse(rank + 1);
            var sb = new StringBuilder();
            sb.Append(Pop()).Append('[');
            string sep = "";
            for (int i = 0; i < rank; i++)
            {
                sb.Append(sep);
                HandleSliceIndex(sb, bnd.Modifiers[i].ToSliceFlags());
                sep = ", ";
            }
            sb.Append(']');
            Push(sb.ToString());
        }

        protected override void PostVisitImpl(BndIdxHomTupNode bnd, int idx)
        {
            Reverse(2);
            if (!bnd.Modifier.HasIndexMods())
                Push("{0}[{1}]", Pop(), Pop());
            else
            {
                var sb = new StringBuilder();
                sb.Append(Pop()).Append('[');
                HandleSliceIndex(sb, bnd.Modifier.ToSliceFlags());
                sb.Append(']');
                Push(sb.ToString());
            }
        }

        protected override void HandleSliceIndex(StringBuilder sb, SliceItemFlags item)
        {
            var ind = Pop();

            if (!item.HasIndexMods())
                sb.Append(ind);
            else
            {
                string mod = "";
                if ((item & SliceItemFlags.IndexBack) != 0)
                    mod = "^";
                if ((item & SliceItemFlags.IndexWrap) != 0)
                    mod += "%";
                if ((item & SliceItemFlags.IndexClip) != 0)
                    mod += "&";
                sb.AppendFormat("{0}({1})", mod, ind);
            }
        }

        protected override void HandleSliceTuple(StringBuilder sb, SliceItemFlags item)
        {
            sb.Append(Pop());
        }

        protected override void HandleSliceRange(StringBuilder sb, SliceItemFlags item)
        {
            HandleSliceRange(sb, item, "{0}:{1}:{2}");
        }

        protected override void PostVisitImpl(BndTextSliceNode bnd, int idx)
        {
            Reverse(bnd.ChildCount);
            var sb = new StringBuilder();
            sb.Append(Pop()).Append('[');

            HandleSliceItem(sb, bnd.Item);

            sb.Append(']');
            Push(sb.ToString());
        }

        protected override void PostVisitImpl(BndTensorSliceNode bnd, int idx)
        {
            Reverse(bnd.ChildCount);
            var sb = new StringBuilder();
            sb.Append(Pop()).Append('[');

            HandleSliceItems(sb, bnd);

            sb.Append(']');
            Push(sb.ToString());
        }

        protected override void PostVisitImpl(BndTupleSliceNode bnd, int idx)
        {
            Validation.Assert(bnd.ChildCount == 1);
            var sb = new StringBuilder();
            sb.Append(Pop()).Append('[');
            sb.Append(bnd.Start);
            sb.Append(':').Append(bnd.Start + bnd.Count * bnd.Step);
            sb.Append(':').Append(bnd.Step);
            sb.Append(']');
            Push(sb.ToString());
        }

        protected override void PostVisitImpl(BndCompareNode bnd, int idx)
        {
            int len = bnd.Args.Length;
            Validation.Assert(len >= 2);
            switch (len)
            {
            case 2:
                Push("{2} {0} {1}", bnd.Ops[0].GetStr(), Pop(), Pop());
                break;
            default:
                Reverse(len);
                var sb = new StringBuilder();
                for (int i = 0; i < len; i++)
                {
                    if (i > 0)
                        sb.AppendFormat(" {0} ", bnd.Ops[i - 1].GetStr());
                    sb.Append(Pop());
                }
                Push(sb.ToString());
                break;
            }
        }

        protected override void PostVisitImpl(BndSequenceNode bnd, int idx)
        {
            int len = bnd.Items.Length;
            Reverse(len);
            var sb = new StringBuilder("[");
            string sep = "";
            for (int i = 0; i < len; i++)
            {
                sb.Append(sep).Append(Pop());
                sep = ", ";
            }
            sb.Append(']');
            Push(sb.ToString());
        }

        protected override void PostVisitImpl(BndTupleNode bnd, int idx)
        {
            int len = bnd.Items.Length;
            Reverse(len);
            var sb = new StringBuilder("(");
            string sep = "";
            for (int i = 0; i < len; i++)
            {
                sb.Append(sep).Append(Pop());
                sep = ", ";
            }
            if (len == 1)
                sb.Append(',');
            sb.Append(')');
            Push(sb.ToString());
        }
    }
}
