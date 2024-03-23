// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

using Microsoft.Rexl;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Lex;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;

namespace DocBench;

internal partial class EditNodeForm : Form
{
    private readonly Executor _exec;
    private readonly Func<string, bool> _isNameValid;
    private readonly NPath _nameCloak;
    private long _version;
    private long _versionBound;
    private RexlFormula _fma;
    private BoundFormula _bfma;
    private NPath _fullName;

    private EditNodeForm(Executor exec, Func<string, bool> isNameValid, NPath nameCloak)
    {
        Validation.AssertValue(exec);
        Validation.AssertValue(isNameValid);
        _exec = exec;
        _isNameValid = isNameValid;
        _nameCloak = nameCloak;

        InitializeComponent();
        _splitStatic.SplitterWidth = 8;
        UpdateOk();
    }

    public static EditNodeForm CreateAdd(Executor exec, Func<string, bool> isNameValid)
    {
        Validation.AssertValue(exec);
        Validation.AssertValue(isNameValid);
        var res = new EditNodeForm(exec, isNameValid, default);
        res._checkSimpleRename.Visible = false;
        res._checkSimpleRename.Enabled = false;
        return res;
    }

    public static EditNodeForm CreateEdit(Executor exec, Func<string, bool> isNameValid, NPath nameCur)
    {
        Validation.AssertValue(exec);
        var res = new EditNodeForm(exec, isNameValid, nameCur);
        res._checkSimpleRename.Visible = true;
        res._checkSimpleRename.Enabled = true;
        return res;
    }

    public string NameValue
    {
        get { return _txtName.Text; }
        set { _txtName.Text = value; }
    }

    public string ExprValue
    {
        get { return _txtExpr.Text; }
        set { _txtExpr.Text = value; }
    }

    public string ExtraValue
    {
        get { return _txtExtra.Text; }
        set { _txtExtra.Text = value; }
    }

    public bool IsSimpleRename
    {
        get { return _checkSimpleRename.Checked; }
    }

    private void UpdateOk()
    {
        if (string.IsNullOrWhiteSpace(_txtExpr.Text))
            _btnOk.Enabled = false;
        else
            _btnOk.Enabled = _isNameValid(_txtName.Text);
    }

    private void _txtName_TextChanged(object sender, EventArgs e)
    {
        UpdateOk();
    }

    private void _txtExpr_TextChanged(object sender, EventArgs e)
    {
        UpdateOk();
        _version++;
        _timer.Start();
    }

    private void _txtExpr_SuggestionUpdate(object sender, EventArgs e)
    {
        _timer.Start();
    }

    private void _timer_Tick(object sender, EventArgs e)
    {
        _timer.Stop();

        if (_versionBound < _version)
        {
            BindExpr();
            _versionBound = _version;
        }

        SuggestNodeInfo();
    }

    private void BindExpr()
    {
        var str = _txtName.Text;
        int ich = 0;
        if (!LexUtils.TryLexPath(ref ich, str, out _fullName) || ich != str.Length)
            _fullName = NPath.Root;

        _fma = RexlFormula.Create(SourceContext.Create(_txtExpr.Text));
        _bfma = _exec.BindTrial(_fma, _nameCloak, _fullName);

        var sink = new SbTypeSink();
        sink.WriteLine("Parse Tree: {0}", _fma.ParseTree);

        void WriteNote(string prefix, RexlNode node)
        {
            sink.Write(prefix);

            // If TryGetNodeDType returns false, we still print default(DType) ('x').
            _bfma.TryGetNodeType(node, out DType type);
            sink.TWrite("DType:").WriteType(type);

            _bfma.TryGetNodeScopeInfo(node, out var scopes);
            if (scopes == null)
                return;

            sink.Write(", Scopes:[");
            string pre = "";
            foreach (var scope in scopes)
            {
                sink.Write(pre);
                pre = ", ";
                if (scope.Alias.IsValid)
                    sink.WriteEscapedName(scope.Alias).Write(':');
                sink.WriteType(scope.Type);
            }
            sink.Write(']');
        }

        sink.WriteLine("Parse Tree Dump: ");
        RexlTreeDumper.Print(sink, _fma.ParseTree, "", _txtExpr.Text, WriteNote).WriteLine();
        sink.WriteLine();

        sink.WriteLine("Bound Tree: {0}", _bfma.BoundTree);
        sink.WriteLine();

        if (_fma.HasDiagnostics)
        {
            sink.WriteLine("=== Parse Diagnostics ===");
            foreach (var diag in _fma.Diagnostics)
            {
                diag.Format(sink);
                sink.WriteLine();
            }
        }

        if (_bfma.HasDiagnostics)
        {
            sink.WriteLine("=== Bind Diagnostics ===");
            foreach (var diag in _bfma.Diagnostics)
            {
                diag.Format(sink);
                sink.WriteLine();
            }
        }

        if (_bfma.IsGood)
            sink.TWrite("Type: ").WriteType(_bfma.BoundTree.Type);

        _txtErrors.Text = sink.Builder.ToString();
    }

    private void AppendTokenInfo(StringBuilder sb, Immutable.Array<Token> tokens, int ichMin, int ichLim, int iTokMin, int iTokLim)
    {
        Validation.AssertValue(sb);
        Validation.Assert(iTokMin <= iTokLim);

        // Display token information.
        sb.AppendLine("=== Token Information ===");

        if (iTokLim == iTokMin)
        {
            sb.AppendFormat("No token in character range ({0},{1}).", ichMin, ichLim);
            sb.AppendLine();
            if (iTokMin >= 1)
            {
                sb.AppendFormat("Token immediately left: {0}", tokens[iTokMin - 1]);
                sb.AppendLine();
            }

            if (iTokLim < tokens.Length)
            {
                sb.AppendFormat("Token immediately right: {0}", tokens[iTokLim]);
                sb.AppendLine();
            }
        }
        else if (iTokLim == iTokMin + 1)
            sb.AppendFormat("Token in character range ({1},{2}): {0}", tokens[iTokMin].Render(), ichMin, ichLim);
        else
        {
            sb.AppendFormat("Tokens in character range ({0},{1}).", ichMin, ichLim);
            sb.AppendLine();
            for (int i = iTokMin; i < iTokLim; i++)
            {
                var token = tokens[i];
                sb.AppendFormat("{0}:{1}  ", token.Kind, token.Range);
            }
        }
        sb.AppendLine();

        sb.AppendLine();
    }

    private void AppendParseNodeInfo(StringBuilder sb, BoundFormula bndFma, RexlNode left, RexlNode right)
    {
        Validation.AssertValue(sb);
        Validation.AssertValueOrNull(left);
        Validation.AssertValueOrNull(right);

        sb.AppendLine("=== Parse Node Information ===");

        if (left == right)
        {
            if (left == null)
            {
                sb.AppendLine("No parse node in selection.");
                sb.AppendLine();
                return;
            }

            AppendParseNodeInfoCore(sb, bndFma, left, "Node");
            return;
        }

        if (left != null)
        {
            AppendParseNodeInfoCore(sb, bndFma, left, "Left Node");
            sb.AppendLine();
        }

        if (right != null)
        {
            AppendParseNodeInfoCore(sb, bndFma, right, "Right Node");
            sb.AppendLine();
        }

        sb.AppendLine();
    }

    private void AppendParseNodeInfoCore(StringBuilder sb, BoundFormula bndFma, RexlNode node, string prefix)
    {
        Validation.AssertValue(sb);
        Validation.AssertValueOrNull(node);

        bndFma.TryGetNodeType(node, out DType type);
        sb.AppendFormat("{4}: {0}, range: {1}, full range: {2}, type: {3}", node, node.GetRange(), node.GetFullRange(), type, prefix);
        sb.AppendLine();

        if (bndFma.TryGetNodeScopeInfo(node, out NestedScopeInfo[] scopes))
        {
            Validation.AssertValue(scopes);
            Validation.Assert(scopes.Length > 0);
            sb.AppendLine("  Scopes:");

            for (int iscp = 0; iscp < scopes.Length; iscp++)
            {
                var scope = scopes[iscp];
                sb.Append("    it");
                for (int j = 0; j < iscp; j++)
                    sb.Append(".up");
                sb.Append(": ");
                if (scope.Alias.IsValid)
                    sb.AppendFormat("{0}: ", scope.Alias.Escape());
                sb.AppendLine(scope.Type.Serialize());
            }
        }

        sb.AppendLine("  Path to root:");

        int indent = 0;
        RexlNode n = node;
        while (n != null)
        {
            bndFma.TryGetNodeType(n, out DType t);
            sb.Append("    ");
            for (int i = 0; i < indent; i++)
            {
                if (i < indent - 1)
                    sb.Append("    ");
                else
                    sb.Append("|-->");
            }

            sb.AppendFormat("Node[{0}, kind:{1}, tok:{3}], type:{2}", n, n.Kind, t, n.Token);
            sb.AppendLine();
            n = bndFma.Formula.GetParent(n);
            indent++;
        }
        if (indent == 1)
            sb.AppendLine("    (Node is at root level.)");

        sb.AppendLine();
        sb.AppendLine();
    }

    private void AppendFunctionCallInfo(StringBuilder sb, BoundFormula bndFma, RexlNode left, RexlNode right)
    {
        var fma = bndFma.Formula;
        var leftCall = GetCallNodeFromInsideArgument(fma, left);
        var rightCall = GetCallNodeFromInsideArgument(fma, right);
        if (leftCall == null && rightCall == null)
            return;

        sb.AppendLine("=== Function Call Information ===");
        sb.AppendLine();
        if (leftCall == rightCall)
        {
            sb.AppendFormat("Selection is in the argument list of {0} function.", leftCall.IdentPath);
            sb.AppendLine();
            AppendCallNodeInfo(sb, bndFma, leftCall);
            sb.AppendLine();
        }
        else if (leftCall != null)
        {
            sb.AppendFormat("Left to the selection is in the argument list of {0} function.", leftCall.IdentPath);
            sb.AppendLine();
            AppendCallNodeInfo(sb, bndFma, leftCall);
            sb.AppendLine();
        }
        else if (rightCall != null)
        {
            sb.AppendFormat("Right to the selection is in the argument list of {0} function.", rightCall.IdentPath);
            sb.AppendLine();
            AppendCallNodeInfo(sb, bndFma, rightCall);
            sb.AppendLine();
        }

        sb.AppendLine();
    }

    private CallNode GetCallNodeFromInsideArgument(RexlFormula fma, RexlNode node)
    {
        if (node == null)
            return null;

        var ancestor = fma.GetParent(node);
        for (; ; )
        {
            if (ancestor == null || ancestor is CallNode)
                break;

            ancestor = fma.GetParent(ancestor);
        }

        Validation.Assert(ancestor == null || ancestor is CallNode);
        return ancestor as CallNode;
    }

    private void AppendCallNodeInfo(StringBuilder sb, BoundFormula bndFma, CallNode node)
    {
        Validation.AssertValue(node);
        var args = node.Args;
        var cargs = args.Count;

        if (node.TokPipe == null)
            Validation.Assert(node.Token == node.TokOpen);
        else
        {
            Validation.Assert(node.Token == node.TokPipe);
            Validation.Assert(node.TokPipe.Kind == TokKind.SubGrt);
            Validation.Assert(cargs > 0);

            sb.AppendLine("  Function call is in pipe form.");
        }

        if (cargs == 0)
            sb.AppendLine("  No argument specified.");
        else
        {
            sb.AppendLine("  Argument list:");
            for (int i = 0; i < cargs; i++)
            {
                var arg = args.Children[i];
                bndFma.TryGetNodeType(arg, out DType type);
                sb.AppendFormat("    Arg {0}: {1}, Range: {2}, Full Range: {3}, Type: {4}", i, arg, arg.GetRange(), arg.GetFullRange(), type);
                sb.AppendLine();
            }
        }

        sb.AppendLine();

        if (!bndFma.TryGetOper(node, out var oper) || !oper.IsFunc)
            return;

        sb.AppendLine("=== Rexl Func Information ===");
        sb.AppendLine($"Path: {oper.Path}");
        sb.AppendLine($"(ArityMin, ArityMax): ({oper.ArityMin}, {oper.ArityMax})");
        sb.AppendLine();
    }

    private void SuggestNodeInfo()
    {
        Validation.Assert(_version == _versionBound);
        if (_fma == null || _bfma == null)
            return;
        Validation.Assert(_bfma.Formula == _fma);

        var tokens = _fma.AllTokens;
        if (tokens.IsDefaultOrEmpty)
            return;

        var ichMin = _txtExpr.SelectionStart;
        var ichLim = ichMin + _txtExpr.SelectionLength;

        _fma.GetTokenRangeFromCharRange(ichMin, ichLim, out int itokMin, out int itokLim);
        (var left, var right) = _fma.GetNodesFromCharRange(ichMin, ichLim);

        var sb = new StringBuilder();
        AppendTokenInfo(sb, tokens, ichMin, ichLim, itokMin, itokLim);
        AppendParseNodeInfo(sb, _bfma, left, right);
        AppendFunctionCallInfo(sb, _bfma, left, right);

        _txtRawInfo.Text = sb.ToString();

        // Get the names that could go here.
        // REVIEW: This is merely a first attempt, not a polished algorithm! Needs to be improved.
        List<NameInfo> names = null;
        if (left is DottedNameNode dnn && dnn.Token.Range.Min <= ichMin && ichMin <= dnn.GetFullRange().Lim)
        {
            if (dnn.Root is FirstNameNode && _bfma.TryGetNamespace(dnn.Left, out var ns))
            {
                // Name is a namespace, so only add nodes within it.
                foreach (var node in _exec.Doc.GetFlowNodes())
                {
                    if (node.Name.StartsWith(ns) && node.Name.NameCount > ns.NameCount)
                    {
                        if (names == null)
                            names = new List<NameInfo>();
                        names.Add(NameInfo.Global(NPath.Root.AppendPartial(node.Name, ns.NameCount)));
                    }
                }
            }
            else
            {
                // After a dot, so only add fields of the left.
                _bfma.GetFieldNames(ref names, dnn.Left);
            }
        }
        else if (left is FirstNameNode fnn && fnn.Ident.Range.Min <= ichMin && ichMin <= fnn.Ident.Range.Lim)
        {
            // The left node is a FirstNameNode, so add scope items and globals.
            // REVIEW: Are there other left-node or same-node cases that should produce names?
            _bfma.GetScopeItemNames(ref names, fnn);
            AddGlobals(ref names);
        }
        else if (right != null && right != left)
        {
            // The right node is different than the left, so add things according to the right scope.
            _bfma.GetScopeItemNames(ref names, right);
            AddGlobals(ref names);
        }

        if (names != null)
            _txtNameList.Text = string.Join(Environment.NewLine, names);
        else
            _txtNameList.Text = "";
    }

    private void AddGlobals(ref List<NameInfo> names)
    {
        foreach (var name in _exec.GetGlobalNames())
        {
            if (name == _nameCloak)
                continue;
            if (names == null)
                names = new List<NameInfo>();
            names.Add(NameInfo.Global(name));
        }
    }
}
