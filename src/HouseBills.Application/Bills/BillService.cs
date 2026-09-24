using HouseBills.Application.Common;
using HouseBills.Application.Persistence;
using HouseBills.Application.Resources;
using HouseBills.Domain;

namespace HouseBills.Application.Bills;

internal sealed class BillService(
    IBillRepository repository,
    IPayeeRepository payees,
    ICategoryRepository categories,
    IClock clock) : IBillService
{
    public async Task<IReadOnlyList<BillListItem>> ListAsync(BillFilter filter, CancellationToken cancellationToken)
    {
        var today = clock.Today;
        var items = await repository.ListAsync(filter, today, cancellationToken);
        return items.Select(i => i with { Status = Bill.DetermineStatus(i.DueDate, i.PaidOn, today) }).ToList();
    }

    public async Task<Result<int>> SaveAsync(SaveBillRequest request, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        FieldValidation.Text(errors, request.Description, Bill.DescriptionMaxLength, Messages.Field_Description, required: true);
        FieldValidation.Text(errors, request.Notes, Bill.NotesMaxLength, Messages.Field_Notes, required: false);
        FieldValidation.Amount(errors, request.Amount, Messages.Field_Amount);
        await BillReferenceValidation.ValidateAsync(errors, request.PayeeId, request.CategoryId, payees, categories, cancellationToken);
        if (FieldValidation.ToError(errors) is { } validationError)
        {
            return validationError;
        }

        if (request.Id is not { } id)
        {
            var bill = new Bill(request.Description, request.PayeeId, request.CategoryId, request.Amount, request.DueDate, request.Notes, request.IsEstimated);
            await repository.AddAsync(bill, cancellationToken);
            return bill.Id;
        }

        var existing = await repository.GetAsync(id, cancellationToken);
        if (existing is null)
        {
            return Error.NotFound(Messages.Bill_NotFound);
        }

        existing.Update(request.Description, request.PayeeId, request.CategoryId, request.Amount, request.DueDate, request.Notes, request.IsEstimated);
        var result = await repository.TryUpdateAsync(existing, request.RowVersion, cancellationToken);
        return result.IsSuccess ? id : result.Error!;
    }

    public async Task<Result> MarkPaidAsync(MarkBillPaidRequest request, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        FieldValidation.Amount(errors, request.PaidAmount, Messages.Field_PaidAmount);
        if (request.PaidOn > clock.Today)
        {
            errors.Add(Messages.Validation_PaymentInFuture);
        }

        if (FieldValidation.ToError(errors) is { } validationError)
        {
            return validationError;
        }

        var bill = await repository.GetAsync(request.Id, cancellationToken);
        if (bill is null)
        {
            return Error.NotFound(Messages.Bill_NotFound);
        }

        bill.MarkPaid(request.PaidOn, request.PaidAmount);
        return await repository.TryUpdateAsync(bill, request.RowVersion, cancellationToken);
    }

    public async Task<Result> MarkUnpaidAsync(int id, byte[] rowVersion, CancellationToken cancellationToken)
    {
        var bill = await repository.GetAsync(id, cancellationToken);
        if (bill is null)
        {
            return Error.NotFound(Messages.Bill_NotFound);
        }

        bill.MarkUnpaid();
        return await repository.TryUpdateAsync(bill, rowVersion, cancellationToken);
    }

    public Task<Result> DeleteAsync(int id, byte[] rowVersion, CancellationToken cancellationToken)
    {
        return repository.TryDeleteAsync(id, rowVersion, cancellationToken);
    }
}