using PcMonitor.Core.Capture;
using PcMonitor.Core.History;
using PcMonitor.Core.Issues;
using PcMonitor.Core.Issues.Rules;
using PcMonitor.Core.Sensors;

namespace PcMonitor.App.Composition;

public sealed class Services : IDisposable
{
    public ISensorService Sensors { get; }
    public IssueEvaluator Issues { get; }
    public ICaptureService Capture { get; }
    public HourlyHistoryReader History { get; }
    public Settings.SettingsStore Settings { get; } = new();

    public Services()
    {
        Directory.CreateDirectory(Paths.AppDataFolder);
        History = new HourlyHistoryReader(Paths.HourlyFolder, watch: true);
        Sensors = new SensorService(History, Paths.LogFile);
        Issues = new IssueEvaluator(new IIssueRule[]
        {
            new ThermalThrottleRule(),
            new CpuPackageTempHighRule(),
            new CommitNearExhaustionRule(),
            new DriveCCriticalRule(),
            new RunawayProcessRule(),
            new EventLogSpikeRule(),
            new CpuPackageTempElevatedRule(),
            new RamPressureRule(),
            new SustainedCpuHogRule(),
            new MemoryHogRule(),
            new DriveCLowRule(),
            new PagefilePressureRule(),
            new EventLogUptickRule(),
            new DiskQueueElevatedRule(),
        });
        Capture = new CaptureService(new PowerShellProcessRunner(), Paths.ScriptsFolder, Paths.SysLogsRoot);
    }

    public void Dispose()
    {
        Sensors.Dispose();
        History.Dispose();
    }
}
