namespace PcMonitor.Core.Sensors;

public interface IEventLogPoller
{
    int? Last5MinutesErrors { get; }
    int? LastHourErrors { get; }
    void RefreshIfDue(DateTimeOffset now);
}
