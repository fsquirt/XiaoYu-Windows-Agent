using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using XiaoYu_LAM.AgentEngine;

namespace XiaoYu_LAM.ToolForm
{
    public partial class AuditLogForm : Form
    {
        private List<AuditEntry> _currentEntries = new List<AuditEntry>();
        private DateTime _selectedDate = DateTime.Now;

        public AuditLogForm()
        {
            InitializeComponent();
            LoadAvailableDates();
            LoadLogsForDate(_selectedDate);
            
            // 订阅实时更新
            AuditLogger.OnEntryAdded += OnAuditEntryAdded;
        }

        private void OnAuditEntryAdded(AuditEntry entry)
        {
            if (this.IsDisposed) return;
            
            // 只在显示当天日志时才更新
            if (entry.Timestamp.Date == _selectedDate.Date)
            {
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(new Action(() => OnAuditEntryAdded(entry)));
                    return;
                }
                
                _currentEntries.Add(entry);
                AddEntryToListView(entry);
            }
        }

        private void LoadAvailableDates()
        {
            dateComboBox.Items.Clear();
            
            var files = AuditLogger.GetAvailableLogFiles();
            var dates = new HashSet<DateTime>();
            
            // 添加今天
            dates.Add(DateTime.Today);
            
            // 从文件名解析日期
            foreach (var file in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (fileName.StartsWith("audit_") && fileName.Length >= 14)
                {
                    var dateStr = fileName.Substring(6, 8);
                    if (DateTime.TryParseExact(dateStr, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var date))
                    {
                        dates.Add(date);
                    }
                }
            }
            
            // 按日期倒序排列
            var sortedDates = new List<DateTime>(dates);
            sortedDates.Sort((a, b) => b.CompareTo(a));
            
            foreach (var date in sortedDates)
            {
                dateComboBox.Items.Add(date.ToString("yyyy-MM-dd"));
            }
            
            if (dateComboBox.Items.Count > 0)
            {
                dateComboBox.SelectedIndex = 0;
            }
        }

        private void LoadLogsForDate(DateTime date)
        {
            _selectedDate = date;
            logListView.Items.Clear();
            _currentEntries.Clear();
            
            // 先从文件加载
            var entries = AuditLogger.LoadFromFile(date);
            _currentEntries.AddRange(entries);
            
            // 如果是今天，再加上内存中的记录
            if (date.Date == DateTime.Today)
            {
                var recentEntries = AuditLogger.GetRecentEntries(500);
                foreach (var entry in recentEntries)
                {
                    if (entry.Timestamp.Date == date.Date && !_currentEntries.Contains(entry))
                    {
                        _currentEntries.Add(entry);
                    }
                }
            }
            
            // 按时间排序
            _currentEntries.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
            
            // 显示到列表
            foreach (var entry in _currentEntries)
            {
                AddEntryToListView(entry);
            }
            
            // 更新统计
            UpdateStatistics();
        }

        private void AddEntryToListView(AuditEntry entry)
        {
            var item = new ListViewItem(entry.Timestamp.ToString("HH:mm:ss"));
            item.SubItems.Add(entry.SessionId ?? "-");
            item.SubItems.Add(entry.EventTypeDisplay);
            item.SubItems.Add(entry.Source ?? "-");
            
            var msg = entry.Message ?? "";
            if (msg.Length > 100) msg = msg.Substring(0, 100) + "...";
            item.SubItems.Add(msg);
            
            // 根据事件类型设置颜色
            switch (entry.EventType)
            {
                case AuditEventType.Error:
                case AuditEventType.ToolError:
                    item.BackColor = Color.FromArgb(255, 230, 230);
                    item.ForeColor = Color.DarkRed;
                    break;
                case AuditEventType.SessionStart:
                    item.BackColor = Color.FromArgb(230, 255, 230);
                    item.Font = new Font(logListView.Font, FontStyle.Bold);
                    break;
                case AuditEventType.SessionEnd:
                    item.BackColor = Color.FromArgb(230, 230, 255);
                    item.Font = new Font(logListView.Font, FontStyle.Bold);
                    break;
                case AuditEventType.UserIntervention:
                    item.BackColor = Color.FromArgb(255, 255, 200);
                    break;
                case AuditEventType.UIAOperation:
                    item.BackColor = Color.FromArgb(240, 248, 255);
                    break;
            }
            
            item.Tag = entry;
            logListView.Items.Add(item);
            
            // 自动滚动到最新
            if (logListView.Items.Count > 0)
            {
                logListView.EnsureVisible(logListView.Items.Count - 1);
            }
        }

        private void UpdateStatistics()
        {
            int sessionCount = 0;
            int toolCallCount = 0;
            int errorCount = 0;
            int uiaOpCount = 0;
            
            var sessions = new HashSet<string>();
            
            foreach (var entry in _currentEntries)
            {
                switch (entry.EventType)
                {
                    case AuditEventType.SessionStart:
                        sessions.Add(entry.SessionId);
                        break;
                    case AuditEventType.ToolCall:
                        toolCallCount++;
                        break;
                    case AuditEventType.Error:
                    case AuditEventType.ToolError:
                        errorCount++;
                        break;
                    case AuditEventType.UIAOperation:
                        uiaOpCount++;
                        break;
                }
            }
            
            sessionCount = sessions.Count;
            
            statsLabel.Text = $"统计: 会话 {sessionCount} | 工具调用 {toolCallCount} | UIA操作 {uiaOpCount} | 错误 {errorCount} | 总记录 {_currentEntries.Count}";
        }

        private void dateComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (dateComboBox.SelectedItem != null)
            {
                if (DateTime.TryParse(dateComboBox.SelectedItem.ToString(), out var date))
                {
                    LoadLogsForDate(date);
                }
            }
        }

        private void logListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (logListView.SelectedItems.Count > 0)
            {
                var entry = logListView.SelectedItems[0].Tag as AuditEntry;
                if (entry != null)
                {
                    detailTextBox.Text = $"时间: {entry.Timestamp:yyyy-MM-dd HH:mm:ss}\r\n" +
                                          $"会话ID: {entry.SessionId}\r\n" +
                                          $"事件类型: {entry.EventTypeDisplay}\r\n" +
                                          $"来源: {entry.Source}\r\n" +
                                          $"详细信息:\r\n{entry.Message}";
                }
            }
            else
            {
                detailTextBox.Text = "";
            }
        }

        private void filterComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            logListView.Items.Clear();
            
            var filterType = filterComboBox.SelectedIndex;
            
            foreach (var entry in _currentEntries)
            {
                bool show = filterType switch
                {
                    0 => true, // 全部
                    1 => entry.EventType == AuditEventType.SessionStart || entry.EventType == AuditEventType.SessionEnd,
                    2 => entry.EventType == AuditEventType.ToolCall || entry.EventType == AuditEventType.ToolResult || entry.EventType == AuditEventType.ToolError,
                    3 => entry.EventType == AuditEventType.UIAOperation,
                    4 => entry.EventType == AuditEventType.Error || entry.EventType == AuditEventType.ToolError,
                    5 => entry.EventType == AuditEventType.UserIntervention,
                    _ => true
                };
                
                if (show)
                {
                    AddEntryToListView(entry);
                }
            }
        }

        private void refreshButton_Click(object sender, EventArgs e)
        {
            LoadLogsForDate(_selectedDate);
        }

        private void exportButton_Click(object sender, EventArgs e)
        {
            using (var saveDlg = new SaveFileDialog())
            {
                saveDlg.Filter = "文本文件|*.txt|JSON文件|*.json|CSV文件|*.csv";
                saveDlg.FileName = $"audit_log_{_selectedDate:yyyyMMdd}";
                
                if (saveDlg.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var ext = System.IO.Path.GetExtension(saveDlg.FileName).ToLower();
                        
                        if (ext == ".json")
                        {
                            var json = System.Text.Json.JsonSerializer.Serialize(_currentEntries, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                            File.WriteAllText(saveDlg.FileName, json);
                        }
                        else if (ext == ".csv")
                        {
                            var sb = new System.Text.StringBuilder();
                            sb.AppendLine("时间,会话ID,事件类型,来源,信息");
                            foreach (var entry in _currentEntries)
                            {
                                sb.AppendLine($"\"{entry.Timestamp:yyyy-MM-dd HH:mm:ss}\",\"{entry.SessionId}\",\"{entry.EventTypeDisplay}\",\"{entry.Source}\",\"{entry.Message?.Replace("\"", "\"\"")}\"");
                            }
                            File.WriteAllText(saveDlg.FileName, sb.ToString(), System.Text.Encoding.UTF8);
                        }
                        else
                        {
                            var sb = new System.Text.StringBuilder();
                            sb.AppendLine($"审计日志导出 - {_selectedDate:yyyy-MM-dd}");
                            sb.AppendLine(new string('=', 80));
                            foreach (var entry in _currentEntries)
                            {
                                sb.AppendLine($"[{entry.Timestamp:HH:mm:ss}] [{entry.SessionId}] [{entry.EventTypeDisplay}] {entry.Source}: {entry.Message}");
                            }
                            File.WriteAllText(saveDlg.FileName, sb.ToString(), System.Text.Encoding.UTF8);
                        }
                        
                        MessageBox.Show($"导出成功！共 {_currentEntries.Count} 条记录", "导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void clearTodayButton_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "确定要清除今天内存中的审计日志缓存吗？\n文件中的历史记录不会删除。",
                "确认清除",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            
            if (result == DialogResult.Yes)
            {
                AuditLogger.ClearMemoryCache();
                LoadLogsForDate(_selectedDate);
                MessageBox.Show("已清除内存缓存", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            AuditLogger.OnEntryAdded -= OnAuditEntryAdded;
            base.OnFormClosed(e);
        }
    }
}
