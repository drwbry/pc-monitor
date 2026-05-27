using System.Text.Json;
using PcMonitor.Core.Models;

namespace PcMonitor.Core.History;

public static class HourlyJsonParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static HourlyEntry? Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<HourlyEntry>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
