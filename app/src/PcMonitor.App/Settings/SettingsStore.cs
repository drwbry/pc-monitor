using System.Text.Json;
using PcMonitor.App.Composition;

namespace PcMonitor.App.Settings;

public sealed class SettingsStore
{
    private readonly string _path;
    public UserSettings Current { get; private set; } = new();

    public SettingsStore() : this(Paths.SettingsFile) { }

    public SettingsStore(string path)
    {
        _path = path;
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_path))
                Current = JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(_path)) ?? new();
        }
        catch
        {
            Current = new UserSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(Current,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
