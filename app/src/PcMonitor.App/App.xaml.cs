using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using PcMonitor.App.Composition;

namespace PcMonitor.App;

public partial class App : System.Windows.Application
{
    private const string MutexName = @"Global\MarshPcMonitor.SingleInstance";
    private const string PipeName = "MarshPcMonitor.Activate";
    private Mutex? _mutex;
    private NotifyIcon? _trayIcon;
    public Services? Services { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, MutexName, out var isFirst);
        if (!isFirst)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                client.Connect(500);
                using var w = new StreamWriter(client);
                w.WriteLine("activate");
            }
            catch { }
            Shutdown();
            return;
        }

        Services = new Services();
        InitTrayIcon();
        _ = Task.Run(ActivationListener);
        base.OnStartup(e);
    }

    private void InitTrayIcon()
    {
        var exePath = Process.GetCurrentProcess().MainModule?.FileName;
        var icon = exePath is not null ? Icon.ExtractAssociatedIcon(exePath) : SystemIcons.Application;

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => ShowMainWindow());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Shutdown());

        _trayIcon = new NotifyIcon
        {
            Icon = icon,
            Text = "Marsh PC Monitor",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _trayIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    public void ShowMainWindow()
    {
        Dispatcher.Invoke(() =>
        {
            if (MainWindow is { } w)
            {
                w.Show();
                w.ShowInTaskbar = true;
                if (w.WindowState == WindowState.Minimized)
                    w.WindowState = WindowState.Normal;
                w.Activate();
                w.Topmost = true;
                w.Topmost = false;
            }
        });
    }

    private async Task ActivationListener()
    {
        while (true)
        {
            try
            {
                using var server = new NamedPipeServerStream(PipeName, PipeDirection.In);
                await server.WaitForConnectionAsync();
                using var r = new StreamReader(server);
                var msg = await r.ReadLineAsync();
                if (msg == "activate") ShowMainWindow();
            }
            catch { }
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        Services?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
