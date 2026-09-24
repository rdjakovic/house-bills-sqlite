using HouseBills.Application.Bills;
using HouseBills.Application.Import;
using HouseBills.Application.Payees;
using HouseBills.Application.Persistence;
using HouseBills.Domain;

using Microsoft.EntityFrameworkCore;

namespace HouseBills.Infrastructure.Tests;

[Collection(SqliteCollection.Name)]
public sealed class ImportTests(SqliteFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ImportBillsAsync_NewPayeeAndCategory_SavesAllAndSkipsThemOnSecondImport()
    {
        // A year no other test writes to.
        var due = new DateOnly(2014, 3, 10);
        var payee = $"Import payee {Guid.NewGuid():N}";
        var category = $"Import category {Guid.NewGuid():N}";
        ImportedBill[] rows =
        [
            new(2, "Garage rent", payee, category, 60m, due, false, due, 60m, "cash"),
            new(3, "Garage rent", payee, category, 60m, due.AddMonths(1), false, null, null, null),
        ];
        var imports = fixture.Get<IImportService>();

        var first = await imports.ImportBillsAsync(rows, Ct);
        var second = await imports.ImportBillsAsync(rows, Ct);

        first.Value.ShouldBe(new ImportSummary(2, 0, PayeesCreated: 1, CategoriesCreated: 1));
        second.Value.ShouldBe(new ImportSummary(0, 2));
        var payeeId = (await fixture.Get<IPayeeService>().ListAsync(Ct)).Single(p => p.Name == payee).Id;
        var bills = await fixture.Get<IBillService>().ListAsync(new BillFilter(null, null, PayeeId: payeeId), Ct);
        bills.Select(b => (b.DueDate, b.CategoryName, b.PaidAmount, b.Notes)).ShouldBe([(due, category, 60m, "cash"), (due.AddMonths(1), category, (decimal?)null, (string?)null)]);
    }

    [Fact]
    public async Task AddAsync_BillsFail_RollsBackPayeesAndCategories()
    {
        var payee = new Payee($"Rolled back {Guid.NewGuid():N}", null, null);
        var category = new Category($"Rolled back {Guid.NewGuid():N}");

        await Should.ThrowAsync<InvalidOperationException>(() => fixture.Get<IImportRepository>().AddAsync(
            [payee],
            [category],
            () => throw new InvalidOperationException("bills could not be built"),
            Ct));

        await using var db = await fixture.Get<IDbContextFactory<Persistence.AppDbContext>>().CreateDbContextAsync(Ct);
        (await db.Payees.AnyAsync(p => p.Name == payee.Name, Ct)).ShouldBeFalse();
        (await db.Categories.AnyAsync(c => c.Name == category.Name, Ct)).ShouldBeFalse();
    }
}