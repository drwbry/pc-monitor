using PcMonitor.Core.Models;

namespace PcMonitor.Core.Issues.Rules;

public sealed class ThermalThrottleRule : IIssueRule
{
    public string RuleId => "thermal-throttle-active";
    public IssueSeverity Severity => IssueSeverity.Red;
    public TimeSpan SustainedFor => TimeSpan.Zero;

    public RuleCheck Check(SensorSnapshot s)
    {
        if (s.IsThrottling != true) return RuleCheck.NotMet;
        var detail = s.CpuPackageTempC.HasValue
            ? $"CPU package at {s.CpuPackageTempC:F0}°C; PROCHOT detected."
            : "PROCHOT detected.";
        return new RuleCheck(true,
            Title: "Thermal throttle active",
            Detail: detail,
            Metrics: new Dictionary<string, double?> { ["temp_c"] = s.CpuPackageTempC });
    }
}
