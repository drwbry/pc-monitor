namespace PcMonitor.App.Settings;

public sealed class UserSettings
{
    public bool ExplainerCollapsed { get; set; } = false;
    public DateTimeOffset? LastLaunch { get; set; }
}
