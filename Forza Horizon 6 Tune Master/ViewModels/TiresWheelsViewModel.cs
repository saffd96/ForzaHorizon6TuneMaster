using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;

namespace Forza_Horizon_6_Tune_Master.ViewModels;

public class TiresWheelsViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private readonly PartDisplayNameResolver _resolver = new();
    private readonly Fh6DatabaseService _db = Fh6DatabaseService.Instance;
    private SelectedParts _parts = null!;
    private int _makeId;

    public ObservableCollection<PartOption> TireCompounds { get; } = new();
    public ObservableCollection<PartOption> TireWidthsFront { get; } = new();
    public ObservableCollection<PartOption> TireWidthsRear { get; } = new();
    public ObservableCollection<PartOption> TireAspectRatiosFront { get; } = new();
    public ObservableCollection<PartOption> TireAspectRatiosRear { get; } = new();
    public ObservableCollection<PartOption> RimsFront { get; } = new();
    public ObservableCollection<PartOption> RimsRear { get; } = new();
    public ObservableCollection<PartOption> TrackSpacingsFront { get; } = new();
    public ObservableCollection<PartOption> TrackSpacingsRear { get; } = new();

    public PartOption? SelectedTireCompound         { get => Pick(_parts.TireCompoundPartId, TireCompounds);         set { if (value != null) _parts.TireCompoundPartId = value.Id; } }
    public PartOption? SelectedTireWidthFront       { get => Pick(_parts.TireWidthFrontPartId, TireWidthsFront);     set { if (value != null) _parts.TireWidthFrontPartId = value.Id; } }
    public PartOption? SelectedTireWidthRear        { get => Pick(_parts.TireWidthRearPartId, TireWidthsRear);       set { if (value != null) _parts.TireWidthRearPartId = value.Id; } }
    public PartOption? SelectedTireAspectRatioFront { get => Pick(_parts.TireAspectRatioFrontPartId, TireAspectRatiosFront); set { if (value != null) _parts.TireAspectRatioFrontPartId = value.Id; } }
    public PartOption? SelectedTireAspectRatioRear  { get => Pick(_parts.TireAspectRatioRearPartId, TireAspectRatiosRear);   set { if (value != null) _parts.TireAspectRatioRearPartId = value.Id; } }
    public PartOption? SelectedRimFront             { get => Pick(_parts.RimFrontPartId, RimsFront);                 set { if (value != null) _parts.RimFrontPartId = value.Id; } }
    public PartOption? SelectedRimRear              { get => Pick(_parts.RimRearPartId, RimsRear);                   set { if (value != null) _parts.RimRearPartId = value.Id; } }
    public PartOption? SelectedTrackSpacingFront    { get => Pick(_parts.TrackSpacingFrontPartId, TrackSpacingsFront); set { if (value != null) _parts.TrackSpacingFrontPartId = value.Id; } }
    public PartOption? SelectedTrackSpacingRear     { get => Pick(_parts.TrackSpacingRearPartId, TrackSpacingsRear);   set { if (value != null) _parts.TrackSpacingRearPartId = value.Id; } }

    public void LoadForCar(CarCard car, SelectedParts parts)
    {
        _parts = parts;
        _makeId = _db.GetCar(car.CarDbId)?.MakeID ?? 0;

        int ordinal = car.CarDbId;
        int carBodyId = car.CarBodyId;

        _parts.TireCompoundPartId         ??= PickStock(_db.GetTireCompounds(ordinal))?.Id;
        _parts.TireWidthFrontPartId       ??= PickStock(_db.GetTireWidthsFront(carBodyId))?.Id;
        _parts.TireWidthRearPartId        ??= PickStock(_db.GetTireWidthsRear(carBodyId))?.Id;
        _parts.TireAspectRatioFrontPartId ??= PickStock(_db.GetTireAspectRatiosFront(carBodyId))?.Id;
        _parts.TireAspectRatioRearPartId  ??= PickStock(_db.GetTireAspectRatiosRear(carBodyId))?.Id;
        _parts.RimFrontPartId             ??= PickStock(_db.GetRimsFront(ordinal))?.Id;
        _parts.RimRearPartId              ??= PickStock(_db.GetRimsRear(ordinal))?.Id;
        _parts.TrackSpacingFrontPartId    ??= PickStock(_db.GetTrackSpacingsFront(carBodyId))?.Id;
        _parts.TrackSpacingRearPartId     ??= PickStock(_db.GetTrackSpacingsRear(carBodyId))?.Id;

        Populate(TireCompounds,         _db.GetTireCompounds(ordinal));
        Populate(TireWidthsFront,       _db.GetTireWidthsFront(carBodyId));
        Populate(TireWidthsRear,        _db.GetTireWidthsRear(carBodyId));
        Populate(TireAspectRatiosFront, _db.GetTireAspectRatiosFront(carBodyId));
        Populate(TireAspectRatiosRear,  _db.GetTireAspectRatiosRear(carBodyId));
        Populate(RimsFront,             _db.GetRimsFront(ordinal));
        Populate(RimsRear,              _db.GetRimsRear(ordinal));
        Populate(TrackSpacingsFront,    _db.GetTrackSpacingsFront(carBodyId));
        Populate(TrackSpacingsRear,     _db.GetTrackSpacingsRear(carBodyId));
        RefreshSelections();
    }

    private void RefreshSelections()
    {
        OnPropertyChanged(nameof(SelectedTireCompound));
        OnPropertyChanged(nameof(SelectedTireWidthFront));
        OnPropertyChanged(nameof(SelectedTireWidthRear));
        OnPropertyChanged(nameof(SelectedTireAspectRatioFront));
        OnPropertyChanged(nameof(SelectedTireAspectRatioRear));
        OnPropertyChanged(nameof(SelectedRimFront));
        OnPropertyChanged(nameof(SelectedRimRear));
        OnPropertyChanged(nameof(SelectedTrackSpacingFront));
        OnPropertyChanged(nameof(SelectedTrackSpacingRear));
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
