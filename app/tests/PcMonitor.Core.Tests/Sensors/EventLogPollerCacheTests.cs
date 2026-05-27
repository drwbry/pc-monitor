using FluentAssertions;
using PcMonitor.Core.Sensors;
using Xunit;

namespace PcMonitor.Core.Tests.Sensors;

public class EventLogPollerCacheTests
{
    [Fact]
    public void RefreshIfDue_OnlyCallsBackendOncePerCacheWindow()
    {
        var calls = 0;
        var poller = new EventLogPoller(
            queryFn: _ => { calls++; return (2, 7); },
            refreshInterval: TimeSpan.FromSeconds(60));
        var t = DateTimeOffset.UnixEpoch;
        poller.RefreshIfDue(t);
        poller.RefreshIfDue(t.AddSeconds(30));
        poller.RefreshIfDue(t.AddSeconds(59));
        calls.Should().Be(1);
        poller.RefreshIfDue(t.AddSeconds(60));
        calls.Should().Be(2);
    }

    [Fact]
    public void Counts_ExposedFromLastQuery()
    {
        var poller = new EventLogPoller(
            queryFn: _ => (3, 11),
            refreshInterval: TimeSpan.FromSeconds(60));
        poller.RefreshIfDue(DateTimeOffset.UnixEpoch);
        poller.Last5MinutesErrors.Should().Be(3);
        poller.LastHourErrors.Should().Be(11);
    }
}
