using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PlayniteCloudSync
{
    public class CloudSyncSettings : INotifyPropertyChanged
    {
        private string apiBaseUrl = "https://web-eight-delta-33.vercel.app";
        private string deviceToken;
        private bool syncOnStartup = true;
        private bool autoSyncEnabled = true;
        private int autoSyncIntervalMinutes = 15;
        private bool syncAchievements = true;
        private DateTime? lastSyncedAt;
        private DateTime? lastPulledAt;

        public string ApiBaseUrl
        {
            get => apiBaseUrl;
            set => SetField(ref apiBaseUrl, value);
        }

        public string DeviceToken
        {
            get => deviceToken;
            set => SetField(ref deviceToken, value);
        }

        public bool SyncOnStartup
        {
            get => syncOnStartup;
            set => SetField(ref syncOnStartup, value);
        }

        public bool AutoSyncEnabled
        {
            get => autoSyncEnabled;
            set => SetField(ref autoSyncEnabled, value);
        }

        public int AutoSyncIntervalMinutes
        {
            get => autoSyncIntervalMinutes;
            set => SetField(ref autoSyncIntervalMinutes, value < 1 ? 1 : value);
        }

        // Whether to read achievement counts from the PlayniteAchievements plugin's local cache
        // and include them in the push. Off by user choice, not just by absence: someone might
        // have that plugin installed but not want its data leaving their PC, so this needs its
        // own switch rather than only auto-detecting whether the plugin is present.
        public bool SyncAchievements
        {
            get => syncAchievements;
            set => SetField(ref syncAchievements, value);
        }

        public DateTime? LastSyncedAt
        {
            get => lastSyncedAt;
            set => SetField(ref lastSyncedAt, value);
        }

        // Cursor for incremental pulls - the server's clock, not this PC's, to avoid clock-skew
        // gaps. Kept separate from LastSyncedAt (push) since push/pull can fail independently.
        public DateTime? LastPulledAt
        {
            get => lastPulledAt;
            set => SetField(ref lastPulledAt, value);
        }

        public bool IsConnected => !string.IsNullOrEmpty(DeviceToken);

        public event PropertyChangedEventHandler PropertyChanged;

        private void SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            if (propertyName == nameof(DeviceToken))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsConnected)));
            }
        }

        public CloudSyncSettings Clone()
        {
            return new CloudSyncSettings
            {
                apiBaseUrl = this.apiBaseUrl,
                deviceToken = this.deviceToken,
                syncOnStartup = this.syncOnStartup,
                autoSyncEnabled = this.autoSyncEnabled,
                autoSyncIntervalMinutes = this.autoSyncIntervalMinutes,
                syncAchievements = this.syncAchievements,
                lastSyncedAt = this.lastSyncedAt,
                lastPulledAt = this.lastPulledAt
            };
        }

        public void CopyFrom(CloudSyncSettings other)
        {
            ApiBaseUrl = other.apiBaseUrl;
            DeviceToken = other.deviceToken;
            SyncOnStartup = other.syncOnStartup;
            AutoSyncEnabled = other.autoSyncEnabled;
            AutoSyncIntervalMinutes = other.autoSyncIntervalMinutes;
            SyncAchievements = other.syncAchievements;
            LastSyncedAt = other.lastSyncedAt;
            LastPulledAt = other.lastPulledAt;
        }
    }
}
