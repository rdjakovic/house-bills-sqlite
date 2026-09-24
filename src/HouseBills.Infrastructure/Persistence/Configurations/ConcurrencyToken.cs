using HouseBills.Domain;

using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseBills.Infrastructure.Persistence.Configurations;

internal static class ConcurrencyToken
{
    /// <summary>Row version of seeded rows; replaced by a random one on their first update.</summary>
    public static readonly byte[] Seeded = [0, 0, 0, 0, 0, 0, 0, 1];

    /// <summary>Configures the app-issued concurrency token (see <see cref="AppDbContext"/>).</summary>
    public static void HasAppRowVersion<T>(this EntityTypeBuilder<T> builder)
        where T : Entity
    {
        builder.Property(e => e.RowVersion).IsConcurrencyToken().IsRequired();
    }
}