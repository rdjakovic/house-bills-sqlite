using System.Globalization;
using System.Windows;
using System.Windows.Markup;

using CommunityToolkit.Mvvm.Messaging;

using HouseBills.Application.Preferences;
using HouseBills.Application.Resources;
using HouseBills.Presentation.Resources;

namespace HouseBills.Wpf.Localization;

/// <summary>
/// Applies the UI language and its formats: Serbian uses Serbian formats ("1.234,56 RSD"); English keeps the
/// Windows regional settings the app started with.
/// </summary>
internal sealed class LocalizationService(IUserPreferencesStore preferences, IMessenger messenger) : ILocalizationService
{
    public static readonly LanguageOption English = new("en", "English");
    public static readonly LanguageOption Serbian = new("sr-Latn-RS", "Srpski");

    // Captured before anything changes it: English uses the user's own Windows formats.
    private readonly CultureInfo _windowsFormatting = CultureInfo.CurrentCulture;

    public IReadOnlyList<LanguageOption> Languages { get; } = [English, Serbian];

    public LanguageOption Current { get; private set; } = English;

    public CultureInfo FormattingCulture => FormattingFor(Current);

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var saved = (await preferences.LoadAsync(cancellationToken)).Language;
        Apply(Languages.FirstOrDefault(l => l.CultureName == saved) ?? DefaultFor(CultureInfo.CurrentUICulture));
    }

    public async Task SetLanguageAsync(LanguageOption language, CancellationToken cancellationToken)
    {
        if (language == Current)
        {
            return;
        }

        Apply(language);
        messenger.Send(new LanguageChangedMessage(language));

        var saved = await preferences.LoadAsync(cancellationToken);
        await preferences.SaveAsync(saved with { Language = language.CultureName }, cancellationToken);
    }

    private static LanguageOption DefaultFor(CultureInfo windowsLanguage)
    {
        return windowsLanguage.TwoLetterISOLanguageName == "sr" ? Serbian : English;
    }

    private CultureInfo FormattingFor(LanguageOption language) => language == Serbian ? FormattingCultures.Serbian : _windowsFormatting;

    private void Apply(LanguageOption language)
    {
        var culture = CultureInfo.GetCultureInfo(language.CultureName);
        var formatting = FormattingFor(language);

        // The ambient cultures are not reliable for this: they are async-local (a change inside an async method is
        // undone for its caller) and WPF's dispatcher preserves each thread's own culture. So the resource classes and
        // LocalizedStrings are pinned explicitly; the ambient values are still set for anything else that reads them.
        Strings.Culture = culture;
        Messages.Culture = culture;
        LocalizedStrings.FormattingCulture = formatting;
        LocalizedStrings.RegionalCulture = _windowsFormatting;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = formatting;
        CultureInfo.CurrentCulture = formatting;

        // Dates, date pickers and number input in open windows follow the element Language; pages are recreated on
        // navigation, so they pick it up too. (Amounts use the Money converter, which reads FormattingCulture.)
        var xmlLanguage = XmlLanguage.GetLanguage(formatting.IetfLanguageTag);
        foreach (Window window in System.Windows.Application.Current?.Windows ?? new WindowCollection())
        {
            window.Language = xmlLanguage;
        }

        Current = language;
        LocalizedStrings.Instance.Refresh();
    }
}