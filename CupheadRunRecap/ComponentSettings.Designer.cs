namespace CupheadRunRecap
{
    partial class ComponentSettings
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.btnBrowse = new System.Windows.Forms.Button();
            this.txtFilepath = new System.Windows.Forms.TextBox();
            this.lblFilepath = new System.Windows.Forms.Label();
            this.chkStarSkipDisplayMethod = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // btnBrowse
            // 
            this.btnBrowse.Location = new System.Drawing.Point(535, 34);
            this.btnBrowse.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.Size = new System.Drawing.Size(100, 28);
            this.btnBrowse.TabIndex = 0;
            this.btnBrowse.Text = "Browse";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            // 
            // txtFilepath
            // 
            this.txtFilepath.Location = new System.Drawing.Point(21, 37);
            this.txtFilepath.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.txtFilepath.Name = "txtFilepath";
            this.txtFilepath.Size = new System.Drawing.Size(504, 22);
            this.txtFilepath.TabIndex = 1;
            // 
            // lblFilepath
            // 
            this.lblFilepath.AutoSize = true;
            this.lblFilepath.Location = new System.Drawing.Point(17, 17);
            this.lblFilepath.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblFilepath.Name = "lblFilepath";
            this.lblFilepath.Size = new System.Drawing.Size(258, 16);
            this.lblFilepath.TabIndex = 2;
            this.lblFilepath.Text = "Where should Run Recap files get saved?";
            // 
            // chkStarSkipDisplayMethod
            // 
            this.chkStarSkipDisplayMethod.AutoSize = true;
            this.chkStarSkipDisplayMethod.Location = new System.Drawing.Point(21, 78);
            this.chkStarSkipDisplayMethod.Name = "chkStarSkipDisplayMethod";
            this.chkStarSkipDisplayMethod.Size = new System.Drawing.Size(442, 20);
            this.chkStarSkipDisplayMethod.TabIndex = 3;
            this.chkStarSkipDisplayMethod.Text = "Display Star Skip Count as Raw Number instead of Decimal Notation?";
            this.chkStarSkipDisplayMethod.UseVisualStyleBackColor = true;
            this.chkStarSkipDisplayMethod.CheckedChanged += new System.EventHandler(this.chkStarSkipDisplayMethod_CheckedChanged);
            // 
            // ComponentSettings
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.chkStarSkipDisplayMethod);
            this.Controls.Add(this.lblFilepath);
            this.Controls.Add(this.txtFilepath);
            this.Controls.Add(this.btnBrowse);
            this.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.Name = "ComponentSettings";
            this.Size = new System.Drawing.Size(661, 230);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.TextBox txtFilepath;
        private System.Windows.Forms.Label lblFilepath;
        private System.Windows.Forms.CheckBox chkStarSkipDisplayMethod;
    }
}
