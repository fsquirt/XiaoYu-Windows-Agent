namespace XiaoYu_LAM.ToolForm
{
    partial class AuditLogForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.headerPanel = new System.Windows.Forms.Panel();
            this.statsLabel = new System.Windows.Forms.Label();
            this.clearTodayButton = new System.Windows.Forms.Button();
            this.exportButton = new System.Windows.Forms.Button();
            this.refreshButton = new System.Windows.Forms.Button();
            this.filterComboBox = new System.Windows.Forms.ComboBox();
            this.filterLabel = new System.Windows.Forms.Label();
            this.dateComboBox = new System.Windows.Forms.ComboBox();
            this.dateLabel = new System.Windows.Forms.Label();
            this.splitContainer = new System.Windows.Forms.SplitContainer();
            this.logListView = new System.Windows.Forms.ListView();
            this.columnHeaderTime = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeaderSession = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeaderType = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeaderSource = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeaderMessage = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.detailTextBox = new System.Windows.Forms.TextBox();
            this.headerPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            this.SuspendLayout();
            // 
            // headerPanel
            // 
            this.headerPanel.Controls.Add(this.statsLabel);
            this.headerPanel.Controls.Add(this.clearTodayButton);
            this.headerPanel.Controls.Add(this.exportButton);
            this.headerPanel.Controls.Add(this.refreshButton);
            this.headerPanel.Controls.Add(this.filterComboBox);
            this.headerPanel.Controls.Add(this.filterLabel);
            this.headerPanel.Controls.Add(this.dateComboBox);
            this.headerPanel.Controls.Add(this.dateLabel);
            this.headerPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.headerPanel.Location = new System.Drawing.Point(0, 0);
            this.headerPanel.Name = "headerPanel";
            this.headerPanel.Size = new System.Drawing.Size(900, 50);
            this.headerPanel.TabIndex = 0;
            // 
            // statsLabel
            // 
            this.statsLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.statsLabel.Location = new System.Drawing.Point(540, 28);
            this.statsLabel.Name = "statsLabel";
            this.statsLabel.Size = new System.Drawing.Size(350, 20);
            this.statsLabel.TabIndex = 7;
            this.statsLabel.Text = "统计: 加载中...";
            // 
            // clearTodayButton
            // 
            this.clearTodayButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.clearTodayButton.Location = new System.Drawing.Point(813, 6);
            this.clearTodayButton.Name = "clearTodayButton";
            this.clearTodayButton.Size = new System.Drawing.Size(75, 23);
            this.clearTodayButton.TabIndex = 6;
            this.clearTodayButton.Text = "清缓存";
            this.clearTodayButton.UseVisualStyleBackColor = true;
            this.clearTodayButton.Click += new System.EventHandler(this.clearTodayButton_Click);
            // 
            // exportButton
            // 
            this.exportButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.exportButton.Location = new System.Drawing.Point(732, 6);
            this.exportButton.Name = "exportButton";
            this.exportButton.Size = new System.Drawing.Size(75, 23);
            this.exportButton.TabIndex = 5;
            this.exportButton.Text = "导出";
            this.exportButton.UseVisualStyleBackColor = true;
            this.exportButton.Click += new System.EventHandler(this.exportButton_Click);
            // 
            // refreshButton
            // 
            this.refreshButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.refreshButton.Location = new System.Drawing.Point(651, 6);
            this.refreshButton.Name = "refreshButton";
            this.refreshButton.Size = new System.Drawing.Size(75, 23);
            this.refreshButton.TabIndex = 4;
            this.refreshButton.Text = "刷新";
            this.refreshButton.UseVisualStyleBackColor = true;
            this.refreshButton.Click += new System.EventHandler(this.refreshButton_Click);
            // 
            // filterComboBox
            // 
            this.filterComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)));
            this.filterComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.filterComboBox.FormattingEnabled = true;
            this.filterComboBox.Items.AddRange(new object[] {
            "全部",
            "会话",
            "工具调用",
            "UIA操作",
            "错误",
            "用户干预"});
            this.filterComboBox.Location = new System.Drawing.Point(500, 7);
            this.filterComboBox.Name = "filterComboBox";
            this.filterComboBox.Size = new System.Drawing.Size(100, 21);
            this.filterComboBox.TabIndex = 3;
            this.filterComboBox.SelectedIndexChanged += new System.EventHandler(this.filterComboBox_SelectedIndexChanged);
            // 
            // filterLabel
            // 
            this.filterLabel.AutoSize = true;
            this.filterLabel.Location = new System.Drawing.Point(460, 10);
            this.filterLabel.Name = "filterLabel";
            this.filterLabel.Size = new System.Drawing.Size(35, 12);
            this.filterLabel.TabIndex = 2;
            this.filterLabel.Text = "筛选:";
            // 
            // dateComboBox
            // 
            this.dateComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)));
            this.dateComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.dateComboBox.FormattingEnabled = true;
            this.dateComboBox.Location = new System.Drawing.Point(80, 7);
            this.dateComboBox.Name = "dateComboBox";
            this.dateComboBox.Size = new System.Drawing.Size(120, 21);
            this.dateComboBox.TabIndex = 1;
            this.dateComboBox.SelectedIndexChanged += new System.EventHandler(this.dateComboBox_SelectedIndexChanged);
            // 
            // dateLabel
            // 
            this.dateLabel.AutoSize = true;
            this.dateLabel.Location = new System.Drawing.Point(12, 10);
            this.dateLabel.Name = "dateLabel";
            this.dateLabel.Size = new System.Drawing.Size(65, 12);
            this.dateLabel.TabIndex = 0;
            this.dateLabel.Text = "选择日期:";
            // 
            // splitContainer
            // 
            this.splitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer.Location = new System.Drawing.Point(0, 50);
            this.splitContainer.Name = "splitContainer";
            this.splitContainer.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer.Panel1
            // 
            this.splitContainer.Panel1.Controls.Add(this.logListView);
            // 
            // splitContainer.Panel2
            // 
            this.splitContainer.Panel2.Controls.Add(this.detailTextBox);
            this.splitContainer.Size = new System.Drawing.Size(900, 450);
            this.splitContainer.SplitterDistance = 300;
            this.splitContainer.TabIndex = 1;
            // 
            // logListView
            // 
            this.logListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeaderTime,
            this.columnHeaderSession,
            this.columnHeaderType,
            this.columnHeaderSource,
            this.columnHeaderMessage});
            this.logListView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.logListView.FullRowSelect = true;
            this.logListView.GridLines = true;
            this.logListView.Location = new System.Drawing.Point(0, 0);
            this.logListView.Name = "logListView";
            this.logListView.Size = new System.Drawing.Size(900, 300);
            this.logListView.TabIndex = 0;
            this.logListView.UseCompatibleStateImageBehavior = false;
            this.logListView.View = System.Windows.Forms.View.Details;
            this.logListView.SelectedIndexChanged += new System.EventHandler(this.logListView_SelectedIndexChanged);
            // 
            // columnHeaderTime
            // 
            this.columnHeaderTime.Text = "时间";
            this.columnHeaderTime.Width = 80;
            // 
            // columnHeaderSession
            // 
            this.columnHeaderSession.Text = "会话ID";
            this.columnHeaderSession.Width = 80;
            // 
            // columnHeaderType
            // 
            this.columnHeaderType.Text = "事件类型";
            this.columnHeaderType.Width = 90;
            // 
            // columnHeaderSource
            // 
            this.columnHeaderSource.Text = "来源";
            this.columnHeaderSource.Width = 100;
            // 
            // columnHeaderMessage
            // 
            this.columnHeaderMessage.Text = "信息";
            this.columnHeaderMessage.Width = 530;
            // 
            // detailTextBox
            // 
            this.detailTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.detailTextBox.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.detailTextBox.Location = new System.Drawing.Point(0, 0);
            this.detailTextBox.Multiline = true;
            this.detailTextBox.Name = "detailTextBox";
            this.detailTextBox.ReadOnly = true;
            this.detailTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.detailTextBox.Size = new System.Drawing.Size(900, 146);
            this.detailTextBox.TabIndex = 0;
            // 
            // AuditLogForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(900, 500);
            this.Controls.Add(this.splitContainer);
            this.Controls.Add(this.headerPanel);
            this.MinimumSize = new System.Drawing.Size(700, 400);
            this.Name = "AuditLogForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "审计日志";
            this.headerPanel.ResumeLayout(false);
            this.headerPanel.PerformLayout();
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel2.ResumeLayout(false);
            this.splitContainer.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).EndInit();
            this.splitContainer.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel headerPanel;
        private System.Windows.Forms.Label dateLabel;
        private System.Windows.Forms.ComboBox dateComboBox;
        private System.Windows.Forms.SplitContainer splitContainer;
        private System.Windows.Forms.ListView logListView;
        private System.Windows.Forms.ColumnHeader columnHeaderTime;
        private System.Windows.Forms.ColumnHeader columnHeaderSession;
        private System.Windows.Forms.ColumnHeader columnHeaderType;
        private System.Windows.Forms.ColumnHeader columnHeaderSource;
        private System.Windows.Forms.ColumnHeader columnHeaderMessage;
        private System.Windows.Forms.TextBox detailTextBox;
        private System.Windows.Forms.ComboBox filterComboBox;
        private System.Windows.Forms.Label filterLabel;
        private System.Windows.Forms.Button refreshButton;
        private System.Windows.Forms.Button exportButton;
        private System.Windows.Forms.Button clearTodayButton;
        private System.Windows.Forms.Label statsLabel;
    }
}
