using PcMonitor.Core.Models;

namespace PcMonitor.Core.Sensors;

public sealed class ProcessSampler
{
    private readonly IProcessEnumerator _enumerator;
    private readonly int _logicalCores;
    private DateTimeOffset _lastSampleAt;
    private Dictionary<int, RawProcess> _previous = new();

    public ProcessSampler(IProcessEnumerator enumerator, int logicalCores)
    {
        _enumerator = enumerator;
        _logicalCores = Math.Max(1, logicalCores);
    }

    public IReadOnlyList<ProcessSample> Sample(DateTimeOffset now)
    {
        var current = _enumerator.Enumerate().ToDictionary(p => p.Pid);

        if (_previous.Count == 0)
        {
            _previous = current;
            _lastSampleAt = now;
            return Array.Empty<ProcessSample>();
        }

        var deltaSeconds = (now - _lastSampleAt).TotalSeconds;
        if (deltaSeconds <= 0)
        {
            _previous = current;
            _lastSampleAt = now;
            return Array.Empty<ProcessSample>();
        }

        var results = new List<ProcessSample>(current.Count);
        foreach (var (pid, proc) in current)
        {
            if (!_previous.TryGetValue(pid, out var prev)) continue;
            var cpuSeconds = (proc.TotalProcessorTime - prev.TotalProcessorTime).TotalSeconds;
            var pct = (cpuSeconds / deltaSeconds) / _logicalCores * 100.0;
            pct = Math.Clamp(pct, 0, 100);
            var ramMb = proc.WorkingSetBytes / (1024.0 * 1024.0);
            results.Add(new ProcessSample(pid, proc.Name, pct, ramMb));
        }

        _previous = current;
        _lastSampleAt = now;

        return results
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ProcessSample(0, g.Key,
                Math.Min(100.0, g.Sum(p => p.CpuPercent)),
                g.Sum(p => p.RamMb)))
            .OrderByDescending(p => p.CpuPercent)
            .ToList();
    }
}
