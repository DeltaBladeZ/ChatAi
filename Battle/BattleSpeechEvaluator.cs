using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TaleWorlds.Library;

namespace ChatAi.Battle
{
    public class BattleSpeechEvaluator
    {
        /// <summary>
        /// Returns a morale boost score from 1..10 based on how rousing/effective the speech is.
        /// </summary>
        public async Task<int> EvaluateBoostAsync(string speechText)
        {
            if (string.IsNullOrWhiteSpace(speechText))
                return 0;

            string prompt =
                "You are evaluating a short battlefield speech.\n" +
                "Return ONLY an integer from 1 to 10 for how much it would boost morale.\n" +
                "1 = barely motivating, 10 = extremely rousing.\n" +
                "Speech: \"" + speechText + "\"\n" +
                "Score:";

            string response = string.Empty;
            try
            {
                response = await AIHelper.GetResponse(prompt);
            }
            catch (Exception ex)
            {
                if (ChatAi.SettingsUtil.IsDebugLoggingEnabled())
                {
                    InformationManager.DisplayMessage(new InformationMessage($"[DynamicBattles][SpeechEval] Error: {ex.Message}"));
                }
                return 0;
            }

            int score = ParseScore(response);
            return Math.Max(1, Math.Min(10, score));
        }

        private static int ParseScore(string response)
        {
            if (string.IsNullOrWhiteSpace(response)) return 0;
            // Extract first integer
            var m = Regex.Match(response, "-?\\d+");
            if (!m.Success) return 0;
            if (int.TryParse(m.Value, out int val)) return val;
            return 0;
        }
    }
}


