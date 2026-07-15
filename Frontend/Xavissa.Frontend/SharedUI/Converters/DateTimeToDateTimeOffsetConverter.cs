using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Xavissa.Frontend.Converters
{
    public class NullableDateConverter : Avalonia.Data.Converters.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value; // Already DateTime or null
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture
        )
        {
            if (value is DateTime dt)
                return (DateTime?)dt;
            return null;
        }
    }
}
