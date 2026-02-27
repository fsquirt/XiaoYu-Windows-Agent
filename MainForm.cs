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
            richTextBox1.SelectionColor = role == "AI" ? Color.Blue : (role == "System" ? Color.Red : Color.Black);
            richTextBox1.AppendText($"[{time}] <{role}>: {message}\n\n");
            richTextBox1.ScrollToCaret(); // 滚动到最下面
        }

        public void UpdateVisionImage_bak(Bitmap bmp)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => UpdateVisionImage(bmp)));
                return;
            }

            if (pictureBox1.Image != null)
            {
                pictureBox1.Image.Dispose();
            }
            // 克隆一份防止跨线程占用冲突
            pictureBox1.Image = new Bitmap(bmp);
        }

        public void UpdateVisionImage(Bitmap bmp)
        {
            // 1. 跨线程安全调用
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => UpdateVisionImage(bmp)));
                return;
            }

            // 2. 暂存并断开旧图片引用
            // 这里必须先设为 null，否则在 Dispose 时 PictureBox 可能会尝试重绘已释放的资源导致红叉或崩溃
            Image oldImage = pictureBox1.Image;
            pictureBox1.Image = null;

            // 3. 立即销毁旧图片
            // 释放 GDI+ 句柄和非托管内存，这是降内存的关键
            if (oldImage != null)
            {
                oldImage.Dispose();
            }

            // 4. 赋值新图片
            // 使用 new Bitmap(bmp) 进行深拷贝是正确的，这防止了源 bmp 在其他线程被 Dispose 后导致 UI 崩溃
            if (bmp != null)
            {
                pictureBox1.Image = new Bitmap(bmp);
            }

            // 5. 【关键优化】强制垃圾回收
            // 通常不建议手动调用 GC，但在处理大量 Bitmap/GDI 对象时，这是标准做法。
            // 因为 Bitmap 占用的是"非托管堆"，CLR 经常感知不到内存压力而不去回收。
            // 这行代码会强制 CLR 立即清理刚才 Dispose 掉的旧图片内存。
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        private void 关于晓予ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string appName = "晓予";
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
