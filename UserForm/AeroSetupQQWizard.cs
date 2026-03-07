using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using XiaoYu_LAM.AgentEngine;

namespace XiaoYu_LAM.UserForm
{
    public partial class AeroSetupQQWizard : Form
    {
        public AeroSetupQQWizard()
        {
            InitializeComponent();
        }

        private void commandLink2_Click(object sender, EventArgs e)
        {
            if (wizardControl1 != null && wizardControl1.Pages != null && wizardControl1.Pages.Count > 2)
            {
                wizardControl1.NextPage(wizardControl1.Pages[1]);
            }
        }

        private void LLBotConfig_Commit(object sender, AeroWizard.WizardPageConfirmEventArgs e)
        {
            ConfigManager.QqBotUrl = textBox1.Text;
            ConfigManager.QqBotPort = textBox2.Text;
            ConfigManager.QqBotToken = textBox3.Text;
            ConfigManager.QqAdminQQ = long.Parse(textBox4.Text);
            ConfigManager.SaveConfig();
        }
    }
}
