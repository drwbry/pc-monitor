namespace PcMonitor.Core.GameMode;

public enum GameModeItemKind
{
    /// <summary>Desktop app: close its process tree, relaunch it un-elevated afterwards.</summary>
    App,
    /// <summary>Windows service: stop it, start it again afterwards. StartMode is never changed.</summary>
    Service,
    /// <summary>Something stopped/started by commands (e.g. WSL). Detected by DetectProcessNames.</summary>
    Command,
}

public sealed record GameModeItem
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public GameModeItemKind Kind { get; init; } = GameModeItemKind.App;

    /// <summary>App: process names (no .exe), case-insensitive.</summary>
    public string[] ProcessNames { get; init; } = [];
    /// <summary>App: optional case-insensitive substring the exe path must contain.</summary>
    public string? PathContains { get; init; }
    /// <summary>App: relaunch through shell:AppsFolder\{AppId}. Otherwise the captured exe + args are used.</summary>
    public string? AppId { get; init; }
    /// <summary>App: graceful close, run as "{exe} {CloseArgs}" before killing (e.g. OneDrive /shutdown).</summary>
    public string? CloseArgs { get; init; }

    /// <summary>Service: service name.</summary>
    public string? ServiceName { get; init; }

    /// <summary>Command: processes whose presence means "running" (e.g. vmmemWSL).</summary>
    public string[] DetectProcessNames { get; init; } = [];
    public CommandSpec? StopCommand { get; init; }
    public CommandSpec? StartCommand { get; init; }

    /// <summary>Extra processes whose RAM is attributed to this item (e.g. vmmem for a VM service).</summary>
    public string[] MemoryProcessNames { get; init; } = [];

    /// <summary>False = close for Game Mode but never bring it back.</summary>
    public bool Relaunch { get; init; } = true;
    public string? Note { get; init; }
}

public sealed record CommandSpec(string FileName, string Arguments);

public sealed record GameModeConfig
{
    public List<GameModeItem> Items { get; init; } = new();
}

/// <summary>How to bring an app back. AppId wins; otherwise ExePath + Arguments.</summary>
public sealed record LaunchSpec(string? AppId, string? ExePath, string? Arguments);

public readonly record struct ProcessInfo(
    int Pid, int ParentPid, string Name, string? ExePath, string? CommandLine, long PrivateBytes);

public sealed record PlannedItem(GameModeItem Item, long MemoryBytes, LaunchSpec? Launch);

public sealed record StoppedItem(GameModeItem Item, LaunchSpec? Launch);

public sealed record GameModeState
{
    public bool Active { get; init; }
    public DateTimeOffset? EnteredAt { get; init; }
    public List<StoppedItem> Stopped { get; init; } = new();
}
