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
