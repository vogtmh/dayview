using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Web.Http;
using DayView.Models;

namespace DayView.Services
{
    /// <summary>
    /// Discovers feeds two ways:
    /// 1) Keyword search via the public Feedly Cloud search API (no signup/token).
    /// 2) Auto-discovery from a website URL by reading its &lt;link rel="alternate"&gt;
    ///    feed declarations (used when the user pastes a site URL rather than a feed URL).
    /// </summary>
    public class FeedDiscoveryService
    {
        private const string SearchUrl = "https://cloud.feedly.com/v3/search/feeds?count=25&query=";

        private static readonly Regex LinkTagRegex = new Regex(
            "<link\\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex HrefRegex = new Regex(
            "href\\s*=\\s*[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex TitleAttrRegex = new Regex(
            "title\\s*=\\s*[\"']([^\"']*)[\"']", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly HttpClient _http = new HttpClient();

        public FeedDiscoveryService()
        {
            _http.DefaultRequestHeaders.UserAgent.TryParseAdd("DayView/2.0");
        }

        /// <summary>
        /// Keyword search. Returns an empty list on error rather than throwing.
        /// </summary>
        public async Task<List<FeedSearchResult>> SearchAsync(string query)
        {
            var results = new List<FeedSearchResult>();
            if (string.IsNullOrWhiteSpace(query)) return results;

            try
            {
                var uri = new Uri(SearchUrl + Uri.EscapeDataString(query.Trim()));
                string json = await _http.GetStringAsync(uri);

                JsonObject root;
                if (!JsonObject.TryParse(json, out root)) return results;
                if (!root.ContainsKey("results")) return results;

                var arr = root.GetNamedArray("results");
                foreach (var entry in arr)
                {
                    var obj = entry.GetObject();

                    string feedId = GetString(obj, "feedId");
                    if (string.IsNullOrEmpty(feedId))
                        feedId = GetString(obj, "id");

                    string feedUrl = StripFeedPrefix(feedId);
                    if (string.IsNullOrEmpty(feedUrl)) continue;

                    results.Add(new FeedSearchResult
                    {
                        Title = GetString(obj, "title"),
                        Url = feedUrl,
                        Website = GetString(obj, "website"),
                        Description = GetString(obj, "description"),
                        IconUrl = GetString(obj, "iconUrl"),
                        Subscribers = GetNumber(obj, "subscribers")
                    });
                }
            }
            catch
            {
                // Network/parse failure -> empty result list.
            }
            return results;
        }

        /// <summary>
        /// Resolves a user-entered URL into a feed. If the URL is already a feed it is
        /// returned as-is; otherwise the page is fetched and its declared feed link is
        /// extracted. Returns null when nothing usable is found.
        /// </summary>
        public async Task<FeedSource> ResolveFeedAsync(string inputUrl)
        {
            if (string.IsNullOrWhiteSpace(inputUrl)) return null;

            string url = inputUrl.Trim();
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }

            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri)) return null;

            string body;
            try
            {
                body = await _http.GetStringAsync(uri);
            }
            catch
            {
                return null;
            }

            // If the response is itself a feed, accept the URL directly.
            if (LooksLikeFeed(body))
            {
                return new FeedSource { Title = ExtractFeedTitle(body) ?? uri.Host, Url = url };
            }

            // Otherwise scan the HTML for a declared feed link.
            var discovered = ExtractFeedLink(body, uri);
            return discovered;
        }

        // ==================== Helpers ====================

        private static string StripFeedPrefix(string feedId)
        {
            if (string.IsNullOrEmpty(feedId)) return null;
            const string prefix = "feed/";
            if (feedId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return feedId.Substring(prefix.Length);
            return feedId;
        }

        private static bool LooksLikeFeed(string body)
        {
            if (string.IsNullOrEmpty(body)) return false;
            string head = body.Length > 1000 ? body.Substring(0, 1000) : body;
            return head.IndexOf("<rss", StringComparison.OrdinalIgnoreCase) >= 0
                || head.IndexOf("<feed", StringComparison.OrdinalIgnoreCase) >= 0
                || head.IndexOf("<rdf:RDF", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ExtractFeedTitle(string body)
        {
            var m = Regex.Match(body, "<title[^>]*>(.*?)</title>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (m.Success)
            {
                string t = System.Net.WebUtility.HtmlDecode(m.Groups[1].Value).Trim();
                if (!string.IsNullOrEmpty(t)) return t;
            }
            return null;
        }

        private static FeedSource ExtractFeedLink(string html, Uri baseUri)
        {
            if (string.IsNullOrEmpty(html)) return null;

            foreach (Match linkTag in LinkTagRegex.Matches(html))
            {
                string tag = linkTag.Value;
                if (tag.IndexOf("alternate", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (tag.IndexOf("application/rss+xml", StringComparison.OrdinalIgnoreCase) < 0 &&
                    tag.IndexOf("application/atom+xml", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var href = HrefRegex.Match(tag);
                if (!href.Success) continue;

                string feedHref = System.Net.WebUtility.HtmlDecode(href.Groups[1].Value).Trim();
                Uri feedUri;
                if (!Uri.TryCreate(baseUri, feedHref, out feedUri)) continue;

                string title = null;
                var titleMatch = TitleAttrRegex.Match(tag);
                if (titleMatch.Success)
                    title = System.Net.WebUtility.HtmlDecode(titleMatch.Groups[1].Value).Trim();

                return new FeedSource
                {
                    Title = string.IsNullOrEmpty(title) ? baseUri.Host : title,
                    Url = feedUri.ToString()
                };
            }
            return null;
        }

        private static string GetString(JsonObject obj, string key)
        {
            try
            {
                if (obj.ContainsKey(key) && obj.GetNamedValue(key).ValueType == JsonValueType.String)
                    return obj.GetNamedString(key);
            }
            catch { }
            return null;
        }

        private static long GetNumber(JsonObject obj, string key)
        {
            try
            {
                if (obj.ContainsKey(key) && obj.GetNamedValue(key).ValueType == JsonValueType.Number)
                    return (long)obj.GetNamedNumber(key);
            }
            catch { }
            return 0;
        }
    }
}
