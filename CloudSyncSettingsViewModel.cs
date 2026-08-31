using System.Collections.Generic;
using Playnite.SDK;

namespace PlayniteCloudSync
{
    public class CloudSyncSettingsViewModel : ISettings
    {
        private readonly PlayniteCloudSyncPlugin plugin;
        private CloudSyncSettings editingClone;

        public CloudSyncSettings Settings { get; private set; }

        public CloudSyncSettingsViewModel(PlayniteCloudSyncPlugin plugin)
        {
            this.plugin = plugin;
            var saved = plugin.LoadPluginSettings<CloudSyncSettings>();
            Settings = saved ?? new CloudSyncSettings();
        }

        public void BeginEdit()
        {
            editingClone = Settings.Clone();
        }

        public void CancelEdit()
        {
            Settings.CopyFrom(editingClone);
        }

        public void EndEdit()
        {
            plugin.SavePluginSettings(Settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            if (string.IsNullOrWhiteSpace(Settings.ApiBaseUrl))
            {
                errors.Add("API base URL is required.");
            }
            return errors.Count == 0;
        }

        // Background syncs save settings (LastSyncedAt) via a freshly-deserialized instance,
        // separate from this long-lived, data-bound Settings object - pull those changes in.
        public void RefreshFromDisk()
        {
            var saved = plugin.LoadPluginSettings<CloudSyncSettings>();
            if (saved != null)
            {
                Settings.CopyFrom(saved);
            }
        }
    }
}
