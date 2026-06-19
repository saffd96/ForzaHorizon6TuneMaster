using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;

namespace Forza_Horizon_6_Tune_Master.ViewModels;

// "Аэродинамика и внешний вид" module: front/rear bumper, side skirts, rear wing.
// Weight reduction and chassis stiffness moved to the chassis module (SuspensionViewModel).
public class AeroVisualViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private readonly PartDisplayNameResolver _resolver = new();
    private readonly Fh6DatabaseService _db = Fh6DatabaseService.Instance;
    private SelectedParts _parts = null!;
    private int _makeId;

    public ObservableCollection<PartOption> FrontBumpers { get; } = new();
    public ObservableCollection<PartOption> RearWings { get; } = new();
    public ObservableCollection<PartOption> RearBumpers { get; } = new();
    public ObservableCollection<PartOption> SideSkirts { get; } = new();

    public PartOption? SelectedFrontBumper
    {
        get => FrontBumpers.FirstOrDefault(o => o.Id == _parts.FrontBumperPartId);
        set { if (value != null) _parts.FrontBumperPartId = value.Id; }
    }

    public PartOption? SelectedRearWing
    {
        get => RearWings.FirstOrDefault(o => o.Id == _parts.RearWingPartId);
        set { if (value != null) _parts.RearWingPartId = value.Id; }
    }

    public PartOption? SelectedRearBumper
    {
        get => RearBumpers.FirstOrDefault(o => o.Id == _parts.RearBumperPartId);
        set { if (value != null) _parts.RearBumperPartId = value.Id; }
    }

    public PartOption? SelectedSideSkirt
    {
        get => SideSkirts.FirstOrDefault(o => o.Id == _parts.SideSkirtPartId);
        set { if (value != null) _parts.SideSkirtPartId = value.Id; }
    }

    public void LoadForCar(CarCard car, SelectedParts parts)
    {
        _parts = parts;
        _makeId = _db.GetCar(car.CarDbId)?.MakeID ?? 0;

        int ordinal = car.CarDbId;
        int carBodyId = car.CarBodyId;

        LoadBodyKit(FrontBumpers, _db.GetFrontBumpers(carBodyId), () => parts.FrontBumperPartId,
            id => parts.FrontBumperPartId = id);
        LoadBodyKit(RearWings, _db.GetRearWings(ordinal), () => parts.RearWingPartId,
            id => parts.RearWingPartId = id);
        LoadBodyKit(RearBumpers, _db.GetRearBumpers(carBodyId), () => parts.RearBumperPartId,
            id => parts.RearBumperPartId = id);
        LoadBodyKit(SideSkirts, _db.GetSideSkirts(carBodyId), () => parts.SideSkirtPartId,
            id => parts.SideSkirtPartId = id);

        RefreshSelections();
    }

    private void RefreshSelections()
    {
        OnPropertyChanged(nameof(SelectedFrontBumper));
        OnPropertyChanged(nameof(SelectedRearWing));
        OnPropertyChanged(nameof(SelectedRearBumper));
        OnPropertyChanged(nameof(SelectedSideSkirt));
    }

    private void LoadBodyKit<T>(ObservableCollection<PartOption> target,
        System.Collections.Generic.List<T> list, System.Func<int?> getId, System.Action<int?> setId)
        where T : DbUpgradePart
    {
        if (!getId().HasValue)
            setId(PickStock(list)?.Id);
        ReplaceAll(target, _resolver.ToOptions(list, _makeId));
    }

    private static void ReplaceAll(ObservableCollection<PartOption> target, ObservableCollection<PartOption> source)
    {
        target.Clear();
        foreach (var o in source) target.Add(o);
    }

    private static T? PickStock<T>(System.Collections.Generic.List<T> parts) where T : DbUpgradePart =>
        parts.FirstOrDefault(p => p.IsStock) ?? parts.FirstOrDefault();
}
