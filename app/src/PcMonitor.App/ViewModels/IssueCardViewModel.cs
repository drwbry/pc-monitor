using CommunityToolkit.Mvvm.ComponentModel;
using PcMonitor.Core.Models;

namespace PcMonitor.App.ViewModels;

/// <summary>Observable so the cockpit can refresh durations in place instead of rebuilding cards each tick.</summary>
public sealed partial class IssueCardViewModel : ObservableObject
{
    public string RuleId { get; }
    public IssueSeverity Severity { get; }
    public string Title { get; }
    public DateTimeOffset FirstSeen { get; }
    [ObservableProperty] private string _detail;
    [ObservableProperty] private string _durationText;
    public bool IsRed => Severity == IssueSeverity.Red;
    public bool IsYellow => Severity == IssueSeverity.Yellow;

    public IssueCardViewModel(IssueState s, DateTimeOffset now)
    {
        RuleId = s.RuleId;
        Severity = s.Severity;
        Title = s.Title;
        FirstSeen = s.FirstSeen;
        _detail = s.Detail;
        _durationText = FormatDuration(now - s.FirstSeen);
    }

    /// <summary>True when this card can show the given state without being recreated.</summary>
    public bool Matches(IssueState s) => s.RuleId == RuleId && s.Severity == Severity && s.Title == Title;

    public void Update(IssueState s, DateTimeOffset now)
    {
        Detail = s.Detail;
        DurationText = FormatDuration(now - s.FirstSeen);
    }

    private static string FormatDuration(TimeSpan d)
    {
        if (d.TotalSeconds < 60) return $"{(int)d.TotalSeconds}s";
        if (d.TotalMinutes < 60) return $"{(int)d.TotalMinutes} min";
        return $"{(int)d.TotalHours}h {d.Minutes}m";
    }
}
