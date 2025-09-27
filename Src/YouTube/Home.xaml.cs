using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Newtonsoft.Json;
using Windows.UI.Xaml.Data;
using Windows.Storage;
using YouTube.Models;
using Windows.UI.Core;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Media;

namespace YouTube
{
    public class StringIsNullOrEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string s = value as string;
            return string.IsNullOrEmpty(s) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Home : Page
    {
        // TEMP / RnD / TODO
        private const string API_KEY_SETTING = "YouTubeApiKey";
        private ObservableCollection<VideoInfo> videos;
        private bool isLoading = false;
        private bool isLoadingMore = false;
        private string nextPageToken = "";
        private HashSet<string> loadedVideoIds;

        public Home()
        {
            this.InitializeComponent();
            videos = new ObservableCollection<VideoInfo>();
            loadedVideoIds = new HashSet<string>();
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            if (localSettings.Values.ContainsKey("EnableChannelThumbnails"))
                Config.EnableChannelThumbnails = (bool)localSettings.Values["EnableChannelThumbnails"];
            this.Loaded += Home_Loaded;
        }

        private async void Home_Loaded(object sender, RoutedEventArgs e)
        {
            await RefreshData();
        }

        private async void ScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (isLoadingMore || isLoading) return;

            var scrollViewer = sender as ScrollViewer;
            if (scrollViewer == null) return;

            // Check if we're near the bottom (within 100 pixels)
            if (scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - 100)
            {
                await LoadMoreVideos();
            }
        }

        private void UpdateUI(bool dataLoaded)
        {
            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;

            if (dataLoaded)
            {
                MainScrollViewer.Visibility = Visibility.Visible;
                ErrorPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                MainScrollViewer.Visibility = Visibility.Collapsed;
                ErrorPanel.Visibility = Visibility.Visible;
            }
        }

        public async Task RefreshData()
        {
            if (isLoading) return;
            isLoading = true;
            
            // Reset pagination state
            nextPageToken = "";
            loadedVideoIds.Clear();
            
            UpdateUI(true);
            LoadingRing.IsActive = true;
            LoadingRing.Visibility = Visibility.Visible;

            MainScrollViewer.Visibility = Visibility.Collapsed; // Hide content while loading
            ErrorPanel.Visibility = Visibility.Collapsed; // Hide error while loading

            // Set ItemsSource once if not already set
            if (VideosItemsControl.ItemsSource == null)
            {
                VideosItemsControl.ItemsSource = videos;
            }

            try
            {
                string url;

                // Проверяем наличие токена в Config
                if (!string.IsNullOrEmpty(Config.UserToken))
                {
                    // Используем endpoint для рекомендаций с токеном
                    url = Config.ApiBaseUrl + "get_recommendations.php?token=" + Uri.EscapeDataString(Config.UserToken);
                    System.Diagnostics.Debug.WriteLine("Using recommendations API with token");
                }
                else
                {
                    // Используем стандартный endpoint с API ключом
                    var localSettings = ApplicationData.Current.LocalSettings;
                    if (!localSettings.Values.ContainsKey(API_KEY_SETTING))
                    {
                        System.Diagnostics.Debug.WriteLine("API key is missing and no token available");
                        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            UpdateUI(false);
                        });
                        return;
                    }

                    string apiKey = localSettings.Values[API_KEY_SETTING].ToString();
                    url = Config.ApiBaseUrl + "get_top_videos.php?apikey=" + Uri.EscapeDataString(apiKey);
                    System.Diagnostics.Debug.WriteLine("Using top videos API with API key");
                }

                using (var client = new HttpClient())
                {
                    try
                    {
                        var response = await client.GetStringAsync(url);
                        var newVideos = JsonConvert.DeserializeObject<List<VideoInfo>>(response);
                        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            videos.Clear();
                            if (newVideos != null)
                            {
                                // Filter out videos we already have and add unique video IDs
                                foreach (var video in newVideos)
                                {
                                    if (!string.IsNullOrEmpty(video.video_id) && loadedVideoIds.Add(video.video_id))
                                    {
                                        videos.Add(video);
                                    }
                                }
                            }
                            UpdateUI(videos.Count > 0);

                            // Логируем источник данных
                            if (!string.IsNullOrEmpty(Config.UserToken))
                            {
                                System.Diagnostics.Debug.WriteLine($"Loaded {videos.Count} recommended videos using token");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine(Config.UserToken);
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error in RefreshData: {ex}");

                        // Если ошибка с токеном, попробуем использовать API ключ как fallback
                        if (!string.IsNullOrEmpty(Config.UserToken))
                        {
                            System.Diagnostics.Debug.WriteLine("Token request failed, trying API key fallback");

                            var localSettings = ApplicationData.Current.LocalSettings;
                            if (localSettings.Values.ContainsKey(API_KEY_SETTING))
                            {
                                string apiKey = localSettings.Values[API_KEY_SETTING].ToString();
                                string fallbackUrl = Config.ApiBaseUrl + "get_top_videos.php?apikey=" + Uri.EscapeDataString(apiKey);

                                try
                                {
                                    var fallbackResponse = await client.GetStringAsync(fallbackUrl);
                                    var fallbackVideos = JsonConvert.DeserializeObject<List<VideoInfo>>(fallbackResponse);

                                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                                    {
                                        videos.Clear();
                                        if (fallbackVideos != null)
                                        {
                                            // Filter out videos we already have and add unique video IDs
                                            foreach (var video in fallbackVideos)
                                            {
                                                if (!string.IsNullOrEmpty(video.video_id) && loadedVideoIds.Add(video.video_id))
                                                {
                                                    videos.Add(video);
                                                }
                                            }
                                        }
                                        UpdateUI(videos.Count > 0);
                                        System.Diagnostics.Debug.WriteLine($"Loaded {videos.Count} videos using API key fallback");
                                    });
                                    return;
                                }
                                catch (Exception fallbackEx)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Fallback also failed: {fallbackEx}");
                                }
                            }
                        }

                        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            UpdateUI(false);
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in RefreshData: {ex}");
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    UpdateUI(false);
                });
            }
            finally
            {
                isLoading = false;
            }
        }

        private async Task LoadMoreVideos()
        {
            if (isLoadingMore || isLoading) return;
            isLoadingMore = true;

            // Show bottom loading indicator
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                BottomLoadingPanel.Visibility = Visibility.Visible;
                BottomLoadingRing.IsActive = true;
            });

            try
            {
                string url;

                // Build URL with pagination if available
                if (!string.IsNullOrEmpty(Config.UserToken))
                {
                    url = Config.ApiBaseUrl + "get_recommendations.php?token=" + Uri.EscapeDataString(Config.UserToken);
                    if (!string.IsNullOrEmpty(nextPageToken))
                    {
                        url += "&pageToken=" + Uri.EscapeDataString(nextPageToken);
                    }
                }
                else
                {
                    var localSettings = ApplicationData.Current.LocalSettings;
                    if (!localSettings.Values.ContainsKey(API_KEY_SETTING))
                    {
                        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            BottomLoadingPanel.Visibility = Visibility.Collapsed;
                            BottomLoadingRing.IsActive = false;
                        });
                        return;
                    }

                    string apiKey = localSettings.Values[API_KEY_SETTING].ToString();
                    url = Config.ApiBaseUrl + "get_top_videos.php?apikey=" + Uri.EscapeDataString(apiKey);
                    if (!string.IsNullOrEmpty(nextPageToken))
                    {
                        url += "&pageToken=" + Uri.EscapeDataString(nextPageToken);
                    }
                }

                using (var client = new HttpClient())
                {
                    try
                    {
                        var response = await client.GetStringAsync(url);
                        var newVideos = JsonConvert.DeserializeObject<List<VideoInfo>>(response);

                        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            if (newVideos != null)
                            {
                                int addedCount = 0;
                                // Filter out videos we already have and add only new ones
                                foreach (var video in newVideos)
                                {
                                    if (!string.IsNullOrEmpty(video.video_id) && loadedVideoIds.Add(video.video_id))
                                    {
                                        videos.Add(video);
                                        addedCount++;
                                    }
                                }

                                // ObservableCollection automatically updates UI without resetting scroll position
                                System.Diagnostics.Debug.WriteLine($"Added {addedCount} new videos out of {newVideos.Count} received");
                            }

                            BottomLoadingPanel.Visibility = Visibility.Collapsed;
                            BottomLoadingRing.IsActive = false;
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error loading more videos: {ex}");
                        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            BottomLoadingPanel.Visibility = Visibility.Collapsed;
                            BottomLoadingRing.IsActive = false;
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in LoadMoreVideos: {ex}");
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    BottomLoadingPanel.Visibility = Visibility.Collapsed;
                    BottomLoadingRing.IsActive = false;
                });
            }
            finally
            {
                isLoadingMore = false;
            }
        }

        private void VideoCard_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                if (button?.Tag == null)
                {
                    System.Diagnostics.Debug.WriteLine("Button tag is null");
                    return;
                }
                
                var clickedVideo = button.Tag as VideoInfo;
                if (clickedVideo == null)
                {
                    System.Diagnostics.Debug.WriteLine("Failed to cast to VideoInfo");
                    return;
                }
                
                if (string.IsNullOrEmpty(clickedVideo.video_id))
                {
                    System.Diagnostics.Debug.WriteLine("video_id is null or empty");
                    return;
                }
                
                System.Diagnostics.Debug.WriteLine($"Navigating to video: {clickedVideo.video_id}");
                
                // Get the root frame
                var rootFrame = Window.Current.Content as Frame;
                if (rootFrame != null)
                {
                    rootFrame.Navigate(typeof(Video), clickedVideo.video_id);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Root frame is null");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in VideoCard_Click: {ex}");
                // Handle errors silently
            }
        }

        private async void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshData();
        }
    }
}
