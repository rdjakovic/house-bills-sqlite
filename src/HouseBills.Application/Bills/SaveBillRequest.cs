namespace HouseBills.Application.Bills;

/// <summary>Create (<paramref name="Id"/> is <c>null</c>) or update a bill's details (not its paid state).</summary>
/// <param name="IsEstimated">The amount is still an estimate (see <see cref="Domain.Bill.IsEstimated"/>).</param>
public sealed record SaveBillRequest(
    int? Id,
    string Description,
    int PayeeId,
    int CategoryId,
    decimal Amount,
    DateOnly DueDate,
    string? Notes,
    byte[]? RowVersion,
    bool IsEstimated = false);