using System;
using Windows.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;
using Newtonsoft.Json;
using YouTube.Models;

namespace YouTube
{
    public static class ViewHistory
    {
        private const string HISTORY_SETTING = "ViewHistory";
        private const int MAX_HISTORY_ITEMS = 100;

        public static async Task<List<VideoInfo>> GetHistory()
        {
            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;
                if (localSettings.Values.ContainsKey(HISTORY_SETTING))
                {
                    string historyJson = localSettings.Values[HISTORY_SETTING].ToString();
                    return JsonConvert.DeserializeObject<List<VideoInfo>>(historyJson) ?? new List<VideoInfo>();
                }
            }
            catch (Exception)
            {
                // Handle any errors silently
            }
            return new List<VideoInfo>();
        }

        public static async Task AddToHistory(VideoInfo video)
        {
            try
            {
                var history = await GetHistory();
                
                // Remove if already exists
                history.RemoveAll(v => v.video_id == video.video_id);
                
                // Add to beginning of list
                history.Insert(0, video);
                
                // Keep only last MAX_HISTORY_ITEMS
                if (history.Count > MAX_HISTORY_ITEMS)
                {
                    history = history.GetRange(0, MAX_HISTORY_ITEMS);
                }

                // Save to settings
                var localSettings = ApplicationData.Current.LocalSettings;
                localSettings.Values[HISTORY_SETTING] = JsonConvert.SerializeObject(history);
            }
            catch (Exception)
            {
                // Handle any errors silently
            }
        }

        public static async Task ClearHistory()
        {
            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;
                if (localSettings.Values.ContainsKey(HISTORY_SETTING))
                {
                    localSettings.Values.Remove(HISTORY_SETTING);
                }
            }
            catch (Exception)
            {
                // Handle any errors silently
            }
        }
    }

    public static class NavigationManager
    {
        private static EventHandler _homeTabHandler;
        private static EventHandler _accountTabHandler;
        private static EventHandler _historyTabHandler;
        private static EventHandler _settingsTabHandler;
        private static EventHandler<string> _searchRequestedHandler;
        private static EventHandler<string> _searchTextChangedHandler;

        public static void InitializeTabBarNavigation(tabbar tabbar, Frame frame)
        {
            if (tabbar != null)
            {
                System.Diagnostics.Debug.WriteLine("Initializing TabBar Navigation");
                
                // Remove existing handlers if any
                if (_homeTabHandler != null)
                    tabbar.HomeTabClicked -= _homeTabHandler;
                if (_accountTabHandler != null)
                    tabbar.AccountTabClicked -= _accountTabHandler;

                // Create and store new handlers
                _homeTabHandler = (sender, e) => 
                {
                    System.Diagnostics.Debug.WriteLine("Home Tab Clicked");
                    NavigateToHome(frame);
                };
                _accountTabHandler = (sender, e) =>
                {
                    System.Diagnostics.Debug.WriteLine("Account Tab Clicked");
                    NavigateToAccount(frame);
                };
                _historyTabHandler = (sender, e) => 
                {
                    System.Diagnostics.Debug.WriteLine("History Tab Clicked");
                    NavigateToHistory(frame);
                };
                _settingsTabHandler = (sender, e) => 
                {
                    System.Diagnostics.Debug.WriteLine("Settings Tab Clicked");
                    NavigateToSettings(frame);
                };

                // Add new handlers
                tabbar.HomeTabClicked += _homeTabHandler;
                tabbar.AccountTabClicked += _accountTabHandler;

                System.Diagnostics.Debug.WriteLine("TabBar Navigation Initialized");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("TabBar is null");
            }
        }

        public static void InitializeNavBarNavigation(navbar navbar, Frame frame)
        {
            if (navbar != null)
            {
                System.Diagnostics.Debug.WriteLine("Initializing NavBar Navigation");
                
                // Remove existing handlers if any
                if (_searchRequestedHandler != null)
                    navbar.SearchRequested -= _searchRequestedHandler;
                if (_searchTextChangedHandler != null)
                    navbar.SearchTextChanged -= _searchTextChangedHandler;

                // Create and store new handlers
                _searchRequestedHandler = (sender, searchText) => 
                {
                    System.Diagnostics.Debug.WriteLine($"Search Requested: {searchText}");
                    NavigateToSearch(frame, searchText);
                };
                _searchTextChangedHandler = (sender, searchText) => 
                {
                    System.Diagnostics.Debug.WriteLine($"Search Text Changed: {searchText}");
                };

                // Add new handlers
                navbar.SearchRequested += _searchRequestedHandler;
                navbar.SearchTextChanged += _searchTextChangedHandler;

                System.Diagnostics.Debug.WriteLine("NavBar Navigation Initialized");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("NavBar is null");
            }
        }

        private static void NavigateToHome(Frame frame)
        {
            if (frame != null)
            {
                System.Diagnostics.Debug.WriteLine("Navigating to Home");
                frame.Navigate(typeof(MainPage));
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Frame is null when trying to navigate to Home");
            }
        }

        private static void NavigateToAccount(Frame frame)
        {
            if (frame != null)
            {
                System.Diagnostics.Debug.WriteLine("Navigating to Account");
                frame.Navigate(typeof(AccountLogin));
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Frame is null when trying to navigate to Account");
            }
        }

        private static void NavigateToHistory(Frame frame)
        {
            if (frame != null)
            {
                System.Diagnostics.Debug.WriteLine("Navigating to History");
                frame.Navigate(typeof(History));
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Frame is null when trying to navigate to History");
            }
        }

        private static void NavigateToSettings(Frame frame)
        {
            if (frame != null)
            {
                System.Diagnostics.Debug.WriteLine("Navigating to Settings");
                frame.Navigate(typeof(Settings));
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Frame is null when trying to navigate to Settings");
            }
        }

        private static void NavigateToSearch(Frame frame, string searchText)
        {
            if (frame != null && !string.IsNullOrWhiteSpace(searchText))
            {
                System.Diagnostics.Debug.WriteLine($"Navigating to Search with query: {searchText}");
                frame.Navigate(typeof(Search), searchText);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Frame is null or search text is empty when trying to navigate to Search");
            }
        }
    }
} 