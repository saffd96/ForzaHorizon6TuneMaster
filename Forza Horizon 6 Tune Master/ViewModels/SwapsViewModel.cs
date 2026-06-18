using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;

namespace Forza_Horizon_6_Tune_Master.ViewModels;

public class SwapsViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private readonly PartDisplayNameResolver _resolver = new();
    private readonly Fh6DatabaseService _db = Fh6DatabaseService.Instance;
    private SelectedParts _parts = null!;
    private int _makeId;

    public ObservableCollection<PartOption> EngineSwaps { get; } = new();
    public ObservableCollection<PartOption> ForcedInductions { get; } = new();

    public PartOption? SelectedEngineSwap
    {
        get => EngineSwaps.FirstOrDefault(o => o.Id == _parts.EngineSwapPartId);
        set { if (value != null) _parts.EngineSwapPartId = value.Id; }
    }

    public PartOption? SelectedForcedInduction
    {
        get => ForcedInductions.FirstOrDefault(o => o.Id == _parts.ForcedInductionPartId);
        set { if (value != null) _parts.ForcedInductionPartId = value.Id; }
    }

    public void LoadForCar(CarCard car, SelectedParts parts)
    {
        _parts = parts;
        _makeId = _db.GetCar(car.CarDbId)?.MakeID ?? 0;

        int ordinal = car.CarDbId;
        int engineId = parts.EngineId ?? 0;

        if (!parts.EngineSwapPartId.HasValue)
        {
            var list = _db.GetEngineSwaps(ordinal);
            parts.EngineSwapPartId = PickStock(list)?.Id;
            ReplaceAll(EngineSwaps, _resolver.ToOptions(list, _makeId));
        }
        else
            ReplaceAll(EngineSwaps, _resolver.ToOptions(_db.GetEngineSwaps(ordinal), _makeId));

        if (!parts.ForcedInductionPartId.HasValue) parts.ForcedInductionPartId = null;
        bool canShowFi = !IsEngineSwapStock() || HasStockForcedInduction(engineId);
        if (canShowFi)
            ReplaceAll(ForcedInductions, MergeForcedInductionOptions(engineId));
        else
        {
            ForcedInductions.Clear();
            _parts.ForcedInductionPartId = null;
        }

        RefreshSelections();
    }

    public void ReloadForcedInduction(int engineId)
    {
        bool canShowFi = !IsEngineSwapStock() || HasStockForcedInduction(engineId);
        if (canShowFi)
            ReplaceAll(ForcedInductions, MergeForcedInductionOptions(engineId));
        else
        {
            ForcedInductions.Clear();
            _parts.ForcedInductionPartId = null;
        }
    }

    public void ResetForcedInductionToStock(int engineId)
    {
        bool canShowFi = !IsEngineSwapStock() || HasStockForcedInduction(engineId);
        if (canShowFi)
        {
            ReplaceAll(ForcedInductions, MergeForcedInductionOptions(engineId));
            _parts.ForcedInductionPartId = ForcedInductions.FirstOrDefault(o => o.IsStock)?.Id;
            if (!_parts.ForcedInductionPartId.HasValue && ForcedInductions.Count > 0)
                _parts.ForcedInductionPartId = ForcedInductions[0].Id;
        }
        else
        {
            ForcedInductions.Clear();
            _parts.ForcedInductionPartId = null;
        }
        OnPropertyChanged(nameof(SelectedForcedInduction));
    }

    private void RefreshSelections()
    {
        OnPropertyChanged(nameof(SelectedEngineSwap));
        OnPropertyChanged(nameof(SelectedForcedInduction));
    }

    private ObservableCollection<PartOption> MergeForcedInductionOptions(int engineId)
    {
        var all = new System.Collections.Generic.List<PartOption>();
        foreach (var p in _db.GetTurbosSingle(engineId))
            all.Add(new PartOption { Id = p.Id, DisplayName = _resolver.Resolve(p, _makeId), IsStock = p.IsStock });
        foreach (var p in _db.GetTurbosTwin(engineId))
            all.Add(new PartOption { Id = p.Id, DisplayName = _resolver.Resolve(p, _makeId), IsStock = p.IsStock });
        foreach (var p in _db.GetCSC(engineId))
            all.Add(new PartOption { Id = p.Id, DisplayName = _resolver.Resolve(p, _makeId), IsStock = p.IsStock });
        foreach (var p in _db.GetDSC(engineId))
            all.Add(new PartOption { Id = p.Id, DisplayName = _resolver.Resolve(p, _makeId), IsStock = p.IsStock });
        var col = new ObservableCollection<PartOption>();
        foreach (var o in all) col.Add(o);
        return col;
    }

    private bool IsEngineSwapStock()
    {
        if (!_parts.EngineSwapPartId.HasValue) return true;
        var stock = EngineSwaps.FirstOrDefault(o => o.IsStock);
        return stock != null && _parts.EngineSwapPartId.Value == stock.Id;
    }

    private bool HasStockForcedInduction(int engineId)
    {
        return _db.GetTurbosSingle(engineId).Any(p => p.IsStock)
            || _db.GetTurbosTwin(engineId).Any(p => p.IsStock)
            || _db.GetCSC(engineId).Any(p => p.IsStock)
            || _db.GetDSC(engineId).Any(p => p.IsStock);
    }

    private static void ReplaceAll(ObservableCollection<PartOption> target, ObservableCollection<PartOption> source)
    {
        target.Clear();
        foreach (var o in source) target.Add(o);
    }

    private static T? PickStock<T>(System.Collections.Generic.List<T> parts) where T : DbUpgradePart =>
        parts.FirstOrDefault(p => p.IsStock) ?? parts.FirstOrDefault();
}
