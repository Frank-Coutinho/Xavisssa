using System;
using System.Linq;
using System.Globalization;
using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;

public class LocalizationService : ILocalizationService
{
    private const string BaseUri = "avares://Xavissa.Frontend/Views/Localization/";
    private const string EnglishFile = "Strings.en-US.axaml";
    private const string PortugueseFile = "Strings.pt-PT.axaml";

    public string CurrentLanguage { get; private set; } = "en-US";
    public event Action? LanguageChanged;

    public void SetLanguage(string languageCode)
    {
        if (string.Equals(languageCode, "pt-PT", StringComparison.OrdinalIgnoreCase))
            SwitchLanguage(PortugueseFile, "pt-PT");
        else
            SwitchLanguage(EnglishFile, "en-US");
    }

    public void SetEnglish() => SwitchLanguage(EnglishFile, "en-US");

    public void SetPortuguese() => SwitchLanguage(PortugueseFile, "pt-PT");

    private void SwitchLanguage(string languageFile, string languageCode)
    {
        var app = Application.Current!;
        var dictionaries = app.Resources.MergedDictionaries;

        var existingLanguageDictionaries = dictionaries
            .OfType<ResourceInclude>()
            .Where(d =>
                d.Source != null
                && (
                    d.Source.OriginalString.EndsWith(EnglishFile, StringComparison.OrdinalIgnoreCase)
                    || d.Source.OriginalString.EndsWith(PortugueseFile, StringComparison.OrdinalIgnoreCase)
                )
            )
            .ToList();

        foreach (var languageDictionary in existingLanguageDictionaries)
            dictionaries.Remove(languageDictionary);

        dictionaries.Add(
            new ResourceInclude(new Uri("avares://Xavissa.Frontend/"))
            {
                Source = new Uri($"{BaseUri}{languageFile}"),
            }
        );

        CurrentLanguage = languageCode;
        var culture = string.Equals(languageCode, "pt-PT", StringComparison.OrdinalIgnoreCase)
            ? CultureInfo.GetCultureInfo("pt-PT")
            : CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        LanguageChanged?.Invoke();
    }

    public string GetString(string key)
    {
        var app = Application.Current;
        if (app == null)
            return key;

        return app.Resources.TryGetResource(key, null, out var value) && value is not null
            ? value.ToString() ?? key
            : key;
    }
}
