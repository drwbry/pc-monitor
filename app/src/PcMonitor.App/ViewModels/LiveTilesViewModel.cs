using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PcMonitor.Core.Models;

namespace PcMonitor.App.ViewModels;

public partial class LiveTilesViewModel : ObservableObject
{
    [ObservableProperty] private string _cpuPercent = "--";
    [ObservableProperty] private string _ramText = "--";
    [ObservableProperty] private string _tempText = "--";
    [ObservableProperty] private string _driveCText = "--";
    [ObservableProperty] private bool _tempUnavailable;

    public ObservableCollection<ProcessRow> TopProcesses { get; } = new();

    public void Apply(SensorSnapshot s)
    {
        CpuPercent = s.CpuPercent is double cpu ? $"{cpu:F0}%" : "--";
        RamText = $"{s.RamUsedGb:F1} / {s.RamTotalGb:F0} GB";
        TempText = s.CpuPackageTempC is double t ? $"{t:F0}°C" : "--";
        TempUnavailable = s.CpuPackageTempC is null;
        DriveCText = s.DriveCFreeGb is double gb ? $"{gb:F0} GB free" : "--";
        TopProcesses.Clear();
        foreach (var p in s.TopProcesses.Take(5))
            TopProcesses.Add(new ProcessRow(p.Name, $"{p.CpuPercent:F1}", $"{p.RamMb / 1024.0:F1} GB"));
    }
}

public sealed record ProcessRow(string Name, string CpuPercent, string Ram);
