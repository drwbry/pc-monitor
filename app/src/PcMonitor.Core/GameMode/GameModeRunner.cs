namespace PcMonitor.Core.GameMode;

/// <summary>
/// Executes a Game Mode plan. Entering closes apps first, then command items (WSL), then services
/// (the Cowork VM goes after the Claude app). Exiting runs the reverse: services, commands, apps.
/// </summary>
public sealed class GameModeRunner
{
    private static readonly TimeSpan ServiceTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan GracefulCloseWait = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    private readonly IGameModeSystem _sys;

    public GameModeRunner(IGameModeSystem sys) => _sys = sys;

    private static int EnterOrder(GameModeItemKind k) => k switch
    {
        GameModeItemKind.App => 0,
        GameModeItemKind.Command => 1,
        _ => 2,
    };

    public async Task<IReadOnlyList<StoppedItem>> EnterAsync(
        IReadOnlyList<PlannedItem> selected, bool dryRun, Action<string> log)
    {
        var stopped = new List<StoppedItem>();
        foreach (var p in selected.OrderBy(p => EnterOrder(p.Item.Kind)))
        {
            var item = p.Item;
            if (GameModePlanner.IsProtected(item)) { log($"Skipped {item.DisplayName}: protected"); continue; }
            if (dryRun) { log($"[dry run] would stop {item.DisplayName}"); continue; }
            try
            {
                switch (item.Kind)
                {
                    case GameModeItemKind.App:
                        await CloseAppAsync(item);
                        break;
                    case GameModeItemKind.Command:
                        if (item.StopCommand is null) throw new InvalidOperationException("no StopCommand");
                        await _sys.RunHiddenAsync(item.StopCommand.FileName, item.StopCommand.Arguments, CommandTimeout);
                        break;
                    case GameModeItemKind.Service:
                        _sys.StopService(item.ServiceName!, ServiceTimeout);
                        break;
                }
                stopped.Add(new StoppedItem(item, p.Launch));
                log($"Stopped {item.DisplayName}");
            }
            catch (Exception ex)
            {
                log($"FAILED to stop {item.DisplayName}: {ex.Message}");
            }
        }
        return stopped;
    }

    public async Task ExitAsync(IReadOnlyList<StoppedItem> selected, bool dryRun, Action<string> log)
    {
        foreach (var s in selected.OrderByDescending(s => EnterOrder(s.Item.Kind)))
        {
            var item = s.Item;
            if (dryRun) { log($"[dry run] would start {item.DisplayName}"); continue; }
            try
            {
                switch (item.Kind)
                {
                    case GameModeItemKind.Service:
                        _sys.StartService(item.ServiceName!, ServiceTimeout);
                        break;
                    case GameModeItemKind.Command:
                        if (item.StartCommand is null) throw new InvalidOperationException("no StartCommand");
                        await _sys.RunHiddenAsync(item.StartCommand.FileName, item.StartCommand.Arguments, CommandTimeout);
                        break;
                    case GameModeItemKind.App:
                        if (s.Launch is null) throw new InvalidOperationException("no relaunch info was captured");
                        _sys.LaunchUnelevated(item.Id, s.Launch);
                        break;
                }
                log($"Started {item.DisplayName}");
            }
            catch (Exception ex)
            {
                log($"FAILED to start {item.DisplayName}: {ex.Message}");
            }
        }
    }

    private async Task CloseAppAsync(GameModeItem item)
    {
        var roots = GameModePlanner.FindRoots(item, _sys.GetProcesses());
        if (item.CloseArgs is not null && roots.FirstOrDefault(r => r.ExePath is not null).ExePath is { } exe)
        {
            await _sys.RunHiddenAsync(exe, item.CloseArgs, CommandTimeout);
            var deadline = GracefulCloseWait;
            while (deadline > TimeSpan.Zero)
            {
                roots = GameModePlanner.FindRoots(item, _sys.GetProcesses());
                if (roots.Count == 0) return;
                await _sys.DelayAsync(PollInterval);
                deadline -= PollInterval;
            }
        }
        foreach (var r in roots)
        {
            try { _sys.KillProcessTree(r.Pid); }
            catch { /* already gone, or exited with its parent */ }
        }
    }
}
