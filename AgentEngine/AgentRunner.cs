using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XiaoYu_LAM.UIAEngine;

namespace XiaoYu_LAM.AgentEngine
{
    public class AgentRunner : IDisposable
    {
        public MSAFEngine MsafEngine { get; private set; }
        public mainEngine UiaEngine { get; private set; }
        public AgentSession CurrentSession { get; private set; }

        private CancellationTokenSource _cts;
        private StringBuilder _sessionHistoryLog = new StringBuilder(); // 用于最终反思的日志记录

        // 定义对外暴露的回调事件
        public event Action<string> OnStreamText;
        public event Action<string> OnToolCall;
        public event Action<string, string> OnToolResult;
        public event Action<string, string> OnLog; // role, msg
        public event Action<Bitmap, Bitmap> OnImageScanned;

        public AgentRunner()
        {
            UiaEngine = new mainEngine();
            MsafEngine = new MSAFEngine();

            // 监听截图
            UiaEngine.OnScanCompleted += (drawnBmp, originalBmp) =>
            {
                // 必须将截图暂存给 MSAFEngine，中间件才能拿到图片！
                if (drawnBmp != null)
                {
                    MsafEngine.PendingImage?.Dispose(); // 释放上一次可能残留的
                    MsafEngine.PendingImage = new Bitmap(drawnBmp); // 克隆一份给 MSAFEngine
                }

                OnImageScanned?.Invoke(drawnBmp, originalBmp);
            };

            // 监听底层截图
            UiaEngine.OnScanCompleted += (drawnBmp, originalBmp) => OnImageScanned?.Invoke(drawnBmp, originalBmp);

            // 初始化Agent
            MsafEngine.CreateAgent(UiaEngine);
        }

        public async Task RunTaskAsync(string userInput)
        {
            _cts = new CancellationTokenSource();
            try
            {
                if (CurrentSession == null) CurrentSession = await MsafEngine.XiaoYuAgent.CreateSessionAsync();

                //OnLog?.Invoke("System", $"开始执行任务: {userInput}");
                _sessionHistoryLog.AppendLine($"\n【用户指令】: {userInput}");

                string currentToolCall = "";

                // 使用流式，完美实现一边说一边做
                var updates = MsafEngine.XiaoYuAgent.RunStreamingAsync(userInput, CurrentSession, cancellationToken: _cts.Token);
                await foreach (var update in updates)
                {
                    foreach (var content in update.Contents)
                    {
                        if (content is TextContent textContent && !string.IsNullOrEmpty(textContent.Text))
                        {
                            OnStreamText?.Invoke(textContent.Text);
                            _sessionHistoryLog.Append(textContent.Text); // 记录思考过程
                        }
                        else if (content is FunctionCallContent functionCall)
                        {
                            currentToolCall = functionCall.Name;
                            OnToolCall?.Invoke(currentToolCall);
                            _sessionHistoryLog.AppendLine($"\n[调用工具] {functionCall.Name}");
                        }
                        else if (content is FunctionResultContent functionResult)
                        {
                            string res = functionResult.Result?.ToString() ?? "";
                            OnToolResult?.Invoke(currentToolCall, res);
                            // 截断过长结果，防止总结时超出Token
                            if (res.Length > 200) res = res.Substring(0, 200) + "...";
                            _sessionHistoryLog.AppendLine($"[工具结果] {res}");
                        }
                    }
                }
                //OnLog?.Invoke("System", "当前指令执行完毕。");
            }
            catch (TaskCanceledException) { OnLog?.Invoke("System", "任务已被手动终止。"); }
            catch (Exception ex) { OnLog?.Invoke("Error", ex.Message); }
            finally
            {
                _cts?.Dispose();
                _cts = null;
            }
        }

        // 后台反思方法
        public async Task SummarizeSessionAsync()
        {
            if (_sessionHistoryLog.Length < 20) return; // 没啥操作就不总结了

            Console.WriteLine("\n[System] 会话已结束，正在后台进行经验总结...");

            string prompt = $@"
你是一个经验总结助手。请分析以下刚刚完成的电脑操作历史记录。
如果在操作过程中遇到了错误（例如控件找不到、工具返回异常等）并通过尝试其他方法解决了问题，请提取出系统性的经验教训（例如：在这个软件中点击无效，必须用双击）。
如果一切顺利，没有走弯路，请直接回复“无”。
如果回复不是“无”，请直接用一句话简明扼要地列出经验，不要包含任何废话。

【操作历史】
{_sessionHistoryLog.ToString()}";

            try
            {
                // 【修复报错】：使用干净的 ReflectionChatClient，不带任何流式拦截器
                var reflectionAgent = MsafEngine.ReflectionChatClient.AsAIAgent(name: "Reflector");
                var refRes = await reflectionAgent.RunAsync(prompt);
                string memory = refRes.Text.Trim();

                if (memory != "无" && !memory.Contains("没有发现") && memory.Length > 2)
                {
                    MemoryManager.AddMemory(memory);
                    Console.WriteLine($"\n[Memory] 已提取并保存新经验: {memory}");
                }
                else
                {
                    Console.WriteLine("\n[System] 本次操作顺利，无需新增经验。");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[Error] 经验总结失败: {ex.Message}");
            }
        }
        
        public void CancelTask()
        {
            _cts?.Cancel();
        }

        public async Task RestoreSessionAsync(string filePath)
        {
            CurrentSession = await MsafEngine.RestoreSessionFromMarkdown(filePath);
        }

        public async Task BackupSessionAsync(string filePath, string uiText)
        {
            await MsafEngine.BackupSessionToMarkdown(CurrentSession, filePath, uiText);
        }

        public void Dispose()
        {
            CancelTask();
            UiaEngine?.Dispose();
            MsafEngine?.PendingImage?.Dispose();
        }
    }
}