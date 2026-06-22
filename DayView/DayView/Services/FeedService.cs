using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Web.Syndication;
using DayView.Models;

namespace DayView.Services
{
    /// <summary>
    /// Fetches and parses RSS/Atom feeds using the built-in SyndicationClient,
    /// extracting a lead image where the feed provides one. Also merges multiple
    /// feeds into a single, date-sorted timeline for the aggregated "All" view.
    /// </summary>
    public class FeedService
    {
        // Media RSS namespace (media:content / media:thumbnail).
        private const string MediaNs = "http://search.yahoo.com/mrss/";
        // content:encoded namespace.
        private const string ContentNs = "http://purl.org/rss/1.0/modules/content/";

        private static readonly Regex ImgSrcRegex = new Regex(
            "<img[^>]+src\\s*=\\s*[\"']([^\"']+)[\"']",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex TagRegex = new Regex("<[^>]+>", RegexOptions.Compiled);

        private readonly SyndicationClient _client = new SyndicationClient();

        public FeedService()
        {
            _client.BypassCacheOnRetrieve = true;
            _client.SetRequestHeader("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) DayView/2.0");
        }

        /// <summary>
        /// Fetches a single feed. Throws on network/parse errors so the caller can
        /// surface a message. The feed's own title is used when feedTitle is empty.
        /// </summary>
        public async Task<List<Article>> FetchFeedAsync(string url, string feedTitle)
        {
            var feed = await _client.RetrieveFeedAsync(new Uri(url));
            string resolvedTitle = !string.IsNullOrEmpty(feedTitle)
                ? feedTitle
                : (feed.Title != null ? feed.Title.Text : url);

            var articles = new List<Article>();
            foreach (var item in feed.Items)
            {
                articles.Add(BuildArticle(item, resolvedTitle));
            }
            return articles;
        }

        /// <summary>
        /// Fetches several feeds, merges them and sorts newest-first. Feeds that
        /// fail are skipped so one broken feed doesn't blank the whole timeline.
        /// </summary>
        public async Task<List<Article>> FetchAggregatedAsync(IEnumerable<FeedSource> feeds)
        {
            var tasks = feeds.Select(async f =>
            {
                try
                {
                    return await FetchFeedAsync(f.Url, f.Title);
                }
                catch
                {
                    return new List<Article>();
                }
            });

            var results = await Task.WhenAll(tasks);
            return results
                .SelectMany(r => r)
                .OrderByDescending(a => a.PublishedDate)
                .ToList();
        }

        // ==================== Item parsing ====================

        private Article BuildArticle(SyndicationItem item, string feedTitle)
        {
            string title = item.Title != null ? item.Title.Text : "(untitled)";

            // Body: prefer content:encoded, fall back to summary/description.
            string contentHtml = GetContentEncoded(item);
            string summaryHtml = item.Summary != null ? item.Summary.Text : null;
            if (string.IsNullOrEmpty(contentHtml))
                contentHtml = summaryHtml;

            string link = GetLink(item);
            DateTimeOffset published = GetPublishedDate(item);
            string imageUrl = ExtractImage(item, contentHtml, summaryHtml);
            string summaryText = MakeSummaryText(summaryHtml ?? contentHtml);

            return new Article
            {
                Title = title,
                FeedTitle = feedTitle,
                PublishedDate = published,
                Summary = summaryText,
                ContentHtml = contentHtml,
                Link = link,
                ImageUrl = imageUrl
            };
        }

        private string GetContentEncoded(SyndicationItem item)
        {
            foreach (var ext in item.ElementExtensions)
            {
                if (ext.NodeName == "encoded" && ext.NodeNamespace == ContentNs)
                    return ext.NodeValue;
            }
            return null;
        }

        private string GetLink(SyndicationItem item)
        {
            if (item.Links != null)
            {
                // Prefer the canonical "alternate" web link.
                foreach (var l in item.Links)
                {
                    if (l.Relationship == "alternate" && l.Uri != null)
                        return l.Uri.ToString();
                }
                foreach (var l in item.Links)
                {
                    if (l.Uri != null && l.Relationship != "enclosure")
                        return l.Uri.ToString();
                }
            }
            if (item.Id != null && item.Id.StartsWith("http"))
                return item.Id;
            return null;
        }

        private DateTimeOffset GetPublishedDate(SyndicationItem item)
        {
            if (item.PublishedDate != DateTimeOffset.MinValue)
                return item.PublishedDate;
            if (item.LastUpdatedTime != DateTimeOffset.MinValue)
                return item.LastUpdatedTime;
            return DateTimeOffset.Now;
        }

        /// <summary>
        /// Image-extraction ladder:
        /// 1) media:content / media:thumbnail (Media RSS)
        /// 2) an image enclosure link
        /// 3) first &lt;img&gt; in content:encoded
        /// 4) first &lt;img&gt; in the description/summary
        /// Returns null when the feed carries no image (text-only layout).
        /// </summary>
        private string ExtractImage(SyndicationItem item, string contentHtml, string summaryHtml)
        {
            string fromMedia = GetMediaImage(item);
            if (!string.IsNullOrEmpty(fromMedia)) return fromMedia;

            string fromEnclosure = GetEnclosureImage(item);
            if (!string.IsNullOrEmpty(fromEnclosure)) return fromEnclosure;

            string fromContent = FirstImgSrc(contentHtml);
            if (!string.IsNullOrEmpty(fromContent)) return fromContent;

            string fromSummary = FirstImgSrc(summaryHtml);
            if (!string.IsNullOrEmpty(fromSummary)) return fromSummary;

            return null;
        }

        private string GetMediaImage(SyndicationItem item)
        {
            foreach (var ext in item.ElementExtensions)
            {
                if (ext.NodeNamespace != MediaNs) continue;
                if (ext.NodeName != "content" && ext.NodeName != "thumbnail") continue;

                // Read the attributes into a name->value map. The attribute element
                // type (ISyndicationAttribute) can't be named here, so iterate with var.
                string url = null, type = null, medium = null;
                foreach (var attr in ext.AttributeExtensions)
                {
                    if (attr.Name == "url") url = attr.Value;
                    else if (attr.Name == "type") type = attr.Value;
                    else if (attr.Name == "medium") medium = attr.Value;
                }
                if (string.IsNullOrEmpty(url)) continue;

                // For media:content, ignore non-image media (e.g. video).
                if (ext.NodeName == "content")
                {
                    if (!string.IsNullOrEmpty(type) && !type.StartsWith("image", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (string.IsNullOrEmpty(type) && !string.IsNullOrEmpty(medium) &&
                        !medium.Equals("image", StringComparison.OrdinalIgnoreCase))
                        continue;
                }
                return url;
            }
            return null;
        }

        private string GetEnclosureImage(SyndicationItem item)
        {
            if (item.Links == null) return null;
            foreach (var l in item.Links)
            {
                if (l.Relationship == "enclosure" && l.Uri != null)
                {
                    string mediaType = l.MediaType ?? "";
                    if (mediaType.StartsWith("image", StringComparison.OrdinalIgnoreCase))
                        return l.Uri.ToString();
                }
            }
            return null;
        }

        private static string FirstImgSrc(string html)
        {
            if (string.IsNullOrEmpty(html)) return null;
            var m = ImgSrcRegex.Match(html);
            return m.Success ? WebUtilityDecode(m.Groups[1].Value) : null;
        }

        private static string MakeSummaryText(string html)
        {
            if (string.IsNullOrEmpty(html)) return "";
            string text = TagRegex.Replace(html, " ");
            text = WebUtilityDecode(text);
            text = Regex.Replace(text, "\\s+", " ").Trim();
            if (text.Length > 300)
                text = text.Substring(0, 300).TrimEnd() + "…";
            return text;
        }

        private static string WebUtilityDecode(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return System.Net.WebUtility.HtmlDecode(s);
        }
    }
}
