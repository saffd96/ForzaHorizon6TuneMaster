using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

public class SavedProfile
{
    public const string ProfileVersion = "v2.4";

    public CarCard Car { get; set; } = new();
    public TrackInfo Track { get; set; } = new();
    public SelectedParts? Parts { get; set; } = new();
    public TuningConstraints? Constraints
    {
        get => _constraints;
        set => _constraints = value ?? new TuningConstraints();
    }
    private TuningConstraints _constraints = new();
    public TuneResult? LastResult { get; set; }
    public string? Version { get; set; }
}

public class StorageService
{
    private static string ProfilesDir => ForzaPaths.ProfilesDir;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public StorageService()
    {
        Directory.CreateDirectory(ProfilesDir);
    }

    [Obsolete("Use Save(string, SavedProfile)")]
    public string Save(CarCard car, TrackInfo track, TuningConstraints _, TuneResult? result)
    {
        var guid = Guid.NewGuid().ToString("N")[..8];
        Save(guid, new SavedProfile { Car = car, Track = track, Parts = new SelectedParts(), LastResult = result });
        return guid;
    }

    public void Save(string profileName, SavedProfile profile)
    {
        string safeName = SanitizeProfileName(profileName);
        string path = Path.Combine(ProfilesDir, $"{safeName}.json");
        string json = JsonSerializer.Serialize(profile, JsonOptions);
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, path, overwrite: true);
    }

    public SavedProfile? Load(string profileName)
    {
        string safeName = SanitizeProfileName(profileName);
        string path = Path.Combine(ProfilesDir, $"{safeName}.json");
        if (!File.Exists(path)) return null;
        try
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SavedProfile>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public List<string> GetProfileNames()
    {
        return Directory.GetFiles(ProfilesDir, "*.json")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .OrderBy(n => n)
            .ToList();
    }

    public void Delete(string profileName)
    {
        string safeName = SanitizeProfileName(profileName);
        string path = Path.Combine(ProfilesDir, $"{safeName}.json");
        if (File.Exists(path)) File.Delete(path);
    }

    public void DeleteAll()
    {
        foreach (var name in GetProfileNames())
            Delete(name);
    }

    private static string SanitizeProfileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalid, chars[i]) >= 0)
                chars[i] = '_';
        }
        return new string(chars);
    }
}
