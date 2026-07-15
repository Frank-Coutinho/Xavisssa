using System;
using System.Linq;
using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;

public class ThemeService : IThemeService
{
    private const string BaseUri = "avares://Xavissa.Frontend/SharedUI/Themes/";

    private const string LightTheme = "LightTheme.axaml";
    private const string DarkTheme = "DarkTheme.axaml";

    public void SetLight() => SwitchTheme(LightTheme);

    public void SetDark() => SwitchTheme(DarkTheme);

    private void SwitchTheme(string themeFile)
    {
        var app = Application.Current!;
        var dictionaries = app.Resources.MergedDictionaries;

        // Remove only light/dark variant dictionaries; keep BaseTheme.axaml.
        var existingThemes = dictionaries
            .OfType<ResourceInclude>()
            .Where(d =>
                d.Source != null
                && (
                    d.Source.OriginalString.EndsWith(LightTheme, StringComparison.OrdinalIgnoreCase)
                    || d.Source.OriginalString.EndsWith(DarkTheme, StringComparison.OrdinalIgnoreCase)
                )
            )
            .ToList();

        foreach (var theme in existingThemes)
            dictionaries.Remove(theme);

        // Add new theme
        dictionaries.Add(
            new ResourceInclude(new Uri("avares://Xavissa.Frontend/"))
            {
                Source = new Uri($"{BaseUri}{themeFile}"),
            }
        );
    }
}

