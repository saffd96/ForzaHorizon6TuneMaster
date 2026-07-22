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

    [ResetToStock("Swaps")]
    public int? EngineSwapPartId
    {
        get => _engineSwapPartId;
        set { if (Set(ref _engineSwapPartId, value)) { OnEngineSwapChanged(); } }
    }

    // Body-kit conversion (List_UpgradeCarBody.Id). Changing it re-keys every CarBody-scoped
    // upgrade and the body geometry; the host VM (MainViewModel.OnPartsChanged) resolves the
    // new CarBodyID, updates the car body and reloads the dependent modules.
    private int? _bodyKitPartId;

    [ResetToStock("Swaps")]
    public int? BodyKitPartId
    {
        get => _bodyKitPartId;
        set { if (Set(ref _bodyKitPartId, value)) OnBodyKitChanged(); }
    }

    private int? _forcedInductionPartId;

    [ResetToStock("Swaps")]
    public int? ForcedInductionPartId { get => _forcedInductionPartId; set { if (Set(ref _forcedInductionPartId, value)) OnPartChanged(); } }

    private int? _rearWingPartId;

    [ResetToStock("Swaps")]
    public int? RearWingPartId { get => _rearWingPartId; set { if (Set(ref _rearWingPartId, value)) OnPartChanged(); } }

    private int? _frontBumperPartId;
    [ResetToStock("Swaps")]
    public int? FrontBumperPartId { get => _frontBumperPartId; set { if (Set(ref _frontBumperPartId, value)) OnPartChanged(); } }

    private int? _rearBumperPartId;
    [ResetToStock("Swaps")]
    public int? RearBumperPartId { get => _rearBumperPartId; set { if (Set(ref _rearBumperPartId, value)) OnPartChanged(); } }

    private int? _sideSkirtPartId;
    [ResetToStock("Swaps")]
    public int? SideSkirtPartId { get => _sideSkirtPartId; set { if (Set(ref _sideSkirtPartId, value)) OnPartChanged(); } }

    private int? _hoodPartId;
    [ResetToStock("Swaps")]
    public int? HoodPartId { get => _hoodPartId; set { if (Set(ref _hoodPartId, value)) OnPartChanged(); } }

    // Wheel model selection: Id from List_Wheels (null = stock).
    private int? _wheelFrontId;
    [ResetToStock("Swaps")]
    public int? WheelFrontId { get => _wheelFrontId; set { if (Set(ref _wheelFrontId, value)) OnPartChanged(); } }
    private int? _wheelRearId;
    [ResetToStock("Swaps")]
    public int? WheelRearId { get => _wheelRearId; set { if (Set(ref _wheelRearId, value)) OnPartChanged(); } }
    // Stock wheel lightness tier + stock wheel/tyre geometry — baseline for the
    // wheel-style weight delta (see ComputeTotalMassDiff / WheelTierMassDiff).
    private int _stockRimTier;
    private int _stockFrontDiameterIn, _stockRearDiameterIn;
    private int _stockFrontTireWidthMm, _stockRearTireWidthMm;

    private int? _weightReductionPartId;

    [ResetToStock("Swaps")]
    public int? WeightReductionPartId { get => _weightReductionPartId; set { if (Set(ref _weightReductionPartId, value)) OnPartChanged(); } }

    private int? _chassisStiffnessPartId;

    [ResetToStock("Swaps")]
    public int? ChassisStiffnessPartId { get => _chassisStiffnessPartId; set { if (Set(ref _chassisStiffnessPartId, value)) OnPartChanged(); } }

    // ── Engine parts ────────────────────────────────────────────────────────

    private int? _camshaftPartId;

    [ResetToStock("Engine")]
    public int? CamshaftPartId { get => _camshaftPartId; set { if (Set(ref _camshaftPartId, value)) OnPartChanged(); } }
    private int? _displacementPartId;

    [ResetToStock("Engine")]
    public int? DisplacementPartId { get => _displacementPartId; set { if (Set(ref _displacementPartId, value)) OnPartChanged(); } }
    private int? _valvesPartId;

    [ResetToStock("Engine")]
    public int? ValvesPartId { get => _valvesPartId; set { if (Set(ref _valvesPartId, value)) OnPartChanged(); } }
    private int? _pistonsPartId;

    [ResetToStock("Engine")]
    public int? PistonsPartId { get => _pistonsPartId; set { if (Set(ref _pistonsPartId, value)) OnPartChanged(); } }
    private int? _fuelSystemPartId;

    [ResetToStock("Engine")]
    public int? FuelSystemPartId { get => _fuelSystemPartId; set { if (Set(ref _fuelSystemPartId, value)) OnPartChanged(); } }
    private int? _ignitionPartId;

    [ResetToStock("Engine")]
    public int? IgnitionPartId { get => _ignitionPartId; set { if (Set(ref _ignitionPartId, value)) OnPartChanged(); } }
    private int? _exhaustPartId;

    [ResetToStock("Engine")]
    public int? ExhaustPartId { get => _exhaustPartId; set { if (Set(ref _exhaustPartId, value)) OnPartChanged(); } }
    private int? _intakePartId;

    [ResetToStock("Engine")]
    public int? IntakePartId { get => _intakePartId; set { if (Set(ref _intakePartId, value)) OnPartChanged(); } }
    private int? _flywheelPartId;

    [ResetToStock("Engine")]
    public int? FlywheelPartId { get => _flywheelPartId; set { if (Set(ref _flywheelPartId, value)) OnPartChanged(); } }
    private int? _manifoldPartId;

    [ResetToStock("Engine")]
    public int? ManifoldPartId { get => _manifoldPartId; set { if (Set(ref _manifoldPartId, value)) OnPartChanged(); } }
    private int? _oilCoolingPartId;

    [ResetToStock("Engine")]
    public int? OilCoolingPartId { get => _oilCoolingPartId; set { if (Set(ref _oilCoolingPartId, value)) OnPartChanged(); } }
    private int? _restrictorPartId;

    [ResetToStock("Engine")]
    public int? RestrictorPartId { get => _restrictorPartId; set { if (Set(ref _restrictorPartId, value)) OnPartChanged(); } }
    private int? _intercoolerPartId;

    [ResetToStock("Engine")]
    public int? IntercoolerPartId { get => _intercoolerPartId; set { if (Set(ref _intercoolerPartId, value)) OnPartChanged(); } }

    // Manual rev-limiter override: when set, PowerCalculator uses this value as car.MaxRPM
    // instead of the DB-derived redline (GameRedlineScale is only validated on one car —
    // this is the escape hatch for the rest). Everything downstream (gearing, top speed,
    // launch RPM, power/torque curve display) reads car.MaxRPM, so setting this alone is
    // enough to make it "participate in calculations" without reshaping the curve data.
    private int? _maxRpmOverride;

    [ResetToStock("Engine")]
    public int? MaxRpmOverride { get => _maxRpmOverride; set { if (Set(ref _maxRpmOverride, value)) OnPartChanged(); } }

    // ── Suspension ──────────────────────────────────────────────────────────

    private int? _springDamperPartId;

    [ResetToStock("Suspension")]
    public int? SpringDamperPartId { get => _springDamperPartId; set { if (Set(ref _springDamperPartId, value)) OnPartChanged(); } }
    private int? _tireCompoundPartId;

    [ResetToStock("Suspension")]
    public int? TireCompoundPartId { get => _tireCompoundPartId; set { if (Set(ref _tireCompoundPartId, value)) OnPartChanged(); } }
    private int? _tireWidthFrontPartId;

    [ResetToStock("Suspension")]
    public int? TireWidthFrontPartId { get => _tireWidthFrontPartId; set { if (Set(ref _tireWidthFrontPartId, value)) OnPartChanged(); } }
    private int? _tireWidthRearPartId;

    [ResetToStock("Suspension")]
    public int? TireWidthRearPartId { get => _tireWidthRearPartId; set { if (Set(ref _tireWidthRearPartId, value)) OnPartChanged(); } }
    private int? _brakePartId;

    [ResetToStock("Suspension")]
    public int? BrakePartId { get => _brakePartId; set { if (Set(ref _brakePartId, value)) OnPartChanged(); } }
    private int? _antiSwayFrontPartId;

    [ResetToStock("Suspension")]
    public int? AntiSwayFrontPartId { get => _antiSwayFrontPartId; set { if (Set(ref _antiSwayFrontPartId, value)) OnPartChanged(); } }
    private int? _antiSwayRearPartId;

    [ResetToStock("Suspension")]
    public int? AntiSwayRearPartId { get => _antiSwayRearPartId; set { if (Set(ref _antiSwayRearPartId, value)) OnPartChanged(); } }

    // Manual min/max overrides for spring rate and dampers — same escape-hatch pattern as
    // MaxRpmOverride above: when set, the suspension calculators clamp against these instead of
    // the selected SpringDamper part's DB range. Null means "use the DB-derived bound" (default).
    private double? _springFrontMinOverride;
    [ResetToStock("Suspension")]
    public double? SpringFrontMinOverride { get => _springFrontMinOverride; set { if (Set(ref _springFrontMinOverride, value)) OnPartChanged(); } }
    private double? _springFrontMaxOverride;
    [ResetToStock("Suspension")]
    public double? SpringFrontMaxOverride { get => _springFrontMaxOverride; set { if (Set(ref _springFrontMaxOverride, value)) OnPartChanged(); } }
    private double? _springRearMinOverride;
    [ResetToStock("Suspension")]
    public double? SpringRearMinOverride { get => _springRearMinOverride; set { if (Set(ref _springRearMinOverride, value)) OnPartChanged(); } }
    private double? _springRearMaxOverride;
    [ResetToStock("Suspension")]
    public double? SpringRearMaxOverride { get => _springRearMaxOverride; set { if (Set(ref _springRearMaxOverride, value)) OnPartChanged(); } }

    // Ride height uses a direct-value override, same shape as MaxRpmOverride — not a min/max
    // range like the spring overrides above: the user types the exact clearance they want and
    // SuspensionCalculator.CalculateRideHeight uses it as-is instead of the computed value.
    private double? _rideHeightFrontOverride;
    [ResetToStock("Suspension")]
    public double? RideHeightFrontOverride { get => _rideHeightFrontOverride; set { if (Set(ref _rideHeightFrontOverride, value)) OnPartChanged(); } }
    private double? _rideHeightRearOverride;
    [ResetToStock("Suspension")]
    public double? RideHeightRearOverride { get => _rideHeightRearOverride; set { if (Set(ref _rideHeightRearOverride, value)) OnPartChanged(); } }

    // ── Transmission ────────────────────────────────────────────────────────

    private int? _drivetrainSwapPartId;

    [ResetToStock("Transmission")]
    public int? DrivetrainSwapPartId { get => _drivetrainSwapPartId; set { if (Set(ref _drivetrainSwapPartId, value)) OnDrivetrainSwapChanged(); } }

    private int? _transmissionPartId;

    [ResetToStock("Transmission")]
    public int? TransmissionPartId { get => _transmissionPartId; set { if (Set(ref _transmissionPartId, value)) OnPartChanged(); } }
    private int? _clutchPartId;

    [ResetToStock("Transmission")]
    public int? ClutchPartId { get => _clutchPartId; set { if (Set(ref _clutchPartId, value)) OnPartChanged(); } }
    private int? _drivelinePartId;

    [ResetToStock("Transmission")]
    public int? DrivelinePartId { get => _drivelinePartId; set { if (Set(ref _drivelinePartId, value)) OnPartChanged(); } }
    private int? _differentialPartId;

    [ResetToStock("Transmission")]
    public int? DifferentialPartId { get => _differentialPartId; set { if (Set(ref _differentialPartId, value)) OnPartChanged(); } }

    // Manual gearbox: the player decides exactly when to shift and can hold each gear all the
    // way to the rev limiter, unlike an automatic which GearingCalculator targets at
    // CalculationHelpers.RevLimitFraction (95% of MaxRPM) to leave a safety margin. When set,
    // GearingCalculator computes gear ratios/final drive against 100% of MaxRPM instead.
    // Not [ResetToStock] — it's a driving/calculation preference, not a fitted part, and the
    // reset mechanism sets tagged properties to null via reflection, which a non-nullable bool
    // can't accept.
    private bool _isManualTransmission;

    public bool IsManualTransmission { get => _isManualTransmission; set { if (Set(ref _isManualTransmission, value)) OnPartChanged(); } }

    // ── Wheels ──────────────────────────────────────────────────────────────

    private int? _tireProfilePartId;

    [ResetToStock("Wheels")]
    public int? TireProfilePartId { get => _tireProfilePartId; set { if (Set(ref _tireProfilePartId, value)) OnPartChanged(); } }
    private int? _tireAspectRatioFrontPartId;

    [ResetToStock("Wheels")]
    public int? TireAspectRatioFrontPartId { get => _tireAspectRatioFrontPartId; set { if (Set(ref _tireAspectRatioFrontPartId, value)) OnPartChanged(); } }
    private int? _tireAspectRatioRearPartId;

    [ResetToStock("Wheels")]
    public int? TireAspectRatioRearPartId { get => _tireAspectRatioRearPartId; set { if (Set(ref _tireAspectRatioRearPartId, value)) OnPartChanged(); } }
    private int? _rimFrontPartId;

    [ResetToStock("Wheels")]
    public int? RimFrontPartId { get => _rimFrontPartId; set { if (Set(ref _rimFrontPartId, value)) OnPartChanged(); } }
    private int? _rimRearPartId;

    [ResetToStock("Wheels")]
    public int? RimRearPartId { get => _rimRearPartId; set { if (Set(ref _rimRearPartId, value)) OnPartChanged(); } }
    private int? _trackSpacingFrontPartId;

    [ResetToStock("Wheels")]
    public int? TrackSpacingFrontPartId { get => _trackSpacingFrontPartId; set { if (Set(ref _trackSpacingFrontPartId, value)) OnPartChanged(); } }
    private int? _trackSpacingRearPartId;

    [ResetToStock("Wheels")]
    public int? TrackSpacingRearPartId { get => _trackSpacingRearPartId; set { if (Set(ref _trackSpacingRearPartId, value)) OnPartChanged(); } }

    // ── Motor (electric) ────────────────────────────────────────────────────

    private int? _motorSwapPartId;

    [ResetToStock("Motor")]
    public int? MotorSwapPartId { get => _motorSwapPartId; set { if (Set(ref _motorSwapPartId, value)) OnMotorSwapChanged(); } }

    private int? _motorPartId;

    [ResetToStock("Motor")]
    public int? MotorPartId { get => _motorPartId; set { if (Set(ref _motorPartId, value)) OnPartChanged(); } }

    // ── Resolved IDs (from DB, set when car/engine/swap changes) ────────────

    private int? _engineId;
    public int? EngineId { get => _engineId; set => Set(ref _engineId, value); }

    private int? _drivetrainId;
    public int? DrivetrainId { get => _drivetrainId; set => Set(ref _drivetrainId, value); }

    private int? _carBodyOrdinal;
    public int? CarBodyOrdinal { get => _carBodyOrdinal; set => Set(ref _carBodyOrdinal, value); }

    // CarBodyId at load time (Ordinal×1000, before any body-kit swap) — the baseline for
    // BodyKitCurbWeightDiff(). Captured once in SetCarData, same pattern as _stockRimTier etc.
    private int _stockCarBodyId;

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
        _stockCarBodyId = car.CarBodyId;
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

    public void ResetCategory(string category)
    {
        foreach (var prop in _resetTargets)
        {
            var attr = prop.GetCustomAttribute<ResetToStockAttribute>(false);
            if (attr?.Category == category)
                prop.SetValue(this, null);
        }
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
        // Skip stock FI: its mass is already baked into the car's curbWeight (native engine)
        // or into the engine swap's MassDiff (swapped engine). Only count non-stock upgrades.
        if (_forcedInductionPartId != null)
        {
            var fi = _db.GetForcedInductionById(_forcedInductionPartId.Value);
            if (fi is { IsStock: false })
                total += fi.MassDiff;
        }
        // Intercooler: for engines whose intercooler list has a genuine IsStock row (a free
        // baseline bundled with the FI kit, MassDiff always 0 for that row — verified across
        // all 351 such engines in the DB), only the delta above that baseline is new mass.
        // ~43% of FI-capable engines (269/620) have NO IsStock row at all — every intercooler
        // tier for them is a real, separately-required part, so there is nothing to subtract.
        // Previously this subtracted the lowest-*Level* row's MassDiff unconditionally, which
        // for those 269 engines silently treated a real ~20-35 kg part as "already bundled" and
        // undercounted total mass by that amount on every forced-induction build. Subtracting
        // the actual IsStock row (0 when present, nothing when absent) fixes that while leaving
        // the 351 engines with a genuine stock row unaffected (their stock row already sits at
        // the lowest Level with MassDiff 0, so the result is identical there).
        if (_intercoolerPartId != null)
        {
            var ic = _db.GetIntercoolerById(_intercoolerPartId.Value);
            if (ic != null)
            {
                double delta = ic.MassDiff;
                if (_forcedInductionPartId != null)
                {
                    var allIcs = _db.GetIntercoolers(EngineId ?? 0);
                    delta -= allIcs.FirstOrDefault(x => x.IsStock)?.MassDiff ?? 0;
                }
                if (delta > 0) total += delta;
            }
        }
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
        total += PartMassDiff(_hoodPartId, id => _db.GetHoodById(id));
        total += PartMassDiff(_antiSwayFrontPartId, id => _db.GetArbFrontById(id));
        total += PartMassDiff(_antiSwayRearPartId, id => _db.GetArbRearById(id));
        // TireWidthMassCoef: the DB's flat width MassDiff undershoots real in-game growth —
        // see constant definition below for calibration.
        total += PartMassDiff(_tireWidthFrontPartId, id => _db.GetTireWidthFrontById(id)) * TireWidthMassCoef;
        total += PartMassDiff(_tireWidthRearPartId, id => _db.GetTireWidthRearById(id)) * TireWidthMassCoef;
        total += PartMassDiff(_tireAspectRatioFrontPartId, id => _db.GetTireAspectRatioFrontById(id));
        total += PartMassDiff(_tireAspectRatioRearPartId, id => _db.GetTireAspectRatioRearById(id));
        // RimSizeMassCoef: the DB's flat rim-diameter MassDiff undershoots real in-game growth —
        // see constant definition below for calibration.
        total += PartMassDiff(_rimFrontPartId, id => _db.GetRimFrontById(id)) * RimSizeMassCoef;
        total += PartMassDiff(_rimRearPartId, id => _db.GetRimRearById(id)) * RimSizeMassCoef;
        total += PartMassDiff(_trackSpacingFrontPartId, id => _db.GetTrackSpacingFrontById(id));
        total += PartMassDiff(_trackSpacingRearPartId, id => _db.GetTrackSpacingRearById(id));
        total += PartMassDiff(_drivetrainSwapPartId, id => _db.GetDrivetrainSwapById(id));
        total += PartMassDiff(_motorSwapPartId, id => _db.GetMotorSwapById(id));
        total += PartMassDiff(_motorPartId, id => _db.GetMotorPartById(id));
        total += PartMassDiff(_weightReductionPartId, id => _db.GetWeightReductionById(id));
        total += PartMassDiff(_chassisStiffnessPartId, id => _db.GetChassisStiffnessById(id));
        total += PartMassDiff(_bodyKitPartId, id => _db.GetCarBodyKitById(id));
        // List_UpgradeCarBody.MassDiff (above) is 0 for the vast majority of body-kit
        // conversions — it is NOT where the game encodes a kit's real mass difference. The
        // authoritative per-body curb weight is List_UpgradeCarBodyWeight's stock (Level 0)
        // InitialMass, which is keyed per CarBodyID and can differ from the original body's
        // by over 100 kg (e.g. a stripped race-shell conversion) with zero reflected above.
        total += BodyKitCurbWeightDiff();
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
    // Refitted against 4 real in-game readings on a 2018 Ferrari FXXK Evo (stock wheel
    // MassLevel 4) at TWO different fitment sizes — confirming the D²·W shape itself (not
    // just the coefficient), since the residual stays within ~0.6 kg at both sizes:
    //   stock fitment  (front 19"×305mm, rear 20"×365mm): VS XX (diff=1) +7 kg,  Design DH (diff=4) +26 kg
    //   20"/355 front, 21"/395 rear fitment:              VS XX (diff=1) +8 kg,  Design DH (diff=4) +31 kg
    // The prior 6.8e-5 (fitted "across several cars/rim sizes" with no recorded reference
    // data) overshot every one of these by 24-34%. Least-squares fit through all 4 points
    // (line forced through the origin, since diff=0 must give 0 kg): COEF ≈ 4.99e-5.
    private const double WheelTierMassCoef = 4.99e-5;

    // List_UpgradeCarBodyTireWidthFront/Rear.MassDiff is a real, per-car-consistent field (unlike
    // rim diameter, see the CLAUDE.md invariants table) but undershoots in-game weight growth by a
    // stable factor. Calibrated 2026-07 from 9 real in-game readings across two very different
    // cars, isolating width alone (both cars only expose ONE tire-aspect-ratio row in the DB — the
    // displayed profile is a fixed function of width, not an independent choice, so no separate
    // aspect-ratio effect confounds these readings):
    //   Toyota GT86 (17" stock, 215mm): 7 points, front+rear, width 215->295mm
    //   Ferrari FXXK Evo WP (19-20" stock): 2 points, front 305->355mm, rear 365->395mm
    // Least-squares fit through the origin (real = k * dbMassDiff) across all 9 points: k = 2.596.
    private const double TireWidthMassCoef = 2.6;

    // List_UpgradeRimSizeFront/Rear.MassDiff also undershoots real growth, but by a much smaller,
    // near-1x factor. Early 1-2-step tests on 3 cars looked wildly inconsistent (ratio 0.7x-2.2x)
    // because whole-kg in-game weight rounding dominates such small deltas. Resolved 2026-07 with
    // an 8-step test on a Toyota Tacoma TRD Pro (16" stock, all 8 rim-size levels, front+rear
    // together, profile held stock) — cumulative deltas up to +25 kg swamp the rounding noise and
    // give a tight, consistent fit (ratio 0.92-1.15 across all 8 points). Combined least-squares
    // fit (Tacoma's 8 points + GT86's 2 + FXXK Evo's 2, all stock-tier wheel so WheelTierMassDiff
    // contributes nothing): k = 1.10. One car (1985 Toyota Sprinter FE) is a confirmed 2x outlier
    // against this otherwise-consistent formula — see the CLAUDE.md invariants table; not
    // accommodated in code, since 3 of 4 tested cars agree tightly on k=1.10.
    private const double RimSizeMassCoef = 1.10;

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

    // Mass delta from swapping to an alternate body (List_UpgradeCarBody.CarBodyId != stock).
    // Uses each body's own stock List_UpgradeCarBodyWeight entry as its true curb weight —
    // that InitialMass is per-CarBodyID and is where the game actually encodes a kit's mass
    // change (List_UpgradeCarBody.MassDiff is a separate, usually-zero hardware delta; see
    // ComputeTotalMass). Falls back to 0 (no adjustment) if either body has no stock entry.
    private double BodyKitCurbWeightDiff()
    {
        int currentBodyId = CarBodyOrdinal ?? _stockCarBodyId;
        if (currentBodyId == _stockCarBodyId) return 0;

        var stock = _db.GetWeightReductions(_stockCarBodyId).FirstOrDefault(w => w.IsStock);
        var kit = _db.GetWeightReductions(currentBodyId).FirstOrDefault(w => w.IsStock);
        if (stock == null || kit == null) return 0;

        return kit.InitialMass - stock.InitialMass;
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
        total = AddWeightDist(total, _hoodPartId, id => _db.GetHoodById(id));
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
