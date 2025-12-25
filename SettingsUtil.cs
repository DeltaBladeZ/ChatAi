using System;
using MCM.Abstractions.Base.Global;

namespace ChatAi
{
    /// <summary>
    /// Centralized, fail-safe settings access. Older Bannerlord versions can initialize MCM/settings later,
    /// so direct ChatAiSettings.Instance access may be null during early campaign boot.
    /// </summary>
    internal static class SettingsUtil
    {
        public static ChatAiSettings? TryGet()
        {
            try
            {
                // Prefer MCM GlobalSettings when available, but fall back to the generated Instance.
                return GlobalSettings<ChatAiSettings>.Instance ?? ChatAiSettings.Instance;
            }
            catch
            {
                return null;
            }
        }

        public static bool IsDebugLoggingEnabled()
        {
            try
            {
                return TryGet()?.EnableDebugLogging == true;
            }
            catch
            {
                return false;
            }
        }
    }
}


