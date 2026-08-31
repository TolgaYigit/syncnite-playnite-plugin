using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
                MenuSection = "@Cloud Sync",
                Action = _ => SyncNowWithProgress()
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
                    logger.Error(ex, $"Playnite Cloud Sync: sync failed ({reason}).");
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
                logger.Error(result.Error, "Playnite Cloud Sync: manual sync failed.");
                PlayniteApi.Dialogs.ShowErrorMessage(result.Error.Message, "Cloud Sync failed");
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
                logger.Debug("Playnite Cloud Sync: not connected, skipping sync.");
                return;
            }

            var ct = progress?.CancelToken ?? CancellationToken.None;
            var client = new CloudSyncApiClient(settings.ApiBaseUrl, settings.DeviceToken);

            if (progress != null)
            {
                progress.IsIndeterminate = true;
                progress.Text = "Pushing your library to the cloud...";
            }

            var games = PlayniteApi.Database.Games
                .Where(g => !g.Hidden)
                .Select(g => new PushGame
                {
                    PlayniteId = g.Id.ToString(),
                    Name = g.Name,
                    Source = g.Source?.Name,
                    InstallStatus = g.IsInstalled ? "Installed" : "Uninstalled",
                    PlaytimeMinutes = (long)(g.Playtime / 60),
                    LastPlayed = g.LastActivity?.ToUniversalTime().ToString("o")
                })
                .ToList();

            var pushedCount = await client.PushGamesAsync(games, ct);
            logger.Info($"Playnite Cloud Sync: pushed {pushedCount} games.");
            settings.LastSyncedAt = DateTime.UtcNow;

            if (progress != null)
            {
                progress.Text = "Checking for changes made on the web...";
            }

            var pulledCount = await PullFromCloudAsync(settings, client, progress, ct);
            logger.Info($"Playnite Cloud Sync: applied {pulledCount} edits from the web.");

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
                    logger.Warn($"Playnite Cloud Sync: could not parse playnite_id '{pulled.PlayniteId}' as a Guid.");
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
