using HouseBills.Domain;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseBills.Infrastructure.Persistence.Configurations;

internal sealed class RecurringBillConfiguration : IEntityTypeConfiguration<RecurringBill>
{
    public void Configure(EntityTypeBuilder<RecurringBill> builder)
    {
        builder.ToTable("RecurringBills");
        builder.HasKey(r => r.Id);
        builder.HasAppRowVersion();
        builder.Property(r => r.Name).HasMaxLength(RecurringBill.NameMaxLength).IsRequired().UseCollation("NOCASE");
        builder.Property(r => r.Amount).StoredAsMinorUnits();
        builder.Property(r => r.Frequency).IsRequired();
        builder.Property(r => r.StartDate).IsRequired();
        builder.Property(r => r.Notes).HasMaxLength(RecurringBill.NotesMaxLength);
        builder.Ignore(r => r.Schedule);

        builder.HasOne<Payee>().WithMany().HasForeignKey(r => r.PayeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Category>().WithMany().HasForeignKey(r => r.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}