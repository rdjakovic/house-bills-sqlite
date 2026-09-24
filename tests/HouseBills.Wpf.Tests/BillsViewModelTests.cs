using HouseBills.Application.Bills;
using HouseBills.Application.Categories;
using HouseBills.Application.Common;
using HouseBills.Application.Import;
using HouseBills.Application.Payees;
using HouseBills.Application.RecurringBills;
using HouseBills.Domain;
using HouseBills.Wpf.Services;
using HouseBills.Wpf.ViewModels;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace HouseBills.Wpf.Tests;

public sealed class BillsViewModelTests
{
    private static readonly DateOnly Today = new(2026, 9, 24);
    private static readonly byte[] RowVersion = [1, 2, 3, 4, 5, 6, 7, 8];

    private readonly IBillService _bills = Substitute.For<IBillService>();
    private readonly IRecurringBillService _recurring = Substitute.For<IRecurringBillService>();
    private readonly IPayeeService _payees = Substitute.For<IPayeeService>();
    private readonly ICategoryService _categories = Substitute.For<ICategoryService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly IFileSaver _files = Substitute.For<IFileSaver>();
    private readonly BillsViewModel _viewModel;

    public BillsViewModelTests()
    {
        var clock = Substitute.For<IClock>();
        clock.Today.Returns(Today);
        _payees.ListAsync(Arg.Any<CancellationToken>()).Returns([new PayeeDto(1, "Power Co", null, null, RowVersion)]);
        _categories.ListAsync(Arg.Any<CancellationToken>()).Returns([new CategoryDto(1, "Utilities", RowVersion)]);
        _bills.ListAsync(Arg.Any<BillFilter>(), Arg.Any<CancellationToken>()).Returns([
            Item(1, 100m, paidOn: null),
            Item(2, 40m, paidOn: Today),
        ]);
        _viewModel = new BillsViewModel(_bills, _recurring, _payees, _categories, clock, Substitute.For<IImportService>(), _files, Substitute.For<IFileReader>(), _dialogs, NullLogger<BillsViewModel>.Instance);
    }

    [Fact]
    public async Task OnNavigatedToAsync_VisitedTwice_GeneratesOnceAndLoadsBillsWithTotals()
    {
        await _viewModel.OnNavigatedToAsync();
        await _viewModel.OnNavigatedToAsync();

        await _recurring.Received(1).GenerateUpcomingBillsAsync(Arg.Any<CancellationToken>());
        _viewModel.Bills.Count.ShouldBe(2);
        _viewModel.Payees.ShouldHaveSingleItem();
        _viewModel.TotalAmount.ShouldBe(140m);
        _viewModel.OutstandingAmount.ShouldBe(100m);
        _viewModel.IsBusy.ShouldBeFalse();
    }

    [Fact]
    public async Task Refresh_SearchText_IsPassedToServiceAndClearedByClearFilters()
    {
        _viewModel.SearchText = "struja";

        await _viewModel.RefreshCommand.ExecuteAsync(null);
        await _viewModel.ClearFiltersCommand.ExecuteAsync(null);

        await _bills.Received(1).ListAsync(Arg.Is<BillFilter>(f => f.Search == "struja"), Arg.Any<CancellationToken>());
        _viewModel.SearchText.ShouldBeNull();
        await _bills.Received(1).ListAsync(Arg.Is<BillFilter>(f => f.Search == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Export_LocationChosen_SavesTheListedBillsAsCsv()
    {
        await _viewModel.OnNavigatedToAsync();
        _dialogs.PickCsvSaveLocation("HouseBills-2026-09-24.csv").Returns(@"D:\bills.csv");

        await _viewModel.ExportCommand.ExecuteAsync(null);

        await _files.Received(1).SaveTextAsync(@"D:\bills.csv", Arg.Is<string>(csv => csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length == 3), Arg.Any<CancellationToken>());
        _dialogs.Received(1).ShowInfo(Arg.Is<string>(m => m.Contains(@"D:\bills.csv")));
    }

    [Fact]
    public async Task Export_FileOpenElsewhere_ShowsFriendlyError()
    {
        _dialogs.PickCsvSaveLocation(Arg.Any<string>()).Returns(@"D:\bills.csv");
        _files.SaveTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(new IOException("in use by EXCEL"));

        await _viewModel.ExportCommand.ExecuteAsync(null);

        _dialogs.Received(1).ShowError(Arg.Is<string>(m => !m.Contains("EXCEL")));
    }

    [Fact]
    public async Task OnNavigatedToAsync_ServiceThrows_ShowsFriendlyErrorAndClearsBusy()
    {
        _bills.ListAsync(Arg.Any<BillFilter>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("db down"));

        await _viewModel.OnNavigatedToAsync();

        _dialogs.Received(1).ShowError(Arg.Is<string>(m => m.StartsWith("Could not load bills.") && !m.Contains("db down")));
        _viewModel.IsBusy.ShouldBeFalse();
    }

    [Fact]
    public void RecordPayment_PaidOrUnpaidSelection_EnabledOnlyForUnpaid()
    {
        _viewModel.SelectedBill = Item(1, 100m, paidOn: null);
        _viewModel.StartPaymentCommand.CanExecute(null).ShouldBeTrue();
        _viewModel.MarkUnpaidCommand.CanExecute(null).ShouldBeFalse();

        _viewModel.SelectedBill = Item(2, 40m, paidOn: Today);
        _viewModel.StartPaymentCommand.CanExecute(null).ShouldBeFalse();
        _viewModel.MarkUnpaidCommand.CanExecute(null).ShouldBeTrue();
    }

    [Fact]
    public async Task ConfirmPayment_Success_MarksPaidWithDefaultsAndClosesPanel()
    {
        _bills.MarkPaidAsync(Arg.Any<MarkBillPaidRequest>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        _viewModel.SelectedBill = Item(1, 100m, paidOn: null);
        _viewModel.StartPaymentCommand.Execute(null);

        await _viewModel.ConfirmPaymentCommand.ExecuteAsync(null);

        await _bills.Received(1).MarkPaidAsync(new MarkBillPaidRequest(1, Today, 100m, RowVersion), Arg.Any<CancellationToken>());
        _viewModel.Payment.ShouldBeNull();
    }

    [Fact]
    public async Task Save_EmptyForm_ShowsValidationErrorsWithoutCallingService()
    {
        _viewModel.NewCommand.Execute(null);

        await _viewModel.SaveCommand.ExecuteAsync(null);

        _viewModel.Editor.ShouldNotBeNull();
        _viewModel.Editor.HasErrors.ShouldBeTrue();
        await _bills.DidNotReceiveWithAnyArgs().SaveAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Save_ServiceRejects_ShowsMessageInEditorAndKeepsItOpen()
    {
        _bills.SaveAsync(Arg.Any<SaveBillRequest>(), Arg.Any<CancellationToken>()).Returns(Error.Validation("Select a payee."));
        _viewModel.NewCommand.Execute(null);
        var editor = _viewModel.Editor!;
        editor.Description = "Water";
        editor.PayeeId = 1;
        editor.CategoryId = 1;
        editor.Amount = 12.5m;

        await _viewModel.SaveCommand.ExecuteAsync(null);

        _viewModel.Editor.ShouldBeSameAs(editor);
        editor.ErrorMessage.ShouldBe("Select a payee.");
    }

    [Fact]
    public async Task Delete_NotConfirmed_DoesNotCallService()
    {
        _dialogs.Confirm(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        _viewModel.SelectedBill = Item(1, 100m, paidOn: null);

        await _viewModel.DeleteCommand.ExecuteAsync(null);

        await _bills.DidNotReceiveWithAnyArgs().DeleteAsync(default, default!, TestContext.Current.CancellationToken);
    }

    private static BillListItem Item(int id, decimal amount, DateOnly? paidOn) =>
        new(id, $"Bill {id}", 1, "Power Co", 1, "Utilities", amount, Today, paidOn, paidOn is null ? null : amount, null, null, RowVersion)
        {
            Status = paidOn is null ? BillStatus.DueSoon : BillStatus.Paid,
        };
}