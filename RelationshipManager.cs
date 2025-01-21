using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ChatAi
{
    public class RelationshipManager
    {
        private readonly string _logFilePath = Path.Combine(BasePath.Name, "Modules", "ChatAi", "mod_log.txt");

        // Relationship modifiers (default values, overridden by settings)
        private readonly Dictionary<string, int> _relationshipModifiers = new()
        {
            { "insult", -5 },
            { "compliment", 1 },
            { "neutral", 0 },
            { "praise", 3 },
            { "thank", 2 },
            { "challenge", -10 },
        };

        public async Task<int> UpdateRelationshipBasedOnMessage(Hero npc, string playerInput)
        {
            try
            {
                // Analyze the intent of the player's message
                string intent = await AnalyzeIntent(playerInput);
                LogMessage($"DEBUG: Relationship intent for NPC {npc.Name}: {intent}");

                // Get relationship modifier from settings or default
                int relationshipChange = _relationshipModifiers.ContainsKey(intent)
                    ? _relationshipModifiers[intent]
                    : 0;

                // Dynamically adjust values from settings
                relationshipChange = AdjustRelationshipChange(relationshipChange);

                if (relationshipChange != 0)
                {
                    // Update relationship
                    int currentRelation = (int)Math.Round(npc.GetRelationWithPlayer());
                    int newRelation = currentRelation + relationshipChange;

                    // Apply cap from settings
                    int relationCap = ChatAiSettings.Instance.MaxRelationshipChange;
                    if (newRelation > relationCap)
                    {
                        relationshipChange = relationCap - currentRelation; // Adjust change to hit the cap
                        newRelation = relationCap;
                    }

                    CharacterRelationManager.SetHeroRelation(Hero.MainHero, npc, newRelation);

                    // Optionally update the clan leader's relation
                    if (ChatAiSettings.Instance.EnableRelationshipTracking && npc.Clan?.Leader != null && npc != npc.Clan.Leader)
                    {
                        CharacterRelationManager.SetHeroRelation(Hero.MainHero, npc.Clan.Leader, newRelation);
                    }

                    // Output relationship change
                    RelationshipOutput(relationshipChange, newRelation, npc.Name.ToString());

                    return relationshipChange;
                }

                LogMessage($"DEBUG: No relationship change for NPC {npc.Name}.");
                return 0;
            }
            catch (Exception ex)
            {
                LogMessage($"DEBUG: Relationship update failed for NPC {npc.Name}: {ex.Message}");
                return 0;
            }
        }

        private int AdjustRelationshipChange(int baseChange)
        {
            if (baseChange > 0)
            {
                return Math.Min(baseChange + ChatAiSettings.Instance.BaseRelationshipGain, ChatAiSettings.Instance.MaxRelationshipChange);
            }
            else if (baseChange < 0)
            {
                return Math.Max(baseChange + ChatAiSettings.Instance.BaseRelationshipLoss, -ChatAiSettings.Instance.MaxRelationshipChange);
            }

            return baseChange;
        }

        private void RelationshipOutput(int rel, int newRelation, string hero)
        {
            bool enableChatLogOutput = ChatAiSettings.Instance.EnableRelationshipTracking;
            TextObject textObject = new TextObject("Error");
            Color color = new Color(1000f, 1000f, 1000f, 1f);

            if (rel != 0)
            {
                if (rel < 0)
                {
                    string value = "{=RELATION_DECREASE}Your relation decreased by {DECREASED_VALUE} with {HERO}";
                    textObject = new TextObject(value);
                    textObject.SetTextVariable("DECREASED_VALUE", Math.Abs(rel));
                    textObject.SetTextVariable("NEW_RELATION_VALUE", newRelation);
                    textObject.SetTextVariable("HERO", hero);
                    color = new Color(1f, 0f, 0f, 1f); // Red
                }
                else if (rel > 0)
                {
                    string value = "{=RELATION_INCREASE}Your relation increased by {INCREASED_VALUE} with {HERO}";
                    textObject = new TextObject(value);
                    textObject.SetTextVariable("INCREASED_VALUE", rel);
                    textObject.SetTextVariable("NEW_RELATION_VALUE", newRelation);
                    textObject.SetTextVariable("HERO", hero);
                    color = new Color(0f, 1f, 0f, 1f); // Green
                }

                if (enableChatLogOutput)
                {
                    InformationManager.DisplayMessage(new InformationMessage(textObject.ToString(), color));
                }
            }
        }

        private async Task<string> AnalyzeIntent(string playerInput)
        {
            string prompt = $"The player said: '{playerInput}'. Classify the message as one of the following: [insult, compliment, neutral, praise, thank, challenge]. Only return the classification.";
            LogMessage($"DEBUG: Calling AIHelper.GetResponse for RelationshipManager with prompt: {prompt}");
            string response = await AIHelper.GetResponse(prompt);
            LogMessage($"DEBUG: Intent analysis result: {response}");
            return response.Trim().ToLower();
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
