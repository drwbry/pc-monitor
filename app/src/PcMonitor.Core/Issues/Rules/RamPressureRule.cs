using PcMonitor.Core.Models;

namespace PcMonitor.Core.Issues.Rules;

public sealed class RamPressureRule : IIssueRule
{
    public string RuleId => "ram-pressure";
    public IssueSeverity Severity => IssueSeverity.Yellow;
    public TimeSpan SustainedFor => TimeSpan.Zero;

    public RuleCheck Check(SensorSnapshot s)
    {
        if (s.FreePhysicalRamPercent >= 15) return RuleCheck.NotMet;
        return new RuleCheck(true,
            Title: "RAM pressure",
            Detail: $"Free physical RAM at {s.FreePhysicalRamPercent:F0}%.",
            Metrics: new Dictionary<string, double?> { ["free_pct"] = s.FreePhysicalRamPercent });
    }
}
