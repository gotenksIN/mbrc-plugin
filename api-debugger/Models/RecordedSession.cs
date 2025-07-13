using System;
using System.Collections.Generic;

namespace MusicBeeRemote.ApiDebugger.Models;

/// <summary>
/// A recorded proxy session containing request-response pairs.
/// </summary>
public class RecordedSession
{
    /// <summary>
    /// Unique identifier for the session.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// User-friendly name for the session (e.g., "Release v1.0").
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Optional description of the session.
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// When the session was recorded.
    /// </summary>
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The target host that was proxied.
    /// </summary>
    public string TargetHost { get; set; } = "";

    /// <summary>
    /// The target port that was proxied.
    /// </summary>
    public int TargetPort { get; set; }

    /// <summary>
    /// The recorded request-response pairs.
    /// </summary>
    public List<RequestResponsePair> Pairs { get; set; } = new();

    /// <summary>
    /// Gets a display string for the session.
    /// </summary>
    public string DisplayName => string.IsNullOrEmpty(Name)
        ? $"Session {RecordedAt:yyyy-MM-dd HH:mm}"
        : $"{Name} ({Pairs.Count} pairs)";
}
