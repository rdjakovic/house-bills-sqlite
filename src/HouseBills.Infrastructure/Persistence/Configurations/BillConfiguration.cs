using HouseBills.Domain;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseBills.Infrastructure.Persistence.Configurations;

internal sealed class BillConfiguration : IEntityTypeConfiguration<Bill>
{
    public void Configure(EntityTypeBuilder<Bill> builder)
    {
        builder.ToTable("Bills");
        builder.HasKey(b => b.Id);
        builder.HasAppRowVersion();
        builder.Property(b => b.Description).HasMaxLength(Bill.DescriptionMaxLength).IsRequired().UseCollation("NOCASE");
        builder.Property(b => b.Amount).StoredAsMinorUnits();
        builder.Property(b => b.PaidAmount).StoredAsMinorUnits();
        builder.Property(b => b.Notes).HasMaxLength(Bill.NotesMaxLength);
        builder.Ignore(b => b.IsPaid);

        builder.HasOne<Payee>().WithMany().HasForeignKey(b => b.PayeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Category>().WithMany().HasForeignKey(b => b.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RecurringBill>().WithMany().HasForeignKey(b => b.RecurringBillId).OnDelete(DeleteBehavior.SetNull);

        // Bill list and reports filter on a due-date range (dates are stored as sortable 'yyyy-MM-dd' text).
        builder.HasIndex(b => b.DueDate);

        // Safety net against generating the same occurrence twice.
        builder.HasIndex(b => new { b.RecurringBillId, b.DueDate }).IsUnique();
    }
}