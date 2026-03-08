using Anthropic;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using XiaoYu_LAM.AgentEngine;
using XiaoYu_LAM.ToolForm;
using XiaoYu_LAM.UIAEngine;
using XiaoYu_LAM.UserForm;

namespace XiaoYu_LAM
{

    public partial class MainForm : Form
    {
        // 静态实例，方便其他地方万一需要直接操作 MainForm（虽然用 Console 就够了）
        public static MainForm Instance;
        private XiaoYu_LAM.AgentEngine.TencentQQ _qqService;
        public MainForm(string initialTask = null)
        {
            InitializeComponent();
            this.Load += MainForm_Load;
            
            // 初始化右键菜单
            InitSkillsContextMenu();
            InitSchTaskContextMenu();
            InitMemoryContextMenu();
            InitContextMenu();
            InitCheckBoxEvents();

            // Console.Write重定向
            Console.SetOut(new TextBoxWriter(this.LogrichTextBox1));

            // 启动 IPC 监听
            IPCManager.StartServer(HandleIncomingTask);

            // 如果是带参数启动的，直接触发
            if (!string.IsNullOrEmpty(initialTask))
            {
                // 延迟执行确保 UI 加载完毕
                _ = Task.Delay(1000).ContinueWith(_ => HandleIncomingTask(initialTask));
            }

            // 读取硬盘上的记忆并显示
            MemoryManager.LoadMemories();
            RefreshMemoryListView();

            // 订阅数据变化事件，当后台有新总结的记忆或删除了记忆时，自动刷新 UI
            MemoryManager.OnMemoriesChanged += RefreshMemoryListView;

            // 初始化 QQ 服务实例
            _qqService = new XiaoYu_LAM.AgentEngine.TencentQQ();
        }

        public string MODEL_NAME = "";
        public string API_URL = "";
        public string API_KEY = "";
        public string PROTOCOL = "";
        public string[] SkillsFolders = new string[0];
        public bool UseAgentSkills = false; // 对应 config 中的 ENABLE


        private void LoadMarkdownFiles()
        {
            string targetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MarkDown", "conversation");

            if (!Directory.Exists(targetPath))
            {
                MessageBox.Show($"目录不存在: {targetPath}");
                return;
            }

            ChatListView.Items.Clear();

            // 获取所有 .md 文件
            DirectoryInfo dir = new DirectoryInfo(targetPath);
            FileInfo[] files = dir.GetFiles("*.md");

            foreach (FileInfo file in files)
            {
                // 解析 ChatTitle (取第一个 '_' 前的内容)
                string fileName = Path.GetFileNameWithoutExtension(file.Name);
                string chatTitle = fileName.Contains("_") ? fileName.Split('_')[0] : fileName;

                // 创建 ListViewItem
                ListViewItem item = new ListViewItem(chatTitle); // 第一列
                item.SubItems.Add(file.CreationTime.ToString("yyyy-MM-dd HH:mm:ss")); // 第二列
                item.SubItems.Add(file.FullName); // 第三列 (绝对路径)

                ChatListView.Items.Add(item);
            }
        }

        private void InitContextMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();

            // 批量打开
            ToolStripMenuItem openItem = new ToolStripMenuItem("打开选中文件");
            openItem.Click += (s, e) =>
            {
                foreach (ListViewItem item in ChatListView.SelectedItems)
                {
                    string filePath = item.SubItems[2].Text; // 获取第三列的路径
                    if (File.Exists(filePath))
                    {
                        Process.Start("notepad.exe", filePath);
                    }
                }
            };

            // 批量删除
            ToolStripMenuItem deleteItem = new ToolStripMenuItem("删除选中文件");
            deleteItem.Click += (s, e) =>
            {
                int count = ChatListView.SelectedItems.Count;
                if (count == 0) return;

                var result = MessageBox.Show($"确定要删除选中的 {count} 个文件吗？", "确认批量删除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    // 删除时要倒着删或者先把选中的项存进一个临时 List
                    // 否则删掉第一个后，SelectedItems 的索引会发生变化导致报错
                    var selectedList = new System.Collections.Generic.List<ListViewItem>();
                    foreach (ListViewItem item in ChatListView.SelectedItems)
                    {
                        selectedList.Add(item);
                    }

                    foreach (ListViewItem item in selectedList)
                    {
                        try
                        {
                            string filePath = item.SubItems[2].Text;
                            if (File.Exists(filePath))
                            {
                                File.Delete(filePath); // 删磁盘文件
                            }
                            ChatListView.Items.Remove(item); // 删界面显示
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"文件 {item.Text} 删除失败: {ex.Message}");
                        }
                    }
                }
            };

            menu.Items.Add(openItem);
            menu.Items.Add(deleteItem);

            ChatListView.ContextMenuStrip = menu;
        }

        // 重定向标准输出流
        public class TextBoxWriter : TextWriter
        {
            private RichTextBox _textBox;

            public TextBoxWriter(RichTextBox textBox)
            {
                _textBox = textBox;
            }

            public override void Write(char value)
            {
                // 检查文本框是否还在
                if (_textBox == null || _textBox.IsDisposed) return;
                // 必须考虑跨线程问题，因为 Console.WriteLine 可能在后台线程调用
                if (_textBox.InvokeRequired)
                {
                    _textBox.BeginInvoke(new Action(() => Write(value)));
                }
                else
                {
                    _textBox.AppendText(value.ToString());
                    // 让滚动条自动滚到底部
                    _textBox.ScrollToCaret();
                }
            }

            // 为了性能，建议也重写这个
            public override void Write(string value)
            {
                // 检查文本框是否还在
                if (_textBox == null || _textBox.IsDisposed) return;

                if (_textBox.InvokeRequired)
                {
                    _textBox.BeginInvoke(new Action(() => Write(value)));
                }
                else
                {
                    _textBox.AppendText(value);
                    _textBox.ScrollToCaret();
                }
            }
            public override Encoding Encoding => Encoding.UTF8;
        }

        private void LoadConfig()
        {
            try
            {
                // 基础 API 配置
                ApiUrlTextbox.Text = ConfigManager.ApiUrl;
                ApiKeyTextBox.Text = ConfigManager.ApiKey;
                ModelNameTextBox.Text = ConfigManager.ModelName;

                if (ConfigManager.Protocol == "OpenAI")
                    IsOpenAICheckBox.Checked = true;
                else if (ConfigManager.Protocol == "Anthropic")
                    IsAnthropicCheckBox.Checked = true;

                // 特性设置 CheckBox 初始化
                var chkDeepThink = this.Controls.Find("IsDeepThinkMode", true).FirstOrDefault() as CheckBox;
                if (chkDeepThink != null) chkDeepThink.Checked = ConfigManager.IsDeepThinkMode;

                var chkDelPic = this.Controls.Find("IsDeleteHistoryPic", true).FirstOrDefault() as CheckBox;
                if (chkDelPic != null) chkDelPic.Checked = ConfigManager.IsDeleteHistoryPic;

                var chkHideUIA = this.Controls.Find("IsHideUIAoutInChatForm", true).FirstOrDefault() as CheckBox;
                if (chkHideUIA != null) chkHideUIA.Checked = ConfigManager.IsHideUIAoutInChatForm;

                // Skills 模块初始化
                // 暂时解绑事件，防止初始化时弹窗
                IsUseAgentSkills.CheckedChanged -= IsUseAgentSkills_CheckedChanged;
                IsUseAgentSkills.Checked = ConfigManager.EnableSkills;
                IsUseAgentSkills.CheckedChanged += IsUseAgentSkills_CheckedChanged;

                SkillFolderlistView.Items.Clear();
                foreach (var p in ConfigManager.SkillsFolders)
                {
                    SkillFolderlistView.Items.Add(new ListViewItem(p));
                }

                // 4. 状态栏显示
                if (!ConfigManager.IsConfigValid)
                {
                    toolStripStatusLabel1.Text = "配置文件缺少字段！请在欢迎窗口重新配置并验证可用性";
                }
                else
                {
                    toolStripStatusLabel1.Text = $"当前模型: {ConfigManager.ModelName}, 协议: {ConfigManager.Protocol}";
                    toolStripStatusLabel2.Text = ConfigManager.ApiUrl;
                }

                // 根据已加载的 THINKING_DEEPTH 设置菜单项的图标（先清除再设置）
                try
                {
                    无ToolStripMenuItem.Image = null;
                    低ToolStripMenuItem.Image = null;
                    中ToolStripMenuItem.Image = null;
                    高ToolStripMenuItem.Image = null;
                    深入研究ToolStripMenuItem.Image = null;

                    switch (ConfigManager.ThinkingDeepth)
                    {
                        case 0:
                            无ToolStripMenuItem.Image = Properties.Resources.OK;
                            break;
                        case 1:
                            低ToolStripMenuItem.Image = Properties.Resources.OK;
                            break;
                        case 2:
                            中ToolStripMenuItem.Image = Properties.Resources.OK;
                            break;
                        case 3:
                            高ToolStripMenuItem.Image = Properties.Resources.OK;
                            break;
                        case 4:
                            深入研究ToolStripMenuItem.Image = Properties.Resources.OK;
                            break;
                    }
                }
                catch
                {
                    // 忽略资源缺失或设置失败
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("应用配置到UI时发生错误：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                toolStripStatusLabel1.Text = "应用配置失败";
            }
        }

        private void LoadTaskSch()
        {
            TaskSchEngine.CreateTaskSchFolder();

            // 清空现有列表项
            SchTaskListView.Items.Clear();

            // Console.WriteLine("当前共有" + TaskSchEngine.ListTaskFolderTasks().Count + "个计划任务。");

            foreach (TaskInfo task in TaskSchEngine.ListTaskFolderTasks())
            {
                // 将任务的五个字段添加为 ListViewItem 的列
                var item = new ListViewItem(task.Name ?? string.Empty);
                item.SubItems.Add(task.Triggers ?? string.Empty);
                item.SubItems.Add(task.Description ?? string.Empty);
                string taskdetail = task.Actions.ToString().Split(new[] { "--task " }, 2, StringSplitOptions.None).LastOrDefault();
                item.SubItems.Add(taskdetail ?? string.Empty);
                item.SubItems.Add(task.NextRunTime?.ToString() ?? string.Empty);

                SchTaskListView.Items.Add(item);

                // 保留控制台输出以便调试
                // Console.WriteLine($"{task.Name} {task.Triggers} {task.Description} {task.Actions} {task.NextRunTime}");
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            ChatListView.MultiSelect = true;
            LoadConfig();    // 加载配置文件
            LoadMarkdownFiles(); // 加载对话记录列表

            MemoryListView.Columns[0].Width = -2;
        }

        public void UpdateVisionImage(Bitmap bmp)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => UpdateVisionImage(bmp)));
                return;
            }

            // 先拿到旧图的引用
            Image oldImage = pictureBox1.Image;

            // 断开引用
            pictureBox1.Image = null;

            // 立即手动销毁旧图，释放 GDI 句柄
            if (oldImage != null)
            {
                oldImage.Dispose();
                oldImage = null;
            }

            // 显示新图（克隆一份，防止源图被释放导致这里显示红叉）
            if (bmp != null)
            {
                pictureBox1.Image = new Bitmap(bmp);
            }
        }

        private void 关于晓予ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string appName = "Windows晓予 0.1(Beta)";
            string otherStuff = "https://github.com/fsquirt/XiaoYu-Windows-Agent\n基于无障碍接口让LLM操作Windows";
            IntPtr iconHandle = this.Icon != null ? this.Icon.Handle : IntPtr.Zero;

            // 弹出 Windows 经典的关于对话框
            ShellAbout(this.Handle, appName, otherStuff, iconHandle);
        }

        private void 新建任务ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ChatForm chatForm = new ChatForm(this);
            chatForm.ShowDialog();

            chatForm = null;
            GC.WaitForFullGCApproach();
            GC.Collect();
        }

        [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int ShellAbout(IntPtr hWnd, string szApp, string szOtherStuff, IntPtr hIcon);

        private void button5_Click(object sender, EventArgs e)
        {
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

        private void button6_Click(object sender, EventArgs e)
        {
            Process.Start("notepad.exe", Path.GetFullPath("MarkDown\\SystemPrompt.md"));
        }

        private void AddSkillsPathButton_Click(object sender, EventArgs e)
        {
            // 弹窗让用户选择一个文件夹，选完后把路径显示在 SkillFolderlistView 的 SkillsFolderPath 列里面
            try
            {
                using (var dlg = new FolderBrowserDialog())
                {
                    dlg.Description = "请选择 Skills 所在文件夹";
                    dlg.ShowNewFolderButton = true;

                    if (dlg.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.SelectedPath))
                    {
                        string path = dlg.SelectedPath;

                        // 检查是否已存在相同路径（避免重复添加）
                        bool exists = false;
                        foreach (ListViewItem it in SkillFolderlistView.Items)
                        {
                            if (string.Equals(it.Text, path, StringComparison.OrdinalIgnoreCase))
                            {
                                exists = true;
                                break;
                            }
                        }

                        if (exists)
                        {
                            MessageBox.Show("该路径已存在于列表中。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }

                        var item = new ListViewItem(path);
                        SkillFolderlistView.Items.Add(item);
                        SyncSkills(); // 同步
                        // 选中并确保可见
                        item.Selected = true;
                        item.Focused = true;
                        item.EnsureVisible();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"添加 Skills 路径时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        private void SyncSkills()
        {
            // 同步界面上的路径列表到 ConfigManager
            ConfigManager.SkillsFolders = SkillFolderlistView.Items.Cast<ListViewItem>()
                                               .Select(item => item.Text)
                                               .ToArray();

            ConfigManager.EnableSkills = IsUseAgentSkills.Checked;

            // 直接调用全局的保存方法
            ConfigManager.SaveConfig();
        }

        // 辅助方法：在特定节之后更新或插入行
        private void UpdateOrInsertConfigLine(List<string> lines, int sectionIndex, string keyPrefix, string newLine)
        {
            for (int i = sectionIndex + 1; i < lines.Count; i++)
            {
                if (lines[i].Trim().StartsWith("[")) break; // 到了下一个节还没找到
                if (lines[i].Trim().StartsWith(keyPrefix))
                {
                    lines[i] = newLine;
                    return;
                }
            }
            lines.Insert(sectionIndex + 1, newLine);
        }

        private void button7_Click(object sender, EventArgs e)
        {
            Process.Start("notepad.exe", Path.GetFullPath("config.ini"));
        }



        private void InitCheckBoxEvents()
        {
            var chkDeepThink = this.Controls.Find("IsDeepThinkMode", true).FirstOrDefault() as CheckBox;
            if (chkDeepThink != null)
            {
                chkDeepThink.CheckedChanged += (s, e) =>
                {
                    ConfigManager.IsDeepThinkMode = chkDeepThink.Checked;
                    ConfigManager.SaveConfig();
                };
            }

            var chkDelPic = this.Controls.Find("IsDeleteHistoryPic", true).FirstOrDefault() as CheckBox;
            if (chkDelPic != null)
            {
                chkDelPic.CheckedChanged += (s, e) =>
                {
                    ConfigManager.IsDeleteHistoryPic = chkDelPic.Checked;
                    ConfigManager.SaveConfig();
                };
            }

            var chkHideUIA = this.Controls.Find("IsHideUIAoutInChatForm", true).FirstOrDefault() as CheckBox;
            if (chkHideUIA != null)
            {
                chkHideUIA.CheckedChanged += (s, e) =>
                {
                    ConfigManager.IsHideUIAoutInChatForm = chkHideUIA.Checked;
                    ConfigManager.SaveConfig();
                };
            }
        }

        // 原始的 Skills 勾选事件，附带安全警告
        private void IsUseAgentSkills_CheckedChanged(object sender, EventArgs e)
        {
            if (IsUseAgentSkills.Checked)
            {
                MessageBox.Show("启用此选项后，将会使用Skills，虽然Microsoft Agent Framework暂时不支持脚本型Skills，但是此程序是直接操作这台计算机!!\n仅使用来自可信来源的技能。技能指令会注入到智能体的上下文中，并可能影响智能体的行为。", "安全性警告!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            ConfigManager.EnableSkills = IsUseAgentSkills.Checked;
            ConfigManager.SaveConfig();
        }

        private void InitSkillsContextMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem deleteItem = new ToolStripMenuItem("移除路径");
            deleteItem.Click += (s, e) =>
            {
                if (SkillFolderlistView.SelectedItems.Count > 0)
                {
                    foreach (ListViewItem item in SkillFolderlistView.SelectedItems)
                        SkillFolderlistView.Items.Remove(item);
                    SyncSkills(); // 同步
                }
            };
            menu.Items.Add(deleteItem);
            SkillFolderlistView.ContextMenuStrip = menu;
        }

        // 计划任务列表右键菜单删除任务
        private void InitSchTaskContextMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem deleteItem = new ToolStripMenuItem("删除选中任务");
            deleteItem.Click += (s, e) =>
            {
                if (SchTaskListView.SelectedItems.Count == 0) return;

                int count = SchTaskListView.SelectedItems.Count;
                var result = MessageBox.Show($"确定要删除选中的 {count} 个计划任务吗？", "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result != DialogResult.Yes) return;

                // 先把选中的项拷贝到临时列表以避免枚举时修改集合
                var selected = new List<ListViewItem>();
                foreach (ListViewItem it in SchTaskListView.SelectedItems) selected.Add(it);

                foreach (var it in selected)
                {
                    try
                    {
                        string taskName = it.Text;
                        TaskSchEngine.DeleteTask(taskName);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"删除任务 {it.Text} 失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }

                // 刷新列表
                LoadTaskSch();
            };

            menu.Items.Add(deleteItem);
            SchTaskListView.ContextMenuStrip = menu;
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControl1.SelectedTab.Text == "计划任务")
            {
                LoadTaskSch();
            }
            if (tabControl1.SelectedTab.Text == "记忆")
            {
                RefreshMemoryListView();
            }
            if (tabControl1.SelectedTab.Text == "对话记录")
            {
                LoadMarkdownFiles();
            }
        }

        private void 设置ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            tabControl1.SelectedIndex = 5;
        }

        // 处理收到的计划任务
        private void HandleIncomingTask(string task)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => HandleIncomingTask(task)));
                return;
            }

            // 检查是否有打开的 ChatForm
            if (Application.OpenForms.OfType<ChatForm>().Any())
            {
                var res = MessageBox.Show($"收到计划任务：[{task}]\n当前存在正在进行的聊天会话，是否允许计划任务后台执行？(这可能会抢夺您的鼠标)",
                    "计划任务", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);

                if (res == DialogResult.Cancel)
                {
                    Console.WriteLine("用户取消了计划任务的执行。");
                    return;
                }
            }

            // 后台无头执行
            ExecuteHeadlessTask(task);
        }

        private async void ExecuteHeadlessTask(string task)
        {
            Console.WriteLine($"\n========== 开始执行后台计划任务 ==========\n任务: {task}");

            AgentRunner runner = new AgentRunner();

            // 挂载控制台输出 (由于Console重定向了，这里会直接输出到LogrichTextBox1)
            runner.OnStreamText += txt => Console.Write(txt);
            runner.OnToolCall += tool => Console.WriteLine($"\n[调用工具: {tool}]");
            runner.OnToolResult += (tool, res) => {
                if (!ConfigManager.IsHideUIAoutInChatForm) Console.WriteLine($"\n[工具结果]:\n{res}");
            };
            runner.OnLog += (role, msg) => Console.WriteLine($"\n[{role}] {msg}");

            // 释放图片内存防止泄漏
            runner.OnImageScanned += (drawn, orig) => {
                drawn?.Dispose();
                orig?.Dispose();
            };

            await runner.RunTaskAsync(task);

            Console.WriteLine($"\n========== 计划任务执行完毕 ==========\n");
            runner.Dispose();
        }

        private void 退出ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }

        private void 保存日志ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // 弹出保存文件对话框并将 LogrichTextBox1 的内容保存为文本文件
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "文本文件|*.txt|所有文件|*.*";
            saveFileDialog.RestoreDirectory = true;
            saveFileDialog.FileName = "log.txt";

            if (saveFileDialog.ShowDialog() != DialogResult.OK)
                return;

            try
            {
                string path = saveFileDialog.FileName;
                // 使用 UTF-8 编码保存文件
                File.WriteAllText(path, LogrichTextBox1.Text ?? string.Empty, Encoding.UTF8);
                MessageBox.Show($"日志已保存到: {path}", "保存成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存日志时发生错误: {ex.Message}", "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshMemoryListView()
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(RefreshMemoryListView));
                return;
            }

            MemoryListView.Items.Clear();
            foreach (var mem in MemoryManager.Memories)
            {
                MemoryListView.Items.Add(new ListViewItem(mem));
            }
        }

        private void InitMemoryContextMenu()
        {
            ContextMenuStrip memoryMenu = new ContextMenuStrip();

            ToolStripMenuItem deleteItem = new ToolStripMenuItem("删除选中记忆");
            deleteItem.Click += (s, e) =>
            {
                // 如果没有选中任何项，直接返回
                if (MemoryListView.SelectedItems.Count == 0) return;

                // 询问用户是否确定删除
                var result = MessageBox.Show(
                    $"确定要删除选中的 {MemoryListView.SelectedItems.Count} 条记忆吗？\n删除后 AI 将不再参考这些经验。",
                    "确认删除",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    // 收集所有需要删除的文本
                    var itemsToDelete = new List<string>();
                    foreach (ListViewItem item in MemoryListView.SelectedItems)
                    {
                        itemsToDelete.Add(item.Text);
                    }

                    // 从管理器中逐个删除
                    foreach (string text in itemsToDelete)
                    {
                        MemoryManager.RemoveMemory(text);
                    }

                    // RemoveMemory 内部调用了 SaveMemories，它会触发 OnMemoriesChanged，进而调用 RefreshMemoryListView，所以不需要在这里手动操作 UI 元素。
                }
            };

            memoryMenu.Items.Add(deleteItem);

            // 将菜单绑定到 ListView
            MemoryListView.ContextMenuStrip = memoryMenu;

            // 让 ListView 支持多选，方便批量删除
            MemoryListView.MultiSelect = true;
            MemoryListView.FullRowSelect = true;
        }

        private void SaveConfigButton_Click(object sender, EventArgs e)
        {
            ConfigManager.ApiKey = ApiKeyTextBox.Text;
            ConfigManager.ApiUrl = ApiUrlTextbox.Text;
            ConfigManager.ModelName = ModelNameTextBox.Text;
            ConfigManager.SaveConfig();
        }

        private async void VerifyConfigButton_Click(object sender, EventArgs e)
        {
            try
            {
                string API_URL = ApiUrlTextbox.Text;
                string API_KEY = ApiKeyTextBox.Text;
                string MODEL_NAME = ModelNameTextBox.Text;

                if (IsOpenAICheckBox.Checked)
                {
                    ChatClient client = new ChatClient(
                        model: MODEL_NAME,
                        credential: new ApiKeyCredential(API_KEY),
                        options: new OpenAIClientOptions()
                        {
                            Endpoint = new Uri(API_URL)
                        });

                    ChatCompletion completion = client.CompleteChat("速速回我任意内容，我正在测试和你的聊天API是否正常");
                    MessageBox.Show("验证成功！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    VerifyConfigButton.Text = "验证通过";

                    client = null;
                    completion = null;
                }
                else if (IsAnthropicCheckBox.Checked)
                {
                    AnthropicClient client = new AnthropicClient { ApiKey = API_KEY, BaseUrl = API_URL };
                    IChatClient chatClient = client.AsIChatClient(MODEL_NAME)
                        .AsBuilder()
                        .UseFunctionInvocation()
                        .Build();
                    var response = await chatClient.GetResponseAsync("速速回我任意内容，我正在测试和你的聊天API是否正常");
                    MessageBox.Show("验证成功！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    VerifyConfigButton.Text = "验证通过";

                    client = null;
                    chatClient = null;
                }
                else
                {
                    MessageBox.Show("请先选择协议类型（OpenAI或Anthropic）", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show("发生错误：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                VerifyConfigButton.Text = "失败";
            }

        }

        private void StopButton_Click(object sender, EventArgs e)
        {
            //寻找并直接关闭所有AgnetRunner实例，包括计划任务和ChatForm创建的
            try
            {
                // 先尝试温和地取消所有任务
                AgentEngine.AgentRunner.CancelAll();

                // 再强制终止并释放所有实例
                AgentEngine.AgentRunner.TerminateAll();

                Console.WriteLine("已终止所有 AgentRunner 实例。");
                toolStripStatusLabel1.Text = "已终止所有任务";
            }
            catch (Exception ex)
            {
                Console.WriteLine("终止所有 AgentRunner 时出错: " + ex.Message);
                toolStripStatusLabel1.Text = "终止任务时出错";
            }
        }

        // 统一的思考深度菜单处理器，五个选项共用一个函数（类似 InitCheckBoxEvents 的做法）
        private void SetThinkingDepth_Click(object sender, EventArgs e)
        {
            if (!(sender is ToolStripMenuItem item)) return;

            // 清除所有项的图标
            无ToolStripMenuItem.Image = null;
            低ToolStripMenuItem.Image = null;
            中ToolStripMenuItem.Image = null;
            高ToolStripMenuItem.Image = null;
            深入研究ToolStripMenuItem.Image = null;

            switch (item.Name)
            {
                case "无ToolStripMenuItem":
                    ConfigManager.ThinkingDeepth = 0;
                    break;
                case "低ToolStripMenuItem":
                    ConfigManager.ThinkingDeepth = 1;
                    break;
                case "中ToolStripMenuItem":
                    ConfigManager.ThinkingDeepth = 2;
                    break;
                case "高ToolStripMenuItem":
                    ConfigManager.ThinkingDeepth = 3;
                    break;
                case "深入研究ToolStripMenuItem":
                    ConfigManager.ThinkingDeepth = 4;
                    break;
                default:
                    return;
            }

            ConfigManager.SaveConfig();

            // 设置被选中项的图标为资源中的 OK（若不存在会抛出资源错误）
            try
            {
                item.Image = Properties.Resources.OK;
            }
            catch
            {
                // 忽略无资源或其他设置失败的情况
            }
        }

        private void 刷新ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LogrichTextBox1.Text = "";
        }

        private void 查看帮助ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //打开网页 https://www.cloudyou.top/xiaoyu-help/
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://www.cloudyou.top/xiaoyu-help/",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开帮助页面: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void 接入QQToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            AeroSetupQQWizard aeroSetupQQWizard = new AeroSetupQQWizard();
            aeroSetupQQWizard.ShowDialog();
        }

        private void IsUseTencentQQ_CheckedChanged(object sender, EventArgs e)
        {
            if (IsUseTencentQQ.Checked)
            {
                // 检查配置是否完整
                if (ConfigManager.QqAdminQQ == 0 || string.IsNullOrEmpty(ConfigManager.QqBotUrl))
                {
                    MessageBox.Show("请先在设置向导或配置文件中填写完整的 QQ 机器人配置！", "配置缺失", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    IsUseTencentQQ.Checked = false;
                    return;
                }

                // 启动服务
                _qqService.StartAsync();
                toolStripStatusLabel1.Text = "QQ 监听服务已启动";
            }
            else
            {
                // 停止服务
                _qqService.Stop();
                toolStripStatusLabel1.Text = "QQ 监听服务已停止";
            }
        }

        private void 审计日志ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var auditForm = new ToolForm.AuditLogForm();
            auditForm.Show(this);
        }
    }
}
