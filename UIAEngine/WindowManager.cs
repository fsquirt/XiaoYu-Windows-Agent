using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using Microsoft.Agents.AI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace XiaoYu_LAM.UIAEngine
{
    // 专门处理获取窗口、改变窗口状态、运行程序

    internal class WindowManager
    {
        private readonly UiaContext _context;

        public WindowManager(UiaContext context)
        {
            _context = context;
        }

        public string GetRunningWindow()
        {
            List<WindowInfo> windowList = new List<WindowInfo>();
            try
            {
                var desktop = _context.Automation.GetDesktop();
                var cf = _context.Automation.ConditionFactory;

                var condition = new OrCondition(
                    cf.ByControlType(ControlType.Window),
                    cf.ByControlType(ControlType.Pane),
                    cf.ByControlType(ControlType.Custom)
                );

                var potentialWindows = desktop.FindAllChildren(condition);

                foreach (var win in potentialWindows)
                {
                    try
                    {
                        string title = win.Properties.Name.ValueOrDefault;
                        if (string.IsNullOrWhiteSpace(title) || title == "任务切换" || title == "Program Manager") continue;

                        var rect = win.BoundingRectangle;
                        if (rect.Width <= 0 || rect.Height <= 0) continue;
                        if (win.Properties.IsOffscreen.ValueOrDefault) continue;

                        IntPtr hWnd = win.Properties.NativeWindowHandle.ValueOrDefault;
                        if (hWnd == IntPtr.Zero) continue;

                        int pid = win.Properties.ProcessId.ValueOrDefault;
                        string processPath = "未知进程";
                        try
                        {
                            var proc = System.Diagnostics.Process.GetProcessById(pid);
                            processPath = proc.MainModule.FileName;
                        }
                        catch
                        {
                            try { processPath = $"[{System.Diagnostics.Process.GetProcessById(pid).ProcessName}.exe] (无权限获取路径)"; }
                            catch { }
                        }

                        NativeMethods.WINDOWPLACEMENT placement = new NativeMethods.WINDOWPLACEMENT();
                        placement.length = Marshal.SizeOf(placement);
                        NativeMethods.GetWindowPlacement(hWnd, ref placement);

                        string status = "普通";
                        if (placement.showCmd == NativeMethods.SW_SHOWMINIMIZED) status = "最小化";
                        else if (placement.showCmd == NativeMethods.SW_SHOWMAXIMIZED) status = "全屏/最大化";

                        windowList.Add(new WindowInfo
                        {
                            Title = title,
                            PID = pid,
                            ProcessPath = processPath,
                            Status = status,
                            Handle = hWnd
                        });
                    }
                    catch { continue; }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("获取窗口列表失败: " + ex.Message);
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("【执行结果: 获取窗口列表】");
            foreach (var w in windowList)
            {
                sb.AppendLine($"- 句柄: {w.Handle}, 进程: {w.ProcessPath}, 状态: {w.Status}, 标题: {w.Title}");
            }
            return sb.ToString();
        }

        public string GetALLDesktopLnk()
        {
            List<ShortcutInfo> shortcuts = new List<ShortcutInfo>();
            var desktops = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
            };

            foreach (var desktop in desktops.Distinct())
            {
                if (!Directory.Exists(desktop)) continue;

                foreach (string file in Directory.GetFiles(desktop, "*.lnk"))
                {
                    try
                    {
                        NativeMethods.IShellLinkW link = (NativeMethods.IShellLinkW)new NativeMethods.ShellLink();
                        ((NativeMethods.IPersistFile)link).Load(file, 0);

                        StringBuilder pathBuilder = new StringBuilder(260);
                        link.GetPath(pathBuilder, pathBuilder.Capacity, IntPtr.Zero, 0);

                        shortcuts.Add(new ShortcutInfo
                        {
                            Name = Path.GetFileNameWithoutExtension(file),
                            Path = pathBuilder.ToString()
                        });
                    }
                    catch { }
                }
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("【执行结果: 获取桌面快捷方式】");
            foreach (var s in shortcuts)
            {
                sb.AppendLine($"- 名称: {s.Name}, 路径: {s.Path}");
            }
            return sb.ToString();
        }

        public string RestoreWindow(long hWnd) { return ChangeWindowState(hWnd, NativeMethods.SW_RESTORE, "恢复"); }
        public string NormalizeWindow(long hWnd) { return ChangeWindowState(hWnd, NativeMethods.SW_SHOWNORMAL, "普通化"); }
        public string MaximizeWindow(long hWnd) { return ChangeWindowState(hWnd, NativeMethods.SW_SHOWMAXIMIZED, "最大化"); }

        private string ChangeWindowState(long hWnd, int cmd, string actionName)
        {
            IntPtr handle = new IntPtr(hWnd);
            try
            {
                if (handle == IntPtr.Zero) return "错误：无效的窗口句柄。";
                NativeMethods.ShowWindow(handle, cmd);
                return $"已发送{actionName}窗口指令。请使用 ScanWindow 重新获取界面。";
            }
            catch (Exception ex) { return $"{actionName}窗口失败: {ex.Message}"; }
        }

        public string BringWindowToFront(long hWnd)
        {
            IntPtr handle = new IntPtr(hWnd);
            if (handle == IntPtr.Zero) return "错误：无效的句柄。";
            NativeMethods.SetForegroundWindow(handle);
            return "已尝试将窗口移动到最前端。";
        }

        public string RunProgram(string path)
        {
            try
            {
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
                return $"成功发送启动指令，路径：{path}。请使用 GetWindows 检查程序是否已出现。";
            }
            catch (Exception ex) { return $"启动程序失败: {ex.Message}"; }
        }
    }
}