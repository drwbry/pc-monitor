using FluentAssertions;
using PcMonitor.Core.History;
using Xunit;

namespace PcMonitor.Core.Tests.History;

public class HourlyHistoryReaderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pcmon-tests-" + Guid.NewGuid());

    public HourlyHistoryReaderTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static string MakeJson(string isoTimestamp, double cpuPct) =>
        $$"""
        {
          "timestamp": "{{isoTimestamp}}",
          "cpu_load_pct": {{cpuPct}},
          "ram": { "total_gb": 64.0, "free_gb": 50.0 },
          "disks": [{ "drive": "C", "free_gb": 400.0 }],
          "system_errors_last_hour": 0,
          "app_errors_last_hour": 0
        }
        """;

    private static string MakeJsonWithErrors(string isoTimestamp, int sysErr, int appErr) =>
        $$"""
        {
          "timestamp": "{{isoTimestamp}}",
          "system_errors_last_hour": {{sysErr}},
          "app_errors_last_hour": {{appErr}}
        }
        """;

    [Fact]
    public void ReadAll_EmptyFolder_ReturnsEmpty()
    {
        var reader = new HourlyHistoryReader(_dir);
        reader.ReadAll().Should().BeEmpty();
    }

    [Fact]
    public void ReadAll_ReadsAllJsonFiles_SortedByTimestamp()
    {
        File.WriteAllText(Path.Combine(_dir, "stats_2026-05-26_14-00.json"),
            MakeJson("2026-05-26T14:00:00-04:00", 10));
        File.WriteAllText(Path.Combine(_dir, "stats_2026-05-26_13-00.json"),
            MakeJson("2026-05-26T13:00:00-04:00", 20));
        var reader = new HourlyHistoryReader(_dir);
        var entries = reader.ReadAll();
        entries.Should().HaveCount(2);
        // Sorted descending by timestamp — most recent first
        entries[0].CpuPercent.Should().Be(10);
        entries[1].CpuPercent.Should().Be(20);
    }

    [Fact]
    public void ReadAll_SkipsMalformedFiles()
    {
        File.WriteAllText(Path.Combine(_dir, "stats_2026-05-26_14-00.json"),
            MakeJson("2026-05-26T14:00:00-04:00", 10));
        File.WriteAllText(Path.Combine(_dir, "stats_2026-05-26_15-00.json"), "{ not valid");
        var reader = new HourlyHistoryReader(_dir);
        reader.ReadAll().Should().HaveCount(1);
    }

    [Fact]
    public void AverageHourlyErrorCount_FewerThanThreshold_ReturnsNull()
    {
        File.WriteAllText(Path.Combine(_dir, "stats_a.json"),
            MakeJsonWithErrors("2026-05-26T14:00:00-04:00", 5, 3));
        var reader = new HourlyHistoryReader(_dir);
        reader.AverageHourlyErrorCount(hoursBack: 24).Should().BeNull();
    }

    [Fact]
    public void AverageHourlyErrorCount_AveragesErrorTotals()
    {
        for (var i = 0; i < 6; i++)
            File.WriteAllText(Path.Combine(_dir, $"stats_{i}.json"),
                MakeJsonWithErrors($"2026-05-26T1{i}:00:00-04:00", 2, 1));
        var reader = new HourlyHistoryReader(_dir);
        reader.AverageHourlyErrorCount(hoursBack: 24).Should().Be(3.0);
    }
}
