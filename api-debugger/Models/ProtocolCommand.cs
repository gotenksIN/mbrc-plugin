namespace MusicBeeRemote.ApiDebugger.Models;

/// <summary>
/// Represents a protocol command with display name for the UI.
/// </summary>
public class ProtocolCommand
{
    /// <summary>
    /// Gets or sets the command context string sent to the API.
    /// </summary>
    public string Command { get; set; }

    /// <summary>
    /// Gets or sets the display name shown in the UI dropdown.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Creates a new protocol command.
    /// </summary>
    public ProtocolCommand(string command, string displayName)
    {
        Command = command;
        DisplayName = $"{command} - {displayName}";
    }
}
