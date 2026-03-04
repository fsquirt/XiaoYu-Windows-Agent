using Anthropic;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatRole = Microsoft.Extensions.AI.ChatRole;


namespace XiaoYu_LAM.AgentEngine
{
    public class MSAFEngine
    {

        public AIAgent XiaoYuAgent { get; private set; }

        // 暂存由底层扫描触发的图片，等待 Middleware 注入给 LLM
        public Bitmap PendingImage { get; set; }

        public void CreateAgent(UIAEngine.mainEngine uiEngine)
        {
            //读取系统提示词
            string sysPromptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MarkDown", "SystemPrompt.md");
            string sysPrompt = "";
            if (File.Exists(sysPromptPath))
            {
                sysPrompt = File.ReadAllText(sysPromptPath);
            }
            else
            {
                MessageBox.Show($"严重错误：系统提示词文件丢失！\n程序试图寻找：{sysPromptPath}\n\n请在VS中将该文件的属性设置为'复制到输出目录'。", "配置丢失", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string modelName = ConfigManager.ModelName;
            string apiUrl = ConfigManager.ApiUrl;
            string apiKey = ConfigManager.ApiKey;
            string protocol = ConfigManager.Protocol;
            bool isdeepseek = ConfigManager.IsDeepThinkMode;
            bool useAgentSkills = ConfigManager.EnableSkills;
            string[] skillsFolders = ConfigManager.SkillsFolders;

            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiUrl) || string.IsNullOrEmpty(modelName))
            {
                throw new Exception("API 配置缺失，请先在主窗口配置并保存。");
            }

            // 兼容 openai 和 anthropic
            if (protocol == "OpenAI")
            {
                // 创建基础 OpenAI Client
                OpenAIClientOptions options = new OpenAIClientOptions() { Endpoint = new Uri(apiUrl) };

                if (isdeepseek == true)
                {
                    // 请求拦截器：注入 thinking: { type: enabled}
                    options.AddPolicy(new DoubaoDeepThinkingPolicy(), PipelinePosition.PerCall);
                    // 响应拦截器：拿下流数据，打印 reasoning_content
                    options.AddPolicy(new DoubaoReasoningResponsePolicy(), PipelinePosition.PerCall);
                }

                OpenAI.Chat.ChatClient rawClient = new OpenAIClient(new ApiKeyCredential(apiKey), options).GetChatClient(modelName);
                // 将其转为 MSAF 标准的 IChatClient，并挂载图片注入中间件
                IChatClient meaiClient = new ImageInjectingChatClient(rawClient.AsIChatClient(), this);

#pragma warning disable MAAI001 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
                ChatOptions chatOptions = new ChatOptions();

                if (isdeepseek) // 使用思考
                {
                    Console.WriteLine("使用深度思考模式");
                    chatOptions = new ChatOptions()
                    {
                        Instructions = sysPrompt,
                        Tools = uiEngine.GetTools(), // 直接获取原生工具
                        Reasoning = new ReasoningOptions()
                        {
                            Effort = ReasoningEffort.High,
                            Output = ReasoningOutput.Full
                        },
                    };
                }
                else
                {
                    chatOptions = new ChatOptions()
                    {
                        Instructions = sysPrompt,
                        Tools = uiEngine.GetTools()
                    };
                }

                if ((ConfigManager.EnableSkills) || (ConfigManager.SkillsFolders.Count() > 0)) // 使用技能
                {
                    Console.WriteLine("使用Skills");
                    FileAgentSkillsProvider skillsProvider = new FileAgentSkillsProvider(skillPaths: ConfigManager.SkillsFolders);
                    // 构建 MSAF AIAgent，并把 uiEngine 里的工具全塞进去
                    XiaoYuAgent = meaiClient.AsAIAgent(new ChatClientAgentOptions()
                    {
                        Name = "晓予",
                        ChatOptions = chatOptions,
                        AIContextProviders = new List<AIContextProvider> { skillsProvider }
                    });
                }
                else
                {
                    XiaoYuAgent = meaiClient.AsAIAgent(new ChatClientAgentOptions()
                    {
                        Name = "晓予",
                        ChatOptions = chatOptions
                    });
                }
            }

#pragma warning restore MAAI001 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
            else if (protocol == "Anthropic")
            {
                AnthropicClient rawClient = new AnthropicClient { ApiKey = apiKey, BaseUrl = apiUrl };

                // 第二个雷霆BUG 原Anthropic协议Agnet缺少图片注入 并且原UseFunctionInvocation()底层拦截了工具调用
                IChatClient meaiClient = new ImageInjectingChatClient(rawClient.AsIChatClient(modelName), this);

                XiaoYuAgent = meaiClient.AsAIAgent(new ChatClientAgentOptions()
                {
                    Name = "晓予",
                    ChatOptions = new ChatOptions()
                    {
                        Instructions = sysPrompt,
                        Tools = uiEngine.GetTools() // 直接获取原生工具
                    }
                });
            }
        }

        //中间件拦截对话流，将暂存的图片作为最新的视觉上下文注入给大模型
        private async Task<ChatResponse> ImageInjectingMiddleware(IEnumerable<ChatMessage> messages, ChatOptions options, IChatClient innerChatClient, CancellationToken cancellationToken)
        {
            var msgList = messages.ToList();

            if (PendingImage != null)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    PendingImage.Save(ms, ImageFormat.Jpeg);
                    byte[] imgBytes = ms.ToArray();

                    // 创建一个包含文字和图片的多模态 User 消息
                    var contents = new List<AIContent>
                    {
                        new TextContent("【系统自动注入】这是最新扫描的界面截图，请结合此图的编号与刚才工具返回的列表信息，进行下一步操作："),
                        new DataContent(imgBytes, "image/jpeg")
                    };

                    msgList.Add(new ChatMessage(ChatRole.User, contents));
                }

                PendingImage.Dispose();
                PendingImage = null; // 注入完毕，清空暂存
            }

            // 放行给底层 LLM 请求
            return await innerChatClient.GetResponseAsync(msgList, options, cancellationToken);
        }

        // 将会话输出到MarkDown文件，把人类可读的对话记录也存入 Markdown
        public async Task BackupSessionToMarkdown(AgentSession session, string filePath, string chatHistoryText)
        {
            if (XiaoYuAgent == null) return;
            JsonElement sessionElement = await XiaoYuAgent.SerializeSessionAsync(session);
            string sessionJson = sessionElement.GetRawText();

            string markdownContent = string.Format(
                "# XiaoYu Agent Session\n\n## Metadata\n- Date: {0}\n\n## Chat History\n{1}\n\n## Session Data\n```json\n{2}\n```",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                chatHistoryText,
                sessionJson
            );

            using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                await writer.WriteAsync(markdownContent);
            }
        }

        // 从MarkDown文件恢复会话
        public async Task<AgentSession> RestoreSessionFromMarkdown(string filePath)
        {
            if (XiaoYuAgent == null) throw new Exception("Agent尚未初始化，无法恢复会话");

            string markdownContent;
            using (StreamReader reader = new StreamReader(filePath, Encoding.UTF8))
            {
                markdownContent = await reader.ReadToEndAsync();
            }

            Match match = Regex.Match(markdownContent, @"```json\s*([\s\S]*?)\s*```");
            if (!match.Success) throw new Exception("文件中未找到有效的 Session JSON 数据");

            using (JsonDocument jsonDoc = JsonDocument.Parse(match.Groups[1].Value))
            {
                JsonElement sessionElement = jsonDoc.RootElement.Clone();
                return await XiaoYuAgent.DeserializeSessionAsync(sessionElement);
            }
        }



        /// <summary>
        /// 响应流拦截策略：用于替换原始响应流，以便偷窥数据
        /// </summary>
        internal class DoubaoReasoningResponsePolicy : PipelinePolicy
        {
            public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
            {
                ProcessNext(message, pipeline, currentIndex);
                WrapStream(message);
            }

            public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
            {
                await ProcessNextAsync(message, pipeline, currentIndex).ConfigureAwait(false);
                WrapStream(message);
            }

            private void WrapStream(PipelineMessage message)
            {
                // 只有成功的响应且是流式内容才拦截
                if (message.Response != null && message.Response.ContentStream != null)
                {
                    // 用我们要监听的流替换原始流
                    message.Response.ContentStream = new ReasoningSpyStream(message.Response.ContentStream);
                }
            }
        }

        /// <summary>
        /// 间谍流：在读取数据的同时，用正则提取 reasoning_content 并打印
        /// </summary>
        internal class ReasoningSpyStream : Stream
        {
            private readonly Stream _innerStream;
            // 用于匹配 reasoning_content 的正则，匹配模式： "reasoning_content": "内容" 
            private static readonly Regex _regex = new Regex("\"reasoning_content\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.Compiled);

            public ReasoningSpyStream(Stream inner)
            {
                _innerStream = inner;
            }

            public override bool CanRead => _innerStream.CanRead;
            public override bool CanSeek => _innerStream.CanSeek;
            public override bool CanWrite => _innerStream.CanWrite;
            public override long Length => _innerStream.Length;
            public override long Position { get => _innerStream.Position; set => _innerStream.Position = value; }

            public override void Flush() => _innerStream.Flush();

            public override int Read(byte[] buffer, int offset, int count)
            {
                int bytesRead = _innerStream.Read(buffer, offset, count);
                if (bytesRead > 0) InspectData(buffer, offset, bytesRead);
                return bytesRead;
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                int bytesRead = await _innerStream.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
                if (bytesRead > 0) InspectData(buffer, offset, bytesRead);
                return bytesRead;
            }

            private void InspectData(byte[] buffer, int offset, int count)
            {
                try
                {
                    // 将字节转为字符串，注意：流式数据可能会截断多字节字符（如中文），
                    // 但因为我们只关心 JSON key，这部分通常是 ASCII，风险较低。
                    // 真正严谨的做法需要维护一个解码器状态，但这里为了简单直接转换。

                    // 👆 管那么多干什么能用就行了，我只看看有没有开深度思考
                    string chunk = Encoding.UTF8.GetString(buffer, offset, count);

                    MatchCollection matches = _regex.Matches(chunk);
                    foreach (Match match in matches)
                    {
                        if (match.Success && match.Groups.Count > 1)
                        {
                            string rawReasoning = match.Groups[1].Value;

                            // JSON 转义字符处理（比如 \n 转换为换行，\" 转为 "）
                            // 简单的反转义
                            string unescaped = Regex.Unescape(rawReasoning);

                            // 直接输出到控制台
                            Console.Write(unescaped);

                            // 同时输出到 VS 的调试窗口，防止 Console 没开的时候看不到
                            System.Diagnostics.Debug.Write(unescaped);
                        }
                    }
                }
                catch
                {
                    // 忽略所有解析错误，不要影响主流程
                }
            }

            public override long Seek(long offset, SeekOrigin origin) => _innerStream.Seek(offset, origin);
            public override void SetLength(long value) => _innerStream.SetLength(value);
            public override void Write(byte[] buffer, int offset, int count) => _innerStream.Write(buffer, offset, count);

            protected override void Dispose(bool disposing)
            {
                if (disposing) _innerStream.Dispose();
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// 专门用于拦截 OpenAI 请求并注入 extra_body 参数（如深度思考）的策略
        /// </summary>
        internal class DoubaoDeepThinkingPolicy : PipelinePolicy
        {
            public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
            {
                InjectThinkingParam(message);
                ProcessNext(message, pipeline, currentIndex);
            }

            public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
            {
                InjectThinkingParam(message);
                await ProcessNextAsync(message, pipeline, currentIndex);
            }

            private void InjectThinkingParam(PipelineMessage message)
            {
                // 1. 仅拦截发往 chat/completions 的 POST 请求
                if (message.Request.Method == "POST" &&
                    (message.Request.Uri.AbsolutePath.EndsWith("/chat/completions") || message.Request.Uri.ToString().Contains("/chat/completions")))
                {
                    // 2. 读取原始 JSON Body
                    var content = message.Request.Content;
                    if (content != null)
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            content.WriteTo(ms, default);
                            ms.Position = 0;

                            try
                            {
                                // 3. 解析并修改 JSON
                                var jsonNode = JsonNode.Parse(ms).AsObject();

                                // 注入 thinking 参数 (对应 Python 的 extra_body={"thinking": {"type": "enabled"}})
                                jsonNode["thinking"] = new JsonObject
                                {
                                    ["type"] = "enabled"
                                };

                                // 4. 写回 Request Body
                                var newJson = jsonNode.ToJsonString();
                                message.Request.Content = BinaryContent.Create(BinaryData.FromString(newJson));
                            }
                            catch
                            {
                                // 如果解析失败，保持原样，不影响正常流程
                            }
                        }
                    }
                }
            }
        }

        // 自定义 Client，负责拦截对话并自动注入最新截图
        internal class ImageInjectingChatClient : DelegatingChatClient
        {
            private readonly MSAFEngine _engine;

            public ImageInjectingChatClient(IChatClient innerClient, MSAFEngine engine) : base(innerClient)
            {
                _engine = engine;
            }

            // 我操 第一个雷霆BUG修复 让LLM能正确接收到注入图片后的消息列表 原先LLM成瞎子了 纯靠读取控件文字勉强完成的操作 我操
            public override Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> chatMessages, ChatOptions options = null, CancellationToken cancellationToken = default)
            {
                // 把原消息放进去加工，塞入图片
                var newMessages = InjectImageIfNeeded((IList<ChatMessage>)chatMessages);
                // 把带有图片的新消息发给底层真正的大模型
                return base.GetResponseAsync(newMessages, options, cancellationToken);
            }

            public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> chatMessages, ChatOptions options = null, CancellationToken cancellationToken = default)
            {
                // 把原消息放进去加工，塞入图片
                var newMessages = InjectImageIfNeeded((IList<ChatMessage>)chatMessages);
                // 把带有图片的新消息发给底层真正的大模型
                return base.GetStreamingResponseAsync(newMessages, options, cancellationToken);
            }

            private IList<ChatMessage> InjectImageIfNeeded(IList<ChatMessage> messages)
            {
                var msgList = messages.ToList();

                var mf = Application.OpenForms.OfType<MainForm>().FirstOrDefault();
                bool isDeleteHistory = ConfigManager.IsDeleteHistoryPic;

                if (isDeleteHistory)
                {
                    foreach (var msg in msgList)
                    {
                        if (msg.Role == ChatRole.Tool)
                        {
                            for (int i = 0; i < msg.Contents.Count; i++)
                            {
                                if (msg.Contents[i] is FunctionResultContent fnRes && fnRes.Result is string resStr)
                                {
                                    if (resStr.Contains("以下是识别到的控件信息：") || resStr.Contains("以下是识别到的图片控件信息："))
                                    {
                                        int cutIndex = Math.Max(resStr.IndexOf("以下是识别到的控件信息："), resStr.IndexOf("以下是识别到的图片控件信息："));
                                        if (cutIndex >= 0)
                                        {
                                            string newRes = resStr.Substring(0, cutIndex) + "[...历史UI列表已折叠，以节省 Token...]\n";
                                            msg.Contents[i] = new FunctionResultContent(fnRes.CallId, newRes);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // 注入新图片
                if (_engine.PendingImage != null)
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        _engine.PendingImage.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                        byte[] imgBytes = ms.ToArray();

                        var contents = new List<AIContent>
                        {
                            new TextContent("【系统自动注入】这是最新扫描的界面截图，请结合此图的编号与刚才工具返回的列表信息，进行下一步操作："),
                            new DataContent(imgBytes, "image/jpeg")
                        };
                        msgList.Add(new ChatMessage(ChatRole.User, contents));
                    }

                    _engine.PendingImage.Dispose();
                    _engine.PendingImage = null;
                }

                return msgList;
            }
        }
    }
}
