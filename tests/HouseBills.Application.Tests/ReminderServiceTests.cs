using HouseBills.Application.Common;
using HouseBills.Application.Overview;
using HouseBills.Application.Preferences;
using HouseBills.Application.RecurringBills;
using HouseBills.Application.Reminders;

using NSubstitute;

namespace HouseBills.Application.Tests;

public sealed class ReminderServiceTests
{
    private readonly IRecurringBillService _recurring = Substitute.For<IRecurringBillService>();
    private readonly IOverviewService _overview = Substitute.For<IOverviewService>();
    private readonly IUserPreferencesStore _preferences = Substitute.For<IUserPreferencesStore>();
    private readonly ReminderService _service;

    public ReminderServiceTests()
    {
        var clock = Substitute.For<IClock>();
        clock.Today.Returns(TestData.Today);
        _preferences.LoadAsync(Arg.Any<CancellationToken>()).Returns(new UserPreferences("sr-Latn-RS"));
        _service = new ReminderService(_recurring, _overview, _preferences, clock);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetTodaysReminderAsync_BillsNeedAttention_GeneratesRecurringFirstAndReturnsCounts()
    {
        GivenSummary(overdue: 2, dueSoon: 1);

        var reminder = await _service.GetTodaysReminderAsync(Ct);

        reminder.ShouldBe(new BillReminder(2, 200m, 1, 10m));
        Received.InOrder(() =>
        {
            _recurring.GenerateUpcomingBillsAsync(Arg.Any<CancellationToken>());
            _overview.GetSummaryAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task GetTodaysReminderAsync_NothingOverdueOrDueSoon_ReturnsNull()
    {
        GivenSummary(overdue: 0, dueSoon: 0);

        (await _service.GetTodaysReminderAsync(Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task GetTodaysReminderAsync_AlreadyRemindedToday_ReturnsNullWithoutWork()
    {
        GivenSummary(overdue: 2, dueSoon: 1);
        _preferences.LoadAsync(Arg.Any<CancellationToken>()).Returns(new UserPreferences("en", LastReminderOn: TestData.Today));

        (await _service.GetTodaysReminderAsync(Ct)).ShouldBeNull();

        await _recurring.DidNotReceiveWithAnyArgs().GenerateUpcomingBillsAsync(Ct);
    }

    [Fact]
    public async Task GetTodaysReminderAsync_RemindedYesterday_RemindsAgain()
    {
        GivenSummary(overdue: 1, dueSoon: 0);
        _preferences.LoadAsync(Arg.Any<CancellationToken>()).Returns(new UserPreferences("en", LastReminderOn: TestData.Today.AddDays(-1)));

        (await _service.GetTodaysReminderAsync(Ct)).ShouldNotBeNull();
    }

    [Fact]
    public async Task MarkRemindedAsync_Always_SavesTodayKeepingOtherPreferences()
    {
        await _service.MarkRemindedAsync(Ct);

        await _preferences.Received(1).SaveAsync(new UserPreferences("sr-Latn-RS", LastReminderOn: TestData.Today), Arg.Any<CancellationToken>());
    }

    private void GivenSummary(int overdue, int dueSoon)
    {
        _overview.GetSummaryAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new OverviewSummary(overdue, overdue * 100m, dueSoon, dueSoon * 10m, 0m, 0m, 0m, [], []));
    }
}