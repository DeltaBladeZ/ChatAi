using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using NAudio.Wave;
using TaleWorlds.Library; 

namespace ChatAi
{
    public class AzureSpeechToText
    {
        private readonly string _logFilePath = Path.Combine(BasePath.Name, "Modules", "ChatAi", "mod_log.txt");

        public async Task<string> RecognizeSpeechAsync()
        {
            string subscriptionKey = ChatAiSettings.Instance.AzureTTSKey; // Using the same key as TTS for now
            string region = ChatAiSettings.Instance.AzureTTSRegion;

            if (string.IsNullOrWhiteSpace(subscriptionKey) || string.IsNullOrWhiteSpace(region))
            {
                LogMessage("Azure STT key or region is missing. Please configure the settings.");
                return null;
            }

            try
            {
                var speechConfig = SpeechConfig.FromSubscription(subscriptionKey, region);

                using var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
                using var recognizer = new SpeechRecognizer(speechConfig, audioConfig);

                LogMessage("Speak into the microphone.");
                var result = await recognizer.RecognizeOnceAsync();

                if (result.Reason == ResultReason.RecognizedSpeech)
                {
                    LogMessage($"RECOGNIZED: Text={result.Text}");
                    return result.Text;
                }
                else if (result.Reason == ResultReason.NoMatch)
                {
                    LogMessage($"NOMATCH: Speech could not be recognized.");
                }
                else if (result.Reason == ResultReason.Canceled)
                {
                    var cancellation = CancellationDetails.FromResult(result);
                    LogMessage($"CANCELED: Reason={cancellation.Reason}");

                    if (cancellation.Reason == CancellationReason.Error)
                    {
                        LogMessage($"CANCELED: ErrorCode={cancellation.ErrorCode}");
                        LogMessage($"CANCELED: ErrorDetails={cancellation.ErrorDetails}");
                        LogMessage($"CANCELED: Did you update the subscription info?");
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                LogMessage($"ERROR during speech recognition: {ex.Message}");
                return null;
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
