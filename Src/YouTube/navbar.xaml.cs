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
using Windows.UI.Core;
using Windows.UI.Xaml.Media.Imaging;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Threading;
using Windows.Storage;
using YouTube.Models;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace YouTube
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class navbar : Page
    {
        public event EventHandler<string> SearchRequested;
        public event EventHandler<string> SearchTextChanged;

        private Frame _rootFrame;
        private HttpClient _httpClient;
        private string _apiBaseUrl = Config.ApiBaseUrl;
        private CancellationTokenSource _suggestionsCancellationTokenSource;
        private const string SEARCH_HISTORY_SETTING = "SearchHistory";
        private List<SearchHistoryItem> _searchHistory;
        
        public class SearchHistoryItem
        {
            public string SearchText { get; set; }
            public DateTime Timestamp { get; set; }
        }

        public navbar()
        {
            this.InitializeComponent();
            this.Loaded += Navbar_Loaded;
            this.Unloaded += Navbar_Unloaded;
            Window.Current.SizeChanged += Window_SizeChanged;
            InitializeHttpClient();
            UpdateLayout();
            _searchHistory = LoadSearchHistory();
            // Debug to check if search history is loaded
            System.Diagnostics.Debug.WriteLine($"Loaded {_searchHistory.Count} search history items");
        }

        private void InitializeHttpClient()
        {
            try
            {
                _httpClient = new HttpClient();
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
                _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
                System.Diagnostics.Debug.WriteLine($"HTTP Client initialized with base URL: {_apiBaseUrl}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing HTTP client: {ex.Message}");
            }
        }

        private void Navbar_Loaded(object sender, RoutedEventArgs e)
        {
            _rootFrame = Window.Current.Content as Frame;
            UpdateLayout();
            UpdateSearchHistoryVisibility();
            System.Diagnostics.Debug.WriteLine("Navbar loaded - UpdateSearchHistoryVisibility called");
        }

        private void Navbar_Unloaded(object sender, RoutedEventArgs e)
        {
            Window.Current.SizeChanged -= Window_SizeChanged;
            _httpClient?.Dispose();
        }

        private void Window_SizeChanged(object sender, WindowSizeChangedEventArgs e)
        {
            UpdateLayout();
        }

        private void UpdateLayout()
        {
            var bounds = Window.Current.Bounds;
            if (bounds.Width < 800)
            {
                VerticalLayout.Visibility = Visibility.Visible;
                HorizontalLayout.Visibility = Visibility.Collapsed;
            }
            else
            {
                VerticalLayout.Visibility = Visibility.Collapsed;
                HorizontalLayout.Visibility = Visibility.Visible;
            }
        }

        private void ShowSearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (_rootFrame != null)
            {
                _rootFrame.Navigate(typeof(Searching));
            }
        }

        private void SuggestionItem_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null && button.DataContext is string)
            {
                string selectedSuggestion = button.DataContext as string;
                SearchInputHorizontal.Text = selectedSuggestion;
                SuggestionsList.Visibility = Visibility.Collapsed;
                if (_rootFrame != null)
                {
                    AddToSearchHistory(selectedSuggestion);
                    _rootFrame.Navigate(typeof(Search), selectedSuggestion);
                }
            }
        }

        private async void SearchInputHorizontal_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = SearchInputHorizontal.Text;
            if (string.IsNullOrWhiteSpace(searchText))
            {
                SuggestionsList.Visibility = Visibility.Collapsed;
                UpdateSearchHistoryVisibility();
                return;
            }

            // Cancel any pending suggestion requests
            _suggestionsCancellationTokenSource?.Cancel();
            _suggestionsCancellationTokenSource = new CancellationTokenSource();

            try
            {
                await GetSearchSuggestions(searchText, _suggestionsCancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting suggestions: {ex}");
            }
        }

        private async Task GetSearchSuggestions(string query, CancellationToken cancellationToken)
        {
            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;
                if (!localSettings.Values.ContainsKey("YouTubeApiKey"))
                {
                    return;
                }

                string apiKey = localSettings.Values["YouTubeApiKey"].ToString();
                string suggestionsUrl = $"{_apiBaseUrl}get_search_suggestions.php?query={Uri.EscapeDataString(query)}&apikey={apiKey}";

                var response = await _httpClient.GetStringAsync(suggestionsUrl);
                var suggestionsData = JsonConvert.DeserializeObject<SearchSuggestionsResponse>(response);

                if (!cancellationToken.IsCancellationRequested)
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        if (suggestionsData?.suggestions != null && suggestionsData.suggestions.Any())
                        {
                            SuggestionsList.ItemsSource = suggestionsData.suggestions.Select(s => s[0].ToString());
                            SuggestionsList.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            SuggestionsList.Visibility = Visibility.Collapsed;
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetSearchSuggestions: {ex}");
            }
        }

        private void SearchInputHorizontal_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                if (!string.IsNullOrWhiteSpace(SearchInputHorizontal.Text))
                {
                    if (_rootFrame != null)
                    {
                        AddToSearchHistory(SearchInputHorizontal.Text);
                        _rootFrame.Navigate(typeof(Search), SearchInputHorizontal.Text);
                    }
                }
            }
            else
            {
                SearchTextChanged?.Invoke(this, SearchInputHorizontal.Text);
            }
        }

        private void SearchButtonHorizontal_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(SearchInputHorizontal.Text))
            {
                if (_rootFrame != null)
                {
                    AddToSearchHistory(SearchInputHorizontal.Text);
                    _rootFrame.Navigate(typeof(Search), SearchInputHorizontal.Text);
                }
            }
        }
        
        private List<SearchHistoryItem> LoadSearchHistory()
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            List<SearchHistoryItem> history;
            
            if (localSettings.Values.ContainsKey(SEARCH_HISTORY_SETTING))
            {
                string json = localSettings.Values[SEARCH_HISTORY_SETTING].ToString();
                history = JsonConvert.DeserializeObject<List<SearchHistoryItem>>(json) ?? new List<SearchHistoryItem>();
            }
            else
            {
                history = new List<SearchHistoryItem>();
                
            }
            
            return history;
        }

        private void SaveSearchHistory()
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            string json = JsonConvert.SerializeObject(_searchHistory);
            localSettings.Values[SEARCH_HISTORY_SETTING] = json;
        }

        private void AddToSearchHistory(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText)) return;

            _searchHistory.RemoveAll(x => x.SearchText.Equals(searchText, StringComparison.OrdinalIgnoreCase));
            _searchHistory.Insert(0, new SearchHistoryItem
            {
                SearchText = searchText,
                Timestamp = DateTime.Now
            });

            if (_searchHistory.Count > 10)
            {
                _searchHistory = _searchHistory.Take(10).ToList();
            }

            SaveSearchHistory();
        }
        
        private void UpdateSearchHistoryVisibility()
        {
            if (string.IsNullOrWhiteSpace(SearchInputHorizontal.Text))
            {
                if (_searchHistory != null && _searchHistory.Any())
                {
                    HistoryList.ItemsSource = _searchHistory;
                    HistoryList.Visibility = Visibility.Visible;
                    SuggestionsList.Visibility = Visibility.Collapsed;
                    System.Diagnostics.Debug.WriteLine($"Showing {_searchHistory.Count} search history items");
                }
                else
                {
                    HistoryList.Visibility = Visibility.Collapsed;
                    System.Diagnostics.Debug.WriteLine("No search history items to show");
                }
            }
            else
            {
                HistoryList.Visibility = Visibility.Collapsed;
                System.Diagnostics.Debug.WriteLine("Search text is not empty, hiding search history");
            }
        }
        
        private void SearchInputHorizontal_GotFocus(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("SearchInputHorizontal got focus");
            if (string.IsNullOrWhiteSpace(SearchInputHorizontal.Text))
            {
                UpdateSearchHistoryVisibility();
                System.Diagnostics.Debug.WriteLine("Search input empty, showing history");
            }
        }
        
        private void HistoryItem_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null && button.DataContext is SearchHistoryItem)
            {
                var historyItem = button.DataContext as SearchHistoryItem;
                SearchInputHorizontal.Text = historyItem.SearchText;
                HistoryList.Visibility = Visibility.Collapsed;
                if (_rootFrame != null)
                {
                    _rootFrame.Navigate(typeof(Search), historyItem.SearchText);
                }
            }
        }
        
        private void RemoveHistoryItem_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null && button.DataContext is SearchHistoryItem)
            {
                var historyItem = button.DataContext as SearchHistoryItem;
                _searchHistory.Remove(historyItem);
                SaveSearchHistory();
                UpdateSearchHistoryVisibility();
            }
        }
    }

    public class SearchSuggestionsResponse
    {
        public string query { get; set; }
        public List<List<object>> suggestions { get; set; }
    }
}
