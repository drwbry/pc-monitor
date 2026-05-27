using System.Text;
using PcMonitor.Core.Models;

namespace PcMonitor.Core.Capture;

public sealed class CaptureService : ICaptureService
{
    private readonly IProcessRunner _runner;
    private readonly string _scriptsDir;
    private readonly string _logsDir;

    public CaptureService(IProcessRunner runner, string scriptsDir, string logsDir)
    {
        _runner = runner;
        _scriptsDir = scriptsDir;
        _logsDir = logsDir;
    }

    public async Task<CaptureResult> RunAsync(CaptureKind kind, Action<CaptureLine> onLine, CancellationToken ct)
    {
        var (scriptFile, filePrefix) = kind switch
        {
            CaptureKind.Diagnostic => ("diagnose.ps1", "diagnostic_"),
            CaptureKind.LiveProbe => ("live-probe.ps1", "live_"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        var scriptPath = Path.Combine(_scriptsDir, scriptFile);
        if (!File.Exists(scriptPath))
        {
            return new CaptureResult(kind, false, false, null, null, null,
                $"Script not found: {scriptPath}. Re-run install.ps1 or copy from repo files/.");
        }

        var stderr = new StringBuilder();
        var startedAt = DateTime.UtcNow.AddSeconds(-1);
        int exitCode;
        try
        {
            exitCode = await _runner.RunAsync(
                "powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                (text, isErr) =>
                {
                    if (isErr) stderr.AppendLine(text);
                    onLine(new CaptureLine(DateTimeOffset.UtcNow, isErr, text));
                },
                ct);
        }
        catch (OperationCanceledException)
        {
            return new CaptureResult(kind, false, true, null, null, null, null);
        }

        string? newest = null;
        try
        {
            newest = Directory.EnumerateFiles(_logsDir, filePrefix + "*.txt")
                .Select(p => new FileInfo(p))
                .Where(fi => fi.CreationTimeUtc >= startedAt)
                .OrderByDescending(fi => fi.CreationTimeUtc)
                .Select(fi => fi.FullName)
                .FirstOrDefault();
        }
        catch { }

        var ok = exitCode == 0 && newest is not null;
        return new CaptureResult(
            kind,
            Success: ok,
            Cancelled: false,
            ExitCode: exitCode,
            WindowsPath: newest,
            WslPath: WslPathConverter.ToWsl(newest),
            StdErr: stderr.Length == 0 ? null : stderr.ToString().TrimEnd());
    }
}
