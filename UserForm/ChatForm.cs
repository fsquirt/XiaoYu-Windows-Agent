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
        private mainEngine _uiaEngine;
        private MSAFEngine _msafEngine;
        private AgentSession _currentSession;
        private CancellationTokenSource _cts;

        private string _sessionDirectory;

        // 用于管理流式输出状态
        private bool _isAiTyping = false;
        // 记录当前会话的文件名
        private string _currentFileName = "";

        // 流式输出缓冲区
        private StringBuilder _streamBuffer = new StringBuilder();
        private System.Windows.Forms.Timer _uiRefreshTimer;
        private object _bufferLock = new object();

        public ChatForm(MainForm mainForm)
        {
            InitializeComponent();
            _mainForm = mainForm;
            _uiaEngine = new mainEngine();
            _msafEngine = new MSAFEngine();

            _sessionDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MarkDown", "conversation");
            if (!Directory.Exists(_sessionDirectory)) Directory.CreateDirectory(_sessionDirectory);

            // 监听底层产生的截图
            _uiaEngine.OnScanCompleted += OnImageScanned;
        }

        private void ChatForm_Load(object sender, EventArgs e)
        {
            btnStop.Enabled = false;
            _msafEngine.CreateAgent(_uiaEngine);
            LoadSessionList();

            // 初始化时选中第一个（新建会话）
            if (comboBox1.Items.Count > 0)
            {
                comboBox1.SelectedIndex = 0;
            }

            this.FormClosing += ChatForm_FormClosing; // 注册关闭事件

            //初始化 UI 刷新定时器
            _uiRefreshTimer = new System.Windows.Forms.Timer();
            _uiRefreshTimer.Interval = 500; //500ms 刷新一次
            _uiRefreshTimer.Tick += UiRefreshTimer_Tick;
            _uiRefreshTimer.Start();
        }

        private void ChatForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 停止定时器，防止它继续访问已销毁的控件
            if (_uiRefreshTimer != null)
            {
                _uiRefreshTimer.Stop();
                _uiRefreshTimer.Dispose();
                _uiRefreshTimer = null;
            }

            // 停止正在进行的任务
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
            }

            // 销毁 UI 引擎 (释放 FlaUI 的 COM 对象)
            if (_uiaEngine != null)
            {
                _uiaEngine.OnScanCompleted -= OnImageScanned; // 解绑事件
                _uiaEngine.Dispose();
                _uiaEngine = null;
            }

            // 清空图片暂存
            if (_msafEngine != null)
            {
                if (_msafEngine.PendingImage != null)
                {
                    _msafEngine.PendingImage.Dispose();
                }
                _msafEngine = null;
            }


            // 清空 RichTextBox，解除对图片的引用
            ConversationRichTextBox.Clear();
            ConversationRichTextBox.Dispose();


            // 强制 GC
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

        // 当扫描工具执行完，底层产生图片时的处理
        private void OnImageScanned(Bitmap drawnBmp, Bitmap originalBmp)
        {
            _mainForm.UpdateVisionImage(drawnBmp); //  MainForm 内部会自己 Clone 拷贝，不用管它

            // 动态读取免打扰 CheckBox
            bool isHideUIA = false;
            var chk = _mainForm.Controls.Find("IsHideUIAoutInChatForm", true).FirstOrDefault() as CheckBox;
            if (chk != null) isHideUIA = chk.Checked;
            chk = null;

            if (!isHideUIA)
            {
                // 单独给 UI 线程克隆一份，让 UI 线程用完后自己 Dispose
                Bitmap uiBmp = new Bitmap(drawnBmp);
                this.BeginInvoke(new Action(() => AppendImageToUI(uiBmp)));
            }

            // 给 MSAF 引擎克隆一份，引擎的 Middleware 注入完成后也会自己 Dispose
            _msafEngine.PendingImage = new Bitmap(drawnBmp);

            // 因为 mainEngine 在 ScanInternal 里 new 出了这两张图，触发事件后却忘了销毁它们。
            // 我们作为事件的接收者，既然各个分支都已经 Copy 完了，必须在这里把源头掐死！
            try { drawnBmp?.Dispose(); } catch { }
            try { originalBmp?.Dispose(); } catch { }
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
                _currentSession = null;
                _currentFileName = ""; // 清空文件名标记
                ConversationRichTextBox.Clear();
            }
            else
            {
                try
                {
                    _currentFileName = comboBox1.SelectedItem.ToString();
                    string filePath = Path.Combine(_sessionDirectory, _currentFileName);

                    // 恢复底层的历史记忆
                    _currentSession = await _msafEngine.RestoreSessionFromMarkdown(filePath);

                    // 恢复界面的可视聊天记录
                    string fileText = File.ReadAllText(filePath);
                    var match = Regex.Match(fileText, @"## Chat History\r?\n([\s\S]*?)\r?\n## Session Data");
                    if (match.Success)
                    {
                        ConversationRichTextBox.Text = match.Groups[1].Value.Trim() + "\n\n";
                    }

                    AppendLog("System", $"成功恢复会话: {_currentFileName}");

                    filePath = null;
                    fileText = null;
                    match = null;
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
            _cts = new CancellationTokenSource();

            AppendLog("User", userInput);
            txtInput.Clear(); // 发送后清空输入框

            try
            {
                // 如果是新会话，创建 Session 并生成文件名
                if (_currentSession == null)
                {
                    _currentSession = await _msafEngine.XiaoYuAgent.CreateSessionAsync();

                    // 提取用户第一句话作为文件名（去掉非法字符和换行，最长15字）
                    string safeInput = string.Join("_", userInput.Split(Path.GetInvalidFileNameChars()));
                    safeInput = safeInput.Replace("\r", "").Replace("\n", "").Replace(" ", "");
                    string title = safeInput.Length > 15 ? safeInput.Substring(0, 15) : safeInput;

                    _currentFileName = $"{title}_{DateTime.Now:yyyyMMdd_HHmmss}.md";

                    safeInput = null;
                    title = null;
                }

                var updates = _msafEngine.XiaoYuAgent.RunStreamingAsync(userInput, _currentSession, cancellationToken: _cts.Token);
                string currentToolCall = "";
                var enumerator = updates.GetAsyncEnumerator(_cts.Token);

                bool isHideUIA = false;
                var chk = _mainForm.Controls.Find("IsHideUIAoutInChatForm", true).FirstOrDefault() as CheckBox;
                if (chk != null) isHideUIA = chk.Checked;

                try
                {
                    while (await enumerator.MoveNextAsync())
                    {
                        var update = enumerator.Current;
                        foreach (var content in update.Contents)
                        {
                            if (content is TextContent textContent && !string.IsNullOrEmpty(textContent.Text))
                            {
                                AppendStreamText(textContent.Text);
                            }
                            else if (content is FunctionCallContent functionCall)
                            {
                                if (_isAiTyping) { AppendStreamText("\n\n"); _isAiTyping = false; }
                                currentToolCall = functionCall.Name;
                                if (!isHideUIA) AppendLog("System", $"🔄 正在调用工具: {functionCall.Name}...");
                            }
                            else if (content is FunctionResultContent functionResult)
                            {
                                if (_isAiTyping) { AppendStreamText("\n\n"); _isAiTyping = false; }
                                if (!isHideUIA) AppendLog("ToolResult", $"[{currentToolCall}] 结果: \n{functionResult.Result}");
                            }
                        }

                        update = null;
                    }
                }
                finally
                {
                    if (enumerator != null) await enumerator.DisposeAsync();
                    if (_isAiTyping) { AppendStreamText("\n\n"); _isAiTyping = false; }
                    updates = null;
                    currentToolCall = null;
                    enumerator = null;
                    chk = null;

                }

                // 后台静默保存，不刷新 ComboBox，防止触发事件清空屏幕
                if (!string.IsNullOrEmpty(_currentFileName))
                {
                    string filePath = Path.Combine(_sessionDirectory, _currentFileName);
                    await _msafEngine.BackupSessionToMarkdown(_currentSession, filePath, ConversationRichTextBox.Text);
                    filePath = null;
                }
            }
            catch (TaskCanceledException)
            {
                AppendLog("System", "任务已被手动终止。");

            }
            catch (Exception ex)
            {
                AppendLog("Error", ex.Message);

            }
            finally
            {
                btnExecute.Enabled = true;
                btnStop.Enabled = false;
                txtInput.Enabled = true;
                toolStripLabel1.Text = "任务执行完毕";
                _cts?.Dispose();
                _cts = null;

                userInput = null;

            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            _cts?.Cancel();
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