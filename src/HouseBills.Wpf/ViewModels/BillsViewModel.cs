using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HouseBills.Application.Bills;
using HouseBills.Application.Categories;
using HouseBills.Application.Common;
using HouseBills.Application.Payees;
using HouseBills.Application.RecurringBills;
using HouseBills.Presentation.Resources;
using HouseBills.Wpf.Localization;
using HouseBills.Wpf.Services;
using HouseBills.Wpf.ViewModels.Bills;

using Microsoft.Extensions.Logging;

namespace HouseBills.Wpf.ViewModels;

public sealed partial class BillsViewModel : PageViewModel
{
    private readonly IBillService _bills;
    private readonly IRecurringBillService _recurringBills;
    private readonly IPayeeService _payees;
    private readonly ICategoryService _categories;
    private readonly IClock _clock;
    private bool _hasGeneratedBills;

    public BillsViewModel(
        IBillService bills,
        IRecurringBillService recurringBills,
        IPayeeService payees,
        ICategoryService categories,
        IClock clock,
        IDialogService dialogs,
        ILogger<BillsViewModel> logger)
        : base(dialogs, logger)
    {
        _bills = bills;
        _recurringBills = recurringBills;
        _payees = payees;
        _categories = categories;
        _clock = clock;
        StatusFilter = BillStatusFilter.Unpaid;
    }

    public override string Title => Strings.Page_Bills;

    public ObservableCollection<BillListItem> Bills { get; } = [];

    public ObservableCollection<PayeeDto> Payees { get; } = [];

    public ObservableCollection<CategoryDto> Categories { get; } = [];

    public IReadOnlyList<BillStatusFilter> StatusFilters { get; } = Enum.GetValues<BillStatusFilter>();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand), nameof(DeleteCommand), nameof(StartPaymentCommand), nameof(MarkUnpaidCommand))]
    public partial BillListItem? SelectedBill { get; set; }

    [ObservableProperty]
    public partial BillStatusFilter StatusFilter { get; set; }

    [ObservableProperty]
    public partial DateOnly? DueFrom { get; set; }

    [ObservableProperty]
    public partial DateOnly? DueTo { get; set; }

    [ObservableProperty]
    public partial int? CategoryFilterId { get; set; }

    [ObservableProperty]
    public partial int? PayeeFilterId { get; set; }

    [ObservableProperty]
    public partial string? SearchText { get; set; }

    /// <summary>Sum of the listed bills' amounts.</summary>
    [ObservableProperty]
    public partial decimal TotalAmount { get; set; }

    /// <summary>Sum of the listed unpaid bills' amounts.</summary>
    [ObservableProperty]
    public partial decimal OutstandingAmount { get; set; }

    [ObservableProperty]
    public partial BillEditorViewModel? Editor { get; set; }

    [ObservableProperty]
    public partial PaymentEditorViewModel? Payment { get; set; }

    public override Task OnNavigatedToAsync()
    {
        return RunAsync(
            async () =>
            {
                if (!_hasGeneratedBills)
                {
                    await _recurringBills.GenerateUpcomingBillsAsync(CancellationToken.None);
                    _hasGeneratedBills = true;
                }

                await LoadLookupsAsync(CancellationToken.None);
                await LoadBillsAsync(CancellationToken.None);
            },
            Strings.Bills_LoadFailed);
    }

    [RelayCommand]
    private Task RefreshAsync(CancellationToken cancellationToken)
    {
        return RunAsync(() => LoadBillsAsync(cancellationToken), Strings.Bills_LoadFailed);
    }

    [RelayCommand]
    private Task ClearFiltersAsync(CancellationToken cancellationToken)
    {
        StatusFilter = BillStatusFilter.All;
        DueFrom = null;
        DueTo = null;
        CategoryFilterId = null;
        PayeeFilterId = null;
        SearchText = null;
        return RefreshAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task GenerateAsync(CancellationToken cancellationToken)
    {
        var created = 0;
        var succeeded = await RunAsync(
            async () =>
            {
                created = await _recurringBills.GenerateUpcomingBillsAsync(cancellationToken);
                await LoadBillsAsync(cancellationToken);
            },
            Strings.Bills_GenerateFailed);

        if (succeeded)
        {
            Dialogs.ShowInfo(created == 0 ? Strings.Bills_NoneGenerated : string.Format(LocalizedStrings.FormattingCulture, Strings.Bills_Generated, created));
        }
    }

    [RelayCommand]
    private void New()
    {
        Payment = null;
        Editor = new BillEditorViewModel(null, _clock.Today, Payees, Categories);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Edit()
    {
        Payment = null;
        Editor = new BillEditorViewModel(SelectedBill, _clock.Today, Payees, Categories);
    }

    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (Editor is not { } editor)
        {
            return;
        }

        if (await SubmitAsync(editor, async () => await _bills.SaveAsync(editor.ToRequest(), cancellationToken), () => LoadBillsAsync(cancellationToken)))
        {
            Editor = null;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        Editor = null;
        Payment = null;
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteAsync(CancellationToken cancellationToken)
    {
        if (SelectedBill is not { } bill || !Dialogs.Confirm(Strings.Bills_DeleteTitle, string.Format(LocalizedStrings.FormattingCulture, Strings.Bills_DeleteConfirm, bill.Description, bill.DueDate)))
        {
            return;
        }

        Cancel();
        await ExecuteAndReloadAsync(() => _bills.DeleteAsync(bill.Id, bill.RowVersion, cancellationToken), () => LoadBillsAsync(cancellationToken), Strings.Bills_DeleteFailed);
    }

    [RelayCommand(CanExecute = nameof(IsUnpaidSelected))]
    private void StartPayment()
    {
        Editor = null;
        Payment = new PaymentEditorViewModel(SelectedBill!, _clock.Today);
    }

    [RelayCommand]
    private async Task ConfirmPaymentAsync(CancellationToken cancellationToken)
    {
        if (Payment is not { } payment)
        {
            return;
        }

        if (await SubmitAsync(payment, () => _bills.MarkPaidAsync(payment.ToRequest(), cancellationToken), () => LoadBillsAsync(cancellationToken)))
        {
            Payment = null;
        }
    }

    [RelayCommand(CanExecute = nameof(IsPaidSelected))]
    private Task MarkUnpaidAsync(CancellationToken cancellationToken)
    {
        var bill = SelectedBill!;
        return ExecuteAndReloadAsync(() => _bills.MarkUnpaidAsync(bill.Id, bill.RowVersion, cancellationToken), () => LoadBillsAsync(cancellationToken), Strings.Bills_UpdateFailed);
    }

    private bool HasSelection() => SelectedBill is not null;

    private bool IsUnpaidSelected() => SelectedBill is { PaidOn: null };

    private bool IsPaidSelected() => SelectedBill is { PaidOn: not null };

    private async Task LoadLookupsAsync(CancellationToken cancellationToken)
    {
        var payees = await _payees.ListAsync(cancellationToken);
        var categories = await _categories.ListAsync(cancellationToken);
        Payees.Clear();
        foreach (var payee in payees)
        {
            Payees.Add(payee);
        }

        Categories.Clear();
        foreach (var category in categories)
        {
            Categories.Add(category);
        }
    }

    private async Task LoadBillsAsync(CancellationToken cancellationToken)
    {
        var filter = new BillFilter(DueFrom, DueTo, StatusFilter, CategoryFilterId, PayeeFilterId, SearchText);
        var bills = await _bills.ListAsync(filter, cancellationToken);
        Bills.Clear();
        foreach (var bill in bills)
        {
            Bills.Add(bill);
        }

        TotalAmount = bills.Sum(b => b.Amount);
        OutstandingAmount = bills.Where(b => b.PaidOn is null).Sum(b => b.Amount);
    }
}