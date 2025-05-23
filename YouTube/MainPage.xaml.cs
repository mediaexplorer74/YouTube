using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.System.Display;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using YouTube.Models;
using Windows.UI.Xaml.Media;
using Windows.Media.Core;
using Windows.UI.Xaml.Media.Animation;

namespace YouTube
{
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private List<VideoInfo> _allVideos = new List<VideoInfo>();
        private List<VideoInfo> _displayedVideos = new List<VideoInfo>();
        private int _currentPage = 0;
        private bool _isSearchInputFocused = false;
        private DisplayRequest _displayRequest;
        private string _apiBaseUrl = Config.ApiBaseUrl;
        private string _defaultQuality = Config.DefaultQuality;
        private readonly Settings _settings;
        private string _selectedCategoryId = "0"; // 0 means "All"
        private bool _isShowingDialog = false;
        private List<VideoInfo> _allRelatedVideos = new List<VideoInfo>();
        private List<VideoInfo> _displayedRelatedVideos = new List<VideoInfo>();
        private int _relatedVideosPage = 0;
        private const int RELATED_VIDEOS_PER_PAGE = 10;
        private string _currentVideoId;
        private List<VideoInfo> _relatedVideos = new List<VideoInfo>();
        private DispatcherTimer _skipOverlayTimer;
        private bool _isDoubleTapRight = true;

        // --- Author (Channel) page logic ---
        private class AuthorChannelInfo
        {
            public string title { get; set; }
            public string description { get; set; }
            public string thumbnail { get; set; }
            public string banner { get; set; }
            public string subscriber_count { get; set; }
            public string video_count { get; set; }
        }
        private class AuthorApiResponse
        {
            public AuthorChannelInfo channel_info { get; set; }
            public List<VideoInfo> videos { get; set; }
        }
        private string _currentAuthorName;
        private List<VideoInfo> _currentAuthorVideos = new List<VideoInfo>();

        // --- Search suggestions ---
        private List<string> _searchSuggestions = new List<string>();
        private async void SearchInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            var query = (sender as TextBox)?.Text;
            if (SuggestionsOverlay.Visibility == Visibility.Visible)
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    SuggestionsListBox.ItemsSource = null;
                    return;
                }
                try
                {
                    var url = GetApiUrlWithKey($"{ApiBaseUrl}get_search_suggestions.php?query={Uri.EscapeDataString(query)}");
                    var response = await _httpClient.GetStringAsync(url);
                    var data = JsonConvert.DeserializeObject<SearchSuggestionsResponse>(response);
                    _searchSuggestions = data?.suggestions?.Select(s => s[0]?.ToString())
                        .Where(s => !string.IsNullOrEmpty(s) && !s.Equals(query, StringComparison.OrdinalIgnoreCase))
                        .ToList() ?? new List<string>();
                    SuggestionsListBox.ItemsSource = _searchSuggestions;
                }
                catch
                {
                    SuggestionsListBox.ItemsSource = null;
                }
            }
        }
        private void SearchSuggestionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SuggestionsOverlay.Visibility == Visibility.Visible)
            {
                var suggestion = SuggestionsListBox.SelectedItem as string;
                if (!string.IsNullOrEmpty(suggestion))
                {
                    SuggestionsOverlay.Visibility = Visibility.Collapsed;
                    TopBar.Visibility = Visibility.Visible;
                    CategoriesScrollViewer.Visibility = Visibility.Collapsed;
                    VideoListView.Visibility = Visibility.Visible;
                    SearchInput.Text = suggestion;
                    SearchContent(suggestion);
                }
            }
        }
        private class SearchSuggestionsResponse
        {
            public string query { get; set; }
            public List<List<object>> suggestions { get; set; }
        }
        // --- End search suggestions ---

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string ApiBaseUrl
        {
            get { return _apiBaseUrl; }
            set
            {
                if (_apiBaseUrl != value)
                {
                    _apiBaseUrl = value.EndsWith("/") ? value : value + "/";
                    OnPropertyChanged();
                }
            }
        }

        public string DefaultQuality
        {
            get { return _defaultQuality; }
            set
            {
                if (_defaultQuality != value)
                {
                    _defaultQuality = value;
                    OnPropertyChanged();
                }
            }
        }

        public MainPage()
        {
            this.InitializeComponent();
            _settings = new Settings();
            LoadSettings();
            this.Loaded += MainPage_Loaded;
            this.Unloaded += MainPage_Unloaded;
            SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
            PreventAutoShowKeyboard();
            Windows.UI.Core.CoreWindow.GetForCurrentThread().Activated += CoreWindow_Activated;

            // Initialize skip overlay timer
            _skipOverlayTimer = new DispatcherTimer();
            _skipOverlayTimer.Interval = TimeSpan.FromSeconds(1);
            _skipOverlayTimer.Tick += SkipOverlayTimer_Tick;

            // Subscribe to window size changed event
            Window.Current.SizeChanged += Window_SizeChanged;

            // Check if YouTube API Key is set
            if (string.IsNullOrEmpty(_settings.YoutubeApiKey))
            {
                ShowApiKeyDialog().ConfigureAwait(false);
            }
            else
            {
                LoadCategories().ConfigureAwait(false);
                LoadVideos().ConfigureAwait(false);
            }
        }

        private void Window_SizeChanged(object sender, Windows.UI.Core.WindowSizeChangedEventArgs e)
        {
            UpdateVideoCardLayout();
            UpdateVideoPlayerLayout();
            UpdateHistoryLayout();
            UpdateAuthorCardLayout();
        }

        private void UpdateVideoCardLayout()
        {
            if (VideosContainer == null || VideosContainer.ItemsPanelRoot == null) return;

            var windowWidth = Window.Current.Bounds.Width;
            var isLandscape = windowWidth > 800; // Threshold for landscape mode

            var itemsWrapGrid = VideosContainer.ItemsPanelRoot as ItemsWrapGrid;
            if (itemsWrapGrid == null) return;

            if (isLandscape)
            {
                // Calculate number of columns based on window width
                var columns = Math.Max(2, (int)(windowWidth / 300)); // Minimum 2 columns
                itemsWrapGrid.MaximumRowsOrColumns = columns;
                itemsWrapGrid.ItemWidth = (windowWidth - (columns * 16)) / columns; // Account for margins
            }
            else
            {
                // Single column for portrait mode
                itemsWrapGrid.MaximumRowsOrColumns = 1;
                itemsWrapGrid.ItemWidth = double.NaN; // Auto width (full width)
            }
        }

        private void UpdateVideoPlayerLayout()
        {
            if (VideoPlayerView.Visibility != Visibility.Visible) return;
            var windowWidth = Window.Current.Bounds.Width;
            var isLandscape = windowWidth > 800;

            // Две колонки в landscape, одна в portrait
            if (isLandscape)
            {
                PlayerColumn.Width = new GridLength(1, GridUnitType.Star);
                RelatedColumn.Width = new GridLength(400);
                RelatedPanel.Visibility = Visibility.Visible;
                PlayerInfoPanel.Margin = new Thickness(24, 0, 12, 0);
                PlayerInfoPanel.MaxWidth = 900;
                RelatedPanelVertical.Visibility = Visibility.Collapsed;
                RelatedVideosContainerVertical.ItemsSource = null;
            }
            else
            {
                PlayerColumn.Width = new GridLength(1, GridUnitType.Star);
                RelatedColumn.Width = new GridLength(0);
                RelatedPanel.Visibility = Visibility.Collapsed;
                PlayerInfoPanel.Margin = new Thickness(0);
                PlayerInfoPanel.MaxWidth = double.PositiveInfinity;
                RelatedPanelVertical.Visibility = Visibility.Visible;
                RelatedVideosContainerVertical.ItemsSource = RelatedVideosContainer.ItemsSource;
            }
        }

        private void UpdateHistoryLayout()
        {
            if (HistoryContainer == null || HistoryContainer.ItemsPanelRoot == null) return;

            var windowWidth = Window.Current.Bounds.Width;
            var isLandscape = windowWidth > 800; // Threshold for landscape mode

            var itemsWrapGrid = HistoryContainer.ItemsPanelRoot as ItemsWrapGrid;
            if (itemsWrapGrid == null) return;

            if (isLandscape)
            {
                // Calculate number of columns based on window width
                var columns = Math.Max(2, (int)(windowWidth / 300)); // Minimum 2 columns
                itemsWrapGrid.MaximumRowsOrColumns = columns;
                itemsWrapGrid.ItemWidth = (windowWidth - (columns * 16)) / columns; // Account for margins
            }
            else
            {
                // Single column for portrait mode
                itemsWrapGrid.MaximumRowsOrColumns = 1;
                itemsWrapGrid.ItemWidth = double.NaN; // Auto width (full width)
            }
        }

        private async Task ShowApiKeyDialog()
        {
            try
            {
                if (_isShowingDialog)
                {
                    return;
                }

                _isShowingDialog = true;

                if (_settings == null)
                {
                    Debug.WriteLine("Settings is null");
                    _isShowingDialog = false;
                    return;
                }

                var dialog = new ContentDialog
                {
                    Title = "YouTube API Key Required",
                    Content = new TextBox
                    {
                        Header = "Please enter your YouTube API Key",
                        PlaceholderText = "Enter API Key here",
                        Margin = new Thickness(0, 10, 0, 0)
                    },
                    PrimaryButtonText = "Save",
                    SecondaryButtonText = "Cancel",
                    IsPrimaryButtonEnabled = true
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    var apiKey = (dialog.Content as TextBox)?.Text;
                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        _settings.YoutubeApiKey = apiKey;
                        await LoadCategories();
                        await LoadVideos();
                    }
                }

                _isShowingDialog = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ShowApiKeyDialog error: {ex}");
                _isShowingDialog = false;
            }
        }

        private void LoadSettings()
        {
            try
            {
                Debug.WriteLine("Loading settings...");
                Debug.WriteLine($"ApiBaseUrl from settings: {_settings.ApiBaseUrl}");
                Debug.WriteLine($"DefaultQuality from settings: {_settings.DefaultQuality}");
                Debug.WriteLine($"YoutubeApiKey from settings: {_settings.YoutubeApiKey}");

                ApiUrlTextBox.Text = _settings.ApiBaseUrl;
                QualityTextBox.Text = _settings.DefaultQuality;
                RoundedCornersToggle.IsOn = _settings.UseRoundedCorners;
                YoutubeApiKeyTextBox.Text = _settings.YoutubeApiKey ?? string.Empty;

                // Обновляем локальные переменные
                _apiBaseUrl = _settings.ApiBaseUrl;
                _defaultQuality = _settings.DefaultQuality;

                Debug.WriteLine("Settings loaded:");
                Debug.WriteLine($"ApiBaseUrl: {_apiBaseUrl}");
                Debug.WriteLine($"DefaultQuality: {_defaultQuality}");
                Debug.WriteLine($"UseRoundedCorners: {_settings.UseRoundedCorners}");

                UpdateCornerRadius(_settings.UseRoundedCorners);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Load settings error: {ex}");
            }
        }

        private async void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Debug.WriteLine("Saving settings...");
                Debug.WriteLine($"Current ApiBaseUrl: {_settings.ApiBaseUrl}");
                Debug.WriteLine($"Current DefaultQuality: {_settings.DefaultQuality}");
                Debug.WriteLine($"Current YoutubeApiKey: {_settings.YoutubeApiKey}");

                _settings.ApiBaseUrl = ApiUrlTextBox.Text;
                _settings.DefaultQuality = QualityTextBox.Text;
                _settings.UseRoundedCorners = RoundedCornersToggle.IsOn;
                _settings.YoutubeApiKey = YoutubeApiKeyTextBox.Text;

                // Обновляем локальные переменные
                _apiBaseUrl = _settings.ApiBaseUrl;
                _defaultQuality = _settings.DefaultQuality;

                // Обновляем значения в Config
                Config.ApiBaseUrl = _settings.ApiBaseUrl;
                Config.DefaultQuality = _settings.DefaultQuality;

                Debug.WriteLine("Settings saved:");
                Debug.WriteLine($"New ApiBaseUrl: {_settings.ApiBaseUrl}");
                Debug.WriteLine($"New DefaultQuality: {_settings.DefaultQuality}");
                Debug.WriteLine($"New YoutubeApiKey: {_settings.YoutubeApiKey}");

                // Показываем диалог с предупреждением
                var dialog = new ContentDialog
                {
                    Title = "Настройки сохранены",
                    Content = "Для применения настроек требуется перезапустить приложение.",
                    PrimaryButtonText = "OK",
                    IsPrimaryButtonEnabled = true
                };

                await dialog.ShowAsync();
                BackToVideoList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Save settings error: {ex}");
            }
        }

        private string GetApiUrlWithKey(string baseUrl)
        {
            var separator = baseUrl.Contains("?") ? "&" : "?";
            return $"{baseUrl}{separator}apikey={_settings.YoutubeApiKey}";
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await LoadDefaultContent();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Initialization error: {ex}");
            }
        }

        private void MainPage_Unloaded(object sender, RoutedEventArgs e)
        {
            ReleaseResources();
        }

        private async Task LoadDefaultContent()
        {
            try
            {
                _currentPage = 0;
                VideoListLoadingRing.IsActive = true;
                VideoListLoadingRing.Visibility = Visibility.Visible;
                
                // Загружаем категории и видео параллельно
                var categoriesTask = LoadCategories();
                var videosTask = _httpClient.GetStringAsync(ApiBaseUrl + "get_top_videos.php");
                
                await Task.WhenAll(categoriesTask, videosTask);
                
                _allVideos = JsonConvert.DeserializeObject<List<VideoInfo>>(videosTask.Result);
                    UpdateDisplayedVideos();
                CategoriesScrollViewer.Visibility = Visibility.Visible;
                
                // Ждем, пока все карточки не будут отображены
                await Task.Delay(100); // Даем время на рендеринг
                VideoListLoadingRing.IsActive = false;
                VideoListLoadingRing.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Load default content error: {ex}");
                VideoListLoadingRing.IsActive = false;
                VideoListLoadingRing.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateDisplayedVideos()
        {
            try
            {
                if (_allVideos == null)
                {
                    Debug.WriteLine("_allVideos is null");
                    return;
                }

                // Показываем все видео сразу
                _displayedVideos = _allVideos.ToList();
                
                if (VideosContainer != null)
                {
                    VideosContainer.ItemsSource = _displayedVideos;
                }

                // Удаляем управление кнопкой LoadMoreButton
                // Применяем скругления после обновления списка
                UpdateCornerRadius(_settings?.UseRoundedCorners ?? true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Update display error: {ex}");
            }
        }

        private async void SearchInput_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                if (SuggestionsOverlay.Visibility == Visibility.Visible)
                {
                    SuggestionsOverlay.Visibility = Visibility.Collapsed;
                    TopBar.Visibility = Visibility.Visible;
                    CategoriesScrollViewer.Visibility = Visibility.Collapsed;
                    VideoListView.Visibility = Visibility.Visible;
                    var query = SearchInputOverlay.Text;
                    SearchInput.Text = query;
                    SearchContent(query);
                }
                else
                {
                    var query = SearchInput.Text;
                    if (string.IsNullOrWhiteSpace(query))
                    {
                        await LoadDefaultContent();
                        CategoriesScrollViewer.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        SearchContent(query);
                    }
                }
            }
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (SuggestionsOverlay.Visibility == Visibility.Visible)
            {
                SuggestionsOverlay.Visibility = Visibility.Collapsed;
                TopBar.Visibility = Visibility.Visible;
                CategoriesScrollViewer.Visibility = Visibility.Collapsed;
                VideoListView.Visibility = Visibility.Visible;
                var query = SearchInputOverlay.Text;
                SearchInput.Text = query;
                SearchContent(query);
            }
            else
            {
                var query = SearchInput.Text;
                if (string.IsNullOrWhiteSpace(query))
                {
                    LoadDefaultContent().ConfigureAwait(false);
                    CategoriesScrollViewer.Visibility = Visibility.Visible;
                }
                else
                {
                    SearchContent(query);
                }
            }
        }

        private void SearchContent(string query)
        {
            try
            {
                _currentPage = 0;
                VideoListLoadingRing.IsActive = true;
                VideoListLoadingRing.Visibility = Visibility.Visible;
                HistoryView.Visibility = Visibility.Collapsed;
                HistoryContainer.ItemsSource = null;
                
                var encodedQuery = Uri.EscapeDataString(query);
                var url = GetApiUrlWithKey(ApiBaseUrl + $"get_search_videos.php?query={encodedQuery}");
                var response = _httpClient.GetStringAsync(url);
                _allVideos = JsonConvert.DeserializeObject<List<VideoInfo>>(response.Result);
                UpdateDisplayedVideos();
                CategoriesScrollViewer.Visibility = Visibility.Collapsed;
                
                // Ждем, пока все карточки не будут отображены
                Task.Delay(100).ContinueWith(_ => 
                {
                    Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        VideoListLoadingRing.IsActive = false;
                        VideoListLoadingRing.Visibility = Visibility.Collapsed;
                    });
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Search error: {ex}");
                Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    SearchInput.PlaceholderText = "Search failed";
                    VideoListLoadingRing.IsActive = false;
                    VideoListLoadingRing.Visibility = Visibility.Collapsed;
                }).AsTask().ConfigureAwait(false);
            }
        }

        private async void VideoCard_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Не открывать видео, если клик был по имени автора или аватару
                var args = e as PointerRoutedEventArgs;
                if (args != null)
                {
                    var original = args.OriginalSource as FrameworkElement;
                    if (original != null && (original is TextBlock || original is Image))
                    {
                        // Если это имя автора или аватар, не открываем видео
                        return;
                    }
                }
                var clickedButton = sender as Button;
                if (clickedButton == null) return;

                var selectedVideo = clickedButton.DataContext as VideoInfo;
                if (selectedVideo == null) return;

                // Close current video if playing
                if (VideoPlayer.Source != null)
                {
                    VideoPlayer.Source = null;
                }

                await OpenVideoPlayer(selectedVideo);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Video card click error: {ex}");
            }
        }

        private async Task OpenVideoPlayer(VideoInfo video)
        {
            try
            {
                // Скрыть верхнюю полоску на странице видео
                TopBar.Visibility = Visibility.Collapsed;
                SearchPanel.Visibility = Visibility.Collapsed;
                VideoListView.Visibility = Visibility.Collapsed;
                VideoPlayerView.Visibility = Visibility.Visible;
                SettingsView.Visibility = Visibility.Collapsed;
                HistoryView.Visibility = Visibility.Collapsed;
                CategoriesScrollViewer.Visibility = Visibility.Collapsed;
                UpdateBackButtonVisibility();
                RequestDisplayKeepOn();

                UpdateVideoPlayerLayout();

                VideoInfoLoadingRing.IsActive = true;
                VideoInfoLoadingRing.Visibility = Visibility.Visible;

                // Reset video state
                _currentVideoId = video.VideoId;
                _relatedVideosPage = 0;
                _relatedVideos = new List<VideoInfo>();
                RelatedVideosContainer.ItemsSource = null;

                // Load video details and related videos in parallel
                var videoInfoUrl = GetApiUrlWithKey($"{ApiBaseUrl}get-ytvideo-info.php?video_id={video.VideoId}&quality={DefaultQuality}");
                var relatedVideosUrl = GetApiUrlWithKey($"{ApiBaseUrl}get_related_videos.php?video_id={video.VideoId}&page=0");

                var videoInfoTask = _httpClient.GetStringAsync(videoInfoUrl);
                var relatedVideosTask = _httpClient.GetStringAsync(relatedVideosUrl);

                await Task.WhenAll(videoInfoTask, relatedVideosTask);

                var videoDetails = JsonConvert.DeserializeObject<VideoDetails>(videoInfoTask.Result);
                var relatedVideos = JsonConvert.DeserializeObject<List<VideoInfo>>(relatedVideosTask.Result);

                // Update video data with received details
                video.Title = videoDetails.Title ?? video.Title;
                video.Author = videoDetails.Author ?? video.Author;
                video.Description = videoDetails.Description ?? video.Description;
                video.Thumbnail = videoDetails.Thumbnail ?? video.Thumbnail;
                video.ChannelThumbnail = videoDetails.ChannelThumbnail ?? video.ChannelThumbnail;
                video.Views = videoDetails.Views;
                video.Likes = videoDetails.Likes;
                video.PublishedAt = videoDetails.PublishedAt;

                // Add video to history
                await ViewHistory.AddToHistory(video);

                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    DisplayVideoInfo(videoDetails);
                    if (!string.IsNullOrEmpty(videoDetails.VideoUrl))
                    {
                        VideoPlayer.Source = MediaSource.CreateFromUri(new Uri(videoDetails.VideoUrl));
                    }

                    // Display related videos
                    if (relatedVideos != null && relatedVideos.Any())
                    {
                        _relatedVideos = relatedVideos;
                        RelatedVideosContainer.ItemsSource = _relatedVideos;
                    }

                    VideoInfoLoadingRing.IsActive = false;
                    VideoInfoLoadingRing.Visibility = Visibility.Collapsed;
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Video player error: {ex}");
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    VideoTitleText.Text = "Error loading video";
                    VideoAuthorText.Text = ex.Message;
                    VideoDescriptionText.Text = string.Empty;
                    VideoViewsText.Text = "0";
                    VideoLikesText.Text = "0";
                    VideoUploadDateText.Text = "Дата неизвестна";
                    VideoInfoLoadingRing.IsActive = false;
                    VideoInfoLoadingRing.Visibility = Visibility.Collapsed;
                });
            }
        }

        private void DisplayVideoInfo(VideoDetails video)
        {
            if (video == null) return;

            // Заголовок
            VideoTitleText.Text = video.Title ?? "Без названия";
            VideoAuthorText.Text = video.Author ?? "Неизвестный автор";
            VideoDescriptionText.Text = video.Description ?? "Описание отсутствует";
            
            // Форматируем количество просмотров
            long views = video.Views;
            VideoViewsText.Text = views >= 1000000
                ? $"{views / 1000000:N1}M"
                : views >= 1000
                ? $"{views / 1000:N1}K"
                : $"{views}";

            // Форматируем количество лайков
            long likes = video.Likes;
            VideoLikesText.Text = likes >= 1000000
                ? $"{likes / 1000000:N1}M"
                : likes >= 1000
                ? $"{likes / 1000:N1}K"
                : likes.ToString();

            // Форматируем дату загрузки
            if (!string.IsNullOrEmpty(video.PublishedAt))
            {
                string dateText = video.PublishedAt;
                if (dateText.Length > 9)
                {
                    dateText = dateText.Substring(0, dateText.Length - 9);
                }
                VideoUploadDateText.Text = dateText;
            }
            else
            {
                VideoUploadDateText.Text = "Дата неизвестна";
            }

            try
            {
                if (!string.IsNullOrEmpty(video.ChannelThumbnail))
                {
                    ChannelImage.Source = new BitmapImage(new Uri(video.ChannelThumbnail));
                }
                else
                {
                    // Установить изображение по умолчанию для канала
                    ChannelImage.Source = new BitmapImage(new Uri(ApiBaseUrl + "default_channel.png"));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting channel thumbnail: {ex}");
            }

            // Комментарии
            try
            {
                if (video is VideoDetails)
                {
                    var details = (VideoDetails)video;
                    if (details.Comments != null && details.Comments.Count > 0)
                    {
                        CommentsPanel.Visibility = Visibility.Visible;
                        CommentsList.ItemsSource = details.Comments;
                        CommentsHeader.Text = $"Комментарии ({details.Comments.Count})";
                    }
                    else
                    {
                        CommentsPanel.Visibility = Visibility.Collapsed;
                        CommentsList.ItemsSource = null;
                    }
                }
                else
                {
                    CommentsPanel.Visibility = Visibility.Collapsed;
                    CommentsList.ItemsSource = null;
                }
            }
            catch { CommentsPanel.Visibility = Visibility.Collapsed; CommentsList.ItemsSource = null; }
        }

        private async void BackToVideoList()
        {
            try
            {
                ReleaseDisplayRequest();
                VideoPlayer.Source = null;
                VideoPlayerView.Visibility = Visibility.Collapsed;
                VideoListView.Visibility = Visibility.Visible;
                SettingsView.Visibility = Visibility.Collapsed;
                HistoryView.Visibility = Visibility.Collapsed;
                HistoryContainer.ItemsSource = null;
                CategoriesScrollViewer.Visibility = Visibility.Visible;
                SuggestionsOverlay.Visibility = Visibility.Collapsed;
                SearchPanel.Visibility = Visibility.Collapsed;
                SearchInput.Text = "";
                SearchInputOverlay.Text = "";
                TopBar.Visibility = Visibility.Visible;
                await LoadVideos();
                UpdateBackButtonVisibility();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Back to list error: {ex}");
            }
        }

        private void UpdateBackButtonVisibility()
        {
            try
            {
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                    (VideoPlayerView.Visibility == Visibility.Visible || SettingsView.Visibility == Visibility.Visible || AuthorView.Visibility == Visibility.Visible) ?
                    AppViewBackButtonVisibility.Visible :
                    AppViewBackButtonVisibility.Collapsed;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Back button visibility error: {ex}");
            }
        }

        private void RequestDisplayKeepOn()
        {
            try
            {
                if (_displayRequest == null)
                {
                    _displayRequest = new DisplayRequest();
                    _displayRequest.RequestActive();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Display request error: {ex}");
            }
        }

        private void ReleaseDisplayRequest()
        {
            try
            {
                if (_displayRequest != null)
                {
                    _displayRequest.RequestRelease();
                    _displayRequest = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Release display error: {ex}");
            }
        }

        private void PreventAutoShowKeyboard()
        {
            try
            {
                InputPane.GetForCurrentView().Showing += (s, args) =>
                {
                    if (!_isSearchInputFocused)
                        args.EnsuredFocusedElementInView = true;
                };

                SearchInput.GotFocus += (s, e) => _isSearchInputFocused = true;
                SearchInput.LostFocus += (s, e) => _isSearchInputFocused = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Keyboard prevention error: {ex}");
            }
        }

        private void OnBackRequested(object sender, BackRequestedEventArgs e)
        {
            try
            {
                if (SuggestionsOverlay.Visibility == Visibility.Visible)
                {
                    SuggestionsOverlay.Visibility = Visibility.Collapsed;
                    TopBar.Visibility = Visibility.Visible;
                    CategoriesScrollViewer.Visibility = Visibility.Visible;
                    VideoListView.Visibility = Visibility.Visible;
                    SearchPanel.Visibility = Visibility.Collapsed;
                    SearchInput.Text = "";
                    SearchInputOverlay.Text = "";
                    e.Handled = true;
                    return;
                }
                if (AuthorView.Visibility == Visibility.Visible)
                {
                    CloseAuthorPage();
                    e.Handled = true;
                }
                else if (VideoPlayerView.Visibility == Visibility.Visible || SettingsView.Visibility == Visibility.Visible)
                {
                    BackToVideoList();
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Back requested error: {ex}");
            }
        }

        private void CoreWindow_Activated(CoreWindow sender, WindowActivatedEventArgs args)
        {
            try
            {
                if (args.WindowActivationState != CoreWindowActivationState.Deactivated)
                {
                    CheckForUrlActivation();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Window activation error: {ex}");
            }
        }

        private async void CheckForUrlActivation()
        {
            try
            {
                var args = App.Current as IApplicationViewActivatedEventArgs;
                if (args != null && args.Kind == ActivationKind.Protocol)
                {
                    var protocolArgs = args as ProtocolActivatedEventArgs;
                    if (protocolArgs?.Uri != null)
                    {
                        string videoId = null;
                        var query = protocolArgs.Uri.Query;
                        if (!string.IsNullOrEmpty(query))
                        {
                            var queryParams = query.TrimStart('?').Split('&');
                            foreach (var param in queryParams)
                            {
                                var parts = param.Split('=');
                                if (parts.Length == 2 && parts[0] == "v")
                                {
                                    videoId = parts[1];
                                    break;
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(videoId))
                        {
                            await OpenVideoFromUrl(videoId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"URL activation error: {ex}");
            }
        }

        private async Task OpenVideoFromUrl(string videoId)
        {
            try
            {
                var video = _allVideos.FirstOrDefault(v => v.VideoId == videoId);
                if (video != null)
                {
                    await OpenVideoPlayer(video);
                    return;
                }

                var videoInfoUrl = $"{ApiBaseUrl}get-ytvideo-info.php?video_id={videoId}&quality={DefaultQuality}";
                var response = await _httpClient.GetStringAsync(videoInfoUrl);
                var videoDetails = JsonConvert.DeserializeObject<VideoDetails>(response);

                var newVideo = new VideoInfo
                {
                    VideoId = videoDetails.VideoId,
                    Title = videoDetails.Title,
                    Author = videoDetails.Author,
                    Thumbnail = videoDetails.Thumbnail,
                    ChannelThumbnail = videoDetails.ChannelThumbnail,
                    Url = videoDetails.VideoUrl
                };

                await OpenVideoPlayer(newVideo);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Open from URL error: {ex}");
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    VideoTitleText.Text = "Error loading video";
                    VideoAuthorText.Text = ex.Message;
                });
            }
        }

        private void ReleaseResources()
        {
            try
            {
                if (VideoPlayer.Source != null)
                {
                    VideoPlayer.Source = null;
                }

                if (_displayRequest != null)
                {
                    _displayRequest.RequestRelease();
                    _displayRequest = null;
                }

                if (_skipOverlayTimer != null)
                {
                    _skipOverlayTimer.Stop();
                }

                SystemNavigationManager.GetForCurrentView().BackRequested -= OnBackRequested;
                Windows.UI.Core.CoreWindow.GetForCurrentThread().Activated -= CoreWindow_Activated;
                Window.Current.SizeChanged -= Window_SizeChanged;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Resource release error: {ex}");
            }
        }

        private async void HomeTab_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                VideoPlayerView.Visibility = Visibility.Collapsed;
                VideoListView.Visibility = Visibility.Visible;
                SearchPanel.Visibility = Visibility.Collapsed;
                SettingsView.Visibility = Visibility.Collapsed;
                HistoryView.Visibility = Visibility.Collapsed;
                CategoriesScrollViewer.Visibility = Visibility.Visible;
                HistoryContainer.ItemsSource = null;
                SuggestionsOverlay.Visibility = Visibility.Collapsed;
                SearchInput.Text = "";
                SearchInputOverlay.Text = "";
                TopBar.Visibility = Visibility.Visible;
                await LoadDefaultContent();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Home tab click error: {ex}");
            }
        }

        private void SettingsTab_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                VideoPlayerView.Visibility = Visibility.Collapsed;
                VideoListView.Visibility = Visibility.Collapsed;
                SearchPanel.Visibility = Visibility.Collapsed;
                SettingsView.Visibility = Visibility.Visible;
                HistoryView.Visibility = Visibility.Collapsed;
                CategoriesScrollViewer.Visibility = Visibility.Collapsed;
                HistoryContainer.ItemsSource = null;
                UpdateBackButtonVisibility();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Settings tab click error: {ex}");
            }
        }

        private async Task LoadCategories()
        {
            try
            {
                if (_settings == null || string.IsNullOrEmpty(_settings.YoutubeApiKey))
                {
                    await ShowApiKeyDialog();
                    return;
                }

                if (string.IsNullOrEmpty(ApiBaseUrl))
                {
                    Debug.WriteLine("ApiBaseUrl is null or empty");
                    return;
                }

                var url = GetApiUrlWithKey(ApiBaseUrl + "get-categories.php");
                var response = await _httpClient.GetStringAsync(url);
                var categories = JsonConvert.DeserializeObject<List<Category>>(response);
                
                if (categories != null)
                {
                    // Add "All" category at the beginning
                    categories.Insert(0, new Category { id = "0", title = "All" });
                    
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        if (CategoriesContainer != null)
                        {
                            CategoriesContainer.ItemsSource = categories;
                            UpdateCategoriesCornerRadius();
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Load categories error: {ex}");
            }
        }

        private async Task LoadVideos()
        {
            try
            {
                if (_settings == null || string.IsNullOrEmpty(_settings.YoutubeApiKey))
                {
                    await ShowApiKeyDialog();
                    return;
                }

                if (string.IsNullOrEmpty(ApiBaseUrl))
                {
                    Debug.WriteLine("ApiBaseUrl is null or empty");
                    return;
                }

                _currentPage = 0;
                var url = GetApiUrlWithKey(ApiBaseUrl + "get_top_videos.php");
                var response = await _httpClient.GetStringAsync(url);
                var videos = JsonConvert.DeserializeObject<List<VideoInfo>>(response);
                
                if (videos != null)
                {
                    _allVideos = videos;
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        UpdateDisplayedVideos();
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Load videos error: {ex}");
            }
        }

        private async Task LoadCategoryVideos(string categoryId)
        {
            try
            {
                if (string.IsNullOrEmpty(_settings.YoutubeApiKey))
                {
                    await ShowApiKeyDialog();
                    return;
                }

                var url = GetApiUrlWithKey(ApiBaseUrl + $"get-categories_videos.php?categoryId={categoryId}");
                var response = await _httpClient.GetStringAsync(url);
                var videos = JsonConvert.DeserializeObject<List<VideoInfo>>(response);
                
                if (videos != null)
                {
                    _allVideos = videos;
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        UpdateDisplayedVideos();
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Category videos load error: {ex}");
            }
        }

        private void UpdateCategoriesCornerRadius()
        {
            try
            {
                var radius = RoundedCornersToggle?.IsOn == true ? 12 : 0;
                
                if (CategoriesContainer?.ItemsPanelRoot != null)
                {
                    foreach (ContentPresenter presenter in CategoriesContainer.ItemsPanelRoot.Children)
                    {
                        var border = VisualTreeHelper.GetChild(presenter, 0) as Border;
                        if (border != null)
                        {
                            border.CornerRadius = new CornerRadius(radius);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Update categories corner radius error: {ex}");
            }
        }

        private void UpdateCornerRadius(bool useRoundedCorners)
        {
            try
            {
                var radius = useRoundedCorners ? 12 : 0;
                
                // Страница видео
                if (LikesPanel != null)
                {
                    LikesPanel.CornerRadius = new CornerRadius(radius);
                }
                if (DescriptionBorder != null)
                {
                    DescriptionBorder.CornerRadius = new CornerRadius(radius);
                }

                // Поиск
                if (SearchPanel != null)
                {
                    SearchPanel.CornerRadius = new CornerRadius(radius);
                }

                // Карточки видео
                if (VideosContainer?.ItemsPanelRoot != null)
                {
                    foreach (ContentPresenter presenter in VideosContainer.ItemsPanelRoot.Children)
                    {
                        var button = presenter.ContentTemplate.LoadContent() as Button;
                        if (button != null)
                        {
                            var border = button.Content as Border;
                            if (border != null)
                            {
                                border.CornerRadius = new CornerRadius(radius);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Update corner radius error: {ex}");
            }
        }

        private void RoundedCornersToggle_Toggled(object sender, RoutedEventArgs e)
        {
            UpdateCornerRadius(RoundedCornersToggle.IsOn);
            UpdateCategoriesCornerRadius();
        }

        private async void CategoryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var category = button.DataContext as Category;
                _selectedCategoryId = category.id;

                if (_selectedCategoryId == "0")
                {
                    await LoadVideos();
                }
                else
                {
                    await LoadCategoryVideos(_selectedCategoryId);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Category button click error: {ex}");
            }
        }

        private void ToggleDescriptionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DescriptionScrollViewer.Height == 200)
                {
                    DescriptionScrollViewer.Height = double.NaN; // Auto height
                    ToggleDescriptionButton.Content = "Show Less";
                }
                else
                {
                    DescriptionScrollViewer.Height = 200;
                    ToggleDescriptionButton.Content = "Show More";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Toggle description error: {ex}");
            }
        }

        private async void HistoryTab_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                VideoPlayerView.Visibility = Visibility.Collapsed;
                VideoListView.Visibility = Visibility.Collapsed;
                SearchPanel.Visibility = Visibility.Collapsed;
                SettingsView.Visibility = Visibility.Collapsed;
                HistoryView.Visibility = Visibility.Visible;
                CategoriesScrollViewer.Visibility = Visibility.Collapsed;

                var history = await ViewHistory.GetHistory();
                HistoryContainer.ItemsSource = history;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"History tab click error: {ex}");
            }
        }

        private async void LoadMoreRelated_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(_currentVideoId)) return;

                var url = GetApiUrlWithKey($"{ApiBaseUrl}get_related_videos.php?video_id={_currentVideoId}&page={_relatedVideosPage + 1}");
                var response = await _httpClient.GetStringAsync(url);
                var newVideos = JsonConvert.DeserializeObject<List<VideoInfo>>(response);

                if (newVideos != null && newVideos.Any())
                {
                    _relatedVideosPage++;
                    _relatedVideos.AddRange(newVideos);
                    var currentList = new List<VideoInfo>(_relatedVideos);
                    RelatedVideosContainer.ItemsSource = currentList;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Load more related videos error: {ex}");
            }
        }

        private void VideoPlayer_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (VideoPlayer.MediaPlayer == null) return;

            var position = e.GetPosition(VideoPlayer);
            var playerWidth = VideoPlayer.ActualWidth;
            var isRightSide = position.X > playerWidth / 2;

            _isDoubleTapRight = isRightSide;
            SkipOverlay.Visibility = Visibility.Visible;
            SkipIcon.Glyph = isRightSide ? "\uE111" : "\uE112"; // Forward and backward glyphs
            SkipText.Text = isRightSide ? "10 seconds" : "-10 seconds";

            // Skip the video
            var currentPosition = VideoPlayer.MediaPlayer.Position;
            var newPosition = isRightSide ? 
                currentPosition.Add(TimeSpan.FromSeconds(10)) : 
                currentPosition.Subtract(TimeSpan.FromSeconds(10));

            // Ensure we don't go below 0
            if (newPosition.TotalSeconds < 0)
                newPosition = TimeSpan.Zero;

            VideoPlayer.MediaPlayer.Position = newPosition;

            // Start the timer to hide the overlay
            _skipOverlayTimer.Start();
        }

        private void SkipOverlayTimer_Tick(object sender, object e)
        {
            _skipOverlayTimer.Stop();
            SkipOverlay.Visibility = Visibility.Collapsed;
        }

        public class Category
        {
            public string id { get; set; }
            public string title { get; set; }
        }

        public class Comment
        {
            [JsonProperty("author")]
            public string Author { get; set; }

            [JsonProperty("text")]
            public string Text { get; set; }

            [JsonProperty("published_at")]
            public string PublishedAt { get; set; }

            [JsonProperty("author_thumbnail")]
            public string AuthorThumbnail { get; set; }
        }

        public class VideoDetails
        {
            [JsonProperty("title")]
            public string Title { get; set; }

            [JsonProperty("author")]
            public string Author { get; set; }

            [JsonProperty("description")]
            public string Description { get; set; }

            [JsonProperty("video_id")]
            public string VideoId { get; set; }

            [JsonProperty("embed_url")]
            public string EmbedUrl { get; set; }

            [JsonProperty("duration")]
            public string Duration { get; set; }

            [JsonProperty("published_at")]
            public string PublishedAt { get; set; }

            [JsonProperty("likes")]
            public long Likes { get; set; }

            [JsonProperty("views")]
            public long Views { get; set; }

            [JsonProperty("comment_count")]
            public int CommentCount { get; set; }

            [JsonProperty("comments")]
            public List<Comment> Comments { get; set; }

            [JsonProperty("channel_thumbnail")]
            public string ChannelThumbnail { get; set; }

            [JsonProperty("thumbnail")]
            public string Thumbnail { get; set; }

            [JsonProperty("video_url")]
            public string VideoUrl { get; set; }
        }

        private async Task OpenAuthorPage(string authorName)
        {
            try
            {
                // Скрываем все кроме AuthorView
                VideoPlayerView.Visibility = Visibility.Collapsed;
                VideoListView.Visibility = Visibility.Collapsed;
                SearchPanel.Visibility = Visibility.Collapsed;
                SettingsView.Visibility = Visibility.Collapsed;
                HistoryView.Visibility = Visibility.Collapsed;
                CategoriesScrollViewer.Visibility = Visibility.Collapsed;
                AuthorView.Visibility = Visibility.Visible;
                TopBar.Visibility = Visibility.Collapsed;
                UpdateBackButtonVisibility();

                _currentAuthorName = authorName;
                AuthorTitle.Text = authorName;
                AuthorSubscribers.Text = "";
                AuthorVideosCount.Text = "";
                AuthorAvatar.Source = null;
                AuthorBanner.Source = null;
                AuthorVideosContainer.ItemsSource = null;

                // Загружаем данные о канале
                var url = GetApiUrlWithKey($"{ApiBaseUrl}get_author_videos.php?author={Uri.EscapeDataString(authorName)}");
                var response = await _httpClient.GetStringAsync(url);
                var data = JsonConvert.DeserializeObject<AuthorApiResponse>(response);
                if (data?.channel_info != null)
                {
                    AuthorTitle.Text = data.channel_info.title;
                    AuthorSubscribers.Text = $"{data.channel_info.subscriber_count} подписчиков";
                    AuthorVideosCount.Text = $"{data.channel_info.video_count} видео";
                    if (!string.IsNullOrEmpty(data.channel_info.thumbnail))
                        AuthorAvatar.Source = new BitmapImage(new Uri(data.channel_info.thumbnail));
                    if (!string.IsNullOrEmpty(data.channel_info.banner))
                        AuthorBanner.Source = new BitmapImage(new Uri(data.channel_info.banner));
                }
                _currentAuthorVideos = data?.videos ?? new List<VideoInfo>();
                AuthorVideosContainer.ItemsSource = _currentAuthorVideos;
                UpdateAuthorCardLayout();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OpenAuthorPage error: {ex}");
            }
        }

        private void CloseAuthorPage()
        {
            // Скрыть все вьюшки
            AuthorView.Visibility = Visibility.Collapsed;
            VideoPlayerView.Visibility = Visibility.Collapsed;
            SettingsView.Visibility = Visibility.Collapsed;
            HistoryView.Visibility = Visibility.Collapsed;
            // Показать только нужные
            VideoListView.Visibility = Visibility.Visible;
            // Скрыть все поисковые панели и оверлеи
            SearchPanel.Visibility = Visibility.Collapsed;
            SuggestionsOverlay.Visibility = Visibility.Collapsed;
            SearchInput.Text = "";
            SearchInputOverlay.Text = "";
            TopBar.Visibility = Visibility.Visible;
            CategoriesScrollViewer.Visibility = Visibility.Visible;
            // Сбросить содержимое AuthorView
            AuthorTitle.Text = "";
            AuthorSubscribers.Text = "";
            AuthorVideosCount.Text = "";
            AuthorAvatar.Source = null;
            AuthorBanner.Source = null;
            AuthorVideosContainer.ItemsSource = null;
            _currentAuthorName = null;
            _currentAuthorVideos = new List<VideoInfo>();
            UpdateBackButtonVisibility();
        }

        private void AuthorVideoCard_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var btn = sender as Button;
                var video = btn?.DataContext as VideoInfo;
                if (video != null)
                {
                    AuthorView.Visibility = Visibility.Collapsed;
                    OpenVideoPlayer(video).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AuthorVideoCard_Click error: {ex}");
            }
        }

        private void UpdateAuthorCardLayout()
        {
            if (AuthorVideosContainer == null || AuthorVideosContainer.ItemsPanelRoot == null) return;
            var windowWidth = Window.Current.Bounds.Width;
            var isLandscape = windowWidth > 800;
            var itemsWrapGrid = AuthorVideosContainer.ItemsPanelRoot as ItemsWrapGrid;
            if (itemsWrapGrid == null) return;
            if (isLandscape)
            {
                var columns = Math.Max(2, (int)(windowWidth / 300));
                itemsWrapGrid.MaximumRowsOrColumns = columns;
                itemsWrapGrid.ItemWidth = (windowWidth - (columns * 16)) / columns;
            }
            else
            {
                itemsWrapGrid.MaximumRowsOrColumns = 1;
                itemsWrapGrid.ItemWidth = double.NaN;
            }
        }

        private void VideoAuthorText_Tapped(object sender, TappedRoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(VideoAuthorText.Text))
                {
                    VideoPlayerView.Visibility = Visibility.Collapsed;
                    OpenAuthorPage(VideoAuthorText.Text).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VideoAuthorText_Tapped error: {ex}");
            }
        }

        private void ChannelPicture_Tapped(object sender, TappedRoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(VideoAuthorText.Text))
                {
                    VideoPlayerView.Visibility = Visibility.Collapsed;
                    OpenAuthorPage(VideoAuthorText.Text).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ChannelPicture_Tapped error: {ex}");
            }
        }

        private void CardAuthor_Tapped(object sender, TappedRoutedEventArgs e)
        {
            try
            {
                string author = null;
                var tb = sender as TextBlock;
                if (tb != null && tb.Text != null)
                {
                    author = tb.Text;
                }
                var img = sender as Image;
                if (img != null && img.DataContext is VideoInfo)
                {
                    var vi = (VideoInfo)img.DataContext;
                    author = vi.Author;
                }
                var fe = sender as FrameworkElement;
                if (fe != null && fe.DataContext is VideoInfo)
                {
                    var vi2 = (VideoInfo)fe.DataContext;
                    author = vi2.Author;
                }
                if (!string.IsNullOrEmpty(author))
                {
                    e.Handled = true;
                    OpenAuthorPage(author).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("CardAuthor_Tapped error: " + ex);
            }
        }

        // Показываем SuggestionsOverlay, скрываем всё остальное
        private void ShowSearchButton_Click(object sender, RoutedEventArgs e)
        {
            TopBar.Visibility = Visibility.Collapsed;
            SearchPanel.Visibility = Visibility.Collapsed;
            CategoriesScrollViewer.Visibility = Visibility.Collapsed;
            VideoListView.Visibility = Visibility.Collapsed;
            SuggestionsOverlay.Visibility = Visibility.Visible;
            SearchInputOverlay.Focus(FocusState.Programmatic);
        }

        // Скрываем SuggestionsOverlay, возвращаем всё обратно
        private void CloseSearchButton_Click(object sender, RoutedEventArgs e)
        {
            SuggestionsOverlay.Visibility = Visibility.Collapsed;
            TopBar.Visibility = Visibility.Visible;
            CategoriesScrollViewer.Visibility = Visibility.Visible;
            VideoListView.Visibility = Visibility.Visible;
            SearchInputOverlay.Text = string.Empty;
            SuggestionsListBox.ItemsSource = null;
        }
    }
}