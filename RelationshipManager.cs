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
        private readonly string _logFilePath = PathHelper.GetModFilePath("mod_log.txt");



        public async Task<int> UpdateRelationshipBasedOnMessage(Hero npc, string playerInput)
        {
            LogMessage("============================================================");
            LogMessage($"Relationship Manager Starting for NPC: {npc.Name}");
            LogMessage("============================================================");

            try
            {
                // Step 1: Get base relationship change from AI sentiment analysis
                int relationshipChange = await AnalyzeIntent(playerInput);
                LogMessage($"DEBUG: Initial AI relationship change for NPC {npc.Name}: {relationshipChange}");

                // Step 2: Modify based on BaseRelationshipGain and BaseRelationshipLoss
                if (relationshipChange > 0)
                {
                    LogMessage($"DEBUG: Positive change detected. Adding BaseRelationshipGain: {ChatAiSettings.Instance.BaseRelationshipGain}");
                    relationshipChange += ChatAiSettings.Instance.BaseRelationshipGain;
                }
                else if (relationshipChange < 0)
                {
                    LogMessage($"DEBUG: Negative change detected. Subtracting BaseRelationshipLoss: {ChatAiSettings.Instance.BaseRelationshipLoss}");
                    relationshipChange -= ChatAiSettings.Instance.BaseRelationshipLoss;
                }
                LogMessage($"DEBUG: Relationship change after applying Base Gain/Loss: {relationshipChange}");

                // Step 3: Check if MaxRelationshipChange is 0 (Disables changes)
                int maxChange = ChatAiSettings.Instance.MaxRelationshipChange;
                if (maxChange == 0)
                {
                    LogMessage($"DEBUG: MaxRelationshipChange is set to 0. No relationship changes will be applied.");
                    return 0; // Exit early, preventing unnecessary calculations.
                }

                // Step 4: Cap relationship change using MaxRelationshipChange
                if (relationshipChange > maxChange)
                {
                    LogMessage($"DEBUG: Relationship change {relationshipChange} exceeds max limit {maxChange}. Capping it to {maxChange}.");
                }
                else if (relationshipChange < -maxChange)
                {
                    LogMessage($"DEBUG: Relationship change {relationshipChange} is below min limit {-maxChange}. Capping it to {-maxChange}.");
                }

                relationshipChange = Math.Max(-maxChange, Math.Min(maxChange, relationshipChange));
                LogMessage($"DEBUG: Final relationship change after capping: {relationshipChange}");

                // Step 5: Apply the final relationship change
                if (relationshipChange != 0)
                {
                    int currentRelation = (int)Math.Round(npc.GetRelationWithPlayer());
                    int newRelation = currentRelation + relationshipChange;
                    LogMessage($"DEBUG: Current relationship with {npc.Name}: {currentRelation}, after change: {newRelation}");

                    // Step 6: Ensure final relationship value stays between -100 and 100
                    int relationCap = 100; // Hard cap for relationships
                    if (newRelation > relationCap)
                    {
                        LogMessage($"DEBUG: New relationship value {newRelation} exceeds max cap {relationCap}. Adjusting.");
                    }
                    else if (newRelation < -relationCap)
                    {
                        LogMessage($"DEBUG: New relationship value {newRelation} is below min cap {-relationCap}. Adjusting.");
                    }

                    newRelation = Math.Max(-relationCap, Math.Min(relationCap, newRelation));
                    LogMessage($"DEBUG: Final new relationship value for {npc.Name}: {newRelation}");

                    // Apply relationship update
                    CharacterRelationManager.SetHeroRelation(Hero.MainHero, npc, newRelation);
                    LogMessage($"DEBUG: Applied relationship change for {npc.Name}. New relation: {newRelation}");

                    // Update clan leader if enabled
                    if (ChatAiSettings.Instance.EnableRelationshipTracking && npc.Clan?.Leader != null && npc != npc.Clan.Leader)
                    {
                        LogMessage($"DEBUG: Updating NPC's clan leader ({npc.Clan.Leader.Name}) relationship as well.");
                        CharacterRelationManager.SetHeroRelation(Hero.MainHero, npc.Clan.Leader, newRelation);
                    }

                    // Store the last relationship change globally
                    RelationshipTracker.SetRelationshipChange(npc, relationshipChange);
                    LogMessage($"DEBUG: Stored relationship change for {npc.Name}: {relationshipChange}");

                    // Display UI message
                    RelationshipOutput(relationshipChange, newRelation, npc.Name.ToString());

                    LogMessage("============================================================");
                    LogMessage($"Relationship Manager Completed for NPC: {npc.Name}");
                    LogMessage("============================================================");
                    return relationshipChange;
                }

                LogMessage($"DEBUG: No relationship change for NPC {npc.Name}.");
                return 0;
            }
            catch (Exception ex)
            {
                LogMessage($"ERROR: Relationship update failed for NPC {npc.Name}: {ex.Message}");
                return 0;
            }
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

        private async Task<int> AnalyzeIntent(string playerInput)
        {
            // New AI prompt: Instead of returning categories, it returns a direct numerical change
            string prompt = $"Analyze the following message for sentiment impact on an NPC relationship. Assign a numerical score based on the player's words: \n\n" +
                            $"Message: \"{playerInput}\"\n\n" +
                            $"Guidelines:\n" +
                            $"- Very offensive messages (curses, insults, threats) → return -10\n" +
                            $"- Insults or dismissive comments → return between -5 and -9\n" +
                            $"- Neutral/indifferent messages → return 0\n" +
                            $"- Positive but not impactful (small talk, polite words) → return between +1 and +2\n" +
                            $"- Warm interactions (thank you, friendly gesture, slight praise) → return between +3 and +4\n" +
                            $"- Highly positive interactions (deep praise, expressing trust, loyalty) → return between +5 and +10\n\n" +
                            $"Return ONLY the number, without any additional text. If the message is just a question or normal talking message return 0." +
                            $"Examples: Player says 'I hate you' → return -10, Player says how are you? → return 0, Player says 'You are a good friend' → return +5, Player says 'Tell me about yourself' → return +0";

            LogMessage($"DEBUG: Calling AIHelper.GetResponse for RelationshipManager with prompt: {prompt}");

            string response = await AIHelper.GetResponse(prompt);
            LogMessage($"DEBUG: Intent analysis result: {response}");


            // Ensure AI returns a valid integer, fallback to 0 if invalid
            if (int.TryParse(response.Trim(), out int result))
            {
                return result;
            }

            LogMessage("WARNING: AI response was not a valid integer. Defaulting to 0.");
            return 0;
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
