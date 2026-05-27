using PcMonitor.Core.Models;

namespace PcMonitor.Core.Capture;

public interface ICaptureService
{
    Task<CaptureResult> RunAsync(
        CaptureKind kind,
        Action<CaptureLine> onLine,
        CancellationToken ct);
}
