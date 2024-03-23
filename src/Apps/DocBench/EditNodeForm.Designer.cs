namespace DocBench
{
    partial class EditNodeForm
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
            this.components = new System.ComponentModel.Container();
            this._timer = new System.Windows.Forms.Timer(this.components);
            this._split = new System.Windows.Forms.SplitContainer();
            this._splitExpr = new System.Windows.Forms.SplitContainer();
            this._txtExpr = new System.Windows.Forms.TextBox();
            this._txtExtra = new System.Windows.Forms.TextBox();
            this._splitStatic = new System.Windows.Forms.SplitContainer();
            this._txtErrors = new System.Windows.Forms.TextBox();
            this._splitInfo = new System.Windows.Forms.SplitContainer();
            this._txtNameList = new System.Windows.Forms.TextBox();
            this._txtRawInfo = new System.Windows.Forms.TextBox();
            this._panBtns = new System.Windows.Forms.Panel();
            this._btnOk = new System.Windows.Forms.Button();
            this._btnCancel = new System.Windows.Forms.Button();
            this._panName = new System.Windows.Forms.Panel();
            this._checkSimpleRename = new System.Windows.Forms.CheckBox();
            this._lblName = new System.Windows.Forms.Label();
            this._txtName = new System.Windows.Forms.TextBox();
            ((System.ComponentModel.ISupportInitialize)(this._split)).BeginInit();
            this._split.Panel1.SuspendLayout();
            this._split.Panel2.SuspendLayout();
            this._split.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._splitExpr)).BeginInit();
            this._splitExpr.Panel1.SuspendLayout();
            this._splitExpr.Panel2.SuspendLayout();
            this._splitExpr.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._splitStatic)).BeginInit();
            this._splitStatic.Panel1.SuspendLayout();
            this._splitStatic.Panel2.SuspendLayout();
            this._splitStatic.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._splitInfo)).BeginInit();
            this._splitInfo.Panel1.SuspendLayout();
            this._splitInfo.Panel2.SuspendLayout();
            this._splitInfo.SuspendLayout();
            this._panBtns.SuspendLayout();
            this._panName.SuspendLayout();
            this.SuspendLayout();
            // 
            // _timer
            // 
            this._timer.Tick += new System.EventHandler(this._timer_Tick);
            // 
            // _split
            // 
            this._split.Dock = System.Windows.Forms.DockStyle.Fill;
            this._split.Location = new System.Drawing.Point(0, 67);
            this._split.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this._split.Name = "_split";
            this._split.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // _split.Panel1
            // 
            this._split.Panel1.Controls.Add(this._splitExpr);
            // 
            // _split.Panel2
            // 
            this._split.Panel2.Controls.Add(this._splitStatic);
            this._split.Size = new System.Drawing.Size(1071, 607);
            this._split.SplitterDistance = 105;
            this._split.SplitterWidth = 9;
            this._split.TabIndex = 1;
            this._split.TabStop = false;
            // 
            // _splitExpr
            // 
            this._splitExpr.Dock = System.Windows.Forms.DockStyle.Fill;
            this._splitExpr.Location = new System.Drawing.Point(0, 0);
            this._splitExpr.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this._splitExpr.Name = "_splitExpr";
            // 
            // _splitExpr.Panel1
            // 
            this._splitExpr.Panel1.Controls.Add(this._txtExpr);
            // 
            // _splitExpr.Panel2
            // 
            this._splitExpr.Panel2.Controls.Add(this._txtExtra);
            this._splitExpr.Size = new System.Drawing.Size(1071, 105);
            this._splitExpr.SplitterDistance = 642;
            this._splitExpr.SplitterWidth = 5;
            this._splitExpr.TabIndex = 0;
            this._splitExpr.TabStop = false;
            // 
            // _txtExpr
            // 
            this._txtExpr.AcceptsReturn = true;
            this._txtExpr.Dock = System.Windows.Forms.DockStyle.Fill;
            this._txtExpr.Font = new System.Drawing.Font("Consolas", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this._txtExpr.Location = new System.Drawing.Point(0, 0);
            this._txtExpr.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this._txtExpr.Multiline = true;
            this._txtExpr.Name = "_txtExpr";
            this._txtExpr.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this._txtExpr.Size = new System.Drawing.Size(642, 105);
            this._txtExpr.TabIndex = 0;
            this._txtExpr.WordWrap = false;
            this._txtExpr.MouseClick += new System.Windows.Forms.MouseEventHandler(this._txtExpr_SuggestionUpdate);
            this._txtExpr.TextChanged += new System.EventHandler(this._txtExpr_TextChanged);
            this._txtExpr.KeyUp += new System.Windows.Forms.KeyEventHandler(this._txtExpr_SuggestionUpdate);
            // 
            // _txtExtra
            // 
            this._txtExtra.AcceptsReturn = true;
            this._txtExtra.Dock = System.Windows.Forms.DockStyle.Fill;
            this._txtExtra.Font = new System.Drawing.Font("Consolas", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this._txtExtra.Location = new System.Drawing.Point(0, 0);
            this._txtExtra.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this._txtExtra.Multiline = true;
            this._txtExtra.Name = "_txtExtra";
            this._txtExtra.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this._txtExtra.Size = new System.Drawing.Size(424, 105);
            this._txtExtra.TabIndex = 1;
            this._txtExtra.WordWrap = false;
            // 
            // _splitStatic
            // 
            this._splitStatic.Dock = System.Windows.Forms.DockStyle.Fill;
            this._splitStatic.Location = new System.Drawing.Point(0, 0);
            this._splitStatic.Margin = new System.Windows.Forms.Padding(2);
            this._splitStatic.Name = "_splitStatic";
            this._splitStatic.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // _splitStatic.Panel1
            // 
            this._splitStatic.Panel1.Controls.Add(this._txtErrors);
            // 
            // _splitStatic.Panel2
            // 
            this._splitStatic.Panel2.Controls.Add(this._splitInfo);
            this._splitStatic.Size = new System.Drawing.Size(1071, 493);
            this._splitStatic.SplitterDistance = 232;
            this._splitStatic.SplitterWidth = 9;
            this._splitStatic.TabIndex = 1;
            this._splitStatic.TabStop = false;
            // 
            // _txtErrors
            // 
            this._txtErrors.Dock = System.Windows.Forms.DockStyle.Fill;
            this._txtErrors.Font = new System.Drawing.Font("Consolas", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this._txtErrors.Location = new System.Drawing.Point(0, 0);
            this._txtErrors.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this._txtErrors.Multiline = true;
            this._txtErrors.Name = "_txtErrors";
            this._txtErrors.ReadOnly = true;
            this._txtErrors.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this._txtErrors.Size = new System.Drawing.Size(1071, 232);
            this._txtErrors.TabIndex = 1;
            this._txtErrors.TabStop = false;
            this._txtErrors.WordWrap = false;
            // 
            // _splitInfo
            // 
            this._splitInfo.Dock = System.Windows.Forms.DockStyle.Fill;
            this._splitInfo.Location = new System.Drawing.Point(0, 0);
            this._splitInfo.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this._splitInfo.Name = "_splitInfo";
            // 
            // _splitInfo.Panel1
            // 
            this._splitInfo.Panel1.Controls.Add(this._txtNameList);
            // 
            // _splitInfo.Panel2
            // 
            this._splitInfo.Panel2.Controls.Add(this._txtRawInfo);
            this._splitInfo.Size = new System.Drawing.Size(1071, 252);
            this._splitInfo.SplitterDistance = 243;
            this._splitInfo.SplitterWidth = 5;
            this._splitInfo.TabIndex = 3;
            this._splitInfo.TabStop = false;
            // 
            // _txtNameList
            // 
            this._txtNameList.Dock = System.Windows.Forms.DockStyle.Fill;
            this._txtNameList.Font = new System.Drawing.Font("Consolas", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this._txtNameList.Location = new System.Drawing.Point(0, 0);
            this._txtNameList.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this._txtNameList.Multiline = true;
            this._txtNameList.Name = "_txtNameList";
            this._txtNameList.ReadOnly = true;
            this._txtNameList.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this._txtNameList.Size = new System.Drawing.Size(243, 252);
            this._txtNameList.TabIndex = 3;
            this._txtNameList.TabStop = false;
            this._txtNameList.WordWrap = false;
            // 
            // _txtRawInfo
            // 
            this._txtRawInfo.Dock = System.Windows.Forms.DockStyle.Fill;
            this._txtRawInfo.Font = new System.Drawing.Font("Consolas", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this._txtRawInfo.Location = new System.Drawing.Point(0, 0);
            this._txtRawInfo.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this._txtRawInfo.Multiline = true;
            this._txtRawInfo.Name = "_txtRawInfo";
            this._txtRawInfo.ReadOnly = true;
            this._txtRawInfo.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this._txtRawInfo.Size = new System.Drawing.Size(823, 252);
            this._txtRawInfo.TabIndex = 2;
            this._txtRawInfo.TabStop = false;
            this._txtRawInfo.WordWrap = false;
            // 
            // _panBtns
            // 
            this._panBtns.Controls.Add(this._btnOk);
            this._panBtns.Controls.Add(this._btnCancel);
            this._panBtns.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._panBtns.Location = new System.Drawing.Point(0, 674);
            this._panBtns.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this._panBtns.Name = "_panBtns";
            this._panBtns.Size = new System.Drawing.Size(1071, 60);
            this._panBtns.TabIndex = 2;
            // 
            // _btnOk
            // 
            this._btnOk.DialogResult = System.Windows.Forms.DialogResult.OK;
            this._btnOk.Location = new System.Drawing.Point(68, 20);
            this._btnOk.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this._btnOk.Name = "_btnOk";
            this._btnOk.Size = new System.Drawing.Size(88, 27);
            this._btnOk.TabIndex = 0;
            this._btnOk.Text = "O&K";
            this._btnOk.UseVisualStyleBackColor = true;
            // 
            // _btnCancel
            // 
            this._btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._btnCancel.Location = new System.Drawing.Point(248, 20);
            this._btnCancel.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this._btnCancel.Name = "_btnCancel";
            this._btnCancel.Size = new System.Drawing.Size(88, 27);
            this._btnCancel.TabIndex = 1;
            this._btnCancel.Text = "&Cancel";
            this._btnCancel.UseVisualStyleBackColor = true;
            // 
            // _panName
            // 
            this._panName.Controls.Add(this._checkSimpleRename);
            this._panName.Controls.Add(this._lblName);
            this._panName.Controls.Add(this._txtName);
            this._panName.Dock = System.Windows.Forms.DockStyle.Top;
            this._panName.Location = new System.Drawing.Point(0, 0);
            this._panName.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this._panName.Name = "_panName";
            this._panName.Size = new System.Drawing.Size(1071, 67);
            this._panName.TabIndex = 0;
            // 
            // _checkSimpleRename
            // 
            this._checkSimpleRename.AutoSize = true;
            this._checkSimpleRename.Location = new System.Drawing.Point(342, 24);
            this._checkSimpleRename.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this._checkSimpleRename.Name = "_checkSimpleRename";
            this._checkSimpleRename.Size = new System.Drawing.Size(108, 19);
            this._checkSimpleRename.TabIndex = 2;
            this._checkSimpleRename.TabStop = false;
            this._checkSimpleRename.Text = "Simple &Rename";
            this._checkSimpleRename.UseVisualStyleBackColor = true;
            // 
            // _lblName
            // 
            this._lblName.AutoSize = true;
            this._lblName.Location = new System.Drawing.Point(49, 24);
            this._lblName.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this._lblName.Name = "_lblName";
            this._lblName.Size = new System.Drawing.Size(42, 15);
            this._lblName.TabIndex = 1;
            this._lblName.Text = "&Name:";
            this._lblName.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // _txtName
            // 
            this._txtName.Font = new System.Drawing.Font("Consolas", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this._txtName.Location = new System.Drawing.Point(100, 20);
            this._txtName.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this._txtName.Name = "_txtName";
            this._txtName.Size = new System.Drawing.Size(221, 23);
            this._txtName.TabIndex = 0;
            this._txtName.TextChanged += new System.EventHandler(this._txtName_TextChanged);
            // 
            // EditNodeForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this._btnCancel;
            this.ClientSize = new System.Drawing.Size(1071, 734);
            this.Controls.Add(this._split);
            this.Controls.Add(this._panBtns);
            this.Controls.Add(this._panName);
            this.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.Name = "EditNodeForm";
            this.Text = "Edit Rexl Node";
            this._split.Panel1.ResumeLayout(false);
            this._split.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._split)).EndInit();
            this._split.ResumeLayout(false);
            this._splitExpr.Panel1.ResumeLayout(false);
            this._splitExpr.Panel1.PerformLayout();
            this._splitExpr.Panel2.ResumeLayout(false);
            this._splitExpr.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._splitExpr)).EndInit();
            this._splitExpr.ResumeLayout(false);
            this._splitStatic.Panel1.ResumeLayout(false);
            this._splitStatic.Panel1.PerformLayout();
            this._splitStatic.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._splitStatic)).EndInit();
            this._splitStatic.ResumeLayout(false);
            this._splitInfo.Panel1.ResumeLayout(false);
            this._splitInfo.Panel1.PerformLayout();
            this._splitInfo.Panel2.ResumeLayout(false);
            this._splitInfo.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._splitInfo)).EndInit();
            this._splitInfo.ResumeLayout(false);
            this._panBtns.ResumeLayout(false);
            this._panName.ResumeLayout(false);
            this._panName.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Timer _timer;
        private System.Windows.Forms.SplitContainer _split;
        private System.Windows.Forms.Panel _panBtns;
        private System.Windows.Forms.Button _btnOk;
        private System.Windows.Forms.Button _btnCancel;
        private System.Windows.Forms.Panel _panName;
        private System.Windows.Forms.Label _lblName;
        private System.Windows.Forms.TextBox _txtName;
        private System.Windows.Forms.SplitContainer _splitStatic;
        private System.Windows.Forms.TextBox _txtErrors;
        private System.Windows.Forms.TextBox _txtRawInfo;
        private System.Windows.Forms.SplitContainer _splitInfo;
        private System.Windows.Forms.TextBox _txtNameList;
        private System.Windows.Forms.CheckBox _checkSimpleRename;
        private System.Windows.Forms.SplitContainer _splitExpr;
        private System.Windows.Forms.TextBox _txtExpr;
        private System.Windows.Forms.TextBox _txtExtra;
    }
}