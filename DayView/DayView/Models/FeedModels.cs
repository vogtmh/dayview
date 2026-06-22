using System;
using System.Runtime.Serialization;

namespace DayView.Models
{
    /// <summary>
    /// A subscribed RSS/Atom feed. Persisted as JSON in the app's LocalFolder.
    /// </summary>
    [DataContract]
    public class FeedSource
    {
        [DataMember]
        public string Title { get; set; }

        [DataMember]
        public string Url { get; set; }

        // Optional feed icon (from discovery). Not all feeds provide one.
        [DataMember]
        public string IconUrl { get; set; }
    }

    /// <summary>
    /// A single article parsed from a feed. Built at runtime, not persisted.
    /// Implements INotifyPropertyChanged so the UI can react if values change.
    /// </summary>
    public class Article : System.ComponentModel.INotifyPropertyChanged
    {
        public string Title { get; set; }

        // Title of the feed this article came from (shown in the aggregated view).
        public string FeedTitle { get; set; }

        public DateTimeOffset PublishedDate { get; set; }

        // Short plain-text summary shown in the list.
        public string Summary { get; set; }

        // Full HTML content for the in-app reader (content:encoded when available,
        // otherwise the description/summary).
        public string ContentHtml { get; set; }

        // Canonical article link, opened in the browser as a fallback.
        public string Link { get; set; }

        // Extracted lead image URL. Empty/null when the feed has no image.
        public string ImageUrl { get; set; }

        // German-style date string (DD.MM.YYYY HH:MM) for the list, like the old app.
        public string DisplayDate
        {
            get { return PublishedDate.LocalDateTime.ToString("dd.MM.yyyy HH:mm"); }
        }

        // "Feed name, DD.MM.YYYY HH:MM" footer line.
        public string FeedAndDate
        {
            get
            {
                return string.IsNullOrEmpty(FeedTitle)
                    ? DisplayDate
                    : FeedTitle + ", " + DisplayDate;
            }
        }

        // Image column collapses to text-only when the feed provides no image.
        public Windows.UI.Xaml.Visibility ImageVisibility
        {
            get
            {
                return string.IsNullOrEmpty(ImageUrl)
                    ? Windows.UI.Xaml.Visibility.Collapsed
                    : Windows.UI.Xaml.Visibility.Visible;
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>
    /// A feed found via keyword discovery (Feedly search API). Not persisted.
    /// </summary>
    public class FeedSearchResult : System.ComponentModel.INotifyPropertyChanged
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public string Website { get; set; }
        public string Description { get; set; }
        public string IconUrl { get; set; }
        public long Subscribers { get; set; }

        private bool _isSubscribed;

        // Whether the user already follows this feed. Drives the Add button visuals.
        public bool IsSubscribed
        {
            get { return _isSubscribed; }
            set
            {
                if (_isSubscribed == value) return;
                _isSubscribed = value;
                Raise("IsSubscribed");
                Raise("AddButtonContent");
                Raise("AddButtonBackground");
                Raise("AddButtonEnabled");
            }
        }

        // Segoe MDL2 Assets glyphs: Add when not following, CheckMark once added.
        public string AddButtonContent
        {
            get { return _isSubscribed ? "\uE73E" : "\uE710"; }
        }

        public Windows.UI.Xaml.Media.Brush AddButtonBackground
        {
            get
            {
                return _isSubscribed
                    ? new Windows.UI.Xaml.Media.SolidColorBrush(
                        Windows.UI.ColorHelper.FromArgb(0xFF, 0x4C, 0xD9, 0x64))
                    : new Windows.UI.Xaml.Media.SolidColorBrush(
                        Windows.UI.ColorHelper.FromArgb(0xFF, 0x33, 0x33, 0x33));
            }
        }

        public bool AddButtonEnabled
        {
            get { return !_isSubscribed; }
        }

        public string SubscribersText
        {
            get
            {
                if (Subscribers <= 0) return "";
                return Subscribers.ToString("N0") + " subscribers";
            }
        }

        public Windows.UI.Xaml.Visibility IconVisibility
        {
            get
            {
                return string.IsNullOrEmpty(IconUrl)
                    ? Windows.UI.Xaml.Visibility.Collapsed
                    : Windows.UI.Xaml.Visibility.Visible;
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        private void Raise(string name)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new System.ComponentModel.PropertyChangedEventArgs(name));
        }
    }
}
