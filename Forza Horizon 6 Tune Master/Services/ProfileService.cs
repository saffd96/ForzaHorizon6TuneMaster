using System.Collections.Generic;
using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

public class ProfileService
{
    private readonly StorageService _storage;

    public ProfileService(StorageService storage)
    {
        _storage = storage;
    }

    public string AutoProfileName(CarCard car, TrackInfo track)
    {
        var t = LocalizationService.Instance;
        var dt = t.T($"Enum_DriveType_{car.DriveType}");
        var et = t.T($"Enum_EngineType_{car.EngineType}");
        var disc = t.T($"Discipline{track.Discipline}");
        var season = t.T($"Enum_Season_{track.Season}");
        return $"{car.Year} {car.Make} {car.Model} {dt} {et} {disc} {season}".Trim();
    }

    public string Save(CarCard car, TrackInfo track, TuningConstraints _, TuneResult? result) =>
        Save(car, track, new SelectedParts(), result);

    public string Save(CarCard car, TrackInfo track, SelectedParts parts, TuneResult? result, TuningConstraints? constraints = null)
    {
        string name = AutoProfileName(car, track);
        car.Name = name;
        _storage.Save(name, new SavedProfile
        {
            Car = car, Track = track, Parts = parts, LastResult = result,
            Constraints = constraints ?? new TuningConstraints(),
            Version = SavedProfile.ProfileVersion
        });
        return name;
    }

    public SavedProfile? Load(string name) => _storage.Load(name);

    public void Delete(string name) => _storage.Delete(name);

    public void DeleteAll() => _storage.DeleteAll();

    public List<string> GetProfileNames() => _storage.GetProfileNames();
}
