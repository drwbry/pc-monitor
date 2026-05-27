namespace PcMonitor.App.Composition;

public static class Paths
{
    public static string SysLogsRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SysLogs");
    public static string HourlyFolder => Path.Combine(SysLogsRoot, "hourly");
    public static string ScriptsFolder => Path.Combine(SysLogsRoot, "scripts");
    public static string AppDataFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PcMonitor");
    public static string LogFile => Path.Combine(AppDataFolder, "log.txt");
    public static string SettingsFile => Path.Combine(AppDataFolder, "settings.json");
}
