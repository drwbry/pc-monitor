namespace PcMonitor.Core.GameMode;

public static class GameModePlanner
{
    /// <summary>
    /// Processes and services Game Mode must never touch, whatever the config says:
    /// tuning tools, game launchers, peripherals, drivers, security, VPNs and PcMonitor itself.
    /// (Discord is not here: it is an optional, unchecked-by-default item instead.)
    /// </summary>
    public static readonly IReadOnlySet<string> ProtectedProcessNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "PcMonitor", "ThrottleStop", "MSIAfterburner", "RTSS", "Lenovo Legion Toolkit",
        "steam", "steamwebhelper", "EpicGamesLauncher", "EADesktop", "EALauncher", "GalaxyClient",
        "lghub", "lghub_agent", "lghub_system_tray", "lghub_updater", "EarTrumpet",
        "NVDisplay.Container", "nvcontainer", "NVIDIA App", "RtkAudUService64",
        "MsMpEng", "SecurityHealthSystray", "Surfshark", "Surfshark.Service",
        "tailscaled", "tailscale-ipn", "PanGPA", "PanGPS",
        "explorer", "dwm", "csrss", "winlogon", "lsass", "svchost", "services", "wininit",
    };

    public static readonly IReadOnlySet<string> ProtectedServiceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "WinDefend", "Tailscale", "Surfshark Service", "PanGPS", "NvContainerLocalSystem",
        "WSLService", "vmcompute", "Audiosrv", "AudioEndpointBuilder",
    };

    public static bool IsProtected(GameModeItem item) =>
        item.ProcessNames.Any(ProtectedProcessNames.Contains)
        || (item.ServiceName is not null && ProtectedServiceNames.Contains(item.ServiceName));

    /// <summary>Items from the config that are running right now, with RAM and how to relaunch them.</summary>
    public static IReadOnlyList<PlannedItem> PlanEnter(
        GameModeConfig config, IReadOnlyList<ProcessInfo> procs, Func<string, bool> isServiceRunning)
    {
        var result = new List<PlannedItem>();
        foreach (var item in config.Items)
        {
            if (IsProtected(item)) continue;
            switch (item.Kind)
            {
                case GameModeItemKind.App:
                {
                    var roots = FindRoots(item, procs);
                    if (roots.Count == 0) continue;
                    var mem = TreeMemory(roots, procs) + NamedMemory(item.MemoryProcessNames, procs);
                    result.Add(new PlannedItem(item, mem, BuildLaunch(item, roots[0])));
                    break;
                }
                case GameModeItemKind.Service:
                    if (item.ServiceName is null || !isServiceRunning(item.ServiceName)) continue;
                    result.Add(new PlannedItem(item, NamedMemory(item.MemoryProcessNames, procs), null));
                    break;
                case GameModeItemKind.Command:
                    if (!AnyNamed(item.DetectProcessNames, procs)) continue;
                    var cmdMem = NamedMemory(item.DetectProcessNames.Concat(item.MemoryProcessNames), procs);
                    result.Add(new PlannedItem(item, cmdMem, null));
                    break;
            }
        }
        return result;
    }

    /// <summary>What Game Mode closed that should come back and isn't already running again.</summary>
    public static IReadOnlyList<StoppedItem> PlanExit(
        GameModeState state, IReadOnlyList<ProcessInfo> procs, Func<string, bool> isServiceRunning)
    {
        return state.Stopped
            .Where(s => s.Item.Relaunch && !IsRunning(s.Item, procs, isServiceRunning))
            .ToList();
    }

    public static bool IsRunning(GameModeItem item, IReadOnlyList<ProcessInfo> procs, Func<string, bool> isServiceRunning) =>
        item.Kind switch
        {
            GameModeItemKind.App => FindRoots(item, procs).Count > 0,
            GameModeItemKind.Service => item.ServiceName is not null && isServiceRunning(item.ServiceName),
            GameModeItemKind.Command => AnyNamed(item.DetectProcessNames, procs),
            _ => false,
        };

    /// <summary>Matching processes whose parent is not itself a match (the app's top-level processes).</summary>
    public static IReadOnlyList<ProcessInfo> FindRoots(GameModeItem item, IReadOnlyList<ProcessInfo> procs)
    {
        var matches = procs.Where(p => Matches(item, p)).ToList();
        var ids = matches.Select(p => p.Pid).ToHashSet();
        return matches.Where(p => !ids.Contains(p.ParentPid)).OrderBy(p => p.Pid).ToList();
    }

    private static bool Matches(GameModeItem item, ProcessInfo p)
    {
        if (!item.ProcessNames.Contains(p.Name, StringComparer.OrdinalIgnoreCase)) return false;
        if (item.PathContains is null) return true;
        return p.ExePath is not null && p.ExePath.Contains(item.PathContains, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Private bytes of the roots and every descendant (WebView2 helpers, renderers, ...).</summary>
    private static long TreeMemory(IReadOnlyList<ProcessInfo> roots, IReadOnlyList<ProcessInfo> procs)
    {
        var children = procs.ToLookup(p => p.ParentPid);
        var seen = new HashSet<int>();
        var stack = new Stack<ProcessInfo>(roots);
        long total = 0;
        while (stack.Count > 0)
        {
            var p = stack.Pop();
            if (!seen.Add(p.Pid)) continue;
            total += p.PrivateBytes;
            foreach (var c in children[p.Pid])
                if (c.Pid != p.Pid) stack.Push(c);
        }
        return total;
    }

    private static long NamedMemory(IEnumerable<string> names, IReadOnlyList<ProcessInfo> procs)
    {
        var set = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        return procs.Where(p => set.Contains(p.Name)).Sum(p => p.PrivateBytes);
    }

    private static bool AnyNamed(IEnumerable<string> names, IReadOnlyList<ProcessInfo> procs)
    {
        var set = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        return set.Count > 0 && procs.Any(p => set.Contains(p.Name));
    }

    private static LaunchSpec? BuildLaunch(GameModeItem item, ProcessInfo root)
    {
        if (!item.Relaunch) return null;
        if (!string.IsNullOrWhiteSpace(item.AppId)) return new LaunchSpec(item.AppId, null, null);
        if (root.ExePath is null) return null;
        return new LaunchSpec(null, root.ExePath, ArgumentsFromCommandLine(root.CommandLine));
    }

    /// <summary>Strip the leading exe (quoted or bare) from a command line; null when no arguments.</summary>
    public static string? ArgumentsFromCommandLine(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine)) return null;
        var s = commandLine.TrimStart();
        string rest;
        if (s.StartsWith('"'))
        {
            var close = s.IndexOf('"', 1);
            rest = close < 0 ? "" : s[(close + 1)..];
        }
        else
        {
            var space = s.IndexOf(' ');
            rest = space < 0 ? "" : s[space..];
        }
        rest = rest.Trim();
        return rest.Length == 0 ? null : rest;
    }
}
