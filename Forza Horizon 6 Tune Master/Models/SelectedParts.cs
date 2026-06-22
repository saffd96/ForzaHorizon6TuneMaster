using System;
using System.Linq;
using System.Reflection;
using Forza_Horizon_6_Tune_Master.Services;

namespace Forza_Horizon_6_Tune_Master.Models;

public class SelectedParts : NotifyBase
{
    private readonly Fh6DatabaseService _db = Fh6DatabaseService.Instance;

    private double _curbWeight;
    // Stock front weight distribution in PERCENT (e.g. 52), captured from the car. Part
    // WeightDistDiff values (engine/drivetrain swap, fuel, exhaust, FI, chassis, oil cooling)
    // are stored as FRACTIONS in the DB, so the running total is added back as *100.
    private double _baseWeightDistFront = 50.0;

    // ── Events ──────────────────────────────────────────────────────────────

    public event Action? PartsChanged;
    public event Action<double, double?>? CarMassUpdated; // totalMass, frontWeightDistPercent (null = keep current)

    // ── Swaps ───────────────────────────────────────────────────────────────

    private int? _engineSwapPartId;

    [ResetToStock]
    public int? EngineSwapPartId
    {
        get => _engineSwapPartId;
        set { if (Set(ref _engineSwapPartId, value)) { OnEngineSwapChanged(); } }
    }

    // Body-kit conversion (List_UpgradeCarBody.Id). Changing it re-keys every CarBody-scoped
    // upgrade and the body geometry; the host VM (MainViewModel.OnPartsChanged) resolves the
    // new CarBodyID, updates the car body and reloads the dependent modules.
    private int? _bodyKitPartId;

    [ResetToStock]
    public int? BodyKitPartId
    {
        get => _bodyKitPartId;
        set { if (Set(ref _bodyKitPartId, value)) OnBodyKitChanged(); }
    }

    private int? _forcedInductionPartId;

    [ResetToStock]
    public int? ForcedInductionPartId { get => _forcedInductionPartId; set { if (Set(ref _forcedInductionPartId, value)) OnPartChanged(); } }

    private int? _rearWingPartId;

    [ResetToStock]
    public int? RearWingPartId { get => _rearWingPartId; set { if (Set(ref _rearWingPartId, value)) OnPartChanged(); } }

    private int? _frontBumperPartId;
    [ResetToStock]
    public int? FrontBumperPartId { get => _frontBumperPartId; set { if (Set(ref _frontBumperPartId, value)) OnPartChanged(); } }

    private int? _rearBumperPartId;
    [ResetToStock]
    public int? RearBumperPartId { get => _rearBumperPartId; set { if (Set(ref _rearBumperPartId, value)) OnPartChanged(); } }

    private int? _sideSkirtPartId;
    [ResetToStock]
    public int? SideSkirtPartId { get => _sideSkirtPartId; set { if (Set(ref _sideSkirtPartId, value)) OnPartChanged(); } }

    // Wheel model selection: Id from List_Wheels (null = stock).
    private int? _wheelFrontId;
    [ResetToStock]
    public int? WheelFrontId { get => _wheelFrontId; set { if (Set(ref _wheelFrontId, value)) OnPartChanged(); } }
    private int? _wheelRearId;
    [ResetToStock]
    public int? WheelRearId { get => _wheelRearId; set { if (Set(ref _wheelRearId, value)) OnPartChanged(); } }
    // Stock wheel lightness tier + stock wheel/tyre geometry — baseline for the
    // wheel-style weight delta (see ComputeTotalMassDiff / WheelTierMassDiff).
    private int _stockRimTier;
    private int _stockFrontDiameterIn, _stockRearDiameterIn;
    private int _stockFrontTireWidthMm, _stockRearTireWidthMm;

    private int? _weightReductionPartId;

    [ResetToStock]
    public int? WeightReductionPartId { get => _weightReductionPartId; set { if (Set(ref _weightReductionPartId, value)) OnPartChanged(); } }

    private int? _chassisStiffnessPartId;

    [ResetToStock]
    public int? ChassisStiffnessPartId { get => _chassisStiffnessPartId; set { if (Set(ref _chassisStiffnessPartId, value)) OnPartChanged(); } }

    // ── Engine parts ────────────────────────────────────────────────────────

    private int? _camshaftPartId;

    [ResetToStock]
    public int? CamshaftPartId { get => _camshaftPartId; set { if (Set(ref _camshaftPartId, value)) OnPartChanged(); } }
    private int? _displacementPartId;

    [ResetToStock]
    public int? DisplacementPartId { get => _displacementPartId; set { if (Set(ref _displacementPartId, value)) OnPartChanged(); } }
    private int? _valvesPartId;

    [ResetToStock]
    public int? ValvesPartId { get => _valvesPartId; set { if (Set(ref _valvesPartId, value)) OnPartChanged(); } }
    private int? _pistonsPartId;

    [ResetToStock]
    public int? PistonsPartId { get => _pistonsPartId; set { if (Set(ref _pistonsPartId, value)) OnPartChanged(); } }
    private int? _fuelSystemPartId;

    [ResetToStock]
    public int? FuelSystemPartId { get => _fuelSystemPartId; set { if (Set(ref _fuelSystemPartId, value)) OnPartChanged(); } }
    private int? _ignitionPartId;

    [ResetToStock]
    public int? IgnitionPartId { get => _ignitionPartId; set { if (Set(ref _ignitionPartId, value)) OnPartChanged(); } }
    private int? _exhaustPartId;

    [ResetToStock]
    public int? ExhaustPartId { get => _exhaustPartId; set { if (Set(ref _exhaustPartId, value)) OnPartChanged(); } }
    private int? _intakePartId;

    [ResetToStock]
    public int? IntakePartId { get => _intakePartId; set { if (Set(ref _intakePartId, value)) OnPartChanged(); } }
    private int? _flywheelPartId;

    [ResetToStock]
    public int? FlywheelPartId { get => _flywheelPartId; set { if (Set(ref _flywheelPartId, value)) OnPartChanged(); } }
    private int? _manifoldPartId;

    [ResetToStock]
    public int? ManifoldPartId { get => _manifoldPartId; set { if (Set(ref _manifoldPartId, value)) OnPartChanged(); } }
    private int? _oilCoolingPartId;

    [ResetToStock]
    public int? OilCoolingPartId { get => _oilCoolingPartId; set { if (Set(ref _oilCoolingPartId, value)) OnPartChanged(); } }
    private int? _restrictorPartId;

    [ResetToStock]
    public int? RestrictorPartId { get => _restrictorPartId; set { if (Set(ref _restrictorPartId, value)) OnPartChanged(); } }
    private int? _intercoolerPartId;

    [ResetToStock]
    public int? IntercoolerPartId { get => _intercoolerPartId; set { if (Set(ref _intercoolerPartId, value)) OnPartChanged(); } }

    // ── Suspension ──────────────────────────────────────────────────────────

    private int? _springDamperPartId;

    [ResetToStock]
    public int? SpringDamperPartId { get => _springDamperPartId; set { if (Set(ref _springDamperPartId, value)) OnPartChanged(); } }
    private int? _tireCompoundPartId;

    [ResetToStock]
    public int? TireCompoundPartId { get => _tireCompoundPartId; set { if (Set(ref _tireCompoundPartId, value)) OnPartChanged(); } }
    private int? _tireWidthFrontPartId;

    [ResetToStock]
    public int? TireWidthFrontPartId { get => _tireWidthFrontPartId; set { if (Set(ref _tireWidthFrontPartId, value)) OnPartChanged(); } }
    private int? _tireWidthRearPartId;

    [ResetToStock]
    public int? TireWidthRearPartId { get => _tireWidthRearPartId; set { if (Set(ref _tireWidthRearPartId, value)) OnPartChanged(); } }
    private int? _brakePartId;

    [ResetToStock]
    public int? BrakePartId { get => _brakePartId; set { if (Set(ref _brakePartId, value)) OnPartChanged(); } }
    private int? _antiSwayFrontPartId;

    [ResetToStock]
    public int? AntiSwayFrontPartId { get => _antiSwayFrontPartId; set { if (Set(ref _antiSwayFrontPartId, value)) OnPartChanged(); } }
    private int? _antiSwayRearPartId;

    [ResetToStock]
    public int? AntiSwayRearPartId { get => _antiSwayRearPartId; set { if (Set(ref _antiSwayRearPartId, value)) OnPartChanged(); } }

    // ── Transmission ────────────────────────────────────────────────────────

    private int? _drivetrainSwapPartId;

    [ResetToStock]
    public int? DrivetrainSwapPartId { get => _drivetrainSwapPartId; set { if (Set(ref _drivetrainSwapPartId, value)) OnDrivetrainSwapChanged(); } }

    private int? _transmissionPartId;

    [ResetToStock]
    public int? TransmissionPartId { get => _transmissionPartId; set { if (Set(ref _transmissionPartId, value)) OnPartChanged(); } }
    private int? _clutchPartId;

    [ResetToStock]
    public int? ClutchPartId { get => _clutchPartId; set { if (Set(ref _clutchPartId, value)) OnPartChanged(); } }
    private int? _drivelinePartId;

    [ResetToStock]
    public int? DrivelinePartId { get => _drivelinePartId; set { if (Set(ref _drivelinePartId, value)) OnPartChanged(); } }
    private int? _differentialPartId;

    [ResetToStock]
    public int? DifferentialPartId { get => _differentialPartId; set { if (Set(ref _differentialPartId, value)) OnPartChanged(); } }

    // ── Wheels ──────────────────────────────────────────────────────────────

    private int? _tireProfilePartId;

    [ResetToStock]
    public int? TireProfilePartId { get => _tireProfilePartId; set { if (Set(ref _tireProfilePartId, value)) OnPartChanged(); } }
    private int? _tireAspectRatioFrontPartId;

    [ResetToStock]
    public int? TireAspectRatioFrontPartId { get => _tireAspectRatioFrontPartId; set { if (Set(ref _tireAspectRatioFrontPartId, value)) OnPartChanged(); } }
    private int? _tireAspectRatioRearPartId;

    [ResetToStock]
    public int? TireAspectRatioRearPartId { get => _tireAspectRatioRearPartId; set { if (Set(ref _tireAspectRatioRearPartId, value)) OnPartChanged(); } }
    private int? _rimFrontPartId;

    [ResetToStock]
    public int? RimFrontPartId { get => _rimFrontPartId; set { if (Set(ref _rimFrontPartId, value)) OnPartChanged(); } }
    private int? _rimRearPartId;

    [ResetToStock]
    public int? RimRearPartId { get => _rimRearPartId; set { if (Set(ref _rimRearPartId, value)) OnPartChanged(); } }
    private int? _trackSpacingFrontPartId;

    [ResetToStock]
    public int? TrackSpacingFrontPartId { get => _trackSpacingFrontPartId; set { if (Set(ref _trackSpacingFrontPartId, value)) OnPartChanged(); } }
    private int? _trackSpacingRearPartId;

    [ResetToStock]
    public int? TrackSpacingRearPartId { get => _trackSpacingRearPartId; set { if (Set(ref _trackSpacingRearPartId, value)) OnPartChanged(); } }

    // ── Motor (electric) ────────────────────────────────────────────────────

    private int? _motorSwapPartId;

    [ResetToStock]
    public int? MotorSwapPartId { get => _motorSwapPartId; set { if (Set(ref _motorSwapPartId, value)) OnMotorSwapChanged(); } }

    private int? _motorPartId;

    [ResetToStock]
    public int? MotorPartId { get => _motorPartId; set { if (Set(ref _motorPartId, value)) OnPartChanged(); } }

    // ── Resolved IDs (from DB, set when car/engine/swap changes) ────────────

    private int? _engineId;
    public int? EngineId { get => _engineId; set => Set(ref _engineId, value); }

    private int? _drivetrainId;
    public int? DrivetrainId { get => _drivetrainId; set => Set(ref _drivetrainId, value); }

    private int? _carBodyOrdinal;
    public int? CarBodyOrdinal { get => _carBodyOrdinal; set => Set(ref _carBodyOrdinal, value); }

    private int? _motorId;
    public int? MotorId { get => _motorId; set => Set(ref _motorId, value); }

    // ── Init from car ──────────────────────────────────────────────────────

    public void SetCarData(CarCard car, bool resetParts = true)
    {
        _curbWeight = car.CurbWeightKg;
        var dbCar = _db.GetCar(car.CarDbId);
        _baseWeightDistFront = dbCar != null && dbCar.WeightDistribution > 0
            ? dbCar.WeightDistribution * 100.0
            : (car.WeightDistributionFront > 0 ? car.WeightDistributionFront : 50.0);
        EngineId = car.EngineDbId;
        var stockDt = _db.GetStockDrivetrain(car.CarDbId);
        DrivetrainId = stockDt?.DrivetrainID;
        CarBodyOrdinal = car.CarBodyId;
        _stockRimTier = _db.GetStockWheelTier(dbCar?.MediaName) ?? 0;
        _stockFrontDiameterIn = dbCar?.FrontWheelDiameterIN ?? 0;
        _stockRearDiameterIn = dbCar?.RearWheelDiameterIN ?? 0;
        _stockFrontTireWidthMm = dbCar?.FrontTireWidthMM ?? 0;
        _stockRearTireWidthMm = dbCar?.RearTireWidthMM ?? 0;
        if (resetParts)
            ResetAllToStock();
    }

    public void ResolveEngineAndMotorIds()
    {
        var engineSwap = _engineSwapPartId != null ? _db.GetEngineSwapById(_engineSwapPartId.Value) : null;
        if (engineSwap != null)
            EngineId = engineSwap.EngineID;

        var motorSwap = _motorSwapPartId != null ? _db.GetMotorSwapById(_motorSwapPartId.Value) : null;
        if (motorSwap != null)
            MotorId = motorSwap.MotorID;

        var drivetrainSwap = _drivetrainSwapPartId != null ? _db.GetDrivetrainSwapById(_drivetrainSwapPartId.Value) : null;
        if (drivetrainSwap != null)
            DrivetrainId = drivetrainSwap.DrivetrainID;
    }

    public void ResetAllToStock()
    {
        foreach (var prop in _resetTargets)
            prop.SetValue(this, null);
    }

    private static readonly PropertyInfo[] _resetTargets = typeof(SelectedParts)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.IsDefined(typeof(ResetToStockAttribute), false))
        .ToArray();

    // ── Mass computation ────────────────────────────────────────────────────

    public double ComputeTotalMass()
    {
        double total = _curbWeight;
        total += PartMassDiff(_engineSwapPartId, id => _db.GetEngineSwapById(id));
        total += PartMassDiff(_camshaftPartId, id => _db.GetCamshaftById(id));
        total += PartMassDiff(_displacementPartId, id => _db.GetDisplacementById(id));
        total += PartMassDiff(_valvesPartId, id => _db.GetValvesById(id));
        total += PartMassDiff(_pistonsPartId, id => _db.GetPistonsById(id));
        total += PartMassDiff(_fuelSystemPartId, id => _db.GetFuelSystemById(id));
        total += PartMassDiff(_ignitionPartId, id => _db.GetIgnitionById(id));
        total += PartMassDiff(_exhaustPartId, id => _db.GetExhaustById(id));
        total += PartMassDiff(_intakePartId, id => _db.GetIntakeById(id));
        total += PartMassDiff(_flywheelPartId, id => _db.GetFlywheelById(id));
        total += PartMassDiff(_manifoldPartId, id => _db.GetManifoldById(id));
        total += PartMassDiff(_oilCoolingPartId, id => _db.GetOilCoolingById(id));
        total += PartMassDiff(_restrictorPartId, id => _db.GetRestrictorById(id));
        total += PartMassDiff(_forcedInductionPartId, id => _db.GetForcedInductionById(id));
        total += PartMassDiff(_intercoolerPartId, id => _db.GetIntercoolerById(id));
        total += PartMassDiff(_tireCompoundPartId, id => _db.GetTireCompoundById(id));
        total += PartMassDiff(_springDamperPartId, id => _db.GetSpringDamperById(id));
        total += PartMassDiff(_brakePartId, id => _db.GetBrakesById(id));
        total += PartMassDiff(_differentialPartId, id => _db.GetDifferentialById(id));
        total += PartMassDiff(_transmissionPartId, id => _db.GetTransmissionById(id));
        total += PartMassDiff(_clutchPartId, id => _db.GetClutchById(id));
        total += PartMassDiff(_drivelinePartId, id => _db.GetDrivelineById(id));
        total += PartMassDiff(_rearWingPartId, id => _db.GetRearWingById(id));
        total += PartMassDiff(_frontBumperPartId, id => _db.GetFrontBumperById(id));
        total += PartMassDiff(_rearBumperPartId, id => _db.GetRearBumperById(id));
        total += PartMassDiff(_sideSkirtPartId, id => _db.GetSideSkirtById(id));
        total += PartMassDiff(_antiSwayFrontPartId, id => _db.GetArbFrontById(id));
        total += PartMassDiff(_antiSwayRearPartId, id => _db.GetArbRearById(id));
        total += PartMassDiff(_tireWidthFrontPartId, id => _db.GetTireWidthFrontById(id));
        total += PartMassDiff(_tireWidthRearPartId, id => _db.GetTireWidthRearById(id));
        total += PartMassDiff(_tireAspectRatioFrontPartId, id => _db.GetTireAspectRatioFrontById(id));
        total += PartMassDiff(_tireAspectRatioRearPartId, id => _db.GetTireAspectRatioRearById(id));
        total += PartMassDiff(_rimFrontPartId, id => _db.GetRimFrontById(id));
        total += PartMassDiff(_rimRearPartId, id => _db.GetRimRearById(id));
        total += PartMassDiff(_trackSpacingFrontPartId, id => _db.GetTrackSpacingFrontById(id));
        total += PartMassDiff(_trackSpacingRearPartId, id => _db.GetTrackSpacingRearById(id));
        total += PartMassDiff(_drivetrainSwapPartId, id => _db.GetDrivetrainSwapById(id));
        total += PartMassDiff(_motorSwapPartId, id => _db.GetMotorSwapById(id));
        total += PartMassDiff(_motorPartId, id => _db.GetMotorPartById(id));
        total += PartMassDiff(_weightReductionPartId, id => _db.GetWeightReductionById(id));
        total += PartMassDiff(_chassisStiffnessPartId, id => _db.GetChassisStiffnessById(id));
        total += PartMassDiff(_bodyKitPartId, id => _db.GetCarBodyKitById(id));
        // Wheel style: the game's weight change is driven by the wheel's lightness
        // tier (List_Wheels.MassLevel), NOT its raw Mass. Δ per axle scales with the
        // fitted rim diameter² and tyre width. (Reverse-engineered from in-game
        // readings; see WheelTierMassDiff.)
        total += WheelTierMassDiff(_wheelFrontId, _stockFrontDiameterIn, _stockFrontTireWidthMm,
            _rimFrontPartId, id => _db.GetRimFrontById(id)?.FrontWheelDiameter,
            _tireWidthFrontPartId, id => _db.GetTireWidthFrontById(id)?.FrontTireWidth);
        total += WheelTierMassDiff(_wheelRearId, _stockRearDiameterIn, _stockRearTireWidthMm,
            _rimRearPartId, id => _db.GetRimRearById(id)?.RearWheelDiameter,
            _tireWidthRearPartId, id => _db.GetTireWidthRearById(id)?.RearTireWidth);
        return Math.Max(total, 1.0);
    }

    // Per-tier kg ≈ COEF · D² · W for the whole car; each axle is half of that.
    // COEF fitted to in-game weight readings across several cars/rim sizes.
    private const double WheelTierMassCoef = 6.8e-5;

    private double WheelTierMassDiff(int? wheelId, int stockDiameterIn, int stockTireWidthMm,
        int? rimPartId, Func<int, int?> rimDiameter,
        int? tireWidthPartId, Func<int, int?> tireWidth)
    {
        if (wheelId == null) return 0;
        var tier = _db.GetWheelTierById(wheelId.Value);
        if (tier == null) return 0;

        double d = rimPartId != null ? rimDiameter(rimPartId.Value) ?? stockDiameterIn : stockDiameterIn;
        double w = tireWidthPartId != null ? tireWidth(tireWidthPartId.Value) ?? stockTireWidthMm : stockTireWidthMm;
        if (d <= 0 || w <= 0) return 0;

        // Higher tier = lighter wheel ⇒ negative when fitting a lighter wheel than stock.
        return (_stockRimTier - tier.Value) * WheelTierMassCoef * d * d * w * 0.5;
    }

    public double? ComputeTotalWeightDistDiff()
    {
        double? total = null;
        total = AddWeightDist(total, _engineSwapPartId, id => _db.GetEngineSwapById(id));
        total = AddWeightDist(total, _drivetrainSwapPartId, id => _db.GetDrivetrainSwapById(id));
        total = AddWeightDist(total, _fuelSystemPartId, id => _db.GetFuelSystemById(id));
        total = AddWeightDist(total, _exhaustPartId, id => _db.GetExhaustById(id));
        total = AddWeightDist(total, _forcedInductionPartId, id => _db.GetForcedInductionById(id));
        total = AddWeightDist(total, _chassisStiffnessPartId, id => _db.GetChassisStiffnessById(id));
        total = AddWeightDist(total, _oilCoolingPartId, id => _db.GetOilCoolingById(id));
        return total;
    }

    private static double PartMassDiff<T>(int? partId, Func<int, T?> getter) where T : DbUpgradePart
    {
        if (partId == null) return 0;
        var part = getter(partId.Value);
        return part?.MassDiff ?? 0;
    }

    private static double? AddWeightDist<T>(double? current, int? partId, Func<int, T?> getter) where T : DbUpgradePart
    {
        if (partId == null) return current;
        var part = getter(partId.Value);
        if (part?.WeightDistDiff == null) return current;
        return (current ?? 0) + part.WeightDistDiff.Value;
    }

    // ── Internal ────────────────────────────────────────────────────────────

    private void OnEngineSwapChanged()
    {
        var swap = _engineSwapPartId != null ? _db.GetEngineSwapById(_engineSwapPartId.Value) : null;
        if (swap != null)
            EngineId = swap.EngineID;
        OnPartChanged();
    }

    private void OnMotorSwapChanged()
    {
        var swap = _motorSwapPartId != null ? _db.GetMotorSwapById(_motorSwapPartId.Value) : null;
        if (swap != null)
            MotorId = swap.MotorID;
        OnPartChanged();
    }

    private void OnDrivetrainSwapChanged()
    {
        var swap = _drivetrainSwapPartId != null ? _db.GetDrivetrainSwapById(_drivetrainSwapPartId.Value) : null;
        if (swap != null)
            DrivetrainId = swap.DrivetrainID;
        OnPartChanged();
    }

    private void OnBodyKitChanged()
    {
        var kit = _bodyKitPartId != null ? _db.GetCarBodyKitById(_bodyKitPartId.Value) : null;
        if (kit != null)
            CarBodyOrdinal = kit.CarBodyId;
        OnPartChanged();
    }

    // Final front weight distribution in PERCENT = stock baseline + Σ(part WeightDistDiff)×100.
    // Returns null when no fitted part carries a WeightDistDiff, so the caller leaves the car's
    // current distribution untouched (stock cars keep their DB value).
    public double? ComputeWeightDistFront()
    {
        var diff = ComputeTotalWeightDistDiff();
        if (diff == null) return null;
        return _baseWeightDistFront + diff.Value * 100.0;
    }

    private void OnPartChanged()
    {
        PartsChanged?.Invoke();
        CarMassUpdated?.Invoke(ComputeTotalMass(), ComputeWeightDistFront());
    }
}
