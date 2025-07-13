using System;

namespace MusicBeeRemote.ApiDebugger.Models;

/// <summary>
/// Direction of a recorded message.
/// </summary>
public enum MessageDirection
{
    /// <summary>
    /// Message sent from app to plugin (request).
    /// </summary>
    AppToPlugin,

    /// <summary>
    /// Message sent from plugin to app (response or push notification).
    /// </summary>
    PluginToApp
}

/// <summary>
/// A single recorded message from a proxy session.
/// </summary>
public class RecordedMessage
{
    /// <summary>
    /// When the message was intercepted.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Direction of the message.
    /// </summary>
    public MessageDirection Direction { get; set; }

    /// <summary>
    /// The raw JSON content of the message.
    /// </summary>
    public string RawJson { get; set; } = "";
}
