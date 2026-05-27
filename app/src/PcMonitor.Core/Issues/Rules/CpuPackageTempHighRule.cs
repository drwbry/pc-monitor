using PcMonitor.Core.Models;

namespace PcMonitor.Core.Issues.Rules;

public sealed class CpuPackageTempHighRule : IIssueRule
{
    public string RuleId => "cpu-temp-critical";
    public IssueSeverity Severity => IssueSeverity.Red;
    public TimeSpan SustainedFor => TimeSpan.FromSeconds(30);

    public RuleCheck Check(SensorSnapshot s)
    {
        if (s.CpuPackageTempC is not double t || t < 95) return RuleCheck.NotMet;
        return new RuleCheck(true,
            Title: "CPU package temperature critical",
            Detail: $"{t:F0}°C",
            Metrics: new Dictionary<string, double?> { ["temp_c"] = t });
    }
}
