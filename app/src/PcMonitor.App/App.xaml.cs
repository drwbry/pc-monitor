using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Windows;
using PcMonitor.App.Composition;

namespace PcMonitor.App;

public partial class App : Application
{
    private const string MutexName = @"Global\MarshPcMonitor.SingleInstance";
    private const string PipeName = "MarshPcMonitor.Activate";
    private Mutex? _mutex;
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
        _ = Task.Run(ActivationListener);
        base.OnStartup(e);
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
                if (msg == "activate")
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (MainWindow is { } w)
                        {
                            if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
                            w.Activate();
                            w.Topmost = true;
                            w.Topmost = false;
                        }
                    });
                }
            }
            catch { }
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Services?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
