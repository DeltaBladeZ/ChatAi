using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Library;
using System.IO;

namespace ChatAi.Quests
{
    public class GangLeaderNeedsToOffloadStolenGoodsIssueHandler : IQuestHandler
    {
        private readonly string _logFilePath = Path.Combine(BasePath.Name, "Modules", "ChatAi", "mod_log.txt");
        public bool HandleQuest(IssueBase issue, Hero npc)
        {
            try
            {
                MethodInfo generateQuestMethod = issue.GetType().GetMethod("GenerateIssueQuest", BindingFlags.Instance | BindingFlags.NonPublic);
                if (generateQuestMethod != null)
                {
                    var quest = generateQuestMethod.Invoke(issue, new object[] { Guid.NewGuid().ToString() });
                    MethodInfo questAcceptedMethod = quest.GetType().GetMethod("QuestAcceptedConsequences", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (questAcceptedMethod != null)
                    {
                        questAcceptedMethod.Invoke(quest, null);
                        LogMessage("DEBUG: GangLeaderNeedsToOffloadStolenGoodsIssue quest successfully started.");
                        return true; 
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error here
                LogMessage($"ERROR: {ex.Message}");
            }

            LogMessage("ERROR: Failed to start GangLeaderNeedsToOffloadStolenGoodsIssue quest.");
            return false;
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
                File.AppendAllText(_logFilePath, logMessage);



            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"Logging error: {ex.Message}"));
            }
        }
    }
}
