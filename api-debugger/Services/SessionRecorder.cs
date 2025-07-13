using System;
using System.Collections.Generic;
using MusicBeeRemote.ApiDebugger.Models;
using Newtonsoft.Json.Linq;

namespace MusicBeeRemote.ApiDebugger.Services;

/// <summary>
/// Records proxy messages into request-response pairs for session comparison.
/// </summary>
public class SessionRecorder
{
    private readonly Dictionary<string, PendingRequest> _pendingRequests = new();
    private RecordedSession? _currentSession;
    private int _sequenceCounter;
    private string? _currentClientId;

    /// <summary>
    /// Whether recording is currently active.
    /// </summary>
    public bool IsRecording => _currentSession != null;

    /// <summary>
    /// The current session being recorded, or null if not recording.
    /// </summary>
    public RecordedSession? CurrentSession => _currentSession;

    /// <summary>
    /// Number of pairs recorded in the current session.
    /// </summary>
    public int PairCount => _currentSession?.Pairs.Count ?? 0;

    /// <summary>
    /// Starts a new recording session.
    /// </summary>
    /// <param name="sessionName">Name for the session.</param>
    /// <param name="targetHost">The target host being proxied.</param>
    /// <param name="targetPort">The target port being proxied.</param>
    public void StartRecording(string sessionName, string targetHost, int targetPort)
    {
        _currentSession = new RecordedSession
        {
            Name = sessionName,
            TargetHost = targetHost,
            TargetPort = targetPort,
            RecordedAt = DateTime.UtcNow
        };
        _pendingRequests.Clear();
        _sequenceCounter = 0;
        _currentClientId = null;
    }

    /// <summary>
    /// Stops recording and returns the completed session.
    /// </summary>
    /// <returns>The recorded session, or null if not recording.</returns>
    public RecordedSession? StopRecording()
    {
        var session = _currentSession;
        _currentSession = null;
        _pendingRequests.Clear();
        _sequenceCounter = 0;
        return session;
    }

    /// <summary>
    /// Processes an intercepted message and adds it to the session.
    /// </summary>
    /// <param name="message">The intercepted log message.</param>
    public void ProcessMessage(LogMessage message)
    {
        if (_currentSession == null || !message.HasJson)
            return;

        var context = ExtractContext(message.OriginalJson!);
        if (string.IsNullOrEmpty(context))
            return;

        // Track client ID from protocol handshake
        if (context == "protocol" && message.Type == LogMessageType.Sent)
        {
            _currentClientId = ExtractClientId(message.OriginalJson!);
        }

        var direction = message.Type == LogMessageType.Sent
            ? MessageDirection.AppToPlugin
            : MessageDirection.PluginToApp;

        var recorded = new RecordedMessage
        {
            Timestamp = message.Timestamp,
            Direction = direction,
            RawJson = message.OriginalJson!
        };

        if (direction == MessageDirection.AppToPlugin)
        {
            // Request: store as pending, waiting for response
            _pendingRequests[context] = new PendingRequest
            {
                Message = recorded,
                Context = context
            };
        }
        else
        {
            // Response: try to match with pending request
            if (_pendingRequests.TryGetValue(context, out var pending))
            {
                _pendingRequests.Remove(context);

                var pair = new RequestResponsePair
                {
                    SequenceNumber = ++_sequenceCounter,
                    Context = context,
                    ClientId = _currentClientId,
                    Request = pending.Message,
                    Response = recorded,
                    ResponseTimeMs = (int)(recorded.Timestamp - pending.Message.Timestamp).TotalMilliseconds
                };

                _currentSession.Pairs.Add(pair);
            }
            else
            {
                // No matching request - this is a push notification
                var pair = new RequestResponsePair
                {
                    SequenceNumber = ++_sequenceCounter,
                    Context = context,
                    ClientId = _currentClientId,
                    Request = null,
                    Response = recorded,
                    ResponseTimeMs = null
                };

                _currentSession.Pairs.Add(pair);
            }
        }
    }

    /// <summary>
    /// Extracts the context field from a JSON message.
    /// </summary>
    private static string? ExtractContext(string json)
    {
        try
        {
            var obj = JObject.Parse(json);
            return obj["context"]?.ToString();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts the client_id from a protocol handshake message.
    /// </summary>
    private static string? ExtractClientId(string json)
    {
        try
        {
            var obj = JObject.Parse(json);
            var data = obj["data"];
            if (data is JObject dataObj)
            {
                return dataObj["client_id"]?.ToString();
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    private sealed class PendingRequest
    {
        public RecordedMessage Message { get; set; } = null!;
        public string Context { get; set; } = "";
    }
}
