using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace MusicBeeRemote.ApiDebugger.ViewModels;

/// <summary>
/// Converts a boolean to a brush for active/inactive tab styling.
/// </summary>
public class BoolToBrushConverter : IValueConverter
{
    public static readonly BoolToBrushConverter Instance = new();

    private static readonly SolidColorBrush ActiveBrush = new(Color.Parse("#4EC9B0"));   // Teal - active
    private static readonly SolidColorBrush InactiveBrush = new(Color.Parse("#808080")); // Gray - inactive

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isActive)
        {
            return isActive ? ActiveBrush : InactiveBrush;
        }

        return InactiveBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
