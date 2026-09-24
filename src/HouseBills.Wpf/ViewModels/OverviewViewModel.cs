using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HouseBills.Application.Bills;
using HouseBills.Application.Common;
using HouseBills.Application.Overview;
using HouseBills.Application.RecurringBills;
using HouseBills.Domain;
using HouseBills.Presentation.Resources;
using HouseBills.Wpf.Charts;
using HouseBills.Wpf.Localization;
using HouseBills.Wpf.Services;

using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;

using Microsoft.Extensions.Logging;

namespace HouseBills.Wpf.ViewModels;

/// <summary>The start page: what is overdue or due soon, this month so far, and the next bills to pay.</summary>
public sealed partial class OverviewViewModel : PageViewModel
{
    private const int NextBillsCount = 8;

    private readonly IOverviewService _overview;
    private readonly IRecurringBillService _recurringBills;
    private readonly INavigationService _navigation;
    private readonly IClock _clock;
    private readonly IChartColors _chartColors;
    private bool _hasGeneratedBills;

    public OverviewViewModel(
        IOverviewService overview,
        IRecurringBillService recurringBills,
        INavigationService navigation,
        IClock clock,
        IChartColors chartColors,
        IDialogService dialogs,
        ILogger<OverviewViewModel> logger)
        : base(dialogs, logger)
    {
        _overview = overview;
        _recurringBills = recurringBills;
        _navigation = navigation;
        _clock = clock;
        _chartColors = chartColors;
    }

    public override string Title => Strings.Page_Overview;

    public string DueSoonTitle => string.Format(LocalizedStrings.FormattingCulture, Strings.Overview_DueSoon, Bill.DueSoonDays);

    public ObservableCollection<BillListItem> NextBills { get; } = [];

    [ObservableProperty]
    public partial decimal OverdueAmount { get; set; }

    [ObservableProperty]
    public partial string? OverdueCountText { get; set; }

    [ObservableProperty]
    public partial decimal DueSoonAmount { get; set; }

    [ObservableProperty]
    public partial string? DueSoonCountText { get; set; }

    [ObservableProperty]
    public partial decimal ThisMonthTotal { get; set; }

    [ObservableProperty]
    public partial string? ThisMonthPaidText { get; set; }

    /// <summary>"+12 % compared with last month"; <c>null</c> when last month had no bills.</summary>
    [ObservableProperty]
    public partial string? ChangeVsLastMonthText { get; set; }

    [ObservableProperty]
    public partial bool HasOverdue { get; set; }

    [ObservableProperty]
    public partial bool HasNextBills { get; set; }

    [ObservableProperty]
    public partial ISeries[] HistorySeries { get; set; } = [];

    [ObservableProperty]
    public partial Axis[] HistoryXAxes { get; set; } = [];

    [ObservableProperty]
    public partial Axis[] HistoryYAxes { get; set; } = [];

    [ObservableProperty]
    public partial SolidColorPaint? ChartTextPaint { get; set; }

    [ObservableProperty]
    public partial SolidColorPaint? TooltipBackgroundPaint { get; set; }

    [ObservableProperty]
    public partial bool HasHistory { get; set; }

    public override Task OnNavigatedToAsync()
    {
        OnPropertyChanged(nameof(DueSoonTitle));
        return RunAsync(
            async () =>
            {
                // The start page shows upcoming bills, so recurring ones must exist first (as on the bill list).
                if (!_hasGeneratedBills)
                {
                    await _recurringBills.GenerateUpcomingBillsAsync(CancellationToken.None);
                    _hasGeneratedBills = true;
                }

                Apply(await _overview.GetSummaryAsync(NextBillsCount, CancellationToken.None));
            },
            Strings.Overview_LoadFailed);
    }

    [RelayCommand]
    private Task ShowOverdueAsync() =>
        _navigation.NavigateToAsync<BillsViewModel>(bills => bills.ApplyFilter(BillStatusFilter.Overdue));

    [RelayCommand]
    private Task ShowDueSoonAsync()
    {
        var today = _clock.Today;
        return _navigation.NavigateToAsync<BillsViewModel>(bills => bills.ApplyFilter(BillStatusFilter.Unpaid, today, today.AddDays(Bill.DueSoonDays)));
    }

    [RelayCommand]
    private Task ShowUnpaidAsync() =>
        _navigation.NavigateToAsync<BillsViewModel>(bills => bills.ApplyFilter(BillStatusFilter.Unpaid));

    private void Apply(OverviewSummary summary)
    {
        var culture = LocalizedStrings.FormattingCulture;
        OverdueAmount = summary.OverdueAmount;
        OverdueCountText = string.Format(culture, Strings.Overview_BillCount, summary.OverdueCount);
        HasOverdue = summary.OverdueCount > 0;
        DueSoonAmount = summary.DueSoonAmount;
        DueSoonCountText = string.Format(culture, Strings.Overview_BillCount, summary.DueSoonCount);
        ThisMonthTotal = summary.ThisMonthTotal;
        ThisMonthPaidText = string.Format(culture, Strings.Overview_PaidSoFar, MoneyCharts.Format(summary.ThisMonthPaid));
        ChangeVsLastMonthText = summary.LastMonthTotal == 0m
            ? null
            : string.Format(culture, Strings.Overview_ChangeVsLastMonth, FormatChange((summary.ThisMonthTotal - summary.LastMonthTotal) / summary.LastMonthTotal));

        NextBills.Clear();
        foreach (var bill in summary.NextBills)
        {
            NextBills.Add(bill);
        }

        HasNextBills = NextBills.Count > 0;
        BuildHistoryChart(summary.LastTwelveMonths);
    }

    /// <summary>"+12%" / "-5 %": signed, in the language's own percent format.</summary>
    private static string FormatChange(decimal change)
    {
        var percent = change.ToString("P0", LocalizedStrings.FormattingCulture);
        return change > 0m ? "+" + percent : percent;
    }

    /// <summary>Rebuilt on every load, so colors (theme), month names (language) and money formats are current.</summary>
    private void BuildHistoryChart(IReadOnlyList<MonthTotal> months)
    {
        var text = new SolidColorPaint(_chartColors.Text);
        ChartTextPaint = text;
        TooltipBackgroundPaint = new SolidColorPaint(_chartColors.TooltipBackground);
        HasHistory = months.Any(m => m.TotalAmount != 0m);

        var culture = LocalizedStrings.Culture;
        HistoryXAxes = [MoneyCharts.MonthAxis(months.Select(m => m.Month), text)];
        HistoryYAxes = [MoneyCharts.AmountAxis(text, _chartColors.Gridlines)];
        HistorySeries =
        [
            MoneyCharts.Columns(
                Strings.Overview_LastTwelveMonths,
                months.Select(m => m.TotalAmount),
                ChartPalette.At(0),
                point =>
                {
                    var month = months[point.Index];
                    var name = MoneyCharts.Capitalize(culture.DateTimeFormat.GetMonthName(month.Month), culture);
                    return $"{name} {month.Year}: {MoneyCharts.Format(month.TotalAmount)}";
                }),
        ];
    }
}