using OpenAI;
using OpenAI.Chat;
using OpenAI.Moderations;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
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

        public int ClickLabel1Time = 0;
        public WelcomeForm()
        {
            InitializeComponent();
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

            // 从程序目录下的 config.ini 文件中读取配置
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
                }
                else
                {
                    toolStripStatusLabel1.Text = "这好像是你第一次使用，请使用支持输入图片的LLM";
                }
            }
            catch (Exception ex)
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

        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                string API_URL = textBox1.Text;
                string API_KEY = textBox2.Text;
                string MODEL_NAME = textBox3.Text;
                // 创建OPENAI客户端并尝试发送一句测试信息等待LLM回复，以验证API URL、API Key和模型名称的正确性
                ChatClient client = new ChatClient(
                    model: MODEL_NAME,
                    credential: new ApiKeyCredential(API_KEY),
                    options: new OpenAIClientOptions()
                    {
                        Endpoint = new Uri(API_URL)
                });

                toolStripStatusLabel1.Text = "正在等待" + API_URL + "响应";

                ChatCompletion completion = client.CompleteChat("爱你");
                Console.WriteLine($"[ASSISTANT]: {completion.Content[0].Text}");
                toolStripStatusLabel1.Text = MODEL_NAME + "成功响应:" + completion.Content[0].Text.Substring(0,15).Replace('\n','。') + "..."; //取15个够了多了会打乱ui
                button2.Text = "验证通过";

                client = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show("发生错误：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        private void button1_Click(object sender, EventArgs e)
        {
            // 将API配置保存到程序目录下config.ini文件然后启动主窗口
            try
            {
                string apiUrl = (textBox1.Text ?? string.Empty).Trim();
                string apiKey = (textBox2.Text ?? string.Empty).Trim();
                string modelName = (textBox3.Text ?? string.Empty).Trim();
                string protocol = checkBox1.Checked ? "OpenAI" : (checkBox2.Checked ? "Anthropic" : "Unknown");

                var lines = new System.Collections.Generic.List<string>();
                lines.Add("[LLM_PROVIDER]");
                lines.Add("API_URL=" + apiUrl);
                lines.Add("API_KEY=" + apiKey);
                lines.Add("MODEL_NAME=" + modelName);
                lines.Add("PROTOCOL=" + protocol);

                string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
                System.IO.File.WriteAllLines(path, lines, System.Text.Encoding.UTF8);

                toolStripStatusLabel1.Text = ("配置已保存: " + path);

                // 启动主窗口并隐藏当前向导，关闭主窗口时同时关闭向导以结束程序
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
            if (ClickLabel1Time >= 1)
            {
                Win32APIDesktop scanWindow = new Win32APIDesktop();
                scanWindow.Show();
            }
        }
    }
}
