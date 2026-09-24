using HouseBills.Application.Bills;
using HouseBills.Application.Common;
using HouseBills.Application.Persistence;
using HouseBills.Application.Resources;
using HouseBills.Domain;

namespace HouseBills.Application.Import;

internal sealed class ImportService(
    IImportRepository importRepository,
    IPayeeRepository payees,
    ICategoryRepository categories,
    IBillRepository bills,
    IClock clock) : IImportService
{
    // Names are unique in the database ignoring ASCII case; ignoring all case here never lets a clash through.
    private static readonly StringComparer NameComparer = StringComparer.OrdinalIgnoreCase;

    public async Task<Result<ImportSummary>> ImportPayeesAsync(IReadOnlyList<ImportedPayee> rows, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        foreach (var row in rows)
        {
            ValidateRow(errors, row.Row, rowErrors =>
            {
                FieldValidation.Text(rowErrors, row.Name, Payee.NameMaxLength, Messages.Field_Name, required: true);
                FieldValidation.Text(rowErrors, row.AccountReference, Payee.AccountReferenceMaxLength, Messages.Field_AccountReference, required: false);
                FieldValidation.Text(rowErrors, row.Notes, Payee.NotesMaxLength, Messages.Field_Notes, required: false);
            });
        }

        if (FieldValidation.ToError(errors) is { } validationError)
        {
            return validationError;
        }

        var names = (await payees.ListAsync(cancellationToken)).Select(p => p.Name).ToHashSet(NameComparer);
        var added = rows.Where(r => names.Add(r.Name.Trim())).Select(r => new Payee(r.Name.Trim(), r.AccountReference, r.Notes)).ToList();
        await importRepository.AddAsync(added, [], () => [], cancellationToken);
        return new ImportSummary(added.Count, rows.Count - added.Count);
    }

    public async Task<Result<ImportSummary>> ImportCategoriesAsync(IReadOnlyList<ImportedCategory> rows, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        foreach (var row in rows)
        {
            ValidateRow(errors, row.Row, rowErrors => FieldValidation.Text(rowErrors, row.Name, Category.NameMaxLength, Messages.Field_Name, required: true));
        }

        if (FieldValidation.ToError(errors) is { } validationError)
        {
            return validationError;
        }

        var names = (await categories.ListAsync(cancellationToken)).Select(c => c.Name).ToHashSet(NameComparer);
        var added = rows.Where(r => names.Add(r.Name.Trim())).Select(r => new Category(r.Name.Trim())).ToList();
        await importRepository.AddAsync([], added, () => [], cancellationToken);
        return new ImportSummary(added.Count, rows.Count - added.Count);
    }

    public async Task<Result<ImportSummary>> ImportBillsAsync(IReadOnlyList<ImportedBill> rows, CancellationToken cancellationToken)
    {
        var today = clock.Today;
        var errors = new List<string>();
        foreach (var row in rows)
        {
            ValidateRow(errors, row.Row, rowErrors => ValidateBill(rowErrors, row, today));
        }

        if (FieldValidation.ToError(errors) is { } validationError)
        {
            return validationError;
        }

        if (rows.Count == 0)
        {
            return new ImportSummary(0, 0);
        }

        var seen = (await bills.ListAsync(new BillFilter(rows.Min(r => r.DueDate), rows.Max(r => r.DueDate)), today, cancellationToken))
            .Select(b => BillKey.Of(b.DueDate, b.Description, b.PayeeName, b.Amount))
            .ToHashSet();
        var toAdd = rows.Where(r => seen.Add(BillKey.Of(r.DueDate, r.Description, r.PayeeName, r.Amount))).ToList();

        var payeeIds = (await payees.ListAsync(cancellationToken)).ToDictionary(p => p.Name, p => (Func<int>)(() => p.Id), NameComparer);
        var categoryIds = (await categories.ListAsync(cancellationToken)).ToDictionary(c => c.Name, c => (Func<int>)(() => c.Id), NameComparer);
        var newPayees = new List<Payee>();
        var newCategories = new List<Category>();
        foreach (var row in toAdd)
        {
            // New entities get their ids when saved, so the lookups read the id then.
            if (!payeeIds.ContainsKey(row.PayeeName.Trim()))
            {
                var payee = new Payee(row.PayeeName.Trim(), null, null);
                newPayees.Add(payee);
                payeeIds.Add(payee.Name, () => payee.Id);
            }

            if (!categoryIds.ContainsKey(row.CategoryName.Trim()))
            {
                var category = new Category(row.CategoryName.Trim());
                newCategories.Add(category);
                categoryIds.Add(category.Name, () => category.Id);
            }
        }

        await importRepository.AddAsync(
            newPayees,
            newCategories,
            () => toAdd.Select(row => CreateBill(row, payeeIds[row.PayeeName.Trim()](), categoryIds[row.CategoryName.Trim()]())).ToList(),
            cancellationToken);
        return new ImportSummary(toAdd.Count, rows.Count - toAdd.Count, newPayees.Count, newCategories.Count);
    }

    private static void ValidateBill(List<string> errors, ImportedBill row, DateOnly today)
    {
        FieldValidation.Text(errors, row.Description, Bill.DescriptionMaxLength, Messages.Field_Description, required: true);
        FieldValidation.Text(errors, row.PayeeName, Payee.NameMaxLength, Messages.Field_Payee, required: true);
        FieldValidation.Text(errors, row.CategoryName, Category.NameMaxLength, Messages.Field_Category, required: true);
        FieldValidation.Text(errors, row.Notes, Bill.NotesMaxLength, Messages.Field_Notes, required: false);
        FieldValidation.Amount(errors, row.Amount, Messages.Field_Amount);
        if (row.PaidAmount is { } paidAmount)
        {
            FieldValidation.Amount(errors, paidAmount, Messages.Field_PaidAmount);
        }

        if (row.PaidOn is null && row.PaidAmount is not null)
        {
            errors.Add(Messages.Import_PaidAmountWithoutDate);
        }

        if (row.PaidOn > today)
        {
            errors.Add(Messages.Validation_PaymentInFuture);
        }
    }

    private static Bill CreateBill(ImportedBill row, int payeeId, int categoryId)
    {
        var bill = new Bill(row.Description, payeeId, categoryId, row.Amount, row.DueDate, row.Notes, row.IsEstimated);
        if (row.PaidOn is { } paidOn)
        {
            bill.MarkPaid(paidOn, row.PaidAmount ?? row.Amount);
        }

        return bill;
    }

    /// <summary>Checks one row, prefixing its messages with the row number.</summary>
    private static void ValidateRow(List<string> errors, int row, Action<List<string>> validate)
    {
        var rowErrors = new List<string>();
        validate(rowErrors);
        errors.AddRange(rowErrors.Select(e => FieldValidation.Format(Messages.Import_RowError, row, e)));
    }

    /// <summary>What makes two bills the same for an import; text ignores case and surrounding spaces.</summary>
    private readonly record struct BillKey(DateOnly DueDate, string Description, string PayeeName, decimal Amount)
    {
        public static BillKey Of(DateOnly dueDate, string description, string payeeName, decimal amount) =>
            new(dueDate, description.Trim().ToUpperInvariant(), payeeName.Trim().ToUpperInvariant(), amount);
    }
}