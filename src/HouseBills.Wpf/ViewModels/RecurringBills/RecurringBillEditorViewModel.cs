using System.ComponentModel.DataAnnotations;

using CommunityToolkit.Mvvm.ComponentModel;

using HouseBills.Application.Categories;
using HouseBills.Application.Payees;
using HouseBills.Application.RecurringBills;
using HouseBills.Domain;
using HouseBills.Presentation.Resources;

namespace HouseBills.Wpf.ViewModels.RecurringBills;

public sealed partial class RecurringBillEditorViewModel : EditorViewModel
{
    public RecurringBillEditorViewModel(RecurringBillDto? template, DateOnly defaultStartDate, IReadOnlyList<PayeeDto> payees, IReadOnlyList<CategoryDto> categories)
    {
        Id = template?.Id;
        RowVersion = template?.RowVersion;
        Payees = payees;
        Categories = categories;
        Name = template?.Name ?? string.Empty;
        PayeeId = template?.PayeeId;
        CategoryId = template?.CategoryId;
        Amount = template?.Amount;
        Frequency = template?.Frequency ?? BillFrequency.Monthly;
        StartDate = template?.StartDate ?? defaultStartDate;
        EndDate = template?.EndDate;
        Notes = template?.Notes;
        AmountVaries = template?.AmountVaries ?? false;
        ResetValidation();
    }

    public int? Id { get; }

    public byte[]? RowVersion { get; }

    public IReadOnlyList<PayeeDto> Payees { get; }

    public IReadOnlyList<CategoryDto> Categories { get; }

    public IReadOnlyList<BillFrequency> Frequencies { get; } = Enum.GetValues<BillFrequency>();

    public override string Title => Id is null ? Strings.Recurring_EditorNewTitle : Strings.Recurring_EditorEditTitle;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessageResourceType = typeof(Strings), ErrorMessageResourceName = nameof(Strings.Validation_NameRequired))]
    [MaxLength(RecurringBill.NameMaxLength, ErrorMessageResourceType = typeof(Strings), ErrorMessageResourceName = nameof(Strings.Validation_MaxLength))]
    public partial string Name { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessageResourceType = typeof(Strings), ErrorMessageResourceName = nameof(Strings.Validation_SelectPayee))]
    public partial int? PayeeId { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessageResourceType = typeof(Strings), ErrorMessageResourceName = nameof(Strings.Validation_SelectCategory))]
    public partial int? CategoryId { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessageResourceType = typeof(Strings), ErrorMessageResourceName = nameof(Strings.Validation_AmountRequired))]
    [Range(typeof(decimal), "0.01", "9999999999999999.99", ParseLimitsInInvariantCulture = true, ErrorMessageResourceType = typeof(Strings), ErrorMessageResourceName = nameof(Strings.Validation_AmountPositive))]
    public partial decimal? Amount { get; set; }

    [ObservableProperty]
    public partial BillFrequency Frequency { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessageResourceType = typeof(Strings), ErrorMessageResourceName = nameof(Strings.Validation_FirstDueDateRequired))]
    public partial DateOnly? StartDate { get; set; }

    [ObservableProperty]
    public partial DateOnly? EndDate { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [MaxLength(RecurringBill.NotesMaxLength, ErrorMessageResourceType = typeof(Strings), ErrorMessageResourceName = nameof(Strings.Validation_MaxLength))]
    public partial string? Notes { get; set; }

    /// <summary>Generated bills are estimates based on the last actual amount.</summary>
    [ObservableProperty]
    public partial bool AmountVaries { get; set; }

    /// <summary>Builds the request; call only after <see cref="EditorViewModel.Validate"/> succeeded.</summary>
    public SaveRecurringBillRequest ToRequest()
    {
        return new SaveRecurringBillRequest(Id, Name, PayeeId!.Value, CategoryId!.Value, Amount!.Value, Frequency, StartDate!.Value, EndDate, Notes, RowVersion, AmountVaries);
    }
}