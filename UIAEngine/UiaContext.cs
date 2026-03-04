using System;
using System.Collections.Generic;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace XiaoYu_LAM.UIAEngine
{
    // 用于让扫描模块和交互模块共享 FlaUI 引擎以及缓存的控件列表（这样 LLM 发送 ID 时，InteractionManager 才能找到 ScannerManager 刚刚扫到的控件）

    public class UiaContext : IDisposable
    {
        public UIA3Automation Automation { get; }
        public Dictionary<int, AutomationElement> LastScanElements { get; }

        public UiaContext()
        {
            Automation = new UIA3Automation();
            LastScanElements = new Dictionary<int, AutomationElement>();
        }

        public void Dispose()
        {
            LastScanElements.Clear();
            Automation?.Dispose();
        }
    }
}