using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;

namespace Forza_Horizon_6_Tune_Master.ViewModels;

public class TransmissionViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private readonly PartDisplayNameResolver _resolver = new();
    private readonly Fh6DatabaseService _db = Fh6DatabaseService.Instance;
    private SelectedParts _parts = null!;
    private int _makeId;
    private int _carDbId;

    public ObservableCollection<PartOption> DrivetrainSwaps { get; } = new();
    public ObservableCollection<PartOption> Transmissions { get; } = new();
    public ObservableCollection<PartOption> Clutches { get; } = new();
    public ObservableCollection<PartOption> Drivelines { get; } = new();
    public ObservableCollection<PartOption> Differentials { get; } = new();

    public PartOption? SelectedDrivetrainSwap { get => Pick(_parts.DrivetrainSwapPartId, DrivetrainSwaps); set { if (value != null) _parts.DrivetrainSwapPartId = value.Id; } }
    public PartOption? SelectedTransmission  { get => Pick(_parts.TransmissionPartId, Transmissions); set { if (value != null) _parts.TransmissionPartId = value.Id; } }
    public PartOption? SelectedClutch        { get => Pick(_parts.ClutchPartId, Clutches);            set { if (value != null) _parts.ClutchPartId = value.Id; } }
    public PartOption? SelectedDriveline     { get => Pick(_parts.DrivelinePartId, Drivelines);        set { if (value != null) _parts.DrivelinePartId = value.Id; } }
    public PartOption? SelectedDifferential  { get => Pick(_parts.DifferentialPartId, Differentials);  set { if (value != null) _parts.DifferentialPartId = value.Id; } }

    public void LoadForCar(CarCard car, SelectedParts parts)
    {
        _parts = parts;
        _carDbId = car.CarDbId;
        _makeId = _db.GetCar(car.CarDbId)?.MakeID ?? 0;

        var swaps = _db.GetDrivetrainSwaps(car.CarDbId);
        _parts.DrivetrainSwapPartId ??= PickStock(swaps)?.Id;

        Populate(DrivetrainSwaps, swaps);
        LoadForSelectedDrivetrain();
    }

    public void ReloadForDrivetrain(int drivetrainId)
    {
        LoadForDrivetrain(drivetrainId);
    }

    public void ResetToStockForDrivetrain(int drivetrainId)
    {
        int transLevel   = LevelOf(_parts.TransmissionPartId,  id => _db.GetTransmissionById(id));
        int clutchLevel  = LevelOf(_parts.ClutchPartId,        id => _db.GetClutchById(id));
        int driveLevel   = LevelOf(_parts.DrivelinePartId,     id => _db.GetDrivelineById(id));
        int diffLevel    = LevelOf(_parts.DifferentialPartId,  id => _db.GetDifferentialById(id));

        _parts.TransmissionPartId  = MatchOrStock(_db.GetTransmissions(drivetrainId), transLevel)?.Id;
        _parts.ClutchPartId        = MatchOrStock(_db.GetClutches(drivetrainId), clutchLevel)?.Id;
        _parts.DrivelinePartId     = MatchOrStock(_db.GetDrivelines(drivetrainId), driveLevel)?.Id;
        _parts.DifferentialPartId  = MatchOrStock(_db.GetDifferentials(drivetrainId), diffLevel)?.Id;

        LoadForDrivetrain(drivetrainId);
    }

    public void LoadForSelectedDrivetrain()
    {
        int drivetrainId = GetSelectedDrivetrainId();
        LoadForDrivetrain(drivetrainId);
    }

    private void LoadForDrivetrain(int drivetrainId)
    {
        _parts.TransmissionPartId ??= PickStock(_db.GetTransmissions(drivetrainId))?.Id;
        _parts.ClutchPartId       ??= PickStock(_db.GetClutches(drivetrainId))?.Id;
        _parts.DrivelinePartId    ??= PickStock(_db.GetDrivelines(drivetrainId))?.Id;
        _parts.DifferentialPartId ??= PickStock(_db.GetDifferentials(drivetrainId))?.Id;

        Populate(Transmissions, _db.GetTransmissions(drivetrainId));
        Populate(Clutches,       _db.GetClutches(drivetrainId));
        Populate(Drivelines,     _db.GetDrivelines(drivetrainId));
        Populate(Differentials,  _db.GetDifferentials(drivetrainId));
        RefreshSelections();
    }

    private int GetSelectedDrivetrainId()
    {
        var swap = _parts.DrivetrainSwapPartId != null
            ? _db.GetDrivetrainSwapById(_parts.DrivetrainSwapPartId.Value)
            : null;
        if (swap != null)
            return swap.DrivetrainID;
        var stock = _db.GetStockDrivetrain(_carDbId);
        if (stock != null)
            return stock.DrivetrainID;
        return _parts.DrivetrainId ?? 0;
    }

    private void RefreshSelections()
    {
        OnPropertyChanged(nameof(SelectedDrivetrainSwap));
        OnPropertyChanged(nameof(SelectedTransmission));
        OnPropertyChanged(nameof(SelectedClutch));
        OnPropertyChanged(nameof(SelectedDriveline));
        OnPropertyChanged(nameof(SelectedDifferential));
    }

    private void Populate<T>(ObservableCollection<PartOption> target, System.Collections.Generic.List<T> source) where T : DbUpgradePart
    {
        target.Clear();
        foreach (var o in _resolver.ToOptions(source, _makeId))
            target.Add(o);
    }

    private static PartOption? Pick(int? partId, ObservableCollection<PartOption> options) =>
        options.FirstOrDefault(o => o.Id == partId);

    private static int LevelOf<T>(int? partId, Func<int, T?> getter) where T : DbUpgradePart
    {
        if (partId == null) return 0;
        return getter(partId.Value)?.Level ?? 0;
    }

    private static T? MatchOrStock<T>(System.Collections.Generic.List<T> parts, int level) where T : DbUpgradePart
    {
        if (level > 0)
        {
            var match = parts.FirstOrDefault(p => p.Level == level);
            if (match != null) return match;
        }
        return parts.FirstOrDefault(p => p.IsStock) ?? parts.FirstOrDefault();
    }

    private static T? PickStock<T>(System.Collections.Generic.List<T> parts) where T : DbUpgradePart =>
        parts.FirstOrDefault(p => p.IsStock) ?? parts.FirstOrDefault();
}
