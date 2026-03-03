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

        // 检查启动的时候是否带有 --task 参数，如果有自动开始LLM执行任务
        public void CheckArgTask()
        {
            if (Environment.GetCommandLineArgs().Contains("--task"))
            {
                // 输出参数内容
                string[] args = Environment.GetCommandLineArgs();
                MessageBox.Show("自动执行LLM任务:" + args[2], "启动参数", MessageBoxButtons.OK, MessageBoxIcon.Information);

                // 直接启动主窗口并隐藏当前向导
                var main = new MainForm();
                main.FormClosed += (s, ev) => this.Close();
                main.Show();
                this.Hide();
            }
        }

        private void WelcomeForm_Load(object sender, EventArgs e)
        {
            // 检测是否在管理员权限下运行
            bool isAdmin = false;
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                isAdmin = false;
            }

            if (!isAdmin)
            {
                var result = MessageBox.Show("晓予未以管理员权限运行，晓予将无法操作管理员身份运行的进程窗口。是否以管理员身份重新启动？", "权限不足", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result == DialogResult.Yes)
                {
                    try
                    {
                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = Application.ExecutablePath,
                            UseShellExecute = true,
                            Verb = "runas"
                        };
                        System.Diagnostics.Process.Start(psi);
                        Application.Exit();
                        return;
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("无法以管理员权限重新启动晓予，请确认当前Windows用户权限", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    this.Text = this.Text + "（非管理员权限）";
                }
            }
            else
            {
                this.Text = this.Text + "（管理员权限）";
            }

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

            if (IsConfigValid)
            {
                CheckArgTask(); //如果配置有效，检查是否带有任务参数，有的话直接执行任务
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
                    if(completion.Content[0].Text.Length > 15)
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
                    if(response.Text.Length > 15)
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
                string apiUrl = (textBox1.Text ?? string.Empty).Trim();
                string apiKey = (textBox2.Text ?? string.Empty).Trim();
                string modelName = (textBox3.Text ?? string.Empty).Trim();
                string protocol = checkBox1.Checked ? "OpenAI" : (checkBox2.Checked ? "Anthropic" : "Unknown");
                string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");

                System.Collections.Generic.List<string> skillsSection = new System.Collections.Generic.List<string>();
                if (System.IO.File.Exists(path))
                {
                    var oldLines = System.IO.File.ReadAllLines(path, System.Text.Encoding.UTF8);
                    bool inSkills = false;
                    foreach (var line in oldLines)
                    {
                        string trimmedLine = line.Trim();
                        // 发现 [SKILLS] 节开始
                        if (trimmedLine.Equals("[SKILLS]", StringComparison.OrdinalIgnoreCase))
                        {
                            inSkills = true;
                        }
                        // 如果已经在 SKILLS 节中，且碰到了下一个 [ 节标志，则停止记录
                        else if (inSkills && trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                        {
                            inSkills = false;
                        }

                        if (inSkills)
                        {
                            skillsSection.Add(line);
                        }
                    }
                }

                // 重新构建文件内容
                var newLines = new System.Collections.Generic.List<string>();

                // 写入 [LLM_PROVIDER] 节
                newLines.Add("[LLM_PROVIDER]");
                newLines.Add("API_URL=" + apiUrl);
                newLines.Add("API_KEY=" + apiKey);
                newLines.Add("MODEL_NAME=" + modelName);
                newLines.Add("PROTOCOL=" + protocol);
                newLines.Add("");

                // 将保留的 [SKILLS] 节写回
                if (skillsSection.Count > 0)
                {
                    newLines.AddRange(skillsSection);
                }
                else
                {
                    // 如果原本没有 [SKILLS] 节，可以初始化一个空的（可选）
                    newLines.Add("[SKILLS]");
                    newLines.Add("ENABLE=False");
                    newLines.Add("SKILLSPATH=");
                }

                // 写入文件
                System.IO.File.WriteAllLines(path, newLines, System.Text.Encoding.UTF8);

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
