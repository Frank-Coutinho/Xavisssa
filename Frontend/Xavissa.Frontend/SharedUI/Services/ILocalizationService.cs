using System;

public interface ILocalizationService
{
    string CurrentLanguage { get; }
    event Action? LanguageChanged;
    void SetLanguage(string languageCode);
    void SetEnglish();
    void SetPortuguese();
    string GetString(string key);
}
