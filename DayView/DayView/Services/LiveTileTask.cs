using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;

namespace DayView.Services
{
    // Registers and runs an in-process background task that periodically refreshes
    // the live tile with the latest headline, even while the app is not running.
    public static class LiveTileTask
    {
        public const string TaskName = "DayView.LiveTileUpdate";

        // The OS enforces a 15-minute minimum for TimeTrigger.
        private const uint MinimumIntervalMinutes = 15;

        // Registers the periodic tile-refresh task (idempotent). Call on launch.
        public static async Task RegisterAsync(int intervalMinutes)
        {
            // Background activity must be granted before registering a trigger.
            var access = await BackgroundExecutionManager.RequestAccessAsync();
            if (access == BackgroundAccessStatus.DeniedBySystemPolicy ||
                access == BackgroundAccessStatus.DeniedByUser)
            {
                return;
            }

            uint minutes = (uint)Math.Max(intervalMinutes, (int)MinimumIntervalMinutes);

            // Re-register if it already exists so interval changes take effect.
            foreach (var existing in BackgroundTaskRegistration.AllTasks.Values)
            {
                if (existing.Name == TaskName)
                    existing.Unregister(true);
            }

            var builder = new BackgroundTaskBuilder();
            builder.Name = TaskName;
            builder.SetTrigger(new TimeTrigger(minutes, false));
            builder.AddCondition(new SystemCondition(SystemConditionType.InternetAvailable));
            // No TaskEntryPoint: this is an in-process task handled in OnBackgroundActivated.
            builder.Register();
        }

        // Removes the periodic tile-refresh task.
        public static void Unregister()
        {
            foreach (var existing in BackgroundTaskRegistration.AllTasks.Values)
            {
                if (existing.Name == TaskName)
                    existing.Unregister(true);
            }
        }

        // Fetches the aggregated feed and updates the tile. Called from
        // App.OnBackgroundActivated.
        public static async Task RunAsync()
        {
            var data = new DataService();
            await data.LoadAsync();
            if (!data.LiveTileEnabled)
            {
                TileService.Clear();
                return;
            }
            if (data.Feeds.Count == 0)
            {
                TileService.UpdateLatest(null);
                return;
            }

            var feed = new FeedService();
            var articles = await feed.FetchAggregatedAsync(data.Feeds);
            TileService.UpdateLatest(articles != null && articles.Count > 0 ? articles[0] : null);
        }
    }
}
