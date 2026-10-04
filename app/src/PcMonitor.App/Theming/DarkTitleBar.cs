using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PcMonitor.App.Theming;

/// <summary>Makes the native title bar dark (and, on Windows 11, the same color as the window).</summary>
public static class DarkTitleBar
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaCaptionColor = 35;
    // COLORREF is 0x00BBGGRR: #0B0F14 -> 0x00140F0B
    private const int CaptionColor = 0x00140F0B;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>Call from the constructor; it applies once the window handle exists.</summary>
    public static void Attach(Window window) =>
        window.SourceInitialized += (_, _) =>
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                var on = 1;
                DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref on, sizeof(int));
                var color = CaptionColor;
                DwmSetWindowAttribute(hwnd, DwmwaCaptionColor, ref color, sizeof(int)); // no-op before Win11
            }
            catch { /* cosmetic only */ }
        };
}
