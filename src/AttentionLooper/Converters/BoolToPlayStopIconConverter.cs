using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace AttentionLooper.Converters;

public class BoolToPlayStopIconConverter : IValueConverter
{
    // Play triangle
    private static readonly Geometry PlayGeometry =
        Geometry.Parse("M 3,0 L 15,8 L 3,16 Z");

    // Stop square
    private static readonly Geometry StopGeometry =
        Geometry.Parse("M 2,2 L 14,2 L 14,14 L 2,14 Z");

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? StopGeometry : PlayGeometry;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return DependencyProperty.UnsetValue;
    }
}
