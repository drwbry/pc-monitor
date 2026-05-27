using FluentAssertions;
using PcMonitor.Core.Capture;
using Xunit;

namespace PcMonitor.Core.Tests.Capture;

public class WslPathConverterTests
{
    [Theory]
    [InlineData(@"C:\Users\dreux\Documents\SysLogs\diagnostic_x.txt",
                "/mnt/c/Users/dreux/Documents/SysLogs/diagnostic_x.txt")]
    [InlineData(@"D:\stuff\file.txt", "/mnt/d/stuff/file.txt")]
    [InlineData(@"c:\Users\My Name\file.txt", "/mnt/c/Users/My Name/file.txt")]
    public void ToWsl_ConvertsDriveLetterAndSlashes(string windowsPath, string expected)
    {
        WslPathConverter.ToWsl(windowsPath).Should().Be(expected);
    }

    [Fact]
    public void ToWsl_NullOrEmpty_ReturnsNull()
    {
        WslPathConverter.ToWsl(null).Should().BeNull();
        WslPathConverter.ToWsl("").Should().BeNull();
    }

    [Fact]
    public void ToWsl_PathWithoutDriveLetter_ReturnsNull()
    {
        WslPathConverter.ToWsl(@"\\server\share\file.txt").Should().BeNull();
    }
}
