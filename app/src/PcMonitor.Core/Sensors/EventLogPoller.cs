namespace PcMonitor.Core.Sensors;

public sealed class EventLogPoller : IEventLogPoller
{
    private readonly Func<DateTimeOffset, (int last5m, int lastHour)> _queryFn;
    private readonly TimeSpan _refreshInterval;
    private DateTimeOffset _lastQueryAt = DateTimeOffset.MinValue;

    public int? Last5MinutesErrors { get; private set; }
    public int? LastHourErrors { get; private set; }

    public EventLogPoller(
        Func<DateTimeOffset, (int last5m, int lastHour)> queryFn,
        TimeSpan refreshInterval)
    {
        _queryFn = queryFn;
        _refreshInterval = refreshInterval;
    }

    public void RefreshIfDue(DateTimeOffset now)
    {
        if (now - _lastQueryAt < _refreshInterval) return;
        try
        {
            var (last5m, lastHour) = _queryFn(now);
            Last5MinutesErrors = last5m;
            LastHourErrors = lastHour;
        }
        catch
        {
            Last5MinutesErrors = null;
            LastHourErrors = null;
        }
        _lastQueryAt = now;
    }

    public static (int last5m, int lastHour) QueryWindowsEventLog(DateTimeOffset now)
    {
        var fiveMinAgo = now.AddMinutes(-5).UtcDateTime.ToString("o");
        var oneHourAgo = now.AddHours(-1).UtcDateTime.ToString("o");

        int Count(string log, string startIso)
        {
            var xpath = $"*[System[(Level=1 or Level=2) and TimeCreated[@SystemTime>='{startIso}']]]";
            var query = new System.Diagnostics.Eventing.Reader.EventLogQuery(log, System.Diagnostics.Eventing.Reader.PathType.LogName, xpath);
            using var reader = new System.Diagnostics.Eventing.Reader.EventLogReader(query);
            var c = 0;
            while (reader.ReadEvent() is { } e)
            {
                e.Dispose();
                c++;
            }
            return c;
        }

        var sys5 = Count("System", fiveMinAgo) + Count("Application", fiveMinAgo);
        var sys60 = Count("System", oneHourAgo) + Count("Application", oneHourAgo);
        return (sys5, sys60);
    }
}
