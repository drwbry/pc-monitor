using System.Windows;
using System.Windows.Media;

namespace PcMonitor.App.Theming;

/// <summary>A sparkline as two frozen point sets (line + filled area) in a fixed Width x Height box.</summary>
public sealed record SparkPoints(PointCollection Line, PointCollection Area)
{
    public const double Width = 220;
    public const double Height = 36;

    public static readonly SparkPoints Empty = Build([], 0, 1);

    /// <param name="min">Bottom of the scale. Pass a fixed value (e.g. 0) so small wiggles stay small.</param>
    /// <param name="max">Top of the scale; widened to fit the data if needed.</param>
    /// <param name="capacity">Slots across the width. With fewer values the line is right-aligned and
    /// grows leftward (a live chart filling up) instead of stretching. Defaults to the value count.</param>
    public static SparkPoints Build(IReadOnlyList<double> values, double min, double max, int capacity = 0)
    {
        var line = new PointCollection(values.Count);
        var area = new PointCollection(values.Count + 2);
        if (values.Count > 0)
        {
            max = Math.Max(max, values.Max());
            min = Math.Min(min, values.Min());
            var range = Math.Max(1e-6, max - min);
            var slots = Math.Max(capacity, values.Count);
            var step = slots > 1 ? Width / (slots - 1) : 0;
            var x0 = (slots - values.Count) * step;
            area.Add(new Point(x0, Height));
            for (var i = 0; i < values.Count; i++)
            {
                // Keep a 1px margin so the stroke isn't clipped at the top and bottom.
                var y = 1 + (Height - 2) * (1 - (values[i] - min) / range);
                var p = new Point(x0 + i * step, y);
                line.Add(p);
                area.Add(p);
            }
            area.Add(new Point(x0 + (values.Count - 1) * step, Height));
        }
        line.Freeze();
        area.Freeze();
        return new SparkPoints(line, area);
    }
}
