using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Playnite.SDK;

namespace PlayniteCloudSync
{
    public struct AchievementCounts
    {
        public int Unlocked;
        public int Total;
    }

    // Reads achievement counts from the PlayniteAchievements plugin's own SQLite cache
    // (https://github.com/justin-delano/PlayniteAchievements), read-only. That plugin owns and
    // refreshes this data on its own schedule - we just piggyback on it between its refreshes,
    // so this must never throw: if it isn't installed, its schema changes underneath us, or the
    // file is momentarily locked, we skip achievements for this sync rather than failing the
    // whole push.
    public static class AchievementsReader
    {
        // PlayniteAchievements' own plugin GUID (source/PlayniteAchievementsPlugin.cs) - its
        // ExtensionsData folder is named after this, same as ours is named after our own Id.
        private const string PlayniteAchievementsPluginId = "e6aad2c9-6e06-4d8d-ac55-ac3b252b5f7b";

        private static readonly ILogger logger = LogManager.GetLogger();

        // Installed = Playnite knows about the addon at all (its files are on disk), regardless
        // of enabled/disabled state. Used to decide whether to even offer the "include
        // achievements" setting, or point the user at installing it instead.
        public static bool IsSupportedPluginInstalled(IPlayniteAPI playniteApi)
        {
            var addons = playniteApi?.Addons?.Addons;
            return addons != null && addons.Contains(PlayniteAchievementsPluginId, StringComparer.OrdinalIgnoreCase);
        }

        public static bool IsSupportedPluginEnabled(IPlayniteAPI playniteApi)
        {
            if (!IsSupportedPluginInstalled(playniteApi))
            {
                return false;
            }

            var disabled = playniteApi?.Addons?.DisabledAddons;
            return disabled == null || !disabled.Contains(PlayniteAchievementsPluginId, StringComparer.OrdinalIgnoreCase);
        }

        public static Dictionary<Guid, AchievementCounts> ReadCurrentUserCounts(IPlayniteAPI playniteApi)
        {
            var result = new Dictionary<Guid, AchievementCounts>();

            try
            {
                var dbPath = Path.Combine(
                    playniteApi.Paths.ExtensionsDataPath,
                    PlayniteAchievementsPluginId,
                    "achievement_cache.db");

                if (!File.Exists(dbPath))
                {
                    // Plugin not installed, or hasn't run yet - not an error.
                    return result;
                }

                var connectionString = new SqliteConnectionStringBuilder
                {
                    DataSource = dbPath,
                    Mode = SqliteOpenMode.ReadOnly
                }.ToString();

                using (var connection = new SqliteConnection(connectionString))
                {
                    connection.Open();

                    using (var command = connection.CreateCommand())
                    {
                        // IsCurrentUser=1 excludes friends' progress (the plugin also caches
                        // that, for its friends-comparison feature) - we only want the local
                        // player's own counts.
                        command.CommandText = @"
                            SELECT g.PlayniteGameId, ugp.AchievementsUnlocked, ugp.TotalAchievements
                            FROM UserGameProgress ugp
                            JOIN Games g ON g.Id = ugp.GameId
                            JOIN Users u ON u.Id = ugp.UserId
                            WHERE u.IsCurrentUser = 1 AND ugp.HasAchievements = 1";

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                if (reader.IsDBNull(0) || !Guid.TryParse(reader.GetString(0), out var gameId))
                                {
                                    continue;
                                }

                                result[gameId] = new AchievementCounts
                                {
                                    Unlocked = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetInt64(1)),
                                    Total = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetInt64(2))
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Syncnite: couldn't read PlayniteAchievements cache, skipping achievements for this sync.");
                result.Clear();
            }

            return result;
        }
    }
}
