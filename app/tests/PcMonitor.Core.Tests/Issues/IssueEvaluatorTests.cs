using FluentAssertions;
using PcMonitor.Core.Issues;
using PcMonitor.Core.Models;
using Xunit;

namespace PcMonitor.Core.Tests.Issues;

public class IssueEvaluatorTests
{
    private static SensorSnapshot Snapshot(DateTimeOffset t) => new(
        t, 0, 0, false, 0, 0, 100, 0, 0, 0, 100, 0, 0, 0, Array.Empty<ProcessSample>());

    private sealed class TestRule : IIssueRule
    {
        public string RuleId { get; init; } = "test";
        public IssueSeverity Severity { get; init; } = IssueSeverity.Yellow;
        public TimeSpan SustainedFor { get; init; } = TimeSpan.Zero;
        public Func<SensorSnapshot, RuleCheck> CheckFn { get; init; } = _ => RuleCheck.NotMet;
        public RuleCheck Check(SensorSnapshot s) => CheckFn(s);
    }

    [Fact]
    public void NoRulesFiring_ReturnsEmpty()
    {
        var ev = new IssueEvaluator(new[] { new TestRule() });
        ev.Evaluate(Snapshot(DateTimeOffset.UnixEpoch)).Should().BeEmpty();
    }

    [Fact]
    public void ImmediateRule_FiresOnFirstMatch()
    {
        var rule = new TestRule
        {
            CheckFn = _ => new RuleCheck(true, Title: "t", Detail: "d"),
        };
        var ev = new IssueEvaluator(new[] { rule });
        var issues = ev.Evaluate(Snapshot(DateTimeOffset.UnixEpoch));
        issues.Should().ContainSingle().Which.Title.Should().Be("t");
    }

    [Fact]
    public void SustainedRule_DoesNotFireBeforeDurationElapses()
    {
        var rule = new TestRule
        {
            SustainedFor = TimeSpan.FromSeconds(30),
            CheckFn = _ => new RuleCheck(true, Title: "t"),
        };
        var ev = new IssueEvaluator(new[] { rule });
        var t0 = DateTimeOffset.UnixEpoch;
        ev.Evaluate(Snapshot(t0)).Should().BeEmpty();
        ev.Evaluate(Snapshot(t0.AddSeconds(29))).Should().BeEmpty();
    }

    [Fact]
    public void SustainedRule_FiresAtDurationBoundary()
    {
        var rule = new TestRule
        {
            SustainedFor = TimeSpan.FromSeconds(30),
            CheckFn = _ => new RuleCheck(true, Title: "t"),
        };
        var ev = new IssueEvaluator(new[] { rule });
        var t0 = DateTimeOffset.UnixEpoch;
        ev.Evaluate(Snapshot(t0));
        ev.Evaluate(Snapshot(t0.AddSeconds(30))).Should().ContainSingle();
    }

    [Fact]
    public void SustainedRule_PreservesFirstSeenAcrossTicks()
    {
        var rule = new TestRule
        {
            SustainedFor = TimeSpan.FromSeconds(30),
            CheckFn = _ => new RuleCheck(true, Title: "t"),
        };
        var ev = new IssueEvaluator(new[] { rule });
        var t0 = DateTimeOffset.UnixEpoch;
        ev.Evaluate(Snapshot(t0));
        var fired = ev.Evaluate(Snapshot(t0.AddSeconds(45))).Single();
        fired.FirstSeen.Should().Be(t0);
    }

    [Fact]
    public void ConditionBreaksThenReturns_ResetsFirstSeen()
    {
        var match = true;
        var rule = new TestRule
        {
            SustainedFor = TimeSpan.FromSeconds(30),
            CheckFn = _ => match ? new RuleCheck(true, Title: "t") : RuleCheck.NotMet,
        };
        var ev = new IssueEvaluator(new[] { rule });
        var t0 = DateTimeOffset.UnixEpoch;
        ev.Evaluate(Snapshot(t0));
        match = false;
        ev.Evaluate(Snapshot(t0.AddSeconds(10)));
        match = true;
        ev.Evaluate(Snapshot(t0.AddSeconds(20))).Should().BeEmpty();
        var fired = ev.Evaluate(Snapshot(t0.AddSeconds(50))).Single();
        fired.FirstSeen.Should().Be(t0.AddSeconds(20));
    }

    [Fact]
    public void DifferentSubjectKeys_TrackIndependently()
    {
        var subject = "A";
        var rule = new TestRule
        {
            SustainedFor = TimeSpan.FromSeconds(30),
            CheckFn = _ => new RuleCheck(true, SubjectKey: subject, Title: "t"),
        };
        var ev = new IssueEvaluator(new[] { rule });
        var t0 = DateTimeOffset.UnixEpoch;
        ev.Evaluate(Snapshot(t0));
        subject = "B";
        ev.Evaluate(Snapshot(t0.AddSeconds(45))).Should().BeEmpty();
        ev.Evaluate(Snapshot(t0.AddSeconds(80))).Should().ContainSingle().Which.FirstSeen
            .Should().Be(t0.AddSeconds(45));
    }
}
