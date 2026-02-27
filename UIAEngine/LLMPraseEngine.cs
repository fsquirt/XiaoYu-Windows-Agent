using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using XiaoYu_LAM.UIAEngine;

namespace XiaoYu_LAM.UIAEngine
{
    // 定义执行结果，方便主程序处理并回传给 LLM
    public class ExecutionResult
    {
        public string TextFeedback { get; set; } // 给 LLM 的文字反馈
        public ScanResult NewScan { get; set; }  // 如果执行了扫描，这里会包含新截图，否则为空
    }

    internal class LLMParseEngine
    {
        private readonly mainEngine _engine;

        public LLMParseEngine(mainEngine engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        /// <summary>
        /// 解析 LLM 的文本回复，并执行对应的原生操作
        /// </summary>
        /// <param name="llmResponse">LLM 输出的完整文本</param>
        public ExecutionResult ParseAndExecute(string llmResponse)
        {
            var result = new ExecutionResult();
            StringBuilder feedback = new StringBuilder();

            try
            {
                // 解析 <GetWindows />
                if (Regex.IsMatch(llmResponse, @"<GetWindows\s*/>", RegexOptions.IgnoreCase))
                {
                    var windows = _engine.GetRunningWindow();
                    feedback.AppendLine("【执行结果: 获取窗口列表】");
                    foreach (var w in windows)
                    {
                        feedback.AppendLine($"- 句柄: {w.Handle}, 进程: {w.ProcessPath}, 状态: {w.Status}, 标题: {w.Title}");
                    }
                }

                // 解析 <GetShortcuts />
                else if (Regex.IsMatch(llmResponse, @"<GetShortcuts\s*/>", RegexOptions.IgnoreCase))
                {
                    var shortcuts = _engine.GetALLDesktopLnk();
                    feedback.AppendLine("【执行结果: 获取桌面快捷方式】");
                    foreach (var s in shortcuts)
                    {
                        feedback.AppendLine($"- 名称: {s.Name}, 路径: {s.Path}");
                    }
                }

                // 解析 <ScanWindow handle="12345" />
                else if (TryParseHandleCommand(llmResponse, @"<ScanWindow\s+handle=""(\d+)""\s*/>", out IntPtr scanHandle))
                {
                    var scanRes = _engine.ScanWindow(scanHandle);
                    result.NewScan = scanRes;
                    feedback.AppendLine("【执行结果: 窗口常规控件扫描完毕】");
                    feedback.AppendLine("已提供包含编号的标记图。以下是识别到的控件信息：");
                    foreach (var kvp in scanRes.ElementDescriptions)
                    {
                        feedback.AppendLine($"ID: {kvp.Key} -> {kvp.Value}");
                    }
                }

                // 解析 <ScanImageControls handle="12345" />
                else if (TryParseHandleCommand(llmResponse, @"<ScanImageControls\s+handle=""(\d+)""\s*/>", out IntPtr imgHandle))
                {
                    var scanRes = _engine.ScanImageControls(imgHandle);
                    result.NewScan = scanRes;
                    feedback.AppendLine("【执行结果: 窗口图片控件扫描完毕】");
                    feedback.AppendLine("已提供包含编号的标记图。以下是识别到的图片控件信息：");
                    foreach (var kvp in scanRes.ElementDescriptions)
                    {
                        feedback.AppendLine($"ID: {kvp.Key} -> {kvp.Value}");
                    }
                }

                // 解析 <PerformAction id="5" />
                else if (TryParseIdCommand(llmResponse, @"<PerformAction\s+id=""(\d+)""\s*/>", out int actionId))
                {
                    string res = _engine.PerformAction(actionId);
                    feedback.AppendLine($"【执行结果: 代码级交互 (ID:{actionId})】\n{res}");
                    feedback.AppendLine("提示：如果界面没有发生你期望的变化，请尝试使用 <MouseClick id=\"...\" />，或者重新 <ScanWindow ... /> 获取最新状态。");
                }

                // 解析 <MouseClick id="5" />
                else if (TryParseIdCommand(llmResponse, @"<MouseClick\s+id=""(\d+)""\s*/>", out int clickId))
                {
                    string res = _engine.PerformMouseClick(clickId);
                    feedback.AppendLine($"【执行结果: 物理鼠标点击 (ID:{clickId})】\n{res}");
                    feedback.AppendLine("提示：请重新 <ScanWindow ... /> 以确认点击后的界面变化。");
                }

                // 解析 <RunProgram path="C:\..." />
                else if (TryParseStringCommand(llmResponse, @"<RunProgram\s+path=""([^""]+)""\s*/>", out string runPath))
                {
                    string res = _engine.RunProgram(runPath);
                    feedback.AppendLine($"【执行结果: 启动程序】\n{res}");
                }

                // 解析 <RestoreWindow handle="12345" />
                else if (TryParseHandleCommand(llmResponse, @"<RestoreWindow\s+handle=""(\d+)""\s*/>", out IntPtr restoreHandle))
                {
                    string res = _engine.RestoreWindow(restoreHandle);
                    feedback.AppendLine($"【执行结果: 恢复最小化窗口】\n{res}");
                }

                // 解析 <NormalizeWindow handle="12345" />
                else if (TryParseHandleCommand(llmResponse, @"<NormalizeWindow\s+handle=""(\d+)""\s*/>", out IntPtr normHandle))
                {
                    string res = _engine.NormalizeWindow(normHandle);
                    feedback.AppendLine($"【执行结果: 取消最大化状态】\n{res}");
                }

                // --- 新增：截取全屏 ---
                else if (Regex.IsMatch(llmResponse, @"<GetFullScreen\s*/>", RegexOptions.IgnoreCase))
                {
                    var scanRes = _engine.GetFullScreen();
                    result.NewScan = scanRes;
                    feedback.AppendLine("【执行结果: 全屏截图完毕】");
                }

                // --- 新增：最大化窗口 ---
                else if (TryParseHandleCommand(llmResponse, @"<MaximizeWindow\s+handle=""(\d+)""\s*/>", out IntPtr maxHandle))
                {
                    string res = _engine.MaximizeWindow(maxHandle);
                    feedback.AppendLine($"【执行结果: 最大化窗口】\n{res}");
                }

                // --- 新增：扫描容器 ---
                else if (TryParseHandleCommand(llmResponse, @"<ScanContainerControls\s+handle=""(\d+)""\s*/>", out IntPtr containerHandle))
                {
                    var scanRes = _engine.ScanContainerControls(containerHandle);
                    result.NewScan = scanRes;
                    feedback.AppendLine("【执行结果: 容器控件扫描完毕】已提供图片，请查找适合滚动的区块ID。");
                }

                // --- 拆分：后台赋值 ---
                else if (TryParseInputTextCommand(llmResponse, @"<SetValue\s+id=""(\d+)""\s+text=""(.*?)""\s*/>", out int setId, out string setText))
                {
                    string res = _engine.SetValue(setId, setText);
                    feedback.AppendLine($"【执行结果: SetValue 代码赋值 (ID:{setId})】\n{res}");
                }

                // --- 拆分：前台物理输入 ---
                else if (TryParseInputTextCommand(llmResponse, @"<TypeText\s+id=""(\d+)""\s+text=""(.*?)""\s*/>", out int typeId, out string typeText))
                {
                    string res = _engine.TypeText(typeId, typeText);
                    feedback.AppendLine($"【执行结果: TypeText 物理输入 (ID:{typeId})】\n{res}");
                }

                // --- 新增：右键点击 ---
                else if (TryParseIdCommand(llmResponse, @"<RightClick\s+id=""(\d+)""\s*/>", out int rightClickId))
                {
                    string res = _engine.RightClick(rightClickId);
                    feedback.AppendLine($"【执行结果: 物理鼠标右键点击 (ID:{rightClickId})】\n{res}");
                }

                // --- 新增：滚动 ---
                else if (TryParseScrollCommand(llmResponse, out int scrollId, out string direction))
                {
                    string res = _engine.Scroll(scrollId, direction);
                    feedback.AppendLine($"【执行结果: 鼠标滚轮滚动 (ID:{scrollId}, 方向:{direction})】\n{res}");
                }

                // --- 新增：按下按键 ---
                else if (TryParseStringCommand(llmResponse, @"<PressKey\s+key=""([^""]+)""\s*/>", out string keyName))
                {
                    string res = _engine.PressKey(keyName);
                    feedback.AppendLine($"【执行结果: 模拟键盘按键】\n{res}");
                }

                // --- 新增：双击 ---
                else if (TryParseIdCommand(llmResponse, @"<DoubleClick\s+id=""(\d+)""\s*/>", out int dbClickId))
                {
                    string res = _engine.DoubleClick(dbClickId);
                    feedback.AppendLine($"【执行结果: 物理鼠标双击 (ID:{dbClickId})】\n{res}");
                    feedback.AppendLine("提示：请重新 <ScanWindow ... /> 确认界面变化。");
                }

                // 没匹配到任何有效指令
                else
                {
                    feedback.AppendLine("【系统提示】未在你的回复中检测到有效的 XML 动作标签。请检查你的指令格式。");
                }
            }
            catch (Exception ex)
            {
                feedback.AppendLine($"【引擎异常】解析或执行过程中发生错误: {ex.Message}");
            }

            result.TextFeedback = feedback.ToString();
            return result;
        }

        #region 正则解析辅助方法

        private bool TryParseStringCommand(string text, string pattern, out string resultStr)
        {
            resultStr = string.Empty;
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                resultStr = match.Groups[1].Value;
                return true;
            }
            return false;
        }

        private bool TryParseHandleCommand(string text, string pattern, out IntPtr handle)
        {
            handle = IntPtr.Zero;
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && long.TryParse(match.Groups[1].Value, out long handleVal))
            {
                handle = new IntPtr(handleVal);
                return true;
            }
            return false;
        }

        private bool TryParseIdCommand(string text, string pattern, out int id)
        {
            id = -1;
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out id))
            {
                return true;
            }
            return false;
        }

        private bool TryParseInputTextCommand(string text, string pattern, out int id, out string inputText)
        {
            id = -1;
            inputText = string.Empty;
            // 匹配 <InputText id="5" text="你好" />
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (match.Success && int.TryParse(match.Groups[1].Value, out id))
            {
                inputText = match.Groups[2].Value;
                return true;
            }
            return false;
        }

        // 新增解析 Scroll 指令
        private bool TryParseScrollCommand(string text, out int id, out string direction)
        {
            id = -1;
            direction = string.Empty;
            var match = Regex.Match(text, @"<Scroll\s+id=""(\d+)""\s+direction=""(up|down)""\s*/>", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out id))
            {
                direction = match.Groups[2].Value;
                return true;
            }
            return false;
        }

        #endregion
    }
}