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
        private AgentRunner _runner; // 唯一需要的核心引擎句柄

        private string _sessionDirectory;

        // 用于管理流式输出状态
        private bool _isAiTyping = false;
        private string _currentFileName = "";

        // 流式输出缓冲区
        private StringBuilder _streamBuffer = new StringBuilder();
        private System.Windows.Forms.Timer _uiRefreshTimer;
        private object _bufferLock = new object();

        public ChatForm(MainForm mainForm)
        {
            InitializeComponent();
            _mainForm = mainForm;

            _sessionDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MarkDown", "conversation");
            if (!Directory.Exists(_sessionDirectory)) Directory.CreateDirectory(_sessionDirectory);

            // 实例化执行器（它内部会自动 new MSAFEngine 和 UIAEngine 并完成配置）
            _runner = new AgentRunner();

            // 绑定事件：文本输出
            _runner.OnStreamText += txt => AppendStreamText(txt);

            // 绑定事件：工具调用开始
            _runner.OnToolCall += tool => {
                if (_isAiTyping) { AppendStreamText("\n\n"); _isAiTyping = false; }
                if (!ConfigManager.IsHideUIAoutInChatForm) AppendLog("System", $"🔄 正在调用工具: {tool}...");
            };

            // 绑定事件：工具调用结果
            _runner.OnToolResult += (tool, res) => {
                if (_isAiTyping) { AppendStreamText("\n\n"); _isAiTyping = false; }
                if (!ConfigManager.IsHideUIAoutInChatForm) AppendLog("ToolResult", $"[{tool}] 结果: \n{res}");
            };

            // 绑定事件：系统日志
            _runner.OnLog += AppendLog;

            // 绑定事件：底层截图产生
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

            _uiRefreshTimer = new System.Windows.Forms.Timer();
            _uiRefreshTimer.Interval = 500;
            _uiRefreshTimer.Tick += UiRefreshTimer_Tick;
            _uiRefreshTimer.Start();
        }

        private void ChatForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_uiRefreshTimer != null)
            {
                _uiRefreshTimer.Stop();
                _uiRefreshTimer.Dispose();
                _uiRefreshTimer = null;
            }

            // 统一销毁 Runner，它内部会处理取消任务和释放资源
            if (_runner != null)
            {
                _runner.Dispose();
                _runner = null;
            }

            ConversationRichTextBox.Clear();
            ConversationRichTextBox.Dispose();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private void UiRefreshTimer_Tick(object sender, EventArgs e)
        {
            if (this.IsDisposed || !this.Visible) return;

            string textToPrint = "";
            lock (_bufferLock)
            {
                if (_streamBuffer.Length == 0) return;
                textToPrint = _streamBuffer.ToString();
                _streamBuffer.Clear();
            }

            // 执行写入操作
            WriteTextToBox(textToPrint, true);

            textToPrint = null; // 释放字符串引用，帮助 GC 回收
        }

        private void WriteTextToBox(string text, bool isAiContent, string role = null)
        {
            // 挂起绘制
            SendMessage(ConversationRichTextBox.Handle, WM_SETREDRAW, 0, IntPtr.Zero);

            try
            {
                // 1. 移动光标到末尾
                // 修复：必须强制转换为 (IntPtr)
                // -2 (0xFFFFFFFE) 和 -1 (0xFFFFFFFF) 是 RichEdit 的特殊常量
                SendMessage(ConversationRichTextBox.Handle, EM_SETSEL, (-2), (IntPtr)(-1));

                // 2. 处理 AI 流式输出的 Header
                if (isAiContent)
                {
                    if (!_isAiTyping)
                    {
                        string time = DateTime.Now.ToString("HH:mm:ss");
                        ConversationRichTextBox.SelectionColor = Color.Blue;
                        ConversationRichTextBox.SelectedText = $"\n\n[{time}] <AI>: "; // 补一个换行区分上一条
                        _isAiTyping = true;
                        time = null;
                    }
                    ConversationRichTextBox.SelectionColor = Color.Blue;
                }
                else
                {
                    // Log 输出，根据角色定颜色
                    _isAiTyping = false; // Log 打断了 AI 的连续输出
                    ConversationRichTextBox.SelectionColor = role == "AI" ? Color.Blue : (role == "System" || role == "ToolResult" ? Color.Gray : Color.Black);
                }

                // 3. 写入实际文本
                ConversationRichTextBox.SelectedText = text;

                // 4. 滚动到底部
                SendMessage(ConversationRichTextBox.Handle, WM_VSCROLL, SB_BOTTOM, IntPtr.Zero);

                // 5. 清空撤销缓冲区 (防止内存泄漏的关键)
                SendMessage(ConversationRichTextBox.Handle, EM_EMPTYUNDOBUFFER, 0, IntPtr.Zero);
            }
            finally
            {
                // 恢复绘制
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

            // 检查是否有残留的流式文本没显示
            string pendingText = "";
            lock (_bufferLock)
            {
                if (_streamBuffer.Length > 0)
                {
                    pendingText = _streamBuffer.ToString();
                    _streamBuffer.Clear();
                }
            }

            // 如果有残留，先强制刷入 UI（视为 AI 的发言）
            if (!string.IsNullOrEmpty(pendingText))
            {
                WriteTextToBox(pendingText, true);
            }

            pendingText = null;

            // 写入本次的 Log 信息（视为 System/Tool 的发言，isAiContent=false）
            // 格式化 Log 文本
            string time = DateTime.Now.ToString("HH:mm:ss");
            string logText = $"\n\n[{time}] <{role}>: {message}";

            // 如果刚才是 AI 在说话，或者是第一次说话，不需要开头的换行，微调一下格式
            if (!_isAiTyping && ConversationRichTextBox.TextLength == 0)
            {
                logText = logText.TrimStart('\n');
            }

            WriteTextToBox(logText, false, role);

            time = null;
            logText = null;
        }

        private void AppendStreamText(string text)
        {
            lock (_bufferLock)
            {
                _streamBuffer.Append(text);
            }
        }

        private void AppendImageToUI(Bitmap bmp)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => AppendImageToUI(bmp)));
                return;
            }

            if (_isAiTyping)
            {
                ConversationRichTextBox.AppendText("\n\n");
                _isAiTyping = false;
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