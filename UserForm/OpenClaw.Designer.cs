namespace XiaoYu_LAM.UserForm
{
    partial class OpenClaw
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(OpenClaw));
            this.wizardControl1 = new AeroWizard.WizardControl();
            this.colorDialog1 = new System.Windows.Forms.ColorDialog();
            this.wizardPage1 = new AeroWizard.WizardPage();
            this.commandLink1 = new XiaoYu_LAM.CommandLink();
            this.commandLink2 = new XiaoYu_LAM.CommandLink();
            ((System.ComponentModel.ISupportInitialize)(this.wizardControl1)).BeginInit();
            this.wizardPage1.SuspendLayout();
            this.SuspendLayout();
            // 
            // wizardControl1
            // 
            this.wizardControl1.Location = new System.Drawing.Point(0, 0);
            this.wizardControl1.Name = "wizardControl1";
            this.wizardControl1.Pages.Add(this.wizardPage1);
            this.wizardControl1.Size = new System.Drawing.Size(552, 456);
            this.wizardControl1.TabIndex = 0;
            this.wizardControl1.Title = "OpenClaw接入向导";
            this.wizardControl1.TitleIcon = ((System.Drawing.Icon)(resources.GetObject("wizardControl1.TitleIcon")));
            // 
            // wizardPage1
            // 
            this.wizardPage1.Controls.Add(this.commandLink2);
            this.wizardPage1.Controls.Add(this.commandLink1);
            this.wizardPage1.Name = "wizardPage1";
            this.wizardPage1.Size = new System.Drawing.Size(505, 300);
            this.wizardPage1.TabIndex = 0;
            this.wizardPage1.Text = "您希望使用哪个地方的龙虾？";
            // 
            // commandLink1
            // 
            this.commandLink1.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.commandLink1.Location = new System.Drawing.Point(3, 3);
            this.commandLink1.Name = "commandLink1";
            this.commandLink1.NoteText = "晓予将尝试自动往本地的龙虾安装扩展";
            this.commandLink1.Size = new System.Drawing.Size(499, 64);
            this.commandLink1.TabIndex = 0;
            this.commandLink1.Text = "使用运行在本地的龙虾";
            this.commandLink1.UseVisualStyleBackColor = true;
            // 
            // commandLink2
            // 
            this.commandLink2.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.commandLink2.Location = new System.Drawing.Point(3, 73);
            this.commandLink2.Name = "commandLink2";
            this.commandLink2.NoteText = "您需要自行配置扩展";
            this.commandLink2.Size = new System.Drawing.Size(499, 64);
            this.commandLink2.TabIndex = 1;
            this.commandLink2.Text = "使用远程的龙虾";
            this.commandLink2.UseVisualStyleBackColor = true;
            // 
            // OpenClaw
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(552, 456);
            this.Controls.Add(this.wizardControl1);
            this.Name = "OpenClaw";
            this.Text = "OpenClaw";
            ((System.ComponentModel.ISupportInitialize)(this.wizardControl1)).EndInit();
            this.wizardPage1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private AeroWizard.WizardControl wizardControl1;
        private System.Windows.Forms.ColorDialog colorDialog1;
        private AeroWizard.WizardPage wizardPage1;
        private CommandLink commandLink1;
        private CommandLink commandLink2;
    }
}