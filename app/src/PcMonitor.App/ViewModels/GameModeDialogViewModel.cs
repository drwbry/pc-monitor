using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcMonitor.Core.GameMode;

namespace PcMonitor.App.ViewModels;

public partial class GameModeRowViewModel : ObservableObject
{
    [ObservableProperty] private bool _isSelected = true;
    public required string Name { get; init; }
    public string? Note { get; init; }
    public string MemoryText { get; init; } = "";
    public long MemoryBytes { get; init; }
    public PlannedItem? Planned { get; init; }
    public StoppedItem? Stopped { get; init; }
}

/// <summary>
/// Step 2 of the two-click flow. Lists what will be closed (or brought back), all checked by default.
/// The user can uncheck items, then confirms.
/// </summary>
public partial class GameModeDialogViewModel : ObservableObject
{
    private readonly GameModeService _svc;

    public bool Exiting { get; }
    public string Title => Exiting ? "End Game Mode" : "Start Game Mode";
    public string Heading => Exiting
        ? "These will be started again. Uncheck anything you want to leave closed."
        : "These will be closed. Uncheck anything you want to keep running.";
    public string ConfirmText => Exiting ? "Confirm - end Game Mode" : "Confirm - start Game Mode";

    public ObservableCollection<GameModeRowViewModel> Rows { get; } = new();
    public ObservableCollection<string> Lines { get; } = new();

    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _isDone;
    [ObservableProperty] private bool _dryRun;
    [ObservableProperty] private string? _status;
    [ObservableProperty] private string? _configError;
    [ObservableProperty] private string _totalText = "";

    public bool IsEditable => !IsLoading && !IsRunning && !IsDone;

    public IAsyncRelayCommand ConfirmCommand { get; }

    public GameModeDialogViewModel(GameModeService svc)
    {
        _svc = svc;
        Exiting = svc.IsActive;
        ConfirmCommand = new AsyncRelayCommand(ConfirmAsync, CanConfirm);
    }

    private bool CanConfirm() =>
        IsEditable && (Rows.Any(r => r.IsSelected) || (Exiting && Rows.Count == 0));

    public async Task LoadAsync()
    {
        try
        {
            if (Exiting)
            {
                var plan = await Task.Run(_svc.PlanExit);
                foreach (var s in plan)
                    AddRow(new GameModeRowViewModel { Name = s.Item.DisplayName, Note = s.Item.Note, Stopped = s });
                if (plan.Count == 0)
                    Status = "Nothing to bring back: everything Game Mode closed is already running. Confirm to switch Game Mode off.";
            }
            else
            {
                var plan = await Task.Run(_svc.PlanEnter);
                ConfigError = _svc.ConfigError;
                foreach (var p in plan)
                    AddRow(new GameModeRowViewModel
                    {
                        Name = p.Item.DisplayName, Note = p.Item.Note, Planned = p,
                        MemoryBytes = p.MemoryBytes, MemoryText = FormatBytes(p.MemoryBytes),
                    });
                if (plan.Count == 0)
                    Status = "Nothing on the Game Mode list is running right now.";
            }
        }
        catch (Exception ex)
        {
            Status = $"Could not read running apps: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            UpdateTotal();
        }
    }

    private void AddRow(GameModeRowViewModel row)
    {
        row.PropertyChanged += OnRowChanged;
        Rows.Add(row);
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GameModeRowViewModel.IsSelected)) UpdateTotal();
    }

    private void UpdateTotal()
    {
        if (!Exiting)
        {
            var bytes = Rows.Where(r => r.IsSelected).Sum(r => r.MemoryBytes);
            TotalText = bytes > 0 ? $"About {FormatBytes(bytes)} of RAM will be freed." : "";
        }
        ConfirmCommand.NotifyCanExecuteChanged();
    }

    private async Task ConfirmAsync()
    {
        IsRunning = true;
        Status = Exiting ? "Starting things back up..." : "Closing...";
        void Log(string line) => Application.Current.Dispatcher.Invoke(() => Lines.Add(line));
        try
        {
            var selected = Rows.Where(r => r.IsSelected).ToList();
            if (Exiting)
            {
                var items = selected.Select(r => r.Stopped!).ToList();
                await Task.Run(() => _svc.ExitAsync(items, DryRun, Log));
                Status = DryRun ? "Dry run finished. Nothing was changed." : "Game Mode is off.";
            }
            else
            {
                var items = selected.Select(r => r.Planned!).ToList();
                await Task.Run(() => _svc.EnterAsync(items, DryRun, Log));
                Status = DryRun ? "Dry run finished. Nothing was changed." : "Game Mode is on. Have fun.";
            }
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
            IsDone = true;
        }
    }

    partial void OnIsLoadingChanged(bool value) => OnEditableChanged();
    partial void OnIsRunningChanged(bool value) => OnEditableChanged();
    partial void OnIsDoneChanged(bool value) => OnEditableChanged();

    private void OnEditableChanged()
    {
        OnPropertyChanged(nameof(IsEditable));
        ConfirmCommand.NotifyCanExecuteChanged();
    }

    private static string FormatBytes(long bytes) =>
        bytes >= 1024L * 1024 * 1024
            ? $"{bytes / (1024.0 * 1024 * 1024):N1} GB"
            : $"{bytes / (1024.0 * 1024):N0} MB";
}
