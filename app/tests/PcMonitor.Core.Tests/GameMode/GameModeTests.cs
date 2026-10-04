using FluentAssertions;
using PcMonitor.Core.GameMode;
using Xunit;

namespace PcMonitor.Core.Tests.GameMode;

internal sealed class FakeSystem : IGameModeSystem
{
    public List<ProcessInfo> Processes { get; } = new();
    public HashSet<string> RunningServices { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Calls { get; } = new();
    /// <summary>When true, a graceful close command makes the matching app exit.</summary>
    public bool CloseCommandWorks { get; set; } = true;

    public IReadOnlyList<ProcessInfo> GetProcesses() => Processes.ToList();
    public bool IsServiceRunning(string serviceName) => RunningServices.Contains(serviceName);
    public void StopService(string serviceName, TimeSpan timeout) { Calls.Add($"stop-svc {serviceName}"); RunningServices.Remove(serviceName); }
    public void StartService(string serviceName, TimeSpan timeout) { Calls.Add($"start-svc {serviceName}"); RunningServices.Add(serviceName); }
    public void KillProcessTree(int pid)
    {
        Calls.Add($"kill {pid}");
        var doomed = new HashSet<int> { pid };
        bool grew;
        do
        {
            grew = false;
            foreach (var p in Processes.Where(p => doomed.Contains(p.ParentPid) && !doomed.Contains(p.Pid)).ToList())
                grew |= doomed.Add(p.Pid);
        } while (grew);
        Processes.RemoveAll(p => doomed.Contains(p.Pid));
    }
    public Task<int> RunHiddenAsync(string fileName, string arguments, TimeSpan timeout)
    {
        Calls.Add($"run {fileName[(fileName.LastIndexOf('\\') + 1)..]} {arguments}");
        if (CloseCommandWorks && arguments == "/shutdown")
            Processes.RemoveAll(p => p.ExePath == fileName);
        return Task.FromResult(0);
    }
    public void LaunchUnelevated(string id, LaunchSpec spec) =>
        Calls.Add($"launch {id} {spec.AppId ?? spec.ExePath} {spec.Arguments}".TrimEnd());
    public Task DelayAsync(TimeSpan delay) => Task.CompletedTask;
}

public class GameModePlannerTests
{
    private static ProcessInfo P(int pid, int parent, string name, long mb = 100, string? path = null, string? cmd = null) =>
        new(pid, parent, name, path ?? $@"C:\Apps\{name}.exe", cmd, mb * 1024 * 1024);

    private static GameModeConfig Config(params GameModeItem[] items) => new() { Items = items.ToList() };

    [Fact]
    public void PlanEnter_App_CountsWholeProcessTreeIncludingWebView2Children()
    {
        var procs = new List<ProcessInfo>
        {
            P(1, 0, "explorer"),
            P(10, 1, "Grammarly.Desktop", 360, cmd: "\"C:\\Apps\\Grammarly.Desktop.exe\" --autostart"),
            P(11, 10, "Superhuman.WebUI", 20),
            P(12, 11, "msedgewebview2", 600),
            P(13, 12, "msedgewebview2", 25),
            P(20, 1, "msedgewebview2", 999), // someone else's WebView2
        };
        var item = new GameModeItem { Id = "g", DisplayName = "Grammarly", ProcessNames = ["Grammarly.Desktop"] };

        var plan = GameModePlanner.PlanEnter(Config(item), procs, _ => false);

        plan.Should().ContainSingle();
        plan[0].MemoryBytes.Should().Be((360L + 20 + 600 + 25) * 1024 * 1024);
        plan[0].Launch.Should().Be(new LaunchSpec(null, @"C:\Apps\Grammarly.Desktop.exe", "--autostart"));
    }

    [Fact]
    public void PlanEnter_SkipsItemsThatAreNotRunning()
    {
        var item = new GameModeItem { Id = "x", DisplayName = "X", ProcessNames = ["Granola"] };
        GameModePlanner.PlanEnter(Config(item), new List<ProcessInfo> { P(1, 0, "explorer") }, _ => false)
            .Should().BeEmpty();
    }

    [Fact]
    public void PlanEnter_PathContainsFiltersOutSameNamedProcesses()
    {
        var procs = new List<ProcessInfo>
        {
            P(5, 1, "claude", path: @"C:\Users\me\.local\bin\claude.exe"),
            P(6, 1, "claude", path: @"C:\Program Files\WindowsApps\Claude_2.1_x64__abc\app\Claude.exe"),
        };
        var item = new GameModeItem
        {
            Id = "claude", DisplayName = "Claude", ProcessNames = ["claude"],
            PathContains = @"\WindowsApps\Claude_", AppId = "Claude_abc!Claude",
        };

        var plan = GameModePlanner.PlanEnter(Config(item), procs, _ => false);

        plan.Should().ContainSingle();
        plan[0].MemoryBytes.Should().Be(100L * 1024 * 1024);
        plan[0].Launch.Should().Be(new LaunchSpec("Claude_abc!Claude", null, null));
    }

    [Fact]
    public void PlanEnter_NeverIncludesProtectedItems()
    {
        var procs = new List<ProcessInfo> { P(5, 1, "Discord"), P(6, 1, "ThrottleStop") };
        var cfg = Config(
            new GameModeItem { Id = "d", DisplayName = "Discord", ProcessNames = ["discord"] },
            new GameModeItem { Id = "t", DisplayName = "TS", ProcessNames = ["ThrottleStop"] },
            new GameModeItem { Id = "s", DisplayName = "Defender", Kind = GameModeItemKind.Service, ServiceName = "WinDefend" });

        GameModePlanner.PlanEnter(cfg, procs, _ => true).Should().BeEmpty();
    }

    [Fact]
    public void PlanEnter_ServiceAndCommandItems_UseDetectionAndMemoryProcesses()
    {
        var procs = new List<ProcessInfo> { P(7, 0, "vmmem", 4100), P(8, 0, "vmmemWSL", 2800) };
        var cfg = Config(
            new GameModeItem
            {
                Id = "wsl", DisplayName = "WSL", Kind = GameModeItemKind.Command, DetectProcessNames = ["vmmemWSL"],
                StopCommand = new("wsl.exe", "--shutdown"), StartCommand = new("schtasks.exe", "/run"),
            },
            new GameModeItem
            {
                Id = "vm", DisplayName = "Cowork", Kind = GameModeItemKind.Service,
                ServiceName = "CoworkVMService", MemoryProcessNames = ["vmmem"],
            },
            new GameModeItem { Id = "off", DisplayName = "Stopped svc", Kind = GameModeItemKind.Service, ServiceName = "Nope" });

        var plan = GameModePlanner.PlanEnter(cfg, procs, s => s == "CoworkVMService");

        plan.Select(p => p.Item.Id).Should().Equal("wsl", "vm");
        plan[0].MemoryBytes.Should().Be(2800L * 1024 * 1024);
        plan[1].MemoryBytes.Should().Be(4100L * 1024 * 1024);
    }

    [Fact]
    public void PlanEnter_RelaunchFalse_HasNoLaunchSpec()
    {
        var item = new GameModeItem { Id = "m", DisplayName = "M365", ProcessNames = ["M365Copilot"], Relaunch = false };
        GameModePlanner.PlanEnter(Config(item), new List<ProcessInfo> { P(3, 1, "M365Copilot") }, _ => false)
            .Single().Launch.Should().BeNull();
    }

    [Fact]
    public void PlanExit_SkipsItemsAlreadyRunningAndItemsNotRelaunched()
    {
        var granola = new GameModeItem { Id = "granola", DisplayName = "Granola", ProcessNames = ["Granola"] };
        var drive = new GameModeItem { Id = "drive", DisplayName = "Drive", ProcessNames = ["GoogleDriveFS"] };
        var m365 = new GameModeItem { Id = "m365", DisplayName = "M365", ProcessNames = ["M365Copilot"], Relaunch = false };
        var state = new GameModeState
        {
            Active = true,
            Stopped = { new(granola, new("com.granola.app", null, null)), new(drive, null), new(m365, null) },
        };

        var plan = GameModePlanner.PlanExit(state, new List<ProcessInfo> { P(4, 1, "GoogleDriveFS") }, _ => false);

        plan.Select(s => s.Item.Id).Should().Equal("granola");
    }

    [Theory]
    [InlineData("\"C:\\Program Files\\App\\a.exe\" --autostart", "--autostart")]
    [InlineData("\"C:\\Program Files\\App\\a.exe\"", null)]
    [InlineData("\"C:\\Program Files\\App\\a.exe\" ", null)]
    [InlineData("C:\\App\\a.exe /background", "/background")]
    [InlineData("C:\\App\\a.exe", null)]
    [InlineData(null, null)]
    [InlineData("\"C:\\a.exe\" -x \"quoted arg\"", "-x \"quoted arg\"")]
    public void ArgumentsFromCommandLine_StripsTheExe(string? cmd, string? expected) =>
        GameModePlanner.ArgumentsFromCommandLine(cmd).Should().Be(expected);
}

public class GameModeRunnerTests
{
    private static ProcessInfo P(int pid, int parent, string name, string? path = null) =>
        new(pid, parent, name, path ?? $@"C:\Apps\{name}.exe", null, 1);

    private static readonly GameModeItem Claude = new()
    {
        Id = "claude", DisplayName = "Claude", ProcessNames = ["claude"], AppId = "Claude!App",
    };
    private static readonly GameModeItem Wsl = new()
    {
        Id = "wsl", DisplayName = "WSL", Kind = GameModeItemKind.Command, DetectProcessNames = ["vmmemWSL"],
        StopCommand = new("wsl.exe", "--shutdown"), StartCommand = new("schtasks.exe", "/run /tn \"WSL AutoStart\""),
    };
    private static readonly GameModeItem Cowork = new()
    {
        Id = "vm", DisplayName = "Cowork", Kind = GameModeItemKind.Service, ServiceName = "CoworkVMService",
    };

    [Fact]
    public async Task Enter_ClosesAppsThenWslThenServices_AndExitRunsInReverse()
    {
        var sys = new FakeSystem();
        sys.Processes.AddRange([P(10, 1, "claude"), P(11, 10, "claude"), P(50, 0, "vmmemWSL")]);
        sys.RunningServices.Add("CoworkVMService");
        var runner = new GameModeRunner(sys);
        var plan = new List<PlannedItem>
        {
            new(Cowork, 0, null), new(Wsl, 0, null), new(Claude, 0, new LaunchSpec("Claude!App", null, null)),
        };

        var stopped = await runner.EnterAsync(plan, dryRun: false, _ => { });

        sys.Calls.Should().Equal("kill 10", "run wsl.exe --shutdown", "stop-svc CoworkVMService");
        sys.Processes.Should().NotContain(p => p.Name == "claude");
        stopped.Should().HaveCount(3);

        sys.Calls.Clear();
        await runner.ExitAsync(stopped, dryRun: false, _ => { });

        sys.Calls.Should().Equal(
            "start-svc CoworkVMService", "run schtasks.exe /run /tn \"WSL AutoStart\"", "launch claude Claude!App");
    }

    [Fact]
    public async Task Enter_DryRun_TouchesNothing()
    {
        var sys = new FakeSystem();
        sys.Processes.Add(P(10, 1, "claude"));
        var logs = new List<string>();

        var stopped = await new GameModeRunner(sys).EnterAsync(
            [new PlannedItem(Claude, 0, null)], dryRun: true, logs.Add);

        sys.Calls.Should().BeEmpty();
        stopped.Should().BeEmpty();
        logs.Should().ContainSingle().Which.Should().Contain("dry run");
    }

    [Fact]
    public async Task Enter_GracefulCloseCommand_RunsBeforeAnyKill()
    {
        var sys = new FakeSystem();
        var exe = @"C:\Program Files\Microsoft OneDrive\OneDrive.exe";
        sys.Processes.Add(P(30, 1, "OneDrive", exe));
        var onedrive = new GameModeItem { Id = "od", DisplayName = "OneDrive", ProcessNames = ["OneDrive"], CloseArgs = "/shutdown" };

        await new GameModeRunner(sys).EnterAsync([new PlannedItem(onedrive, 0, null)], false, _ => { });

        sys.Calls.Should().Equal("run OneDrive.exe /shutdown");
    }

    [Fact]
    public async Task Enter_GracefulCloseIgnored_FallsBackToKill()
    {
        var sys = new FakeSystem { CloseCommandWorks = false };
        sys.Processes.Add(P(30, 1, "OneDrive", @"C:\OD\OneDrive.exe"));
        var onedrive = new GameModeItem { Id = "od", DisplayName = "OneDrive", ProcessNames = ["OneDrive"], CloseArgs = "/shutdown" };

        await new GameModeRunner(sys).EnterAsync([new PlannedItem(onedrive, 0, null)], false, _ => { });

        sys.Calls.Should().Equal("run OneDrive.exe /shutdown", "kill 30");
    }

    [Fact]
    public async Task Exit_AppWithoutLaunchInfo_IsReportedNotThrown()
    {
        var sys = new FakeSystem();
        var logs = new List<string>();

        await new GameModeRunner(sys).ExitAsync([new StoppedItem(Claude, null)], false, logs.Add);

        sys.Calls.Should().BeEmpty();
        logs.Should().ContainSingle().Which.Should().StartWith("FAILED to start Claude");
    }
}

public class GameModeServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pcmon-gm-" + Guid.NewGuid());
    private string ConfigPath => Path.Combine(_dir, "gamemode.json");
    private string StatePath => Path.Combine(_dir, "gamemode-state.json");

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public void LoadConfig_WritesDefaultsOnFirstRun_AndReadsThemBack()
    {
        var svc = new GameModeService(new FakeSystem(), ConfigPath, StatePath);

        var first = svc.LoadConfig();
        File.Exists(ConfigPath).Should().BeTrue();
        File.ReadAllText(ConfigPath).Should().Contain("\"Kind\": \"Service\"");
        var second = svc.LoadConfig();

        second.Items.Select(i => i.Id).Should().Equal(first.Items.Select(i => i.Id));
        second.Items.Single(i => i.Id == "wsl").StopCommand.Should().Be(new CommandSpec("wsl.exe", "--shutdown"));
        svc.ConfigError.Should().BeNull();
    }

    [Fact]
    public void LoadConfig_BrokenJson_FallsBackToDefaultsWithError()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(ConfigPath, "{ not json");
        var svc = new GameModeService(new FakeSystem(), ConfigPath, StatePath);

        svc.LoadConfig().Items.Should().NotBeEmpty();
        svc.ConfigError.Should().Contain("gamemode.json");
    }

    [Fact]
    public async Task EnterThenExit_PersistsStateAcrossRestart()
    {
        var sys = new FakeSystem();
        sys.Processes.Add(new ProcessInfo(10, 1, "Granola", @"C:\G\Granola.exe", null, 1));
        Directory.CreateDirectory(_dir);
        File.WriteAllText(ConfigPath,
            """{ "Items": [ { "Id": "granola", "DisplayName": "Granola", "ProcessNames": ["Granola"], "AppId": "com.granola.app" } ] }""");
        var svc = new GameModeService(sys, ConfigPath, StatePath);
        var changed = 0;
        svc.StateChanged += (_, _) => changed++;

        await svc.EnterAsync(svc.PlanEnter(), dryRun: false, _ => { });

        var reloaded = new GameModeService(sys, ConfigPath, StatePath);
        reloaded.IsActive.Should().BeTrue();
        reloaded.PlanExit().Select(s => s.Item.Id).Should().Equal("granola");

        await reloaded.ExitAsync(reloaded.PlanExit(), dryRun: false, _ => { });
        reloaded.IsActive.Should().BeFalse();
        new GameModeService(sys, ConfigPath, StatePath).IsActive.Should().BeFalse();
        sys.Calls.Should().Equal("kill 10", "launch granola com.granola.app");
        changed.Should().Be(1);
    }

    [Fact]
    public async Task DryRun_DoesNotChangeState()
    {
        var sys = new FakeSystem();
        sys.Processes.Add(new ProcessInfo(10, 1, "Granola", @"C:\G\Granola.exe", null, 1));
        var svc = new GameModeService(sys, ConfigPath, StatePath);

        await svc.EnterAsync(svc.PlanEnter(), dryRun: true, _ => { });

        svc.IsActive.Should().BeFalse();
        File.Exists(StatePath).Should().BeFalse();
    }
}

public class GameModeBlankAppIdTests
{
    [Fact]
    public void PlanEnter_BlankAppId_FallsBackToCapturedExe()
    {
        var item = new GameModeItem { Id = "g", DisplayName = "G", ProcessNames = ["Granola"], AppId = " " };
        var procs = new List<ProcessInfo> { new(3, 1, "Granola", @"C:\G\Granola.exe", "\"C:\\G\\Granola.exe\" --hidden", 1) };

        GameModePlanner.PlanEnter(new GameModeConfig { Items = { item } }, procs, _ => false)
            .Single().Launch.Should().Be(new LaunchSpec(null, @"C:\G\Granola.exe", "--hidden"));
    }
}
