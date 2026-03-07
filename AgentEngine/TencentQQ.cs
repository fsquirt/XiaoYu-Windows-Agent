using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms; // 用于检测 Application.OpenForms

namespace XiaoYu_LAM.AgentEngine
{
    public class TencentQQ
    {
        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private bool _isRunning = false;

        // 启动 QQ 监听服务
        public async Task StartAsync()
        {
            if (_isRunning) return;
            _isRunning = true;
            _cts = new CancellationTokenSource();

            string wsUri = $"{ConfigManager.QqBotUrl}:{ConfigManager.QqBotPort}";
            // 如果 url 结尾带 / 且 port 不为空
            if (!ConfigManager.QqBotUrl.StartsWith("ws://") && !ConfigManager.QqBotUrl.StartsWith("wss://"))
            {
                wsUri = "ws://" + wsUri;
            }

            // 启动后台线程保持连接
            _ = Task.Run(() => ConnectionLoop(wsUri, ConfigManager.QqBotToken, _cts.Token));
        }

        // 停止 QQ 监听服务
        public void Stop()
        {
            _isRunning = false;
            _cts?.Cancel();
            try
            {
                if (_ws != null && _ws.State == WebSocketState.Open)
                    _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client Closing", CancellationToken.None).Wait();
            }
            catch { }
            _ws?.Dispose();
            _ws = null;
        }

        private async Task ConnectionLoop(string uri, string token, CancellationToken tokenCt)
        {
            Console.WriteLine($"[QQ] 正在连接到 {uri} ...");

            while (!tokenCt.IsCancellationRequested)
            {
                try
                {
                    using (_ws = new ClientWebSocket())
                    {
                        if (!string.IsNullOrEmpty(token))
                        {
                            _ws.Options.SetRequestHeader("Authorization", $"Bearer {token}");
                        }

                        await _ws.ConnectAsync(new Uri(uri), tokenCt);
                        Console.WriteLine("[QQ] 连接成功！等待指令...");

                        await ReceiveLoop(_ws, tokenCt);
                    }
                }
                catch (Exception ex)
                {
                    if (!tokenCt.IsCancellationRequested)
                    {
                        Console.WriteLine($"[QQ] 连接断开: {ex.Message}，5秒后重试...");
                        await Task.Delay(5000, tokenCt);
                    }
                }
            }
        }

        private async Task ReceiveLoop(ClientWebSocket ws, CancellationToken tokenCt)
        {
            var buffer = new byte[1024 * 1024]; // 1MB Buffer
            while (ws.State == WebSocketState.Open && !tokenCt.IsCancellationRequested)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), tokenCt);
                if (result.MessageType == WebSocketMessageType.Close) break;

                string message = Encoding.UTF8.GetString(buffer, 0, result.Count);

                try
                {
                    using (JsonDocument doc = JsonDocument.Parse(message))
                    {
                        var root = doc.RootElement;

                        // 筛选私聊消息
                        if (root.TryGetProperty("post_type", out var postType) && postType.GetString() == "message" &&
                            root.TryGetProperty("message_type", out var msgType) && msgType.GetString() == "private")
                        {
                            long senderId = root.GetProperty("user_id").GetInt64();

                            // 只允许管理员操作
                            if (senderId != ConfigManager.QqAdminQQ) return;
                            string rawMsg = root.GetProperty("raw_message").GetString();

                            Console.WriteLine($"[QQ收到] {senderId}: {rawMsg}");

                            // 处理指令
                            await HandleCommandAsync(ws, senderId, rawMsg);
                        }
                    }
                }
                catch //(Exception ex)
                {
                    // 忽略 JSON 解析错误
                    //Console.WriteLine($"[QQ] 解析错误: {ex.Message}");
                }
            }
        }

        private async Task HandleCommandAsync(ClientWebSocket ws, long userId, string command)
        {
            // 检查冲突：ChatForm 是否打开
            bool isChatFormOpen = false;
            // 跨线程访问 UI 检查窗体
            if (Application.OpenForms != null)
            {
                foreach (Form form in Application.OpenForms)
                {
                    if (form.Name == "ChatForm" && form.Visible)
                    {
                        isChatFormOpen = true;
                        break;
                    }
                }
            }

            if (isChatFormOpen)
            {
                await SendPrivateMsgAsync(ws, userId, "⚠️ 忙碌中：桌面端 ChatForm 正在运行，请先关闭窗口或等待任务结束。");
                return;
            }

            // 检查冲突：是否有正在运行的 AgentRunner
            var activeRunners = AgentRunner.GetActiveRunners();
            if (activeRunners.Length > 0)
            {
                await SendPrivateMsgAsync(ws, userId, "⚠️ 忙碌中：后台有正在执行的计划任务，请稍后再试。");
                return;
            }

            await SendPrivateMsgAsync(ws, userId, $"✅ 指令已接收：{command}\n晓予正在启动...");

            // 启动 AgentRunner
            _ = Task.Run(async () =>
            {
                using (var runner = new AgentRunner())
                {
                    // 绑定事件转发给 QQ

                    // 工具调用开始
                    runner.OnToolCall += async (toolName) => {
                        await SendPrivateMsgAsync(ws, userId, $"🔧 [调用工具] {toolName}");
                    };

                    // 截图转发
                    runner.OnImageScanned += async (drawnBmp, origBmp) => {
                        string base64 = BitmapToBase64(drawnBmp);
                        // LLOneBot 支持 Base64 图片发送: [CQ:image,file=base64://...]
                        string cqCode = $"[CQ:image,file=base64://{base64}]";
                        await SendPrivateMsgAsync(ws, userId, $"📸 [视觉扫描]\n{cqCode}");
                    };

                    // 错误日志
                    runner.OnLog += async (role, msg) => {
                        if (role == "Error")
                            await SendPrivateMsgAsync(ws, userId, $"❌ [Error] {msg}");
                    };

                    // 最终 AI 回复
                    runner.OnTextResponse += async (text) => {
                        await SendPrivateMsgAsync(ws, userId, $"🤖 [回复]\n{text}");
                    };

                    try
                    {
                        await runner.RunTaskAsync(command);
                        // 任务完成后，触发反思并发送反思结果
                        await runner.SummarizeSessionAsync();
                        await SendPrivateMsgAsync(ws, userId, "🏁 任务流程结束。");
                    }
                    catch (Exception ex)
                    {
                        await SendPrivateMsgAsync(ws, userId, $"💥 严重错误: {ex.Message}");
                    }
                }
            });
        }

        private async Task SendPrivateMsgAsync(ClientWebSocket ws, long userId, string msg)
        {
            if (ws == null || ws.State != WebSocketState.Open) return;

            var payload = new
            {
                action = "send_private_msg",
                @params = new
                {
                    user_id = userId,
                    message = msg
                }
            };

            string json = JsonSerializer.Serialize(payload);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            try
            {
                await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[QQ] 发送失败: {ex.Message}");
            }
        }

        private string BitmapToBase64(Bitmap bmp)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Jpeg);
                byte[] byteImage = ms.ToArray();
                return Convert.ToBase64String(byteImage);
            }
        }
    }
}