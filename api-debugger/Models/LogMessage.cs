using System;
using System.Globalization;

namespace MusicBeeRemote.ApiDebugger.Models;

/// <summary>
/// Type of log message for color coding.
/// </summary>
public enum LogMessageType
{
    Info,
    Sent,
    Received,
    Success,
    Warning,
    Error
}

/// <summary>
/// Represents a log message with metadata for rich display.
/// </summary>
public class LogMessage
{
    /// <summary>
    /// Gets the timestamp when the message was logged.
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Gets the type of message for color coding.
    /// </summary>
    public LogMessageType Type { get; }

    /// <summary>
    /// Gets the message content.
    /// </summary>
    public string Content { get; }

    /// <summary>
    /// Gets the original JSON data if this is a sent or received message.
    /// </summary>
    public string? OriginalJson { get; }

    /// <summary>
    /// Gets whether this message has JSON data attached.
    /// </summary>
    public bool HasJson => !string.IsNullOrEmpty(OriginalJson);

    /// <summary>
    /// Gets the JSON indicator for display.
    /// </summary>
    public string JsonIndicator => HasJson ? "{}" : "";

    /// <summary>
    /// Gets the formatted timestamp string with milliseconds.
    /// </summary>
    public string TimestampText => Timestamp.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);

    /// <summary>
    /// Gets the direction indicator.
    /// </summary>
    public string DirectionIndicator => Type switch
    {
        LogMessageType.Sent => "\u2192",     // →
        LogMessageType.Received => "\u2190", // ←
        _ => "\u2022"                         // •
    };

    /// <summary>
    /// Gets the color for the message based on type.
    /// </summary>
    public string Color => Type switch
    {
        LogMessageType.Sent => "#569CD6",     // Blue
        LogMessageType.Received => "#6A9955", // Green
        LogMessageType.Success => "#4EC9B0",  // Teal
        LogMessageType.Warning => "#DCDCAA",  // Yellow
        LogMessageType.Error => "#F44747",    // Red
        _ => "#9CDCFE"                        // Light blue (info)
    };

    /// <summary>
    /// Gets the color for the direction indicator.
    /// </summary>
    public string DirectionColor => Type switch
    {
        LogMessageType.Sent => "#569CD6",     // Blue
        LogMessageType.Received => "#6A9955", // Green
        _ => "#808080"                        // Gray
    };

    /// <summary>
    /// Creates a new log message.
    /// </summary>
    public LogMessage(LogMessageType type, string content, string? originalJson = null)
    {
        Timestamp = DateTime.Now;
        Type = type;
        Content = content;
        OriginalJson = originalJson;
    }
}
