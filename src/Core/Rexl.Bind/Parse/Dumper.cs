// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using Microsoft.Rexl.Lex;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;

namespace Microsoft.Rexl.Parse;

/// <summary>
/// Simple tree dumping parse node visitor.
/// </summary>
public static class RexlTreeDumper
{

    /// <summary>
    /// Public entry point for dumping a parse tree.
    /// </summary>
    public static TSink Print<TSink>(TSink sink, RexlNode node,
            string prefix = "", string source = null, Action<string, RexlNode> writeNote = null)
        where TSink : TextSink
    {
        Validation.AssertValue(node);
        Validation.AssertValue(prefix);

        var vtor = new Visitor<TSink>(sink, prefix, source, writeNote);
        node.Accept(vtor);
        return sink;
    }

    private sealed class Visitor<TSink> : RexlTreeVisitor
        where TSink : TextSink
    {
        // The text sink to write to.
        private readonly TSink _sink;

        // Prefix each line with this.
        private readonly string _prefix;

        // The (optional) source code, for displaying fragments.
        private readonly string _source;

        // The (optional) delegate for displaying additional notes.
        private readonly Action<string, RexlNode> _writeNote;

        // Track the nesting level.
        private int _nesting;
        private bool _first;

#if DEBUG
        private Stack<RexlNode> _stack;
#endif

        public Visitor(TSink sink, string prefix, string source = null, Action<string, RexlNode> writeNote = null)
        {
            Validation.AssertValue(sink);
            Validation.AssertValue(prefix);
            Validation.AssertValueOrNull(source);
            Validation.AssertValueOrNull(writeNote);

            _sink = sink;
            _prefix = prefix;
            _source = source;
            _writeNote = writeNote;

            _nesting = 0;
            _first = true;
#if DEBUG
            _stack = new Stack<RexlNode>();
#endif
        }

        private string _GetFrag(SourceRange rng)
        {
            string res = rng.GetFragment();
            return res.Replace("\r\n", "\\n").Replace("\r", "\\n").Replace("\n", "\\n");
        }

        private void _WriteRanges(SourceRange rng, SourceRange rngFull)
        {
            if (_source != null)
                _sink.Write(", rng:{0} [{1}], full:{2} [{3}]", rng, _GetFrag(rng), rngFull, _GetFrag(rngFull));
            else
                _sink.Write(", rng:{0}, full:{1}", rng, rngFull);
        }

        private void _WriteNode(RexlNode node, string extra = null)
        {
            if (_first)
                _first = false;
            else
                _sink.WriteLine();

            _sink.TWrite("{0}{1:00} ", _prefix, _nesting)
                .TWrite(' ', 2 * _nesting)
                .Write("Node[id:{0}, kind:{1}, depth:{2}, tok:{3}",
                    node.Id, node.Kind, node.TreeDepth, node.Token.ToString());
            _WriteRanges(node.GetRange(), node.GetFullRange());
            if (!string.IsNullOrEmpty(extra))
                _sink.Write(", extra=[{0}]", extra);
            _sink.Write(']');

            _writeNote?.Invoke(", ", node);
        }

        private bool _StartNode(RexlNode node, string extra = null, int count = 0)
        {
            Validation.Assert(count >= 0);
            _WriteNode(node, extra);
            _sink.TWrite(", count=").Write(count);
            if (count == 0)
                return false;
            _nesting++;
#if DEBUG
            _stack.Push(node);
#endif
            return true;
        }

        private void _WriteSub(RexlNode node, string fmt, params object[] args)
        {
            _sink.TWriteLine()
                .TWrite("{0}{1:00} ", _prefix, _nesting)
                .TWrite(' ', 2 * _nesting)
                .Write(fmt, args);
        }

        private void _EndNode(RexlNode node)
        {
            Validation.Assert(_nesting > 0);
#if DEBUG
            Validation.Assert(_stack.Count > 0);
            var tmp = _stack.Pop();
            Validation.Assert(node == tmp);
#endif
            _nesting--;
        }

        protected override void VisitImpl(ErrorNode node)
        {
            Validation.AssertValue(node);
            _WriteNode(node, node.Format());
        }

        protected override void VisitImpl(MissingValueNode node)
        {
            Validation.AssertValue(node);
            _WriteNode(node, node.Format());
        }

        protected override void VisitImpl(NullLitNode node)
        {
            Validation.AssertValue(node);
            _WriteNode(node);
        }

        protected override void VisitImpl(BoolLitNode node)
        {
            Validation.AssertValue(node);
            _WriteNode(node, node.Value ? "true" : "false");
        }

        protected override void VisitImpl(NumLitNode node)
        {
            Validation.AssertValue(node);
            _WriteNode(node, node.Value.ToString());
        }

        protected override void VisitImpl(TextLitNode node)
        {
            Validation.AssertValue(node);
            _WriteNode(node);
        }

        protected override void VisitImpl(BoxNode node)
        {
            Validation.AssertValue(node);
            _WriteNode(node);
        }

        protected override void VisitImpl(ItNameNode node)
        {
            Validation.AssertValue(node);
            _WriteNode(node, string.Format("slot={0}", node.UpCount));
        }

        protected override void VisitImpl(ThisNameNode node)
        {
            Validation.AssertValue(node);
            _WriteNode(node);
        }

        protected override void VisitImpl(FirstNameNode node)
        {
            Validation.AssertValue(node);

            string str = node.Ident.Token.ToString();
            if (node.Ident.AtToken != null)
                str = "@" + str;
            _WriteNode(node, str);
        }

        protected override void VisitImpl(MetaPropNode node)
        {
            Validation.AssertValue(node);

            string str = node.Left.ToString() + "$" + node.Right.Token.ToString();
            _WriteNode(node, str);
        }

        protected override void VisitImpl(GotoStmtNode node)
        {
            Validation.AssertValue(node);
            _WriteNode(node, node.Label.Token.ToString());
        }

        protected override void VisitImpl(LabelStmtNode node)
        {
            Validation.AssertValue(node);
            _WriteNode(node, node.Label.Token.ToString());
        }

        protected override bool PreVisitImpl(StmtListNode node)
        {
            Validation.AssertValue(node);
            return _StartNode(node, null, node.Count);
        }

        protected override void PostVisitImpl(StmtListNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override bool PreVisitImpl(ExprListNode node)
        {
            Validation.AssertValue(node);
            return _StartNode(node, null, node.Count);
        }

        protected override void PostVisitImpl(ExprListNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override bool PreVisitImpl(SymListNode node)
        {
            Validation.AssertValue(node);
            return _StartNode(node, null, node.Count);
        }

        protected override void PostVisitImpl(SymListNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override bool PreVisitImpl(SliceListNode node)
        {
            Validation.AssertValue(node);
            return _StartNode(node, null, node.Count);
        }

        protected override void PostVisitImpl(SliceListNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        private string ExtraFromNamespace(NamespaceSpec nss)
        {
            Validation.AssertValue(nss);

            if (nss.IdentPath != null)
                return nss.IdentPath.ToString();
            if (nss.IsRooted)
                return "@";
            return null;
        }

        protected override bool PreVisitImpl(BlockStmtNode node)
        {
            Validation.AssertValue(node);
            return _StartNode(node, null, 1);
        }

        protected override void PostVisitImpl(BlockStmtNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override bool PreVisitImpl(NamespaceStmtNode node)
        {
            Validation.AssertValue(node);

            string extra = ExtraFromNamespace(node.NsSpec);
            return _StartNode(node, extra, Util.ToNum(node.Block != null));
        }

        protected override void PostVisitImpl(NamespaceStmtNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override bool PreVisitImpl(WithStmtNode node)
        {
            Validation.AssertValue(node);

            string extra = node.IdentPaths[0].ToString();
            for (int i = 1; i < node.IdentPaths.Length; i++)
                extra += ", " + node.IdentPaths[i].ToString();
            return _StartNode(node, extra, Util.ToNum(node.Block != null));
        }

        protected override void PostVisitImpl(WithStmtNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override bool PreVisitImpl(IfStmtNode node)
        {
            Validation.AssertValue(node);
            return _StartNode(node, null, node.Else != null ? 3 : 2);
        }

        protected override void PostVisitImpl(IfStmtNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override bool PreVisitImpl(WhileStmtNode node)
        {
            Validation.AssertValue(node);
            return _StartNode(node, null, 2);
        }

        protected override void PostVisitImpl(WhileStmtNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override bool PreVisitImpl(ExecuteStmtNode node)
        {
            Validation.AssertValue(node);
            string extra = null;
            if (node.Namespace != null)
                extra = "in namespace " + ExtraFromNamespace(node.Namespace);
            return _StartNode(node, extra, 1);
        }

        protected override void PostVisitImpl(ExecuteStmtNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override bool PreVisitImpl(ImportStmtNode node)
        {
            Validation.AssertValue(node);
            string extra = null;
            if (node.Namespace != null)
                extra = "in namespace " + ExtraFromNamespace(node.Namespace);
            return _StartNode(node, extra, 1);
        }

        protected override void PostVisitImpl(ImportStmtNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override bool PreVisitImpl(ExprStmtNode node)
        {
            Validation.AssertValue(node);
            return _StartNode(node, null, 1);
        }

        protected override void PostVisitImpl(ExprStmtNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override bool PreVisitImpl(ParenNode node)
        {
            Validation.AssertValue(node);
            return _StartNode(node, null, 1);
        }

        protected override void PostVisitImpl(ParenNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override bool PreVisitImpl(DottedNameNode node)
        {
            Validation.AssertValue(node);
            return _StartNode(node, node.Right.Name.Value, 1);
        }

        protected override void PostVisitImpl(DottedNameNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override bool PreVisitImpl(GetIndexNode node)
        {
            Validation.AssertValue(node);
            return _StartNode(node, null, 1);
        }

        protected override void PostVisitImpl(GetIndexNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override bool PreVisitImpl(UnaryOpNode node)
        {
            Validation.AssertValue(node);
            return _StartNode(node, string.Format("{0}", node.Op), 1);
        }

        protected override void PostVisitImpl(UnaryOpNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override bool PreVisitImpl(BinaryOpNode node)
        {
            Validation.AssertValue(node);
            return _StartNode(node, string.Format("{0}", node.Op), 2);
        }

        protected override bool PreVisitImpl(InHasNode node)
        {
            Validation.AssertValue(node);
            return _StartNode(node, string.Format("{0}", node.Op), 2);
        }

        protected override void PostVisitImpl(BinaryOpNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override void PostVisitImpl(InHasNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override bool PreVisitImpl(CompareNode node)
        {
            Validation.AssertValue(node);
            _StartNode(node, null, node.Count).Verify();
            foreach (var cop in node.Operators)
                _WriteSub(node, "Op:{0}", cop);
            return true;
        }

        protected override void PostVisitImpl(CompareNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override bool PreVisitImpl(SliceItemNode node)
        {
            Validation.AssertValue(node);

            static string GetExtra(Token back, Token edge)
            {
                string e = "v";
                if (edge != null)
                    e = edge.GetStdString() + e;
                if (back != null)
                    e = "^" + e;
                return e;
            }

            string extra;
            int count;
            if (node.IsSimple)
            {
                Validation.Assert(node.Stop == null);
                Validation.Assert(node.Step == null);
                extra = GetExtra(node.StartBack, node.StartEdge);
                count = 1;
            }
            else
            {
                count = 0;
                extra = ":";
                if (node.Start != null)
                {
                    extra = GetExtra(node.StartBack, node.StartEdge) + ":";
                    count++;
                }
                if (node.Stop != null)
                {
                    extra += GetExtra(node.StopBack, null);
                    count++;
                }
                if (node.Step != null)
                {
                    extra += ":v";
                    count++;
                }
            }
            Validation.Assert(count == node.ValueCount);

            return _StartNode(node, extra, count);
        }

        protected override void PostVisitImpl(SliceItemNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override bool PreVisitImpl(IndexingNode node)
        {
            Validation.AssertValue(node);
            return _StartNode(node, null, 2);
        }

        protected override void PostVisitImpl(IndexingNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override bool PreVisitImpl(CallNode node)
        {
            Validation.AssertValue(node);
            var extra = node.IdentPath.ToString();
            return _StartNode(node, extra, 1);
        }

        protected override void PostVisitImpl(CallNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override bool PreVisitImpl(VariableDeclNode node)
        {
            Validation.AssertValue(node);
            string extra = node.Variable != null ? string.Format("var:{0}", node.Variable.Name) : "_";
            return _StartNode(node, extra, 1);
        }

        protected override void PostVisitImpl(VariableDeclNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override bool PreVisitImpl(DirectiveNode node)
        {
            Validation.AssertValue(node);
            return _StartNode(node, string.Format("dir:{0}", node.Directive), 1);
        }

        protected override void PostVisitImpl(DirectiveNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override bool PreVisitImpl(IfNode node)
        {
            Validation.AssertValue(node);
            return _StartNode(node, null, 3);
        }

        protected override void PostVisitImpl(IfNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override bool PreVisitImpl(RecordNode node)
        {
            Validation.AssertValue(node);
            return _StartNode(node, null, 1);
        }

        protected override void PostVisitImpl(RecordNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override bool PreVisitImpl(SequenceNode node)
        {
            Validation.AssertValue(node);
            return _StartNode(node, null, 1);
        }

        protected override void PostVisitImpl(SequenceNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override bool PreVisitImpl(TupleNode node)
        {
            Validation.AssertValue(node);
            return _StartNode(node, null, 1);
        }

        protected override void PostVisitImpl(TupleNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override bool PreVisitImpl(RecordProjectionNode node)
        {
            Validation.AssertValue(node);
            return _StartNode(node, node.IsConcat ? "concat" : null, 2);
        }

        protected override void PostVisitImpl(RecordProjectionNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override bool PreVisitImpl(TupleProjectionNode node)
        {
            Validation.AssertValue(node);
            return _StartNode(node, node.IsConcat ? "concat" : null, 2);
        }

        protected override void PostVisitImpl(TupleProjectionNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override bool PreVisitImpl(ValueProjectionNode node)
        {
            Validation.AssertValue(node);
            return _StartNode(node, null, 2);
        }

        protected override void PostVisitImpl(ValueProjectionNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override bool PreVisitImpl(ModuleProjectionNode node)
        {
            Validation.AssertValue(node);
            return _StartNode(node, null, 2);
        }

        protected override void PostVisitImpl(ModuleProjectionNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override bool PreVisitImpl(DefinitionStmtNode node)
        {
            Validation.AssertValue(node);
            return _StartNode(node, node.IdentPath?.ToString() ?? "this", 1);
        }

        protected override void PostVisitImpl(DefinitionStmtNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override bool PreVisitImpl(FuncStmtNode node)
        {
            Validation.AssertValue(node);
            return _StartNode(node, node.IdentPath.ToString(), 1);
        }

        protected override bool PreVisitImpl(ValueSymDeclNode node)
        {
            Validation.AssertValue(node);
            return _StartNode(node, $"{node.SymKind.ToStr()} {node.Name}", 1);
        }

        protected override void PostVisitImpl(ValueSymDeclNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override bool PreVisitImpl(FreeVarDeclNode node)
        {
            Validation.AssertValue(node);
            return _StartNode(node, node.Name.ToString(), node.ChildCount);
        }

        protected override void PostVisitImpl(FreeVarDeclNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override bool PreVisitImpl(ModuleNode node)
        {
            Validation.AssertValue(node);
            return _StartNode(node, null, 1);
        }

        protected override void PostVisitImpl(ModuleNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override void PostVisitImpl(FuncStmtNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override bool PreVisitImpl(TaskCmdStmtNode node)
        {
            Validation.AssertValue(node);
            return _StartNode(node, node.Token.ToString());
        }

        protected override void PostVisitImpl(TaskCmdStmtNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override bool PreVisitImpl(TaskProcStmtNode node)
        {
            Validation.AssertValue(node);
            return _StartNode(node, node.IdentPath?.ToString(), 1);
        }

        protected override void PostVisitImpl(TaskProcStmtNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override bool PreVisitImpl(TaskBlockStmtNode node)
        {
            Validation.AssertValue(node);
            return _StartNode(node, node.Token.ToString(),
                1 + Util.ToNum(node.With != null) + Util.ToNum(node.Prime != null));
        }

        protected override void PostVisitImpl(TaskBlockStmtNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }

        protected override bool PreVisitImpl(UserProcStmtNode node)
        {
            Validation.AssertValue(node);
            return _StartNode(node, node.Token.ToString(), 1 + Util.ToNum(node.Prime != null));
        }

        protected override void PostVisitImpl(UserProcStmtNode node)
        {
            Validation.AssertValue(node);
            _EndNode(node);
        }
    }
}