using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using XiaoYu_LAM.ToolForm;

namespace XiaoYu_LAM
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        public string MODEL_NAME = "";
        public string API_URL = "";
        public string API_KEY = "";

        public string PROTOCOL = "";

        private void MainForm_Load(object sender, EventArgs e)
        {
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
                    // 在状态栏显示当前使用的模型和协议
                    if (config.ContainsKey("MODEL_NAME") && config.ContainsKey("PROTOCOL"))
                    {
                        toolStripStatusLabel1.Text = $"当前模型: {config["MODEL_NAME"]}, 协议: {config["PROTOCOL"]}";
                        toolStripStatusLabel2.Text = config["API_URL"];

                        MODEL_NAME = config["MODEL_NAME"];
                        PROTOCOL = config["PROTOCOL"];
                        API_URL = config["API_URL"];
                        API_KEY = config["API_KEY"];

                        if((MODEL_NAME == "") || (PROTOCOL == "") || (API_KEY == "") || (API_URL == ""))
                        {
                            toolStripStatusLabel1.Text = "配置文件缺少字段！请在欢迎窗口重新配置并验证可用性";
                        }
                    }
                    else
                    {
                        toolStripStatusLabel1.Text = "配置文件缺少字段！请在欢迎窗口重新配置并验证可用性";
                    }
                }
                else
                {
                    toolStripStatusLabel1.Text = "未找到配置文件 config.ini";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("读取配置文件时发生错误：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                toolStripStatusLabel1.Text = "读取配置文件失败";
            }
            
            //将配置显示到设置页面里
            textBox1.Text = API_URL;
            textBox3.Text = API_KEY;
            textBox2.Text = MODEL_NAME;
            if(PROTOCOL == "OpenAI")
            {
                IsOpenAICheckBox.Checked = true;
            }
            else if(PROTOCOL == "Anthropic")
            {
                IsAnthropicCheckBox.Checked = true;
            }
        }

        public void AppendLog(string role, string message)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => AppendLog(role, message)));
                return;
            }

            // 格式化日志
            string time = DateTime.Now.ToString("HH:mm:ss");
            LogrichTextBox1.SelectionColor = role == "AI" ? Color.Blue : (role == "System" ? Color.Red : Color.Black);
            LogrichTextBox1.AppendText($"[{time}] <{role}>: {message}\n\n");
            LogrichTextBox1.ScrollToCaret(); // 滚动到最下面
        }

        /// <summary>
        /// 在对话流中插入带有红框的截图
        /// </summary>
        public void AppendImageToLog(string role, Bitmap bmp)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => AppendImageToLog(role, bmp)));
                return;
            }

            string time = DateTime.Now.ToString("HH:mm:ss");
            LogrichTextBox1.SelectionStart = LogrichTextBox1.TextLength;
            LogrichTextBox1.SelectionColor = Color.Gray;
            LogrichTextBox1.AppendText($"[{time}] <{role}> 获取了最新界面截图：\n");

            try
            {
                // 暂存用户剪贴板
                IDataObject oldData = Clipboard.GetDataObject();
                // 写入图片并粘贴
                Clipboard.SetImage(bmp);
                LogrichTextBox1.SelectionStart = LogrichTextBox1.TextLength;
                LogrichTextBox1.Paste();
                // 恢复剪贴板
                if (oldData != null) Clipboard.SetDataObject(oldData);
            }
            catch (Exception ex)
            {
                LogrichTextBox1.AppendText($"[图片显示失败: {ex.Message}]\n");
            }

            LogrichTextBox1.AppendText("\n\n");
            LogrichTextBox1.ScrollToCaret();
        }

        public void UpdateVisionImage(Bitmap bmp)
        {
            // 1. 跨线程安全调用
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => UpdateVisionImage(bmp)));
                return;
            }
            Image oldImage = pictureBox1.Image;
            pictureBox1.Image = null;
            if (oldImage != null)
            {
                oldImage.Dispose();
            }
            if (bmp != null)
            {
                pictureBox1.Image = new Bitmap(bmp);
            }
        }

        private void 关于晓予ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string appName = "Windows晓予";
            string otherStuff = "版本: 0.1 (Beta)\n基于无障碍接口让LLM操作Windows";
            IntPtr iconHandle = this.Icon != null ? this.Icon.Handle : IntPtr.Zero;

            // 弹出 Windows 经典的关于对话框
            ShellAbout(this.Handle, appName, otherStuff, iconHandle);
        }

        private void 新建任务ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ChatForm chatForm = new ChatForm(this);
            chatForm.Show();
        }

        [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int ShellAbout(IntPtr hWnd, string szApp, string szOtherStuff, IntPtr hIcon);

    }
}
