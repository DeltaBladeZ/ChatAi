using System;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.Library;

namespace ChatAi.Battle
{
    public class BattleSttController
    {
        private int _isRunning = 0;
        private Player2SpeechToText? _player2Hotkey;
        private Task<string?>? _azureHotkeyTask;

        public bool IsRunning => Interlocked.CompareExchange(ref _isRunning, 0, 0) == 1;

        public async Task<string?> CaptureOnceAsync(int timeoutSeconds, CancellationToken token)
        {
            timeoutSeconds = Math.Max(5, Math.Min(120, timeoutSeconds));

            string backend = ChatAiSettings.Instance.STTBackend?.SelectedValue ?? "[None]";
            if (backend == "[None]")
            {
                return null;
            }

            if (Interlocked.Exchange(ref _isRunning, 1) == 1)
            {
                return null; // already running
            }

            try
            {
                if (backend == "Player2")
                {
                    var stt = new Player2SpeechToText();

                    // Best-effort language set
                    string lang = ChatAiSettings.Instance.STTLanguageCode;
                    if (!string.IsNullOrWhiteSpace(lang))
                    {
                        _ = stt.SetLanguageAsync(lang);
                    }

                    bool started = await stt.StartAsync(timeoutSeconds);
                    if (!started)
                    {
                        return null;
                    }

                    // Stop slightly before timeout (same rationale as ChatBehavior)
                    int stopDelayMs = Math.Max(1, timeoutSeconds - 1) * 1000;
                    try
                    {
                        await Task.Delay(stopDelayMs, token);
                    }
                    catch (TaskCanceledException)
                    {
                        // Cancel = stop early
                    }
                    catch { }

                    // Stop whenever we exit the delay (either timeout or cancellation)
                    return await stt.StopAsync();
                }

                if (backend == "Azure")
                {
                    // Azure recognizer is "single-shot" already
                    var stt = new AzureSpeechToText();
                    using (token.Register(() => { }))
                    {
                        return await stt.RecognizeSpeechAsync();
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                if (ChatAi.SettingsUtil.IsDebugLoggingEnabled())
                {
                    InformationManager.DisplayMessage(new InformationMessage($"[DynamicBattles][STT] Error: {ex.Message}"));
                }
                return null;
            }
            finally
            {
                Interlocked.Exchange(ref _isRunning, 0);
            }
        }

        /// <summary>
        /// Starts an STT session intended to run until the caller stops it (e.g., while a textbox is open).
        /// For Player2, we start with a max timeout and rely on StopHotkeySessionAsync to stop early.
        /// For Azure (single-shot), we start recognition and can only ignore the result if "stopped".
        /// </summary>
        public async Task<bool> StartHotkeySessionAsync()
        {
            string backend = ChatAiSettings.Instance.STTBackend?.SelectedValue ?? "[None]";
            if (backend == "[None]")
                return false;

            if (Interlocked.Exchange(ref _isRunning, 1) == 1)
                return false;

            try
            {
                if (backend == "Player2")
                {
                    _player2Hotkey = new Player2SpeechToText();
                    string lang = ChatAiSettings.Instance.STTLanguageCode;
                    if (!string.IsNullOrWhiteSpace(lang))
                    {
                        _ = _player2Hotkey.SetLanguageAsync(lang);
                    }

                    // Use maximum to approximate "no timeout", we'll stop explicitly when textbox closes.
                    bool started = await _player2Hotkey.StartAsync(120);
                    if (!started)
                    {
                        _player2Hotkey = null;
                        Interlocked.Exchange(ref _isRunning, 0);
                        return false;
                    }
                    return true;
                }

                if (backend == "Azure")
                {
                    // AzureSpeechToText is single-shot; we can't truly stop early with the current implementation.
                    var stt = new AzureSpeechToText();
                    _azureHotkeyTask = stt.RecognizeSpeechAsync();
                    return true;
                }

                Interlocked.Exchange(ref _isRunning, 0);
                return false;
            }
            catch
            {
                _player2Hotkey = null;
                _azureHotkeyTask = null;
                Interlocked.Exchange(ref _isRunning, 0);
                return false;
            }
        }

        public async Task<string?> StopHotkeySessionAsync()
        {
            try
            {
                // Player2: explicitly stop and return recognized text
                if (_player2Hotkey != null)
                {
                    return await _player2Hotkey.StopAsync();
                }

                // Azure: await the single-shot task (cannot stop early)
                if (_azureHotkeyTask != null)
                {
                    return await _azureHotkeyTask;
                }

                return null;
            }
            catch
            {
                return null;
            }
            finally
            {
                _player2Hotkey = null;
                _azureHotkeyTask = null;
                Interlocked.Exchange(ref _isRunning, 0);
            }
        }
    }
}


