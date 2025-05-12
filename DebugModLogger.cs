using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ModuleManager;
using TaleWorlds.MountAndBlade;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using MCM.Common;

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

                // Safely get dropdown values with proper checks
                string GetDropdownValue(MCM.Common.Dropdown<string> dropdown)
                {
                    if (dropdown == null) return "Not Set";
                    try
                    {
                        return dropdown.SelectedValue ?? "Not Set";
                    }
                    catch
                    {
                        return "Not Set";
                    }
                }

                LogMessage($"AI Backend: {GetDropdownValue(settings.AIBackend)}");
                LogMessage($"Voice Backend: {GetDropdownValue(settings.VoiceBackend)}");
                LogMessage($"Max Tokens: {settings.MaxTokens}");
                LogMessage($"Longer Responses: {settings.LongerResponses}");
                LogMessage($"Custom Prompt: {settings.CustomPrompt}");

                LogMessage($"Is it steam version: {IsSteamVersion()}");

                LogMessage($"Toggle Ai driven actions: {settings.ToggleAIActions}");
                LogMessage($"Toggle Quest Information: {settings.ToggleQuestInfo}");
                LogMessage($"Toggle World Event Tracking: {settings.ToggleWorldEvents}");

                LogMessage($"OpenAI API Key: {(string.IsNullOrEmpty(settings.OpenAIAPIKey) ? "Not Set" : "Set")}");
                LogMessage($"OpenAI Model: {GetDropdownValue(settings.OpenAIModel)}");

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

                LogMessage($"Player2 API URL: {settings.Player2ApiUrl}");
                LogMessage($"Player2 Voice ID: {(string.IsNullOrEmpty(settings.Player2VoiceId) ? "Not Set" : "Set")}");
                LogMessage($"Player2 Voice Gender: {GetDropdownValue(settings.Player2VoiceGender)}");
                LogMessage($"Player2 Voice Language: {settings.Player2VoiceLanguage}");
                LogMessage($"Player2 TTS Speed: {settings.Player2TTSSpeed}");
                LogMessage($"Player2 TTS Volume: {settings.Player2TTSVolume}");

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
                LogMessage($"Stack trace: {ex.StackTrace}");
                InformationManager.DisplayMessage(new InformationMessage("Error logging settings. Check logs for details."));
            }
        }

        public async void LogAvailablePlayer2Voices()
        {
            try
            {
                LogMessage("============================================================");
                LogMessage("Available Player2 Voices:");
                LogMessage("============================================================");

                var settings = ChatAiSettings.Instance;
                string apiUrl = settings.Player2ApiUrl;

                using var httpClient = new HttpClient();
                using var request = new HttpRequestMessage(HttpMethod.Get, $"{apiUrl}/v1/tts/voices");
                request.Headers.Add("player2-game-key", "BannerlordChatAI");

                var response = await httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorDetails = await response.Content.ReadAsStringAsync();
                    LogMessage($"Failed to fetch Player2 voices: {errorDetails}");
                    return;
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                var voicesResponse = JsonConvert.DeserializeObject<VoicesResponse>(responseBody);

                if (voicesResponse?.voices != null)
                {
                    foreach (var voice in voicesResponse.voices)
                    {
                        LogMessage($"Voice ID: {voice.id}");
                        LogMessage($"Name: {voice.name}");
                        LogMessage($"Language: {voice.language}");
                        LogMessage($"Gender: {voice.gender}");
                        LogMessage("------------------------------------------------------------");
                    }
                }
                else
                {
                    LogMessage("No voices available or failed to parse response.");
                }

                LogMessage("============================================================");
                LogMessage("Voice listing complete.");
                LogMessage("============================================================");
            }
            catch (Exception ex)
            {
                LogMessage($"Error logging Player2 voices: {ex.Message}");
            }
        }

        private class VoicesResponse
        {
            public List<Player2Voice> voices { get; set; } = new List<Player2Voice>();
        }

        private class Player2Voice
        {
            public string id { get; set; }
            public string name { get; set; }
            public string language { get; set; }
            public string gender { get; set; }
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
