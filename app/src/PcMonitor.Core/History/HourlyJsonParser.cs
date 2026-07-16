using System.Text.Json;
using PcMonitor.Core.Models;

namespace PcMonitor.Core.History;

public static class HourlyJsonParser
{
    public static HourlyEntry? Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var ts = GetTimestamp(root);
            if (ts is null) return null;

            var cpuPct = GetDouble(root, "cpu_load_pct");
            double? ramTotal = null, ramFree = null, ramUsed = null;
            if (root.TryGetProperty("ram", out var ram))
            {
                ramTotal = GetDouble(ram, "total_gb");
                ramFree = GetDouble(ram, "free_gb");
                if (ramTotal.HasValue && ramFree.HasValue)
                    ramUsed = Math.Round(ramTotal.Value - ramFree.Value, 2);
            }

            double? driveCFree = null;
            if (root.TryGetProperty("disks", out var disks) && disks.ValueKind == JsonValueKind.Array)
            {
                foreach (var disk in disks.EnumerateArray())
                {
                    if (disk.TryGetProperty("drive", out var driveEl) &&
                        driveEl.GetString()?.Equals("C", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        driveCFree = GetDouble(disk, "free_gb");
                        break;
                    }
                }
            }

            var sysErr = GetInt(root, "system_errors_last_hour");
            var appErr = GetInt(root, "app_errors_last_hour");

            double? procPerfAvg = null, procPerfMax = null, freqMhz = null;
            if (root.TryGetProperty("cpu_perf", out var cpuPerf))
            {
                procPerfAvg = GetDouble(cpuPerf, "proc_performance_pct_avg");
                procPerfMax = GetDouble(cpuPerf, "proc_performance_pct_max");
                freqMhz = GetDouble(cpuPerf, "frequency_mhz");
            }

            return new HourlyEntry(ts.Value, cpuPct, ramUsed, ramTotal, driveCFree, sysErr, appErr,
                procPerfAvg, procPerfMax, freqMhz);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static DateTimeOffset? GetTimestamp(JsonElement el)
    {
        if (el.TryGetProperty("timestamp", out var ts) &&
            DateTimeOffset.TryParse(ts.GetString(), out var dto))
            return dto;
        return null;
    }

    private static double? GetDouble(JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number)
            return v.GetDouble();
        return null;
    }

    private static int? GetInt(JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number)
            return v.GetInt32();
        return null;
    }
}
