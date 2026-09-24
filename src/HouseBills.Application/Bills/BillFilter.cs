namespace HouseBills.Application.Bills;

/// <summary>Criteria for listing bills. Date bounds apply to the due date and are inclusive.</summary>
/// <param name="Search">
/// Text that must appear in the description, payee, category or notes (ignoring case); blank means no text filter.
/// </param>
public sealed record BillFilter(
    DateOnly? DueFrom,
    DateOnly? DueTo,
    BillStatusFilter Status = BillStatusFilter.All,
    int? CategoryId = null,
    int? PayeeId = null,
    string? Search = null);