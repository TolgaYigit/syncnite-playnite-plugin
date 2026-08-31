using System;
using System.Collections.Generic;
using System.Linq;
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

        private readonly CloudSyncSettingsViewModel settingsViewModel;

        public override Guid Id { get; } = Guid.Parse("f42cf930-4203-49b8-bf09-62ff06ecb92e");

        public PlayniteCloudSyncPlugin(IPlayniteAPI api) : base(api)
        {
            settingsViewModel = new CloudSyncSettingsViewModel(this);
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settingsViewModel;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new CloudSyncSettingsView(settingsViewModel);
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            yield return new MainMenuItem
            {
                Description = "Sync Now",
                MenuSection = "@Cloud Sync",
                Action = _ => FireAndForgetSync("manual sync")
            };
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            FireAndForgetSync("startup");
        }

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            FireAndForgetSync("library updated");
        }

        private void FireAndForgetSync(string reason)
        {
            Task.Run(async () =>
            {
                try
                {
                    await SyncLibraryAsync();
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Playnite Cloud Sync: sync failed ({reason}).");
                }
            });
        }

        private async Task SyncLibraryAsync()
        {
            var settings = LoadPluginSettings<CloudSyncSettings>();
            if (settings == null || !settings.IsConnected)
            {
                logger.Debug("Playnite Cloud Sync: not connected, skipping sync.");
                return;
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

            var client = new CloudSyncApiClient(settings.ApiBaseUrl, settings.DeviceToken);
            var count = await client.PushGamesAsync(games);
            logger.Info($"Playnite Cloud Sync: pushed {count} games.");
        }
    }
}
