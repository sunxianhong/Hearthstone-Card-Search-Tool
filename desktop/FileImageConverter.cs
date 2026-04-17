using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace HearthstoneCardSearchTool;

public sealed class FileImageConverter : IValueConverter
{
    private static readonly ConcurrentDictionary<string, BitmapImage> Cache = new(StringComparer.OrdinalIgnoreCase);

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        return Cache.GetOrAdd(path, static filePath =>
        {
            using var stream = File.OpenRead(filePath);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.DecodePixelWidth = 320;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        });
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
