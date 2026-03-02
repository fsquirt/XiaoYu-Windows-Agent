using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
//using Windows.Management.Deployment;

namespace XiaoYu_LAM.ToolForm
{
    public partial class Win32APIDesktop : Form
    {
        public Win32APIDesktop()
        {
            InitializeComponent();
        }

        // 1. 创建/打开一个桌面
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateDesktop(string lpszDesktop, IntPtr reserved, IntPtr reserved2, int dwFlags, uint dwDesiredAccess, IntPtr lpsa);

        // 2. 启动进程需要的结构体
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct STARTUPINFO
        {
            public Int32 cb;
            public string lpReserved;
            public string lpDesktop; // 关键：指定桌面名称
            public string lpTitle;
            public Int32 dwX;
            public Int32 dwY;
            public Int32 dwXSize;
            public Int32 dwYSize;
            public Int32 dwXCountChars;
            public Int32 dwYCountChars;
            public Int32 dwFillAttribute;
            public Int32 dwFlags;
            public Int16 wShowWindow;
            public Int16 cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateProcess(
            string lpApplicationName,
            string lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("user32.dll")]
        private static extern bool SwitchDesktop(IntPtr hDesktop);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr OpenDesktop(string lpszDesktop, uint dwFlags, bool fInherit, uint dwDesiredAccess);

        // 常量
        private const uint DESKTOP_CREATEWINDOW = 0x0002;
        private const uint DESKTOP_ENUMERATE = 0x0040;
        private const uint DESKTOP_WRITEOBJECTS = 0x0080;
        private const uint DESKTOP_SWITCHDESKTOP = 0x0100;
        private const uint DESKTOP_CREATEMENU = 0x0004;
        private const uint DESKTOP_HOOKCONTROL = 0x0008;
        private const uint DESKTOP_READOBJECTS = 0x0001;
        private const uint DESKTOP_JOURNALRECORD = 0x0010;
        private const uint DESKTOP_JOURNALPLAYBACK = 0x0020;
        private const uint GENERIC_ALL = 0x10000000;

        string desktopName = "XiaoYuAgentDesktop";


        private void button1_Click(object sender, EventArgs e)
        {
            string exePath = "cmd.exe"; // 你想要启动的程序路径
            
            // 1. 创建桌面 (如果已存在则打开)
            IntPtr hDesktop = CreateDesktop(desktopName, IntPtr.Zero, IntPtr.Zero, 0, GENERIC_ALL, IntPtr.Zero);

            if (hDesktop == IntPtr.Zero)
            {
                throw new Exception("无法创建桌面，请检查权限。");
            }

            // 2. 配置启动参数
            STARTUPINFO si = new STARTUPINFO();
            si.cb = Marshal.SizeOf(si);
            // 关键点：指定 "WinSta0\\桌面名"
            si.lpDesktop = "winsta0\\" + desktopName;

            PROCESS_INFORMATION pi = new PROCESS_INFORMATION();

            // 3. 启动进程
            // 注意：第一个参数是 exe 路径，第二个是命令行参数
            bool success = CreateProcess(null, exePath, IntPtr.Zero, IntPtr.Zero, false, 0, IntPtr.Zero, null, ref si, out pi);

            if (!success)
            {
                throw new Exception("启动进程失败。");
            }

            Console.WriteLine($"进程已在桌面 {desktopName} 启动，PID: {pi.dwProcessId}");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            IntPtr hDesktop = CreateDesktop(desktopName, IntPtr.Zero, IntPtr.Zero, 0, GENERIC_ALL, IntPtr.Zero);
            SwitchDesktop(hDesktop);

        }

        private void button3_Click(object sender, EventArgs e)
        {
            IntPtr hDefaultDesktop = OpenDesktop("Default", 0, false, GENERIC_ALL);

            if (hDefaultDesktop == IntPtr.Zero)
            {
                // 如果失败，很可能是权限问题或者名字不对
                // 尝试用 "WinSta0\\Default" 或者只是 "Default"
                Console.WriteLine("打开 Default 桌面失败！");
                return;
            }

            // 2. 切换过去
            bool result = SwitchDesktop(hDefaultDesktop);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            // 在适当的上下文中执行
            //var packageManager = new PackageManager();
            //var packages = packageManager.FindPackages(); // 获取当前用户安装的所有包

            //foreach (var package in packages)
            //{
            //    try
            //    {
            //        // 获取包名称和显示名称
            //        var name = package.Id.Name;
            //        var path = package.InstalledLocation.Path;

            //        // 输出信息
            //        Console.WriteLine($"App: {name}, Package: {path}");
            //    }
            //    catch { }
            //}

        }
    }
}
