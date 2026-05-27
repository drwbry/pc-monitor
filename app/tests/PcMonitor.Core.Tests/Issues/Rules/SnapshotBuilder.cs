using PcMonitor.Core.Models;

namespace PcMonitor.Core.Tests.Issues.Rules;

public static class SnapshotBuilder
{
    public static SensorSnapshot Default(
        DateTimeOffset? t = null,
        double? cpuPct = 5,
        double? tempC = 60,
        bool? throttling = false,
        double freePhysRamPct = 50,
        double? commitPct = 40,
        double? pagefilePct = 10,
        double? diskQ = 0.2,
        double? driveCFree = 400,
        int? errLast5 = 0,
        int? errThisHour = 0,
        double? errAvg24h = 1,
        IReadOnlyList<ProcessSample>? procs = null)
        => new(
            t ?? DateTimeOffset.UnixEpoch,
            cpuPct, tempC, throttling,
            10, 64, freePhysRamPct,
            commitPct, pagefilePct, diskQ, driveCFree,
            errLast5, errThisHour, errAvg24h,
            procs ?? Array.Empty<ProcessSample>());
}
