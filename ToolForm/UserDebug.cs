using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions; // 必须引用，OrCondition 在这里
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;


namespace XiaoYu_LAM
{
    public partial class UserDebug : Form
    {
        private readonly UIA3Automation _automation;
        private List<ElementData> _lastScanResults = new List<ElementData>();

        public UserDebug()
        {
            InitializeComponent();
            _automation = new UIA3Automation();

            if (pictureBox1 != null)
            {
                pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            }
        }

        private void ScanWindow_Load(object sender, EventArgs e)
        {
            RefreshWindowList();
        }

        private void ScanWindow_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_automation != null)
            {
                _automation.Dispose();
            }
        }

        public class WindowItem
        {
            public string Title { get; set; }
            public FlaUI.Core.AutomationElements.AutomationElement Element { get; set; }
            // 缓存句柄用于后台截图
            public IntPtr Handle { get; set; }
            public override string ToString() { return Title; }
        }

        // --- 1. 获取窗口列表 ---
        private void RefreshWindowList()
        {
            comboBox1.Items.Clear();
            try
            {
                var desktop = _automation.GetDesktop();
                var cf = _automation.ConditionFactory;

                // 宽松策略：不仅仅找 Window，还找 Pane 和 Custom 
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
                        string title = win.Name;
                        // 过滤没有名字的窗口
                        if (string.IsNullOrWhiteSpace(title)) continue;
                        if (title == "任务切换" || title == "Program Manager") continue;

                        // 过滤不可见窗口 (坐标全为0)
                        var rect = win.BoundingRectangle;
                        if (rect.Width <= 0 || rect.Height <= 0) continue;

                        int pid = win.Properties.ProcessId;

                        comboBox1.Items.Add(new WindowItem
                        {
                            Title = $"[{pid}] {title}",
                            Element = win,
                            Handle = win.Properties.NativeWindowHandle // 获取句柄
                        });
                    }
                    catch { continue; }
                }

                if (comboBox1.Items.Count > 0) comboBox1.SelectedIndex = 0;
            }
            catch (Exception ex) { toolStripStatusLabel1.Text = ("刷新失败: " + ex.Message); }
        }


        private void btnScan_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image != null)
            {
                pictureBox1.Image.Dispose();
                pictureBox1.Image = null;
            }
            pictureBox1.Visible = true;
            _lastScanResults.Clear();
            if (comboBox2 != null) comboBox2.Items.Clear();

            var selectedItem = comboBox1.SelectedItem as WindowItem;
            if (selectedItem == null) return;

            var targetWindow = selectedItem.Element;
            Bitmap bitmapToDraw = null; // 提前声明，防止 catch 块外访问不到

            try
            {
                // 1. 截图
                bitmapToDraw = CaptureWindowByHandle(selectedItem.Handle);
                if (bitmapToDraw == null) bitmapToDraw = new Bitmap(targetWindow.Capture());

                using (Graphics g = Graphics.FromImage(bitmapToDraw))
                {
                    Pen redPen = new Pen(Color.Red, 2);
                    Font font = new Font("Arial", 9, FontStyle.Bold);
                    SolidBrush textBrush = new SolidBrush(Color.White);
                    SolidBrush bgBrush = new SolidBrush(Color.Blue);

                    var cf = _automation.ConditionFactory;

                    // 2. 查找条件
                    var typeCondition = new OrCondition(
                        cf.ByControlType(ControlType.Button),
                        cf.ByControlType(ControlType.Edit),
                        cf.ByControlType(ControlType.ComboBox),
                        //cf.ByControlType(ControlType.List),
                        cf.ByControlType(ControlType.ListItem),
                        cf.ByControlType(ControlType.MenuItem),
                        cf.ByControlType(ControlType.TabItem),
                        cf.ByControlType(ControlType.Hyperlink),
                        cf.ByControlType(ControlType.CheckBox),
                        cf.ByControlType(ControlType.TreeItem),
                        //cf.ByControlType(ControlType.DataGrid),
                        cf.ByControlType(ControlType.Text),
                        //cf.ByControlType(ControlType.Document),
                        cf.ByControlType(ControlType.RadioButton)//,
                                                                 //cf.ByControlType(ControlType.Image)

                    //cf.ByControlType(ControlType.Group),
                    //cf.ByControlType(ControlType.Custom),
                    //cf.ByControlType(ControlType.Pane),
                    //cf.ByControlType(ControlType.Image)
                    );

                    var rawElements = targetWindow.FindAll(TreeScope.Descendants, typeCondition);

                    // --- 3. 核心：执行几何去重优化 ---
                    var optimizedElements = OptimizeElements(rawElements);

                    // 保存结果供 button4 使用
                    _lastScanResults = optimizedElements;

                    if (toolStripStatusLabel1 != null)
                        toolStripStatusLabel1.Text = ($"原始 {rawElements.Length} -> 优化后 {optimizedElements.Count}");

                    var winRect = targetWindow.BoundingRectangle;
                    int index = 1;

                    foreach (var elData in optimizedElements)
                    {
                        var rect = elData.Rect;

                        // 坐标转换
                        int relativeX = (int)(rect.Left - winRect.Left);
                        int relativeY = (int)(rect.Top - winRect.Top);

                        // 边界保护
                        if (relativeX < 0) relativeX = 0;
                        if (relativeY < 0) relativeY = 0;

                        // 绘制
                        g.DrawRectangle(redPen, relativeX, relativeY, (int)rect.Width, (int)rect.Height);

                        string idText = index.ToString();
                        g.FillRectangle(bgBrush, relativeX, relativeY, idText.Length * 10 + 5, 14);
                        g.DrawString(idText, font, textBrush, relativeX, relativeY - 1);

                        // --- 修复点：安全地获取 Name 属性 ---
                        if (comboBox2 != null)
                        {
                            // 使用 ValueOrDefault 避免报错，如果不支持则返回 null
                            string controlName = elData.Element.Properties.Name.ValueOrDefault;

                            // 很多控件没有名字，给个默认提示
                            if (string.IsNullOrWhiteSpace(controlName))
                            {
                                controlName = "未命名控件";
                            }

                            comboBox2.Items.Add($"{index}: {elData.Type} - {controlName}");
                        }

                        index++;
                    }

                    // 默认选中第一个
                    if (comboBox2 != null && comboBox2.Items.Count > 0) comboBox2.SelectedIndex = 0;
                }

                if (pictureBox1.Image != null) pictureBox1.Image.Dispose();
                pictureBox1.Image = bitmapToDraw;
            }
            catch (Exception ex)
            {
                // 如果出错，记得释放之前可能创建的 bitmap
                if (bitmapToDraw != null) bitmapToDraw.Dispose();

                if (toolStripStatusLabel1 != null)
                    toolStripStatusLabel1.Text = ("扫描错误: " + ex.Message);

                Console.WriteLine(ex.Message.ToString());
            }
        }

        // 即使窗口被遮挡，这个 API 也能截取到画面 (前提是窗口不能最小化)
        private Bitmap CaptureWindowByHandle(IntPtr handle)
        {
            try
            {
                // 获取窗口矩形
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
                        // PrintWindow 标志位: 2 (PW_CLIENTONLY | PW_RENDERFULLCONTENT) 
                        // 能更好支持某些应用，但只支持 Win8.1+
                        // 如果失败，尝试 0
                        bool success = PrintWindow(handle, hdc, 2);
                        if (!success) success = PrintWindow(handle, hdc, 0);

                        if (!success)
                        {
                            g.ReleaseHdc(hdc);
                            return null; // 失败，回退
                        }
                    }
                    finally
                    {
                        g.ReleaseHdc(hdc);
                    }
                }
                return bmp;
            }
            catch { return null; }
        }

        // --- 几何去重 ---
        private List<ElementData> OptimizeElements(FlaUI.Core.AutomationElements.AutomationElement[] elements)
        {
            var list = new List<ElementData>();

            // Step 1: 统一筛选 (Pattern Check + 基础过滤)
            foreach (var el in elements)
            {
                try
                {
                    var rect = el.BoundingRectangle;
                    if (rect.Width <= 0 || rect.Height <= 0) continue;

                    var type = el.ControlType;
                    bool isContainer = (type == ControlType.Pane || type == ControlType.Group ||
                                        type == ControlType.Custom || type == ControlType.DataGrid ||
                                        type == ControlType.Window || type == ControlType.Document);

                    // 【新增 1】检查可见性 (IsOffscreen)
                    // 使用 ValueOrDefault 避免报错。如果获取失败，默认为 false (即假设可见，避免误杀)
                    // 如果 IsOffscreen 为 true，说明控件在屏幕外或被隐藏，直接跳过
                    if (el.Properties.IsOffscreen.ValueOrDefault)
                    {
                        continue;
                    }

                    // 检查是否支持交互
                    bool isClickable = false;
                    try
                    {
                        // 常见的交互模式
                        if (el.Patterns.Invoke.IsSupported ||
                            el.Patterns.Toggle.IsSupported ||
                            el.Patterns.SelectionItem.IsSupported ||
                            el.Patterns.ExpandCollapse.IsSupported ||
                            el.Patterns.Value.IsSupported)
                        {
                            isClickable = true;
                        }
                    }
                    catch { }

                    if (!isClickable)
                    {
                        continue;
                    }

                    //// 2. 如果是容器，丢了 //// (Pane/Group)，必须可点，或者有名字 (可能是被点击绑定的容器)
                    if (isContainer) //&& !isClickable)
                    {
                        continue; // 丢弃不可交互的容器
                    }

                    // 只有通过了上面筛选的才加入列表
                    list.Add(new ElementData
                    {
                        Element = el,
                        Rect = new System.Windows.Rect(rect.X, rect.Y, rect.Width, rect.Height),
                        Area = rect.Width * rect.Height,
                        Type = type,
                        IsClickable = isClickable,
                        IsContainer = isContainer
                    });
                }
                catch { continue; }
            }

            // Step 2: 几何去重 (处理框中框)
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

                    // 检查包含关系 (宽松判断)
                    if (outer.Rect.Contains(inner.Rect) ||
                       (outer.Rect.Contains(new System.Windows.Point(inner.Rect.X + 2, inner.Rect.Y + 2)) &&
                        outer.Rect.Contains(new System.Windows.Point(inner.Rect.Right - 2, inner.Rect.Bottom - 2))))
                    {
                        // A: 大小几乎一样 (重叠) -> 保留更像控件的那个
                        if (Math.Abs(outer.Area - inner.Area) < 100)
                        {
                            if (IsBetterControl(outer.Type, inner.Type))
                                inner.ShouldDraw = false;
                            else
                                outer.ShouldDraw = false;
                        }
                        // B: 明显嵌套
                        else
                        {
                            // 1. 外层是容器 -> 隐藏外层 (保留具体的内层)
                            if (outer.IsContainer)
                            {
                                outer.ShouldDraw = false;
                            }
                            // 2. 外层是按钮，内层是文字/图片 -> 隐藏内层 (按钮是一个整体)
                            else if (outer.Type == ControlType.Button || outer.Type == ControlType.MenuItem || outer.Type == ControlType.ListItem)
                            {
                                inner.ShouldDraw = false;
                            }
                        }
                    }
                }
            }

            return list.Where(x => x.ShouldDraw).ToList();
        }


        // 辅助判断：TypeA 是否比 TypeB "更像一个控件"
        private bool IsBetterControl(ControlType typeA, ControlType typeB)
        {
            int Score(ControlType t)
            {
                if (t == ControlType.Button || t == ControlType.Hyperlink || t == ControlType.Edit) return 100;
                if (t == ControlType.CheckBox || t == ControlType.RadioButton || t == ControlType.ComboBox) return 90;
                if (t == ControlType.ListItem || t == ControlType.MenuItem) return 80;
                if (t == ControlType.Text || t == ControlType.Image) return 10; // 静态内容分低
                if (t == ControlType.Pane || t == ControlType.Group) return 5;  // 容器分最低
                return 0;
            }
            return Score(typeA) > Score(typeB);
        }

        // --- 辅助类：用于离线计算 ---
        class ElementData
        {
            public FlaUI.Core.AutomationElements.AutomationElement Element { get; set; }
            public System.Windows.Rect Rect { get; set; }
            public double Area { get; set; }
            public ControlType Type { get; set; }
            public bool IsClickable { get; set; }
            public bool IsContainer { get; set; } // 是否是容器类型 (Pane, Group, Custom)
            public bool ShouldDraw { get; set; } = true;
        }

        // --- Win32 API 引入 ---
        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcDrawing, uint nFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        const uint WM_LBUTTONDOWN = 0x0201;
        const uint WM_LBUTTONUP = 0x0202;
        const int MK_LBUTTON = 0x0001;
        const uint WM_MOUSEMOVE = 0x0200;

        private void button2_Click(object sender, EventArgs e)
        {
            RefreshWindowList();
            if (pictureBox1.Image != null)
            {
                pictureBox1.Image.Dispose();
                pictureBox1.Image = null;
            }
            _lastScanResults.Clear();
            if (comboBox2 != null) comboBox2.Items.Clear();
            pictureBox1.Visible = false;
            GC.Collect();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            Image image = pictureBox1.Image;
            // 检查图片是否为空
            if (image == null)
            {
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "JPEG文件|*.jpg|PNG文件|*.png|BMP文件|*.bmp|所有文件|*.*";
            saveFileDialog.Title = "保存图片";
            saveFileDialog.FileName = "image";

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                string fileName = saveFileDialog.FileName;
                ImageFormat format = ImageFormat.Jpeg;
                if (fileName.EndsWith(".png"))
                {
                    format = ImageFormat.Png;
                }
                else if (fileName.EndsWith(".bmp"))
                {
                    format = ImageFormat.Bmp;
                }

                image.Save(fileName, format);
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (comboBox2 == null || comboBox2.SelectedIndex < 0)
            {
                MessageBox.Show("请先扫描并选择一个控件 ID");
                return;
            }

            int index = comboBox2.SelectedIndex;
            if (index >= _lastScanResults.Count) return;

            var targetData = _lastScanResults[index];
            var element = targetData.Element;

            try
            {
                string actionTaken = "无操作";
                bool success = false;

                //// 尝试 Invoke (最标准的点击)
                if (element.Patterns.Invoke.IsSupported)
                {
                    element.Patterns.Invoke.Pattern.Invoke();
                    actionTaken = "Invoke (标准点击) 成功";
                    success = true;
                }

                // 尝试 Toggle (复选框/开关)
                if (!success && element.Patterns.Toggle.IsSupported)
                {
                    element.Patterns.Toggle.Pattern.Toggle();
                    actionTaken = "Toggle (切换) 成功";
                    success = true;
                }

                // 尝试 Selection 
                if (!success && element.Patterns.SelectionItem.IsSupported)
                {
                    element.Patterns.SelectionItem.Pattern.Select();
                    actionTaken = "Select (选中) 成功 (但不保证 UI 实际响应了)";
                    success = true;
                }

                // 尝试 Expand/Collapse
                if (!success && element.Patterns.ExpandCollapse.IsSupported)
                {
                    var state = element.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value;
                    if (state == ExpandCollapseState.Expanded)
                        element.Patterns.ExpandCollapse.Pattern.Collapse();
                    else
                        element.Patterns.ExpandCollapse.Pattern.Expand();
                    actionTaken = "Expand/Collapse 成功";
                    success = true;
                }

                //if (!success && element.Patterns.ExpandCollapse.IsSupported)
                //{
                //    // 在执行展开操作之前，立刻发一个虚拟的 MouseMove 过去
                //    // 骗过应用的底层逻辑，让它以为物理鼠标正指着菜单
                //    SendFakeMouseMove(element);

                //    var state = element.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value;
                //    if (state == ExpandCollapseState.Expanded)
                //        element.Patterns.ExpandCollapse.Pattern.Collapse();
                //    else
                //        element.Patterns.ExpandCollapse.Pattern.Expand();

                //    // 展开之后，再发一次 MouseMove，死死锚定住虚拟鼠标
                //    SendFakeMouseMove(element);

                //    actionTaken = state.ToString() + "反向成功";
                //    success = true;
                //}

                // 尝试 LegacyIAccessible
                else if (!success && element.Patterns.LegacyIAccessible.IsSupported)
                {
                    var legacyPattern = element.Patterns.LegacyIAccessible.Pattern;
                    // 检查默认操作是不是 click 或者 press 之类的
                    string defaultAction = legacyPattern.DefaultAction.Value;
                    if (!string.IsNullOrEmpty(defaultAction))
                    {
                        legacyPattern.DoDefaultAction();
                        actionTaken = $"LegacyIAccessible (执行动作: '{defaultAction}') 成功";
                        success = true;
                    }
                }

                // Win32 PostMessage 后台模拟点击
                if (!success)
                {
                    if (element.TryGetClickablePoint(out var point))
                    {
                        // 1. 核心修复：顺藤摸瓜，找到真正归属的 HWND
                        // 有些控件(如WinForm按钮)自己有HWND，有些(如网页元素)没有HWND，需要找它爹
                        IntPtr targetHwnd = IntPtr.Zero;
                        var currentNode = element;

                        while (currentNode != null)
                        {
                            // 尝试获取当前节点的窗口句柄
                            targetHwnd = currentNode.Properties.NativeWindowHandle.ValueOrDefault;
                            if (targetHwnd != IntPtr.Zero)
                            {
                                break;
                            }
                            currentNode = currentNode.Parent;
                        }

                        // 如果连父节点都找不到，就用下拉框里选中的主窗口句柄
                        if (targetHwnd == IntPtr.Zero)
                        {
                            var winItem = comboBox1.SelectedItem as WindowItem;
                            if (winItem != null) targetHwnd = winItem.Handle;
                        }

                        if (targetHwnd != IntPtr.Zero)
                        {
                            // 2. 坐标转换：将屏幕绝对坐标，转换为这个特定 HWND 的内部相对坐标
                            POINT ptClient = new POINT { X = (int)point.X, Y = (int)point.Y };
                            ScreenToClient(targetHwnd, ref ptClient);

                            // 3. lParam 位运算组装
                            int x = ptClient.X;
                            int y = ptClient.Y;
                            IntPtr lParam = (IntPtr)(((y & 0xFFFF) << 16) | (x & 0xFFFF));

                            // 4. 发送后台消息
                            // 先发 MouseMove 欺骗现代 UI 框架的 :hover 检测
                            PostMessage(targetHwnd, WM_MOUSEMOVE, IntPtr.Zero, lParam);
                            System.Threading.Thread.Sleep(10); // 给一点点处理时间

                            // 发送鼠标左键按下和抬起
                            PostMessage(targetHwnd, WM_LBUTTONDOWN, (IntPtr)MK_LBUTTON, lParam);
                            System.Threading.Thread.Sleep(10);
                            PostMessage(targetHwnd, WM_LBUTTONUP, IntPtr.Zero, lParam);

                            actionTaken = $"PostMessage 后台点击成功 (HWND: {targetHwnd}, 相对坐标: {x},{y})";
                            success = true;
                        }
                        else
                        {
                            actionTaken = "致命错误：无法在 UI 树中找到任何可用的窗口句柄。";
                        }
                    }
                }


                if (!success)
                {
                    actionTaken = "该控件不支持任何代码点击模式，也无法获取坐标进行静默点击。";
                }

                toolStripStatusLabel1.Text = ($"操作结果: {actionTaken},控件名: {element.Name},类型: {element.ControlType}");
            }
            catch (Exception ex)
            {
                toolStripStatusLabel1.Text = ($"操作失败: {ex.Message}");
            }
        }
        // --- 辅助函数：向上溯源寻找真实 HWND ---
        private IntPtr GetHwndFromElement(FlaUI.Core.AutomationElements.AutomationElement element)
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

        // --- 辅助函数：发送虚拟的鼠标悬停消息，防止菜单/浮窗自动关闭 ---
        private void SendFakeMouseMove(FlaUI.Core.AutomationElements.AutomationElement element)
        {
            if (element.TryGetClickablePoint(out var point))
            {
                IntPtr targetHwnd = GetHwndFromElement(element);

                // 如果找不到自身句柄，用主窗口句柄兜底
                if (targetHwnd == IntPtr.Zero)
                {
                    var winItem = comboBox1.SelectedItem as WindowItem;
                    if (winItem != null) targetHwnd = winItem.Handle;
                }

                if (targetHwnd != IntPtr.Zero)
                {
                    POINT ptClient = new POINT { X = (int)point.X, Y = (int)point.Y };
                    ScreenToClient(targetHwnd, ref ptClient);
                    IntPtr lParam = (IntPtr)(((ptClient.Y & 0xFFFF) << 16) | (ptClient.X & 0xFFFF));

                    // 发送假装鼠标在上面移动的消息
                    PostMessage(targetHwnd, WM_MOUSEMOVE, IntPtr.Zero, lParam);
                }
            }
        }
    }
}