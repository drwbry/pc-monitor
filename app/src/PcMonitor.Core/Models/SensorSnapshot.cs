namespace PcMonitor.Core.Models;

public sealed record SensorSnapshot(
    DateTimeOffset Timestamp,
    double? CpuPercent,
    double? CpuPackageTempC,
    bool? IsThrottling,
    double RamUsedGb,
    double RamTotalGb,
    double FreePhysicalRamPercent,
    double? CommitUsedPercent,
    double? PagefileUsedPercent,
    double? DiskQueueLength,
    double? DriveCFreeGb,
    int? EventErrorsLast5Minutes,
    int? EventErrorsThisHour,
    double? EventErrors24hHourlyAverage,
    IReadOnlyList<ProcessSample> TopProcesses);

public sealed record ProcessSample(
    int ProcessId,
    string Name,
    double CpuPercent,
    double RamMb);
