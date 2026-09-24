using System.Windows;
using System.Windows.Media;

using HouseBills.Domain;

namespace HouseBills.Wpf.Theming;

/// <summary>
/// Bill status colors for light and dark backgrounds. The active set is put into the application resources as
/// <c>Status{Status}Brush</c> (e.g. <c>StatusOverdueBrush</c>); XAML uses them as DynamicResource, so a theme switch
/// updates open pages.
/// </summary>
public static class StatusColors
{
    /// <summary>On light backgrounds: darker shades, at least 4.5:1 contrast (tested).</summary>
    public static readonly IReadOnlyDictionary<BillStatus, Color> Light = new Dictionary<BillStatus, Color>
    {
        [BillStatus.Paid] = Color.FromRgb(0x2E, 0x7D, 0x32),
        [BillStatus.Overdue] = Color.FromRgb(0xC6, 0x28, 0x28),
        [BillStatus.DueSoon] = Color.FromRgb(0xB2, 0x4A, 0x00),
        [BillStatus.Upcoming] = Color.FromRgb(0x50, 0x65, 0x70),
    };

    /// <summary>On dark backgrounds: lighter shades of the same hues, at least 4.5:1 contrast (tested).</summary>
    public static readonly IReadOnlyDictionary<BillStatus, Color> Dark = new Dictionary<BillStatus, Color>
    {
        [BillStatus.Paid] = Color.FromRgb(0x81, 0xC7, 0x84),
        [BillStatus.Overdue] = Color.FromRgb(0xFF, 0x7B, 0x72),
        [BillStatus.DueSoon] = Color.FromRgb(0xFF, 0xB7, 0x4D),
        [BillStatus.Upcoming] = Color.FromRgb(0xB0, 0xBE, 0xC5),
    };

    public static string BrushKey(BillStatus status) => $"Status{status}Brush";

    /// <summary>Puts the light or dark set into <paramref name="resources"/>, replacing the previous brushes.</summary>
    public static void Apply(ResourceDictionary resources, bool dark)
    {
        foreach (var (status, color) in dark ? Dark : Light)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            resources[BrushKey(status)] = brush;
        }
    }
}