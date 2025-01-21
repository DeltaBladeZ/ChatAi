using System;
using System.Collections.Generic;
using System.Linq;

namespace ChatAi
{
    [Serializable]
    public class NPCContext
    {
        private Dictionary<string, Func<string>> _dynamicStats = new(); // Store dynamic stat providers
        private List<string> _messageHistory = new(); // Keep history of messages

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
            return _dynamicStats.ToDictionary(stat => stat.Key, stat => stat.Value());
        }

        // Add a message to the conversation history
        public void AddMessage(string message)
        {
            _messageHistory.Add(message);

            // Trim history based on MaxHistoryLength
            int maxHistory = ChatAiSettings.Instance.MaxHistoryLength;
            if (_messageHistory.Count > maxHistory)
            {
                _messageHistory.RemoveRange(0, _messageHistory.Count - maxHistory);
            }
        }

        // Get formatted conversation history
        public string GetFormattedHistory()
        {
            return string.Join("\n", _messageHistory);
        }

        // Fetch the most recent NPC message before the player's response
        public string GetLatestNPCMessage()
        {
            var npcMessages = _messageHistory
                .Where(msg => msg.StartsWith("NPC:")) // Find messages that start with "NPC:"
                .ToList();

            // Return the last NPC message without the "NPC:" prefix
            return npcMessages.Count > 0 ? npcMessages.Last().Substring(4) : "No previous message from NPC.";
        }

        // Fetch the entire conversation history for debugging
        public List<string> GetFullMessageHistory()
        {
            return _messageHistory;
        }
    }
}
