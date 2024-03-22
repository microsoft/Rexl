// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Rexl;
using Microsoft.Rexl.Lex;
using System;
using System.Windows.Forms;

namespace DocBench;

public partial class EditNsForm : Form
{
    private readonly Func<EditNsForm, bool> _isOk;

    private bool _hasName;
    private bool _hasNewName;
    private NPath _name;
    private NPath _nameNew;

    public bool HasNsName => _hasName;
    public bool HasNsNewName => _hasNewName;

    public NPath NsName => _name;
    public NPath NsNewName => _nameNew;

    public bool ForceDelete => _chkForce.Checked;

    public EditNsForm(Func<EditNsForm, bool> isOk, Func<EditNsForm, bool> isNewNameValid = null)
    {
        _isOk = isOk;

        InitializeComponent();

        _hasName = LexUtils.TryLexPathOrRoot(_txtName.Text, out _name);
        _hasNewName = LexUtils.TryLexPathOrRoot(_txtNameNew.Text, out _nameNew);
    }

    public static EditNsForm Create(Func<EditNsForm, bool> isOk)
    {
        var form = new EditNsForm(isOk);
        form._lblNameNew.Visible = false;
        form._txtNameNew.Visible = false;
        form._chkForce.Visible = false;
        form.Text = "Create Namespace";
        form.UpdateOk();
        return form;
    }

    public static EditNsForm Delete(Func<EditNsForm, bool> isOk)
    {
        var form = new EditNsForm(isOk);
        form._lblNameNew.Visible = false;
        form._txtNameNew.Visible = false;
        form._chkForce.Visible = true;
        form._chkForce.Checked = true;
        form.Text = "Delete Namespace";
        form.UpdateOk();
        return form;
    }

    public static EditNsForm Rename(Func<EditNsForm, bool> isOk)
    {
        var form = new EditNsForm(isOk);
        form._lblNameNew.Visible = true;
        form._txtNameNew.Visible = true;
        form._chkForce.Visible = false;
        form.Text = "Rename Namespace";
        form.UpdateOk();
        return form;
    }

    private void UpdateOk()
    {
        _btnOk.Enabled = _isOk(this);
    }

    private void _txtName_TextChanged(object sender, EventArgs e)
    {
        _hasName = LexUtils.TryLexPathOrRoot(_txtName.Text, out _name);
        UpdateOk();
    }

    private void _txtNameNew_TextChanged(object sender, EventArgs e)
    {
        _hasNewName = LexUtils.TryLexPathOrRoot(_txtNameNew.Text, out _nameNew);
        UpdateOk();
    }

    private void _chkForce_CheckedChanged(object sender, EventArgs e)
    {
        UpdateOk();
    }
}
