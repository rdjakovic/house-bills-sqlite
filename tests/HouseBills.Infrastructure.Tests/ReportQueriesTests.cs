using HouseBills.Application.Payees;
using HouseBills.Application.Persistence;
using HouseBills.Application.RecurringBills;
using HouseBills.Application.Reports;
using HouseBills.Domain;

namespace HouseBills.Infrastructure.Tests;

[Collection(SqliteCollection.Name)]
public sealed class ReportQueriesTests(SqliteFixture fixture)
{
    // Years no other test writes to, so totals are deterministic.
    private const int Year = 2041;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Reports_BillsAcrossTwoYears_ReturnMonthlyAndCategoryTotals()
    {
        var payee = new Payee($"Report payee {Guid.NewGuid():N}", null, null);
        await fixture.Get<IPayeeRepository>().AddAsync(payee, Ct);
        var bills = fixture.Get<IBillRepository>();
        var paid = new Bill("Power", payee.Id, 1, 100m, new DateOnly(Year, 3, 1), null);
        paid.MarkPaid(new DateOnly(Year, 3, 2), 95.50m);
        await bills.AddAsync(paid, Ct);
        await bills.AddAsync(new Bill("Insurance", payee.Id, 4, 250m, new DateOnly(Year, 3, 31), null), Ct);
        await bills.AddAsync(new Bill("Power", payee.Id, 1, 80m, new DateOnly(Year - 1, 3, 15), null), Ct);
        await bills.AddAsync(new Bill("Outside range", payee.Id, 1, 999m, new DateOnly(Year + 1, 1, 1), null), Ct);
        var reports = fixture.Get<IReportQueries>();

        var months = await reports.GetMonthlySummaryAsync(Year, null, Ct);
        var categories = await reports.GetCategoryTotalsAsync(new DateOnly(Year, 1, 1), new DateOnly(Year, 12, 31), null, Ct);

        months.Count.ShouldBe(12);
        months[2].ShouldBe(new MonthlySummaryRow(3, 2, 350m, 95.50m, 250m, 80m));
        months.Where(m => m.Month != 3).ShouldAllBe(m => m.BillCount == 0 && m.TotalAmount == 0m);
        categories.ShouldBe([
            new CategoryTotalRow("Insurance", 1, 250m, 0m),
            new CategoryTotalRow("Utilities", 1, 100m, 95.50m),
        ]);
    }

    [Fact]
    public async Task GetMonthlySummaryAsync_FractionalAmounts_SumsExactly()
    {
        const int year = Year + 5;
        var payee = new Payee($"Cents payee {Guid.NewGuid():N}", null, null);
        await fixture.Get<IPayeeRepository>().AddAsync(payee, Ct);
        var bills = fixture.Get<IBillRepository>();
        await bills.AddAsync(new Bill("A", payee.Id, 1, 0.10m, new DateOnly(year, 5, 1), null), Ct);
        await bills.AddAsync(new Bill("B", payee.Id, 1, 0.20m, new DateOnly(year, 5, 2), null), Ct);

        var months = await fixture.Get<IReportQueries>().GetMonthlySummaryAsync(year, null, Ct);

        months[4].TotalAmount.ShouldBe(0.30m);
        months[4].OutstandingAmount.ShouldBe(0.30m);
    }

    [Fact]
    public async Task Reports_RecurringBillGiven_CountOnlyBillsGeneratedFromIt()
    {
        // A past year no other test writes to: the template generates all 12 months of it at once.
        const int year = 2016;
        var payeeId = (await fixture.Get<IPayeeService>().SaveAsync(new SavePayeeRequest(null, $"Landlord {Guid.NewGuid():N}", null, null, null), Ct)).Value;
        var recurring = fixture.Get<IRecurringBillService>();
        var rentId = (await recurring.SaveAsync(
            new SaveRecurringBillRequest(null, "Rent", payeeId, 2, 500m, BillFrequency.Monthly, new DateOnly(year, 1, 5), new DateOnly(year, 12, 31), null, null),
            Ct)).Value;
        await recurring.GenerateUpcomingBillsAsync(Ct);
        await fixture.Get<IBillRepository>().AddAsync(new Bill("One-off repair", payeeId, 2, 120m, new DateOnly(year, 3, 10), null), Ct);
        var reports = fixture.Get<IReportQueries>();

        var rentMonths = await reports.GetMonthlySummaryAsync(year, rentId, Ct);
        var rentCategories = await reports.GetCategoryTotalsAsync(new DateOnly(year, 1, 1), new DateOnly(year, 12, 31), rentId, Ct);
        var allMonths = await reports.GetMonthlySummaryAsync(year, null, Ct);

        rentMonths.ShouldAllBe(m => m.BillCount == 1 && m.TotalAmount == 500m);
        rentCategories.ShouldHaveSingleItem().ShouldBe(new CategoryTotalRow("Rent / Mortgage", 12, 6000m, 0m));
        allMonths[2].ShouldBe(new MonthlySummaryRow(3, 2, 620m, 0m, 620m, 0m));
    }
}