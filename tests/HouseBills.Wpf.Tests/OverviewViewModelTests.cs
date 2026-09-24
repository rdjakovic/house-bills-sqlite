using HouseBills.Application.Bills;
using HouseBills.Application.Categories;
using HouseBills.Application.Common;
using HouseBills.Application.Import;
using HouseBills.Application.Overview;
using HouseBills.Application.Payees;
using HouseBills.Application.RecurringBills;
using HouseBills.Domain;
using HouseBills.Wpf.Services;
using HouseBills.Wpf.ViewModels;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace HouseBills.Wpf.Tests;

public sealed class OverviewViewModelTests
{
    private static readonly DateOnly Today = new(2026, 9, 24);

    private readonly IOverviewService _overview = Substitute.For<IOverviewService>();
    private readonly IRecurringBillService _recurring = Substitute.For<IRecurringBillService>();
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly OverviewViewModel _viewModel;

    public OverviewViewModelTests()
    {
        _clock.Today.Returns(Today);
        _overview.GetSummaryAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(Summary(lastMonthTotal: 400m));
        _viewModel = new OverviewViewModel(_overview, _recurring, _navigation, _clock, Substitute.For<IDialogService>(), NullLogger<OverviewViewModel>.Instance);
    }

    [Fact]
    public async Task OnNavigatedToAsync_VisitedTwice_GeneratesRecurringBillsOnceAndShowsSummary()
    {
        await _viewModel.OnNavigatedToAsync();
        await _viewModel.OnNavigatedToAsync();

        await _recurring.Received(1).GenerateUpcomingBillsAsync(Arg.Any<CancellationToken>());
        _viewModel.OverdueAmount.ShouldBe(150m);
        _viewModel.OverdueCountText.ShouldBe("Bills: 2");
        _viewModel.HasOverdue.ShouldBeTrue();
        _viewModel.DueSoonCountText.ShouldBe("Bills: 1");
        _viewModel.DueSoonTitle.ShouldBe("Due in the next 7 days");
        _viewModel.ThisMonthTotal.ShouldBe(500m);
        _viewModel.ChangeVsLastMonthText.ShouldBe("+25% compared with last month");
        _viewModel.NextBills.Count.ShouldBe(2);
    }

    [Fact]
    public async Task OnNavigatedToAsync_NoBillsLastMonth_ShowsNoComparison()
    {
        _overview.GetSummaryAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(Summary(lastMonthTotal: 0m));

        await _viewModel.OnNavigatedToAsync();

        _viewModel.ChangeVsLastMonthText.ShouldBeNull();
    }

    [Fact]
    public async Task ShowOverdue_Always_OpensBillsFilteredToOverdue()
    {
        var bills = CreateBillsViewModel();
        bills.SearchText = "left over";
        await _navigation.NavigateToAsync(Arg.Do<Action<BillsViewModel>?>(prepare => prepare!(bills)));

        await _viewModel.ShowOverdueCommand.ExecuteAsync(null);

        bills.StatusFilter.ShouldBe(BillStatusFilter.Overdue);
        bills.SearchText.ShouldBeNull();
    }

    [Fact]
    public async Task ShowDueSoon_Always_OpensUnpaidBillsDueWithinDueSoonDays()
    {
        var bills = CreateBillsViewModel();
        await _navigation.NavigateToAsync(Arg.Do<Action<BillsViewModel>?>(prepare => prepare!(bills)));

        await _viewModel.ShowDueSoonCommand.ExecuteAsync(null);

        bills.StatusFilter.ShouldBe(BillStatusFilter.Unpaid);
        bills.DueFrom.ShouldBe(Today);
        bills.DueTo.ShouldBe(Today.AddDays(Bill.DueSoonDays));
    }

    private static OverviewSummary Summary(decimal lastMonthTotal) => new(
        OverdueCount: 2,
        OverdueAmount: 150m,
        DueSoonCount: 1,
        DueSoonAmount: 20m,
        ThisMonthTotal: 500m,
        ThisMonthPaid: 300m,
        LastMonthTotal: lastMonthTotal,
        NextBills:
        [
            new BillListItem(1, "Power", 1, "EPS", 1, "Utilities", 100m, Today.AddDays(-3), null, null, null, null, [1]) { Status = BillStatus.Overdue },
            new BillListItem(2, "Water", 1, "Infostan", 1, "Utilities", 50m, Today.AddDays(2), null, null, null, null, [1]) { Status = BillStatus.DueSoon },
        ]);

    private BillsViewModel CreateBillsViewModel() => new(
        Substitute.For<IBillService>(),
        _recurring,
        Substitute.For<IPayeeService>(),
        Substitute.For<ICategoryService>(),
        _clock,
        Substitute.For<IImportService>(),
        Substitute.For<IFileSaver>(),
        Substitute.For<IFileReader>(),
        Substitute.For<IDialogService>(),
        NullLogger<BillsViewModel>.Instance);
}