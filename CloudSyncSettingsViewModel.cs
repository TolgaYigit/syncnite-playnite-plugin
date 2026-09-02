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

        // Pairing/unpairing needs to survive the settings window closing any way other than its
        // own Save button (the X button, Alt+F4, clicking elsewhere) - EndEdit() above only runs
        // on an explicit Save, so a token set via BeginEdit's edit session alone could silently
        // never reach disk. Persist it immediately, and refresh the CancelEdit snapshot so a
        // later Cancel (e.g. after also tweaking the API URL) can't revert a pairing change that
        // already made it to disk and back it out from under a user who thinks they're connected.
        public void SetDeviceToken(string token)
        {
            Settings.DeviceToken = token;
            plugin.SavePluginSettings(Settings);
            editingClone = Settings.Clone();
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
