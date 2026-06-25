using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;

namespace Forza_Horizon_6_Tune_Master.ViewModels;

public class MotorPartsViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private readonly PartDisplayNameResolver _resolver = new();
    private readonly Fh6DatabaseService _db = Fh6DatabaseService.Instance;
    private SelectedParts _parts = null!;
    private int _makeId;

    public ObservableCollection<PartOption> MotorSwaps { get; } = new();
    public ObservableCollection<PartOption> MotorParts { get; } = new();

    public PartOption? SelectedMotorSwap
    {
        get => _parts == null ? null : MotorSwaps.FirstOrDefault(o => o.Id == _parts.MotorSwapPartId);
        set { if (value != null) _parts.MotorSwapPartId = value.Id; }
    }

    public PartOption? SelectedMotorPart
    {
        get => _parts == null ? null : MotorParts.FirstOrDefault(o => o.Id == _parts.MotorPartId);
        set { if (value != null) _parts.MotorPartId = value.Id; }
    }

    public string? MotorInfo { get; private set; }

    public void LoadForCar(CarCard car, SelectedParts parts)
    {
        _parts = parts;
        _makeId = _db.GetCar(car.CarDbId)?.MakeID ?? 0;

        int ordinal = car.CarDbId;

        var swaps = _db.GetMotorSwaps(ordinal);
        _parts.MotorSwapPartId ??= PickStock(swaps)?.Id;
        _parts.MotorId = _parts.MotorSwapPartId != null
            ? _db.GetMotorSwapById(_parts.MotorSwapPartId.Value)?.MotorID
            : null;

        Populate(MotorSwaps, swaps);
        ReloadMotorParts(_parts.MotorId ?? 0);
        RefreshSelections();
    }

    public void ReloadMotorParts(int motorId)
    {
        var parts = _db.GetMotorParts(motorId);
        _parts.MotorPartId ??= PickStock(parts)?.Id;
        Populate(MotorParts, parts);
        UpdateMotorInfo(motorId);
        OnPropertyChanged(nameof(SelectedMotorPart));
    }

    public void ResetToStockForMotor(int motorId)
    {
        _parts.MotorPartId = null;
        ReloadMotorParts(motorId);
    }

    private void UpdateMotorInfo(int motorId)
    {
        var motor = _db.GetMotor(motorId);
        if (motor == null)
        {
            MotorInfo = null;
        }
        else
        {
            string name = string.IsNullOrWhiteSpace(motor.MotorName) ? "Motor" : motor.MotorName;
            MotorInfo = $"{name} · {motor.BatteryCapacity:F1} kWh · {motor.RedlineRPM:F0} rpm";
        }
        OnPropertyChanged(nameof(MotorInfo));
    }

    private void RefreshSelections()
    {
        OnPropertyChanged(nameof(SelectedMotorSwap));
        OnPropertyChanged(nameof(SelectedMotorPart));
        OnPropertyChanged(nameof(MotorInfo));
    }

    private void Populate<T>(ObservableCollection<PartOption> target, System.Collections.Generic.List<T> source) where T : DbUpgradePart
    {
        target.Clear();
        foreach (var o in _resolver.ToOptions(source, _makeId))
            target.Add(o);
    }

    private static T? PickStock<T>(System.Collections.Generic.List<T> parts) where T : DbUpgradePart =>
        parts.FirstOrDefault(p => p.IsStock) ?? parts.FirstOrDefault();
}
