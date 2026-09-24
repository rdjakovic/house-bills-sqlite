using HouseBills.Application.Common;
using HouseBills.Application.Overview;
using HouseBills.Application.Preferences;
using HouseBills.Application.RecurringBills;

namespace HouseBills.Application.Reminders;

internal sealed class ReminderService(
    IRecurringBillService recurringBills,
    IOverviewService overview,
    IUserPreferencesStore preferences,
    IClock clock)
    : IReminderService
{
    public async Task<BillReminder?> GetTodaysReminderAsync(CancellationToken cancellationToken)
    {
        if ((await preferences.LoadAsync(cancellationToken)).LastReminderOn == clock.Today)
        {
            return null;
        }

        await recurringBills.GenerateUpcomingBillsAsync(cancellationToken);
        var summary = await overview.GetSummaryAsync(nextBillsCount: 0, cancellationToken);
        return summary.OverdueCount == 0 && summary.DueSoonCount == 0
            ? null
            : new BillReminder(summary.OverdueCount, summary.OverdueAmount, summary.DueSoonCount, summary.DueSoonAmount);
    }

    public async Task MarkRemindedAsync(CancellationToken cancellationToken)
    {
        var saved = await preferences.LoadAsync(cancellationToken);
        await preferences.SaveAsync(saved with { LastReminderOn = clock.Today }, cancellationToken);
    }
}