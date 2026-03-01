using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace XiaoYu_LAM.UIAEngine
{
    // 定义返回的窗口对象结构
    public class WindowInfo
    {
        public string Title { get; set; }
        public int PID { get; set; }
        public string ProcessPath { get; set; } // 进程的绝对路径
        public string Status { get; set; } // 最小化/普通/全屏
        public IntPtr Handle { get; set; } // 内部操作句柄
    }

    public class ShortcutInfo
    {
        public string Name { get; set; }
        public string Path { get; set; }
    }


    public class mainEngine : IDisposable
    {
        private readonly UIA3Automation _automation;
        // 核心状态：缓存最近一次扫描的控件，ID -> 控件实体
        private Dictionary<int, AutomationElement> _lastScanElements;

        // 当扫描/截图产生时触发，负责把图片抛给 MSAF 和 UI，不再通过 return 返回
        public event Action<Bitmap, Bitmap> OnScanCompleted;

        public mainEngine()
        {
            _automation = new UIA3Automation();
            _lastScanElements = new Dictionary<int, AutomationElement>();
        }

        public void Dispose()
        {
            _lastScanElements.Clear();
            _lastScanElements = null;

            _automation?.Dispose();
        }

        #region Win32 API 声明
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcDrawing, uint nFlags);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
        internal class ShellLink { }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
        internal interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out ushort pwHotkey);
            void SetHotkey(ushort wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchMaxPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
            void Resolve(IntPtr hwnd, uint fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("0000010b-0000-0000-C000-000000000046")]
        internal interface IPersistFile
        {
            void GetClassID(out Guid pClassID);
            void IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        }

        private struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public System.Drawing.Point ptMinPosition;
            public System.Drawing.Point ptMaxPosition;
            public System.Drawing.Rectangle rcNormalPosition;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        private const int SW_SHOWNORMAL = 1;
        private const int SW_SHOWMINIMIZED = 2;
        private const int SW_SHOWMAXIMIZED = 3;
        private const int SW_RESTORE = 9;
        #endregion

        //导出所有 MSAF 工具
        public List<AITool> GetTools()
        {
            // 将所有方法包装为原生的 AITool，并强制指定 Name 以精确对应 Prompt 里的标签名
            // 在 .NET Framework 4.8 中，必须显式声明 Func 委托类型
            return new List<AITool>
            {
                AIFunctionFactory.Create(new Func<string>(this.GetRunningWindow), name: "GetWindows"),
                AIFunctionFactory.Create(new Func<string>(this.GetALLDesktopLnk), name: "GetShortcuts"),
                AIFunctionFactory.Create(new Func<string>(this.GetFullScreen), name: "GetFullScreen"),
                AIFunctionFactory.Create(new Func<long, string>(this.ScanWindow), name: "ScanWindow"),
                AIFunctionFactory.Create(new Func<long, string>(this.ScanImageControls), name: "ScanImageControls"),
                AIFunctionFactory.Create(new Func<long, string>(this.ScanContainerControls), name: "ScanContainerControls"),
                AIFunctionFactory.Create(new Func<long, string>(this.RestoreWindow), name: "RestoreWindow"),
                AIFunctionFactory.Create(new Func<long, string>(this.MaximizeWindow), name: "MaximizeWindow"),
                AIFunctionFactory.Create(new Func<long, string>(this.NormalizeWindow), name: "NormalizeWindow"),
                AIFunctionFactory.Create(new Func<string, string>(this.RunProgram), name: "RunProgram"),
                AIFunctionFactory.Create(new Func<int, string>(this.PerformAction), name: "PerformAction"),
                AIFunctionFactory.Create(new Func<int, string>(this.PerformMouseClick), name: "MouseClick"),
                AIFunctionFactory.Create(new Func<int, string>(this.DoubleClick), name: "DoubleClick"),
                AIFunctionFactory.Create(new Func<int, string>(this.RightClick), name: "RightClick"),
                AIFunctionFactory.Create(new Func<int, string, string>(this.SetValue), name: "SetValue"),
                AIFunctionFactory.Create(new Func<int, string, string>(this.TypeText), name: "TypeText"),
                AIFunctionFactory.Create(new Func<string, string>(this.PressKey), name: "PressKey")
                //AIFunctionFactory.Create(new Func<int, string, string>(this.Scroll), name: "Scroll")
                //AIFunctionFactory.Create(new Func<long, string>(this.BringWindowToFront), name: "BringWindowToFront")
            };
        }

        [Description("将指定窗口移动到屏幕最前端并激活。如果你怀疑窗口被遮挡导致扫描不全，请调用此工具。")]
        public string BringWindowToFront([Description("窗口的纯数字句柄")] long hWnd)
        {
            IntPtr handle = new IntPtr(hWnd);
            if (handle == IntPtr.Zero) return "错误：无效的句柄。";

            SetForegroundWindow(handle);
            return "已尝试将窗口移动到最前端。";
        }

        [Description("获取桌面所有快捷方式。返回桌面上所有的快捷方式名称和路径。如果目标软件没打开，用这个找路径。")]
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
                        IShellLinkW link = (IShellLinkW)new ShellLink();
                        ((IPersistFile)link).Load(file, 0);

                        StringBuilder pathBuilder = new StringBuilder(260);
                        link.GetPath(pathBuilder, pathBuilder.Capacity, IntPtr.Zero, 0);

                        shortcuts.Add(new ShortcutInfo
                        {
                            Name = Path.GetFileNameWithoutExtension(file),
                            Path = pathBuilder.ToString()
                        });
                    }
                    catch
                    {
                        // 跳过损坏或无权限
                    }
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

        [Description("获取当前所有运行的窗口列表。返回包含句柄(Handle)、状态和标题的文本。这是寻找目标程序 Handle 的第一步。")]
        public string GetRunningWindow()
        {
            List<WindowInfo> windowList = new List<WindowInfo>();
            try
            {
                var desktop = _automation.GetDesktop();
                var cf = _automation.ConditionFactory;

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

                        // 过滤无名窗口、特定系统窗口
                        if (string.IsNullOrWhiteSpace(title)) continue;
                        //if (title == "任务切换" || title == "Program Manager" || title == "任务栏") continue;
                        if (title == "任务切换" || title == "Program Manager") continue;

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

                        // 获取窗口状态
                        WINDOWPLACEMENT placement = new WINDOWPLACEMENT();
                        placement.length = Marshal.SizeOf(placement);
                        GetWindowPlacement(hWnd, ref placement);

                        string status = "普通";
                        if (placement.showCmd == SW_SHOWMINIMIZED) status = "最小化";
                        else if (placement.showCmd == SW_SHOWMAXIMIZED) status = "全屏/最大化";

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

        [Description("恢复最小化的窗口。极度重要：若 GetWindows 显示窗口状态为最小化，扫描前必须执行此指令！")]
        public string RestoreWindow([Description("窗口的纯数字句柄")] long hWnd)
        {
            IntPtr handle = new IntPtr(hWnd);
            try
            {
                if (handle == IntPtr.Zero) return "错误：无效的窗口句柄。";
                ShowWindow(handle, SW_RESTORE);
                return "已发送恢复窗口指令，窗口应该已回到屏幕上。请使用 ScanWindow 重新获取界面。";
            }
            catch (Exception ex) { return $"恢复窗口失败: {ex.Message}"; }
        }

        [Description("将最大化的窗口恢复为普通大小。")]
        public string NormalizeWindow([Description("窗口的纯数字句柄")] long hWnd)
        {
            IntPtr handle = new IntPtr(hWnd);
            try
            {
                if (handle == IntPtr.Zero) return "错误：无效的窗口句柄。";
                ShowWindow(handle, SW_SHOWNORMAL);
                return "已发送普通窗口化指令。请使用 ScanWindow 重新获取界面。";
            }
            catch (Exception ex) { return $"普通窗口化失败: {ex.Message}"; }
        }

        [Description("最大化窗口。当发现窗口太小，UI 元素重叠难以看清时使用。")]
        public string MaximizeWindow([Description("窗口的纯数字句柄")] long hWnd)
        {
            IntPtr handle = new IntPtr(hWnd);
            try
            {
                if (handle == IntPtr.Zero) return "错误：无效的窗口句柄。";
                ShowWindow(handle, SW_SHOWMAXIMIZED);
                return "已发送最大化窗口指令。请使用 ScanWindow 重新获取界面。";
            }
            catch (Exception ex) { return $"最大化窗口失败: {ex.Message}"; }
        }

        [Description("截取整个电脑桌面的全屏画面。当你迷失方向，或者找不到特定窗口时使用。")]
        public string GetFullScreen()
        {
            try
            {
                var originalImage = FlaUI.Core.Capturing.Capture.MainScreen().Bitmap;
                OnScanCompleted?.Invoke(originalImage, originalImage);
                return "【执行结果: 全屏截图完毕】\n全屏截图已提供，请查看画面。";
            }
            catch (Exception ex) { return $"截取全屏失败: {ex.Message}"; }
        }


        #region LLM 核心接口：扫描 (Scan)


        [Description("常规扫描窗口，获取带有编号红框的控件截图。必须提供纯数字句柄。")]
        public string ScanWindow([Description("窗口的纯数字句柄")] long hWnd)
        {
            IntPtr handle = new IntPtr(hWnd);
            var cf = _automation.ConditionFactory;
            var typeCondition = new OrCondition(
                cf.ByControlType(ControlType.Button),
                cf.ByControlType(ControlType.Edit),
                cf.ByControlType(ControlType.ComboBox),
                cf.ByControlType(ControlType.ListItem),
                cf.ByControlType(ControlType.MenuItem),
                cf.ByControlType(ControlType.TabItem),
                cf.ByControlType(ControlType.Hyperlink),
                cf.ByControlType(ControlType.CheckBox),
                cf.ByControlType(ControlType.TreeItem),
                cf.ByControlType(ControlType.Text),
                cf.ByControlType(ControlType.RadioButton)
            );

            return ScanInternal(handle, typeCondition, false, "窗口常规控件");
        }

        [Description("单独扫描纯图片控件。当常规扫描漏掉了某些看起来像按钮的图标时使用。")]
        public string ScanImageControls([Description("窗口的纯数字句柄")] long hWnd)
        {
            IntPtr handle = new IntPtr(hWnd);
            var cf = _automation.ConditionFactory;
            return ScanInternal(handle, cf.ByControlType(ControlType.Image), true, "窗口图片控件");
        }


        private string ScanInternal(IntPtr hWnd, ConditionBase condition, bool isImageOnly, string scanType)
        {
            _lastScanElements.Clear();
            try
            {
                var targetWindow = _automation.FromHandle(hWnd);
                if (targetWindow == null) return "错误：无法获取窗口 UIA 节点，可能句柄已失效。";

                // 自动将窗口移动到最前端 
                SetForegroundWindow(hWnd);
                System.Threading.Thread.Sleep(1000); // 给桌面一点渲染时间

                Bitmap originalBmp = CaptureWindowByHandle(hWnd);
                if (originalBmp == null) originalBmp = new Bitmap(targetWindow.Capture());
                Bitmap drawnBmp = new Bitmap(originalBmp);

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"【执行结果: {scanType}扫描完毕】");
                sb.AppendLine("已提供包含编号的标记图。以下是识别到的控件信息：");

                using (Graphics g = Graphics.FromImage(drawnBmp))
                {
                    Pen redPen = new Pen(Color.Red, 2);
                    Font font = new Font("Arial", 9, FontStyle.Bold);
                    SolidBrush textBrush = new SolidBrush(Color.White);
                    SolidBrush bgBrush = new SolidBrush(Color.Blue);

                    var rawElements = targetWindow.FindAll(TreeScope.Descendants, condition);

                    // 传入 scanType，以便进行不同的过滤逻辑
                    var optimizedElements = OptimizeElements(rawElements, isImageOnly, scanType);

                    int index = 1;
                    var winRect = targetWindow.BoundingRectangle;

                    foreach (var elData in optimizedElements)
                    {
                        var rect = elData.Rect;
                        int relativeX = Math.Max(0, (int)(rect.Left - winRect.Left));
                        int relativeY = Math.Max(0, (int)(rect.Top - winRect.Top));

                        g.DrawRectangle(redPen, relativeX, relativeY, (int)rect.Width, (int)rect.Height);
                        string idText = index.ToString();
                        g.FillRectangle(bgBrush, relativeX, relativeY, idText.Length * 10 + 5, 14);
                        g.DrawString(idText, font, textBrush, relativeX, relativeY - 1);

                        _lastScanElements[index] = elData.Element;
                        string controlName = elData.Element.Properties.Name.ValueOrDefault;
                        if (string.IsNullOrWhiteSpace(controlName)) controlName = "<无名>";

                        sb.AppendLine($"ID: {index} ->[{elData.Type}] {controlName}");
                        index++;
                    }
                }

                OnScanCompleted?.Invoke(drawnBmp, originalBmp);
                return sb.ToString();
            }
            catch
            {
                return "错误：无法获取窗口 UIA 节点，可能句柄已失效。";
            }
        }


        [Description("单独扫描容器控件（列表、表格、树），用于寻找大区块以便滚动")]
        public string ScanContainerControls([Description("窗口的纯数字句柄")] long hWnd)
        {
            IntPtr handle = new IntPtr(hWnd);
            var cf = _automation.ConditionFactory;
            var typeCondition = new OrCondition(
                cf.ByControlType(ControlType.List), cf.ByControlType(ControlType.Pane),
                cf.ByControlType(ControlType.DataGrid), cf.ByControlType(ControlType.Tree),
                cf.ByControlType(ControlType.Table), cf.ByControlType(ControlType.Group)
            );
            return ScanInternal(handle, typeCondition, true, "容器控件");
        }

        #endregion

        #region LLM 核心接口：交互 (Interact)

        [Description("后台代码级交互（优先使用，用于左键点击、选中、展开，速度快不抢鼠标）。")]
        public string PerformAction([Description("要操作的控件纯数字ID")] int id)
        {
            if (!_lastScanElements.TryGetValue(id, out var element)) return $"错误：未找到 ID 为 {id} 的控件，请重新扫描。";
            try
            {
                if (element.Patterns.Invoke.IsSupported) { element.Patterns.Invoke.Pattern.Invoke(); return "Invoke (标准点击) 成功"; }
                if (element.Patterns.Toggle.IsSupported) { element.Patterns.Toggle.Pattern.Toggle(); return "Toggle (切换) 成功"; }
                if (element.Patterns.SelectionItem.IsSupported) { element.Patterns.SelectionItem.Pattern.Select(); return "Select (选中) 成功"; }
                if (element.Patterns.ExpandCollapse.IsSupported)
                {
                    var state = element.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value;
                    if (state == ExpandCollapseState.Expanded) element.Patterns.ExpandCollapse.Pattern.Collapse();
                    else element.Patterns.ExpandCollapse.Pattern.Expand();
                    return "Expand/Collapse 成功";
                }
                if (element.Patterns.LegacyIAccessible.IsSupported)
                {
                    var legacyPattern = element.Patterns.LegacyIAccessible.Pattern;
                    string defaultAction = legacyPattern.DefaultAction.Value;
                    if (!string.IsNullOrEmpty(defaultAction))
                    {
                        legacyPattern.DoDefaultAction();
                        return $"LegacyIAccessible (执行动作: '{defaultAction}') 成功";
                    }
                }
                return "该控件不支持代码级交互，建议使用 MouseClick。";
            }
            catch (Exception ex) { return $"交互抛出异常: {ex.Message}"; }
        }

        [Description("前台物理鼠标左键双击。打开文件夹、打开文件时，通常需要双击！")]
        public string DoubleClick([Description("要操作的控件纯数字ID")] int id)
        {
            if (!_lastScanElements.TryGetValue(id, out var element)) return $"错误：未找到 ID {id}。";
            try
            {
                if (element.TryGetClickablePoint(out var point))
                {
                    IntPtr hwnd = GetHwndFromElement(element);
                    if (hwnd != IntPtr.Zero) { SetForegroundWindow(hwnd); System.Threading.Thread.Sleep(100); }
                    FlaUI.Core.Input.Mouse.DoubleClick(point);
                    return "真实鼠标双击发送成功。";
                }
                return "错误：无法获取可点击坐标。";
            }
            catch (Exception ex) { return $"双击异常: {ex.Message}"; }
        }

        [Description("前台物理鼠标左键点击（备用方案：当 PerformAction 反馈执行成功但界面没反应时使用）。")]
        public string PerformMouseClick([Description("要操作的控件纯数字ID")] int id)
        {
            if (!_lastScanElements.TryGetValue(id, out var element)) return $"错误：未找到 ID {id}。";
            try
            {
                if (element.TryGetClickablePoint(out var point))
                {
                    IntPtr hwnd = GetHwndFromElement(element);
                    if (hwnd != IntPtr.Zero) { SetForegroundWindow(hwnd); System.Threading.Thread.Sleep(100); }
                    FlaUI.Core.Input.Mouse.Click(point);
                    return "真实鼠标点击发送成功。";
                }
                return "错误：该控件不可见或无法获取可点击坐标。";
            }
            catch (Exception ex) { return $"物理点击异常: {ex.Message}"; }
        }

        [Description("启动指定路径的程序（通过快捷方式或exe绝对路径）。")]
        public string RunProgram([Description("程序的完整路径")] string path)
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

        [Description("后台代码级写入文本（优先尝试的文本输入方式，瞬间完成）。")]
        public string SetValue([Description("要输入文本的控件ID")] int id, [Description("要输入的文字")] string text)
        {
            if (!_lastScanElements.TryGetValue(id, out var element)) return $"错误：未找到 ID {id}。";
            try
            {
                if (element.Patterns.Value.IsSupported && !element.Patterns.Value.Pattern.IsReadOnly.Value)
                {
                    element.Patterns.Value.Pattern.SetValue(text);
                    return "SetValue (后台代码赋值) 成功。";
                }
                return "该控件不支持 ValuePattern 后台赋值，请尝试使用 TypeText。";
            }
            catch (Exception ex) { return $"SetValue 异常: {ex.Message}"; }
        }

        [Description("前台物理模拟打字（当 SetValue 失败或不支持时使用，会先强制点击聚焦，全选删除旧内容，再敲击新内容）。")]
        public string TypeText([Description("要输入文本的控件ID")] int id, [Description("要输入的文字")] string text)
        {
            if (!_lastScanElements.TryGetValue(id, out var element)) return $"错误：未找到 ID {id}。";
            try
            {
                // 点击聚焦
                string clickRes = PerformMouseClick(id);
                if (clickRes.Contains("错误")) return $"TypeText 失败，无法聚焦: {clickRes}";

                System.Threading.Thread.Sleep(200); 

                // Pressing 会按下按键，using 结束时会自动释放按键
                using (FlaUI.Core.Input.Keyboard.Pressing(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL))
                {
                    FlaUI.Core.Input.Keyboard.Type(FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_A);
                }

                System.Threading.Thread.Sleep(100); // 等待选中

                // 模拟 Backspace 删除内容
                FlaUI.Core.Input.Keyboard.Type(FlaUI.Core.WindowsAPI.VirtualKeyShort.BACK);
                System.Threading.Thread.Sleep(100); // 等待内容被清空

                FlaUI.Core.Input.Keyboard.Type(text);
                return "TypeText (物理点击 + 全选删除 + 键盘模拟) 输入成功。";
            }
            catch (Exception ex) { return $"TypeText 异常: {ex.Message}"; }
        }

        [Description("物理鼠标右键点击（用于呼出右键菜单）。")]
        public string RightClick([Description("要操作的控件纯数字ID")] int id)
        {
            if (!_lastScanElements.TryGetValue(id, out var element)) return $"错误：未找到 ID {id}。";
            try
            {
                if (element.TryGetClickablePoint(out var point))
                {
                    IntPtr hwnd = GetHwndFromElement(element);
                    if (hwnd != IntPtr.Zero) { SetForegroundWindow(hwnd); System.Threading.Thread.Sleep(100); }
                    FlaUI.Core.Input.Mouse.RightClick(point);
                    return "真实鼠标右键点击成功。";
                }
                return "错误：无法获取可点击坐标。";
            }
            catch (Exception ex) { return $"右键点击异常: {ex.Message}"; }
        }

        [Description("对指定的容器区块进行物理滚轮翻页。必须提供 direction 参数。")]
        public string Scroll([Description("要滚动的容器控件ID")] int id, [Description("滚动方向，只能为 'down' 或 'up'")] string direction)
        {
            if (!_lastScanElements.TryGetValue(id, out var element)) return $"错误：未找到 ID {id}。";
            try
            {
                var rect = element.BoundingRectangle;
                if (rect.IsEmpty) return "错误：控件没有有效的边界。";

                int centerX = (int)(rect.Left + rect.Width / 2);
                int centerY = (int)(rect.Top + rect.Height / 2);
                try { element.Focus(); } catch { }

                FlaUI.Core.Input.Mouse.Position = new System.Drawing.Point(centerX, centerY);
                System.Threading.Thread.Sleep(50);

                double scrollAmount = direction.ToLower() == "down" ? -3.0 : 3.0;
                FlaUI.Core.Input.Mouse.Scroll(scrollAmount);

                return $"已在目标区域中心点 ({centerX},{centerY}) 向 {direction} 物理滚动。请重新扫描检查。";
            }
            catch (Exception ex) { return $"滚动异常: {ex.Message}"; }
        }

        [Description("模拟按下键盘按键。支持: Enter, Esc, Tab, Space, Back, Delete 等。")]
        public string PressKey([Description("按键名称")] string keyName)
        {
            try
            {
                if (Enum.TryParse(keyName, true, out FlaUI.Core.WindowsAPI.VirtualKeyShort vKey))
                {
                    FlaUI.Core.Input.Keyboard.Press(vKey);
                    return $"已成功按下按键: {keyName}";
                }
                return $"错误：不支持的按键名称 '{keyName}'。常见支持：Enter, Esc, Tab, Space, Back, Delete";
            }
            catch (Exception ex) { return $"按键异常: {ex.Message}"; }
        }

        #endregion

        #region 内部工具与算法 (Capture, Optimize, HWND)

        private Bitmap CaptureWindowByHandle(IntPtr handle)
        {
            try
            {
                GetWindowRect(handle, out RECT rect);
                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;
                if (width <= 0 || height <= 0) return null;

                Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    IntPtr hdc = g.GetHdc();
                    try
                    {
                        bool success = PrintWindow(handle, hdc, 2);
                        if (!success) success = PrintWindow(handle, hdc, 0);
                        if (!success) { g.ReleaseHdc(hdc); return null; }
                    }
                    finally { g.ReleaseHdc(hdc); }
                }
                return bmp;
            }
            catch { return null; }
        }

        private IntPtr GetHwndFromElement(AutomationElement element)
        {
            var currentNode = element;
            while (currentNode != null)
            {
                IntPtr hwnd = currentNode.Properties.NativeWindowHandle.ValueOrDefault;
                if (hwnd != IntPtr.Zero) return hwnd;
                currentNode = currentNode.Parent;
            }
            return IntPtr.Zero;
        }

        class ElementData
        {
            public AutomationElement Element { get; set; }
            public System.Windows.Rect Rect { get; set; }
            public double Area { get; set; }
            public ControlType Type { get; set; }
            public bool IsContainer { get; set; }
            public bool ShouldDraw { get; set; } = true;
        }

        private List<ElementData> OptimizeElements(AutomationElement[] elements, bool isImageOnly, string scanType)
        {
            var list = new List<ElementData>();
            bool isContainerScan = scanType == "容器控件";

            foreach (var el in elements)
            {
                try
                {
                    var rect = el.BoundingRectangle;
                    if (rect.Width <= 0 || rect.Height <= 0) continue;

                    if (el.Properties.IsOffscreen.ValueOrDefault) continue;

                    var type = el.ControlType;
                    bool isContainer = (type == ControlType.Pane || type == ControlType.Group ||
                                        type == ControlType.Custom || type == ControlType.DataGrid ||
                                        type == ControlType.Window || type == ControlType.Document);

                    bool isClickable = false;
                    try
                    {
                        if (el.Patterns.Invoke.IsSupported || el.Patterns.Toggle.IsSupported ||
                            el.Patterns.SelectionItem.IsSupported || el.Patterns.ExpandCollapse.IsSupported ||
                            el.Patterns.Value.IsSupported)
                        {
                            isClickable = true;
                        }
                    }
                    catch { }

                    // ====== 核心过滤逻辑 ======
                    if (isContainerScan)
                    {
                        // 如果是专扫容器，只保留容器
                        if (!isContainer) continue;
                    }
                    else if (isImageOnly)
                    {
                        // 如果是专扫图片，放行
                    }
                    else
                    {
                        if (!isClickable) continue; // 1. 不可点，直接丢
                        if (isContainer) continue;  // 2. 是容器，直接丢
                    }

                    list.Add(new ElementData
                    {
                        Element = el,
                        Rect = new System.Windows.Rect(rect.X, rect.Y, rect.Width, rect.Height),
                        Area = rect.Width * rect.Height,
                        Type = type,
                        IsContainer = isContainer
                    });
                }
                catch { continue; }
            }

            // 几何去重 (保留，用于处理大小极为相近的重叠按钮)
            var arr = list.ToArray();
            int count = arr.Length;

            for (int i = 0; i < count; i++)
            {
                if (!arr[i].ShouldDraw) continue;
                var outer = arr[i];

                for (int j = 0; j < count; j++)
                {
                    if (i == j) continue;
                    if (!arr[j].ShouldDraw) continue;
                    var inner = arr[j];

                    if (outer.Rect.Contains(inner.Rect) ||
                       (outer.Rect.Contains(new System.Windows.Point(inner.Rect.X + 2, inner.Rect.Y + 2)) &&
                        outer.Rect.Contains(new System.Windows.Point(inner.Rect.Right - 2, inner.Rect.Bottom - 2))))
                    {
                        if (Math.Abs(outer.Area - inner.Area) < 100)
                        {
                            if (IsBetterControl(outer.Type, inner.Type)) inner.ShouldDraw = false;
                            else outer.ShouldDraw = false;
                        }
                        else
                        {
                            if (outer.IsContainer) outer.ShouldDraw = false;
                            else if (outer.Type == ControlType.Button || outer.Type == ControlType.MenuItem || outer.Type == ControlType.ListItem)
                                inner.ShouldDraw = false;
                        }
                    }
                }
            }

            return list.Where(x => x.ShouldDraw).ToList();
        }


        private bool IsBetterControl(ControlType typeA, ControlType typeB)
        {
            int Score(ControlType t)
            {
                if (t == ControlType.Button || t == ControlType.Hyperlink || t == ControlType.Edit) return 100;
                if (t == ControlType.CheckBox || t == ControlType.RadioButton || t == ControlType.ComboBox) return 90;
                if (t == ControlType.ListItem || t == ControlType.MenuItem) return 80;
                if (t == ControlType.Text || t == ControlType.Image) return 10;
                if (t == ControlType.Pane || t == ControlType.Group) return 5;
                return 0;
            }
            return Score(typeA) > Score(typeB);
        }



        #endregion
    }
}