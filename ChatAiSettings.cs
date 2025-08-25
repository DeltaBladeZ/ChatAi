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
            new List<string> { "OpenAI", "KoboldCpp", "Ollama", "OpenRouter", "Player2" }, 4);

        [SettingPropertyDropdown("Voice Backend", RequireRestart = false, HintText = "Select the backend to use for voice responses.")]
        [SettingPropertyGroup("API Settings/Main Settings", GroupOrder = 0)]
        public Dropdown<string> VoiceBackend { get; set; } = new Dropdown<string>(
            new List<string> { "Azure", "Player2", "[None]" }, 1);

        [SettingPropertyInteger("Max Tokens", 100, 4000, RequireRestart = false, HintText = "Set the maximum number of tokens for AI responses.")]
        [SettingPropertyGroup("API Settings/Main Settings", GroupOrder = 0)]
        public int MaxTokens { get; set; } = 1000;

        [SettingPropertyBool("Enable Longer Responses", RequireRestart = false, HintText = "Enable to allow longer responses from the NPCs.")]
        [SettingPropertyGroup("API Settings/Main Settings", GroupOrder = 0)]
        public bool LongerResponses { get; set; } = false;

        [SettingPropertyText("Custom Global Information", RequireRestart = false, HintText = "Enter any information or instructions you want to pass to all npcs. Examples: 'You are in the Game of Thrones universe.' or 'speak only in japanese'.")]
        [SettingPropertyGroup("API Settings/Main Settings", GroupOrder = 0)]
        public string CustomPrompt { get; set; } = "";

        [SettingPropertyBool("Enable AI Driven Actions", RequireRestart = false, HintText = "Toggle AI driven actions like telling npcs to go to a location, accepting quests, etc. Disabling can help save tokens and improve performance with lower quality models.")]
        [SettingPropertyGroup("API Settings/Main Settings", GroupOrder = 0)]
        public bool ToggleAIActions { get; set; } = true;

        [SettingPropertyBool("Enable Quest Information", RequireRestart = false, HintText = "Toggle giving NPCs information about quests they offer, if they have them. Disabling can help save tokens and improve performance with lower quality models.")]
        [SettingPropertyGroup("API Settings/Main Settings", GroupOrder = 0)]
        public bool ToggleQuestInfo { get; set; } = true;

        [SettingPropertyBool("Enable World Event Tracking", RequireRestart = false, HintText = "Toggle whether world events (such as family deaths, clan leader changes, ect) are tracked and passed to related NPCs.")]
        [SettingPropertyGroup("API Settings/Main Settings", GroupOrder = 0)]
        public bool ToggleWorldEvents { get; set; } = true;

        [SettingPropertyBool("Enable Equipment Information", RequireRestart = false, HintText = "Include concise armor and weapon descriptions for the NPC and the player in the prompt.")]
        [SettingPropertyGroup("API Settings/Main Settings", GroupOrder = 0)]
        public bool ToggleEquipmentInfo { get; set; } = true;

        // OpenAI Settings Group
        [SettingPropertyText("OpenAI API Key", RequireRestart = false, HintText = "Enter your OpenAI API key here.")]
        [SettingPropertyGroup("API Settings/OpenAI Settings", GroupOrder = 3)]
        public string OpenAIAPIKey { get; set; } = "";

        [SettingPropertyDropdown("OpenAI Model", RequireRestart = false, HintText = "Select the OpenAI model to use for generating responses.")]
        [SettingPropertyGroup("API Settings/OpenAI Settings", GroupOrder = 3)]
        public Dropdown<string> OpenAIModel { get; set; } = new Dropdown<string>(
            new List<string> { "gpt-4", "gpt-3.5-turbo", "gpt-4o-mini", "gpt-4o" }, 0);

        // Kobald Settings Group
        [SettingPropertyText("KoboldCpp URL", RequireRestart = false, HintText = "Enter the URL of your KoboldCpp model server.")]
        [SettingPropertyGroup("API Settings/Kobaldccp Settings", GroupOrder = 4)]
        public string LocalModelURL { get; set; } = "http://localhost:5001/api/v1/generate";

        [SettingPropertyText("KoboldCpp Model", RequireRestart = false, HintText = "Enter the name of the model you want to use with KoboldCpp.")]
        [SettingPropertyGroup("API Settings/Kobaldccp Settings", GroupOrder = 4)]
        public string KoboldCppModel { get; set; } = "Your-Model-Name";


        // Ollama Settings Group
        [SettingPropertyText("Ollama URL", RequireRestart = false, HintText = "Enter the URL of your Ollama model server.")]
        [SettingPropertyGroup("API Settings/Ollama Settings", GroupOrder = 5)]
        public string OllamaURL { get; set; } = "http://localhost:11434/api/chat";

        [SettingPropertyText("Ollama Model", RequireRestart = false, HintText = "Enter the name of the Ollama model you want to use.")]
        [SettingPropertyGroup("API Settings/Ollama Settings", GroupOrder = 5)]
        public string OllamaModel { get; set; } = "Your-Model-Name";


        // OpenRouter Settings Group
        [SettingPropertyText("OpenRouter API Key", RequireRestart = false, HintText = "Enter your OpenRouter API key here.")]
        [SettingPropertyGroup("API Settings/OpenRouter Settings", GroupOrder = 6)]
        public string OpenRouterAPIKey { get; set; } = "";

        [SettingPropertyText("OpenRouter Model", RequireRestart = false, HintText = "Enter the OpenRouter model to use for generating responses.")]
        [SettingPropertyGroup("API Settings/OpenRouter Settings", GroupOrder = 6)]
        public string OpenRouterModel { get; set; } = "gpt-4";

        // Azure TTS Settings Group
        [SettingPropertyText("Azure TTS Subscription Key", RequireRestart = false, HintText = "Enter your Azure TTS subscription key.")]
        [SettingPropertyGroup("Azure TTS Settings", GroupOrder = 7)]
        public string AzureTTSKey { get; set; } = "";

        [SettingPropertyText("Azure TTS Region", RequireRestart = false, HintText = "Enter your Azure TTS service region.")]
        [SettingPropertyGroup("Azure TTS Settings", GroupOrder = 7)]
        public string AzureTTSRegion { get; set; } = "";

        [SettingPropertyText("Male Voice", RequireRestart = false, HintText = "Specify the Azure TTS voice to use for male NPCs.")]
        [SettingPropertyGroup("Azure TTS Settings", GroupOrder = 7)]
        public string MaleVoice { get; set; } = "en-US-GuyNeural";

        [SettingPropertyText("Female Voice", RequireRestart = false, HintText = "Specify the Azure TTS voice to use for female NPCs.")]
        [SettingPropertyGroup("Azure TTS Settings", GroupOrder = 7)]
        public string FemaleVoice { get; set; } = "en-US-JennyNeural";

        [SettingPropertyButton("Azure TTS Voices", Content = "List of voices", RequireRestart = false, HintText = "Click to open the list of Azure TTS voices you can use.")]
        [SettingPropertyGroup("Azure TTS Settings", GroupOrder = 7)]
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
        [SettingPropertyInteger("Base Relationship Gain", 0, 20, RequireRestart = false,
            HintText = "Amount added to positive relationship changes. If an NPC reaction is +5, and this is set to 3, the final change will be +8.")]
        [SettingPropertyGroup("Relationship Manager Settings", GroupOrder = 6)]
        public int BaseRelationshipGain { get; set; } = 1;

        [SettingPropertyInteger("Base Relationship Loss", -20, 0, RequireRestart = false,
            HintText = "Amount subtracted from negative relationship changes. If an NPC reaction is -5, and this is set to -3, the final change will be -8.")]
        [SettingPropertyGroup("Relationship Manager Settings", GroupOrder = 6)]
        public int BaseRelationshipLoss { get; set; } = -1;

        [SettingPropertyInteger("Max Relationship Change Per Interaction", 1, 50, RequireRestart = false,
            HintText = "Caps the maximum relationship change (positive or negative) per interaction. If set to 10, changes cannot exceed +10 or go below -10. If set to 0, relationship changes are disabled.")]
        [SettingPropertyGroup("Relationship Manager Settings", GroupOrder = 6)]
        public int MaxRelationshipChange { get; set; } = 10;

        [SettingPropertyBool("Enable Relationship Tracking", RequireRestart = false,
            HintText = "Toggle whether to track and display relationship changes with NPCs. Disabling prevents relationship changes from showing on screen, but they are still applied.")]
        [SettingPropertyGroup("Relationship Manager Settings", GroupOrder = 6)]
        public bool EnableRelationshipTracking { get; set; } = true;


        // Prompt Settings Group
        [SettingPropertyInteger("Max History Length", 1, 50, RequireRestart = false, HintText = "Maximum number of conversation history entries to include in the prompt. More history will cause slower responses as well as consume more tokens but increase memory of NPCs.")]
        [SettingPropertyGroup("Prompt Settings", GroupOrder = 7)]
        public int MaxHistoryLength { get; set; } = 5;

        // Version Section, show current mod version, then if press button open nexus mod page with version 
        [SettingPropertyButton("Current Version: 0.2.3", Content = "Check for updates", RequireRestart = false, HintText = "Click to check for updates on the Nexus Mods page.")]
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


        // Button to clear all NPC context and history, placed at the bottom
        [SettingPropertyButton("Clear NPC Data", Content = "Delete ALL History", RequireRestart = false, HintText = "Click to clear all saved NPC data, including conversation history and context.")]
        [SettingPropertyGroup("Data Management", GroupOrder = 99)]
        public Action ClearNPCData { get; set; } = (() =>
        {
            try
            {
                InformationManager.ShowInquiry(new InquiryData(
                    "Confirm Data Reset",
                    "Are you sure you want to clear all NPC data? \n\n" +
                    "This will delete all conversation history and context for all NPCs on ALL games saves. This action cannot be undone!",
                    true, true, "Yes", "No",
                    () =>
                    {
                        ChatBehavior.Instance.ClearAllNPCData();
                        InformationManager.DisplayMessage(new InformationMessage("All NPC context and history cleared!"));
                    },
                    () => { }
                ));
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"Error clearing NPC data: {ex.Message}"));
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




        // Button to visit Patreon page
        [SettingPropertyButton("Thanks to our Patreon Supporters: Jeonsa", Content = "Become a Supporter on Patreon to support the mod!", RequireRestart = false, HintText = "Click to visit our Patreon page and support the development of this mod.")]
        [SettingPropertyGroup("Credits", GroupOrder = 9)]
        public Action VisitPatreon { get; set; } = (() =>
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://www.patreon.com/c/DeltaBlade",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"Error opening Patreon page: {ex.Message}"));
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
                // Use PathHelper to get the correct mod directory path
                string modFolderPath = PathHelper.GetModFolderPath();

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

        // Player2 Settings Group
        [SettingPropertyText("Player2 API URL", RequireRestart = false, HintText = "The URL for the Player2 API. Default is http://localhost:4315")]
        [SettingPropertyGroup("API Settings/Player2 Settings", GroupOrder = 1)]
        public string Player2ApiUrl { get; set; } = "http://localhost:4315";

        [SettingPropertyButton("Test Player2 Connection", Content = "Test Connection", RequireRestart = false, HintText = "Click to test your connection to Player2 and verify it's running correctly.")]
        [SettingPropertyGroup("API Settings/Player2 Settings", GroupOrder = 1)]
        public Action TestPlayer2Connection { get; set; } = (async () =>
        {
            try
            {
                InformationManager.DisplayMessage(new InformationMessage("Testing connection to Player2..."));
                
                var debugLogger = new DebugModLogger();
                bool isAvailable = await debugLogger.CheckPlayer2Availability();
                
                if (!isAvailable)
                {
                    InformationManager.ShowInquiry(
                        new InquiryData(
                            "Player2 Connection Failed", 
                            "Could not connect to Player2. Please ensure:\n\n" +
                            "1. Player2 is downloaded from player2.game\n" +
                            "2. Player2 is running in the background\n" +
                            "3. The API URL is correct in the settings\n\n" +
                            "Would you like to open the Player2 website?",
                            true, true, "Open Website", "Cancel", 
                            () => 
                            {
                                try
                                {
                                    Process.Start(new ProcessStartInfo
                                    {
                                        FileName = "https://player2.game",
                                        UseShellExecute = true
                                    });
                                }
                                catch (Exception ex)
                                {
                                    InformationManager.DisplayMessage(new InformationMessage($"Error opening website: {ex.Message}"));
                                }
                            }, 
                            null
                        )
                    );
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"Error testing Player2 connection: {ex.Message}"));
            }
        });

        [SettingPropertyButton("Download Player2", Content = "Open Website", RequireRestart = false, HintText = "Opens the Player2 website where you can download the application.")]
        [SettingPropertyGroup("API Settings/Player2 Settings", GroupOrder = 1)]
        public Action OpenPlayer2Website { get; set; } = (() =>
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://player2.game",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"Error opening Player2 website: {ex.Message}"));
            }
        });

        // Player2 TTS Settings Group
        [SettingPropertyText("Player2 Male Voice ID", RequireRestart = false, HintText = "The ID of the voice to use for male NPCs. Leave empty to use default voice.")]
        [SettingPropertyGroup("API Settings/Player2 TTS Settings", GroupOrder = 2)]
        public string Player2MaleVoiceId { get; set; } = "";

        [SettingPropertyText("Player2 Female Voice ID", RequireRestart = false, HintText = "The ID of the voice to use for female NPCs. Leave empty to use default voice.")]
        [SettingPropertyGroup("API Settings/Player2 TTS Settings", GroupOrder = 2)]
        public string Player2FemaleVoiceId { get; set; } = "";

        [SettingPropertyText("Player2 Voice Language", RequireRestart = false, HintText = "The language code for the voice (e.g., en_US).")]
        [SettingPropertyGroup("API Settings/Player2 TTS Settings", GroupOrder = 2)]
        public string Player2VoiceLanguage { get; set; } = "en_US";

        [SettingPropertyButton("Save Available Player2 Voices to Log", Content = "Save to File", RequireRestart = false, HintText = "Click to fetch all available Player2 voices and save them to player2_voices.txt in the mod folder.")]
        [SettingPropertyGroup("API Settings/Player2 TTS Settings", GroupOrder = 2)]
        public Action RefreshPlayer2Voices { get; set; } = (() =>
        {
            try
            {
                Player2TextToSpeech.RefreshAvailableVoices();
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"Error fetching Player2 voices: {ex.Message}"));
            }
        });

        [SettingPropertyInteger("Player2 TTS Speed", 50, 200, RequireRestart = false, HintText = "The speed of the TTS voice (50 to 200, where 100 is normal speed).")]
        [SettingPropertyGroup("API Settings/Player2 TTS Settings", GroupOrder = 2)]
        public int Player2TTSSpeed { get; set; } = 100;

        [SettingPropertyInteger("Player2 TTS Volume", 0, 100, RequireRestart = false, HintText = "The volume of the TTS voice (0 to 100).")]
        [SettingPropertyGroup("API Settings/Player2 TTS Settings", GroupOrder = 2)]
        public int Player2TTSVolume { get; set; } = 70;


    }
}