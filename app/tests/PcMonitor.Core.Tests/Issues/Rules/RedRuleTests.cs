using FluentAssertions;
using PcMonitor.Core.Issues.Rules;
using PcMonitor.Core.Models;
using Xunit;

namespace PcMonitor.Core.Tests.Issues.Rules;

public class RedRuleTests
{
    [Fact]
    public void ThermalThrottle_FiresWhenIsThrottlingTrue()
    {
        new ThermalThrottleRule().Check(SnapshotBuilder.Default(throttling: true, tempC: 97))
            .ConditionMet.Should().BeTrue();
    }

    [Fact]
    public void ThermalThrottle_DoesNotFireWhenFalseOrNull()
    {
        new ThermalThrottleRule().Check(SnapshotBuilder.Default(throttling: false)).ConditionMet.Should().BeFalse();
        new ThermalThrottleRule().Check(SnapshotBuilder.Default(throttling: null)).ConditionMet.Should().BeFalse();
    }

    [Theory]
    [InlineData(94.9, false)]
    [InlineData(95.0, true)]
    [InlineData(99.0, true)]
    public void CpuTempCritical_BoundaryAt95(double temp, bool met)
    {
        new CpuPackageTempHighRule().Check(SnapshotBuilder.Default(tempC: temp))
            .ConditionMet.Should().Be(met);
    }

    [Theory]
    [InlineData(94.9, false)]
    [InlineData(95.0, true)]
    public void CommitNearExhaustion_BoundaryAt95(double commit, bool met)
    {
        new CommitNearExhaustionRule().Check(SnapshotBuilder.Default(commitPct: commit))
            .ConditionMet.Should().Be(met);
    }

    [Theory]
    [InlineData(5.0, false)]
    [InlineData(4.9, true)]
    [InlineData(0.0, true)]
    public void DriveCCritical_BoundaryBelow5Gb(double free, bool met)
    {
        new DriveCCriticalRule().Check(SnapshotBuilder.Default(driveCFree: free))
            .ConditionMet.Should().Be(met);
    }

    [Fact]
    public void RunawayProcess_FiresWhenAnyProcessAbove50Percent()
    {
        var procs = new[] { new ProcessSample(123, "chrome.exe", 60, 1000) };
        var result = new RunawayProcessRule().Check(SnapshotBuilder.Default(procs: procs));
        result.ConditionMet.Should().BeTrue();
        result.SubjectKey.Should().Be("chrome.exe:123");
    }

    [Fact]
    public void RunawayProcess_DoesNotFireAt50OrBelow()
    {
        var procs = new[] { new ProcessSample(1, "p", 50, 1) };
        new RunawayProcessRule().Check(SnapshotBuilder.Default(procs: procs))
            .ConditionMet.Should().BeFalse();
    }

    [Theory]
    [InlineData(9, false)]
    [InlineData(10, true)]
    public void EventLogSpike_BoundaryAt10(int count, bool met)
    {
        new EventLogSpikeRule().Check(SnapshotBuilder.Default(errLast5: count))
            .ConditionMet.Should().Be(met);
    }
}
