using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using XiaoYu_LAM.AgentEngine;
using XiaoYu_LAM.UIAEngine;

namespace XiaoYu_LAM
{
    public partial class ChatForm : Form
    {
        private MainForm _mainForm;
        private AgentRunner _runner;
        private string _sessionDirectory;
        private string _currentFileName = "";
        private bool _isAiTyping = false;

        public ChatForm(MainForm mainForm)
        {
            InitializeComponent();
            _mainForm = mainForm;

            _sessionDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MarkDown", "conversation");
            if (!Directory.Exists(_sessionDirectory)) Directory.CreateDirectory(_sessionDirectory);

            _runner = new AgentRunner();

            // 流式文本极速追加
            _runner.OnStreamText += AppendStreamText;

            // 工具调用显示
            _runner.OnToolCall += tool => {
                if (_isAiTyping) { AppendStreamText("\n\n"); _isAiTyping = false; }
                if (!ConfigManager.IsHideUIAoutInChatForm) AppendLog("System", $"🔄 正在调用工具: {tool}...");
            };

            // 工具结果显示
            _runner.OnToolResult += (tool, res) => {
                if (_isAiTyping) { AppendStreamText("\n\n"); _isAiTyping = false; }
                if (!ConfigManager.IsHideUIAoutInChatForm) AppendLog("ToolResult", $"[{tool}] 结果: \n{res}");
            };

            _runner.OnLog += AppendLog;
            _runner.OnImageScanned += HandleScannedImage;
        }

        private void HandleScannedImage(Bitmap drawnBmp, Bitmap origBmp)
        {
            // MainForm 的更新
            _mainForm.UpdateVisionImage(drawnBmp);

            // 2. 如果没有勾选“免打扰”，就显示在聊天框里
            if (!ConfigManager.IsHideUIAoutInChatForm)
            {
                // 必须克隆一份，因为原始的 drawnBmp 会在 AgentRunner 的事件流结束后被释放
                Bitmap uiBmp = new Bitmap(drawnBmp);

                try
                {
                    // 使用 Invoke (同步调用) 确保 UI 粘贴完图片之前，uiBmp 不会被 Dispose
                    this.Invoke(new Action(() =>
                    {
                        AppendImageToUI(uiBmp);
                    }));
                }
                finally
                {
                    // 粘贴完了，立刻回收 UI 线程用的这张大图
                    uiBmp.Dispose();
                    uiBmp = null;
                }
            }
        }

        private void ChatForm_Load(object sender, EventArgs e)
        {
            btnStop.Enabled = false;
            LoadSessionList();

            if (comboBox1.Items.Count > 0)
            {
                comboBox1.SelectedIndex = 0;
            }

            this.FormClosing += ChatForm_FormClosing;
        }

        private void ChatForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_runner != null)
            {
                var runnerToDispose = _runner;
                _runner = null; // 剥离UI引用

                // 开启后台线程进行总结，不阻塞窗口关闭
                Task.Run(async () =>
                {
                    try { await runnerToDispose.SummarizeSessionAsync(); }
                    finally { runnerToDispose.Dispose(); }
                });
            }

            ConversationRichTextBox.Clear();
            ConversationRichTextBox.Dispose();
            GC.Collect();
        }

        private void AppendStreamText(string text)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => AppendStreamText(text)));
                return;
            }

            SendMessage(ConversationRichTextBox.Handle, WM_SETREDRAW, 0, IntPtr.Zero);
            try
            {
                SendMessage(ConversationRichTextBox.Handle, EM_SETSEL, (-2), (IntPtr)(-1));

                if (!_isAiTyping)
                {
                    string time = DateTime.Now.ToString("HH:mm:ss");
                    ConversationRichTextBox.SelectionColor = Color.Blue;
                    ConversationRichTextBox.SelectedText = $"\n\n[{time}] <AI>: ";
                    _isAiTyping = true;
                }

                ConversationRichTextBox.SelectionColor = Color.Blue;
                ConversationRichTextBox.SelectedText = text;

                SendMessage(ConversationRichTextBox.Handle, WM_VSCROLL, SB_BOTTOM, IntPtr.Zero);
                SendMessage(ConversationRichTextBox.Handle, EM_EMPTYUNDOBUFFER, 0, IntPtr.Zero);
            }
            finally
            {
                SendMessage(ConversationRichTextBox.Handle, WM_SETREDRAW, 1, IntPtr.Zero);
                ConversationRichTextBox.Invalidate();
            }
        }
        private void AppendLog(string role, string message)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => AppendLog(role, message)));
                return;
            }

            string time = DateTime.Now.ToString("HH:mm:ss");
            string logText = $"\n\n[{time}] <{role}>: \n{message}";

            SendMessage(ConversationRichTextBox.Handle, WM_SETREDRAW, 0, IntPtr.Zero);
            try
            {
                SendMessage(ConversationRichTextBox.Handle, EM_SETSEL, (-2), (IntPtr)(-1));
                ConversationRichTextBox.SelectionColor = role == "AI" ? Color.Blue : (role == "System" || role == "ToolResult" ? Color.Gray : (role == "Memory" ? Color.DarkGreen : Color.Black));
                ConversationRichTextBox.SelectedText = logText;
                SendMessage(ConversationRichTextBox.Handle, WM_VSCROLL, SB_BOTTOM, IntPtr.Zero);
                SendMessage(ConversationRichTextBox.Handle, EM_EMPTYUNDOBUFFER, 0, IntPtr.Zero);
            }
            finally
            {
                SendMessage(ConversationRichTextBox.Handle, WM_SETREDRAW, 1, IntPtr.Zero);
                ConversationRichTextBox.Invalidate();
            }
        }


        private void AppendImageToUI(Bitmap bmp)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => AppendImageToUI(bmp)));
                return;
            }

            string time = DateTime.Now.ToString("HH:mm:ss");
            ConversationRichTextBox.SelectionStart = ConversationRichTextBox.TextLength;
            ConversationRichTextBox.SelectionColor = Color.Gray;
            ConversationRichTextBox.AppendText($"[{time}] <System> 截取了界面：\n");

            time = null;

            // 在贴入 RichTextBox 之前，缩小图片
            int maxWidth = 400; // 缩略图最大宽度
            int newWidth = bmp.Width > maxWidth ? maxWidth : bmp.Width;
            int newHeight = (int)(bmp.Height * ((float)newWidth / bmp.Width));

            using (Bitmap thumbnail = new Bitmap(bmp, new Size(newWidth, newHeight)))
            {
                // 写入缩略图
                Clipboard.SetImage(thumbnail);

                // 粘贴
                ConversationRichTextBox.SelectionStart = ConversationRichTextBox.TextLength;
                ConversationRichTextBox.ReadOnly = false; // 确保可写
                ConversationRichTextBox.Paste();
                ConversationRichTextBox.ReadOnly = true;

                // 立刻清空系统剪贴板，防止底层 DIB 句柄残留
                Clipboard.Clear();
            }

            ConversationRichTextBox.AppendText("\n\n");

            //ConversationRichTextBox.ScrollToCaret();
            SendMessage(ConversationRichTextBox.Handle, WM_VSCROLL, SB_BOTTOM, IntPtr.Zero);
            SendMessage(ConversationRichTextBox.Handle, EM_EMPTYUNDOBUFFER, 0, IntPtr.Zero);
        }

        private void LoadSessionList()
        {
            comboBox1.Items.Clear();
            comboBox1.Items.Add("--- 创建新会话 ---");

            string[] files = Directory.GetFiles(_sessionDirectory, "*.md");
            // 按创建时间倒序排列，把最新的排在前面
            var sortedFiles = files.OrderByDescending(f => File.GetCreationTime(f)).ToArray();

            foreach (var f in sortedFiles)
            {
                comboBox1.Items.Add(Path.GetFileName(f));
            }

            files = null;
            sortedFiles = null;
        }

        // 下拉框选择历史会话
        private async void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox1.SelectedIndex <= 0)
            {
                // 这里的清理可能需要扩展 Runner 的能力，如果是空会话，我们可以不操作，或者给 Runner 加个清空方法
                _currentFileName = "";
                ConversationRichTextBox.Clear();
            }
            else
            {
                try
                {
                    _currentFileName = comboBox1.SelectedItem.ToString();
                    string filePath = Path.Combine(_sessionDirectory, _currentFileName);

                    // 让 Runner 去恢复会话
                    await _runner.RestoreSessionAsync(filePath);

                    // 恢复界面的可视聊天记录
                    string fileText = File.ReadAllText(filePath);
                    var match = Regex.Match(fileText, @"## Chat History\r?\n([\s\S]*?)\r?\n## Session Data");
                    if (match.Success)
                    {
                        ConversationRichTextBox.Text = match.Groups[1].Value.Trim() + "\n\n";
                    }

                    AppendLog("System", $"成功恢复会话: {_currentFileName}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("恢复会话失败: " + ex.Message);
                }
            }
        }

        private async void btnExecute_Click(object sender, EventArgs e)
        {
            string userInput = txtInput.Text.Trim();
            if (string.IsNullOrEmpty(userInput)) return;

            toolStripLabel1.Text = "正在执行任务...";
            btnExecute.Enabled = false;
            btnStop.Enabled = true;
            txtInput.Enabled = false;

            AppendLog("User", userInput);
            txtInput.Clear();

            // 文件名生成逻辑不变
            if (_runner.CurrentSession == null)
            {
                string safeInput = string.Join("_", userInput.Split(Path.GetInvalidFileNameChars())).Replace("\r", "").Replace("\n", "").Replace(" ", "");
                string title = safeInput.Length > 15 ? safeInput.Substring(0, 15) : safeInput;
                _currentFileName = $"{title}_{DateTime.Now:yyyyMMdd_HHmmss}.md";
            }

            // 调用 Runner
            await _runner.RunTaskAsync(userInput);

            // 备份历史
            if (!string.IsNullOrEmpty(_currentFileName))
            {
                string filePath = Path.Combine(_sessionDirectory, _currentFileName);
                await _runner.BackupSessionAsync(filePath, ConversationRichTextBox.Text);
            }

            btnExecute.Enabled = true;
            btnStop.Enabled = false;
            txtInput.Enabled = true;
            toolStripLabel1.Text = "任务执行完毕";
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            _runner.CancelTask();
            btnStop.Enabled = false;
            toolStripLabel1.Text = "任务已终止";
        }



        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int wMsg, int wParam, IntPtr lParam);
        private const int WM_SETREDRAW = 11;
        private const int WM_VSCROLL = 0x0115;
        private const int SB_BOTTOM = 7;
        private const int EM_EMPTYUNDOBUFFER = 0x00CD; // 撤销缓冲区指令
        private const int EM_SETSEL = 0x00B1;
        private const int EM_REPLACESEL = 0x00C2;
    }
}