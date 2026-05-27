using PcMonitor.Core.Models;

namespace PcMonitor.Core.Issues.Rules;

public sealed class CpuPackageTempElevatedRule : IIssueRule
{
    public string RuleId => "cpu-temp-elevated";
    public IssueSeverity Severity => IssueSeverity.Yellow;
    public TimeSpan SustainedFor => TimeSpan.FromMinutes(1);

    public RuleCheck Check(SensorSnapshot s)
    {
        if (s.CpuPackageTempC is not double t || t < 85) return RuleCheck.NotMet;
        return new RuleCheck(true,
            Title: "CPU package temperature elevated",
            Detail: $"{t:F0}°C",
            Metrics: new Dictionary<string, double?> { ["temp_c"] = t });
    }
}
