using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Core;
using System.Threading.Tasks;
using YouTube.Models;

namespace YouTube
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class History : Page
    {
        private Frame _frame;

        public History()
        {
            this.InitializeComponent();
            _frame = Window.Current.Content as Frame;

            // Initialize navigation
            NavigationManager.InitializeTabBarNavigation(tabbar, _frame);
            NavigationManager.InitializeNavBarNavigation(navbar, _frame);

            this.Loaded += History_Loaded;
            this.Unloaded += History_Unloaded;
        }

        private void History_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Register for back button press
                SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
                UpdateBackButtonVisibility();
                LoadHistory();
            }
            catch (Exception)
            {
                // Handle any initialization errors silently
            }
        }

        private void History_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Unregister from back button press
                SystemNavigationManager.GetForCurrentView().BackRequested -= OnBackRequested;
            }
            catch (Exception)
            {
                // Handle any cleanup errors silently
            }
        }

        private void OnBackRequested(object sender, BackRequestedEventArgs e)
        {
            if (_frame != null && _frame.CanGoBack)
            {
                e.Handled = true;
                _frame.GoBack();
            }
        }

        private void UpdateBackButtonVisibility()
        {
            try
            {
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = 
                    _frame != null && _frame.CanGoBack ? AppViewBackButtonVisibility.Visible : AppViewBackButtonVisibility.Collapsed;
            }
            catch (Exception)
            {
                // Handle any errors silently
            }
        }

        private async void LoadHistory()
        {
            try
            {
                var history = await ViewHistory.GetHistory();
                if (HistoryContainer != null)
                {
                    HistoryContainer.ItemsSource = history;
                }
            }
            catch (Exception)
            {
                // Handle any errors silently
            }
        }

        private void VideoCard_Click(object sender, ItemClickEventArgs e)
        {
            try
            {
                var videoInfo = e.ClickedItem as VideoInfo;
                if (videoInfo != null && !string.IsNullOrEmpty(videoInfo.video_id))
                {
                    _frame.Navigate(typeof(Video), videoInfo.video_id);
                }
            }
            catch (Exception)
            {
                // Handle error silently
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            UpdateBackButtonVisibility();
            LoadHistory();
        }
    }
}
