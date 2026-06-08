using System.Text.Json.Serialization;
using Forza_Horizon_6_Tune_Master.Services;

namespace Forza_Horizon_6_Tune_Master.Models;

public class CarCard : NotifyBase
{
    private const double MaxSpeedClampKmh = 700.0;
    public Guid Id { get; set; } = Guid.NewGuid();

    private string _name = LocalizationService.Instance.T("ProfileDefaultName");
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

    // Mass
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
            // Don't mark as explicit when deserializing default 50 → preserves engine-position override
            if (_weightDistributionFront == 50 && value == 50 && !HasExplicitWeightDistribution)
                return;
            HasExplicitWeightDistribution = true;
            Set(ref _weightDistributionFront, value);
        }
    }

    // Engine
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

    private EngineType _engineType = EngineType.V6;
    public EngineType EngineType
    {
        get => _engineType;
        set { Set(ref _engineType, value); OnPropertyChanged(nameof(PowerPeakRPM)); OnPropertyChanged(nameof(TorquePeakRPM)); OnPropertyChanged(nameof(ShowAspiration)); }
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
            OnPropertyChanged(nameof(ShowAntiLag));
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

    [JsonIgnore]
    public bool ShowAntiLag => AspirationType is Models.AspirationType.SingleTurbo || AspirationType is Models.AspirationType.TwinTurbo;

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
            OnPropertyChanged(nameof(IsElectricPowertrain));
            OnPropertyChanged(nameof(ShowAspiration));
            OnPropertyChanged(nameof(PowerPeakRPM));
            OnPropertyChanged(nameof(TorquePeakRPM));
        }
    }

    [JsonIgnore]
    public bool IsElectricPowertrain => PowertrainType == PowertrainType.Electric;

    [JsonIgnore]
    public bool ShowAspiration => PowertrainType != PowertrainType.Electric;

    // Computed RPM peaks — derived from engine/powertrain type characteristics
    [JsonIgnore]
    public int PowerPeakRPM
    {
        get
        {
            if (PowertrainType == PowertrainType.Electric)
                return (int)Math.Round(MaxRPM * 0.45 / 100.0) * 100;
            return (int)Math.Round(MaxRPM * EngineType switch
            {
                EngineType.I1       => 0.91,  // одноцилиндровый — пик почти на максимуме
                EngineType.I2       => 0.89,  // двухцилиндровый — высокооборотистый
                EngineType.I3       => 0.87,
                EngineType.I4       => 0.85,
                EngineType.I5       => 0.84,
                EngineType.I6       => 0.82,
                EngineType.I8       => 0.80,  // рядная восьмёрка — аналог V8 по характеру
                EngineType.Boxer    => 0.87,
                EngineType.V6       => 0.83,
                EngineType.V8       => 0.80,
                EngineType.V10      => 0.88,
                EngineType.V12      => 0.82,
                EngineType.W12      => 0.81,  // W12 близок к V12
                EngineType.Rotary   => 0.92,
                _                   => 0.85
            } / 100.0) * 100;
        }
    }

    [JsonIgnore]
    public int TorquePeakRPM
    {
        get
        {
            if (PowertrainType == PowertrainType.Electric)
                return 0;
            int peak = (int)Math.Round(MaxRPM * EngineType switch
            {
                EngineType.I1       => 0.65,  // одноцилиндровый — момент тоже в верхах
                EngineType.I2       => 0.60,  // двухцилиндровый
                EngineType.I3       => 0.55,
                EngineType.I4       => 0.58,
                EngineType.I5       => 0.57,
                EngineType.I6       => 0.55,
                EngineType.I8       => 0.52,  // рядная восьмёрка — очень ровный момент
                EngineType.Boxer    => 0.60,
                EngineType.V6       => 0.52,
                EngineType.V8       => 0.50,
                EngineType.V10      => 0.62,
                EngineType.V12      => 0.53,
                EngineType.W12      => 0.50,  // W12 — широкий диапазон, момент низко
                EngineType.Rotary   => 0.70,
                _                   => 0.57
            } / 100.0) * 100;
            return Math.Max(500, peak);
        }
    }

    // Transmission
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

    // Tires
    private int _frontTireWidth = 235;
    public int FrontTireWidth
    {
        get => _frontTireWidth;
        set { Set(ref _frontTireWidth, value); OnPropertyChanged(nameof(FrontWheelDiameterInch)); }
    }

    private int _frontTireProfile = 40;
    public int FrontTireProfile
    {
        get => _frontTireProfile;
        set { Set(ref _frontTireProfile, value); OnPropertyChanged(nameof(FrontWheelDiameterInch)); OnPropertyChanged(nameof(MaxSpeedKmh)); }
    }

    private int _rearTireWidth = 265;
    public int RearTireWidth
    {
        get => _rearTireWidth;
        set { Set(ref _rearTireWidth, value); OnPropertyChanged(nameof(RearWheelDiameterInch)); }
    }

    private int _rearTireProfile = 35;
    public int RearTireProfile
    {
        get => _rearTireProfile;
        set { Set(ref _rearTireProfile, value); OnPropertyChanged(nameof(RearWheelDiameterInch)); OnPropertyChanged(nameof(MaxSpeedKmh)); }
    }

    private int _frontRimDiameter = 19;
    public int FrontRimDiameter
    {
        get => _frontRimDiameter;
        set { Set(ref _frontRimDiameter, value); OnPropertyChanged(nameof(FrontWheelDiameterInch)); }
    }

    private int _rearRimDiameter = 19;
    public int RearRimDiameter
    {
        get => _rearRimDiameter;
        set { Set(ref _rearRimDiameter, value); OnPropertyChanged(nameof(RearWheelDiameterInch)); }
    }

    private TireType _tireType = TireType.Sport;
    public TireType TireType
    {
        get => _tireType;
        set { Set(ref _tireType, value); }
    }

    // Geometry
    private double _wheelbase = 2700;
    public double Wheelbase
    {
        get => _wheelbase;
        set { Set(ref _wheelbase, value); }
    }

    private double _frontTrack = 1550;
    public double FrontTrack
    {
        get => _frontTrack;
        set { Set(ref _frontTrack, value); }
    }

    private double _rearTrack = 1570;
    public double RearTrack
    {
        get => _rearTrack;
        set { Set(ref _rearTrack, value); }
    }

    // Body drag inputs — override the estimated CdA when known.
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

    // v = (P / (0.5 × ρ × CdA_body))^(1/3)  — body drag only (display estimate).
    // Uses Cd × FrontalAreaM2 when both are set; otherwise estimates from mass/tire profile.
    // Wing-induced drag is added in TuneGeneratorService using computed r.AeroFront/Rear.
    // Forza uses engine HP directly at wheels — no drivetrain loss (confirmed: forums.forza.net).
    [JsonIgnore]
    public double CdABodyEstimate
    {
        get
        {
            if (Cd > 0 && FrontalAreaM2 > 0)
                return Cd * FrontalAreaM2;
            double avgProfile = (FrontTireProfile + RearTireProfile) / 2.0;
            double bodyFactor = Math.Clamp(1.0 + Math.Max(0, (avgProfile - 45.0) / 20.0) * 2.0, 1.0, 3.5);
            return (0.50 + TotalMass / 2500.0) * bodyFactor;
        }
    }

    [JsonIgnore]
    public double MaxSpeedKmh
    {
        get
        {
            double powerWatts = PowerHP * 745.7;
            double vMaxMs     = Math.Pow(powerWatts / (0.5 * 1.225 * CdABodyEstimate), 1.0 / 3.0);
            return Math.Round(Math.Clamp(vMaxMs * 3.6, 60.0, MaxSpeedClampKmh));
        }
        set { /* computed — no-op for backward compat with saved profiles */ }
    }

    // Aero
    private bool _hasFrontAero = false;
    public bool HasFrontAero
    {
        get => _hasFrontAero;
        set { Set(ref _hasFrontAero, value); }
    }

    private bool _hasRearAero = false;
    public bool HasRearAero
    {
        get => _hasRearAero;
        set { Set(ref _hasRearAero, value); }
    }

    // ARB (anti-roll bars)
    private bool _hasFrontARB;
    public bool HasFrontARB
    {
        get => _hasFrontARB;
        set { Set(ref _hasFrontARB, value); OnPropertyChanged(nameof(HasAnyARB)); }
    }

    private bool _hasRearARB;
    public bool HasRearARB
    {
        get => _hasRearARB;
        set { Set(ref _hasRearARB, value); OnPropertyChanged(nameof(HasAnyARB)); }
    }

    [JsonIgnore]
    public bool HasAnyARB => HasFrontARB || HasRearARB;

    // Upgrades
    private SuspensionUpgrade _suspensionUpgrade = SuspensionUpgrade.Sport;
    public SuspensionUpgrade SuspensionUpgrade
    {
        get => _suspensionUpgrade;
        set { Set(ref _suspensionUpgrade, value); OnPropertyChanged(nameof(SuspensionAllowsAdvancedTuning)); }
    }

    [JsonIgnore]
    public bool SuspensionAllowsAdvancedTuning => SuspensionUpgrade is
        SuspensionUpgrade.Race or SuspensionUpgrade.Rally or SuspensionUpgrade.Drift or SuspensionUpgrade.Offroad;

    private DifferentialUpgrade _differentialUpgrade = DifferentialUpgrade.Sport;
    public DifferentialUpgrade DifferentialUpgrade
    {
        get => _differentialUpgrade;
        set { Set(ref _differentialUpgrade, value); }
    }

    private BrakesUpgrade _brakesUpgrade = BrakesUpgrade.Sport;
    public BrakesUpgrade BrakesUpgrade
    {
        get => _brakesUpgrade;
        set { Set(ref _brakesUpgrade, value); }
    }

    // Computed tire dimensions
    [JsonIgnore]
    public double FrontWheelDiameterInch => FrontRimDiameter + 2.0 * FrontTireWidth * FrontTireProfile / 100.0 / 25.4;
    [JsonIgnore]
    public double RearWheelDiameterInch  => RearRimDiameter  + 2.0 * RearTireWidth  * RearTireProfile  / 100.0 / 25.4;
    [JsonIgnore]
    public double DrivenWheelDiameterInch => DriveType switch
    {
        DriveType.FWD => FrontWheelDiameterInch,
        _             => RearWheelDiameterInch
    };
}
