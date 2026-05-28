using System.ComponentModel;
using System.Reflection;
using System.Windows;
using PcMonitor.App.ViewModels;
using PcMonitor.App.Views.Dialogs;
using PcMonitor.Core.Models;

namespace PcMonitor.App.Views;

public partial class CockpitWindow : Window
{
    public CockpitWindow()
    {
        InitializeComponent();
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "?";
        Title = $"Marsh PC Monitor v{version}";
        var svc = ((App)Application.Current).Services!;
        var vm = new CockpitViewModel(svc);
        DataContext = vm;
        vm.CaptureRequested += (_, kind) => OpenCaptureDialog(kind, vm, svc);
        Closed += (_, _) => vm.Dispose();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
            HideToTray();
        base.OnStateChanged(e);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        HideToTray();
    }

    private void HideToTray()
    {
        Hide();
        ShowInTaskbar = false;
    }

    private void OpenCaptureDialog(CaptureKind kind, CockpitViewModel vm, Composition.Services svc)
    {
        vm.CaptureRunning = true;
        var dialog = new CaptureDialog(svc.Capture, kind) { Owner = this };
        dialog.Closed += (_, _) => vm.CaptureRunning = false;
        dialog.Show();
    }
}
