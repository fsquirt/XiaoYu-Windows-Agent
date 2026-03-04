using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using Microsoft.Agents.AI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;

namespace XiaoYu_LAM.UIAEngine
{
    // 处理各种 UI 元素的遍历、清洗以及边框绘制
    internal class ScannerManager
    {
        private readonly UiaContext _context;
        public event Action<Bitmap, Bitmap> OnScanCompleted;

        public ScannerManager(UiaContext context)
        {
            _context = context;
        }

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

        public string ScanWindow(long hWnd)
        {
            IntPtr handle = new IntPtr(hWnd);
            var cf = _context.Automation.ConditionFactory;
            var typeCondition = new OrCondition(
                cf.ByControlType(ControlType.Button), cf.ByControlType(ControlType.Edit),
                cf.ByControlType(ControlType.ComboBox), cf.ByControlType(ControlType.ListItem),
                cf.ByControlType(ControlType.MenuItem), cf.ByControlType(ControlType.TabItem),
                cf.ByControlType(ControlType.Hyperlink), cf.ByControlType(ControlType.CheckBox),
                cf.ByControlType(ControlType.TreeItem), cf.ByControlType(ControlType.Text),
                cf.ByControlType(ControlType.RadioButton)
            );
            return ScanInternal(handle, typeCondition, false, "窗口常规控件");
        }

        public string ScanImageControls(long hWnd)
        {
            IntPtr handle = new IntPtr(hWnd);
            return ScanInternal(handle, _context.Automation.ConditionFactory.ByControlType(ControlType.Image), true, "窗口图片控件");
        }

        public string ScanContainerControls(long hWnd)
        {
            IntPtr handle = new IntPtr(hWnd);
            var cf = _context.Automation.ConditionFactory;
            var typeCondition = new OrCondition(
                cf.ByControlType(ControlType.List), cf.ByControlType(ControlType.Pane),
                cf.ByControlType(ControlType.DataGrid), cf.ByControlType(ControlType.Tree),
                cf.ByControlType(ControlType.Table), cf.ByControlType(ControlType.Group)
            );
            return ScanInternal(handle, typeCondition, true, "容器控件");
        }

        private string ScanInternal(IntPtr hWnd, ConditionBase condition, bool isImageOnly, string scanType)
        {
            _context.LastScanElements.Clear();
            try
            {
                var targetWindow = _context.Automation.FromHandle(hWnd);
                if (targetWindow == null) return "错误：无法获取窗口 UIA 节点，可能句柄已失效。";

                NativeMethods.SetForegroundWindow(hWnd);
                System.Threading.Thread.Sleep(1000);

                Bitmap originalBmp = CaptureWindowByHandle(hWnd) ?? new Bitmap(targetWindow.Capture());
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

                        _context.LastScanElements[index] = elData.Element;
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

        private Bitmap CaptureWindowByHandle(IntPtr handle)
        {
            try
            {
                NativeMethods.GetWindowRect(handle, out NativeMethods.RECT rect);
                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;
                if (width <= 0 || height <= 0) return null;

                Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    IntPtr hdc = g.GetHdc();
                    try
                    {
                        bool success = NativeMethods.PrintWindow(handle, hdc, 2) || NativeMethods.PrintWindow(handle, hdc, 0);
                        if (!success) { g.ReleaseHdc(hdc); return null; }
                    }
                    finally { g.ReleaseHdc(hdc); }
                }
                return bmp;
            }
            catch { return null; }
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

                    if (isContainerScan) { if (!isContainer) continue; }
                    else if (isImageOnly) { }
                    else { if (!isClickable || isContainer) continue; }

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

            var arr = list.ToArray();
            int count = arr.Length;

            for (int i = 0; i < count; i++)
            {
                if (!arr[i].ShouldDraw) continue;
                var outer = arr[i];

                for (int j = 0; j < count; j++)
                {
                    if (i == j || !arr[j].ShouldDraw) continue;
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
    }
}