using System.Windows.Media;

namespace PcMonitor.App.Theming;

/// <summary>
/// Brushes that code picks at runtime (health, meter severity). Created once and frozen so the
/// 1-second tick never allocates brushes. Values mirror the tokens in Views/Theme.xaml.
/// </summary>
public static class Palette
{
    public static readonly Brush Good = Make(0x3F, 0xB9, 0x50);
    public static readonly Brush Warn = Make(0xD2, 0x99, 0x22);
    public static readonly Brush Bad = Make(0xF8, 0x51, 0x49);
    public static readonly Brush Muted = Make(0x8B, 0x98, 0xA8);
    public static readonly Brush Cpu = Make(0x58, 0xA6, 0xFF);
    public static readonly Brush Ram = Make(0x39, 0xC5, 0xBB);

    public static readonly Brush GoodTint = Make(0x3F, 0xB9, 0x50, 0x26);
    public static readonly Brush WarnTint = Make(0xD2, 0x99, 0x22, 0x26);
    public static readonly Brush BadTint = Make(0xF8, 0x51, 0x49, 0x26);

    /// <summary>Normal color until warn, amber until bad, then red.</summary>
    public static Brush ForLevel(double? value, double warn, double bad, Brush normal) =>
        value is not double v ? Muted : v >= bad ? Bad : v >= warn ? Warn : normal;

    private static Brush Make(byte r, byte g, byte b, byte a = 0xFF)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }
}
