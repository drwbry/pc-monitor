using PcMonitor.Core.Models;

namespace PcMonitor.Core.Issues.Rules;

public sealed class RunawayProcessRule : IIssueRule
{
    public string RuleId => "runaway-process";
    public IssueSeverity Severity => IssueSeverity.Red;
    public TimeSpan SustainedFor => TimeSpan.FromMinutes(5);

    public RuleCheck Check(SensorSnapshot s)
    {
        var top = s.TopProcesses.OrderByDescending(p => p.CpuPercent).FirstOrDefault();
        if (top is null || top.CpuPercent <= 50) return RuleCheck.NotMet;
        return new RuleCheck(true,
            SubjectKey: $"{top.Name}:{top.ProcessId}",
            Title: $"{top.Name} high CPU",
            Detail: $"{top.CpuPercent:F0}% CPU",
            Metrics: new Dictionary<string, double?> { ["cpu_pct"] = top.CpuPercent });
    }
}
