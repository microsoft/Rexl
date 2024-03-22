// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

using Microsoft.Rexl.Lex;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;

namespace Microsoft.Rexl.Parse;

using TSink = SbTextSink;

/// <summary>
/// Simple pretty-printing parse node visitor.
/// </summary>
public sealed class RexlPrettyPrinter : RexlTreeVisitor
{
    private const string _ErrorStr = "<error>";
    private const string _MissingStr = "<missing>";

    private const string _parenOpen = "(";
    private const string _parenClose = ")";
    private const string _squareOpen = "[";
    private const string _squareClose = "]";
    private const string _curlyOpen = "{ ";
    private const string _curlyClose = " }";
    private const string _comma = ", ";
    private const string _colon = " : ";
    private const string _colequ = " := ";
    private const string _semi = "; ";
    private const string _dot = ".";
    private const string _dol = "$";
    private const string _null = "null";
    private const string _this = "this";
    private const string _if = " if ";
    private const string _else = " else ";
    private const string _box = "_";

    private const string _import = "import ";
    private const string _namespace = "namespace";
    private const string _func = "func ";
    private const string _module = "module ";
    private const string _eval = "eval ";
    private const string _maximize = "maximize ";
    private const string _minimize = "minimize ";

    private const string _in = " in ";
    private const string _from = " from ";
    private const string _to = " to ";
    private const string _def = " def ";

    private readonly RexlLexer _lex;
    // We use these mostly as stacks. A couple places we want to "pop" (process) several
    // items in the same order as we pushed them, so need to be able to index.
    private readonly List<string> _sstack;
    private readonly List<Precedence> _pstack;

    private TSink _sink;
    private bool _lockSb;

    private RexlPrettyPrinter()
    {
        _lex = RexlLexer.Instance;
        _sstack = new List<string>();
        _pstack = new List<Precedence>();
        _sink = new TSink();
    }

    /// <summary>
    /// Checkout out a string builder.
    /// </summary>
    private TSink _Start()
    {
        Validation.Assert(!_lockSb);
        _lockSb = true;
        _sink.Builder.Clear();
        return _sink;
    }

    /// <summary>
    /// Check in the string builder and push its contents.
    /// </summary>
    private string _End(TSink sink)
    {
        Validation.Assert(_lockSb);
        Validation.Assert(sink == _sink);
        _lockSb = false;
        return _sink.Builder.ToString();
    }

    /// <summary>
    /// Check the string builder back in and push its contents.
    /// </summary>
    private void _PushEnd(TSink sink, Precedence prec)
    {
        Validation.Assert(_lockSb);
        Validation.Assert(sink == _sink);
        _lockSb = false;
        _Push(_sink.Builder.ToString(), prec);
    }

    /// <summary>
    /// Append the given string to the builder, optionally with parens.
    /// </summary>
    private TSink _Append(TSink sink, string str, bool needParens)
    {
        Validation.Assert(_lockSb);
        Validation.Assert(sink == _sink);
        if (needParens)
            return sink.TWrite(_parenOpen).TWrite(str).TWrite(_parenClose);
        return sink.TWrite(str);
    }

    /// <summary>
    /// Push a value on the stack.
    /// </summary>
    private void _Push(string str, Precedence prec)
    {
        Validation.AssertValue(str);
        Validation.Assert(_pstack.Count == _sstack.Count);
        _sstack.Add(str);
        _pstack.Add(prec);
        Validation.Assert(_pstack.Count == _sstack.Count);
    }

    /// <summary>
    /// Pop a value from the stack.
    /// </summary>
    private string _Pop(out Precedence prec)
    {
        Validation.AssertNonEmpty(_sstack);
        Validation.Assert(_pstack.Count == _sstack.Count);
        int index = _sstack.Count - 1;
        string str = _sstack[index];
        prec = _pstack[index];
        _sstack.RemoveAt(index);
        _pstack.RemoveAt(index);
        return str;
    }

    /// <summary>
    /// Reverse the top count values of the stack.
    /// </summary>
    private void _Reverse(int count)
    {
        Validation.AssertIndexInclusive(count, _sstack.Count);
        Validation.Assert(_pstack.Count == _sstack.Count);

        if (count > 1)
        {
            _sstack.Reverse(_sstack.Count - count, count);
            _pstack.Reverse(_pstack.Count - count, count);
        }
    }

    /// <summary>
    /// Pop the top value from the stack and append its contents, parenthesizing based
    /// on the given precedence minimum.
    /// </summary>
    private void _AppendPop(TSink sink, Precedence precMin)
    {
        string str = _Pop(out var prec);
        _Append(sink, str, prec < precMin);
    }

    private void _AppendIdent(TSink sink, Identifier ident)
    {
        Validation.AssertValue(sink);
        Validation.AssertValue(ident);

        if (ident.AtToken != null)
            sink.Write('@');
        ident.Token.Format(sink);
    }

    private void _AppendFullName(TSink sink, IdentPath idents)
    {
        Validation.AssertValue(idents);

        _AppendIdent(sink, idents.Idents[0]);
        for (int i = 1; i < idents.Idents.Length; i++)
        {
            sink.Write(_dot);
            _AppendIdent(sink, idents.Idents[i]);
        }
    }

    private string Finish(Precedence precMin)
    {
        Validation.Assert(_sstack.Count == 1);
        Validation.Assert(_pstack.Count == 1);
        var sink = _Start();
        _AppendPop(sink, precMin);
        return _End(sink);
    }

    /// <summary>
    /// Public entry point for pretty printing rexl parse trees.
    /// </summary>
    public static string Print(RexlNode node)
    {
        Validation.AssertValue(node);

        RexlPrettyPrinter pretty = new RexlPrettyPrinter();
        node.Accept(pretty);

        Precedence precMin;
        if (node is StmtNode)
            precMin = Precedence.Stmt;
        else if (node is StmtListNode)
            precMin = Precedence.StmtList;
        else
            precMin = Precedence.Expr;

        return pretty.Finish(precMin);
    }

    /// <summary>
    /// Public entry point for pretty printing a rexl parse tree with a minimum precedence,
    /// used for determining whether parentheses are needed around it.
    /// </summary>
    public static string Print(RexlNode node, Precedence precMin)
    {
        Validation.AssertValue(node);

        RexlPrettyPrinter pretty = new RexlPrettyPrinter();
        node.Accept(pretty);

        return pretty.Finish(precMin);
    }

    protected override void VisitImpl(ErrorNode node)
    {
        Validation.AssertValue(node);
        _Push(_ErrorStr, Precedence.Error);
    }

    protected override void VisitImpl(MissingValueNode node)
    {
        Validation.AssertValue(node);
        _Push(_MissingStr, Precedence.Error);
    }

    protected override void VisitImpl(NullLitNode node)
    {
        Validation.AssertValue(node);
        _Push(_null, Precedence.Atomic);
    }

    protected override void VisitImpl(BoolLitNode node)
    {
        Validation.AssertValue(node);
        _Push(_lex.GetFixedText(node.Value ? TokKind.KwdTrue : TokKind.KwdFalse), Precedence.Atomic);
    }

    protected override void VisitImpl(NumLitNode node)
    {
        Validation.AssertValue(node);
        _Push(node.Value.ToString(), Precedence.Atomic);
    }

    protected override void VisitImpl(TextLitNode node)
    {
        Validation.AssertValue(node);
        _Push(LexUtils.GetTextLiteral(node.Value), Precedence.Atomic);
    }

    protected override void VisitImpl(BoxNode node)
    {
        Validation.AssertValue(node);
        _Push(_box, Precedence.Atomic);
    }

    protected override void VisitImpl(ItNameNode node)
    {
        Validation.AssertValue(node);
        _Push(node.UpCount == 0 ? "it" : string.Format("it${0}", node.UpCount), Precedence.Atomic);
    }

    protected override void VisitImpl(ThisNameNode node)
    {
        Validation.AssertValue(node);
        _Push(_this, Precedence.Atomic);
    }

    protected override void VisitImpl(FirstNameNode node)
    {
        Validation.AssertValue(node);

        var sink = _Start();
        _AppendIdent(sink, node.Ident);
        _PushEnd(sink, Precedence.Atomic);
    }

    protected override void VisitImpl(MetaPropNode node)
    {
        Validation.AssertValue(node);

        TokKind tid = node.Token.Kind;
        Validation.Assert(tid == TokKind.Dol);

        var sink = _Start();
        _AppendFullName(sink, node.Left);
        sink.Write(_dol);
        _AppendIdent(sink, node.Right);
        _PushEnd(sink, Precedence.Primary);
    }

    protected override void VisitImpl(GotoStmtNode node)
    {
        Validation.AssertValue(node);
        Validation.Assert(node.Token.Kind == TokKind.KwdGoto);

        var sink = _Start();
        sink.Write("goto ");
        _AppendIdent(sink, node.Label);
        _PushEnd(sink, Precedence.Stmt);
    }

    protected override void VisitImpl(LabelStmtNode node)
    {
        Validation.AssertValue(node);
        Validation.Assert(node.Token.Kind == TokKind.Colon);

        var sink = _Start();
        _AppendIdent(sink, node.Label);
        sink.Write(':');
        _PushEnd(sink, Precedence.Stmt);
    }

    protected override void PostVisitImpl(StmtListNode node)
    {
        Validation.AssertValue(node);
        int count = node.Count;

        // Reverse the operands.
        _Reverse(count);

        var sink = _Start();
        bool needSemi = false;
        for (int i = 0; i < count; i++)
        {
            if (needSemi)
                sink.Write(_semi);
            else if (i > 0)
                sink.Write(' ');
            _AppendPop(sink, Precedence.Stmt);
            needSemi = node.Children[i].NeedsSemi;
        }
        _PushEnd(sink, Precedence.StmtList);
    }

    protected override void PostVisitImpl(ExprListNode node)
    {
        _PostVisitList(node, _comma, Precedence.Expr, Precedence.ExprList);
    }

    protected override void PostVisitImpl(SymListNode node)
    {
        _PostVisitList(node, _semi, Precedence.Expr, Precedence.SymList);
    }

    protected override void PostVisitImpl(SliceListNode node)
    {
        _PostVisitList(node, _comma, Precedence.Expr, Precedence.ExprList);
    }

    private void _PostVisitList<TItem>(VariadicBase<TItem> node, string op, Precedence precItem, Precedence precList)
        where TItem : RexlNode
    {
        Validation.AssertValue(node);
        int count = node.Count;

        // Reverse the operands.
        _Reverse(count);

        var sink = _Start();
        string sep = "";
        for (int i = 0; i < count; i++)
        {
            sink.Write(sep);
            sep = op;
            _AppendPop(sink, precItem);
        }
        _PushEnd(sink, precList);
    }

    private void AppendNs(TSink sink, NamespaceSpec nss)
    {
        Validation.AssertValue(sink);
        Validation.AssertValue(nss);

        sink.Write(_namespace);
        if (nss.IdentPath != null)
        {
            sink.Write(' ');
            _AppendFullName(sink, nss.IdentPath);
        }
        else if (nss.IsRooted)
            sink.Write(" @");
    }

    protected override void PostVisitImpl(BlockStmtNode node)
    {
        Validation.AssertValue(node);

        var sink = _Start();
        sink.Write(_curlyOpen);
        _AppendPop(sink, Precedence.StmtList);
        sink.Write(_curlyClose);

        _PushEnd(sink, Precedence.Stmt);
    }

    protected override void PostVisitImpl(NamespaceStmtNode node)
    {
        Validation.AssertValue(node);

        var sink = _Start();
        AppendNs(sink, node.NsSpec);

        if (node.Block != null)
        {
            sink.Write(' ');
            _AppendPop(sink, Precedence.StmtList);
        }

        _PushEnd(sink, Precedence.Stmt);
    }

    protected override void PostVisitImpl(WithStmtNode node)
    {
        Validation.AssertValue(node);

        var sink = _Start().TWrite("with ");
        string pre = "";
        foreach (var path in node.IdentPaths)
        {
            sink.Write(pre);
            _AppendFullName(sink, path);
            pre = ", ";
        }

        if (node.Block != null)
        {
            sink.Write(' ');
            _AppendPop(sink, Precedence.StmtList);
        }

        _PushEnd(sink, Precedence.Stmt);
    }

    protected override void PostVisitImpl(IfStmtNode node)
    {
        Validation.AssertValue(node);

        _Reverse(node.Else != null ? 3 : 2);

        var sink = _Start().TWrite("if (");
        _AppendPop(sink, Precedence.Expr);
        sink.Write(") ");
        _AppendPop(sink, Precedence.Stmt);
        if (node.Else != null)
        {
            if (node.Then is not BlockStmtNode)
                sink.Write(';');
            sink.Write(" else ");
            _AppendPop(sink, Precedence.Stmt);
        }

        _PushEnd(sink, Precedence.Stmt);
    }

    protected override void PostVisitImpl(WhileStmtNode node)
    {
        Validation.AssertValue(node);

        _Reverse(2);

        var sink = _Start().TWrite("while (");
        _AppendPop(sink, Precedence.Expr);
        sink.Write(") ");
        _AppendPop(sink, Precedence.Stmt);

        _PushEnd(sink, Precedence.Stmt);
    }

    protected override void PostVisitImpl(ExecuteStmtNode node)
    {
        Validation.AssertValue(node);

        var sink = _Start();
        sink.Write(_import);
        _AppendPop(sink, Precedence.Expr);

        var ns = node.Namespace;
        if (ns != null)
        {
            sink.Write(" in ");
            AppendNs(sink, ns);
        }
        _PushEnd(sink, Precedence.Stmt);
    }

    protected override void PostVisitImpl(ImportStmtNode node)
    {
        Validation.AssertValue(node);

        var sink = _Start();
        sink.Write(_import);
        _AppendPop(sink, Precedence.Expr);

        var ns = node.Namespace;
        if (ns != null)
        {
            sink.Write(" in ");
            AppendNs(sink, ns);
        }
        _PushEnd(sink, Precedence.Stmt);
    }

    protected override void PostVisitImpl(ExprStmtNode node)
    {
        Validation.AssertValue(node);

        // REVIEW: Should we provide any indication that the Expr is being wrapped as a statement?
        // If not, nothing to do (the Expr is already on the stack).
    }

    protected override void PostVisitImpl(DefinitionStmtNode node)
    {
        Validation.AssertValue(node);
        Validation.Assert(node.ForThis == (node.DefnKind == DefnKind.This));

        var sink = _Start();

        switch (node.DefnKind)
        {
        case DefnKind.Publish: sink.Write("publish "); break;
        case DefnKind.Primary: sink.Write("primary"); break;
        case DefnKind.Stream: sink.Write("stream "); break;

        case DefnKind.This:
            Validation.Assert(node.IdentPath == null);
            break;

        default:
            Validation.Assert(node.DefnKind == DefnKind.None);
            break;
        }

        if (node.IdentPath != null)
            _AppendFullName(sink, node.IdentPath);
        else
            sink.Write("this");

        sink.Write(_colequ);
        _AppendPop(sink, Precedence.Expr);
        _PushEnd(sink, Precedence.Stmt);
    }

    protected override void PostVisitImpl(FuncStmtNode node)
    {
        Validation.AssertValue(node);

        var sink = _Start();

        sink.Write(_func);
        _AppendFullName(sink, node.IdentPath);
        sink.Write(_parenOpen);
        var pre = "";
        foreach (var name in node.ParamIdents)
        {
            sink.Write(pre);
            pre = _comma;
            _AppendIdent(sink, name);
        }
        sink.TWrite(_parenClose).Write(_colequ);
        _AppendPop(sink, Precedence.Expr);
        _PushEnd(sink, Precedence.Stmt);
    }

    protected override void PostVisitImpl(ParenNode node)
    {
        Validation.AssertValue(node);
        // Nothing to do, since pretty printing does its own parenthesizing.
    }

    protected override void PostVisitImpl(DottedNameNode node)
    {
        Validation.AssertValue(node);

        var sink = _Start();

        _AppendPop(sink, Precedence.Primary);
        sink.Write(_dot);
        node.Right.Token.Format(sink);

        _PushEnd(sink, Precedence.Primary);
    }

    protected override bool PreVisitImpl(GetIndexNode node)
    {
        Validation.AssertValue(node);

        var sink = _Start();

        sink.Write('#');
        if (node.NameChild != null)
            _AppendIdent(sink, node.NameChild.Ident);
        else if (node.ItChild != null)
        {
            if (node.ItChild.UpCount == 0)
                sink.Write("it");
            else
                sink.TWrite("it$").Write(node.ItChild.UpCount);
        }
        else if (node.Slot > 0)
            sink.Write(node.Slot);

        _PushEnd(sink, Precedence.Atomic);
        return false;
    }

    protected override void PostVisitImpl(GetIndexNode node)
    {
        // Shouldn't get here.
        Validation.Assert(false);
    }

    protected override void PostVisitImpl(UnaryOpNode node)
    {
        Validation.AssertValue(node);
        Validation.Assert(_sstack.Count >= 1);
        Validation.Assert(_pstack.Count >= 1);

        var precCur = Precedence.PrefixUnary;
        var precMin = Precedence.PrefixUnary;
        bool prefix = true;
        string op = null;
        var tid = node.Token.Kind;
        switch (node.Op)
        {
        default:
            Validation.Assert(false);
            op = _ErrorStr;
            precCur = Precedence.Error;
            precMin = Precedence.Lim;
            break;

        // Prefix.
        case UnaryOp.Not:
            Validation.Assert(tid == TokKind.KwdNot | tid == TokKind.Bng);
            if (tid == TokKind.KwdNot)
            {
                precCur = Precedence.Not;
                precMin = Precedence.Not;
            }
            break;
        case UnaryOp.Negate:
            Validation.Assert(tid == TokKind.Sub);
            break;
        case UnaryOp.Posate:
            Validation.Assert(tid == TokKind.Add);
            break;
        case UnaryOp.BitNot:
            Validation.Assert(tid == TokKind.KwdBnot | tid == TokKind.Tld);
            if (tid == TokKind.KwdBnot)
            {
                precCur = Precedence.BitNot;
                precMin = Precedence.BitNot;
            }
            break;

        // Postfix.
        case UnaryOp.Percent:
            Validation.Assert(tid == TokKind.Per);
            prefix = false;
            precCur = Precedence.PostfixUnary;
            precMin = Precedence.PostfixUnary;
            break;
        }

        op = op ?? _lex.GetFixedText(tid);

        var sink = _Start();
        if (prefix)
        {
            sink.Write(op);
            if (LexUtils.NeedSpaceBeforeIdent(_sink.Builder))
                sink.Write(' ');
        }
        _AppendPop(sink, precMin);
        if (!prefix)
            sink.Write(op);

        _PushEnd(sink, precCur);
    }

    protected override void PostVisitImpl(BinaryOpNode node)
    {
        Validation.AssertValue(node);

        TokKind tid = node.Token.Kind;
        Precedence precCur;
        Precedence? precMinLeft = null;
        Precedence? precMinRight = null;
        string op = null;

        bool space = true;
        switch (node.Op)
        {
        // These are the odd-ball cases.
        default:
            Validation.Assert(false);
            precCur = Precedence.Error;
            precMinLeft = precMinRight = Precedence.Lim;
            op = _ErrorStr;
            break;

        case BinaryOp.Error:
            precCur = Precedence.Error;
            precMinLeft = precMinRight = Precedence.Lim;
            op = _ErrorStr;
            break;
        case BinaryOp.Power:
            Validation.Assert(tid == TokKind.MulMul | tid == TokKind.Car);
            tid = TokKind.Car;
            // Right associative, and without spaces around the operator.
            precMinLeft = (precCur = Precedence.Power) + 1;
            precMinRight = Precedence.PrefixUnary;
            space = false;
            break;
        case BinaryOp.Coalesce:
            Validation.Assert(tid == TokKind.QueQue);
            // Right associative.
            precMinLeft = (precCur = Precedence.Coalesce) + 1;
            precMinRight = Precedence.Coalesce;
            break;

        // These cases are all left associative.
        case BinaryOp.Or:
            Validation.Assert(tid == TokKind.KtxOr | tid == TokKind.BarBar);
            tid = TokKind.KtxOr;
            precCur = Precedence.Or;
            break;
        case BinaryOp.Xor:
            Validation.Assert(tid == TokKind.KtxXor | tid == TokKind.CarCar);
            tid = TokKind.KtxXor;
            precCur = Precedence.Xor;
            break;
        case BinaryOp.And:
            Validation.Assert(tid == TokKind.KtxAnd | tid == TokKind.AmpAmp);
            tid = TokKind.KtxAnd;
            precCur = Precedence.And;
            break;

        case BinaryOp.IntDiv:
            Validation.Assert(tid == TokKind.KtxDiv);
            precCur = Precedence.Mul;
            break;
        case BinaryOp.IntMod:
            Validation.Assert(tid == TokKind.KtxMod);
            precCur = Precedence.Mul;
            break;
        case BinaryOp.Shl:
            Validation.Assert(tid == TokKind.LssLss | tid == TokKind.KtxShl);
            tid = TokKind.KtxShl;
            precCur = Precedence.Shift;
            break;
        case BinaryOp.Shr:
            Validation.Assert(tid == TokKind.GrtGrt | tid == TokKind.KtxShr);
            tid = TokKind.KtxShr;
            precCur = Precedence.Shift;
            break;
        case BinaryOp.Shri:
            Validation.Assert(tid == TokKind.KtxShri);
            precCur = Precedence.Shift;
            break;
        case BinaryOp.Shru:
            Validation.Assert(tid == TokKind.GrtGrtGrt | tid == TokKind.KtxShru);
            tid = TokKind.KtxShru;
            precCur = Precedence.Shift;
            break;
        case BinaryOp.Min:
            Validation.Assert(tid == TokKind.KtxMin);
            precCur = Precedence.MinMax;
            break;
        case BinaryOp.Max:
            Validation.Assert(tid == TokKind.KtxMax);
            precCur = Precedence.MinMax;
            break;
        case BinaryOp.BitOr:
            Validation.Assert(tid == TokKind.KtxBor);
            precCur = Precedence.BitOr;
            break;
        case BinaryOp.BitXor:
            Validation.Assert(tid == TokKind.KtxBxor);
            precCur = Precedence.BitXor;
            break;
        case BinaryOp.BitAnd:
            Validation.Assert(tid == TokKind.KtxBand);
            precCur = Precedence.BitAnd;
            break;

        case BinaryOp.GenConcat:
            Validation.Assert(tid == TokKind.Amp);
            precCur = Precedence.Concat;
            break;
        case BinaryOp.SeqConcat:
            Validation.Assert(tid == TokKind.AddAdd);
            precCur = Precedence.Concat;
            break;
        case BinaryOp.Add:
            Validation.Assert(tid == TokKind.Add);
            precCur = Precedence.Add;
            break;
        case BinaryOp.Sub:
            Validation.Assert(tid == TokKind.Sub);
            precCur = Precedence.Add;
            break;
        case BinaryOp.Mul:
            Validation.Assert(tid == TokKind.Mul);
            precCur = Precedence.Mul;
            break;
        case BinaryOp.Div:
            Validation.Assert(tid == TokKind.Div);
            precCur = Precedence.Mul;
            break;
        case BinaryOp.Pipe:
            Validation.Assert(tid == TokKind.EquGrt | tid == TokKind.Bar);
            tid = TokKind.Bar;
            precCur = Precedence.Pipe;
            break;
        }

        if (op == null)
            op = _lex.GetFixedText(tid);

        // Swap the top two values.
        _Reverse(2);

        // Default to left-associativity.
        var sink = _Start();
        _AppendPop(sink, precMinLeft ?? precCur);
        if (space)
            sink.TWrite(' ').TWrite(op).Write(' ');
        else
            sink.Write(op);
        _AppendPop(sink, precMinRight ?? precCur + 1);

        _PushEnd(sink, precCur);
    }

    protected override void PostVisitImpl(InHasNode node)
    {
        Validation.AssertValue(node);

        // Swap the top two values.
        _Reverse(2);

        // We have left-associativity.
        var sink = _Start();
        _AppendPop(sink, Precedence.InHas);
        sink.Write(' ');
        if (node.Not != null)
        {
            sink.Write(node.Not.GetTextString());
            if (node.Not.Kind == TokKind.KwdNot)
                sink.Write(' ');
        }
        if (node.Tld != null)
            sink.Write(node.Tld.GetTextString());
        sink.TWrite(_lex.GetFixedText(node.Token.Kind)).Write(' ');
        _AppendPop(sink, Precedence.InHas + 1);

        _PushEnd(sink, Precedence.InHas);
    }

    protected override void PostVisitImpl(CompareNode node)
    {
        Validation.AssertValue(node);

        // Swap the top count values.
        _Reverse(node.Count);

        var sink = _Start();
        _AppendPop(sink, Precedence.Compare + 1);
        for (int i = 1; i < node.Count; i++)
        {
            sink
                .TWrite(' ')
                .TWrite(node.Operators[i - 1].GetStr())
                .Write(' ');
            _AppendPop(sink, Precedence.Compare + 1);
        }

        _PushEnd(sink, Precedence.Compare);
    }

    protected override void PostVisitImpl(SliceItemNode node)
    {
        Validation.AssertValue(node);
        Validation.Assert(node.Start != null || node.Colon1 != null);
        Validation.Assert(node.Step == null || node.Colon2 != null);

        if (node.Colon1 == null)
        {
            string prefix = "";
            if (node.StartBack != null)
                prefix = "^";
            if (node.StartEdge != null)
                prefix += node.StartEdge.GetStdString();
            if (prefix != "")
            {
                var idx = _Pop(out var prec);
                _Push(prefix + idx, Precedence.Expr);
            }
            return;
        }
        Validation.Assert(node.StartEdge == null);

        if (node.Start == null && node.Stop == null && node.Colon2 == null)
        {
            _Push(":", Precedence.Expr);
            return;
        }

        var sink = _Start();

        int num = 0;
        if (node.Start != null)
            num++;
        if (node.Stop != null)
            num++;
        if (node.Step != null)
            num++;
        _Reverse(num);

        if (node.Start != null)
        {
            if (node.StartBack != null)
                sink.Write('^');
            _AppendPop(sink, Precedence.Expr);
        }
        sink.Write(':');
        if (node.Stop != null)
        {
            if (node.StopBack != null)
                sink.Write('^');
            if (node.StopStar != null)
                sink.Write('*');
            _AppendPop(sink, Precedence.Expr);
        }
        if (node.Colon2 != null)
        {
            sink.Write(':');
            if (node.Step != null)
                _AppendPop(sink, Precedence.Expr);
        }
        _PushEnd(sink, Precedence.Expr);
    }

    protected override void PostVisitImpl(IndexingNode node)
    {
        Validation.AssertValue(node);

        var sink = _Start();

        _Reverse(2);
        _AppendPop(sink, Precedence.Primary);
        sink.Write(_squareOpen);
        _AppendPop(sink, Precedence.ExprList);
        sink.Write(_squareClose);
        _PushEnd(sink, Precedence.Primary);
    }

    protected override bool PreVisitImpl(CallNode node)
    {
        Validation.AssertValue(node);
        if (node.TokPipe == null)
            return true;

        Validation.AssertValue(node.TokPipe);

        int count = node.Args.Count;
        Validation.Assert(count > 0);

        var first = node.Args.Children[0];
        var vdn = first as VariableDeclNode;
        (vdn?.Value ?? first).Accept(this);
        for (int i = 1; i < count; i++)
            node.Args.Children[i].Accept(this);
        _Reverse(count);

        var sink = _Start();
        _AppendPop(sink, Precedence.PostfixUnary);
        sink.Write(_lex.GetFixedText(node.TokPipe.Kind));
        _AppendFullName(sink, node.IdentPath);
        sink.Write(_parenOpen);

        if (vdn != null)
        {
            sink.Write("as ");
            if (vdn.Variable != null)
                vdn.Variable.Token.Format(sink);
            else
                sink.Write(_box);
            sink.Write(", ");
        }

        var sep = "";
        for (int i = 1; i < count; i++)
        {
            sink.Write(sep);
            sep = _comma;
            _AppendPop(sink, Precedence.Expr);
        }

        sink.Write(_parenClose);
        _PushEnd(sink, Precedence.Primary);
        return false;
    }

    protected override void PostVisitImpl(CallNode node)
    {
        Validation.AssertValue(node);

        var sink = _Start();

        _AppendFullName(sink, node.IdentPath);

        sink.Write(_parenOpen);
        _AppendPop(sink, Precedence.ExprList);
        sink.Write(_parenClose);
        _PushEnd(sink, Precedence.Primary);
    }

    protected override void PostVisitImpl(VariableDeclNode node)
    {
        Validation.AssertValue(node);

        var sink = _Start();
        if (node.Variable != null)
            node.Variable.Token.Format(sink);
        else
            sink.Write(_box);
        sink.Write(_colon);
        _AppendPop(sink, Precedence.Expr);
        _PushEnd(sink, Precedence.Expr);
    }

    protected override void PostVisitImpl(DirectiveNode node)
    {
        Validation.AssertValue(node);

        var sink = _Start();
        sink.TWrite(node.Directive.ToSrcText()).Write(' ');
        _AppendPop(sink, Precedence.Expr);
        _PushEnd(sink, Precedence.Expr);
    }

    protected override void PostVisitImpl(IfNode node)
    {
        Validation.AssertValue(node);

        var sink = _Start();

        // Bring Head to the top.
        _Reverse(3);

        _AppendPop(sink, Precedence.If + 1);
        sink.Write(_if);
        _AppendPop(sink, Precedence.Expr);
        sink.Write(_else);
        _AppendPop(sink, Precedence.If);
        _PushEnd(sink, Precedence.If);
    }

    protected override void PostVisitImpl(RecordNode node)
    {
        Validation.AssertValue(node);

        var sink = _Start();

        sink.Write(_curlyOpen);
        _AppendPop(sink, Precedence.ExprList);
        sink.Write(_curlyClose);
        _PushEnd(sink, Precedence.Atomic);
    }

    protected override void PostVisitImpl(SequenceNode node)
    {
        Validation.AssertValue(node);

        var sink = _Start();

        sink.Write(_squareOpen);
        _AppendPop(sink, Precedence.ExprList);
        sink.Write(_squareClose);
        _PushEnd(sink, Precedence.Atomic);
    }

    protected override void PostVisitImpl(TupleNode node)
    {
        Validation.AssertValue(node);

        var sink = _Start();

        sink.Write(_parenOpen);
        _AppendPop(sink, Precedence.ExprList);
        if (node.Items.Count == 1)
            sink.Write(',');
        sink.Write(_parenClose);
        _PushEnd(sink, Precedence.Atomic);
    }

    protected override void PostVisitImpl(RecordProjectionNode node)
    {
        Validation.AssertValue(node);

        var sink = _Start();
        _Reverse(2);
        _AppendPop(sink, Precedence.Primary);
        sink.Write(node.IsConcat ? "+>" : "->");
        _AppendPop(sink, Precedence.Atomic);
        _PushEnd(sink, Precedence.Primary);
    }

    protected override void PostVisitImpl(TupleProjectionNode node)
    {
        Validation.AssertValue(node);

        var sink = _Start();
        _Reverse(2);
        _AppendPop(sink, Precedence.Primary);
        sink.Write(node.IsConcat ? "+>" : "->");
        _AppendPop(sink, Precedence.Atomic);
        _PushEnd(sink, Precedence.Primary);
    }

    protected override void PostVisitImpl(ValueProjectionNode node)
    {
        Validation.AssertValue(node);

        var sink = _Start();
        _Reverse(2);
        _AppendPop(sink, Precedence.Primary);
        sink.Write("->(");
        _AppendPop(sink, Precedence.Expr);
        sink.Write(')');
        _PushEnd(sink, Precedence.Primary);
    }

    protected override void PostVisitImpl(ModuleProjectionNode node)
    {
        Validation.AssertValue(node);

        var sink = _Start();
        _Reverse(2);
        _AppendPop(sink, Precedence.Primary);
        AppendWithPop(sink, node.Record, "=>");
        _PushEnd(sink, Precedence.Primary);
    }

    protected override void PostVisitImpl(ValueSymDeclNode node)
    {
        Validation.AssertValue(node);

        var sink = _Start();
        sink.TWrite(node.SymKind.ToStr()).Write(' ');
        _AppendIdent(sink, node.Name);
        sink.Write(_colequ);
        _AppendPop(sink, Precedence.Expr);
        _PushEnd(sink, Precedence.Expr);
    }

    protected override void PostVisitImpl(FreeVarDeclNode node)
    {
        Validation.AssertValue(node);

        int carg = node.ChildCount;
        if (carg > 1)
            _Reverse(carg);

        var sink = _Start();

        sink.TWrite(node.SymKind.ToStr()).Write(' ');
        _AppendIdent(sink, node.Name);
        if (node.ValueIn != null)
        {
            sink.Write(_in);
            _AppendPop(sink, Precedence.Expr);
        }
        if (node.ValueFrom != null)
        {
            sink.Write(_from);
            _AppendPop(sink, Precedence.Expr);
        }
        if (node.ValueTo != null)
        {
            sink.Write(_to);
            _AppendPop(sink, Precedence.Expr);
        }
        if (node.ValueDef != null)
        {
            sink.Write(_def);
            _AppendPop(sink, Precedence.Expr);
        }
        if (node.TokOptReq != null)
            sink.TWrite(' ').Write(_lex.GetFixedText(node.TokOptReq.Kind));

        _PushEnd(sink, Precedence.Expr);
    }

    protected override void PostVisitImpl(ModuleNode node)
    {
        Validation.AssertValue(node);

        var sink = _Start();

        sink.Write("module { ");
        _AppendPop(sink, Precedence.StmtList);
        sink.Write(" }");

        _PushEnd(sink, Precedence.Primary);
    }

    /// <summary>
    /// Handle a <c>with { ... }</c> or <c>with (...)</c>.
    /// </summary>
    private void AppendWithPop(TSink sink, ExprNode node, string prefix = " with ")
    {
        sink.Write(prefix);
        bool parens = node.Kind != NodeKind.Record;
        if (parens)
            sink.Write("(");
        _AppendPop(sink, Precedence.Error);
        if (parens)
            sink.Write(")");
    }

    protected override void PostVisitImpl(TaskCmdStmtNode node)
    {
        Validation.AssertValue(node);

        var sink = _Start();

        sink.TWrite(RexlLexer.Instance.GetFixedText(node.Cmd)).Write(' ');
        _AppendFullName(sink, node.Name);
        _PushEnd(sink, Precedence.Stmt);
    }

    protected override void PostVisitImpl(TaskProcStmtNode node)
    {
        Validation.AssertValue(node);

        var sink = _Start();

        sink.TWrite(RexlLexer.Instance.GetFixedText(node.Modifier)).Write(' ');
        if (node.IdentPath != null)
        {
            _AppendFullName(sink, node.IdentPath);
            sink.Write(" as ");
        }
        _AppendPop(sink, Precedence.Primary);

        _PushEnd(sink, Precedence.Stmt);
    }

    protected override void PostVisitImpl(TaskBlockStmtNode node)
    {
        Validation.AssertValue(node);

        var sink = _Start();

        sink.TWrite(RexlLexer.Instance.GetFixedText(node.Modifier)).Write(' ');
        _AppendFullName(sink, node.IdentPath);

        int count = 1 + Util.ToNum(node.With != null) + Util.ToNum(node.Prime != null);
        _Reverse(count);

        if (node.With != null)
            AppendWithPop(sink, node.With);

        if (node.Prime != null)
        {
            sink.Write(" prime ");
            _AppendPop(sink, Precedence.Stmt);
        }

        sink.Write(" play ");
        _AppendPop(sink, Precedence.Stmt);

        _PushEnd(sink, Precedence.Stmt);
    }

    protected override void PostVisitImpl(UserProcStmtNode node)
    {
        Validation.AssertValue(node);

        var sink = _Start();

        sink.Write("proc ");
        _AppendFullName(sink, node.IdentPath);
        sink.Write(_parenOpen);
        var pre = "";
        foreach (var name in node.ParamIdents)
        {
            sink.Write(pre);
            pre = _comma;
            _AppendIdent(sink, name);
        }
        sink.Write(_parenClose);

        if (node.Prime != null)
        {
            _Reverse(2);
            sink.Write(" prime ");
            _AppendPop(sink, Precedence.Stmt);
        }

        sink.Write(" play ");
        _AppendPop(sink, Precedence.Stmt);

        _PushEnd(sink, Precedence.Stmt);
    }
}
