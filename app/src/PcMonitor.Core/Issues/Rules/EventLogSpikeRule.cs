using PcMonitor.Core.Models;

namespace PcMonitor.Core.Issues.Rules;

public sealed class EventLogSpikeRule : IIssueRule
{
    public string RuleId => "event-log-spike";
    public IssueSeverity Severity => IssueSeverity.Red;
    public TimeSpan SustainedFor => TimeSpan.Zero;

    public RuleCheck Check(SensorSnapshot s)
    {
        if (s.EventErrorsLast5Minutes is not int c || c < 10) return RuleCheck.NotMet;
        return new RuleCheck(true,
            Title: "Event log error spike",
            Detail: $"{c} errors in the last 5 minutes.",
            Metrics: new Dictionary<string, double?> { ["errors_5m"] = c });
    }
}
