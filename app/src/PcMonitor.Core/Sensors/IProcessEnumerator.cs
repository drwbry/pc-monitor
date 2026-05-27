namespace PcMonitor.Core.Sensors;

public readonly record struct RawProcess(int Pid, string Name, TimeSpan TotalProcessorTime, long WorkingSetBytes);

public interface IProcessEnumerator
{
    IReadOnlyList<RawProcess> Enumerate();
}
