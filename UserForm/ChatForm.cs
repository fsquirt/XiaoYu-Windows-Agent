using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
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
            _msafEngine.CreateAgent(_uiaEngine); // 初始化 MSAF
            LoadSessionList();
        }

        // 当扫描工具执行完，底层产生图片时的处理
        private void OnImageScanned(Bitmap drawnBmp, Bitmap originalBmp)
        {
            // 1. 发给 MainForm UI 显示
            _mainForm.UpdateVisionImage(drawnBmp);
            _mainForm.AppendImageToLog("System", drawnBmp);

            // 2. 存到 MSAF 引擎，等下一轮自动注入给大模型
            _msafEngine.PendingImage = new Bitmap(drawnBmp);
        }

        private void LoadSessionList()
        {
            comboBox1.Items.Clear();
            comboBox1.Items.Add("--- 创建新会话 ---");

            string[] files = Directory.GetFiles(_sessionDirectory, "*.md");
            foreach (var f in files)
            {
                comboBox1.Items.Add(Path.GetFileName(f));
            }
            comboBox1.SelectedIndex = 0;
        }

        // 下拉框选择历史会话
        private async void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox1.SelectedIndex <= 0)
            {
                _currentSession = null; // 准备开新坑
            }
            else
            {
                try
                {
                    string filePath = Path.Combine(_sessionDirectory, comboBox1.SelectedItem.ToString());
                    _currentSession = await _msafEngine.RestoreSessionFromMarkdown(filePath);
                    _mainForm.AppendLog("System", $"成功恢复会话: {comboBox1.SelectedItem}");
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

            _mainForm.AppendLog("User", userInput);

            try
            {
                // 如果没有 session，就创建一个新的
                if (_currentSession == null)
                {
                    _currentSession = await _msafEngine.XiaoYuAgent.CreateSessionAsync();
                }

                // 核心：一句话调用大模型！它会自动帮你循环调用工具！
                var updates = _msafEngine.XiaoYuAgent.RunStreamingAsync(userInput, _currentSession, cancellationToken: _cts.Token);

                string currentToolCall = "";

                var enumerator = updates.GetAsyncEnumerator(_cts.Token);
                try
                {
                    while (await enumerator.MoveNextAsync())
                    {
                        var update = enumerator.Current;
                        foreach (var content in update.Contents)
                        {
                            if (content is TextContent textContent && !string.IsNullOrEmpty(textContent.Text))
                            {
                                _mainForm.AppendLog("AI", textContent.Text);
                            }
                            else if (content is FunctionCallContent functionCall)
                            {
                                currentToolCall = functionCall.Name;
                                _mainForm.AppendLog("System", $"🔄 正在调用工具: {functionCall.Name}...");
                            }
                            else if (content is FunctionResultContent functionResult)
                            {
                                _mainForm.AppendLog("ToolResult", $"[{currentToolCall}] 结果: {functionResult.Result}");
                            }
                        }
                    }
                }
                finally
                {
                    if (enumerator != null)
                    {
                        await enumerator.DisposeAsync();
                    }
                }

                // 一轮大对话跑完，自动备份 Session
                string fileName = $"Session_{DateTime.Now:yyyyMMdd_HHmmss}.md";
                if (comboBox1.SelectedIndex > 0) fileName = comboBox1.SelectedItem.ToString(); // 覆盖旧档

                await _msafEngine.BackupSessionToMarkdown(_currentSession, Path.Combine(_sessionDirectory, fileName));
                if (comboBox1.SelectedIndex <= 0) LoadSessionList(); // 刷新列表
            }
            catch (TaskCanceledException)
            {
                _mainForm.AppendLog("System", "任务已被手动终止。");
            }
            catch (Exception ex)
            {
                _mainForm.AppendLog("Error", ex.Message);
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