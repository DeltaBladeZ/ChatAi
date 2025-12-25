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
using TaleWorlds.ScreenSystem;
using TaleWorlds.SaveSystem.Load;
using System.Text.RegularExpressions;

namespace ChatAi
{
    public class ChatBehavior : CampaignBehaviorBase
    {
        private static ChatBehavior _instance;
        public static ChatBehavior Instance => _instance ??= new ChatBehavior();

		private System.Threading.CancellationTokenSource _uiDebugWatcherCts;
		private string _lastTopScreenName = "";

        private readonly string _logFilePath = PathHelper.GetModFilePath("mod_log.txt");

        private readonly string _saveDataPath = PathHelper.GetModFolderPath("save_data");
        private string _currentSaveFolder;

        // Dictionary to store NPC contexts with timestamp of when they were loaded
        private Dictionary<string, (NPCContext context, DateTime loadTime)> _npcContexts = new Dictionary<string, (NPCContext, DateTime)>();
        public override void RegisterEvents()
        {
            LogMessage("[DEBUG] Registering ChatBehavior...");

            // Always register world events (independent of settings availability)
            try
            {
                // Manually register WorldEventListener
                WorldEventListener worldEventListener = new WorldEventListener();
                worldEventListener.RegisterEvents();
            }
            catch
            {
                // Fail closed
            }

            // Settings may not be initialized yet on older Bannerlord versions.
            var settings = SettingsUtil.TryGet();

            // Initialize Player2 health check if Player2 is selected as either voice or AI backend
            if (settings != null &&
                (settings.VoiceBackend?.SelectedValue == "Player2" ||
                 settings.AIBackend?.SelectedValue == "Player2"))
            {
                try
                {
                    // Initialize Player2 health check
                    Player2TextToSpeech.InitializeHealthCheck();
                    LogMessage("[DEBUG] Initialized Player2 health check.");
                    
                    // Use the new Player2 availability check method
                    var debugLogger = new DebugModLogger();
                    _ = Task.Run(async () => {
                        // Wait a brief moment to allow the game to fully load
                        await Task.Delay(2000);
                        
                        // Check Player2 availability and display user-friendly messages if there are issues
                        bool isAvailable = await debugLogger.CheckPlayer2Availability();
                        
                        if (isAvailable)
                        {
                            LogMessage("[INFO] Player2 availability check successful during startup");
                        }
                        else
                        {
                            LogMessage("[WARNING] Player2 availability check failed during startup. AI and/or voice features may not work.");
                            
                            // Log all settings for diagnostics
                            debugLogger.LogAllSettings();
                        }
                    });
                }
                catch (Exception ex)
                {
                    LogMessage($"[ERROR] Failed to initialize Player2 health check: {ex.Message}");
                    LogMessage("[WARNING] Player2 connectivity issues detected. Player2 must be downloaded and running for AI responses and/or voice synthesis.");
                    
                    // Display a more visible warning to the user
                    InformationManager.ShowInquiry(
                        new InquiryData(
                            "Player2 Connection Issue", 
                            "Player2 is selected as your AI backend or voice provider, but a connection couldn't be established.\n\n" +
                            "Please ensure Player2 is:\n" +
                            "1. Downloaded and installed from player2.game\n" +
                            "2. Running in the background\n" +
                            "3. Using the correct port (default: 4315)\n\n" +
                            "Without Player2 running, AI responses and/or voice synthesis will not work properly.",
                            true, false, "OK", null, null, null
                        )
                    );
                }
            }

			// Start UI screen debug watcher if enabled
			if (settings != null && settings.EnableUIScreenDebugLogs)
			{
				StartUiDebugWatcher();
			}
        }

		private void StartUiDebugWatcher()
		{
			try
			{
				_uiDebugWatcherCts?.Cancel();
				_uiDebugWatcherCts = new System.Threading.CancellationTokenSource();
				var token = _uiDebugWatcherCts.Token;
				_ = Task.Run(async () =>
				{
					while (!token.IsCancellationRequested)
					{
						try
						{
							string top = ScreenManager.TopScreen?.GetType().Name ?? "null";
							if (!string.Equals(top, _lastTopScreenName, StringComparison.Ordinal))
							{
								_lastTopScreenName = top;
								LogTopScreenInfo("[Watcher]");
							}
						}
						catch (Exception ex)
						{
							LogMessage($"[UI] Watcher error: {ex.Message}");
						}
						await Task.Delay(250, token);
					}
				}, token);
				LogMessage("[UI] Screen watcher started.");
			}
			catch (Exception ex)
			{
				LogMessage($"[UI] Failed to start screen watcher: {ex.Message}");
			}
		}

		public void LogTopScreenInfo(string tag = "")
		{
			try
			{
				var top = ScreenManager.TopScreen;
				string topName = top?.GetType().FullName ?? "null";
				LogMessage($"{tag} TopScreen={topName}");

				// Attempt to enumerate layers via reflection
				if (top != null)
				{
					var layersField = typeof(ScreenBase).GetField("_layers", BindingFlags.Instance | BindingFlags.NonPublic);
					var layers = layersField?.GetValue(top) as System.Collections.IEnumerable;
					if (layers != null)
					{
						int i = 0;
						foreach (var layer in layers)
						{
							string lname = layer?.GetType().FullName ?? "null";
							LogMessage($"{tag} Layer[{i}]={lname}");
							i++;
						}
					}
					else
					{
						LogMessage($"{tag} Could not reflect layers on TopScreen.");
					}
				}
			}
			catch (Exception ex)
			{
				LogMessage($"[UI] LogTopScreenInfo error: {ex.Message}");
			}
		}
		// Overlay UI for STT was removed in favor of using the conversation options directly.

        public void ClearAllNPCData()
        {
            try
            {
                LogMessage("[DEBUG] Attempting to clear all NPC data...");

                // Check if the main save data folder exists
                if (!Directory.Exists(_saveDataPath))
                {
                    LogMessage($"[WARNING] Save data folder does not exist: {_saveDataPath}. No files to delete.");
                    return;
                }

                // Get all game save folders inside "save_data"
                string[] saveDirectories = Directory.GetDirectories(_saveDataPath);

                if (saveDirectories.Length == 0)
                {
                    LogMessage("[WARNING] No game save folders found in save_data. Nothing to delete.");
                }
                else
                {
                    LogMessage($"[DEBUG] Found {saveDirectories.Length} game save folders. Deleting NPC files...");

                    foreach (string saveFolder in saveDirectories)
                    {
                        try
                        {
                            string[] npcFiles = Directory.GetFiles(saveFolder, "*.json");

                            if (npcFiles.Length == 0)
                            {
                                LogMessage($"[WARNING] No NPC save files found in {saveFolder}. Skipping...");
                                continue;
                            }

                            LogMessage($"[DEBUG] Found {npcFiles.Length} NPC save files in {saveFolder} to delete.");

                            foreach (string file in npcFiles)
                            {
                                try
                                {
                                    File.Delete(file);
                                    LogMessage($"[DEBUG] Deleted NPC save file: {file}");
                                }
                                catch (Exception fileEx)
                                {
                                    LogMessage($"[ERROR] Failed to delete file {file}: {fileEx.Message}");
                                }
                            }
                        }
                        catch (Exception folderEx)
                        {
                            LogMessage($"[ERROR] Failed to process save folder {saveFolder}: {folderEx.Message}");
                        }
                    }
                }

                // Clear in-memory NPC data
                _npcContexts.Clear();
                LogMessage("[DEBUG] Cleared in-memory NPC context.");

                InformationManager.DisplayMessage(new InformationMessage("All NPC context data across all saves has been cleared!"));
            }
            catch (Exception ex)
            {
                LogMessage($"[ERROR] Failed to clear NPC data: {ex.Message}");
                LogMessage($"[ERROR] StackTrace: {ex.StackTrace}");
            }
        }

        public override void SyncData(IDataStore dataStore)
        {

        }
        private string GetActiveSaveDirectory()
        {
            try
            {
                LogMessage("[DEBUG] Attempting to retrieve the active save directory...");

                // Generate a unique folder name for the current game save (e.g., based on Campaign ID)
                string saveFolderName = Campaign.Current.UniqueGameId.ToString();
                LogMessage($"[DEBUG] Current Campaign ID: {saveFolderName}");

                // Create the full path using PathHelper
                string saveFolderPath = Path.Combine(_saveDataPath, saveFolderName);
                LogMessage($"[DEBUG] Computed save folder path: {saveFolderPath}");

                // Ensure the save_data directory exists
                PathHelper.EnsureDirectoryExists(_saveDataPath);

                // Check if directory already exists
                if (!Directory.Exists(saveFolderPath))
                {
                    Directory.CreateDirectory(saveFolderPath);
                    LogMessage($"[DEBUG] Save directory did not exist. Created new directory: {saveFolderPath}");
                }
                else
                {
                    LogMessage($"[DEBUG] Save directory already exists: {saveFolderPath}");
                }

                _currentSaveFolder = saveFolderPath;
                return saveFolderPath;
            }
            catch (Exception ex)
            {
                LogMessage($"[ERROR] Failed to get active save directory: {ex.Message}");
                return _saveDataPath; // Default to the main save_data folder if error occurs
            }
        }

        private void SaveNPCContext(string npcId, Hero npc, NPCContext context)
        {
            try
            {
                LogMessage($"[DEBUG] Attempting to save NPC context for {npcId}...");

                if (string.IsNullOrEmpty(_currentSaveFolder))
                {
                    _currentSaveFolder = GetActiveSaveDirectory();
                    LogMessage($"[DEBUG] Retrieved active save directory: {_currentSaveFolder}");
                }

                // Convert NPC name to a safe filename format
                string safeNpcName = context.Name.Replace(" ", "_").Replace("/", "").Replace("\\", "").Replace("?", "");
                string npcFilePath = Path.Combine(_currentSaveFolder, $"{safeNpcName}.json");

                LogMessage($"[DEBUG] Computed save file path: {npcFilePath}");

                // Ensure Name is not null or "Unknown NPC"
                if (string.IsNullOrEmpty(context.Name) || context.Name == "Unknown NPC")
                {
                    string npcName = npc?.Name?.ToString() ?? "Unknown_NPC";
                    LogMessage($"[WARNING] NPC {npcId} had an invalid or missing name. Assigning: {npcName}.");
                    context.Name = npcName;
                }

                LogMessage($"[DEBUG] NPC {npcId} is being saved with name: {context.Name}");

                // Log the number of messages being saved
                LogMessage($"[DEBUG] NPC {npcId} message history count: {context.MessageHistory.Count}");

                // Create a fully populated context object for saving
                NPCContext fullContext = new NPCContext
                {
                    Name = context.Name,
                    MessageHistory = new List<string>(context.MessageHistory) 
                };

                // Convert to JSON and save
                string json = JsonConvert.SerializeObject(fullContext, Formatting.Indented);
                LogMessage($"[DEBUG] JSON Serialization successful. Writing to file...");

                File.WriteAllText(npcFilePath, json);
                LogMessage($"[DEBUG] Successfully saved NPC context to: {npcFilePath}");
                
                // Update the in-memory cache with the current timestamp
                if (_npcContexts.ContainsKey(npcId))
                {
                    _npcContexts[npcId] = (context, DateTime.Now);
                    LogMessage($"[DEBUG] Updated in-memory cache timestamp for {npcId}");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"[ERROR] Failed to save NPC data for {npcId}: {ex.Message}");
                LogMessage($"[ERROR] StackTrace: {ex.StackTrace}");
            }
        }

        private NPCContext LoadNPCContext(string npcId)
        {
            try
            {
                LogMessage($"[DEBUG] Attempting to load NPC context for ID: {npcId}");

                if (string.IsNullOrEmpty(_currentSaveFolder))
                {
                    _currentSaveFolder = GetActiveSaveDirectory();
                    LogMessage($"[DEBUG] Retrieved active save directory: {_currentSaveFolder}");
                }

                // Convert NPC name to a safe filename format
                string safeNpcName = Hero.AllAliveHeroes
                    .FirstOrDefault(h => h.StringId == npcId)?
                    .Name?.ToString()
                    .Replace(" ", "_").Replace("/", "").Replace("\\", "").Replace("?", "") ?? npcId;

                string npcFilePath = Path.Combine(_currentSaveFolder, $"{safeNpcName}.json");

                LogMessage($"[DEBUG] Checking if save file exists for {npcId} (Expected path: {npcFilePath})");

                if (!File.Exists(npcFilePath))
                {
                    LogMessage($"[WARNING] No save file found for NPC {npcId}. A new NPC context will be created.");
                    return new NPCContext { Name = "Unknown_NPC" };
                }

                LogMessage($"[DEBUG] Save file found for {npcId}. Attempting to read and deserialize...");

                string json = File.ReadAllText(npcFilePath);
                NPCContext loadedContext = JsonConvert.DeserializeObject<NPCContext>(json) ?? new NPCContext { Name = "Unknown_NPC" };

                LogMessage($"[DEBUG] Successfully deserialized NPC context for {npcId}.");

                // Check if the loaded context has a valid name
                if (string.IsNullOrEmpty(loadedContext.Name) || loadedContext.Name == "Unknown_NPC")
                {
                    string npcName = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == npcId)?.Name?.ToString() ?? "Unknown_NPC";
                    LogMessage($"[WARNING] NPC {npcId} had no name in save. Assigning real name: {npcName}.");
                    loadedContext.Name = npcName;
                }
                else
                {
                    LogMessage($"[DEBUG] Loaded NPC {npcId} has a valid name: {loadedContext.Name}");
                }

                return loadedContext;
            }
            catch (Exception ex)
            {
                LogMessage($"[ERROR] Failed to load NPC data for {npcId}: {ex.Message}");
                LogMessage($"[ERROR] StackTrace: {ex.StackTrace}");
            }

            // If no file was found or deserialization failed, return a new context
            return new NPCContext { Name = "Unknown_NPC" };
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
            LogMessage($"[DEBUG] Attempting to retrieve NPC context for {npc.Name} (ID: {npcId})");

            if (string.IsNullOrEmpty(_currentSaveFolder))
            {
                _currentSaveFolder = GetActiveSaveDirectory();
            }

            // Generate file path for this NPC
            string safeNpcName = npc.Name.ToString().Replace(" ", "_").Replace("/", "").Replace("\\", "").Replace("?", "");
            string npcFilePath = Path.Combine(_currentSaveFolder, $"{safeNpcName}.json");

            // Check if file exists and when it was last modified (to detect if it's been deleted since we cached it)
            bool fileExists = File.Exists(npcFilePath);
            DateTime fileModTime = fileExists ? File.GetLastWriteTime(npcFilePath) : DateTime.MinValue;

            // If NPC context already exists in memory, check if it's still valid
            if (_npcContexts.ContainsKey(npcId))
            {
                var (cachedContext, cacheTime) = _npcContexts[npcId];
                
                // If file no longer exists or has been modified since we cached it
                if (!fileExists || (fileExists && fileModTime > cacheTime))
                {
                    LogMessage($"[DEBUG] NPC {npc.Name} (ID: {npcId}) context is stale or file was deleted. Reloading from disk.");
                    // Don't return cached context - fall through to reload or create new
                }
                else
                {
                    LogMessage($"[DEBUG] NPC {npc.Name} (ID: {npcId}) found in memory and up to date. Returning existing context.");
                    return cachedContext;
                }
            }

            // Load or create context
            NPCContext loadedContext;
            if (fileExists)
            {
                LogMessage($"[DEBUG] Loading NPC context for {npc.Name} (ID: {npcId}) from file.");
                loadedContext = LoadNPCContext(npcId);
                
                // Check if deserialization failed (rare but possible)
                if (loadedContext == null)
                {
                    LogMessage($"[WARNING] Failed to deserialize context for {npc.Name}. Creating a new one.");
                    loadedContext = new NPCContext();
                }
            }
            else
            {
                LogMessage($"[WARNING] No save file found for {npc.Name} (ID: {npcId}). Creating a new NPC context.");
                loadedContext = new NPCContext();
            }

            // Ensure the name is set correctly
            if (string.IsNullOrEmpty(loadedContext.Name) || loadedContext.Name == "Unknown_NPC")
            {
                LogMessage($"[WARNING] NPC {npcId} had an invalid or missing name. Assigning real name: {npc.Name}.");
                loadedContext.Name = npc.Name.ToString();
            }

            // Store in memory with current timestamp
            _npcContexts[npcId] = (loadedContext, DateTime.Now);

            // Always refresh NPC stats
            LogMessage($"[DEBUG] Updating dynamic stats for NPC {npc.Name} (ID: {npcId}).");
            UpdateNPCStats(loadedContext, npc);

            return loadedContext;
        }

        private void UpdateNPCStats(NPCContext context, Hero npc)
        {
            // Store in both Dynamic and Static Stats
            context.AddDynamicStat("Title/Occupation", () => npc.Occupation.ToString());
            context.AddStaticStat("Title/Occupation", npc.Occupation.ToString());

            context.AddDynamicStat("Fief", () => GetHeroFiefsDisplay(npc));
            context.AddStaticStat("Fief", GetHeroFiefsDisplay(npc));

            context.AddDynamicStat("Relationship with player", () => npc.GetRelationWithPlayer().ToString());
            context.AddStaticStat("Relationship with player", npc.GetRelationWithPlayer().ToString());

            context.AddDynamicStat("Renown", () => npc.Clan?.Renown.ToString() ?? "Unknown");
            context.AddStaticStat("Renown", npc.Clan?.Renown.ToString() ?? "Unknown");

            context.AddDynamicStat("Personality", () => GetPersonalityDescription(npc));
            context.AddStaticStat("Personality", GetPersonalityDescription(npc));

            context.AddDynamicStat("Age", () => npc.Age.ToString("0"));
            context.AddStaticStat("Age", npc.Age.ToString("0"));

            context.AddDynamicStat("Gender", () => npc.IsFemale ? "Female" : "Male");
            context.AddStaticStat("Gender", npc.IsFemale ? "Female" : "Male");

            context.AddDynamicStat("Culture", () => npc.Culture?.Name?.ToString() ?? "Unknown");
            context.AddStaticStat("Culture", npc.Culture?.Name?.ToString() ?? "Unknown");

            context.AddDynamicStat("Kingdom", () => npc.Clan?.Kingdom?.Name?.ToString() ?? "No kingdom");
            context.AddStaticStat("Kingdom", npc.Clan?.Kingdom?.Name?.ToString() ?? "No kingdom");

            context.AddDynamicStat("Clan", () => npc.Clan?.Name?.ToString() ?? "No clan");
            context.AddStaticStat("Clan", npc.Clan?.Name?.ToString() ?? "No clan");

            context.AddDynamicStat("Clan Leader", () => npc.Clan?.Leader?.Name?.ToString() ?? "No leader");
            context.AddStaticStat("Clan Leader", npc.Clan?.Leader?.Name?.ToString() ?? "No leader");

            context.AddDynamicStat("Mother", () => npc.Mother?.Name?.ToString() ?? "Unknown");
            context.AddStaticStat("Mother", npc.Mother?.Name?.ToString() ?? "Unknown");

            context.AddDynamicStat("Father", () => npc.Father?.Name?.ToString() ?? "Unknown");
            context.AddStaticStat("Father", npc.Father?.Name?.ToString() ?? "Unknown");

            context.AddDynamicStat("Children", () => npc.Children.Any() ? string.Join(", ", npc.Children.Select(c => c.Name.ToString())) : "None");
            context.AddStaticStat("Children", npc.Children.Any() ? string.Join(", ", npc.Children.Select(c => c.Name.ToString())) : "None");

            context.AddDynamicStat("Spouse", () => npc.Spouse?.Name?.ToString() ?? "None");
            context.AddStaticStat("Spouse", npc.Spouse?.Name?.ToString() ?? "None");
        }

        private static List<Settlement> GetHeroFiefs(Hero hero)
        {
            var fiefs = new List<Settlement>();
            try
            {
                if (hero?.GovernorOf != null)
                {
                    var govSettlement = hero.GovernorOf.Owner?.Settlement ?? hero.GovernorOf.Settlement;
                    if (govSettlement != null)
                    {
                        fiefs.Add(govSettlement);
                    }
                }

                foreach (var s in Settlement.All)
                {
                    if ((s.IsTown || s.IsCastle) && s.Town?.Governor == hero && !fiefs.Contains(s))
                    {
                        fiefs.Add(s);
                    }
                }

                if (hero?.Clan?.Leader == hero)
                {
                    foreach (var s in Settlement.All)
                    {
                        if ((s.IsTown || s.IsCastle) && s.OwnerClan == hero.Clan && !fiefs.Contains(s))
                        {
                            fiefs.Add(s);
                        }
                    }
                }
            }
            catch { }
            return fiefs;
        }

        private static string GetHeroFiefsDisplay(Hero hero)
        {
            var fiefs = GetHeroFiefs(hero);
            if (fiefs.Count == 0) return "No fief";
            var names = fiefs.Select(s => s?.Name?.ToString()).Where(n => !string.IsNullOrWhiteSpace(n));
            var joined = string.Join(", ", names);
            return string.IsNullOrWhiteSpace(joined) ? "No fief" : joined;
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

			// If the player is on the world map, attempt to determine nearest settlement using reflection (API-safe)
			if (Hero.MainHero.PartyBelongedTo != null)
			{
				double px, py;
				if (TryGet2DPosition(Hero.MainHero.PartyBelongedTo, out px, out py))
				{
					Settlement nearestSettlement = null;
					double bestDist2 = double.MaxValue;
					foreach (var s in Settlement.All)
					{
						double sx, sy;
						if (TryGet2DPosition(s, out sx, out sy))
						{
							double dx = sx - px;
							double dy = sy - py;
							double d2 = dx * dx + dy * dy;
							if (d2 < bestDist2)
							{
								bestDist2 = d2;
								nearestSettlement = s;
							}
						}
					}
					if (nearestSettlement != null)
					{
						string settlementType = GetSettlementType(nearestSettlement);
						string kingdom = nearestSettlement.OwnerClan?.Kingdom?.Name?.ToString() ?? "no kingdom";
						return $"on the road near the {settlementType} of {nearestSettlement.Name}, part of the kingdom of {kingdom}";
					}
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

		private static bool TryGet2DPosition(object instance, out double x, out double y)
		{
			x = 0; y = 0;
			if (instance == null) return false;
			try
			{
				var t = instance.GetType();
				object? posVal = null;

				// 1) Prefer properties (newer builds tend to expose Position2D as a property)
				var posProp = t.GetProperty("Position2D", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
					?? t.GetProperty("Position", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
					?? t.GetProperty("MapPosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (posProp != null)
				{
					posVal = posProp.GetValue(instance);
				}
				else
				{
					// 2) Fallback to fields (some older builds exposed Position2D as a field)
					var posField = t.GetField("Position2D", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
						?? t.GetField("Position", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
						?? t.GetField("MapPosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					if (posField != null)
					{
						posVal = posField.GetValue(instance);
					}
					else
					{
						// 3) Fallback to methods (defensive: some versions may have GetPosition2D())
						var posMethod = t.GetMethod("GetPosition2D", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null)
							?? t.GetMethod("GetPosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null)
							?? t.GetMethod("GetMapPosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
						if (posMethod != null)
						{
							posVal = posMethod.Invoke(instance, null);
						}
					}
				}

				return TryExtractXY(posVal, out x, out y);
			}
			catch { return false; }
		}

		private static bool TryExtractXY(object? vectorObj, out double x, out double y)
		{
			x = 0; y = 0;
			if (vectorObj == null) return false;
			try
			{
				var vt = vectorObj.GetType();

				// Common variants across Bannerlord builds / TaleWorlds structs:
				// - Properties: X/Y or x/y
				// - Fields: x/y or X/Y
				// - Sometimes a 3D vector is used (X/Z or x/z); treat Z as Y for map coords if Y is missing.

				static bool TryGetNumber(Type type, object obj, string name, out double val)
				{
					val = 0;
					try
					{
						var p = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
						if (p != null)
						{
							object? pv = p.GetValue(obj);
							if (pv != null)
							{
								val = Convert.ToDouble(pv);
								return true;
							}
						}

						var f = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
						if (f != null)
						{
							object? fv = f.GetValue(obj);
							if (fv != null)
							{
								val = Convert.ToDouble(fv);
								return true;
							}
						}
					}
					catch { }
					return false;
				}

				bool hasX = TryGetNumber(vt, vectorObj, "X", out double x1) || TryGetNumber(vt, vectorObj, "x", out x1);
				bool hasY = TryGetNumber(vt, vectorObj, "Y", out double y1) || TryGetNumber(vt, vectorObj, "y", out y1);
				bool hasZ = TryGetNumber(vt, vectorObj, "Z", out double z1) || TryGetNumber(vt, vectorObj, "z", out z1);

				// Standard 2D
				if (hasX && hasY)
				{
					x = x1;
					y = y1;
					return true;
				}

				// 3D vector where map plane might be X/Z
				if (hasX && hasZ)
				{
					x = x1;
					y = z1;
					return true;
				}

				// Last-ditch: if we only got Y/Z, treat Y as X and Z as Y
				if (hasY && hasZ)
				{
					x = y1;
					y = z1;
					return true;
				}
			}
			catch { }
			return false;
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
                prompt += $"\n{stat.Key}: {stat.Value}\n\n";
            }

            // Retrieve and include relationship change feedback
            int lastRelationshipChange = RelationshipTracker.GetRelationshipChange(npc);
            if (lastRelationshipChange != 0)
            {
                string relationshipFeedback = lastRelationshipChange > 0
                    ? $"You liked the player's last message, and your relationship with them has improved by {lastRelationshipChange}."
                    : $"You did not like the player's last message, and your relationship with them has worsened by {Math.Abs(lastRelationshipChange)}.";

                prompt += $"\n\n{relationshipFeedback}\n";
            }

            // fetch the quest details toggled true for on and false for off
            bool questDetailsEnabled = ChatAiSettings.Instance.ToggleQuestInfo;

            if (questDetailsEnabled)
            {
                LogMessage($"DEBUG: Quest details are enabled.");
                // Add quest details
                var questManager = new QuestManager();
                string questDetails = questManager.GetQuestDetailsForPrompt(npc);
                if (!string.IsNullOrWhiteSpace(questDetails))
                {
                    prompt += $"\nHere are your quest details and script examples for offering it: {questDetails}\n";
                }

                // Check for quest conditions and add failure reason if applicable
                var escortHandler = new EscortMerchantCaravanHandler();
                var quests = questManager.GetQuestsForNPC(npc);
            }

            if (ChatAiSettings.Instance.ToggleWorldEvents)
            {
                LogMessage($"DEBUG: World events are enabled.");
                List<string> recentEvents = WorldEventListener.GetEventsForNPC(npc);
                WorldEventTracker.LogMessage($"[DEBUG] Events for {npc.Name}: {string.Join(", ", recentEvents)}");

                if (recentEvents.Any())
                {
                    prompt += "\n\nRecent world events affecting you:\n";
                    foreach (string eventDescription in recentEvents)
                    {
                        WorldEventTracker.LogMessage($"[DEBUG] Adding event to {npc.Name}'s prompt: {eventDescription}");
                        prompt += $"- {eventDescription}\n";
                    }
                }
            }

            // Inject compact world-state prompt hints (e.g., prisoner status)
            string worldStateHints = WorldPromptHints.GetHintsForNPC(npc);
            if (!string.IsNullOrWhiteSpace(worldStateHints))
            {
                prompt += $"\n\nAdditional situational context:\n{worldStateHints}\n";
            }

            prompt += "\n\nRecent conversation history:\n";
            prompt += context.GetFormattedHistory();

            // fetch the custom prompt from settings
            string customPrompt = ChatAiSettings.Instance.CustomPrompt;
            
            if (!string.IsNullOrWhiteSpace(customPrompt))
            {
                LogMessage($"\n\nDEBUG: Custom prompt from settings: {customPrompt}");
                prompt += $"\n\nExtra Instructions or Context:{customPrompt}";
            }

            // Instructions for response style
            prompt += "\n\nInstructions:\n";
            prompt += "- Answer the player's latest question or comment, in character and with the correct personality.\n";
            prompt += "- Do not reference the AI, modern concepts, or anything outside this world.\n";
            prompt += "- Use the information provided to craft a response that fits the medieval fantasy setting. \n";
            prompt += "- Act in a way that fits your character's personality and traits.\n";

            // Equipment info (optional)
            if (ChatAiSettings.Instance.ToggleEquipmentInfo)
            {
                try
                {
                    string npcEquip = OutfitSummaryBuilder.BuildOutfitSummary(npc);
                    string playerEquip = OutfitSummaryBuilder.BuildOutfitSummary(Hero.MainHero);
                    prompt += $"\nYour Equipment: {npcEquip}\n";
                    prompt += $"The Player's Equipment: {playerEquip}\n";
                }
                catch { }
            }
            if (longerResponses)
            {
                prompt += "- Provide a long 2-3 paragraph, detailed and immersive response, that responds to the player.\n";
            }
            else
            {
                prompt += "- Keep responses concise and immersive. Avoid overly verbose replies unless directly asked for details.\n";
            }

            if (questDetailsEnabled)
            {
                prompt += "- If you offer any quest, try to convince the player to accept it, while also making sure to respond to the latest response by the player. If you don't have a current quest don't mention anything about quests.\n";
            }

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
                    "Sure, let's chat.",
                    null,
                    null
                );

                // Player's Input Option
                SafeAddPlayerLine(
                    starter,
                    "chat_with_me_input",
                    "chat_with_me_input",
                    "chat_with_me_processing",
					"Chat with text",
                    null,
                    async () => { await HandlePlayerInput(); }
                );

                // Player's Speech Input Option (uses Player2 STT with timeout)
                SafeAddPlayerLine(
                    starter,
                    "chat_with_me_input_speech",
                    "chat_with_me_input",
                    "chat_with_me_processing",
                    "Speak using microphone",
                    () =>
                    {
                        try
                        {
                            return ChatAiSettings.Instance.STTBackend?.SelectedValue == "Player2";
                        }
                        catch { return false; }
                    },
                    async () =>
                    {
                        await HandlePlayerSpeechInput();
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
                        {
                            LogMessage("Azure TTS selected. Playing deferred audio.");
                            AzureTextToSpeech.PlayDeferredAudio(); // Use static method
                        }
                        else if (ChatAiSettings.Instance.VoiceBackend?.SelectedValue == "Player2")
                        {
                            LogMessage("Player2 TTS selected. Playing stored text.");
                            OnContinueClicked();
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
                    () => { if (ChatAiSettings.Instance.VoiceBackend?.SelectedValue == "Azure") { LogMessage("Player ended the conversation. Cancelling playback..."); AzureTextToSpeech.CancelPlayback(); } }
                );
                // Placeholder End State
                SafeAddDialogLine(
                    starter,
                    "end_conversation",
                    "end_conversation",
                    "hero_main_options",
                    "Farewell. May your journeys be safe and prosperous.",
                    () => { return true; },
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

            var settings = ChatAiSettings.Instance;
            try
            {
                string ttsText = settings.SkipStageDirectionsInTTS ? SanitizeForTTS(text) : (text ?? string.Empty);
                if (settings.VoiceBackend?.SelectedValue == "Player2")
                {
                    LogMessage("[DEBUG] Using Player2 for text-to-speech");
                    
                    try
                    {
                        // Store the text for later playback
                        Player2TextToSpeech.StoreTextForPlayback(ttsText, npc.IsFemale);
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"[ERROR] Player2 TTS failed: {ex.Message}");
                        LogMessage("[ERROR] Please ensure Player2 is downloaded from player2.game, properly installed, and running in the background.");
                                                 InformationManager.DisplayMessage(new InformationMessage("Could not generate speech with Player2. Please ensure Player2 is downloaded from player2.game and running."));
                    }
                }
                else if (settings.VoiceBackend?.SelectedValue == "Azure")
                {
                    var tts = new AzureTextToSpeech();
                    await tts.GenerateSpeech(ttsText, npc.IsFemale);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"[ERROR] Failed to generate speech: {ex.Message}");
            }
        }

        private string SanitizeForTTS(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return input ?? string.Empty;
            }

            // Remove content inside asterisk pairs, e.g., *nods* or *waves at you*
            string withoutStageDirections = Regex.Replace(input, "\\*[^*]*\\*", string.Empty);

            // Collapse multiple whitespace into a single space and trim
            withoutStageDirections = Regex.Replace(withoutStageDirections, "\\s{2,}", " ").Trim();

            return withoutStageDirections;
        }

        // Add this method to handle the continue click
        public static void OnContinueClicked()
        {
            var settings = ChatAiSettings.Instance;
            if (settings.VoiceBackend?.SelectedValue == "Player2")
            {
                try
                {
                    Player2TextToSpeech.PlayStoredText();
                }
                catch (Exception ex)
                {
                    // Static method cannot access instance LogMessage, so we use InformationManager directly
                    InformationManager.DisplayMessage(new InformationMessage("Failed to play speech with Player2. Please ensure Player2 is downloaded from player2.game and running."));
                    
                    // Try to write to a log file directly (simplified version)
                    try 
                    {
                        string logPath = PathHelper.GetModFilePath("mod_log.txt");
                        File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - [ERROR] Player2 speech playback failed: {ex.Message}\n");
                        File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - [ERROR] Please ensure Player2 is downloaded from player2.game, properly installed, and running in the background.\n");
                    }
                    catch 
                    {
                        // Silently fail if we can't write to the log
                    }
                }
            }
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

			await ProcessPlayerMessage(userInput);
		}

        // Microphone-driven input using Player2 STT and the configured timeout
        private async Task HandlePlayerSpeechInput()
        {
            try
            {
                LogMessage("[STT] Starting Player2 STT flow...");
                if (ChatAiSettings.Instance.STTBackend?.SelectedValue != "Player2")
                {
                    InformationManager.DisplayMessage(new InformationMessage("Player2 STT is not selected."));
                    LogMessage("[STT] Aborted: STT backend is not Player2.");
                    return;
                }

                var stt = new Player2SpeechToText();

                string lang = ChatAiSettings.Instance.STTLanguageCode;
                if (!string.IsNullOrWhiteSpace(lang))
                {
                    LogMessage($"[STT] Setting language: {lang}");
                    _ = stt.SetLanguageAsync(lang);
                }

                int timeout = ChatAiSettings.Instance.STTTimeoutSeconds;
                LogMessage($"[STT] Requesting start with timeout={timeout}s");
                bool started = await stt.StartAsync(timeout);
                if (!started)
                {
                    LogMessage("[STT] Start failed.");
                    InformationManager.DisplayMessage(new InformationMessage("Could not start speech recognition."));
                    return;
                }

                InformationManager.DisplayMessage(new InformationMessage($"Listening... (up to {timeout}s)"));

                // Stop slightly BEFORE timeout to avoid Player2 discarding input
                int stopDelayMs = Math.Max(1, timeout - 1) * 1000;
                try { await Task.Delay(stopDelayMs); } catch { }

                LogMessage("[STT] Stopping recognition...");
				string recognized = await stt.StopAsync();
				InformationManager.DisplayMessage(new InformationMessage("Stopped listening."));

                if (string.IsNullOrWhiteSpace(recognized))
                {
                    LogMessage("[STT] No speech recognized.");
                    InformationManager.DisplayMessage(new InformationMessage("No speech recognized."));
                    MBTextManager.SetTextVariable("DYNAMIC_NPC_RESPONSE", "I did not hear anything.");
                    return;
                }

                LogMessage($"[STT] Recognized: {recognized}");
                await ProcessPlayerMessage(recognized);
            }
            catch (Exception ex)
            {
                LogMessage($"[STT] Error during speech input: {ex.Message}");
                InformationManager.DisplayMessage(new InformationMessage("Speech input failed."));
            }
        }

		private async Task ProcessPlayerMessage(string userInput)
		{
			try
			{
				LogMessage($"[UI] Processing player message: {userInput}");
			}
			catch { }

            Hero npc = Hero.OneToOneConversationHero;
            if (npc == null)
            {
                LogMessage("Error: No NPC found for the conversation.");
                return;
            }

            NPCContext context = GetOrCreateNPCContext(npc);

            string npcLastMessage = context.GetLatestNPCMessage(); // Fetch NPC's last message before user input

            context.AddMessage($"User: {userInput}");
            UpdateNPCStats(context, npc);
            SaveNPCContext(npc.StringId, npc, context); 

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

            // fetch if ai driven actions are enabled
            bool aiDrivenActions = ChatAiSettings.Instance.ToggleAIActions;

            AIActionEvaluator.Action action = AIActionEvaluator.Action.None;
            Settlement targetSettlement = null;
            string confirmationMessage = null;
            int hiringCost = 0;

            if (aiDrivenActions)
            {
                var result = await _actionEvaluator.EvaluateActionWithTargetAndMessageAndCost(npc, userInput);
                action = result.Item1;
                targetSettlement = result.Item2;
                confirmationMessage = result.Item3;
                hiringCost = result.Item4;

                LogMessage($"DEBUG: Handling action {action.ToString()} for NPC {npc.Name} with target {targetSettlement?.Name?.ToString() ?? "null"} and cost {hiringCost}.");
            }
            else
            {
                LogMessage("AI-driven actions are disabled in settings. Skipping action evaluation.");
            }

            // Handle Action
            if (action != AIActionEvaluator.Action.None && 
                action != AIActionEvaluator.Action.OfferToJoinParty && 
                (action != AIActionEvaluator.Action.GoToSettlement || targetSettlement != null))
            {
                HandleAction(npc, action, targetSettlement, hiringCost);
            }
            else if (confirmationMessage != null)
            {
                LogMessage($"DEBUG: Confirmation message: {confirmationMessage}");
            }

            // Special handling for OfferToJoinParty - we want this to flow through to the AI response
            string extraPromptInfo = "";
            if (action == AIActionEvaluator.Action.OfferToJoinParty)
            {
                // Store the hire cost in the NPC context for later retrieval
                context.AddStaticStat("HireCost", hiringCost.ToString());
                SaveNPCContext(npc.StringId, npc, context);
                
                extraPromptInfo = $"Offer to join the player's party for {hiringCost} denars. Make sure to mention this specific amount.";
                LogMessage($"DEBUG: Added hire cost of {hiringCost} to {npc.Name}'s context and prompt.");
            }

            // Prompt Generation
            string prompt = GeneratePrompt(npc, extraPromptInfo.Length > 0 ? extraPromptInfo : confirmationMessage);
            string response = "";
            
            try 
            {
                // Check if using Player2 as AI backend
                if (ChatAiSettings.Instance.AIBackend?.SelectedValue == "Player2")
                {
                    LogMessage("[DEBUG] Using Player2 as AI backend for response generation");
                }
                
                response = await AIHelper.GetResponse(prompt);
                
                if (string.IsNullOrWhiteSpace(response))
                {
                    if (ChatAiSettings.Instance.AIBackend?.SelectedValue == "Player2")
                    {
                        LogMessage("[ERROR] Empty response received from Player2. Please ensure Player2 is downloaded from player2.game and running in the background.");
                        InformationManager.DisplayMessage(new InformationMessage("No response from Player2. Make sure Player2 is downloaded from player2.game and running in the background."));
                        response = "I apologize, but I cannot respond at the moment. There may be an issue with the connection to Player2. Please ensure Player2 is downloaded from player2.game and running.";
                    }
                    else 
                    {
                        response = "Sorry, I couldn't understand that.";
                        LogMessage("[ERROR] Empty AI response. Default used.");
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"[ERROR] Failed to get AI response: {ex.Message}");
                
                if (ChatAiSettings.Instance.AIBackend?.SelectedValue == "Player2")
                {
                    LogMessage("[ERROR] Player2 error detected. Ensure Player2 is downloaded from player2.game, properly installed, and running in the background.");
                    InformationManager.DisplayMessage(new InformationMessage("Error connecting to Player2. Please make sure Player2 is downloaded from player2.game and running."));
                    response = "I apologize, but there seems to be a technical issue with Player2. Please ensure Player2 is downloaded from player2.game and running in the background.";
                }
                else
                {
                    response = "Sorry, I'm having trouble responding right now.";
                }
            }

            // Add the AI's response to the conversation history
            context.AddMessage($"NPC: {response}");

            SaveNPCContext(npc.StringId, npc, context);

            MBTextManager.SetTextVariable("DYNAMIC_NPC_RESPONSE", response);

            // Call Azure TTS to synthesize the response
            GenerateNPCSpeech(npc, response);

            // check if quest information is toggled on
            bool questInfoEnabled = ChatAiSettings.Instance.ToggleQuestInfo;

            if (questInfoEnabled)
            {
                LogMessage("DEBUG: Quest information is enabled. Analyzing quest acceptance...");

            


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
            }

            // wait 1 second before displaying the I am ready to respond now message to make sure azure tts has finished
            await Task.Delay(1000);


            InformationManager.DisplayMessage(new InformationMessage("I am ready to respond now!"));
            
        }

        private void HandleAction(Hero npc, AIActionEvaluator.Action action, Settlement targetSettlement = null, int hiringCost = 0)
        {
            // fetch if ai driven actions are enabled
            bool aiDrivenActions = ChatAiSettings.Instance.ToggleAIActions;

            if (!aiDrivenActions)
            {
                LogMessage("AI-driven actions are disabled in settings. Skipping action execution.");
                return;
            }

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

                case AIActionEvaluator.Action.JoinParty:
                    LogMessage($"DEBUG: Processing JoinParty action for NPC {npc.Name}.");
                    bool success = behaviorLogic.JoinPlayerParty(npc);
                    if (success)
                    {
                        LogMessage($"DEBUG: NPC {npc.Name} successfully joined player's party.");
                        InformationManager.DisplayMessage(new InformationMessage($"{npc.Name} has agreed to join your party!"));
                    }
                    else
                    {
                        LogMessage($"DEBUG: NPC {npc.Name} failed to join player's party.");
                        InformationManager.DisplayMessage(new InformationMessage($"{npc.Name} is unable to join your party at this time."));
                    }
                    break;
                    
                case AIActionEvaluator.Action.AcceptJoinOffer:
                    LogMessage($"DEBUG: Processing AcceptJoinOffer action for NPC {npc.Name} with cost {hiringCost}.");
                    if (hiringCost > 0)
                    {
                        // First check if player can afford
                        if (!behaviorLogic.CanPlayerAffordHiring(hiringCost))
                        {
                            LogMessage($"DEBUG: Player cannot afford hire cost of {hiringCost}. Current gold: {Hero.MainHero.Gold}");
                            InformationManager.DisplayMessage(new InformationMessage($"You don't have enough money to hire {npc.Name}. You need {hiringCost} denars."));
                            return;
                        }
                        
                        // Try to join with the cost
                        bool joinSuccess = behaviorLogic.JoinPlayerParty(npc, hiringCost);
                        if (joinSuccess)
                        {
                            LogMessage($"DEBUG: NPC {npc.Name} successfully joined player's party for {hiringCost} denars.");
                            InformationManager.DisplayMessage(new InformationMessage($"You paid {hiringCost} denars, and {npc.Name} has joined your party!"));
                        }
                        else
                        {
                            LogMessage($"DEBUG: NPC {npc.Name} failed to join player's party after payment.");
                            InformationManager.DisplayMessage(new InformationMessage($"Despite the payment, {npc.Name} was unable to join your party."));
                        }
                    }
                    else
                    {
                        LogMessage($"DEBUG: Invalid hiring cost of {hiringCost} for {npc.Name}.");
                        InformationManager.DisplayMessage(new InformationMessage($"There seems to be an issue with the hiring arrangement."));
                    }
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
                // Settings may not be initialized yet on older Bannerlord versions.
                if (!SettingsUtil.IsDebugLoggingEnabled())
                {
                    return; // Skip logging if disabled
                }

                string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}\n";

                // Use PathHelper to get the correct log file path
                string logFilePath = _logFilePath;
                string logDirectory = Path.GetDirectoryName(logFilePath);

                // Ensure the log directory exists
                if (!string.IsNullOrEmpty(logDirectory))
                {
                    PathHelper.EnsureDirectoryExists(logDirectory);
                }

                // Write the log message to the file
                File.AppendAllText(logFilePath, logMessage);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"Logging error: {ex.Message}"));
            }
        }

        // Add a method for AIActionEvaluator to access NPCContext
        public NPCContext GetOrCreateNPCContextForAnalysis(Hero npc)
        {
            if (npc == null) return null;
            return GetOrCreateNPCContext(npc);
        }

    }
}

