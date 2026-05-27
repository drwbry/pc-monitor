using PcMonitor.Core.Models;

namespace PcMonitor.Core.Issues.Rules;

public sealed class PagefilePressureRule : IIssueRule
{
    public string RuleId => "pagefile-pressure";
    public IssueSeverity Severity => IssueSeverity.Yellow;
    public TimeSpan SustainedFor => TimeSpan.Zero;

    public RuleCheck Check(SensorSnapshot s)
    {
        if (s.PagefileUsedPercent is not double p || p <= 50) return RuleCheck.NotMet;
        return new RuleCheck(true,
            Title: "Pagefile pressure",
            Detail: $"Pagefile at {p:F0}% of allocated.",
            Metrics: new Dictionary<string, double?> { ["pagefile_pct"] = p });
    }
}
