namespace HouseBills.Application.Overview;

/// <summary>Total of the bills due in one calendar month.</summary>
public sealed record MonthTotal(int Year, int Month, decimal TotalAmount);