using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using XiaoYu_LAM.AgentEngine;
using XiaoYu_LAM.ToolForm;

namespace XiaoYu_LAM
{
    public partial class MainForm : Form
    {
        // 静态实例，方便其他地方万一需要直接操作 MainForm（虽然用 Console 就够了）
        public static MainForm Instance;
        public MainForm()
        {
            InitializeComponent();
            // 绑定窗体加载事件
            this.Load += MainForm_Load;

            // 初始化右键菜单
            InitContextMenu();
            InitSkillsContextMenu();
            InitSchTaskContextMenu();

            Console.SetOut(new TextBoxWriter(this.LogrichTextBox1));
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
            openItem.Click += (s, e) => {
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
            deleteItem.Click += (s, e) => {
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
            // 从程序目录下的 config.ini 文件中读取配置
            try
            {
                string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
                if (System.IO.File.Exists(path))
                {
                    var lines = System.IO.File.ReadAllLines(path, System.Text.Encoding.UTF8);
                    foreach (var line in lines)
                    {
                        if (!line.Contains('=')) continue;
                        var parts = line.Split(new char[] { '=' }, 2);
                        string key = parts[0].Trim();
                        string value = parts[1].Trim();

                        if (key == "MODEL_NAME") MODEL_NAME = value;
                        else if (key == "PROTOCOL") PROTOCOL = value;
                        else if (key == "API_URL") API_URL = value;
                        else if (key == "API_KEY") API_KEY = value;
                        else if (key == "SKILLSPATH")
                        {
                            SkillsFolders = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                            SkillFolderlistView.Items.Clear();
                            foreach (var p in SkillsFolders) SkillFolderlistView.Items.Add(new ListViewItem(p));
                        }
                        else if (key == "ENABLE")
                        {
                            // 解析 ENABLE 值
                            UseAgentSkills = value.Equals("True", StringComparison.OrdinalIgnoreCase);
                            // 暂时断开事件绑定，防止 LoadConfig 时触发 CheckChanged 又跑去写文件
                            IsUseAgentSkills.CheckedChanged -= IsUseAgentSkills_CheckedChanged;
                            IsUseAgentSkills.Checked = UseAgentSkills;
                            IsUseAgentSkills.CheckedChanged += IsUseAgentSkills_CheckedChanged;
                        }
                    }

                    // 在状态栏显示当前使用的模型和协议
                    if ((MODEL_NAME == "") || (PROTOCOL == "") || (API_KEY == "") || (API_URL == ""))
                    {
                        toolStripStatusLabel1.Text = "配置文件缺少字段！请在欢迎窗口重新配置并验证可用性";
                    }
                    else
                    {
                        toolStripStatusLabel1.Text = $"当前模型: {MODEL_NAME}, 协议: {PROTOCOL}";
                        toolStripStatusLabel2.Text = API_URL;
                    }
                }
                else
                {
                    toolStripStatusLabel1.Text = "未找到配置文件 config.ini";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("读取配置文件时发生错误：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                toolStripStatusLabel1.Text = "读取配置文件失败";
            }

            //将配置显示到设置页面里
            textBox1.Text = API_URL;
            textBox3.Text = API_KEY;
            textBox2.Text = MODEL_NAME;
            if (PROTOCOL == "OpenAI")
            {
                IsOpenAICheckBox.Checked = true;
            }
            else if (PROTOCOL == "Anthropic")
            {
                IsAnthropicCheckBox.Checked = true;
            }
        }

        private void LoadTaskSch()
        {
            TaskSchEngine.CreateTaskSchFolder();

            // 清空现有列表项
            SchTaskListView.Items.Clear();

            foreach (TaskInfo task in TaskSchEngine.ListTaskFolderTasks())
            {
                // 将任务的五个字段添加为 ListViewItem 的列
                var item = new ListViewItem(task.Name ?? string.Empty);
                item.SubItems.Add(task.Triggers ?? string.Empty);
                item.SubItems.Add(task.Description ?? string.Empty);
                item.SubItems.Add(task.Actions ?? string.Empty);
                item.SubItems.Add(task.NextRunTime?.ToString() ?? string.Empty);

                SchTaskListView.Items.Add(item);

                // 保留控制台输出以便调试
                Console.WriteLine($"{task.Name} {task.Triggers} {task.Description} {task.Actions} {task.NextRunTime}");
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            ChatListView.MultiSelect = true;
            LoadConfig();    // 加载配置文件
            LoadMarkdownFiles(); // 加载对话记录列表
        }

        public void UpdateVisionImage(Bitmap bmp)
        {
            // 跨线程安全调用
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => UpdateVisionImage(bmp)));
                return;
            }
            Image oldImage = pictureBox1.Image;
            pictureBox1.Image = null;
            if (oldImage != null)
            {
                oldImage.Dispose();
            }
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
            // 1. 同步内存变量
            SkillsFolders = SkillFolderlistView.Items.Cast<ListViewItem>()
                                .Select(item => item.Text)
                                .ToArray();
            UseAgentSkills = IsUseAgentSkills.Checked;

            // 2. 准备写入 config.ini
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
            List<string> lines = File.Exists(path) ? File.ReadAllLines(path, Encoding.UTF8).ToList() : new List<string>();

            string skillsPathLine = "SKILLSPATH=" + string.Join(",", SkillsFolders);
            string enableLine = "ENABLE=" + (UseAgentSkills ? "True" : "False");

            // 查找或创建 [SKILLS] 节
            int sectionIndex = lines.FindIndex(l => l.Trim() == "[SKILLS]");
            if (sectionIndex == -1)
            {
                lines.Add("");
                lines.Add("[SKILLS]");
                lines.Add(enableLine);
                lines.Add(skillsPathLine);
            }
            else
            {
                // 更新或插入 ENABLE 和 SKILLSPATH
                UpdateOrInsertConfigLine(lines, sectionIndex, "ENABLE=", enableLine);
                UpdateOrInsertConfigLine(lines, sectionIndex, "SKILLSPATH=", skillsPathLine);
            }

            File.WriteAllLines(path, lines, Encoding.UTF8);
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

        private void IsUseAgentSkills_CheckedChanged(object sender, EventArgs e)
        {
            if(IsUseAgentSkills.Checked == true)
            {
                MessageBox.Show("启用此选项后，将会使用Skills，虽然Microsoft Agent Framework暂时不支持脚本型Skills，但是此程序是直接操作这台计算机!!\n仅使用来自可信来源的技能。技能指令会注入到智能体的上下文中，并可能影响智能体的行为。", "安全性警告!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            // 更新变量并存入文件
            UseAgentSkills = IsUseAgentSkills.Checked;
            SyncSkills();
        }

        private void InitSkillsContextMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem deleteItem = new ToolStripMenuItem("移除路径");
            deleteItem.Click += (s, e) => {
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
            deleteItem.Click += (s, e) => {
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
            if(tabControl1.SelectedTab.Text == "计划任务")
            {
                LoadTaskSch(); 
            }
        }
    }
}
