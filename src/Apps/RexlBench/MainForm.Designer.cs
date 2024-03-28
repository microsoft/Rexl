namespace Microsoft.Rexl.RexlBench
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this._menuMain = new System.Windows.Forms.MenuStrip();
            this._menuFile = new System.Windows.Forms.ToolStripMenuItem();
            this._mniFileNew = new System.Windows.Forms.ToolStripMenuItem();
            this._mniFileOpen = new System.Windows.Forms.ToolStripMenuItem();
            this._mniFileReopen = new System.Windows.Forms.ToolStripMenuItem();
            this._sep1 = new System.Windows.Forms.ToolStripSeparator();
            this._mniFileSave = new System.Windows.Forms.ToolStripMenuItem();
            this._mniFileSaveAs = new System.Windows.Forms.ToolStripMenuItem();
            this._sep4 = new System.Windows.Forms.ToolStripSeparator();
            this._mniFilePickDir = new System.Windows.Forms.ToolStripMenuItem();
            this._sep2 = new System.Windows.Forms.ToolStripSeparator();
            this._mniFileExit = new System.Windows.Forms.ToolStripMenuItem();
            this._menuRun = new System.Windows.Forms.ToolStripMenuItem();
            this._mniRunGo = new System.Windows.Forms.ToolStripMenuItem();
            this._sep3 = new System.Windows.Forms.ToolStripSeparator();
            this._mniRunClear = new System.Windows.Forms.ToolStripMenuItem();
            this._mniRunToggleView = new System.Windows.Forms.ToolStripMenuItem();
            this._mniRunToggleOptimize = new System.Windows.Forms.ToolStripMenuItem();
            this._mniRunShowIL = new System.Windows.Forms.ToolStripMenuItem();
            this._mniRunShowVerboseBoundTree = new System.Windows.Forms.ToolStripMenuItem();
            this._mniRunShowBoundTree = new System.Windows.Forms.ToolStripMenuItem();
            this._mniRunShowTypedParseTree = new System.Windows.Forms.ToolStripMenuItem();
            this._mniRunShowHex = new System.Windows.Forms.ToolStripMenuItem();
            this._mniRunShowTerseTensor = new System.Windows.Forms.ToolStripMenuItem();
            this._mnuView = new System.Windows.Forms.ToolStripMenuItem();
            this._mniNormalFont = new System.Windows.Forms.ToolStripMenuItem();
            this._mniBigFont = new System.Windows.Forms.ToolStripMenuItem();
            this._mniHugeFont = new System.Windows.Forms.ToolStripMenuItem();
            this.editToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this._mniCmtLine = new System.Windows.Forms.ToolStripMenuItem();
            this._mniUnCmtLine = new System.Windows.Forms.ToolStripMenuItem();
            this._mniCmtSel = new System.Windows.Forms.ToolStripMenuItem();
            this._mniUnCmtSel = new System.Windows.Forms.ToolStripMenuItem();
            this._split = new System.Windows.Forms.SplitContainer();
            this._txtCode = new System.Windows.Forms.TextBox();
            this._txtRes = new System.Windows.Forms.TextBox();
            this._menuMain.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._split)).BeginInit();
            this._split.Panel1.SuspendLayout();
            this._split.Panel2.SuspendLayout();
            this._split.SuspendLayout();
            this.SuspendLayout();
            // 
            // _menuMain
            // 
            this._menuMain.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._menuFile,
            this._menuRun,
            this._mnuView,
            this.editToolStripMenuItem});
            this._menuMain.Location = new System.Drawing.Point(0, 0);
            this._menuMain.Name = "_menuMain";
            this._menuMain.Padding = new System.Windows.Forms.Padding(7, 2, 0, 2);
            this._menuMain.Size = new System.Drawing.Size(933, 24);
            this._menuMain.TabIndex = 0;
            // 
            // _menuFile
            // 
            this._menuFile.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._mniFileNew,
            this._mniFileOpen,
            this._mniFileReopen,
            this._sep1,
            this._mniFileSave,
            this._mniFileSaveAs,
            this._sep4,
            this._mniFilePickDir,
            this._sep2,
            this._mniFileExit});
            this._menuFile.Name = "_menuFile";
            this._menuFile.Size = new System.Drawing.Size(37, 20);
            this._menuFile.Text = "&File";
            this._menuFile.Click += new System.EventHandler(this._menuFile_Click);
            // 
            // _mniFileNew
            // 
            this._mniFileNew.Name = "_mniFileNew";
            this._mniFileNew.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.N)));
            this._mniFileNew.Size = new System.Drawing.Size(198, 22);
            this._mniFileNew.Text = "&New";
            this._mniFileNew.Click += new System.EventHandler(this._mniFileNew_Click);
            // 
            // _mniFileOpen
            // 
            this._mniFileOpen.Name = "_mniFileOpen";
            this._mniFileOpen.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.O)));
            this._mniFileOpen.Size = new System.Drawing.Size(198, 22);
            this._mniFileOpen.Text = "&Open..";
            this._mniFileOpen.Click += new System.EventHandler(this._mniFileOpen_Click);
            // 
            // _mniFileReopen
            // 
            this._mniFileReopen.Name = "_mniFileReopen";
            this._mniFileReopen.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.R)));
            this._mniFileReopen.Size = new System.Drawing.Size(198, 22);
            this._mniFileReopen.Text = "&Reopen";
            this._mniFileReopen.Click += new System.EventHandler(this._mniFileReopen_Click);
            // 
            // _sep1
            // 
            this._sep1.Name = "_sep1";
            this._sep1.Size = new System.Drawing.Size(195, 6);
            // 
            // _mniFileSave
            // 
            this._mniFileSave.Name = "_mniFileSave";
            this._mniFileSave.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S)));
            this._mniFileSave.Size = new System.Drawing.Size(198, 22);
            this._mniFileSave.Text = "&Save";
            this._mniFileSave.Click += new System.EventHandler(this._mniFileSave_Click);
            // 
            // _mniFileSaveAs
            // 
            this._mniFileSaveAs.Name = "_mniFileSaveAs";
            this._mniFileSaveAs.Size = new System.Drawing.Size(198, 22);
            this._mniFileSaveAs.Text = "Save &As..";
            this._mniFileSaveAs.Click += new System.EventHandler(this._mniFileSaveAs_Click);
            // 
            // _sep4
            // 
            this._sep4.Name = "_sep4";
            this._sep4.Size = new System.Drawing.Size(195, 6);
            // 
            // _mniFilePickDir
            // 
            this._mniFilePickDir.Name = "_mniFilePickDir";
            this._mniFilePickDir.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.D)));
            this._mniFilePickDir.Size = new System.Drawing.Size(198, 22);
            this._mniFilePickDir.Text = "Pick &Directory...";
            this._mniFilePickDir.Click += new System.EventHandler(this._mniFilePickDir_Click);
            // 
            // _sep2
            // 
            this._sep2.Name = "_sep2";
            this._sep2.Size = new System.Drawing.Size(195, 6);
            // 
            // _mniFileExit
            // 
            this._mniFileExit.Name = "_mniFileExit";
            this._mniFileExit.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Q)));
            this._mniFileExit.Size = new System.Drawing.Size(198, 22);
            this._mniFileExit.Text = "E&xit";
            this._mniFileExit.Click += new System.EventHandler(this._mniFileExit_Click);
            // 
            // _menuRun
            // 
            this._menuRun.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._mniRunGo,
            this._sep3,
            this._mniRunClear,
            this._mniRunToggleView,
            this._mniRunToggleOptimize,
            this._mniRunShowIL,
            this._mniRunShowVerboseBoundTree,
            this._mniRunShowBoundTree,
            this._mniRunShowTypedParseTree,
            this._mniRunShowHex,
            this._mniRunShowTerseTensor});
            this._menuRun.Name = "_menuRun";
            this._menuRun.Size = new System.Drawing.Size(40, 20);
            this._menuRun.Text = "&Run";
            // 
            // _mniRunGo
            // 
            this._mniRunGo.Name = "_mniRunGo";
            this._mniRunGo.ShortcutKeys = System.Windows.Forms.Keys.F5;
            this._mniRunGo.Size = new System.Drawing.Size(282, 22);
            this._mniRunGo.Text = "&Go";
            this._mniRunGo.Click += new System.EventHandler(this._mniRunGo_Click);
            // 
            // _sep3
            // 
            this._sep3.Name = "_sep3";
            this._sep3.Size = new System.Drawing.Size(279, 6);
            // 
            // _mniRunClear
            // 
            this._mniRunClear.Name = "_mniRunClear";
            this._mniRunClear.ShortcutKeys = System.Windows.Forms.Keys.F4;
            this._mniRunClear.Size = new System.Drawing.Size(282, 22);
            this._mniRunClear.Text = "&Clear";
            this._mniRunClear.Click += new System.EventHandler(this._mniRunClear_Click);
            // 
            // _mniRunToggleView
            // 
            this._mniRunToggleView.Name = "_mniRunToggleView";
            this._mniRunToggleView.ShortcutKeys = System.Windows.Forms.Keys.F6;
            this._mniRunToggleView.Size = new System.Drawing.Size(282, 22);
            this._mniRunToggleView.Text = "&Toggle View";
            this._mniRunToggleView.Click += new System.EventHandler(this._mniRunToggleView_Click);
            // 
            // _mniRunToggleOptimize
            // 
            this._mniRunToggleOptimize.Checked = true;
            this._mniRunToggleOptimize.CheckState = System.Windows.Forms.CheckState.Checked;
            this._mniRunToggleOptimize.Name = "_mniRunToggleOptimize";
            this._mniRunToggleOptimize.ShortcutKeys = System.Windows.Forms.Keys.F7;
            this._mniRunToggleOptimize.Size = new System.Drawing.Size(282, 22);
            this._mniRunToggleOptimize.Text = "&Toggle Optimize";
            this._mniRunToggleOptimize.Click += new System.EventHandler(this._mniRunToggleOptimize_Click);
            // 
            // _mniRunShowIL
            // 
            this._mniRunShowIL.Name = "_mniRunShowIL";
            this._mniRunShowIL.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.I)));
            this._mniRunShowIL.Size = new System.Drawing.Size(282, 22);
            this._mniRunShowIL.Text = "&Show IL";
            this._mniRunShowIL.Click += new System.EventHandler(this._mniRunShowIL_Click);
            // 
            // _mniRunShowVerboseBoundTree
            // 
            this._mniRunShowVerboseBoundTree.Name = "_mniRunShowVerboseBoundTree";
            this._mniRunShowVerboseBoundTree.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.B)));
            this._mniRunShowVerboseBoundTree.Size = new System.Drawing.Size(282, 22);
            this._mniRunShowVerboseBoundTree.Text = "Show &Verbose Bound Tree";
            this._mniRunShowVerboseBoundTree.Click += new System.EventHandler(this._mniRunShowVerboseBoundTree_Click);
            // 
            // _mniRunShowBoundTree
            // 
            this._mniRunShowBoundTree.Name = "_mniRunShowBoundTree";
            this._mniRunShowBoundTree.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.B)));
            this._mniRunShowBoundTree.Size = new System.Drawing.Size(282, 22);
            this._mniRunShowBoundTree.Text = "Show &Bound Tree";
            this._mniRunShowBoundTree.Click += new System.EventHandler(this._mniRunShowBoundTree_Click);
            // 
            // _mniRunShowTypedParseTree
            // 
            this._mniRunShowTypedParseTree.Name = "_mniRunShowTypedParseTree";
            this._mniRunShowTypedParseTree.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.T)));
            this._mniRunShowTypedParseTree.Size = new System.Drawing.Size(282, 22);
            this._mniRunShowTypedParseTree.Text = "Show Typed &Parse Tree";
            this._mniRunShowTypedParseTree.Click += new System.EventHandler(this._mniRunShowTypedParseTree_Click);
            // 
            // _mniRunShowHex
            // 
            this._mniRunShowHex.Name = "_mniRunShowHex";
            this._mniRunShowHex.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.H)));
            this._mniRunShowHex.Size = new System.Drawing.Size(282, 22);
            this._mniRunShowHex.Text = "Show &Hex";
            this._mniRunShowHex.Click += new System.EventHandler(this._mniRunShowHex_Click);
            // 
            // _mniRunShowTerseTensor
            // 
            this._mniRunShowTerseTensor.Name = "_mniRunShowTerseTensor";
            this._mniRunShowTerseTensor.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.U)));
            this._mniRunShowTerseTensor.Size = new System.Drawing.Size(282, 22);
            this._mniRunShowTerseTensor.Text = "Show Terse Tensor";
            this._mniRunShowTerseTensor.Click += new System.EventHandler(this._mniRunShowTerseTensor_Click);
            // 
            // _mnuView
            // 
            this._mnuView.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._mniNormalFont,
            this._mniBigFont,
            this._mniHugeFont});
            this._mnuView.Name = "_mnuView";
            this._mnuView.Size = new System.Drawing.Size(44, 20);
            this._mnuView.Text = "&View";
            // 
            // _mniNormalFont
            // 
            this._mniNormalFont.Name = "_mniNormalFont";
            this._mniNormalFont.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.D0)));
            this._mniNormalFont.Size = new System.Drawing.Size(181, 22);
            this._mniNormalFont.Text = "&Normal Font";
            this._mniNormalFont.Click += new System.EventHandler(this._mniNormalFont_Click);
            // 
            // _mniBigFont
            // 
            this._mniBigFont.Name = "_mniBigFont";
            this._mniBigFont.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.D1)));
            this._mniBigFont.Size = new System.Drawing.Size(181, 22);
            this._mniBigFont.Text = "&Big Font";
            this._mniBigFont.Click += new System.EventHandler(this._mniBigFont_Click);
            // 
            // _mniHugeFont
            // 
            this._mniHugeFont.Name = "_mniHugeFont";
            this._mniHugeFont.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.D2)));
            this._mniHugeFont.Size = new System.Drawing.Size(181, 22);
            this._mniHugeFont.Text = "&Huge Font";
            this._mniHugeFont.Click += new System.EventHandler(this._mniHugeFont_Click);
            // 
            // editToolStripMenuItem
            // 
            this.editToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._mniCmtLine,
            this._mniUnCmtLine,
            this._mniCmtSel,
            this._mniUnCmtSel});
            this.editToolStripMenuItem.Name = "editToolStripMenuItem";
            this.editToolStripMenuItem.Size = new System.Drawing.Size(39, 20);
            this.editToolStripMenuItem.Text = "Edit";
            // 
            // _mniCmtLine
            // 
            this._mniCmtLine.Name = "_mniCmtLine";
            this._mniCmtLine.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.K)));
            this._mniCmtLine.Size = new System.Drawing.Size(288, 22);
            this._mniCmtLine.Text = "Comment Line";
            this._mniCmtLine.Click += new System.EventHandler(this._mniCmtLine_Click);
            // 
            // _mniUnCmtLine
            // 
            this._mniUnCmtLine.Name = "_mniUnCmtLine";
            this._mniUnCmtLine.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Alt) 
            | System.Windows.Forms.Keys.K)));
            this._mniUnCmtLine.Size = new System.Drawing.Size(288, 22);
            this._mniUnCmtLine.Text = "Uncomment Line";
            this._mniUnCmtLine.Click += new System.EventHandler(this._mniUnCmtLine_Click);
            // 
            // _mniCmtSel
            // 
            this._mniCmtSel.Name = "_mniCmtSel";
            this._mniCmtSel.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.K)));
            this._mniCmtSel.Size = new System.Drawing.Size(288, 22);
            this._mniCmtSel.Text = "Comment Selection";
            this._mniCmtSel.Click += new System.EventHandler(this._mniCmtSel_Click);
            // 
            // _mniUnCmtSel
            // 
            this._mniUnCmtSel.Name = "_mniUnCmtSel";
            this._mniUnCmtSel.ShortcutKeys = ((System.Windows.Forms.Keys)((((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Alt) 
            | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.K)));
            this._mniUnCmtSel.Size = new System.Drawing.Size(288, 22);
            this._mniUnCmtSel.Text = "Uncomment Selection";
            this._mniUnCmtSel.Click += new System.EventHandler(this._mniUnCmtSel_Click);
            // 
            // _split
            // 
            this._split.Dock = System.Windows.Forms.DockStyle.Fill;
            this._split.Location = new System.Drawing.Point(0, 24);
            this._split.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this._split.Name = "_split";
            // 
            // _split.Panel1
            // 
            this._split.Panel1.Controls.Add(this._txtCode);
            // 
            // _split.Panel2
            // 
            this._split.Panel2.Controls.Add(this._txtRes);
            this._split.Size = new System.Drawing.Size(933, 495);
            this._split.SplitterDistance = 465;
            this._split.SplitterWidth = 9;
            this._split.TabIndex = 3;
            // 
            // _txtCode
            // 
            this._txtCode.AcceptsReturn = true;
            this._txtCode.Dock = System.Windows.Forms.DockStyle.Fill;
            this._txtCode.Font = new System.Drawing.Font("Consolas", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this._txtCode.Location = new System.Drawing.Point(0, 0);
            this._txtCode.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this._txtCode.MaxLength = 2147483647;
            this._txtCode.Multiline = true;
            this._txtCode.Name = "_txtCode";
            this._txtCode.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this._txtCode.Size = new System.Drawing.Size(465, 495);
            this._txtCode.TabIndex = 2;
            this._txtCode.WordWrap = false;
            // 
            // _txtRes
            // 
            this._txtRes.Dock = System.Windows.Forms.DockStyle.Fill;
            this._txtRes.Font = new System.Drawing.Font("Consolas", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this._txtRes.Location = new System.Drawing.Point(0, 0);
            this._txtRes.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this._txtRes.Multiline = true;
            this._txtRes.Name = "_txtRes";
            this._txtRes.ReadOnly = true;
            this._txtRes.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this._txtRes.Size = new System.Drawing.Size(459, 495);
            this._txtRes.TabIndex = 3;
            this._txtRes.WordWrap = false;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(933, 519);
            this.Controls.Add(this._split);
            this.Controls.Add(this._menuMain);
            this.MainMenuStrip = this._menuMain;
            this.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.Name = "MainForm";
            this.Text = "Rexl Work Bench";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_Closed2);
            this.Load += new System.EventHandler(this.Form1_Load);
            this._menuMain.ResumeLayout(false);
            this._menuMain.PerformLayout();
            this._split.Panel1.ResumeLayout(false);
            this._split.Panel1.PerformLayout();
            this._split.Panel2.ResumeLayout(false);
            this._split.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._split)).EndInit();
            this._split.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip _menuMain;
        private System.Windows.Forms.ToolStripMenuItem _menuFile;
        private System.Windows.Forms.ToolStripMenuItem _mniFileExit;
        private System.Windows.Forms.ToolStripMenuItem _mniFileSave;
        private System.Windows.Forms.ToolStripMenuItem _mniFileSaveAs;
        private System.Windows.Forms.ToolStripMenuItem _mniFileOpen;
        private System.Windows.Forms.ToolStripMenuItem _menuRun;
        private System.Windows.Forms.ToolStripMenuItem _mniRunGo;
        private System.Windows.Forms.SplitContainer _split;
        private System.Windows.Forms.TextBox _txtCode;
        private System.Windows.Forms.TextBox _txtRes;
        private System.Windows.Forms.ToolStripSeparator _sep3;
        private System.Windows.Forms.ToolStripMenuItem _mniRunClear;
        private System.Windows.Forms.ToolStripMenuItem _mniRunToggleView;
        private System.Windows.Forms.ToolStripMenuItem _mniFilePickDir;
        private System.Windows.Forms.ToolStripSeparator _sep2;
        private System.Windows.Forms.ToolStripMenuItem _mniRunShowIL;
        private System.Windows.Forms.ToolStripMenuItem _mniRunShowVerboseBoundTree;
        private System.Windows.Forms.ToolStripMenuItem _mniRunShowBoundTree;
        private System.Windows.Forms.ToolStripMenuItem _mniRunShowTypedParseTree;
        private System.Windows.Forms.ToolStripMenuItem _mniFileNew;
        private System.Windows.Forms.ToolStripSeparator _sep1;
        private System.Windows.Forms.ToolStripSeparator _sep4;
        private System.Windows.Forms.ToolStripMenuItem _mniFileReopen;
        private System.Windows.Forms.ToolStripMenuItem _mniRunShowHex;
        private System.Windows.Forms.ToolStripMenuItem _mniRunToggleOptimize;
        private System.Windows.Forms.ToolStripMenuItem _mniRunShowTerseTensor;
        private System.Windows.Forms.ToolStripMenuItem _mnuView;
        private System.Windows.Forms.ToolStripMenuItem _mniNormalFont;
        private System.Windows.Forms.ToolStripMenuItem _mniBigFont;
        private System.Windows.Forms.ToolStripMenuItem _mniHugeFont;
        private System.Windows.Forms.ToolStripMenuItem editToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem _mniCmtLine;
        private System.Windows.Forms.ToolStripMenuItem _mniUnCmtLine;
        private System.Windows.Forms.ToolStripMenuItem _mniCmtSel;
        private System.Windows.Forms.ToolStripMenuItem _mniUnCmtSel;
    }
}

