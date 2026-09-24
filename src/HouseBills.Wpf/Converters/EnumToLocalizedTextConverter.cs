using System.Globalization;
using System.Windows.Data;

using HouseBills.Wpf.Localization;

namespace HouseBills.Wpf.Converters;

/// <summary>Shows an enum value in the UI language, using the resource key <c>Enum_{Type}_{Value}</c>.</summary>
/// <remarks>
/// As a multi-value converter the first value is the enum; any further values are ignored and only serve to re-run the
/// conversion, e.g. a binding to <see cref="LocalizedStrings.Instance"/> so the text follows a language switch.
/// </remarks>
[ValueConversion(typeof(Enum), typeof(string))]
public sealed class EnumToLocalizedTextConverter : IValueConverter, IMultiValueConverter
{
    public static string KeyFor(Enum value) => $"Enum_{value.GetType().Name}_{value}";

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is Enum enumValue ? LocalizedStrings.Instance[KeyFor(enumValue)] : value;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    public object? Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        return Convert(values.FirstOrDefault(), targetType, parameter, culture);
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}