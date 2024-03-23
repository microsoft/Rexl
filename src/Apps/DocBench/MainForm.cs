// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Media;
using System.Windows.Forms;

using Microsoft.Rexl;
using Microsoft.Rexl.Flow;
using Microsoft.Rexl.Lex;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;

namespace DocBench;

public partial class MainForm : Form
{
    private readonly Executor _exec;

    public MainForm()
    {
        InitializeComponent();
        _exec = new Executor(_txtRes, _listNodes, () => _mniShowIL.Checked);

        _mniUndo.Enabled = false;
        _mniRedo.Enabled = false;
        _exec.OnDocChanged += OnDocChanged;
    }

    private void OnDocChanged()
    {
        _mniUndo.Enabled = _exec.UndoCount > 0;
        _mniRedo.Enabled = _exec.RedoCount > 0;
    }

    private void Beep()
    {
        SystemSounds.Beep.Play();
    }

    private void Form1_Load(object sender, EventArgs e)
    {
    }

    private void _mniExit_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void _mniClear_Click(object sender, EventArgs e)
    {
        _txtRes.Clear();
    }

    private double? _fracH;
    private double? _fracV;

    private void _mniToggleView_Click(object sender, EventArgs e)
    {
        if (_split.Orientation == Orientation.Horizontal)
        {
            _fracH = Math.Max(0.1, Math.Min(0.9, (double)_split.SplitterDistance / Math.Max(10.0, _split.Height)));
            int distV = (int)Math.Round((_fracV ?? 0.5) * _split.Width);
            _split.Orientation = Orientation.Vertical;
            _split.SplitterDistance = distV;
        }
        else
        {
            _fracV = Math.Max(0.1, Math.Min(0.9, (double)_split.SplitterDistance / Math.Max(10.0, _split.Width)));
            int distH = (int)Math.Round((_fracH ?? 0.5) * _split.Height);
            _split.Orientation = Orientation.Horizontal;
            _split.SplitterDistance = distH;
        }
    }

    private void _mniLoadScript_Click(object sender, EventArgs e)
    {
        string path;
        using (var dlg = new OpenFileDialog())
        {
            dlg.Title = "Load Script";
            dlg.Filter = "Rexl files (*.rexl)|*.rexl|Text files (*.txt)|*.txt|All files (*.*)|*.*";
            dlg.CheckFileExists = true;

            var res = dlg.ShowDialog(this);
            if (res != DialogResult.OK)
                return;

            path = dlg.FileName;
        }

        string dir = Path.GetDirectoryName(path);
        Directory.SetCurrentDirectory(dir);

        string script = File.ReadAllText(path);
        _exec.LoadScript(script);
        _exec.Flush();
    }

    private void _mniUndo_Click(object sender, EventArgs e)
    {
        if (_exec.UndoCount > 0)
            _exec.Undo();
        else
            Beep();
    }

    private void _mniRedo_Click(object sender, EventArgs e)
    {
        if (_exec.RedoCount > 0)
            _exec.Redo();
        else
            Beep();
    }

    private NPath GetName(string str)
    {
        if (string.IsNullOrEmpty(str))
            return default;

        if (!LexUtils.TryLexPath(str, out var name))
            return default;
        return name;
    }

    private bool IsNewNameValid(string s)
    {
        // We don't allow empty names.
        if (string.IsNullOrEmpty(s))
            return false;
        var name = GetName(s);
        if (name.IsRoot)
            return false;
        return !_exec.Doc.HasNameConflict(name);
    }

    private void _mniAddNode_Click(object sender, EventArgs e)
    {
        NPath name;
        string expr;
        string extra;

        using (var dlg = EditNodeForm.CreateAdd(_exec, IsNewNameValid))
        {
            var res = dlg.ShowDialog(this);
            if (res == DialogResult.Cancel)
                return;

            Validation.Assert(IsNewNameValid(dlg.NameValue));
            name = GetName(dlg.NameValue);
            expr = dlg.ExprValue;
            extra = dlg.ExtraValue;
        }

        var fma = RexlFormula.Create(SourceContext.Create(expr));
        var doc = _exec.Doc;
        var guid = _exec.GuidNext();
        doc = doc.CreateFlowNode(name, fma, null, guid);
        if (!string.IsNullOrWhiteSpace(extra))
            doc = doc.InsertExtraFormula(guid, 0, RexlFormula.Create(SourceContext.Create(extra)));
        _exec.Set(doc);
    }

    private void _mniEditNode_Click(object sender, EventArgs e)
    {
        EditListItem();
    }

    private void EditListItem()
    {
        if (_listNodes.SelectedItems.Count == 0)
        {
            Beep();
            return;
        }

        var item = _listNodes.SelectedItems[0];
        if (!(item.Tag is Guid guid))
        {
            Validation.Assert(false);
            Beep();
            return;
        }

        var node = _exec.Doc.GetNode(guid);
        var nameCur = node.Name;
        bool IsNameValid(string s)
        {
            // We don't allow empty names.
            if (string.IsNullOrEmpty(s))
                return false;
            var name = GetName(s);
            if (name.IsRoot)
                return false;
            if (name == nameCur)
                return true;
            return !_exec.Doc.HasNameConflict(name);
        }

        if (node.Formula != null)
        {
            string exprCur = node.Formula.Text;
            string extraCur = node.ExtraFormulas.Length > 0 ? node.ExtraFormulas[0].fma.Text : null;

            NPath name;
            string expr;
            string extra;
            bool smartRename;

            using (var dlg = EditNodeForm.CreateEdit(_exec, IsNameValid, nameCur))
            {
                dlg.NameValue = nameCur.ToDottedSyntax();
                dlg.ExprValue = exprCur;
                if (extraCur != null)
                    dlg.ExtraValue = extraCur;

                var res = dlg.ShowDialog(this);
                if (res == DialogResult.Cancel)
                    return;

                Validation.Assert(IsNameValid(dlg.NameValue));
                name = GetName(dlg.NameValue);
                expr = dlg.ExprValue;
                extra = dlg.ExtraValue;
                if (string.IsNullOrWhiteSpace(extra))
                    extra = null;
                if (name == nameCur && expr == exprCur && extra == extraCur)
                    return;

                smartRename = !dlg.IsSimpleRename && name != nameCur;
            }

            // If we're doing a smart rename, get the changes in other nodes. Note that hybrid nodes should
            // not have their scripts changed!
            var changes = smartRename ? _exec.Graph.GetRenameChanges(guid, name) : null;
            var doc = _exec.Doc;
            if (name != nameCur)
                doc = doc.RenameGlobal(guid, name, changes);
            if (expr != exprCur)
                doc = doc.SetFormula(guid, expr);
            if (extra != extraCur)
            {
                if (extraCur == null)
                    doc = doc.InsertExtraFormula(guid, 0, RexlFormula.Create(SourceContext.Create(extra)));
                else if (extra == null)
                    doc = doc.RemoveExtraFormula(guid, 0);
                else
                    doc = doc.ReplaceExtraFormula(guid, 0, RexlFormula.Create(SourceContext.Create(extra)));
            }
            _exec.Set(doc);
            return;
        }

        Beep();
        return;
    }

    private void ShowListItem()
    {
        if (_listNodes.SelectedItems.Count == 0)
        {
            Beep();
            return;
        }

        var item = _listNodes.SelectedItems[0];
        if (!(item.Tag is Guid guid))
        {
            Validation.Assert(false);
            Beep();
            return;
        }

        var node = _exec.Doc.GetNode(guid);
        _exec.RenderNodeValue(node);
    }

    private void _mniDeleteNode_Click(object sender, EventArgs e)
    {
        if (_listNodes.SelectedItems.Count == 0)
        {
            Beep();
            return;
        }

        var item = _listNodes.SelectedItems[0];
        if (!(item.Tag is Guid guid))
        {
            Beep();
            return;
        }

        _exec.Set(_exec.Doc.DeleteNode(guid));
    }

    private void _listNodes_DoubleClick(object sender, EventArgs e)
    {
        EditListItem();
    }

    private void _listNodes_KeyPress(object sender, KeyPressEventArgs e)
    {
        if (e.Handled)
            return;
        switch (e.KeyChar)
        {
        case '\r':
            if ((ModifierKeys & Keys.Shift) != 0)
                ShowListItem();
            else
                EditListItem();
            e.Handled = true;
            break;
        }
    }

    private void _mniShowIL_Click(object sender, EventArgs e)
    {
        _mniShowIL.Checked = !_mniShowIL.Checked;
    }

    private bool IsNewNamespaceValid(NPath name)
    {
        if (_exec.Graph.TryGetNamespace(name, out var ns))
        {
            if (ns != null)
                return false;
            return true;
        }
        if (_exec.Doc.TryGetNode(name, out var node))
            return false;
        return true;
    }

    private bool IsNamespace(NPath name)
    {
        return _exec.Doc.ContainsNamespace(name);
    }

    private bool IsExplicitNamespace(NPath name)
    {
        return _exec.Graph.TryGetNamespace(name, out var ns) && ns != null;
    }

    private void _mniEditAddNs_Click(object sender, EventArgs e)
    {
        bool isOk(EditNsForm form)
        {
            return form.HasNsName && IsNewNamespaceValid(form.NsName);
        }

        using (var dlg = EditNsForm.Create(isOk))
        {
            var res = dlg.ShowDialog(this);
            switch (res)
            {
            default:
                return;

            case DialogResult.OK:
                Validation.Assert(dlg.HasNsName);
                _exec.Set(_exec.Doc.CreateExplicitNamespace(dlg.NsName, null));
                break;
            }
        }
    }

    private void _mniEditShowNss_Click(object sender, EventArgs e)
    {
        _exec.ShowNamespaces();
    }

    private void _mniEditDeleteNs_Click(object sender, EventArgs e)
    {
        bool isOk(EditNsForm form)
        {
            if (!form.HasNsName)
                return false;
            if (form.ForceDelete)
                return IsNamespace(form.NsName);
            return IsExplicitNamespace(form.NsName);
        }

        using (var dlg = EditNsForm.Delete(isOk))
        {
            var res = dlg.ShowDialog(this);
            switch (res)
            {
            default:
                return;

            case DialogResult.OK:
                Validation.Assert(dlg.HasNsName);
                _exec.Doc.DeleteNamespace(dlg.NsName, dlg.ForceDelete);
                break;
            }
        }
    }

    private void _mniEditRenameNs_Click(object sender, EventArgs e)
    {
        bool isOk(EditNsForm form)
        {
            if (!form.HasNsName)
                return false;
            if (!form.HasNsNewName)
                return false;
            if (!IsNamespace(form.NsName))
                return false;
            if (_exec.Doc.HasNameConflict(form.NsNewName))
                return false;
            return true;
        }

        using (var dlg = EditNsForm.Rename(isOk))
        {
            var res = dlg.ShowDialog(this);
            switch (res)
            {
            default:
                return;

            case DialogResult.OK:
                Validation.Assert(dlg.HasNsName);
                _exec.Set(_exec.Doc.RenameNamespace(dlg.NsName, dlg.NsNewName));
                break;
            }
        }
    }
}
