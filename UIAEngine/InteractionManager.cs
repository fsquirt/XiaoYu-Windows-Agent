using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Microsoft.Agents.AI;
using System;
using XiaoYu_LAM.AgentEngine;

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

        private string GetControlInfo(int id, AutomationElement element)
        {
            try
            {
                var name = element.Properties.Name.ValueOrDefault;
                var controlType = element.ControlType;
                return $"ID:{id} [{controlType}] {name}";
            }
            catch
            {
                return $"ID:{id}";
            }
        }

        public string PerformAction(int id)
        {
            if (!_context.LastScanElements.TryGetValue(id, out var element))
            {
                var errMsg = $"错误：未找到 ID 为 {id} 的控件，请重新扫描。";
                AuditLogger.LogUIAOperation("PerformAction", id, null, errMsg);
                return errMsg;
            }
            
            var controlInfo = GetControlInfo(id, element);
            
            try
            {
                if (element.Patterns.Invoke.IsSupported) 
                { 
                    element.Patterns.Invoke.Pattern.Invoke(); 
                    AuditLogger.LogUIAOperation("Invoke(点击)", id, controlInfo, "成功");
                    return "Invoke (标准点击) 成功"; 
                }
                if (element.Patterns.Toggle.IsSupported) 
                { 
                    element.Patterns.Toggle.Pattern.Toggle(); 
                    AuditLogger.LogUIAOperation("Toggle(切换)", id, controlInfo, "成功");
                    return "Toggle (切换) 成功"; 
                }
                if (element.Patterns.SelectionItem.IsSupported) 
                { 
                    element.Patterns.SelectionItem.Pattern.Select(); 
                    AuditLogger.LogUIAOperation("Select(选中)", id, controlInfo, "成功");
                    return "Select (选中) 成功"; 
                }
                if (element.Patterns.ExpandCollapse.IsSupported)
                {
                    var state = element.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value;
                    if (state == ExpandCollapseState.Expanded) element.Patterns.ExpandCollapse.Pattern.Collapse();
                    else element.Patterns.ExpandCollapse.Pattern.Expand();
                    AuditLogger.LogUIAOperation("ExpandCollapse", id, controlInfo, $"成功, 状态: {state}");
                    return "Expand/Collapse 成功";
                }
                if (element.Patterns.LegacyIAccessible.IsSupported)
                {
                    var legacyPattern = element.Patterns.LegacyIAccessible.Pattern;
                    string defaultAction = legacyPattern.DefaultAction.Value;
                    if (!string.IsNullOrEmpty(defaultAction))
                    {
                        legacyPattern.DoDefaultAction();
                        AuditLogger.LogUIAOperation("LegacyIAccessible", id, controlInfo, $"动作: {defaultAction}");
                        return $"LegacyIAccessible (执行动作: '{defaultAction}') 成功";
                    }
                }
                AuditLogger.LogUIAOperation("PerformAction", id, controlInfo, "不支持代码级交互");
                return "该控件不支持代码级交互，建议使用 MouseClick。";
            }
            catch (Exception ex) 
            { 
                AuditLogger.LogError("InteractionManager", $"PerformAction 失败: {controlInfo}", ex);
                return $"交互抛出异常: {ex.Message}"; 
            }
        }

        public string DoubleClick(int id)
        {
            if (!_context.LastScanElements.TryGetValue(id, out var element))
            {
                var errMsg = $"错误：未找到 ID {id}。请重新扫描控件";
                AuditLogger.LogUIAOperation("DoubleClick", id, null, errMsg);
                return errMsg;
            }
            
            var controlInfo = GetControlInfo(id, element);
            
            try
            {
                if (element.TryGetClickablePoint(out var point))
                {
                    IntPtr hwnd = GetHwndFromElement(element);
                    if (hwnd != IntPtr.Zero) { NativeMethods.SetForegroundWindow(hwnd); System.Threading.Thread.Sleep(100); }
                    FlaUI.Core.Input.Mouse.DoubleClick(point);
                    AuditLogger.LogUIAOperation("DoubleClick", id, controlInfo, $"坐标: ({point.X}, {point.Y})");
                    return "真实鼠标双击发送成功。";
                }
                AuditLogger.LogUIAOperation("DoubleClick", id, controlInfo, "无法获取可点击坐标");
                return "错误：无法获取可点击坐标。";
            }
            catch (Exception ex) 
            { 
                AuditLogger.LogError("InteractionManager", $"DoubleClick 失败: {controlInfo}", ex);
                return $"双击异常: {ex.Message}"; 
            }
        }

        public string PerformMouseClick(int id)
        {
            if (!_context.LastScanElements.TryGetValue(id, out var element))
            {
                var errMsg = $"错误：未找到 ID {id}。请重新扫描控件";
                AuditLogger.LogUIAOperation("MouseClick", id, null, errMsg);
                return errMsg;
            }
            
            var controlInfo = GetControlInfo(id, element);
            
            try
            {
                if (element.TryGetClickablePoint(out var point))
                {
                    IntPtr hwnd = GetHwndFromElement(element);
                    if (hwnd != IntPtr.Zero) { NativeMethods.SetForegroundWindow(hwnd); System.Threading.Thread.Sleep(100); }
                    FlaUI.Core.Input.Mouse.Click(point);
                    AuditLogger.LogUIAOperation("MouseClick", id, controlInfo, $"坐标: ({point.X}, {point.Y})");
                    return "真实鼠标点击发送成功。";
                }
                AuditLogger.LogUIAOperation("MouseClick", id, controlInfo, "控件不可见或无法获取坐标");
                return "错误：该控件不可见或无法获取可点击坐标。";
            }
            catch (Exception ex) 
            { 
                AuditLogger.LogError("InteractionManager", $"MouseClick 失败: {controlInfo}", ex);
                return $"物理点击异常: {ex.Message}"; 
            }
        }

        public string SetValue(int id, string text)
        {
            if (!_context.LastScanElements.TryGetValue(id, out var element))
            {
                var errMsg = $"错误：未找到 ID {id}。请重新扫描控件";
                AuditLogger.LogUIAOperation("SetValue", id, null, errMsg);
                return errMsg;
            }
            
            var controlInfo = GetControlInfo(id, element);
            
            try
            {
                if (element.Patterns.Value.IsSupported && !element.Patterns.Value.Pattern.IsReadOnly.Value)
                {
                    element.Patterns.Value.Pattern.SetValue(text);
                    AuditLogger.LogUIAOperation("SetValue", id, controlInfo, $"输入: {text}");
                    return "SetValue (后台代码赋值) 成功。";
                }
                AuditLogger.LogUIAOperation("SetValue", id, controlInfo, "不支持 ValuePattern");
                return "该控件不支持 ValuePattern 后台赋值，请尝试使用 TypeText。";
            }
            catch (Exception ex) 
            { 
                AuditLogger.LogError("InteractionManager", $"SetValue 失败: {controlInfo}", ex);
                return $"SetValue 异常: {ex.Message}"; 
            }
        }

        public string TypeText(int id, string text)
        {
            if (!_context.LastScanElements.TryGetValue(id, out var element))
            {
                var errMsg = $"错误：未找到 ID {id}。请重新扫描控件";
                AuditLogger.LogUIAOperation("TypeText", id, null, errMsg);
                return errMsg;
            }
            
            var controlInfo = GetControlInfo(id, element);
            
            try
            {
                string clickRes = PerformMouseClick(id);
                if (clickRes.Contains("错误"))
                {
                    AuditLogger.LogUIAOperation("TypeText", id, controlInfo, $"聚焦失败: {clickRes}");
                    return $"TypeText 失败，无法聚焦: {clickRes}";
                }
                System.Threading.Thread.Sleep(200);

                using (FlaUI.Core.Input.Keyboard.Pressing(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL))
                {
                    FlaUI.Core.Input.Keyboard.Type(FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_A);
                }
                System.Threading.Thread.Sleep(100);

                FlaUI.Core.Input.Keyboard.Type(FlaUI.Core.WindowsAPI.VirtualKeyShort.BACK);
                System.Threading.Thread.Sleep(100);

                FlaUI.Core.Input.Keyboard.Type(text);
                AuditLogger.LogUIAOperation("TypeText", id, controlInfo, $"输入: {text}");
                return "TypeText (物理点击 + 全选删除 + 键盘模拟) 输入成功。";
            }
            catch (Exception ex) 
            { 
                AuditLogger.LogError("InteractionManager", $"TypeText 失败: {controlInfo}", ex);
                return $"TypeText 异常: {ex.Message}"; 
            }
        }

        public string RightClick(int id)
        {
            if (!_context.LastScanElements.TryGetValue(id, out var element))
            {
                var errMsg = $"错误：未找到 ID {id}。请重新扫描控件";
                AuditLogger.LogUIAOperation("RightClick", id, null, errMsg);
                return errMsg;
            }
            
            var controlInfo = GetControlInfo(id, element);
            
            try
            {
                if (element.TryGetClickablePoint(out var point))
                {
                    IntPtr hwnd = GetHwndFromElement(element);
                    if (hwnd != IntPtr.Zero) { NativeMethods.SetForegroundWindow(hwnd); System.Threading.Thread.Sleep(100); }
                    FlaUI.Core.Input.Mouse.RightClick(point);
                    AuditLogger.LogUIAOperation("RightClick", id, controlInfo, $"坐标: ({point.X}, {point.Y})");
                    return "真实鼠标右键点击成功。";
                }
                AuditLogger.LogUIAOperation("RightClick", id, controlInfo, "无法获取可点击坐标");
                return "错误：无法获取可点击坐标。";
            }
            catch (Exception ex) 
            { 
                AuditLogger.LogError("InteractionManager", $"RightClick 失败: {controlInfo}", ex);
                return $"右键点击异常: {ex.Message}"; 
            }
        }

        public string Scroll(int id, string direction)
        {
            if (!_context.LastScanElements.TryGetValue(id, out var element))
            {
                var errMsg = $"错误：未找到 ID {id}。请重新扫描控件";
                AuditLogger.LogUIAOperation("Scroll", id, null, errMsg);
                return errMsg;
            }
            
            var controlInfo = GetControlInfo(id, element);
            
            try
            {
                var rect = element.BoundingRectangle;
                if (rect.IsEmpty)
                {
                    AuditLogger.LogUIAOperation("Scroll", id, controlInfo, "控件没有有效边界");
                    return "错误：控件没有有效的边界。";
                }

                int centerX = (int)(rect.Left + rect.Width / 2);
                int centerY = (int)(rect.Top + rect.Height / 2);
                try { element.Focus(); } catch { }

                FlaUI.Core.Input.Mouse.Position = new System.Drawing.Point(centerX, centerY);
                System.Threading.Thread.Sleep(50);

                double scrollAmount = direction.ToLower() == "down" ? -7.0 : 7.0;
                FlaUI.Core.Input.Mouse.Scroll(scrollAmount);

                AuditLogger.LogUIAOperation("Scroll", id, controlInfo, $"方向: {direction}, 坐标: ({centerX}, {centerY})");
                return $"已在目标区域中心点 ({centerX},{centerY}) 向 {direction} 物理滚动。请重新扫描检查。";
            }
            catch (Exception ex) 
            { 
                AuditLogger.LogError("InteractionManager", $"Scroll 失败: {controlInfo}", ex);
                return $"滚动异常: {ex.Message}"; 
            }
        }

        public string ScrollWithKeyboard(int id, string direction)
        {
            if (!_context.LastScanElements.TryGetValue(id, out var element))
            {
                var errMsg = $"错误：未找到 ID {id}。请重新扫描控件";
                AuditLogger.LogUIAOperation("ScrollWithKeyboard", id, null, errMsg);
                return errMsg;
            }
            
            var controlInfo = GetControlInfo(id, element);
            
            try
            {
                var rect = element.BoundingRectangle;
                if (rect.IsEmpty)
                {
                    AuditLogger.LogUIAOperation("ScrollWithKeyboard", id, controlInfo, "控件没有有效边界");
                    return "错误：控件没有有效的边界。";
                }
                int centerX = (int)(rect.Left + rect.Width / 2);
                int centerY = (int)(rect.Top + rect.Height / 2);
                try { element.Focus(); } catch { }
                FlaUI.Core.Input.Mouse.Position = new System.Drawing.Point(centerX, centerY);
                System.Threading.Thread.Sleep(50);
                FlaUI.Core.WindowsAPI.VirtualKeyShort key = direction.ToLower() == "down" ? FlaUI.Core.WindowsAPI.VirtualKeyShort.NEXT : FlaUI.Core.WindowsAPI.VirtualKeyShort.PRIOR;
                FlaUI.Core.Input.Keyboard.Press(key);
                AuditLogger.LogUIAOperation("ScrollWithKeyboard", id, controlInfo, $"方向: {direction}, 按键: {key}");
                return $"已在目标区域中心点 ({centerX},{centerY}) 模拟按键 {key} 进行滚动。请重新扫描检查。";
            }
            catch (Exception ex) 
            { 
                AuditLogger.LogError("InteractionManager", $"ScrollWithKeyboard 失败: {controlInfo}", ex);
                return $"键盘滚动异常: {ex.Message}"; 
            }
        }

        public string PressKey(string keyName)
        {
            try
            {
                if (Enum.TryParse(keyName, true, out FlaUI.Core.WindowsAPI.VirtualKeyShort vKey))
                {
                    FlaUI.Core.Input.Keyboard.Press(vKey);
                    AuditLogger.LogUIAOperation("PressKey", null, null, $"按键: {keyName}");
                    return $"已成功按下按键: {keyName}";
                }
                AuditLogger.LogUIAOperation("PressKey", null, null, $"不支持的按键: {keyName}");
                return $"错误：不支持的按键名称 '{keyName}'。常见支持：Enter, Esc, Tab, Space, Back, Delete";
            }
            catch (Exception ex) 
            { 
                AuditLogger.LogError("InteractionManager", $"PressKey 失败: {keyName}", ex);
                return $"按键异常: {ex.Message}"; 
            }
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