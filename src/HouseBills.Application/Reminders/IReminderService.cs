namespace HouseBills.Application.Reminders;

/// <summary>Decides whether the user should be reminded about bills today.</summary>
public interface IReminderService
{
    /// <summary>
    /// Generates upcoming recurring bills (so they count), then returns what is overdue or due soon — or <c>null</c> when
    /// nothing needs attention or the user was already reminded today.
    /// </summary>
    Task<BillReminder?> GetTodaysReminderAsync(CancellationToken cancellationToken);

    /// <summary>Records that today's reminder was shown.</summary>
    Task MarkRemindedAsync(CancellationToken cancellationToken);
}