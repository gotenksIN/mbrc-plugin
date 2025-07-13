using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using MusicBeeRemote.ApiDebugger.Models;

namespace MusicBeeRemote.ApiDebugger.ViewModels;

/// <summary>
/// Converts ComparisonStatus to a brush color for display.
/// </summary>
public class ComparisonStatusToBrushConverter : IValueConverter
{
    public static readonly ComparisonStatusToBrushConverter Instance = new();

    private static readonly SolidColorBrush MatchBrush = new(Color.Parse("#4EC9B0"));      // Teal - success
    private static readonly SolidColorBrush DifferentBrush = new(Color.Parse("#F44747")); // Red - error
    private static readonly SolidColorBrush OnlyInABrush = new(Color.Parse("#DCDCAA"));   // Yellow - warning
    private static readonly SolidColorBrush OnlyInBBrush = new(Color.Parse("#CE9178"));   // Orange - warning
    private static readonly SolidColorBrush NoDataBrush = new(Color.Parse("#808080"));    // Gray - neutral

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ComparisonStatus status)
            return NoDataBrush;

        return status switch
        {
            ComparisonStatus.Match => MatchBrush,
            ComparisonStatus.Different => DifferentBrush,
            ComparisonStatus.OnlyInA => OnlyInABrush,
            ComparisonStatus.OnlyInB => OnlyInBBrush,
            ComparisonStatus.NoData => NoDataBrush,
            _ => NoDataBrush
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
