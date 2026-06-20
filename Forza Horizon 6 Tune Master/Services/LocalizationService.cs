using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

public sealed class LocalizationService : INotifyPropertyChanged
{
    private static readonly Lazy<LocalizationService> _instance = new(() => new());
    public static LocalizationService Instance => _instance.Value;

    private Dictionary<string, string> _current = new();
    private Dictionary<string, string> _fallback = new();
    private string _currentCode = "en";

    private const string FallbackCode = "en";

    private static string SettingsPath => ForzaPaths.SettingsPath;

    public string CurrentLanguage => _currentCode;

    public event PropertyChangedEventHandler? PropertyChanged;

    private LocalizationService()
    {
        LoadLanguage(FallbackCode, out _fallback);
    }

    public string this[string key] => T(key);

    public string T(string key)
    {
        if (_current.TryGetValue(key, out var val)) return val;
        if (_fallback.TryGetValue(key, out val)) return val;
        return key;
    }

    public string T(string key, params object[] args)
    {
        var template = T(key);
        return args.Length > 0 ? string.Format(template, args) : template;
    }

    public bool TryGet(string key, out string value)
    {
        if (_current.TryGetValue(key, out var v)) { value = v; return true; }
        if (_fallback.TryGetValue(key, out v)) { value = v; return true; }
        value = key;
        return false;
    }

    public event EventHandler<string>? LanguageLoadFailed;

    public bool SetLanguage(string cultureCode)
    {
        if (string.IsNullOrEmpty(cultureCode)) return true;
        if (cultureCode == _currentCode) return true;

        if (!LoadLanguage(cultureCode, out var dict))
        {
            LanguageLoadFailed?.Invoke(this, cultureCode);
            return false;
        }

        _current = dict;
        _currentCode = cultureCode;

        CultureInfo.CurrentCulture = new CultureInfo(cultureCode);
        CultureInfo.CurrentUICulture = new CultureInfo(cultureCode);

        SaveLanguageSetting(cultureCode);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item"));
        InvalidateAll();
        return true;
    }

    public void InitializeFromSystem()
    {
        var saved = LoadLanguageSetting();
        if (saved != null)
        {
            SetLanguage(saved);
            return;
        }

        var system = CultureInfo.CurrentUICulture;
        var code = system.TwoLetterISOLanguageName;

        if (code == "ru")
            SetLanguage("ru");
        else
            SetLanguage("en");
    }

    private void SaveLanguageSetting(string code)
    {
        SaveSettings(("Language", code));
    }

    private string? LoadLanguageSetting()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return null;
            var json = File.ReadAllText(SettingsPath);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("Language", out var prop) ? prop.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    public void SaveUnitSettings(UnitSystem measurement, PowerUnit power, SpringUnit spring)
    {
        SaveSettings(
            ("MeasurementSystem", measurement.ToString()),
            ("PowerUnit", power.ToString()),
            ("SpringUnit", spring.ToString())
        );
    }

    public (string? measurement, string? power, string? spring) LoadUnitSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return (null, null, null);
            var json = File.ReadAllText(SettingsPath);
            var data = JsonSerializer.Deserialize<Dictionary<string, string?>>(json);
            if (data == null) return (null, null, null);
            data.TryGetValue("MeasurementSystem", out var ms);
            data.TryGetValue("PowerUnit", out var pu);
            data.TryGetValue("SpringUnit", out var su);
            return (ms, pu, su);
        }
        catch
        {
            return (null, null, null);
        }
    }

    private void SaveSettings(params (string key, string? value)[] pairs)
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (dir != null) Directory.CreateDirectory(dir);

            var data = new Dictionary<string, string?>();
            if (File.Exists(SettingsPath))
            {
                try
                {
                    var existing = JsonSerializer.Deserialize<Dictionary<string, string?>>(
                        File.ReadAllText(SettingsPath));
                    if (existing != null) data = existing;
                }
                catch { }
            }

            foreach (var (key, value) in pairs)
                if (value != null) data[key] = value;

            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(data));
        }
        catch { /* best effort */ }
    }

    private bool LoadLanguage(string code, out Dictionary<string, string> dict)
    {
        dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var asm = Assembly.GetExecutingAssembly();

            // Base hand-curated localization file (required)
            var resourceName = $"Forza_Horizon_6_Tune_Master.Localization.{code}.json";
            using var stream = asm.GetManifestResourceStream(resourceName);
            if (stream == null) return false;

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (entries == null) return false;

            dict = new Dictionary<string, string>(entries, StringComparer.OrdinalIgnoreCase);

            // Optional game-extracted strings (e.g. Upgrades, List_DriveType, List_Aspiration).
            // Isolated in its own try-catch so a malformed or oversized GameStrings file
            // doesn't break the entire language load — the base strings still work.
            try
            {
                var gameResourceName = $"Forza_Horizon_6_Tune_Master.Localization.GameStrings.{code}.json";
                using var gameStream = asm.GetManifestResourceStream(gameResourceName);
                if (gameStream != null)
                {
                    using var gameReader = new StreamReader(gameStream);
                    var gameJson = gameReader.ReadToEnd();
                    var gameTables = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(gameJson);
                    if (gameTables != null)
                    {
                        foreach (var (table, tableEntries) in gameTables)
                        {
                            foreach (var (id, value) in tableEntries)
                            {
                                dict[$"{table}_{id}"] = value;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LocalizationService] Failed to load GameStrings.{code}.json: {ex.Message}");
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private void InvalidateAll()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item"));
    }

    public bool IsFallbackKey(string key)
    {
        return !_current.ContainsKey(key) && _fallback.ContainsKey(key);
    }

    public bool IsFirstRun()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return true;
            var json = File.ReadAllText(SettingsPath);
            using var doc = JsonDocument.Parse(json);
            return !doc.RootElement.TryGetProperty("FirstRunCompleted", out var prop) || prop.GetString() != "true";
        }
        catch
        {
            return true;
        }
    }

    public void SetFirstRunCompleted()
    {
        SaveSettings(("FirstRunCompleted", "true"));
    }

    public void ClearFirstRunCompleted()
    {
        SaveSettings(("FirstRunCompleted", "false"));
    }
}
