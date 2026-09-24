using HouseBills.Application.Bills;
using HouseBills.Application.Common;
using HouseBills.Application.Persistence;
using HouseBills.Application.Resources;
using HouseBills.Domain;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HouseBills.Application.RecurringBills;

internal sealed class RecurringBillService(
    IRecurringBillRepository repository,
    IPayeeRepository payees,
    ICategoryRepository categories,
    IClock clock,
    IOptions<BillingOptions> options,
    ILogger<RecurringBillService> logger) : IRecurringBillService
{
    public async Task<IReadOnlyList<RecurringBillDto>> ListAsync(CancellationToken cancellationToken)
    {
        var today = clock.Today;
        var items = await repository.ListAsync(cancellationToken);
        return items
            .Select(i => i with
            {
                NextDueDate = i.IsActive
                    ? new RecurrenceSchedule(i.Frequency, i.StartDate, i.EndDate).NextDueDateOnOrAfter(today)
                    : null,
            })
            .ToList();
    }

    public async Task<Result<int>> SaveAsync(SaveRecurringBillRequest request, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        FieldValidation.Text(errors, request.Name, RecurringBill.NameMaxLength, Messages.Field_Name, required: true);
        FieldValidation.Text(errors, request.Notes, RecurringBill.NotesMaxLength, Messages.Field_Notes, required: false);
        FieldValidation.Amount(errors, request.Amount, Messages.Field_Amount);
        if (!Enum.IsDefined(request.Frequency))
        {
            errors.Add(Messages.Validation_SelectFrequency);
        }

        if (request.EndDate < request.StartDate)
        {
            errors.Add(Messages.Validation_EndBeforeStart);
        }

        await BillReferenceValidation.ValidateAsync(errors, request.PayeeId, request.CategoryId, payees, categories, cancellationToken);
        if (FieldValidation.ToError(errors) is { } validationError)
        {
            return validationError;
        }

        var schedule = new RecurrenceSchedule(request.Frequency, request.StartDate, request.EndDate);
        if (request.Id is not { } id)
        {
            var template = new RecurringBill(request.Name, request.PayeeId, request.CategoryId, request.Amount, schedule, request.Notes, request.AmountVaries);
            await repository.AddAsync(template, cancellationToken);
            return template.Id;
        }

        var existing = await repository.GetAsync(id, cancellationToken);
        if (existing is null)
        {
            return Error.NotFound(Messages.RecurringBill_NotFound);
        }

        existing.Update(request.Name, request.PayeeId, request.CategoryId, request.Amount, schedule, request.Notes, request.AmountVaries);
        var result = await repository.TryUpdateAsync(existing, request.RowVersion, cancellationToken);
        return result.IsSuccess ? id : result.Error!;
    }

    public async Task<Result> SetActiveAsync(int id, bool isActive, byte[] rowVersion, CancellationToken cancellationToken)
    {
        var existing = await repository.GetAsync(id, cancellationToken);
        if (existing is null)
        {
            return Error.NotFound(Messages.RecurringBill_NotFound);
        }

        existing.SetActive(isActive, clock.Today);
        return await repository.TryUpdateAsync(existing, rowVersion, cancellationToken);
    }

    public Task<Result> DeleteAsync(int id, byte[] rowVersion, CancellationToken cancellationToken)
    {
        return repository.TryDeleteAsync(id, rowVersion, cancellationToken);
    }

    public async Task<int> GenerateUpcomingBillsAsync(CancellationToken cancellationToken)
    {
        var upTo = clock.Today.AddDays(options.Value.GenerationLookaheadDays);
        var templates = await repository.ListActiveAsync(cancellationToken);
        var lastActualAmounts = templates.Any(t => t.AmountVaries)
            ? await repository.GetLastActualAmountsAsync(cancellationToken)
            : new Dictionary<int, decimal>();
        var created = 0;
        foreach (var template in templates)
        {
            var expectedRowVersion = template.RowVersion;
            var bills = template.GenerateBills(upTo, lastActualAmounts.TryGetValue(template.Id, out var last) ? last : null);
            if (bills.Count == 0)
            {
                continue;
            }

            try
            {
                await repository.SaveGeneratedBillsAsync(template, expectedRowVersion, bills, cancellationToken);
                created += bills.Count;
            }
            catch (ConcurrencyConflictException)
            {
                logger.LogInformation("Recurring bill {RecurringBillId} was changed concurrently; skipped bill generation.", template.Id);
            }
        }

        if (created > 0)
        {
            logger.LogInformation("Generated {BillCount} bills up to {UpTo}.", created, upTo);
        }

        return created;
    }
}