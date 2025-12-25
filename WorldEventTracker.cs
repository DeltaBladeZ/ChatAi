using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace ChatAi
{
    public class WorldEventTracker
    {
        private static WorldEventTracker _instance;
        public static WorldEventTracker Instance => _instance ??= new WorldEventTracker();

        private Dictionary<string, List<string>> _npcEvents = new Dictionary<string, List<string>>();

        public void AddEvent(string npcId, string eventDescription)
        {
            if (!_npcEvents.ContainsKey(npcId))
            {
                _npcEvents[npcId] = new List<string>();
            }

            _npcEvents[npcId].Add($"{CampaignTime.Now}: {eventDescription}");

            if (_npcEvents[npcId].Count > 5)
            {
                _npcEvents[npcId].RemoveAt(0);
            }
        }


        public List<string> GetRecentEvents(string npcId)
        {
            return _npcEvents.ContainsKey(npcId) ? _npcEvents[npcId] : new List<string>();
        }

        public static void LogMessage(string message)
        {
            try
            {
                // Settings may not be initialized yet on older Bannerlord versions.
                if (!SettingsUtil.IsDebugLoggingEnabled())
                {
                    return;
                }

                string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}\n";

                string logFilePath = PathHelper.GetModFilePath("mod_log.txt");
                string logDirectory = Path.GetDirectoryName(logFilePath);

                // Ensure the log directory exists
                if (!string.IsNullOrEmpty(logDirectory))
                {
                    PathHelper.EnsureDirectoryExists(logDirectory);
                }

                File.AppendAllText(logFilePath, logMessage);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"Logging error: {ex.Message}"));
            }
        }
    }
    

    public class WorldEventListener : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            WorldEventTracker.LogMessage("[DEBUG] Registering WorldEventListener...");
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
            CampaignEvents.OnClanLeaderChangedEvent.AddNonSerializedListener(this, OnClanLeaderChanged);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Syncing data if needed in the future
        }

        private void OnHeroKilled(Hero hero, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
        {
            WorldEventTracker.LogMessage($"[DEBUG] OnHeroKilled triggered for {hero.Name}...");
            if (hero.IsAlive) return;

            string causeOfDeath;
            switch (detail)
            {
                case KillCharacterAction.KillCharacterActionDetail.Murdered:
                    causeOfDeath = killer != null ? $"was murdered by {killer.Name}" : "was assassinated";
                    break;
                case KillCharacterAction.KillCharacterActionDetail.DiedInLabor:
                    causeOfDeath = "died during childbirth";
                    break;
                case KillCharacterAction.KillCharacterActionDetail.DiedOfOldAge:
                    causeOfDeath = "passed away due to old age";
                    break;
                case KillCharacterAction.KillCharacterActionDetail.DiedInBattle:
                    causeOfDeath = killer != null ? $"was slain in battle by {killer.Name}" : "died in battle";
                    break;
                case KillCharacterAction.KillCharacterActionDetail.WoundedInBattle:
                    causeOfDeath = "was severely wounded in battle and succumbed to injuries";
                    break;
                case KillCharacterAction.KillCharacterActionDetail.Executed:
                    causeOfDeath = "was executed";
                    break;
                case KillCharacterAction.KillCharacterActionDetail.Lost:
                    causeOfDeath = "disappeared under mysterious circumstances and is presumed dead";
                    break;
                default:
                    causeOfDeath = "died under unknown circumstances";
                    break;
            }

            string eventText = $"{hero.Name} {causeOfDeath}.";
            WorldEventTracker.LogMessage($"[DEBUG] Saving event: {eventText}");

            // Add event for the dead NPC
            WorldEventTracker.Instance.AddEvent(hero.StringId, eventText);

            // also log this event for family/clan members
            LogEventForRelatedNPCs(hero, eventText);
        }



        private void OnClanLeaderChanged(Hero newLeader, Hero oldLeader)
        {
            string eventText = $"{newLeader.Name} has become the new leader of {newLeader.Clan.Name}.";
            WorldEventTracker.Instance.AddEvent(newLeader.StringId, eventText);
        }

        private void LogEventForRelatedNPCs(Hero hero, string eventText)
        {
            var relatedNPCs = Campaign.Current.CampaignObjectManager.AliveHeroes
                .Where(h => h.Father?.StringId == hero.StringId || h.Mother?.StringId == hero.StringId || h.Clan == hero.Clan)
                .ToList();

            WorldEventTracker.LogMessage($"[DEBUG] Notifying {relatedNPCs.Count} related NPCs about {hero.Name}'s death.");

            foreach (var npc in relatedNPCs)
            {
                WorldEventTracker.Instance.AddEvent(npc.StringId, eventText);
            }
        }



        public static List<string> GetEventsForNPC(Hero npc)
        {
            var events = WorldEventTracker.Instance.GetRecentEvents(npc.StringId);
            WorldEventTracker.LogMessage($"[DEBUG] Retrieved events for {npc.Name}: {string.Join(", ", events)}");
            return events;
        }

    }
}
    
