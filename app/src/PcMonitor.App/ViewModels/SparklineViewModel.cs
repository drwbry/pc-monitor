using CommunityToolkit.Mvvm.ComponentModel;
using PcMonitor.Core.History;

namespace PcMonitor.App.ViewModels;

public partial class SparklineViewModel : ObservableObject
{
    private readonly IHistoryReader _history;
    [ObservableProperty] private string _cpu = "";
    [ObservableProperty] private string _ram = "";
    [ObservableProperty] private string _errors = "";
    [ObservableProperty] private bool _available;

    public SparklineViewModel(IHistoryReader history)
    {
        _history = history;
        _history.Changed += (_, _) => Refresh();
        Refresh();
    }

    public void Refresh()
    {
        var data = _history.ReadAll();
        Available = data.Count > 0;
        if (!Available) return;
        var ordered = data.OrderBy(e => e.Timestamp).TakeLast(24).ToList();
        Cpu = Spark(ordered.Select(e => e.CpuPercent ?? 0));
        Ram = Spark(ordered.Select(e => e.RamUsedGb ?? 0));
        Errors = Spark(ordered.Select(e => (double)((e.SystemErrorsLastHour ?? 0) + (e.AppErrorsLastHour ?? 0))));
    }

    private static string Spark(IEnumerable<double> values)
    {
        const string blocks = "▁▂▃▄▅▆▇█";
        var list = values.ToList();
        if (list.Count == 0) return "";
        var min = list.Min(); var max = list.Max();
        var range = Math.Max(0.001, max - min);
        var sb = new System.Text.StringBuilder(list.Count);
        foreach (var v in list)
        {
            var idx = (int)Math.Round((v - min) / range * (blocks.Length - 1));
            sb.Append(blocks[Math.Clamp(idx, 0, blocks.Length - 1)]);
        }
        return sb.ToString();
    }
}
