// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

using Microsoft.Rexl;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;
using Microsoft.Rexl.Harness;

namespace Microsoft.Rexl.RexlBench;

public partial class MainForm : Form
{
    private const string _fileFilter = "Rexl files (*.rexl)|*.rexl|Text files (*.txt)|*.txt|All files (*.*)|*.*";

    // Config file names. The files are all in the AppData folder.
    // REVIEW: This is hacky, since it assumes that only one instance of the app will be active
    // at a time. Also, it would be more standard to use the registry for some of this.

    // Contains cached script content.
    private const string _cacheName = "lastInput";
    // Contains the path of the last opened file.
    private const string _lastFileName = "lastFile";
    // Contains the path of the "current directory".
    private const string _lastDirName = "lastDir";

    private readonly Context _context;
    private readonly SimpleHarness _exec;
    private readonly SinkImpl _sink;
    private readonly SemaphoreSlim _sem;
    private readonly StringBuilder _sbOut;

    private string _pathSaved;
    private string _scriptSaved;
    private string _scriptCached;

    private double? _fracH;
    private double? _fracV;

    private Font _fontNormal;
    private Font _fontBig;
    private Font _fontHuge;

    public class Context : HarnessConfig
    {
        private readonly MainForm _parent;

        public bool ShowHex { get; private set; }
        public bool ShowTerseTensor { get; private set; }

        public Context(MainForm parent, bool showHex, bool terseTensor, bool optimize,
                bool showIL, bool showParse, bool showBnd, bool verboseBnd)
            : base(verbose: true, optimize: optimize,
                showIL: showIL, showParse: showParse, showBnd: showBnd, verboseBnd: verboseBnd)
        {
            Validation.AssertValue(parent);
            _parent = parent;
            ShowHex = showHex;
            ShowTerseTensor = terseTensor;
        }

        public bool SetShowHex(bool value)
        {
            var prev = ShowHex;
            ShowHex = value;
            return prev;
        }

        public bool SetShowTerseTensor(bool value)
        {
            var prev = ShowTerseTensor;
            ShowTerseTensor = value;
            return prev;
        }

        public void Flush(StringBuilder sb)
        {
            _parent.Flush(sb);
        }
    }

    public MainForm()
    {
        InitializeComponent();

        // Load previous script / cache.
        LoadPrevious();

        _fontNormal = _txtCode.Font;
        _fontBig = new Font(_fontNormal.FontFamily, _fontNormal.Size * 1.5f);
        _fontHuge = new Font(_fontNormal.FontFamily, _fontNormal.Size * 2);

        _mniNormalFont.Checked = true;
        _txtRes.Font = _fontNormal;

        _context = new Context(
            this,
            showHex: _mniRunShowHex.Checked,
            terseTensor: _mniRunShowTerseTensor.Checked,
            optimize: _mniRunToggleOptimize.Checked,
            showIL: _mniRunShowIL.Checked,
            showParse: _mniRunShowTypedParseTree.Checked,
            showBnd: _mniRunShowBoundTree.Checked,
            verboseBnd: _mniRunShowVerboseBoundTree.Checked);

        var codeGen = new CachingEnumerableCodeGenerator(
            new StdEnumerableTypeManager(),
            new Generators());
        _exec = new SimpleHarness(_context, new Operations(), codeGen);
        _sink = new SinkImpl(_context, _exec.TypeManager);
        _sem = new SemaphoreSlim(1, 1);
        _sbOut = new StringBuilder();
    }

    public void Flush(StringBuilder sb)
    {
        _sbOut.Append(sb);
    }

    private void Form1_Load(object sender, EventArgs e)
    {
    }

    private void Form1_Closed2(object sender, FormClosingEventArgs e)
    {
        SaveOnExit(e);
    }

    private void Log(string fmt, params object[] args)
    {
        System.Diagnostics.Debug.WriteLine(fmt, args);
    }

    private string GetConfigPath(string name)
    {
        Validation.AssertNonEmpty(name);
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), name);
    }

    private void DeleteConfigFile(string name)
    {
        var pathConfig = GetConfigPath(name);
        if (!File.Exists(pathConfig))
            return;

        Log("Deleting config at: '{0}'", pathConfig);
        try
        {
            File.Delete(pathConfig);
        }
        catch (Exception exCur)
        {
            Log("Failure deleting config at: '{0}' ({1})", pathConfig, exCur);
        }
    }

    private string ReadConfigFile(string name)
    {
        var pathConfig = GetConfigPath(name);
        if (!File.Exists(pathConfig))
            return null;

        Log("Reading config at: '{0}'", pathConfig);
        try
        {
            return File.ReadAllText(pathConfig);
        }
        catch (Exception exCur)
        {
            Log("Failure reading config at: '{0}' ({1})", pathConfig, exCur);
        }
        return null;
    }

    private bool TryWriteConfigFile(string name, string text, out Exception ex)
    {
        Validation.AssertValue(text);

        var pathConfig = GetConfigPath(name);
        Log("Writing config at: '{0}'", pathConfig);
        try
        {
            File.WriteAllText(pathConfig, text);
            ex = null;
            return true;
        }
        catch (Exception exCur)
        {
            Log("Failure writing config at: '{0}' ({1})", pathConfig, exCur);
            ex = exCur;
            return false;
        }
    }

    private void LoadPrevious()
    {
        _pathSaved = null;
        _scriptSaved = null;
        _scriptCached = null;

        string dir = ReadConfigFile(_lastDirName);
        if (dir != null)
        {
            if (!Directory.Exists(dir))
                DeleteConfigFile(_lastDirName);
            else
                Directory.SetCurrentDirectory(dir);
        }

        string file = ReadConfigFile(_lastFileName);
        if (file != null)
        {
            if (!File.Exists(file))
                DeleteConfigFile(_lastFileName);
            else
            {
                Log("Reading script from: '{0}'", file);
                try
                {
                    SetSaved(file, File.ReadAllText(file), writeConfig: false);
                }
                catch (Exception exCur)
                {
                    Log("Failure reading file at: '{0}' ({1})", file, exCur);
                }
            }
        }

        _scriptCached = ReadConfigFile(_cacheName);

        _txtCode.Text = _scriptCached ?? _scriptSaved ?? "";
        _txtCode.Select(0, 0);
    }

    /// <summary>
    /// If the script doesn't match the last cached script, cache it.
    /// </summary>
    private bool TryCacheScript(string script, out Exception ex)
    {
        Validation.AssertValue(script);

        ex = null;
        if (script == _scriptCached)
            return true;

        if (string.IsNullOrWhiteSpace(script))
        {
            _scriptCached = null;
            return true;
        }

        if (!TryWriteConfigFile(_cacheName, script, out ex))
            return false;

        _scriptCached = script;
        return true;
    }

    private void SetSaved(string path, string script, bool writeConfig)
    {
        Validation.AssertNonEmpty(path);
        _pathSaved = path;
        _scriptSaved = script;
        Text = $"Rexl Work Bench: {Path.GetFileName(path)}";

        if (writeConfig)
            TryWriteConfigFile(_lastFileName, path, out var _);
    }

    private void SetUnsaved()
    {
        _pathSaved = null;
        _scriptSaved = null;
        _scriptCached = null;
        Text = "Rexl Work Bench";

        DeleteConfigFile(_lastFileName);
        DeleteConfigFile(_cacheName);
    }

    private void SaveOnExit(FormClosingEventArgs e)
    {
        string script = _txtCode.Text;

        if (TryCacheScript(script, out var _) && _pathSaved == null)
            return;

        if (!PromptSave(script))
            e.Cancel = true;
    }

    private bool PromptSave(string script)
    {
        Validation.Assert((_pathSaved == null) == (_scriptSaved == null));

        if (_pathSaved != null)
        {
            if (script == _scriptSaved)
                return true;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(script))
                return true;
        }

        var result = MessageBox.Show(
            string.Format("Would you like to save the changes to file '{0}'", _pathSaved ?? "Untitled"),
            "Save changes",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.None);

        switch (result)
        {
        case DialogResult.No:
            break;

        case DialogResult.Yes:
            if (!TrySave())
                return false;
            break;

        default:
            return false;
        }

        return true;
    }

    private bool TrySave(bool isSaveAs = false)
    {
        string path;
        if (isSaveAs || _pathSaved == null)
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "Rexl files (*.rexl)|*.rexl|Text files (*.txt)|*.txt|All files (*.*)|*.*";
                dialog.FilterIndex = 1;
                if (_pathSaved != null)
                    dialog.FileName = _pathSaved;

                if (dialog.ShowDialog() != DialogResult.OK)
                    return false;
                path = dialog.FileName;
            }
        }
        else
            path = _pathSaved;

        string script = _txtCode.Text;
        Log("Saving to: '{0}'", path);
        try
        {
            File.WriteAllText(path, script);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                string.Format("Error saving file: \n\n {0}", ex.Message),
                "File IO Exception",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        SetSaved(path, script, writeConfig: true);
        return true;
    }

    private bool LoadFile(string path)
    {
        Log("Loading from: '{0}'", path);
        string script;
        try
        {
            script = File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                string.Format("Error loading file: \n\n {0}", ex.Message),
                "File IO Exception",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        SetSaved(path, script, writeConfig: true);
        _txtCode.Text = script;
        _txtCode.Select(0, 0);
        return true;
    }

    private void _menuFile_Click(object sender, EventArgs e)
    {
        _mniFileReopen.Enabled = (_pathSaved != null);
    }

    private void _mniFileExit_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void _mniFileNew_Click(object sender, EventArgs e)
    {
        if (!PromptSave(_txtCode.Text))
            return;

        _txtCode.Text = "";
        SetUnsaved();
    }

    private void _mniFileOpen_Click(object sender, EventArgs e)
    {
        if (!PromptSave(_txtCode.Text))
            return;

        string path;
        using (OpenFileDialog dialog = new OpenFileDialog())
        {
            dialog.Filter = _fileFilter;
            if (dialog.ShowDialog() != DialogResult.OK)
                return;
            path = dialog.FileName;
        }

        LoadFile(path);
    }

    private void _mniFileReopen_Click(object sender, EventArgs e)
    {
        if (_pathSaved != null)
            LoadFile(_pathSaved);
    }

    private void _mniFileSave_Click(object sender, EventArgs e)
    {
        TrySave();
    }

    private void _mniFileSaveAs_Click(object sender, EventArgs e)
    {
        TrySave(true);
    }

    private void _mniFilePickDir_Click(object sender, EventArgs e)
    {
        using (var dlg = new FolderBrowserDialog())
        {
            dlg.Description = "Select the working directory.";
            dlg.SelectedPath = Directory.GetCurrentDirectory();
            var res = dlg.ShowDialog(this);
            if (res == DialogResult.OK)
            {
                string path = dlg.SelectedPath;
                Directory.SetCurrentDirectory(path);
                TryWriteConfigFile(_lastDirName, path, out var _);
            }
        }
    }

    private async void _mniRunGo_Click(object sender, EventArgs e)
    {
        string script = _txtCode.Text;
        if (!TryCacheScript(script, out Exception ex))
        {
            var result = MessageBox.Show(
                string.Format("Error caching script:\n\n  {0}\n\nContinue executing?", ex.Message),
                "Warning",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (result != DialogResult.Yes)
                return;
        }

        await _sem.WaitAsync().ConfigureAwait(true);
        try
        {
            _sbOut.Clear();
            _sink.WriteLine();
            var link = LinkHelpers.LinkFromPath(_pathSaved);
            var src = SourceContext.Create(link, _pathSaved, script);
            try
            {
                var (res, state) = await _exec.RunAsync(_sink, src, resetBefore: true).ConfigureAwait(true);
                state?.Dispose();
            }
            finally
            {
                // Call Reset to abort any tasks that haven't completed and "forget" the runners.
                await _exec.CleanupAsync().ConfigureAwait(true);
            }
            _sink.TWriteLine().TWriteLine("================================================").Flush();

            // REVIEW: Should try to do this incrementally rather than all at the end!
            // Normalize carriage returns.
            _sbOut.Replace("\r\n", "\n");
            _sbOut.Replace("\r", "\n");
            _sbOut.Replace("\n", "\r\n");
            _txtRes.AppendText(_sbOut.ToString());
        }
        finally
        {
            _sem.Release();
        }
    }

    private async void _mniRunClear_Click(object sender, EventArgs e)
    {
        await _sem.WaitAsync().ConfigureAwait(true);
        try
        {
            _txtRes.Clear();
        }
        finally
        {
            _sem.Release();
        }
    }

    private void _mniRunToggleView_Click(object sender, EventArgs e)
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

    private void _mniRunToggleOptimize_Click(object sender, EventArgs e)
    {
        _context.SetOptimize(_mniRunToggleOptimize.Checked = !_mniRunToggleOptimize.Checked);
    }

    private void _mniRunShowIL_Click(object sender, EventArgs e)
    {
        _context.SetShowIL(_mniRunShowIL.Checked = !_mniRunShowIL.Checked);
    }

    private void _mniRunShowVerboseBoundTree_Click(object sender, EventArgs e)
    {
        _context.SetShowVerboseBoundTree(_mniRunShowVerboseBoundTree.Checked = !_mniRunShowVerboseBoundTree.Checked);
    }

    private void _mniRunShowBoundTree_Click(object sender, EventArgs e)
    {
        _context.SetShowBoundTree(_mniRunShowBoundTree.Checked = !_mniRunShowBoundTree.Checked);
    }

    private void _mniRunShowTypedParseTree_Click(object sender, EventArgs e)
    {
        _context.SetShowTypedParseTree(_mniRunShowTypedParseTree.Checked = !_mniRunShowTypedParseTree.Checked);
    }

    private void _mniRunShowTerseTensor_Click(object sender, EventArgs e)
    {
        _context.SetShowTerseTensor(_mniRunShowTerseTensor.Checked = !_mniRunShowTerseTensor.Checked);
    }

    private void _mniRunShowHex_Click(object sender, EventArgs e)
    {
        _context.SetShowHex(_mniRunShowHex.Checked = !_mniRunShowHex.Checked);
    }

    private void _mniNormalFont_Click(object sender, EventArgs e)
    {
        _txtCode.Font = _fontNormal;
        _txtRes.Font = _fontNormal;
        _mniNormalFont.Checked = true;
        _mniBigFont.Checked = false;
        _mniHugeFont.Checked = false;
    }

    private void _mniBigFont_Click(object sender, EventArgs e)
    {
        _txtCode.Font = _fontBig;
        _txtRes.Font = _fontBig;
        _mniNormalFont.Checked = false;
        _mniBigFont.Checked = true;
        _mniHugeFont.Checked = false;
    }

    private void _mniHugeFont_Click(object sender, EventArgs e)
    {
        _txtCode.Font = _fontHuge;
        _txtRes.Font = _fontHuge;
        _mniNormalFont.Checked = false;
        _mniBigFont.Checked = false;
        _mniHugeFont.Checked = true;
    }

    private void _mniCmtLine_Click(object sender, EventArgs e)
    {
        var iselStart = _txtCode.SelectionStart;
        var selLen = _txtCode.SelectionLength;
        var text = _txtCode.Text;
        var inewline = LastIndexOf(text, '\n', iselStart);
        var ilineStart = inewline > -1 ? inewline + 1 : 0;

        if (selLen == 0)
        {
            _txtCode.Text = text.Insert(ilineStart, "// ");
            _txtCode.SelectionStart = iselStart + 3;
        }
        else
        {
            var inewlineEnd = LastIndexOf(text, '\n', iselStart + selLen);
            var ilineEnd = inewlineEnd > -1 ? inewlineEnd + 1 : 0;
            var bldr = new StringBuilder();
            bldr.Append(text, 0, ilineStart);

            int itext = ilineStart;
            int delta = 0;
            while (itext <= ilineEnd)
            {
                bldr.Append("// ");
                delta += 3;
                var inewlineNext = text.IndexOf('\n', itext);
                if (inewlineNext < 0)
                    break;
                bldr.Append(text, itext, inewlineNext + 1 - itext);
                itext = inewlineNext + 1;
            }

            bldr.Append(text, itext, text.Length - itext);
            _txtCode.Text = bldr.ToString();

            var iselStartNew = iselStart;
            var selLenNew = selLen + delta;
            if (iselStart != ilineStart && delta > 0)
            {
                iselStartNew += 3;
                selLenNew -= 3;
            }
            _txtCode.SelectionStart = iselStartNew;
            _txtCode.SelectionLength = selLenNew;
        }
    }

    private void _mniUnCmtLine_Click(object sender, EventArgs e)
    {
        var iselStart = _txtCode.SelectionStart;
        var selLen = _txtCode.SelectionLength;
        var text = _txtCode.Text;
        var inewline = LastIndexOf(text, '\n', iselStart);
        var ilineStart = inewline > -1 ? inewline + 1 : 0;

        if (selLen == 0)
        {
            if (ilineStart + 1 < text.Length && text.Substring(ilineStart, 2) == "//")
            {
                var len = ilineStart + 2 < text.Length && text[ilineStart + 2] == ' ' ? 3 : 2;
                _txtCode.Text = text.Remove(ilineStart, len);
                _txtCode.SelectionStart = iselStart - len < ilineStart ? ilineStart : iselStart - len;
            }
        }
        else
        {
            var inewlineEnd = LastIndexOf(text, '\n', iselStart + selLen);
            var ilineEnd = inewlineEnd > -1 ? inewlineEnd + 1 : 0;
            var bldr = new StringBuilder();
            bldr.Append(text, 0, ilineStart);

            int itext = ilineStart;
            int delta = 0;
            while (itext <= ilineEnd)
            {
                var inewlineNext = text.IndexOf('\n', itext);
                if (itext + 1 < text.Length && text.Substring(itext, 2) == "//")
                {
                    var len = itext + 2 < text.Length && text[itext + 2] == ' ' ? 3 : 2;
                    delta -= len;
                    if (inewlineNext < 0)
                    {
                        itext += len;
                        break;
                    }
                    bldr.Append(text, itext + len, inewlineNext + 1 - itext - len);
                }
                else
                {
                    if (inewlineNext < 0)
                        break;
                    bldr.Append(text, itext, inewlineNext + 1 - itext);
                }
                itext = inewlineNext + 1;
            }

            bldr.Append(text, itext, text.Length - itext);
            _txtCode.Text = bldr.ToString();

            var iselStartNew = iselStart;
            var selLenNew = selLen + delta;
            if (iselStart != ilineStart && delta < 0)
            {
                iselStartNew -= 2;
                selLenNew += 2;
                if (ilineStart < text.Length && text[ilineStart + 2] == ' ')
                {
                    iselStartNew -= 1;
                    selLenNew += 1;
                }
            }
            _txtCode.SelectionStart = iselStartNew;
            _txtCode.SelectionLength = selLenNew;
        }
    }

    private int LastIndexOf(string text, char find, int lim)
    {
        for (int i = lim; --i >= 0;)
            if (text[i] == find)
                return i;
        return -1;
    }

    private void _mniCmtSel_Click(object sender, EventArgs e)
    {
        var iselStart = _txtCode.SelectionStart;
        var selLen = _txtCode.SelectionLength;
        if (selLen == 0)
            return;

        var text = _txtCode.Text;
        _txtCode.Text = text.Substring(0, iselStart) + "/* " + text.Substring(iselStart, selLen) + " */" + text.Substring(iselStart + selLen);
        _txtCode.SelectionStart = iselStart;
        _txtCode.SelectionLength = selLen + 6;
    }

    private void _mniUnCmtSel_Click(object sender, EventArgs e)
    {
        var iselStart = _txtCode.SelectionStart;
        var selLen = _txtCode.SelectionLength;
        if (selLen == 0)
            return;

        var text = _txtCode.Text;
        var istart = text.IndexOf("/*", iselStart);
        var iend = text.LastIndexOf("*/", iselStart + selLen);
        if (istart < 0 || iend < 0)
            return;

        var lenStart = istart + 2 < text.Length && text[istart + 2] == ' ' ? 3 : 2;
        var lenEnd = 2;
        if (iend - 1 >= 0 && text[iend - 1] == ' ')
        {
            iend = iend - 1;
            lenEnd = 3;
        }
        _txtCode.Text = text.Substring(0, istart) + text.Substring(istart + lenStart, iend - istart - lenStart) + text.Substring(iend + lenEnd);
        _txtCode.SelectionStart = iselStart;
        _txtCode.SelectionLength = selLen - lenStart - lenEnd;
    }
}

/// <summary>
/// A simple harness implementation.
/// </summary>
public sealed class SimpleHarness : SimpleHarnessWithSinkStack
{
    public SimpleHarness(IHarnessConfig config, OperationRegistry opers, CodeGeneratorBase codeGen,
            Storage storage = null)
        : base(config, opers, codeGen, storage)
    {
    }
}
