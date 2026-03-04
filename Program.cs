using System;
using System.Threading;
using System.Windows.Forms;

namespace XiaoYu_LAM
{
    static class Program
    {
        static Mutex _mutex; [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 加载配置
            AgentEngine.ConfigManager.LoadConfig();

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
                    AgentEngine.IPCManager.SendTaskToExistingInstance(taskContent);
                }
                else
                {
                    MessageBox.Show("晓予已经在运行中了！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return; // 直接退出
            }

            // 程序尚未运行，根据配置决定打开哪个窗口
            Form startupForm;
            if (!AgentEngine.ConfigManager.IsConfigValid)
            {
                startupForm = new WelcomeForm();
            }
            else
            {
                startupForm = new MainForm(taskContent); // 将任务传给MainForm
            }

            Application.Run(startupForm);

            // 退出时释放 Mutex
            GC.KeepAlive(_mutex);
        }
    }
}