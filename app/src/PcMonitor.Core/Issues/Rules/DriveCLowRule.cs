using PcMonitor.Core.Models;

namespace PcMonitor.Core.Issues.Rules;

public sealed class DriveCLowRule : IIssueRule
{
    public string RuleId => "drive-c-low";
    public IssueSeverity Severity => IssueSeverity.Yellow;
    public TimeSpan SustainedFor => TimeSpan.Zero;

    public RuleCheck Check(SensorSnapshot s)
    {
        if (s.DriveCFreeGb is not double gb || gb >= 20) return RuleCheck.NotMet;
        return new RuleCheck(true,
            Title: "Drive C: getting full",
            Detail: $"{gb:F0} GB free.",
            Metrics: new Dictionary<string, double?> { ["free_gb"] = gb });
    }
}
