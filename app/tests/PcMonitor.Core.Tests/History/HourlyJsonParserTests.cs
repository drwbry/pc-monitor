using FluentAssertions;
using PcMonitor.Core.History;
using Xunit;

namespace PcMonitor.Core.Tests.History;

public class HourlyJsonParserTests
{
    private const string ValidJson = """
        {
          "schema_version": 2,
          "timestamp": "2026-05-26T14:00:00-04:00",
          "cpu_load_pct": 12.5,
          "ram": { "total_gb": 64.0, "free_gb": 49.8, "used_pct": 22.2 },
          "disks": [
            { "drive": "C", "used_gb": 100.0, "free_gb": 412.0, "used_pct": 20.0 }
          ],
          "system_errors_last_hour": 0,
          "app_errors_last_hour": 1
        }
        """;

    private const string V3Json = """
        {
          "schema_version": 3,
          "timestamp": "2026-07-15T21:00:00-04:00",
          "cpu_load_pct": 13.0,
          "cpu_queue_length": 28,
          "cpu_perf": { "proc_performance_pct_avg": 66.0, "proc_performance_pct_max": 68.0, "frequency_mhz": 1594.0 },
          "ram": { "total_gb": 31.71, "free_gb": 9.35, "used_pct": 70.5 }
        }
        """;

    [Fact]
    public void Parse_V3WithCpuPerf_PopulatesThrottleFields()
    {
        var entry = HourlyJsonParser.Parse(V3Json);
        entry.Should().NotBeNull();
        entry!.CpuProcPerfPctAvg.Should().Be(66.0);
        entry.CpuProcPerfPctMax.Should().Be(68.0);
        entry.CpuFrequencyMhz.Should().Be(1594.0);
    }

    [Fact]
    public void Parse_V2WithoutCpuPerf_ThrottleFieldsNull()
    {
        var entry = HourlyJsonParser.Parse(ValidJson);
        entry.Should().NotBeNull();
        entry!.CpuProcPerfPctAvg.Should().BeNull();
        entry.CpuProcPerfPctMax.Should().BeNull();
        entry.CpuFrequencyMhz.Should().BeNull();
    }

    [Fact]
    public void Parse_ValidPayload_ReturnsEntry()
    {
        var entry = HourlyJsonParser.Parse(ValidJson);
        entry.Should().NotBeNull();
        entry!.CpuPercent.Should().Be(12.5);
        entry.RamTotalGb.Should().Be(64.0);
        entry.RamUsedGb.Should().BeApproximately(14.2, 0.1);
        entry.DriveCFreeGb.Should().Be(412.0);
        entry.SystemErrorsLastHour.Should().Be(0);
        entry.AppErrorsLastHour.Should().Be(1);
    }

    [Fact]
    public void Parse_MissingFields_PopulatesNulls()
    {
        var json = """{ "timestamp": "2026-05-26T14:00:00-04:00" }""";
        var entry = HourlyJsonParser.Parse(json);
        entry.Should().NotBeNull();
        entry!.CpuPercent.Should().BeNull();
        entry.RamTotalGb.Should().BeNull();
        entry.DriveCFreeGb.Should().BeNull();
    }

    [Fact]
    public void Parse_Malformed_ReturnsNull()
    {
        HourlyJsonParser.Parse("{ not valid").Should().BeNull();
        HourlyJsonParser.Parse("").Should().BeNull();
    }

    [Fact]
    public void Parse_NoTimestamp_ReturnsNull()
    {
        HourlyJsonParser.Parse("""{ "cpu_load_pct": 5 }""").Should().BeNull();
    }
}
