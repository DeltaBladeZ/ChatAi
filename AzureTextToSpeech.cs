using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using NAudio.Wave;

namespace ChatAi
{
    public class AzureTextToSpeech
    {
        private static string _deferredAudioPath;
        private static CancellationTokenSource _playbackCancellationTokenSource;

        public static void CancelPlayback()
        {
            if (_playbackCancellationTokenSource != null)
            {
                _playbackCancellationTokenSource.Cancel();
                _playbackCancellationTokenSource.Dispose();
                _playbackCancellationTokenSource = null;
            }
        }

        public async Task GenerateSpeech(string text, bool isFemale)
        {
            // Check if Azure is selected as the voice backend
            if (ChatAiSettings.Instance.VoiceBackend?.SelectedValue != "Azure")
            {
                LogDebug("Azure TTS skipped - Azure is not selected as the voice backend.");
                return;
            }

            string subscriptionKey = ChatAiSettings.Instance.AzureTTSKey;
            string region = ChatAiSettings.Instance.AzureTTSRegion;

            if (string.IsNullOrWhiteSpace(subscriptionKey) || string.IsNullOrWhiteSpace(region))
            {
                LogDebug("Azure TTS key or region is missing. Please configure the settings in the mod menu.");
                return;
            }

            try
            {
                LogDebug("============================================================");
                LogDebug("Azure TTS Initialization Started");
                LogDebug("============================================================");

                var speechConfig = SpeechConfig.FromSubscription(subscriptionKey, region);

                // Select voice based on gender
                string voiceName = isFemale ? ChatAiSettings.Instance.FemaleVoice : ChatAiSettings.Instance.MaleVoice;
                speechConfig.SpeechSynthesisVoiceName = voiceName;

                LogDebug($"Selected voice: {voiceName}");

                // Save the audio to a temporary file
                string audioFilePath = PathHelper.GetModFilePath("temp_audio.wav");

                LogDebug($"Audio file will be saved to: {audioFilePath}");

                using (var audioConfig = AudioConfig.FromWavFileOutput(audioFilePath))
                using (var synthesizer = new SpeechSynthesizer(speechConfig, audioConfig))
                {
                    var result = await synthesizer.SpeakTextAsync(text);

                    if (result.Reason == ResultReason.SynthesizingAudioCompleted)
                    {
                        LogDebug("Speech synthesis completed successfully.");
                        _deferredAudioPath = audioFilePath;
                        LogDebug($"Deferred audio path set to: {_deferredAudioPath}");
                    }
                    else
                    {
                        LogDebug($"Speech synthesis failed: {result.Reason}");
                        if (result.Reason == ResultReason.Canceled)
                        {
                            var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                            LogDebug($"Error Details: {cancellation.ErrorDetails}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Azure TTS encountered an error: {ex.Message}");
            }

            LogDebug("============================================================");
            LogDebug("Azure TTS Process Completed");
            LogDebug("============================================================");
        }

        public static async void PlayDeferredAudio()
        {
            var instance = new AzureTextToSpeech();
            instance.LogDebug($"Attempting to play deferred audio. Current path: {_deferredAudioPath}");

            if (string.IsNullOrWhiteSpace(_deferredAudioPath))
            {
                instance.LogDebug("No deferred audio to play.");
                return;
            }

            if (!File.Exists(_deferredAudioPath))
            {
                instance.LogDebug($"Deferred audio file does not exist: {_deferredAudioPath}");
                _deferredAudioPath = null;
                return;
            }

            // Initialize the cancellation token source
            _playbackCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _playbackCancellationTokenSource.Token;

            await Task.Run(() =>
            {
                try
                {
                    using (var audioFileReader = new AudioFileReader(_deferredAudioPath))
                    using (var outputDevice = new WaveOutEvent())
                    {
                        outputDevice.Init(audioFileReader);
                        outputDevice.Play();

                        instance.LogDebug("Audio playback started asynchronously.");

                        // Wait for playback to finish or cancellation
                        while (outputDevice.PlaybackState == PlaybackState.Playing)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                instance.LogDebug("Playback cancellation requested. Stopping playback...");
                                outputDevice.Stop();
                                break;
                            }

                            System.Threading.Thread.Sleep(100);
                        }
                    }

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        instance.LogDebug("Deferred audio played successfully.");
                    }
                }
                catch (Exception ex)
                {
                    instance.LogDebug($"Error playing audio with NAudio: {ex.Message}");
                }
                finally
                {
                    _playbackCancellationTokenSource?.Dispose();
                    _playbackCancellationTokenSource = null;
                    _deferredAudioPath = null; // Clear the path after playback
                }
            });
        }

        /// <summary>
        /// Logs debug messages to the console or mod log.
        /// </summary>
        /// <param name="message">The debug message.</param>
        private void LogDebug(string message)
        {
            string logFilePath = PathHelper.GetModFilePath("mod_log.txt");
            try
            {
                if (!SettingsUtil.IsDebugLoggingEnabled())
                {
                    return;
                }

                string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}\n";
                System.IO.File.AppendAllText(logFilePath, logMessage);
            }
            catch (Exception ex)
            {

            }
        }
    }
}
