using System.Globalization;
using System.Windows;
using System.Windows.Data;

using HouseBills.Wpf.Localization;

namespace HouseBills.Wpf.Converters;

/// <summary>
/// Formats an amount like <see cref="MoneyConverter"/>, prefixed with "≈ " when it is an estimate. Values: the amount
/// (<see cref="decimal"/>) and whether it is estimated (<see cref="bool"/>).
/// </summary>
public sealed class EstimatedMoneyConverter : IMultiValueConverter
{
    public object? Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not decimal amount)
        {
            return DependencyProperty.UnsetValue;
        }

        var text = amount.ToString("C", LocalizedStrings.FormattingCulture);
        return values[1] is true ? "≈ " + text : text;
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}