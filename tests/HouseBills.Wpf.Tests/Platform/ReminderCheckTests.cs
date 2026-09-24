using HouseBills.Application.Reminders;
using HouseBills.Wpf.Platform;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace HouseBills.Wpf.Tests.Platform;

public sealed class ReminderCheckTests
{
    private readonly IReminderService _reminders = Substitute.For<IReminderService>();
    private readonly IReminderNotifier _notifier = Substitute.For<IReminderNotifier>();
    private readonly ReminderCheck _check;

    public ReminderCheckTests()
    {
        _check = new ReminderCheck(_reminders, _notifier, NullLogger<ReminderCheck>.Instance);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RunAsync_NothingToRemind_ShowsNothing()
    {
        _reminders.GetTodaysReminderAsync(Arg.Any<CancellationToken>()).Returns((BillReminder?)null);

        await _check.RunAsync(Ct);

        _notifier.DidNotReceiveWithAnyArgs().Show(default!, default!);
        await _reminders.DidNotReceiveWithAnyArgs().MarkRemindedAsync(Ct);
    }

    [Fact]
    public async Task RunAsync_BillsNeedAttention_NotifiesThenRemembersToday()
    {
        _reminders.GetTodaysReminderAsync(Arg.Any<CancellationToken>()).Returns(new BillReminder(2, 150m, 1, 20m));

        await _check.RunAsync(Ct);

        Received.InOrder(() =>
        {
            _notifier.Show("Bills need your attention", Arg.Is<IReadOnlyList<string>>(l => l.Count == 2));
            _reminders.MarkRemindedAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public void Lines_OnlyOverdue_LeavesOutTheDueSoonLine()
    {
        var lines = ReminderCheck.Lines(new BillReminder(3, 15_432.85m, 0, 0m));

        lines.ShouldHaveSingleItem().ShouldStartWith("Overdue bills: 3 (");
    }

    [Theory]
    [InlineData(new[] { "--remind" }, true, new string[0])]
    [InlineData(new[] { "--REMIND", "--Billing:GenerationLookaheadDays=10" }, true, new[] { "--Billing:GenerationLookaheadDays=10" })]
    [InlineData(new[] { "housebills:open" }, false, new string[0])]
    [InlineData(new string[0], false, new string[0])]
    public void CommandLine_Arguments_AreRecognizedAndNotPassedToConfiguration(string[] args, bool isReminder, string[] forConfiguration)
    {
        CommandLine.IsReminderCheck(args).ShouldBe(isReminder);
        CommandLine.ConfigurationArguments(args).ShouldBe(forConfiguration);
    }
}