using System.Windows.Media;

using SkiaSharp;

namespace HouseBills.Wpf.Charts;

/// <summary>Takes chart colors from the active Fluent theme resources, so charts follow light and dark mode.</summary>
internal sealed class FluentChartColors : IChartColors
{
    public SKColor Text => FromResource("TextFillColorPrimaryBrush", SKColors.Gray);

    public SKColor Gridlines => FromResource("ControlStrokeColorDefaultBrush", SKColors.LightGray);

    public SKColor TooltipBackground => FromResource("SolidBackgroundFillColorTertiaryBrush", SKColors.White);

    private static SKColor FromResource(string key, SKColor fallback)
    {
        return System.Windows.Application.Current?.TryFindResource(key) is SolidColorBrush { Color: var c }
            ? new SKColor(c.R, c.G, c.B, c.A)
            : fallback;
    }
}