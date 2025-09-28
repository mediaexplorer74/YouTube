using System;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media.Imaging;

namespace YouTube.Converters
{
    public class ImageSourceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string urlString && !string.IsNullOrEmpty(urlString))
            {
                try
                {
                    return new BitmapImage(new Uri(urlString));
                }
                catch
                {
                    // Возвращаем placeholder изображение при ошибке
                    return new BitmapImage(new Uri("ms-appx:///Assets/placeholder.png"));
                }
            }
            
            return new BitmapImage(new Uri("ms-appx:///Assets/placeholder.png"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}