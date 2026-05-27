using System.Diagnostics;

namespace PcMonitor.Core.Capture;

public sealed class PowerShellProcessRunner : IProcessRunner
{
    public async Task<int> RunAsync(
        string fileName,
        string arguments,
        Action<string, bool> onLine,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        proc.OutputDataReceived += (_, e) => { if (e.Data is not null) onLine(e.Data, false); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) onLine(e.Data, true); };
        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        using var reg = ct.Register(() =>
        {
            try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { }
        });

        await proc.WaitForExitAsync(CancellationToken.None);
        return proc.ExitCode;
    }
}
