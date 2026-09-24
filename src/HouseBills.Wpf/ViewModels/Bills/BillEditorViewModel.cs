using System.ComponentModel.DataAnnotations;

using CommunityToolkit.Mvvm.ComponentModel;

using HouseBills.Application.Bills;
using HouseBills.Application.Categories;
using HouseBills.Application.Payees;
using HouseBills.Domain;
using HouseBills.Presentation.Resources;

namespace HouseBills.Wpf.ViewModels.Bills;

public sealed partial class BillEditorViewModel : EditorViewModel
{
    public BillEditorViewModel(BillListItem? bill, DateOnly defaultDueDate, IReadOnlyList<PayeeDto> payees, IReadOnlyList<CategoryDto> categories)
    {
        Id = bill?.Id;
        RowVersion = bill?.RowVersion;
        Payees = payees;
        Categories = categories;
        Description = bill?.Description ?? string.Empty;
        PayeeId = bill?.PayeeId;
        CategoryId = bill?.CategoryId;
        Amount = bill?.Amount;
        DueDate = bill?.DueDate ?? defaultDueDate;
        Notes = bill?.Notes;
        IsEstimated = bill?.IsEstimated ?? false;
        ResetValidation();
    }

    public int? Id { get; }

    public byte[]? RowVersion { get; }

    public IReadOnlyList<PayeeDto> Payees { get; }

    public IReadOnlyList<CategoryDto> Categories { get; }

    public override string Title => Id is null ? Strings.Bills_EditorNewTitle : Strings.Bills_EditorEditTitle;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessageResourceType = typeof(Strings), ErrorMessageResourceName = nameof(Strings.Validation_DescriptionRequired))]
    [MaxLength(Bill.DescriptionMaxLength, ErrorMessageResourceType = typeof(Strings), ErrorMessageResourceName = nameof(Strings.Validation_MaxLength))]
    public partial string Description { get; set; }

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
    [NotifyDataErrorInfo]
    [Required(ErrorMessageResourceType = typeof(Strings), ErrorMessageResourceName = nameof(Strings.Validation_DueDateRequired))]
    public partial DateOnly? DueDate { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [MaxLength(Bill.NotesMaxLength, ErrorMessageResourceType = typeof(Strings), ErrorMessageResourceName = nameof(Strings.Validation_MaxLength))]
    public partial string? Notes { get; set; }

    /// <summary>The amount is still an estimate; uncheck once the real amount is entered.</summary>
    [ObservableProperty]
    public partial bool IsEstimated { get; set; }

    /// <summary>Builds the request; call only after <see cref="EditorViewModel.Validate"/> succeeded.</summary>
    public SaveBillRequest ToRequest()
    {
        return new SaveBillRequest(Id, Description, PayeeId!.Value, CategoryId!.Value, Amount!.Value, DueDate!.Value, Notes, RowVersion, IsEstimated);
    }
}