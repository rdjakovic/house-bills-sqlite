using HouseBills.Domain;

using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HouseBills.Infrastructure.Persistence.Configurations;

/// <summary>
/// SQLite has no decimal type (EF Core would store TEXT, which SQL can't sum or compare exactly), so amounts are stored
/// as whole minor units (paras/cents) in an INTEGER column. <see cref="MoneyRules"/> guarantees at most two decimals.
/// </summary>
internal static class MoneyConversion
{
    public const decimal MinorUnitsPerUnit = 100m;

    private static readonly ValueConverter<decimal, long> Converter = new(
        amount => decimal.ToInt64(amount * MinorUnitsPerUnit),
        minorUnits => minorUnits / MinorUnitsPerUnit);

    public static PropertyBuilder<decimal> StoredAsMinorUnits(this PropertyBuilder<decimal> property)
    {
        return property.HasConversion(Converter);
    }

    public static PropertyBuilder<decimal?> StoredAsMinorUnits(this PropertyBuilder<decimal?> property)
    {
        return property.HasConversion(Converter);
    }
}