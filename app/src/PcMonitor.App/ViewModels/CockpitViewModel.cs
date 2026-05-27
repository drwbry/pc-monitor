using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcMonitor.App.Composition;
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
    [ObservableProperty] private System.Windows.Media.Brush _healthBrush =
        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3F, 0xB9, 0x50));
    [ObservableProperty] private bool _explainerCollapsed;
    [ObservableProperty] private bool _captureRunning;
    [ObservableProperty] private string? _tempBanner;

    public IRelayCommand<string> CaptureCommand { get; }
    public IRelayCommand ToggleExplainerCommand { get; }

    public event EventHandler<CaptureKind>? CaptureRequested;

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

            Issues.Clear();
            foreach (var i in active) Issues.Add(new IssueCardViewModel(i, now));

            if (active.Any(i => i.Severity == IssueSeverity.Red))
            {
                HealthLabel = "Problems";
                HealthBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xF8, 0x51, 0x49));
            }
            else if (active.Any(i => i.Severity == IssueSeverity.Yellow))
            {
                HealthLabel = "Issues";
                HealthBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xD2, 0x99, 0x22));
            }
            else
            {
                HealthLabel = "All clear";
                HealthBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x3F, 0xB9, 0x50));
            }
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

    public void Dispose() => _timer.Stop();
}
