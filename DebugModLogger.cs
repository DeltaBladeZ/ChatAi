using System;
using System.Linq;
using System.IO;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ModuleManager;
using TaleWorlds.MountAndBlade;

namespace ChatAi
{
    public class DebugModLogger
    {
        private readonly string _logFilePath = Path.Combine(BasePath.Name, "Modules", "ChatAi", "mod_log.txt");

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

        public void LogAllModules()
        {
            try
            {
                var allModules = ModuleHelper.GetModules();
                LogMessage("All Detected Modules:");
                foreach (var module in allModules)
                {
                    LogMessage($"- {module.Name}");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error logging all modules: {ex.Message}");
                InformationManager.DisplayMessage(new InformationMessage("Error logging all modules. Check logs for details."));
            }
        }

    }
}
