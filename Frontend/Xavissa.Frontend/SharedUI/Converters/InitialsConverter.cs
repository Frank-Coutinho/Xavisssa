using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Xavissa.Frontend.Converters
{
    public class InitialsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string name || string.IsNullOrWhiteSpace(name))
                return "";

            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
                return parts[0][0].ToString().ToUpper();
            return $"{parts[0][0]}{parts[1][0]}".ToUpper();
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture
        ) => throw new NotImplementedException();
    }
}
