using System.Globalization;
using System.Windows.Data;

namespace HearthstoneCardSearchTool;

public sealed class StringHasValueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is string text && !string.IsNullOrWhiteSpace(text);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
