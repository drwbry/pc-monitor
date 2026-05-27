using PcMonitor.Core.Models;

namespace PcMonitor.App.ViewModels;

public sealed class IssueCardViewModel
{
    public string RuleId { get; }
    public IssueSeverity Severity { get; }
    public string Title { get; }
    public string Detail { get; }
    public DateTimeOffset FirstSeen { get; }
    public string DurationText { get; }
    public bool IsRed => Severity == IssueSeverity.Red;
    public bool IsYellow => Severity == IssueSeverity.Yellow;

    public IssueCardViewModel(IssueState s, DateTimeOffset now)
    {
        RuleId = s.RuleId;
        Severity = s.Severity;
        Title = s.Title;
        Detail = s.Detail;
        FirstSeen = s.FirstSeen;
        DurationText = FormatDuration(now - s.FirstSeen);
    }

    private static string FormatDuration(TimeSpan d)
    {
        if (d.TotalSeconds < 60) return $"{(int)d.TotalSeconds}s";
        if (d.TotalMinutes < 60) return $"{(int)d.TotalMinutes} min";
        return $"{(int)d.TotalHours}h {d.Minutes}m";
    }
}
