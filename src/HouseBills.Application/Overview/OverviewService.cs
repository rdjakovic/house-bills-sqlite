using HouseBills.Application.Bills;
using HouseBills.Application.Common;
using HouseBills.Application.Persistence;
using HouseBills.Application.Reports;
using HouseBills.Domain;

namespace HouseBills.Application.Overview;

internal sealed class OverviewService(IBillRepository bills, IReportQueries reports, IClock clock) : IOverviewService
{
    public async Task<OverviewSummary> GetSummaryAsync(int nextBillsCount, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(nextBillsCount);
        var today = clock.Today;
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var unpaid = (await bills.ListAsync(new BillFilter(null, null, BillStatusFilter.Unpaid), today, cancellationToken))
            .Select(b => b with { Status = Bill.DetermineStatus(b.DueDate, b.PaidOn, today) })
            .ToList();
        var overdue = unpaid.Where(b => b.Status == BillStatus.Overdue).ToList();
        var dueSoon = unpaid.Where(b => b.Status == BillStatus.DueSoon).ToList();

        var thisMonth = await bills.ListAsync(new BillFilter(monthStart, monthStart.AddMonths(1).AddDays(-1)), today, cancellationToken);
        var lastMonth = await bills.ListAsync(new BillFilter(monthStart.AddMonths(-1), monthStart.AddDays(-1)), today, cancellationToken);

        return new OverviewSummary(
            overdue.Count,
            overdue.Sum(b => b.Amount),
            dueSoon.Count,
            dueSoon.Sum(b => b.Amount),
            thisMonth.Sum(b => b.Amount),
            thisMonth.Where(b => b.PaidOn is not null).Sum(b => b.PaidAmount ?? 0m),
            lastMonth.Sum(b => b.Amount),
            unpaid.Take(nextBillsCount).ToList(),
            await GetLastTwelveMonthsAsync(today, cancellationToken));
    }

    /// <summary>
    /// The monthly summary of this year already carries the same months of last year, so one query covers the last 12
    /// months: this year's months up to now, then last year's remaining months.
    /// </summary>
    private async Task<IReadOnlyList<MonthTotal>> GetLastTwelveMonthsAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var months = (await reports.GetMonthlySummaryAsync(today.Year, cancellationToken)).ToDictionary(m => m.Month);
        var start = new DateOnly(today.Year, today.Month, 1).AddMonths(-11);
        return Enumerable.Range(0, 12)
            .Select(offset => start.AddMonths(offset))
            .Select(month => new MonthTotal(
                month.Year,
                month.Month,
                months.TryGetValue(month.Month, out var row)
                    ? month.Year == today.Year ? row.TotalAmount : row.PreviousYearTotalAmount
                    : 0m))
            .ToList();
    }
}