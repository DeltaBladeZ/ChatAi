using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TaleWorlds.Library;

namespace ChatAi
{
    [Serializable]
    public class NPCContext
    {
        private readonly string _logFilePath = PathHelper.GetModFilePath("mod_log.txt");

        private Dictionary<string, Func<string>> _dynamicStats = new(); // Runtime-only dynamic stats
        private List<string> _messageHistory = new(); // Initialize _messageHistory

        [JsonProperty] // Ensure this gets saved to JSON
        public List<string> MessageHistory { get => _messageHistory; set => _messageHistory = value ?? new List<string>(); }

        [JsonProperty] // Ensure this gets saved
        public string Name { get; set; }

        [JsonProperty] // Ensure stats get saved as plain text
        public Dictionary<string, string> StaticStats { get; set; } = new();

        public NPCContext()
        {
            // Ensure MessageHistory is never null
            MessageHistory = new List<string>();
            StaticStats = new Dictionary<string, string>();
        }

        // Add or update a static stat (used for saving)
        public void AddStaticStat(string key, string value)
        {
            StaticStats[key] = value;
        }

        // Add or update a dynamic stat (runtime only)
        public void AddDynamicStat(string key, Func<string> valueProvider)
        {
            _dynamicStats[key] = valueProvider;
        }

        // Fetch the current value of a stat
        public string GetStat(string key)
        {
            return _dynamicStats.ContainsKey(key)
                ? _dynamicStats[key]()
                : (StaticStats.ContainsKey(key) ? StaticStats[key] : "Unknown");
        }


        // Get all stats as a dictionary (for UI and debugging)
        public Dictionary<string, string> GetAllStats()
        {
            LogMessage($"DEBUG: Found {_dynamicStats.Count + StaticStats.Count} stats for NPC {Name}.");

            // Merge both static (saved) and dynamic (calculated) stats
            var allStats = new Dictionary<string, string>(StaticStats);
            foreach (var kvp in _dynamicStats)
            {
                allStats[kvp.Key] = kvp.Value();
            }

            return allStats;
        }

        // Add a message to the conversation history
        public void AddMessage(string message)
        {
            MessageHistory.Add(message);
            LogMessage($"Added message: {message}");

            // Trim history if needed
            int maxHistory = ChatAiSettings.Instance.MaxHistoryLength;
            if (MessageHistory.Count > maxHistory)
            {
                MessageHistory.RemoveRange(0, MessageHistory.Count - maxHistory);
                LogMessage($"Trimmed message history to {maxHistory} messages.");
            }
        }

        // Get formatted conversation history
        public string GetFormattedHistory()
        {
            return string.Join("\n", MessageHistory);
        }

        // Fetch the most recent NPC message before the player's response
        public string GetLatestNPCMessage()
        {
            var npcMessages = MessageHistory
                .Where(msg => msg.StartsWith("NPC:")) // Find messages that start with "NPC:"
                .ToList();

            return npcMessages.Count > 0 ? npcMessages.Last().Substring(4) : "No previous message from NPC.";
        }

        // Fetch the entire conversation history for debugging
        public List<string> GetFullMessageHistory()
        {
            return MessageHistory;
        }

        // Get the most recent messages for AI analysis
        public List<string> GetRecentMessages(int count)
        {
            // Ensure count is positive
            if (count <= 0)
                return new List<string>();
                
            // Return the last 'count' messages or all messages if there are fewer than 'count'
            return MessageHistory
                .Skip(Math.Max(0, MessageHistory.Count - count))
                .ToList();
        }

        private void LogMessage(string message)
        {
            try
            {
                if (!SettingsUtil.IsDebugLoggingEnabled())
                {
                    return;
                }

                string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}\n";

                // Use PathHelper to get the correct log file path
                string logFilePath = _logFilePath;
                string logDirectory = Path.GetDirectoryName(logFilePath);

                // Ensure the log directory exists
                if (!string.IsNullOrEmpty(logDirectory))
                {
                    PathHelper.EnsureDirectoryExists(logDirectory);
                }

                File.AppendAllText(logFilePath, logMessage);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"Logging error: {ex.Message}"));
            }
        }
    }
}
