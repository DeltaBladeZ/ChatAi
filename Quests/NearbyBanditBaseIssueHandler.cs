using System;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Library;

namespace ChatAi.Quests
{
    public class NearbyBanditBaseIssueHandler : IQuestHandler
     
    {
        private readonly string _logFilePath = Path.Combine(BasePath.Name, "Modules", "ChatAi", "mod_log.txt");
        public bool HandleQuest(IssueBase issue, Hero npc)
        {
            try
            {
                if (issue is NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssue nearbyBanditBaseIssue)
                {
                    // Step 1: Generate the quest
                    var generateQuestMethod = nearbyBanditBaseIssue.GetType()
                        .GetMethod("GenerateIssueQuest", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

                    if (generateQuestMethod == null)
                    {
                        LogMessage("ERROR: GenerateIssueQuest method not found for NearbyBanditBaseIssue.");
                        return false;
                    }

                    var questInstance = generateQuestMethod.Invoke(nearbyBanditBaseIssue, new object[] { Guid.NewGuid().ToString() });

                    if (questInstance == null)
                    {
                        LogMessage("ERROR: Failed to create quest instance for NearbyBanditBaseIssue.");
                        return false;
                    }

                    // Step 2: Start the quest
                    var onQuestAcceptedMethod = questInstance.GetType()
                        .GetMethod("OnQuestAccepted", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

                    if (onQuestAcceptedMethod != null)
                    {
                        onQuestAcceptedMethod.Invoke(questInstance, null);
                        LogMessage("DEBUG: NearbyBanditBaseIssue quest successfully started.");
                        return true;
                    }

                    // Alternative: Try starting the quest directly if available
                    var startQuestMethod = questInstance.GetType()
                        .GetMethod("StartQuest", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

                    if (startQuestMethod != null)
                    {
                        startQuestMethod.Invoke(questInstance, null);
                        LogMessage("DEBUG: NearbyBanditBaseIssue quest started using StartQuest method.");
                        return true;
                    }

                    LogMessage("ERROR: No method found to start the quest for NearbyBanditBaseIssue.");
                    return false;
                }

                LogMessage("ERROR: Issue is not of type NearbyBanditBaseIssue.");
                return false;
            }
            catch (Exception ex)
            {
                LogMessage($"ERROR: Exception occurred while handling NearbyBanditBaseIssue: {ex.Message}");
                return false;
            }
        }

        private void LogMessage(string message)
        {
            try
            {
                // Settings may not be initialized yet on older Bannerlord versions.
                if (!ChatAi.SettingsUtil.IsDebugLoggingEnabled())
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
