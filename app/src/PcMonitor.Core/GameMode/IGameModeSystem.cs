namespace PcMonitor.Core.GameMode;

/// <summary>The side-effecting operations Game Mode needs, so planning and ordering can be tested.</summary>
public interface IGameModeSystem
{
    IReadOnlyList<ProcessInfo> GetProcesses();
    bool IsServiceRunning(string serviceName);
    void StopService(string serviceName, TimeSpan timeout);
    void StartService(string serviceName, TimeSpan timeout);
    void KillProcessTree(int pid);
    Task<int> RunHiddenAsync(string fileName, string arguments, TimeSpan timeout);
    /// <summary>Launch as the normal (non-elevated) desktop user, even when PcMonitor is elevated.</summary>
    void LaunchUnelevated(string id, LaunchSpec spec);
    Task DelayAsync(TimeSpan delay);
}
