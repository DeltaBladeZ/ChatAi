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



        public async Task<int> UpdateRelationshipBasedOnMessage(Hero npc, string playerInput)
        {
            LogMessage("============================================================");
            LogMessage("Relationship Manager Starting.");
            LogMessage("============================================================");
            try
            {
                int relationshipChange = await AnalyzeIntent(playerInput);
                LogMessage($"DEBUG: Relationship change for NPC {npc.Name}: {relationshipChange}");

                relationshipChange = AdjustRelationshipChange(relationshipChange);

                if (relationshipChange != 0)
                {
                    int currentRelation = (int)Math.Round(npc.GetRelationWithPlayer());
                    int newRelation = currentRelation + relationshipChange;

                    int relationCap = ChatAiSettings.Instance.MaxRelationshipChange;
                    if (newRelation > relationCap)
                    {
                        relationshipChange = relationCap - currentRelation;
                        newRelation = relationCap;
                    }

                    CharacterRelationManager.SetHeroRelation(Hero.MainHero, npc, newRelation);

                    if (ChatAiSettings.Instance.EnableRelationshipTracking && npc.Clan?.Leader != null && npc != npc.Clan.Leader)
                    {
                        CharacterRelationManager.SetHeroRelation(Hero.MainHero, npc.Clan.Leader, newRelation);
                    }

                    // Store the last relationship change globally
                    RelationshipTracker.SetRelationshipChange(npc, relationshipChange);

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

            LogMessage("============================================================");
            LogMessage("Relationship Manager Completed.");
            LogMessage("============================================================");

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
                // Check if debug logging is enabled in the settings
                if (!ChatAiSettings.Instance.EnableDebugLogging)
                {
                    return; // Skip logging if disabled
                }

                string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}\n";

                // Determine the log file path dynamically
                string logFilePath = _logFilePath; // Default path
                string logDirectory = Path.GetDirectoryName(logFilePath);

                if (!Directory.Exists(logDirectory))
                {
                    // If the directory does not exist, fall back to the desktop
                    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string desktopLogDirectory = Path.Combine(desktopPath, "ChatAiLogs");

                    if (!Directory.Exists(desktopLogDirectory))
                    {
                        Directory.CreateDirectory(desktopLogDirectory);
                    }

                    logFilePath = Path.Combine(desktopLogDirectory, "mod_log.txt");
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
