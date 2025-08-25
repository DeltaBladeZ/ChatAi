using System;
using System.IO;
using TaleWorlds.Library;

namespace ChatAi
{
    public static class PathHelper
    {
        /// <summary>
        /// Gets the correct mod folder path that works for both manual/Nexus installations and Steam Workshop
        /// </summary>
        /// <returns>The path to the mod folder</returns>
        public static string GetModFolderPath()
        {
            // Try the standard path first (manual/Nexus installations)
            string standardPath = Path.Combine(BasePath.Name, "Modules", "ChatAi");
            if (Directory.Exists(standardPath))
            {
                return standardPath;
            }

            // Try Steam Workshop path
            string steamPath = GetSteamWorkshopPath();
            if (!string.IsNullOrEmpty(steamPath) && Directory.Exists(steamPath))
            {
                return steamPath;
            }

            // Fallback to standard path even if it doesn't exist (will be created)
            return standardPath;
        }

        /// <summary>
        /// Gets the Steam Workshop path for the mod
        /// </summary>
        /// <returns>The Steam Workshop path or null if not found</returns>
        private static string GetSteamWorkshopPath()
        {
            try
            {
                // Steam Workshop path structure:
                // C:\Program Files (x86)\Steam\steamapps\workshop\content\261550\[workshop_id]\
                string steamAppsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "Steam", "steamapps", "workshop", "content", "261550");

                if (!Directory.Exists(steamAppsPath))
                {
                    return null;
                }

                // Look for the ChatAi mod in workshop folders
                string[] workshopFolders = Directory.GetDirectories(steamAppsPath);
                foreach (string folder in workshopFolders)
                {
                    string subModulePath = Path.Combine(folder, "_Module", "SubModule.xml");
                    if (File.Exists(subModulePath))
                    {
                        try
                        {
                            string content = File.ReadAllText(subModulePath);
                            if (content.Contains("<Id value=\"ChatAi\" />"))
                            {
                                return folder;
                            }
                        }
                        catch
                        {
                            // Continue searching if we can't read this file
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the error but don't throw
                InformationManager.DisplayMessage(new InformationMessage($"Error finding Steam Workshop path: {ex.Message}"));
            }

            return null;
        }

        /// <summary>
        /// Gets the path for a file within the mod folder
        /// </summary>
        /// <param name="fileName">The file name</param>
        /// <returns>The full path to the file</returns>
        public static string GetModFilePath(string fileName)
        {
            return Path.Combine(GetModFolderPath(), fileName);
        }

        /// <summary>
        /// Gets the path for a subfolder within the mod folder
        /// </summary>
        /// <param name="folderName">The folder name</param>
        /// <returns>The full path to the folder</returns>
        public static string GetModFolderPath(string folderName)
        {
            return Path.Combine(GetModFolderPath(), folderName);
        }

        /// <summary>
        /// Ensures a directory exists, creating it if necessary
        /// </summary>
        /// <param name="path">The directory path</param>
        public static void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
    }
} 