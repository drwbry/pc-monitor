using PcMonitor.Core.Models;

namespace PcMonitor.Core.Issues.Rules;

public sealed class DriveCCriticalRule : IIssueRule
{
    public string RuleId => "drive-c-critical";
    public IssueSeverity Severity => IssueSeverity.Red;
    public TimeSpan SustainedFor => TimeSpan.Zero;

    public RuleCheck Check(SensorSnapshot s)
    {
        if (s.DriveCFreeGb is not double gb || gb >= 5) return RuleCheck.NotMet;
        return new RuleCheck(true,
            Title: "Drive C: critically full",
            Detail: $"{gb:F1} GB free.",
            Metrics: new Dictionary<string, double?> { ["free_gb"] = gb });
    }
}
