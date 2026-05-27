using PcMonitor.Core.Models;

namespace PcMonitor.Core.Issues;

public interface IIssueRule
{
    string RuleId { get; }
    IssueSeverity Severity { get; }
    TimeSpan SustainedFor { get; }
    RuleCheck Check(SensorSnapshot snapshot);
}
