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
                TimeSpan cpu;
                try { cpu = p.TotalProcessorTime; }
                catch { cpu = TimeSpan.Zero; }
                list.Add(new RawProcess(p.Id, p.ProcessName, cpu, p.WorkingSet64));
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
