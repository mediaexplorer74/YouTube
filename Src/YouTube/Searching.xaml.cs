using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace YouTube
{
    // Вспомогательный класс для правильной десериализации ответа от сервера
    public class SuggestionsResponse
    {
        [JsonProperty("query")]
        public string Query { get; set; }

        [JsonProperty("suggestions")]
        public List<List<object>> Suggestions { get; set; }
    }



    public sealed partial class Searching : Page
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private string _apiBaseUrl = Config.ApiBaseUrl; // Убедитесь, что этот класс и поле существуют
        private string _initialSearchQuery;
        private const string SEARCH_HISTORY_SETTING = "SearchHistory";
        private CancellationTokenSource _suggestionsCancellationTokenSource;
        private List<SearchHistoryItem> _searchHistory;

        public class SearchHistoryItem
        {
            public string SearchText { get; set; }
            public DateTime Timestamp { get; set; }
        }

        public Searching()
        {
            this.InitializeComponent();
            this.Loaded += Searching_Loaded;
            this.Unloaded += Searching_Unloaded;
            _searchHistory = LoadSearchHistory();
            UpdateSearchHistoryVisibility();
        }

        private List<SearchHistoryItem> LoadSearchHistory()
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            if (localSettings.Values.ContainsKey(SEARCH_HISTORY_SETTING))
            {
                string json = localSettings.Values[SEARCH_HISTORY_SETTING].ToString();
                return JsonConvert.DeserializeObject<List<SearchHistoryItem>>(json) ?? new List<SearchHistoryItem>();
            }
            return new List<SearchHistoryItem>();
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

        private void RemoveHistoryItem_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null)
            {
                var historyItem = button.DataContext as SearchHistoryItem;
                if (historyItem != null)
                {
                    _searchHistory.Remove(historyItem);
                    SaveSearchHistory();
                    UpdateSearchHistoryVisibility();
                }
            }
        }

        private void HistoryItem_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null)
            {
                var historyItem = button.DataContext as SearchHistoryItem;
                if (historyItem != null)
                {
                    SearchInput.Text = historyItem.SearchText;
                    PerformSearch();
                }
            }
        }

        private void UpdateSearchHistoryVisibility()
        {
            if (string.IsNullOrWhiteSpace(SearchInput.Text))
            {
                if (_searchHistory != null && _searchHistory.Any())
                {
                    SearchHistoryListView.ItemsSource = _searchHistory;
                    SearchHistoryListView.Visibility = Visibility.Visible;
                }
                else
                {
                    SearchHistoryListView.Visibility = Visibility.Collapsed;
                }
                SuggestionsListView.Visibility = Visibility.Collapsed;
            }
            else
            {
                SearchHistoryListView.Visibility = Visibility.Collapsed;
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter != null)
            {
                _initialSearchQuery = e.Parameter.ToString();
                SearchInput.Text = _initialSearchQuery;
                PerformSearch(); // Сразу выполняем поиск при переходе на страницу с параметром
            }
            UpdateBackButtonVisibility();
            UpdateSearchHistoryVisibility();
        }

        private void Searching_Loaded(object sender, RoutedEventArgs e)
        {
            SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
            UpdateBackButtonVisibility();
        }

        private void Searching_Unloaded(object sender, RoutedEventArgs e)
        {
            SystemNavigationManager.GetForCurrentView().BackRequested -= OnBackRequested;
        }

        private void OnBackRequested(object sender, BackRequestedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                e.Handled = true;
                Frame.GoBack();
            }
        }

        private void UpdateBackButtonVisibility()
        {
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                Frame.CanGoBack ? AppViewBackButtonVisibility.Visible : AppViewBackButtonVisibility.Collapsed;
        }

        private void SearchInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = SearchInput.Text;
            if (string.IsNullOrWhiteSpace(searchText))
            {
                UpdateSearchHistoryVisibility();
                return;
            }

            _suggestionsCancellationTokenSource?.Cancel();
            _suggestionsCancellationTokenSource = new CancellationTokenSource();
            var token = _suggestionsCancellationTokenSource.Token;

            // Асинхронно запускаем получение подсказок
            GetSearchSuggestions(searchText, token);
        }

        private async Task GetSearchSuggestions(string query, CancellationToken cancellationToken)
        {
            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;
                if (!localSettings.Values.ContainsKey("YouTubeApiKey"))
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, UpdateSearchHistoryVisibility);
                    return;
                }

                string apiKey = localSettings.Values["YouTubeApiKey"].ToString();
                string suggestionsUrl = $"{_apiBaseUrl}get_search_suggestions.php?query={Uri.EscapeDataString(query)}&apikey={apiKey}";

                var responseString = await _httpClient.GetStringAsync(suggestionsUrl);

                if (cancellationToken.IsCancellationRequested) return;

                // Десериализуем в сложный объект
                var responseData = JsonConvert.DeserializeObject<SuggestionsResponse>(responseString);

                var suggestionsList = new List<string>();
                if (responseData != null && responseData.Suggestions != null)
                {
                    // Извлекаем из него только нужные нам строки
                    suggestionsList = responseData.Suggestions
                        .Select(item => item.FirstOrDefault()?.ToString())
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToList();
                }

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    if (cancellationToken.IsCancellationRequested) return;

                    if (suggestionsList.Any())
                    {
                        SuggestionsListView.ItemsSource = suggestionsList;
                        SuggestionsListView.Visibility = Visibility.Visible;
                        SearchHistoryListView.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        SuggestionsListView.Visibility = Visibility.Collapsed;
                    }
                });
            }
            catch (TaskCanceledException)
            {
                // Игнорируем отмену задачи
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetSearchSuggestions: {ex}");
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    SuggestionsListView.Visibility = Visibility.Collapsed;
                });
            }
        }

        private void SearchInput_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                PerformSearch();
            }
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            PerformSearch();
        }

        private void PerformSearch()
        {
            var searchText = SearchInput.Text;
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                SuggestionsListView.Visibility = Visibility.Collapsed; // Скрываем подсказки
                AddToSearchHistory(searchText);

                // --- ИЗМЕНЕНИЕ ---
                // Выполняем навигацию на страницу Search.xaml, передавая поисковый запрос
                Frame.Navigate(typeof(Search), searchText);
            }
        }

        private void HomeTab_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(MainPage));
        }

        private void HistoryTab_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(MainPage), "history");
        }

        private void SettingsTab_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(MainPage), "settings");
        }

        private void VideoCard_Click(object sender, RoutedEventArgs e)
        {
            var clickedButton = sender as Button;
            if (clickedButton != null)
            {
                var videoInfo = clickedButton.DataContext as VideoInfo;
                if (videoInfo != null)
                {
                    // Обработка нажатия на видео
                }
            }
        }

        private void SuggestionItem_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null)
            {
                var selectedSuggestion = button.DataContext as string;
                if (!string.IsNullOrEmpty(selectedSuggestion))
                {
                    SearchInput.Text = selectedSuggestion;
                    // --- ИЗМЕНЕНИЕ ---
                    // Вызываем общий метод поиска, который выполнит навигацию на Search.xaml
                    PerformSearch();
                }
            }
        }
    }
}