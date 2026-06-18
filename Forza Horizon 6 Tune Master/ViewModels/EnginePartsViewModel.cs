using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;

namespace Forza_Horizon_6_Tune_Master.ViewModels;

public class EnginePartsViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private readonly PartDisplayNameResolver _resolver = new();
    private readonly Fh6DatabaseService _db = Fh6DatabaseService.Instance;
    private SelectedParts _parts = null!;
    private int _makeId;

    public ObservableCollection<PartOption> Camshafts { get; } = new();
    public ObservableCollection<PartOption> Displacements { get; } = new();
    public ObservableCollection<PartOption> Valves { get; } = new();
    public ObservableCollection<PartOption> Pistons { get; } = new();
    public ObservableCollection<PartOption> FuelSystems { get; } = new();
    public ObservableCollection<PartOption> Ignitions { get; } = new();
    public ObservableCollection<PartOption> Exhausts { get; } = new();
    public ObservableCollection<PartOption> Intakes { get; } = new();
    public ObservableCollection<PartOption> Flywheels { get; } = new();
    public ObservableCollection<PartOption> Manifolds { get; } = new();
    public ObservableCollection<PartOption> OilCoolings { get; } = new();
    public ObservableCollection<PartOption> Restrictors { get; } = new();
    public ObservableCollection<PartOption> Intercoolers { get; } = new();

    public PartOption? SelectedCamshaft     { get => Pick(_parts.CamshaftPartId, Camshafts);     set { if (value != null) _parts.CamshaftPartId = value.Id; } }
    public PartOption? SelectedDisplacement { get => Pick(_parts.DisplacementPartId, Displacements); set { if (value != null) _parts.DisplacementPartId = value.Id; } }
    public PartOption? SelectedValves       { get => Pick(_parts.ValvesPartId, Valves);          set { if (value != null) _parts.ValvesPartId = value.Id; } }
    public PartOption? SelectedPistons      { get => Pick(_parts.PistonsPartId, Pistons);        set { if (value != null) _parts.PistonsPartId = value.Id; } }
    public PartOption? SelectedFuelSystem   { get => Pick(_parts.FuelSystemPartId, FuelSystems); set { if (value != null) _parts.FuelSystemPartId = value.Id; } }
    public PartOption? SelectedIgnition     { get => Pick(_parts.IgnitionPartId, Ignitions);     set { if (value != null) _parts.IgnitionPartId = value.Id; } }
    public PartOption? SelectedExhaust      { get => Pick(_parts.ExhaustPartId, Exhausts);       set { if (value != null) _parts.ExhaustPartId = value.Id; } }
    public PartOption? SelectedIntake       { get => Pick(_parts.IntakePartId, Intakes);         set { if (value != null) _parts.IntakePartId = value.Id; } }
    public PartOption? SelectedFlywheel     { get => Pick(_parts.FlywheelPartId, Flywheels);     set { if (value != null) _parts.FlywheelPartId = value.Id; } }
    public PartOption? SelectedManifold     { get => Pick(_parts.ManifoldPartId, Manifolds);     set { if (value != null) _parts.ManifoldPartId = value.Id; } }
    public PartOption? SelectedOilCooling   { get => Pick(_parts.OilCoolingPartId, OilCoolings); set { if (value != null) _parts.OilCoolingPartId = value.Id; } }
    public PartOption? SelectedRestrictor   { get => Pick(_parts.RestrictorPartId, Restrictors); set { if (value != null) _parts.RestrictorPartId = value.Id; } }
    public PartOption? SelectedIntercooler  { get => Pick(_parts.IntercoolerPartId, Intercoolers); set { if (value != null) _parts.IntercoolerPartId = value.Id; } }

    public void LoadForCar(CarCard car, SelectedParts parts)
    {
        _parts = parts;
        _makeId = _db.GetCar(car.CarDbId)?.MakeID ?? 0;
        int engineId = parts.EngineId ?? 0;

        _parts.CamshaftPartId     ??= PickStock(_db.GetCamshafts(engineId))?.Id;
        _parts.DisplacementPartId ??= PickStock(_db.GetDisplacement(engineId))?.Id;
        _parts.ValvesPartId       ??= PickStock(_db.GetValves(engineId))?.Id;
        _parts.PistonsPartId      ??= PickStock(_db.GetPistons(engineId))?.Id;
        _parts.FuelSystemPartId   ??= PickStock(_db.GetFuelSystems(engineId))?.Id;
        _parts.IgnitionPartId     ??= PickStock(_db.GetIgnition(engineId))?.Id;
        _parts.ExhaustPartId      ??= PickStock(_db.GetExhaust(engineId))?.Id;
        _parts.IntakePartId       ??= PickStock(_db.GetIntake(engineId))?.Id;
        _parts.FlywheelPartId     ??= PickStock(_db.GetFlywheels(engineId))?.Id;
        _parts.ManifoldPartId     ??= PickStock(_db.GetManifolds(engineId))?.Id;
        _parts.OilCoolingPartId   ??= PickStock(_db.GetOilCooling(engineId))?.Id;
        _parts.RestrictorPartId   ??= PickStock(_db.GetRestrictors(engineId))?.Id;
        _parts.IntercoolerPartId  ??= PickStock(_db.GetIntercoolers(engineId))?.Id;

        LoadForEngine(engineId);
    }

    public void ReloadForEngine(int engineId)
    {
        LoadForEngine(engineId);
    }

    public void ResetToStockForEngine(int engineId)
    {
        int camLevel     = LevelOf(_parts.CamshaftPartId,     id => _db.GetCamshaftById(id));
        int dispLevel    = LevelOf(_parts.DisplacementPartId, id => _db.GetDisplacementById(id));
        int valvesLevel  = LevelOf(_parts.ValvesPartId,       id => _db.GetValvesById(id));
        int pistonsLevel = LevelOf(_parts.PistonsPartId,      id => _db.GetPistonsById(id));
        int fuelLevel    = LevelOf(_parts.FuelSystemPartId,   id => _db.GetFuelSystemById(id));
        int ignLevel     = LevelOf(_parts.IgnitionPartId,     id => _db.GetIgnitionById(id));
        int exhLevel     = LevelOf(_parts.ExhaustPartId,      id => _db.GetExhaustById(id));
        int intakeLevel  = LevelOf(_parts.IntakePartId,       id => _db.GetIntakeById(id));
        int flyLevel     = LevelOf(_parts.FlywheelPartId,     id => _db.GetFlywheelById(id));
        int manLevel     = LevelOf(_parts.ManifoldPartId,     id => _db.GetManifoldById(id));
        int oilLevel     = LevelOf(_parts.OilCoolingPartId,   id => _db.GetOilCoolingById(id));
        int restLevel    = LevelOf(_parts.RestrictorPartId,   id => _db.GetRestrictorById(id));
        int interLevel   = LevelOf(_parts.IntercoolerPartId,  id => _db.GetIntercoolerById(id));

        _parts.CamshaftPartId     = MatchOrStock(_db.GetCamshafts(engineId), camLevel)?.Id;
        _parts.DisplacementPartId = MatchOrStock(_db.GetDisplacement(engineId), dispLevel)?.Id;
        _parts.ValvesPartId       = MatchOrStock(_db.GetValves(engineId), valvesLevel)?.Id;
        _parts.PistonsPartId      = MatchOrStock(_db.GetPistons(engineId), pistonsLevel)?.Id;
        _parts.FuelSystemPartId   = MatchOrStock(_db.GetFuelSystems(engineId), fuelLevel)?.Id;
        _parts.IgnitionPartId     = MatchOrStock(_db.GetIgnition(engineId), ignLevel)?.Id;
        _parts.ExhaustPartId      = MatchOrStock(_db.GetExhaust(engineId), exhLevel)?.Id;
        _parts.IntakePartId       = MatchOrStock(_db.GetIntake(engineId), intakeLevel)?.Id;
        _parts.FlywheelPartId     = MatchOrStock(_db.GetFlywheels(engineId), flyLevel)?.Id;
        _parts.ManifoldPartId     = MatchOrStock(_db.GetManifolds(engineId), manLevel)?.Id;
        _parts.OilCoolingPartId   = MatchOrStock(_db.GetOilCooling(engineId), oilLevel)?.Id;
        _parts.RestrictorPartId   = MatchOrStock(_db.GetRestrictors(engineId), restLevel)?.Id;
        _parts.IntercoolerPartId  = MatchOrStock(_db.GetIntercoolers(engineId), interLevel)?.Id;

        LoadForEngine(engineId);
    }

    public void ReloadIntercooler(int engineId)
    {
        LoadIntercoolers(engineId);
        if (!_parts.ForcedInductionPartId.HasValue)
            _parts.IntercoolerPartId = null;
        OnPropertyChanged(nameof(SelectedIntercooler));
    }

    private void LoadForEngine(int engineId)
    {
        Populate(Camshafts,     _db.GetCamshafts(engineId));
        Populate(Displacements, _db.GetDisplacement(engineId));
        Populate(Valves,        _db.GetValves(engineId));
        Populate(Pistons,       _db.GetPistons(engineId));
        Populate(FuelSystems,   _db.GetFuelSystems(engineId));
        Populate(Ignitions,     _db.GetIgnition(engineId));
        Populate(Exhausts,      _db.GetExhaust(engineId));
        Populate(Intakes,       _db.GetIntake(engineId));
        Populate(Flywheels,     _db.GetFlywheels(engineId));
        Populate(Manifolds,     _db.GetManifolds(engineId));
        Populate(OilCoolings,   _db.GetOilCooling(engineId));
        Populate(Restrictors,   _db.GetRestrictors(engineId));
        LoadIntercoolers(engineId);
        RefreshSelections();
    }

    private void LoadIntercoolers(int engineId)
    {
        if (_parts.ForcedInductionPartId.HasValue)
            Populate(Intercoolers, _db.GetIntercoolers(engineId));
        else
        {
            Intercoolers.Clear();
            _parts.IntercoolerPartId = null;
        }
    }

    private void RefreshSelections()
    {
        OnPropertyChanged(nameof(SelectedCamshaft));
        OnPropertyChanged(nameof(SelectedDisplacement));
        OnPropertyChanged(nameof(SelectedValves));
        OnPropertyChanged(nameof(SelectedPistons));
        OnPropertyChanged(nameof(SelectedFuelSystem));
        OnPropertyChanged(nameof(SelectedIgnition));
        OnPropertyChanged(nameof(SelectedExhaust));
        OnPropertyChanged(nameof(SelectedIntake));
        OnPropertyChanged(nameof(SelectedFlywheel));
        OnPropertyChanged(nameof(SelectedManifold));
        OnPropertyChanged(nameof(SelectedOilCooling));
        OnPropertyChanged(nameof(SelectedRestrictor));
        OnPropertyChanged(nameof(SelectedIntercooler));
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
