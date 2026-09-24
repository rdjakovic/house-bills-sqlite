namespace HouseBills.Application.Import;

/// <summary>A bill read from an import file. Payee and category are given by name.</summary>
/// <param name="Row">Row number in the file (the header is row 1), for error messages.</param>
/// <param name="PaidOn">When paid; <c>null</c> for an unpaid bill.</param>
/// <param name="PaidAmount">Amount paid; when <paramref name="PaidOn"/> is set and this is <c>null</c>, the bill amount.</param>
public sealed record ImportedBill(
    int Row,
    string Description,
    string PayeeName,
    string CategoryName,
    decimal Amount,
    DateOnly DueDate,
    bool IsEstimated,
    DateOnly? PaidOn,
    decimal? PaidAmount,
    string? Notes);