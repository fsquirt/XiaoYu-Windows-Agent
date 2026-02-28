using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
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
        }

        private void AppendLog(string role, string message)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => AppendLog(role, message)));
                return;
            }

            // 如果之前 AI 在打字，先换行收尾
            if (_isAiTyping)
            {
                ConversationRichTextBox.AppendText("\n\n");
                _isAiTyping = false;
            }

            string time = DateTime.Now.ToString("HH:mm:ss");
            ConversationRichTextBox.SelectionStart = ConversationRichTextBox.TextLength;
            ConversationRichTextBox.SelectionColor = role == "AI" ? Color.Blue : (role == "System" || role == "ToolResult" ? Color.Gray : Color.Black);
            ConversationRichTextBox.AppendText($"[{time}] <{role}>: {message}\n\n");
            ConversationRichTextBox.ScrollToCaret();
        }

        private void AppendStreamText(string text)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => AppendStreamText(text)));
                return;
            }

            // 新的一轮 AI 说话，先打印个 Header
            if (!_isAiTyping && text != "\n\n")
            {
                string time = DateTime.Now.ToString("HH:mm:ss");
                ConversationRichTextBox.SelectionStart = ConversationRichTextBox.TextLength;
                ConversationRichTextBox.SelectionColor = Color.Blue;
                ConversationRichTextBox.AppendText($"[{time}] <AI>: ");
                _isAiTyping = true;
            }

            ConversationRichTextBox.SelectionStart = ConversationRichTextBox.TextLength;
            ConversationRichTextBox.SelectionColor = Color.Blue;
            ConversationRichTextBox.AppendText(text);
            ConversationRichTextBox.ScrollToCaret();
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

            try
            {
                IDataObject oldData = Clipboard.GetDataObject();
                Clipboard.SetImage(bmp);
                ConversationRichTextBox.SelectionStart = ConversationRichTextBox.TextLength;
                ConversationRichTextBox.Paste();
                if (oldData != null) Clipboard.SetDataObject(oldData);
            }
            catch { }

            ConversationRichTextBox.AppendText("\n\n");
            ConversationRichTextBox.ScrollToCaret();
        }

        // 当扫描工具执行完，底层产生图片时的处理
        private void OnImageScanned(Bitmap drawnBmp, Bitmap originalBmp)
        {
            _mainForm.UpdateVisionImage(drawnBmp); // 给 MainForm 画大图

            // 动态读取免打扰 CheckBox
            bool isHideUIA = false;
            var chk = _mainForm.Controls.Find("IsHideUIAoutInChatForm", true).FirstOrDefault() as CheckBox;
            if (chk != null) isHideUIA = chk.Checked;

            if (!isHideUIA)
            {
                AppendImageToUI(drawnBmp); // 给当前聊天框贴图
            }

            _msafEngine.PendingImage = new Bitmap(drawnBmp);
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
                    }
                }
                finally
                {
                    if (enumerator != null) await enumerator.DisposeAsync();
                    if (_isAiTyping) { AppendStreamText("\n\n"); _isAiTyping = false; }
                }

                // 后台静默保存，不刷新 ComboBox，防止触发事件清空屏幕
                if (!string.IsNullOrEmpty(_currentFileName))
                {
                    string filePath = Path.Combine(_sessionDirectory, _currentFileName);
                    await _msafEngine.BackupSessionToMarkdown(_currentSession, filePath, ConversationRichTextBox.Text);
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
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            _cts?.Cancel();
            btnStop.Enabled = false;
            toolStripLabel1.Text = "任务已终止";
        }
    }
}