using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Background;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace DayView
{
    sealed partial class App : Application
    {
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            if (rootFrame == null)
            {
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;
                Window.Current.Content = rootFrame;
            }

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }
                Window.Current.Activate();

                // Keep the live tile refreshing in the background.
                RegisterLiveTileTask();
            }
        }

        private async void RegisterLiveTileTask()
        {
            try
            {
                var data = new Services.DataService();
                if (data.LiveTileEnabled)
                    await Services.LiveTileTask.RegisterAsync(data.RefreshIntervalMinutes);
                else
                    Services.LiveTileTask.Unregister();
            }
            catch
            {
                // Background registration failing should never block launch.
            }
        }

        protected override async void OnBackgroundActivated(BackgroundActivatedEventArgs args)
        {
            base.OnBackgroundActivated(args);

            var taskInstance = args.TaskInstance;
            if (taskInstance == null || taskInstance.Task == null ||
                taskInstance.Task.Name != Services.LiveTileTask.TaskName)
            {
                return;
            }

            var deferral = taskInstance.GetDeferral();
            try
            {
                await Services.LiveTileTask.RunAsync();
            }
            catch
            {
                // Never crash the background host on a fetch error.
            }
            finally
            {
                deferral.Complete();
            }
        }

        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            deferral.Complete();
        }
    }
}
