using PcMonitor.Core.Models;

namespace PcMonitor.Core.History;

public interface IHistoryReader
{
    IReadOnlyList<HourlyEntry> ReadAll();
    double? AverageHourlyErrorCount(int hoursBack = 24);
    event EventHandler? Changed;
}
