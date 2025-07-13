using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicBeeRemote.ApiDebugger.Models;
using MusicBeeRemote.ApiDebugger.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MusicBeeRemote.ApiDebugger.ViewModels;

/// <summary>
/// ViewModel for the diff view window showing side-by-side comparison with inline highlighting.
/// </summary>
public partial class DiffViewViewModel : ObservableObject
{
    public DiffViewViewModel()
    {
    }

    public DiffViewViewModel(PairComparisonResult result)
    {
        Load(result);
    }

    #region Observable Properties

    [ObservableProperty]
    private string _context = "";

    [ObservableProperty]
    private bool _showRequest = true;

    [ObservableProperty]
    private bool _showResponse;

    [ObservableProperty]
    private ObservableCollection<DiffLine> _leftLines = [];

    [ObservableProperty]
    private ObservableCollection<DiffLine> _rightLines = [];

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private string _statsText = "";

    [ObservableProperty]
    private bool _showSummary = true;

    [ObservableProperty]
    private List<string> _differences = [];

    [ObservableProperty]
    private int _currentDiffIndex;

    [ObservableProperty]
    private int _totalDiffs;

    [ObservableProperty]
    private bool _isLoading;

    #endregion

    private PairComparisonResult? _comparisonResult;
    private List<int> _diffLineIndices = [];

    // Cached diffs for request and response
    private List<DiffLine>? _cachedRequestLeftLines;
    private List<DiffLine>? _cachedRequestRightLines;
    private List<DiffLine>? _cachedResponseLeftLines;
    private List<DiffLine>? _cachedResponseRightLines;
    private List<int>? _cachedRequestDiffIndices;
    private List<int>? _cachedResponseDiffIndices;
    private string? _cachedRequestStatsText;
    private string? _cachedResponseStatsText;

    #region Methods

    /// <summary>
    /// Loads a comparison result into the view model.
    /// </summary>
    public void Load(PairComparisonResult result)
    {
        _comparisonResult = result;
        Context = result.Context;

        // Clear caches
        _cachedRequestLeftLines = null;
        _cachedRequestRightLines = null;
        _cachedResponseLeftLines = null;
        _cachedResponseRightLines = null;
        _cachedRequestDiffIndices = null;
        _cachedResponseDiffIndices = null;
        _cachedRequestStatsText = null;
        _cachedResponseStatsText = null;

        // Default to showing request if available, otherwise response
        if (result.PairA?.Request != null || result.PairB?.Request != null)
        {
            ShowRequest = true;
            ShowResponse = false;
        }
        else
        {
            ShowRequest = false;
            ShowResponse = true;
        }

        _ = UpdateDisplayedJsonAsync();
    }

    /// <summary>
    /// Pre-computes both request and response diffs asynchronously.
    /// Call this after Load() to prepare both views for fast tab switching.
    /// </summary>
    public async Task PrecomputeDiffsAsync()
    {
        if (_comparisonResult == null)
            return;

        IsLoading = true;

        try
        {
            var result = _comparisonResult;

            // Compute both diffs on background thread
            await Task.Run(() =>
            {
                // Request diff
                var requestJsonA = result.PairA?.Request?.RawJson;
                var requestJsonB = result.PairB?.Request?.RawJson;
                var formattedRequestA = FormatJson(requestJsonA);
                var formattedRequestB = FormatJson(requestJsonB);
                var (reqLeft, reqRight) = LineDiffer.ComputeSideBySideDiff(formattedRequestA, formattedRequestB);
                _cachedRequestLeftLines = reqLeft;
                _cachedRequestRightLines = reqRight;
                _cachedRequestDiffIndices = reqLeft
                    .Select((line, index) => (line, index))
                    .Where(x => x.line.Type != DiffLineType.Unchanged)
                    .Select(x => x.index)
                    .ToList();
                var reqStats = LineDiffer.GetStats(reqLeft);
                _cachedRequestStatsText = reqStats.HasChanges
                    ? $"+{reqStats.AddedLines} / -{reqStats.RemovedLines} lines"
                    : "No changes";

                // Response diff
                var responseJsonA = result.PairA?.Response?.RawJson;
                var responseJsonB = result.PairB?.Response?.RawJson;
                var formattedResponseA = FormatJson(responseJsonA);
                var formattedResponseB = FormatJson(responseJsonB);
                var (respLeft, respRight) = LineDiffer.ComputeSideBySideDiff(formattedResponseA, formattedResponseB);
                _cachedResponseLeftLines = respLeft;
                _cachedResponseRightLines = respRight;
                _cachedResponseDiffIndices = respLeft
                    .Select((line, index) => (line, index))
                    .Where(x => x.line.Type != DiffLineType.Unchanged)
                    .Select(x => x.index)
                    .ToList();
                var respStats = LineDiffer.GetStats(respLeft);
                _cachedResponseStatsText = respStats.HasChanges
                    ? $"+{respStats.AddedLines} / -{respStats.RemovedLines} lines"
                    : "No changes";
            });

            // Update UI with cached data
            await UpdateDisplayedJsonAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnShowRequestChanged(bool value)
    {
        if (value)
        {
            ShowResponse = false;
            _ = UpdateDisplayedJsonAsync();
        }
    }

    partial void OnShowResponseChanged(bool value)
    {
        if (value)
        {
            ShowRequest = false;
            _ = UpdateDisplayedJsonAsync();
        }
    }

    private async Task UpdateDisplayedJsonAsync()
    {
        if (_comparisonResult == null)
            return;

        ComparisonStatus status;
        List<string> diffs;

        if (ShowRequest)
        {
            status = _comparisonResult.RequestStatus;
            diffs = _comparisonResult.RequestDiffs;

            // Use cached data if available (instant)
            if (_cachedRequestLeftLines != null && _cachedRequestRightLines != null)
            {
                LeftLines = new ObservableCollection<DiffLine>(_cachedRequestLeftLines);
                RightLines = new ObservableCollection<DiffLine>(_cachedRequestRightLines);
                _diffLineIndices = _cachedRequestDiffIndices ?? [];
                StatsText = _cachedRequestStatsText ?? "No changes";
            }
            else
            {
                // Compute async on background thread
                IsLoading = true;
                LeftLines = [];
                RightLines = [];

                var jsonA = _comparisonResult.PairA?.Request?.RawJson;
                var jsonB = _comparisonResult.PairB?.Request?.RawJson;

                var (left, right, indices, statsText) = await Task.Run(() =>
                {
                    ComputeDiff(jsonA, jsonB, out var l, out var r, out var idx, out var stats);
                    return (l, r, idx, stats);
                });

                _cachedRequestLeftLines = left;
                _cachedRequestRightLines = right;
                _cachedRequestDiffIndices = indices;
                _cachedRequestStatsText = statsText;

                LeftLines = new ObservableCollection<DiffLine>(left);
                RightLines = new ObservableCollection<DiffLine>(right);
                _diffLineIndices = indices;
                StatsText = statsText;
                IsLoading = false;
            }
        }
        else
        {
            status = _comparisonResult.ResponseStatus;
            diffs = _comparisonResult.ResponseDiffs;

            // Use cached data if available (instant)
            if (_cachedResponseLeftLines != null && _cachedResponseRightLines != null)
            {
                LeftLines = new ObservableCollection<DiffLine>(_cachedResponseLeftLines);
                RightLines = new ObservableCollection<DiffLine>(_cachedResponseRightLines);
                _diffLineIndices = _cachedResponseDiffIndices ?? [];
                StatsText = _cachedResponseStatsText ?? "No changes";
            }
            else
            {
                // Compute async on background thread
                IsLoading = true;
                LeftLines = [];
                RightLines = [];

                var jsonA = _comparisonResult.PairA?.Response?.RawJson;
                var jsonB = _comparisonResult.PairB?.Response?.RawJson;

                var (left, right, indices, statsText) = await Task.Run(() =>
                {
                    ComputeDiff(jsonA, jsonB, out var l, out var r, out var idx, out var stats);
                    return (l, r, idx, stats);
                });

                _cachedResponseLeftLines = left;
                _cachedResponseRightLines = right;
                _cachedResponseDiffIndices = indices;
                _cachedResponseStatsText = statsText;

                LeftLines = new ObservableCollection<DiffLine>(left);
                RightLines = new ObservableCollection<DiffLine>(right);
                _diffLineIndices = indices;
                StatsText = statsText;
                IsLoading = false;
            }
        }

        StatusText = ShowRequest ? $"Request: {status}" : $"Response: {status}";
        Differences = diffs;
        TotalDiffs = _diffLineIndices.Count;
        CurrentDiffIndex = TotalDiffs > 0 ? 1 : 0;
    }

    private static void ComputeDiff(string? jsonA, string? jsonB,
        out List<DiffLine> left, out List<DiffLine> right,
        out List<int> diffIndices, out string statsText)
    {
        var formattedA = FormatJson(jsonA);
        var formattedB = FormatJson(jsonB);

        (left, right) = LineDiffer.ComputeSideBySideDiff(formattedA, formattedB);

        diffIndices = left
            .Select((line, index) => (line, index))
            .Where(x => x.line.Type != DiffLineType.Unchanged)
            .Select(x => x.index)
            .ToList();

        var stats = LineDiffer.GetStats(left);
        statsText = stats.HasChanges
            ? $"+{stats.AddedLines} / -{stats.RemovedLines} lines"
            : "No changes";
    }

    private static string FormatJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return "(no data)";

        try
        {
            var token = JToken.Parse(json);
            return token.ToString(Formatting.Indented);
        }
        catch
        {
            return json;
        }
    }

    /// <summary>
    /// Gets the line index of the current diff for scrolling.
    /// </summary>
    public int GetCurrentDiffLineIndex()
    {
        if (CurrentDiffIndex <= 0 || CurrentDiffIndex > _diffLineIndices.Count)
            return -1;

        return _diffLineIndices[CurrentDiffIndex - 1];
    }

    #endregion

    #region Commands

    [RelayCommand]
    private void ShowRequestTab()
    {
        ShowRequest = true;
    }

    [RelayCommand]
    private void ShowResponseTab()
    {
        ShowResponse = true;
    }

    [RelayCommand]
    private void ToggleSummary()
    {
        ShowSummary = !ShowSummary;
    }

    [RelayCommand]
    private void PreviousDiff()
    {
        if (CurrentDiffIndex > 1)
        {
            CurrentDiffIndex--;
            ScrollToDiffRequested?.Invoke(GetCurrentDiffLineIndex());
        }
    }

    [RelayCommand]
    private void NextDiff()
    {
        if (CurrentDiffIndex < TotalDiffs)
        {
            CurrentDiffIndex++;
            ScrollToDiffRequested?.Invoke(GetCurrentDiffLineIndex());
        }
    }

    /// <summary>
    /// Event raised when copy is requested. The view should handle clipboard access.
    /// </summary>
    public event System.Action<string>? CopyRequested;

    /// <summary>
    /// Event raised when scrolling to a diff is requested.
    /// </summary>
    public event System.Action<int>? ScrollToDiffRequested;

    [RelayCommand]
    private void CopyA()
    {
        var text = string.Join("\n", LeftLines.Select(l => l.Text));
        if (!string.IsNullOrEmpty(text))
        {
            CopyRequested?.Invoke(text);
        }
    }

    [RelayCommand]
    private void CopyB()
    {
        var text = string.Join("\n", RightLines.Select(l => l.Text));
        if (!string.IsNullOrEmpty(text))
        {
            CopyRequested?.Invoke(text);
        }
    }

    #endregion
}
