namespace DocBench
{
    partial class EditNsForm
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
            this._lblName = new System.Windows.Forms.Label();
            this._txtName = new System.Windows.Forms.TextBox();
            this._txtNameNew = new System.Windows.Forms.TextBox();
            this._lblNameNew = new System.Windows.Forms.Label();
            this._btnOk = new System.Windows.Forms.Button();
            this._btnCancel = new System.Windows.Forms.Button();
            this._chkForce = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // _lblName
            // 
            this._lblName.AutoSize = true;
            this._lblName.Location = new System.Drawing.Point(53, 15);
            this._lblName.Name = "_lblName";
            this._lblName.Size = new System.Drawing.Size(38, 13);
            this._lblName.TabIndex = 0;
            this._lblName.Text = "&Name:";
            this._lblName.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // _txtName
            // 
            this._txtName.Location = new System.Drawing.Point(97, 12);
            this._txtName.Name = "_txtName";
            this._txtName.Size = new System.Drawing.Size(379, 20);
            this._txtName.TabIndex = 1;
            this._txtName.TextChanged += new System.EventHandler(this._txtName_TextChanged);
            // 
            // _txtNameNew
            // 
            this._txtNameNew.Location = new System.Drawing.Point(97, 38);
            this._txtNameNew.Name = "_txtNameNew";
            this._txtNameNew.Size = new System.Drawing.Size(379, 20);
            this._txtNameNew.TabIndex = 3;
            this._txtNameNew.TextChanged += new System.EventHandler(this._txtNameNew_TextChanged);
            // 
            // _lblNameNew
            // 
            this._lblNameNew.AutoSize = true;
            this._lblNameNew.Location = new System.Drawing.Point(28, 41);
            this._lblNameNew.Name = "_lblNameNew";
            this._lblNameNew.Size = new System.Drawing.Size(63, 13);
            this._lblNameNew.TabIndex = 2;
            this._lblNameNew.Text = "Ne&w Name:";
            this._lblNameNew.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // _btnOk
            // 
            this._btnOk.DialogResult = System.Windows.Forms.DialogResult.OK;
            this._btnOk.Location = new System.Drawing.Point(97, 98);
            this._btnOk.Name = "_btnOk";
            this._btnOk.Size = new System.Drawing.Size(75, 23);
            this._btnOk.TabIndex = 4;
            this._btnOk.Text = "O&K";
            this._btnOk.UseVisualStyleBackColor = true;
            // 
            // _btnCancel
            // 
            this._btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._btnCancel.Location = new System.Drawing.Point(217, 98);
            this._btnCancel.Name = "_btnCancel";
            this._btnCancel.Size = new System.Drawing.Size(75, 23);
            this._btnCancel.TabIndex = 5;
            this._btnCancel.Text = "&Cancel";
            this._btnCancel.UseVisualStyleBackColor = true;
            // 
            // _chkForce
            // 
            this._chkForce.AutoSize = true;
            this._chkForce.Location = new System.Drawing.Point(97, 65);
            this._chkForce.Name = "_chkForce";
            this._chkForce.Size = new System.Drawing.Size(87, 17);
            this._chkForce.TabIndex = 6;
            this._chkForce.Text = "&Force Delete";
            this._chkForce.UseVisualStyleBackColor = true;
            this._chkForce.CheckedChanged += new System.EventHandler(this._chkForce_CheckedChanged);
            // 
            // EditNsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this._btnCancel;
            this.ClientSize = new System.Drawing.Size(488, 141);
            this.Controls.Add(this._chkForce);
            this.Controls.Add(this._btnCancel);
            this.Controls.Add(this._btnOk);
            this.Controls.Add(this._txtNameNew);
            this.Controls.Add(this._lblNameNew);
            this.Controls.Add(this._txtName);
            this.Controls.Add(this._lblName);
            this.Name = "EditNsForm";
            this.Text = "Edit Namespace";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label _lblName;
        private System.Windows.Forms.TextBox _txtName;
        private System.Windows.Forms.TextBox _txtNameNew;
        private System.Windows.Forms.Label _lblNameNew;
        private System.Windows.Forms.Button _btnOk;
        private System.Windows.Forms.Button _btnCancel;
        private System.Windows.Forms.CheckBox _chkForce;
    }
}