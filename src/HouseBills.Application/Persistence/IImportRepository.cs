using HouseBills.Domain;

namespace HouseBills.Application.Persistence;

/// <summary>Writes an import in one transaction.</summary>
public interface IImportRepository
{
    /// <summary>
    /// Inserts <paramref name="payees"/> and <paramref name="categories"/>, then the bills <paramref name="createBills"/>
    /// returns (called once their ids are set, so bills can refer to them). Nothing is saved if any step fails.
    /// </summary>
    Task AddAsync(IReadOnlyList<Payee> payees, IReadOnlyList<Category> categories, Func<IReadOnlyList<Bill>> createBills, CancellationToken cancellationToken);
}