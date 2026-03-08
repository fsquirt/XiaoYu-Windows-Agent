using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XiaoYu_LAM.UIAEngine;
using System.Collections.Generic;

namespace XiaoYu_LAM.AgentEngine
{
    public class AgentRunner : IDisposable
    {
        // 全局注册表，用于跟踪所有运行中的 AgentRunner 实例，便于集中停止/释放
        private static readonly object _globalLock = new object();
        private static readonly List<AgentRunner> _activeRunners = new List<AgentRunner>();

        public static AgentRunner[] GetActiveRunners()
        {
            lock (_globalLock)
            {
                return _activeRunners.ToArray();
            }
        }

        // 取消所有正在运行的任务，但不立即释放实例
        public static void CancelAll()
        {
            AgentRunner[] arr = GetActiveRunners();
            foreach (var r in arr)
            {
                try { r.CancelTask(); } catch { }
            }
        }

        // 取消并释放所有运行器实例
        public static void TerminateAll()
        {
            AgentRunner[] arr = GetActiveRunners();
            foreach (var r in arr)
            {
                try { r.CancelTask(); } catch { }
            }

            foreach (var r in arr)
            {
                try { r.Dispose(); } catch { }
            }

            lock (_globalLock)
            {
                _activeRunners.Clear();
            }
        }

        public MSAFEngine MsafEngine { get; private set; }
        public mainEngine UiaEngine { get; private set; }
        public AgentSession CurrentSession { get; private set; }

        private CancellationTokenSource _cts;
        private StringBuilder _sessionHistoryLog = new StringBuilder(); // 用于最终反思的日志记录

        // 定义对外暴露的回调事件
        public event Action<string> OnStreamText;
        public event Action<string> OnTextResponse;      // 完整输出
        public event Action<string> OnToolCall;
        public event Action<string, string> OnToolResult;
        public event Action<string, string> OnLog; // role, msg
        public event Action<Bitmap, Bitmap> OnImageScanned;

        public AgentRunner()
        {
            UiaEngine = new mainEngine();
            MsafEngine = new MSAFEngine();

            // 将本实例注册到全局列表，便于外部通过静态方法进行集中管理
            lock (_globalLock)
            {
                _activeRunners.Add(this);
            }

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
            // 用于暂存本次回复的纯文本，最后触发 OnTextResponse
            StringBuilder currentTurnText = new StringBuilder();

            // 开始审计会话
            AuditLogger.StartNewSession(userInput);

            try
            {
                if (CurrentSession == null) CurrentSession = await MsafEngine.XiaoYuAgent.CreateSessionAsync();

                _sessionHistoryLog.AppendLine($"\n【用户指令】: {userInput}");
                string currentToolCall = "";

                // 使用 RunStreamingAsync 保持流式，避免 DeepThink 报错
                var updates = MsafEngine.XiaoYuAgent.RunStreamingAsync(userInput, CurrentSession, cancellationToken: _cts.Token);

                await foreach (var update in updates)
                {
                    foreach (var content in update.Contents)
                    {
                        if (content is TextContent textContent && !string.IsNullOrEmpty(textContent.Text))
                        {
                            OnStreamText?.Invoke(textContent.Text);
                            currentTurnText.Append(textContent.Text);
                            _sessionHistoryLog.Append(textContent.Text);
                        }
                        else if (content is FunctionCallContent functionCall)
                        {
                            currentToolCall = functionCall.Name;
                            OnToolCall?.Invoke(currentToolCall);
                            _sessionHistoryLog.AppendLine($"\n[调用工具] {functionCall.Name}");
                            
                            // 审计日志：只记录非 UIA 操作的工具调用
                            // UIA 操作（点击、输入等）由 InteractionManager 详细记录，避免重复
                            if (!IsUIAOperationTool(functionCall.Name))
                            {
                                AuditLogger.LogToolCall(functionCall.Name, functionCall.Arguments?.ToString());
                            }
                        }
                        else if (content is FunctionResultContent functionResult)
                        {
                            string res = functionResult.Result?.ToString() ?? "";
                            OnToolResult?.Invoke(currentToolCall, res);
                            // 注意：工具结果已在 InteractionManager 中通过 LogUIAOperation 详细记录，此处不再重复
                            if (res.Length > 200) res = res.Substring(0, 200) + "...";
                            _sessionHistoryLog.AppendLine($"[工具结果] {res}");
                        }
                    }
                }

                // 循环结束后，如果攒下了文本，触发一次完整响应事件
                if (currentTurnText.Length > 0)
                {
                    OnTextResponse?.Invoke(currentTurnText.ToString());
                    AuditLogger.LogLLMResponse(currentTurnText.ToString());
                }
            }
            catch (TaskCanceledException) 
            { 
                OnLog?.Invoke("System", "任务已被手动终止。");
                AuditLogger.LogUserIntervention("任务终止", "用户取消了任务执行");
            }
            catch (Exception ex) 
            { 
                OnLog?.Invoke("Error", ex.Message);
                AuditLogger.LogError("AgentRunner", "任务执行异常", ex);
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                
                // 结束审计会话
                AuditLogger.EndSession();
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
        
        // 判断是否为 UIA 操作类型的工具（这些工具的详情由 InteractionManager 记录）
        private static bool IsUIAOperationTool(string toolName)
        {
            return toolName switch
            {
                "PerformAction" => true,
                "MouseClick" => true,
                "DoubleClick" => true,
                "RightClick" => true,
                "SetValue" => true,
                "TypeText" => true,
                "PressKey" => true,
                "Scroll" => true,
                "ScrollWithKeyboard" => true,
                _ => false
            };
        }
        
        public void CancelTask()
        {
            _cts?.Cancel();
            AuditLogger.LogUserIntervention("取消任务", "用户主动取消当前任务");
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

            // 从全局列表中移除自身
            lock (_globalLock)
            {
                if (_activeRunners.Contains(this)) _activeRunners.Remove(this);
            }
        }
    }
}