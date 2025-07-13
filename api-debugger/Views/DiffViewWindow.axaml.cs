using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MusicBeeRemote.ApiDebugger.Models;
using MusicBeeRemote.ApiDebugger.ViewModels;

namespace MusicBeeRemote.ApiDebugger.Views;

public partial class DiffViewWindow : Window
{
    private readonly DiffViewViewModel _viewModel;
    private bool _isSyncingScroll;

    public DiffViewWindow()
    {
        InitializeComponent();
        _viewModel = new DiffViewViewModel();
        _viewModel.CopyRequested += OnCopyRequested;
        _viewModel.ScrollToDiffRequested += OnScrollToDiffRequested;
        DataContext = _viewModel;

        // Set up synchronized scrolling
        SetupSynchronizedScrolling();
    }

    public DiffViewWindow(PairComparisonResult result) : this()
    {
        _viewModel.Load(result);
        // Start precomputing both diffs in background
        _ = _viewModel.PrecomputeDiffsAsync();
    }

    private void SetupSynchronizedScrolling()
    {
        // Sync scroll positions between the two panels
        ScrollViewerA.ScrollChanged += (_, e) =>
        {
            if (_isSyncingScroll)
                return;
            _isSyncingScroll = true;
            ScrollViewerB.Offset = ScrollViewerA.Offset;
            _isSyncingScroll = false;
        };

        ScrollViewerB.ScrollChanged += (_, e) =>
        {
            if (_isSyncingScroll)
                return;
            _isSyncingScroll = true;
            ScrollViewerA.Offset = ScrollViewerB.Offset;
            _isSyncingScroll = false;
        };
    }

    private void OnScrollToDiffRequested(int lineIndex)
    {
        if (lineIndex < 0)
            return;

        // Estimate line height (approximately 20 pixels per line)
        const double lineHeight = 20;
        var targetOffset = lineIndex * lineHeight;

        // Center the diff line in the viewport
        var viewportHeight = ScrollViewerA.Viewport.Height;
        var centeredOffset = Math.Max(0, targetOffset - viewportHeight / 2);

        _isSyncingScroll = true;
        ScrollViewerA.Offset = new Avalonia.Vector(ScrollViewerA.Offset.X, centeredOffset);
        ScrollViewerB.Offset = new Avalonia.Vector(ScrollViewerB.Offset.X, centeredOffset);
        _isSyncingScroll = false;
    }

    private void OnCopyRequested(string text)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            _ = CopyToClipboardAsync(clipboard, text);
        }
    }

    private static Task CopyToClipboardAsync(Avalonia.Input.Platform.IClipboard clipboard, string text)
    {
        return clipboard.SetTextAsync(text);
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.CopyRequested -= OnCopyRequested;
        _viewModel.ScrollToDiffRequested -= OnScrollToDiffRequested;
        base.OnClosed(e);
    }
}
