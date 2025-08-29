﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Newtonsoft.Json;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;
using Windows.System.Display;
using Windows.Media.Playback;
using Windows.Media.Core;
using System.Runtime.CompilerServices;
using System.ComponentModel;
using YouTube.Models;
using System.Linq;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.Foundation;

namespace YouTube
{
    public sealed partial class Video : Page, INotifyPropertyChanged
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private string _apiBaseUrl = Config.ApiBaseUrl;
        private string _defaultQuality = Config.DefaultQuality;
        private DisplayRequest _displayRequest;
        private DispatcherTimer _skipOverlayTimer;
        private bool _isDoubleTapRight = true;
        private string _currentVideoId;
        private List<VideoInfo> _relatedVideos = new List<VideoInfo>();
        private int _relatedVideosPage = 0;
        private const int RELATED_VIDEOS_PER_PAGE = 10;
        private Frame _frame;
        private string _currentVideoDescription;
        private bool _isFullScreen = false;
        private string _currentVideoUrl;
        private string _currentQuality;
        private long _currentLikes = 0;
        private bool _isLiked = false;
        private bool _isChangingQuality = false; // Flag to prevent storage conflicts during quality change
        private TimeSpan _videoDuration;

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public Video()
        {
            this.InitializeComponent();
            _frame = Window.Current.Content as Frame;

            NavigationManager.InitializeTabBarNavigation(tabbar, _frame);
            NavigationManager.InitializeNavBarNavigation(navbar, _frame);

            this.Loaded += Video_Loaded;
            this.Unloaded += Video_Unloaded;

            _skipOverlayTimer = new DispatcherTimer();
            _skipOverlayTimer.Interval = TimeSpan.FromSeconds(2);
            _skipOverlayTimer.Tick += SkipOverlayTimer_Tick;

            Window.Current.SizeChanged += Window_SizeChanged;

            // Hook settings click from custom transport controls
            var controls = VideoPlayer.TransportControls as CustomMediaTransportControls;
            if (controls != null)
            {
                controls.SettingsClicked += Controls_SettingsClicked;
            }

            // Default to standard (no explicit quality parameter)
            _currentQuality = null;

            // Инициализация заглушек
            InitializePlaceholders();
        }

        private void InitializePlaceholders()
        {
            // Скрываем комментарии по умолчанию
            CommentsContainerButton.Visibility = Visibility.Collapsed;

            // Устанавливаем значения по умолчанию
            VideoTitleText.Text = "Загрузка...";
            VideoAuthorText.Text = "Загрузка...";
            VideoViewsText.Text = "Загрузка...";
            VideoUploadDateText.Text = "Загрузка...";
        }

        private void Video_Loaded(object sender, RoutedEventArgs e)
        {
            SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
            UpdateVideoPlayerLayout();

            if (!string.IsNullOrEmpty(_currentVideoId))
            {
                LoadVideo(_currentVideoId);
            }
        }

        private void Video_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (VideoPlayer.MediaPlayer != null)
                {
                    VideoPlayer.MediaPlayer.Pause();
                    VideoPlayer.Source = null;
                }

                if (_displayRequest != null)
                {
                    _displayRequest.RequestRelease();
                    _displayRequest = null;
                }

                SystemNavigationManager.GetForCurrentView().BackRequested -= OnBackRequested;
                Window.Current.SizeChanged -= Window_SizeChanged;
            }
            catch (Exception) { }
        }

        private void OnBackRequested(object sender, BackRequestedEventArgs e)
        {
            if (_isFullScreen)
            {
                // Exit fullscreen first
                ToggleFullScreen();
                e.Handled = true;
            }
            else if (_frame.CanGoBack)
            {
                e.Handled = true;
                _frame.GoBack();
            }
        }

        private void UpdateBackButtonVisibility()
        {
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                _frame.CanGoBack ? AppViewBackButtonVisibility.Visible : AppViewBackButtonVisibility.Collapsed;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            try
            {
                if (e.Parameter != null)
                {
                    string videoId = e.Parameter.ToString();
                    if (!string.IsNullOrWhiteSpace(videoId))
                    {
                        LoadVideo(videoId);
                    }
                    else
                    {
                        _frame.Navigate(typeof(MainPage));
                    }
                }
                else
                {
                    _frame.Navigate(typeof(MainPage));
                }
            }
            catch (Exception ex)
            {
                _frame.Navigate(typeof(MainPage));
            }

            UpdateBackButtonVisibility();
        }

        private async void LoadVideo(string videoId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(videoId))
                {
                    ShowErrorDialog("Video ID is missing");
                    return;
                }

                _currentVideoId = videoId;

                var localSettings = ApplicationData.Current.LocalSettings;
                if (!localSettings.Values.ContainsKey("YouTubeApiKey"))
                {
                    ShowErrorDialog("YouTube API Key is missing");
                    return;
                }

                string apiKey = localSettings.Values["YouTubeApiKey"].ToString();
                string videoUrl = $"{_apiBaseUrl}get-ytvideo-info.php?video_id={videoId}&apikey={apiKey}";

                using (var client = new HttpClient())
                {
                    var response = await client.GetStringAsync(videoUrl);
                    var videoDetails = JsonConvert.DeserializeObject<VideoDetails>(response);

                    if (videoDetails != null && !string.IsNullOrEmpty(videoDetails.VideoUrl))
                    {
                        DisplayVideoInfo(videoDetails);
                        await LoadRelatedVideos();

                        // Only add to history if not currently changing quality to avoid storage conflicts
                        if (!_isChangingQuality)
                        {
                            await ViewHistory.AddToHistory(new VideoInfo
                            {
                                video_id = videoDetails.VideoId,
                                title = videoDetails.Title,
                                author = videoDetails.Author,
                                thumbnail = videoDetails.Thumbnail,
                                channel_thumbnail = videoDetails.ChannelThumbnail,
                                Views = FormatViewsCount(videoDetails.Views),
                                PublishedAt = videoDetails.PublishedAt
                            });
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("Skipping history update during quality change");
                        }
                    }
                    else
                    {
                        ShowErrorDialog("Failed to load video details");
                    }
                }
            }
            catch (HttpRequestException httpEx)
            {
                ShowErrorDialog("Network error loading video");
            }
            catch (JsonException jsonEx)
            {
                ShowErrorDialog("Error parsing video data");
            }
            catch (Exception ex)
            {
                ShowErrorDialog("Error loading video");
            }
        }

        private async void ShowErrorDialog(string message)
        {
            try
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    var dialog = new ContentDialog
                    {
                        Title = "Error",
                        Content = message,
                        PrimaryButtonText = "OK"
                    };
                    await dialog.ShowAsync();
                    _frame.Navigate(typeof(MainPage));
                });
            }
            catch (Exception) { }
        }

        private async Task LoadRelatedVideos()
        {
            try
            {
                // Показать индикаторы загрузки
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    RelatedVideosLoadingRing.IsActive = true;
                    RelatedVideosLoadingRing.Visibility = Visibility.Visible;
                    RelatedVideosLoadingRingVertical.IsActive = true;
                    RelatedVideosLoadingRingVertical.Visibility = Visibility.Visible;

                    // Скрыть контейнеры с видео
                    RelatedVideosContainer.Visibility = Visibility.Collapsed;
                    RelatedVideosContainerVertical.Visibility = Visibility.Collapsed;
                });

                var localSettings = ApplicationData.Current.LocalSettings;
                if (!localSettings.Values.ContainsKey("YouTubeApiKey"))
                {
                    return;
                }

                string apiKey = localSettings.Values["YouTubeApiKey"].ToString();
                string relatedUrl = $"{_apiBaseUrl}get_related_videos.php?video_id={_currentVideoId}&page={_relatedVideosPage}&apikey={apiKey}&token={Uri.EscapeDataString(Config.UserToken)}";

                using (var client = new HttpClient())
                {
                    var response = await client.GetStringAsync(relatedUrl);
                    var videos = JsonConvert.DeserializeObject<List<VideoInfo>>(response);

                    if (videos != null)
                    {
                        _relatedVideos.AddRange(videos);

                        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            RelatedVideosContainer.ItemsSource = null;
                            RelatedVideosContainer.ItemsSource = _relatedVideos;
                            RelatedVideosContainerVertical.ItemsSource = null;
                            RelatedVideosContainerVertical.ItemsSource = _relatedVideos;

                            // Показать контейнеры с видео и скрыть индикаторы
                            RelatedVideosContainer.Visibility = Visibility.Visible;
                            RelatedVideosContainerVertical.Visibility = Visibility.Visible;

                            RelatedVideosLoadingRing.IsActive = false;
                            RelatedVideosLoadingRing.Visibility = Visibility.Collapsed;
                            RelatedVideosLoadingRingVertical.IsActive = false;
                            RelatedVideosLoadingRingVertical.Visibility = Visibility.Collapsed;
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    // В случае ошибки тоже скрываем индикаторы
                    RelatedVideosLoadingRing.IsActive = false;
                    RelatedVideosLoadingRing.Visibility = Visibility.Collapsed;
                    RelatedVideosLoadingRingVertical.IsActive = false;
                    RelatedVideosLoadingRingVertical.Visibility = Visibility.Collapsed;

                    // Показываем контейнеры (даже если пустые)
                    RelatedVideosContainer.Visibility = Visibility.Visible;
                    RelatedVideosContainerVertical.Visibility = Visibility.Visible;
                });
            }
        }

        private void DisplayVideoInfo(VideoDetails video)
        {
            VideoTitleText.Text = video.Title;
            VideoAuthorText.Text = video.Author;
            _currentVideoDescription = video.Description;
            VideoViewsText.Text = FormatViewsCount(video.Views) + " просмотров";
            VideoUploadDateText.Text = FormatRelativeDate(video.PublishedAt);

            // Format and display subscriber count
            if (!string.IsNullOrEmpty(video.SubscriberCount))
            {
                SubscriberCountText.Text = FormatSubscriberCount(video.SubscriberCount);
            }
            else
            {
                SubscriberCountText.Text = "";
            }

            // Инициализация лайков
            _currentLikes = video.Likes;
            _isLiked = false;
            LikeCountText.Text = FormatViewsCount(_currentLikes);

            // Improved avatar loading with better error handling
            try
            {
                if (!string.IsNullOrEmpty(video.ChannelThumbnail))
                {
                    var bitmap = new Windows.UI.Xaml.Media.Imaging.BitmapImage();
                    bitmap.ImageFailed += (sender, e) =>
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to load channel avatar: {video.ChannelThumbnail}");
                    };
                    bitmap.UriSource = new Uri(video.ChannelThumbnail);
                    ChannelImage.Source = bitmap;
                }
                else
                {
                    ChannelImage.Source = null;
                    System.Diagnostics.Debug.WriteLine("No channel thumbnail available");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting channel avatar: {ex.Message}");
                ChannelImage.Source = null;
            }

            // Parse duration from metadata
            _videoDuration = ParseIsoDuration(video.Duration);

            // Store video ID and construct base URL for quality system compatibility
            _currentVideoId = video.VideoId;
            _currentVideoUrl = Config.GetVideoUrl(_currentVideoId); // Base URL without quality
            ApplyAndPlayCurrentUrlWithQuality();

            // Обработка комментариев
            if (video.Comments != null && video.Comments.Count > 0)
            {
                // Отображаем последний комментарий
                var lastComment = video.Comments.First();
                CommentsContainerButton.Visibility = Visibility.Visible;

                CommentCountText.Text = $"• {video.CommentCount:N0}";
                LastCommentAuthor.Text = "@" + lastComment.Author;
                LastCommentTime.Text = lastComment.PublishedAt;
                LastCommentText.Text = lastComment.Text.Length > 100
                    ? lastComment.Text.Substring(0, 100) + "..."
                    : lastComment.Text;

                if (!string.IsNullOrEmpty(lastComment.AuthorThumbnail))
                {
                    LastCommentAuthorImage.Source = new Windows.UI.Xaml.Media.Imaging.BitmapImage(new Uri(lastComment.AuthorThumbnail));
                }

                // Сохраняем все комментарии для модального окна
                CommentsList.ItemsSource = video.Comments;
            }
            else
            {
                CommentsContainerButton.Visibility = Visibility.Collapsed;
                CommentsList.ItemsSource = null;
            }

            RequestDisplayKeepOn();
        }

        private TimeSpan ParseIsoDuration(string iso)
        {
            var duration = TimeSpan.Zero;
            if (string.IsNullOrEmpty(iso) || !iso.StartsWith("PT")) return duration;

            string s = iso.Substring(2);
            int hours = 0, minutes = 0, seconds = 0;

            var hIndex = s.IndexOf('H');
            if (hIndex > 0)
            {
                hours = int.Parse(s.Substring(0, hIndex));
                s = s.Substring(hIndex + 1);
            }

            var mIndex = s.IndexOf('M');
            if (mIndex > 0)
            {
                minutes = int.Parse(s.Substring(0, mIndex));
                s = s.Substring(mIndex + 1);
            }

            var sIndex = s.IndexOf('S');
            if (sIndex > 0)
            {
                seconds = int.Parse(s.Substring(0, sIndex));
            }

            return new TimeSpan(hours, minutes, seconds);
        }

        private string FormatViewsCount(long views)
        {
            if (views >= 1000000000)
            {
                return $"{views / 1000000000.0:F1}B";
            }
            else if (views >= 1000000)
            {
                return $"{views / 1000000.0:F1}M";
            }
            else if (views >= 1000)
            {
                return $"{views / 1000.0:F1} тыс";
            }
            return views.ToString();
        }

        private string FormatSubscriberCount(string subscriberCountString)
        {
            try
            {
                long count;
                if (long.TryParse(subscriberCountString, out count))
                {
                    if (count >= 1000000000)
                    {
                        return $"{count / 1000000000.0:F1}B";
                    }
                    else if (count >= 1000000)
                    {
                        return $"{count / 1000000.0:F1}M";
                    }
                    else if (count >= 1000)
                    {
                        return $"{count / 1000.0:F1}K";
                    }
                    else
                    {
                        return count.ToString();
                    }
                }
                else
                {
                    return subscriberCountString;
                }
            }
            catch
            {
                return !string.IsNullOrEmpty(subscriberCountString) ? subscriberCountString : "";
            }
        }

        private string FormatRelativeDate(string publishedAtString)
        {
            try
            {
                DateTime publishedDate;
                if (DateTime.TryParseExact(publishedAtString, "dd.MM.yyyy, HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out publishedDate))
                {
                    var timeSpan = DateTime.Now - publishedDate;
                    var totalDays = (int)timeSpan.TotalDays;

                    if (totalDays == 0)
                    {
                        var hours = (int)timeSpan.TotalHours;
                        var minutes = (int)timeSpan.TotalMinutes;

                        if (hours == 0)
                        {
                            if (minutes < 1)
                                return "только что";
                            else if (minutes == 1)
                                return "1 минуту назад";
                            else if (minutes < 5)
                                return $"{minutes} минуты назад";
                            else
                                return $"{minutes} минут назад";
                        }
                        else if (hours == 1)
                        {
                            return "1 час назад";
                        }
                        else if (hours < 5)
                        {
                            return $"{hours} часа назад";
                        }
                        else
                        {
                            return $"{hours} часов назад";
                        }
                    }
                    else if (totalDays == 1)
                    {
                        return "1 день назад";
                    }
                    else if (totalDays < 7)
                    {
                        if (totalDays < 5)
                            return $"{totalDays} дня назад";
                        else
                            return $"{totalDays} дней назад";
                    }
                    else if (totalDays < 30)
                    {
                        var weeks = totalDays / 7;
                        if (weeks == 1)
                            return "1 неделю назад";
                        else if (weeks < 5)
                            return $"{weeks} недели назад";
                        else
                            return $"{weeks} недель назад";
                    }
                    else if (totalDays < 365)
                    {
                        var months = totalDays / 30;
                        if (months == 1)
                            return "1 месяц назад";
                        else if (months < 5)
                            return $"{months} месяца назад";
                        else
                            return $"{months} месяцев назад";
                    }
                    else
                    {
                        var years = totalDays / 365;
                        if (years == 1)
                            return "1 год назад";
                        else if (years < 5)
                            return $"{years} года назад";
                        else
                            return $"{years} лет назад";
                    }
                }
                else
                {
                    return publishedAtString;
                }
            }
            catch
            {
                return !string.IsNullOrEmpty(publishedAtString) ? publishedAtString : "Дата неизвестна";
            }
        }

        private void RequestDisplayKeepOn()
        {
            if (_displayRequest == null)
            {
                _displayRequest = new DisplayRequest();
            }
            _displayRequest.RequestActive();
        }

        private void VideoPlayer_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            try
            {
                if (VideoPlayer.MediaPlayer == null) return;

                var tapPosition = e.GetPosition(VideoPlayer);
                var playerWidth = VideoPlayer.ActualWidth;
                bool isRightSide = tapPosition.X > playerWidth / 2;
                int skipSeconds = isRightSide ? 10 : -10;

                var currentPosition = VideoPlayer.MediaPlayer.Position;
                var newPosition = currentPosition.Add(TimeSpan.FromSeconds(skipSeconds));

                if (newPosition < TimeSpan.Zero)
                    newPosition = TimeSpan.Zero;
                else if (newPosition > _videoDuration)
                    newPosition = _videoDuration;

                VideoPlayer.MediaPlayer.Position = newPosition;

                SkipOverlay.Visibility = Visibility.Visible;
                SkipIcon.Glyph = skipSeconds > 0 ? "\uE111" : "\uE112";
                SkipText.Text = $"{Math.Abs(skipSeconds)} сек";
                _skipOverlayTimer.Start();
            }
            catch (Exception) { }
        }

        private void SkipOverlayTimer_Tick(object sender, object e)
        {
            SkipOverlay.Visibility = Visibility.Collapsed;
            _skipOverlayTimer.Stop();
        }

        private async void ShowDescriptionButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentVideoDescription))
            {
                var dialog = new ContentDialog
                {
                    Title = "Описание",
                    Content = new ScrollViewer
                    {
                        Content = new TextBlock
                        {
                            Text = _currentVideoDescription,
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = new SolidColorBrush(Windows.UI.Colors.White)
                        },
                        MaxHeight = 400
                    },
                    PrimaryButtonText = "Закрыть",
                    Background = new SolidColorBrush(Windows.UI.Colors.Black)
                };

                dialog.RequestedTheme = ElementTheme.Dark;
                await dialog.ShowAsync();
            }
        }

        private async void LastCommentButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var scrollViewer = new ScrollViewer
                {
                    MaxHeight = 500,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                };

                var commentsPanel = new StackPanel();

                if (CommentsList.ItemsSource != null)
                {
                    var comments = CommentsList.ItemsSource as List<Comment>;
                    if (comments != null)
                    {
                        foreach (var comment in comments)
                        {
                            var commentContainer = new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                Margin = new Thickness(0, 10, 0, 10)
                            };

                            var avatarBorder = new Border
                            {
                                Width = 36,
                                Height = 36,
                                CornerRadius = new CornerRadius(18),
                                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 51, 51, 51))
                            };

                            if (!string.IsNullOrEmpty(comment.AuthorThumbnail))
                            {
                                var avatarImage = new Image
                                {
                                    Source = new Windows.UI.Xaml.Media.Imaging.BitmapImage(new Uri(comment.AuthorThumbnail)),
                                    Stretch = Stretch.UniformToFill
                                };
                                avatarBorder.Child = avatarImage;
                            }

                            var textPanel = new StackPanel
                            {
                                Margin = new Thickness(10, 0, 0, 0)
                            };

                            var authorText = new TextBlock
                            {
                                Text = comment.Author,
                                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 170, 170, 170)),
                                FontWeight = Windows.UI.Text.FontWeights.Bold
                            };

                            var timeText = new TextBlock
                            {
                                Text = comment.PublishedAt,
                                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 102, 102, 102)),
                                FontSize = 12
                            };

                            var commentText = new TextBlock
                            {
                                Text = comment.Text,
                                Foreground = new SolidColorBrush(Windows.UI.Colors.White),
                                TextWrapping = TextWrapping.Wrap,
                                MaxWidth = 500
                            };

                            textPanel.Children.Add(authorText);
                            textPanel.Children.Add(timeText);
                            textPanel.Children.Add(commentText);

                            commentContainer.Children.Add(avatarBorder);
                            commentContainer.Children.Add(textPanel);

                            commentsPanel.Children.Add(commentContainer);
                        }
                    }
                }
                else
                {
                    var noCommentsText = new TextBlock
                    {
                        Text = "Комментарии недоступны",
                        Foreground = new SolidColorBrush(Windows.UI.Colors.Gray),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 20, 0, 20)
                    };
                    commentsPanel.Children.Add(noCommentsText);
                }

                scrollViewer.Content = commentsPanel;

                var dialog = new ContentDialog
                {
                    Title = "Комментарии",
                    Content = scrollViewer,
                    PrimaryButtonText = "Закрыть",
                    Background = new SolidColorBrush(Windows.UI.Colors.Black)
                };

                dialog.RequestedTheme = ElementTheme.Dark;
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                // В случае ошибки просто ничего не делаем
            }
        }

        private void VideoInfoButton_Click(object sender, RoutedEventArgs e)
        {
            ShowDescriptionButton_Click(sender, e);
        }

        private void CommentsContainerButton_Click(object sender, RoutedEventArgs e)
        {
            LastCommentButton_Click(sender, e);
        }

        private void LikeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _isLiked = !_isLiked;

                if (_isLiked)
                {
                    _currentLikes++;
                    LikeCountText.Text = FormatViewsCount(_currentLikes);
                }
                else
                {
                    _currentLikes = Math.Max(0, _currentLikes - 1);
                    LikeCountText.Text = FormatViewsCount(_currentLikes);
                }
            }
            catch (Exception ex)
            {
                // Обработка ошибки
            }
        }

        private async void ShareButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(_currentVideoId))
                {
                    string shareUrl = $"https://youtube.com/watch?v={_currentVideoId}";
                    string shareText = $"Посмотри это видео: {VideoTitleText.Text}";

                    var dialog = new ContentDialog
                    {
                        Title = "Поделиться видео",
                        Content = new StackPanel
                        {
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = shareText,
                                    TextWrapping = TextWrapping.Wrap,
                                    Foreground = new SolidColorBrush(Windows.UI.Colors.White),
                                    Margin = new Thickness(0, 0, 0, 10)
                                },
                                new TextBox
                                {
                                    Text = shareUrl,
                                    IsReadOnly = true,
                                    Foreground = new SolidColorBrush(Windows.UI.Colors.LightBlue)
                                }
                            }
                        },
                        PrimaryButtonText = "Копировать ссылку",
                        SecondaryButtonText = "Закрыть",
                        Background = new SolidColorBrush(Windows.UI.Colors.Black)
                    };

                    dialog.RequestedTheme = ElementTheme.Dark;

                    var result = await dialog.ShowAsync();
                    if (result == ContentDialogResult.Primary)
                    {
                        var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                        dataPackage.SetText(shareUrl);
                        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
                    }
                }
            }
            catch (Exception ex)
            {
                // Обработка ошибки
            }
        }

        private void VideoCard_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                if (button != null && button.DataContext != null)
                {
                    var videoInfo = button.DataContext as VideoInfo;
                    if (videoInfo != null && !string.IsNullOrEmpty(videoInfo.video_id))
                    {
                        _frame.Navigate(typeof(Video), videoInfo.video_id);
                    }
                }
            }
            catch (Exception) { }
        }

        private void ChannelPicture_Tapped(object sender, TappedRoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(VideoAuthorText.Text))
                {
                    _frame.Navigate(typeof(Channel), VideoAuthorText.Text);
                }
            }
            catch (Exception) { }
        }

        private void Window_SizeChanged(object sender, Windows.UI.Core.WindowSizeChangedEventArgs e)
        {
            UpdateVideoPlayerLayout();
        }

        private void UpdateVideoPlayerLayout()
        {
            var windowWidth = Window.Current.Bounds.Width;
            var windowHeight = Window.Current.Bounds.Height;
            bool isPortrait = windowHeight > windowWidth;

            if (isPortrait)
            {
                if (_isFullScreen)
                {
                    ToggleFullScreen();
                }

                PlayerColumn.Width = new GridLength(1, GridUnitType.Star);
                RelatedColumn.Width = new GridLength(0);
                RelatedPanel.Visibility = Visibility.Collapsed;
                RelatedPanelVertical.Visibility = Visibility.Visible;
                VideoPlayer.Height = windowWidth * 0.5625; // 16:9 aspect ratio

                PlayerInfoPanel.Margin = new Thickness(0);
            }
            else
            {
                if (!_isFullScreen)
                {
                    ToggleFullScreen();
                }

                PlayerColumn.Width = new GridLength(1, GridUnitType.Star);
                RelatedColumn.Width = new GridLength(400);
                RelatedPanel.Visibility = Visibility.Visible;
                RelatedPanelVertical.Visibility = Visibility.Collapsed;
                VideoPlayer.Height = 300;

                PlayerInfoPanel.Margin = new Thickness(0);
            }
        }

        private async void ToggleFullScreen()
        {
            if (VideoPlayer.MediaPlayer == null) return;

            _isFullScreen = !_isFullScreen;

            if (_isFullScreen)
            {
                VideoPlayer.AreTransportControlsEnabled = true;
                VideoPlayer.IsFullWindow = true;

                navbar.Visibility = Visibility.Collapsed;
                tabbar.Visibility = Visibility.Collapsed;
                RelatedPanel.Visibility = Visibility.Collapsed;
                RelatedPanelVertical.Visibility = Visibility.Collapsed;
                PlayerInfoPanel.Margin = new Thickness(0);
                var controls = VideoPlayer.TransportControls as CustomMediaTransportControls;
                if (controls != null) controls.IsFullscreen = true;
            }
            else
            {
                VideoPlayer.IsFullWindow = false;

                navbar.Visibility = Visibility.Visible;
                tabbar.Visibility = Visibility.Visible;

                var windowWidth = Window.Current.Bounds.Width;
                var windowHeight = Window.Current.Bounds.Height;
                bool isPortrait = windowHeight > windowWidth;

                if (isPortrait)
                {
                    RelatedPanel.Visibility = Visibility.Collapsed;
                    RelatedPanelVertical.Visibility = Visibility.Visible;
                }
                else
                {
                    RelatedPanel.Visibility = Visibility.Visible;
                    RelatedPanelVertical.Visibility = Visibility.Collapsed;
                }

                PlayerInfoPanel.Margin = new Thickness(0);
                var controls = VideoPlayer.TransportControls as CustomMediaTransportControls;
                if (controls != null) controls.IsFullscreen = false;
            }
        }

        private async void Controls_SettingsClicked(object sender, EventArgs e)
        {
            try
            {
                var flyoutContent = new StackPanel { Orientation = Orientation.Vertical };

                var speedHeader = new TextBlock { Text = "Скорость воспроизведения", Foreground = new SolidColorBrush(Windows.UI.Colors.White), Margin = new Thickness(0, 0, 0, 4) };

                var speedCombo = new ComboBox { Width = 160 };
                speedCombo.Items.Add("0.5x");
                speedCombo.Items.Add("0.75x");
                speedCombo.Items.Add("1.0x");
                speedCombo.Items.Add("1.25x");
                speedCombo.Items.Add("1.5x");
                speedCombo.Items.Add("1.75x");
                speedCombo.Items.Add("2.0x");

                double currentRate = 1.0;
                if (VideoPlayer?.MediaPlayer != null)
                {
                    currentRate = VideoPlayer.MediaPlayer.PlaybackSession.PlaybackRate;
                }

                string currentRateText = $"{currentRate:0.##}x";
                int foundIndex = -1;
                for (int i = 0; i < speedCombo.Items.Count; i++)
                {
                    if ((string)speedCombo.Items[i] == currentRateText)
                    {
                        foundIndex = i;
                        break;
                    }
                }
                speedCombo.SelectedIndex = foundIndex >= 0 ? foundIndex : 2; // default 1.0x

                speedCombo.SelectionChanged += (s, args) =>
                {
                    try
                    {
                        if (VideoPlayer?.MediaPlayer == null) return;
                        var selected = (string)speedCombo.SelectedItem;
                        if (string.IsNullOrEmpty(selected)) return;
                        var rateString = selected.Replace("x", "");
                        double rate;
                        if (double.TryParse(rateString, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out rate))
                        {
                            VideoPlayer.MediaPlayer.PlaybackSession.PlaybackRate = rate;
                        }
                    }
                    catch { }
                };

                var qualityHeader = new TextBlock { Text = "Качество", Foreground = new SolidColorBrush(Windows.UI.Colors.White), Margin = new Thickness(0, 12, 0, 4) };
                var qualityCombo = new ComboBox { Width = 160 };
                qualityCombo.Items.Add("Стандарт");
                qualityCombo.Items.Add("144");
                qualityCombo.Items.Add("360");
                qualityCombo.Items.Add("480");
                qualityCombo.Items.Add("720");
                qualityCombo.Items.Add("1080");

                int selectedIndex = 0; // Default to "Стандарт"

                if (string.IsNullOrEmpty(_currentQuality))
                {
                    selectedIndex = 0;
                }
                else
                {
                    for (int i = 1; i < qualityCombo.Items.Count; i++)
                    {
                        if ((string)qualityCombo.Items[i] == _currentQuality)
                        {
                            selectedIndex = i;
                            break;
                        }
                    }
                }

                qualityCombo.SelectedIndex = selectedIndex;

                qualityCombo.SelectionChanged += (s, args) =>
                {
                    try
                    {
                        var q = (string)qualityCombo.SelectedItem;
                        if (string.IsNullOrEmpty(q)) return;

                        string newQuality = null;
                        if (q != "Стандарт")
                        {
                            newQuality = q;
                        }

                        System.Diagnostics.Debug.WriteLine($"Quality selected: {q}, internal quality: {newQuality ?? "standard"}");
                        ChangeVideoQuality(newQuality);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error changing quality: {ex.Message}");
                    }
                };

                flyoutContent.Children.Add(speedHeader);
                flyoutContent.Children.Add(speedCombo);
                flyoutContent.Children.Add(qualityHeader);
                flyoutContent.Children.Add(qualityCombo);

                var flyout = new Flyout
                {
                    Content = flyoutContent,
                    Placement = FlyoutPlacementMode.Bottom
                };

                var anchor = sender as FrameworkElement;
                if (anchor == null)
                {
                    anchor = VideoPlayer;
                }
                flyout.ShowAt(anchor);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Controls_SettingsClicked: {ex.Message}");
            }
        }

        private void ApplyAndPlayCurrentUrlWithQuality()
        {
            try
            {
                if (string.IsNullOrEmpty(_currentVideoUrl) && !string.IsNullOrEmpty(_currentVideoId))
                {
                    _currentVideoUrl = Config.GetVideoUrl(_currentVideoId);
                }

                if (string.IsNullOrEmpty(_currentVideoUrl)) return;

                string videoUrl;

                if (string.IsNullOrEmpty(_currentQuality))
                {
                    videoUrl = Config.GetVideoUrl(_currentVideoId);
                    System.Diagnostics.Debug.WriteLine($"Using standard quality for video {_currentVideoId}");
                }
                else
                {
                    videoUrl = Config.GetVideoUrl(_currentVideoId, _currentQuality);
                    System.Diagnostics.Debug.WriteLine($"Using quality {_currentQuality} for video {_currentVideoId}");
                }

                System.Diagnostics.Debug.WriteLine($"Setting video source to: {videoUrl}");

                try
                {
                    var mediaSource = MediaSource.CreateFromUri(new Uri(videoUrl));
                    VideoPlayer.Source = mediaSource;
                    VideoPlayer.MediaPlayer.Play();
                    System.Diagnostics.Debug.WriteLine("Video source set successfully");
                }
                catch (System.Runtime.InteropServices.COMException comEx)
                {
                    System.Diagnostics.Debug.WriteLine($"COM Exception setting video source: {comEx.Message}");
                    System.Diagnostics.Debug.WriteLine($"HRESULT: 0x{comEx.HResult:X8}");

                    if (!string.IsNullOrEmpty(_currentQuality))
                    {
                        System.Diagnostics.Debug.WriteLine("Trying fallback to standard quality due to COM exception");
                        var fallbackUrl = Config.GetVideoUrl(_currentVideoId);
                        try
                        {
                            var fallbackSource = MediaSource.CreateFromUri(new Uri(fallbackUrl));
                            VideoPlayer.Source = fallbackSource;
                            VideoPlayer.MediaPlayer.Play();
                            _currentQuality = null;
                            System.Diagnostics.Debug.WriteLine($"Fallback to standard quality successful: {fallbackUrl}");
                        }
                        catch (Exception fallbackEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"Fallback also failed: {fallbackEx.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"General exception setting video source: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ApplyAndPlayCurrentUrlWithQuality: {ex.Message}");
            }
        }

        private async void ChangeVideoQuality(string newQuality)
        {
            try
            {
                _isChangingQuality = true;

                if (_isFullScreen)
                {
                    ToggleFullScreen();
                }

                TimeSpan currentPosition = TimeSpan.Zero;
                double currentRate = 1.0;
                bool wasPlaying = false;

                if (VideoPlayer?.MediaPlayer?.PlaybackSession != null)
                {
                    try
                    {
                        currentPosition = VideoPlayer.MediaPlayer.Position;
                        currentRate = VideoPlayer.MediaPlayer.PlaybackSession.PlaybackRate;
                        wasPlaying = VideoPlayer.MediaPlayer.CurrentState == MediaPlayerState.Playing;

                        VideoPlayer.MediaPlayer.Pause();

                        System.Diagnostics.Debug.WriteLine($"Saved playback state: position={currentPosition.TotalSeconds}s, rate={currentRate}, was playing={wasPlaying}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error saving playback state: {ex.Message}");
                    }
                }

                _currentQuality = newQuality;

                System.Diagnostics.Debug.WriteLine($"Starting quality change to: {newQuality ?? "standard"}");

                await Task.Delay(500);

                ApplyAndPlayCurrentUrlWithQuality();

                var timeout = TimeSpan.FromSeconds(30);
                var startTime = DateTime.Now;
                bool mediaReady = false;

                var mediaOpenedTask = new TaskCompletionSource<bool>();

                TypedEventHandler<MediaPlayer, object> mediaOpenedHandler = (s, e) =>
                {
                    mediaOpenedTask.TrySetResult(true);
                };

                try
                {
                    if (VideoPlayer?.MediaPlayer != null)
                    {
                        VideoPlayer.MediaPlayer.MediaOpened += mediaOpenedHandler;
                    }

                    var timeoutTask = Task.Delay(timeout);
                    var completedTask = await Task.WhenAny(mediaOpenedTask.Task, timeoutTask);

                    if (completedTask == mediaOpenedTask.Task)
                    {
                        mediaReady = true;
                        System.Diagnostics.Debug.WriteLine("Media opened successfully");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Timeout waiting for media to open");
                    }
                }
                finally
                {
                    if (VideoPlayer?.MediaPlayer != null)
                    {
                        VideoPlayer.MediaPlayer.MediaOpened -= mediaOpenedHandler;
                    }
                }

                VideoPlayer.Visibility = Visibility.Visible;

                if (mediaReady && VideoPlayer?.MediaPlayer != null)
                {
                    try
                    {
                        await Task.Delay(500);

                        if (currentPosition.TotalSeconds > 0 && currentPosition <= _videoDuration)
                        {
                            VideoPlayer.MediaPlayer.Position = currentPosition;
                        }
                        VideoPlayer.MediaPlayer.PlaybackSession.PlaybackRate = currentRate;

                        if (wasPlaying)
                        {
                            VideoPlayer.MediaPlayer.Play();
                        }

                        System.Diagnostics.Debug.WriteLine($"Restored playback state: position={currentPosition.TotalSeconds}s, rate={currentRate}, playing={wasPlaying}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error restoring playback state: {ex.Message}");
                        try
                        {
                            if (wasPlaying)
                            {
                                VideoPlayer.MediaPlayer.Play();
                            }
                        }
                        catch { }
                    }
                }
                else if (VideoPlayer?.MediaPlayer != null)
                {
                    try
                    {
                        if (wasPlaying)
                        {
                            VideoPlayer.MediaPlayer.Play();
                        }
                        System.Diagnostics.Debug.WriteLine("Media not fully ready, but attempting to play");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error starting playback: {ex.Message}");

                        if (!string.IsNullOrEmpty(_currentQuality))
                        {
                            System.Diagnostics.Debug.WriteLine($"Playback failed with quality {_currentQuality}, trying standard quality as fallback");
                            _currentQuality = null;

                            try
                            {
                                ApplyAndPlayCurrentUrlWithQuality();
                                await Task.Delay(1000);
                                if (wasPlaying)
                                {
                                    VideoPlayer.MediaPlayer.Play();
                                }
                            }
                            catch (Exception ex2)
                            {
                                System.Diagnostics.Debug.WriteLine($"Standard quality fallback also failed: {ex2.Message}");
                            }
                        }
                    }
                }

                var loadingTime = (DateTime.Now - startTime).TotalSeconds;
                System.Diagnostics.Debug.WriteLine($"Quality changed to: {newQuality ?? "standard"} - Loading time: {loadingTime} seconds, Media ready: {mediaReady}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error changing quality: {ex.Message}");

                VideoPlayer.Visibility = Visibility.Visible;

                try
                {
                    if (VideoPlayer?.MediaPlayer != null)
                    {
                        VideoPlayer.MediaPlayer.Play();
                    }
                }
                catch { }
            }
            finally
            {
                _isChangingQuality = false;
            }
        }

        private string ReplaceOrAddQueryParameter(string url, string key, string value)
        {
            try
            {
                if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(key)) return url;

                int hashIndex = url.IndexOf('#');
                string fragment = hashIndex >= 0 ? url.Substring(hashIndex) : string.Empty;
                string urlNoFrag = hashIndex >= 0 ? url.Substring(0, hashIndex) : url;

                int qIndex = urlNoFrag.IndexOf('?');
                string basePart = qIndex >= 0 ? urlNoFrag.Substring(0, qIndex) : urlNoFrag;
                string queryString = qIndex >= 0 ? urlNoFrag.Substring(qIndex + 1) : string.Empty;

                var parts = new List<string>();
                if (!string.IsNullOrEmpty(queryString))
                {
                    var raw = queryString.Split('&');
                    foreach (var p in raw)
                    {
                        if (string.IsNullOrEmpty(p)) continue;
                        var eqIndex = p.IndexOf('=');
                        string k = eqIndex >= 0 ? p.Substring(0, eqIndex) : p;
                        if (!k.Equals(key, StringComparison.OrdinalIgnoreCase))
                        {
                            parts.Add(p);
                        }
                    }
                }

                string encodedValue = Uri.EscapeDataString(value ?? string.Empty);
                parts.Add(key + "=" + encodedValue);

                string newQuery = string.Join("&", parts);
                string result = basePart + (newQuery.Length > 0 ? ("?" + newQuery) : string.Empty) + fragment;
                return result;
            }
            catch
            {
                return url;
            }
        }

        private string GetQueryParameter(string url, string key)
        {
            try
            {
                if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(key)) return null;

                int hashIndex = url.IndexOf('#');
                string urlNoFrag = hashIndex >= 0 ? url.Substring(0, hashIndex) : url;

                int qIndex = urlNoFrag.IndexOf('?');
                if (qIndex < 0) return null;
                string queryString = urlNoFrag.Substring(qIndex + 1);
                var raw = queryString.Split('&');
                foreach (var p in raw)
                {
                    if (string.IsNullOrEmpty(p)) continue;
                    var eqIndex = p.IndexOf('=');
                    string k = eqIndex >= 0 ? p.Substring(0, eqIndex) : p;
                    if (k.Equals(key, StringComparison.OrdinalIgnoreCase))
                    {
                        return eqIndex >= 0 ? Uri.UnescapeDataString(p.Substring(eqIndex + 1)) : string.Empty;
                    }
                }
                return null;
            }
            catch { return null; }
        }

        private string RemoveQueryParameter(string url, string key)
        {
            try
            {
                if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(key)) return url;

                int hashIndex = url.IndexOf('#');
                string fragment = hashIndex >= 0 ? url.Substring(hashIndex) : string.Empty;
                string urlNoFrag = hashIndex >= 0 ? url.Substring(0, hashIndex) : url;

                int qIndex = urlNoFrag.IndexOf('?');
                if (qIndex < 0)
                {
                    return url;
                }

                string basePart = urlNoFrag.Substring(0, qIndex);
                string queryString = urlNoFrag.Substring(qIndex + 1);

                var parts = new List<string>();
                if (!string.IsNullOrEmpty(queryString))
                {
                    var raw = queryString.Split('&');
                    foreach (var p in raw)
                    {
                        if (string.IsNullOrEmpty(p)) continue;
                        var eqIndex = p.IndexOf('=');
                        string k = eqIndex >= 0 ? p.Substring(0, eqIndex) : p;
                        if (!k.Equals(key, StringComparison.OrdinalIgnoreCase))
                        {
                            parts.Add(p);
                        }
                    }
                }

                string newQuery = string.Join("&", parts);
                string result = basePart + (newQuery.Length > 0 ? ("?" + newQuery) : string.Empty) + fragment;
                return result;
            }
            catch { return url; }
        }
    }

    public class VideoInfo
    {
        [JsonProperty("video_id")]
        public string video_id { get; set; }

        [JsonProperty("title")]
        public string title { get; set; }

        [JsonProperty("author")]
        public string author { get; set; }

        [JsonProperty("thumbnail")]
        public string thumbnail { get; set; }

        [JsonProperty("channel_thumbnail")]
        public string channel_thumbnail { get; set; }

        [JsonProperty("views")]
        public string Views { get; set; }

        [JsonProperty("published_at")]
        public string PublishedAt { get; set; }

        public string Title => title;
        public string Author => author;
        public string Thumbnail => thumbnail;
        public string ChannelThumbnail => channel_thumbnail;
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

        [JsonProperty("subscriberCount")]
        public string SubscriberCount { get; set; }
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
}