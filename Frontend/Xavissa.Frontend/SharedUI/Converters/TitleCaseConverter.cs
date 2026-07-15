using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Xavissa.Frontend.Converters
{
    public class TitleCaseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                TextInfo textInfo = CultureInfo.CurrentCulture.TextInfo;
                return textInfo.ToTitleCase(str.ToLower());
            }
            return value;
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture
        )
        {
            throw new NotImplementedException();
        }
    }
}
