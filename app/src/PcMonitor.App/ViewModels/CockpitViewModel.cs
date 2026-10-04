using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcMonitor.App.Composition;
using PcMonitor.App.Theming;
using PcMonitor.Core.Models;

namespace PcMonitor.App.ViewModels;

public partial class CockpitViewModel : ObservableObject, IDisposable
{
    private readonly Services _svc;
    private readonly DispatcherTimer _timer;

    public LiveTilesViewModel Live { get; }
    public SparklineViewModel Sparkline { get; }
    public ObservableCollection<IssueCardViewModel> Issues { get; } = new();

    [ObservableProperty] private string _healthLabel = "All clear";
    [ObservableProperty] private System.Windows.Media.Brush _healthBrush = Palette.Good;
    [ObservableProperty] private System.Windows.Media.Brush _healthTint = Palette.GoodTint;
    [ObservableProperty] private bool _gameModeActive;
    [ObservableProperty] private bool _hasIssues;

    public string VersionText { get; } =
        "v" + (System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "?");
    [ObservableProperty] private bool _explainerCollapsed;
    [ObservableProperty] private bool _captureRunning;
    [ObservableProperty] private string? _tempBanner;
    [ObservableProperty] private string _gameModeButtonText = "Game Mode";

    public IRelayCommand<string> CaptureCommand { get; }
    public IRelayCommand ToggleExplainerCommand { get; }
    public IRelayCommand GameModeCommand { get; }

    public event EventHandler<CaptureKind>? CaptureRequested;
    public event EventHandler? GameModeRequested;

    public CockpitViewModel(Services svc)
    {
        _svc = svc;
        Live = new LiveTilesViewModel();
        Sparkline = new SparklineViewModel(svc.History);
        ExplainerCollapsed = svc.Settings.Current.ExplainerCollapsed;
        if (!svc.Sensors.TempSensorsAvailable)
            TempBanner = "Temperature sensors unavailable (LibreHardwareMonitor could not load). Temp tile and thermal rules are disabled.";

        CaptureCommand = new RelayCommand<string>(kind =>
        {
            if (kind == "Diagnostic") CaptureRequested?.Invoke(this, CaptureKind.Diagnostic);
            else if (kind == "LiveProbe") CaptureRequested?.Invoke(this, CaptureKind.LiveProbe);
        }, _ => !CaptureRunning);

        ToggleExplainerCommand = new RelayCommand(() =>
        {
            ExplainerCollapsed = !ExplainerCollapsed;
            svc.Settings.Current.ExplainerCollapsed = ExplainerCollapsed;
            svc.Settings.Save();
        });

        GameModeCommand = new RelayCommand(() => GameModeRequested?.Invoke(this, EventArgs.Empty));
        svc.GameMode.StateChanged += OnGameModeChanged;
        UpdateGameModeText();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    private void Tick()
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var snap = _svc.Sensors.Read(now);
            Live.Apply(snap);
            var active = _svc.Issues.Evaluate(snap);

            SyncIssues(active, now);

            if (active.Any(i => i.Severity == IssueSeverity.Red))
                SetHealth("Problems", Palette.Bad, Palette.BadTint);
            else if (active.Any(i => i.Severity == IssueSeverity.Yellow))
                SetHealth("Issues", Palette.Warn, Palette.WarnTint);
            else
                SetHealth("All clear", Palette.Good, Palette.GoodTint);
        }
        catch (Exception ex)
        {
            try
            {
                Directory.CreateDirectory(Paths.AppDataFolder);
                File.AppendAllText(Paths.LogFile, $"{DateTime.UtcNow:o} tick error: {ex}\n");
            }
            catch { }
        }
    }

    private void SetHealth(string label, System.Windows.Media.Brush brush, System.Windows.Media.Brush tint)
    {
        HealthLabel = label;
        HealthBrush = brush;
        HealthTint = tint;
    }

    /// <summary>Keeps existing cards when the same issues are still active; only durations change.</summary>
    private void SyncIssues(IReadOnlyList<IssueState> active, DateTimeOffset now)
    {
        var same = active.Count == Issues.Count && active.Select((a, i) => Issues[i].Matches(a)).All(m => m);
        if (same)
        {
            for (var i = 0; i < active.Count; i++) Issues[i].Update(active[i], now);
        }
        else
        {
            Issues.Clear();
            foreach (var i in active) Issues.Add(new IssueCardViewModel(i, now));
        }
        HasIssues = Issues.Count > 0;
    }

    partial void OnCaptureRunningChanged(bool value) =>
        CaptureCommand.NotifyCanExecuteChanged();

    private void OnGameModeChanged(object? sender, EventArgs e) =>
        System.Windows.Application.Current.Dispatcher.Invoke(UpdateGameModeText);

    private void UpdateGameModeText()
    {
        GameModeActive = _svc.GameMode.IsActive;
        GameModeButtonText = GameModeActive ? "End Game Mode" : "Game Mode";
    }

    public void Dispose()
    {
        _timer.Stop();
        _svc.GameMode.StateChanged -= OnGameModeChanged;
    }
}
