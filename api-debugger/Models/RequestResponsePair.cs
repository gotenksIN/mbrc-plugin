namespace MusicBeeRemote.ApiDebugger.Models;

/// <summary>
/// A paired request and response from a proxy session.
/// </summary>
public class RequestResponsePair
{
    /// <summary>
    /// Sequence number in the session (1-based).
    /// </summary>
    public int SequenceNumber { get; set; }

    /// <summary>
    /// The context/command name (e.g., "nowplayinglist", "playerstatus").
    /// </summary>
    public string Context { get; set; } = "";

    /// <summary>
    /// The client ID if detected from the protocol handshake.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// The request message (App → Plugin). Null for push notifications.
    /// </summary>
    public RecordedMessage? Request { get; set; }

    /// <summary>
    /// The response message (Plugin → App). Null if no response received.
    /// </summary>
    public RecordedMessage? Response { get; set; }

    /// <summary>
    /// Time between request and response in milliseconds.
    /// </summary>
    public int? ResponseTimeMs { get; set; }
}
