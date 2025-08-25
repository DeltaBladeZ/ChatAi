using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using System.Reflection;

namespace ChatAi
{
    public static class WorldPromptHints
    {
        /// <summary>
        /// Produces short, plain-text hints about the NPC's immediate world state
        /// that should influence roleplay and responses.
        /// Example: prisoner status, etc.
        /// </summary>
        public static string GetHintsForNPC(Hero npc)
        {
            if (npc == null)
            {
                return string.Empty;
            }

            List<string> hints = new List<string>();

            AppendPrisonerHintIfAny(npc, hints);
            AppendPartyStatusHintIfAny(npc, hints);
            AppendPregnancyHintIfAny(npc, hints);
            AppendUnderSiegeHintIfAny(npc, hints);
            AppendBesiegingHintIfAny(npc, hints);
            AppendRaidingHintIfAny(npc, hints);

            // Future: Add more world-state hints here (e.g., injured, in army, siege, starvation)

            return hints.Count > 0 ? string.Join(" \n", hints) : string.Empty;
        }

        private static void AppendPrisonerHintIfAny(Hero npc, List<string> hints)
        {
            if (!npc.IsPrisoner)
            {
                return;
            }

            // NPC is a prisoner
            if (npc.PartyBelongedToAsPrisoner == PartyBase.MainParty)
            {
                hints.Add("You are currently a prisoner in the player's party.");
                return;
            }

            var prisonerParty = npc.PartyBelongedToAsPrisoner;
            var settlement = prisonerParty?.Settlement;

            if (settlement != null)
            {
                string placeType = settlement.IsCastle ? "castle" : (settlement.IsTown ? "town" : "settlement");
                hints.Add($"You are currently a prisoner in {settlement.Name} ({placeType}).");
            }
            else
            {
                hints.Add("You are currently a prisoner in an unknown location.");
            }
        }

        private static void AppendPartyStatusHintIfAny(Hero npc, List<string> hints)
        {
            try
            {
                var playerParty = MobileParty.MainParty;
                var npcParty = npc.PartyBelongedTo;

                if (playerParty == null)
                {
                    return;
                }

                // NPC is a direct member of the player's party (e.g., companion)
                if (npcParty == playerParty)
                {
                    hints.Add("You are currently a member of the player's party.");
                    return;
                }

                // NPC travels with the player in the same army (separate party)
                var playerArmy = playerParty.Army;
                var npcArmy = npcParty?.Army;
                if (playerArmy != null && npcArmy != null && ReferenceEquals(playerArmy, npcArmy))
                {
                    hints.Add("You are currently traveling with the player in the same army.");
                }
            }
            catch
            {
                // Best-effort hint; ignore any API issues
            }
        }

        private static void AppendPregnancyHintIfAny(Hero npc, List<string> hints)
        {
            try
            {
                if (!npc.IsFemale)
                {
                    return;
                }

                // Try direct known property first
                var isPregnantProp = npc.GetType().GetProperty(
                    "IsPregnant",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.IgnoreCase);

                bool isPregnant = false;
                if (isPregnantProp != null)
                {
                    object? val = isPregnantProp.GetValue(npc);
                    if (val is bool b)
                    {
                        isPregnant = b;
                    }
                }
                else
                {
                    // Fallback: search any boolean property containing "pregnan"
                    foreach (var prop in npc.GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
                    {
                        if (prop.PropertyType == typeof(bool) && prop.Name.IndexOf("pregnan", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            object? v = prop.GetValue(npc);
                            if (v is bool pb && pb)
                            {
                                isPregnant = true;
                                break;
                            }
                        }
                    }
                }

                if (!isPregnant)
                {
                    return;
                }

                // Compose extra info: father (best-effort) and due time if available
                string extra = string.Empty;
                try
                {
                    var father = npc.Spouse; // Most pregnancies occur within marriage
                    if (father != null)
                    {
                        extra += $" The father is {father.Name}.";
                    }
                }
                catch { }

                // Note: We intentionally omit due-date estimates per design

                hints.Add($"You are currently pregnant.{extra}");
            }
            catch
            {
                // Ignore if API changes
            }
        }

        private static void AppendUnderSiegeHintIfAny(Hero npc, List<string> hints)
        {
            try
            {
                var settlement = npc.CurrentSettlement;
                if (settlement != null && settlement.IsUnderSiege)
                {
                    string placeType = settlement.IsCastle ? "castle" : (settlement.IsTown ? "town" : "settlement");
                    hints.Add($"Your {placeType} of {settlement.Name} is currently under siege.");
                }
            }
            catch { }
        }

        private static void AppendBesiegingHintIfAny(Hero npc, List<string> hints)
        {
            try
            {
                var party = npc.PartyBelongedTo;
                var besieged = party?.SiegeEvent?.BesiegedSettlement;
                if (besieged != null)
                {
                    string placeType = besieged.IsCastle ? "castle" : (besieged.IsTown ? "town" : "settlement");
                    hints.Add($"You are currently besieging the {placeType} of {besieged.Name}.");
                }
            }
            catch { }
        }

        private static void AppendRaidingHintIfAny(Hero npc, List<string> hints)
        {
            try
            {
                var party = npc.PartyBelongedTo;
                if (party == null)
                {
                    return;
                }

                bool isRaiding = false;

                // Prefer a direct IsRaiding flag if present
                var isRaidingProp = party.GetType().GetProperty(
                    "IsRaiding",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                if (isRaidingProp != null)
                {
                    object? val = isRaidingProp.GetValue(party);
                    if (val is bool b && b)
                    {
                        isRaiding = true;
                    }
                }

                // Fallback: search any boolean property/field containing "raid"
                if (!isRaiding)
                {
                    foreach (var prop in party.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        if (prop.PropertyType == typeof(bool) && prop.Name.IndexOf("raid", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            object? v = prop.GetValue(party);
                            if (v is bool rb && rb)
                            {
                                isRaiding = true;
                                break;
                            }
                        }
                    }
                }

                if (!isRaiding)
                {
                    return;
                }

                // Try to identify target settlement (best-effort)
                Settlement? raidTarget = null;

                // 1) Look for a property with Settlement type and name containing raid/target
                foreach (var prop in party.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (typeof(Settlement).IsAssignableFrom(prop.PropertyType) &&
                        (prop.Name.IndexOf("raid", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         prop.Name.IndexOf("target", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        object? s = prop.GetValue(party);
                        if (s is Settlement st)
                        {
                            raidTarget = st;
                            break;
                        }
                    }
                }

                // 2) Inspect AI object for a potential target
                if (raidTarget == null)
                {
                    var aiProp = party.GetType().GetProperty("Ai", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                    var ai = aiProp?.GetValue(party);
                    if (ai != null)
                    {
                        foreach (var prop in ai.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                        {
                            if (typeof(Settlement).IsAssignableFrom(prop.PropertyType) &&
                                (prop.Name.IndexOf("raid", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 prop.Name.IndexOf("target", StringComparison.OrdinalIgnoreCase) >= 0))
                            {
                                object? s = prop.GetValue(ai);
                                if (s is Settlement st)
                                {
                                    raidTarget = st;
                                    break;
                                }
                            }
                        }
                    }
                }

                if (raidTarget != null && raidTarget.IsVillage)
                {
                    hints.Add($"You are currently raiding the village of {raidTarget.Name}.");
                }
                else
                {
                    hints.Add("You are currently raiding.");
                }
            }
            catch { }
        }
    }
}


