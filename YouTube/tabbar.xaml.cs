using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace YouTube
{
    public sealed partial class tabbar : Page
    {
        public event EventHandler HomeTabClicked;
        public event EventHandler ShortsTabClicked;
        public event EventHandler SubscriptionsTabClicked;
        public event EventHandler AccountTabClicked;

        public tabbar()
        {
            this.InitializeComponent();
            InitializeTabBar();
        }

        private void InitializeTabBar()
        {
            // Делаем кнопки Shorts и Подписки неактивными и серыми
            SetButtonInactive(ShortsButton, "Shorts");
            SetButtonInactive(SubscriptionsButton, "Подписки");
        }

        private void SetButtonInactive(Button button, string text)
        {
            button.IsEnabled = false;

            // Находим TextBlock в StackPanel и меняем его цвет на серый
            var stackPanel = button.Content as StackPanel;
            if (stackPanel != null)
            {
                foreach (var child in stackPanel.Children)
                {
                    var textBlock = child as TextBlock;
                    if (textBlock != null && textBlock.Text == text)
                    {
                        textBlock.Foreground = new SolidColorBrush(Colors.Gray);
                        break;
                    }
                }
            }

            // Также делаем изображение полупрозрачным
            var panel = button.Content as StackPanel;
            if (panel != null)
            {
                foreach (var child in panel.Children)
                {
                    var image = child as Image;
                    if (image != null)
                    {
                        image.Opacity = 0.5;
                        break;
                    }
                }
            }
        }

        private void HomeTab_Click(object sender, RoutedEventArgs e)
        {
            HomeTabClicked?.Invoke(this, EventArgs.Empty);
        }

        private void ShortsTab_Click(object sender, RoutedEventArgs e)
        {
            ShortsTabClicked?.Invoke(this, EventArgs.Empty);
        }

        private void SubscriptionsTab_Click(object sender, RoutedEventArgs e)
        {
            SubscriptionsTabClicked?.Invoke(this, EventArgs.Empty);
        }

        private void AccountButton_Click(object sender, RoutedEventArgs e)
        {
            AccountTabClicked?.Invoke(this, EventArgs.Empty);
        }
    }
}