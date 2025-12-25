using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using System.Timers;
using System.IO;
using System.Linq;

namespace ChatAi
{
    public class Player2TextToSpeech
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static bool _isPlaying = false;
        private static string _storedText = null;
        private static bool _storedIsFemale = false;
        private const string GAME_KEY = "BannerlordChatAI";
        private static Timer _healthCheckTimer;
        private static bool _isHealthy = false;
        private static readonly string _logFilePath = PathHelper.GetModFilePath("mod_log.txt");

        public class Player2Voice
        {
            public string id { get; set; }
            public string name { get; set; }
            public string language { get; set; }
            public string gender { get; set; }
        }

        private static void LogMessage(string message)
        {
            try
            {
                // Settings may not be initialized yet on older Bannerlord versions.
                if (!ChatAi.SettingsUtil.IsDebugLoggingEnabled())
                {
                    return; // Skip logging if disabled
                }

                string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}\n";

                // Use PathHelper to get the correct log file path
                string logFilePath = _logFilePath;
                string logDirectory = Path.GetDirectoryName(logFilePath);

                // Ensure the log directory exists
                if (!string.IsNullOrEmpty(logDirectory))
                {
                    PathHelper.EnsureDirectoryExists(logDirectory);
                }

                // Write the log message to the file
                File.AppendAllText(logFilePath, logMessage);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"Logging error: {ex.Message}"));
            }
        }

        // Add health check response model
        private class HealthResponse
        {
            public string client_version { get; set; }
        }

        // Initialize health check timer
        public static void InitializeHealthCheck()
        {
            _healthCheckTimer = new Timer(60000); // 60000 ms = 1 minute
            _healthCheckTimer.Elapsed += async (sender, e) => await CheckHealth();
            _healthCheckTimer.AutoReset = true;
            _healthCheckTimer.Start();
            
            // Initial health check
            _ = CheckHealth();
            LogMessage("Player2 health check initialized");
        }

        // Stop health check timer
        public static void StopHealthCheck()
        {
            _healthCheckTimer?.Stop();
            _healthCheckTimer?.Dispose();
            LogMessage("Player2 health check stopped");
        }

        // Check health endpoint
        private static async Task CheckHealth()
        {
            try
            {
                var settings = ChatAiSettings.Instance;
                string apiUrl = settings.Player2ApiUrl;

                using var request = new HttpRequestMessage(HttpMethod.Get, $"{apiUrl}/v1/health");
                request.Headers.Add("player2-game-key", GAME_KEY);

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    var healthResponse = JsonConvert.DeserializeObject<HealthResponse>(responseBody);
                    _isHealthy = true;
                    LogMessage($"Player2 API is healthy. Client version: {healthResponse.client_version}");
                }
                else
                {
                    _isHealthy = false;
                    var errorDetails = await response.Content.ReadAsStringAsync();
                    LogMessage($"Player2 API health check failed: {errorDetails}");
                }
            }
            catch (Exception ex)
            {
                _isHealthy = false;
                LogMessage($"Error checking Player2 API health: {ex.Message}");
            }
        }

        // Get current health status
        public static bool IsHealthy()
        {
            return _isHealthy;
        }

        public static async Task GenerateSpeech(string text, bool isFemale)
        {
            try
            {
                if (_isPlaying)
                {
                    await StopSpeaking();
                }

                var settings = ChatAiSettings.Instance;
                string apiUrl = settings.Player2ApiUrl;

                var voiceIds = new List<string>();
                
                // Select the appropriate voice ID based on gender
                string voiceId = isFemale ? settings.Player2FemaleVoiceId : settings.Player2MaleVoiceId;
                string voiceLanguage = settings.Player2VoiceLanguage;
                
                // Add the selected voice ID if it's not empty
                if (!string.IsNullOrEmpty(voiceId))
                {
                    voiceIds.Add(voiceId);
                    LogMessage($"Using {(isFemale ? "female" : "male")} voice ID: {voiceId}");
                }
                else
                {
                    LogMessage($"No voice ID set for {(isFemale ? "female" : "male")} NPCs, using default gender selection");
                }

                // Use the NPC's gender directly
                string voiceGender = isFemale ? "female" : "male";
                
                var payload = new
                {
                    play_in_app = true,
                    speed = settings.Player2TTSSpeed / 100f,
                    text = text,
                    voice_gender = voiceGender,
                    voice_ids = voiceIds,
                    voice_language = voiceLanguage
                };

                var jsonPayload = JsonConvert.SerializeObject(payload);
                LogMessage($"Generating speech with payload: {jsonPayload}");

                using var request = new HttpRequestMessage(HttpMethod.Post, $"{apiUrl}/v1/tts/speak")
                {
                    Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                };
                request.Headers.Add("player2-game-key", GAME_KEY);

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorDetails = await response.Content.ReadAsStringAsync();
                    LogMessage($"Failed to generate speech: {errorDetails}");
                    return;
                }

                _isPlaying = true;
                LogMessage("Speech generation started successfully");
            }
            catch (Exception ex)
            {
                LogMessage($"Error generating speech: {ex.Message}");
            }
        }

        public static async Task StopSpeaking()
        {
            try
            {
                var settings = ChatAiSettings.Instance;
                string apiUrl = settings.Player2ApiUrl;

                using var request = new HttpRequestMessage(HttpMethod.Post, $"{apiUrl}/v1/tts/stop");
                request.Headers.Add("player2-game-key", GAME_KEY);

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorDetails = await response.Content.ReadAsStringAsync();
                    LogMessage($"Failed to stop speech: {errorDetails}");
                    return;
                }

                _isPlaying = false;
                LogMessage("Speech stopped successfully");
            }
            catch (Exception ex)
            {
                LogMessage($"Error stopping speech: {ex.Message}");
            }
        }

        public static async Task SetVolume(float volume)
        {
            try
            {
                var settings = ChatAiSettings.Instance;
                string apiUrl = settings.Player2ApiUrl;

                var payload = new { volume = settings.Player2TTSVolume / 100f };
                var jsonPayload = JsonConvert.SerializeObject(payload);
                LogMessage($"Setting volume with payload: {jsonPayload}");

                using var request = new HttpRequestMessage(HttpMethod.Post, $"{apiUrl}/v1/tts/volume")
                {
                    Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                };
                request.Headers.Add("player2-game-key", GAME_KEY);

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorDetails = await response.Content.ReadAsStringAsync();
                    LogMessage($"Failed to set volume: {errorDetails}");
                }
                else
                {
                    LogMessage("Volume set successfully");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error setting volume: {ex.Message}");
            }
        }

        public static void StoreTextForPlayback(string text, bool isFemale)
        {
            _storedText = text;
            _storedIsFemale = isFemale;
            LogMessage($"Text stored for playback. Length: {text?.Length ?? 0}, IsFemale: {isFemale}");
        }

        public static async void PlayStoredText()
        {
            if (_storedText != null)
            {
                LogMessage("Playing stored text");
                await GenerateSpeech(_storedText, _storedIsFemale);
                _storedText = null;
            }
            else
            {
                LogMessage("No stored text to play");
            }
        }

        private class VoicesResponse
        {
            public List<Player2Voice> voices { get; set; } = new List<Player2Voice>();
        }
        
        // Add method to refresh available voices
        public static async void RefreshAvailableVoices()
        {
            try
            {
                InformationManager.DisplayMessage(new InformationMessage("Fetching available Player2 voices and writing to log file..."));
                
                var settings = ChatAiSettings.Instance;
                string apiUrl = settings.Player2ApiUrl;

                using var httpClient = new HttpClient();
                using var request = new HttpRequestMessage(HttpMethod.Get, $"{apiUrl}/v1/tts/voices");
                request.Headers.Add("player2-game-key", GAME_KEY);

                var response = await httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorDetails = await response.Content.ReadAsStringAsync();
                    LogMessage($"Failed to fetch Player2 voices: {errorDetails}");
                    InformationManager.DisplayMessage(new InformationMessage("Failed to fetch Player2 voices. Make sure Player2 is downloaded from player2.game and running."));
                    return;
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                var voicesResponse = JsonConvert.DeserializeObject<VoicesResponse>(responseBody);

                if (voicesResponse?.voices != null && voicesResponse.voices.Count > 0)
                {
                    // Create a dedicated log file for voices using PathHelper
                    string voicesLogPath = PathHelper.GetModFilePath("player2_voices.txt");
                    
                    using (StreamWriter writer = new StreamWriter(voicesLogPath, false)) // 'false' to overwrite the file
                    {
                        writer.WriteLine($"Player2 Available Voices - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                        writer.WriteLine("=============================================================");
                        writer.WriteLine();
                        
                        foreach (var voice in voicesResponse.voices)
                        {
                            writer.WriteLine($"ID: {voice.id}");
                            writer.WriteLine($"Name: {voice.name}");
                            writer.WriteLine($"Gender: {voice.gender}");
                            writer.WriteLine($"Language: {voice.language}");
                            writer.WriteLine("-------------------------------------------------------------");
                        }
                        
                        writer.WriteLine($"\nTotal voices available: {voicesResponse.voices.Count}");
                    }
                    
                    LogMessage($"Successfully fetched {voicesResponse.voices.Count} Player2 voices and saved to {voicesLogPath}");
                    InformationManager.DisplayMessage(new InformationMessage($"Found {voicesResponse.voices.Count} Player2 voices. List saved to player2_voices.txt"));
                }
                else
                {
                    LogMessage("No Player2 voices available");
                    InformationManager.DisplayMessage(new InformationMessage("No Player2 voices available. Make sure Player2 is downloaded from player2.game and properly set up."));
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error refreshing Player2 voices: {ex.Message}");
                InformationManager.DisplayMessage(new InformationMessage($"Error fetching Player2 voices: {ex.Message}"));
            }
        }
    }
} 