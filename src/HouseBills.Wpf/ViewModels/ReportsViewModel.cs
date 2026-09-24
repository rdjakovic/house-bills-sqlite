using System.Collections.ObjectModel;
using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HouseBills.Application.Common;
using HouseBills.Application.Reports;
using HouseBills.Presentation.Resources;
using HouseBills.Wpf.Charts;
using HouseBills.Wpf.Export;
using HouseBills.Wpf.Localization;
using HouseBills.Wpf.Services;
using HouseBills.Wpf.ViewModels.Reports;

using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;

using Microsoft.Extensions.Logging;

namespace HouseBills.Wpf.ViewModels;

public sealed partial class ReportsViewModel : PageViewModel
{
    private const int YearsBack = 5;

    private readonly IReportQueries _reports;
    private readonly IChartColors _chartColors;
    private readonly IFileSaver _files;

    public ReportsViewModel(IReportQueries reports, IClock clock, IChartColors chartColors, IFileSaver files, IDialogService dialogs, ILogger<ReportsViewModel> logger)
        : base(dialogs, logger)
    {
        _reports = reports;
        _chartColors = chartColors;
        _files = files;
        var currentYear = clock.Today.Year;
        Years = Enumerable.Range(currentYear - YearsBack, YearsBack + 2).Reverse().ToList();
        SelectedYear = currentYear;
    }

    public override string Title => Strings.Page_Reports;

    public IReadOnlyList<int> Years { get; }

    public ObservableCollection<MonthlySummaryItem> Months { get; } = [];

    public ObservableCollection<CategoryTotalRow> CategoryTotals { get; } = [];

    [ObservableProperty]
    public partial int SelectedYear { get; set; }

    [ObservableProperty]
    public partial decimal YearTotal { get; set; }

    [ObservableProperty]
    public partial decimal YearPaid { get; set; }

    [ObservableProperty]
    public partial decimal YearOutstanding { get; set; }

    [ObservableProperty]
    public partial decimal PreviousYearTotal { get; set; }

    /// <summary>Month by month: the previous year next to the selected year.</summary>
    [ObservableProperty]
    public partial ISeries[] MonthSeries { get; set; } = [];

    [ObservableProperty]
    public partial Axis[] MonthXAxes { get; set; } = [];

    [ObservableProperty]
    public partial Axis[] MonthYAxes { get; set; } = [];

    /// <summary>One slice per category of the selected year.</summary>
    [ObservableProperty]
    public partial ISeries[] CategorySeries { get; set; } = [];

    /// <summary>Legend and tooltip text; built with the charts so it matches the current theme.</summary>
    [ObservableProperty]
    public partial SolidColorPaint? ChartTextPaint { get; set; }

    [ObservableProperty]
    public partial SolidColorPaint? TooltipBackgroundPaint { get; set; }

    /// <summary><c>false</c> when neither the selected nor the previous year has bills (the charts would be empty).</summary>
    [ObservableProperty]
    public partial bool HasChartData { get; set; }

    /// <summary><c>false</c> when the selected year has no bills (the category chart would be empty).</summary>
    [ObservableProperty]
    public partial bool HasCategoryData { get; set; }

    public override Task OnNavigatedToAsync()
    {
        return RefreshAsync(CancellationToken.None);
    }

    [RelayCommand]
    private Task RefreshAsync(CancellationToken cancellationToken)
    {
        return RunAsync(() => LoadAsync(SelectedYear, cancellationToken), Strings.Reports_LoadFailed);
    }

    /// <summary>Exports the shown year (by month and by category) to a CSV file.</summary>
    [RelayCommand]
    private Task ExportAsync(CancellationToken cancellationToken) =>
        ExportCsvAsync(_files, $"HouseBills-{SelectedYear.ToString(CultureInfo.InvariantCulture)}.csv", () => CsvExports.Report(Months, CategoryTotals), cancellationToken);

    private async Task LoadAsync(int year, CancellationToken cancellationToken)
    {
        var months = await _reports.GetMonthlySummaryAsync(year, cancellationToken);
        var categories = await _reports.GetCategoryTotalsAsync(new DateOnly(year, 1, 1), new DateOnly(year, 12, 31), cancellationToken);

        var max = months.Count == 0 ? 0m : months.Max(m => m.TotalAmount);
        // Month names follow the UI language (Serbian names are lowercase, so capitalize for the table).
        var uiCulture = LocalizedStrings.Culture;
        Months.Clear();
        foreach (var month in months)
        {
            var fraction = max == 0m ? 0d : (double)(month.TotalAmount / max);
            Months.Add(new MonthlySummaryItem(MoneyCharts.Capitalize(uiCulture.DateTimeFormat.GetMonthName(month.Month), uiCulture), month, fraction));
        }

        CategoryTotals.Clear();
        foreach (var category in categories)
        {
            CategoryTotals.Add(category);
        }

        YearTotal = months.Sum(m => m.TotalAmount);
        YearPaid = months.Sum(m => m.PaidAmount);
        YearOutstanding = months.Sum(m => m.OutstandingAmount);
        PreviousYearTotal = months.Sum(m => m.PreviousYearTotalAmount);

        BuildCharts(year, months, categories);
    }

    /// <summary>
    /// Rebuilds the charts on every load, so colors (theme), month names (language) and money formats are current.
    /// </summary>
    private void BuildCharts(int year, IReadOnlyList<MonthlySummaryRow> months, IReadOnlyList<CategoryTotalRow> categories)
    {
        var text = new SolidColorPaint(_chartColors.Text);
        ChartTextPaint = text;
        TooltipBackgroundPaint = new SolidColorPaint(_chartColors.TooltipBackground);
        HasChartData = months.Any(m => m.TotalAmount != 0m || m.PreviousYearTotalAmount != 0m);

        MonthXAxes = [MoneyCharts.MonthAxis(Enumerable.Range(1, 12), text)];
        MonthYAxes = [MoneyCharts.AmountAxis(text, _chartColors.Gridlines)];
        MonthSeries =
        [
            MoneyCharts.Columns((year - 1).ToString(CultureInfo.InvariantCulture), months.Select(m => m.PreviousYearTotalAmount), ChartPalette.Comparison),
            MoneyCharts.Columns(year.ToString(CultureInfo.InvariantCulture), months.Select(m => m.TotalAmount), ChartPalette.At(0)),
        ];

        HasCategoryData = categories.Count > 0;
        var total = categories.Sum(c => c.TotalAmount);
        CategorySeries = categories
            .Select((category, index) => (ISeries)new PieSeries<double>
            {
                Name = category.CategoryName,
                Values = [(double)category.TotalAmount],
                Fill = new SolidColorPaint(ChartPalette.At(index)),
                InnerRadius = 50,
                HoverPushout = 6,
                ToolTipLabelFormatter = _ => FormatShare(category.TotalAmount, total),
            })
            .ToArray();
    }

    /// <summary>"1.234,56 RSD (25 %)": the amount and its share of the year.</summary>
    private static string FormatShare(decimal amount, decimal total)
    {
        var money = MoneyCharts.Format(amount);
        return total == 0m ? money : $"{money} ({(amount / total).ToString("P0", LocalizedStrings.FormattingCulture)})";
    }
}