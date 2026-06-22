using System;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace DayView
{
    public sealed partial class MainPage
    {
        private static readonly Color BrandBlue = Color.FromArgb(0xFF, 0x00, 0x78, 0xD7);

        // ==================== Accent color ====================

        private void LoadAccentSetting()
        {
            bool useSystem = _data.UseSystemAccent;
            AccentColorToggle.IsOn = useSystem;
            ApplyAccent(useSystem);
        }

        private void ApplyAccent(bool useSystemAccent)
        {
            var brush = Application.Current.Resources["AppAccentBrush"] as SolidColorBrush;
            if (brush == null) return;
            brush.Color = useSystemAccent
                ? new UISettings().GetColorValue(UIColorType.Accent)
                : BrandBlue;
        }

        private void AccentColorToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_initialized) return;
            _data.UseSystemAccent = AccentColorToggle.IsOn;
            ApplyAccent(AccentColorToggle.IsOn);
            // Re-apply nav button colors so the active tab reflects the new accent.
            SetActiveTab(_activeTab);
        }

        // ==================== Auto-refresh ====================

        private void InitRefreshIntervalCombo()
        {
            int minutes = _data.RefreshIntervalMinutes;
            int selected = 2; // default to "Every 30 minutes"
            for (int i = 0; i < RefreshIntervalCombo.Items.Count; i++)
            {
                var item = RefreshIntervalCombo.Items[i] as ComboBoxItem;
                if (item != null && item.Tag != null &&
                    int.Parse(item.Tag.ToString()) == minutes)
                {
                    selected = i;
                    break;
                }
            }
            RefreshIntervalCombo.SelectedIndex = selected;
            ApplyRefreshInterval(minutes);
        }

        private void RefreshIntervalCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = RefreshIntervalCombo.SelectedItem as ComboBoxItem;
            if (item == null || item.Tag == null) return;

            int minutes = int.Parse(item.Tag.ToString());
            if (_initialized)
                _data.RefreshIntervalMinutes = minutes;
            ApplyRefreshInterval(minutes);
        }

        private void ApplyRefreshInterval(int minutes)
        {
            _refreshTimer.Stop();
            if (minutes <= 0) return;
            _refreshTimer.Interval = TimeSpan.FromMinutes(minutes);
            _refreshTimer.Start();
        }

        private async void RefreshTimer_Tick(object sender, object e)
        {
            // Only auto-refresh while viewing the feed and not reading an article.
            if (_activeTab == 0 && ReaderOverlay.Visibility == Visibility.Collapsed)
                await LoadCurrentAsync();
        }
    }
}
