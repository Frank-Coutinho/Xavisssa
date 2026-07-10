using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Xavissa.Frontend.Converters
{
    public class HeaderImagePathToBitmapConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string path || string.IsNullOrWhiteSpace(path))
                return null;

            try
            {
                if (path.StartsWith("avares://", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith("pack://", StringComparison.OrdinalIgnoreCase))
                {
                    return new Bitmap(AssetLoader.Open(new Uri(path)));
                }

                if (System.IO.File.Exists(path))
                    return new Bitmap(path);
            }
            catch
            {
                return null;
            }

            return null;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
