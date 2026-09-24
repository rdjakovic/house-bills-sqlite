using HouseBills.Application.Bills;
using HouseBills.Application.Payees;
using HouseBills.Application.Persistence;
using HouseBills.Application.RecurringBills;
using HouseBills.Domain;

namespace HouseBills.Infrastructure.Tests;

[Collection(SqliteCollection.Name)]
public sealed class RecurringBillGenerationTests(SqliteFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GenerateUpcomingBillsAsync_RunTwice_CreatesEachOccurrenceOnce()
    {
        var payeeId = (await fixture.Get<IPayeeService>().SaveAsync(new SavePayeeRequest(null, $"Landlord {Guid.NewGuid():N}", null, null, null), Ct)).Value;
        var recurring = fixture.Get<IRecurringBillService>();
        var start = SqliteFixture.Today.AddMonths(-2);
        var saved = await recurring.SaveAsync(
            new SaveRecurringBillRequest(null, "Rent", payeeId, 2, 950m, BillFrequency.Monthly, start, null, null, null),
            Ct);
        saved.IsSuccess.ShouldBeTrue();

        await recurring.GenerateUpcomingBillsAsync(Ct);
        await recurring.GenerateUpcomingBillsAsync(Ct);

        var bills = await fixture.Get<IBillService>().ListAsync(new BillFilter(null, null, PayeeId: payeeId), Ct);

        // Lookahead is 31 days (through 2026-10-25): start, +1 month, +2 months (= today) and +3 months.
        bills.Select(b => b.DueDate).ShouldBe([start, start.AddMonths(1), start.AddMonths(2), start.AddMonths(3)]);
        bills.ShouldAllBe(b => b.RecurringBillId == saved.Value && b.Amount == 950m);
        (await recurring.ListAsync(Ct)).Single(r => r.Id == saved.Value).GeneratedThrough.ShouldBe(SqliteFixture.Today.AddDays(31));
    }

    [Fact]
    public async Task DeleteAsync_TemplateWithGeneratedBills_KeepsBillsAndClearsLink()
    {
        var payeeId = (await fixture.Get<IPayeeService>().SaveAsync(new SavePayeeRequest(null, $"ISP {Guid.NewGuid():N}", null, null, null), Ct)).Value;
        var recurring = fixture.Get<IRecurringBillService>();
        var id = (await recurring.SaveAsync(
            new SaveRecurringBillRequest(null, "Internet", payeeId, 3, 30m, BillFrequency.Monthly, SqliteFixture.Today, null, null, null),
            Ct)).Value;
        await recurring.GenerateUpcomingBillsAsync(Ct);
        var template = (await recurring.ListAsync(Ct)).Single(r => r.Id == id);

        var result = await recurring.DeleteAsync(id, template.RowVersion, Ct);

        result.IsSuccess.ShouldBeTrue();
        // Today and today + 1 month fall within the 31-day lookahead.
        var bills = await fixture.Get<IBillService>().ListAsync(new BillFilter(null, null, PayeeId: payeeId), Ct);
        bills.Count.ShouldBe(2);
        bills.ShouldAllBe(b => b.RecurringBillId == null);
    }

    [Fact]
    public async Task AmountVaries_GeneratedThenPaid_StoresEstimateAndReturnsLastActualAmount()
    {
        var payeeId = (await fixture.Get<IPayeeService>().SaveAsync(new SavePayeeRequest(null, $"Power {Guid.NewGuid():N}", null, null, null), Ct)).Value;
        var recurring = fixture.Get<IRecurringBillService>();
        var bills = fixture.Get<IBillService>();
        var repository = fixture.Get<IRecurringBillRepository>();

        // Only 2026-10-20 falls within the 31-day lookahead (through 2026-10-25).
        var templateId = (await recurring.SaveAsync(
            new SaveRecurringBillRequest(null, "Electricity", payeeId, 1, 80m, BillFrequency.Monthly, new DateOnly(2026, 10, 20), null, null, null, AmountVaries: true),
            Ct)).Value;
        await recurring.GenerateUpcomingBillsAsync(Ct);

        var generated = (await bills.ListAsync(new BillFilter(null, null, PayeeId: payeeId), Ct)).ShouldHaveSingleItem();
        generated.ShouldSatisfyAllConditions(b => b.Amount.ShouldBe(80m), b => b.IsEstimated.ShouldBeTrue());
        (await recurring.ListAsync(Ct)).Single(r => r.Id == templateId).AmountVaries.ShouldBeTrue();
        (await repository.GetLastActualAmountsAsync(Ct)).ContainsKey(templateId).ShouldBeFalse();

        (await bills.MarkPaidAsync(new MarkBillPaidRequest(generated.Id, SqliteFixture.Today, 93.10m, generated.RowVersion), Ct)).IsSuccess.ShouldBeTrue();

        (await bills.ListAsync(new BillFilter(null, null, PayeeId: payeeId), Ct)).ShouldHaveSingleItem()
            .ShouldSatisfyAllConditions(b => b.Amount.ShouldBe(93.10m), b => b.IsEstimated.ShouldBeFalse());
        (await repository.GetLastActualAmountsAsync(Ct))[templateId].ShouldBe(93.10m);
    }
}