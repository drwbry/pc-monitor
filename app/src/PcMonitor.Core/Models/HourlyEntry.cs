namespace PcMonitor.Core.Models;

public sealed record HourlyEntry(
    DateTimeOffset Timestamp,
    double? CpuPercent,
    double? RamUsedGb,
    double? RamTotalGb,
    double? DriveCFreeGb,
    int? SystemErrorsLastHour,
    int? AppErrorsLastHour,
    double? CpuProcPerfPctAvg = null,
    double? CpuProcPerfPctMax = null,
    double? CpuFrequencyMhz = null);
