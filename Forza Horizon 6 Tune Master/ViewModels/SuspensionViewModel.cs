using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;

namespace Forza_Horizon_6_Tune_Master.ViewModels;

public class SuspensionViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private readonly PartDisplayNameResolver _resolver = new();
    private readonly Fh6DatabaseService _db = Fh6DatabaseService.Instance;
    private SelectedParts _parts = null!;
    private int _makeId;

    public ObservableCollection<PartOption> SpringDampers { get; } = new();
    public ObservableCollection<PartOption> Brakes { get; } = new();
    public ObservableCollection<PartOption> AntiSwayFront { get; } = new();
    public ObservableCollection<PartOption> AntiSwayRear { get; } = new();
    public ObservableCollection<PartOption> WeightReductions { get; } = new();
    public ObservableCollection<PartOption> ChassisStiffness { get; } = new();

    public PartOption? SelectedSpringDamper  { get => _parts == null ? null : Pick(_parts.SpringDamperPartId, SpringDampers);  set { if (value != null) _parts.SpringDamperPartId = value.Id; } }
    public PartOption? SelectedBrake         { get => _parts == null ? null : Pick(_parts.BrakePartId, Brakes);                set { if (value != null) _parts.BrakePartId = value.Id; } }
    public PartOption? SelectedAntiSwayFront { get => _parts == null ? null : Pick(_parts.AntiSwayFrontPartId, AntiSwayFront); set { if (value != null) _parts.AntiSwayFrontPartId = value.Id; } }
    public PartOption? SelectedAntiSwayRear  { get => _parts == null ? null : Pick(_parts.AntiSwayRearPartId, AntiSwayRear);   set { if (value != null) _parts.AntiSwayRearPartId = value.Id; } }
    public PartOption? SelectedWeightReduction { get => _parts == null ? null : Pick(_parts.WeightReductionPartId, WeightReductions); set { if (value != null) _parts.WeightReductionPartId = value.Id; } }
    public PartOption? SelectedChassisStiffness { get => _parts == null ? null : Pick(_parts.ChassisStiffnessPartId, ChassisStiffness); set { if (value != null) _parts.ChassisStiffnessPartId = value.Id; } }

    public void LoadForCar(CarCard car, SelectedParts parts)
    {
        _parts = parts;
        _makeId = _db.GetCar(car.CarDbId)?.MakeID ?? 0;

        int ordinal = car.CarDbId;
        int carBodyId = car.CarBodyId;

        _parts.SpringDamperPartId    ??= PickStock(_db.GetSpringDampers(ordinal))?.Id;
        _parts.BrakePartId           ??= PickStock(_db.GetBrakes(ordinal))?.Id;
        _parts.AntiSwayFrontPartId   ??= PickStock(_db.GetAntiSwayFront(ordinal))?.Id;
        _parts.AntiSwayRearPartId    ??= PickStock(_db.GetAntiSwayRear(ordinal))?.Id;
        _parts.WeightReductionPartId ??= PickStock(_db.GetWeightReductions(carBodyId))?.Id;
        _parts.ChassisStiffnessPartId??= PickStock(_db.GetChassisStiffness(carBodyId))?.Id;

        Populate(SpringDampers,    _db.GetSpringDampers(ordinal));
        Populate(Brakes,           _db.GetBrakes(ordinal));
        Populate(AntiSwayFront,    _db.GetAntiSwayFront(ordinal));
        Populate(AntiSwayRear,     _db.GetAntiSwayRear(ordinal));
        Populate(WeightReductions, _db.GetWeightReductions(carBodyId));
        Populate(ChassisStiffness, _db.GetChassisStiffness(carBodyId));
        RefreshSelections();
    }

    private void RefreshSelections()
    {
        OnPropertyChanged(nameof(SelectedSpringDamper));
        OnPropertyChanged(nameof(SelectedBrake));
        OnPropertyChanged(nameof(SelectedAntiSwayFront));
        OnPropertyChanged(nameof(SelectedAntiSwayRear));
        OnPropertyChanged(nameof(SelectedWeightReduction));
        OnPropertyChanged(nameof(SelectedChassisStiffness));
    }

    private void Populate<T>(ObservableCollection<PartOption> target, System.Collections.Generic.List<T> source) where T : DbUpgradePart
    {
        target.Clear();
        foreach (var o in _resolver.ToOptions(source, _makeId))
            target.Add(o);
    }

    private static PartOption? Pick(int? partId, ObservableCollection<PartOption> options) =>
        options.FirstOrDefault(o => o.Id == partId);

    private static T? PickStock<T>(System.Collections.Generic.List<T> parts) where T : DbUpgradePart =>
        parts.FirstOrDefault(p => p.IsStock) ?? parts.FirstOrDefault();
}
