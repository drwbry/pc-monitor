using PcMonitor.Core.Models;

namespace PcMonitor.Core.Issues.Rules;

public sealed class DiskQueueElevatedRule : IIssueRule
{
    public string RuleId => "disk-queue-elevated";
    public IssueSeverity Severity => IssueSeverity.Yellow;
    public TimeSpan SustainedFor => TimeSpan.FromSeconds(60);

    public RuleCheck Check(SensorSnapshot s)
    {
        if (s.DiskQueueLength is not double q || q <= 4) return RuleCheck.NotMet;
        return new RuleCheck(true,
            Title: "Disk queue elevated",
            Detail: $"Queue length {q:F1}.",
            Metrics: new Dictionary<string, double?> { ["queue"] = q });
    }
}
