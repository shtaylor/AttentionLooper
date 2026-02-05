using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace AttentionLooper.Converters;

/// <summary>
/// Converts IsMuted (bool) to a speaker/muted icon geometry.
/// </summary>
public class BoolToMuteIconConverter : IValueConverter
{
    // Speaker icon (unmuted)
    private static readonly Geometry SpeakerGeometry =
        Geometry.Parse("M 2,5 L 5,5 L 9,1 L 9,15 L 5,11 L 2,11 Z M 11,4 Q 14,8 11,12");

    // Muted speaker icon (with X)
    private static readonly Geometry MutedGeometry =
        Geometry.Parse("M 2,5 L 5,5 L 9,1 L 9,15 L 5,11 L 2,11 Z M 11,5 L 15,11 M 15,5 L 11,11");

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? MutedGeometry : SpeakerGeometry;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return DependencyProperty.UnsetValue;
    }
}

/// <summary>
/// Converts a double (0.0-1.0) to a percentage string like "65%".
/// </summary>
public class VolumeToPercentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d)
            return $"{(int)(d * 100)}%";
        return "0%";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return DependencyProperty.UnsetValue;
    }
}
