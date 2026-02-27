using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

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

    // 定义扫描结果结构
    public class ScanResult
    {
        public Bitmap OriginalImage { get; set; }  // 原始干净截图
        public Bitmap DrawnImage { get; set; }     // 带红框和ID的截图
        public Dictionary<int, string> ElementDescriptions { get; set; } // ID 和 控件类型的描述，供 LLM 参考
    }

    public class mainEngine : IDisposable
    {
        private readonly UIA3Automation _automation;
        // 核心状态：缓存最近一次扫描的控件，ID -> 控件实体
        private Dictionary<int, AutomationElement> _lastScanElements;

        public mainEngine()
        {
            _automation = new UIA3Automation();
            _lastScanElements = new Dictionary<int, AutomationElement>();
        }

        public void Dispose()
        {
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

        /// 获取桌面所有快捷方式
        public List<ShortcutInfo> GetALLDesktopLnk()
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

            return shortcuts;
        }

        /// 获取当前所有运行中的可见窗口 (FlaUI重构版)
        public List<WindowInfo> GetRunningWindow()
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
                        if (title == "任务切换" || title == "Program Manager" || title == "任务栏") continue;

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

            return windowList;
        }

        /// 取消指定窗口的最小化状态
        public string RestoreWindow(IntPtr hWnd)
        {
            try
            {
                if (hWnd != IntPtr.Zero)
                {
                    ShowWindow(hWnd, SW_RESTORE);
                    return "已发送恢复窗口指令，窗口应该已回到屏幕上。请使用 <ScanWindow /> 重新获取界面。";
                }
                return "错误：无效的窗口句柄。";
            }
            catch (Exception ex)
            {
                return $"恢复窗口失败: {ex.Message}";
            }
        }

        /// 将最大化窗口恢复为普通窗口化
        public string NormalizeWindow(IntPtr hWnd)
        {
            try
            {
                if (hWnd != IntPtr.Zero)
                {
                    ShowWindow(hWnd, SW_SHOWNORMAL);
                    return "已发送普通窗口化指令。请使用 <ScanWindow /> 重新获取界面。";
                }
                return "错误：无效的窗口句柄。";
            }
            catch (Exception ex)
            {
                return $"普通窗口化失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 最大化指定窗口
        /// </summary>
        public string MaximizeWindow(IntPtr hWnd)
        {
            try
            {
                if (hWnd != IntPtr.Zero)
                {
                    ShowWindow(hWnd, SW_SHOWMAXIMIZED);
                    return "已发送最大化窗口指令。请使用 <ScanWindow /> 重新获取界面。";
                }
                return "错误：无效的窗口句柄。";
            }
            catch (Exception ex) { return $"最大化窗口失败: {ex.Message}"; }
        }

        /// <summary>
        /// 获取全屏截图（主显示器）
        /// </summary>
        public ScanResult GetFullScreen()
        {
            try
            {
                // 使用 FlaUI 原生全屏截图
                var originalImage = FlaUI.Core.Capturing.Capture.MainScreen().Bitmap;

                return new ScanResult
                {
                    OriginalImage = originalImage,
                    DrawnImage = new Bitmap(originalImage), // 全屏不需要画框，直接传原图
                    ElementDescriptions = new Dictionary<int, string> { { 0, "全屏截图，无具体控件ID" } }
                };
            }
            catch (Exception ex) { throw new Exception($"截取全屏失败: {ex.Message}"); }
        }


        #region LLM 核心接口：扫描 (Scan)

        /// <summary>
        /// 扫描指定窗口的常规可交互控件
        /// </summary>
        public ScanResult ScanWindow(IntPtr hWnd)
        {
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

            return ScanInternal(hWnd, typeCondition, false);
        }

        /// <summary>
        /// 单独扫描指定窗口的 Image 控件 (降低常规扫描的信息密度)
        /// </summary>
        public ScanResult ScanImageControls(IntPtr hWnd)
        {
            var cf = _automation.ConditionFactory;
            var typeCondition = cf.ByControlType(ControlType.Image);

            return ScanInternal(hWnd, typeCondition, true);
        }

        private ScanResult ScanInternal(IntPtr hWnd, ConditionBase condition, bool isImageOnly)
        {
            _lastScanElements.Clear();
            var targetWindow = _automation.FromHandle(hWnd);
            if (targetWindow == null) throw new Exception("无法获取窗口 UIA 节点");

            // 1. 获取原始截图 (原图)
            Bitmap originalBmp = CaptureWindowByHandle(hWnd);
            if (originalBmp == null) originalBmp = new Bitmap(targetWindow.Capture());

            // 2. 创建绘制图副本
            Bitmap drawnBmp = new Bitmap(originalBmp);
            var descriptions = new Dictionary<int, string>();

            using (Graphics g = Graphics.FromImage(drawnBmp))
            {
                Pen redPen = new Pen(Color.Red, 2);
                Font font = new Font("Arial", 9, FontStyle.Bold);
                SolidBrush textBrush = new SolidBrush(Color.White);
                SolidBrush bgBrush = new SolidBrush(Color.Blue);

                var rawElements = targetWindow.FindAll(TreeScope.Descendants, condition);

                // 去重过滤
                var optimizedElements = OptimizeElements(rawElements, isImageOnly);

                var winRect = targetWindow.BoundingRectangle;
                int index = 1;

                foreach (var elData in optimizedElements)
                {
                    var rect = elData.Rect;
                    int relativeX = (int)(rect.Left - winRect.Left);
                    int relativeY = (int)(rect.Top - winRect.Top);

                    if (relativeX < 0) relativeX = 0;
                    if (relativeY < 0) relativeY = 0;

                    // 绘制
                    g.DrawRectangle(redPen, relativeX, relativeY, (int)rect.Width, (int)rect.Height);
                    string idText = index.ToString();
                    g.FillRectangle(bgBrush, relativeX, relativeY, idText.Length * 10 + 5, 14);
                    g.DrawString(idText, font, textBrush, relativeX, relativeY - 1);

                    // 缓存记录
                    _lastScanElements[index] = elData.Element;

                    // 提取名字供 LLM 参考
                    string controlName = elData.Element.Properties.Name.ValueOrDefault;
                    if (string.IsNullOrWhiteSpace(controlName)) controlName = "<无名>";
                    descriptions[index] = $"[{elData.Type}] {controlName}";

                    index++;
                }
            }

            return new ScanResult
            {
                OriginalImage = originalBmp,
                DrawnImage = drawnBmp,
                ElementDescriptions = descriptions
            };
        }

        /// <summary>
        /// 单独扫描容器控件（列表、表格、树），用于寻找大区块以便滚动
        /// </summary>
        public ScanResult ScanContainerControls(IntPtr hWnd)
        {
            var cf = _automation.ConditionFactory;
            var typeCondition = new OrCondition(
                cf.ByControlType(ControlType.List),
                cf.ByControlType(ControlType.Pane),
                cf.ByControlType(ControlType.DataGrid),
                cf.ByControlType(ControlType.Tree),
                cf.ByControlType(ControlType.Table),
                cf.ByControlType(ControlType.Group)
            );
            return ScanInternal(hWnd, typeCondition, true); // 复用 isImageOnly=true，跳过可点击验证
        }

        #endregion

        #region LLM 核心接口：交互 (Interact)

        /// <summary>
        /// 接口 1：执行代码级交互 (Invoke, Toggle 等)。不抢物理鼠标。
        /// </summary>
        public string PerformAction(int elementId)
        {
            if (!_lastScanElements.TryGetValue(elementId, out var element))
                return $"错误：未找到 ID 为 {elementId} 的控件，请重新扫描。";

            try
            {
                if (element.Patterns.Invoke.IsSupported)
                {
                    element.Patterns.Invoke.Pattern.Invoke();
                    return "Invoke (标准点击) 成功";
                }
                if (element.Patterns.Toggle.IsSupported)
                {
                    element.Patterns.Toggle.Pattern.Toggle();
                    return "Toggle (切换) 成功";
                }
                if (element.Patterns.SelectionItem.IsSupported)
                {
                    element.Patterns.SelectionItem.Pattern.Select();
                    return "Select (选中) 成功";
                }
                if (element.Patterns.ExpandCollapse.IsSupported)
                {
                    var state = element.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value;
                    if (state == ExpandCollapseState.Expanded)
                        element.Patterns.ExpandCollapse.Pattern.Collapse();
                    else
                        element.Patterns.ExpandCollapse.Pattern.Expand();
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

                return "该控件不支持代码级交互，建议使用 PerformMouseClick。";
            }
            catch (Exception ex)
            {
                return $"交互抛出异常: {ex.Message}";
            }
        }

        /// <summary>
        /// 物理鼠标左键双击
        /// </summary>
        public string DoubleClick(int elementId)
        {
            if (!_lastScanElements.TryGetValue(elementId, out var element)) return $"错误：未找到 ID {elementId}。";
            try
            {
                if (element.TryGetClickablePoint(out var point))
                {
                    IntPtr hwnd = GetHwndFromElement(element);
                    if (hwnd != IntPtr.Zero)
                    {
                        SetForegroundWindow(hwnd);
                        System.Threading.Thread.Sleep(100);
                    }

                    FlaUI.Core.Input.Mouse.DoubleClick(point); // FlaUI 原生双击
                    return "真实鼠标双击发送成功。";
                }
                return "错误：无法获取可点击坐标。";
            }
            catch (Exception ex) { return $"双击异常: {ex.Message}"; }
        }

        /// <summary>
        /// 接口 2：物理鼠标真实点击 (单桌面终极兜底方案)
        /// 会将窗口前置，并移动真实鼠标点击坐标点。
        /// </summary>
        public string PerformMouseClick(int elementId)
        {
            if (!_lastScanElements.TryGetValue(elementId, out var element))
                return $"错误：未找到 ID 为 {elementId} 的控件。";

            try
            {
                if (element.TryGetClickablePoint(out var point))
                {
                    // 尝试获取所属窗口句柄，并将其提至前台，防止被遮挡
                    IntPtr hwnd = GetHwndFromElement(element);
                    if (hwnd != IntPtr.Zero)
                    {
                        SetForegroundWindow(hwnd);
                        System.Threading.Thread.Sleep(100); // 等待窗口来到前台
                    }

                    // 调用 FlaUI 原生鼠标模拟物理点击
                    FlaUI.Core.Input.Mouse.Click(point);
                    return "真实鼠标点击发送成功。";
                }
                else
                {
                    return "错误：该控件不可见或无法获取可点击坐标(ClickablePoint)。";
                }
            }
            catch (Exception ex)
            {
                return $"物理点击抛出异常: {ex.Message}";
            }
        }

        /// <summary>
        /// 接口 4：运行指定路径的程序 (通过快捷方式路径或 exe 路径)
        /// </summary>
        public string RunProgram(string path)
        {
            try
            {
                // 使用 Windows 默认外壳启动程序
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
                return $"成功发送启动指令，路径：{path}。请使用 <GetWindows /> 检查程序是否已出现。";
            }
            catch (Exception ex)
            {
                return $"启动程序失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 后台静默赋值 (适合原生控件，不抢焦点)
        /// </summary>
        public string SetValue(int elementId, string text)
        {
            if (!_lastScanElements.TryGetValue(elementId, out var element)) return $"错误：未找到 ID {elementId}。";
            try
            {
                if (element.Patterns.Value.IsSupported && !element.Patterns.Value.Pattern.IsReadOnly.Value)
                {
                    element.Patterns.Value.Pattern.SetValue(text);
                    return "SetValue (后台代码赋值) 成功。";
                }
                return "该控件不支持 ValuePattern 后台赋值，请尝试使用 <TypeText />。";
            }
            catch (Exception ex) { return $"SetValue 异常: {ex.Message}"; }
        }

        /// <summary>
        /// 前台物理输入 (点击聚焦 + 键盘打字，通杀 CEF)
        /// </summary>
        public string TypeText(int elementId, string text)
        {
            if (!_lastScanElements.TryGetValue(elementId, out var element)) return $"错误：未找到 ID {elementId}。";
            try
            {
                string clickRes = PerformMouseClick(elementId);
                if (clickRes.Contains("错误")) return $"TypeText 失败，无法聚焦: {clickRes}";

                System.Threading.Thread.Sleep(100); // 等待光标闪烁
                FlaUI.Core.Input.Keyboard.Type(text);
                return "TypeText (物理点击 + 键盘模拟) 输入成功。";
            }
            catch (Exception ex) { return $"TypeText 异常: {ex.Message}"; }
        }

        /// <summary>
        /// 物理鼠标右键点击
        /// </summary>
        public string RightClick(int elementId)
        {
            if (!_lastScanElements.TryGetValue(elementId, out var element)) return $"错误：未找到 ID {elementId}。";
            try
            {
                if (element.TryGetClickablePoint(out var point))
                {
                    IntPtr hwnd = GetHwndFromElement(element);
                    if (hwnd != IntPtr.Zero) SetForegroundWindow(hwnd);

                    FlaUI.Core.Input.Mouse.RightClick(point);
                    return "真实鼠标右键点击成功。";
                }
                return "错误：无法获取可点击坐标。";
            }
            catch (Exception ex) { return $"右键点击异常: {ex.Message}"; }
        }

        /// <summary>
        /// 对指定容器进行物理滚轮滚动
        /// </summary>
        public string Scroll(int elementId, string direction)
        {
            if (!_lastScanElements.TryGetValue(elementId, out var element)) return $"错误：未找到 ID {elementId}。";
            try
            {
                var rect = element.BoundingRectangle;
                if (rect.IsEmpty) return "错误：控件没有有效的边界。";

                // 计算中心点
                int centerX = (int)(rect.Left + rect.Width / 2);
                int centerY = (int)(rect.Top + rect.Height / 2);

                // 尝试设为焦点
                try { element.Focus(); } catch { }

                // 物理移动鼠标到容器中心
                FlaUI.Core.Input.Mouse.Position = new System.Drawing.Point(centerX, centerY);
                System.Threading.Thread.Sleep(50); // 给系统一点反应时间

                // 物理滚动：负数向下，正数向上
                double scrollAmount = direction.ToLower() == "down" ? -3.0 : 3.0; // FlaUI 的 scroll 量，通常 1 = 120 增量
                FlaUI.Core.Input.Mouse.Scroll(scrollAmount);

                return $"已在目标区域中心点 ({centerX},{centerY}) 向 {direction} 物理滚动。请使用 <ScanWindow /> 重新检查。";
            }
            catch (Exception ex) { return $"滚动异常: {ex.Message}"; }
        }

        /// <summary>
        /// 模拟按下常用键盘按键
        /// </summary>
        public string PressKey(string keyName)
        {
            try
            {
                // 将字符串映射到 FlaUI 的 VirtualKeyShort 枚举
                if (Enum.TryParse(keyName, true, out FlaUI.Core.WindowsAPI.VirtualKeyShort vKey))
                {
                    FlaUI.Core.Input.Keyboard.Press(vKey);
                    return $"已成功按下按键: {keyName}";
                }
                return $"错误：不支持的按键名称 '{keyName}'。常见支持：Enter, Esc, Tab, Space, Back, Delete, Up, Down, Left, Right。";
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

        private List<ElementData> OptimizeElements(AutomationElement[] elements, bool isImageOnly)
        {
            var list = new List<ElementData>();

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

                    // 如果是在非纯图片扫描模式下，丢弃不可点击的非交互元素
                    if (!isImageOnly)
                    {
                        if (!isClickable && (isContainer || type == ControlType.Image || type == ControlType.Text))
                            continue;
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

            // 几何去重
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