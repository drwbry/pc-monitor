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
        var svc = ((App)Application.Current).Services!;
        var vm = new CockpitViewModel(svc);
        DataContext = vm;
        vm.CaptureRequested += (_, kind) => OpenCaptureDialog(kind, vm, svc);
        Closed += (_, _) => vm.Dispose();
    }

    private void OpenCaptureDialog(CaptureKind kind, CockpitViewModel vm, Composition.Services svc)
    {
        vm.CaptureRunning = true;
        var dialog = new CaptureDialog(svc.Capture, kind) { Owner = this };
        dialog.Closed += (_, _) => vm.CaptureRunning = false;
        dialog.Show();
    }
}
