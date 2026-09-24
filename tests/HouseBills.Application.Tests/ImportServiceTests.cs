using HouseBills.Application.Bills;
using HouseBills.Application.Categories;
using HouseBills.Application.Common;
using HouseBills.Application.Import;
using HouseBills.Application.Payees;
using HouseBills.Application.Persistence;
using HouseBills.Domain;

using NSubstitute;

namespace HouseBills.Application.Tests;

public sealed class ImportServiceTests
{
    private static readonly DateOnly Due = new(2026, 9, 15);

    private readonly IImportRepository _importRepository = Substitute.For<IImportRepository>();
    private readonly IPayeeRepository _payees = Substitute.For<IPayeeRepository>();
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly IBillRepository _bills = Substitute.For<IBillRepository>();
    private readonly ImportService _service;

    private IReadOnlyList<Payee> _savedPayees = [];
    private IReadOnlyList<Category> _savedCategories = [];
    private IReadOnlyList<Bill> _savedBills = [];

    public ImportServiceTests()
    {
        var clock = Substitute.For<IClock>();
        clock.Today.Returns(TestData.Today);
        _payees.ListAsync(Arg.Any<CancellationToken>()).Returns([new PayeeDto(1, "EPS", null, null, TestData.RowVersion)]);
        _categories.ListAsync(Arg.Any<CancellationToken>()).Returns([new CategoryDto(1, "Utilities", TestData.RowVersion)]);
        _bills.ListAsync(Arg.Any<BillFilter>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns([]);

        // Stands in for the database: new payees/categories get ids before the bills are built.
        _importRepository
            .When(r => r.AddAsync(Arg.Any<IReadOnlyList<Payee>>(), Arg.Any<IReadOnlyList<Category>>(), Arg.Any<Func<IReadOnlyList<Bill>>>(), Arg.Any<CancellationToken>()))
            .Do(call =>
            {
                _savedPayees = call.ArgAt<IReadOnlyList<Payee>>(0);
                _savedCategories = call.ArgAt<IReadOnlyList<Category>>(1);
                var nextId = 100;
                foreach (var payee in _savedPayees)
                {
                    TestData.Persisted(payee, nextId++);
                }

                foreach (var category in _savedCategories)
                {
                    TestData.Persisted(category, nextId++);
                }

                _savedBills = call.ArgAt<Func<IReadOnlyList<Bill>>>(2)();
            });
        _service = new ImportService(_importRepository, _payees, _categories, _bills, clock);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ImportPayeesAsync_ExistingAndRepeatedNames_AddsOnlyNewOnes()
    {
        var result = await _service.ImportPayeesAsync(
            [new ImportedPayee(2, "eps", null, null), new ImportedPayee(3, " Infostan ", "123", "water"), new ImportedPayee(4, "INFOSTAN", null, null)],
            Ct);

        result.Value.ShouldBe(new ImportSummary(1, 2));
        _savedPayees.ShouldHaveSingleItem().Name.ShouldBe("Infostan");
        _savedPayees[0].AccountReference.ShouldBe("123");
    }

    [Fact]
    public async Task ImportCategoriesAsync_InvalidRows_ReportsEachWithRowNumberAndWritesNothing()
    {
        var result = await _service.ImportCategoriesAsync(
            [new ImportedCategory(2, "Pets"), new ImportedCategory(3, " "), new ImportedCategory(4, new string('x', Category.NameMaxLength + 1))],
            Ct);

        result.Error!.Kind.ShouldBe(ErrorKind.Validation);
        result.Error.Message.Split(Environment.NewLine).ShouldBe([
            "Row 3: Name is required.",
            $"Row 4: Name must be at most {Category.NameMaxLength} characters.",
        ]);
        await _importRepository.DidNotReceive().AddAsync(Arg.Any<IReadOnlyList<Payee>>(), Arg.Any<IReadOnlyList<Category>>(), Arg.Any<Func<IReadOnlyList<Bill>>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportBillsAsync_UnknownPayeeAndCategory_CreatesThemOnceAndLinksTheBills()
    {
        var result = await _service.ImportBillsAsync(
            [Bill(2, payee: "Infostan", category: "Water"), Bill(3, payee: "infostan", category: "WATER", due: Due.AddMonths(1)), Bill(4)],
            Ct);

        result.Value.ShouldBe(new ImportSummary(3, 0, PayeesCreated: 1, CategoriesCreated: 1));
        _savedPayees.ShouldHaveSingleItem().Name.ShouldBe("Infostan");
        _savedCategories.ShouldHaveSingleItem().Name.ShouldBe("Water");
        _savedBills.Select(b => (b.PayeeId, b.CategoryId)).ShouldBe([(100, 101), (100, 101), (1, 1)]);
    }

    [Fact]
    public async Task ImportBillsAsync_SameBillInDatabaseOrTwiceInFile_SkipsIt()
    {
        _bills.ListAsync(Arg.Is<BillFilter>(f => f.DueFrom == Due && f.DueTo == Due.AddMonths(1)), TestData.Today, Arg.Any<CancellationToken>())
            .Returns([new BillListItem(9, "POWER", 1, "eps", 1, "Utilities", 100.00m, Due, null, null, null, null, TestData.RowVersion)]);

        var result = await _service.ImportBillsAsync(
            [Bill(2), Bill(3, due: Due.AddMonths(1)), Bill(4, due: Due.AddMonths(1)), Bill(5, amount: 101m)],
            Ct);

        result.Value.ShouldBe(new ImportSummary(2, 2));
        _savedBills.Select(b => (b.DueDate, b.Amount)).ShouldBe([(Due.AddMonths(1), 100m), (Due, 101m)]);
    }

    [Fact]
    public async Task ImportBillsAsync_PaidRows_AddsThemAsPaid()
    {
        var result = await _service.ImportBillsAsync(
            [Bill(2, paidOn: Due, paidAmount: 95.5m), Bill(3, amount: 80m, estimated: true, paidOn: Due, due: Due.AddDays(1))],
            Ct);

        result.IsSuccess.ShouldBeTrue();
        _savedBills[0].PaidAmount.ShouldBe(95.5m);
        _savedBills[1].PaidAmount.ShouldBe(80m);
        _savedBills[1].IsEstimated.ShouldBeFalse();
    }

    [Fact]
    public async Task ImportBillsAsync_PaidAmountWithoutDateOrFuturePayment_RejectsTheFile()
    {
        var result = await _service.ImportBillsAsync(
            [Bill(2, paidAmount: 10m), Bill(3, paidOn: TestData.Today.AddDays(1)), Bill(4, amount: 0m, payee: "")],
            Ct);

        result.Error!.Message.Split(Environment.NewLine).ShouldBe([
            "Row 2: The paid amount is filled in, but the payment date is empty.",
            "Row 3: Payment date cannot be in the future.",
            "Row 4: Payee is required.",
            "Row 4: Amount must be greater than zero, with at most two decimal places.",
        ]);
        await _importRepository.DidNotReceive().AddAsync(Arg.Any<IReadOnlyList<Payee>>(), Arg.Any<IReadOnlyList<Category>>(), Arg.Any<Func<IReadOnlyList<Bill>>>(), Arg.Any<CancellationToken>());
    }

    private static ImportedBill Bill(
        int row,
        string payee = "EPS",
        string category = "Utilities",
        decimal amount = 100m,
        DateOnly? due = null,
        bool estimated = false,
        DateOnly? paidOn = null,
        decimal? paidAmount = null) =>
        new(row, "Power", payee, category, amount, due ?? Due, estimated, paidOn, paidAmount, null);
}