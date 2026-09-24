using HouseBills.Application.RecurringBills;
using HouseBills.Domain;

namespace HouseBills.Application.Persistence;

/// <summary>Persistence for <see cref="RecurringBill"/>.</summary>
public interface IRecurringBillRepository : IRepository<RecurringBill>
{
    /// <summary>All recurring bills ordered by name, with payee/category names. <see cref="RecurringBillDto.NextDueDate"/> is not populated.</summary>
    Task<IReadOnlyList<RecurringBillDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Active templates, loaded for bill generation.</summary>
    Task<IReadOnlyList<RecurringBill>> ListActiveAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Per template: the amount of its most recent (by due date) bill whose amount is actual, not an estimate.
    /// Templates without such a bill are missing from the result.
    /// </summary>
    Task<IReadOnlyDictionary<int, decimal>> GetLastActualAmountsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Atomically inserts <paramref name="bills"/> and saves the template's advanced
    /// <see cref="RecurringBill.GeneratedThrough"/>.
    /// </summary>
    /// <exception cref="Common.ConcurrencyConflictException">The template changed since it was loaded; nothing is saved.</exception>
    Task SaveGeneratedBillsAsync(RecurringBill template, byte[] expectedRowVersion, IReadOnlyList<Bill> bills, CancellationToken cancellationToken);
}