using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Popups;
using Windows.Storage;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
using Windows.UI.Xaml.Input;
using Windows.Devices.Input;

namespace YouTube
{
    public sealed partial class AccountLogin : Page
    {
        private Frame _frame;
        private DispatcherTimer _pollingTimer;
        private const int PollingInterval = 2000;
        private string _lastContent = string.Empty;
        private bool _isAuthenticated = false;
        private Grid _profileGrid;
        private ProgressRing _loadingRing;

        // ⚡️ НОВОЕ: Переменная для хранения контейнера профиля
        private StackPanel _profileContainer;

        public AccountLogin()
        {
            this.InitializeComponent();
            _frame = Window.Current.Content as Frame;
            InitializePollingTimer();

            _loadingRing = new ProgressRing
            {
                IsActive = false,
                Width = 50,
                Height = 50,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            _loadingRing.Foreground = new SolidColorBrush(Windows.UI.Colors.White);

            Grid.SetRow(_loadingRing, 1);
            mainGrid.Children.Add(_loadingRing);

            NavigationManager.InitializeTabBarNavigation(tabbar, _frame);

            System.Diagnostics.Debug.WriteLine("AccountLogin initialized. Starting polling...");
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            string savedToken = ApplicationData.Current.LocalSettings.Values["AuthToken"] as string;

            if (!string.IsNullOrEmpty(savedToken))
            {
                Config.SetUserToken(savedToken);
                ShowAuthenticatedState();
                return;
            }

            _isAuthenticated = false;
            _lastContent = string.Empty;

            // Make sure QR container is visible for unauthorized users
            qrContainer.Visibility = Visibility.Visible;
            
            System.Diagnostics.Debug.WriteLine("Navigated to AccountLogin page. Starting polling...");

            if (_pollingTimer != null && !_pollingTimer.IsEnabled)
            {
                _pollingTimer.Start();
            }

            LoadAuthContent();
        }

        private async void ShowAuthenticatedState()
        {
            _isAuthenticated = true;
            _pollingTimer?.Stop();

            qrContainer.Visibility = Visibility.Collapsed;
            authImage.Visibility = Visibility.Collapsed;
            settingsButton.Visibility = Visibility.Collapsed;

            await FetchAndDisplayAccountInfo();

            System.Diagnostics.Debug.WriteLine("User already authenticated. Showing account info.");
        }

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new MessageDialog("Вы уверены, что хотите выйти?", "Подтверждение выхода");
            dialog.Commands.Add(new UICommand("Да", async (command) =>
            {
                ApplicationData.Current.LocalSettings.Values.Remove("AuthToken");
                Config.SetUserToken("");

                // ⚡️ ИСПРАВЛЕНИЕ: Удаляем _profileContainer из MainGrid
                if (_profileContainer != null)
                {
                    mainGrid.Children.Remove(_profileContainer);
                    _profileContainer = null;
                    _profileGrid = null; // Также сбрасываем _profileGrid
                }

                _isAuthenticated = false;
                _lastContent = string.Empty;

                if (_pollingTimer != null)
                {
                    _pollingTimer.Start();
                }

                LoadAuthContent();

                System.Diagnostics.Debug.WriteLine("User logged out successfully.");
            }));
            dialog.Commands.Add(new UICommand("Нет", null));

            await dialog.ShowAsync();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Navigating to Settings page");
            _frame.Navigate(typeof(Settings));
        }

        private void InitializePollingTimer()
        {
            _pollingTimer = new DispatcherTimer();
            _pollingTimer.Interval = TimeSpan.FromMilliseconds(PollingInterval);
            _pollingTimer.Tick += async (sender, e) => await CheckAuthContentAsync();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _pollingTimer?.Stop();
            System.Diagnostics.Debug.WriteLine("Navigation from AccountLogin page. Polling stopped.");
        }

        private async void LoadAuthContent()
        {
            try
            {
                string authUrl = Config.ApiBaseUrl + "auth";
                System.Diagnostics.Debug.WriteLine($"Sending initial request to: {authUrl}");

                using (var httpClient = new HttpClient())
                {
                    string response = await httpClient.GetStringAsync(authUrl);
                    string extractedContent = ExtractContentFromYtreq(response);
                    _lastContent = extractedContent;

                    System.Diagnostics.Debug.WriteLine($"Initial response received: {GetShortResponseInfo(extractedContent)}");

                    await ProcessAuthResponse(extractedContent);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading auth content: {ex.Message}");
            }
        }

        private async Task CheckAuthContentAsync()
        {
            if (Config.UserToken != "")
            {
                _pollingTimer?.Stop();
                System.Diagnostics.Debug.WriteLine("Already authenticated. Polling stopped.");
                return;
            }

            try
            {
                string authUrl = Config.ApiBaseUrl + "auth";
                System.Diagnostics.Debug.WriteLine($"Polling request to: {authUrl}");

                using (var httpClient = new HttpClient())
                {
                    string response = await httpClient.GetStringAsync(authUrl);
                    string extractedContent = ExtractContentFromYtreq(response);

                    System.Diagnostics.Debug.WriteLine($"Polling response: {GetShortResponseInfo(extractedContent)}");

                    if (extractedContent != _lastContent || string.IsNullOrEmpty(_lastContent))
                    {
                        System.Diagnostics.Debug.WriteLine("Response content changed. Processing...");
                        _lastContent = extractedContent;
                        await ProcessAuthResponse(extractedContent);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Response content unchanged. Skipping processing.");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking auth content: {ex.Message}");
            }
        }

        private string ExtractContentFromYtreq(string response)
        {
            try
            {
                var match = Regex.Match(response, @"<ytreq>(.*?)</ytreq>", RegexOptions.Singleline);
                if (match.Success && match.Groups.Count > 1)
                {
                    string content = match.Groups[1].Value.Trim();
                    content = Regex.Replace(content, @"<[^>]*>", string.Empty);
                    System.Diagnostics.Debug.WriteLine($"Extracted content from ytreq: {GetShortResponseInfo(content)}");
                    System.Diagnostics.Debug.WriteLine($"{response}");
                    return content;
                }
                System.Diagnostics.Debug.WriteLine("No <ytreq> tags found in response");
                return response;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error extracting content from ytreq: {ex.Message}");
                return response;
            }
        }

        private async Task ProcessAuthResponse(string response)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"Processing response: {GetResponseType(response)}");

                    if (response.StartsWith("Token:", StringComparison.OrdinalIgnoreCase))
                    {
                        string token = response.Replace("Token:", "").Trim();
                        System.Diagnostics.Debug.WriteLine($"Token received: {token.Substring(0, Math.Min(20, token.Length))}...");

                        _pollingTimer.Stop();
                        _isAuthenticated = true;

                        ApplicationData.Current.LocalSettings.Values["AuthToken"] = token;
                        Config.SetUserToken(token);

                        System.Diagnostics.Debug.WriteLine("Token saved to local settings and Config");

                        qrContainer.Visibility = Visibility.Collapsed;
                        authImage.Visibility = Visibility.Collapsed;
                        qrText.Visibility = Visibility.Collapsed;

                        await FetchAndDisplayAccountInfo();

                        System.Diagnostics.Debug.WriteLine("Showing account info UI");

                        var dialog = new MessageDialog("Аутентификация прошла успешно!", "Успех");
                        await dialog.ShowAsync();

                        System.Diagnostics.Debug.WriteLine("Success dialog shown");
                    }
                    else if (IsBase64Image(response))
                    {
                        System.Diagnostics.Debug.WriteLine("Base64 image received");

                        qrContainer.Visibility = Visibility.Visible;
                        authImage.Visibility = Visibility.Visible;

                        string base64String = response;
                        if (base64String.Contains("base64,"))
                        {
                            base64String = base64String.Split(new string[] { "base64," }, StringSplitOptions.None)[1];
                        }

                        byte[] imageBytes = Convert.FromBase64String(base64String);
                        System.Diagnostics.Debug.WriteLine($"Image bytes length: {imageBytes.Length}");

                        var bitmapImage = new BitmapImage();
                        using (var stream = new MemoryStream(imageBytes))
                        {
                            await bitmapImage.SetSourceAsync(stream.AsRandomAccessStream());
                        }

                        authImage.Source = bitmapImage;
                        System.Diagnostics.Debug.WriteLine("QR code image displayed");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Unknown auth response format. Length: {response.Length}");
                        System.Diagnostics.Debug.WriteLine($"Response preview: {GetResponsePreview(response)}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error processing auth response: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                }
            });
        }

        private async Task FetchAndDisplayAccountInfo()
        {
            // ⚡️ ИСПРАВЛЕНИЕ: Убеждаемся, что старый контейнер профиля удален
            if (_profileContainer != null)
            {
                mainGrid.Children.Remove(_profileContainer);
                _profileContainer = null;
            }

            qrContainer.Visibility = Visibility.Collapsed;

            _loadingRing.IsActive = true;
            _loadingRing.Visibility = Visibility.Visible;

            _profileContainer = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(10, 20, 10, 0)
            };

            Grid.SetRow(_profileContainer, 1);

            _profileGrid = new Grid();
            _profileGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _profileGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _profileGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _profileGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _profileGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            try
            {
                string apiUrl = Config.ApiBaseUrl + "account_info?token=" + Config.UserToken;
                System.Diagnostics.Debug.WriteLine($"Fetching account info from: {apiUrl}");

                using (var httpClient = new HttpClient())
                {
                    string jsonResponse = await httpClient.GetStringAsync(apiUrl);
                    var root = JObject.Parse(jsonResponse);

                    var googleAccount = root["google_account"] as JObject;
                    var youtubeChannel = root["youtube_channel"] as JObject;

                    string givenName = googleAccount?["given_name"]?.ToString() ?? "Unknown";
                    string customUrl = youtubeChannel?["custom_url"]?.ToString() ?? "";
                    string pictureUrl = googleAccount?["picture"]?.ToString() ?? "";

                    Border profileBorder = new Border
                    {
                        Width = 88,
                        Height = 88,
                        Margin = new Thickness(20),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        BorderThickness = new Thickness(1),
                        Child = new Image
                        {
                            Stretch = Stretch.UniformToFill
                        }
                    };

                    Image profileImage = profileBorder.Child as Image;

                    if (!string.IsNullOrEmpty(pictureUrl))
                    {
                        try
                        {
                            BitmapImage bitmap = new BitmapImage(new Uri(pictureUrl));
                            profileImage.Source = bitmap;

                            System.Diagnostics.Debug.WriteLine($"Profile image loaded from: {pictureUrl}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error loading profile image: {ex.Message}");
                        }
                    }

                    Grid.SetColumn(profileBorder, 0);
                    Grid.SetRow(profileBorder, 0);
                    _profileGrid.Children.Add(profileBorder);

                    StackPanel stackPanel = new StackPanel
                    {
                        Orientation = Orientation.Vertical,
                        Margin = new Thickness(20)
                    };
                    Grid.SetColumn(stackPanel, 1);
                    Grid.SetRow(stackPanel, 0);

                    TextBlock nameTextBlock = new TextBlock
                    {
                        Text = givenName,
                        FontSize = 24,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = new SolidColorBrush(Windows.UI.Colors.White)
                    };
                    stackPanel.Children.Add(nameTextBlock);

                    StackPanel urlPanel = new StackPanel
                    {
                        Orientation = Orientation.Vertical,
                        Margin = new Thickness(0, 5, 0, 0)
                    };

                    TextBlock customUrlTextBlock = new TextBlock
                    {
                        Text = customUrl,
                        FontSize = 12,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = new SolidColorBrush(Windows.UI.Colors.White)
                    };
                    urlPanel.Children.Add(customUrlTextBlock);

                    TextBlock channelLinkTextBlock = new TextBlock
                    {
                        Text = "Перейти на канал >",
                        FontSize = 12,
                        Foreground = new SolidColorBrush(Windows.UI.Colors.Gray),
                        Margin = new Thickness(0, 5, 0, 0)
                    };
                    channelLinkTextBlock.PointerPressed += (sender, args) =>
                    {
                        if (!string.IsNullOrEmpty(customUrl))
                        {
                            System.Diagnostics.Debug.WriteLine($"Navigating to Channel page with customUrl: {customUrl}");
                            _frame.Navigate(typeof(Channel), customUrl);
                        }
                    };
                    urlPanel.Children.Add(channelLinkTextBlock);

                    stackPanel.Children.Add(urlPanel);

                    StackPanel buttonPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Margin = new Thickness(0, 10, 0, 0)
                    };

                    Button settingsButton = new Button
                    {
                        Content = "Настройки",
                        Background = new SolidColorBrush(Windows.UI.Colors.DarkGray),
                        Foreground = new SolidColorBrush(Windows.UI.Colors.White),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Margin = new Thickness(0, 0, 10, 0)
                    };
                    settingsButton.Click += SettingsButton_Click;
                    buttonPanel.Children.Add(settingsButton);

                    Button logoutButton = new Button
                    {
                        Content = "Выйти",
                        Background = new SolidColorBrush(Windows.UI.Colors.DarkGray),
                        Foreground = new SolidColorBrush(Windows.UI.Colors.White),
                        HorizontalAlignment = HorizontalAlignment.Left
                    };
                    logoutButton.Click += LogoutButton_Click;
                    buttonPanel.Children.Add(logoutButton);

                    stackPanel.Children.Add(buttonPanel);

                    _profileGrid.Children.Add(stackPanel);

                    TextBlock historyTextBlock = new TextBlock
                    {
                        Text = "История",
                        FontSize = 24,
                        Margin = new Thickness(20, 10, 0, 0),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Foreground = new SolidColorBrush(Windows.UI.Colors.White)
                    };
                    Grid.SetColumn(historyTextBlock, 0);
                    Grid.SetRow(historyTextBlock, 1);
                    _profileGrid.Children.Add(historyTextBlock);

                    _profileContainer.Children.Add(_profileGrid);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching or displaying account info: {ex.Message}");
                TextBlock errorText = new TextBlock
                {
                    Text = "Ошибка загрузки данных аккаунта",
                    FontSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Windows.UI.Colors.White)
                };
                _profileContainer.Children.Add(errorText);
            }
            finally
            {
                _loadingRing.IsActive = false;
                _loadingRing.Visibility = Visibility.Collapsed;
            }

            mainGrid.Children.Add(_profileContainer);
            Grid.SetRow(_profileContainer, 1);

            Grid historyContainer = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            Grid.SetRow(historyContainer, 2);
            Grid.SetColumnSpan(historyContainer, 2);
            _profileGrid.Children.Add(historyContainer);

            ProgressRing historyLoading = new ProgressRing
            {
                IsActive = true,
                Visibility = Visibility.Visible,
                Width = 50,
                Height = 50,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0)
            };
            historyContainer.Children.Add(historyLoading);

            await FetchHistoryAsync(historyContainer, historyLoading);
        }

        private async Task FetchHistoryAsync(Grid container, ProgressRing loading)
        {
            try
            {
                string historyUrl = Config.ApiBaseUrl + "get_history.php?token=" + Config.UserToken;
                System.Diagnostics.Debug.WriteLine($"Fetching history from: {historyUrl}");

                using (var httpClient = new HttpClient())
                {
                    string jsonResponse = await httpClient.GetStringAsync(historyUrl);
                    JArray videos = JArray.Parse(jsonResponse);

                    ScrollViewer scrollViewer = new ScrollViewer
                    {
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch
                    };

                    StackPanel videoPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Margin = new Thickness(10)
                    };

                    scrollViewer.Content = videoPanel;

                    foreach (var video in videos)
                    {
                        string thumbnailUrl = video["thumbnail"]?.ToString();
                        string title = video["title"]?.ToString();
                        string videoId = video["video_id"]?.ToString();

                        if (string.IsNullOrEmpty(thumbnailUrl) || string.IsNullOrEmpty(title) || string.IsNullOrEmpty(videoId)) continue;

                        StackPanel card = new StackPanel
                        {
                            Orientation = Orientation.Vertical,
                            Margin = new Thickness(10),
                            Width = 150,
                            Tag = videoId
                        };

                        card.PointerPressed += (sender, args) =>
                        {
                            var currentPoint = args.GetCurrentPoint(card);
                            if (currentPoint.PointerDevice.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse && currentPoint.Properties.IsLeftButtonPressed)
                            {
                                var clickedCard = sender as StackPanel;
                                if (clickedCard != null && clickedCard.Tag != null)
                                {
                                    string vidId = clickedCard.Tag.ToString();
                                    System.Diagnostics.Debug.WriteLine($"Navigating to video: {vidId}");
                                    _frame.Navigate(typeof(Video), vidId);
                                    args.Handled = true;
                                }
                            }
                        };

                        card.Tapped += (sender, args) =>
                        {
                            var tappedCard = sender as StackPanel;
                            if (tappedCard != null && tappedCard.Tag != null)
                            {
                                string vidId = tappedCard.Tag.ToString();
                                System.Diagnostics.Debug.WriteLine($"Navigating to video via Tapped event: {vidId}");
                                _frame.Navigate(typeof(Video), vidId);
                                args.Handled = true;
                            }
                        };

                        // Create a Grid to hold both the thumbnail and overlay images
                        Grid thumbnailContainer = new Grid
                        {
                            Width = 150,
                            Height = 84
                        };

                        Image thumbnailImage = new Image
                        {
                            Width = 150,
                            Height = 84,
                            Stretch = Stretch.UniformToFill
                        };

                        BitmapImage bitmap = new BitmapImage(new Uri(thumbnailUrl));
                        thumbnailImage.Source = bitmap;

                        // Add the thumbnail image to the grid
                        thumbnailContainer.Children.Add(thumbnailImage);

                        // Create and add the overlay image
                        Image overlayImage = new Image
                        {
                            Width = 150,
                            Height = 84,
                            Stretch = Stretch.Fill,
                            Source = new BitmapImage(new Uri("ms-appx:///Assets/rounding_up.png"))
                        };

                        thumbnailContainer.Children.Add(overlayImage);

                        card.Children.Add(thumbnailContainer);

                        TextBlock titleBlock = new TextBlock
                        {
                            Text = title,
                            TextWrapping = TextWrapping.Wrap,
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            MaxLines = 2,
                            FontSize = 12,
                            Foreground = new SolidColorBrush(Windows.UI.Colors.White)
                        };

                        card.Children.Add(titleBlock);

                        videoPanel.Children.Add(card);
                    }

                    container.Children.Add(scrollViewer);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching history: {ex.Message}");
                TextBlock errorText = new TextBlock
                {
                    Text = "Ошибка загрузки истории",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Windows.UI.Colors.White)
                };
                container.Children.Add(errorText);
            }
            finally
            {
                loading.Visibility = Visibility.Collapsed;
            }
        }

        private string GetShortResponseInfo(string response)
        {
            if (string.IsNullOrEmpty(response))
                return "Empty response";
            if (response.StartsWith("Token:", StringComparison.OrdinalIgnoreCase))
                return $"Token response (length: {response.Length})";
            if (IsBase64Image(response))
                return $"Base64 image (length: {response.Length})";
            return $"Unknown format (length: {response.Length})";
        }

        private string GetResponseType(string response)
        {
            if (string.IsNullOrEmpty(response))
                return "Empty";
            if (response.StartsWith("Token:", StringComparison.OrdinalIgnoreCase))
                return "Token";
            if (IsBase64Image(response))
                return "Base64 Image";
            return "Unknown";
        }

        private string GetResponsePreview(string response)
        {
            if (string.IsNullOrEmpty(response))
                return "Empty";
            if (response.Length <= 100)
                return response;
            return response.Substring(0, 100) + "...";
        }

        private bool IsBase64Image(string s)
        {
            if (string.IsNullOrEmpty(s))
                return false;
            if (s.Contains("base64,") || (s.Length > 100 && !s.Contains(" ")))
            {
                try
                {
                    string testString = s;
                    if (testString.Contains("base64,"))
                    {
                        testString = testString.Split(new string[] { "base64," }, StringSplitOptions.None)[1];
                    }
                    Convert.FromBase64String(testString);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }
    }
}