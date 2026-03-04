using System;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;

namespace XiaoYu_LAM.AgentEngine
{
    public static class IPCManager
    {
        private const string PipeName = "XiaoYu_LAM_Pipe";

        // 发送任务给已存在的实例
        public static void SendTaskToExistingInstance(string task)
        {
            try
            {
                using (var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                {
                    client.Connect(2000); // 2秒超时
                    byte[] bytes = Encoding.UTF8.GetBytes(task);
                    client.Write(bytes, 0, bytes.Length);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"IPC发送失败: {ex.Message}");
            }
        }

        // 在主实例中启动监听服务器
        public static void StartServer(Action<string> onTaskReceived)
        {
            Task.Run(() =>
            {
                while (true)
                {
                    try
                    {
                        using (var server = new NamedPipeServerStream(PipeName, PipeDirection.In))
                        {
                            server.WaitForConnection();
                            byte[] buffer = new byte[4096];
                            int bytesRead = server.Read(buffer, 0, buffer.Length);
                            string task = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            onTaskReceived?.Invoke(task);
                        }
                    }
                    catch
                    {
                        // 忽略异常，继续循环监听
                    }
                }
            });
        }
    }
}