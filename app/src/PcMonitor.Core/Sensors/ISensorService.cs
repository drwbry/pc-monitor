using PcMonitor.Core.Models;

namespace PcMonitor.Core.Sensors;

public interface ISensorService : IDisposable
{
    SensorSnapshot Read(DateTimeOffset now);
    bool TempSensorsAvailable { get; }
}
