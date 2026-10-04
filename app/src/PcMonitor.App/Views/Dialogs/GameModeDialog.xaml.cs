using System.Windows;
using PcMonitor.App.ViewModels;
using PcMonitor.Core.GameMode;

namespace PcMonitor.App.Views.Dialogs;

public partial class GameModeDialog : Window
{
    public GameModeDialog(GameModeService svc)
    {
        InitializeComponent();
        var vm = new GameModeDialogViewModel(svc);
        DataContext = vm;
        Loaded += async (_, _) => await vm.LoadAsync();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
