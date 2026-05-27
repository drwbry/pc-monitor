using System.Windows;
using PcMonitor.App.ViewModels;
using PcMonitor.Core.Capture;
using PcMonitor.Core.Models;

namespace PcMonitor.App.Views.Dialogs;

public partial class CaptureDialog : Window
{
    private readonly CaptureDialogViewModel _vm;

    public CaptureDialog(ICaptureService capture, CaptureKind kind)
    {
        InitializeComponent();
        _vm = new CaptureDialogViewModel(capture, kind);
        DataContext = _vm;
        Loaded += async (_, _) => await _vm.RunAsync();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
