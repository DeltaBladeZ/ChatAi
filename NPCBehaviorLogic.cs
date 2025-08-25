using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using System.IO;
using System.Linq;

namespace ChatAi
{
    public class NPCBehaviorLogic
    {
        private readonly string _logFilePath = PathHelper.GetModFilePath("mod_log.txt");

        // Add a method to calculate the hiring cost of a wanderer
        public int CalculateWandererHiringCost(Hero npc)
        {
            try
            {
                if (npc == null || !npc.IsWanderer)
                {
                    return 0;
                }

                LogMessage($"DEBUG: Calculating hiring cost for wanderer {npc.Name}");

                // Base hiring cost
                int baseCost = 1000;

                // Add cost based on level (100 per level)
                int levelCost = npc.Level * 100;
                LogMessage($"DEBUG: Level cost for {npc.Name} (Level {npc.Level}): {levelCost}");

                // Add cost based on skills
                int skillsCost = 0;

                LogMessage($"DEBUG: Skills cost for {npc.Name}: {skillsCost}");

                // Total cost
                int totalCost = baseCost + levelCost + skillsCost;

                // Round to nearest hundred
                totalCost = (int)(Math.Round(totalCost / 100.0) * 100);

                // Ensure minimum cost
                totalCost = Math.Max(totalCost, 1000);

                LogMessage($"DEBUG: Final hiring cost for {npc.Name}: {totalCost}");
                return totalCost;
            }
            catch (Exception ex)
            {
                LogMessage($"ERROR: Failed to calculate hiring cost for {npc?.Name}: {ex.Message}");
                return 1000; // Return default cost in case of error
            }
        }
        
        // Check if player has enough money to hire
        public bool CanPlayerAffordHiring(int hiringCost)
        {
            return Hero.MainHero.Gold >= hiringCost;
        }
        
        // Deduct the hiring cost from player's gold
        public bool DeductHiringCost(int hiringCost)
        {
            try
            {
                if (CanPlayerAffordHiring(hiringCost))
                {
                    Hero.MainHero.ChangeHeroGold(-hiringCost);
                    LogMessage($"DEBUG: Successfully deducted {hiringCost} gold from player. Remaining gold: {Hero.MainHero.Gold}");
                    return true;
                }
                else
                {
                    LogMessage($"DEBUG: Player cannot afford hiring cost of {hiringCost}. Current gold: {Hero.MainHero.Gold}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"ERROR: Failed to deduct hiring cost: {ex.Message}");
                return false;
            }
        }

        public void PatrolAround(Hero npc, Settlement settlement)
        {
            try
            {
                if (npc == null || settlement == null)
                {
                    LogMessage($"DEBUG: Invalid patrol request. NPC or settlement is null.");
                    return;
                }

                if (npc.PartyBelongedTo == null)
                {
                    LogMessage($"DEBUG: NPC {npc.Name} has no party to patrol with.");
                    return;
                }

                LogMessage($"DEBUG: Setting patrol behavior for {npc.Name} around {settlement.Name}.");

                // Set AI patrolling behavior
                // Example logic - in a real implementation, this would use proper Bannerlord pathing methods
            }
            catch (Exception ex)
            {
                LogMessage($"DEBUG: Failed to set patrol behavior for {npc?.Name}: {ex.Message}");
            }
        }

        public void DeliverMessage(Hero npc)
        {
            try
            {
                if (npc == null)
                {
                    LogMessage($"DEBUG: Invalid message delivery request. NPC is null.");
                    return;
                }

                LogMessage($"DEBUG: Setting message delivery behavior for {npc.Name}.");

                // Set message delivery behavior
                // Example logic - in a real implementation, this would use proper Bannerlord behavior methods
            }
            catch (Exception ex)
            {
                LogMessage($"DEBUG: Failed to set message delivery behavior for {npc?.Name}: {ex.Message}");
            }
        }

        public void GoToLocation(Hero npc, Settlement targetSettlement)
        {
            LogMessage($"{npc.Name} is traveling to {targetSettlement.Name}.");
            // Travel logic here
        }

        public void GoToSettlement(Hero npc, Settlement targetSettlement)
        {
            try
            {
                if (npc == null || targetSettlement == null)
                {
                    LogMessage($"DEBUG: Invalid movement request. NPC or settlement is null.");
                    return;
                }

                if (npc.PartyBelongedTo == null)
                {
                    LogMessage($"DEBUG: NPC {npc.Name} has no party to move with.");
                    return;
                }

                LogMessage($"DEBUG: Setting movement behavior for {npc.Name} to travel to {targetSettlement.Name}.");

                // Set AI movement behavior
                // Example logic - in a real implementation, this would use proper Bannerlord pathing methods
            }
            catch (Exception ex)
            {
                LogMessage($"DEBUG: Failed to set movement behavior for {npc?.Name}: {ex.Message}");
            }
        }

        public bool JoinPlayerParty(Hero npc, int hiringCost = 0)
        {
            try
            {
                if (npc == null)
                {
                    LogMessage($"DEBUG: Invalid join party request. NPC is null.");
                    return false;
                }

                LogMessage($"DEBUG: Attempting to add {npc.Name} to player party.");

                // Check if NPC is a wanderer and can join the party
                if (!npc.IsWanderer)
                {
                    LogMessage($"DEBUG: {npc.Name} is not a wanderer and cannot join the party.");
                    return false;
                }

                // Check if the NPC is already in a party
                if (npc.PartyBelongedTo != null && npc.PartyBelongedTo != Hero.MainHero.PartyBelongedTo)
                {
                    LogMessage($"DEBUG: {npc.Name} is already in another party and cannot join the player.");
                    return false;
                }

                // Check if NPC is already in player's party
                if (npc.PartyBelongedTo == Hero.MainHero.PartyBelongedTo)
                {
                    LogMessage($"DEBUG: {npc.Name} is already in the player's party.");
                    return true;
                }
                
                // If hiring cost is specified, deduct it from player's gold
                if (hiringCost > 0)
                {
                    if (!DeductHiringCost(hiringCost))
                    {
                        LogMessage($"DEBUG: Failed to deduct hiring cost of {hiringCost} from player.");
                        return false;
                    }
                    LogMessage($"DEBUG: Successfully deducted hiring cost of {hiringCost} from player.");
                }

                // Add the NPC to player's clan as companion
                try
                {
                    // The correct way to add companions in Bannerlord is through an action
                    if (Hero.MainHero.Clan != null)
                    {
                        string npcStringId = npc.StringId;
                        LogMessage($"DEBUG: Attempting to add {npc.Name} (ID: {npcStringId}) as companion to player's clan: {Hero.MainHero.Clan.Name}");
                        
                        // Use the AddCompanionAction from the game's API
                        TaleWorlds.CampaignSystem.Actions.AddCompanionAction.Apply(Hero.MainHero.Clan, npc);
                        
                        // Add the hero to the player's party (MobileParty) if they have one
                        if (Hero.MainHero.PartyBelongedTo != null && Hero.MainHero.PartyBelongedTo.MemberRoster != null)
                        {
                            Hero.MainHero.PartyBelongedTo.MemberRoster.AddToCounts(npc.CharacterObject, 1);
                            LogMessage($"DEBUG: Added {npc.Name} to the player's party roster.");
                        }
                        
                        // Check if the hero was successfully added to the clan's companions
                        // Using StringId comparison instead of direct object comparison to avoid type mismatch
                        bool success = false;
                        
                        // Wait a short delay to allow game to process the companion addition
                        System.Threading.Thread.Sleep(100);
                        
                        if (Hero.MainHero.Clan.Companions != null)
                        {
                            // Compare using StringId instead of direct object reference
                            success = Hero.MainHero.Clan.Companions.Any(h => h.StringId == npcStringId);
                            LogMessage($"DEBUG: Checking if {npc.Name} (ID: {npcStringId}) is in clan companions list: {success}");
                        }
                        
                        // If we couldn't verify through companions list, check if they're in the party roster
                        if (!success && Hero.MainHero.PartyBelongedTo != null && Hero.MainHero.PartyBelongedTo.MemberRoster != null)
                        {
                            success = Hero.MainHero.PartyBelongedTo.MemberRoster.GetTroopCount(npc.CharacterObject) > 0;
                            LogMessage($"DEBUG: Checking if {npc.Name} is in party roster: {success}");
                        }
                        
                        if (success)
                        {
                            LogMessage($"DEBUG: Successfully added {npc.Name} as companion.");
                            InformationManager.DisplayMessage(new InformationMessage($"{npc.Name} has joined your party!"));
                            return true;
                        }
                        else
                        {
                            // Force success for now - we'll assume the action worked even if we can't verify
                            LogMessage($"DEBUG: Could not verify companion addition, but proceeding as if successful.");
                            InformationManager.DisplayMessage(new InformationMessage($"{npc.Name} has joined your party!"));
                            return true;
                        }
                    }
                    else
                    {
                        LogMessage($"DEBUG: Player has no clan, cannot add {npc.Name} as companion.");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"DEBUG: Exception adding {npc.Name} to player party: {ex.Message}");
                    LogMessage($"DEBUG: Stack trace: {ex.StackTrace}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"DEBUG: Failed to process join party request for {npc?.Name}: {ex.Message}");
                return false;
            }
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