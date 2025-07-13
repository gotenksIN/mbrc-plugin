using System;

namespace MusicBeeRemote.ApiDebugger.Models;

/// <summary>
/// Represents the type of client connecting to the API.
/// </summary>
public enum ClientType
{
    Android,
    iOS,
    Windows,
    Generic
}

/// <summary>
/// Contains connection information for a discovered or configured MusicBee Remote endpoint.
/// </summary>
public class ConnectionInfo
{
    /// <summary>
    /// Gets or sets the IP address of the endpoint.
    /// </summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the port number of the endpoint.
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Gets or sets the display name of the endpoint.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when this endpoint was discovered.
    /// </summary>
    public DateTime DiscoveredAt { get; set; }

    /// <summary>
    /// Gets or sets whether currently connected to this endpoint.
    /// </summary>
    public bool IsConnected { get; set; }

    /// <summary>
    /// Gets or sets the client type for this connection.
    /// </summary>
    public ClientType ClientType { get; set; }

    /// <summary>
    /// Gets a display-friendly name for the connection.
    /// </summary>
    public string DisplayName => string.IsNullOrEmpty(Name)
        ? $"{IpAddress}:{Port}"
        : $"{Name} ({IpAddress}:{Port})";

    /// <summary>
    /// Creates a new instance with default values.
    /// </summary>
    public ConnectionInfo()
    {
        DiscoveredAt = DateTime.Now;
        ClientType = ClientType.Generic;
    }

    /// <summary>
    /// Creates a new instance with the specified connection details.
    /// </summary>
    public ConnectionInfo(string ipAddress, int port, string name = "", ClientType clientType = ClientType.Generic)
        : this()
    {
        IpAddress = ipAddress;
        Port = port;
        Name = name;
        ClientType = clientType;
    }
}
