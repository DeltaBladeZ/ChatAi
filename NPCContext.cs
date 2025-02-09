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
        private readonly string _logFilePath = Path.Combine(BasePath.Name, "Modules", "ChatAi", "mod_log.txt");
        private Dictionary<string, Func<string>> _dynamicStats = new(); // Store dynamic stat providers
        private List<string> _messageHistory = new(); // Initialize _messageHistory

        [JsonProperty]
        public List<string> MessageHistory { get => _messageHistory; set => _messageHistory = value; }

        public string Name { get; set; }

        // Add or update a static stat
        public void AddStaticStat(string key, string value)
        {
            _dynamicStats[key] = () => value; // Use a lambda that always returns the same value
        }

        // Add or update a dynamic stat
        public void AddDynamicStat(string key, Func<string> valueProvider)
        {
            _dynamicStats[key] = valueProvider;
        }

        // Fetch the current value of a stat
        public string GetStat(string key)
        {
            return _dynamicStats.ContainsKey(key) ? _dynamicStats[key]() : "Unknown";
        }

        // Get all stats as a dictionary
        public Dictionary<string, string> GetAllStats()
        {
            LogMessage("Fetching all NPC stats.");
            LogMessage($"DEBUG: Found {_dynamicStats.Count} stats for NPC {Name}.");
            return _dynamicStats.ToDictionary(stat => stat.Key, stat => stat.Value());
        }

        // Add a message to the conversation history
        public void AddMessage(string message)
        {
            _messageHistory.Add(message);
            LogMessage($"Added message: {message}");

            // Trim history based on MaxHistoryLength
            int maxHistory = ChatAiSettings.Instance.MaxHistoryLength;
            if (_messageHistory.Count > maxHistory)
            {
                _messageHistory.RemoveRange(0, _messageHistory.Count - maxHistory);
                LogMessage($"Trimmed message history to {maxHistory} messages.");
            }
        }

        // Get formatted conversation history
        public string GetFormattedHistory()
        {
            LogMessage($"Fetching formatted message history for NPC {Name}: {string.Join(", ", _messageHistory)}");
            return string.Join("\n", _messageHistory);
        }

        // Fetch the most recent NPC message before the player's response
        public string GetLatestNPCMessage()
        {
            var npcMessages = _messageHistory
                .Where(msg => msg.StartsWith("NPC:")) // Find messages that start with "NPC:"
                .ToList();

            LogMessage($"\nDEBUG: Found {npcMessages.Count} previous messages from NPC.");
            // Return the last NPC message without the "NPC:" prefix
            return npcMessages.Count > 0 ? npcMessages.Last().Substring(4) : "No previous message from NPC.";
        }

        // Fetch the entire conversation history for debugging
        public List<string> GetFullMessageHistory()
        {
            LogMessage("Fetching full message history.");
            return _messageHistory;
        }

        private void LogMessage(string message)
        {
            try
            {
                // Check if debug logging is enabled in the settings
                if (!ChatAiSettings.Instance.EnableDebugLogging)
                {
                    return; // Skip logging if disabled
                }

                string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}\n";

                // Determine the log file path dynamically
                string logFilePath = _logFilePath; // Default path
                string logDirectory = Path.GetDirectoryName(logFilePath);

                if (!Directory.Exists(logDirectory))
                {
                    // If the directory does not exist, fall back to the desktop
                    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string desktopLogDirectory = Path.Combine(desktopPath, "ChatAiLogs");

                    if (!Directory.Exists(desktopLogDirectory))
                    {
                        Directory.CreateDirectory(desktopLogDirectory);
                    }

                    logFilePath = Path.Combine(desktopLogDirectory, "mod_log.txt");
                }

                // Write the log message to the file
                File.AppendAllText(logFilePath, logMessage);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"Logging error: {ex.Message}"));
            }
        }
    }
}
