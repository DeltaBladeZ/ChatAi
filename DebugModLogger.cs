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

        // New method to check if Player2 is available and display helpful messages
        public async Task<bool> CheckPlayer2Availability(bool displayMessages = true)
        {
            try
            {
                var settings = ChatAiSettings.Instance;
                string apiUrl = settings.Player2ApiUrl;
                
                LogMessage("[DEBUG] Checking Player2 availability at URL: " + apiUrl);
                
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(5); // Short timeout to avoid long waits
                
                using var request = new HttpRequestMessage(HttpMethod.Get, $"{apiUrl}/v1/health");
                request.Headers.Add("player2-game-key", "BannerlordChatAI");
                
                var response = await httpClient.SendAsync(request);
                
                if (response.IsSuccessStatusCode)
                {
                    LogMessage("[INFO] Player2 is running and responding correctly");
                    if (displayMessages)
                    {
                        InformationManager.DisplayMessage(new InformationMessage("Player2 connection successful! AI services are ready."));
                    }
                    return true;
                }
                else
                {
                    var errorDetails = await response.Content.ReadAsStringAsync();
                    LogMessage($"[ERROR] Player2 responded with error: {response.StatusCode} - {errorDetails}");
                    
                    if (displayMessages)
                    {
                        ShowPlayer2ErrorMessage("Player2 is responding, but with errors. Please check the mod_log.txt file for details.");
                    }
                    return false;
                }
            }
            catch (HttpRequestException ex)
            {
                LogMessage($"[ERROR] Cannot connect to Player2: {ex.Message}");
                
                if (displayMessages)
                {
                    ShowPlayer2ErrorMessage(
                        "Cannot connect to Player2. Please make sure:\n" +
                        "1. Player2 is downloaded and installed from player2.game\n" +
                        "2. Player2 is currently running\n" +
                        "3. API URL is correct (default: http://localhost:4315)"
                    );
                }
                return false;
            }
            catch (TaskCanceledException)
            {
                LogMessage("[ERROR] Connection to Player2 timed out");
                
                if (displayMessages)
                {
                    ShowPlayer2ErrorMessage(
                        "Connection to Player2 timed out. Please make sure:\n" +
                        "1. Player2 is running\n" +
                        "2. Your firewall isn't blocking the connection"
                    );
                }
                return false;
            }
            catch (Exception ex)
            {
                LogMessage($"[ERROR] Unexpected error checking Player2 availability: {ex.Message}");
                
                if (displayMessages)
                {
                    ShowPlayer2ErrorMessage("Unexpected error connecting to Player2. Check mod_log.txt for details.");
                }
                return false;
            }
        }
        
        private void ShowPlayer2ErrorMessage(string message)
        {
            // First log the message
            LogMessage($"[PLAYER2-ERROR] {message}");
            
            // Then show a popup with download information
            InformationManager.ShowInquiry(
                new InquiryData(
                    "Player2 Connection Issue", 
                    $"{message}\n\n" +
                    "Player2 is required for AI responses and/or voice when selected in settings.\n\n" +
                    "You can download Player2 from: player2.game",
                    true, false, "OK", null, null, null
                )
            );
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
                LogMessage($"Player2 Male Voice ID: {(string.IsNullOrEmpty(settings.Player2MaleVoiceId) ? "Not Set" : settings.Player2MaleVoiceId)}");
                LogMessage($"Player2 Female Voice ID: {(string.IsNullOrEmpty(settings.Player2FemaleVoiceId) ? "Not Set" : settings.Player2FemaleVoiceId)}");
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
