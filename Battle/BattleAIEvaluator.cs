using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TaleWorlds.Library;

namespace ChatAi.Battle
{
    public class BattleAIEvaluator
    {
        public enum IntentType
        {
            None,
            Order,
            Speech
        }

        public class IntentResult
        {
            public IntentType Type { get; set; } = IntentType.None;
            public string Order { get; set; } = string.Empty;   // e.g., Charge, Retreat, HoldPosition, FormationShieldWall, HoldFire, FireAtWill
            public string Target { get; set; } = "All";         // All, Infantry, Archers, Cavalry
            public string Raw { get; set; } = string.Empty;     // Raw model output for debugging
        }

        public async Task<IntentResult> EvaluateAsync(string playerText)
        {
            if (string.IsNullOrWhiteSpace(playerText))
            {
                return new IntentResult();
            }

            // Build a compact instruction to minimize latency
            string prompt =
                "Classify the player's battle command. Respond as a single-line JSON only.\n" +
                "Schema: {\"intent\":\"Order|Speech|None\",\"order\":\"Charge|Retreat|HoldPosition|FollowMe|FormationShieldWall|FormationLine|FormationSquare|FormationWedge|HoldFire|FireAtWill|None\",\"target\":\"All|Infantry|Archers|Cavalry\"}\n" +
                "Player: \"" + playerText + "\"\n" +
                "If unsure, use intent \"None\".";

            string response = string.Empty;
            try
            {
                response = await AIHelper.GetResponse(prompt);
            }
            catch (Exception ex)
            {
                if (ChatAi.SettingsUtil.IsDebugLoggingEnabled())
                {
                    InformationManager.DisplayMessage(new InformationMessage($"[DynamicBattles] AI error: {ex.Message}"));
                }
            }

            // Try to parse structured output, else fallback to heuristic
            var parsed = ParseStructured(response);
            if (parsed != null)
            {
                parsed.Raw = response ?? string.Empty;
                return parsed;
            }

            // Heuristic fallback
            var heuristic = HeuristicClassify(playerText);
            heuristic.Raw = response ?? string.Empty;
            return heuristic;
        }

        private static IntentResult ParseStructured(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return null;
            try
            {
                // Extract first {...} block if the model wrapped content
                var m = Regex.Match(response, "\\{[\\s\\S]*\\}");
                string json = m.Success ? m.Value : response;
                var jo = JObject.Parse(json);
                string intent = (jo["intent"]?.ToString() ?? "None").Trim();
                string order = (jo["order"]?.ToString() ?? "None").Trim();
                string target = (jo["target"]?.ToString() ?? "All").Trim();

                IntentType type = intent.Equals("Order", StringComparison.OrdinalIgnoreCase)
                    ? IntentType.Order
                    : intent.Equals("Speech", StringComparison.OrdinalIgnoreCase)
                        ? IntentType.Speech
                        : IntentType.None;

                if (type == IntentType.None)
                    order = "None";

                // Normalize target
                if (!IsKnownTarget(target)) target = "All";

                return new IntentResult
                {
                    Type = type,
                    Order = order,
                    Target = target
                };
            }
            catch
            {
                return null;
            }
        }

        private static bool IsKnownTarget(string t)
        {
            if (string.IsNullOrWhiteSpace(t)) return false;
            switch (t.Trim().ToLowerInvariant())
            {
                case "all":
                case "infantry":
                case "archers":
                case "cavalry":
                    return true;
                default:
                    return false;
            }
        }

        private static IntentResult HeuristicClassify(string input)
        {
            string text = input.ToLowerInvariant();
            string target = "All";
            if (text.Contains("infantry")) target = "Infantry";
            else if (text.Contains("archer") || text.Contains("bow")) target = "Archers";
            else if (text.Contains("cavalry") || text.Contains("horse")) target = "Cavalry";

            if (ContainsAny(text, "charge", "attack"))
                return Order("Charge", target);
            if (ContainsAny(text, "retreat", "fall back", "fallback"))
                return Order("Retreat", target);
            if (ContainsAny(text, "hold position", "hold ground", "hold here"))
                return Order("HoldPosition", target);
            if (ContainsAny(text, "follow me", "on me"))
                return Order("FollowMe", target);

            if (ContainsAny(text, "shield wall"))
                return Order("FormationShieldWall", target);
            if (ContainsAny(text, "line formation", "form line"))
                return Order("FormationLine", target);
            if (ContainsAny(text, "square formation", "form square"))
                return Order("FormationSquare", target);
            if (ContainsAny(text, "wedge formation", "form wedge"))
                return Order("FormationWedge", target);

            if (ContainsAny(text, "hold fire", "cease fire"))
                return Order("HoldFire", target);
            if (ContainsAny(text, "fire at will"))
                return Order("FireAtWill", target);

            // If it looks like a motivational speech
            if (ContainsAny(text, "brave", "courage", "honor", "victory", "glory"))
            {
                return new IntentResult { Type = IntentType.Speech, Order = "None", Target = target };
            }

            return new IntentResult();
        }

        private static bool ContainsAny(string text, params string[] phrases)
        {
            foreach (var p in phrases)
            {
                if (text.Contains(p)) return true;
            }
            return false;
        }

        private static IntentResult Order(string order, string target)
        {
            return new IntentResult
            {
                Type = IntentType.Order,
                Order = order,
                Target = target
            };
        }
    }
}

