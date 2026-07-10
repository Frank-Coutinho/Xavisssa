using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Xavissa.Frontend.ViewModels;

namespace Xavissa.Frontend.Converters
{
    public class CustomDateEnabledConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is HistoryViewModel.FilterOption option
                && option == HistoryViewModel.FilterOption.Custom;
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture
        )
        {
            return Avalonia.Data.BindingOperations.DoNothing;
        }
    }
}
