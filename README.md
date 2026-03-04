# 晓予 
> 基于Windows无障碍接口 (UIA) 与多模态大模型的 Windows 桌面自动化智能体。

[![.NET Framework](https://img.shields.io/badge/.NET-4.8-blue.svg)](https://dotnet.microsoft.com/)
[![Microsoft Agent Framework](https://img.shields.io/badge/Agent-Microsoft%20Agent%20Framework-brightgreen)](https://github.com/microsoft/agent-framework)

[🎬 点击观看演示视频](https://www.cloudyou.top/演示视频.mp4)
![主界面截图](https://www.cloudyou.top/images/mainform.png)

⚠️ **警告**：本软件处于早期开发阶段，功能不稳定。请勿在生产环境或重要数据上使用。

## 工作原理：它是如何让LLM控制 Windows 的？
使用 **UIA (UI Automation)** 融合定位的方案：
1. **扫描**：利用 [FlaUI](https://github.com/FlaUI/FlaUI) 库遍历当前活动窗口的 UIA 树，提取所有可交互控件（按钮、输入框、列表等）。
2. **标记**：给每个提取到的控件分配一个数字 ID，并在屏幕截图的对应位置画上红色数字边框。
3. **决策**：将带有数字编号的截图，以及控件类型列表发送给 LLM。LLM 会结合视觉和文字理解当前界面状态，并决定下一步操作（例如：“我需要点击 ID 为 15 的搜索按钮”）。
4. **执行**：调用原生 Windows API 或外设模拟进行物理交互。

以网易云音乐为例，通过 UIA 扫描到的控件会被标记出来，LLM 可以根据这些标记进行智能决策：
![网易云音乐](https://www.cloudyou.top/images/wyy2.jpg)

## 快速开始

### 运行环境
* **.NET Framework 4.8**
* 建议以**管理员权限**运行（程序内置了自动提权逻辑），否则无法操作系统级或其他管理员权限运行的窗口。

### 配置说明
第一次启动程序时，会弹出欢迎配置向导。你需要提供一个支持视觉理解和工具调用的大模型 API：
* **支持协议**：OpenAI 
* 配置完成后，设置会保存在根目录的 `config.ini` 中。

## 注意
本项目涉及直接操作操作系统键鼠。LLM 存在幻觉可能，可能会执行意料之外的破坏性操作（如误删文件、发送错误消息等），请在有人值守的情况下使用

## License
WTFPL