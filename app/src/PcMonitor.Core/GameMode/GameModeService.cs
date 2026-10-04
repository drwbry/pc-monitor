using System.Text.Json;
using System.Text.Json.Serialization;

namespace PcMonitor.Core.GameMode;

/// <summary>
/// Owns the Game Mode config (gamemode.json, user-editable) and state (gamemode-state.json,
/// so the "bring back" list survives a PcMonitor restart or a reboot).
/// </summary>
public sealed class GameModeService
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IGameModeSystem _sys;
    private readonly GameModeRunner _runner;
    private readonly string _configPath;
    private readonly string _statePath;
    private readonly string? _logPath;

    public GameModeState State { get; private set; } = new();
    public bool IsActive => State.Active;
    public string? ConfigError { get; private set; }
    public string ConfigPath => _configPath;
    public event EventHandler? StateChanged;

    /// <param name="logPath">Optional run log (gamemode.log) so a session can be diagnosed afterwards.</param>
    public GameModeService(IGameModeSystem sys, string configPath, string statePath, string? logPath = null)
    {
        _logPath = logPath;
        _sys = sys;
        _runner = new GameModeRunner(sys);
        _configPath = configPath;
        _statePath = statePath;
        State = LoadState();
    }

    public GameModeConfig LoadConfig()
    {
        ConfigError = null;
        try
        {
            if (!File.Exists(_configPath))
            {
                var defaults = GameModeDefaults.Create();
                Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
                File.WriteAllText(_configPath, JsonSerializer.Serialize(defaults, Json));
                return defaults;
            }
            return JsonSerializer.Deserialize<GameModeConfig>(File.ReadAllText(_configPath), Json)
                   ?? GameModeDefaults.Create();
        }
        catch (Exception ex)
        {
            ConfigError = $"{Path.GetFileName(_configPath)} could not be read ({ex.Message}). Using the built-in list.";
            return GameModeDefaults.Create();
        }
    }

    public IReadOnlyList<PlannedItem> PlanEnter() =>
        GameModePlanner.PlanEnter(LoadConfig(), _sys.GetProcesses(), _sys.IsServiceRunning);

    public IReadOnlyList<StoppedItem> PlanExit() =>
        GameModePlanner.PlanExit(State, _sys.GetProcesses(), _sys.IsServiceRunning);

    public IReadOnlyList<StoppedItem> AlreadyRunning() =>
        GameModePlanner.AlreadyRunning(State, _sys.GetProcesses(), _sys.IsServiceRunning);

    public async Task EnterAsync(IReadOnlyList<PlannedItem> selected, bool dryRun, Action<string> log)
    {
        log = Tee(log, $"Start Game Mode{(dryRun ? " (dry run)" : "")}: {Names(selected.Select(p => p.Item))}");
        var stopped = await _runner.EnterAsync(selected, dryRun, log);
        if (dryRun) return;
        SetState(new GameModeState { Active = true, EnteredAt = DateTimeOffset.Now, Stopped = stopped.ToList() });
    }

    /// <summary>Starts the selected items. Unselected items are dropped: the user chose not to bring them back.</summary>
    public async Task ExitAsync(IReadOnlyList<StoppedItem> selected, bool dryRun, Action<string> log)
    {
        var skipped = AlreadyRunning();
        log = Tee(log, $"End Game Mode{(dryRun ? " (dry run)" : "")}: {Names(selected.Select(s => s.Item))}"
                       + (skipped.Count > 0 ? $" | already running: {Names(skipped.Select(s => s.Item))}" : ""));
        await _runner.ExitAsync(selected, dryRun, log);
        if (dryRun) return;
        SetState(new GameModeState());
    }

    private static string Names(IEnumerable<GameModeItem> items)
    {
        var list = items.Select(i => i.DisplayName).ToList();
        return list.Count == 0 ? "(nothing)" : string.Join(", ", list);
    }

    /// <summary>Wraps the UI log so every line also lands in gamemode.log with a timestamp.</summary>
    private Action<string> Tee(Action<string> log, string header)
    {
        Append($"=== {header}");
        return line =>
        {
            Append(line);
            log(line);
        };
    }

    private void Append(string line)
    {
        if (_logPath is null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
            File.AppendAllText(_logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {line}{Environment.NewLine}");
        }
        catch { }
    }

    private void SetState(GameModeState state)
    {
        State = state;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_statePath)!);
            File.WriteAllText(_statePath, JsonSerializer.Serialize(state, Json));
        }
        catch { }
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private GameModeState LoadState()
    {
        try
        {
            if (File.Exists(_statePath))
                return JsonSerializer.Deserialize<GameModeState>(File.ReadAllText(_statePath), Json) ?? new();
        }
        catch { }
        return new GameModeState();
    }
}
