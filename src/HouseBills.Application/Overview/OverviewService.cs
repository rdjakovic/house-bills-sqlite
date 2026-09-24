using HouseBills.Application.Bills;
using HouseBills.Application.Common;
using HouseBills.Application.Persistence;
using HouseBills.Domain;

namespace HouseBills.Application.Overview;

internal sealed class OverviewService(IBillRepository bills, IClock clock) : IOverviewService
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
            unpaid.Take(nextBillsCount).ToList());
    }
}