using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicBeeRemote.ApiDebugger.Models;
using MusicBeeRemote.ApiDebugger.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MusicBeeRemote.ApiDebugger.ViewModels;

/// <summary>
/// ViewModel for the Proxy window that manages TCP proxy state and message interception.
/// </summary>
public partial class ProxyWindowViewModel : ObservableObject, IDisposable
{
    private readonly ProxyService _proxyService;
    private readonly SessionRecorder _sessionRecorder;
    private bool _disposed;

    public ProxyWindowViewModel()
    {
        _proxyService = new ProxyService();
        _proxyService.MessageIntercepted += OnMessageIntercepted;
        _proxyService.ConnectionCountChanged += OnConnectionCountChanged;
        _proxyService.StatusChanged += OnStatusChanged;

        _sessionRecorder = new SessionRecorder();

        AddLogMessage(LogMessageType.Info, "Proxy mode initialized");
        AddLogMessage(LogMessageType.Info, "Configure listen port and target, then click Start to begin proxying");
    }

    #region Observable Properties

    [ObservableProperty]
    private string _listenPort = "3001";

    [ObservableProperty]
    private string _targetHost = "127.0.0.1";

    [ObservableProperty]
    private string _targetPort = "3000";

    [ObservableProperty]
    private string _statusText = "Stopped";

    [ObservableProperty]
    private string _statusColor = "#F44747"; // Red

    [ObservableProperty]
    private string _connectionCountText = "0";

    [ObservableProperty]
    private bool _canStart = true;

    [ObservableProperty]
    private bool _canStop;

    [ObservableProperty]
    private bool _canEditConfig = true;

    [ObservableProperty]
    private ObservableCollection<LogMessage> _logMessages = new();

    [ObservableProperty]
    private LogMessage? _selectedMessage;

    [ObservableProperty]
    private string _selectedMessageJson = "";

    [ObservableProperty]
    private bool _filterAppToPlugin = true;

    [ObservableProperty]
    private bool _filterPluginToApp = true;

    [ObservableProperty]
    private Bitmap? _coverImage;

    [ObservableProperty]
    private bool _hasCover;

    [ObservableProperty]
    private bool _hasSelectedMessage;

    [ObservableProperty]
    private string _searchFilter = "";

    [ObservableProperty]
    private bool _isLoadingJson;

    [ObservableProperty]
    private bool _isJsonTruncated;

    [ObservableProperty]
    private string _jsonSizeInfo = "";

    // Session Recording Properties
    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private string _sessionName = "";

    [ObservableProperty]
    private int _recordedPairCount;

    [ObservableProperty]
    private string _recordingStatusText = "";

    [ObservableProperty]
    private ObservableCollection<RecordedSession> _loadedSessions = new();

    // Session Comparison Properties
    [ObservableProperty]
    private RecordedSession? _selectedSessionA;

    [ObservableProperty]
    private RecordedSession? _selectedSessionB;

    [ObservableProperty]
    private string _ignoreFields = "client_id, timestamp";

    [ObservableProperty]
    private bool _ignoreArrayOrder;

    [ObservableProperty]
    private bool _isComparing;

    #endregion

    // Threshold for "large" JSON (characters)
    private const int LargeJsonThreshold = 50000;
    private const int TruncatePreviewLength = 10000;
    private CancellationTokenSource? _jsonLoadCts;
    private string? _fullJsonCache;

    #region Computed Properties

    /// <summary>
    /// Whether recording can be started (proxy running and not already recording).
    /// </summary>
    public bool CanStartRecording => CanStop && !IsRecording;

    /// <summary>
    /// Whether a session can be saved (has current session or loaded sessions).
    /// </summary>
    public bool CanSaveSession => _sessionRecorder.CurrentSession != null || LoadedSessions.Count > 0;

    /// <summary>
    /// Whether there are loaded sessions to compare.
    /// </summary>
    public bool HasLoadedSessions => LoadedSessions.Count >= 2;

    /// <summary>
    /// Whether two different sessions are selected for comparison.
    /// </summary>
    public bool CanCompareSessions => SelectedSessionA != null && SelectedSessionB != null && SelectedSessionA != SelectedSessionB;

    /// <summary>
    /// Gets the filtered messages based on direction and search filter settings.
    /// </summary>
    public ObservableCollection<LogMessage> FilteredMessages
    {
        get
        {
            var hasSearch = !string.IsNullOrWhiteSpace(SearchFilter);
            var searchTerm = SearchFilter ?? "";

            var filtered = LogMessages.Where(m =>
            {
                // Direction filter
                var passesDirection = m.Type switch
                {
                    LogMessageType.Sent => FilterAppToPlugin,
                    LogMessageType.Received => FilterPluginToApp,
                    // Always show info/error/success/warning messages
                    _ => true
                };

                if (!passesDirection)
                    return false;

                // Search filter (matches context in Content or in JSON)
                if (hasSearch)
                {
                    var contentMatch = m.Content.Contains(searchTerm, StringComparison.OrdinalIgnoreCase);
                    var jsonMatch = m.OriginalJson?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false;
                    return contentMatch || jsonMatch;
                }

                return true;
            });

            return new ObservableCollection<LogMessage>(filtered);
        }
    }

    #endregion

    #region Commands

    [RelayCommand]
    private async Task StartProxyAsync()
    {
        if (!int.TryParse(ListenPort, out var listenPortNum) || listenPortNum < 1 || listenPortNum > 65535)
        {
            AddLogMessage(LogMessageType.Error, $"Invalid listen port: {ListenPort}");
            return;
        }

        if (!int.TryParse(TargetPort, out var targetPortNum) || targetPortNum < 1 || targetPortNum > 65535)
        {
            AddLogMessage(LogMessageType.Error, $"Invalid target port: {TargetPort}");
            return;
        }

        if (string.IsNullOrWhiteSpace(TargetHost))
        {
            AddLogMessage(LogMessageType.Error, "Target host cannot be empty");
            return;
        }

        CanStart = false;
        CanEditConfig = false;
        StatusText = "Starting...";
        StatusColor = "#DCDCAA"; // Yellow

        try
        {
            await _proxyService.StartAsync(listenPortNum, TargetHost, targetPortNum);
            CanStop = true;
            StatusText = "Running";
            StatusColor = "#4EC9B0"; // Teal
        }
        catch (Exception ex)
        {
            AddLogMessage(LogMessageType.Error, $"Failed to start proxy: {ex.Message}");
            CanStart = true;
            CanEditConfig = true;
            StatusText = "Failed";
            StatusColor = "#F44747"; // Red
        }
    }

    [RelayCommand]
    private void StopProxy()
    {
        _proxyService.Stop();
        CanStop = false;
        CanStart = true;
        CanEditConfig = true;
        StatusText = "Stopped";
        StatusColor = "#F44747"; // Red
    }

    [RelayCommand]
    private void ClearMessages()
    {
        LogMessages.Clear();
        SelectedMessage = null;
        SelectedMessageJson = "";
        CoverImage?.Dispose();
        CoverImage = null;
        HasCover = false;
        HasSelectedMessage = false;
        OnPropertyChanged(nameof(FilteredMessages));
    }

    /// <summary>
    /// Event raised when copy is requested. The view should handle clipboard access.
    /// </summary>
    public event Action<string>? CopyRequested;

    [RelayCommand]
    private void CopyMessage()
    {
        // Copy full JSON, not truncated
        var jsonToCopy = _fullJsonCache ?? SelectedMessageJson;
        if (!string.IsNullOrEmpty(jsonToCopy))
        {
            CopyRequested?.Invoke(jsonToCopy);
        }
    }

    [RelayCommand]
    private async Task LoadFullJsonAsync()
    {
        if (string.IsNullOrEmpty(_fullJsonCache))
            return;

        IsLoadingJson = true;
        IsJsonTruncated = false;

        try
        {
            // Load on background thread to avoid UI freeze
            var formatted = await Task.Run(() => _fullJsonCache);
            SelectedMessageJson = formatted!;
        }
        finally
        {
            IsLoadingJson = false;
        }
    }

    #endregion

    #region Session Recording Commands

    [RelayCommand]
    private void StartRecording()
    {
        if (_sessionRecorder.IsRecording)
            return;

        var name = string.IsNullOrWhiteSpace(SessionName)
            ? $"Session {DateTime.Now:yyyy-MM-dd HH:mm}"
            : SessionName;

        if (!int.TryParse(TargetPort, out var targetPort))
            targetPort = 3000;

        _sessionRecorder.StartRecording(name, TargetHost, targetPort);
        IsRecording = true;
        RecordedPairCount = 0;
        RecordingStatusText = "Recording...";
        OnPropertyChanged(nameof(CanSaveSession));
        AddLogMessage(LogMessageType.Success, $"Started recording session: {name}");
    }

    [RelayCommand]
    private void StopRecording()
    {
        if (!_sessionRecorder.IsRecording)
            return;

        var session = _sessionRecorder.StopRecording();
        IsRecording = false;
        RecordingStatusText = "";

        if (session != null)
        {
            LoadedSessions.Add(session);
            OnPropertyChanged(nameof(CanSaveSession));
            OnPropertyChanged(nameof(HasLoadedSessions));
            AddLogMessage(LogMessageType.Success, $"Stopped recording: {session.Name} ({session.Pairs.Count} pairs)");
        }
    }

    /// <summary>
    /// Event raised when a session save is requested. The view should handle the file dialog.
    /// </summary>
    public event Func<RecordedSession, Task>? SaveSessionRequested;

    [RelayCommand]
    private async Task SaveSessionAsync()
    {
        var session = _sessionRecorder.CurrentSession;
        if (session == null && LoadedSessions.Count > 0)
        {
            // If not currently recording, save the most recent session
            session = LoadedSessions[^1];
        }

        if (session == null)
        {
            AddLogMessage(LogMessageType.Warning, "No session to save");
            return;
        }

        if (SaveSessionRequested != null)
        {
            await SaveSessionRequested.Invoke(session);
        }
    }

    /// <summary>
    /// Event raised when a session load is requested. The view should handle the file dialog.
    /// </summary>
    public event Func<Task<RecordedSession?>>? LoadSessionRequested;

    [RelayCommand]
    private async Task LoadSessionAsync()
    {
        if (LoadSessionRequested != null)
        {
            var session = await LoadSessionRequested.Invoke();
            if (session != null)
            {
                LoadedSessions.Add(session);
                OnPropertyChanged(nameof(CanSaveSession));
                OnPropertyChanged(nameof(HasLoadedSessions));
                AddLogMessage(LogMessageType.Success, $"Loaded session: {session.Name} ({session.Pairs.Count} pairs)");
            }
        }
    }

    #endregion

    #region Session Comparison Commands

    /// <summary>
    /// Event raised when comparison results should be shown. The view should handle opening the window.
    /// </summary>
    public event Action<SessionComparisonResult>? ShowComparisonRequested;

    [RelayCommand]
    private async Task CompareSessionsAsync()
    {
        if (SelectedSessionA == null || SelectedSessionB == null)
        {
            AddLogMessage(LogMessageType.Warning, "Please select two sessions to compare");
            return;
        }

        if (SelectedSessionA == SelectedSessionB)
        {
            AddLogMessage(LogMessageType.Warning, "Please select two different sessions to compare");
            return;
        }

        IsComparing = true;
        AddLogMessage(LogMessageType.Info, "Comparing sessions...");

        try
        {
            var sessionA = SelectedSessionA;
            var sessionB = SelectedSessionB;
            var ignoreFields = IgnoreFields;
            var ignoreArrayOrder = IgnoreArrayOrder;

            // Run comparison on background thread
            var result = await Task.Run(() =>
            {
                var options = new ComparisonOptions
                {
                    IgnoreFields = ignoreFields.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                    IgnoreArrayOrder = ignoreArrayOrder,
                    NormalizeWhitespace = true
                };

                return SessionComparer.Compare(sessionA, sessionB, options);
            });

            AddLogMessage(LogMessageType.Success, $"Comparison complete: {result.Summary}");
            ShowComparisonRequested?.Invoke(result);
        }
        catch (Exception ex)
        {
            AddLogMessage(LogMessageType.Error, $"Comparison failed: {ex.Message}");
        }
        finally
        {
            IsComparing = false;
        }
    }

    partial void OnSelectedSessionAChanged(RecordedSession? value)
    {
        OnPropertyChanged(nameof(CanCompareSessions));
    }

    partial void OnSelectedSessionBChanged(RecordedSession? value)
    {
        OnPropertyChanged(nameof(CanCompareSessions));
    }

    [RelayCommand]
    private void ClearSessions()
    {
        LoadedSessions.Clear();
        SelectedSessionA = null;
        SelectedSessionB = null;
        OnPropertyChanged(nameof(CanSaveSession));
        OnPropertyChanged(nameof(HasLoadedSessions));
        AddLogMessage(LogMessageType.Info, "Cleared all loaded sessions");
    }

    #endregion

    #region Filter Handling

    partial void OnFilterAppToPluginChanged(bool value)
    {
        OnPropertyChanged(nameof(FilteredMessages));
    }

    partial void OnFilterPluginToAppChanged(bool value)
    {
        OnPropertyChanged(nameof(FilteredMessages));
    }

    partial void OnSearchFilterChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredMessages));
    }

    partial void OnIsRecordingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanStartRecording));
    }

    partial void OnCanStopChanged(bool value)
    {
        OnPropertyChanged(nameof(CanStartRecording));
    }

    partial void OnRecordedPairCountChanged(int value)
    {
        if (IsRecording)
        {
            RecordingStatusText = $"Recording... {value} pairs captured";
        }
    }

    partial void OnSelectedMessageChanged(LogMessage? value)
    {
        // Cancel any pending JSON loading
        _jsonLoadCts?.Cancel();
        _jsonLoadCts = null;
        _fullJsonCache = null;

        HasSelectedMessage = value?.HasJson == true;
        IsJsonTruncated = false;
        IsLoadingJson = false;
        JsonSizeInfo = "";

        // Clear previous cover
        CoverImage?.Dispose();
        CoverImage = null;
        HasCover = false;

        if (value?.HasJson == true)
        {
            var rawJson = value.OriginalJson!;
            var jsonLength = rawJson.Length;

            // Show size info for any JSON
            JsonSizeInfo = FormatSize(jsonLength);

            if (jsonLength > LargeJsonThreshold)
            {
                // Large JSON - load async with truncation option
                _ = LoadJsonAsync(rawJson);
            }
            else
            {
                // Small JSON - load synchronously
                LoadJsonSync(rawJson);
            }
        }
        else
        {
            SelectedMessageJson = "";
        }
    }

    private void LoadJsonSync(string rawJson)
    {
        try
        {
            var json = JObject.Parse(rawJson);
            SelectedMessageJson = json.ToString(Formatting.Indented);
            _fullJsonCache = SelectedMessageJson;

            // Check for cover data
            TryExtractCoverImage(json);
        }
        catch
        {
            SelectedMessageJson = rawJson;
            _fullJsonCache = rawJson;
        }
    }

    private async Task LoadJsonAsync(string rawJson)
    {
        _jsonLoadCts = new CancellationTokenSource();
        var ct = _jsonLoadCts.Token;

        IsLoadingJson = true;

        try
        {
            // Parse and format on background thread
            var (formatted, truncated, jsonObj) = await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                var json = JObject.Parse(rawJson);
                var fullFormatted = json.ToString(Formatting.Indented);

                ct.ThrowIfCancellationRequested();

                // Truncate for preview if very large
                string preview;
                bool isTruncated;

                if (fullFormatted.Length > TruncatePreviewLength)
                {
                    // Find a good break point (end of a line)
                    var breakPoint = fullFormatted.LastIndexOf('\n', TruncatePreviewLength);
                    if (breakPoint < TruncatePreviewLength / 2)
                        breakPoint = TruncatePreviewLength;

                    preview = fullFormatted[..breakPoint] + "\n\n... [truncated - click 'Load Full' to see all]";
                    isTruncated = true;
                }
                else
                {
                    preview = fullFormatted;
                    isTruncated = false;
                }

                return (fullFormatted, isTruncated, json);
            }, ct);

            ct.ThrowIfCancellationRequested();

            _fullJsonCache = formatted;
            IsJsonTruncated = truncated;
            SelectedMessageJson = truncated ? truncated.ToString() : formatted;

            // Fix: use the actual preview string
            SelectedMessageJson = await Task.Run(() =>
            {
                if (formatted.Length > TruncatePreviewLength)
                {
                    var breakPoint = formatted.LastIndexOf('\n', TruncatePreviewLength);
                    if (breakPoint < TruncatePreviewLength / 2)
                        breakPoint = TruncatePreviewLength;
                    return formatted[..breakPoint] + "\n\n... [truncated - click 'Load Full' to see all]";
                }
                return formatted;
            }, ct);

            IsJsonTruncated = formatted.Length > TruncatePreviewLength;

            // Check for cover on UI thread
            TryExtractCoverImage(jsonObj);
        }
        catch (OperationCanceledException)
        {
            // Selection changed, ignore
        }
        catch
        {
            SelectedMessageJson = rawJson.Length > TruncatePreviewLength
                ? rawJson[..TruncatePreviewLength] + "\n\n... [truncated]"
                : rawJson;
            _fullJsonCache = rawJson;
            IsJsonTruncated = rawJson.Length > TruncatePreviewLength;
        }
        finally
        {
            IsLoadingJson = false;
        }
    }

    private static string FormatSize(int length)
    {
        return length switch
        {
            < 1024 => $"{length} bytes",
            < 1024 * 1024 => $"{length / 1024.0:F1} KB",
            _ => $"{length / (1024.0 * 1024.0):F2} MB"
        };
    }

    private void TryExtractCoverImage(JObject json)
    {
        try
        {
            var context = json["context"]?.ToString();
            if (context != "nowplayingcover")
                return;

            var data = json["data"];
            string? base64Cover = null;

            if (data is JObject dataObj)
            {
                // Format: { "cover": "base64...", "status": "200" }
                base64Cover = dataObj["cover"]?.ToString();
            }
            else if (data?.Type == JTokenType.String)
            {
                // Format: data is directly the base64 string
                base64Cover = data.ToString();
            }

            if (!string.IsNullOrEmpty(base64Cover))
            {
                var imageBytes = Convert.FromBase64String(base64Cover);
                using var ms = new MemoryStream(imageBytes);
                CoverImage = new Bitmap(ms);
                HasCover = true;
            }
        }
        catch
        {
            // Ignore cover parsing errors
        }
    }

    #endregion

    #region Event Handlers

    private void OnMessageIntercepted(LogMessage message)
    {
        // Record the message if recording is active
        if (_sessionRecorder.IsRecording)
        {
            _sessionRecorder.ProcessMessage(message);
            Dispatcher.UIThread.Post(() =>
            {
                RecordedPairCount = _sessionRecorder.PairCount;
            });
        }

        Dispatcher.UIThread.Post(() =>
        {
            LogMessages.Add(message);
            OnPropertyChanged(nameof(FilteredMessages));
        });
    }

    private void OnConnectionCountChanged(int count)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ConnectionCountText = count.ToString(CultureInfo.InvariantCulture);
        });
    }

    private void OnStatusChanged(string status)
    {
        Dispatcher.UIThread.Post(() =>
        {
            AddLogMessage(LogMessageType.Info, $"Status: {status}");
        });
    }

    #endregion

    #region Utility Methods

    private void AddLogMessage(LogMessageType type, string content, string? originalJson = null)
    {
        var logMessage = new LogMessage(type, content, originalJson);
        Dispatcher.UIThread.Post(() =>
        {
            LogMessages.Add(logMessage);
            OnPropertyChanged(nameof(FilteredMessages));
        });
    }

    /// <summary>
    /// Gets the JSON content for a message (for dialog display).
    /// </summary>
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
        if (_disposed)
            return;
        _disposed = true;

        _proxyService.MessageIntercepted -= OnMessageIntercepted;
        _proxyService.ConnectionCountChanged -= OnConnectionCountChanged;
        _proxyService.StatusChanged -= OnStatusChanged;
        _proxyService.Dispose();

        CoverImage?.Dispose();

        GC.SuppressFinalize(this);
    }

    #endregion
}
