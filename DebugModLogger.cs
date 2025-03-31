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

        // check if the mod is steam version or nexus version by checking the path
        public bool IsSteamVersion()
        {
            return BasePath.Name.IndexOf("Steam", StringComparison.OrdinalIgnoreCase) >= 0;
        }



        public void LogAllSettings()
        {
            Version version = typeof(DebugModLogger).Assembly.GetName().Version;

            try
            {
                var settings = ChatAiSettings.Instance;
                if (settings == null)
                {
                    LogMessage("Error: ChatAiSettings instance is null.");
                    return;
                }
                LogMessage("============================================================");
                LogMessage("Current ChatAi Settings:");
                LogMessage("============================================================");

                LogMessage($"AI Backend: {settings.AIBackend?.SelectedValue ?? "Not Set"}");
                LogMessage($"Voice Backend: {settings.VoiceBackend?.SelectedValue ?? "Not Set"}");
                LogMessage($"Max Tokens: {settings.MaxTokens}");
                LogMessage($"Longer Responses: {settings.LongerResponses}");
                LogMessage($"Custom Prompt: {settings.CustomPrompt}");

                LogMessage($"Is it steam version: {IsSteamVersion()}");

                LogMessage($"Toggle Ai driven actions: {settings.ToggleAIActions}");
                LogMessage($"Toggle Quest Information: {settings.ToggleQuestInfo}");
                LogMessage($"Toggle World Event Tracking: {settings.ToggleWorldEvents}");

                    LogMessage($"OpenAI API Key: {(string.IsNullOrEmpty(settings.OpenAIAPIKey) ? "Not Set" : "Set")}");
                LogMessage($"OpenAI Model: {settings.OpenAIModel?.SelectedValue ?? "Not Set"}");

                LogMessage($"KoboldCpp URL: {settings.LocalModelURL}");
                LogMessage($"KoboldCpp Model: {settings.KoboldCppModel}");

                LogMessage($"Ollama URL: {settings.OllamaURL}");
                LogMessage($"Ollama Model: {settings.OllamaModel}");

                LogMessage($"OpenRouter API Key: {(string.IsNullOrEmpty(settings.OpenRouterAPIKey) ? "Not Set" : "Set")}");
                LogMessage($"OpenRouter Model: {settings.OpenRouterModel}");

                LogMessage($"Azure TTS Key: {(string.IsNullOrEmpty(settings.AzureTTSKey) ? "Not Set" : "Set")}");
                LogMessage($"Azure TTS Region: {settings.AzureTTSRegion}");
                LogMessage($"Male Voice: {settings.MaleVoice}");
                LogMessage($"Female Voice: {settings.FemaleVoice}");

                LogMessage($"Base Relationship Gain: {settings.BaseRelationshipGain}");
                LogMessage($"Base Relationship Loss: {settings.BaseRelationshipLoss}");
                LogMessage($"Max Relationship Change: {settings.MaxRelationshipChange}");
                LogMessage($"Enable Relationship Tracking: {settings.EnableRelationshipTracking}");

                LogMessage($"Max History Length: {settings.MaxHistoryLength}");
                LogMessage($"Enable Debug Logging: {settings.EnableDebugLogging}");

                LogMessage(version != null ? $"Mod Version: {version}" : "Mod Version: Unknown");

                LogMessage("============================================================");
                LogMessage("Settings logging complete.");
                LogMessage("============================================================");
            }
            catch (Exception ex)
            {
                LogMessage($"Error logging settings: {ex.Message}");
                InformationManager.DisplayMessage(new InformationMessage("Error logging settings. Check logs for details."));
            }
        }
        public void LogAllModules()
        {
            try
            {
                var allModules = ModuleHelper.GetModules();
                LogMessage("============================================================");
                LogMessage("All Detected Modules:");
                LogMessage("============================================================");
                foreach (var module in allModules)
                {
                    LogMessage($"- {module.Name}");
                }
                LogMessage("============================================================");
                LogMessage("Module logging complete.");
                LogMessage("============================================================");
            }
            catch (Exception ex)
            {
                LogMessage($"Error logging all modules: {ex.Message}");
                InformationManager.DisplayMessage(new InformationMessage("Error logging all modules. Check logs for details."));
            }
        }

    }
}
