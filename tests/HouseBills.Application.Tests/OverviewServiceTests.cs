using HouseBills.Application.Bills;
using HouseBills.Application.Common;
using HouseBills.Application.Overview;
using HouseBills.Application.Persistence;
using HouseBills.Application.Reports;
using HouseBills.Domain;

using NSubstitute;

namespace HouseBills.Application.Tests;

public sealed class OverviewServiceTests
{
    // TestData.Today is 2026-09-24.
    private static readonly DateOnly SeptemberStart = new(2026, 9, 1);

    private readonly IBillRepository _bills = Substitute.For<IBillRepository>();
    private readonly IReportQueries _reports = Substitute.For<IReportQueries>();
    private readonly OverviewService _service;

    public OverviewServiceTests()
    {
        var clock = Substitute.For<IClock>();
        clock.Today.Returns(TestData.Today);
        _bills.ListAsync(Arg.Any<BillFilter>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns([]);
        _reports.GetMonthlySummaryAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Enumerable.Range(1, 12).Select(m => new MonthlySummaryRow(m, 0, 0m, 0m, 0m, 0m)).ToList());
        _service = new OverviewService(_bills, _reports, clock);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetSummaryAsync_UnpaidBills_CountsOverdueAndDueSoonAndListsNextBills()
    {
        _bills.ListAsync(Arg.Is<BillFilter>(f => f.Status == BillStatusFilter.Unpaid), TestData.Today, Arg.Any<CancellationToken>()).Returns([
            Item(1, 100m, TestData.Today.AddDays(-10)),
            Item(2, 50m, TestData.Today.AddDays(-1)),
            Item(3, 20m, TestData.Today),
            Item(4, 30m, TestData.Today.AddDays(Bill.DueSoonDays)),
            Item(5, 40m, TestData.Today.AddDays(Bill.DueSoonDays + 1)),
        ]);

        var summary = await _service.GetSummaryAsync(nextBillsCount: 3, Ct);

        summary.OverdueCount.ShouldBe(2);
        summary.OverdueAmount.ShouldBe(150m);
        summary.DueSoonCount.ShouldBe(2);
        summary.DueSoonAmount.ShouldBe(50m);
        summary.NextBills.Select(b => (b.Id, b.Status)).ShouldBe([(1, BillStatus.Overdue), (2, BillStatus.Overdue), (3, BillStatus.DueSoon)]);
    }

    [Fact]
    public async Task GetSummaryAsync_BillsThisAndLastMonth_TotalsByCalendarMonth()
    {
        _bills.ListAsync(Arg.Is<BillFilter>(f => f.DueFrom == SeptemberStart && f.DueTo == new DateOnly(2026, 9, 30)), TestData.Today, Arg.Any<CancellationToken>())
            .Returns([Item(1, 100m, SeptemberStart, paid: 90m), Item(2, 60m, new DateOnly(2026, 9, 30))]);
        _bills.ListAsync(Arg.Is<BillFilter>(f => f.DueFrom == new DateOnly(2026, 8, 1) && f.DueTo == new DateOnly(2026, 8, 31)), TestData.Today, Arg.Any<CancellationToken>())
            .Returns([Item(3, 200m, new DateOnly(2026, 8, 15), paid: 200m)]);

        var summary = await _service.GetSummaryAsync(5, Ct);

        summary.ThisMonthTotal.ShouldBe(160m);
        summary.ThisMonthPaid.ShouldBe(90m);
        summary.LastMonthTotal.ShouldBe(200m);
    }

    [Fact]
    public async Task GetSummaryAsync_Always_ReturnsLastTwelveMonthsAcrossTheYearBoundary()
    {
        // Current-year totals are 1000 + month, previous-year totals 2000 + month.
        _reports.GetMonthlySummaryAsync(2026, Arg.Any<CancellationToken>())
            .Returns(Enumerable.Range(1, 12).Select(m => new MonthlySummaryRow(m, 1, 1000m + m, 0m, 0m, 2000m + m)).ToList());

        var summary = await _service.GetSummaryAsync(5, Ct);

        summary.LastTwelveMonths.Count.ShouldBe(12);
        summary.LastTwelveMonths[0].ShouldBe(new MonthTotal(2025, 10, 2010m));
        summary.LastTwelveMonths[2].ShouldBe(new MonthTotal(2025, 12, 2012m));
        summary.LastTwelveMonths[3].ShouldBe(new MonthTotal(2026, 1, 1001m));
        summary.LastTwelveMonths[^1].ShouldBe(new MonthTotal(2026, 9, 1009m));
    }

    private static BillListItem Item(int id, decimal amount, DateOnly dueDate, decimal? paid = null) =>
        new(id, $"Bill {id}", 1, "Payee", 1, "Category", amount, dueDate, paid is null ? null : dueDate, paid, null, null, TestData.RowVersion);
}