using System.Diagnostics;
using System.Management;
using LibreHardwareMonitor.Hardware;
using PcMonitor.Core.History;
using PcMonitor.Core.Models;

namespace PcMonitor.Core.Sensors;

public sealed class SensorService : ISensorService
{
    private readonly Computer? _computer;
    private readonly ProcessSampler _processes;
    private readonly EventLogPoller _events;
    private readonly IHistoryReader _history;
    private readonly PerformanceCounter? _cpuTotal;
    private readonly PerformanceCounter? _diskQueue;

    public bool TempSensorsAvailable { get; }

    public SensorService(IHistoryReader history)
    {
        _history = history;
        try
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsMemoryEnabled = true,
                IsStorageEnabled = true,
            };
            _computer.Open();
            TempSensorsAvailable = true;
        }
        catch
        {
            _computer = null;
            TempSensorsAvailable = false;
        }

        try { _cpuTotal = new PerformanceCounter("Processor", "% Processor Time", "_Total"); _cpuTotal.NextValue(); } catch { _cpuTotal = null; }
        try { _diskQueue = new PerformanceCounter("PhysicalDisk", "Current Disk Queue Length", "_Total"); _diskQueue.NextValue(); } catch { _diskQueue = null; }

        _processes = new ProcessSampler(new SystemProcessEnumerator(), Environment.ProcessorCount);
        _events = new EventLogPoller(EventLogPoller.QueryWindowsEventLog, TimeSpan.FromSeconds(60));
    }

    public SensorSnapshot Read(DateTimeOffset now)
    {
        double? cpu = null;
        try { cpu = _cpuTotal?.NextValue(); } catch { }

        double? tempC = null;
        bool? throttling = null;
        try
        {
            if (_computer is not null)
            {
                foreach (var hw in _computer.Hardware)
                {
                    if (hw.HardwareType != HardwareType.Cpu) continue;
                    hw.Update();
                    foreach (var sensor in hw.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature &&
                            sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase))
                            tempC = sensor.Value;
                        if (sensor.Name.Contains("Throttle", StringComparison.OrdinalIgnoreCase) ||
                            sensor.Name.Contains("PROCHOT", StringComparison.OrdinalIgnoreCase))
                            throttling = (sensor.Value ?? 0) > 0;
                    }
                }
            }
        }
        catch { }
        if (tempC is double t && throttling is null) throttling = t >= 99;

        var (ramUsed, ramTotal, freePhysPct, commitPct, pagefilePct, driveCFree) = ReadMemoryAndDisk();
        double? diskQ = null;
        try { diskQ = _diskQueue?.NextValue(); } catch { }

        _events.RefreshIfDue(now);
        var procs = _processes.Sample(now).OrderByDescending(p => p.CpuPercent).Take(10).ToList();
        var avg24h = _history.AverageHourlyErrorCount();

        return new SensorSnapshot(
            now, cpu, tempC, throttling,
            ramUsed, ramTotal, freePhysPct,
            commitPct, pagefilePct, diskQ, driveCFree,
            _events.Last5MinutesErrors, _events.LastHourErrors, avg24h,
            procs);
    }

    private static (double ramUsed, double ramTotal, double freePhysPct, double? commitPct, double? pagefilePct, double? driveCFree) ReadMemoryAndDisk()
    {
        double ramUsed = 0, ramTotal = 0, freePhysPct = 0;
        double? commitPct = null, pagefilePct = null, driveCFree = null;
        try
        {
            using var os = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            foreach (ManagementObject m in os.Get())
            {
                var totalKb = Convert.ToDouble(m["TotalVisibleMemorySize"]);
                var freeKb = Convert.ToDouble(m["FreePhysicalMemory"]);
                ramTotal = totalKb / 1024.0 / 1024.0;
                ramUsed = (totalKb - freeKb) / 1024.0 / 1024.0;
                freePhysPct = totalKb == 0 ? 0 : (freeKb / totalKb * 100.0);
            }
        }
        catch { }

        try
        {
            using var pf = new ManagementObjectSearcher("SELECT AllocatedBaseSize, CurrentUsage FROM Win32_PageFileUsage");
            foreach (ManagementObject m in pf.Get())
            {
                var alloc = Convert.ToDouble(m["AllocatedBaseSize"]);
                var used = Convert.ToDouble(m["CurrentUsage"]);
                if (alloc > 0) pagefilePct = used / alloc * 100.0;
            }
        }
        catch { }

        try
        {
            using var commit = new ManagementObjectSearcher("SELECT CommittedBytes, CommitLimit FROM Win32_PerfRawData_PerfOS_Memory");
            foreach (ManagementObject m in commit.Get())
            {
                var committed = Convert.ToDouble(m["CommittedBytes"]);
                var limit = Convert.ToDouble(m["CommitLimit"]);
                if (limit > 0) commitPct = committed / limit * 100.0;
            }
        }
        catch { }

        try
        {
            var drive = new DriveInfo("C");
            if (drive.IsReady) driveCFree = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
        }
        catch { }

        return (ramUsed, ramTotal, freePhysPct, commitPct, pagefilePct, driveCFree);
    }

    public void Dispose()
    {
        _cpuTotal?.Dispose();
        _diskQueue?.Dispose();
        _computer?.Close();
    }
}
