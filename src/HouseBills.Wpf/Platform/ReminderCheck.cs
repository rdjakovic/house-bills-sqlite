using HouseBills.Application.Reminders;
using HouseBills.Domain;
using HouseBills.Presentation.Resources;
using HouseBills.Wpf.Charts;
using HouseBills.Wpf.Localization;

using Microsoft.Extensions.Logging;

namespace HouseBills.Wpf.Platform;

/// <summary>The sign-in reminder (<see cref="CommandLine.RemindArgument"/>): notify about bills that need attention.</summary>
public sealed class ReminderCheck(IReminderService reminders, IReminderNotifier notifier, ILogger<ReminderCheck> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var reminder = await reminders.GetTodaysReminderAsync(cancellationToken);
        if (reminder is null)
        {
            logger.LogInformation("Reminder check: nothing to remind about today.");
            return;
        }

        notifier.Show(Strings.Reminder_Title, Lines(reminder));
        await reminders.MarkRemindedAsync(cancellationToken);
        logger.LogInformation("Reminder shown: {Overdue} overdue, {DueSoon} due soon.", reminder.OverdueCount, reminder.DueSoonCount);
    }

    /// <summary>One line each for overdue and due-soon bills, in the chosen language; lines with no bills are left out.</summary>
    public static IReadOnlyList<string> Lines(BillReminder reminder)
    {
        var culture = LocalizedStrings.FormattingCulture;
        var lines = new List<string>();
        if (reminder.OverdueCount > 0)
        {
            lines.Add(string.Format(culture, Strings.Reminder_Overdue, reminder.OverdueCount, MoneyCharts.Format(reminder.OverdueAmount)));
        }

        if (reminder.DueSoonCount > 0)
        {
            lines.Add(string.Format(culture, Strings.Reminder_DueSoon, Bill.DueSoonDays, reminder.DueSoonCount, MoneyCharts.Format(reminder.DueSoonAmount)));
        }

        return lines;
    }
}