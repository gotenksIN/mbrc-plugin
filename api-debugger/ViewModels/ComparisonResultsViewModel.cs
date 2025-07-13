using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicBeeRemote.ApiDebugger.Models;

namespace MusicBeeRemote.ApiDebugger.ViewModels;

/// <summary>
/// ViewModel for the comparison results window.
/// </summary>
public partial class ComparisonResultsViewModel : ObservableObject
{
    private SessionComparisonResult? _comparisonResult;

    public ComparisonResultsViewModel()
    {
    }

    public ComparisonResultsViewModel(SessionComparisonResult result)
    {
        _comparisonResult = result;
        AllResults = new ObservableCollection<PairComparisonResult>(result.Results);
        UpdateFilteredResults();
    }

    #region Observable Properties

    [ObservableProperty]
    private string _sessionAName = "";

    [ObservableProperty]
    private string _sessionBName = "";

    [ObservableProperty]
    private string _summaryText = "";

    [ObservableProperty]
    private int _sessionAPairCount;

    [ObservableProperty]
    private int _sessionBPairCount;

    [ObservableProperty]
    private bool _showDifferencesOnly;

    [ObservableProperty]
    private ObservableCollection<PairComparisonResult> _allResults = [];

    [ObservableProperty]
    private ObservableCollection<PairComparisonResult> _filteredResults = [];

    [ObservableProperty]
    private PairComparisonResult? _selectedResult;

    [ObservableProperty]
    private ObservableCollection<string> _availableContexts = [];

    [ObservableProperty]
    private string? _selectedContext;

    #endregion

    #region Computed Properties

    /// <summary>
    /// Gets the comparison result data.
    /// </summary>
    public SessionComparisonResult? ComparisonResult => _comparisonResult;

    #endregion

    #region Methods

    /// <summary>
    /// Loads the comparison result data into the view model.
    /// </summary>
    public void Load(SessionComparisonResult result)
    {
        _comparisonResult = result;

        SessionAName = result.SessionA.Name;
        SessionBName = result.SessionB.Name;
        SessionAPairCount = result.SessionA.Pairs.Count;
        SessionBPairCount = result.SessionB.Pairs.Count;
        SummaryText = result.Summary;

        AllResults = new ObservableCollection<PairComparisonResult>(result.Results);

        // Build list of unique contexts for filtering
        var contexts = result.Results
            .Select(r => r.Context)
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        AvailableContexts = new ObservableCollection<string>(["All Contexts", .. contexts]);
        SelectedContext = "All Contexts";

        UpdateFilteredResults();
    }

    partial void OnShowDifferencesOnlyChanged(bool value)
    {
        UpdateFilteredResults();
    }

    partial void OnSelectedContextChanged(string? value)
    {
        UpdateFilteredResults();
    }

    private void UpdateFilteredResults()
    {
        var results = AllResults.AsEnumerable();

        // Filter by context if a specific one is selected
        if (!string.IsNullOrEmpty(SelectedContext) && SelectedContext != "All Contexts")
        {
            results = results.Where(r => r.Context == SelectedContext);
        }

        // Filter by differences only if enabled
        if (ShowDifferencesOnly)
        {
            results = results.Where(r => r.HasDifferences);
        }

        FilteredResults = new ObservableCollection<PairComparisonResult>(results);
    }

    #endregion

    #region Commands

    [RelayCommand]
    private void ShowAll()
    {
        ShowDifferencesOnly = false;
    }

    [RelayCommand]
    private void ShowDiffsOnly()
    {
        ShowDifferencesOnly = true;
    }

    /// <summary>
    /// Event raised when the user wants to view a diff detail.
    /// </summary>
    public event Action<PairComparisonResult>? ViewDiffRequested;

    [RelayCommand]
    private void ViewDiff(PairComparisonResult? result)
    {
        if (result != null)
        {
            ViewDiffRequested?.Invoke(result);
        }
    }

    #endregion
}
