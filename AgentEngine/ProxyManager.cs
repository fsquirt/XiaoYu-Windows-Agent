using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.IO;

namespace XiaoYu_LAM.AgentEngine
{
    public static class ProxyManager
    {
        private static readonly object _lock = new object();

        public static bool IsProxyActive { get; private set; } = false;
        public static string OriginalApiUrl { get; private set; } = "";
        public static string ProxiedApiUrl { get; private set; } = "";

        // 引入 Win32 LoadLibrary
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string libname);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string moduleName);

        // 显式预加载 DLL
        private static void PreloadProxyDll()
        {
            // 如果已经加载过，就不再加载
            if (GetModuleHandle("RustTLSProxy.dll") != IntPtr.Zero) return;

            string dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RustTLSProxy.dll");

            if (!File.Exists(dllPath))
            {
                Console.WriteLine($"[ProxyManager] 致命：文件不存在 -> {dllPath}");
                return;
            }

            Console.WriteLine($"[ProxyManager] 尝试显式加载 DLL: {dllPath}");
            IntPtr handle = LoadLibrary(dllPath);

            if (handle == IntPtr.Zero)
            {
                int errorCode = Marshal.GetLastWin32Error();
                Console.WriteLine($"[ProxyManager] LoadLibrary 失败! 错误代码: {errorCode}");

                if (errorCode == 126) Console.WriteLine("  -> 错误 126: 找不到指定的模块 (通常是缺 VC++ 运行库或 UCRT)");
                if (errorCode == 193) Console.WriteLine("  -> 错误 193: 这里的 DLL 不是有效的 Win32 程序 (可能是 32位/64位 不匹配)");
            }
            else
            {
                Console.WriteLine($"[ProxyManager] DLL 预加载成功! 句柄: {handle}");
            }
        }


        // 初始化或更新代理。 如果没配URL就不管；如果URL变了，就新开一个端口代理过去。
        public static void InitializeOrUpdate()
        {
            PreloadProxyDll();
            lock (_lock)
            {
                var osVersion = Environment.OSVersion.Version;
                // 如果是 Win10 及以上，直接退出，不需要代理
                if (osVersion.Major >= 10)
                {
                    IsProxyActive = false;
                    return;
                }

                string currentUrl = ConfigManager.ApiUrl;

                // 如果配置还是空的（比如第一次刚进向导），先不启动
                if (string.IsNullOrEmpty(currentUrl))
                {
                    return;
                }

                // 如果 URL 没变，说明代理已经在正常工作了，无需重复启动
                if (OriginalApiUrl == currentUrl)
                {
                    return;
                }

                // URL 发生了变化（或者刚配好第一次保存），启动新的代理
                Console.WriteLine($"[ProxyManager] 准备为目标 {currentUrl} 启动 Rust TLS 代理...");

                try
                {
                    int freePort = FindFreePort();
                    if (freePort == -1)
                    {
                        Console.WriteLine("[ProxyManager] 错误：找不到可用的本地端口。");
                        return;
                    }

                    // 更新记录的 URL
                    OriginalApiUrl = currentUrl;

                    // 启动 Rust DLL 线程
                    int result = NativeMethods.StartBridge(freePort, OriginalApiUrl);
                    if (result != 0)
                    {
                        Console.WriteLine($"[ProxyManager] 错误：启动 Rust 代理失败，返回码: {result}");
                        return;
                    }

                    // 设置代理激活状态
                    ProxiedApiUrl = $"http://127.0.0.1:{freePort}";
                    IsProxyActive = true;

                    Console.WriteLine($"[ProxyManager] 代理启动成功！已将请求转发至: {ProxiedApiUrl}");
                }
                catch (DllNotFoundException)
                {
                    Console.WriteLine("[ProxyManager] 严重错误：未找到 RustTLSProxy.dll！代理无法启动。");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ProxyManager] 未知异常: {ex.Message}");
                }
            }
        }

        // 临时获取一个代理 URL，专门用于 UI 界面的连接测试。不会修改全局配置。
        public static string GetTemporaryProxyUrl(string targetUrl)
        {
            PreloadProxyDll();
            // 如果是 Win10 及以上，直接返回原 URL，不需要代理
            if (Environment.OSVersion.Version.Major >= 10)
            {
                return targetUrl;
            }

            try
            {
                // 找一个新的空闲端口
                int freePort = FindFreePort();
                if (freePort == -1) return targetUrl;

                // 调用 Rust DLL 开启一个临时的代理线程
                int result = NativeMethods.StartBridge(freePort, targetUrl);
                if (result != 0)
                {
                    Console.WriteLine($"[ProxyManager] 临时代理启动失败，错误码: {result}");
                    return targetUrl;
                }

                Console.WriteLine($"[ProxyManager] 为测试分配临时代理: 127.0.0.1:{freePort} -> {targetUrl}");

                // 返回本地代理地址
                return $"http://127.0.0.1:{freePort}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProxyManager] 临时代理异常: {ex.Message}");
                return targetUrl;
            }
        }

        public static string GetEffectiveApiUrl()
        {
            // 每次获取时，确保状态是最新的
            InitializeOrUpdate();
            return IsProxyActive ? ProxiedApiUrl : ConfigManager.ApiUrl;
        }

        private static int FindFreePort()
        {
            try
            {
                TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                listener.Stop();
                return port;
            }
            catch { return -1; }
        }

        private static class NativeMethods
        {
            [DllImport("RustTLSProxy.dll", CallingConvention = CallingConvention.Cdecl)]
            public static extern int StartBridge(int listen_port, string target_url_ptr);
        }
    }
}