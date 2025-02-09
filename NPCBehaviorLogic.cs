using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using System.IO;

namespace ChatAi
{
    public class NPCBehaviorLogic
    {
        private readonly string _logFilePath = System.IO.Path.Combine(BasePath.Name, "Modules", "ChatAi", "mod_log.txt");

        public void PatrolAround(Hero npc, Settlement settlement)
        {
            LogMessage($"{npc.Name} begins patrolling around {settlement.Name}.");
            // Patrol logic here
        }


        public void DeliverMessage(Hero npc)
        {
            LogMessage($"{npc.Name} is delivering a message.");
            // Deliver message logic here
        }

        public void GoToLocation(Hero npc, Settlement targetSettlement)
        {
            LogMessage($"{npc.Name} is traveling to {targetSettlement.Name}.");
            // Travel logic here
        }

        public void GoToSettlement(Hero npc, Settlement targetSettlement)
        {
            if (npc.PartyBelongedTo != null && targetSettlement != null)
            {
                npc.PartyBelongedTo.Ai.SetMoveGoToSettlement(targetSettlement);
                LogMessage($"{npc.Name} is now traveling to {targetSettlement.Name}.");
            }
            else
            {
                LogMessage($"{npc.Name} cannot travel to the target settlement because they are not part of a valid party or the target settlement is invalid.");
            }
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