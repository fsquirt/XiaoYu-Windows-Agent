using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using XiaoYu_LAM.AgentEngine;
using XiaoYu_LAM.UserForm;

namespace XiaoYu_LAM
{
    static class Program
    {
        static Mutex _mutex; [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 管理员权限检测 (放在最前面！)
            if (!IsAdministrator())
            {
                var result = MessageBox.Show("晓予未以管理员权限运行，将无法操作管理员身份运行的进程窗口。\n是否以管理员身份重新启动？", "权限不足", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result == DialogResult.Yes)
                {
                    try
                    {
                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = Application.ExecutablePath,
                            Arguments = string.Join(" ", args),
                            UseShellExecute = true,
                            Verb = "runas" // 请求提权
                        };
                        System.Diagnostics.Process.Start(psi);

                        // 启动提权进程后，当前非管理员进程直接光速退出，不往下走
                        Environment.Exit(0);
                        return;
                    }
                    catch
                    {
                        MessageBox.Show("无法以管理员权限重新启动晓予，请确认当前Windows用户权限", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }

            // 加载配置
            ConfigManager.LoadConfig();

            // 检查启动参数 --task
            string taskContent = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--task" && i + 1 < args.Length)
                {
                    taskContent = args[i + 1];
                    break;
                }
            }

            // 单实例检测
            bool createdNew;
            _mutex = new Mutex(true, "XiaoYu_LAM_SingleInstance_Mutex", out createdNew);

            if (!createdNew)
            {
                // 如果程序已经存在
                if (!string.IsNullOrEmpty(taskContent))
                {
                    // 把任务通过IPC发送给存在的实例
                    IPCManager.SendTaskToExistingInstance(taskContent);
                }
                else
                {
                    MessageBox.Show("晓予已经在运行中了！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return; // 直接退出
            }

            // 正常启动流程
            Form startupForm;
            if (!ConfigManager.IsConfigValid)
            {
                // 创建MarkDown\conversation文件夹
                string path = AppDomain.CurrentDomain.BaseDirectory + "MarkDown\\conversation";
                System.IO.Directory.CreateDirectory(path);

                // 实例化向导窗口
                AeroSetupAPIWizard wizard = new AeroSetupAPIWizard();

                // 以对话框模式显示向导 (ShowDialog) 这样代码会暂停在这里，直到向导关闭
                if (wizard.ShowDialog() == DialogResult.OK)
                {
                    // 只有当向导正常完成（返回 OK）时，才启动主窗口
                    startupForm = new MainForm(taskContent);
                }
                else
                {
                    // 向导被取消或关闭，直接退出程序
                    return;
                }
            }
            else
            {
                startupForm = new MainForm(taskContent); // 将任务传给MainForm
            }

            // 如果当前不是管理员，改一下窗体标题作为提示
            if (!IsAdministrator())
            {
                startupForm.Text += "（非管理员权限）";
            }

            Application.Run(startupForm);

            // 退出时释放 Mutex
            GC.KeepAlive(_mutex);
        }

        // 检查管理员权限的辅助方法
        private static bool IsAdministrator()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }
    }
}