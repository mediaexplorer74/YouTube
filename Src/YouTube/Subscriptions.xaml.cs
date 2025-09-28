using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using YouTube.Models;

namespace YouTube
{
    public sealed partial class Subscriptions : Page
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private string _apiBaseUrl = Config.ApiBaseUrl;
        private int _subscriptionsPage = 0;
        private const int SUBSCRIPTIONS_PER_PAGE = 20;

        public ObservableCollection<VideoInfo> SubscriptionVideos { get; set; } = new ObservableCollection<VideoInfo>();

        private Frame _rootFrame;

        public Subscriptions()
        {
            this.InitializeComponent();
            _rootFrame = Window.Current.Content as Frame;

            NavigationManager.InitializeTabBarNavigation(tabbar, _rootFrame);
            NavigationManager.InitializeNavBarNavigation(navbar, _rootFrame);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await LoadSubscriptionVideos();
        }

        private async Task LoadSubscriptionVideos()
        {
            try
            {
                LoadingRing.IsActive = true;
                LoadingRing.Visibility = Visibility.Visible;

                var localSettings = ApplicationData.Current.LocalSettings;
                if (!localSettings.Values.ContainsKey("YouTubeApiKey"))
                {
                    ShowErrorDialog("YouTube API Key is missing");
                    LoadingRing.IsActive = false;
                    LoadingRing.Visibility = Visibility.Collapsed;
                    return;
                }

                string apiKey = localSettings.Values["YouTubeApiKey"].ToString();
                //string subscriptionsUrl = $"{_apiBaseUrl}get_subscriptions_videos.php?" +
                string subscriptionsUrl = $"{_apiBaseUrl}get_recommendations.php?" +
                    $"page={_subscriptionsPage}&" +
                    $"apikey={apiKey}&" +
                    $"token={Uri.EscapeDataString(Config.UserToken)}&" + 
                    $"count=10";

                var response = await _httpClient.GetStringAsync(subscriptionsUrl);
                var videos = JsonConvert.DeserializeObject<List<VideoInfo>>(response);

                if (videos != null)
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        foreach (var video in videos)
                        {
                            SubscriptionVideos.Add(video);
                        }
                    });
                }
            }
            catch (HttpRequestException httpEx)
            {
                ShowErrorDialog("Network error loading subscriptions.");
            }
            catch (JsonException jsonEx)
            {
                ShowErrorDialog("Error parsing subscriptions data.");
            }
            catch (Exception ex)
            {
                ShowErrorDialog("Error loading subscriptions.");
            }
            finally
            {
                LoadingRing.IsActive = false;
                LoadingRing.Visibility = Visibility.Collapsed;
            }
        }

        private async void ShowErrorDialog(string message)
        {
            /*await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                var dialog = new ContentDialog
                {
                    Title = "Error",
                    Content = message,
                    PrimaryButtonText = "OK"
                };
                await dialog.ShowAsync();
            });*/
            Debug.WriteLine("[ex] Subscriptions exception: " + message);
        }

        private void VideoCard_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var videoId = button?.Tag as string;

            if (!string.IsNullOrEmpty(videoId))
            {
                _rootFrame.Navigate(typeof(Video), videoId);
            }
        }

        private void SubscriptionsGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var video = e.ClickedItem as VideoInfo;
            if (video != null && !string.IsNullOrEmpty(video.video_id))
            {
                _rootFrame.Navigate(typeof(Video), video.video_id);
            }
        }
    }
}