using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace YouTube
{
    sealed partial class App : Application
    {
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
            Debug.WriteLine("[App] Application initialized");
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Debug.WriteLine("[App] Application launched normally");
            InitializeRootFrame(e?.Arguments);
            Config.LoadUserToken();

            Frame rootFrame = Window.Current.Content as Frame;

            if (rootFrame == null)
            {
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;
                rootFrame.Navigated += OnNavigated;
                Window.Current.Content = rootFrame;
            }

            // Setup system back button behavior
            SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }
                Window.Current.Activate();
            }

            UpdateBackButtonVisibility();
        }

        private void OnNavigated(object sender, NavigationEventArgs e)
        {
            UpdateBackButtonVisibility();
        }

        private void OnBackRequested(object sender, BackRequestedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;
            
            if (rootFrame != null && rootFrame.CanGoBack)
            {
                e.Handled = true;
                rootFrame.GoBack();
            }
        }

        private void UpdateBackButtonVisibility()
        {
            Frame rootFrame = Window.Current.Content as Frame;
            
            if (rootFrame != null)
            {
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = 
                    rootFrame.CanGoBack ? 
                    AppViewBackButtonVisibility.Visible : 
                    AppViewBackButtonVisibility.Collapsed;
            }
        }

        protected override void OnActivated(IActivatedEventArgs args)
        {
            Debug.WriteLine($"[App] Application activated with kind: {args.Kind}");

            string extractedVideoId = null;

            if (args.Kind == ActivationKind.Protocol)
            {
                try
                {
                    var protocolArgs = args as ProtocolActivatedEventArgs;
                    if (protocolArgs?.Uri != null)
                    {
                        Debug.WriteLine($"[App] Received URI: {protocolArgs.Uri}");
                        extractedVideoId = ExtractYouTubeVideoId(protocolArgs.Uri.AbsoluteUri);
                        Debug.WriteLine($"[App] Extracted Video ID: {extractedVideoId}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[App] Error during activation: {ex}");
                }
            }

            InitializeRootFrame(extractedVideoId);
            base.OnActivated(args);
        }

        private string ExtractYouTubeVideoId(string url)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url))
                {
                    Debug.WriteLine("[App] Empty URL received");
                    return null;
                }

                // Улучшенное регулярное выражение для всех форматов YouTube URL
                var pattern = @"(?:youtube\.com\/(?:[^\/]+\/.+\/|(?:v|e(?:mbed)?)\/|.*[?&]v=)|youtu\.be\/)([^""&?\/\s]{11})";
                var match = Regex.Match(url, pattern, RegexOptions.IgnoreCase);

                if (!match.Success)
                {
                    Debug.WriteLine($"[App] No video ID found in URL: {url}");
                    return null;
                }

                return match.Groups[1].Value;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[App] Error extracting video ID: {ex}");
                return null;
            }
        }

        private void InitializeRootFrame(object navigationParameter)
        {
            try
            {
                Frame rootFrame = Window.Current.Content as Frame;

                if (rootFrame == null)
                {
                    Debug.WriteLine("[App] Creating new root frame");
                    rootFrame = new Frame();
                    rootFrame.NavigationFailed += OnNavigationFailed;
                    rootFrame.Navigated += OnNavigated;
                    Window.Current.Content = rootFrame;
                }

                // Setup system back button behavior
                SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;

                if (rootFrame.Content == null)
                {
                    if (navigationParameter is string && !string.IsNullOrEmpty(navigationParameter as string))
                    {
                        Debug.WriteLine($"[App] Navigating to Video page with ID: {navigationParameter}");
                        rootFrame.Navigate(typeof(Video), navigationParameter);
                    }
                    else
                    {
                        Debug.WriteLine("[App] Navigating to Main page");
                        rootFrame.Navigate(typeof(MainPage));
                    }
                }
                else if (navigationParameter is string && !string.IsNullOrEmpty(navigationParameter as string))
                {
                    // Если приложение уже запущено, но получен новый videoId
                    Debug.WriteLine($"[App] Reloading with new Video ID: {navigationParameter}");
                    rootFrame.Navigate(typeof(Video), navigationParameter);
                }

                UpdateBackButtonVisibility();
                Window.Current.Activate();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[App] Error initializing root frame: {ex}");
                throw;
            }
        }

        private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            Debug.WriteLine($"[App] Navigation failed: {e.Exception}");
            throw new Exception($"Failed to load Page {e.SourcePageType.FullName}", e.Exception);
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            Debug.WriteLine("[App] Application suspending");
            var deferral = e.SuspendingOperation.GetDeferral();
            deferral.Complete();
        }
    }
}