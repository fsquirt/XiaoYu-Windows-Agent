using Anthropic;
using Anthropic.Core;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Moderations;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using XiaoYu_LAM.ToolForm;

namespace XiaoYu_LAM
{
    public partial class WelcomeForm : Form
    {
        public bool IsConfigValid = false;

        public int ClickLabel1Time = 0;
        public WelcomeForm()
        {
            InitializeComponent();
        }

        private void WelcomeForm_Load(object sender, EventArgs e)
        {
            try
            {
                string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
                if (System.IO.File.Exists(path))
                {
                    var lines = System.IO.File.ReadAllLines(path, System.Text.Encoding.UTF8);
                    var config = new Dictionary<string, string>();
                    foreach (var line in lines)
                    {
                        if (line.Contains('='))
                        {
                            var parts = line.Split(new char[] { '=' }, 2);
                            config[parts[0].Trim()] = parts[1].Trim();
                        }
                    }
                    textBox1.Text = config["API_URL"];
                    textBox2.Text = config["API_KEY"];
                    textBox3.Text = config["MODEL_NAME"];

                    if (config["PROTOCOL"] == "Anthropic")
                    {
                        checkBox2.Checked = true;
                    }
                    else if (config["PROTOCOL"] == "OpenAI")
                    {
                        checkBox1.Checked = true;
                    }

                    IsConfigValid = true;
                }
                else
                {
                    toolStripStatusLabel1.Text = "这好像是你第一次使用，请使用支持输入图片的LLM";
                }
            }
            catch
            {
                toolStripStatusLabel1.Text = "就绪";
            }

        }

        private void label1_Click(object sender, EventArgs e)
        {
            ClickLabel1Time++;
            if (ClickLabel1Time >= 1)
            {
                UserDebug scanWindow = new UserDebug();
                scanWindow.Show();
            }
        }

        private async void button2_Click(object sender, EventArgs e)
        {
            try
            {
                string API_URL = textBox1.Text;
                string API_KEY = textBox2.Text;
                string MODEL_NAME = textBox3.Text;

                // 添加Anthropic支持
                if (checkBox1.Checked)
                {
                    // 创建OPENAI客户端并尝试发送一句测试信息等待LLM回复，以验证API URL、API Key和模型名称的正确性
                    ChatClient client = new ChatClient(
                        model: MODEL_NAME,
                        credential: new ApiKeyCredential(API_KEY),
                        options: new OpenAIClientOptions()
                        {
                            Endpoint = new Uri(API_URL)
                        });

                    toolStripStatusLabel1.Text = "正在等待" + API_URL + "响应";

                    ChatCompletion completion = client.CompleteChat("速速回我任意内容，我正在测试和你的聊天API是否正常");
                    if (completion.Content[0].Text.Length > 15)
                    {
                        toolStripStatusLabel1.Text = MODEL_NAME + "成功响应:" + completion.Content[0].Text.Substring(0, 15).Replace('\n', '。') + "..."; //取15个够了多了会打乱ui
                    }
                    else
                    {
                        toolStripStatusLabel1.Text = MODEL_NAME + "成功响应:" + completion.Content[0].Text.Replace('\n', '。') + "..."; //取15个够了多了会打乱ui
                    }
                    button2.Text = "验证通过";

                    client = null;
                    completion = null;
                }
                else if (checkBox2.Checked)
                {
                    AnthropicClient client = new AnthropicClient { ApiKey = API_KEY, BaseUrl = API_URL };
                    IChatClient chatClient = client.AsIChatClient(MODEL_NAME)
                        .AsBuilder()
                        .UseFunctionInvocation()
                        .Build();
                    toolStripStatusLabel1.Text = "正在等待" + API_URL + "响应";

                    var response = await chatClient.GetResponseAsync("速速回我任意内容，我正在测试和你的聊天API是否正常");
                    if (response.Text.Length > 15)
                    {
                        toolStripStatusLabel1.Text = MODEL_NAME + "成功响应:" + response.ToString().Substring(0, 15).Replace('\n', '。') + "..."; //取15个够了多了会打乱ui
                    }
                    else
                    {
                        toolStripStatusLabel1.Text = MODEL_NAME + "成功响应:" + response.ToString().Replace('\n', '。') + "...";
                    }

                    button2.Text = "验证通过";

                    client = null;
                    chatClient = null;
                }
                else
                {
                    toolStripStatusLabel1.Text = "请选择协议类型";
                    button2.Text = "失败";
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show("发生错误：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                button2.Text = "失败";
            }

        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                AgentEngine.ConfigManager.ApiUrl = (textBox1.Text ?? string.Empty).Trim();
                AgentEngine.ConfigManager.ApiKey = (textBox2.Text ?? string.Empty).Trim();
                AgentEngine.ConfigManager.ModelName = (textBox3.Text ?? string.Empty).Trim();
                AgentEngine.ConfigManager.Protocol = checkBox1.Checked ? "OpenAI" : (checkBox2.Checked ? "Anthropic" : "Unknown");

                AgentEngine.ConfigManager.SaveConfig();

                var main = new MainForm();
                main.FormClosed += (s, ev) => this.Close();
                main.Show();
                this.Hide();
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存配置时发生错误：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void label2_Click(object sender, EventArgs e)
        {
            ClickLabel1Time++;
            if (ClickLabel1Time >= 15)
            {
                Win32APIDesktop scanWindow = new Win32APIDesktop();
                scanWindow.Show();
            }
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            MessageBox.Show("这是好事啊，但是我手头没有带NPU的电脑，所以还没有适配");
        }

        private void checkBox2_Click(object sender, EventArgs e)
        {
            if (checkBox1.Checked)
            {
                checkBox1.Checked = false;
            }
        }

        private void checkBox1_Click(object sender, EventArgs e)
        {
            if (checkBox2.Checked)
            {
                checkBox2.Checked = false;
            }

        }
    }
}
