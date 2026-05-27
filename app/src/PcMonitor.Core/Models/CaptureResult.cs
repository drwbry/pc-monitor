namespace PcMonitor.Core.Models;

public enum CaptureKind
{
    Diagnostic,
    LiveProbe,
}

public sealed record CaptureResult(
    CaptureKind Kind,
    bool Success,
    bool Cancelled,
    int? ExitCode,
    string? WindowsPath,
    string? WslPath,
    string? StdErr);

public sealed record CaptureLine(
    DateTimeOffset Timestamp,
    bool IsStdErr,
    string Text);
