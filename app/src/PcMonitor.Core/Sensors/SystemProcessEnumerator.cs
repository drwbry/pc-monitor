using System.Diagnostics;
using System.Management;

namespace PcMonitor.Core.Sensors;

public sealed class SystemProcessEnumerator : IProcessEnumerator
{
    private Dictionary<int, long> _privateWsCache = new();
    private DateTimeOffset _lastWsRefresh = DateTimeOffset.MinValue;
    private static readonly TimeSpan WsRefreshInterval = TimeSpan.FromSeconds(5);

    public IReadOnlyList<RawProcess> Enumerate()
    {
        RefreshPrivateWorkingSetIfDue();

        var procs = Process.GetProcesses();
        var list = new List<RawProcess>(procs.Length);
        foreach (var p in procs)
        {
            try
            {
                TimeSpan cpu;
                try { cpu = p.TotalProcessorTime; }
                catch { cpu = TimeSpan.Zero; }

                var ram = _privateWsCache.TryGetValue(p.Id, out var ws) ? ws : p.WorkingSet64;
                list.Add(new RawProcess(p.Id, p.ProcessName, cpu, ram));
            }
            catch
            {
                // process exited or access denied; skip
            }
            finally
            {
                p.Dispose();
            }
        }
        return list;
    }

    private void RefreshPrivateWorkingSetIfDue()
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastWsRefresh < WsRefreshInterval) return;
        try
        {
            var cache = new Dictionary<int, long>();
            using var searcher = new ManagementObjectSearcher(
                "SELECT IDProcess, WorkingSetPrivate FROM Win32_PerfFormattedData_PerfProc_Process");
            foreach (ManagementObject m in searcher.Get())
            {
                var pid = Convert.ToInt32(m["IDProcess"]);
                var bytes = Convert.ToInt64(m["WorkingSetPrivate"]);
                cache[pid] = bytes;
            }
            _privateWsCache = cache;
            _lastWsRefresh = now;
        }
        catch { }
    }
}
