using MCM.Abstractions.Base.Global;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Bannerlord.UIExtenderEx;

namespace ChatAi
{
    public class SubModule : MBSubModuleBase
    {
		private UIExtender _uiExtender;

        protected override void OnGameStart(Game game, IGameStarter gameStarter)
        {
            base.OnGameStart(game, gameStarter);

            if (gameStarter is CampaignGameStarter campaignStarter)
            {
                var chatBehavior = new ChatBehavior();
                campaignStarter.AddBehavior(chatBehavior);
                chatBehavior.AddDialogs(campaignStarter);
                DebugModLogger modLogger = new DebugModLogger();
                modLogger.LogEnvironmentInfo();
                modLogger.LogAllSettings();
            }
        }

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

			// UIExtenderEx integration is currently disabled while we validate patch API version compatibility.

            // Ensure the settings instance is initialized
            var settings = GlobalSettings<ChatAiSettings>.Instance;
            if (settings == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("Failed to initialize ChatAiSettings."));
            }
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            InformationManager.DisplayMessage(new InformationMessage("ChatAi: Mod loaded!"));
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);
            try
            {
                var settings = GlobalSettings<ChatAiSettings>.Instance ?? ChatAiSettings.Instance;
                if (settings != null && settings.EnableDynamicBattles)
                {
                    mission.AddMissionBehavior(new Battle.BattleTextInputBehavior());
                }
            }
            catch
            {
                // Fail closed
            }
        }
 
    }
}
