using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace PlayniteCloudSync
{
    public class PlayniteCloudSyncPlugin : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private static readonly TimeSpan AutoSyncCheckInterval = TimeSpan.FromMinutes(1);

        // Reported to the web app on every push so it can flag an out-of-date install (see
        // Settings' "Connected devices" and the notification bell). Keep this in sync by hand
        // with extension.yaml's own Version field and web/src/lib/versions.ts's
        // LATEST_PLUGIN_VERSION whenever this is bumped - Playnite's plugin loader doesn't
        // expose a way to read a plugin's own manifest version back from inside itself.
        public const string PluginVersion = "0.3.2";

        private readonly CloudSyncSettingsViewModel settingsViewModel;
        private Timer autoSyncTimer;

        public override Guid Id { get; } = Guid.Parse("f42cf930-4203-49b8-bf09-62ff06ecb92e");

        public PlayniteCloudSyncPlugin(IPlayniteAPI api) : base(api)
        {
            Properties = new GenericPluginProperties { HasSettings = true };
            settingsViewModel = new CloudSyncSettingsViewModel(this);
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settingsViewModel;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new CloudSyncSettingsView(this, settingsViewModel);
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            yield return new MainMenuItem
            {
                Description = "Sync Now",
                MenuSection = "@Syncnite",
                Action = _ => SyncNowWithProgress()
            };
        }

        // A single always-visible button next to Playnite's own top-bar icons (filter, view
        // toggle, etc) - one click straight to a sync, no menu to open first like the main-menu
        // item above (kept as-is for discoverability/muscle memory - both call the same code).
        public override IEnumerable<TopPanelItem> GetTopPanelItems()
        {
            yield return new TopPanelItem
            {
                Icon = new TextBlock
                {
                    Text = "↻",
                    FontSize = 20,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                },
                Title = "Sync with Syncnite",
                Activated = () => SyncNowWithProgress()
            };
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            var settings = LoadPluginSettings<CloudSyncSettings>();
            if (settings != null && settings.IsConnected && settings.SyncOnStartup)
            {
                FireAndForgetSync("startup");
            }

            autoSyncTimer = new Timer(
                _ => CheckAutoSync(),
                null,
                AutoSyncCheckInterval,
                AutoSyncCheckInterval);
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            autoSyncTimer?.Dispose();
            autoSyncTimer = null;
        }

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            FireAndForgetSync("library updated");
        }

        private void CheckAutoSync()
        {
            var settings = LoadPluginSettings<CloudSyncSettings>();
            if (settings == null || !settings.IsConnected || !settings.AutoSyncEnabled)
            {
                return;
            }

            var due = settings.LastSyncedAt == null ||
                DateTime.UtcNow - settings.LastSyncedAt.Value >=
                    TimeSpan.FromMinutes(settings.AutoSyncIntervalMinutes);

            if (due)
            {
                FireAndForgetSync("auto-sync");
            }
        }

        // Silent path for background triggers (startup, library changes, the auto-sync timer) -
        // no progress dialog, since popping one unprompted every time the library changes or
        // every N minutes would be disruptive rather than helpful.
        private void FireAndForgetSync(string reason)
        {
            Task.Run(async () =>
            {
                try
                {
                    await SyncNowAsync();
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Syncnite: sync failed ({reason}).");
                }
            });
        }

        // User-initiated path (the "Sync Now" menu item) - shows the same kind of progress
        // dialog Playnite uses for its own library updates, and lets the user cancel.
        public void SyncNowWithProgress()
        {
            var progressOptions = new GlobalProgressOptions("Syncing with cloud...", true)
            {
                IsIndeterminate = true
            };

            var result = PlayniteApi.Dialogs.ActivateGlobalProgress(
                args => SyncNowAsync(args),
                progressOptions);

            if (result.Error != null)
            {
                logger.Error(result.Error, "Syncnite: manual sync failed.");
                PlayniteApi.Dialogs.ShowErrorMessage(result.Error.Message, "Syncnite sync failed");
            }
        }

        /// Pushes the current library, then pulls down any edits made on the web since the
        /// last pull. Throws on failure (callers driving UI need the error); background
        /// triggers go through FireAndForgetSync, which catches and logs instead.
        public async Task SyncNowAsync(GlobalProgressActionArgs progress = null)
        {
            var settings = LoadPluginSettings<CloudSyncSettings>();
            if (settings == null || !settings.IsConnected)
            {
                logger.Debug("Syncnite: not connected, skipping sync.");
                return;
            }

            var ct = progress?.CancelToken ?? CancellationToken.None;
            var client = new CloudSyncApiClient(CloudSyncApiClient.DefaultBaseUrl, settings.DeviceToken);

            if (progress != null)
            {
                progress.IsIndeterminate = true;
                progress.Text = "Pushing your library to the cloud...";
            }

            // Also require the companion plugin to be enabled right now, not just installed -
            // otherwise a stale cache from before someone disabled it would keep getting pushed.
            var achievementCounts = settings.SyncAchievements && AchievementsReader.IsSupportedPluginEnabled(PlayniteApi)
                ? AchievementsReader.ReadCurrentUserCounts(PlayniteApi)
                : new Dictionary<Guid, AchievementCounts>();

            // Previously skipped Hidden games entirely, which meant anything hidden by a
            // duplicate-management plugin (e.g. Duplicate Hider, which hides all but one
            // Playnite entry for the same title owned on multiple sources) never reached the
            // cloud at all - there was nothing for the game detail page's "Also owned on"
            // switcher to find. Now pushes everything; LocalHidden below still seeds this app's
            // own (web-owned, filterable via "Show hidden") hidden field the same way it always
            // has, so default library browsing is unaffected.
            var games = PlayniteApi.Database.Games
                .Select(g =>
                {
                    achievementCounts.TryGetValue(g.Id, out var achievements);
                    return new PushGame
                    {
                        PlayniteId = g.Id.ToString(),
                        Name = g.Name,
                        Source = g.Source?.Name,
                        SourceGameId = g.GameId,
                        InstallStatus = g.IsInstalled ? "Installed" : "Uninstalled",
                        PlaytimeMinutes = (long)(g.Playtime / 60),
                        LastPlayed = g.LastActivity?.ToUniversalTime().ToString("o"),
                        DateAdded = g.Added?.ToUniversalTime().ToString("o"),
                        AchievementsUnlocked = achievements.Total > 0 ? (int?)achievements.Unlocked : null,
                        AchievementsTotal = achievements.Total > 0 ? (int?)achievements.Total : null,
                        LocalTags = (g.TagIds ?? new List<Guid>())
                            .Select(id => PlayniteApi.Database.Tags.Get(id)?.Name)
                            .Where(name => name != null)
                            .ToList(),
                        LocalCategories = (g.CategoryIds ?? new List<Guid>())
                            .Select(id => PlayniteApi.Database.Categories.Get(id)?.Name)
                            .Where(name => name != null)
                            .ToList(),
                        LocalNotes = g.Notes,
                        LocalCompletionStatus = g.CompletionStatusId != Guid.Empty
                            ? PlayniteApi.Database.CompletionStatuses.Get(g.CompletionStatusId)?.Name
                            : null,
                        LocalFavorite = g.Favorite,
                        LocalHidden = g.Hidden
                    };
                })
                .ToList();

            // Sent in batches, not one request for the whole library - Vercel's serverless
            // functions (what /api/sync/push runs on) hard-cap a request body at 4.5MB,
            // enforced before our code even runs, so it comes back as a bare HTTP 413 with no
            // JSON body to show the user. A ~500-byte-per-game payload crosses that around the
            // low tens of thousands of games; reported live by a user with a five-digit
            // library. Chunk size is deliberately generous (not just under the wire) so a
            // very large library still finishes in a handful of requests, comfortably inside
            // the server's own sync-push rate limit.
            const int PushChunkSize = 2000;
            var pushedCount = 0;
            for (var offset = 0; offset < games.Count; offset += PushChunkSize)
            {
                ct.ThrowIfCancellationRequested();
                var chunk = games.Skip(offset).Take(PushChunkSize).ToList();
                if (progress != null && games.Count > PushChunkSize)
                {
                    progress.Text = $"Pushing your library to the cloud... ({Math.Min(offset + PushChunkSize, games.Count)}/{games.Count})";
                }
                pushedCount += await client.PushGamesAsync(chunk, PluginVersion, ct);
            }
            logger.Info($"Syncnite: pushed {pushedCount} games.");
            settings.LastSyncedAt = DateTime.UtcNow;

            if (progress != null)
            {
                progress.Text = "Checking for changes made on the web...";
            }

            var pulledCount = await PullFromCloudAsync(settings, client, progress, ct);
            logger.Info($"Syncnite: applied {pulledCount} edits from the web.");

            SavePluginSettings(settings);
            settingsViewModel.RefreshFromDisk();
        }

        private async Task<int> PullFromCloudAsync(
            CloudSyncSettings settings,
            CloudSyncApiClient client,
            GlobalProgressActionArgs progress,
            CancellationToken ct)
        {
            var result = await client.PullGamesAsync(settings.LastPulledAt, ct);
            var pulledGames = result.Games ?? new List<PullGame>();
            var applied = 0;

            if (progress != null && pulledGames.Count > 0)
            {
                progress.IsIndeterminate = false;
                progress.ProgressMaxValue = pulledGames.Count;
                progress.CurrentProgressValue = 0;
            }

            foreach (var pulled in pulledGames)
            {
                ct.ThrowIfCancellationRequested();

                if (!Guid.TryParse(pulled.PlayniteId, out var gameId))
                {
                    logger.Warn($"Syncnite: could not parse playnite_id '{pulled.PlayniteId}' as a Guid.");
                    continue;
                }

                var game = PlayniteApi.Database.Games.Get(gameId);
                if (game == null)
                {
                    // Game no longer exists in this PC's library (removed, or belongs to a
                    // different device's copy of the same title) - nothing to apply it to.
                    continue;
                }

                if (progress != null)
                {
                    progress.Text = $"Applying web changes to {game.Name}...";
                }

                game.Notes = pulled.Notes;
                game.Favorite = pulled.Favorite;
                game.Hidden = pulled.Hidden;
                game.TagIds = (pulled.Tags ?? new List<string>())
                    .Select(name => PlayniteApi.Database.Tags.Add(name).Id)
                    .ToList();
                game.CategoryIds = (pulled.Categories ?? new List<string>())
                    .Select(name => PlayniteApi.Database.Categories.Add(name).Id)
                    .ToList();
                game.CompletionStatusId = string.IsNullOrEmpty(pulled.CompletionStatus)
                    ? Guid.Empty
                    : PlayniteApi.Database.CompletionStatuses.Add(pulled.CompletionStatus).Id;

                PlayniteApi.Database.Games.Update(game);
                applied++;

                if (progress != null)
                {
                    progress.CurrentProgressValue = applied;
                }
            }

            if (!string.IsNullOrEmpty(result.ServerTime) &&
                DateTime.TryParse(
                    result.ServerTime,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var serverTime))
            {
                settings.LastPulledAt = serverTime;
            }

            return applied;
        }
    }
}
