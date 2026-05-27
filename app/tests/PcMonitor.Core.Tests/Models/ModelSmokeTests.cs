using FluentAssertions;
using PcMonitor.Core.Models;
using Xunit;

namespace PcMonitor.Core.Tests.Models;

public class ModelSmokeTests
{
    [Fact]
    public void SensorSnapshot_RoundTripsViaWith()
    {
        var s = new SensorSnapshot(
            DateTimeOffset.UnixEpoch, 12.5, 70, false,
            10, 64, 84, 30, 5, 0.3, 400, 0, 1, 0.5,
            Array.Empty<ProcessSample>());
        (s with { CpuPercent = 99 }).CpuPercent.Should().Be(99);
    }

    [Fact]
    public void IssueSeverity_RedIsGreaterThanYellow()
    {
        ((int)IssueSeverity.Red).Should().BeGreaterThan((int)IssueSeverity.Yellow);
    }
}
