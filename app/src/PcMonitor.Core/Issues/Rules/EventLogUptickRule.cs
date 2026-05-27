using PcMonitor.Core.Models;

namespace PcMonitor.Core.Issues.Rules;

public sealed class EventLogUptickRule : IIssueRule
{
    public string RuleId => "event-log-uptick";
    public IssueSeverity Severity => IssueSeverity.Yellow;
    public TimeSpan SustainedFor => TimeSpan.Zero;

    public RuleCheck Check(SensorSnapshot s)
    {
        if (s.EventErrors24hHourlyAverage is not double avg || avg <= 0) return RuleCheck.NotMet;
        if (s.EventErrorsThisHour is not int now) return RuleCheck.NotMet;
        if (now < 2 * avg) return RuleCheck.NotMet;
        return new RuleCheck(true,
            Title: "Event log uptick",
            Detail: $"{now} errors this hour vs {avg:F1} avg.",
            Metrics: new Dictionary<string, double?>
            {
                ["errors_this_hour"] = now,
                ["avg_24h"] = avg,
            });
    }
}
