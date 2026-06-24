using System;
using System.Linq;
using System.Text.Json.Serialization;

namespace Forza_Horizon_6_Tune_Master.Models;

public class CarCard : NotifyBase
{
    private const double MaxSpeedClampKmh = 700.0;
    public Guid Id { get; set; } = Guid.NewGuid();

    // DB-backed identifiers
    public int CarDbId { get; set; }
    [JsonIgnore] public int EngineDbId { get; set; }
    [JsonIgnore] public int MotorDbId { get; set; }
    [JsonIgnore] public int CarBodyId { get; set; }
    [JsonIgnore] public int DriveTypeID { get; set; }
    [JsonIgnore] public double[]? CachedTorqueCurveNm { get; set; }
    [JsonIgnore] public double[]? CachedPowerCurveHP { get; set; }
    [JsonIgnore] public double[]? CachedClosedThrottleCurveNm { get; set; }

    private string _name = "";
    public string Name
    {
        get => _name;
        set { Set(ref _name, value); }
    }

    private string _make = "";
    public string Make
    {
        get => _make;
        set { Set(ref _make, value); }
    }

    private string _model = "";
    public string Model
    {
        get => _model;
        set { Set(ref _model, value); }
    }

    private int _year = 2024;
    public int Year
    {
        get => _year;
        set { Set(ref _year, value); }
    }

    [JsonIgnore] public string DisplayName => $"{Year} {Make} {Model}".Trim();

    // ── Rotational inertia factor (computed by PowerCalculator) ───────────

    [JsonIgnore] public double RotationalInertiaFactor { get; set; } = 1.0;

    // ── Mass ───────────────────────────────────────────────────────────────

    private double _totalMass = 1400;
    public double TotalMass
    {
        get => _totalMass;
        set { Set(ref _totalMass, Math.Max(value, 1.0)); OnPropertyChanged(nameof(MaxSpeedKmh)); }
    }

    private double _weightDistributionFront = 50;
    [JsonIgnore] public bool HasExplicitWeightDistribution { get; internal set; }
    public double WeightDistributionFront
    {
        get => _weightDistributionFront;
        set
        {
            if (Set(ref _weightDistributionFront, value))
                HasExplicitWeightDistribution = true;
        }
    }

    // ── Engine ─────────────────────────────────────────────────────────────

    private EngineType _engineType = EngineType.V6;
    public EngineType EngineType
    {
        get => _engineType;
        set { Set(ref _engineType, value); OnPropertyChanged(nameof(PowerPeakRPM)); OnPropertyChanged(nameof(TorquePeakRPM)); }
    }

    private EnginePosition _enginePosition = EnginePosition.Front;
    public EnginePosition EnginePosition
    {
        get => _enginePosition;
        set { Set(ref _enginePosition, value); }
    }

    private AspirationType? _aspirationType = Models.AspirationType.Natural;
    public AspirationType? AspirationType
    {
        get => _aspirationType;
        set
        {
            Set(ref _aspirationType, value);
            if (value is null || (value.Value != Models.AspirationType.SingleTurbo && value.Value != Models.AspirationType.TwinTurbo))
                AntiLag = false;
        }
    }

    private FuelType _fuelType = FuelType.Gasoline;
    public FuelType FuelType
    {
        get => _fuelType;
        set { Set(ref _fuelType, value); OnPropertyChanged(nameof(TorquePeakRPM)); }
    }

    private bool _antiLag;
    public bool AntiLag
    {
        get => _antiLag;
        set { Set(ref _antiLag, value); }
    }

    private PowertrainType _powertrainType = PowertrainType.ICE;
    public PowertrainType PowertrainType
    {
        get => _powertrainType;
        set
        {
            if (Set(ref _powertrainType, value))
            {
                if (value == PowertrainType.Electric)
                    AspirationType = null;
            }
            OnPropertyChanged(nameof(PowerPeakRPM));
            OnPropertyChanged(nameof(TorquePeakRPM));
        }
    }

    // ── Power curve peaks (computed from Cached* curves or estimated) ──────

    [JsonIgnore]
    public int PowerPeakRPM
    {
        get
        {
            if (CachedPowerCurveHP is { Length: > 0 })
            {
                double max = CachedPowerCurveHP.Max();
                int idx = Array.IndexOf(CachedPowerCurveHP, max);
                return (int)Math.Round(MaxRPM * idx / (double)(CachedPowerCurveHP.Length - 1) / 100.0) * 100;
            }
            if (PowertrainType == PowertrainType.Electric)
                return (int)Math.Round(MaxRPM * 0.45 / 100.0) * 100;
            return EstimatePeakRPM(EngineType, MaxRPM, _peakRpmFactors);
        }
    }

    [JsonIgnore]
    public int TorquePeakRPM
    {
        get
        {
            if (CachedTorqueCurveNm is { Length: > 0 })
            {
                double max = CachedTorqueCurveNm.Max();
                int idx = Array.IndexOf(CachedTorqueCurveNm, max);
                return Math.Max(500, (int)Math.Round(MaxRPM * idx / (double)(CachedTorqueCurveNm.Length - 1) / 100.0) * 100);
            }
            if (PowertrainType == PowertrainType.Electric)
                return 0;
            return Math.Max(500, EstimatePeakRPM(EngineType, MaxRPM, _torquePeakRpmFactors));
        }
    }

    private static readonly (EngineType Type, double Factor)[] _peakRpmFactors =
    [
        (EngineType.I1, 0.91), (EngineType.I2, 0.89), (EngineType.I3, 0.87),
        (EngineType.I4, 0.85), (EngineType.I5, 0.84), (EngineType.I6, 0.82),
        (EngineType.I8, 0.80), (EngineType.Boxer, 0.87), (EngineType.V6, 0.83),
        (EngineType.V8, 0.80), (EngineType.V10, 0.88), (EngineType.V12, 0.82),
        (EngineType.W12, 0.81), (EngineType.Rotary, 0.92)
    ];

    private static readonly (EngineType Type, double Factor)[] _torquePeakRpmFactors =
    [
        (EngineType.I1, 0.65), (EngineType.I2, 0.60), (EngineType.I3, 0.55),
        (EngineType.I4, 0.58), (EngineType.I5, 0.57), (EngineType.I6, 0.55),
        (EngineType.I8, 0.52), (EngineType.Boxer, 0.60), (EngineType.V6, 0.52),
        (EngineType.V8, 0.50), (EngineType.V10, 0.62), (EngineType.V12, 0.53),
        (EngineType.W12, 0.50), (EngineType.Rotary, 0.70)
    ];

    private static int EstimatePeakRPM(EngineType engineType, int maxRPM, (EngineType Type, double Factor)[] table)
    {
        for (int i = 0; i < table.Length; i++)
        {
            if (table[i].Type == engineType)
                return (int)Math.Round(maxRPM * table[i].Factor / 100.0) * 100;
        }
        return (int)Math.Round(maxRPM * 0.85 / 100.0) * 100;
    }

    // ── Transmission ───────────────────────────────────────────────────────

    private DriveType _driveType = DriveType.RWD;
    public DriveType DriveType
    {
        get => _driveType;
        set { Set(ref _driveType, value); }
    }

    private int _gearCount = 6;
    public int GearCount
    {
        get => _gearCount;
        set { Set(ref _gearCount, value); }
    }

    public int MaxAvailableGearCount { get; set; } = 10;

    private bool _allowGearCalculation = true;
    public bool AllowGearCalculation
    {
        get => _allowGearCalculation;
        set { Set(ref _allowGearCalculation, value); }
    }

    private bool _onlyFinalDriveCalculation = false;
    public bool OnlyFinalDriveCalculation
    {
        get => _onlyFinalDriveCalculation;
        set { Set(ref _onlyFinalDriveCalculation, value); }
    }

    // ── Tires (stock from DB) ──────────────────────────────────────────────

    public int FrontTireWidth { get; set; }
    public int FrontTireProfile { get; set; }
    public int RearTireWidth { get; set; }
    public int RearTireProfile { get; set; }
    public int FrontRimDiameter { get; set; }
    public int RearRimDiameter { get; set; }
    public int StockFrontTireProfile { get; set; }
    public int StockRearTireProfile { get; set; }

    // ── Geometry ───────────────────────────────────────────────────────────

    public double Wheelbase { get; set; }
    public double FrontTrack { get; set; }
    public double RearTrack { get; set; }

    // ── Body drag ──────────────────────────────────────────────────────────

    private double _cd;
    public double Cd
    {
        get => _cd;
        set { Set(ref _cd, value); OnPropertyChanged(nameof(MaxSpeedKmh)); }
    }

    private double _frontalAreaM2;
    public double FrontalAreaM2
    {
        get => _frontalAreaM2;
        set { Set(ref _frontalAreaM2, value); OnPropertyChanged(nameof(MaxSpeedKmh)); }
    }

    [JsonIgnore]
    public double CdABodyEstimate
    {
        get
        {
            if (Cd > 0 && FrontalAreaM2 > 0)
                return Cd * FrontalAreaM2;
            double avgProfile = (FrontTireProfile + RearTireProfile) / 2.0;
            double bodyFactor = Math.Clamp(1.0 + Math.Max(0, (avgProfile - 45.0) / 20.0) * 2.0, 1.0, 3.5);
            return Math.Clamp((0.50 + TotalMass / 2500.0) * bodyFactor, 0.30, 2.00);
        }
    }

    [JsonIgnore]
    public double MaxSpeedKmh
    {
        get
        {
            double powerWatts = PowerHP * PhysicsConstants.HpToWatt;
            double vMaxMs = Math.Pow(powerWatts / (0.5 * PhysicsConstants.AirDensity * CdABodyEstimate), 1.0 / 3.0);
            return Math.Round(Math.Clamp(vMaxMs * PhysicsConstants.MsToKmh, 60.0, MaxSpeedClampKmh));
        }
        set { /* computed */ }
    }

    // ── Power/Torque (set by PowerCalculator) ──────────────────────────────

    private double _powerHP = 300;
    public double PowerHP
    {
        get => _powerHP;
        set { Set(ref _powerHP, value); OnPropertyChanged(nameof(MaxSpeedKmh)); }
    }

    private double _torqueNm = 400;
    public double TorqueNm
    {
        get => _torqueNm;
        set { Set(ref _torqueNm, value); }
    }

    private int _maxRPM = 7000;
    public int MaxRPM
    {
        get => _maxRPM;
        set { Set(ref _maxRPM, value); OnPropertyChanged(nameof(PowerPeakRPM)); OnPropertyChanged(nameof(TorquePeakRPM)); }
    }

    // True when power/torque are an ESTIMATE rather than dyno-anchored game data — set by
    // PowerCalculator for the ~30 race/rally swap engines whose donor car isn't in the game
    // DB, so there's no exact stock figure to anchor to. The UI shows a "≈" marker.
    private bool _powerIsEstimated;
    [JsonIgnore]
    public bool PowerIsEstimated
    {
        get => _powerIsEstimated;
        set { Set(ref _powerIsEstimated, value); }
    }

    // ── Computed tire dimensions (for calculators) ─────────────────────────

    [JsonIgnore]
    public double FrontWheelDiameterInch => FrontRimDiameter + 2.0 * FrontTireWidth * FrontTireProfile / 100.0 / PhysicsConstants.MmPerInch;
    [JsonIgnore]
    public double RearWheelDiameterInch => RearRimDiameter + 2.0 * RearTireWidth * RearTireProfile / 100.0 / PhysicsConstants.MmPerInch;
    [JsonIgnore]
    public double DrivenWheelDiameterInch => DriveType switch
    {
        DriveType.FWD => FrontWheelDiameterInch,
        _ => RearWheelDiameterInch
    };

    // ── Legacy enum mappings (set by MainViewModel.MapPartIdsToEnums) ────────
    private TireType _tireType = TireType.Sport;
    public TireType TireType
    {
        get => _tireType;
        set { Set(ref _tireType, value); }
    }
    private SuspensionUpgrade _suspensionUpgrade = SuspensionUpgrade.Sport;
    public SuspensionUpgrade SuspensionUpgrade
    {
        get => _suspensionUpgrade;
        set { Set(ref _suspensionUpgrade, value); OnPropertyChanged(nameof(SuspensionAllowsAdvancedTuning)); }
    }
    public DifferentialUpgrade DifferentialUpgrade { get; set; }
    public BrakesUpgrade BrakesUpgrade { get; set; }

    // ── Legacy booleans (set by MainViewModel on part change) ────────────────
    public bool HasFrontAero { get; set; }
    public bool HasRearAero { get; set; }
    public bool HasFrontARB { get; set; }
    public bool HasRearARB { get; set; }
    public bool SuspensionAllowsAdvancedTuning => SuspensionUpgrade is
        SuspensionUpgrade.Race or SuspensionUpgrade.Rally or SuspensionUpgrade.Drift or SuspensionUpgrade.Offroad;

    // ── Legacy part IDs (for backward-compat tests) ─────────────────────────
    public int? RearWingPartId { get; set; }
    public int? ArbFrontPartId { get; set; }
    public int? ArbRearPartId { get; set; }

    // ── Legacy computed properties ────────────────────────────────────────────
    [JsonIgnore]
    public bool ShowAntiLag => AspirationType.HasValue && (AspirationType.Value == Models.AspirationType.SingleTurbo || AspirationType.Value == Models.AspirationType.TwinTurbo);
    [JsonIgnore]
    public bool IsElectricPowertrain => PowertrainType == PowertrainType.Electric;
    [JsonIgnore]
    public bool ShowAspiration => PowertrainType != PowertrainType.Electric;

    // ── Legacy weight ────────────────────────────────────────────────────────
    public double CurbWeightKg { get; set; }

}
