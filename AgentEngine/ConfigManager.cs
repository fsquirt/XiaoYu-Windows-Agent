using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using XiaoYu_LAM;

namespace XiaoYu_LAM.AgentEngine
{
    public static class ConfigManager
    {
        public static string ApiUrl { get; set; } = "";
        public static string ApiKey { get; set; } = "";
        public static string ModelName { get; set; } = "";
        public static string Protocol { get; set; } = "OpenAI";

        // Skills 
        public static bool EnableSkills { get; set; } = false;
        public static string[] SkillsFolders { get; set; } = new string[0];

        // UI & 特性设置
        public static bool IsDeepThinkMode { get; set; } = false;
        public static bool IsDeleteHistoryPic { get; set; } = false;
        public static bool IsHideUIAoutInChatForm { get; set; } = false;

        public static bool IsConfigValid => !string.IsNullOrEmpty(ApiKey) && !string.IsNullOrEmpty(ApiUrl);

        public static int ThinkingDeepth { get; set; } = 3;

        private static string ConfigPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");

        public static void LoadConfig()
        {
            if (!File.Exists(ConfigPath)) return;

            var lines = File.ReadAllLines(ConfigPath, Encoding.UTF8);
            foreach (var line in lines)
            {
                if (!line.Contains('=')) continue;
                var parts = line.Split(new char[] { '=' }, 2);
                string key = parts[0].Trim();
                string value = parts[1].Trim();

                switch (key)
                {
                    case "API_URL": ApiUrl = value; break;
                    case "API_KEY": ApiKey = value; break;
                    case "MODEL_NAME": ModelName = value; break;
                    case "PROTOCOL": Protocol = value; break;
                    case "ENABLE": EnableSkills = value.Equals("True", StringComparison.OrdinalIgnoreCase); break;
                    case "SKILLSPATH": SkillsFolders = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries); break;
                    case "IS_DEEP_THINK": IsDeepThinkMode = value.Equals("True", StringComparison.OrdinalIgnoreCase); break;
                    case "IS_DEL_HISTORY_PIC": IsDeleteHistoryPic = value.Equals("True", StringComparison.OrdinalIgnoreCase); break;
                    case "IS_HIDE_UIA": IsHideUIAoutInChatForm = value.Equals("True", StringComparison.OrdinalIgnoreCase); break;
                    case "THINKING_DEEPTH":if (int.TryParse(value, out int depth)) {ThinkingDeepth = depth;} break;
                }
            }
        }

        public static void SaveConfig()
        {
            var lines = new List<string>
            {
                "[LLM_PROVIDER]",
                $"API_URL={ApiUrl}",
                $"API_KEY={ApiKey}",
                $"MODEL_NAME={ModelName}",
                $"PROTOCOL={Protocol}",
                "",
                "[SKILLS]",
                $"ENABLE={EnableSkills}",
                $"SKILLSPATH={string.Join(",", SkillsFolders)}",
                "",
                "[UI_SETTINGS]",
                $"IS_DEEP_THINK={IsDeepThinkMode}",
                $"IS_DEL_HISTORY_PIC={IsDeleteHistoryPic}",
                $"IS_HIDE_UIA={IsHideUIAoutInChatForm}",
                $"THINKING_DEEPTH={ThinkingDeepth}"
            };
            File.WriteAllLines(ConfigPath, lines, Encoding.UTF8);
        }
    }
}