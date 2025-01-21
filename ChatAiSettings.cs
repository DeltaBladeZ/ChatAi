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
        [SettingPropertyGroup("API Settings/Main Settings")]
        public Dropdown<string> AIBackend { get; set; } = new Dropdown<string>(
            new List<string> { "OpenAI", "KoboldCpp", "LocalAI", "OpenRouter" }, 0);

        [SettingPropertyInteger("Max Tokens", 100, 4000, RequireRestart = false, HintText = "Set the maximum number of tokens for AI responses.")]
        [SettingPropertyGroup("API Settings/Main Settings")]
        public int MaxTokens { get; set; } = 1000;

        [SettingPropertyBool("Enable Longer Responses", RequireRestart = false, HintText = "Enable to allow longer responses from the NPCs.")]
        [SettingPropertyGroup("API Settings/Main Settings")]
        public bool LongerResponses { get; set; } = false;

        // OpenAI Settings Group
        [SettingPropertyText("OpenAI API Key", RequireRestart = false, HintText = "Enter your OpenAI API key here.")]
        [SettingPropertyGroup("API Settings/OpenAI Settings")]
        public string OpenAIAPIKey { get; set; } = "";

        [SettingPropertyDropdown("OpenAI Model", RequireRestart = false, HintText = "Select the OpenAI model to use for generating responses.")]
        [SettingPropertyGroup("API Settings/OpenAI Settings")]
        public Dropdown<string> OpenAIModel { get; set; } = new Dropdown<string>(
            new List<string> { "gpt-4", "gpt-3.5-turbo", "gpt-4o-mini", "gpt-4o" }, 0);

        // Local Model Settings Group
        [SettingPropertyText("KoboldCpp URL", RequireRestart = false, HintText = "Enter the URL of your KoboldCpp model server.")]
        [SettingPropertyGroup("API Settings/Local Model Settings")]
        public string LocalModelURL { get; set; } = "http://localhost:5001/api/v1/generate";

        // LocalAI Settings Group
        [SettingPropertyText("LocalAI URL", RequireRestart = false, HintText = "Enter the URL of your LocalAI server.")]
        [SettingPropertyGroup("API Settings/LocalAI Settings")]
        public string LocalAIUrl { get; set; } = "http://localhost:5001/v1/chat/completions";

        // OpenRouter Settings Group
        [SettingPropertyText("OpenRouter API Key", RequireRestart = false, HintText = "Enter your OpenRouter API key here.")]
        [SettingPropertyGroup("API Settings/OpenRouter Settings")]
        public string OpenRouterAPIKey { get; set; } = "";

        [SettingPropertyText("OpenRouter Model", RequireRestart = false, HintText = "Enter the OpenRouter model to use for generating responses.")]
        [SettingPropertyGroup("API Settings/OpenRouter Settings")]
        public string OpenRouterModel { get; set; } = "gpt-4";

        // Add these fields under the "API Settings" section
        [SettingPropertyText("Azure TTS Subscription Key", RequireRestart = false, HintText = "Enter your Azure TTS subscription key.")]
        [SettingPropertyGroup("Azure TTS Settings")]
        public string AzureTTSKey { get; set; } = "";

        [SettingPropertyText("Azure TTS Region", RequireRestart = false, HintText = "Enter your Azure TTS service region.")]
        [SettingPropertyGroup("Azure TTS Settings")]
        public string AzureTTSRegion { get; set; } = "";

        [SettingPropertyText("Male Voice", RequireRestart = false, HintText = "Specify the Azure TTS voice to use for male NPCs.")]
        [SettingPropertyGroup("Azure TTS Settings")]
        public string MaleVoice { get; set; } = "en-US-GuyNeural";

        [SettingPropertyText("Female Voice", RequireRestart = false, HintText = "Specify the Azure TTS voice to use for female NPCs.")]
        [SettingPropertyGroup("Azure TTS Settings")]
        public string FemaleVoice { get; set; } = "en-US-JennyNeural";

        //button to open the list of azure voices website
        [SettingPropertyButton("Azure TTS Voices", Content = "List of voices", RequireRestart = false, HintText = "Click to open the list of Azure TTS voices you can use.")]
        [SettingPropertyGroup("Azure TTS Settings")]

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
        [SettingPropertyGroup("Relationship Manager Settings")]
        public int BaseRelationshipGain { get; set; } = 3;

        [SettingPropertyInteger("Base Relationship Loss", -10, 0, RequireRestart = false, HintText = "Base relationship loss for negative interactions.")]
        [SettingPropertyGroup("Relationship Manager Settings")]
        public int BaseRelationshipLoss { get; set; } = -5;

        [SettingPropertyInteger("Max Relationship Change Per Interaction", 1, 50, RequireRestart = false, HintText = "Maximum relationship change per interaction.")]
        [SettingPropertyGroup("Relationship Manager Settings")]
        public int MaxRelationshipChange { get; set; } = 10;

        [SettingPropertyBool("Enable Relationship Tracking", RequireRestart = false, HintText = "Toggle whether NPC relationship changes are tracked and displayed.")]
        [SettingPropertyGroup("Relationship Manager Settings")]
        public bool EnableRelationshipTracking { get; set; } = true;

        // Prompt Settings Group
        [SettingPropertyInteger("Max History Length", 1, 50, RequireRestart = false, HintText = "Maximum number of conversation history entries to include in the prompt. More history will cause slower responses as well as consume more tokens but increase memory of NPCs.")]
        [SettingPropertyGroup("Prompt Settings")]
        public int MaxHistoryLength { get; set; } = 5;

        // Debugging Mode Group
        [SettingPropertyBool("Enable Debug Logging", RequireRestart = false, HintText = "Enable or disable debug logging for the mod. Logs are saved to the mod_log.txt file, located in the ChatAi module folder.")]
        [SettingPropertyGroup("Debugging")]
        public bool EnableDebugLogging { get; set; } = false;



        // Button to open mod folder
        [SettingPropertyButton("Open Mod Folder", Content = "Open Mod Folder", RequireRestart = false, HintText = "Click to open the mod folder containing the log file.")]
        [SettingPropertyGroup("Debugging")]
        public Action OpenModFolder { get; set; } = (() =>
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
                    // Display a message if the folder is not found
                    InformationManager.DisplayMessage(new InformationMessage("Mod folder not found."));
                }
            }
            catch (Exception ex)
            {
                // Display error message
                InformationManager.DisplayMessage(new InformationMessage($"Error opening mod folder: {ex.Message}"));
            }
        });
    }
}