using System.Diagnostics;
using System.Management;
using System.ServiceProcess;

namespace PcMonitor.Core.GameMode;

public sealed class WindowsGameModeSystem : IGameModeSystem
{
    private readonly string _launcherFolder;

    /// <param name="launcherFolder">Where .lnk files are written for apps that relaunch with arguments.</param>
    public WindowsGameModeSystem(string launcherFolder) => _launcherFolder = launcherFolder;

    public IReadOnlyList<ProcessInfo> GetProcesses()
    {
        var list = new List<ProcessInfo>();
        using var searcher = new ManagementObjectSearcher(
            "SELECT ProcessId, ParentProcessId, Name, ExecutablePath, CommandLine, PrivatePageCount FROM Win32_Process");
        foreach (ManagementObject m in searcher.Get())
        {
            using (m)
            {
                var name = m["Name"] as string ?? "";
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
                list.Add(new ProcessInfo(
                    Convert.ToInt32(m["ProcessId"]),
                    Convert.ToInt32(m["ParentProcessId"]),
                    name,
                    m["ExecutablePath"] as string,
                    m["CommandLine"] as string,
                    Convert.ToInt64(m["PrivatePageCount"] ?? 0L)));
            }
        }
        return list;
    }

    public bool IsServiceRunning(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            return sc.Status is ServiceControllerStatus.Running or ServiceControllerStatus.StartPending;
        }
        catch (InvalidOperationException)
        {
            return false; // not installed
        }
    }

    public void StopService(string serviceName, TimeSpan timeout)
    {
        using var sc = new ServiceController(serviceName);
        if (sc.Status == ServiceControllerStatus.Stopped) return;
        if (sc.Status != ServiceControllerStatus.StopPending) sc.Stop();
        sc.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
    }

    public void StartService(string serviceName, TimeSpan timeout)
    {
        using var sc = new ServiceController(serviceName);
        if (sc.Status == ServiceControllerStatus.Running) return;
        if (sc.Status != ServiceControllerStatus.StartPending) sc.Start();
        sc.WaitForStatus(ServiceControllerStatus.Running, timeout);
    }

    public void KillProcessTree(int pid)
    {
        using var p = Process.GetProcessById(pid);
        p.Kill(entireProcessTree: true);
    }

    public async Task<int> RunHiddenAsync(string fileName, string arguments, TimeSpan timeout)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var proc = Process.Start(psi) ?? throw new InvalidOperationException($"could not start {fileName}");
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await proc.WaitForExitAsync(cts.Token);
            return proc.ExitCode;
        }
        catch (OperationCanceledException)
        {
            throw new System.TimeoutException($"{Path.GetFileName(fileName)} did not finish within {timeout.TotalSeconds:N0}s");
        }
    }

    /// <summary>
    /// PcMonitor runs elevated, and Process.Start would hand that admin token to the app.
    /// Asking explorer.exe to open the target makes the running (non-elevated) shell launch it instead.
    /// Apps that need arguments get a .lnk shortcut, which explorer opens the same way.
    /// </summary>
    public void LaunchUnelevated(string id, LaunchSpec spec)
    {
        string target;
        if (!string.IsNullOrWhiteSpace(spec.AppId))
            target = $@"shell:AppsFolder\{spec.AppId}";
        else if (string.IsNullOrWhiteSpace(spec.ExePath))
            throw new InvalidOperationException("no AppId or exe path to launch");
        else if (string.IsNullOrWhiteSpace(spec.Arguments))
            target = spec.ExePath;
        else
            target = WriteShortcut(id, spec.ExePath, spec.Arguments);

        using var _ = Process.Start(new ProcessStartInfo("explorer.exe", $"\"{target}\"") { UseShellExecute = false });
    }

    private string WriteShortcut(string id, string exePath, string arguments)
    {
        Directory.CreateDirectory(_launcherFolder);
        var safe = string.Concat(id.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var path = Path.Combine(_launcherFolder, safe + ".lnk");
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
                        ?? throw new InvalidOperationException("WScript.Shell is not available");
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic lnk = shell.CreateShortcut(path);
        lnk.TargetPath = exePath;
        lnk.Arguments = arguments;
        lnk.WorkingDirectory = Path.GetDirectoryName(exePath);
        lnk.Save();
        return path;
    }

    public Task DelayAsync(TimeSpan delay) => Task.Delay(delay);
}
