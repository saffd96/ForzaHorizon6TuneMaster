using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;

namespace Forza_Horizon_6_Tune_Master.ViewModels;

// "Кузовные комплекты и модификации" module: the big swaps — engine, drivetrain and
// forced induction (a turbo/supercharger conversion is itself a swap). Forced induction
// is a two-step choice: here we pick only the TYPE (NA / single turbo / twin turbo /
// centrifugal / positive-displacement); the specific tier/level within that type is
// chosen further down in the engine module. All data comes from the DB.
public class SwapsViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private readonly PartDisplayNameResolver _resolver = new();
    private readonly Fh6DatabaseService _db = Fh6DatabaseService.Instance;
    private SelectedParts _parts = null!;
    private int _makeId;

    public ObservableCollection<PartOption> BodyKits { get; } = new();
    public ObservableCollection<PartOption> EngineSwaps { get; } = new();
    public ObservableCollection<PartOption> DrivetrainSwaps { get; } = new();
    public ObservableCollection<FiTypeOption> ForcedInductionTypes { get; } = new();

    public PartOption? SelectedBodyKit
    {
        get => BodyKits.FirstOrDefault(o => o.Id == _parts.BodyKitPartId);
        set { if (value != null) _parts.BodyKitPartId = value.Id; }
    }

    public PartOption? SelectedEngineSwap
    {
        get => EngineSwaps.FirstOrDefault(o => o.Id == _parts.EngineSwapPartId);
        set { if (value != null) _parts.EngineSwapPartId = value.Id; }
    }

    public PartOption? SelectedDrivetrainSwap
    {
        get => DrivetrainSwaps.FirstOrDefault(o => o.Id == _parts.DrivetrainSwapPartId);
        set { if (value != null) _parts.DrivetrainSwapPartId = value.Id; }
    }

    public FiTypeOption? SelectedForcedInductionType
    {
        get { var kind = CurrentFiKind(); return ForcedInductionTypes.FirstOrDefault(t => t.Kind == kind); }
        set
        {
            if (value == null) return;
            // Picking a type sets FI to that type's lowest tier (the base of that type);
            // the engine module rebuilds the level list from the DB off this change.
            _parts.ForcedInductionPartId = value.Kind == FiKind.None
                ? null
                : FiPartsOfKind(EngineId, value.Kind).OrderBy(p => p.Level).FirstOrDefault()?.Id;
            OnPropertyChanged(nameof(SelectedForcedInductionType));
        }
    }

    private int EngineId => _parts.EngineId ?? 0;

    // Engines that ship with factory forced induction (a stock turbo/SC marked IsStock in
    // its upgrade table) can't have it removed or swapped for another type — only upgraded
    // by level (handled in the engine module). So the aspiration-TYPE dropdown is hidden
    // for them; NA engines keep it so the user can add a turbo/supercharger.
    public bool ShowForcedInductionType => _parts == null || StockFiPart(EngineId) == null;

    public void LoadForCar(CarCard car, SelectedParts parts)
    {
        _parts = parts;
        _makeId = _db.GetCar(car.CarDbId)?.MakeID ?? 0;

        int ordinal = car.CarDbId;

        // Body kit drives which CarBodyID the bumper/skirt/tire-fitment/track lookups key off.
        var bodyKits = _db.GetCarBodies(ordinal);
        parts.BodyKitPartId ??= PickStock(bodyKits)?.Id;
        ReplaceAll(BodyKits, _resolver.ToOptions(bodyKits, _makeId));

        var engineSwaps = _db.GetEngineSwaps(ordinal);
        parts.EngineSwapPartId ??= PickStock(engineSwaps)?.Id;
        ReplaceAll(EngineSwaps, _resolver.ToOptions(engineSwaps, _makeId));

        // Loaded before TransmissionVM.LoadForCar so the default drivetrain-swap id is
        // available when the transmission/clutch/diff sections resolve their drivetrain.
        var drivetrainSwaps = _db.GetDrivetrainSwaps(ordinal);
        parts.DrivetrainSwapPartId ??= PickStock(drivetrainSwaps)?.Id;
        ReplaceAll(DrivetrainSwaps, _resolver.ToOptions(drivetrainSwaps, _makeId));

        // Forced induction default: factory FI part if any, otherwise none (NA). Set here
        // (before EngineVM.LoadForCar) so the engine module's intercooler resolves correctly.
        if (!parts.ForcedInductionPartId.HasValue)
            parts.ForcedInductionPartId = StockFiPart(EngineId)?.Id;
        BuildFiTypes(EngineId);

        RefreshSelections();
    }

    // Called on an engine swap: rebuild forced-induction type/level for the new engine.
    public void ResetForcedInductionForEngine(int engineId)
    {
        _parts.ForcedInductionPartId = StockFiPart(engineId)?.Id;
        BuildFiTypes(engineId);
        OnPropertyChanged(nameof(SelectedForcedInductionType));
        OnPropertyChanged(nameof(ShowForcedInductionType));
    }

    // Re-fires the dropdown bindings after a body-kit change repopulates lists elsewhere.
    public void RefreshBodyKitSelection() => OnPropertyChanged(nameof(SelectedBodyKit));

    private void RefreshSelections()
    {
        OnPropertyChanged(nameof(SelectedBodyKit));
        OnPropertyChanged(nameof(SelectedEngineSwap));
        OnPropertyChanged(nameof(SelectedDrivetrainSwap));
        OnPropertyChanged(nameof(SelectedForcedInductionType));
        OnPropertyChanged(nameof(ShowForcedInductionType));
    }

    // ── Forced induction helpers ─────────────────────────────────────────────
    private FiKind CurrentFiKind()
    {
        if (!_parts.ForcedInductionPartId.HasValue) return FiKind.None;
        return _db.GetForcedInductionById(_parts.ForcedInductionPartId.Value) switch
        {
            DbUpgradeTurboSingle => FiKind.SingleTurbo,
            DbUpgradeTurboTwin   => FiKind.TwinTurbo,
            DbUpgradeCSC         => FiKind.Centrifugal,
            DbUpgradeDSC         => FiKind.PositiveDisplacement,
            _                    => FiKind.None
        };
    }

    private List<DbUpgradePart> FiPartsOfKind(int engineId, FiKind kind) => kind switch
    {
        FiKind.SingleTurbo          => _db.GetTurbosSingle(engineId).Cast<DbUpgradePart>().ToList(),
        FiKind.TwinTurbo            => _db.GetTurbosTwin(engineId).Cast<DbUpgradePart>().ToList(),
        FiKind.Centrifugal          => _db.GetCSC(engineId).Cast<DbUpgradePart>().ToList(),
        FiKind.PositiveDisplacement => _db.GetDSC(engineId).Cast<DbUpgradePart>().ToList(),
        _                           => new List<DbUpgradePart>()
    };

    private DbUpgradePart? StockFiPart(int engineId)
    {
        foreach (var kind in new[] { FiKind.SingleTurbo, FiKind.TwinTurbo, FiKind.Centrifugal, FiKind.PositiveDisplacement })
        {
            var stock = FiPartsOfKind(engineId, kind).FirstOrDefault(p => p.IsStock);
            if (stock != null) return stock;
        }
        return null;
    }

    private void BuildFiTypes(int engineId)
    {
        ForcedInductionTypes.Clear();
        ForcedInductionTypes.Add(new FiTypeOption { Kind = FiKind.None, DisplayName = Aspir(1) });
        if (_db.GetTurbosSingle(engineId).Count > 0) ForcedInductionTypes.Add(new FiTypeOption { Kind = FiKind.SingleTurbo, DisplayName = Aspir(2) });
        if (_db.GetTurbosTwin(engineId).Count > 0)   ForcedInductionTypes.Add(new FiTypeOption { Kind = FiKind.TwinTurbo, DisplayName = Aspir(3) });
        if (_db.GetCSC(engineId).Count > 0)          ForcedInductionTypes.Add(new FiTypeOption { Kind = FiKind.Centrifugal, DisplayName = Aspir(6) });
        if (_db.GetDSC(engineId).Count > 0)          ForcedInductionTypes.Add(new FiTypeOption { Kind = FiKind.PositiveDisplacement, DisplayName = Aspir(5) });
    }

    private static string Aspir(int id)
    {
        string key = $"List_Aspiration_IDS_DisplayName_{id}";
        string v = LocalizationService.Instance.T(key);
        return v == key ? key : v;
    }

    private static void ReplaceAll(ObservableCollection<PartOption> target, ObservableCollection<PartOption> source)
    {
        target.Clear();
        foreach (var o in source) target.Add(o);
    }

    private static T? PickStock<T>(List<T> parts) where T : DbUpgradePart =>
        parts.FirstOrDefault(p => p.IsStock) ?? parts.FirstOrDefault();
}
