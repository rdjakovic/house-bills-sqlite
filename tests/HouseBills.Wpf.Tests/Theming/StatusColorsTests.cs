using System.Windows.Media;

using HouseBills.Domain;
using HouseBills.Wpf.Theming;

namespace HouseBills.Wpf.Tests.Theming;

public sealed class StatusColorsTests
{
    // Fluent window background and card (layer) colors.
    private static readonly Color[] LightBackgrounds = [Color.FromRgb(0xF3, 0xF3, 0xF3), Color.FromRgb(0xFB, 0xFB, 0xFB)];
    private static readonly Color[] DarkBackgrounds = [Color.FromRgb(0x20, 0x20, 0x20), Color.FromRgb(0x2B, 0x2B, 0x2B)];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Colors_EveryStatus_HasReadableContrastOnItsBackgrounds(bool dark)
    {
        var colors = dark ? StatusColors.Dark : StatusColors.Light;
        var backgrounds = dark ? DarkBackgrounds : LightBackgrounds;

        colors.Keys.Order().ShouldBe(Enum.GetValues<BillStatus>());
        foreach (var (status, color) in colors)
        {
            foreach (var background in backgrounds)
            {
                ContrastRatio(color, background).ShouldBeGreaterThanOrEqualTo(4.5, $"{status} on {background} ({(dark ? "dark" : "light")})");
            }
        }
    }

    [Fact]
    public void Apply_Dark_ReplacesBrushesUnderTheirKeys()
    {
        var resources = new System.Windows.ResourceDictionary();

        StatusColors.Apply(resources, dark: false);
        StatusColors.Apply(resources, dark: true);

        ((SolidColorBrush)resources["StatusOverdueBrush"]).Color.ShouldBe(StatusColors.Dark[BillStatus.Overdue]);
        resources.Count.ShouldBe(4);
    }

    /// <summary>WCAG 2 contrast ratio.</summary>
    private static double ContrastRatio(Color a, Color b)
    {
        var (lighter, darker) = (Luminance(a), Luminance(b)) is var (x, y) && x > y ? (x, y) : (y, x);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double Luminance(Color c)
    {
        static double Channel(byte value)
        {
            var s = value / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(c.R)) + (0.7152 * Channel(c.G)) + (0.0722 * Channel(c.B));
    }
}