using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Newtonsoft.Json;
using Windows.UI.Core;
using Windows.UI.Xaml;
using YouTube.Models;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace YouTube
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Search : Page
    {
        private const string API_KEY_SETTING = "YouTubeApiKey";
        private List<VideoInfo> searchResults;
        private Frame _frame;
        private string _lastSearchQuery;

        public Search()
        {
            this.InitializeComponent();
            searchResults = new List<VideoInfo>();
            _frame = Window.Current.Content as Frame;
            // Загружаем настройку отображения иконок каналов из локальных настроек
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            if (localSettings.Values.ContainsKey("EnableChannelThumbnails"))
                Config.EnableChannelThumbnails = (bool)localSettings.Values["EnableChannelThumbnails"];
            // Initialize navigation
            NavigationManager.InitializeTabBarNavigation(tabbar, _frame);
            this.Loaded += Search_Loaded;
            this.Unloaded += Search_Unloaded;
        }

        private void Search_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Register for back button press
                SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
                UpdateBackButtonVisibility();
            }
            catch (Exception)
            {
                // Handle any initialization errors silently
            }
        }

        private void Search_Unloaded(object sender, RoutedEventArgs e)
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

        private void TabBar_HomeTabClicked(object sender, EventArgs e)
        {
            if (_frame != null)
            {
                _frame.Navigate(typeof(MainPage));
            }
        }

        private void TabBar_HistoryTabClicked(object sender, EventArgs e)
        {
            if (_frame != null)
            {
                _frame.Navigate(typeof(MainPage), "history");
            }
        }

        private void TabBar_SettingsTabClicked(object sender, EventArgs e)
        {
            if (_frame != null)
            {
                _frame.Navigate(typeof(MainPage), "settings");
            }
        }

        private void NavBar_SearchRequested(object sender, string searchText)
        {
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                PerformSearch(searchText);
            }
        }

        private void NavBar_SearchTextChanged(object sender, string searchText)
        {
            // Handle search text changes if needed
        }

        // Event handlers for YouTube-style search interface
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null)
            {
                // Show/hide clear button based on text
                ClearButton.Visibility = string.IsNullOrEmpty(textBox.Text) ? Visibility.Collapsed : Visibility.Visible;
                
                // Show suggestions panel when typing
                if (!string.IsNullOrEmpty(textBox.Text) && textBox.Text.Length > 0)
                {
                    // Here you could implement search suggestions
                    // For now, we'll just hide the panel
                    SuggestionsPanel.Visibility = Visibility.Collapsed;
                }
                else
                {
                    SuggestionsPanel.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void SearchTextBox_KeyUp(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                var textBox = sender as TextBox;
                if (textBox != null && !string.IsNullOrWhiteSpace(textBox.Text))
                {
                    PerformSearch(textBox.Text);
                    SuggestionsPanel.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Text = string.Empty;
            ClearButton.Visibility = Visibility.Collapsed;
            SuggestionsPanel.Visibility = Visibility.Collapsed;
        }

        private void SuggestionsList_ItemClick(object sender, ItemClickEventArgs e)
        {
            var suggestion = e.ClickedItem as string;
            if (!string.IsNullOrEmpty(suggestion))
            {
                SearchTextBox.Text = suggestion;
                PerformSearch(suggestion);
                SuggestionsPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateUI(bool dataLoaded)
        {
            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;

            if (dataLoaded)
            {
                SearchResultsGrid.Visibility = Visibility.Visible;
                ErrorPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                SearchResultsGrid.Visibility = Visibility.Collapsed;
                ErrorPanel.Visibility = Visibility.Visible;
            }
        }

        public async void PerformSearch(string query)
        {
            _lastSearchQuery = query; // Store the last query
            
            // Update the search textbox if it's different from current text
            if (SearchTextBox != null && SearchTextBox.Text != query)
            {
                SearchTextBox.Text = query;
            }
            
            UpdateUI(true);
            LoadingRing.IsActive = true;
            LoadingRing.Visibility = Visibility.Visible;

            SearchResultsGrid.Visibility = Visibility.Collapsed; // Hide content while loading
            ErrorPanel.Visibility = Visibility.Collapsed; // Hide error while loading
            SuggestionsPanel.Visibility = Visibility.Collapsed; // Hide suggestions while searching

            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;
                if (!localSettings.Values.ContainsKey(API_KEY_SETTING))
                {
                    System.Diagnostics.Debug.WriteLine("API key is missing");
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        UpdateUI(false);
                    });
                    return;
                }

                string apiKey = localSettings.Values[API_KEY_SETTING].ToString();
                string searchUrl = Config.ApiBaseUrl + $"get_search_videos.php?query={Uri.EscapeDataString(query)}&apikey={Uri.EscapeDataString(apiKey)}";

                using (var client = new HttpClient())
                {
                    try
                    {
                        var response = await client.GetStringAsync(searchUrl);
                        var newResults = JsonConvert.DeserializeObject<List<VideoInfo>>(response);
                        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            searchResults.Clear();
                            if (newResults != null)
                                searchResults.AddRange(newResults);
                            SearchResultsGrid.ItemsSource = null;
                            SearchResultsGrid.ItemsSource = searchResults;
                            UpdateUI(searchResults.Count > 0);
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error in PerformSearch: {ex}");
                        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            UpdateUI(false);
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in PerformSearch: {ex}");
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    UpdateUI(false);
                });
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter != null)
            {
                string query = e.Parameter.ToString();
                PerformSearch(query);
            }
            UpdateBackButtonVisibility();
        }

        private void VideosListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var clickedVideo = e.ClickedItem as VideoInfo;
            if (clickedVideo != null)
            {
                _frame.Navigate(typeof(Video), clickedVideo.video_id);
            }
        }

        private async void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_lastSearchQuery))
            {
                PerformSearch(_lastSearchQuery);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("No previous search query to retry.");
            }
        }

        public void ShowSearchResults(List<VideoInfo> results)
        {
            if (SearchResultsGrid != null)
            {
                SearchResultsGrid.ItemsSource = null;
                SearchResultsGrid.ItemsSource = results;
            }
        }
    }
}
