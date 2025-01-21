using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.LinQuick;

namespace ChatAi
{
    public class AIActionEvaluator
    {
        private readonly string _logFilePath = Path.Combine(BasePath.Name, "Modules", "ChatAi", "mod_log.txt");

        public enum Action
        {
            None,
            PatrolAroundTown,
            DeliverMessage,
            GoToLocation,
            GoToSettlement
        }

        // Unified weight factors
        private readonly float RelationshipWeight = 0.5f;
        private readonly float RenownWeight = 0.3f;
        private readonly float BaseWeight = 0.2f;
        private readonly float ActionThreshold = 0.5f;

        // Evaluate actions with logic for different intents
        public async Task<(Action, Settlement, string)> EvaluateActionWithTargetAndMessage(Hero npc, string playerInput)
        {
            try
            {
                
                string intent = await AnalyzeIntent(playerInput);


                if (intent.IndexOf("Go to Location", StringComparison.OrdinalIgnoreCase) >= 0)

                {
                    var settlementName = ExtractSettlementName(playerInput);
                    var targetSettlement = Settlement.All.FirstOrDefault(s =>
                        s.Name.ToString().Equals(settlementName, StringComparison.OrdinalIgnoreCase));

                    if (targetSettlement != null)
                    {
                        var action = Action.GoToSettlement;
                        var (weight, reason) = CalculateWeight(npc);

                        if (weight >= ActionThreshold)
                            return (action, targetSettlement, $"{npc.Name} agrees to travel to {targetSettlement.Name}. With the reasons: {reason}");
                        else
                            return (Action.None, null, $"{npc.Name} decides not to travel to {targetSettlement.Name}. With the reasons: {reason}");
                    }

                    return (Action.None, null, $"{npc.Name} cannot find the specified location.");
                }

                // Handle other actions
                var actionForIntent = DecideAction(npc, intent);
                if (actionForIntent == Action.None)
                    return (Action.None, null, null);

                var (weightForAction, reasonForAction) = CalculateWeight(npc);

                if (weightForAction >= ActionThreshold)
                    return (actionForIntent, null, $"{npc.Name} acknowledges the command and will act accordingly. With the reasons: {reasonForAction}");
                else
                    return (Action.None, null, $"{npc.Name} decides not to act. With the reasons: {reasonForAction}");
            }
            catch (Exception ex)
            {
                LogMessage($"DEBUG: Action evaluation failed for NPC {npc.Name}: {ex.Message}");
                return (Action.None, null, $"{npc.Name} is confused and cannot act.");
            }
        }

        private Action DecideAction(Hero npc, string intent)
        {
            // Match intent to action
            return intent switch
            {
                "Patrol Around Town" => Action.PatrolAroundTown,
                "Deliver Message" => Action.DeliverMessage,
                _ => Action.None
            };
        }

        private string ExtractSettlementName(string input)
        {
            var words = input.Split(' ');
            foreach (var word in words)
            {
                if (Settlement.All.Any(s => s.Name.ToString().Equals(word, StringComparison.OrdinalIgnoreCase)))
                {
                    return word;
                }
            }
            return null;
        }

        private (float weight, string reason) CalculateWeight(Hero npc)
        {
            float relationshipWeight = npc.GetRelationWithPlayer() / 100f;
            float playerRenown = Hero.MainHero.Clan?.Renown ?? 0;
            float npcRenown = npc.Clan?.Renown ?? 0;
            float renownWeight = (playerRenown - npcRenown) / 2000f;

            float totalWeight = relationshipWeight + renownWeight;

            List<string> reasons = new();

            if (relationshipWeight < 0)
                reasons.Add("Your relationship with me is too low.");
            else if (relationshipWeight > 0)
                reasons.Add("We have a good relationship.");

            if (renownWeight > 0)
                reasons.Add("Your renown exceeds mine.");
            else if (renownWeight < 0)
                reasons.Add("Your renown is too low compared to mine.");

            totalWeight = Math.Max(totalWeight, 0);
            string reason = string.Join(" ", reasons);

         // LogMessage($"DEBUG: Calculated weight for NPC {npc.Name}: Relationship Weight = {relationshipWeight}, Renown Weight = {renownWeight}, Total Weight = {totalWeight}, Reason = {reason}");

            return (totalWeight, reason);
        }

        private async Task<string> AnalyzeIntent(string playerInput)
        {
            string prompt = $"The player said: '{playerInput}'. Determine the intent from the following: [Go to Location, None]. Only return the intent.";
 
            string response = await AIHelper.GetResponse(prompt);
            return response.Trim();
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
                File.AppendAllText(_logFilePath, logMessage);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"Logging error: {ex.Message}"));
            }
        }

    }

}
