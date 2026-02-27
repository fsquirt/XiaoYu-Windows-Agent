using OpenAI;
using OpenAI.Chat;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using XiaoYu_LAM.UIAEngine;

namespace XiaoYu_LAM
{
    public partial class ChatForm : Form
    {
        private MainForm _mainForm;
        private mainEngine _uiaEngine;
        private LLMParseEngine _parser;

        private CancellationTokenSource _cts; // 用于终止任务
        private string _markdownFilePath;     // 当前对话保存路径
        private List<ChatMessage> _chatHistory; // 维护大模型上下文记忆

        public ChatForm(MainForm mainForm)
        {
            InitializeComponent();
            _mainForm = mainForm;
            _uiaEngine = new mainEngine();
            _parser = new LLMParseEngine(_uiaEngine);
        }

        public ChatForm()
        {
            InitializeComponent();
        }

        // --- 点击【执行任务】 ---
        private async void btnExecute_Click(object sender, EventArgs e)
        {
            toolStripLabel1.Text = "正在执行任务...";
            toolStripLabel1.Image  = Properties.Resources.WorkingIcon; // 设置加载动画

            string taskPrompt = txtInput.Text.Trim();
            if (string.IsNullOrEmpty(taskPrompt)) return;

            // UI 状态切换
            btnExecute.Enabled = false;
            btnStop.Enabled = true;
            txtInput.Enabled = false;
            _cts = new CancellationTokenSource();

            // 初始化 Markdown 文件
            InitMarkdownFile();

            // 初始化 LLM 客户端和历史记录
            ChatClient client = new ChatClient(
                model: _mainForm.MODEL_NAME,
                credential: new ApiKeyCredential(_mainForm.API_KEY),
                options: new OpenAIClientOptions() { Endpoint = new Uri(_mainForm.API_URL) }
            );

            _chatHistory = new List<ChatMessage>();

            // 读取 System Prompt
            string sysPromptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MarkDown", "SystemPrompt.md");
            string sysPrompt = "";
            if (File.Exists(sysPromptPath))
            {
                sysPrompt = File.ReadAllText(sysPromptPath);
            }
            else
            {
                // 弹窗警告，明确告诉你文件没找到，路径在哪里
                MessageBox.Show($"严重错误：系统提示词文件丢失！\n程序试图寻找：{sysPromptPath}\n\n请在VS中将该文件的属性设置为'复制到输出目录'。", "配置丢失", MessageBoxButtons.OK, MessageBoxIcon.Error);

                // 强制终止，不要让它用错误的身份继续跑
                btnExecute.Enabled = true;
                btnStop.Enabled = false;
                txtInput.Enabled = true;
                return;
            }
            //string sysPrompt = File.Exists(sysPromptPath) ? File.ReadAllText(sysPromptPath) : "你是一个Windows操作助手...";
            _chatHistory.Add(new SystemChatMessage(sysPrompt));

            _mainForm.AppendLog("system",sysPrompt);

            // 添加用户初始任务
            _chatHistory.Add(new UserChatMessage(taskPrompt));
            _mainForm.AppendLog("User", taskPrompt);
            SaveToMarkdown("User", taskPrompt);

            // 启动 Agent 异步循环
            try
            {
                await AgentLoopAsync(client, _cts.Token);
            }
            catch (TaskCanceledException)
            {
                _mainForm.AppendLog("System", "任务已被用户手动终止。");
            }
            catch (Exception ex)
            {
                _mainForm.AppendLog("Error", ex.Message);
            }
            finally
            {
                // 恢复 UI 状态
                btnExecute.Enabled = true;
                btnStop.Enabled = false;
                txtInput.Enabled = true;
                _cts?.Dispose();
                _cts = null;
                toolStripLabel1.Text = "任务执行完毕";
                toolStripLabel1.Image = Properties.Resources.ReadyIcon;
            }
        }

        // --- 点击【终止任务】 ---
        private void btnStop_Click(object sender, EventArgs e)
        {
            _cts?.Cancel();
            btnStop.Enabled = false;
            toolStripLabel1.Text = "任务已终止";
            toolStripLabel1.Image = Properties.Resources.WarnIcon;
        }

        // --- 核心优化：Token 瘦身器 (修剪历史上下文) ---
        private void PruneChatHistory()
        {
            // 找出最后一条 UserChatMessage（因为最新的截图和UI树在最后一条里，我们要保留它）
            int lastUserMsgIndex = -1;
            for (int i = _chatHistory.Count - 1; i >= 0; i--)
            {
                if (_chatHistory[i] is UserChatMessage)
                {
                    lastUserMsgIndex = i;
                    break;
                }
            }

            // 遍历并修改历史记录
            for (int i = 0; i < _chatHistory.Count; i++)
            {
                // 跳过最新的那条用户消息，让 LLM 能看到当下的屏幕
                if (i == lastUserMsgIndex) continue;

                if (_chatHistory[i] is UserChatMessage userMsg)
                {
                    bool modified = false;
                    List<ChatMessageContentPart> newParts = new List<ChatMessageContentPart>();

                    foreach (var part in userMsg.Content)
                    {
                        // 在 OpenAI v2 SDK 中，只有文本内容的 Text 属性不为 null
                        if (part.Text != null)
                        {
                            string text = part.Text;

                            // 暴力截断历史的超长控件列表
                            if (text.Contains("以下是识别到的控件信息：") || text.Contains("以下是识别到的图片控件信息："))
                            {
                                int index1 = text.IndexOf("以下是识别到的控件信息：");
                                int index2 = text.IndexOf("以下是识别到的图片控件信息：");
                                int cutIndex = Math.Max(index1, index2);

                                if (cutIndex >= 0)
                                {
                                    text = text.Substring(0, cutIndex) + "[...历史图片与UI列表已折叠，以节省 Token...]";
                                    modified = true;
                                }
                            }

                            newParts.Add(ChatMessageContentPart.CreateTextPart(text));
                        }
                        else
                        {
                            // 如果 part.Text 是 null，说明它是 ImagePart (历史图片)
                            // 我们直接丢弃它 (不加入 newParts)，实现去图瘦身
                            modified = true;
                        }
                    }

                    // 如果对这条历史消息做了“瘦身”操作，我们就替换掉原来的消息
                    if (modified)
                    {
                        _chatHistory[i] = new UserChatMessage(newParts);
                    }
                }
            }
        }

        // --- 核心：Agent 自动化循环 ---
        private async Task AgentLoopAsync(ChatClient client, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                _mainForm.AppendLog("System", "正在思考...");

                PruneChatHistory();

                // 1. 发送请求给 LLM
                ChatCompletion completion = await client.CompleteChatAsync(_chatHistory, cancellationToken: token);
                string llmReply = completion.Content[0].Text;

                // 记录 LLM 的回复
                _chatHistory.Add(new AssistantChatMessage(llmReply));
                _mainForm.AppendLog("AI", llmReply);
                SaveToMarkdown("AI", llmReply);

                // 如果 LLM 觉得任务完成了，可以自己设定一个关键词退出循环
                if (llmReply.Contains("<TaskComplete />"))
                {
                    _mainForm.AppendLog("System", "任务圆满完成！");
                    break;
                }

                // 2. 将 LLM 回复扔给引擎解析并执行底层 UIA 动作
                _mainForm.AppendLog("System", "正在执行引擎指令...");
                ExecutionResult execResult = _parser.ParseAndExecute(llmReply);

                // 3. 将引擎的执行结果（文字反馈）输出到主界面日志
                _mainForm.AppendLog("Engine", execResult.TextFeedback);


                // 4. 构建下一轮发给 LLM 的反馈信息 (多模态)
                var nextUserMessageParts = new List<ChatMessageContentPart>();
                nextUserMessageParts.Add(ChatMessageContentPart.CreateTextPart(execResult.TextFeedback));

                // 如果执行操作后产生了新的屏幕截图（比如 ScanWindow）
                if (execResult.NewScan != null && execResult.NewScan.DrawnImage != null)
                {
                    // 更新 MainForm 的 PictureBox (给人类看)
                    _mainForm.UpdateVisionImage(execResult.NewScan.DrawnImage);

                    // 将带有红框编号的图片转为 Base64 塞进 LLM 消息 (给 AI 看)
                    using (MemoryStream ms = new MemoryStream())
                    {
                        execResult.NewScan.DrawnImage.Save(ms, ImageFormat.Jpeg);
                        BinaryData imageBytes = BinaryData.FromBytes(ms.ToArray());

                        // SDK 要求提供 mime 类型
                        nextUserMessageParts.Add(ChatMessageContentPart.CreateImagePart(imageBytes, "image/jpeg"));
                    }
                    SaveToMarkdown("System", execResult.TextFeedback + "\n\n*(附带了一张最新屏幕截图)*");
                }
                else
                {
                    SaveToMarkdown("System", execResult.TextFeedback);
                }

                // 将引擎的反馈（文字+可能有的图片）作为用户的名义发给大模型
                _chatHistory.Add(new UserChatMessage(nextUserMessageParts));

                // 防死循环机制：如果 LLM 开始胡言乱语，没触发任何动作
                if (execResult.TextFeedback.Contains("未在你的回复中检测到有效的 XML"))
                {
                    // 可以设定连续几次无效就抛出异常终止，这里先让它继续纠正自己
                }

                try
                {
                    // 既然 UI 已经深拷贝了一份，这里的原始数据就可以扔了
                    // 如果 NewScan 有东西，且 OriginalImage 也有东西，那就销毁它。
                    execResult.NewScan?.OriginalImage?.Dispose();
                    execResult.NewScan?.DrawnImage?.Dispose();
                    // 将引用置空，方便 GC 回收
                    execResult.NewScan = null;
                }
                catch { }

                // 稍微休息一下，防止 API 频率过高
                await Task.Delay(1000, token);
            }
        }

        // --- 辅助：初始化 Markdown 文件 ---
        private void InitMarkdownFile()
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MarkDown", "conversation");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _markdownFilePath = Path.Combine(dir, $"Session_{timestamp}.md");

            File.WriteAllText(_markdownFilePath, $"# 对话记录 {timestamp}\n\n");
        }

        // --- 辅助：保存到 Markdown ---
        private void SaveToMarkdown(string role, string content)
        {
            if (string.IsNullOrEmpty(_markdownFilePath)) return;

            string mdContent = "";
            if (role == "User") mdContent = $"### 🧑 User\n{content}\n\n---\n\n";
            else if (role == "AI") mdContent = $"### 🤖 AI Agent\n```xml\n{content}\n```\n\n---\n\n";
            else if (role == "System") mdContent = $"### ⚙️ Engine Result\n> {content.Replace("\n", "\n> ")}\n\n---\n\n";

            File.AppendAllText(_markdownFilePath, mdContent);
        }

        private void ChatForm_Load(object sender, EventArgs e)
        {
            btnStop.Enabled = false;
        }
    }
}
