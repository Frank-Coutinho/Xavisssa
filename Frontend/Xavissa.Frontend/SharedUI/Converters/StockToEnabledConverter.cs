using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Xavissa.Frontend.Converters
{
    public class StockToEnabledConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int qty)
                return qty > 0;
            return false;
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture
        ) => throw new NotImplementedException();
    }

    public class StockToButtonTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int qty)
                return qty > 0 ? "Add" : "Out of stock";
            return "Add";
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture
        ) => throw new NotImplementedException();
    }
}
