using FluentAssertions;
using PcMonitor.Core.History;
using PcMonitor.Core.Models;
using Xunit;

namespace PcMonitor.Core.Tests.History;

public class HourlyJsonParserTests
{
    [Fact]
    public void Parse_ValidPayload_ReturnsEntry()
    {
        var json = """
        {
          "Timestamp": "2026-05-26T14:00:00-04:00",
          "CpuPercent": 12.5,
          "RamUsedGb": 14.2,
          "RamTotalGb": 64.0,
          "DriveCFreeGb": 412.0,
          "SystemErrorsLastHour": 0,
          "AppErrorsLastHour": 1
        }
        """;
        var entry = HourlyJsonParser.Parse(json);
        entry.Should().NotBeNull();
        entry!.CpuPercent.Should().Be(12.5);
        entry.RamTotalGb.Should().Be(64.0);
    }

    [Fact]
    public void Parse_MissingFields_PopulatesNulls()
    {
        var json = """{ "Timestamp": "2026-05-26T14:00:00-04:00" }""";
        var entry = HourlyJsonParser.Parse(json);
        entry.Should().NotBeNull();
        entry!.CpuPercent.Should().BeNull();
    }

    [Fact]
    public void Parse_Malformed_ReturnsNull()
    {
        HourlyJsonParser.Parse("{ not valid").Should().BeNull();
        HourlyJsonParser.Parse("").Should().BeNull();
    }
}
