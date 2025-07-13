using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicBeeRemote.ApiDebugger.Constants;
using MusicBeeRemote.ApiDebugger.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MusicBeeRemote.ApiDebugger.ViewModels;

public partial class SecondaryConnectionViewModel : ObservableObject, IDisposable
{
    private TcpClient? _tcpClient;
    private NetworkStream? _networkStream;
    private readonly string _ipAddress;
    private readonly int _port;
    private readonly string _clientId;

    private bool _isConnected;
    private bool _handshakeCompleted;
    private bool _playerAcknowledged;
    private string? _negotiatedProtocol;
    private string _savedResponse = string.Empty;

    public SecondaryConnectionViewModel() : this("127.0.0.1", 3000)
    {
    }

    public SecondaryConnectionViewModel(string ipAddress, int port)
    {
        _ipAddress = ipAddress;
        _port = port;
        _clientId = "secondary_" + Guid.NewGuid().ToString("N")[..8];

        AddLogMessage(LogMessageType.Info, $"Secondary connection form initialized for {_ipAddress}:{_port}");
        AddLogMessage(LogMessageType.Info, $"Client ID: {_clientId}");
        AddLogMessage(LogMessageType.Info, "This connection will use no_broadcast=true following Android client pattern");
    }

    #region Observable Properties

    [ObservableProperty]
    private string _windowTitle = "";

    [ObservableProperty]
    private string _statusText = "Disconnected";

    [ObservableProperty]
    private string _statusColor = "Red";

    [ObservableProperty]
    private string _clientIdDisplay = "";

    [ObservableProperty]
    private bool _canConnect = true;

    [ObservableProperty]
    private bool _canDisconnect;

    [ObservableProperty]
    private bool _canTest;

    [ObservableProperty]
    private ObservableCollection<LogMessage> _logMessages = new();

    [ObservableProperty]
    private string _responseText = "";

    [ObservableProperty]
    private Bitmap? _coverImage;

    [ObservableProperty]
    private int _offset;

    [ObservableProperty]
    private int _limit = 10;

    [ObservableProperty]
    private int _selectedPaginatedCommandIndex;

    #endregion

    public string Title => $"Secondary Connection Test - {_ipAddress}:{_port}";
    public string ClientIdText => $"Client ID: {_clientId}";

    public static string[] PaginatedCommands { get; } =
    [
        "browsegenres - Browse Genres",
        "browseartists - Browse Artists",
        "browsealbums - Browse Albums",
        "browsetracks - Browse Tracks",
        "nowplayinglist - Now Playing List",
        "playlistlist - Playlist List",
        "radiostations - Radio Stations"
    ];

    #region Commands

    [RelayCommand]
    private async Task ConnectAsync()
    {
        CanConnect = false;
        StatusText = "Connecting...";
        StatusColor = "#DCDCAA";

        try
        {
            AddLogMessage(LogMessageType.Info, $"Starting secondary connection to {_ipAddress}:{_port}");
            AddLogMessage(LogMessageType.Info, $"Client ID: {_clientId}");

            _handshakeCompleted = false;
            _playerAcknowledged = false;
            _negotiatedProtocol = null;

            _tcpClient = new TcpClient();
            AddLogMessage(LogMessageType.Info, "Connecting to TCP socket...");

            await _tcpClient.ConnectAsync(_ipAddress, _port);
            _networkStream = _tcpClient.GetStream();

            _isConnected = true;
            AddLogMessage(LogMessageType.Success, "TCP connection established");
            StatusText = "Connected, performing handshake...";

            AddLogMessage(LogMessageType.Info, "Starting message listener task...");
            _ = Task.Run(ListenForMessages);

            await Task.Delay(100);

            await PerformHandshake();

            AddLogMessage(LogMessageType.Info, "Handshake initiated, waiting for completion...");

            var handshakeSuccess = await WaitForHandshakeCompletion();
            if (handshakeSuccess)
            {
                AddLogMessage(LogMessageType.Success, "Handshake completed successfully");
                StatusText = "Connected (No-Broadcast)";
                StatusColor = "#4EC9B0";
                CanTest = true;
            }
            else
            {
                AddLogMessage(LogMessageType.Warning, "Handshake timed out");
                Disconnect();
            }

            UpdateConnectionState(_isConnected);
        }
        catch (Exception ex)
        {
            AddLogMessage(LogMessageType.Error, $"Connection failed: {ex.Message}");
            Disconnect();
        }
        finally
        {
            if (!_isConnected)
                CanConnect = true;
        }
    }

    [RelayCommand]
    private void DisconnectCommand()
    {
        Disconnect();
    }

    [RelayCommand]
    private async Task TestCoverAsync()
    {
        if (!_handshakeCompleted)
            return;

        AddLogMessage(LogMessageType.Info, "Testing cover request...");
        var coverRequest = new { context = "nowplayingcover", data = "" };
        await SendMessage(coverRequest);
    }

    [RelayCommand]
    private async Task TestTrackInfoAsync()
    {
        if (!_handshakeCompleted)
            return;

        AddLogMessage(LogMessageType.Info, "Testing track info request...");
        var trackRequest = new { context = "nowplayingtrack", data = "" };
        await SendMessage(trackRequest);
    }

    [RelayCommand]
    private async Task TestPlayerStatusAsync()
    {
        if (!_handshakeCompleted)
            return;

        AddLogMessage(LogMessageType.Info, "Testing player status request...");
        var request = new { context = "playerstatus", data = "" };
        await SendMessage(request);
    }

    [RelayCommand]
    private async Task SendPaginatedAsync()
    {
        if (!_handshakeCompleted)
            return;

        var selectedCommand = PaginatedCommands[SelectedPaginatedCommandIndex];
        var context = selectedCommand.Split(" - ")[0];

        AddLogMessage(LogMessageType.Info, $"Sending {context} (offset={Offset}, limit={Limit})...");
        var request = new
        {
            context,
            data = new { offset = Offset, limit = Limit }
        };
        await SendMessage(request);
    }

    [RelayCommand]
    private void ClearLog()
    {
        LogMessages.Clear();
        ResponseText = "";
    }

    [RelayCommand]
    private void SaveResponse()
    {
        if (string.IsNullOrWhiteSpace(ResponseText))
        {
            AddLogMessage(LogMessageType.Warning, "No response to save");
            return;
        }

        _savedResponse = ResponseText;
        AddLogMessage(LogMessageType.Success, $"Response saved ({_savedResponse.Length} characters)");
    }

    [RelayCommand]
    private void ClearSavedResponse()
    {
        if (string.IsNullOrEmpty(_savedResponse))
        {
            AddLogMessage(LogMessageType.Warning, "No saved response to clear");
            return;
        }

        _savedResponse = string.Empty;
        AddLogMessage(LogMessageType.Info, "Saved response cleared");
    }

    [RelayCommand]
    private void CompareResponse()
    {
        if (string.IsNullOrEmpty(_savedResponse))
        {
            AddLogMessage(LogMessageType.Warning, "No saved response to compare with");
            return;
        }

        if (string.IsNullOrWhiteSpace(ResponseText))
        {
            AddLogMessage(LogMessageType.Warning, "No current response to compare");
            return;
        }

        var isIdentical = string.Equals(_savedResponse, ResponseText, StringComparison.Ordinal);

        if (isIdentical)
        {
            AddLogMessage(LogMessageType.Success, "Responses are identical");
        }
        else
        {
            AddLogMessage(LogMessageType.Warning, "Responses differ");
        }
    }

    #endregion

    #region Connection Management

    private void UpdateConnectionState(bool connected)
    {
        _isConnected = connected;
        CanConnect = !connected;
        CanDisconnect = connected;
        CanTest = connected && _handshakeCompleted;

        if (!connected)
        {
            StatusText = "Disconnected";
            StatusColor = "#F44747";
        }
    }

    private void Disconnect()
    {
        try
        {
            _networkStream?.Close();
            _tcpClient?.Close();
        }
        catch
        {
            // Ignore errors during disconnect
        }
        finally
        {
            _handshakeCompleted = false;
            _playerAcknowledged = false;
            _negotiatedProtocol = null;
            UpdateConnectionState(false);
            AddLogMessage(LogMessageType.Info, "Disconnected");
        }
    }

    #endregion

    #region Protocol Communication

    private async Task PerformHandshake()
    {
        AddLogMessage(LogMessageType.Info, "Sending player message...");
        await SendPlayerMessage();
        AddLogMessage(LogMessageType.Info, "Waiting for player response before sending protocol...");
    }

    private Task SendPlayerMessage()
    {
        var playerMessage = new
        {
            context = "player",
            data = "Android"
        };
        return SendMessage(playerMessage);
    }

    private Task SendProtocolMessage()
    {
        var protocolMessage = new
        {
            context = "protocol",
            data = new
            {
                protocol_version = 4,
                no_broadcast = true
            }
        };
        return SendMessage(protocolMessage);
    }

    private async Task SendMessage(object message)
    {
        if (!_isConnected || _networkStream == null)
            return;

        try
        {
            var json = JsonConvert.SerializeObject(message);
            var prettyJson = JsonConvert.SerializeObject(message, Formatting.Indented);
            var data = Encoding.UTF8.GetBytes(json + ProtocolConstants.MessageTerminator);

            // Extract context for display, store full JSON for popup
            var context = (message as dynamic)?.context?.ToString() ?? "message";
            AddLogMessage(LogMessageType.Sent, context, prettyJson);

            await _networkStream.WriteAsync(data);
        }
        catch (Exception ex)
        {
            AddLogMessage(LogMessageType.Error, $"Send error: {ex.Message}");
        }
    }

    private async Task<bool> WaitForHandshakeCompletion()
    {
        var timeout = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < timeout && _isConnected)
        {
            if (_handshakeCompleted)
                return true;
            await Task.Delay(100);
        }
        return false;
    }

    private async Task ListenForMessages()
    {
        var buffer = new byte[4096];
        var messageBuffer = new StringBuilder();

        try
        {
            while (_isConnected && _networkStream != null)
            {
                var bytesRead = await _networkStream.ReadAsync(buffer);
                if (bytesRead == 0)
                    break;

                var data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                messageBuffer.Append(data);

                var messages = messageBuffer.ToString();
                var lines = messages.Split('\n');

                for (var i = 0; i < lines.Length - 1; i++)
                {
                    if (!string.IsNullOrWhiteSpace(lines[i]))
                        await ProcessMessage(lines[i]);
                }

                messageBuffer.Clear();
                if (!string.IsNullOrWhiteSpace(lines[^1]))
                    messageBuffer.Append(lines[^1]);
            }
        }
        catch (Exception ex)
        {
            if (_isConnected)
                Dispatcher.UIThread.Post(() => AddLogMessage(LogMessageType.Error, $"Listen error: {ex.Message}"));
        }
    }

    private Task ProcessMessage(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var json = JObject.Parse(message);
                var context = json["context"]?.ToString();
                var data = json["data"];
                var prettyJson = json.ToString(Formatting.Indented);

                AddLogMessage(LogMessageType.Received, context ?? "unknown", prettyJson);

                ResponseText = prettyJson;

                if (context == "player" && !_playerAcknowledged)
                {
                    _playerAcknowledged = true;
                    AddLogMessage(LogMessageType.Success, "Player acknowledged");
                    _ = Task.Run(async () =>
                    {
                        Dispatcher.UIThread.Post(() => AddLogMessage(LogMessageType.Info, "Sending protocol message..."));
                        await SendProtocolMessage();
                    });
                }
                else if (context == "protocol")
                {
                    _negotiatedProtocol = data?.ToString();
                    if (_playerAcknowledged)
                    {
                        _handshakeCompleted = true;
                        AddLogMessage(LogMessageType.Success, $"Protocol negotiated: {_negotiatedProtocol}");
                    }
                }

                if (context == "nowplayingcover")
                    ProcessCoverResponse(data);
                else if (context is "browsegenres" or "browseartists" or "browsealbums" or
                         "browsetracks" or "nowplayinglist" or "playlistlist" or "radiostations")
                    ProcessPaginatedResponse(context, data);
            }
            catch (Exception ex)
            {
                AddLogMessage(LogMessageType.Error, $"Process message error: {ex.Message}");
            }
        });

        return Task.CompletedTask;
    }

    #endregion

    #region Response Processing

    private void ProcessCoverResponse(JToken? data)
    {
        try
        {
            if (data?.Type == JTokenType.Object)
            {
                var coverBase64 = data["cover"]?.ToString();
                var status = data["status"]?.ToString();

                AddLogMessage(LogMessageType.Info, $"Cover response - Status: {status}");

                if (status == "200" && !string.IsNullOrEmpty(coverBase64))
                    DisplayCoverImage(coverBase64);
                else if (status == "1")
                    AddLogMessage(LogMessageType.Info, "Cover ready but not included");
                else if (status == "404")
                    AddLogMessage(LogMessageType.Warning, "No cover available");
            }
            else if (data?.Type == JTokenType.String)
            {
                var coverBase64 = data.ToString();
                if (!string.IsNullOrEmpty(coverBase64))
                    DisplayCoverImage(coverBase64);
            }
        }
        catch (Exception ex)
        {
            AddLogMessage(LogMessageType.Error, $"Cover processing error: {ex.Message}");
        }
    }

    private void ProcessPaginatedResponse(string context, JToken? data)
    {
        try
        {
            if (data?.Type == JTokenType.Object)
            {
                var total = data["total"]?.ToString() ?? "?";
                var offset = data["offset"]?.ToString() ?? "?";
                var limit = data["limit"]?.ToString() ?? "?";

                AddLogMessage(LogMessageType.Info, $"{context} - Total: {total}, Offset: {offset}, Limit: {limit}");

                if (data["data"] is JArray items)
                {
                    AddLogMessage(LogMessageType.Success, $"Received {items.Count} items");
                }
            }
        }
        catch (Exception ex)
        {
            AddLogMessage(LogMessageType.Error, $"{context} processing error: {ex.Message}");
        }
    }

    private void DisplayCoverImage(string base64Data)
    {
        try
        {
            var imageBytes = Convert.FromBase64String(base64Data);
            using var ms = new MemoryStream(imageBytes);
            var image = new Bitmap(ms);

            Dispatcher.UIThread.Post(() =>
            {
                CoverImage?.Dispose();
                CoverImage = image;
                AddLogMessage(LogMessageType.Success, $"Cover image displayed ({imageBytes.Length} bytes)");
            });
        }
        catch (Exception ex)
        {
            AddLogMessage(LogMessageType.Error, $"Cover display error: {ex.Message}");
        }
    }

    #endregion

    #region Utility Methods

    private void AddLogMessage(LogMessageType type, string content, string? originalJson = null)
    {
        var logMessage = new LogMessage(type, content, originalJson);
        Dispatcher.UIThread.Post(() => LogMessages.Add(logMessage));
        Console.WriteLine($"LOG: [{type}] {content}");
    }

    public static string? GetJsonForMessage(LogMessage message)
    {
        return message.OriginalJson;
    }

    public void Cleanup()
    {
        Dispose();
    }

    public void Dispose()
    {
        Disconnect();
        CoverImage?.Dispose();
        GC.SuppressFinalize(this);
    }

    #endregion
}
