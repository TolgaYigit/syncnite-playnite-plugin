using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Playnite.SDK;

namespace PlayniteCloudSync
{
    public class CloudSyncSettingsView : UserControl
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly PlayniteCloudSyncPlugin plugin;
        private readonly CloudSyncSettingsViewModel viewModel;

        private readonly Ellipse statusDot;
        private readonly TextBlock statusText;
        private readonly TextBlock lastSyncedText;
        private readonly TextBlock errorText;

        private readonly TextBox apiBaseUrlBox;
        private readonly StackPanel pairingPanel;
        private readonly TextBox pairingCodeBox;
        private readonly Button connectButton;
        private readonly Button disconnectButton;

        private readonly StackPanel syncPanel;
        private readonly CheckBox startupSyncCheckBox;
        private readonly CheckBox autoSyncCheckBox;
        private readonly TextBox intervalBox;
        private readonly Button syncNowButton;

        public CloudSyncSettingsView(PlayniteCloudSyncPlugin plugin, CloudSyncSettingsViewModel viewModel)
        {
            this.plugin = plugin;
            this.viewModel = viewModel;

            var root = new StackPanel { Margin = new Thickness(4), Width = 420 };

            // --- Status header ---
            var statusRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            statusDot = new Ellipse { Width = 10, Height = 10, Margin = new Thickness(0, 0, 8, 0) };
            statusText = new TextBlock { FontWeight = FontWeights.Bold, FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
            statusRow.Children.Add(statusDot);
            statusRow.Children.Add(statusText);
            root.Children.Add(statusRow);

            lastSyncedText = new TextBlock { Margin = new Thickness(18, 0, 0, 16), Opacity = 0.7, FontSize = 11 };
            root.Children.Add(lastSyncedText);

            errorText = new TextBlock
            {
                Margin = new Thickness(0, 0, 0, 8),
                Foreground = Brushes.OrangeRed,
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed
            };
            root.Children.Add(errorText);

            // --- Connection section ---
            root.Children.Add(SectionHeader("Connection"));

            root.Children.Add(new TextBlock { Text = "API base URL", Margin = new Thickness(0, 0, 0, 2), Opacity = 0.8 });
            apiBaseUrlBox = new TextBox { Text = viewModel.Settings.ApiBaseUrl, Margin = new Thickness(0, 0, 0, 10) };
            apiBaseUrlBox.TextChanged += (s, e) => viewModel.Settings.ApiBaseUrl = apiBaseUrlBox.Text;
            root.Children.Add(apiBaseUrlBox);

            pairingPanel = new StackPanel();
            pairingPanel.Children.Add(new TextBlock
            {
                Text = "Pairing code (generate one on the web app's Settings page)",
                Margin = new Thickness(0, 0, 0, 4),
                Opacity = 0.8
            });
            var pairingRow = new DockPanel();
            pairingCodeBox = new TextBox { Width = 140, CharacterCasing = CharacterCasing.Upper, MaxLength = 8 };
            DockPanel.SetDock(pairingCodeBox, Dock.Left);
            pairingRow.Children.Add(pairingCodeBox);
            connectButton = new Button { Content = "Connect", Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(10, 2, 10, 2) };
            connectButton.Click += ConnectButton_Click;
            pairingRow.Children.Add(connectButton);
            pairingPanel.Children.Add(pairingRow);
            root.Children.Add(pairingPanel);

            disconnectButton = new Button
            {
                Content = "Disconnect",
                Margin = new Thickness(0, 4, 0, 0),
                Padding = new Thickness(10, 2, 10, 2),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            disconnectButton.Click += (s, e) =>
            {
                viewModel.Settings.DeviceToken = null;
                Refresh();
            };
            root.Children.Add(disconnectButton);

            // --- Sync section ---
            syncPanel = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };
            syncPanel.Children.Add(SectionHeader("Sync"));

            startupSyncCheckBox = new CheckBox
            {
                Content = "Sync automatically when Playnite starts",
                IsChecked = viewModel.Settings.SyncOnStartup,
                Margin = new Thickness(0, 0, 0, 8)
            };
            startupSyncCheckBox.Checked += (s, e) => viewModel.Settings.SyncOnStartup = true;
            startupSyncCheckBox.Unchecked += (s, e) => viewModel.Settings.SyncOnStartup = false;
            syncPanel.Children.Add(startupSyncCheckBox);

            var autoSyncRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            autoSyncCheckBox = new CheckBox
            {
                Content = "Automatically sync every",
                IsChecked = viewModel.Settings.AutoSyncEnabled,
                VerticalAlignment = VerticalAlignment.Center
            };
            autoSyncCheckBox.Checked += (s, e) => viewModel.Settings.AutoSyncEnabled = true;
            autoSyncCheckBox.Unchecked += (s, e) => viewModel.Settings.AutoSyncEnabled = false;
            autoSyncRow.Children.Add(autoSyncCheckBox);

            intervalBox = new TextBox
            {
                Width = 40,
                Margin = new Thickness(8, 0, 4, 0),
                Text = viewModel.Settings.AutoSyncIntervalMinutes.ToString(CultureInfo.InvariantCulture)
            };
            intervalBox.TextChanged += (s, e) =>
            {
                if (int.TryParse(intervalBox.Text, out var minutes))
                {
                    viewModel.Settings.AutoSyncIntervalMinutes = minutes;
                }
            };
            autoSyncRow.Children.Add(intervalBox);
            autoSyncRow.Children.Add(new TextBlock { Text = "minutes", VerticalAlignment = VerticalAlignment.Center });
            syncPanel.Children.Add(autoSyncRow);

            syncNowButton = new Button
            {
                Content = "Sync Now",
                Padding = new Thickness(10, 2, 10, 2),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            syncNowButton.Click += SyncNowButton_Click;
            syncPanel.Children.Add(syncNowButton);

            root.Children.Add(syncPanel);

            Content = root;
            Refresh();
        }

        private static TextBlock SectionHeader(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8),
                Opacity = 0.9
            };
        }

        private void Refresh()
        {
            var connected = viewModel.Settings.IsConnected;

            statusDot.Fill = connected ? Brushes.LimeGreen : Brushes.Gray;
            statusText.Text = connected ? "Connected" : "Not connected";

            lastSyncedText.Text = connected
                ? viewModel.Settings.LastSyncedAt.HasValue
                    ? "Last synced " + viewModel.Settings.LastSyncedAt.Value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
                    : "Not synced yet"
                : "";

            pairingPanel.Visibility = connected ? Visibility.Collapsed : Visibility.Visible;
            disconnectButton.Visibility = connected ? Visibility.Visible : Visibility.Collapsed;
            syncPanel.Visibility = connected ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            var code = pairingCodeBox.Text?.Trim();
            if (string.IsNullOrEmpty(code))
            {
                return;
            }

            connectButton.IsEnabled = false;
            errorText.Visibility = Visibility.Collapsed;
            statusText.Text = "Connecting...";

            try
            {
                var client = new CloudSyncApiClient(apiBaseUrlBox.Text);
                var token = await client.RedeemPairingCodeAsync(code);
                viewModel.Settings.DeviceToken = token;
                Refresh();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Playnite Cloud Sync: pairing failed.");
                errorText.Text = "Connection failed: " + ex.Message;
                errorText.Visibility = Visibility.Visible;
                Refresh();
            }
            finally
            {
                connectButton.IsEnabled = true;
            }
        }

        private void SyncNowButton_Click(object sender, RoutedEventArgs e)
        {
            syncNowButton.IsEnabled = false;
            errorText.Visibility = Visibility.Collapsed;

            try
            {
                // Shows the same progress dialog as the main menu's "Sync Now".
                plugin.SyncNowWithProgress();
                Refresh();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Playnite Cloud Sync: manual sync from settings failed.");
                errorText.Text = "Sync failed: " + ex.Message;
                errorText.Visibility = Visibility.Visible;
            }
            finally
            {
                syncNowButton.IsEnabled = true;
            }
        }
    }
}
