using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

public class SavedProfile
{
    public const string ProfileVersion = "v1.41";

    public CarCard Car { get; set; } = new();
    public TrackInfo Track { get; set; } = new();
    public TuningConstraints Constraints { get; set; } = new();
    public TuneResult? LastResult { get; set; }
    public List<string> AiEstimatedFields { get; set; } = new();
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

    public void Save(string profileName, SavedProfile profile)
    {
        string safeName = string.Join("_", profileName.Split(Path.GetInvalidFileNameChars()));
        string path = Path.Combine(ProfilesDir, $"{safeName}.json");
        string json = JsonSerializer.Serialize(profile, JsonOptions);
        File.WriteAllText(path, json);
    }

    public SavedProfile? Load(string profileName)
    {
        string safeName = string.Join("_", profileName.Split(Path.GetInvalidFileNameChars()));
        string path = Path.Combine(ProfilesDir, $"{safeName}.json");
        if (!File.Exists(path)) return null;
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<SavedProfile>(json, JsonOptions);
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
        string safeName = string.Join("_", profileName.Split(Path.GetInvalidFileNameChars()));
        string path = Path.Combine(ProfilesDir, $"{safeName}.json");
        if (File.Exists(path)) File.Delete(path);
    }
}
