using HouseBills.Application.Common;
using HouseBills.Application.RecurringBills;
using HouseBills.Application.Reports;
using HouseBills.Domain;
using HouseBills.Wpf.Charts;
using HouseBills.Wpf.Services;
using HouseBills.Wpf.ViewModels;

using LiveChartsCore.SkiaSharpView;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using SkiaSharp;

namespace HouseBills.Wpf.Tests;

public sealed class ReportsViewModelTests
{
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IReportQueries _reports = Substitute.For<IReportQueries>();
    private readonly IRecurringBillService _recurringBills = Substitute.For<IRecurringBillService>();
    private readonly IChartColors _colors = Substitute.For<IChartColors>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();

    public ReportsViewModelTests()
    {
        _clock.Today.Returns(new DateOnly(2026, 9, 24));
        _colors.Text.Returns(SKColors.White);
        _recurringBills.ListAsync(Arg.Any<CancellationToken>()).Returns([Template(3, "Power: EPS"), Template(7, "Rent")]);
    }

    [Fact]
    public async Task OnNavigatedToAsync_CurrentYear_LoadsTotalsAndScalesBars()
    {
        GivenYear2026();
        var viewModel = CreateViewModel();

        await viewModel.OnNavigatedToAsync();

        viewModel.SelectedYear.ShouldBe(2026);
        viewModel.YearTotal.ShouldBe(750m);
        viewModel.YearOutstanding.ShouldBe(150m);
        viewModel.PreviousYearTotal.ShouldBe(120m);
        viewModel.Months[2].BarFraction.ShouldBe(1d);
        viewModel.Months[0].BarFraction.ShouldBe(0.25d);
        viewModel.Months[2].ChangeFromPreviousYear.ShouldBe(190m);
        viewModel.CategoryTotals.Count.ShouldBe(2);
    }

    [Fact]
    public async Task OnNavigatedToAsync_YearWithBills_BuildsMonthAndCategoryCharts()
    {
        GivenYear2026();
        var viewModel = CreateViewModel();

        await viewModel.OnNavigatedToAsync();

        viewModel.HasChartData.ShouldBeTrue();
        viewModel.HasCategoryData.ShouldBeTrue();
        viewModel.MonthSeries.Select(s => s.Name).ShouldBe(["2025", "2026"]);
        ((ColumnSeries<double>)viewModel.MonthSeries[1]).Values!.ShouldBe([50d, 50d, 200d, 50d, 50d, 50d, 50d, 50d, 50d, 50d, 50d, 50d]);
        ((ColumnSeries<double>)viewModel.MonthSeries[0]).Values!.ShouldAllBe(v => v == 10d);
        viewModel.MonthXAxes.ShouldHaveSingleItem().Labels!.Count.ShouldBe(12);
        viewModel.CategorySeries.Select(s => s.Name).ShouldBe(["Utilities", "Insurance"]);
        viewModel.ChartTextPaint!.Color.ShouldBe(SKColors.White);
    }

    [Fact]
    public async Task OnNavigatedToAsync_NoBillsInEitherYear_HidesCharts()
    {
        _reports.GetMonthlySummaryAsync(2026, null, Arg.Any<CancellationToken>())
            .Returns(Enumerable.Range(1, 12).Select(m => new MonthlySummaryRow(m, 0, 0m, 0m, 0m, 0m)).ToList());
        _reports.GetCategoryTotalsAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), null, Arg.Any<CancellationToken>()).Returns([]);
        var viewModel = CreateViewModel();

        await viewModel.OnNavigatedToAsync();

        viewModel.HasChartData.ShouldBeFalse();
        viewModel.HasCategoryData.ShouldBeFalse();
        viewModel.CategorySeries.ShouldBeEmpty();
    }

    [Fact]
    public async Task OnNavigatedToAsync_FirstVisit_OffersAllBillsThenEachRecurringBillAndShowsAllBills()
    {
        GivenYear2026();
        var viewModel = CreateViewModel();

        await viewModel.OnNavigatedToAsync();

        viewModel.RecurringBillOptions.ShouldBe([new(null, "All bills"), new(3, "Power: EPS"), new(7, "Rent")]);
        viewModel.SelectedRecurringBill.ShouldBe(viewModel.RecurringBillOptions[0]);
        viewModel.RecurringBillOptions[2].ToString().ShouldBe("Rent");
        await _reports.Received(1).GetMonthlySummaryAsync(2026, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refresh_RecurringBillSelected_LoadsOnlyThatBillsTotals()
    {
        GivenYear2026();
        _reports.GetMonthlySummaryAsync(2026, 7, Arg.Any<CancellationToken>())
            .Returns(Enumerable.Range(1, 12).Select(m => new MonthlySummaryRow(m, 1, 400m, 400m, 0m, 380m)).ToList());
        _reports.GetCategoryTotalsAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), 7, Arg.Any<CancellationToken>())
            .Returns([new CategoryTotalRow("Housing", 12, 4800m, 4800m)]);
        var viewModel = CreateViewModel();
        await viewModel.OnNavigatedToAsync();

        viewModel.SelectedRecurringBill = viewModel.RecurringBillOptions.Single(o => o.RecurringBillId == 7);
        await viewModel.RefreshCommand.ExecuteAsync(null);

        viewModel.YearTotal.ShouldBe(4800m);
        viewModel.PreviousYearTotal.ShouldBe(4560m);
        viewModel.CategoryTotals.ShouldHaveSingleItem().CategoryName.ShouldBe("Housing");
        viewModel.CategorySeries.Select(s => s.Name).ShouldBe(["Housing"]);
        ((ColumnSeries<double>)viewModel.MonthSeries[1]).Values!.ShouldAllBe(v => v == 400d);
    }

    [Fact]
    public async Task OnNavigatedToAsync_SelectedRecurringBillStillExists_KeepsItSelected()
    {
        GivenYear2026();
        var viewModel = CreateViewModel();
        await viewModel.OnNavigatedToAsync();
        viewModel.SelectedRecurringBill = viewModel.RecurringBillOptions.Single(o => o.RecurringBillId == 7);

        await viewModel.OnNavigatedToAsync();

        viewModel.SelectedRecurringBill!.RecurringBillId.ShouldBe(7);
        await _reports.Received(1).GetMonthlySummaryAsync(2026, 7, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnNavigatedToAsync_SelectedRecurringBillWasDeleted_FallsBackToAllBills()
    {
        GivenYear2026();
        var viewModel = CreateViewModel();
        await viewModel.OnNavigatedToAsync();
        viewModel.SelectedRecurringBill = viewModel.RecurringBillOptions.Single(o => o.RecurringBillId == 7);
        _recurringBills.ListAsync(Arg.Any<CancellationToken>()).Returns([Template(3, "Power: EPS")]);

        await viewModel.OnNavigatedToAsync();

        viewModel.SelectedRecurringBill!.RecurringBillId.ShouldBeNull();
        await _reports.DidNotReceive().GetMonthlySummaryAsync(Arg.Any<int>(), 7, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Export_SelectionChangedAfterShow_SuggestsFileNameOfShownReport()
    {
        GivenYear2026();
        var viewModel = CreateViewModel();
        await viewModel.OnNavigatedToAsync();
        viewModel.SelectedRecurringBill = viewModel.RecurringBillOptions.Single(o => o.RecurringBillId == 3);
        await viewModel.RefreshCommand.ExecuteAsync(null);
        viewModel.SelectedYear = 2025;
        viewModel.SelectedRecurringBill = viewModel.RecurringBillOptions[0];

        await viewModel.ExportCommand.ExecuteAsync(null);

        // A colon is not allowed in file names.
        _dialogs.Received(1).PickCsvSaveLocation("HouseBills-2026-Power_ EPS.csv");
    }

    private void GivenYear2026()
    {
        _reports.GetMonthlySummaryAsync(2026, null, Arg.Any<CancellationToken>()).Returns(
            Enumerable.Range(1, 12)
                .Select(m => new MonthlySummaryRow(m, 1, m == 3 ? 200m : 50m, 50m, m == 3 ? 150m : 0m, 10m))
                .ToList());
        _reports.GetCategoryTotalsAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), null, Arg.Any<CancellationToken>())
            .Returns([new CategoryTotalRow("Utilities", 12, 600m, 500m), new CategoryTotalRow("Insurance", 1, 150m, 100m)]);
    }

    private static RecurringBillDto Template(int id, string name) =>
        new(id, name, 1, "Payee", 1, "Utilities", 100m, BillFrequency.Monthly, new DateOnly(2025, 1, 1), null, null, true, null, [1]);

    private ReportsViewModel CreateViewModel() =>
        new(_reports, _recurringBills, _clock, _colors, Substitute.For<IFileSaver>(), _dialogs, NullLogger<ReportsViewModel>.Instance);
}