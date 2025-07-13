using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MusicBeeRemote.ApiDebugger.Models;

namespace MusicBeeRemote.ApiDebugger.Services;

/// <summary>
/// TCP proxy service that sits between an app and the plugin,
/// intercepting and logging all messages in both directions.
/// </summary>
public class ProxyService : IDisposable
{
    private int _listenPort;
    private string _targetHost = "127.0.0.1";
    private int _targetPort;

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private readonly List<ProxyConnection> _connections = new();
    private readonly object _connectionsLock = new();
    private int _connectionCounter;
    private bool _disposed;

    /// <summary>
    /// Fired when a message is intercepted.
    /// </summary>
    public event Action<LogMessage>? MessageIntercepted;

    /// <summary>
    /// Fired when the connection count changes.
    /// </summary>
    public event Action<int>? ConnectionCountChanged;

    /// <summary>
    /// Fired when the service status changes.
    /// </summary>
    public event Action<string>? StatusChanged;

    /// <summary>
    /// Gets whether the proxy is currently running.
    /// </summary>
    public bool IsRunning => _listener != null;

    /// <summary>
    /// Gets the current number of active connections.
    /// </summary>
    public int ConnectionCount
    {
        get
        {
            lock (_connectionsLock)
            {
                return _connections.Count;
            }
        }
    }

    /// <summary>
    /// Starts the proxy service.
    /// </summary>
    /// <param name="listenPort">Port to listen for incoming app connections.</param>
    /// <param name="targetHost">Host where the plugin is running.</param>
    /// <param name="targetPort">Port where the plugin is listening.</param>
    public Task StartAsync(int listenPort, string targetHost, int targetPort)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_listener != null)
        {
            throw new InvalidOperationException("Proxy is already running");
        }

        _listenPort = listenPort;
        _targetHost = targetHost;
        _targetPort = targetPort;

        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, _listenPort);

        try
        {
            _listener.Start();
            OnStatusChanged($"Listening on port {_listenPort}, forwarding to {_targetHost}:{_targetPort}");
            OnMessageIntercepted(new LogMessage(
                LogMessageType.Success,
                $"Proxy started: listening on port {_listenPort}, target {_targetHost}:{_targetPort}"));

            // Start accepting connections
            _ = AcceptConnectionsAsync(_cts.Token);
        }
        catch (SocketException ex)
        {
            _listener = null;
            _cts.Dispose();
            _cts = null;
            OnStatusChanged("Failed to start");
            throw new InvalidOperationException($"Failed to start listener on port {_listenPort}: {ex.Message}", ex);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the proxy service.
    /// </summary>
    public void Stop()
    {
        if (_listener == null)
            return;

        try
        {
            _cts?.Cancel();
            _listener.Stop();
        }
        catch
        {
            // Ignore errors during shutdown
        }

        // Close all active connections
        lock (_connectionsLock)
        {
            foreach (var connection in _connections)
            {
                try
                {
                    connection.Dispose();
                }
                catch
                {
                    // Ignore
                }
            }

            _connections.Clear();
        }

        OnConnectionCountChanged(0);

        _listener = null;
        _cts?.Dispose();
        _cts = null;

        OnStatusChanged("Stopped");
        OnMessageIntercepted(new LogMessage(LogMessageType.Info, "Proxy stopped"));
    }

    private async Task AcceptConnectionsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null)
        {
            try
            {
                var appClient = await _listener.AcceptTcpClientAsync(ct);
                var connectionId = $"Conn-{++_connectionCounter}";

                OnMessageIntercepted(new LogMessage(
                    LogMessageType.Info,
                    $"[{connectionId}] New connection from {appClient.Client.RemoteEndPoint}"));

                // Try to connect to the plugin
                var pluginClient = new TcpClient();

                try
                {
                    await pluginClient.ConnectAsync(_targetHost, _targetPort, ct);

                    OnMessageIntercepted(new LogMessage(
                        LogMessageType.Success,
                        $"[{connectionId}] Connected to plugin at {_targetHost}:{_targetPort}"));

                    // Create and track the proxy connection
                    var connection = new ProxyConnection(appClient, pluginClient, connectionId);
                    connection.MessageForwarded += OnMessageIntercepted;
                    connection.Disconnected += OnConnectionDisconnected;

                    lock (_connectionsLock)
                    {
                        _connections.Add(connection);
                    }

                    OnConnectionCountChanged(ConnectionCount);

                    // Start forwarding (fire and forget)
                    _ = RunConnectionAsync(connection, ct);
                }
                catch (SocketException ex)
                {
                    OnMessageIntercepted(new LogMessage(
                        LogMessageType.Error,
                        $"[{connectionId}] Failed to connect to plugin: {ex.Message}"));

                    pluginClient.Dispose();
                    appClient.Close();
                    appClient.Dispose();
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on cancellation
                break;
            }
            catch (ObjectDisposedException)
            {
                // Listener was disposed
                break;
            }
            catch (SocketException)
            {
                // Listener error
                break;
            }
        }
    }

    private async Task RunConnectionAsync(ProxyConnection connection, CancellationToken ct)
    {
        try
        {
            await connection.StartForwardingAsync(ct);
        }
        catch
        {
            // Error already logged by the connection
        }
        finally
        {
            RemoveConnection(connection);
        }
    }

    private void OnConnectionDisconnected(string connectionId)
    {
        OnMessageIntercepted(new LogMessage(
            LogMessageType.Info,
            $"[{connectionId}] Disconnected"));
    }

    private void RemoveConnection(ProxyConnection connection)
    {
        lock (_connectionsLock)
        {
            _connections.Remove(connection);
        }

        connection.MessageForwarded -= OnMessageIntercepted;
        connection.Disconnected -= OnConnectionDisconnected;
        connection.Dispose();

        OnConnectionCountChanged(ConnectionCount);
    }

    private void OnMessageIntercepted(LogMessage message)
    {
        MessageIntercepted?.Invoke(message);
    }

    private void OnConnectionCountChanged(int count)
    {
        ConnectionCountChanged?.Invoke(count);
    }

    private void OnStatusChanged(string status)
    {
        StatusChanged?.Invoke(status);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        Stop();
        GC.SuppressFinalize(this);
    }
}
