namespace HouseBills.Application.Reports;

/// <summary>
/// Read-only reporting queries. Amounts are grouped by bill due date. When <c>recurringBillId</c> is given, only bills
/// generated from that recurring bill count; otherwise all bills do.
/// </summary>
public interface IReportQueries
{
    /// <summary>Exactly 12 rows (January..December) for <paramref name="year"/>; months without bills have zero totals.</summary>
    Task<IReadOnlyList<MonthlySummaryRow>> GetMonthlySummaryAsync(int year, int? recurringBillId, CancellationToken cancellationToken);

    /// <summary>Per-category totals for bills due between <paramref name="from"/> and <paramref name="to"/> (inclusive), largest first.</summary>
    Task<IReadOnlyList<CategoryTotalRow>> GetCategoryTotalsAsync(DateOnly from, DateOnly to, int? recurringBillId, CancellationToken cancellationToken);
}