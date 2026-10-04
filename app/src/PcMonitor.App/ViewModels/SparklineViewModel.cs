using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using PcMonitor.App.Theming;
using PcMonitor.Core.History;

namespace PcMonitor.App.ViewModels;

/// <summary>24h trend charts built from the hourly snapshots. Rebuilt only when a new snapshot lands.</summary>
public partial class SparklineViewModel : ObservableObject
{
    private readonly IHistoryReader _history;
    [ObservableProperty] private SparkPoints _cpu = SparkPoints.Empty;
    [ObservableProperty] private SparkPoints _ram = SparkPoints.Empty;
    [ObservableProperty] private SparkPoints _errors = SparkPoints.Empty;
    [ObservableProperty] private string _cpuSummary = "";
    [ObservableProperty] private string _ramSummary = "";
    [ObservableProperty] private string _errorsSummary = "";
    [ObservableProperty] private bool _available;

    public SparklineViewModel(IHistoryReader history)
    {
        _history = history;
        _history.Changed += (_, _) => Application.Current?.Dispatcher.BeginInvoke(Refresh);
        Refresh();
    }

    public void Refresh()
    {
        var data = _history.ReadAll();
        Available = data.Count > 0;
        if (!Available) return;
        var ordered = data.OrderBy(e => e.Timestamp).TakeLast(24).ToList();

        var cpu = ordered.Select(e => e.CpuPercent ?? 0).ToList();
        var ram = ordered.Select(e => e.RamUsedGb ?? 0).ToList();
        var errors = ordered.Select(e => (double)((e.SystemErrorsLastHour ?? 0) + (e.AppErrorsLastHour ?? 0))).ToList();

        Cpu = SparkPoints.Build(cpu, 0, 100);
        Ram = SparkPoints.Build(ram, 0, 1);
        Errors = SparkPoints.Build(errors, 0, 1);
        CpuSummary = $"avg {cpu.Average():F0}%  ·  peak {cpu.Max():F0}%";
        RamSummary = $"avg {ram.Average():F1} GB  ·  peak {ram.Max():F1} GB";
        ErrorsSummary = $"{errors.Sum():F0} total  ·  worst hour {errors.Max():F0}";
    }
}
