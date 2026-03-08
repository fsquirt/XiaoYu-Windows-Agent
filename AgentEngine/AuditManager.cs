using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace XiaoYu_LAM.AgentEngine
{
    public static class AuditManager
    {
        /// <summary>
        /// 启动 PSR 记录
        /// </summary>
        /// <param name="outputPath">完整的 .zip 文件路径</param>
        public static void StartRecording(string outputPath)
        {
            try
            {
                StopRecording();

                string directory = Path.GetDirectoryName(outputPath);
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

                // 使用完整的系统路径，防止重定向问题
                string psrPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "psr.exe");

                string args = $"/start /output \"{outputPath}\" /sc 1 /gui 0 /maxsc 200";

                ProcessStartInfo psi = new ProcessStartInfo(psrPath, args)
                {
                    UseShellExecute = true,
                    CreateNoWindow = false 
                };

                Process.Start(psi);
                Console.WriteLine($"[Audit] 审计已启动: {Path.GetFileName(outputPath)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Audit] 启动记录失败: {ex.Message}");
            }
        }

        public static void StopRecording()
        {
            try
            {
                string psrPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "psr.exe");

                string args = $"/stop";
                ProcessStartInfo psi = new ProcessStartInfo(psrPath, args)
                {
                    UseShellExecute = true,
                    CreateNoWindow = false
                };

                Process.Start(psi);
                Console.WriteLine("[Audit] 审计指令：停止记录。");
                Thread.Sleep(500); // 给系统释放文件锁的时间
            }
            catch { }
        }
    }
}