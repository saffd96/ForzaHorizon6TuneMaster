using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;

namespace Forza_Horizon_6_Tune_Master.Services;

public sealed class LocalizationService : INotifyPropertyChanged
{
    private static readonly Lazy<LocalizationService> _instance = new(() => new());
    public static LocalizationService Instance => _instance.Value;

    private Dictionary<string, string> _current = new();
    private Dictionary<string, string> _fallback = new();
    private string _currentCode = "en";

    private const string FallbackCode = "en";

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ForzaTuneMaster", "settings.json");

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
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (dir != null) Directory.CreateDirectory(dir);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new { Language = code }));
        }
        catch { /* best effort */ }
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

    private bool LoadLanguage(string code, out Dictionary<string, string> dict)
    {
        dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var asm = Assembly.GetExecutingAssembly();
            var resourceName = $"Forza_Horizon_6_Tune_Master.Localization.{code}.json";
            using var stream = asm.GetManifestResourceStream(resourceName);
            if (stream == null) return false;

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (entries == null) return false;
            dict = new Dictionary<string, string>(entries, StringComparer.OrdinalIgnoreCase);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public const string InvalidateRequestedEvent = "LanguageChanged";

    public static void RaiseLanguageChanged()
    {
        Instance.PropertyChanged?.Invoke(Instance, new PropertyChangedEventArgs("Item"));
    }

    private void InvalidateAll()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item"));
    }

    public bool IsFallbackKey(string key)
    {
        return !_current.ContainsKey(key) && _fallback.ContainsKey(key);
    }
}
