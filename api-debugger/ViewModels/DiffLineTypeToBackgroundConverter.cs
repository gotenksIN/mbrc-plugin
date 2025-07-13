using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using MusicBeeRemote.ApiDebugger.Services;

namespace MusicBeeRemote.ApiDebugger.ViewModels;

/// <summary>
/// Converts DiffLineType to background brush for inline highlighting.
/// </summary>
public class DiffLineTypeToBackgroundConverter : IValueConverter
{
    public static readonly DiffLineTypeToBackgroundConverter Instance = new();

    // Subtle background colors that don't obscure text
    private static readonly IBrush AddedBackground = new SolidColorBrush(Color.FromArgb(30, 80, 200, 80));     // Subtle green
    private static readonly IBrush RemovedBackground = new SolidColorBrush(Color.FromArgb(30, 200, 80, 80));   // Subtle red
    private static readonly IBrush ModifiedBackground = new SolidColorBrush(Color.FromArgb(20, 200, 200, 80)); // Subtle yellow
    private static readonly IBrush UnchangedBackground = Brushes.Transparent;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DiffLineType lineType)
        {
            return lineType switch
            {
                DiffLineType.Added => AddedBackground,
                DiffLineType.Removed => RemovedBackground,
                DiffLineType.Modified => ModifiedBackground,
                _ => UnchangedBackground
            };
        }

        return UnchangedBackground;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts DiffLineType to foreground brush.
/// </summary>
public class DiffLineTypeToForegroundConverter : IValueConverter
{
    public static readonly DiffLineTypeToForegroundConverter Instance = new();

    private static readonly IBrush AddedForeground = new SolidColorBrush(Color.FromRgb(180, 255, 180));    // Light green
    private static readonly IBrush RemovedForeground = new SolidColorBrush(Color.FromRgb(255, 180, 180));  // Light red
    private static readonly IBrush ModifiedForeground = new SolidColorBrush(Color.FromRgb(212, 212, 212)); // Normal (segments handle highlighting)
    private static readonly IBrush UnchangedForeground = new SolidColorBrush(Color.FromRgb(212, 212, 212)); // Light gray

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DiffLineType lineType)
        {
            return lineType switch
            {
                DiffLineType.Added => AddedForeground,
                DiffLineType.Removed => RemovedForeground,
                DiffLineType.Modified => ModifiedForeground,
                _ => UnchangedForeground
            };
        }

        return UnchangedForeground;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts DiffLineType to a gutter indicator (+, -, ~, or space).
/// </summary>
public class DiffLineTypeToGutterConverter : IValueConverter
{
    public static readonly DiffLineTypeToGutterConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DiffLineType lineType)
        {
            return lineType switch
            {
                DiffLineType.Added => "+",
                DiffLineType.Removed => "-",
                DiffLineType.Modified => "~",
                _ => " "
            };
        }

        return " ";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts DiffSegment.IsChanged to background brush for inline value highlighting.
/// </summary>
public class DiffSegmentToBackgroundConverter : IValueConverter
{
    public static readonly DiffSegmentToBackgroundConverter Instance = new();

    // More prominent highlight for the actual changed value
    private static readonly IBrush ChangedBackground = new SolidColorBrush(Color.FromArgb(100, 255, 100, 100)); // Red highlight
    private static readonly IBrush UnchangedBackground = Brushes.Transparent;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isChanged && isChanged)
        {
            return ChangedBackground;
        }

        return UnchangedBackground;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts DiffSegment.IsChanged to foreground brush.
/// </summary>
public class DiffSegmentToForegroundConverter : IValueConverter
{
    public static readonly DiffSegmentToForegroundConverter Instance = new();

    private static readonly IBrush ChangedForeground = new SolidColorBrush(Color.FromRgb(255, 255, 255));  // White for contrast
    private static readonly IBrush UnchangedForeground = new SolidColorBrush(Color.FromRgb(212, 212, 212)); // Normal gray

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isChanged && isChanged)
        {
            return ChangedForeground;
        }

        return UnchangedForeground;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
