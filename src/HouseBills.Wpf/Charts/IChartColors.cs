using SkiaSharp;

namespace HouseBills.Wpf.Charts;

/// <summary>Theme-dependent chart colors, read when a chart is built so it matches the current light/dark theme.</summary>
public interface IChartColors
{
    /// <summary>Axis labels, legend and tooltip text.</summary>
    SKColor Text { get; }

    /// <summary>Axis separator lines.</summary>
    SKColor Gridlines { get; }

    /// <summary>Tooltip background.</summary>
    SKColor TooltipBackground { get; }
}