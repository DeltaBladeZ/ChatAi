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
                if (!ChatAiSettings.Instance.EnableDebugLogging)
                {
                    return;
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
