using System;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using DayView.Models;

namespace DayView
{
    public sealed partial class MainPage
    {
        // ==================== Subscribed feeds list ====================

        private void RebuildSubscribedList()
        {
            // Snapshot into a plain list so the ItemsControl shows the current feeds.
            var list = new List<FeedSource>(_data.Feeds);
            SubscribedList.ItemsSource = list;
        }

        private async void RemoveFeed_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var feed = btn != null ? btn.Tag as FeedSource : null;
            if (feed == null) return;

            int previousIndex = FeedChips.SelectedIndex;
            await _data.RemoveFeedAsync(feed.Url);

            RebuildSubscribedList();
            // Keep the current selection valid after the feed list shrinks.
            int target = previousIndex >= _chipNames.Count - 1 ? 0 : previousIndex;
            BuildChips(target);
            await LoadCurrentAsync();
        }

        // ==================== Add by URL ====================

        private async void AddUrlButton_Click(object sender, RoutedEventArgs e)
        {
            await AddByUrlAsync();
        }

        private async void AddUrlBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
                await AddByUrlAsync();
        }

        private async System.Threading.Tasks.Task AddByUrlAsync()
        {
            string input = AddUrlBox.Text;
            if (string.IsNullOrWhiteSpace(input)) return;

            SetSettingsStatus("Resolving feed…", false);
            AddUrlButton.IsEnabled = false;
            try
            {
                var resolved = await _discovery.ResolveFeedAsync(input);
                if (resolved == null || string.IsNullOrEmpty(resolved.Url))
                {
                    SetSettingsStatus("No feed found at that address.", true);
                    return;
                }

                if (_data.IsSubscribed(resolved.Url))
                {
                    SetSettingsStatus("You already follow that feed.", true);
                    return;
                }

                bool added = await _data.AddFeedAsync(resolved.Title, resolved.Url, resolved.IconUrl);
                if (added)
                {
                    AddUrlBox.Text = "";
                    OnFeedsChanged();
                    SetSettingsStatus("Added \"" + resolved.Title + "\".", false);
                }
            }
            catch
            {
                SetSettingsStatus("Could not add that feed.", true);
            }
            finally
            {
                AddUrlButton.IsEnabled = true;
            }
        }

        // ==================== Keyword search ====================

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            await SearchAsync();
        }

        private async void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
                await SearchAsync();
        }

        private async System.Threading.Tasks.Task SearchAsync()
        {
            string query = SearchBox.Text;
            if (string.IsNullOrWhiteSpace(query)) return;

            SearchResultsList.ItemsSource = null;
            SearchStatusText.Visibility = Visibility.Collapsed;
            SearchLoadingRing.IsActive = true;
            SearchButton.IsEnabled = false;

            try
            {
                var results = await _discovery.SearchAsync(query);
                SearchResultsList.ItemsSource = results;

                if (results.Count == 0)
                {
                    SearchStatusText.Text = "No feeds found for \"" + query.Trim() + "\".";
                    SearchStatusText.Visibility = Visibility.Visible;
                }
            }
            catch
            {
                SearchStatusText.Text = "Search failed. Check your connection and try again.";
                SearchStatusText.Visibility = Visibility.Visible;
            }
            finally
            {
                SearchLoadingRing.IsActive = false;
                SearchButton.IsEnabled = true;
            }
        }

        private async void AddSearchResult_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var result = btn != null ? btn.Tag as FeedSearchResult : null;
            if (result == null) return;

            if (_data.IsSubscribed(result.Url))
            {
                SetSettingsStatus("You already follow \"" + result.Title + "\".", true);
                return;
            }

            bool added = await _data.AddFeedAsync(result.Title, result.Url, result.IconUrl);
            if (added)
            {
                OnFeedsChanged();
                SetSettingsStatus("Added \"" + result.Title + "\".", false);
            }
        }

        // ==================== Shared helpers ====================

        // Refreshes UI that depends on the subscribed-feed list after an add.
        private async void OnFeedsChanged()
        {
            RebuildSubscribedList();
            int current = FeedChips.SelectedIndex;
            BuildChips(current < 0 ? 0 : current);
            await LoadCurrentAsync();
        }

        private void SetSettingsStatus(string message, bool isError)
        {
            SettingsStatus.Text = message;
            SettingsStatus.Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(
                isError ? Windows.UI.ColorHelper.FromArgb(0xFF, 0xFF, 0x6B, 0x6B)
                        : Windows.UI.ColorHelper.FromArgb(0xFF, 0x4C, 0xD9, 0x64));
        }
    }
}
