using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;

namespace Forza_Horizon_6_Tune_Master.ViewModels;

// "Кузовные комплекты и модификации" module: the big swaps — engine, drivetrain and
// forced induction. Forced induction is a SINGLE dropdown listing every option
// (NA + each turbo/supercharger tier), so picking a different one directly changes
// the installed part (and therefore the power). All data comes from the DB.
public class SwapsViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private readonly PartDisplayNameResolver _resolver = new();
    private readonly Fh6DatabaseService _db = Fh6DatabaseService.Instance;
    private SelectedParts _parts = null!;
    private int _makeId;

    private const int NoneId = -1; // sentinel for the "no forced induction" (NA) option

    public ObservableCollection<PartOption> EngineSwaps { get; } = new();
    public ObservableCollection<PartOption> DrivetrainSwaps { get; } = new();
    public ObservableCollection<PartOption> ForcedInductions { get; } = new();

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

    public PartOption? SelectedForcedInduction
    {
        get => _parts.ForcedInductionPartId.HasValue
            ? ForcedInductions.FirstOrDefault(o => o.Id == _parts.ForcedInductionPartId.Value)
            : ForcedInductions.FirstOrDefault(o => o.Id == NoneId);
        set
        {
            if (value == null) return;
            _parts.ForcedInductionPartId = value.Id == NoneId ? (int?)null : value.Id;
            OnPropertyChanged(nameof(SelectedForcedInduction));
        }
    }

    private int EngineId => _parts.EngineId ?? 0;

    public void LoadForCar(CarCard car, SelectedParts parts)
    {
        _parts = parts;
        _makeId = _db.GetCar(car.CarDbId)?.MakeID ?? 0;

        int ordinal = car.CarDbId;

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
        BuildForcedInductions(EngineId);

        RefreshSelections();
    }

    // Called on an engine swap: rebuild the forced-induction list for the new engine.
    public void ResetForcedInductionForEngine(int engineId)
    {
        _parts.ForcedInductionPartId = StockFiPart(engineId)?.Id;
        BuildForcedInductions(engineId);
        OnPropertyChanged(nameof(SelectedForcedInduction));
    }

    private void RefreshSelections()
    {
        OnPropertyChanged(nameof(SelectedEngineSwap));
        OnPropertyChanged(nameof(SelectedDrivetrainSwap));
        OnPropertyChanged(nameof(SelectedForcedInduction));
    }

    // ── Forced induction helpers ─────────────────────────────────────────────
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

    // One flat list: "Нет" + every turbo/supercharger tier available for the engine.
    private void BuildForcedInductions(int engineId)
    {
        ForcedInductions.Clear();
        ForcedInductions.Add(new PartOption { Id = NoneId, DisplayName = Aspir(1), IsStock = true });
        foreach (var kind in new[] { FiKind.SingleTurbo, FiKind.TwinTurbo, FiKind.Centrifugal, FiKind.PositiveDisplacement })
            foreach (var p in FiPartsOfKind(engineId, kind))
                ForcedInductions.Add(new PartOption { Id = p.Id, DisplayName = _resolver.Resolve(p, _makeId), IsStock = p.IsStock });
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
