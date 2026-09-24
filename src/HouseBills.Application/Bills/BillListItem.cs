using HouseBills.Domain;

namespace HouseBills.Application.Bills;

public sealed record BillListItem(
    int Id,
    string Description,
    int PayeeId,
    string PayeeName,
    int CategoryId,
    string CategoryName,
    decimal Amount,
    DateOnly DueDate,
    DateOnly? PaidOn,
    decimal? PaidAmount,
    string? Notes,
    int? RecurringBillId,
    byte[] RowVersion)
{
    public BillStatus Status { get; init; }

    /// <summary>See <see cref="Bill.IsEstimated"/>.</summary>
    public bool IsEstimated { get; init; }
}