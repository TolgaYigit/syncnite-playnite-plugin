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
                lastSyncedAt = this.lastSyncedAt
            };
        }

        public void CopyFrom(CloudSyncSettings other)
        {
            ApiBaseUrl = other.apiBaseUrl;
            DeviceToken = other.deviceToken;
            AutoSyncEnabled = other.autoSyncEnabled;
            AutoSyncIntervalMinutes = other.autoSyncIntervalMinutes;
            LastSyncedAt = other.lastSyncedAt;
        }
    }
}
