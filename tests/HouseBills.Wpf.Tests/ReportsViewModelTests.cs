using HouseBills.Application.Common;
using HouseBills.Application.Reports;
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
    private readonly IChartColors _colors = Substitute.For<IChartColors>();

    public ReportsViewModelTests()
    {
        _clock.Today.Returns(new DateOnly(2026, 9, 24));
        _colors.Text.Returns(SKColors.White);
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
        _reports.GetMonthlySummaryAsync(2026, Arg.Any<CancellationToken>())
            .Returns(Enumerable.Range(1, 12).Select(m => new MonthlySummaryRow(m, 0, 0m, 0m, 0m, 0m)).ToList());
        _reports.GetCategoryTotalsAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns([]);
        var viewModel = CreateViewModel();

        await viewModel.OnNavigatedToAsync();

        viewModel.HasChartData.ShouldBeFalse();
        viewModel.HasCategoryData.ShouldBeFalse();
        viewModel.CategorySeries.ShouldBeEmpty();
    }

    private void GivenYear2026()
    {
        _reports.GetMonthlySummaryAsync(2026, Arg.Any<CancellationToken>()).Returns(
            Enumerable.Range(1, 12)
                .Select(m => new MonthlySummaryRow(m, 1, m == 3 ? 200m : 50m, 50m, m == 3 ? 150m : 0m, 10m))
                .ToList());
        _reports.GetCategoryTotalsAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Arg.Any<CancellationToken>())
            .Returns([new CategoryTotalRow("Utilities", 12, 600m, 500m), new CategoryTotalRow("Insurance", 1, 150m, 100m)]);
    }

    private ReportsViewModel CreateViewModel() =>
        new(_reports, _clock, _colors, Substitute.For<IFileSaver>(), Substitute.For<IDialogService>(), NullLogger<ReportsViewModel>.Instance);
}