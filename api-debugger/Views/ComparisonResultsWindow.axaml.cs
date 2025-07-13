using System;
using Avalonia.Controls;
using MusicBeeRemote.ApiDebugger.Models;
using MusicBeeRemote.ApiDebugger.ViewModels;

namespace MusicBeeRemote.ApiDebugger.Views;

public partial class ComparisonResultsWindow : Window
{
    private readonly ComparisonResultsViewModel _viewModel;

    public ComparisonResultsWindow()
    {
        InitializeComponent();
        _viewModel = new ComparisonResultsViewModel();
        _viewModel.ViewDiffRequested += OnViewDiffRequested;
        DataContext = _viewModel;
    }

    public ComparisonResultsWindow(SessionComparisonResult result) : this()
    {
        _viewModel.Load(result);
    }

    private void OnViewDiffRequested(PairComparisonResult result)
    {
        var diffWindow = new DiffViewWindow(result);
        diffWindow.ShowDialog(this);
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.ViewDiffRequested -= OnViewDiffRequested;
        base.OnClosed(e);
    }
}
