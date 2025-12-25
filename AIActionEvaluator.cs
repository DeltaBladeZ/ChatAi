using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace ChatAi
{
    public class AIActionEvaluator
    {
        private readonly string _logFilePath = PathHelper.GetModFilePath("mod_log.txt");
        private NPCBehaviorLogic _npcBehaviorLogic = new NPCBehaviorLogic();
        private Hero _currentNpc;

        // Dictionary to track hiring costs for each NPC
        private Dictionary<string, int> _npcHiringCosts = new Dictionary<string, int>();

        public enum Action
        {
            None,
            PatrolAroundTown,
            DeliverMessage,
            GoToLocation,
            GoToSettlement,
            JoinParty,
            OfferToJoinParty, // New action for the NPC to offer joining with a cost
            AcceptJoinOffer    // New action for player accepting the offer
        }

        public enum NpcType
        {
            Wanderer,
            PartyMember,
            FactionLeader,
            Villager,
            Unknown
        }

        // Unified weight factors
        private readonly float RelationshipWeight = 0.5f;
        private readonly float RenownWeight = 0.3f;
        private readonly float BaseWeight = 0.2f;
        private readonly float ActionThreshold = 0.5f;

        // Get the hiring cost for an NPC
        public async Task<int> GetAIDeterminedHiringCost(Hero npc)
        {
            // Check if we already have a cached cost
            if (_npcHiringCosts.ContainsKey(npc.StringId))
            {
                return _npcHiringCosts[npc.StringId];
            }
            
            try
            {
                // Calculate a base cost for reference
                int baseCost = _npcBehaviorLogic.CalculateWandererHiringCost(npc);
                
                // Prepare information about the NPC for the AI to use in determining the price
                string npcInfo = BuildNpcInfoForPricing(npc);
                
                // Ask the AI to determine the hiring cost
                string prompt = $"You are an NPC named {npc.Name} in the medieval game Bannerlord. " +
                                $"Based on your character's traits and information below, determine how many 'denars' (gold coins) " +
                                $"you would ask from the player to join their party as a companion.\n\n" +
                                $"Your information:\n{npcInfo}\n\n" +
                                $"The standard market rate for someone of your skills would be around {baseCost} denars.\n\n" +
                                $"Consider your personality, background, current situation, and relationship with the player. " +
                                $"If you're greedy, you might ask for more. If you're generous or desperate, you might ask for less.\n\n" +
                                $"Respond with ONLY a number between 500 and 5000. No explanations, just the number.";
                
                LogMessage($"Asking AI to determine hiring cost for {npc.Name} with base cost suggestion of {baseCost}");
                
                string response = await AIHelper.GetResponse(prompt);
                LogMessage($"AI response for pricing: {response}");
                
                // Parse the response to get the cost
                int aiDeterminedCost = ParseAIDeterminedCost(response, baseCost);
                
                // Cache the result
                _npcHiringCosts[npc.StringId] = aiDeterminedCost;
                
                LogMessage($"Final AI-determined cost for {npc.Name}: {aiDeterminedCost} denars");
                return aiDeterminedCost;
            }
            catch (Exception ex)
            {
                LogMessage($"Error determining AI cost for {npc.Name}: {ex.Message}");
                
                // Fallback to calculated cost
                int fallbackCost = _npcBehaviorLogic.CalculateWandererHiringCost(npc);
                _npcHiringCosts[npc.StringId] = fallbackCost;
                return fallbackCost;
            }
        }
        
        private string BuildNpcInfoForPricing(Hero npc)
        {
            var info = new StringBuilder();
            
            // Basic info
            info.AppendLine($"Name: {npc.Name}");
            info.AppendLine($"Gender: {(npc.IsFemale ? "Female" : "Male")}");
            info.AppendLine($"Occupation: {npc.Occupation}");
            info.AppendLine($"Culture: {npc.Culture?.Name?.ToString() ?? "Unknown"}");
            info.AppendLine($"Age: {npc.Age:F0}");
            
            // Skills and combat abilities
            info.AppendLine($"Level: {npc.Level}");
            
            // Relationship with player
            info.AppendLine($"Relationship with player: {npc.GetRelationWithPlayer()}");
            
            // Personality traits
            string personality = GetPersonalityDescription(npc);
            info.AppendLine($"Personality: {personality}");
            
            // Current situation
            info.AppendLine($"Currently in settlement: {npc.CurrentSettlement?.Name?.ToString() ?? "No"}");
            
            return info.ToString();
        }
        
        private string GetPersonalityDescription(Hero npc)
        {
            if (npc == null) return "Unknown personality";

            var descriptions = new List<string>();

            // Calculating
            var calculating = npc.GetTraitLevel(TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultTraits.Calculating);
            if (calculating == 2) descriptions.Add("Cerebral");
            else if (calculating == 1) descriptions.Add("Calculating");
            else if (calculating == -1) descriptions.Add("Impulsive");
            else if (calculating == -2) descriptions.Add("Hotheaded");

            // Generosity
            var generosity = npc.GetTraitLevel(TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultTraits.Generosity);
            if (generosity == 2) descriptions.Add("Munificent");
            else if (generosity == 1) descriptions.Add("Generous");
            else if (generosity == -1) descriptions.Add("Closefisted");
            else if (generosity == -2) descriptions.Add("Tightfisted");

            // Honor
            var honor = npc.GetTraitLevel(TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultTraits.Honor);
            if (honor == 2) descriptions.Add("Honorable");
            else if (honor == 1) descriptions.Add("Honest");
            else if (honor == -1) descriptions.Add("Devious");
            else if (honor == -2) descriptions.Add("Deceitful");

            // Mercy
            var mercy = npc.GetTraitLevel(TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultTraits.Mercy);
            if (mercy == 2) descriptions.Add("Compassionate");
            else if (mercy == 1) descriptions.Add("Merciful");
            else if (mercy == -1) descriptions.Add("Cruel");
            else if (mercy == -2) descriptions.Add("Sadistic");

            // Valor
            var valor = npc.GetTraitLevel(TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultTraits.Valor);
            if (valor == 2) descriptions.Add("Fearless");
            else if (valor == 1) descriptions.Add("Daring");
            else if (valor == -1) descriptions.Add("Cautious");
            else if (valor == -2) descriptions.Add("Very Cautious");

            // Combine descriptions into a single string
            return descriptions.Count > 0 ? string.Join(", ", descriptions) : "A balanced personality.";
        }
        
        private int ParseAIDeterminedCost(string response, int defaultCost)
        {
            try
            {
                // Try to extract just numeric characters from the response
                string numericPart = new string(response.Where(c => char.IsDigit(c)).ToArray());
                
                if (string.IsNullOrEmpty(numericPart))
                {
                    LogMessage($"Couldn't extract numeric price from AI response: {response}");
                    return defaultCost;
                }
                
                if (int.TryParse(numericPart, out int cost))
                {
                    // Ensure the cost is within reasonable bounds
                    cost = Math.Max(500, Math.Min(cost, 5000));
                    
                    // Round to nearest 50
                    cost = (int)(Math.Round(cost / 50.0) * 50);
                    
                    return cost;
                }
                
                LogMessage($"Failed to parse numeric price: {numericPart}");
                return defaultCost;
            }
            catch (Exception ex)
            {
                LogMessage($"Error parsing AI price: {ex.Message}");
                return defaultCost;
            }
        }

        // Get the hiring cost for an NPC using the old method
        public int GetHiringCost(Hero npc)
        {
            if (_npcHiringCosts.ContainsKey(npc.StringId))
            {
                return _npcHiringCosts[npc.StringId];
            }
            
            // Calculate and save the cost
            int cost = _npcBehaviorLogic.CalculateWandererHiringCost(npc);
            _npcHiringCosts[npc.StringId] = cost;
            return cost;
        }

        // Detect the type of NPC
        // Detect the type and occupation of the NPC
        // Detect the type and occupation of the NPC dynamically
        private (NpcType npcType, string occupation) DetectNpcType(Hero npc)
        {
            // Dynamically determine the occupation by converting the Occupation enum to a string
            string occupation = npc.Occupation.ToString() ?? "Unknown";

            if (npc.PartyBelongedTo == Hero.MainHero.PartyBelongedTo)
            {
                // If the NPC is in the player's party
                return (NpcType.PartyMember, occupation);
            }
            else if (npc.IsFactionLeader)
            {
                // If the NPC is a faction leader
                return (NpcType.FactionLeader, occupation);
            }
            else if (npc.CurrentSettlement != null && npc.CurrentSettlement.IsVillage)
            {
                // If the NPC is in a village
                return (NpcType.Villager, occupation);
            }
            else if (npc.IsWanderer)
            {
                // If the NPC is a wanderer
                return (NpcType.Wanderer, occupation);
            }

            // Default to Unknown if no other type matches
            return (NpcType.Unknown, occupation);
        }

        public async Task<(Action, Settlement, string, int)> EvaluateActionWithTargetAndMessageAndCost(Hero npc, string playerInput)
        {
            LogMessage($"============================================================");
            LogMessage($"AiActionEvaluator Started: Evaluating action for NPC {npc.Name} with player input: {playerInput}");
            LogMessage($"============================================================");
            try
            {
                // Set _currentNpc for use in other methods
                _currentNpc = npc;
                
                // TESTING
                try
                {
                    var occupations = Enum.GetValues(typeof(Occupation));

                    foreach (var occupation1 in occupations)
                    {
                       // LogMessage($"DEBUG: List of occupations: {occupation1}");
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"DEBUG: Error listing occupations: {ex.Message}");
                }

                // Detect the type and occupation of the NPC
                var (npcType, occupation) = DetectNpcType(npc);

                // Log the detected type and occupation
                LogMessage($"Detected NPC Type for {npc.Name}: {npcType}, Occupation: {occupation}");

                // Check for join party acceptance from player
                if (npcType == NpcType.Wanderer || occupation.Contains("Wanderer"))
                {
                    // Check if a hiring price has already been determined for this NPC
                    bool hasExistingOffer = _npcHiringCosts.ContainsKey(npc.StringId);
                    
                    // Check for join party request - this should be processed before accept hire
                    string joinIntent = await AnalyzeJoinPartyIntent(playerInput);
                    if (joinIntent.IndexOf("Join Party", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        LogMessage($"Detected join party request for {npc.Name}");
                        
                        // Ask the AI to determine the hiring cost based on NPC's personality
                        int hiringCost = await GetAIDeterminedHiringCost(npc);
                        LogMessage($"AI determined hiring cost for {npc.Name}: {hiringCost}");
                        
                        // Check player's current gold and include it in the response
                        int playerGold = Hero.MainHero.Gold;
                        LogMessage($"Player's current gold: {playerGold}");
                        
                        string costMessage;
                        if (playerGold < hiringCost)
                        {
                            costMessage = $"{npc.Name} is willing to join your party for {hiringCost} denars, but notices you only have {playerGold} denars. \"You'll need more coin before I can join you.\"";
                        }
                        else
                        {
                            costMessage = $"{npc.Name} is willing to join your party for {hiringCost} denars.";
                        }
                        
                        // Return OfferToJoinParty so the conversation can continue with negotiation
                        return (Action.OfferToJoinParty, null, costMessage, hiringCost);
                    }
                    
                    // Only check for acceptance if we have a previous offer (hire cost) set
                    if (hasExistingOffer)
                    {
                        // Check if this is the player accepting a previous offer
                        NPCContext context = ChatBehavior.Instance?.GetOrCreateNPCContextForAnalysis(npc);
                        string acceptIntent = await AnalyzeAcceptHireIntent(playerInput, context);
                        if (acceptIntent.IndexOf("Accept Hire", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            LogMessage($"Detected player accepting to hire {npc.Name}");
                            int hiringCost = _npcHiringCosts[npc.StringId];
                            
                            // Check if player can afford
                            if (_npcBehaviorLogic.CanPlayerAffordHiring(hiringCost))
                            {
                                return (Action.AcceptJoinOffer, null, $"{npc.Name} is happy to join your party for {hiringCost} denars.", hiringCost);
                            }
                            else
                            {
                                int playerGold = Hero.MainHero.Gold;
                                return (Action.None, null, $"{npc.Name} looks at your coin purse with disappointment. \"You only have {playerGold} denars. You need {hiringCost} denars to hire me.\"", 0);
                            }
                        }
                        // Add fallback for ambiguous responses that might be acceptances
                        else if (acceptIntent == "None" && (
                                 playerInput.Contains("yes") || playerInput.Contains("ok") || 
                                 playerInput.Contains("sure") || playerInput.Contains("fine") || 
                                 playerInput.Contains("agree") || playerInput.Contains("accept") ||
                                 playerInput.Contains("join") || playerInput.Contains("deal")))
                        {
                            // Implement backup plan for when intent analysis returns None but player might be accepting
                            LogMessage($"Intent analysis returned 'None' for potential acceptance: '{playerInput}'");
                            
                            int hiringCost = _npcHiringCosts[npc.StringId];
                            
                            // Check if player can afford
                            if (_npcBehaviorLogic.CanPlayerAffordHiring(hiringCost))
                            {
                                LogMessage($"Using backup acceptance plan: Accepting the offer for {hiringCost} denars");
                                return (Action.AcceptJoinOffer, null, $"{npc.Name} nods. \"Very well, I'll join your party for {hiringCost} denars.\"", hiringCost);
                            }
                            else
                            {
                                int playerGold = Hero.MainHero.Gold;
                                LogMessage($"Player cannot afford hiring cost ({playerGold}/{hiringCost})");
                                return (Action.None, null, $"{npc.Name} looks at your coin purse. \"You only have {playerGold} denars. You need {hiringCost} denars to hire me.\"", 0);
                            }
                        }
                    }
                }

                // Handle other actions
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
                            return (action, targetSettlement, $"{npc.Name} agrees to travel to {targetSettlement.Name}. With the reasons: {reason}", 0);
                        else
                            return (Action.None, null, $"{npc.Name} decides not to travel to {targetSettlement.Name}. With the reasons: {reason}", 0);
                    }

                    return (Action.None, null, $"{npc.Name} cannot find the specified location.", 0);
                }

                // Handle other actions
                var actionForIntent = DecideAction(npc, intent);
                if (actionForIntent == Action.None)
                    return (Action.None, null, null, 0);

                var (weightForAction, reasonForAction) = CalculateWeight(npc);

                if (weightForAction >= ActionThreshold)
                    return (actionForIntent, null, $"{npc.Name} acknowledges the command and will act accordingly. With the reasons: {reasonForAction}", 0);
                else
                    return (Action.None, null, $"{npc.Name} decides not to act. With the reasons: {reasonForAction}", 0);
            }
            catch (Exception ex)
            {
                LogMessage($"DEBUG: Action evaluation failed for NPC {npc.Name}: {ex.Message}");
                return (Action.None, null, $"{npc.Name} is confused and cannot act.", 0);
            }
        }

        // Compatibility method for legacy code
        public async Task<(Action, Settlement, string)> EvaluateActionWithTargetAndMessage(Hero npc, string playerInput)
        {
            var result = await EvaluateActionWithTargetAndMessageAndCost(npc, playerInput);
            return (result.Item1, result.Item2, result.Item3);
        }

        private Action DecideAction(Hero npc, string intent)
        {
            // Match intent to action
            return intent switch
            {
                "Patrol Around Town" => Action.PatrolAroundTown,
                "Deliver Message" => Action.DeliverMessage,
                "Join Party" => Action.JoinParty,
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

            return (totalWeight, reason);
        }

        private async Task<string> AnalyzeIntent(string playerInput)
        {
            string prompt = $"The player said: '{playerInput}'. Determine the intent from the following: [Go to Location, None]. Only return the intent." +
                            "Intructions: If the player wants the NPC to go to a location, return 'Go to Location'. If the player does not say anything related to one of the intents, return 'None'."+
                            "Examples: If the player says 'Go to the Sargot', the intent is 'Go to Location'. If the player says 'I am lost and dont know my location', the intent is 'None'.";


            string response = await AIHelper.GetResponse(prompt);
            return response.Trim();
        }

        private async Task<string> AnalyzeJoinPartyIntent(string playerInput)
        {
            string prompt = $"The player said: '{playerInput}'. Determine if the player is asking the NPC to join their party. " +
                            "Return only 'Join Party' if the player is asking the NPC to join, travel with, accompany, or become a companion in their party. " +
                            "Return 'None' if the player is not asking the NPC to join their party. " +
                            "Examples: " +
                            "If the player says 'Would you like to join my party?' return 'Join Party'. " +
                            "If the player says 'Come with me as a companion' return 'Join Party'. " + 
                            "If the player says 'I need you to travel with me' return 'Join Party'. " +
                            "If the player says 'I am looking for companions' return 'Join Party'. " +
                            "If the player says 'Tell me about yourself' return 'None'.";

            string response = await AIHelper.GetResponse(prompt);
            return response.Trim();
        }
        
        private async Task<string> AnalyzeAcceptHireIntent(string playerInput, NPCContext context = null)
        {
            if (string.IsNullOrWhiteSpace(playerInput))
                return "None";

            int hiringCost = 0;
            if (_currentNpc != null && _npcHiringCosts.ContainsKey(_currentNpc.StringId))
            {
                hiringCost = _npcHiringCosts[_currentNpc.StringId];
            }

            string prompt = $@"The NPC has just offered to join the player's party for {hiringCost} denars (gold coins).
Analyze if the player's response indicates they are accepting the hire offer.

Examples of accepting the offer:
- ""I accept your offer""
- ""Sure, I'll pay you to join me""
- ""Yes, I agree to your terms""
- ""That's a fair price, welcome aboard""
- ""You're hired""
- ""Deal""
- Simple affirmations like ""yes"", ""ok"", ""sure"", ""fine"", ""agree"", ""join""

Return ONLY ""Accept Hire"" if the player is accepting the offer.
Return ONLY ""None"" if the player is clearly refusing or changing the subject.

";

            // Add context from recent messages if available
            if (context != null)
            {
                var recentMessages = context.GetRecentMessages(3);
                if (recentMessages.Count > 0)
                {
                    prompt += "Previous messages for context:\n";
                    foreach (var msg in recentMessages)
                    {
                        string role = msg.StartsWith("User:") ? "Player" : "NPC";
                        prompt += $"{role}: {msg.Substring(msg.IndexOf(':') + 1).Trim()}\n";
                    }
                    prompt += "\n";
                }
            }

            prompt += $"Player's response: {playerInput}\n\nIntent:";

            string intent = await AIHelper.GetResponse(prompt);
            LogMessage($"Accept Hire Intent Analysis: '{playerInput}' -> '{intent}'");
            return intent.Trim();
        }

        private void LogMessage(string message)
        {
            try
            {
                // Settings may not be initialized yet on older Bannerlord versions.
                if (!SettingsUtil.IsDebugLoggingEnabled())
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
    }
}
