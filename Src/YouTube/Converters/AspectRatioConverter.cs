using System;
using Windows.UI.Xaml.Data;

namespace YouTube.Converters
{
    public class AspectRatioConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var width = value as double?;
            var ratioStr = parameter as string;
            
            if (width.HasValue && ratioStr != null)
            {
                double aspectRatio;
                if (double.TryParse(ratioStr, out aspectRatio))
                {
                    return width.Value * aspectRatio;
                }
            }
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
} 