using MCM.Abstractions.Base.Global;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ChatAi
{
    public class SubModule : MBSubModuleBase
    {
        protected override void OnGameStart(Game game, IGameStarter gameStarter)
        {
            base.OnGameStart(game, gameStarter);

            if (gameStarter is CampaignGameStarter campaignStarter)
            {
                var chatBehavior = new ChatBehavior();
                campaignStarter.AddBehavior(chatBehavior);
                chatBehavior.AddDialogs(campaignStarter);
                DebugModLogger modLogger = new DebugModLogger();
                modLogger.LogAllModules();
                modLogger.LogAllSettings();
            }
        }

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

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

        
    }
}
