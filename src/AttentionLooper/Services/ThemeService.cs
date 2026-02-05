using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;

namespace AttentionLooper.Services;

public class ThemeService
{
    private const string DarkThemeUri = "Themes/DarkTheme.xaml";
    private const string LightThemeUri = "Themes/LightTheme.xaml";

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AttentionLooper", "settings.json");

    private string _currentThemeChoice = "Dark";

    public string CurrentThemeChoice => _currentThemeChoice;

    public void ApplyTheme(string choice)
    {
        _currentThemeChoice = choice;

        string uri = choice switch
        {
            "Light" => LightThemeUri,
            "System" => IsSystemDarkMode() ? DarkThemeUri : LightThemeUri,
            _ => DarkThemeUri
        };

        var app = Application.Current;
        if (app == null) return;

        var merged = app.Resources.MergedDictionaries;
        merged.Clear();
        merged.Add(new ResourceDictionary { Source = new Uri(uri, UriKind.Relative) });

        SaveSetting("theme", choice);
    }

    public string LoadPreference()
    {
        var val = LoadSetting("theme");
        return val is "Dark" or "Light" or "System" ? val : "Dark";
    }

    public string? LoadSoundPreference() => LoadSetting("selectedSound");

    public void SaveSoundPreference(string name) => SaveSetting("selectedSound", name);

    private string? LoadSetting(string key)
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty(key, out var prop))
                    return prop.GetString();
            }
        }
        catch { }
        return null;
    }

    private void SaveSetting(string key, string value)
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(dir);

            var dict = new Dictionary<string, string>();
            if (File.Exists(SettingsPath))
            {
                try
                {
                    var existing = File.ReadAllText(SettingsPath);
                    var doc = JsonDocument.Parse(existing);
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        var s = prop.Value.GetString();
                        if (s != null) dict[prop.Name] = s;
                    }
                }
                catch { }
            }

            dict[key] = value;
            var json = JsonSerializer.Serialize(dict);
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }

    private static bool IsSystemDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var val = key?.GetValue("AppsUseLightTheme");
            if (val is int i)
                return i == 0;
        }
        catch { }
        return true; // default to dark
    }
}
