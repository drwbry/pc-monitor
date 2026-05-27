namespace PcMonitor.Core.Capture;

public interface IProcessRunner
{
    Task<int> RunAsync(
        string fileName,
        string arguments,
        Action<string, bool> onLine,
        CancellationToken ct);
}
