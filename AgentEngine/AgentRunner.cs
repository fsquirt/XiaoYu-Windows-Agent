using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System;
using System.Drawing;
using System.IO;
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
                if (CurrentSession == null)
                {
                    CurrentSession = await MsafEngine.XiaoYuAgent.CreateSessionAsync();
                }

                OnLog?.Invoke("System", $"开始执行任务: {userInput}");
                string currentToolCall = "";

                var updates = MsafEngine.XiaoYuAgent.RunStreamingAsync(userInput, CurrentSession, cancellationToken: _cts.Token);
                await foreach (var update in updates)
                {
                    foreach (var content in update.Contents)
                    {
                        if (content is TextContent textContent && !string.IsNullOrEmpty(textContent.Text))
                        {
                            OnStreamText?.Invoke(textContent.Text);
                        }
                        else if (content is FunctionCallContent functionCall)
                        {
                            currentToolCall = functionCall.Name;
                            OnToolCall?.Invoke(currentToolCall);
                        }
                        else if (content is FunctionResultContent functionResult)
                        {
                            OnToolResult?.Invoke(currentToolCall, functionResult.Result?.ToString());
                        }
                    }
                }
                OnLog?.Invoke("System", "任务执行完毕");
            }
            catch (TaskCanceledException)
            {
                OnLog?.Invoke("System", "任务已被手动终止。");
            }
            catch (Exception ex)
            {
                OnLog?.Invoke("Error", ex.Message);
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
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