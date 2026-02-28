using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.ClientModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatRole = Microsoft.Extensions.AI.ChatRole;


namespace XiaoYu_LAM.AgentEngine
{
    internal class MSAFEngine
    {
        public AIAgent XiaoYuAgent { get; private set; }
        private readonly UIAEngine.mainEngine _uiaEngine;

        // 用于触发 UI 更新图片的事件
        public event Action<Bitmap> OnImageCaptured;

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

            //读取MainForm里的API配置
            string modelName = "";
            string apiUrl = "";
            string apiKey = "";
            string protocol = "";

            try
            {
                // 在已打开的窗体中查找 MainForm 的实例
                var mainForm = Application.OpenForms.OfType<MainForm>().FirstOrDefault();
                if (mainForm != null)
                {
                    modelName = mainForm.MODEL_NAME ?? "";
                    apiUrl = mainForm.API_URL ?? "";
                    apiKey = mainForm.API_KEY ?? "";
                    protocol = mainForm.PROTOCOL ?? "";
                }
                else
                {
                    // 退回到通过类型名查找（以防命名空间或加载时机不同）
                    foreach (Form f in Application.OpenForms)
                    {
                        if (f.GetType().Name == "MainForm")
                        {
                            dynamic mf = f;
                            try { modelName = mf.MODEL_NAME ?? ""; } catch { }
                            try { apiUrl = mf.API_URL ?? ""; } catch { }
                            try { apiKey = mf.API_KEY ?? ""; } catch { }
                            try { protocol = mf.PROTOCOL ?? ""; } catch { }
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("读取主窗体配置失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiUrl) || string.IsNullOrEmpty(modelName))
            {
                throw new Exception("API 配置缺失，请先在主窗口配置并保存。");
            }

            // 创建基础 OpenAI Client
            OpenAIClientOptions options = new OpenAIClientOptions() { Endpoint = new Uri(apiUrl) };
            OpenAI.Chat.ChatClient rawClient = new OpenAIClient(new ApiKeyCredential(apiKey), options).GetChatClient(modelName);

            // 将其转为 MSAF 标准的 IChatClient，并挂载图片注入中间件
            IChatClient meaiClient = new ImageInjectingChatClient(rawClient.AsIChatClient(), this);

            // 构建 MSAF AIAgent，并把 uiEngine 里的工具全塞进去
            XiaoYuAgent = meaiClient.AsAIAgent(new ChatClientAgentOptions()
            {
                Name = "XiaoYu",
                ChatOptions = new ChatOptions()
                {
                    Instructions = sysPrompt,
                    Tools = uiEngine.GetTools() // 直接获取原生工具
                }
            });
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

        // 将会话输出到MarkDown文件
        public async Task BackupSessionToMarkdown(AgentSession session, string filePath)
        {
            if (XiaoYuAgent == null) return;
            JsonElement sessionElement = await XiaoYuAgent.SerializeSessionAsync(session);
            string sessionJson = sessionElement.GetRawText();

            string markdownContent = string.Format(
                "# XiaoYu Agent Session\n\n## Session Data\n```json\n{0}\n```\n\n## Metadata\n- Date: {1}",
                sessionJson,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
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

        // 自定义 Client，负责拦截对话并自动注入最新截图
        internal class ImageInjectingChatClient : DelegatingChatClient
        {
            private readonly MSAFEngine _engine;

            public ImageInjectingChatClient(IChatClient innerClient, MSAFEngine engine) : base(innerClient)
            {
                _engine = engine;
            }

            // 核心注入逻辑：检查是否有待发送的图片
            private IList<ChatMessage> InjectImageIfNeeded(IList<ChatMessage> messages)
            {
                if (_engine.PendingImage != null)
                {
                    var msgList = messages.ToList(); // 复制一份以便修改
                    using (MemoryStream ms = new MemoryStream())
                    {
                        // 将 Bitmap 转为 Jpeg 字节流
                        _engine.PendingImage.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                        byte[] imgBytes = ms.ToArray();

                        // 创建一个包含文字和图片的多模态 User 消息
                        var contents = new List<AIContent>
                        {
                                new TextContent("【系统自动注入】这是最新扫描的界面截图，请结合此图的编号与刚才工具返回的列表信息，进行下一步操作："),
                                new DataContent(imgBytes, "image/jpeg")
                        };

                        msgList.Add(new ChatMessage(ChatRole.User, contents));
                    }

                    // 注入完后销毁图片并置空，防止下一轮重复发送
                    _engine.PendingImage.Dispose();
                    _engine.PendingImage = null;

                    return msgList;
                }

                // 如果没有图片，直接返回原始消息列表
                return messages;
            }

        }
    }
}
