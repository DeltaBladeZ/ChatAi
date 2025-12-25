using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ChatAi.Battle
{
    public static class BattleMoraleEffects
    {
        /// <summary>
        /// Best-effort morale boost. Uses only public APIs (no reflection).
        /// Returns true if a boost was applied, false if no known morale API is available.
        /// </summary>
        public static bool TryApplyMoraleBoost(Mission mission, int boostAmount)
        {
            try
            {
                if (mission?.PlayerTeam == null) return false;
                boostAmount = Math.Max(0, Math.Min(10, boostAmount));
                if (boostAmount <= 0) return false;

                var team = mission.PlayerTeam;
                // Public morale APIs vary by Bannerlord version. Prefer team-level morale if available.
                if (TryAddTeamMorale(team, boostAmount))
                {
                    // Green message for positive morale boost
                    InformationManager.DisplayMessage(
                        new InformationMessage($"Rousing speech! Morale +{boostAmount}", new Color(0f, 1f, 0f, 1f))
                    );
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryAddTeamMorale(Team team, int boostAmount)
        {
            try
            {
                // Apply morale to agents on the team (public extension exists: Agent.ChangeMorale(float)).
                bool applied = false;
                foreach (var agent in team.ActiveAgents)
                {
                    if (agent == null) continue;
                    try
                    {
                        agent.ChangeMorale((float)boostAmount);
                        applied = true;
                    }
                    catch { }
                }
                return applied;
            }
            catch
            {
                return false;
            }
        }
    }
}


