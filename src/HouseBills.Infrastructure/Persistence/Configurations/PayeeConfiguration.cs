using HouseBills.Domain;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseBills.Infrastructure.Persistence.Configurations;

internal sealed class PayeeConfiguration : IEntityTypeConfiguration<Payee>
{
    public void Configure(EntityTypeBuilder<Payee> builder)
    {
        builder.ToTable("Payees");
        builder.HasKey(p => p.Id);
        builder.HasAppRowVersion();
        builder.Property(p => p.Name).HasMaxLength(Payee.NameMaxLength).IsRequired().UseCollation("NOCASE");
        builder.Property(p => p.AccountReference).HasMaxLength(Payee.AccountReferenceMaxLength);
        builder.Property(p => p.Notes).HasMaxLength(Payee.NotesMaxLength);
        builder.HasIndex(p => p.Name).IsUnique();
    }
}