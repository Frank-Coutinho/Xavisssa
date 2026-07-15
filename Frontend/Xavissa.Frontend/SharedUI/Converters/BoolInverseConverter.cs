using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Xavissa.Frontend.Converters
{
    public class BoolInverseConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is bool b && !b;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
