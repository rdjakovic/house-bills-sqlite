using SkiaSharp;

namespace HouseBills.Wpf.Charts;

/// <summary>Series colors that stay distinguishable on both light and dark backgrounds.</summary>
public static class ChartPalette
{
    /// <summary>The comparison series (previous year): neutral, so the current year stands out.</summary>
    public static readonly SKColor Comparison = new(0x9E, 0x9E, 0x9E);

    private static readonly SKColor[] Colors =
    [
        new(0x88, 0x5E, 0xD6), // violet (close to the app's accent)
        new(0x2E, 0x9C, 0xCA), // blue
        new(0xF2, 0x9E, 0x4C), // orange
        new(0x4C, 0xAF, 0x50), // green
        new(0xE5, 0x5B, 0x7C), // pink
        new(0xF4, 0xD0, 0x3F), // yellow
        new(0x26, 0xA6, 0x9A), // teal
        new(0xA1, 0x88, 0x7F), // brown
        new(0x5C, 0x6B, 0xC0), // indigo
        new(0xC0, 0xCA, 0x33), // lime
    ];

    /// <summary>The color for the series at <paramref name="index"/>; colors repeat after ten series.</summary>
    public static SKColor At(int index) => Colors[index % Colors.Length];
}