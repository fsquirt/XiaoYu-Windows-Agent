using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// 请确保这里的命名空间与你的项目一致
// 根据你的报错图片，你的主程序集似乎叫 XiaoYu_LAM
// 如果报错提示找不到类，请将 YourProjectNamespace 改为 XiaoYu_LAM
namespace XiaoYu_LAM
{
    public class CommandLink : Button
    {
        // Windows 常量
        private const int BS_COMMANDLINK = 0x0000000E;
        private const int BCM_SETNOTE = 0x00001609;

        private string noteText = string.Empty;

        public CommandLink()
        {
            this.FlatStyle = FlatStyle.System;
        }

        [Category("Appearance")]
        [Description("Command Link 的补充说明文字（副标题）。")]
        [DefaultValue("")]
        public string NoteText
        {
            get { return noteText; }
            set
            {
                noteText = value;
                UpdateNoteText();
            }
        }

        // 重写 CreateParams 以添加 BS_COMMANDLINK 样式
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= BS_COMMANDLINK;
                return cp;
            }
        }

        // 更新副标题文本
        private void UpdateNoteText()
        {
            if (this.IsHandleCreated && !string.IsNullOrEmpty(noteText))
            {
                SendMessage(this.Handle, BCM_SETNOTE, IntPtr.Zero, noteText);
            }
        }

        // 当句柄创建时（例如窗口显示时）应用文本
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateNoteText();
        }

        // 修复部分：DllImport 必须单独一行，不能放在注释后面
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
    }
}