namespace HouseBills.Domain;

/// <summary>
/// Base type for persisted aggregates: identity plus an optimistic-concurrency token.
/// </summary>
public abstract class Entity
{
    public int Id { get; private set; }

    /// <summary>Optimistic-concurrency token; persistence issues a new one on every save.</summary>
    public byte[] RowVersion { get; private set; } = [];
}