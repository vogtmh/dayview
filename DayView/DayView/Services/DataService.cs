using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using DayView.Models;

namespace DayView.Services
{
    /// <summary>
    /// Persists the list of subscribed feeds as a JSON file in the app's LocalFolder,
    /// and small app settings in LocalSettings. Seeds the Tagesschau feed on first run.
    /// </summary>
    public class DataService
    {
        private const string FeedsFile = "feeds.json";
        private const string SeededKey = "feedsSeeded";
        private const string UseSystemAccentKey = "useSystemAccent";
        private const string RefreshIntervalKey = "refreshIntervalMinutes";

        // Default feed seeded on first launch (the old app's "alle" / all-news feed).
        private const string DefaultFeedUrl = "https://www.tagesschau.de/infoservices/alle-meldungen-100~rss2.xml";
        private const string DefaultFeedTitle = "tagesschau";

        private readonly SemaphoreSlim _saveLock = new SemaphoreSlim(1, 1);
        private readonly ApplicationDataContainer _localSettings = ApplicationData.Current.LocalSettings;

        public ObservableCollection<FeedSource> Feeds { get; private set; } = new ObservableCollection<FeedSource>();

        public async Task LoadAsync()
        {
            var feeds = await LoadListAsync<FeedSource>(FeedsFile);
            Feeds = new ObservableCollection<FeedSource>(feeds ?? new List<FeedSource>());

            // Seed the default Tagesschau feed exactly once, on first run.
            bool seeded = _localSettings.Values.ContainsKey(SeededKey) && (bool)_localSettings.Values[SeededKey];
            if (!seeded && Feeds.Count == 0)
            {
                Feeds.Add(new FeedSource { Title = DefaultFeedTitle, Url = DefaultFeedUrl });
                await SaveFeedsAsync();
                _localSettings.Values[SeededKey] = true;
            }
        }

        // ==================== Feeds ====================

        public bool IsSubscribed(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            foreach (var f in Feeds)
                if (string.Equals(f.Url, url, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public async Task<bool> AddFeedAsync(string title, string url, string iconUrl)
        {
            if (string.IsNullOrWhiteSpace(url) || IsSubscribed(url)) return false;
            Feeds.Add(new FeedSource
            {
                Title = string.IsNullOrWhiteSpace(title) ? url : title.Trim(),
                Url = url.Trim(),
                IconUrl = iconUrl
            });
            await SaveFeedsAsync();
            return true;
        }

        public async Task RemoveFeedAsync(string url)
        {
            for (int i = Feeds.Count - 1; i >= 0; i--)
                if (string.Equals(Feeds[i].Url, url, StringComparison.OrdinalIgnoreCase))
                    Feeds.RemoveAt(i);
            await SaveFeedsAsync();
        }

        public async Task SaveFeedsAsync()
        {
            await SaveListAsync(Feeds.ToList(), FeedsFile);
        }

        // ==================== Settings ====================

        public bool UseSystemAccent
        {
            get { return _localSettings.Values.ContainsKey(UseSystemAccentKey) && (bool)_localSettings.Values[UseSystemAccentKey]; }
            set { _localSettings.Values[UseSystemAccentKey] = value; }
        }

        public int RefreshIntervalMinutes
        {
            get
            {
                if (_localSettings.Values.ContainsKey(RefreshIntervalKey))
                    return (int)_localSettings.Values[RefreshIntervalKey];
                return 30;
            }
            set { _localSettings.Values[RefreshIntervalKey] = value; }
        }

        // ==================== Generic JSON persistence ====================

        private async Task<List<T>> LoadListAsync<T>(string fileName)
        {
            try
            {
                var folder = ApplicationData.Current.LocalFolder;
                var file = await folder.GetFileAsync(fileName);
                var json = await FileIO.ReadTextAsync(file);
                if (string.IsNullOrEmpty(json)) return new List<T>();
                var serializer = new DataContractJsonSerializer(typeof(List<T>));
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    return serializer.ReadObject(stream) as List<T> ?? new List<T>();
                }
            }
            catch (FileNotFoundException)
            {
                return new List<T>();
            }
            catch
            {
                return new List<T>();
            }
        }

        private async Task SaveListAsync<T>(List<T> list, string fileName)
        {
            await _saveLock.WaitAsync();
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(List<T>));
                using (var stream = new MemoryStream())
                {
                    serializer.WriteObject(stream, list);
                    var json = Encoding.UTF8.GetString(stream.ToArray());
                    var folder = ApplicationData.Current.LocalFolder;
                    var tempName = fileName + ".tmp";
                    var tempFile = await folder.CreateFileAsync(tempName, CreationCollisionOption.ReplaceExisting);
                    await FileIO.WriteTextAsync(tempFile, json);
                    await tempFile.RenameAsync(fileName, NameCollisionOption.ReplaceExisting);
                }
            }
            finally
            {
                _saveLock.Release();
            }
        }
    }
}
