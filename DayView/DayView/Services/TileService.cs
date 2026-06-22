using System;
using System.Collections.Generic;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace DayView.Services
{
    // Updates the primary app tile with the latest article so it works as a
    // live tile on the Start screen. Uses adaptive tile templates so the text
    // shows on every tile size (small shows just the app name).
    public static class TileService
    {
        // Shows the latest headline on the live tile. A null/empty article
        // clears the tile back to its default logo.
        public static void UpdateLatest(Models.Article latest)
        {
            var updater = TileUpdateManager.CreateTileUpdaterForApplication();

            if (latest == null || string.IsNullOrWhiteSpace(latest.Title))
            {
                updater.Clear();
                return;
            }

            try
            {
                var xml = BuildTileXml(latest);
                updater.Update(new TileNotification(xml));
            }
            catch
            {
                // A bad tile payload should never crash the app.
            }
        }

        // Resets the tile back to its default logo.
        public static void Clear()
        {
            TileUpdateManager.CreateTileUpdaterForApplication().Clear();
        }

        private static XmlDocument BuildTileXml(Models.Article latest)
        {
            string title = Escape(latest.Title);
            string source = Escape(string.IsNullOrEmpty(latest.FeedTitle) ? "" : latest.FeedTitle);

            // Medium tile: headline only (no app name or source — not enough room).
            // Wide / large tiles: source label + wrapped headline.
            string sourceLine = string.IsNullOrEmpty(source)
                ? ""
                : "<text hint-style=\"captionSubtle\">" + source + "</text>";

            // Use the article image as a darkened background on the wide tile so
            // the headline stays readable on top of it.
            string wideBackground = string.IsNullOrEmpty(latest.ImageUrl)
                ? ""
                : "<image placement=\"background\" hint-overlay=\"60\" src=\"" + Escape(latest.ImageUrl) + "\" />";

            string payload =
                "<tile>" +
                  "<visual branding=\"name\">" +

                    "<binding template=\"TileMedium\" branding=\"none\">" +
                      "<text hint-style=\"body\" hint-wrap=\"true\" hint-maxLines=\"5\">" + title + "</text>" +
                    "</binding>" +

                    "<binding template=\"TileWide\">" +
                      wideBackground +
                      sourceLine +
                      "<text hint-style=\"base\" hint-wrap=\"true\" hint-maxLines=\"3\">" + title + "</text>" +
                    "</binding>" +

                    "<binding template=\"TileLarge\">" +
                      sourceLine +
                      "<text hint-style=\"subtitle\" hint-wrap=\"true\" hint-maxLines=\"6\">" + title + "</text>" +
                    "</binding>" +

                  "</visual>" +
                "</tile>";

            var doc = new XmlDocument();
            doc.LoadXml(payload);
            return doc;
        }

        private static string Escape(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }
    }
}
