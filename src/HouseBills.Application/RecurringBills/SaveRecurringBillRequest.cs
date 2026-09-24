using HouseBills.Domain;

namespace HouseBills.Application.RecurringBills;

/// <summary>Create (<paramref name="Id"/> is <c>null</c>) or update a recurring bill template.</summary>
/// <param name="AmountVaries">Generated bills are estimates based on the last actual amount (see <see cref="RecurringBill.AmountVaries"/>).</param>
public sealed record SaveRecurringBillRequest(
    int? Id,
    string Name,
    int PayeeId,
    int CategoryId,
    decimal Amount,
    BillFrequency Frequency,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Notes,
    byte[]? RowVersion,
    bool AmountVaries = false);