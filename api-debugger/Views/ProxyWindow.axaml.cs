using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using MusicBeeRemote.ApiDebugger.Models;
using MusicBeeRemote.ApiDebugger.Services;
using MusicBeeRemote.ApiDebugger.ViewModels;

namespace MusicBeeRemote.ApiDebugger.Views;

public partial class ProxyWindow : Window, IDisposable
{
    private static readonly string[] JsonPatterns = ["*.json"];
    private static readonly string[] AllFilesPatterns = ["*.*"];

    private readonly ProxyWindowViewModel _viewModel;
    private bool _disposed;

    public ProxyWindow()
    {
        InitializeComponent();
        _viewModel = new ProxyWindowViewModel();
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        _viewModel.LogMessages.CollectionChanged += LogMessages_CollectionChanged;
        _viewModel.CopyRequested += OnCopyRequested;
        _viewModel.SaveSessionRequested += OnSaveSessionRequested;
        _viewModel.LoadSessionRequested += OnLoadSessionRequested;
        _viewModel.ShowComparisonRequested += OnShowComparisonRequested;
        DataContext = _viewModel;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProxyWindowViewModel.SelectedMessageJson))
        {
            UpdateSelectedMessageHighlighting();
        }
    }

    private void LogMessages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && _viewModel.LogMessages.Count > 0)
        {
            // Auto-scroll to the last item
            MessagesListBox.ScrollIntoView(_viewModel.LogMessages[^1]);
        }
    }

    private void UpdateSelectedMessageHighlighting()
    {
        var jsonText = _viewModel.SelectedMessageJson;
        if (string.IsNullOrEmpty(jsonText))
        {
            SelectedMessageTextBlock.Inlines?.Clear();
            return;
        }

        ApplyJsonSyntaxHighlighting(SelectedMessageTextBlock, jsonText);
    }

    protected override void OnClosed(EventArgs e)
    {
        Dispose();
        base.OnClosed(e);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _viewModel.LogMessages.CollectionChanged -= LogMessages_CollectionChanged;
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            _viewModel.CopyRequested -= OnCopyRequested;
            _viewModel.SaveSessionRequested -= OnSaveSessionRequested;
            _viewModel.LoadSessionRequested -= OnLoadSessionRequested;
            _viewModel.ShowComparisonRequested -= OnShowComparisonRequested;
            _viewModel.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    private void OnCopyRequested(string text)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            _ = CopyToClipboardAsync(clipboard, text);
        }
    }

    private async Task OnSaveSessionRequested(RecordedSession session)
    {
        var storage = StorageProvider;
        var suggestedName = SessionStorage.GenerateFilename(session);
        var defaultFolder = SessionStorage.GetDefaultSessionsFolder();

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Session",
            SuggestedFileName = suggestedName,
            DefaultExtension = "json",
            FileTypeChoices = new List<FilePickerFileType>
            {
                new("JSON Files") { Patterns = JsonPatterns },
                new("All Files") { Patterns = AllFilesPatterns }
            },
            SuggestedStartLocation = await storage.TryGetFolderFromPathAsync(defaultFolder)
        });

        if (file != null)
        {
            try
            {
                await SessionStorage.SaveSessionAsync(session, file.Path.LocalPath);
                await ShowMessageDialogAsync("Saved", $"Session saved to {file.Name}");
            }
            catch (Exception ex)
            {
                await ShowMessageDialogAsync("Error", $"Failed to save session: {ex.Message}");
            }
        }
    }

    private async Task<RecordedSession?> OnLoadSessionRequested()
    {
        var storage = StorageProvider;
        var defaultFolder = SessionStorage.GetDefaultSessionsFolder();

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load Session",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("JSON Files") { Patterns = JsonPatterns },
                new("All Files") { Patterns = AllFilesPatterns }
            },
            SuggestedStartLocation = await storage.TryGetFolderFromPathAsync(defaultFolder)
        });

        if (files.Count > 0)
        {
            try
            {
                return await SessionStorage.LoadSessionAsync(files[0].Path.LocalPath);
            }
            catch (Exception ex)
            {
                await ShowMessageDialogAsync("Error", $"Failed to load session: {ex.Message}");
            }
        }

        return null;
    }

    private void OnShowComparisonRequested(SessionComparisonResult result)
    {
        var comparisonWindow = new ComparisonResultsWindow(result);
        comparisonWindow.ShowDialog(this);
    }

    private void ExpandJson_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedMessage?.HasJson != true)
            return;

        var jsonData = ProxyWindowViewModel.GetJsonForMessage(_viewModel.SelectedMessage);
        if (!string.IsNullOrEmpty(jsonData))
        {
            _ = ShowJsonDialogAsync(jsonData, $"JSON: {_viewModel.SelectedMessage.Content}");
        }
    }

    private void MessagesList_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not ListBox listBox || listBox.SelectedItem is not LogMessage selectedMessage)
            return;

        // Only show dialog for messages that have JSON attached
        if (!selectedMessage.HasJson)
            return;

        var jsonData = ProxyWindowViewModel.GetJsonForMessage(selectedMessage);
        if (!string.IsNullOrEmpty(jsonData))
        {
            _ = ShowJsonDialogAsync(jsonData, $"JSON: {selectedMessage.Content}");
        }
    }

    private Task ShowJsonDialogAsync(string jsonText, string title)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 700,
            Height = 550,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.Parse("#1e1e1e"))
        };

        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            Margin = new Avalonia.Thickness(12)
        };

        var scrollViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        };

        var textBlock = new SelectableTextBlock
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            Padding = new Avalonia.Thickness(12),
            Background = new SolidColorBrush(Color.Parse("#252526")),
            TextWrapping = TextWrapping.NoWrap
        };

        ApplyJsonSyntaxHighlighting(textBlock, jsonText);

        scrollViewer.Content = textBlock;
        Grid.SetRow(scrollViewer, 0);

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            Margin = new Avalonia.Thickness(0, 12, 0, 0)
        };

        var copyButton = new Button
        {
            Content = "Copy to Clipboard",
            Padding = new Avalonia.Thickness(16, 8),
            Margin = new Avalonia.Thickness(0, 0, 8, 0),
            Background = new SolidColorBrush(Color.Parse("#0e639c")),
            Foreground = Brushes.White
        };
        copyButton.Click += (s, e) =>
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            {
                _ = CopyToClipboardAsync(clipboard, jsonText);
            }
        };

        var closeButton = new Button
        {
            Content = "Close",
            Padding = new Avalonia.Thickness(16, 8),
            Background = new SolidColorBrush(Color.Parse("#3c3c3c")),
            Foreground = Brushes.White
        };
        closeButton.Click += (s, e) => dialog.Close();

        buttonPanel.Children.Add(copyButton);
        buttonPanel.Children.Add(closeButton);
        Grid.SetRow(buttonPanel, 1);

        grid.Children.Add(scrollViewer);
        grid.Children.Add(buttonPanel);

        dialog.Content = grid;
        return dialog.ShowDialog(this);
    }

    private static void ApplyJsonSyntaxHighlighting(SelectableTextBlock textBlock, string json)
    {
        var keyColor = Color.Parse("#9CDCFE");
        var stringColor = Color.Parse("#CE9178");
        var numberColor = Color.Parse("#B5CEA8");
        var boolNullColor = Color.Parse("#569CD6");
        var bracketColor = Color.Parse("#D4D4D4");
        var colonColor = Color.Parse("#D4D4D4");

        var inlines = textBlock.Inlines ?? new InlineCollection();
        inlines.Clear();

        var pattern = @"(""(?:[^""\\]|\\.)*"")\s*(:)|" +
                      @"(""(?:[^""\\]|\\.)*"")|" +
                      @"(-?\d+\.?\d*(?:[eE][+-]?\d+)?)|" +
                      @"(true|false|null)|" +
                      @"([{}\[\],:])|" +
                      @"(\s+)";

        var regex = new Regex(pattern);
        var matches = regex.Matches(json);

        foreach (Match match in matches)
        {
            if (match.Groups[1].Success)
            {
                inlines.Add(new Run(match.Groups[1].Value) { Foreground = new SolidColorBrush(keyColor) });
                inlines.Add(new Run(match.Groups[2].Value) { Foreground = new SolidColorBrush(colonColor) });
            }
            else if (match.Groups[3].Success)
            {
                inlines.Add(new Run(match.Groups[3].Value) { Foreground = new SolidColorBrush(stringColor) });
            }
            else if (match.Groups[4].Success)
            {
                inlines.Add(new Run(match.Groups[4].Value) { Foreground = new SolidColorBrush(numberColor) });
            }
            else if (match.Groups[5].Success)
            {
                inlines.Add(new Run(match.Groups[5].Value) { Foreground = new SolidColorBrush(boolNullColor) });
            }
            else if (match.Groups[6].Success)
            {
                inlines.Add(new Run(match.Groups[6].Value) { Foreground = new SolidColorBrush(bracketColor) });
            }
            else if (match.Groups[7].Success)
            {
                inlines.Add(new Run(match.Groups[7].Value));
            }
        }
    }

    private Task ShowMessageDialogAsync(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 350,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = new SolidColorBrush(Color.Parse("#1e1e1e"))
        };

        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            Margin = new Avalonia.Thickness(20)
        };

        var textBlock = new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.Parse("#e0e0e0"))
        };
        Grid.SetRow(textBlock, 0);

        var okButton = new Button
        {
            Content = "OK",
            Width = 80,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Avalonia.Thickness(0, 10, 0, 0),
            Background = new SolidColorBrush(Color.Parse("#0e639c")),
            Foreground = Brushes.White
        };
        okButton.Click += (s, e) => dialog.Close();
        Grid.SetRow(okButton, 1);

        grid.Children.Add(textBlock);
        grid.Children.Add(okButton);

        dialog.Content = grid;
        return dialog.ShowDialog(this);
    }

    private async Task CopyToClipboardAsync(Avalonia.Input.Platform.IClipboard clipboard, string text)
    {
        await clipboard.SetTextAsync(text);
        await ShowMessageDialogAsync("Copied", "JSON copied to clipboard!");
    }
}
