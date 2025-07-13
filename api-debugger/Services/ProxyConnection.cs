using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MusicBeeRemote.ApiDebugger.Constants;
using MusicBeeRemote.ApiDebugger.Models;
using Newtonsoft.Json.Linq;

namespace MusicBeeRemote.ApiDebugger.Services;

/// <summary>
/// Manages a single proxy connection pair between an app client and the plugin.
/// Forwards messages bidirectionally while logging all traffic.
/// </summary>
public class ProxyConnection : IDisposable
{
    private readonly TcpClient _appClient;
    private readonly TcpClient _pluginClient;
    private readonly string _connectionId;
    private readonly CancellationTokenSource _cts;
    private bool _disposed;
    private string? _clientId;

    /// <summary>
    /// Fired when a message is forwarded in either direction.
    /// </summary>
    public event Action<LogMessage>? MessageForwarded;

    /// <summary>
    /// Fired when the connection is disconnected.
    /// </summary>
    public event Action<string>? Disconnected;

    /// <summary>
    /// Fired when a client ID is detected from the protocol handshake.
    /// </summary>
    public event Action<string, string>? ClientIdDetected;

    /// <summary>
    /// Gets the unique identifier for this connection.
    /// </summary>
    public string ConnectionId => _connectionId;

    /// <summary>
    /// Gets the client ID if detected from the protocol handshake, otherwise null.
    /// </summary>
    public string? ClientId => _clientId;

    /// <summary>
    /// Gets the display name for this connection (client ID if available, otherwise connection ID).
    /// </summary>
    public string DisplayName => _clientId != null ? $"{_clientId[..8]}..." : _connectionId;

    /// <summary>
    /// Gets the remote endpoint of the app client.
    /// </summary>
    public string AppEndpoint { get; }

    /// <summary>
    /// Creates a new proxy connection.
    /// </summary>
    /// <param name="appClient">The TCP client connected from the app.</param>
    /// <param name="pluginClient">The TCP client connected to the plugin.</param>
    /// <param name="connectionId">Unique identifier for this connection.</param>
    public ProxyConnection(TcpClient appClient, TcpClient pluginClient, string connectionId)
    {
        _appClient = appClient ?? throw new ArgumentNullException(nameof(appClient));
        _pluginClient = pluginClient ?? throw new ArgumentNullException(nameof(pluginClient));
        _connectionId = connectionId ?? throw new ArgumentNullException(nameof(connectionId));
        _cts = new CancellationTokenSource();

        AppEndpoint = _appClient.Client.RemoteEndPoint?.ToString() ?? "Unknown";
    }

    /// <summary>
    /// Starts bidirectional message forwarding.
    /// </summary>
    public async Task StartForwardingAsync(CancellationToken externalToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, externalToken);
        var token = linkedCts.Token;

        try
        {
            var appStream = _appClient.GetStream();
            var pluginStream = _pluginClient.GetStream();

            // Run both forwarding directions concurrently
            var appToPlugin = ForwardAsync(
                appStream,
                pluginStream,
                LogMessageType.Sent, // App → Plugin (outgoing from app perspective)
                "App → Plugin",
                token);

            var pluginToApp = ForwardAsync(
                pluginStream,
                appStream,
                LogMessageType.Received, // Plugin → App (incoming to app perspective)
                "Plugin → App",
                token);

            // Wait for either direction to complete (indicates disconnect)
            await Task.WhenAny(appToPlugin, pluginToApp);

            // Cancel the other direction
            await _cts.CancelAsync();
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation
        }
        catch (Exception ex)
        {
            OnMessageForwarded(new LogMessage(
                LogMessageType.Error,
                $"[{_connectionId}] Connection error: {ex.Message}"));
        }
        finally
        {
            OnDisconnected(_connectionId);
        }
    }

    private async Task ForwardAsync(
        NetworkStream source,
        NetworkStream dest,
        LogMessageType direction,
        string directionLabel,
        CancellationToken ct)
    {
        var buffer = new byte[8192];
        var messageBuffer = new StringBuilder();

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var bytesRead = await source.ReadAsync(buffer, ct);

                if (bytesRead == 0)
                {
                    // Connection closed
                    OnMessageForwarded(new LogMessage(
                        LogMessageType.Info,
                        $"[{_connectionId}] {directionLabel} connection closed"));
                    break;
                }

                var data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                messageBuffer.Append(data);

                // Process complete messages (delimited by \r\n)
                var bufferContent = messageBuffer.ToString();
                int terminatorIndex;

                while ((terminatorIndex = bufferContent.IndexOf(
                    ProtocolConstants.MessageTerminator,
                    StringComparison.Ordinal)) >= 0)
                {
                    var message = bufferContent.Substring(0, terminatorIndex);
                    bufferContent = bufferContent.Substring(
                        terminatorIndex + ProtocolConstants.MessageTerminator.Length);

                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        // Try to detect client ID from protocol handshake (App → Plugin direction)
                        if (direction == LogMessageType.Sent && _clientId == null)
                        {
                            TryExtractClientId(message);
                        }

                        // Log the intercepted message
                        var summary = GetMessageSummary(message);
                        var displayId = _clientId != null ? $"{_clientId[..Math.Min(8, _clientId.Length)]}" : _connectionId;
                        OnMessageForwarded(new LogMessage(
                            direction,
                            $"[{displayId}] {directionLabel}: {summary}",
                            message));
                    }
                }

                messageBuffer.Clear();
                messageBuffer.Append(bufferContent);

                // Forward the raw data to destination
                await dest.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                await dest.FlushAsync(ct);
            }
        }
        catch (IOException)
        {
            // Connection closed or reset
        }
        catch (SocketException)
        {
            // Connection error
        }
        catch (OperationCanceledException)
        {
            // Cancellation requested
        }
    }

    private void TryExtractClientId(string json)
    {
        try
        {
            var obj = JObject.Parse(json);
            var context = obj["context"]?.ToString();

            if (context == "protocol")
            {
                var data = obj["data"];
                if (data is JObject dataObj)
                {
                    var clientId = dataObj["client_id"]?.ToString();
                    if (!string.IsNullOrEmpty(clientId))
                    {
                        _clientId = clientId;
                        OnMessageForwarded(new LogMessage(
                            LogMessageType.Success,
                            $"[{_connectionId}] Client ID detected: {clientId}"));
                        ClientIdDetected?.Invoke(_connectionId, clientId);
                    }
                }
            }
        }
        catch
        {
            // Ignore parsing errors
        }
    }

    private static string GetMessageSummary(string json)
    {
        try
        {
            // Quick extraction of context without full JSON parsing for performance
            const string contextKey = "\"context\":";
            var contextIndex = json.IndexOf(contextKey, StringComparison.Ordinal);

            if (contextIndex >= 0)
            {
                var valueStart = json.IndexOf('"', contextIndex + contextKey.Length);
                if (valueStart >= 0)
                {
                    var valueEnd = json.IndexOf('"', valueStart + 1);
                    if (valueEnd > valueStart)
                    {
                        return json.AsSpan(valueStart + 1, valueEnd - valueStart - 1).ToString();
                    }
                }
            }

            // Fallback: truncate if too long
            return json.Length > 50 ? string.Concat(json.AsSpan(0, 47), "...") : json;
        }
        catch
        {
            return json.Length > 50 ? string.Concat(json.AsSpan(0, 47), "...") : json;
        }
    }

    private void OnMessageForwarded(LogMessage message)
    {
        MessageForwarded?.Invoke(message);
    }

    private void OnDisconnected(string connectionId)
    {
        Disconnected?.Invoke(connectionId);
    }

    /// <summary>
    /// Stops the connection and releases resources.
    /// </summary>
    public void Stop()
    {
        if (!_cts.IsCancellationRequested)
        {
            _cts.Cancel();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        Stop();

        try
        { _appClient.Close(); }
        catch { /* ignore */ }
        try
        { _pluginClient.Close(); }
        catch { /* ignore */ }
        try
        { _appClient.Dispose(); }
        catch { /* ignore */ }
        try
        { _pluginClient.Dispose(); }
        catch { /* ignore */ }
        _cts.Dispose();

        GC.SuppressFinalize(this);
    }
}
