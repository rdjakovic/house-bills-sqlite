using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;

using HouseBills.Presentation.Resources;

namespace HouseBills.Wpf.Localization;

/// <summary>
/// Indexer over <see cref="Strings"/> for XAML bindings (<c>[Key]</c>). <see cref="Refresh"/> re-evaluates every
/// binding, so visible text follows a language change without restarting.
/// </summary>
/// <remarks>
/// A single static instance because markup extensions (<see cref="TrExtension"/>) can't receive DI services;
/// it holds no state besides the event, and C# code uses the typed <see cref="Strings"/> class instead.
/// </remarks>
public sealed class LocalizedStrings : INotifyPropertyChanged
{
    private LocalizedStrings()
    {
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static LocalizedStrings Instance { get; } = new();

    /// <summary>The chosen UI language (see <see cref="LocalizationService"/>); use it instead of the ambient CurrentUICulture.</summary>
    public static CultureInfo Culture => Strings.Culture ?? CultureInfo.CurrentUICulture;

    /// <summary>Formats that go with the chosen language (set by <see cref="LocalizationService"/>); used for amounts and messages.</summary>
    public static CultureInfo FormattingCulture { get; internal set; } = CultureInfo.CurrentCulture;

    /// <summary>
    /// The Windows regional settings the app started with, whatever language is chosen (set by
    /// <see cref="LocalizationService"/>). For files other programs read, e.g. CSV for Excel.
    /// </summary>
    public static CultureInfo RegionalCulture { get; internal set; } = CultureInfo.CurrentCulture;

    /// <summary>Text for <paramref name="key"/> in the chosen UI language; "[key]" if missing, so gaps are visible.</summary>
    /// <remarks>Uses <see cref="Strings.Culture"/> (set by the localization service), like the typed properties.</remarks>
    public string this[string key] => Strings.ResourceManager.GetString(key, Strings.Culture) ?? $"[{key}]";

    public void Refresh()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Binding.IndexerName));
    }
}