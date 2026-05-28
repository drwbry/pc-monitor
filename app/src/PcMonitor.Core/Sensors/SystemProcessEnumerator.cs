using System.Diagnostics;

namespace PcMonitor.Core.Sensors;

public sealed class SystemProcessEnumerator : IProcessEnumerator
{
    public IReadOnlyList<RawProcess> Enumerate()
    {
        var procs = Process.GetProcesses();
        var list = new List<RawProcess>(procs.Length);
        foreach (var p in procs)
        {
            try
            {
                list.Add(new RawProcess(p.Id, p.ProcessName, p.TotalProcessorTime, p.PrivateMemorySize64));
            }
            catch
            {
                // process exited or access denied; skip
            }
            finally
            {
                p.Dispose();
            }
        }
        return list;
    }
}
