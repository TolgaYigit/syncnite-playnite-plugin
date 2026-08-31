using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PlayniteCloudSync
{
    public class CloudSyncSettings : INotifyPropertyChanged
    {
        private string apiBaseUrl = "http://localhost:3000";
        private string deviceToken;

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
                deviceToken = this.deviceToken
            };
        }

        public void CopyFrom(CloudSyncSettings other)
        {
            ApiBaseUrl = other.apiBaseUrl;
            DeviceToken = other.deviceToken;
        }
    }
}
