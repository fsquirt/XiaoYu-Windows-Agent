using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace XiaoYu_LAM.AgentEngine
{
    /// <summary>
    /// 操作审计日志记录器
    /// 记录所有 Agent 执行的操作，便于追溯和复盘
    /// </summary>
    public static class AuditLogger
    {
        private static readonly object _lock = new object();
        private static string AuditLogPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "audit_logs");
        private static string CurrentLogFile => Path.Combine(AuditLogPath, $"audit_{DateTime.Now:yyyyMMdd}.jsonl");

        // 内存中保留最近的记录，便于 UI 快速显示
        private static readonly List<AuditEntry> _recentEntries = new List<AuditEntry>();
        private const int MaxRecentEntries = 500;

        /// <summary>
        /// 触发此事件通知 UI 刷新审计日志列表
        /// </summary>
        public static event Action<AuditEntry> OnEntryAdded;

        /// <summary>
        /// 当前会话ID，用于关联同一次任务中的所有操作
        /// </summary>
        public static string CurrentSessionId { get; private set; }

        static AuditLogger()
        {
            EnsureLogDirectory();
        }

        private static void EnsureLogDirectory()
        {
            if (!Directory.Exists(AuditLogPath))
            {
                Directory.CreateDirectory(AuditLogPath);
            }
        }

        /// <summary>
        /// 开始新的审计会话
        /// </summary>
        public static void StartNewSession(string userTask)
        {
            CurrentSessionId = Guid.NewGuid().ToString("N").Substring(0, 8);
            Log(AuditEventType.SessionStart, "Session", $"开始执行任务: {userTask}");
        }

        /// <summary>
        /// 结束当前审计会话
        /// </summary>
        public static void EndSession(string summary = null)
        {
            var msg = string.IsNullOrEmpty(summary) ? "会话结束" : $"会话结束: {summary}";
            Log(AuditEventType.SessionEnd, "Session", msg);
            CurrentSessionId = null;
        }

        /// <summary>
        /// 记录工具调用
        /// </summary>
        public static void LogToolCall(string toolName, string parameters = null)
        {
            Log(AuditEventType.ToolCall, toolName, parameters);
        }

        /// <summary>
        /// 记录工具执行结果
        /// </summary>
        public static void LogToolResult(string toolName, string result, bool success = true)
        {
            var eventType = success ? AuditEventType.ToolResult : AuditEventType.ToolError;
            // 截断过长的结果
            var truncatedResult = result?.Length > 500 ? result.Substring(0, 500) + "..." : result;
            Log(eventType, toolName, truncatedResult);
        }

        /// <summary>
        /// 记录 UIA 操作（点击、输入等）
        /// </summary>
        public static void LogUIAOperation(string operation, int? controlId = null, string controlName = null, string value = null)
        {
            var sb = new StringBuilder();
            sb.Append($"操作: {operation}");
            if (controlId.HasValue) sb.Append($", 控件ID: {controlId}");
            if (!string.IsNullOrEmpty(controlName)) sb.Append($", 控件名: {controlName}");
            if (!string.IsNullOrEmpty(value)) sb.Append($", 值: {value}");
            
            Log(AuditEventType.UIAOperation, operation, sb.ToString());
        }

        /// <summary>
        /// 记录 LLM 响应
        /// </summary>
        public static void LogLLMResponse(string text, bool isThinking = false)
        {
            var eventType = isThinking ? AuditEventType.LLMThinking : AuditEventType.LLMResponse;
            var truncated = text?.Length > 300 ? text.Substring(0, 300) + "..." : text;
            Log(eventType, "LLM", truncated);
        }

        /// <summary>
        /// 记录错误
        /// </summary>
        public static void LogError(string source, string message, Exception ex = null)
        {
            var msg = ex != null ? $"{message}\n异常: {ex.Message}" : message;
            Log(AuditEventType.Error, source, msg);
        }

        /// <summary>
        /// 记录用户干预
        /// </summary>
        public static void LogUserIntervention(string action, string details = null)
        {
            var msg = string.IsNullOrEmpty(details) ? action : $"{action}: {details}";
            Log(AuditEventType.UserIntervention, "User", msg);
        }

        /// <summary>
        /// 核心日志记录方法
        /// </summary>
        private static void Log(AuditEventType eventType, string source, string message)
        {
            var entry = new AuditEntry
            {
                Timestamp = DateTime.Now,
                SessionId = CurrentSessionId ?? "no-session",
                EventType = eventType,
                Source = source,
                Message = message
            };

            // 添加到内存列表
            lock (_lock)
            {
                _recentEntries.Add(entry);
                if (_recentEntries.Count > MaxRecentEntries)
                {
                    _recentEntries.RemoveAt(0);
                }
            }

            // 写入文件（异步，不阻塞主流程）
            ThreadPool.QueueUserWorkItem(_ => WriteEntryToFile(entry));

            // 触发事件通知 UI
            OnEntryAdded?.Invoke(entry);
        }

        private static void WriteEntryToFile(AuditEntry entry)
        {
            try
            {
                EnsureLogDirectory();
                var json = JsonSerializer.Serialize(entry);
                File.AppendAllText(CurrentLogFile, json + "\n", Encoding.UTF8);
            }
            catch
            {
                // 静默失败，不影响主流程
            }
        }

        /// <summary>
        /// 获取最近的审计记录
        /// </summary>
        public static List<AuditEntry> GetRecentEntries(int count = 100)
        {
            lock (_lock)
            {
                var result = new List<AuditEntry>();
                int start = Math.Max(0, _recentEntries.Count - count);
                for (int i = start; i < _recentEntries.Count; i++)
                {
                    result.Add(_recentEntries[i]);
                }
                return result;
            }
        }

        /// <summary>
        /// 获取指定会话的所有记录
        /// </summary>
        public static List<AuditEntry> GetSessionEntries(string sessionId)
        {
            lock (_lock)
            {
                return _recentEntries.FindAll(e => e.SessionId == sessionId);
            }
        }

        /// <summary>
        /// 从文件加载历史审计记录
        /// </summary>
        public static List<AuditEntry> LoadFromFile(DateTime? date = null)
        {
            var targetDate = date ?? DateTime.Now;
            var filePath = Path.Combine(AuditLogPath, $"audit_{targetDate:yyyyMMdd}.jsonl");
            var entries = new List<AuditEntry>();

            if (!File.Exists(filePath)) return entries;

            try
            {
                foreach (var line in File.ReadAllLines(filePath, Encoding.UTF8))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var entry = JsonSerializer.Deserialize<AuditEntry>(line);
                    if (entry != null) entries.Add(entry);
                }
            }
            catch { }

            return entries;
        }

        /// <summary>
        /// 清除内存中的记录（保留文件记录）
        /// </summary>
        public static void ClearMemoryCache()
        {
            lock (_lock)
            {
                _recentEntries.Clear();
            }
        }

        /// <summary>
        /// 获取所有可用的审计日志文件
        /// </summary>
        public static List<string> GetAvailableLogFiles()
        {
            var files = new List<string>();
            if (!Directory.Exists(AuditLogPath)) return files;

            foreach (var file in Directory.GetFiles(AuditLogPath, "audit_*.jsonl"))
            {
                files.Add(file);
            }
            files.Sort((a, b) => b.CompareTo(a)); // 按日期倒序
            return files;
        }
    }

    /// <summary>
    /// 审计日志条目
    /// </summary>
    public class AuditEntry
    {
        public DateTime Timestamp { get; set; }
        public string SessionId { get; set; }
        public AuditEventType EventType { get; set; }
        public string Source { get; set; }
        public string Message { get; set; }

        /// <summary>
        /// 获取事件类型的显示文本
        /// </summary>
        public string EventTypeDisplay => EventType switch
        {
            AuditEventType.SessionStart => "会话开始",
            AuditEventType.SessionEnd => "会话结束",
            AuditEventType.ToolCall => "工具调用",
            AuditEventType.ToolResult => "工具结果",
            AuditEventType.ToolError => "工具错误",
            AuditEventType.UIAOperation => "UIA操作",
            AuditEventType.LLMResponse => "LLM响应",
            AuditEventType.LLMThinking => "LLM思考",
            AuditEventType.Error => "错误",
            AuditEventType.UserIntervention => "用户干预",
            _ => EventType.ToString()
        };
    }

    /// <summary>
    /// 审计事件类型
    /// </summary>
    public enum AuditEventType
    {
        /// <summary>会话开始</summary>
        SessionStart,
        /// <summary>会话结束</summary>
        SessionEnd,
        /// <summary>工具调用</summary>
        ToolCall,
        /// <summary>工具执行结果</summary>
        ToolResult,
        /// <summary>工具执行错误</summary>
        ToolError,
        /// <summary>UIA 操作（点击、输入等）</summary>
        UIAOperation,
        /// <summary>LLM 响应文本</summary>
        LLMResponse,
        /// <summary>LLM 思考过程</summary>
        LLMThinking,
        /// <summary>错误</summary>
        Error,
        /// <summary>用户干预（停止、修改等）</summary>
        UserIntervention
    }
}
