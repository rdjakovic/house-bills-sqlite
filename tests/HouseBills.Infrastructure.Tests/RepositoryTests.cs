using HouseBills.Application.Bills;
using HouseBills.Application.Common;
using HouseBills.Application.Persistence;
using HouseBills.Domain;

using Microsoft.EntityFrameworkCore;

namespace HouseBills.Infrastructure.Tests;

[Collection(SqliteCollection.Name)]
public sealed class RepositoryTests(SqliteFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ListAsync_FreshDatabase_ContainsSeededCategories()
    {
        var categories = await fixture.Get<ICategoryRepository>().ListAsync(Ct);

        categories.Select(c => c.Name).ShouldContain("Utilities");
    }

    [Fact]
    public async Task UpdateAsync_StaleRowVersion_ThrowsConcurrencyConflict()
    {
        var repository = fixture.Get<IPayeeRepository>();
        var payee = new Payee(Unique("Water Co"), null, null);
        await repository.AddAsync(payee, Ct);
        var staleVersion = payee.RowVersion;

        var firstEdit = (await repository.GetAsync(payee.Id, Ct))!;
        firstEdit.Update(firstEdit.Name, "A-1", null);
        await repository.UpdateAsync(firstEdit, staleVersion, Ct);

        var secondEdit = (await repository.GetAsync(payee.Id, Ct))!;
        secondEdit.Update(secondEdit.Name, "B-2", null);
        await Should.ThrowAsync<ConcurrencyConflictException>(() => repository.UpdateAsync(secondEdit, staleVersion, Ct));
        (await repository.GetAsync(payee.Id, Ct))!.AccountReference.ShouldBe("A-1");
    }

    [Fact]
    public async Task DeleteAsync_StaleRowVersion_ThrowsConcurrencyConflictAndKeepsRow()
    {
        var repository = fixture.Get<ICategoryRepository>();
        var category = new Category(Unique("Garden"));
        await repository.AddAsync(category, Ct);

        await Should.ThrowAsync<ConcurrencyConflictException>(() => repository.DeleteAsync(category.Id, [1, 2, 3, 4, 5, 6, 7, 8], Ct));
        (await repository.ExistsAsync(category.Id, Ct)).ShouldBeTrue();

        await repository.DeleteAsync(category.Id, category.RowVersion, Ct);
        (await repository.ExistsAsync(category.Id, Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task ListAsync_OverdueFilter_ReturnsOnlyUnpaidPastDueBillsWithNames()
    {
        var payee = new Payee(Unique("Filter payee"), null, null);
        await fixture.Get<IPayeeRepository>().AddAsync(payee, Ct);
        var bills = fixture.Get<IBillRepository>();
        var overdue = new Bill("Overdue", payee.Id, 1, 10m, SqliteFixture.Today.AddDays(-3), null);
        var paid = new Bill("Paid", payee.Id, 1, 10m, SqliteFixture.Today.AddDays(-3), null);
        paid.MarkPaid(SqliteFixture.Today, 10m);
        var upcoming = new Bill("Upcoming", payee.Id, 1, 10m, SqliteFixture.Today.AddDays(3), null);
        foreach (var bill in new[] { overdue, paid, upcoming })
        {
            await bills.AddAsync(bill, Ct);
        }

        var result = await bills.ListAsync(new BillFilter(null, null, BillStatusFilter.Overdue, PayeeId: payee.Id), SqliteFixture.Today, Ct);

        var item = result.ShouldHaveSingleItem();
        item.Description.ShouldBe("Overdue");
        item.PayeeName.ShouldBe(payee.Name);
        item.CategoryName.ShouldBe("Utilities");
    }

    [Fact]
    public async Task IsInUseAsync_PayeeWithBill_ReturnsTrue()
    {
        var payees = fixture.Get<IPayeeRepository>();
        var payee = new Payee(Unique("Used payee"), null, null);
        await payees.AddAsync(payee, Ct);
        await fixture.Get<IBillRepository>().AddAsync(new Bill("Gas", payee.Id, 1, 5m, SqliteFixture.Today, null), Ct);

        (await payees.IsInUseAsync(payee.Id, Ct)).ShouldBeTrue();
    }

    [Fact]
    public async Task NameExistsAsync_SameNameDifferentCase_ReturnsTrue()
    {
        (await fixture.Get<ICategoryRepository>().NameExistsAsync("UTILITIES", null, Ct)).ShouldBeTrue();
    }

    [Fact]
    public async Task AddAsync_NameDiffersOnlyInCase_ViolatesUniqueIndex()
    {
        var repository = fixture.Get<IPayeeRepository>();
        var name = Unique("Gas Co");
        await repository.AddAsync(new Payee(name, null, null), Ct);

        await Should.ThrowAsync<DbUpdateException>(() => repository.AddAsync(new Payee(name.ToUpperInvariant(), null, null), Ct));
    }

    [Theory]
    [InlineData("0.01")]
    [InlineData("1234.56")]
    [InlineData("9999999999999999.99")]
    public async Task GetAsync_StoredAmount_RoundTripsExactly(string amountText)
    {
        var amount = decimal.Parse(amountText, System.Globalization.CultureInfo.InvariantCulture);
        var payee = new Payee(Unique("Amount payee"), null, null);
        await fixture.Get<IPayeeRepository>().AddAsync(payee, Ct);
        var bills = fixture.Get<IBillRepository>();
        var bill = new Bill("Amount", payee.Id, 1, amount, SqliteFixture.Today, null);
        bill.MarkPaid(SqliteFixture.Today, amount);
        await bills.AddAsync(bill, Ct);

        var loaded = (await bills.GetAsync(bill.Id, Ct))!;

        loaded.Amount.ShouldBe(amount);
        loaded.PaidAmount.ShouldBe(amount);
    }

    [Theory]
    [InlineData("ELECTRIC", "Electricity")]
    [InlineData("power co", "Electricity")]
    [InlineData("insur", "Home cover")]
    [InlineData("meter 42", "Water")]
    [InlineData("100%", "Discount")]
    public async Task ListAsync_Search_MatchesDescriptionPayeeCategoryOrNotesIgnoringCase(string search, string expected)
    {
        var tag = Guid.NewGuid().ToString("N");
        var payees = fixture.Get<IPayeeRepository>();
        var power = new Payee($"Power Co {tag}", null, null);
        var other = new Payee($"Other {tag}", null, null);
        await payees.AddAsync(power, Ct);
        await payees.AddAsync(other, Ct);
        var bills = fixture.Get<IBillRepository>();
        await bills.AddAsync(new Bill("Electricity", power.Id, 1, 10m, SqliteFixture.Today, null), Ct);
        await bills.AddAsync(new Bill("Home cover", other.Id, 4, 10m, SqliteFixture.Today, null), Ct);
        await bills.AddAsync(new Bill("Water", other.Id, 1, 10m, SqliteFixture.Today, "Meter 42"), Ct);
        await bills.AddAsync(new Bill("Discount", other.Id, 7, 10m, SqliteFixture.Today, "100% off"), Ct);
        await bills.AddAsync(new Bill("Rent", other.Id, 2, 10m, SqliteFixture.Today, "1000 off"), Ct);

        var found = new List<BillListItem>();
        foreach (var payeeId in new[] { power.Id, other.Id })
        {
            found.AddRange(await bills.ListAsync(new BillFilter(null, null, PayeeId: payeeId, Search: search), SqliteFixture.Today, Ct));
        }

        found.ShouldHaveSingleItem().Description.ShouldBe(expected);
    }

    [Fact]
    public async Task ListAsync_BlankSearch_DoesNotFilter()
    {
        var payee = new Payee(Unique("Blank search"), null, null);
        await fixture.Get<IPayeeRepository>().AddAsync(payee, Ct);
        await fixture.Get<IBillRepository>().AddAsync(new Bill("Any", payee.Id, 1, 10m, SqliteFixture.Today, null), Ct);

        var result = await fixture.Get<IBillRepository>().ListAsync(new BillFilter(null, null, PayeeId: payee.Id, Search: "   "), SqliteFixture.Today, Ct);

        result.ShouldHaveSingleItem();
    }

    private static string Unique(string name) => $"{name} {Guid.NewGuid():N}";
}