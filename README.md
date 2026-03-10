# 晓予 (XiaoYu_LAM)
> 基于 Windows 无障碍接口 (UIA) 与多模态大模型的 Windows 桌面自动化智能体 (Large Action Model Agent)。

[![.NET Framework](https://img.shields.io/badge/.NET-4.8-blue.svg)](https://dotnet.microsoft.com/)
[![Microsoft Agent Framework](https://img.shields.io/badge/Agent-Microsoft%20Agent%20Framework-brightgreen)](https://github.com/microsoft/agent-framework)
[![License](https://img.shields.io/badge/License-WTFPL-lightgrey.svg)](http://www.wtfpl.net/)

[🎬 点击观看演示视频](https://www.cloudyou.top/演示视频2.mp4)

![主界面截图](https://www.cloudyou.top/images/mainform.png)

# ⚠️ **警告**
此项目主要功能都已经完成将不会再进行大规模更新。issue&pr只接受bug报告，**不接受**任何新功能相关内容。
如果想加功能自己fork一个分支，项目协议WTFPL允许你随意修改和发布。

## 🌟 项目简介

**晓予 (XiaoYu)** 是一个能够真正“理解”并操作 Windows 桌面的 AI 智能体。它通过融合 UI Automation (UIA) 控件树扫描与 AI 视觉能力，将 Windows 桌面转化为大模型可以交互的环境。你只需要用自然语言下达指令（例如：“帮我打开网易云音乐并播放一首周杰伦的歌”），晓予就能自动分析屏幕、找到对应按钮并操控鼠标键盘完成任务。

## ⚙️ 工作原理 (Scan -> Mark -> Decision -> Execute)

为了让 LLM 精确控制 Windows 系统，晓予采用了“视觉+结构”融合定位方案，整体流程如下：

1. **扫描 (Scan)**：利用 FlaUI 库遍历当前活动窗口的 UIA 树，提取出屏幕上所有可交互的控件（如按钮、文本框、列表、图片控件等）。
2. **标记 (Mark)**：给每个提取到的控件分配一个全局唯一的数字 ID，并在屏幕截图的对应控件位置画上红色数字边框。
3. **决策 (Decision)**：将带有数字编号的截图，以及控件类型的高效结构化列表（经过 Token 压缩和历史折叠处理）发送给底层的多模态大语言模型，LLM 结合视觉和文字理解当前界面状态，并决定下一步操作（例如：“我需要点击 ID 为 15 的搜索按钮”或“我需要在 ID 为 8 的文本框输入内容”）。
4. **执行 (Execute)**：调用原生 Windows API 进行后台消息传递，或模拟外设进行前台物理交互（如 `PerformAction`、`DoubleClick`、`TypeText` 等）。

![工作原理演示图](https://www.cloudyou.top/images/wyy2.jpg)

## 🚀 核心特性

- **强大底座**：底层基于最新的 **Microsoft Agent Framework (MSAF)** 构建，具备良好的扩展性并支持标准化的 Tool Calling。
- **混合交互引擎 (UIAEngine)**：支持后台代码级交互（瞬间完成，不抢占鼠标指针）与前台物理模拟（鼠标移动、双击、物理滚轮翻页、键盘按键），从而适应各种高难度 UI 场景和反外挂机制。
- **深度思考模型双重兼容**：内置请求和响应流拦截器，无缝支持具有“深度推理”能力的大模型，并能在控制台实时可视化推理过程 (`reasoning_content`)。
- **记忆与会话管理**：可以将当前会话（包含各种历史调用状态）以 JSON 结合人类可读 Markdown 格式转储，支持跨进程会话恢复，有效防止进程意外关闭后的上下文丢失。
- **自定义技能扩展 (Agent Skills)**：采用基于文件系统的 Skills (.md + Scripts) 挂载机制，开发者可通过文本轻松扩展工具和系统预设提示。
- **远程与联动操作**：集成第三方社交软件（Tencent QQ）等联动层，支持从 QQ 远程发送任务指令以触发宿主机操作，获取执行日志；
- **现代化代理支持 (RustTLSProxy)**：内置一层轻量级的 Rust 本地网络代理，专为解决在 Windows 7 / 8 / 8.1 较老的系统架构下不支持 TLS 1.2+ 的通信加密拦截痛点。

## ⚡ 快速开始

### 运行环境
- **操作系统**：Windows 7 / 8 / 10 / 11
- **运行库**：.NET Framework 4.8

### 首次环境配置
第一次启动程序时，若不存在全局 `config.ini`，系统将自动弹出基于 Aero API 的配置向导窗口。你需要前置准备：
1. **大语言模型 API 接入**：目前支持完整的 OpenAI 兼容协议（包含 OpenAI SDK 兼容厂商接口）。
2. **要求多模态模型**：请务必确保配置使用的 Endpoint 支持**视觉多模态能力 (Vision Mode)**，因为模型需要依靠看截图来解析 UI。
3. 配置成功并保存后，系统将自动进入日常应用界面。

![工作原理演示图](https://www.cloudyou.top/images/areowizard.png)

### 命令行调用方式
支持通过进程间通信机制 (IPC) 或者通过命令行直接发起任务实例：
```bash
XiaoYu_LAM.exe --task "帮我在桌面上新建一个名为工作目录的文件夹"
```

## 🏗️ 目录结构简介

- **`AgentEngine/`**: AI 核心驱动层，负责与大模型 API 通信，包装 Microsoft Agent Framework 对象注入、跨平台协议适配、Prompt 注入与 Memory 状态留存。
- **`UIAEngine/`**: 自动化与无障碍支持层。主要包含原生窗口管理 (`WindowManager`)、UIA扫描节点获取 (`ScannerManager`) 以及键鼠模拟互动 (`InteractionManager`) 工具集抽象。
- **`UserForm/`**: 各类图形交互界面集结处，包括主聊天窗、任务日志展示区和 Aero 向导界面等。
- **`RustTLSProxy/`**: Rust 编写的本地反向 TLS 加密代理层，实现现代 API 与老旧系统底层库之间的兼容通讯。
- **`MarkDown/`**: 用于存放系统级 SystemPrompt 定义，以及运行中的历史记录归档存放中心。

## 📜 许可证 (License)

本项目采用 **WTFPL** (Do What The F*ck You Want To Public License) 协议开源，详情请参阅项目根目录下的 [LICENSE](LICENSE) 文件。