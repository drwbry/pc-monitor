using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using PcMonitor.App.Theming;
using PcMonitor.Core.Models;

namespace PcMonitor.App.ViewModels;

public partial class LiveTilesViewModel : ObservableObject
{
    private const int HistoryLength = 60; // one point per 1s tick
    private const int ProcessRows = 5;
    private readonly Queue<double> _cpuHistory = new();
    private readonly Queue<double> _ramHistory = new();

    [ObservableProperty] private string _cpuPercent = "--";
    [ObservableProperty] private Brush _cpuBrush = Palette.Cpu;
    [ObservableProperty] private SparkPoints _cpuSpark = SparkPoints.Empty;

    [ObservableProperty] private string _ramText = "--";
    [ObservableProperty] private string _ramSub = "";
    [ObservableProperty] private Brush _ramBrush = Palette.Ram;
    [ObservableProperty] private SparkPoints _ramSpark = SparkPoints.Empty;

    [ObservableProperty] private string _tempText = "--";
    [ObservableProperty] private string _tempSub = "CPU package";
    [ObservableProperty] private double _tempFraction;
    [ObservableProperty] private Brush _tempBrush = Palette.Muted;
    [ObservableProperty] private bool _tempUnavailable;

    [ObservableProperty] private string _driveCText = "--";
    [ObservableProperty] private Brush _driveBrush = Palette.Good;

    public ObservableCollection<ProcessRow> TopProcesses { get; } = new();

    public void Apply(SensorSnapshot s)
    {
        CpuPercent = s.CpuPercent is double cpu ? $"{cpu:F0}%" : "--";
        CpuBrush = Palette.ForLevel(s.CpuPercent, 85, 95, Palette.Cpu);
        CpuSpark = Push(_cpuHistory, s.CpuPercent ?? 0, 0, 100);

        var ramPct = s.RamTotalGb > 0 ? s.RamUsedGb / s.RamTotalGb * 100 : 0;
        RamText = $"{s.RamUsedGb:F1} GB";
        RamSub = $"of {s.RamTotalGb:F0} GB  ·  {ramPct:F0}%";
        RamBrush = Palette.ForLevel(100 - s.FreePhysicalRamPercent, 85, 95, Palette.Ram);
        RamSpark = Push(_ramHistory, ramPct, 0, 100);

        TempText = s.CpuPackageTempC is double t ? $"{t:F0}°C" : "--";
        TempUnavailable = s.CpuPackageTempC is null;
        TempSub = TempUnavailable ? "sensor unavailable" : "CPU package";
        TempFraction = s.CpuPackageTempC is double tf ? Math.Clamp(tf / 100.0, 0, 1) : 0;
        TempBrush = Palette.ForLevel(s.CpuPackageTempC, 85, 95, Palette.Good);

        DriveCText = s.DriveCFreeGb is double gb ? $"{gb:F0} GB" : "--";
        DriveBrush = s.DriveCFreeGb is not double free ? Palette.Muted
            : free < 5 ? Palette.Bad : free < 20 ? Palette.Warn : Palette.Good;

        ApplyProcesses(s.TopProcesses);
    }

    private static SparkPoints Push(Queue<double> history, double value, double min, double max)
    {
        history.Enqueue(value);
        while (history.Count > HistoryLength) history.Dequeue();
        return SparkPoints.Build(history.ToList(), min, max, HistoryLength);
    }

    /// <summary>Updates the existing rows in place so the list doesn't rebuild its visuals every second.</summary>
    private void ApplyProcesses(IReadOnlyList<ProcessSample> samples)
    {
        var top = samples.Take(ProcessRows).ToList();
        var maxRam = top.Count > 0 ? Math.Max(1, top.Max(p => p.RamMb)) : 1;
        while (TopProcesses.Count > top.Count) TopProcesses.RemoveAt(TopProcesses.Count - 1);
        while (TopProcesses.Count < top.Count) TopProcesses.Add(new ProcessRow());
        for (var i = 0; i < top.Count; i++)
            TopProcesses[i].Update(top[i], maxRam);
    }
}

public partial class ProcessRow : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _cpuPercent = "";
    [ObservableProperty] private string _ram = "";
    [ObservableProperty] private double _ramFraction;

    public void Update(ProcessSample p, double maxRamMb)
    {
        Name = p.Name;
        CpuPercent = $"{p.CpuPercent:F1}%";
        Ram = p.RamMb >= 1024 ? $"{p.RamMb / 1024.0:F1} GB" : $"{p.RamMb:F0} MB";
        RamFraction = p.RamMb / maxRamMb;
    }
}
