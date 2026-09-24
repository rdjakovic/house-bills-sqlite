using System.Globalization;

using HouseBills.Application.Common;
using HouseBills.Application.Reports;
using HouseBills.Wpf.Charts;
using HouseBills.Wpf.Services;
using HouseBills.Wpf.ViewModels;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace HouseBills.Wpf.Tests.Localization;

[Collection(UiCultureCollection.Name)]
public sealed class SerbianReportsTests : IDisposable
{
    private readonly CultureInfo _originalUiCulture = CultureInfo.CurrentUICulture;

    public void Dispose() => CultureInfo.CurrentUICulture = _originalUiCulture;

    [Fact]
    public async Task OnNavigatedToAsync_SerbianUi_ShowsCapitalizedSerbianMonthNames()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("sr-Latn-RS");
        var clock = Substitute.For<IClock>();
        clock.Today.Returns(new DateOnly(2026, 9, 24));
        var reports = Substitute.For<IReportQueries>();
        reports.GetMonthlySummaryAsync(2026, Arg.Any<CancellationToken>())
            .Returns(Enumerable.Range(1, 12).Select(m => new MonthlySummaryRow(m, 0, 0m, 0m, 0m, 0m)).ToList());
        reports.GetCategoryTotalsAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns([]);
        var viewModel = new ReportsViewModel(reports, clock, Substitute.For<IChartColors>(), Substitute.For<IFileSaver>(), Substitute.For<IDialogService>(), NullLogger<ReportsViewModel>.Instance);

        await viewModel.OnNavigatedToAsync();

        viewModel.Months[0].MonthName.ShouldBe("Januar");
        viewModel.Months[11].MonthName.ShouldBe("Decembar");
        viewModel.MonthXAxes[0].Labels![0].ShouldStartWith("Jan");
        viewModel.Title.ShouldBe("Izveštaji");
    }
}