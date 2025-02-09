using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using TaleWorlds.LinQuick;
using System;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Library;
using System.Diagnostics;


namespace ChatAi
{
    public class ChatAiSettings : AttributeGlobalSettings<ChatAiSettings>
    {
        public override string Id => "ChatAiSettings";
        public override string DisplayName => "ChatAi Settings";
        public override string FolderName => "ChatAi";
        public override string FormatType => "json";

        // Main Settings Group
        [SettingPropertyDropdown("AI Backend", RequireRestart = false, HintText = "Select the backend to use for AI responses.")]
        [SettingPropertyGroup("API Settings/Main Settings", GroupOrder = 0)]
        public Dropdown<string> AIBackend { get; set; } = new Dropdown<string>(
            new List<string> { "OpenAI", "KoboldCpp", "Ollama", "OpenRouter", "Deepseek" }, 0);

        // voice backend
        [SettingPropertyDropdown("Voice Backend", RequireRestart = false, HintText = "Select the backend to use for voice responses.")]
        [SettingPropertyGroup("API Settings/Main Settings", GroupOrder = 0)]
        public Dropdown<string> VoiceBackend { get; set; } = new Dropdown<string>(
            new List<string> { "Azure", "PLACEHOLDER" }, 0);

        [SettingPropertyInteger("Max Tokens", 100, 4000, RequireRestart = false, HintText = "Set the maximum number of tokens for AI responses.")]
        [SettingPropertyGroup("API Settings/Main Settings", GroupOrder = 0)]
        public int MaxTokens { get; set; } = 1000;

        [SettingPropertyBool("Enable Longer Responses", RequireRestart = false, HintText = "Enable to allow longer responses from the NPCs.")]
        [SettingPropertyGroup("API Settings/Main Settings", GroupOrder = 0)]
        public bool LongerResponses { get; set; } = false;

        // setting for adding customizable information to prompt
        [SettingPropertyText("Custom Global Information", RequireRestart = false, HintText = "Enter any information or instructions you want to pass to all npcs. Examples: 'You are in the Game of Thrones universe.' or 'speak only in japanese'.")]
        [SettingPropertyGroup("API Settings/Main Settings", GroupOrder = 0)]
        public string CustomPrompt { get; set; } = "";

        // setting to disable ai driven actions
        [SettingPropertyBool("Enable AI Driven Actions", RequireRestart = false, HintText = "Toggle AI driven actions like telling npcs to go to a location, accepting quests, etc. Disabling can help save tokens and improve performance with lower quality models.")]
        [SettingPropertyGroup("API Settings/Main Settings", GroupOrder = 0)]
        public bool ToggleAIActions { get; set; } = true;

        // setting to disable giving npcs information about quests (better to disable if using local models/free versions)
        [SettingPropertyBool("Enable Quest Information", RequireRestart = false, HintText = "Toggle giving NPCs information about quests they offer, if they have them. Disabling can help save tokens and improve performance with lower quality models.")]
        [SettingPropertyGroup("API Settings/Main Settings", GroupOrder = 0)]
        public bool ToggleQuestInfo { get; set; } = true;


        // OpenAI Settings Group
        [SettingPropertyText("OpenAI API Key", RequireRestart = false, HintText = "Enter your OpenAI API key here.")]
        [SettingPropertyGroup("API Settings/OpenAI Settings", GroupOrder = 1)]
        public string OpenAIAPIKey { get; set; } = "";

        [SettingPropertyDropdown("OpenAI Model", RequireRestart = false, HintText = "Select the OpenAI model to use for generating responses.")]
        [SettingPropertyGroup("API Settings/OpenAI Settings", GroupOrder = 1)]
        public Dropdown<string> OpenAIModel { get; set; } = new Dropdown<string>(
            new List<string> { "gpt-4", "gpt-3.5-turbo", "gpt-4o-mini", "gpt-4o" }, 0);

        // Kobald Settings Group
        [SettingPropertyText("KoboldCpp URL", RequireRestart = false, HintText = "Enter the URL of your KoboldCpp model server.")]
        [SettingPropertyGroup("API Settings/Kobaldccp Settings", GroupOrder = 2)]
        public string LocalModelURL { get; set; } = "http://localhost:5001/api/v1/generate";

        [SettingPropertyText("KoboldCpp Model", RequireRestart = false, HintText = "Enter the name of the model you want to use with KoboldCpp.")]
        [SettingPropertyGroup("API Settings/Kobaldccp Settings", GroupOrder = 2)]
        public string KoboldCppModel { get; set; } = "Your-Model-Name";


        // Ollama Settings Group
        [SettingPropertyText("Ollama URL", RequireRestart = false, HintText = "Enter the URL of your Ollama model server.")]
        [SettingPropertyGroup("API Settings/Ollama Settings", GroupOrder = 3)]
        public string OllamaURL { get; set; } = "http://localhost:11434/api/chat";

        [SettingPropertyText("Ollama Model", RequireRestart = false, HintText = "Enter the name of the Ollama model you want to use.")]
        [SettingPropertyGroup("API Settings/Ollama Settings", GroupOrder = 5)]
        public string OllamaModel { get; set; } = "Your-Model-Name";


        // OpenRouter Settings Group
        [SettingPropertyText("OpenRouter API Key", RequireRestart = false, HintText = "Enter your OpenRouter API key here.")]
        [SettingPropertyGroup("API Settings/OpenRouter Settings", GroupOrder = 4)]
        public string OpenRouterAPIKey { get; set; } = "";

        [SettingPropertyText("OpenRouter Model", RequireRestart = false, HintText = "Enter the OpenRouter model to use for generating responses.")]
        [SettingPropertyGroup("API Settings/OpenRouter Settings", GroupOrder = 4)]
        public string OpenRouterModel { get; set; } = "gpt-4";

        // Azure TTS Settings Group
        [SettingPropertyText("Azure TTS Subscription Key", RequireRestart = false, HintText = "Enter your Azure TTS subscription key.")]
        [SettingPropertyGroup("Azure TTS Settings", GroupOrder = 5)]
        public string AzureTTSKey { get; set; } = "";

        [SettingPropertyText("Azure TTS Region", RequireRestart = false, HintText = "Enter your Azure TTS service region.")]
        [SettingPropertyGroup("Azure TTS Settings", GroupOrder = 5)]
        public string AzureTTSRegion { get; set; } = "";

        [SettingPropertyText("Male Voice", RequireRestart = false, HintText = "Specify the Azure TTS voice to use for male NPCs.")]
        [SettingPropertyGroup("Azure TTS Settings", GroupOrder = 5)]
        public string MaleVoice { get; set; } = "en-US-GuyNeural";

        [SettingPropertyText("Female Voice", RequireRestart = false, HintText = "Specify the Azure TTS voice to use for female NPCs.")]
        [SettingPropertyGroup("Azure TTS Settings", GroupOrder = 5)]
        public string FemaleVoice { get; set; } = "en-US-JennyNeural";

        [SettingPropertyButton("Azure TTS Voices", Content = "List of voices", RequireRestart = false, HintText = "Click to open the list of Azure TTS voices you can use.")]
        [SettingPropertyGroup("Azure TTS Settings", GroupOrder = 5)]
        public Action AzureTTSVoices { get; set; } = (() =>
        {
            try
            {
                // Open the Azure TTS voices list in the default browser
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://learn.microsoft.com/en-us/azure/ai-services/speech-service/language-support?tabs=tts#text-to-speech",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                // Display error message
                InformationManager.DisplayMessage(new InformationMessage($"Error opening Azure TTS voices list: {ex.Message}"));
            }
        });



        // Relationship Manager Settings Group
        [SettingPropertyInteger("Base Relationship Gain", 0, 10, RequireRestart = false, HintText = "Base relationship gain for positive interactions.")]
        [SettingPropertyGroup("Relationship Manager Settings", GroupOrder = 6)]
        public int BaseRelationshipGain { get; set; } = 3;

        [SettingPropertyInteger("Base Relationship Loss", -10, 0, RequireRestart = false, HintText = "Base relationship loss for negative interactions.")]
        [SettingPropertyGroup("Relationship Manager Settings", GroupOrder = 6)]
        public int BaseRelationshipLoss { get; set; } = -5;

        [SettingPropertyInteger("Max Relationship Change Per Interaction", 1, 50, RequireRestart = false, HintText = "Maximum relationship change per interaction.")]
        [SettingPropertyGroup("Relationship Manager Settings", GroupOrder = 6)]
        public int MaxRelationshipChange { get; set; } = 10;

        [SettingPropertyBool("Enable Relationship Tracking", RequireRestart = false, HintText = "Toggle whether NPC relationship changes are tracked and displayed.")]
        [SettingPropertyGroup("Relationship Manager Settings", GroupOrder = 6)]
        public bool EnableRelationshipTracking { get; set; } = true;

        // Prompt Settings Group
        [SettingPropertyInteger("Max History Length", 1, 50, RequireRestart = false, HintText = "Maximum number of conversation history entries to include in the prompt. More history will cause slower responses as well as consume more tokens but increase memory of NPCs.")]
        [SettingPropertyGroup("Prompt Settings", GroupOrder = 7)]
        public int MaxHistoryLength { get; set; } = 5;

        // Version Section, show current mod version, then if press button open nexus mod page with version 
        [SettingPropertyButton("Current Version: 0.1.6", Content = "Check for updates", RequireRestart = false, HintText = "Click to check for updates on the Nexus Mods page.")]
        [SettingPropertyGroup("Version", GroupOrder = 10)]
        public Action CheckForUpdates { get; set; } = (() =>
        {
            try
            {
                // Open the Nexus Mods page for the mod
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://www.nexusmods.com/mountandblade2bannerlord/mods/7540?tab=files&jump_to_comment=149470778",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                // Display error message
                InformationManager.DisplayMessage(new InformationMessage($"Error checking for updates: {ex.Message}"));
            }
        });




        // Credits Section
        [SettingPropertyText("Credits", RequireRestart = false, HintText = "Acknowledgements and credits for the mod.")]
        [SettingPropertyGroup("Credits", GroupOrder = 9)]

        // Button to open credits
        [SettingPropertyButton("Created by:", Content = "DeltaBlade", RequireRestart = false, HintText = "Click to check out the developer")]


        public Action Credits { get; set; } = (() =>
        {
            try
            {
                // Open the Azure TTS voices list in the default browser
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://x.com/DeltaBladeZ",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                // Display error message
                InformationManager.DisplayMessage(new InformationMessage($"Error opening credits: {ex.Message}"));
            }
        });

        // Debugging Mode Group
        [SettingPropertyBool("Enable Debug Logging", RequireRestart = false, HintText = "Enable or disable debug logging for the mod. Logs are saved to the mod_log.txt file, located in the ChatAi module folder.")]
        [SettingPropertyGroup("Debugging", GroupOrder = 8)]
        public bool EnableDebugLogging { get; set; } = false;


        // button to open the mod folder
        [SettingPropertyButton("Open debugging file location", Content = "Open", RequireRestart = false, HintText = "Opens the location of your mod_log file.")]
        [SettingPropertyGroup("Debugging", GroupOrder = 8)]


        // Modify the OpenModFolder action to use System.IO.Path explicitly
        public Action OpenModFolder { get; set; } = (static () =>
        {
            try
            {
                // Use BasePath.Name to construct the mod directory path
                string modFolderPath = System.IO.Path.Combine(BasePath.Name, "Modules", "ChatAi");

                if (Directory.Exists(modFolderPath))
                {
                    // Open the mod folder in the file explorer
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = modFolderPath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    // Log and open Desktop folder as a fallback
                    InformationManager.DisplayMessage(new InformationMessage("ChatAi mod folder not found. Opening Desktop instead (Steam version)."));

                    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = desktopPath,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                // Log error message
                InformationManager.DisplayMessage(new InformationMessage($"Error opening folder: {ex.Message}"));
            }
        });

    }
}