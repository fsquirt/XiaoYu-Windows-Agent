using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using XiaoYu_LAM.AgentEngine;

namespace XiaoYu_LAM.UIAEngine
{
    // 原样保留给 LLM 看的 [Description] 标签和方法签名

    public class mainEngine : IDisposable
    {
        private readonly UiaContext _context;
        private readonly WindowManager _windowManager;
        private readonly ScannerManager _scannerManager;
        private readonly InteractionManager _interactionManager;

        // 当扫描/截图产生时触发
        public event Action<Bitmap, Bitmap> OnScanCompleted;

        public mainEngine()
        {
            _context = new UiaContext();
            _windowManager = new WindowManager(_context);
            _scannerManager = new ScannerManager(_context);
            _interactionManager = new InteractionManager(_context);

            // 转发扫描完成事件
            _scannerManager.OnScanCompleted += (drawnBmp, originalBmp) => OnScanCompleted?.Invoke(drawnBmp, originalBmp);
        }

        public void Dispose()
        {
            _context?.Dispose();
        }

        // 导出所有 MSAF 工具
        public List<AITool> GetTools()
        {
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
                AIFunctionFactory.Create(new Func<string, string>(this.PressKey), name: "PressKey"),
                AIFunctionFactory.Create(new Func<int, string, string>(this.Scroll), name: "Scroll"),
                AIFunctionFactory.Create(new Func<string,string,string,int,int,string>(TaskSchEngine.CreateTask),name:"CreateTask")//,
                //AIFunctionFactory.Create(new Func<long, string>(this.BringWindowToFront), name: "BringWindowToFront")
            };
        }

        #region WindowManager 门面
        [Description("获取桌面所有快捷方式。返回桌面上所有的快捷方式名称和路径。如果目标软件没打开，用这个找路径。")]
        public string GetALLDesktopLnk() => _windowManager.GetALLDesktopLnk(); 
        [Description("获取当前所有运行的窗口列表。返回包含句柄(Handle)、状态和标题的文本。这是寻找目标程序 Handle 的第一步。")]
        public string GetRunningWindow() => _windowManager.GetRunningWindow(); 
        [Description("恢复最小化的窗口。极度重要：若 GetWindows 显示窗口状态为最小化，扫描前必须执行此指令！")]
        public string RestoreWindow([Description("窗口的纯数字句柄")] long hWnd) => _windowManager.RestoreWindow(hWnd); 
        [Description("将最大化的窗口恢复为普通大小。")]
        public string NormalizeWindow([Description("窗口的纯数字句柄")] long hWnd) => _windowManager.NormalizeWindow(hWnd); 
        [Description("最大化窗口。当发现窗口太小，UI 元素重叠难以看清时使用。")]
        public string MaximizeWindow([Description("窗口的纯数字句柄")] long hWnd) => _windowManager.MaximizeWindow(hWnd); 
        [Description("将指定窗口移动到屏幕最前端并激活。如果你怀疑窗口被遮挡导致扫描不全，请调用此工具。")]
        public string BringWindowToFront([Description("窗口的纯数字句柄")] long hWnd) => _windowManager.BringWindowToFront(hWnd); 
        [Description("启动指定路径的程序（通过快捷方式或exe绝对路径）。")]
        public string RunProgram([Description("程序的完整路径")] string path) => _windowManager.RunProgram(path);
        #endregion

        #region ScannerManager 门面
        [Description("截取整个电脑桌面的全屏画面。当你迷失方向，或者找不到特定窗口时使用。")]
        public string GetFullScreen() => _scannerManager.GetFullScreen(); 
        [Description("常规扫描窗口，获取带有编号红框的控件截图。必须提供纯数字句柄。")]
        public string ScanWindow([Description("窗口的纯数字句柄")] long hWnd) => _scannerManager.ScanWindow(hWnd); 
        [Description("单独扫描纯图片控件。当常规扫描漏掉了某些看起来像按钮的图标时使用。")]
        public string ScanImageControls([Description("窗口的纯数字句柄")] long hWnd) => _scannerManager.ScanImageControls(hWnd); 
        [Description("单独扫描容器控件（列表、表格、树），用于寻找大区块以便滚动")]
        public string ScanContainerControls([Description("窗口的纯数字句柄")] long hWnd) => _scannerManager.ScanContainerControls(hWnd);
        #endregion

        #region InteractionManager 门面
        [Description("后台代码级交互（优先使用，用于左键点击、选中、展开，速度快不抢鼠标）。")]
        public string PerformAction([Description("要操作的控件纯数字ID")] int id) => _interactionManager.PerformAction(id); 
        [Description("前台物理鼠标左键双击。打开文件夹、打开文件时，通常需要双击！")]
        public string DoubleClick([Description("要操作的控件纯数字ID")] int id) => _interactionManager.DoubleClick(id); 
        [Description("前台物理鼠标左键点击（备用方案：当 PerformAction 反馈执行成功但界面没反应时使用）。")]
        public string PerformMouseClick([Description("要操作的控件纯数字ID")] int id) => _interactionManager.PerformMouseClick(id); 
        [Description("后台代码级写入文本（优先尝试的文本输入方式，瞬间完成）。")]
        public string SetValue([Description("要输入文本的控件ID")] int id, [Description("要输入的文字")] string text) => _interactionManager.SetValue(id, text); 
        [Description("前台物理模拟打字（当 SetValue 失败或不支持时使用，会先强制点击聚焦，全选删除旧内容，再敲击新内容）。")]
        public string TypeText([Description("要输入文本的控件ID")] int id, [Description("要输入的文字")] string text) => _interactionManager.TypeText(id, text); 
        [Description("物理鼠标右键点击（用于呼出右键菜单）。")]
        public string RightClick([Description("要操作的控件纯数字ID")] int id) => _interactionManager.RightClick(id); 
        [Description("对指定的容器区块进行物理滚轮翻页。必须提供 direction 参数。")]
        public string Scroll([Description("要滚动的容器控件ID")] int id, [Description("滚动方向，只能为 'down' 或 'up'")] string direction) => _interactionManager.Scroll(id, direction); 
        [Description("模拟按下键盘按键。支持: Enter, Esc, Tab, Space, Back, Delete 等。")]
        public string PressKey([Description("按键名称")] string keyName) => _interactionManager.PressKey(keyName);
        #endregion
    }
}