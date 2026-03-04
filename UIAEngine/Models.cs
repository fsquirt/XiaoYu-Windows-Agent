using System;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace XiaoYu_LAM.UIAEngine
{
    // 集中存放所有的数据实体结构

    public class WindowInfo
    {
        public string Title { get; set; }
        public int PID { get; set; }
        public string ProcessPath { get; set; }
        public string Status { get; set; }
        public IntPtr Handle { get; set; }
    }

    public class ShortcutInfo
    {
        public string Name { get; set; }
        public string Path { get; set; }
    }

    internal class ElementData
    {
        public AutomationElement Element { get; set; }
        public System.Windows.Rect Rect { get; set; }
        public double Area { get; set; }
        public ControlType Type { get; set; }
        public bool IsContainer { get; set; }
        public bool ShouldDraw { get; set; } = true;
    }
}