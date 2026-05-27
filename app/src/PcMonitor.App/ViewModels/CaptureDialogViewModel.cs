using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcMonitor.Core.Capture;
using PcMonitor.Core.Models;

namespace PcMonitor.App.ViewModels;

public partial class CaptureDialogViewModel : ObservableObject, IDisposable
{
    private readonly ICaptureService _capture;
    private readonly CaptureKind _kind;
    private readonly CancellationTokenSource _cts = new();

    [ObservableProperty] private string _status = "Running…";
    [ObservableProperty] private bool _isRunning = true;
    [ObservableProperty] private bool _isSuccess;
    [ObservableProperty] private bool _isFailure;
    [ObservableProperty] private string? _windowsPath;
    [ObservableProperty] private string? _wslPath;
    [ObservableProperty] private string? _suggestedPrompt;
    [ObservableProperty] private string? _stdErr;
    public ObservableCollection<string> Lines { get; } = new();

    public IRelayCommand CancelCommand { get; }
    public IRelayCommand CopyPromptCommand { get; }
    public IRelayCommand OpenInExplorerCommand { get; }

    public CaptureDialogViewModel(ICaptureService capture, CaptureKind kind)
    {
        _capture = capture;
        _kind = kind;
        CancelCommand = new RelayCommand(() => _cts.Cancel());
        CopyPromptCommand = new RelayCommand(
            () => { if (SuggestedPrompt is not null) Clipboard.SetText(SuggestedPrompt); },
            () => SuggestedPrompt is not null);
        OpenInExplorerCommand = new RelayCommand(
            () =>
            {
                if (WindowsPath is null) return;
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{WindowsPath}\"");
            },
            () => WindowsPath is not null);
    }

    public void Dispose() => _cts.Dispose();

    public async Task RunAsync()
    {
        try
        {
            var result = await _capture.RunAsync(_kind,
                line => Application.Current.Dispatcher.Invoke(() => Lines.Add(line.Text)),
                _cts.Token);
            IsRunning = false;
            if (result.Cancelled) { Status = "Cancelled."; IsFailure = true; return; }
            if (!result.Success)
            {
                Status = "Capture failed.";
                IsFailure = true;
                StdErr = result.StdErr;
                return;
            }
            IsSuccess = true;
            Status = "Capture complete.";
            WindowsPath = result.WindowsPath;
            WslPath = result.WslPath;
            SuggestedPrompt = _kind == CaptureKind.Diagnostic
                ? $"Read {result.WslPath} and give me the top 5 issues to address."
                : $"Read {result.WslPath} and tell me what's hammering the CPU right now.";
            CopyPromptCommand.NotifyCanExecuteChanged();
            OpenInExplorerCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            IsRunning = false;
            IsFailure = true;
            Status = $"Error: {ex.Message}";
        }
    }
}
