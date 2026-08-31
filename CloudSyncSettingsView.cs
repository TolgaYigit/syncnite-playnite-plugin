using System;
using System.Windows;
using System.Windows.Controls;
using Playnite.SDK;

namespace PlayniteCloudSync
{
    public class CloudSyncSettingsView : UserControl
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly CloudSyncSettingsViewModel viewModel;
        private readonly TextBox apiBaseUrlBox;
        private readonly TextBox pairingCodeBox;
        private readonly TextBlock statusText;
        private readonly Button connectButton;

        public CloudSyncSettingsView(CloudSyncSettingsViewModel viewModel)
        {
            this.viewModel = viewModel;

            var root = new StackPanel { Margin = new Thickness(4) };

            root.Children.Add(new TextBlock
            {
                Text = "API base URL",
                Margin = new Thickness(0, 0, 0, 4)
            });
            apiBaseUrlBox = new TextBox
            {
                Text = viewModel.Settings.ApiBaseUrl,
                Margin = new Thickness(0, 0, 0, 12)
            };
            apiBaseUrlBox.TextChanged += (s, e) => viewModel.Settings.ApiBaseUrl = apiBaseUrlBox.Text;
            root.Children.Add(apiBaseUrlBox);

            statusText = new TextBlock { Margin = new Thickness(0, 0, 0, 8) };
            root.Children.Add(statusText);

            var pairingRow = new DockPanel();
            pairingCodeBox = new TextBox
            {
                Width = 140,
                CharacterCasing = CharacterCasing.Upper,
                MaxLength = 8
            };
            DockPanel.SetDock(pairingCodeBox, Dock.Left);
            pairingRow.Children.Add(pairingCodeBox);

            connectButton = new Button
            {
                Content = "Connect",
                Margin = new Thickness(8, 0, 0, 0),
                Padding = new Thickness(10, 2, 10, 2)
            };
            connectButton.Click += ConnectButton_Click;
            pairingRow.Children.Add(connectButton);

            root.Children.Add(new TextBlock
            {
                Text = "Pairing code (generate one on the web app's Settings page)",
                Margin = new Thickness(0, 0, 0, 4)
            });
            root.Children.Add(pairingRow);

            var disconnectButton = new Button
            {
                Content = "Disconnect",
                Margin = new Thickness(0, 12, 0, 0),
                Padding = new Thickness(10, 2, 10, 2),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            disconnectButton.Click += (s, e) =>
            {
                viewModel.Settings.DeviceToken = null;
                RefreshStatus();
            };
            root.Children.Add(disconnectButton);

            Content = root;
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            statusText.Text = viewModel.Settings.IsConnected
                ? "Status: connected"
                : "Status: not connected";
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            var code = pairingCodeBox.Text?.Trim();
            if (string.IsNullOrEmpty(code))
            {
                return;
            }

            connectButton.IsEnabled = false;
            statusText.Text = "Connecting...";

            try
            {
                var client = new CloudSyncApiClient(apiBaseUrlBox.Text);
                var token = await client.RedeemPairingCodeAsync(code);
                viewModel.Settings.DeviceToken = token;
                RefreshStatus();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Playnite Cloud Sync: pairing failed.");
                statusText.Text = "Connection failed: " + ex.Message;
            }
            finally
            {
                connectButton.IsEnabled = true;
            }
        }
    }
}
