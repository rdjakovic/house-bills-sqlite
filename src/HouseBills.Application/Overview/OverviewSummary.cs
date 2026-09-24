using HouseBills.Application.Bills;

namespace HouseBills.Application.Overview;

/// <summary>What needs attention now, for the start page.</summary>
/// <param name="OverdueCount">Unpaid bills whose due date has passed.</param>
/// <param name="OverdueAmount">Sum of the overdue bills' amounts.</param>
/// <param name="DueSoonCount">Unpaid bills due today or within <see cref="Domain.Bill.DueSoonDays"/> days.</param>
/// <param name="DueSoonAmount">Sum of the due-soon bills' amounts.</param>
/// <param name="ThisMonthTotal">Sum of all bills due this calendar month.</param>
/// <param name="ThisMonthPaid">Paid amounts of the bills due this month.</param>
/// <param name="LastMonthTotal">Sum of all bills due last calendar month.</param>
/// <param name="NextBills">The first unpaid bills by due date (overdue first), with <see cref="BillListItem.Status"/> set.</param>
public sealed record OverviewSummary(
    int OverdueCount,
    decimal OverdueAmount,
    int DueSoonCount,
    decimal DueSoonAmount,
    decimal ThisMonthTotal,
    decimal ThisMonthPaid,
    decimal LastMonthTotal,
    IReadOnlyList<BillListItem> NextBills);