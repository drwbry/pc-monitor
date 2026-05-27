using PcMonitor.Core.Models;

namespace PcMonitor.Core.Issues.Rules;

public sealed class MemoryHogRule : IIssueRule
{
    public string RuleId => "memory-hog";
    public IssueSeverity Severity => IssueSeverity.Yellow;
    public TimeSpan SustainedFor => TimeSpan.Zero;

    public RuleCheck Check(SensorSnapshot s)
    {
        var top = s.TopProcesses.OrderByDescending(p => p.RamMb).FirstOrDefault();
        if (top is null || top.RamMb <= 4096) return RuleCheck.NotMet;
        return new RuleCheck(true,
            SubjectKey: $"{top.Name}:{top.ProcessId}",
            Title: $"{top.Name} memory hog",
            Detail: $"{top.RamMb / 1024.0:F1} GB RAM",
            Metrics: new Dictionary<string, double?> { ["ram_mb"] = top.RamMb });
    }
}
