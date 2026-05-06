// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Collections.Concurrent;
using System.Text;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

/// <summary>
/// ViewModel for the ZWR Import Tool page.
/// Allows selecting a directory of .zwr files and importing them into Orleans grains.
/// </summary>
public partial class ZwrImportViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly OrleansGrainService _grains;

    /// <summary>Buffered log lines from background import threads.</summary>
    private readonly ConcurrentQueue<string> _pendingLines = new();

    /// <summary>Accumulates all log output efficiently.</summary>
    private readonly StringBuilder _logBuilder = new();

    /// <summary>Timer that flushes pending log lines to the UI every 250 ms.</summary>
    private DispatcherTimer? _flushTimer;

    [ObservableProperty]
    private string _selectedPath = string.Empty;

    [ObservableProperty]
    private string _logText = string.Empty;

    [ObservableProperty]
    private bool _isImporting;

    [ObservableProperty]
    private string? _error;

    public ZwrImportViewModel(ApiClient api, OrleansGrainService grains)
    {
        _api = api;
        _grains = grains;
    }

    partial void OnIsImportingChanged(bool value) => ImportCommand.NotifyCanExecuteChanged();
    partial void OnSelectedPathChanged(string value) => ImportCommand.NotifyCanExecuteChanged();

    [RelayCommand]
    private void BrowseFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select ZWR Import Directory"
        };
        if (dialog.ShowDialog() == true)
        {
            SelectedPath = dialog.FolderName;
        }
    }

    [RelayCommand(CanExecute = nameof(CanImport))]
    private async Task ImportAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedPath)) return;

        IsImporting = true;
        Error = null;
        _logBuilder.Clear();
        LogText = string.Empty;

        // Start a timer that flushes queued log lines to the UI at a fixed interval.
        // This prevents thousands of individual dispatcher invocations from freezing the UI.
        _flushTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _flushTimer.Tick += (_, _) => FlushPendingLines();
        _flushTimer.Start();

        var logger = new ActionLogger(message => _pendingLines.Enqueue(message));

        try
        {
            AppendLog($"Starting ZWR import from: {SelectedPath}");

            // TODO: ZwrImportOrchestrator requires IGrainFactory (direct grain access).
            // A server-side import API endpoint is needed to support this from the WPF client.
            throw new NotSupportedException(
                "ZWR import is not yet supported via the REST API. " +
                "A server-side import endpoint must be created first.");
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            AppendLog($"[ERROR] {ex.Message}");
        }
        finally
        {
            _flushTimer?.Stop();
            _flushTimer = null;
            FlushPendingLines();
            IsImporting = false;
        }
    }

    private bool CanImport() => !IsImporting && !string.IsNullOrWhiteSpace(SelectedPath);

    /// <summary>
    /// Drains all queued log lines into the StringBuilder and updates LogText once.
    /// Called on the UI thread by the DispatcherTimer.
    /// </summary>
    private void FlushPendingLines()
    {
        if (_pendingLines.IsEmpty) return;

        while (_pendingLines.TryDequeue(out string? line))
        {
            _logBuilder.AppendLine(line);
        }

        LogText = _logBuilder.ToString();
    }

    private void AppendLog(string message)
    {
        _logBuilder.AppendLine(message);
        LogText = _logBuilder.ToString();
    }
}
