using FluentAssertions;
using PcMonitor.Core.Issues.Rules;
using PcMonitor.Core.Models;
using Xunit;

namespace PcMonitor.Core.Tests.Issues.Rules;

public class YellowRuleTests
{
    [Theory]
    [InlineData(84.9, false)]
    [InlineData(85.0, true)]
    public void TempElevated_BoundaryAt85(double t, bool met) =>
        new CpuPackageTempElevatedRule().Check(SnapshotBuilder.Default(tempC: t)).ConditionMet.Should().Be(met);

    [Theory]
    [InlineData(15.0, false)]
    [InlineData(14.9, true)]
    public void RamPressure_BoundaryAt15Percent(double free, bool met) =>
        new RamPressureRule().Check(SnapshotBuilder.Default(freePhysRamPct: free)).ConditionMet.Should().Be(met);

    [Fact]
    public void SustainedCpuHog_FiresAbove30()
    {
        var procs = new[] { new ProcessSample(1, "p", 31, 0) };
        new SustainedCpuHogRule().Check(SnapshotBuilder.Default(procs: procs)).ConditionMet.Should().BeTrue();
    }

    [Fact]
    public void MemoryHog_FiresAbove4Gb()
    {
        var procs = new[] { new ProcessSample(1, "p", 0, 4097) };
        new MemoryHogRule().Check(SnapshotBuilder.Default(procs: procs)).ConditionMet.Should().BeTrue();
    }

    [Theory]
    [InlineData(20.0, false)]
    [InlineData(19.9, true)]
    public void DriveCLow_BoundaryBelow20Gb(double gb, bool met) =>
        new DriveCLowRule().Check(SnapshotBuilder.Default(driveCFree: gb)).ConditionMet.Should().Be(met);

    [Theory]
    [InlineData(50.0, false)]
    [InlineData(50.1, true)]
    public void PagefilePressure_BoundaryAt50(double p, bool met) =>
        new PagefilePressureRule().Check(SnapshotBuilder.Default(pagefilePct: p)).ConditionMet.Should().Be(met);

    [Theory]
    [InlineData(11, 6.0, false)]  // 11 < 2*6=12 → not met
    [InlineData(12, 6.0, true)]   // 12 == 2*6=12 → met
    [InlineData(8, 5.0, false)]   // 8 < 2*5=10 → not met
    [InlineData(10, 5.0, true)]   // 10 == 2*5=10 → met
    public void EventLogUptick_DoubleBaseline(int now, double avg, bool met) =>
        new EventLogUptickRule().Check(SnapshotBuilder.Default(errThisHour: now, errAvg24h: avg))
            .ConditionMet.Should().Be(met);

    [Fact]
    public void EventLogUptick_NullAverageDoesNotFire() =>
        new EventLogUptickRule().Check(SnapshotBuilder.Default(errThisHour: 100, errAvg24h: null))
            .ConditionMet.Should().BeFalse();

    [Theory]
    [InlineData(4.0, false)]
    [InlineData(4.1, true)]
    public void DiskQueueElevated_BoundaryAbove4(double q, bool met) =>
        new DiskQueueElevatedRule().Check(SnapshotBuilder.Default(diskQ: q)).ConditionMet.Should().Be(met);
}
