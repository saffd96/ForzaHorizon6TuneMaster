using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;

namespace Forza_Horizon_6_Tune_Master.ViewModels;

public class AeroVisualViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private readonly PartDisplayNameResolver _resolver = new();
    private readonly Fh6DatabaseService _db = Fh6DatabaseService.Instance;
    private SelectedParts _parts = null!;
    private int _makeId;

    public ObservableCollection<PartOption> RearWings { get; } = new();
    public ObservableCollection<PartOption> WeightReductions { get; } = new();
    public ObservableCollection<PartOption> ChassisStiffness { get; } = new();

    public PartOption? SelectedRearWing
    {
        get => RearWings.FirstOrDefault(o => o.Id == _parts.RearWingPartId);
        set { if (value != null) _parts.RearWingPartId = value.Id; }
    }

    public PartOption? SelectedWeightReduction
    {
        get => WeightReductions.FirstOrDefault(o => o.Id == _parts.WeightReductionPartId);
        set { if (value != null) _parts.WeightReductionPartId = value.Id; }
    }

    public PartOption? SelectedChassisStiffness
    {
        get => ChassisStiffness.FirstOrDefault(o => o.Id == _parts.ChassisStiffnessPartId);
        set { if (value != null) _parts.ChassisStiffnessPartId = value.Id; }
    }

    public void LoadForCar(CarCard car, SelectedParts parts)
    {
        _parts = parts;
        _makeId = _db.GetCar(car.CarDbId)?.MakeID ?? 0;

        int ordinal = car.CarDbId;
        int carBodyId = car.CarBodyId;

        if (!parts.RearWingPartId.HasValue)
        {
            var list = _db.GetRearWings(ordinal);
            parts.RearWingPartId = PickStock(list)?.Id;
            ReplaceAll(RearWings, _resolver.ToOptions(list, _makeId));
        }
        else
            ReplaceAll(RearWings, _resolver.ToOptions(_db.GetRearWings(ordinal), _makeId));

        if (!parts.WeightReductionPartId.HasValue)
        {
            var list = _db.GetWeightReductions(carBodyId);
            parts.WeightReductionPartId = PickStock(list)?.Id;
            ReplaceAll(WeightReductions, _resolver.ToOptions(list, _makeId));
        }
        else
            ReplaceAll(WeightReductions, _resolver.ToOptions(_db.GetWeightReductions(carBodyId), _makeId));

        if (!parts.ChassisStiffnessPartId.HasValue)
        {
            var list = _db.GetChassisStiffness(carBodyId);
            parts.ChassisStiffnessPartId = PickStock(list)?.Id;
            ReplaceAll(ChassisStiffness, _resolver.ToOptions(list, _makeId));
        }
        else
            ReplaceAll(ChassisStiffness, _resolver.ToOptions(_db.GetChassisStiffness(carBodyId), _makeId));

        RefreshSelections();
    }

    private void RefreshSelections()
    {
        OnPropertyChanged(nameof(SelectedRearWing));
        OnPropertyChanged(nameof(SelectedWeightReduction));
        OnPropertyChanged(nameof(SelectedChassisStiffness));
    }

    private static void ReplaceAll(ObservableCollection<PartOption> target, ObservableCollection<PartOption> source)
    {
        target.Clear();
        foreach (var o in source) target.Add(o);
    }

    private static T? PickStock<T>(System.Collections.Generic.List<T> parts) where T : DbUpgradePart =>
        parts.FirstOrDefault(p => p.IsStock) ?? parts.FirstOrDefault();
}
