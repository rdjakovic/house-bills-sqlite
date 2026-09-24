namespace HouseBills.Application.Reminders;

/// <summary>What a reminder tells the user: unpaid bills that are overdue or due soon.</summary>
public sealed record BillReminder(int OverdueCount, decimal OverdueAmount, int DueSoonCount, decimal DueSoonAmount);