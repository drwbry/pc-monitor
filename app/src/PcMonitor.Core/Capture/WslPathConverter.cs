namespace PcMonitor.Core.Capture;

public static class WslPathConverter
{
    public static string? ToWsl(string? windowsPath)
    {
        if (string.IsNullOrEmpty(windowsPath)) return null;
        if (windowsPath.Length < 2 || windowsPath[1] != ':') return null;
        var drive = char.ToLowerInvariant(windowsPath[0]);
        if (drive < 'a' || drive > 'z') return null;
        var rest = windowsPath[2..].Replace('\\', '/');
        if (!rest.StartsWith('/')) rest = "/" + rest;
        return $"/mnt/{drive}{rest}";
    }
}
