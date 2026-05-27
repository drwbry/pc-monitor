using PcMonitor.Core.Models;

namespace PcMonitor.Core.History;

public sealed class HourlyHistoryReader : IHistoryReader, IDisposable
{
    private const int MinSamplesForAverage = 6;
    private readonly string _folder;
    private readonly FileSystemWatcher? _watcher;

    public event EventHandler? Changed;

    public HourlyHistoryReader(string folder, bool watch = false)
    {
        _folder = folder;
        if (watch && Directory.Exists(folder))
        {
            _watcher = new FileSystemWatcher(folder, "*.json")
            {
                IncludeSubdirectories = false,
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            };
            _watcher.Created += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
            _watcher.Changed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public IReadOnlyList<HourlyEntry> ReadAll()
    {
        if (!Directory.Exists(_folder)) return Array.Empty<HourlyEntry>();
        var entries = new List<HourlyEntry>();
        foreach (var file in Directory.EnumerateFiles(_folder, "*.json"))
        {
            try
            {
                var entry = HourlyJsonParser.Parse(File.ReadAllText(file));
                if (entry is not null) entries.Add(entry);
            }
            catch (IOException) { /* skip locked/partial files */ }
        }
        return entries.OrderByDescending(e => e.Timestamp).ToList();
    }

    public double? AverageHourlyErrorCount(int hoursBack = 24)
    {
        var cutoff = DateTimeOffset.UtcNow.AddHours(-hoursBack);
        var recent = ReadAll().Where(e => e.Timestamp >= cutoff).ToList();
        if (recent.Count < MinSamplesForAverage) return null;
        return recent.Average(e => (e.SystemErrorsLastHour ?? 0) + (e.AppErrorsLastHour ?? 0));
    }

    public void Dispose() => _watcher?.Dispose();
}
