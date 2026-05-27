using FluentAssertions;
using PcMonitor.Core.Capture;
using PcMonitor.Core.Models;
using Xunit;

namespace PcMonitor.Core.Tests.Capture;

public class CaptureServiceTests : IDisposable
{
    private readonly string _scriptsDir = Path.Combine(Path.GetTempPath(), "pcmon-scripts-" + Guid.NewGuid());
    private readonly string _logsDir = Path.Combine(Path.GetTempPath(), "pcmon-logs-" + Guid.NewGuid());

    public CaptureServiceTests()
    {
        Directory.CreateDirectory(_scriptsDir);
        Directory.CreateDirectory(_logsDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_scriptsDir, recursive: true); } catch { }
        try { Directory.Delete(_logsDir, recursive: true); } catch { }
    }

    private sealed class FakeRunner : IProcessRunner
    {
        public int ExitCode { get; init; }
        public Action<Action<string, bool>>? Emit { get; init; }
        public Task<int> RunAsync(string fileName, string arguments, Action<string, bool> onLine, CancellationToken ct)
        {
            Emit?.Invoke(onLine);
            return Task.FromResult(ExitCode);
        }
    }

    [Fact]
    public async Task RunAsync_MissingScript_ReturnsFailure()
    {
        var svc = new CaptureService(new FakeRunner(), _scriptsDir, _logsDir);
        var result = await svc.RunAsync(CaptureKind.Diagnostic, _ => { }, CancellationToken.None);
        result.Success.Should().BeFalse();
        result.StdErr.Should().Contain("not found");
    }

    [Fact]
    public async Task RunAsync_SuccessWithMatchingNewFile_ReturnsPath()
    {
        File.WriteAllText(Path.Combine(_scriptsDir, "diagnose.ps1"), "# stub");
        var newFile = Path.Combine(_logsDir, "diagnostic_2026-05-26_14-32.txt");
        var runner = new FakeRunner
        {
            ExitCode = 0,
            Emit = onLine =>
            {
                File.WriteAllText(newFile, "stub output");
                onLine("done", false);
            },
        };
        var svc = new CaptureService(runner, _scriptsDir, _logsDir);
        var result = await svc.RunAsync(CaptureKind.Diagnostic, _ => { }, CancellationToken.None);
        result.Success.Should().BeTrue();
        result.WindowsPath.Should().Be(newFile);
        result.WslPath.Should().Be(WslPathConverter.ToWsl(newFile));
    }

    [Fact]
    public async Task RunAsync_NonZeroExit_ReturnsFailureWithStderr()
    {
        File.WriteAllText(Path.Combine(_scriptsDir, "diagnose.ps1"), "# stub");
        var runner = new FakeRunner
        {
            ExitCode = 1,
            Emit = onLine => onLine("boom", true),
        };
        var svc = new CaptureService(runner, _scriptsDir, _logsDir);
        var result = await svc.RunAsync(CaptureKind.Diagnostic, _ => { }, CancellationToken.None);
        result.Success.Should().BeFalse();
        result.ExitCode.Should().Be(1);
        result.StdErr.Should().Contain("boom");
    }
}
