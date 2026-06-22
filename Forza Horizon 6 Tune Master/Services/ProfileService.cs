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

    // Profile names must be STABLE across UI language changes. Building the name from localized
    // strings meant switching language renamed every file, so saved profiles duplicated or
    // appeared lost. Use invariant enum tokens instead (Make/Model/Year are already
    // language-neutral); the localized labels stay in the UI only.
    public string AutoProfileName(CarCard car, TrackInfo track)
    {
        return $"{car.Year} {car.Make} {car.Model} {car.DriveType} {car.EngineType} {track.Discipline} {track.Season}".Trim();
    }

    public string Save(CarCard car, TrackInfo track, TuningConstraints _, TuneResult? result) =>
        Save(car, track, new SelectedParts(), result);

    public string Save(CarCard car, TrackInfo track, SelectedParts parts, TuneResult? result, TuningConstraints? constraints = null)
    {
        string name = AutoProfileName(car, track);
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
