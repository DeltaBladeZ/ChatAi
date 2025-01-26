using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Newtonsoft.Json;
using System.Reflection;
using TaleWorlds.CampaignSystem.Issues;
using ChatAi.Quests;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.ScreenSystem;




namespace ChatAi
{
    public class ChatBehavior : CampaignBehaviorBase
    {




        private readonly string _logFilePath = Path.Combine(BasePath.Name, "Modules", "ChatAi", "mod_log.txt");

        // Dictionary to store NPC contexts
        private Dictionary<string, NPCContext> _npcContexts = new Dictionary<string, NPCContext>();
        public override void RegisterEvents() {}
        public override void SyncData(IDataStore dataStore)
        {
            try
            {

                if (dataStore.IsSaving)
                {
                    // Convert _npcContexts to a serializable format (JSON string)
                    string serializedData = SerializeNPCContexts();
                    dataStore.SyncData("_serializedNPCContexts", ref serializedData);
                }
                else if (dataStore.IsLoading)
                {
                    // Load serialized data and deserialize it back into _npcContexts
                    string serializedData = null;
                    dataStore.SyncData("_serializedNPCContexts", ref serializedData);

                    if (!string.IsNullOrEmpty(serializedData))
                    {
                        DeserializeNPCContexts(serializedData);
                    }
                    else
                    {
                        _npcContexts = new Dictionary<string, NPCContext>(); 
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error syncing data: {ex.Message}");
            }
        }

        private string SerializeNPCContexts()
        {
            try
            {
                return JsonConvert.SerializeObject(_npcContexts);
            }
            catch (Exception ex)
            {
                LogMessage($"Error serializing NPC contexts: {ex.Message}");
                return string.Empty;
            }
        }
        private void DeserializeNPCContexts(string serializedData)
        {
            try
            {
                _npcContexts = JsonConvert.DeserializeObject<Dictionary<string, NPCContext>>(serializedData)
                               ?? new Dictionary<string, NPCContext>();
            }
            catch (Exception ex)
            {
                LogMessage($"Error deserializing NPC contexts: {ex.Message}");
                _npcContexts = new Dictionary<string, NPCContext>(); // Fallback to empty if deserialization fails
            }
        }

        private string GetCurrentDate()
        {
            CampaignTime now = CampaignTime.Now;

            int year = now.GetYear; // Get the current year
            int totalDays = (int)now.ToDays; // Total days elapsed in the campaign
            int dayOfYear = totalDays % 84; // Days elapsed in the current year (84 days per year)

            // Calculate month and day within the month
            int month = dayOfYear / 21; // 21 days per month
            int dayOfMonth = dayOfYear % 21 + 1; // Day of the month (1-indexed)

            // Convert month index to a readable name
            string[] monthNames = { "Spring", "Summer", "Autumn", "Winter" };
            string monthName = month >= 0 && month < monthNames.Length ? monthNames[month] : $"Month {month + 1}";

            return $"{monthName} {dayOfMonth}, Year {year}";
        }

        private NPCContext GetOrCreateNPCContext(Hero npc)
        {
            string npcId = npc.StringId;

            if (!_npcContexts.ContainsKey(npcId))
            {
                var context = new NPCContext
                {
                    Name = npc.Name.ToString()
                };

                // Add dynamic stats
                context.AddDynamicStat("Title/Occupation", () => npc.Occupation.ToString());
                context.AddDynamicStat("Fief", () => npc.CurrentSettlement?.Name?.ToString() ?? "No fief");
                context.AddDynamicStat("Relationship with player(-100 being hated/enemies, 0 being neutral, 100 being very liked/best friends)",
                    () => npc.GetRelationWithPlayer().ToString());
                context.AddDynamicStat("Renown", () => npc.Clan?.Renown.ToString() ?? "Unknown");
                context.AddDynamicStat("Personality", () => GetPersonalityDescription(npc));
                context.AddDynamicStat("Age", () => npc.Age.ToString("0")); // Format age to avoid decimals
                context.AddDynamicStat("Gender", () => npc.IsFemale ? "Female" : "Male");
                context.AddDynamicStat("Culture", () => npc.Culture?.Name?.ToString() ?? "Unknown");

                // Add kingdom affiliation dynamically
                context.AddDynamicStat("Kingdom",
                    () => npc.Clan?.Kingdom?.Name?.ToString() ?? "No kingdom");

                // Add clan information dynamically
                context.AddDynamicStat("Clan",
                    () => npc.Clan?.Name?.ToString() ?? "No clan");
                context.AddDynamicStat("Clan Leader",
                    () => npc.Clan?.Leader?.Name?.ToString() ?? "No leader");

                // npc mother and father
                context.AddDynamicStat("Mother",
                    () => npc.Mother?.Name?.ToString() ?? "Unknown");
                context.AddDynamicStat("Father",
                    () => npc.Father?.Name?.ToString() ?? "Unknown");

                // children if any
                context.AddDynamicStat("Children",
                    () => npc.Children.Any() ? string.Join(", ", npc.Children.Select(c => c.Name.ToString())) : "None");

                // spouse if any
                context.AddDynamicStat("Spouse",
                    () => npc.Spouse?.Name?.ToString() ?? "None");



                _npcContexts[npcId] = context;
            }

            return _npcContexts[npcId];
        }



        private string GetPersonalityDescription(Hero npc)
        {
            if (npc == null) return "Unknown personality";

            var descriptions = new List<string>();

            // Calculating
            var calculating = npc.GetTraitLevel(DefaultTraits.Calculating);
            if (calculating == 2) descriptions.Add("Cerebral");
            else if (calculating == 1) descriptions.Add("Calculating");
            else if (calculating == -1) descriptions.Add("Impulsive");
            else if (calculating == -2) descriptions.Add("Hotheaded");

            // Generosity
            var generosity = npc.GetTraitLevel(DefaultTraits.Generosity);
            if (generosity == 2) descriptions.Add("Munificent");
            else if (generosity == 1) descriptions.Add("Generous");
            else if (generosity == -1) descriptions.Add("Closefisted");
            else if (generosity == -2) descriptions.Add("Tightfisted");

            // Honor
            var honor = npc.GetTraitLevel(DefaultTraits.Honor);
            if (honor == 2) descriptions.Add("Honorable");
            else if (honor == 1) descriptions.Add("Honest");
            else if (honor == -1) descriptions.Add("Devious");
            else if (honor == -2) descriptions.Add("Deceitful");

            // Mercy
            var mercy = npc.GetTraitLevel(DefaultTraits.Mercy);
            if (mercy == 2) descriptions.Add("Compassionate");
            else if (mercy == 1) descriptions.Add("Merciful");
            else if (mercy == -1) descriptions.Add("Cruel");
            else if (mercy == -2) descriptions.Add("Sadistic");

            // Valor
            var valor = npc.GetTraitLevel(DefaultTraits.Valor);
            if (valor == 2) descriptions.Add("Fearless");
            else if (valor == 1) descriptions.Add("Daring");
            else if (valor == -1) descriptions.Add("Cautious");
            else if (valor == -2) descriptions.Add("Very Cautious");

            // Combine descriptions into a single string
            return descriptions.Count > 0 ? string.Join(", ", descriptions) : "A balanced personality.";
        }

        private string GetPlayerLocation()
        {
            // If the player is inside a settlement
            if (Hero.MainHero.CurrentSettlement != null)
            {
                string settlementType = GetSettlementType(Hero.MainHero.CurrentSettlement);
                string kingdom = Hero.MainHero.CurrentSettlement.OwnerClan?.Kingdom?.Name?.ToString() ?? "no kingdom";
                return $"in the {settlementType} of {Hero.MainHero.CurrentSettlement.Name}, part of the kingdom of {kingdom}";
            }


            // If the player is on the world map, use their MobileParty position
            if (Hero.MainHero.PartyBelongedTo != null)
            {
                var playerPosition = Hero.MainHero.PartyBelongedTo.Position2D;

                Settlement nearestSettlement = Settlement.All
                    .OrderBy(s => s.Position2D.Distance(playerPosition))
                    .FirstOrDefault();

                if (nearestSettlement != null)
                {
                    string settlementType = GetSettlementType(nearestSettlement);
                    string kingdom = nearestSettlement.OwnerClan?.Kingdom?.Name?.ToString() ?? "no kingdom";
                    return $"on the road near the {settlementType} of {nearestSettlement.Name}, part of the kingdom of {kingdom}";
                }
            }

            // Fallback for unknown location
            return "in an unknown location";
        }


        private string GetSettlementType(Settlement settlement)
        {
            if (settlement.IsTown) return "town";
            if (settlement.IsCastle) return "castle";
            if (settlement.IsVillage) return "village";
            return "unknown settlement";
        }

        private QuestManager _questManager = new QuestManager();
        private string GeneratePrompt(Hero npc, string confirmationMessage = "")
        {
            NPCContext context = GetOrCreateNPCContext(npc);

            string playerName = Hero.MainHero?.Name?.ToString() ?? "Stranger";
            string playerLocation = GetPlayerLocation();
            string currentDate = GetCurrentDate();

            // Fetch the "Longer Responses" setting
            bool longerResponses = ChatAiSettings.Instance.LongerResponses;

            string prompt = $"You are {context.Name}, an NPC in the medieval fantasy world of the game Bannerlord. Respond as a medieval character would in this setting.\n\n";
            prompt += $"You are speaking with {playerName}, who is currently {playerLocation}. The current date is {currentDate}.\n";

            // Fetch all dynamic stats
            foreach (var stat in context.GetAllStats())
            {
                prompt += $"\n{stat.Key}: {stat.Value}\n";
            }

            // Add quest details
            var questManager = new QuestManager();
            string questDetails = questManager.GetQuestDetailsForPrompt(npc);
            if (!string.IsNullOrWhiteSpace(questDetails))
            {
                prompt += $"\n{questDetails}\n";
            }

            // Check for quest conditions and add failure reason if applicable
            var escortHandler = new EscortMerchantCaravanHandler();
            var quests = questManager.GetQuestsForNPC(npc);

            //BROKEN CODE FIX LATER
            //        foreach (var quest in quests)
            //       {
            //            if (quest is EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue escortIssue)
            //           {
            //                 LogMessage($"DEBUG: Checking quest conditions for NPC {npc.Name}.");
            //                MethodInfo conditionsMethod = typeof(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue)
            //                     .GetMethod("CanPlayerTakeQuestConditions", BindingFlags.Instance | BindingFlags.NonPublic);
            //
            //      if (conditionsMethod != null)
            //      {
            //           //          object[] parameters = { npc, null, null, null };
            //          bool canAccept = (bool)conditionsMethod.Invoke(escortIssue, parameters);
            //          LogMessage($"DEBUG: Quest conditions check result: {canAccept}");
            //
            //         if (!canAccept && parameters[1] is string reason)
            //             LogMessage($"DEBUG: Quest rejection reason: {reason}");
            //        {
            //            prompt += $"\n\nYou can not let the player accept your quest due to this reason: {reason}";
            //      }
            //  }
            //    }
            //  }


            prompt += "\n\nRecent conversation history:\n";
            prompt += context.GetFormattedHistory();

            // Instructions for response style
            prompt += "\n\nInstructions:\n";
            prompt += "- Answer the player's latest question or comment, in character and with the correct personality.\n";
            prompt += "- Do not reference the AI, modern concepts, or anything outside this world.\n";
            if (longerResponses)
            {
                prompt += "- Provide a long 2-4 paragraph, detailed and immersive response, that responds to the player.\n";
            }
            else
            {
                prompt += "- Keep responses concise and immersive. Avoid overly verbose replies unless directly asked for details.\n";
            }
            prompt += "- If you offer any quest, try to convince the player to accept it. If you don't have a current quest don't mention anything about quests.\n";




            if (!string.IsNullOrEmpty(confirmationMessage))
            {
                prompt += $"\n\nExtra Instruction: {confirmationMessage}";
            }

            prompt += $"\n\n{context.Name} Responds: ";

            LogMessage($"DEBUG: Generated prompt for NPC {npc.Name}: {prompt}");

            return prompt;
        }



        public void AddDialogs(CampaignGameStarter starter)
        {
           

            try
            {
                string variableName = "DYNAMIC_NPC_RESPONSE";
                string defaultValue = "I have nothing to say right now.";

                // Initialize the dynamic response variable
                if (!string.IsNullOrEmpty(variableName))
                {
                    MBTextManager.SetTextVariable(variableName, defaultValue);
                    
                }



                // Start Chat Option
                SafeAddPlayerLine(
                    starter,
                    "chat_with_me_start",
                    "hero_main_options",
                    "chat_with_me_response",
                    "Chat with me.",
                    () => Hero.OneToOneConversationHero != null,
                    null
                );

                // NPC's Response
                SafeAddDialogLine(
                    starter,
                    "chat_with_me_response",
                    "chat_with_me_response",
                    "chat_with_me_input",
                    "Sure, let's chat."
                );

                // Player's Input Option
                SafeAddPlayerLine(
                    starter,
                    "chat_with_me_input",
                    "chat_with_me_input",
                    "chat_with_me_processing",
                    "Let me think...",
                    null,
                    async () =>
                    {
     
                        await HandlePlayerInput();


                    }
                );


                // NPC's Thinking Placeholder
                SafeAddDialogLine(
                    starter,
                    "chat_with_me_processing",
                    "chat_with_me_processing",
                    "chat_with_me_dynamic_response",
                    "Please wait while I think..."



                );

                // NPC's Dynamic Response
                SafeAddDialogLine(
                    starter,
                    "chat_with_me_dynamic_response",
                    "chat_with_me_dynamic_response",
                    "chat_with_me_input",
                    "{=dynamic_response}{DYNAMIC_NPC_RESPONSE}",
                    null,
                    () =>
                    {
                        LogMessage("Player clicked to continue the conversation and hear the NPC's response.");
                        //check if azure is the backend selected in chatAi settings
                        if (ChatAiSettings.Instance.VoiceBackend?.SelectedValue == "Azure")
                        LogMessage("Azure TTS selected. Playing deferred audio.");
                        {
                            AzureTextToSpeech.PlayDeferredAudio(); // Use static method
                        }

                    }

                );

                // Goodbye Option
                SafeAddPlayerLine(
                    starter,
                    "chat_with_me_input_goodbye",
                    "chat_with_me_input",
                    "end_conversation",
                    "Goodbye.",
                    null,
                    () =>
                    {
                        if (ChatAiSettings.Instance.VoiceBackend?.SelectedValue == "Azure")
                            LogMessage("Player ended the conversation. Cancelling playback...");
                            AzureTextToSpeech.CancelPlayback();
                        

                    }
                );
                // Placeholder End State
                SafeAddDialogLine(
                    starter,
                    "end_conversation",
                    "end_conversation",
                    "hero_main_options",
                    "Farewell. May your journeys be safe and prosperous.",
                    () =>
                    {
                        
                        return true;
                    },
                    null
                );
            }
            catch (Exception ex)
            {
                LogMessage($"Error adding dialog options: {ex.Message}");
            }
        }
        private AIActionEvaluator _actionEvaluator = new AIActionEvaluator();

        private NPCBehaviorLogic _npcBehaviorLogic = new NPCBehaviorLogic();


        private void LogQuestDetails(IssueBase quest)
        {
            try
            {
                var type = quest.GetType();
                LogMessage($"\nDEBUG: Inspecting quest type: {type.Name}");

                // Log all methods
                var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                foreach (var method in methods)
                {
                    LogMessage($"\nDEBUG: Method - {method.Name}");
                }

                // Log all properties
                var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                foreach (var property in properties)
                {
                    LogMessage($"\nDEBUG: Property - {property.Name} ({property.PropertyType.Name})");
                }

                // Log all fields
                var fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                foreach (var field in fields)
                {
                    LogMessage($"\nDEBUG: Field - {field.Name} ({field.FieldType.Name})");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"\nERROR: Failed to log details for quest type {quest.GetType().Name}: {ex.Message}");
            }
        }

        public async void GenerateNPCSpeech(Hero npc, string text)
        {
            LogMessage($"Generating speech for NPC: {npc.Name}. Gender: {(npc.IsFemale ? "Female" : "Male")}");

            var tts = new AzureTextToSpeech();
            await tts.GenerateSpeech(text, npc.IsFemale); // Pass gender to TTS
        }



        private async Task HandlePlayerInput()
        {

            // Fetch the player's input
            string userInput = await TextInput("Type your message to the NPC");
            if (userInput != null) {
                userInput = userInput.Trim();
            }





            LogMessage($"DEBUG: Player input received: {userInput}");

            if (string.IsNullOrWhiteSpace(userInput))
            {
                MBTextManager.SetTextVariable("DYNAMIC_NPC_RESPONSE", "Goodbye.");
                LogMessage("User input was empty. Ending conversation.");
                return;
            }

            Hero npc = Hero.OneToOneConversationHero;
            if (npc == null)
            {
                LogMessage("Error: No NPC found for the conversation.");
                return;
            }

            NPCContext context = GetOrCreateNPCContext(npc);

            string npcLastMessage = context.GetLatestNPCMessage(); // Fetch NPC's last message before user input

            context.AddMessage($"User: {userInput}");

            InformationManager.DisplayMessage(new InformationMessage("Please wait, I am thinking!"));

            // Update relationship based on player's message
            var relationshipManager = new RelationshipManager();
            int relationshipChange = await relationshipManager.UpdateRelationshipBasedOnMessage(npc, userInput);

            if (relationshipChange != 0)
            {
                npc.SetPersonalRelation(Hero.MainHero, (int)Math.Round(npc.GetRelationWithPlayer()) + relationshipChange);
            }

            // Ensure updated relationship is used in prompt generation
            int updatedRelation = (int)Math.Round(npc.GetRelationWithPlayer());

            var (action, targetSettlement, confirmationMessage) = await _actionEvaluator.EvaluateActionWithTargetAndMessage(npc, userInput);

            LogMessage($"DEBUG: Handling action {action.ToString()} for NPC {npc.Name} with target {targetSettlement?.Name?.ToString() ?? "null"}.");

            // Handle Action
            if (action != AIActionEvaluator.Action.None && (action != AIActionEvaluator.Action.GoToSettlement || targetSettlement != null))
            {
                HandleAction(npc, action, targetSettlement);
            }
            else if (confirmationMessage != null)
            {
                LogMessage($"DEBUG: Confirmation message: {confirmationMessage}");
            }

            // Prompt Generation
            string prompt = GeneratePrompt(npc, confirmationMessage);
            //LogMessage($"DEBUG: Calling AIHelper.GetResponse for chatbot with prompt: {prompt}");
            string response = await AIHelper.GetResponse(prompt);

            if (string.IsNullOrWhiteSpace(response))
            {
                response = "Sorry, I couldn't understand that.";
                LogMessage("Empty AI response. Default used.");
            }

            // Add the AI's response to the conversation history
            context.AddMessage($"NPC: {response}");

            MBTextManager.SetTextVariable("DYNAMIC_NPC_RESPONSE", response);

            // Call Azure TTS to synthesize the response
            LogMessage("Initiating Azure TTS...");
            GenerateNPCSpeech(npc, response);
            LogMessage("Azure TTS synthesis completed.");

            // Analyze Quest Acceptance
            bool isQuestAccepted = await _questManager.AnalyzeQuestAcceptance(npcLastMessage, userInput);

            if (isQuestAccepted)
            {
                var quests = _questManager.GetQuestsForNPC(npc);

                if (quests.Count > 0)
                {
                    foreach (var issue in quests)
                    {
                        LogMessage($"DEBUG: Attempting to handle quest {issue.GetType().Name} for NPC {npc.Name}.");
                        if (_questManager.HandleQuest(issue, npc))
                        {
                            LogMessage($"DEBUG: Successfully handled quest {issue.GetType().Name} for NPC {npc.Name}.");
                        }
                        else
                        {
                            LogMessage($"ERROR: Failed to handle quest {issue.GetType().Name} for NPC {npc.Name}.");
                        }
                    }
                }
                else
                {
                    LogMessage($"DEBUG: No active quests available to accept for NPC {npc.Name}.");
                }
            }

            InformationManager.DisplayMessage(new InformationMessage("I am ready to respond now!"));
        }





        private void HandleAction(Hero npc, AIActionEvaluator.Action action, Settlement targetSettlement = null)
        {

            var behaviorLogic = new NPCBehaviorLogic();

            switch (action)
            {
                case AIActionEvaluator.Action.GoToSettlement:
                    if (targetSettlement != null)
                    {
                        behaviorLogic.GoToSettlement(npc, targetSettlement);
                    }
                    else
                    {
                        LogMessage($"DEBUG: GoToSettlement action triggered with null targetSettlement for NPC {npc.Name}.");
                    }
                    break;

                case AIActionEvaluator.Action.PatrolAroundTown:
                    if (npc.CurrentSettlement != null)
                    {
                        behaviorLogic.PatrolAround(npc, npc.CurrentSettlement);
                    }
                    else
                    {
                        LogMessage($"DEBUG: PatrolAroundTown action for NPC {npc.Name} failed: No current settlement.");
                    }
                    break;

                case AIActionEvaluator.Action.DeliverMessage:
                    behaviorLogic.DeliverMessage(npc);
                    break;

                default:
                    LogMessage($"DEBUG: No valid action detected for NPC {npc.Name}.");
                    break;
            }
        }






        private Task<string> TextInput(string prompt)
        {
            var tcs = new TaskCompletionSource<string>();

            InformationManager.ShowTextInquiry(new TextInquiryData(
                prompt,
                string.Empty,
                true, true,
                "Send",
                "Cancel",
                result =>
                {
                    
                    tcs.SetResult(result);
                },
                () =>
                {
                    
                    tcs.SetResult(null);
                }));

            return tcs.Task;
        }

        private void SafeAddPlayerLine(
            CampaignGameStarter starter,
            string id,
            string inputToken,
            string outputToken,
            string text,
            ConversationSentence.OnConditionDelegate conditionDelegate = null,
            ConversationSentence.OnConsequenceDelegate consequenceDelegate = null)
        {
            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(inputToken))
            {
                starter.AddPlayerLine(id, inputToken, outputToken, text, conditionDelegate, consequenceDelegate);
                
            }
            else
            {
                LogMessage($"Skipped adding player line. id or inputToken is null/empty.");
            }
        }

        private void SafeAddDialogLine(
            CampaignGameStarter starter,
            string id,
            string inputToken,
            string outputToken,
            string text,
            ConversationSentence.OnConditionDelegate conditionDelegate = null,
            ConversationSentence.OnConsequenceDelegate consequenceDelegate = null)
        {
            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(inputToken))
            {
                starter.AddDialogLine(id, inputToken, outputToken, text, conditionDelegate, consequenceDelegate);
               
            }
            else
            {
                LogMessage($"Skipped adding dialog line. id or inputToken is null/empty.");
            }
        }

        private void LogMessage(string message)
        {
            try
            {
                // Check if debug logging is enabled in the settings
                if (!ChatAiSettings.Instance.EnableDebugLogging)
                {
                    return; // Skip logging if disabled
                }

                string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}\n";
                File.AppendAllText(_logFilePath, logMessage);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"Logging error: {ex.Message}"));
            }
        }

    }
}
