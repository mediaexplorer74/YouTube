using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Net.Http;
using Newtonsoft.Json;
using System.Threading.Tasks;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.UI.Core;

namespace YouTube
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Channel : Page
    {
        private string channelName;
        private HttpClient httpClient;
        private Frame _frame;

        public Channel()
        {
            this.InitializeComponent();
            httpClient = new HttpClient();
            _frame = Window.Current.Content as Frame;

            // Subscribe to back button press
            SystemNavigationManager.GetForCurrentView().BackRequested += Channel_BackRequested;

            // Initialize navigation
            NavigationManager.InitializeTabBarNavigation(tabbar, _frame);
            NavigationManager.InitializeNavBarNavigation(navbar, _frame);
        }

        private void Channel_BackRequested(object sender, BackRequestedEventArgs e)
        {
            if (_frame.CanGoBack)
            {
                e.Handled = true;
                _frame.GoBack();
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            // Unsubscribe from back button press
            SystemNavigationManager.GetForCurrentView().BackRequested -= Channel_BackRequested;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter != null)
            {
                channelName = e.Parameter.ToString();
                await LoadChannelData();
            }
        }

        private async Task LoadChannelData()
        {
            try
            {
                LoadingGrid.Visibility = Visibility.Visible;
                MainContent.Visibility = Visibility.Collapsed;

                // Get saved API key
                var localSettings = ApplicationData.Current.LocalSettings;
                if (!localSettings.Values.ContainsKey("YouTubeApiKey"))
                {
                    System.Diagnostics.Debug.WriteLine("YouTube API Key is missing");
                    return;
                }

                string apiKey = localSettings.Values["YouTubeApiKey"].ToString();
                string apiUrl = $"{Config.ApiBaseUrl}get_author_videos.php?author={Uri.EscapeDataString(channelName)}&apikey={apiKey}";
                
                // Debug output
                System.Diagnostics.Debug.WriteLine($"Channel API Request URL: {apiUrl}");
                System.Diagnostics.Debug.WriteLine($"Channel Name: {channelName}");
                System.Diagnostics.Debug.WriteLine($"API Key: {apiKey}");

                string response = await httpClient.GetStringAsync(apiUrl);
                System.Diagnostics.Debug.WriteLine($"API Response: {response}");

                var data = JsonConvert.DeserializeObject<ChannelResponse>(response);

                if (data != null && data.channel_info != null)
                {
                    // Update channel info
                    ChannelTitle.Text = data.channel_info.title;
                    ChannelStats.Text = $"{data.channel_info.subscriber_count} subscribers • {data.channel_info.video_count} videos";
                    ChannelDescription.Text = data.channel_info.description;

                    // Load channel images
                    if (!string.IsNullOrEmpty(data.channel_info.thumbnail))
                    {
                        ChannelIcon.Source = new BitmapImage(new Uri(data.channel_info.thumbnail));
                    }
                    if (!string.IsNullOrEmpty(data.channel_info.banner))
                    {
                        ChannelBanner.Source = new BitmapImage(new Uri(data.channel_info.banner));
                    }

                    // Update videos list
                    VideosGrid.ItemsSource = data.videos;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Failed to parse channel data or channel_info is null");
                }

                // After loading is complete
                LoadingGrid.Visibility = Visibility.Collapsed;
                MainContent.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                // Handle error
                LoadingGrid.Visibility = Visibility.Collapsed;
                MainContent.Visibility = Visibility.Visible;
                System.Diagnostics.Debug.WriteLine($"Error loading channel data: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        private void VideosListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var video = e.ClickedItem as VideoItem;
            if (video != null)
            {
                _frame.Navigate(typeof(Video), video.video_id);
            }
        }
    }

    public class ChannelResponse
    {
        public ChannelInfo channel_info { get; set; }
        public List<VideoItem> videos { get; set; }
    }

    public class ChannelInfo
    {
        public string title { get; set; }
        public string description { get; set; }
        public string thumbnail { get; set; }
        public string banner { get; set; }
        public string subscriber_count { get; set; }
        public string video_count { get; set; }
    }
}
