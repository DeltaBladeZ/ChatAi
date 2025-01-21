using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using ChatAi.Quests;

namespace ChatAi
{
    public class QuestManager
    {
        private readonly string _logFilePath = Path.Combine(BasePath.Name, "Modules", "ChatAi", "mod_log.txt");
        private readonly Dictionary<string, IQuestHandler> _questHandlers = new();
        private readonly Dictionary<Hero, bool> _questAcceptedFlags = new();

        public QuestManager()
        {
            LoadQuestHandlers();
        }

        // Load quest handlers dynamically
        private void LoadQuestHandlers()
        {
            var handlerMapping = new Dictionary<string, Type>
    {
        { "EscortMerchantCaravanIssue", typeof(EscortMerchantCaravanHandler) },
        { "NearbyBanditBaseIssue", typeof(NearbyBanditBaseIssueHandler) },
        { "GangLeaderNeedsToOffloadStolenGoodsIssue", typeof(GangLeaderNeedsToOffloadStolenGoodsIssueHandler)   }
        // Add mappings for all handlers
    };

            foreach (var entry in handlerMapping)
            {
                _questHandlers[entry.Key] = (IQuestHandler)Activator.CreateInstance(entry.Value);
            }
            LogMessage($"DEBUG: Loaded {_questHandlers.Count} quest handlers.");
        }


        // Check if a quest has already been accepted
        public bool HasAcceptedQuest(Hero npc)
        {
            return _questAcceptedFlags.ContainsKey(npc) && _questAcceptedFlags[npc];
        }

        // Mark the quest as accepted
        public void SetQuestAccepted(Hero npc)
        {
            _questAcceptedFlags[npc] = true;
        }

        // Analyze quest acceptance through AI
        public async Task<bool> AnalyzeQuestAcceptance(string npcMessage, string playerMessage)
        {
            string prompt = $"The NPC said: \"{npcMessage}\". The player responded: \"{playerMessage}\". " +
                            "Classify the player's response as one of the following categories: " +
                            "[accept_quest, other]. " +
                            "If the player is agreeing to accept a quest, classify it as 'accept_quest'. " +
                            "Only return the category name. Asking about the quest does not count as accepting it.";

            LogMessage($"DEBUG: Calling AIHelper.GetResponse for quest acceptance analysis with prompt: {prompt}");

            string response = await AIHelper.GetResponse(prompt);

            LogMessage($"DEBUG: Quest acceptance analysis result: {response}");

            return response.Trim().Equals("accept_quest", StringComparison.OrdinalIgnoreCase);
        }

        // Retrieve all active quests for a given NPC
        public List<IssueBase> GetQuestsForNPC(Hero npc)
        {
            var quests = new List<IssueBase>();

            try
            {
                var issueManager = Campaign.Current.IssueManager;

                // Primary: Retrieve quests tied directly to the NPC
                if (issueManager.Issues.TryGetValue(npc, out IssueBase issue))
                {
                    LogMessage($"DEBUG: Found an active quest for NPC {npc.Name}: {issue.GetType().Name}");
                    quests.Add(issue);
                }
                else
                {
                    LogMessage($"DEBUG: No active quests found for NPC {npc.Name}.");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"ERROR: Failed to retrieve quests for NPC {npc.Name}: {ex.Message}");
            }

            return quests;
        }

        public bool HandleQuest(IssueBase issue, Hero npc)
        {
            try
            {
                LogMessage($"DEBUG: Attempting to handle quest of type {issue.GetType().Name} for NPC {npc.Name}");

                string issueType = issue.GetType().Name;
                if (_questHandlers.TryGetValue(issueType, out var handler))
                {
                    return handler.HandleQuest(issue, npc);
                }

                LogMessage($"ERROR: No handler found for quest type {issueType}");
            }
            catch (Exception ex)
            {
                LogMessage($"ERROR: Failed to handle quest of type {issue.GetType().Name} for NPC {npc.Name}: {ex.Message}");
            }

            return false;
        }


        // Generate quest details for the AI prompt
        public string GetQuestDetailsForPrompt(Hero npc)
        {
            var quests = GetQuestsForNPC(npc);
            LogMessage($"DEBUG: Found {quests.Count} quest(s) for NPC {npc.Name}.");

            var questDetails = new List<string>();

            foreach (var quest in quests)
            {
                try
                {
                    LogMessage($"DEBUG: Inspecting quest of type {quest.GetType().Name} for NPC {npc.Name}.");

                    string title = ExtractProperty(quest, "Title", "Unknown Quest");
                    string description = ExtractProperty(quest, "Description", "Details are unavailable.");
                    string brief = ExtractProperty(quest, "IssueBriefByIssueGiver", "No context provided.");
                    string solution = ExtractProperty(quest, "IssueQuestSolutionExplanationByIssueGiver", "No solution provided.");

                    questDetails.Add($"- **{title}**\n  **Description**: {description}\n  **Quest Giver's Brief**: {brief}\n  **Solution**: {solution}");
                }
                catch (Exception ex)
                {
                    LogMessage($"DEBUG: Error processing quest {quest.GetType().Name}: {ex.Message}");
                }
            }

            return questDetails.Count > 0 ? $"Quests:\n{string.Join("\n", questDetails)}" : "No quests available.";
        }

        // Extract a property value dynamically
        public string ExtractProperty(object quest, string propertyName, string defaultValue)
        {
            try
            {
                var property = quest.GetType().GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (property != null)
                {
                    var value = property.GetValue(quest);
                    if (value != null)
                    {
                        if (value is Settlement settlement)
                            return settlement.Name?.ToString() ?? defaultValue;
                        return value.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"DEBUG: Failed to extract property '{propertyName}' from quest {quest.GetType().Name}: {ex.Message}");
            }
            return defaultValue;
        }



        private void LogMessage(string message)
        {
            try
            {
                if (!ChatAiSettings.Instance.EnableDebugLogging)
                {
                    return;
                }

                string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}\n";
                File.AppendAllText(_logFilePath, logMessage);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"Logging error: {ex.Message}"));
            }
        }
    }

    // Interface for quest handlers
    public interface IQuestHandler
    {
        bool HandleQuest(IssueBase issue, Hero npc);
    }

}