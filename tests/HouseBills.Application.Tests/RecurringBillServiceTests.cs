using HouseBills.Application.Common;
using HouseBills.Application.Persistence;
using HouseBills.Application.RecurringBills;
using HouseBills.Domain;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace HouseBills.Application.Tests;

public sealed class RecurringBillServiceTests
{
    private readonly IRecurringBillRepository _repository = Substitute.For<IRecurringBillRepository>();
    private readonly IPayeeRepository _payees = Substitute.For<IPayeeRepository>();
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly RecurringBillService _service;

    public RecurringBillServiceTests()
    {
        var clock = Substitute.For<IClock>();
        clock.Today.Returns(TestData.Today);
        _payees.ExistsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(true);
        _categories.ExistsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(true);
        _service = new RecurringBillService(
            _repository,
            _payees,
            _categories,
            clock,
            Options.Create(new BillingOptions { GenerationLookaheadDays = 10 }),
            NullLogger<RecurringBillService>.Instance);
    }

    [Fact]
    public async Task GenerateUpcomingBillsAsync_ActiveTemplate_SavesBillsUpToLookahead()
    {
        var template = Template(1, start: TestData.Today.AddDays(-14), BillFrequency.Weekly);
        _repository.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([template]);

        var created = await _service.GenerateUpcomingBillsAsync(TestContext.Current.CancellationToken);

        // Start-14, start-7, today and today+7 fall within [start, today+10].
        created.ShouldBe(4);
        await _repository.Received(1).SaveGeneratedBillsAsync(
            template,
            TestData.RowVersion,
            Arg.Is<IReadOnlyList<Bill>>(bills => bills.Count == 4 && bills.All(b => b.RecurringBillId == 1)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateUpcomingBillsAsync_ConflictOnOneTemplate_SkipsItAndContinues()
    {
        var conflicting = Template(1, TestData.Today, BillFrequency.Monthly);
        var other = Template(2, TestData.Today, BillFrequency.Monthly);
        _repository.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([conflicting, other]);
        _repository.SaveGeneratedBillsAsync(conflicting, Arg.Any<byte[]>(), Arg.Any<IReadOnlyList<Bill>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConcurrencyConflictException());

        var created = await _service.GenerateUpcomingBillsAsync(TestContext.Current.CancellationToken);

        created.ShouldBe(1);
        await _repository.Received(1).SaveGeneratedBillsAsync(other, Arg.Any<byte[]>(), Arg.Any<IReadOnlyList<Bill>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_EndBeforeStart_ReturnsValidationError()
    {
        var request = new SaveRecurringBillRequest(null, "Rent", 1, 1, 900m, BillFrequency.Monthly, TestData.Today, TestData.Today.AddDays(-1), null, null);

        var result = await _service.SaveAsync(request, TestContext.Current.CancellationToken);

        result.Error!.Kind.ShouldBe(ErrorKind.Validation);
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ListAsync_ActiveAndInactive_PopulatesNextDueDateOnlyForActive()
    {
        _repository.ListAsync(Arg.Any<CancellationToken>()).Returns([
            Dto(1, isActive: true, start: new DateOnly(2026, 1, 30)),
            Dto(2, isActive: false, start: new DateOnly(2026, 1, 30)),
        ]);

        var items = await _service.ListAsync(TestContext.Current.CancellationToken);

        items[0].NextDueDate.ShouldBe(new DateOnly(2026, 9, 30));
        items[1].NextDueDate.ShouldBeNull();
    }

    private static RecurringBill Template(int id, DateOnly start, BillFrequency frequency) =>
        TestData.Persisted(new RecurringBill($"Template {id}", 1, 1, 50m, new RecurrenceSchedule(frequency, start, null), null), id);

    private static RecurringBillDto Dto(int id, bool isActive, DateOnly start) =>
        new(id, "Rent", 1, "Landlord", 1, "Rent", 900m, BillFrequency.Monthly, start, null, null, isActive, null, TestData.RowVersion);

    [Fact]
    public async Task GenerateUpcomingBillsAsync_AmountVaries_EstimatesFromLastActualAmount()
    {
        var template = TestData.Persisted(
            new RecurringBill("Electricity", 1, 1, 50m, new RecurrenceSchedule(BillFrequency.Monthly, TestData.Today, null), null, amountVaries: true), 7);
        _repository.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([template]);
        _repository.GetLastActualAmountsAsync(Arg.Any<CancellationToken>()).Returns(new Dictionary<int, decimal> { [7] = 61.25m });

        await _service.GenerateUpcomingBillsAsync(TestContext.Current.CancellationToken);

        await _repository.Received(1).SaveGeneratedBillsAsync(
            template,
            Arg.Any<byte[]>(),
            Arg.Is<IReadOnlyList<Bill>>(bills => bills.Count > 0 && bills.All(b => b.Amount == 61.25m && b.IsEstimated)),
            Arg.Any<CancellationToken>());
    }
}