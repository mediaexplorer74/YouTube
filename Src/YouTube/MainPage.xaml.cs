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
using System.Threading.Tasks;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace YouTube
{
    public static class DialogManager
    {
        private static bool _isDialogShowing = false;
        private static ContentDialog _currentDialog;

        public static async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
        {
            if (_isDialogShowing)
            {
                return ContentDialogResult.None;
            }

            _isDialogShowing = true;
            try
            {
                if (_currentDialog != null)
                {
                    _currentDialog.Hide();
                    _currentDialog = null;
                }

                await Task.Delay(100); // Add small delay to ensure previous dialog is closed
                _currentDialog = dialog;
                var result = await dialog.ShowAsync();
                return result;
            }
            catch (Exception)
            {
                return ContentDialogResult.None;
            }
            finally
            {
                _currentDialog = null;
                _isDialogShowing = false;
            }
        }
    }

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private const string API_KEY_SETTING = "YouTubeApiKey";
        private Frame _frame;

        public MainPage()
        {
            this.InitializeComponent();
            _frame = Window.Current.Content as Frame;
            
            // Initialize navigation
            NavigationManager.InitializeTabBarNavigation(tabbar, _frame);
            NavigationManager.InitializeNavBarNavigation(navbar, _frame);

            // Check for API key on startup
            this.Loaded += MainPage_Loaded;

            // Show Home page by default
            ShowHomePage();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter != null)
            {
                string searchQuery = e.Parameter.ToString();
                if (!string.IsNullOrWhiteSpace(searchQuery))
                {
                    homepage.Visibility = Visibility.Collapsed;
                    searchpage.Visibility = Visibility.Visible;
                    searchpage.PerformSearch(searchQuery);
                }
            }
            else
            {
                ShowHomePage();
            }
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            await CheckAndRequestApiKey();
            
            // Show Home page by default
            homepage.Visibility = Visibility.Visible;
            searchpage.Visibility = Visibility.Collapsed;
            
            await Task.Delay(200); // Add delay before refreshing data
            await homepage.RefreshData();
        }

        private async Task CheckAndRequestApiKey()
        {
            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;
                if (!localSettings.Values.ContainsKey(API_KEY_SETTING))
                {
                    var dialog = new ContentDialog
                    {
                        Title = "YouTube API Key Required",
                        Content = new TextBox
                        {
                            PlaceholderText = "Enter your YouTube API Key",
                            Width = 300
                        },
                        PrimaryButtonText = "Save",
                        SecondaryButtonText = "Cancel"
                    };

                    var result = await DialogManager.ShowDialogAsync(dialog);
                    if (result == ContentDialogResult.Primary)
                    {
                        var apiKey = (dialog.Content as TextBox).Text;
                        if (!string.IsNullOrWhiteSpace(apiKey))
                        {
                            localSettings.Values[API_KEY_SETTING] = apiKey;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Handle errors silently
            }
        }

        private async void ShowHomePage()
        {
            try
            {
                homepage.Visibility = Visibility.Visible;
                searchpage.Visibility = Visibility.Collapsed;
                await Task.Delay(200); // Add delay before refreshing data
                await homepage.RefreshData();
            }
            catch (Exception)
            {
                // Handle errors silently
            }
        }

        private async void ShowSearchPage(string searchQuery)
        {
            try
            {
                homepage.Visibility = Visibility.Collapsed;
                searchpage.Visibility = Visibility.Visible;
                await Task.Delay(200); // Add delay before performing search
                searchpage.PerformSearch(searchQuery);
            }
            catch (Exception)
            {
                // Handle errors silently
            }
        }
    }
}
