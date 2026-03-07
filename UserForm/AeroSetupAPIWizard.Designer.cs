namespace XiaoYu_LAM.UserForm
{
    partial class AeroSetupAPIWizard
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AeroSetupAPIWizard));
            this.wizardControl1 = new AeroWizard.WizardControl();
            this.ChooseProviderWizardPage = new AeroWizard.WizardPage();
            this.commandLink2 = new XiaoYu_LAM.CommandLink();
            this.UseRemoteLLMProvider = new XiaoYu_LAM.CommandLink();
            this.InputAPIConfigWizardPage = new AeroWizard.WizardPage();
            this.label4 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.textBox3 = new System.Windows.Forms.TextBox();
            this.textBox2 = new System.Windows.Forms.TextBox();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.ChooseProtocolWizardPage = new AeroWizard.WizardPage();
            this.commandLink4 = new XiaoYu_LAM.CommandLink();
            this.commandLink3 = new XiaoYu_LAM.CommandLink();
            this.VerifyConfigwizardPage = new AeroWizard.WizardPage();
            this.richTextBox1 = new System.Windows.Forms.RichTextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.ChooseDeepThinkwizardPage = new AeroWizard.WizardPage();
            this.commandLink7 = new XiaoYu_LAM.CommandLink();
            this.commandLink6 = new XiaoYu_LAM.CommandLink();
            this.commandLink5 = new XiaoYu_LAM.CommandLink();
            this.FinishIWizardPage = new AeroWizard.WizardPage();
            this.label7 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.wizardControl1)).BeginInit();
            this.ChooseProviderWizardPage.SuspendLayout();
            this.InputAPIConfigWizardPage.SuspendLayout();
            this.ChooseProtocolWizardPage.SuspendLayout();
            this.VerifyConfigwizardPage.SuspendLayout();
            this.ChooseDeepThinkwizardPage.SuspendLayout();
            this.FinishIWizardPage.SuspendLayout();
            this.SuspendLayout();
            // 
            // wizardControl1
            // 
            this.wizardControl1.Location = new System.Drawing.Point(0, 0);
            this.wizardControl1.Name = "wizardControl1";
            this.wizardControl1.Pages.Add(this.ChooseProviderWizardPage);
            this.wizardControl1.Pages.Add(this.InputAPIConfigWizardPage);
            this.wizardControl1.Pages.Add(this.ChooseProtocolWizardPage);
            this.wizardControl1.Pages.Add(this.VerifyConfigwizardPage);
            this.wizardControl1.Pages.Add(this.ChooseDeepThinkwizardPage);
            this.wizardControl1.Pages.Add(this.FinishIWizardPage);
            this.wizardControl1.Size = new System.Drawing.Size(544, 441);
            this.wizardControl1.TabIndex = 0;
            this.wizardControl1.Title = "Windows 晓予配置向导";
            this.wizardControl1.TitleIcon = ((System.Drawing.Icon)(resources.GetObject("wizardControl1.TitleIcon")));
            // 
            // ChooseProviderWizardPage
            // 
            this.ChooseProviderWizardPage.Controls.Add(this.commandLink2);
            this.ChooseProviderWizardPage.Controls.Add(this.UseRemoteLLMProvider);
            this.ChooseProviderWizardPage.HelpText = "";
            this.ChooseProviderWizardPage.Name = "ChooseProviderWizardPage";
            this.ChooseProviderWizardPage.NextPage = this.InputAPIConfigWizardPage;
            this.ChooseProviderWizardPage.ShowCancel = false;
            this.ChooseProviderWizardPage.ShowNext = false;
            this.ChooseProviderWizardPage.Size = new System.Drawing.Size(497, 285);
            this.ChooseProviderWizardPage.TabIndex = 0;
            this.ChooseProviderWizardPage.Text = "您希望由谁来驱动晓予？";
            // 
            // commandLink2
            // 
            this.commandLink2.Dock = System.Windows.Forms.DockStyle.Top;
            this.commandLink2.Enabled = false;
            this.commandLink2.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.commandLink2.Location = new System.Drawing.Point(0, 59);
            this.commandLink2.Name = "commandLink2";
            this.commandLink2.NoteText = "需要您的电脑是Windows 11 AI+ PC。暂不支持，将在后续适配";
            this.commandLink2.Size = new System.Drawing.Size(497, 60);
            this.commandLink2.TabIndex = 1;
            this.commandLink2.Text = "通过Windows AI API继续";
            this.commandLink2.UseVisualStyleBackColor = true;
            // 
            // UseRemoteLLMProvider
            // 
            this.UseRemoteLLMProvider.Dock = System.Windows.Forms.DockStyle.Top;
            this.UseRemoteLLMProvider.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.UseRemoteLLMProvider.Location = new System.Drawing.Point(0, 0);
            this.UseRemoteLLMProvider.Name = "UseRemoteLLMProvider";
            this.UseRemoteLLMProvider.NoteText = "您需要保持Internet连接";
            this.UseRemoteLLMProvider.Size = new System.Drawing.Size(497, 59);
            this.UseRemoteLLMProvider.TabIndex = 0;
            this.UseRemoteLLMProvider.Text = "通过远程的LLM服务商";
            this.UseRemoteLLMProvider.UseVisualStyleBackColor = true;
            this.UseRemoteLLMProvider.Click += new System.EventHandler(this.UseRemoteLLMProvider_Click);
            // 
            // InputAPIConfigWizardPage
            // 
            this.InputAPIConfigWizardPage.Controls.Add(this.label4);
            this.InputAPIConfigWizardPage.Controls.Add(this.label3);
            this.InputAPIConfigWizardPage.Controls.Add(this.label2);
            this.InputAPIConfigWizardPage.Controls.Add(this.textBox3);
            this.InputAPIConfigWizardPage.Controls.Add(this.textBox2);
            this.InputAPIConfigWizardPage.Controls.Add(this.textBox1);
            this.InputAPIConfigWizardPage.Controls.Add(this.label1);
            this.InputAPIConfigWizardPage.Name = "InputAPIConfigWizardPage";
            this.InputAPIConfigWizardPage.ShowCancel = false;
            this.InputAPIConfigWizardPage.Size = new System.Drawing.Size(497, 285);
            this.InputAPIConfigWizardPage.TabIndex = 1;
            this.InputAPIConfigWizardPage.Text = "请输入LLM相关配置";
            this.InputAPIConfigWizardPage.Commit += new System.EventHandler<AeroWizard.WizardPageConfirmEventArgs>(this.InputAPIConfigWizardPage_Commit);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(8, 136);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(48, 17);
            this.label4.TabIndex = 6;
            this.label4.Text = "模型ID:";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(3, 109);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(54, 17);
            this.label3.TabIndex = 5;
            this.label3.Text = "API密钥:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(3, 77);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(54, 17);
            this.label2.TabIndex = 4;
            this.label2.Text = "API地址:";
            // 
            // textBox3
            // 
            this.textBox3.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBox3.Location = new System.Drawing.Point(63, 106);
            this.textBox3.Name = "textBox3";
            this.textBox3.PasswordChar = '*';
            this.textBox3.Size = new System.Drawing.Size(384, 22);
            this.textBox3.TabIndex = 3;
            // 
            // textBox2
            // 
            this.textBox2.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBox2.Location = new System.Drawing.Point(63, 136);
            this.textBox2.Name = "textBox2";
            this.textBox2.Size = new System.Drawing.Size(384, 22);
            this.textBox2.TabIndex = 2;
            // 
            // textBox1
            // 
            this.textBox1.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBox1.Location = new System.Drawing.Point(63, 74);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(384, 22);
            this.textBox1.TabIndex = 1;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Dock = System.Windows.Forms.DockStyle.Top;
            this.label1.Location = new System.Drawing.Point(0, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(371, 51);
            this.label1.TabIndex = 0;
            this.label1.Text = "可以在您的LLM服务商处找到API地址，API密钥，模型ID等相关配置\r\n\r\n请使用具备图片理解能力的模型";
            // 
            // ChooseProtocolWizardPage
            // 
            this.ChooseProtocolWizardPage.AllowCancel = false;
            this.ChooseProtocolWizardPage.Controls.Add(this.commandLink4);
            this.ChooseProtocolWizardPage.Controls.Add(this.commandLink3);
            this.ChooseProtocolWizardPage.Name = "ChooseProtocolWizardPage";
            this.ChooseProtocolWizardPage.ShowCancel = false;
            this.ChooseProtocolWizardPage.ShowNext = false;
            this.ChooseProtocolWizardPage.Size = new System.Drawing.Size(497, 285);
            this.ChooseProtocolWizardPage.TabIndex = 2;
            this.ChooseProtocolWizardPage.Text = "您的LLM服务商使用哪款通信协议？";
            // 
            // commandLink4
            // 
            this.commandLink4.Dock = System.Windows.Forms.DockStyle.Top;
            this.commandLink4.Enabled = false;
            this.commandLink4.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.commandLink4.Location = new System.Drawing.Point(0, 61);
            this.commandLink4.Name = "commandLink4";
            this.commandLink4.NoteText = "暂不支持，将在后续版本中更新";
            this.commandLink4.Size = new System.Drawing.Size(497, 61);
            this.commandLink4.TabIndex = 1;
            this.commandLink4.Text = "Anthropic兼容";
            this.commandLink4.UseVisualStyleBackColor = true;
            this.commandLink4.Click += new System.EventHandler(this.commandLink4_Click);
            // 
            // commandLink3
            // 
            this.commandLink3.Dock = System.Windows.Forms.DockStyle.Top;
            this.commandLink3.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.commandLink3.Location = new System.Drawing.Point(0, 0);
            this.commandLink3.Name = "commandLink3";
            this.commandLink3.NoteText = "大部分LLM服务商使用的协议";
            this.commandLink3.Size = new System.Drawing.Size(497, 61);
            this.commandLink3.TabIndex = 0;
            this.commandLink3.Text = "OpenAI兼容";
            this.commandLink3.UseVisualStyleBackColor = true;
            this.commandLink3.Click += new System.EventHandler(this.commandLink3_Click);
            // 
            // VerifyConfigwizardPage
            // 
            this.VerifyConfigwizardPage.AllowBack = false;
            this.VerifyConfigwizardPage.AllowNext = false;
            this.VerifyConfigwizardPage.Controls.Add(this.richTextBox1);
            this.VerifyConfigwizardPage.Controls.Add(this.label5);
            this.VerifyConfigwizardPage.Name = "VerifyConfigwizardPage";
            this.VerifyConfigwizardPage.ShowCancel = false;
            this.VerifyConfigwizardPage.Size = new System.Drawing.Size(497, 285);
            this.VerifyConfigwizardPage.TabIndex = 3;
            this.VerifyConfigwizardPage.Text = "正在验证您的配置";
            this.VerifyConfigwizardPage.Initialize += new System.EventHandler<AeroWizard.WizardPageInitEventArgs>(this.VerifyConfigwizardPage_Initialize);
            // 
            // richTextBox1
            // 
            this.richTextBox1.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.richTextBox1.Location = new System.Drawing.Point(6, 38);
            this.richTextBox1.Name = "richTextBox1";
            this.richTextBox1.Size = new System.Drawing.Size(376, 217);
            this.richTextBox1.TabIndex = 1;
            this.richTextBox1.Text = "准备发起请求\n";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(3, 4);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(188, 17);
            this.label5.TabIndex = 0;
            this.label5.Text = "请稍后，正在等待您的服务商响应";
            // 
            // ChooseDeepThinkwizardPage
            // 
            this.ChooseDeepThinkwizardPage.AllowBack = false;
            this.ChooseDeepThinkwizardPage.AllowCancel = false;
            this.ChooseDeepThinkwizardPage.AllowNext = false;
            this.ChooseDeepThinkwizardPage.Controls.Add(this.commandLink7);
            this.ChooseDeepThinkwizardPage.Controls.Add(this.commandLink6);
            this.ChooseDeepThinkwizardPage.Controls.Add(this.commandLink5);
            this.ChooseDeepThinkwizardPage.Name = "ChooseDeepThinkwizardPage";
            this.ChooseDeepThinkwizardPage.ShowCancel = false;
            this.ChooseDeepThinkwizardPage.ShowNext = false;
            this.ChooseDeepThinkwizardPage.Size = new System.Drawing.Size(497, 285);
            this.ChooseDeepThinkwizardPage.TabIndex = 4;
            this.ChooseDeepThinkwizardPage.Text = "您希望使用深度思考功能吗？";
            // 
            // commandLink7
            // 
            this.commandLink7.Dock = System.Windows.Forms.DockStyle.Top;
            this.commandLink7.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.commandLink7.Location = new System.Drawing.Point(0, 120);
            this.commandLink7.Name = "commandLink7";
            this.commandLink7.NoteText = "响应速度非常慢，将会把思考深度设置为 深入研究";
            this.commandLink7.Size = new System.Drawing.Size(497, 60);
            this.commandLink7.TabIndex = 2;
            this.commandLink7.Text = "每一步都深入研究";
            this.commandLink7.UseVisualStyleBackColor = true;
            this.commandLink7.Click += new System.EventHandler(this.commandLink7_Click);
            // 
            // commandLink6
            // 
            this.commandLink6.Dock = System.Windows.Forms.DockStyle.Top;
            this.commandLink6.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.commandLink6.Location = new System.Drawing.Point(0, 60);
            this.commandLink6.Name = "commandLink6";
            this.commandLink6.NoteText = "将会把思考深度设置为 轻度";
            this.commandLink6.Size = new System.Drawing.Size(497, 60);
            this.commandLink6.TabIndex = 1;
            this.commandLink6.Text = "使用推荐的思考深度";
            this.commandLink6.UseVisualStyleBackColor = true;
            this.commandLink6.Click += new System.EventHandler(this.commandLink6_Click);
            // 
            // commandLink5
            // 
            this.commandLink5.Dock = System.Windows.Forms.DockStyle.Top;
            this.commandLink5.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.commandLink5.Location = new System.Drawing.Point(0, 0);
            this.commandLink5.Name = "commandLink5";
            this.commandLink5.NoteText = "最快响应速度";
            this.commandLink5.Size = new System.Drawing.Size(497, 60);
            this.commandLink5.TabIndex = 0;
            this.commandLink5.Text = "不要使用深度思考功能";
            this.commandLink5.UseVisualStyleBackColor = true;
            this.commandLink5.Click += new System.EventHandler(this.commandLink5_Click);
            // 
            // FinishIWizardPage
            // 
            this.FinishIWizardPage.AllowBack = false;
            this.FinishIWizardPage.AllowCancel = false;
            this.FinishIWizardPage.Controls.Add(this.label7);
            this.FinishIWizardPage.IsFinishPage = true;
            this.FinishIWizardPage.Name = "FinishIWizardPage";
            this.FinishIWizardPage.ShowCancel = false;
            this.FinishIWizardPage.Size = new System.Drawing.Size(497, 285);
            this.FinishIWizardPage.TabIndex = 5;
            this.FinishIWizardPage.Text = "配置完成";
            this.FinishIWizardPage.Commit += new System.EventHandler<AeroWizard.WizardPageConfirmEventArgs>(this.FinishIWizardPage_Commit);
            this.FinishIWizardPage.Initialize += new System.EventHandler<AeroWizard.WizardPageInitEventArgs>(this.FinishIWizardPage_Initialize);
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Dock = System.Windows.Forms.DockStyle.Top;
            this.label7.Location = new System.Drawing.Point(0, 0);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(252, 17);
            this.label7.TabIndex = 0;
            this.label7.Text = "配置已经写入到程序根目录下config.ini文件中";
            // 
            // AeroSetupAPIWizard
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(544, 441);
            this.Controls.Add(this.wizardControl1);
            this.Name = "AeroSetupAPIWizard";
            this.Text = "AeroSetupAPIWizard";
            this.Load += new System.EventHandler(this.AeroSetupAPIWizard_Load);
            ((System.ComponentModel.ISupportInitialize)(this.wizardControl1)).EndInit();
            this.ChooseProviderWizardPage.ResumeLayout(false);
            this.ChooseProviderWizardPage.PerformLayout();
            this.InputAPIConfigWizardPage.ResumeLayout(false);
            this.InputAPIConfigWizardPage.PerformLayout();
            this.ChooseProtocolWizardPage.ResumeLayout(false);
            this.VerifyConfigwizardPage.ResumeLayout(false);
            this.VerifyConfigwizardPage.PerformLayout();
            this.ChooseDeepThinkwizardPage.ResumeLayout(false);
            this.FinishIWizardPage.ResumeLayout(false);
            this.FinishIWizardPage.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private AeroWizard.WizardControl wizardControl1;
        private AeroWizard.WizardPage ChooseProviderWizardPage;
        private CommandLink commandLink2;
        private CommandLink UseRemoteLLMProvider;
        private AeroWizard.WizardPage InputAPIConfigWizardPage;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textBox3;
        private System.Windows.Forms.TextBox textBox2;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private AeroWizard.WizardPage ChooseProtocolWizardPage;
        private CommandLink commandLink4;
        private CommandLink commandLink3;
        private AeroWizard.WizardPage VerifyConfigwizardPage;
        private System.Windows.Forms.RichTextBox richTextBox1;
        private System.Windows.Forms.Label label5;
        private AeroWizard.WizardPage ChooseDeepThinkwizardPage;
        private CommandLink commandLink7;
        private CommandLink commandLink6;
        private CommandLink commandLink5;
        private AeroWizard.WizardPage FinishIWizardPage;
        private System.Windows.Forms.Label label7;
    }
}