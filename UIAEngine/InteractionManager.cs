using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Microsoft.Agents.AI;
using System;

namespace XiaoYu_LAM.UIAEngine
{
    // 处理所有的鼠标点击、键盘输入和原生 UIA 控件行为调用

    internal class InteractionManager
    {
        private readonly UiaContext _context;

        public InteractionManager(UiaContext context)
        {
            _context = context;
        }

        public string PerformAction(int id)
        {
            if (!_context.LastScanElements.TryGetValue(id, out var element)) return $"错误：未找到 ID 为 {id} 的控件，请重新扫描。";
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

        public string DoubleClick(int id)
        {
            if (!_context.LastScanElements.TryGetValue(id, out var element)) return $"错误：未找到 ID {id}。";
            try
            {
                if (element.TryGetClickablePoint(out var point))
                {
                    IntPtr hwnd = GetHwndFromElement(element);
                    if (hwnd != IntPtr.Zero) { NativeMethods.SetForegroundWindow(hwnd); System.Threading.Thread.Sleep(100); }
                    FlaUI.Core.Input.Mouse.DoubleClick(point);
                    return "真实鼠标双击发送成功。";
                }
                return "错误：无法获取可点击坐标。";
            }
            catch (Exception ex) { return $"双击异常: {ex.Message}"; }
        }

        public string PerformMouseClick(int id)
        {
            if (!_context.LastScanElements.TryGetValue(id, out var element)) return $"错误：未找到 ID {id}。";
            try
            {
                if (element.TryGetClickablePoint(out var point))
                {
                    IntPtr hwnd = GetHwndFromElement(element);
                    if (hwnd != IntPtr.Zero) { NativeMethods.SetForegroundWindow(hwnd); System.Threading.Thread.Sleep(100); }
                    FlaUI.Core.Input.Mouse.Click(point);
                    return "真实鼠标点击发送成功。";
                }
                return "错误：该控件不可见或无法获取可点击坐标。";
            }
            catch (Exception ex) { return $"物理点击异常: {ex.Message}"; }
        }

        public string SetValue(int id, string text)
        {
            if (!_context.LastScanElements.TryGetValue(id, out var element)) return $"错误：未找到 ID {id}。";
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

        public string TypeText(int id, string text)
        {
            if (!_context.LastScanElements.TryGetValue(id, out var element)) return $"错误：未找到 ID {id}。";
            try
            {
                string clickRes = PerformMouseClick(id);
                if (clickRes.Contains("错误")) return $"TypeText 失败，无法聚焦: {clickRes}";
                System.Threading.Thread.Sleep(200);

                using (FlaUI.Core.Input.Keyboard.Pressing(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL))
                {
                    FlaUI.Core.Input.Keyboard.Type(FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_A);
                }
                System.Threading.Thread.Sleep(100);

                FlaUI.Core.Input.Keyboard.Type(FlaUI.Core.WindowsAPI.VirtualKeyShort.BACK);
                System.Threading.Thread.Sleep(100);

                FlaUI.Core.Input.Keyboard.Type(text);
                return "TypeText (物理点击 + 全选删除 + 键盘模拟) 输入成功。";
            }
            catch (Exception ex) { return $"TypeText 异常: {ex.Message}"; }
        }

        public string RightClick(int id)
        {
            if (!_context.LastScanElements.TryGetValue(id, out var element)) return $"错误：未找到 ID {id}。";
            try
            {
                if (element.TryGetClickablePoint(out var point))
                {
                    IntPtr hwnd = GetHwndFromElement(element);
                    if (hwnd != IntPtr.Zero) { NativeMethods.SetForegroundWindow(hwnd); System.Threading.Thread.Sleep(100); }
                    FlaUI.Core.Input.Mouse.RightClick(point);
                    return "真实鼠标右键点击成功。";
                }
                return "错误：无法获取可点击坐标。";
            }
            catch (Exception ex) { return $"右键点击异常: {ex.Message}"; }
        }

        public string Scroll(int id, string direction)
        {
            if (!_context.LastScanElements.TryGetValue(id, out var element)) return $"错误：未找到 ID {id}。";
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

        public string PressKey(string keyName)
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
    }
}