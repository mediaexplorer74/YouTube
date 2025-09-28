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
    public sealed partial class Shorts : Page
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private string _apiBaseUrl = Config.ApiBaseUrl;
        private int _shortsPage = 0;
        private const int SHORTS_PER_PAGE = 20; // Assuming a page size for shorts

        public ObservableCollection<ShortsVideo> ShortsVideos { get; set; } = new ObservableCollection<ShortsVideo>();

        private Frame _rootFrame;

        public Shorts()
        {
            this.InitializeComponent();
            _rootFrame = Window.Current.Content as Frame;

            NavigationManager.InitializeTabBarNavigation(tabbar, _rootFrame);
            NavigationManager.InitializeNavBarNavigation(navbar, _rootFrame);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await LoadShortsVideos();
        }

        private async Task LoadShortsVideos()
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
                string shortsUrl = $"{_apiBaseUrl}get_shorts.php?page={_shortsPage}&apikey={apiKey}&token={Uri.EscapeDataString(Config.UserToken)}";

                var response = await _httpClient.GetStringAsync(shortsUrl);
                var shorts = JsonConvert.DeserializeObject<List<ShortsVideo>>(response);

                if (shorts != null)
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        foreach (var shortVideo in shorts)
                        {
                            ShortsVideos.Add(shortVideo);
                        }
                    });
                }
            }
            catch (HttpRequestException httpEx)
            {
                ShowErrorDialog("Network error loading Shorts.");
            }
            catch (JsonException jsonEx)
            {
                ShowErrorDialog("Error parsing Shorts data.");
            }
            catch (Exception ex)
            {
                ShowErrorDialog("Error loading Shorts.");
            }
            finally
            {
                LoadingRing.IsActive = false;
                LoadingRing.Visibility = Visibility.Collapsed;
            }
        }

        private async void ShowErrorDialog(string message)
        {

            /*await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                var dialog = new ContentDialog
                {
                    Title = "Error",
                    Content = message,
                    PrimaryButtonText = "OK"
                };
                dialog.ShowAsync();
            });*/
            Debug.WriteLine("[ex] Shorts exception: " + message);
        }

        private void ShortsVideoCard_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var videoId = button?.Tag as string;

            if (!string.IsNullOrEmpty(videoId))
            {
                _rootFrame.Navigate(typeof(Video), videoId);
            }
        }

        private void ShortsGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var shortVideo = e.ClickedItem as ShortsVideo;
            if (shortVideo != null && !string.IsNullOrEmpty(shortVideo.video_id))
            {
                _rootFrame.Navigate(typeof(Video), shortVideo.video_id);
            }
        }
    }
}