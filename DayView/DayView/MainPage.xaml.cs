using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using DayView.Models;
using DayView.Services;

namespace DayView
{
    public sealed partial class MainPage : Page
    {
        private readonly DataService _data = new DataService();
        private readonly FeedService _feed = new FeedService();
        private readonly FeedDiscoveryService _discovery = new FeedDiscoveryService();

        // Source dropdown labels: index 0 is the aggregated "All" view, the rest
        // map to _sortedFeeds (feeds sorted alphabetically by title).
        private readonly ObservableCollection<string> _sourceNames = new ObservableCollection<string>();
        private readonly List<FeedSource> _sortedFeeds = new List<FeedSource>();
        private readonly ObservableCollection<Article> _articles = new ObservableCollection<Article>();

        private readonly DispatcherTimer _refreshTimer = new DispatcherTimer();

        private bool _initialized;
        private bool _suppressSourceReload;
        private Article _currentArticle;

        // 0 = Feed tab, 1 = Settings tab.
        private int _activeTab;

        public MainPage()
        {
            this.InitializeComponent();
            FeedSourceCombo.ItemsSource = _sourceNames;
            ArticlesList.ItemsSource = _articles;
            _refreshTimer.Tick += RefreshTimer_Tick;

            SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
            this.Loaded += MainPage_Loaded;
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await _data.LoadAsync();

            LoadAccentSetting();
            InitRefreshIntervalCombo();
            BuildInfoText.Text = "DayView 2.0 — build " + BuildInfo.Date;

            RebuildSubscribedList();
            BuildSources("All");
            SetFeedsSubtab(0);

            _initialized = true;

            // Selecting the first source loads the aggregated feed.
            await LoadCurrentAsync();
        }

        // ==================== Source dropdown ====================

        // Rebuilds the dropdown ("All" + feeds sorted alphabetically) and selects the
        // item matching selectName (falling back to "All"). Reloading is suppressed
        // here; callers reload explicitly afterwards.
        private void BuildSources(string selectName)
        {
            _suppressSourceReload = true;
            try
            {
                _sortedFeeds.Clear();
                _sortedFeeds.AddRange(_data.Feeds);
                _sortedFeeds.Sort((a, b) =>
                    string.Compare(a.Title, b.Title, StringComparison.CurrentCultureIgnoreCase));

                _sourceNames.Clear();
                _sourceNames.Add("All");
                foreach (var f in _sortedFeeds)
                    _sourceNames.Add(f.Title);

                int selectIndex = string.IsNullOrEmpty(selectName) ? 0 : _sourceNames.IndexOf(selectName);
                if (selectIndex < 0) selectIndex = 0;
                FeedSourceCombo.SelectedIndex = selectIndex;
            }
            finally
            {
                _suppressSourceReload = false;
            }
        }

        // The currently selected source name, or "All" when nothing is selected.
        private string SelectedSourceName
        {
            get
            {
                return FeedSourceCombo.SelectedItem as string ?? "All";
            }
        }

        private async void FeedSourceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_initialized || _suppressSourceReload) return;
            await LoadCurrentAsync();
        }

        // ==================== Loading articles ====================

        private async Task LoadCurrentAsync()
        {
            int index = FeedSourceCombo.SelectedIndex;
            if (index < 0) index = 0;

            _articles.Clear();
            FeedErrorText.Visibility = Visibility.Collapsed;
            FeedLoadingRing.IsActive = true;

            try
            {
                List<Article> result;
                if (index == 0)
                {
                    result = await _feed.FetchAggregatedAsync(_data.Feeds);
                }
                else
                {
                    var source = _sortedFeeds[index - 1];
                    result = await _feed.FetchFeedAsync(source.Url, source.Title);
                }

                foreach (var a in result)
                    _articles.Add(a);

                if (_articles.Count == 0)
                {
                    FeedErrorText.Text = _data.Feeds.Count == 0
                        ? "No feeds yet. Add one from the Settings tab."
                        : "No articles found.";
                    FeedErrorText.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                FeedErrorText.Text = "Could not load this feed.\n" + ex.Message;
                FeedErrorText.Visibility = Visibility.Visible;
            }
            finally
            {
                FeedLoadingRing.IsActive = false;
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadCurrentAsync();
        }

        // ==================== Reader overlay ====================

        private void ArticlesList_ItemClick(object sender, ItemClickEventArgs e)
        {
            var article = e.ClickedItem as Article;
            if (article == null) return;
            ShowReader(article);
        }

        private void ShowReader(Article article)
        {
            _currentArticle = article;
            ReaderTitle.Text = article.Title;
            ReaderWebView.NavigateToString(BuildReaderHtml(article));
            ReaderOverlay.Visibility = Visibility.Visible;
        }

        private void ReaderBack_Click(object sender, RoutedEventArgs e)
        {
            CloseReader();
        }

        private void CloseReader()
        {
            ReaderOverlay.Visibility = Visibility.Collapsed;
            // Stop any media/script in the WebView and release memory.
            ReaderWebView.NavigateToString("<html><body style='background:#000'></body></html>");
            _currentArticle = null;
        }

        private async void OpenInBrowser_Click(object sender, RoutedEventArgs e)
        {
            if (_currentArticle == null || string.IsNullOrEmpty(_currentArticle.Link)) return;
            Uri uri;
            if (Uri.TryCreate(_currentArticle.Link, UriKind.Absolute, out uri))
                await Launcher.LaunchUriAsync(uri);
        }

        // Wraps the article body in a mobile-friendly dark stylesheet. Images are
        // made responsive; feeds without images simply render as text.
        private static string BuildReaderHtml(Article article)
        {
            string body = article.ContentHtml;
            if (string.IsNullOrEmpty(body))
                body = "<p>" + System.Net.WebUtility.HtmlEncode(article.Summary ?? "") + "</p>";

            string heading = System.Net.WebUtility.HtmlEncode(article.Title ?? "");
            string meta = System.Net.WebUtility.HtmlEncode(article.FeedAndDate ?? "");

            return
                "<!DOCTYPE html><html><head>" +
                "<meta charset='utf-8' />" +
                "<meta name='viewport' content='width=device-width, initial-scale=1' />" +
                "<style>" +
                "html,body{margin:0;padding:0;background:#000;color:#eee;" +
                "font-family:'Segoe UI',sans-serif;font-size:17px;line-height:1.55;}" +
                ".wrap{padding:16px;}" +
                "h1{font-size:22px;line-height:1.3;margin:0 0 6px 0;color:#fff;}" +
                ".meta{color:#888;font-size:13px;margin:0 0 16px 0;}" +
                "img{max-width:100%;height:auto;border-radius:6px;}" +
                "a{color:#4aa3ff;}" +
                "figure{margin:12px 0;}" +
                "figcaption{color:#999;font-size:13px;}" +
                "pre,code{white-space:pre-wrap;word-wrap:break-word;}" +
                "</style></head><body><div class='wrap'>" +
                "<h1>" + heading + "</h1>" +
                "<div class='meta'>" + meta + "</div>" +
                body +
                "</div></body></html>";
        }

        // ==================== Bottom navigation ====================

        private void NavFeed_Click(object sender, RoutedEventArgs e)
        {
            SetActiveTab(0);
        }

        private void NavFeeds_Click(object sender, RoutedEventArgs e)
        {
            SetActiveTab(1);
        }

        private void NavSettings_Click(object sender, RoutedEventArgs e)
        {
            SetActiveTab(2);
        }

        // 0 = Feed, 1 = Feeds (management), 2 = Settings.
        private void SetActiveTab(int tab)
        {
            _activeTab = tab;
            FeedPanel.Visibility = tab == 0 ? Visibility.Visible : Visibility.Collapsed;
            FeedsPanel.Visibility = tab == 1 ? Visibility.Visible : Visibility.Collapsed;
            SettingsPanel.Visibility = tab == 2 ? Visibility.Visible : Visibility.Collapsed;

            NavFeed.Background = tab == 0 ? AccentBrush : InactiveNavBrush;
            NavFeeds.Background = tab == 1 ? AccentBrush : InactiveNavBrush;
            NavSettings.Background = tab == 2 ? AccentBrush : InactiveNavBrush;
        }

        // ==================== Feeds subtabs ====================

        private void FeedsDiscoverTab_Click(object sender, RoutedEventArgs e)
        {
            SetFeedsSubtab(0);
        }

        private void FeedsManualTab_Click(object sender, RoutedEventArgs e)
        {
            SetFeedsSubtab(1);
        }

        // 0 = Discover (default), 1 = Add by URL.
        private void SetFeedsSubtab(int sub)
        {
            DiscoverSubPanel.Visibility = sub == 0 ? Visibility.Visible : Visibility.Collapsed;
            ManualSubPanel.Visibility = sub == 1 ? Visibility.Visible : Visibility.Collapsed;

            FeedsDiscoverTab.Background = sub == 0 ? AccentBrush : InactiveTabBrush;
            FeedsManualTab.Background = sub == 1 ? AccentBrush : InactiveTabBrush;
        }

        private static Windows.UI.Xaml.Media.Brush AccentBrush
        {
            get { return (Windows.UI.Xaml.Media.Brush)Application.Current.Resources["AppAccentBrush"]; }
        }

        private static readonly Windows.UI.Xaml.Media.Brush InactiveNavBrush =
            new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.ColorHelper.FromArgb(0xFF, 0x11, 0x11, 0x11));

        private static readonly Windows.UI.Xaml.Media.Brush InactiveTabBrush =
            new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.ColorHelper.FromArgb(0xFF, 0x33, 0x33, 0x33));

        // ==================== Back button ====================

        private void OnBackRequested(object sender, BackRequestedEventArgs e)
        {
            if (ReaderOverlay.Visibility == Visibility.Visible)
            {
                CloseReader();
                e.Handled = true;
            }
            else if (_activeTab != 0)
            {
                SetActiveTab(0);
                e.Handled = true;
            }
        }
    }
}
