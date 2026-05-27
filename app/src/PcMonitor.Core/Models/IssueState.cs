namespace PcMonitor.Core.Models;

public enum IssueSeverity
{
    Yellow = 1,
    Red = 2,
}

public sealed record IssueState(
    string RuleId,
    IssueSeverity Severity,
    string Title,
    string Detail,
    DateTimeOffset FirstSeen,
    IReadOnlyDictionary<string, double?> Metrics);
