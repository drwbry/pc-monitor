namespace PcMonitor.Core.Issues;

public sealed record RuleCheck(
    bool ConditionMet,
    string? SubjectKey = null,
    string? Title = null,
    string? Detail = null,
    IReadOnlyDictionary<string, double?>? Metrics = null)
{
    public static RuleCheck NotMet { get; } = new(false);
}
