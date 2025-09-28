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
using Windows.Storage;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.ApplicationModel;
using Windows.UI.Core;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace YouTube
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Settings : Page, INotifyPropertyChanged
    {
        private const string API_KEY_SETTING = "YouTubeApiKey";
        private const string API_BASE_URL_SETTING = "ApiBaseUrl";
        private const string CHANNEL_THUMBNAILS_SETTING = "EnableChannelThumbnails";

        private string _apiBaseUrl;
        private string _apiKey;
        private Frame _frame;
        private bool _enableChannelThumbnails = true;

        public event PropertyChangedEventHandler PropertyChanged;

        public string ApiBaseUrl
        {
            get { return _apiBaseUrl; }
            set
            {
                if (_apiBaseUrl != value)
                {
                    _apiBaseUrl = value;
                    OnPropertyChanged();
                    SaveApiBaseUrl();
                }
            }
        }

        public string ApiKey
        {
            get { return _apiKey; }
            set
            {
                if (_apiKey != value)
                {
                    _apiKey = value;
                    OnPropertyChanged();
                    SaveApiKey();
                }
            }
        }

        public bool EnableChannelThumbnails
        {
            get { return _enableChannelThumbnails; }
            set
            {
                if (_enableChannelThumbnails != value)
                {
                    _enableChannelThumbnails = value;
                    OnPropertyChanged();
                    SaveChannelThumbnailsSetting();
                }
            }
        }

        public Settings()
        {
            this.InitializeComponent();
            _frame = Window.Current.Content as Frame;

            // Initialize navigation
            NavigationManager.InitializeTabBarNavigation(tabbar, _frame);
            NavigationManager.InitializeNavBarNavigation(navbar, _frame);

            // Load settings
            LoadSettings();

            // Set version info
            var version = Package.Current.Id.Version;
            VersionInfoText.Text = $"Version: {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Update back button visibility when navigating to this page
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                _frame.CanGoBack ?
                AppViewBackButtonVisibility.Visible :
                AppViewBackButtonVisibility.Collapsed;
        }

        private void LoadSettings()
        {
            var localSettings = ApplicationData.Current.LocalSettings;

            // Load API Base URL
            if (localSettings.Values.ContainsKey(API_BASE_URL_SETTING))
            {
                ApiBaseUrl = localSettings.Values[API_BASE_URL_SETTING].ToString();
            }
            else
            {
                ApiBaseUrl = Config.ApiBaseUrl;
            }

            // Load API Key
            if (localSettings.Values.ContainsKey(API_KEY_SETTING))
            {
                ApiKey = localSettings.Values[API_KEY_SETTING].ToString();
            }

            // Load channel thumbnails toggle
            if (localSettings.Values.ContainsKey(CHANNEL_THUMBNAILS_SETTING))
            {
                EnableChannelThumbnails = (bool)localSettings.Values[CHANNEL_THUMBNAILS_SETTING];
            }
            else
            {
                EnableChannelThumbnails = Config.EnableChannelThumbnails;
            }
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void SaveApiBaseUrl()
        {
            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;
                localSettings.Values[API_BASE_URL_SETTING] = ApiBaseUrl;
                Config.ApiBaseUrl = ApiBaseUrl;
            }
            catch (Exception)
            {
                // Handle error silently
            }
        }

        private void SaveApiKey()
        {
            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;
                localSettings.Values[API_KEY_SETTING] = ApiKey;
            }
            catch (Exception)
            {
                // Handle error silently
            }
        }

        private void SaveChannelThumbnailsSetting()
        {
            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;
                localSettings.Values[CHANNEL_THUMBNAILS_SETTING] = EnableChannelThumbnails;
                Config.EnableChannelThumbnails = EnableChannelThumbnails;
            }
            catch (Exception)
            {
                // Handle error silently
            }
        }

        private void SaveApiBaseUrl_Click(object sender, RoutedEventArgs e)
        {
            SaveApiBaseUrl();
            ShowSaveSuccessMessage("API Base URL saved successfully");
        }

        private void SaveApiKey_Click(object sender, RoutedEventArgs e)
        {
            SaveApiKey();
            ShowSaveSuccessMessage("API Key saved successfully");
        }

        private async void ShowSaveSuccessMessage(string message)
        {
            var dialog = new ContentDialog
            {
                Title = "Success",
                Content = message,
                PrimaryButtonText = "OK"
            };
            await dialog.ShowAsync();
        }

        private async void ShowErrorMessage(string message)
        {
            var dialog = new ContentDialog
            {
                Title = "Error",
                Content = message,
                PrimaryButtonText = "OK"
            };
            await dialog.ShowAsync();
        }

        private void NavBar_SearchRequested(object sender, string searchText)
        {
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                _frame.Navigate(typeof(Search), searchText);
            }
        }

        private void NavBar_SearchTextChanged(object sender, string searchText)
        {
            // Handle search text changes if needed
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
                _frame.Navigate(typeof(History));
            }
        }

        private void TabBar_SettingsTabClicked(object sender, EventArgs e)
        {
            // Already on settings page
        }

        private void ApiBaseUrlTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ApiBaseUrlTextBox != null)
            {
                ApiBaseUrl = ApiBaseUrlTextBox.Text;
            }
        }

        private void ApiKeyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ApiKeyTextBox != null)
            {
                ApiKey = ApiKeyTextBox.Text;
            }
        }

        private void ChannelThumbnailsToggle_Toggled(object sender, RoutedEventArgs e)
        {
            EnableChannelThumbnails = ChannelThumbnailsToggle.IsOn;
        }
    }
}