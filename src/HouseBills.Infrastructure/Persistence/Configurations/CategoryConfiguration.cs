using HouseBills.Domain;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseBills.Infrastructure.Persistence.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(c => c.Id);
        builder.HasAppRowVersion();
        builder.Property(c => c.Name).HasMaxLength(Category.NameMaxLength).IsRequired().UseCollation("NOCASE");
        builder.HasIndex(c => c.Name).IsUnique();

        builder.HasData(
            new { Id = 1, Name = "Utilities", RowVersion = ConcurrencyToken.Seeded },
            new { Id = 2, Name = "Rent / Mortgage", RowVersion = ConcurrencyToken.Seeded },
            new { Id = 3, Name = "Internet & Phone", RowVersion = ConcurrencyToken.Seeded },
            new { Id = 4, Name = "Insurance", RowVersion = ConcurrencyToken.Seeded },
            new { Id = 5, Name = "Taxes & Fees", RowVersion = ConcurrencyToken.Seeded },
            new { Id = 6, Name = "Subscriptions", RowVersion = ConcurrencyToken.Seeded },
            new { Id = 7, Name = "Other", RowVersion = ConcurrencyToken.Seeded });
    }
}