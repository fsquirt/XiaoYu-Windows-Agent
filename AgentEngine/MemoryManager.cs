using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace XiaoYu_LAM.AgentEngine
{
    public static class MemoryManager
    {
        private static string MemoryFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "memories.json");
        public static List<string> Memories { get; private set; } = new List<string>();

        // 触发事件告诉 UI 需要整体刷新记忆列表
        public static event Action OnMemoriesChanged;

        // 当新增记忆时触发此事件，通知 MainForm 更新 ListView
        public static event Action<string> OnMemoryAdded;

        public static void LoadMemories()
        {
            if (File.Exists(MemoryFilePath))
            {
                try
                {
                    string json = File.ReadAllText(MemoryFilePath);
                    Memories = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                }
                catch
                {
                    Memories = new List<string>();
                }
            }
        }

        public static void AddMemory(string memory)
        {
            if (string.IsNullOrWhiteSpace(memory)) return;
            Memories.Add(memory);
            File.WriteAllText(MemoryFilePath, JsonSerializer.Serialize(Memories, new JsonSerializerOptions { WriteIndented = true }));
            OnMemoryAdded?.Invoke(memory);
        }

        // 删除指定记忆的方法
        public static void RemoveMemory(string memoryText)
        {
            if (Memories.Remove(memoryText))
            {
                SaveMemories();
            }
        }

        // 提取的保存方法
        private static void SaveMemories()
        {
            File.WriteAllText(MemoryFilePath, JsonSerializer.Serialize(Memories, new JsonSerializerOptions { WriteIndented = true }));
            OnMemoriesChanged?.Invoke(); // 通知 UI 刷新
        }

        // 提供给 Agent Framework 的上下文提供者
        public static AIContextProvider CreateContextProvider()
        {
            return new SimpleMemoryProvider();
        }
    }

    // 继承自 MessageAIContextProvider，在每次对话前动态注入记忆
    internal class SimpleMemoryProvider : MessageAIContextProvider
    {
        protected override ValueTask<IEnumerable<ChatMessage>> ProvideMessagesAsync(InvokingContext context, CancellationToken cancellationToken = default)
        {
            if (MemoryManager.Memories.Count == 0)
            {
                return new ValueTask<IEnumerable<ChatMessage>>(Array.Empty<ChatMessage>());
            }

            // 将所有记忆拼接成一段 System 提示词
            string memoryText = "【重要历史经验提示】\n" + string.Join("\n", MemoryManager.Memories) + "\n请在操作时务必参考以上经验，避免重复犯错。";
            var msg = new ChatMessage(ChatRole.System, memoryText);
            return new ValueTask<IEnumerable<ChatMessage>>(new[] { msg });
        }
    }
}