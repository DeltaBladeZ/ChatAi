using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace ChatAi
{
    public static class RelationshipTracker
    {
        // Stores relationship changes for each NPC
        private static Dictionary<string, int> _npcRelationshipChanges = new();

        // Store the last relationship change for a specific NPC
        public static void SetRelationshipChange(Hero npc, int change)
        {
            if (npc == null) return;
            _npcRelationshipChanges[npc.StringId] = change;
        }

        // Retrieve the last relationship change for a specific NPC
        public static int GetRelationshipChange(Hero npc)
        {
            if (npc == null || !_npcRelationshipChanges.ContainsKey(npc.StringId))
                return 0; // Default to 0 if no change is recorded

            return _npcRelationshipChanges[npc.StringId];
        }
    }
}
