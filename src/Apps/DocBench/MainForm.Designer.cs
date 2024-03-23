namespace DocBench
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
            this._mniLoadScript = new System.Windows.Forms.ToolStripMenuItem();
            this._sepFile1 = new System.Windows.Forms.ToolStripSeparator();
            this._mniExit = new System.Windows.Forms.ToolStripMenuItem();
            this._mnuEdit = new System.Windows.Forms.ToolStripMenuItem();
            this._mniUndo = new System.Windows.Forms.ToolStripMenuItem();
            this._mniRedo = new System.Windows.Forms.ToolStripMenuItem();
            this._sepEdit1 = new System.Windows.Forms.ToolStripSeparator();
            this._mniAddNode = new System.Windows.Forms.ToolStripMenuItem();
            this._mniEditNode = new System.Windows.Forms.ToolStripMenuItem();
            this._mniDeleteNode = new System.Windows.Forms.ToolStripMenuItem();
            this._sepEdit2 = new System.Windows.Forms.ToolStripSeparator();
            this._mniEditShowNss = new System.Windows.Forms.ToolStripMenuItem();
            this._mniEditAddNs = new System.Windows.Forms.ToolStripMenuItem();
            this._mniEditDeleteNs = new System.Windows.Forms.ToolStripMenuItem();
            this._mniEditRenameNs = new System.Windows.Forms.ToolStripMenuItem();
            this._menuView = new System.Windows.Forms.ToolStripMenuItem();
            this._mniClear = new System.Windows.Forms.ToolStripMenuItem();
            this._mniToggleView = new System.Windows.Forms.ToolStripMenuItem();
            this._mniShowIL = new System.Windows.Forms.ToolStripMenuItem();
            this._split = new System.Windows.Forms.SplitContainer();
            this._listNodes = new System.Windows.Forms.ListView();
            this._colGuid = new System.Windows.Forms.ColumnHeader();
            this._colStatus = new System.Windows.Forms.ColumnHeader();
            this._colName = new System.Windows.Forms.ColumnHeader();
            this._colNodeType = new System.Windows.Forms.ColumnHeader();
            this._colNodeExpr = new System.Windows.Forms.ColumnHeader();
            this._colNodeExtra = new System.Windows.Forms.ColumnHeader();
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
            this._mnuEdit,
            this._menuView});
            this._menuMain.Location = new System.Drawing.Point(0, 0);
            this._menuMain.Name = "_menuMain";
            this._menuMain.Padding = new System.Windows.Forms.Padding(7, 2, 0, 2);
            this._menuMain.Size = new System.Drawing.Size(1254, 24);
            this._menuMain.TabIndex = 0;
            // 
            // _menuFile
            // 
            this._menuFile.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._mniLoadScript,
            this._sepFile1,
            this._mniExit});
            this._menuFile.Name = "_menuFile";
            this._menuFile.Size = new System.Drawing.Size(37, 20);
            this._menuFile.Text = "&File";
            // 
            // _mniLoadScript
            // 
            this._mniLoadScript.Name = "_mniLoadScript";
            this._mniLoadScript.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.L)));
            this._mniLoadScript.Size = new System.Drawing.Size(182, 22);
            this._mniLoadScript.Text = "&Load Script...";
            this._mniLoadScript.Click += new System.EventHandler(this._mniLoadScript_Click);
            // 
            // _sepFile1
            // 
            this._sepFile1.Name = "_sepFile1";
            this._sepFile1.Size = new System.Drawing.Size(179, 6);
            // 
            // _mniExit
            // 
            this._mniExit.Name = "_mniExit";
            this._mniExit.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Q)));
            this._mniExit.Size = new System.Drawing.Size(182, 22);
            this._mniExit.Text = "E&xit";
            this._mniExit.Click += new System.EventHandler(this._mniExit_Click);
            // 
            // _mnuEdit
            // 
            this._mnuEdit.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._mniUndo,
            this._mniRedo,
            this._sepEdit1,
            this._mniAddNode,
            this._mniEditNode,
            this._mniDeleteNode,
            this._sepEdit2,
            this._mniEditShowNss,
            this._mniEditAddNs,
            this._mniEditDeleteNs,
            this._mniEditRenameNs});
            this._mnuEdit.Name = "_mnuEdit";
            this._mnuEdit.Size = new System.Drawing.Size(39, 20);
            this._mnuEdit.Text = "&Edit";
            // 
            // _mniUndo
            // 
            this._mniUndo.Name = "_mniUndo";
            this._mniUndo.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Z)));
            this._mniUndo.Size = new System.Drawing.Size(264, 22);
            this._mniUndo.Text = "&Undo";
            this._mniUndo.Click += new System.EventHandler(this._mniUndo_Click);
            // 
            // _mniRedo
            // 
            this._mniRedo.Name = "_mniRedo";
            this._mniRedo.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Y)));
            this._mniRedo.Size = new System.Drawing.Size(264, 22);
            this._mniRedo.Text = "&Redo";
            this._mniRedo.Click += new System.EventHandler(this._mniRedo_Click);
            // 
            // _sepEdit1
            // 
            this._sepEdit1.Name = "_sepEdit1";
            this._sepEdit1.Size = new System.Drawing.Size(261, 6);
            // 
            // _mniAddNode
            // 
            this._mniAddNode.Name = "_mniAddNode";
            this._mniAddNode.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.A)));
            this._mniAddNode.Size = new System.Drawing.Size(264, 22);
            this._mniAddNode.Text = "&Add Node...";
            this._mniAddNode.Click += new System.EventHandler(this._mniAddNode_Click);
            // 
            // _mniEditNode
            // 
            this._mniEditNode.Name = "_mniEditNode";
            this._mniEditNode.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.E)));
            this._mniEditNode.Size = new System.Drawing.Size(264, 22);
            this._mniEditNode.Text = "&Edit Node...";
            this._mniEditNode.Click += new System.EventHandler(this._mniEditNode_Click);
            // 
            // _mniDeleteNode
            // 
            this._mniDeleteNode.Name = "_mniDeleteNode";
            this._mniDeleteNode.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.D)));
            this._mniDeleteNode.Size = new System.Drawing.Size(264, 22);
            this._mniDeleteNode.Text = "&Delete Node";
            this._mniDeleteNode.Click += new System.EventHandler(this._mniDeleteNode_Click);
            // 
            // _sepEdit2
            // 
            this._sepEdit2.Name = "_sepEdit2";
            this._sepEdit2.Size = new System.Drawing.Size(261, 6);
            // 
            // _mniEditShowNss
            // 
            this._mniEditShowNss.Name = "_mniEditShowNss";
            this._mniEditShowNss.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.S)));
            this._mniEditShowNss.Size = new System.Drawing.Size(264, 22);
            this._mniEditShowNss.Text = "&Show Namespaces";
            this._mniEditShowNss.Click += new System.EventHandler(this._mniEditShowNss_Click);
            // 
            // _mniEditAddNs
            // 
            this._mniEditAddNs.Name = "_mniEditAddNs";
            this._mniEditAddNs.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.N)));
            this._mniEditAddNs.Size = new System.Drawing.Size(264, 22);
            this._mniEditAddNs.Text = "Add &Namespace...";
            this._mniEditAddNs.Click += new System.EventHandler(this._mniEditAddNs_Click);
            // 
            // _mniEditDeleteNs
            // 
            this._mniEditDeleteNs.Name = "_mniEditDeleteNs";
            this._mniEditDeleteNs.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.D)));
            this._mniEditDeleteNs.Size = new System.Drawing.Size(264, 22);
            this._mniEditDeleteNs.Text = "Delete Namespace...";
            this._mniEditDeleteNs.Click += new System.EventHandler(this._mniEditDeleteNs_Click);
            // 
            // _mniEditRenameNs
            // 
            this._mniEditRenameNs.Name = "_mniEditRenameNs";
            this._mniEditRenameNs.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.R)));
            this._mniEditRenameNs.Size = new System.Drawing.Size(264, 22);
            this._mniEditRenameNs.Text = "Rename Namespace...";
            this._mniEditRenameNs.Click += new System.EventHandler(this._mniEditRenameNs_Click);
            // 
            // _menuView
            // 
            this._menuView.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._mniClear,
            this._mniToggleView,
            this._mniShowIL});
            this._menuView.Name = "_menuView";
            this._menuView.Size = new System.Drawing.Size(44, 20);
            this._menuView.Text = "&View";
            // 
            // _mniClear
            // 
            this._mniClear.Name = "_mniClear";
            this._mniClear.ShortcutKeys = System.Windows.Forms.Keys.F4;
            this._mniClear.Size = new System.Drawing.Size(156, 22);
            this._mniClear.Text = "&Clear";
            this._mniClear.Click += new System.EventHandler(this._mniClear_Click);
            // 
            // _mniToggleView
            // 
            this._mniToggleView.Name = "_mniToggleView";
            this._mniToggleView.ShortcutKeys = System.Windows.Forms.Keys.F6;
            this._mniToggleView.Size = new System.Drawing.Size(156, 22);
            this._mniToggleView.Text = "&Toggle View";
            this._mniToggleView.Click += new System.EventHandler(this._mniToggleView_Click);
            // 
            // _mniShowIL
            // 
            this._mniShowIL.Name = "_mniShowIL";
            this._mniShowIL.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.I)));
            this._mniShowIL.Size = new System.Drawing.Size(156, 22);
            this._mniShowIL.Text = "&Show IL";
            this._mniShowIL.Click += new System.EventHandler(this._mniShowIL_Click);
            // 
            // _split
            // 
            this._split.Dock = System.Windows.Forms.DockStyle.Fill;
            this._split.Location = new System.Drawing.Point(0, 24);
            this._split.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this._split.Name = "_split";
            this._split.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // _split.Panel1
            // 
            this._split.Panel1.Controls.Add(this._listNodes);
            // 
            // _split.Panel2
            // 
            this._split.Panel2.Controls.Add(this._txtRes);
            this._split.Size = new System.Drawing.Size(1254, 844);
            this._split.SplitterDistance = 366;
            this._split.SplitterWidth = 9;
            this._split.TabIndex = 3;
            // 
            // _listNodes
            // 
            this._listNodes.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this._colGuid,
            this._colStatus,
            this._colName,
            this._colNodeType,
            this._colNodeExpr,
            this._colNodeExtra});
            this._listNodes.Dock = System.Windows.Forms.DockStyle.Fill;
            this._listNodes.Font = new System.Drawing.Font("Consolas", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this._listNodes.FullRowSelect = true;
            this._listNodes.Location = new System.Drawing.Point(0, 0);
            this._listNodes.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this._listNodes.MultiSelect = false;
            this._listNodes.Name = "_listNodes";
            this._listNodes.Size = new System.Drawing.Size(1254, 366);
            this._listNodes.TabIndex = 0;
            this._listNodes.UseCompatibleStateImageBehavior = false;
            this._listNodes.View = System.Windows.Forms.View.Details;
            this._listNodes.DoubleClick += new System.EventHandler(this._listNodes_DoubleClick);
            this._listNodes.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this._listNodes_KeyPress);
            // 
            // _colGuid
            // 
            this._colGuid.Text = "Guid";
            this._colGuid.Width = 270;
            // 
            // _colStatus
            // 
            this._colStatus.Text = "Status";
            this._colStatus.Width = 120;
            // 
            // _colName
            // 
            this._colName.Text = "Name";
            this._colName.Width = 120;
            // 
            // _colNodeType
            // 
            this._colNodeType.Text = "Type";
            this._colNodeType.Width = 147;
            // 
            // _colNodeExpr
            // 
            this._colNodeExpr.Text = "Expression";
            this._colNodeExpr.Width = 800;
            // 
            // _colNodeExtra
            // 
            this._colNodeExtra.Text = "Extra";
            this._colNodeExtra.Width = 500;
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
            this._txtRes.Size = new System.Drawing.Size(1254, 469);
            this._txtRes.TabIndex = 3;
            this._txtRes.WordWrap = false;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1254, 868);
            this.Controls.Add(this._split);
            this.Controls.Add(this._menuMain);
            this.MainMenuStrip = this._menuMain;
            this.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.Name = "MainForm";
            this.Text = "Data Flow Document Work Bench";
            this.Load += new System.EventHandler(this.Form1_Load);
            this._menuMain.ResumeLayout(false);
            this._menuMain.PerformLayout();
            this._split.Panel1.ResumeLayout(false);
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
        private System.Windows.Forms.ToolStripMenuItem _mniExit;
        private System.Windows.Forms.ToolStripMenuItem _menuView;
        private System.Windows.Forms.SplitContainer _split;
        private System.Windows.Forms.TextBox _txtRes;
        private System.Windows.Forms.ToolStripMenuItem _mniClear;
        private System.Windows.Forms.ToolStripMenuItem _mniToggleView;
        private System.Windows.Forms.ToolStripMenuItem _mniLoadScript;
        private System.Windows.Forms.ToolStripSeparator _sepFile1;
        private System.Windows.Forms.ListView _listNodes;
        private System.Windows.Forms.ColumnHeader _colNodeType;
        private System.Windows.Forms.ColumnHeader _colNodeExpr;
        private System.Windows.Forms.ColumnHeader _colName;
        private System.Windows.Forms.ToolStripMenuItem _mnuEdit;
        private System.Windows.Forms.ToolStripMenuItem _mniUndo;
        private System.Windows.Forms.ToolStripMenuItem _mniRedo;
        private System.Windows.Forms.ToolStripSeparator _sepEdit1;
        private System.Windows.Forms.ToolStripMenuItem _mniAddNode;
        private System.Windows.Forms.ToolStripMenuItem _mniEditNode;
        private System.Windows.Forms.ColumnHeader _colStatus;
        private System.Windows.Forms.ToolStripMenuItem _mniDeleteNode;
        private System.Windows.Forms.ToolStripMenuItem _mniShowIL;
        private System.Windows.Forms.ColumnHeader _colGuid;
        private System.Windows.Forms.ToolStripSeparator _sepEdit2;
        private System.Windows.Forms.ToolStripMenuItem _mniEditAddNs;
        private System.Windows.Forms.ToolStripMenuItem _mniEditShowNss;
        private System.Windows.Forms.ToolStripMenuItem _mniEditDeleteNs;
        private System.Windows.Forms.ToolStripMenuItem _mniEditRenameNs;
        private System.Windows.Forms.ColumnHeader _colNodeExtra;
    }
}

