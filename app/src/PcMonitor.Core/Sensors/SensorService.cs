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

    public SensorService(IHistoryReader history, string? logPath = null)
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
            var cpuPackageFound = false;
            if (logPath is not null)
            {
                try
                {
                    var lines = new System.Text.StringBuilder();
                    lines.AppendLine($"=== LHM sensors @ {DateTimeOffset.Now:o} ===");
                    foreach (var hw in _computer.Hardware)
                    {
                        hw.Update();
                        lines.AppendLine($"  HW: {hw.HardwareType} | {hw.Name}");
                        foreach (var s in hw.Sensors)
                            lines.AppendLine($"    [{s.SensorType}] {s.Name} = {s.Value}");
                    }
                    File.AppendAllText(logPath, lines.ToString());
                }
                catch { }
            }
            foreach (var hw in _computer.Hardware)
            {
                if (hw.HardwareType != HardwareType.Cpu) continue;
                hw.Update();
                if (hw.Sensors.Any(s => s.SensorType == SensorType.Temperature &&
                    s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase)))
                    cpuPackageFound = true;
            }
            TempSensorsAvailable = cpuPackageFound;
        }
        catch
        {
            _computer = null;
        }

        // If LHM didn't find a readable package sensor, check whether the ACPI fallback works.
        if (!TempSensorsAvailable)
            TempSensorsAvailable = ReadAcpiCpuTemp() is not null;

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

        // LHM may find sensor slots but return null values (ring 0 driver blocked by Secure Boot).
        // Fall back to ACPI thermal zones via WMI, which needs no kernel driver.
        if (tempC is null)
            tempC = ReadAcpiCpuTemp();

        if (tempC is double t && throttling is null) throttling = t >= 99;

        var (ramUsed, ramTotal, freePhysPct, commitPct, pagefilePct, driveCFree) = ReadMemoryAndDisk();
        double? diskQ = null;
        try { diskQ = _diskQueue?.NextValue(); } catch { }

        _events.RefreshIfDue(now);
        var procs = _processes.Sample(now).Take(10).ToList();
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

    private static double? ReadAcpiCpuTemp()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM MSAcpi_ThermalZoneTemperature");
            double? maxTemp = null;
            foreach (ManagementObject m in searcher.Get())
            {
                var tenthsKelvin = Convert.ToDouble(m["CurrentTemperature"]);
                var tempC = tenthsKelvin / 10.0 - 273.15;
                if (tempC is > 0 and < 150)
                    maxTemp = maxTemp is null ? tempC : Math.Max(maxTemp.Value, tempC);
            }
            return maxTemp;
        }
        catch { return null; }
    }

    public void Dispose()
    {
        _cpuTotal?.Dispose();
        _diskQueue?.Dispose();
        _computer?.Close();
    }
}
