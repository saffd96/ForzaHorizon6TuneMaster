using System.Text.Json.Serialization;

namespace Forza_Horizon_6_Tune_Master.Models;

public class CarCard : NotifyBase
{
    public Guid Id { get; set; } = Guid.NewGuid();

    private string _name = "Новый профиль";
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
        set { Set(ref _totalMass, value); OnPropertyChanged(nameof(MaxSpeedKmh)); }
    }

    private double _weightDistributionFront = 50;
    public double WeightDistributionFront
    {
        get => _weightDistributionFront;
        set { Set(ref _weightDistributionFront, value); }
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

    private AspirationType _aspirationType = AspirationType.Natural;
    public AspirationType AspirationType
    {
        get => _aspirationType;
        set
        {
            Set(ref _aspirationType, value);
            OnPropertyChanged(nameof(ShowAntiLag));
            if (value != AspirationType.SingleTurbo)
                AntiLag = false;
        }
    }

    private bool _antiLag;
    public bool AntiLag
    {
        get => _antiLag;
        set { Set(ref _antiLag, value); }
    }

    [JsonIgnore]
    public bool ShowAntiLag => AspirationType == AspirationType.SingleTurbo;

    private PowertrainType _powertrainType = PowertrainType.ICE;
    public PowertrainType PowertrainType
    {
        get => _powertrainType;
        set
        {
            Set(ref _powertrainType, value);
            OnPropertyChanged(nameof(IsElectricPowertrain));
            OnPropertyChanged(nameof(ShowAspiration));
            OnPropertyChanged(nameof(PowerPeakRPM));
            OnPropertyChanged(nameof(TorquePeakRPM));
        }
    }

    [JsonIgnore]
    public bool IsElectricPowertrain => PowertrainType == PowertrainType.Electric;

    [JsonIgnore]
    public bool ShowAspiration => PowertrainType != PowertrainType.Electric
                               && EngineType    != EngineType.Electric;

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
                EngineType.I3       => 0.87,
                EngineType.I4       => 0.85,
                EngineType.I5       => 0.84,
                EngineType.I6       => 0.82,
                EngineType.Boxer    => 0.87,
                EngineType.V6       => 0.83,
                EngineType.V8       => 0.80,
                EngineType.V10      => 0.88,
                EngineType.V12      => 0.82,
                EngineType.Rotary   => 0.92,
                EngineType.Electric => 0.45,
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
            return Math.Max(500, (int)Math.Round(MaxRPM * EngineType switch
            {
                EngineType.I3       => 0.55,
                EngineType.I4       => 0.58,
                EngineType.I5       => 0.57,
                EngineType.I6       => 0.55,
                EngineType.Boxer    => 0.60,
                EngineType.V8       => 0.50,
                EngineType.V10      => 0.62,
                EngineType.V12      => 0.53,
                EngineType.Rotary   => 0.70,
                EngineType.Electric => 0.03,
                _                   => 0.57
            } / 100.0) * 100);
        }
    }

    // Transmission
    private DriveType _driveType = DriveType.RWD;
    public DriveType DriveType
    {
        get => _driveType;
        set { Set(ref _driveType, value); OnPropertyChanged(nameof(MaxSpeedKmh)); }
    }

    private int _gearCount = 6;
    public int GearCount
    {
        get => _gearCount;
        set { Set(ref _gearCount, value); }
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

    // Performance — computed from aerodynamic drag: v = (2P × η / (CdA × ρ))^(1/3)
    // CdA estimated from mass + tire profile (high profile = SUV body = more drag)
    // η: AWD=0.87 (two diffs), RWD/FWD=0.92
    [JsonIgnore]
    public double MaxSpeedKmh
    {
        get
        {
            double avgProfile  = (FrontTireProfile + RearTireProfile) / 2.0;
            double bodyFactor  = avgProfile > 55 ? 3.0 : 1.0;
            double cdA         = (0.40 + TotalMass / 3000.0) * bodyFactor;
            double eta         = DriveType == DriveType.AWD ? 0.87 : 0.92;
            double powerWatts  = PowerHP * 745.7 * eta;
            double vMaxMs      = Math.Pow(2.0 * powerWatts / (cdA * 1.225), 1.0 / 3.0);
            return Math.Round(Math.Clamp(vMaxMs * 3.6, 60.0, 600.0));
        }
        set { /* computed — no-op for backward compat with profiles and tests */ }
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

    // Upgrades
    private SuspensionUpgrade _suspensionUpgrade = SuspensionUpgrade.Sport;
    public SuspensionUpgrade SuspensionUpgrade
    {
        get => _suspensionUpgrade;
        set { Set(ref _suspensionUpgrade, value); }
    }

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
}
