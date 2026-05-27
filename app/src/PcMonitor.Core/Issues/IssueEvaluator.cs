using PcMonitor.Core.Models;

namespace PcMonitor.Core.Issues;

public sealed class IssueEvaluator
{
    private readonly IReadOnlyList<IIssueRule> _rules;
    private readonly Dictionary<(string RuleId, string? Subject), DateTimeOffset> _activeSince = new();

    public IssueEvaluator(IEnumerable<IIssueRule> rules)
    {
        _rules = rules.ToList();
    }

    public IReadOnlyList<IssueState> Evaluate(SensorSnapshot snapshot)
    {
        var stillActive = new HashSet<(string, string?)>();
        var emitted = new List<IssueState>();

        foreach (var rule in _rules)
        {
            var check = rule.Check(snapshot);
            if (!check.ConditionMet) continue;

            var key = (rule.RuleId, check.SubjectKey);
            if (!_activeSince.TryGetValue(key, out var firstSeen))
            {
                firstSeen = snapshot.Timestamp;
                _activeSince[key] = firstSeen;
            }
            stillActive.Add(key);

            if (snapshot.Timestamp - firstSeen >= rule.SustainedFor)
            {
                emitted.Add(new IssueState(
                    rule.RuleId,
                    rule.Severity,
                    check.Title ?? "",
                    check.Detail ?? "",
                    firstSeen,
                    check.Metrics ?? new Dictionary<string, double?>()));
            }
        }

        foreach (var stale in _activeSince.Keys.Where(k => !stillActive.Contains(k)).ToList())
        {
            _activeSince.Remove(stale);
        }

        return emitted
            .OrderByDescending(i => i.Severity)
            .ThenBy(i => i.FirstSeen)
            .ToList();
    }
}
