using System;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ScreenSystem;
using TaleWorlds.InputSystem;
using System.Threading;
using System.Threading.Tasks;


namespace ChatAi.Battle
{
    public class BattleTextInputBehavior : MissionBehavior
    {
        private DateTime _lastInvoke = DateTime.MinValue;
        private readonly TimeSpan _cooldown = TimeSpan.FromMilliseconds(500);
        private bool _promptActive;
        private bool _pendingPrompt;
        private DateTime _pendingPromptAt = DateTime.MinValue;
        private readonly BattleSttController _stt = new BattleSttController();
        private CancellationTokenSource? _alwaysOnCts;
        private bool _alwaysOnStarted;
        private bool _hotkeySttSessionStarted;
        

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        public override void OnMissionTick(float dt)
        {
            try
            {
                var settings = ChatAiSettings.Instance;
                if (settings == null || !settings.EnableDynamicBattles)
                {
                    return;
                }

                // Some Bannerlord versions will instantly "confirm" a newly opened TextInquiry if we open it
                // on the same key-release event (notably for Enter/Space/Tab). To stay compatible, we schedule
                // the prompt for the next tick (tiny delay) only for these keys.
                if (_pendingPrompt && !_promptActive)
                {
                    var now2 = DateTime.UtcNow;
                    if (now2 >= _pendingPromptAt)
                    {
                        _pendingPrompt = false;
                        ShowTextInquiryPrompt();
                    }
                }

                // Always-on STT mode (no hotkey, no prompt)
                if (settings.EnableBattleSTT && settings.DynamicBattlesSttAlwaysOn)
                {
                    EnsureAlwaysOnSttLoop(settings);
                }
                else
                {
                    // If Always-On was turned off mid-mission, stop the loop
                    if (_alwaysOnStarted)
                    {
                        try
                        {
                            _alwaysOnCts?.Cancel();
                        }
                        catch { }
                    }
                }

                // Require hotkey to show the prompt
                var selectedHotkey = settings.BattleInputHotkey?.SelectedValue ?? "T";
                if (!IsTextInputHotkeyReleased(selectedHotkey))
                {
                    return;
                }

                if (!IsEligibleContext(settings))
                {
                    return;
                }

                // basic cooldown
                var now = DateTime.UtcNow;
                if (_promptActive) return;
                if (now - _lastInvoke < _cooldown) return;

                // Use default UI only (TextInquiry). Overlay disabled by design.
                _lastInvoke = now;
                TryDebug("Dynamic Battles: hotkey detected, opening command prompt.");

                // Always show the text prompt so the player can type.
                // Fallback: delay opening for keys that commonly auto-confirm on the same key-release event.
                if (RequiresDelayedPrompt(selectedHotkey))
                {
                    _pendingPrompt = true;
                    _pendingPromptAt = now.AddMilliseconds(50);
                }
                else
                {
                    ShowTextInquiryPrompt();
                }

                // If STT-on-hotkey is enabled, also listen in parallel so the player can speak instead.
                // If Always-On is enabled, don't start a competing hotkey STT session; Always-On will hear speech anyway.
                if (settings.EnableBattleSTT && settings.DynamicBattlesSttOnHotkey && !settings.DynamicBattlesSttAlwaysOn && !_stt.IsRunning)
                {
                    _hotkeySttSessionStarted = true;
                    _ = Task.Run(async () =>
                    {
                        bool ok = await _stt.StartHotkeySessionAsync();
                        TryDebug(ok ? "[STT] Hotkey session started." : "[STT] Hotkey session failed to start.");
                        if (!ok) _hotkeySttSessionStarted = false;
                    });
                }
            }
            catch
            {
                // fail-closed; never disrupt mission
            }
        }

        private static bool RequiresDelayedPrompt(string hotkey)
        {
            if (string.IsNullOrWhiteSpace(hotkey)) return false;
            switch (hotkey.Trim().ToUpperInvariant())
            {
                case "ENTER":
                case "SPACE":
                case "TAB":
                    return true;
                default:
                    return false;
            }
        }

        private void EnsureAlwaysOnSttLoop(ChatAiSettings settings)
        {
            try
            {
                if (_alwaysOnStarted) return;
                _alwaysOnStarted = true;
                _alwaysOnCts?.Cancel();
                _alwaysOnCts = new CancellationTokenSource();
                var token = _alwaysOnCts.Token;

                _ = Task.Run(async () =>
                {
                    TryDebug("[STT] Always-on loop started.");
                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            // If settings were toggled off mid-battle, stop loop
                            var st = ChatAiSettings.Instance;
                            if (st == null || !st.EnableDynamicBattles || !st.DynamicBattlesSttAlwaysOn)
                            {
                                break;
                            }

                            int timeout = st.BattleSTTTimeoutSeconds;
                            string? text = await _stt.CaptureOnceAsync(timeout, token);
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                TryDebug($"[STT] Heard: {text}");
                                HandleSubmittedText(text);
                            }

                            // small cooldown to avoid hammering STT backend
                            await Task.Delay(250, token);
                        }
                        catch
                        {
                            await Task.Delay(500, token);
                        }
                    }
                    TryDebug("[STT] Always-on loop stopped.");
                    _alwaysOnStarted = false;
                }, token);
            }
            catch
            {
                _alwaysOnStarted = false;
            }
        }

        private bool IsEligibleContext(ChatAiSettings s)
        {
            // Siege detection is non-trivial and API varies; skip for MVP to avoid false positives.
            // Future: add robust siege detection and add optional gating settings if needed.
            return true;
        }

        private void ShowTextInquiryPrompt()
        {
            try
            {
                var vm = new BattleTextInputVM();
                _promptActive = true;
                InformationManager.ShowTextInquiry(
                    new TextInquiryData(
                        "Dynamic Battles", 
                        "Type your command", 
                        true, true, 
                        "Send", "Cancel",
                        text =>
                        {
                            // If the user "sends" an empty message (common when closing with Enter),
                            // treat it like closing/cancel: stop STT and process recognized speech.
                            if (string.IsNullOrWhiteSpace(text))
                            {
                                StopHotkeySttSession(processRecognizedText: true);
                                _promptActive = false;
                                return;
                            }

                            // If player typed and sent, stop STT session but DO NOT process STT text (avoid duplicate actions).
                            StopHotkeySttSession(processRecognizedText: false);
                            vm.CommandText = text;
                            vm.ExecuteSend();
                            HandleSubmittedText(text);
                            _promptActive = false;
                        },
                        () =>
                        {
                            // If player closed the prompt, stop STT session AND process recognized text (speech path).
                            StopHotkeySttSession(processRecognizedText: true);
                            _promptActive = false;
                        }
                    )
                );
            }
            catch (Exception ex)
            {
                // Debug-only: don't spam users in battle unless debugging is enabled
                TryDebug($"Dynamic Battles input failed: {ex.Message}");
                _promptActive = false;
                StopHotkeySttSession(processRecognizedText: false);
            }
        }

        private void StopHotkeySttSession(bool processRecognizedText)
        {
            try
            {
                if (!_hotkeySttSessionStarted)
                    return;

                _hotkeySttSessionStarted = false;

                _ = Task.Run(async () =>
                {
                    string? recognized = await _stt.StopHotkeySessionAsync();
                    if (!processRecognizedText)
                    {
                        return;
                    }
                    if (string.IsNullOrWhiteSpace(recognized))
                    {
                        TryDebug("[STT] No speech recognized.");
                        return;
                    }
                    TryDebug($"[STT] Recognized: {recognized}");
                    HandleSubmittedText(recognized);
                });
            }
            catch { }
        }

        private async void HandleSubmittedText(string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text)) return;
                // Evaluate intent
                var evaluator = new BattleAIEvaluator();
                var result = await evaluator.EvaluateAsync(text);
                TryDebug($"Detected intent: Type={result.Type}, Order={result.Order}, Target={result.Target}");

                if (result.Type == BattleAIEvaluator.IntentType.Order)
                {
                    bool ok = BattleOrderMapper.TryExecute(Mission, result);
                    TryDebug(ok
                        ? $"Order executed successfully: {result.Order}@{result.Target}"
                        : $"Order execution not supported/failed: {result.Order}@{result.Target}");
                }
                else if (result.Type == BattleAIEvaluator.IntentType.Speech)
                {
                    TryDebug("Detected Speech. Scoring morale boost...");

                    var speechEval = new BattleSpeechEvaluator();
                    int boost = await speechEval.EvaluateBoostAsync(text);
                    TryDebug($"Speech score={boost}/10. Applying morale boost...");

                    bool applied = BattleMoraleEffects.TryApplyMoraleBoost(Mission, boost);
                    TryDebug(applied
                        ? $"Morale boost applied (+{boost})."
                        : "Morale boost could not be applied (no compatible morale API found).");
                }
                else
                {
                    TryDebug("No actionable intent detected.");
                }
            }
            catch { }
        }

        private static void TryDebug(string message)
        {
            try
            {
                var st = ChatAiSettings.Instance;
                if (st != null && st.EnableDebugLogging)
                {
                    // On-screen hint
                    InformationManager.DisplayMessage(new InformationMessage(message));
                    // File log
                    var logPath = PathHelper.GetModFilePath("mod_log.txt");
                    var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - [DynamicBattles] {message}\n";
                    System.IO.File.AppendAllText(logPath, line);
                }
            }
            catch { }
        }

        private static bool IsTextInputHotkeyReleased(string hotkey)
        {
            // Simple mapper for A-Z, 0-9; defaults to 'T'
            var key = MapHotkey(hotkey);
            try
            {
                var mgr = Mission.Current?.InputManager;
                if (mgr != null)
                {
                    return mgr.IsKeyReleased(key);
                }
            }
            catch { }
            return Input.IsKeyReleased(key);
        }

        private static InputKey MapHotkey(string keyText)
        {
            if (string.IsNullOrWhiteSpace(keyText)) return InputKey.T;
            keyText = keyText.Trim();
            if (keyText.Length == 1)
            {
                char c = char.ToUpperInvariant(keyText[0]);
                if (c >= 'A' && c <= 'Z')
                {
                    return (InputKey)((int)InputKey.A + (c - 'A'));
                }
                if (c >= '0' && c <= '9')
                {
                    // Map 0..9 -> D0..D9
                    return (InputKey)((int)InputKey.D0 + (c - '0'));
                }
            }
            // Fallbacks for common names
            switch (keyText.ToUpperInvariant())
            {
                case "SPACE": return InputKey.Space;
                case "ENTER": return InputKey.Enter;
                case "TAB": return InputKey.Tab;
                default: return InputKey.T;
            }
        }

        public override void OnRemoveBehavior()
        {
            base.OnRemoveBehavior();
            try
            {
                _hotkeySttSessionStarted = false;
                _pendingPrompt = false;
                _alwaysOnCts?.Cancel();
                _alwaysOnCts?.Dispose();
                _alwaysOnCts = null;
            }
            catch { }
            _alwaysOnStarted = false;
        }

         
    }
}

