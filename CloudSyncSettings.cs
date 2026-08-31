using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PlayniteCloudSync
{
    public class CloudSyncSettings : INotifyPropertyChanged
    {
        private string apiBaseUrl = "http://localhost:3000";
        private string deviceToken;
        private bool autoSyncEnabled = true;
        private int autoSyncIntervalMinutes = 15;
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
                autoSyncEnabled = this.autoSyncEnabled,
                autoSyncIntervalMinutes = this.autoSyncIntervalMinutes,
                lastSyncedAt = this.lastSyncedAt,
                lastPulledAt = this.lastPulledAt
            };
        }

        public void CopyFrom(CloudSyncSettings other)
        {
            ApiBaseUrl = other.apiBaseUrl;
            DeviceToken = other.deviceToken;
            AutoSyncEnabled = other.autoSyncEnabled;
            AutoSyncIntervalMinutes = other.autoSyncIntervalMinutes;
            LastSyncedAt = other.lastSyncedAt;
            LastPulledAt = other.lastPulledAt;
        }
    }
}
