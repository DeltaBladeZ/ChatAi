using System;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Library;
using TaleWorlds.Core;
using System.Collections.Generic;

namespace ChatAi.Battle
{
    public static class BattleOrderMapper
    {
        private static void LogDebug(string message)
        {
            try
            {
                if (!ChatAi.SettingsUtil.IsDebugLoggingEnabled()) return;
                var logPath = PathHelper.GetModFilePath("mod_log.txt");
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - [DynamicBattles] {message}\n";
                System.IO.File.AppendAllText(logPath, line);
            }
            catch { }
        }

        public static bool TryExecute(Mission mission, BattleAIEvaluator.IntentResult intent)
        {
            try
            {
                if (mission == null || mission.PlayerTeam == null || mission.PlayerTeam.PlayerOrderController == null)
                {
                    LogDebug("Order mapper: missing mission/team/controller.");
                    return false;
                }

                var controller = mission.PlayerTeam.PlayerOrderController;
                LogDebug($"Issuing order: {intent.Order}@{intent.Target} via {controller.GetType().Name}");

                switch (intent.Order)
                {
                    case "Charge":
                        return IssueToTarget(mission, controller, OrderType.Charge, intent.Target);
                    case "Retreat":
                        return IssueToTarget(mission, controller, OrderType.Retreat, intent.Target);
                    case "HoldPosition":
                        // Most Bannerlord builds expose "StandYourGround" as the "hold position" command.
                        return IssueToTarget(mission, controller, OrderType.StandYourGround, intent.Target);
                    case "FollowMe":
                        return IssueFollowMe(mission, controller, intent.Target);
                    case "HoldFire":
                        return IssueToTarget(mission, controller, OrderType.HoldFire, intent.Target);
                    case "FireAtWill":
                        return IssueToTarget(mission, controller, OrderType.FireAtWill, intent.Target);
                    case "FormationLine":
                        return IssueToTarget(mission, controller, OrderType.ArrangementLine, intent.Target);
                    // Note: ShieldWall/Square/Wedge order enum names differ across Bannerlord versions.
                    // We'll add them once we confirm the correct OrderType names for this build.
                    // Formation/toggles can be added incrementally; for now we skip to avoid API mismatch
                    default:
                        LogDebug($"Unsupported order mapping: {intent.Order}");
                        return false;
                }
            }
            catch (Exception ex)
            {
                if (ChatAi.SettingsUtil.IsDebugLoggingEnabled())
                    InformationManager.DisplayMessage(new InformationMessage($"[DynamicBattles] Order execution failed: {ex.Message}"));
                LogDebug($"Order execution exception: {ex.Message}");
                return false;
            }
        }

        private static bool IssueFollowMe(Mission mission, OrderController controller, string target)
        {
            var leader = mission.PlayerTeam.Leader;
            if (string.Equals(target, "All", StringComparison.OrdinalIgnoreCase))
            {
                // Select all formations, then follow leader (if available), else generic FollowMe
                if (SelectAllFormations(controller, mission.PlayerTeam))
                {
                    if (leader != null)
                    {
                        LogDebug("FollowMe@All with leader via SetOrderWithAgent.");
                        controller.SetOrderWithAgent(OrderType.FollowMe, leader);
                        return true;
                    }
                    LogDebug("FollowMe@All via SetOrder.");
                    controller.SetOrder(OrderType.FollowMe);
                    return true;
                }
                // Fallback direct
                try
                {
                    if (leader != null)
                    {
                        controller.SetOrderWithAgent(OrderType.FollowMe, leader);
                        LogDebug("FollowMe@All fallback SetOrderWithAgent.");
                        return true;
                    }
                    controller.SetOrder(OrderType.FollowMe);
                    LogDebug("FollowMe@All fallback SetOrder.");
                    return true;
                }
                catch (Exception ex)
                {
                    LogDebug($"FollowMe@All fallback failed: {ex.Message}");
                    return false;
                }
            }
            else
            {
                var f = ResolveTargetFormation(mission.PlayerTeam, target);
                if (f == null)
                {
                    LogDebug($"No formations resolved for target '{target}', attempting All.");
                    return IssueFollowMe(mission, controller, "All");
                }
                if (SelectOnly(controller, f))
                {
                    if (leader != null)
                    {
                        LogDebug($"FollowMe@{target} with leader via SetOrderWithAgent.");
                        controller.SetOrderWithAgent(OrderType.FollowMe, leader);
                        return true;
                    }
                    LogDebug($"FollowMe@{target} via SetOrder.");
                    controller.SetOrder(OrderType.FollowMe);
                    return true;
                }
                LogDebug($"Selection failed for target '{target}'.");
                return false;
            }
        }

        private static bool IssueToTarget(Mission mission, OrderController controller, OrderType order, string target)
        {
            if (string.Equals(target, "All", StringComparison.OrdinalIgnoreCase))
            {
                if (SelectAllFormations(controller, mission.PlayerTeam))
                {
                    LogDebug($"SetOrder({order}) for selection (All).");
                    controller.SetOrder(order);
                    return true;
                }
                // Fallback direct
                try
                {
                    controller.SetOrder(order);
                    LogDebug($"Fallback SetOrder({order}) (may depend on selection).");
                    return true;
                }
                catch (Exception ex)
                {
                    LogDebug($"Fallback SetOrder({order}) failed: {ex.Message}");
                    return false;
                }
            }
            else
            {
                var f = ResolveTargetFormation(mission.PlayerTeam, target);
                if (f == null)
                {
                    LogDebug($"No formations resolved for target '{target}'.");
                    return false;
                }
                if (SelectOnly(controller, f))
                {
                    LogDebug($"SetOrder({order}) after selecting {target}.");
                    controller.SetOrder(order);
                    return true;
                }
                LogDebug($"Could not issue {order} to {target}.");
                return false;
            }
        }

        private static Formation ResolveTargetFormation(Team team, string target)
        {
            try
            {
                if (team == null) return null;
                switch ((target ?? "").ToLowerInvariant())
                {
                    case "infantry":
                        return team.GetFormation(FormationClass.Infantry);
                    case "archers":
                        return team.GetFormation(FormationClass.Ranged);
                    case "cavalry":
                        return team.GetFormation(FormationClass.Cavalry);
                }
            }
            catch (Exception ex)
            {
                LogDebug($"ResolveTargetFormations error: {ex.Message}");
            }
            return null;
        }

        private static List<Formation> GetAllFormations(Team team)
        {
            var result = new List<Formation>();
            try
            {
                if (team == null) return result;
                // Collect common formation classes
                var classes = new List<FormationClass>
                {
                    FormationClass.Infantry,
                    FormationClass.Ranged,
                    FormationClass.Cavalry,
                    FormationClass.HorseArcher
                };
                foreach (var fc in classes)
                {
                    var f = team.GetFormation(fc);
                    if (f != null) result.Add(f);
                }
            }
            catch (Exception ex)
            {
                LogDebug($"GetAllFormations error: {ex.Message}");
            }
            return result;
        }

        private static bool SelectAllFormations(OrderController controller, Team team)
        {
            try
            {
                controller.ClearSelectedFormations();
                foreach (var f in GetAllFormations(team))
                {
                    controller.SelectFormation(f);
                }
                return true;
            }
            catch (Exception ex)
            {
                LogDebug($"SelectAllFormations error: {ex.Message}");
            }
            return false;
        }

        private static bool SelectOnly(OrderController controller, Formation formation)
        {
            try
            {
                controller.ClearSelectedFormations();
                controller.SelectFormation(formation);
                return true;
            }
            catch (Exception ex)
            {
                LogDebug($"SelectOnly error: {ex.Message}");
                return false;
            }
        }
    }
}

