using Anthropic;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using XiaoYu_LAM.AgentEngine;

namespace XiaoYu_LAM.UserForm
{
    public partial class AeroSetupAPIWizard : Form
    {
        public AeroSetupAPIWizard()
        {
            InitializeComponent();
            Control.CheckForIllegalCrossThreadCalls = false;
        }

        private void UseRemoteLLMProvider_Click(object sender, EventArgs e)
        {
            if (wizardControl1 != null && wizardControl1.Pages != null && wizardControl1.Pages.Count > 2)
            {
                wizardControl1.NextPage(wizardControl1.Pages[1]);
            }
        }

        private void commandLink3_Click(object sender, EventArgs e)
        {
            if (wizardControl1 != null && wizardControl1.Pages != null && wizardControl1.Pages.Count > 2)
            {
                wizardControl1.NextPage(wizardControl1.Pages[3]);
            }
            ConfigManager.Protocol = "OpenAI";
            ConfigManager.SaveConfig();
        }

        private void commandLink4_Click(object sender, EventArgs e)
        {
            if (wizardControl1 != null && wizardControl1.Pages != null && wizardControl1.Pages.Count > 2)
            {
                wizardControl1.NextPage(wizardControl1.Pages[3]);
            }
            ConfigManager.Protocol = "Anthropic";
        }

        public void VerifyAPI()
        {
            // 验证API有效性
            try
            {
                if (ConfigManager.Protocol == "OpenAI")
                {
                    ChatClient client = new ChatClient(
                        model: ConfigManager.ModelName,
                        credential: new ApiKeyCredential(ConfigManager.ApiKey),
                        options: new OpenAIClientOptions()
                        {
                            Endpoint = new Uri(ConfigManager.ApiUrl)
                        });

                    richTextBox1.Text = richTextBox1.Text + "正在等待" + ConfigManager.ApiUrl + "响应\n";

                    ChatCompletion completion = client.CompleteChat("速速回我任意内容，我正在测试和你的聊天API是否正常");
                    richTextBox1.Text = richTextBox1.Text + "响应内容:" + completion.Content[0].Text + "\n";

                    client = null;
                    completion = null;

                    richTextBox1.Text = richTextBox1.Text + "配置有效";

                    wizardControl1.Pages[3].AllowNext = true;
                }
                else if (ConfigManager.Protocol == "Anthropic")
                {

                }
                else
                {
                    MessageBox.Show("发生错误：检测不到有效的协议类型", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    SetupFailed();
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show("发生错误：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetupFailed();
            }
        }

        private void VerifyConfigwizardPage_Initialize(object sender, AeroWizard.WizardPageInitEventArgs e)
        {
            Thread verifyThread = new Thread(VerifyAPI);
            verifyThread.Start();
        }

        private void InputAPIConfigWizardPage_Commit(object sender, AeroWizard.WizardPageConfirmEventArgs e)
        {
            ConfigManager.ApiKey = textBox3.Text;
            ConfigManager.ApiUrl = textBox1.Text;
            ConfigManager.ModelName = textBox2.Text;
            ConfigManager.SaveConfig();
        }

        private void commandLink5_Click(object sender, EventArgs e)
        {
            ConfigManager.IsDeepThinkMode = false;
            ConfigManager.ThinkingDeepth = 0;
            ConfigManager.SaveConfig();
            if (wizardControl1 != null && wizardControl1.Pages != null && wizardControl1.Pages.Count > 2)
            {
                wizardControl1.NextPage(wizardControl1.Pages[5]);
            }
        }

        private void commandLink6_Click(object sender, EventArgs e)
        {
            ConfigManager.IsDeepThinkMode = true;
            ConfigManager.ThinkingDeepth = 1;
            ConfigManager.SaveConfig();
            if (wizardControl1 != null && wizardControl1.Pages != null && wizardControl1.Pages.Count > 2)
            {
                wizardControl1.NextPage(wizardControl1.Pages[5]);
            }
        }

        private void commandLink7_Click(object sender, EventArgs e)
        {
            ConfigManager.IsDeepThinkMode = true;
            ConfigManager.ThinkingDeepth = 4;
            ConfigManager.SaveConfig();
            if (wizardControl1 != null && wizardControl1.Pages != null && wizardControl1.Pages.Count > 2)
            {
                wizardControl1.NextPage(wizardControl1.Pages[5]);
            }
        }

        private void FinishIWizardPage_Initialize(object sender, AeroWizard.WizardPageInitEventArgs e)
        {
            ConfigManager.SaveConfig();
        }

        private void FinishIWizardPage_Commit(object sender, AeroWizard.WizardPageConfirmEventArgs e)
        {
            this.DialogResult = DialogResult.OK;
        }

        private void AeroSetupAPIWizard_Load(object sender, EventArgs e)
        {

        }

        private void SetupFailed()
        {
            //删除程序根目录下config.ini
            File.Delete(Environment.CurrentDirectory + "\\config.ini");
            Environment.Exit(0);
        }
    }
}
