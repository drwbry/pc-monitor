using PcMonitor.Core.Models;

namespace PcMonitor.Core.Issues.Rules;

public sealed class CommitNearExhaustionRule : IIssueRule
{
    public string RuleId => "commit-near-exhaustion";
    public IssueSeverity Severity => IssueSeverity.Red;
    public TimeSpan SustainedFor => TimeSpan.Zero;

    public RuleCheck Check(SensorSnapshot s)
    {
        if (s.CommitUsedPercent is not double c || c < 95) return RuleCheck.NotMet;
        return new RuleCheck(true,
            Title: "RAM commit near exhaustion",
            Detail: $"Committed {c:F0}% of limit — system is paging.",
            Metrics: new Dictionary<string, double?> { ["commit_pct"] = c });
    }
}
