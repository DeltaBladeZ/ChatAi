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

namespace ChatAi
{
    public class Player2TextToSpeech
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static List<Player2Voice> _availableVoices = new List<Player2Voice>();
        private static bool _isPlaying = false;
        private static string _storedText = null;
        private static bool _storedIsFemale = false;
        private const string GAME_KEY = "BannerlordChatAI";
        private static Timer _healthCheckTimer;
        private static bool _isHealthy = false;
        private static readonly string _logFilePath = Path.Combine(BasePath.Name, "Modules", "ChatAi", "mod_log.txt");

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

        public static async Task RefreshAvailableVoices()
        {
            try
            {
                var settings = ChatAiSettings.Instance;
                string apiUrl = settings.Player2ApiUrl;

                using var request = new HttpRequestMessage(HttpMethod.Get, $"{apiUrl}/v1/tts/voices");
                request.Headers.Add("player2-game-key", GAME_KEY);

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorDetails = await response.Content.ReadAsStringAsync();
                    LogMessage($"Failed to fetch Player2 voices: {errorDetails}");
                    return;
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                var voicesResponse = JsonConvert.DeserializeObject<VoicesResponse>(responseBody);
                _availableVoices = voicesResponse.voices;

                LogMessage($"Successfully loaded {_availableVoices.Count} Player2 voices.");
            }
            catch (Exception ex)
            {
                LogMessage($"Error refreshing Player2 voices: {ex.Message}");
            }
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
                if (!string.IsNullOrEmpty(settings.Player2VoiceId))
                {
                    voiceIds.Add(settings.Player2VoiceId);
                }

                var payload = new
                {
                    play_in_app = true,
                    speed = settings.Player2TTSSpeed / 100f,
                    text = text,
                    voice_gender = isFemale ? "female" : "male",
                    voice_ids = voiceIds,
                    voice_language = settings.Player2VoiceLanguage
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
    }
} 