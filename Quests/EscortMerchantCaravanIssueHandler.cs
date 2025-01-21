using System;
using System.IO;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Library;

namespace ChatAi.Quests
{
    public class EscortMerchantCaravanHandler : IQuestHandler
    {
        private readonly string _logFilePath = Path.Combine(BasePath.Name, "Modules", "ChatAi", "mod_log.txt");

        public bool HandleQuest(IssueBase issue, Hero npc)
        {
            try
            {
                if (issue is EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue escortIssue)
                {
                    // Step 1: Check if the player meets the conditions for the quest
                    MethodInfo conditionsMethod = typeof(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue)
                        .GetMethod("CanPlayerTakeQuestConditions", BindingFlags.Instance | BindingFlags.NonPublic);

                    if (conditionsMethod != null)
                    {
                        // Prepare parameters for the method call
                        object[] parameters = { npc, null, null, null };

                        bool canAccept = (bool)conditionsMethod.Invoke(escortIssue, parameters);

                        if (!canAccept)
                        {
                            // Extract the reason from the second parameter
                            var reason = parameters[1] as string;

                            LogMessage("Player does not meet the conditions for the quest.");
                            if (!string.IsNullOrEmpty(reason))
                            {
                                LogMessage($"- Reason: {reason}");
                            }
                            else
                            {
                                LogMessage("- Reason: No specific reason provided.");
                            }

                            return false;
                        }
                    }

                    // Step 2: Generate the quest
                    MethodInfo generateQuestMethod = typeof(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue)
                        .GetMethod("GenerateIssueQuest", BindingFlags.Instance | BindingFlags.NonPublic);

                    if (generateQuestMethod == null)
                    {
                        LogMessage("ERROR: GenerateIssueQuest method not found.");
                        return false;
                    }

                    QuestBase questInstance = (QuestBase)generateQuestMethod.Invoke(escortIssue, new object[] { Guid.NewGuid().ToString() });

                    if (questInstance == null)
                    {
                        LogMessage("ERROR: Failed to create quest instance.");
                        return false;
                    }

                    // Step 3: Start the quest
                    MethodInfo startQuestMethod = questInstance.GetType()
                        .GetMethod("StartQuest", BindingFlags.Instance | BindingFlags.Public);

                    if (startQuestMethod != null)
                    {
                        startQuestMethod.Invoke(questInstance, null);
                        LogMessage("DEBUG: Escort Merchant Caravan quest started successfully.");
                        return true;
                    }

                    LogMessage("ERROR: No method found to start the quest.");
                    return false;
                }

                LogMessage("ERROR: Issue is not of type EscortMerchantCaravanIssue.");
                return false;
            }
            catch (Exception ex)
            {
                LogMessage($"ERROR: Exception occurred while handling Escort Merchant Caravan quest: {ex.Message}");
                return false;
            }
        }

        private void LogMessage(string message)
        {
            try
            {
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
