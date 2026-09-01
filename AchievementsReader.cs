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
        // Two different identifiers, easy to conflate but not interchangeable - verified live
        // against a real install with PlayniteAchievements 3.0.0. Addons.Addons/DisabledAddons
        // are keyed by the addon's *catalog manifest* Id (extension.yaml's `Id:` field), which
        // for anything distributed through Playnite's official Add-ons browser is a
        // human-readable slug, not a GUID - PlayniteAchievements' own extension.yaml declares
        // `Id: PlayniteAchievements`. The GUID below is a separate thing: the plugin class's own
        // compiled Guid Id (every Plugin subclass has one, ours included), which is what Playnite
        // actually names the per-addon ExtensionsData folder after - confirmed by finding
        // achievement_cache.db live under ExtensionsData\e6aad2c9-.... Using the GUID for the
        // installed/enabled check (the original bug here) meant it never matched the manifest Id
        // Addons.Addons actually contains, so the "include achievements" setting stayed grayed
        // out even with the addon genuinely installed and enabled.
        private const string PlayniteAchievementsManifestId = "PlayniteAchievements";
        private const string PlayniteAchievementsDataFolderId = "e6aad2c9-6e06-4d8d-ac55-ac3b252b5f7b";

        private static readonly ILogger logger = LogManager.GetLogger();

        // Installed = Playnite knows about the addon at all (its files are on disk), regardless
        // of enabled/disabled state. Used to decide whether to even offer the "include
        // achievements" setting, or point the user at installing it instead.
        public static bool IsSupportedPluginInstalled(IPlayniteAPI playniteApi)
        {
            var addons = playniteApi?.Addons?.Addons;
            return addons != null && addons.Contains(PlayniteAchievementsManifestId, StringComparer.OrdinalIgnoreCase);
        }

        public static bool IsSupportedPluginEnabled(IPlayniteAPI playniteApi)
        {
            if (!IsSupportedPluginInstalled(playniteApi))
            {
                return false;
            }

            var disabled = playniteApi?.Addons?.DisabledAddons;
            return disabled == null || !disabled.Contains(PlayniteAchievementsManifestId, StringComparer.OrdinalIgnoreCase);
        }

        public static Dictionary<Guid, AchievementCounts> ReadCurrentUserCounts(IPlayniteAPI playniteApi)
        {
            var result = new Dictionary<Guid, AchievementCounts>();

            try
            {
                var dbPath = Path.Combine(
                    playniteApi.Paths.ExtensionsDataPath,
                    PlayniteAchievementsDataFolderId,
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
