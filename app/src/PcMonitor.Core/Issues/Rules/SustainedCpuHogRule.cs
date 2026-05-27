using PcMonitor.Core.Models;

namespace PcMonitor.Core.Issues.Rules;

public sealed class SustainedCpuHogRule : IIssueRule
{
    public string RuleId => "sustained-cpu-hog";
    public IssueSeverity Severity => IssueSeverity.Yellow;
    public TimeSpan SustainedFor => TimeSpan.FromMinutes(10);

    public RuleCheck Check(SensorSnapshot s)
    {
        var top = s.TopProcesses.OrderByDescending(p => p.CpuPercent).FirstOrDefault();
        if (top is null || top.CpuPercent <= 30) return RuleCheck.NotMet;
        return new RuleCheck(true,
            SubjectKey: $"{top.Name}:{top.ProcessId}",
            Title: $"{top.Name} sustained CPU",
            Detail: $"{top.CpuPercent:F0}% CPU",
            Metrics: new Dictionary<string, double?> { ["cpu_pct"] = top.CpuPercent });
    }
}
