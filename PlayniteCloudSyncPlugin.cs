using System;
using System.Collections.Generic;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Plugins;

namespace PlayniteCloudSync
{
    public class PlayniteCloudSyncPlugin : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        public override Guid Id { get; } = Guid.Parse("f42cf930-4203-49b8-bf09-62ff06ecb92e");

        public PlayniteCloudSyncPlugin(IPlayniteAPI api) : base(api)
        {
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            // Phase 0: no-op menu item, just proves the plugin loaded and can extend the UI.
            yield return new MainMenuItem
            {
                Description = "Cloud Sync: Hello",
                MenuSection = "@Cloud Sync",
                Action = _ => logger.Info("Playnite Cloud Sync: menu item clicked.")
            };
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            logger.Info("Playnite Cloud Sync plugin started. Library currently has "
                + PlayniteApi.Database.Games.Count + " games (read-only, nothing is modified yet).");
        }
    }
}
