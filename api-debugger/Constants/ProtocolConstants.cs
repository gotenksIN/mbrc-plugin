namespace MusicBeeRemote.ApiDebugger.Constants;

/// <summary>
/// Protocol constants for the API debugger
/// </summary>
internal static class ProtocolConstants
{
    /// <summary>
    /// The message terminator sequence used for all socket communications.
    /// Uses CRLF (Carriage Return + Line Feed) as per network protocol standards.
    /// </summary>
    public const string MessageTerminator = "\r\n";
}
