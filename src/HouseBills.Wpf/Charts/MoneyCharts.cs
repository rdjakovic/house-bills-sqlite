using System.Globalization;

using HouseBills.Wpf.Localization;

using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Drawing.Geometries;
using LiveChartsCore.SkiaSharpView.Painting;

using SkiaSharp;

namespace HouseBills.Wpf.Charts;

/// <summary>Chart parts shared by the pages that chart amounts, formatted in the chosen language.</summary>
public static class MoneyCharts
{
    /// <summary>A month axis with abbreviated month names in the UI language ("Jan", "Feb"…).</summary>
    public static Axis MonthAxis(IEnumerable<int> months, SolidColorPaint text)
    {
        var culture = LocalizedStrings.Culture;
        return new Axis
        {
            Labels = months.Select(m => Capitalize(culture.DateTimeFormat.GetAbbreviatedMonthName(m), culture)).ToArray(),
            LabelsPaint = text,
            TextSize = 12,
        };
    }

    /// <summary>A value axis starting at zero, labelled with whole amounts ("30.000 RSD").</summary>
    public static Axis AmountAxis(SolidColorPaint text, SKColor gridlines)
    {
        return new Axis
        {
            MinLimit = 0,
            Labeler = value => Format((decimal)value, "C0"),
            LabelsPaint = text,
            SeparatorsPaint = new SolidColorPaint(gridlines) { StrokeThickness = 1 },
            TextSize = 12,
        };
    }

    /// <summary>Columns of amounts; the tooltip shows the amount, or <paramref name="tooltip"/>'s text for the column.</summary>
    public static ColumnSeries<double> Columns(
        string name,
        IEnumerable<decimal> amounts,
        SKColor color,
        Func<ChartPoint<double, RoundedRectangleGeometry, LabelGeometry>, string>? tooltip = null)
    {
        return new ColumnSeries<double>
        {
            Name = name,
            Values = amounts.Select(a => (double)a).ToArray(),
            Fill = new SolidColorPaint(color),
            MaxBarWidth = 18,
            Padding = 2,
            YToolTipLabelFormatter = tooltip ?? (point => Format((decimal)point.Model, "C")),
        };
    }

    /// <summary>An amount in the chosen language's money format.</summary>
    public static string Format(decimal amount, string format = "C") => amount.ToString(format, LocalizedStrings.FormattingCulture);

    /// <summary>Month names are lowercase in Serbian; capitalize them for labels.</summary>
    public static string Capitalize(string text, CultureInfo culture) =>
        text.Length == 0 ? text : culture.TextInfo.ToUpper(text[0]) + text[1..];
}