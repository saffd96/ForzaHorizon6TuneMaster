using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;
using Forza_Horizon_6_Tune_Master.Views;

namespace Forza_Horizon_6_Tune_Master.ViewModels;

public class MainViewModel : NotifyBase
{
    private readonly TuneGeneratorService _generator = new();
    private readonly StorageService _storage = new();
    private readonly ProfileService _profileService;
    private int _tuneGenerationCount;

    // ── Selected Parts (upgrade choices) ─────────────────────────────────────
    private SelectedParts _selectedParts = new();
    public SelectedParts SelectedParts
    {
        get => _selectedParts;
        set
        {
            if (value == null) return;
            if (_selectedParts != null)
            {
                _selectedParts.PartsChanged -= OnPartsChanged;
                _selectedParts.CarMassUpdated -= OnCarMassUpdated;
            }
            _selectedParts = value;
            _selectedParts.PartsChanged += OnPartsChanged;
            _selectedParts.CarMassUpdated += OnCarMassUpdated;
            OnPropertyChanged();
        }
    }

    private void OnCarMassUpdated(double totalMass, double? weightDistSum)
    {
        Car.TotalMass = totalMass;
    }

// ── Sub-ViewModels for part selection ────────────────────────────────────
public SwapsViewModel SwapsVM { get; } = new();
public EnginePartsViewModel EngineVM { get; } = new();
public MotorPartsViewModel MotorVM { get; } = new();
public SuspensionViewModel SuspensionVM { get; } = new();
public TransmissionViewModel TransmissionVM { get; } = new();
public TiresWheelsViewModel TiresWheelsVM { get; } = new();
public AeroVisualViewModel AeroVisualVM { get; } = new();

    // ── Models ──────────────────────────────────────────────────────────────
    private CarCard _car = new();
    public CarCard Car
    {
        get => _car;
        set
        {
            if (value == null) return;
            if (_car != null) _car.PropertyChanged -= OnModelChanged;
            if (_car != null) _car.PropertyChanged -= OnCarPropertyChanged;
            _car = value;
            _car.PropertyChanged += OnModelChanged;
            _car.PropertyChanged += OnCarPropertyChanged;
            OnPropertyChanged();
            NotifyCarDisplayProperties();
            OnPropertyChanged(nameof(HasCenterDiffBias));
            OnPropertyChanged(nameof(HasAWDFrontDiff));
            OnPropertyChanged(nameof(SelectedCarDisplayText));
            OnModelChanged(null, null!);
            if (!_isLoadingProfile)
                LoadSubViewModels(value);
            IsElectricCar = Fh6DatabaseService.Instance.GetCar(_car.CarDbId)?.AspirationTypeId == 8;
        }
    }

    private TrackInfo _track = new();
    public TrackInfo Track
    {
        get => _track;
        set
        {
            if (value == null) return;
            if (_track != null) _track.PropertyChanged -= OnModelChanged;
            _track = value;
            _track.PropertyChanged += OnModelChanged;
            OnPropertyChanged();
            OnModelChanged(null, null!);
        }
    }



    private int? _lastEngineSwapPartId;
    private int? _lastForcedInductionPartId;
    private int? _lastMotorSwapPartId;
    private int? _lastDrivetrainSwapPartId;

    private void OnPartsChanged()
    {
        if (_selectedParts.EngineSwapPartId != _lastEngineSwapPartId)
        {
            _lastEngineSwapPartId = _selectedParts.EngineSwapPartId;
            int engineId = _selectedParts.EngineSwapPartId != null
                ? Fh6DatabaseService.Instance.GetEngineSwapById(_selectedParts.EngineSwapPartId.Value)?.EngineID ?? Car.EngineDbId
                : Car.EngineDbId;
            Car.EngineDbId = engineId;
            // Reset forced induction (in the swaps module) first so the engine module's
            // intercooler resolves against the new engine's FI before it reloads.
            SwapsVM.ResetForcedInductionForEngine(engineId);
            EngineVM.ResetToStockForEngine(engineId);
        }

        if (_selectedParts.ForcedInductionPartId != _lastForcedInductionPartId)
        {
            _lastForcedInductionPartId = _selectedParts.ForcedInductionPartId;
            // FI type is chosen in the swaps module; rebuild the engine module's level
            // dropdown (and intercooler) from the DB for the new forced induction.
            EngineVM.ReloadForcedInductionLevels();
            EngineVM.ReloadIntercooler(_selectedParts.EngineId ?? Car.EngineDbId);
            UpdateAspirationTypeFromForcedInduction();
        }

        if (_selectedParts.MotorSwapPartId != _lastMotorSwapPartId)
        {
            _lastMotorSwapPartId = _selectedParts.MotorSwapPartId;
            int motorId = _selectedParts.MotorSwapPartId != null
                ? Fh6DatabaseService.Instance.GetMotorSwapById(_selectedParts.MotorSwapPartId.Value)?.MotorID ?? Car.MotorDbId
                : Car.MotorDbId;
            Car.MotorDbId = motorId;
            MotorVM.ResetToStockForMotor(motorId);
        }

        if (_selectedParts.DrivetrainSwapPartId != _lastDrivetrainSwapPartId)
        {
            _lastDrivetrainSwapPartId = _selectedParts.DrivetrainSwapPartId;
            var swap = _selectedParts.DrivetrainSwapPartId != null
                ? Fh6DatabaseService.Instance.GetDrivetrainSwapById(_selectedParts.DrivetrainSwapPartId.Value)
                : null;
            if (swap != null)
            {
                Car.DriveTypeID = swap.DriveTypeID;
                Car.DriveType = MapDriveType(swap.DriveTypeID);
                TransmissionVM.ResetToStockForDrivetrain(swap.DrivetrainID);
                OnPropertyChanged(nameof(HasCenterDiffBias));
                OnPropertyChanged(nameof(HasAWDFrontDiff));
            }
        }

        // Recalculate power/torque/RPM when any engine part changes.
        PowerCalculator.Calculate(Car, _selectedParts);
        // Update tire/rim dimensions from selected parts.
        UpdateTireAndWheelData();
        // Always update enums when ANY part changes (spring, tire, brakes, diff, aero, ARB, etc.)
        MapPartIdsToEnums(Car);
        Constraints.ApplyPhysicsBounds(Car, _selectedParts, Fh6DatabaseService.Instance);

        if (!IsAutoGenerate || _isGenerating) return;
        _lastInputChange = DateTime.Now;
        _ = DebounceGenerate();
    }

    private static Models.DriveType MapDriveType(int driveTypeId) => driveTypeId switch
    {
        0 => Models.DriveType.FWD,
        2 => Models.DriveType.AWD,
        _ => Models.DriveType.RWD
    };

    private TuneResult? _tuneResult;
    public TuneResult? TuneResult
    {
        get => _tuneResult;
        set
        {
            _tuneResult = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasResult));
            OnPropertyChanged(nameof(HasAWDFrontDiff));
            OnPropertyChanged(nameof(HasLaunchControl));
        }
    }
    public bool HasResult        => _tuneResult != null;
    public bool HasAWDFrontDiff  => Car.DriveType == Models.DriveType.AWD && _tuneResult?.CenterDiffBias.HasValue == true;
    public bool HasDiffDecel
    {
        get
        {
            var diff = TuningPhysicsContext.Differential(Car, _selectedParts, Fh6DatabaseService.Instance);
            return Math.Max(diff.RearLimitedSlipTorqueDecel, diff.FrontLimitedSlipTorqueDecel) > 0.01;
        }
    }
    public bool HasLaunchControl => _tuneResult?.LaunchControlRpm.HasValue == true;

    private bool _isElectricCar;
    public bool IsElectricCar
    {
        get => _isElectricCar;
        private set { _isElectricCar = value; OnPropertyChanged(); }
    }

    // ── Overlay mode ───────────────────────────────────────────────────────────
    private bool _isOverlayMode;
    public bool IsOverlayMode
    {
        get => _isOverlayMode;
        set { _isOverlayMode = value; OnPropertyChanged(); }
    }

    private double _overlayOpacity = 0.9;
    public double OverlayOpacity
    {
        get => _overlayOpacity;
        set { _overlayOpacity = value; OnPropertyChanged(); }
    }

    // ── Car database + specs ──────────────────────────────────────────────────

    // ── Car database + specs ──────────────────────────────────────────────────
    private readonly CarSpecController _carSpec = new();
    private bool _isLoadingProfile;

    public CarData? SelectedCar
    {
        get => _carSpec.SelectedCar;
        set
        {
            _carSpec.SelectCar(value, _car, _isLoadingProfile);
            if (!_isLoadingProfile)
            {
                TuneResult = null;
                LoadSubViewModels(_car);
                OnPropertyChanged(nameof(SelectedParts));
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedCarDisplayText));
            OnPropertyChanged(nameof(HasSelectedCar));
        }
    }
    public bool HasSelectedCar => _carSpec.HasSelectedCar;

    public string CarSearchText
    {
        get => _carSpec.CarSearchText;
        set
        {
            _carSpec.CarSearchText = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<CarData> FilteredCarDatabase => _carSpec.FilteredCarDatabase;

    public string SelectedCarDisplayText => _carSpec.SelectedCar?.DisplayName
        ?? $"{_car.Year} {_car.Make} {_car.Model}".Trim();

    // ── Unit system ──────────────────────────────────────────────────────────
    private bool _syncingUnitSystem;
    private UnitSystem _measurementSystem = UnitSystem.Metric;
    public UnitSystem MeasurementSystem
    {
        get => _measurementSystem;
        set
        {
            if (_measurementSystem == value) return;
            _measurementSystem = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(UseImperial));
            OnPropertyChanged(nameof(SpeedDisplay));
            OnPropertyChanged(nameof(MassDisplay));
            OnPropertyChanged(nameof(TorqueDisplay));
            OnPropertyChanged(nameof(WheelbaseDisplay));
            OnPropertyChanged(nameof(FrontTrackDisplay));
            OnPropertyChanged(nameof(RearTrackDisplay));
            OnPropertyChanged(nameof(SpeedFieldLabel));
            OnPropertyChanged(nameof(MassFieldLabel));
            OnPropertyChanged(nameof(TorqueFieldLabel));
            OnPropertyChanged(nameof(WheelbaseFieldLabel));
            OnPropertyChanged(nameof(FrontTrackFieldLabel));
            OnPropertyChanged(nameof(RearTrackFieldLabel));
            OnPropertyChanged(nameof(UnitToggleLabel));
            if (!_syncingUnitSystem)
            {
                _syncingUnitSystem = true;
                try { _selectedUnitSystemItem = UnitSystemOptions.FirstOrDefault(o => o.Value == value); OnPropertyChanged(nameof(SelectedUnitSystemItem)); }
                finally { _syncingUnitSystem = false; }
            }
            SaveUnitSettings();
        }
    }

    public bool UseImperial => _measurementSystem == UnitSystem.Imperial;

    // ── Unit display properties for CarCardView ──────────────────────────────

    private bool _syncingPowerUnit;
    private PowerUnit _powerUnit = PowerUnit.HP;
    public PowerUnit PowerUnit
    {
        get => _powerUnit;
        set
        {
            if (_powerUnit == value) return;
            _powerUnit = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PowerDisplay));
            OnPropertyChanged(nameof(PowerFieldLabel));
            OnPropertyChanged(nameof(PowerUnitToggleLabel));
            if (!_syncingPowerUnit)
            {
                _syncingPowerUnit = true;
                try { _selectedPowerUnitItem = PowerUnitOptions.FirstOrDefault(o => o.Value == value); OnPropertyChanged(nameof(SelectedPowerUnitItem)); }
                finally { _syncingPowerUnit = false; }
            }
            SaveUnitSettings();
        }
    }

    private bool _syncingSpringUnit;
    private SpringUnit _springUnit = SpringUnit.NMm;
    public SpringUnit SpringUnit
    {
        get => _springUnit;
        set
        {
            if (_springUnit == value) return;
            _springUnit = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SpringUnitToggleLabel));
            if (!_syncingSpringUnit)
            {
                _syncingSpringUnit = true;
                try { _selectedSpringUnitItem = SpringUnitOptions.FirstOrDefault(o => o.Value == value); OnPropertyChanged(nameof(SelectedSpringUnitItem)); }
                finally { _syncingSpringUnit = false; }
            }
            SaveUnitSettings();
        }
    }

    // ── Unit display properties for CarCardView ──────────────────────────────
    public double PowerDisplay => UnitConverter.PowerToDisplay(_car.PowerHP, _powerUnit);

    public double SpeedDisplay
    {
        get => UnitConverter.SpeedToDisplay(_car.MaxSpeedKmh, UseImperial);
        set { /* computed — read-only */ OnPropertyChanged(); }
    }

    public double MassDisplay
    {
        get => UnitConverter.MassToDisplay(_car.TotalMass, UseImperial);
        set
        {
            _car.TotalMass = UnitConverter.MassFromDisplay(value, UseImperial);
            OnPropertyChanged();
        }
    }

    public double TorqueDisplay => UnitConverter.TorqueToDisplay(_car.TorqueNm, UseImperial);

    public double WheelbaseDisplay
    {
        get => UnitConverter.LengthToDisplay(_car.Wheelbase, UseImperial);
        set
        {
            _car.Wheelbase = UnitConverter.LengthFromDisplay(value, UseImperial);
            OnPropertyChanged();
        }
    }

    public double FrontTrackDisplay
    {
        get => UnitConverter.LengthToDisplay(_car.FrontTrack, UseImperial);
        set
        {
            _car.FrontTrack = UnitConverter.LengthFromDisplay(value, UseImperial);
            OnPropertyChanged();
        }
    }

    public double RearTrackDisplay
    {
        get => UnitConverter.LengthToDisplay(_car.RearTrack, UseImperial);
        set
        {
            _car.RearTrack = UnitConverter.LengthFromDisplay(value, UseImperial);
            OnPropertyChanged();
        }
    }

    // ── Legacy Constraints (for test compatibility) ───────────────────────────
    public TuningConstraints Constraints { get; } = new();

    public double TirePressureFrontMinDisplay
    {
        get => UnitConverter.TirePressureToDisplay(Constraints.TirePressureFrontMin, UseImperial);
        set { Constraints.TirePressureFrontMin = UnitConverter.TirePressureFromDisplay(value, UseImperial); OnPropertyChanged(); }
    }

    public double RideHeightFrontMinDisplay
    {
        get => UseImperial ? Math.Round(Constraints.RideHeightFrontMin / 25.4, 1) : Constraints.RideHeightFrontMin;
        set { Constraints.RideHeightFrontMin = UseImperial ? Math.Round(value * 25.4, 0) : value; OnPropertyChanged(); }
    }

    public double SpringFrontMinDisplay
    {
        get
        {
            var nmm = Constraints.SpringFrontMin;
            return _springUnit switch
            {
                SpringUnit.KgfMm => Math.Round(nmm / 9.807 * 10.0, 1),
                SpringUnit.LbsIn => Math.Round(nmm / 0.175127, 1),
                _ => nmm
            };
        }
        set
        {
            Constraints.SpringFrontMin = _springUnit switch
            {
                SpringUnit.KgfMm => value / 10.0 * 9.807,
                SpringUnit.LbsIn => value * 0.175127,
                _ => value
            };
            OnPropertyChanged();
        }
    }

    // ── Unit-aware labels for CarCardView ────────────────────────────────────
    private string T(string key) => LocalizationService.Instance.T(key);

    public string PowerFieldLabel => _powerUnit switch
    {
        PowerUnit.KW => T("FieldPower_KW"),
        PowerUnit.PS => T("FieldPower_PS"),
        _            => T("FieldPower_HP")
    };
    public string SpeedFieldLabel => _measurementSystem == UnitSystem.Imperial
        ? T("FieldSpeed_Imperial") : T("FieldSpeed_Metric");
    public string MassFieldLabel  => _measurementSystem == UnitSystem.Imperial
        ? T("FieldMass_Imperial") : T("FieldMass_Metric");
    public string TorqueFieldLabel => _measurementSystem == UnitSystem.Imperial
        ? T("FieldTorque_Imperial") : T("FieldTorque_Metric");
    public string WheelbaseFieldLabel => _measurementSystem == UnitSystem.Imperial
        ? T("FieldWheelbase_Imperial") : T("FieldWheelbase_Metric");
    public string FrontTrackFieldLabel => _measurementSystem == UnitSystem.Imperial
        ? T("FieldFrontTrack_Imperial") : T("FieldFrontTrack_Metric");
    public string RearTrackFieldLabel => _measurementSystem == UnitSystem.Imperial
        ? T("FieldRearTrack_Imperial") : T("FieldRearTrack_Metric");
    public string MaxRPMFieldLabel => Car.PowertrainType == PowertrainType.Electric
        ? T("FieldMaxRPM_Electric") : T("FieldMaxRPM_ICE");

    // ── Unit option lists + selected items for dropdowns ─────────────────────
    private List<UnitSystemOption> _unitSystemOptions = new();
    public List<UnitSystemOption> UnitSystemOptions
    {
        get => _unitSystemOptions;
        set { _unitSystemOptions = value; OnPropertyChanged(); }
    }

    private List<PowerUnitOption> _powerUnitOptions = new();
    public List<PowerUnitOption> PowerUnitOptions
    {
        get => _powerUnitOptions;
        set { _powerUnitOptions = value; OnPropertyChanged(); }
    }

    private List<SpringUnitOption> _springUnitOptions = new();
    public List<SpringUnitOption> SpringUnitOptions
    {
        get => _springUnitOptions;
        set { _springUnitOptions = value; OnPropertyChanged(); }
    }

    private UnitSystemOption? _selectedUnitSystemItem;
    public UnitSystemOption? SelectedUnitSystemItem
    {
        get => _selectedUnitSystemItem;
        set
        {
            if (value == null || _selectedUnitSystemItem?.Value == value.Value) return;
            if (_syncingUnitSystem) return;
            _syncingUnitSystem = true;
            try
            {
                _selectedUnitSystemItem = value;
                OnPropertyChanged();
                _measurementSystem = value.Value;
                OnPropertyChanged(nameof(MeasurementSystem));
                OnPropertyChanged(nameof(UseImperial));
                OnPropertyChanged(nameof(SpeedDisplay));
                OnPropertyChanged(nameof(MassDisplay));
                OnPropertyChanged(nameof(TorqueDisplay));
                OnPropertyChanged(nameof(WheelbaseDisplay));
                OnPropertyChanged(nameof(FrontTrackDisplay));
                OnPropertyChanged(nameof(RearTrackDisplay));
                OnPropertyChanged(nameof(SpeedFieldLabel));
                OnPropertyChanged(nameof(MassFieldLabel));
                OnPropertyChanged(nameof(TorqueFieldLabel));
                OnPropertyChanged(nameof(WheelbaseFieldLabel));
                OnPropertyChanged(nameof(FrontTrackFieldLabel));
                OnPropertyChanged(nameof(RearTrackFieldLabel));
                OnPropertyChanged(nameof(UnitToggleLabel));
                SaveUnitSettings();
            }
            finally { _syncingUnitSystem = false; }
        }
    }

    private PowerUnitOption? _selectedPowerUnitItem;
    public PowerUnitOption? SelectedPowerUnitItem
    {
        get => _selectedPowerUnitItem;
        set
        {
            if (value == null || _selectedPowerUnitItem?.Value == value.Value) return;
            if (_syncingPowerUnit) return;
            _syncingPowerUnit = true;
            try
            {
                _selectedPowerUnitItem = value;
                OnPropertyChanged();
                _powerUnit = value.Value;
                OnPropertyChanged(nameof(PowerUnit));
                OnPropertyChanged(nameof(PowerDisplay));
                OnPropertyChanged(nameof(PowerFieldLabel));
                OnPropertyChanged(nameof(PowerUnitToggleLabel));
                SaveUnitSettings();
            }
            finally { _syncingPowerUnit = false; }
        }
    }

    private SpringUnitOption? _selectedSpringUnitItem;
    public SpringUnitOption? SelectedSpringUnitItem
    {
        get => _selectedSpringUnitItem;
        set
        {
            if (value == null || _selectedSpringUnitItem?.Value == value.Value) return;
            if (_syncingSpringUnit) return;
            _syncingSpringUnit = true;
            try
            {
                _selectedSpringUnitItem = value;
                OnPropertyChanged();
                _springUnit = value.Value;
                OnPropertyChanged(nameof(SpringUnit));
                OnPropertyChanged(nameof(SpringUnitToggleLabel));
                SaveUnitSettings();
            }
            finally { _syncingSpringUnit = false; }
        }
    }

    // ── Unit toggle labels ───────────────────────────────────────────────────
    public string UnitToggleLabel => _measurementSystem == UnitSystem.Imperial
        ? T("UnitImperialLabel") : T("UnitMetricLabel");

    public string PowerUnitToggleLabel => _powerUnit switch
    {
        PowerUnit.PS => $"⚡ {T("FieldPower_PS")}",
        PowerUnit.KW => $"⚡ {T("FieldPower_KW")}",
        _            => $"⚡ {T("FieldPower_HP")}"
    };

    public string SpringUnitToggleLabel => _springUnit switch
    {
        SpringUnit.NMm   => T("SpringUnitNmmLabel"),
        SpringUnit.LbsIn => T("SpringUnitLbsInLabel"),
        _                => T("SpringUnitKgfMmLabel")
    };

    // ── Unit toggle commands ─────────────────────────────────────────────────
    public RelayCommand ToggleUnitsCommand      { get; }
    public RelayCommand TogglePowerUnitCommand  { get; }
    public RelayCommand ToggleSpringUnitCommand { get; }
    public RelayCommand SetLanguageCommand { get; }

    // ── Language ────────────────────────────────────────────────────────────
    private bool _syncingLanguage;
    private List<LanguageOption> _languageOptions = new()
    {
        new() { Code = "ru" },
        new() { Code = "en" },
    };
    public List<LanguageOption> LanguageOptions
    {
        get => _languageOptions;
        set { _languageOptions = value; OnPropertyChanged(); }
    }

    private LanguageOption? _selectedLanguageItem;
    public LanguageOption? SelectedLanguageItem
    {
        get => _selectedLanguageItem;
        set
        {
            if (value == null || _selectedLanguageItem?.Code == value.Code) return;
            if (_syncingLanguage) return;
            _syncingLanguage = true;
            try
            {
                var prev = _selectedLanguageItem;
                _selectedLanguageItem = value;
                OnPropertyChanged();
                if (!LocalizationService.Instance.SetLanguage(value.Code))
                {
                    _selectedLanguageItem = prev;
                    OnPropertyChanged();
                    StatusMessage = string.Format(LocalizationService.Instance.T("LanguageLoadError"), value.Code);
                }
            }
            finally { _syncingLanguage = false; }
        }
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "Item")
        {
            _ = Application.Current?.Dispatcher.BeginInvoke(InvalidateAllLanguageDependent);
        }
    }

    private void RefreshLanguageLabels()
    {
        var t = LocalizationService.Instance;
        foreach (var o in _languageOptions)
        {
            var langKey = $"Language{char.ToUpperInvariant(o.Code[0])}{o.Code[1..]}";
            o.Label = $"{o.Code} {t.T(langKey)}";
        }
    }

    private void InvalidateAllLanguageDependent()
    {
        OnPropertyChanged(nameof(PowerFieldLabel));
        OnPropertyChanged(nameof(SpeedFieldLabel));
        OnPropertyChanged(nameof(MassFieldLabel));
        OnPropertyChanged(nameof(TorqueFieldLabel));
        OnPropertyChanged(nameof(WheelbaseFieldLabel));
        OnPropertyChanged(nameof(FrontTrackFieldLabel));
        OnPropertyChanged(nameof(RearTrackFieldLabel));
        OnPropertyChanged(nameof(MaxRPMFieldLabel));
        OnPropertyChanged(nameof(UnitToggleLabel));
        OnPropertyChanged(nameof(PowerUnitToggleLabel));
        OnPropertyChanged(nameof(SpringUnitToggleLabel));
        // Force cached status/busy messages to re-localize by resetting to empty
        _statusMessage = "";
        _busyMessage = "";
        OnPropertyChanged(nameof(StatusMessage));
        OnPropertyChanged(nameof(BusyMessage));
        OnPropertyChanged(nameof(SelectedCarDisplayText));
        RefreshUnitOptionLabels();
        RefreshLanguageLabels();
        // Force all ComboBox selection boxes to re-render with new labels
        // Re-assign unit items to force ComboBox re-render with new language labels.
        var us = _selectedUnitSystemItem;
        var pp = _selectedPowerUnitItem;
        var ss = _selectedSpringUnitItem;
        var ll = _selectedLanguageItem;
        _selectedUnitSystemItem = null!;
        _selectedPowerUnitItem = null!;
        _selectedSpringUnitItem = null!;
        _selectedLanguageItem = null!;
        OnPropertyChanged(nameof(SelectedUnitSystemItem));
        OnPropertyChanged(nameof(SelectedPowerUnitItem));
        OnPropertyChanged(nameof(SelectedSpringUnitItem));
        OnPropertyChanged(nameof(SelectedLanguageItem));
        _selectedUnitSystemItem = us;
        _selectedPowerUnitItem = pp;
        _selectedSpringUnitItem = ss;
        _selectedLanguageItem = ll;
        OnPropertyChanged(nameof(SelectedUnitSystemItem));
        OnPropertyChanged(nameof(SelectedPowerUnitItem));
        OnPropertyChanged(nameof(SelectedSpringUnitItem));
        OnPropertyChanged(nameof(SelectedLanguageItem));
        // Force all TuneResult-bound converters to re-evaluate with new language.
        // Use a temporary non-null TuneResult to avoid null references in converters.
        var tr = _tuneResult;
        _tuneResult = new TuneResult();
        OnPropertyChanged(nameof(TuneResult));
        _tuneResult = tr;
        OnPropertyChanged(nameof(TuneResult));
    }

    private void RefreshUnitOptionLabels()
    {
        var t = LocalizationService.Instance;

        if (_unitSystemOptions != null)
        {
            foreach (var o in _unitSystemOptions)
                o.Label = o.Value == UnitSystem.Metric ? t.T("UnitMetricLabel") : t.T("UnitImperialLabel");
        }
        if (_powerUnitOptions != null)
        {
            foreach (var o in _powerUnitOptions)
                o.Label = $"⚡ {t.T(o.Value == PowerUnit.HP ? "FieldPower_HP" : o.Value == PowerUnit.PS ? "FieldPower_PS" : "FieldPower_KW")}";
        }
        if (_springUnitOptions != null)
        {
            foreach (var o in _springUnitOptions)
                o.Label = o.Value == SpringUnit.KgfMm ? t.T("SpringUnitKgfMmLabel")
                        : o.Value == SpringUnit.NMm ? t.T("SpringUnitNmmLabel")
                        : t.T("SpringUnitLbsInLabel");
        }
    }

    // ── Profile management ──────────────────────────────────────────────────
    private ObservableCollection<string> _profiles = new();
    public ObservableCollection<string> Profiles { get => _profiles; set { _profiles = value; OnPropertyChanged(); } }

    private string? _selectedProfile;
    public string? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            _selectedProfile = value;
            OnPropertyChanged();
            LoadCommand.Raise();
            DeleteProfileCommand.Raise();
            ProfileSearchText = value ?? "";
            if (value != null) LoadProfile();
        }
    }

    private string _profileSearchText = "";
    public string ProfileSearchText
    {
        get => _profileSearchText;
        set
        {
            if (_profileSearchText == value) return;
            _profileSearchText = value;
            OnPropertyChanged();
            ApplyProfileFilter();
        }
    }

    private readonly ObservableCollection<string> _filteredProfiles = new();
    public ObservableCollection<string> FilteredProfiles => _filteredProfiles;

    private void ApplyProfileFilter()
    {
        var query = string.IsNullOrWhiteSpace(_profileSearchText)
            ? null
            : _profileSearchText.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        _filteredProfiles.Clear();
        foreach (var profile in _profiles)
        {
            if (query == null || query.All(q => profile.ToLowerInvariant().Contains(q)))
                _filteredProfiles.Add(profile);
        }
    }

    // AWD centre diff visibility
    public bool HasCenterDiffBias => Car.DriveType == Models.DriveType.AWD;


    // ── Auto-generate ───────────────────────────────────────────────────────
    public bool IsAutoGenerate => true;

    private bool _isGenerating;
    public bool IsGenerating
    {
        get => _isGenerating;
        set
        {
            _isGenerating = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsBusy));
        }
    }

    public bool IsLoadingCars
    {
        get => _carSpec.IsLoadingCars;
        set => _carSpec.IsLoadingCars = value;
    }

    public bool IsBusy => _isGenerating || _carSpec.IsLoadingCars;

    private string _busyMessage = "";
    public string BusyMessage
    {
        get => _busyMessage;
        set { _busyMessage = value; OnPropertyChanged(); }
    }

    private int _pendingGenerationId;
    private DateTime _lastInputChange = DateTime.MinValue;
    private CancellationTokenSource? _debounceCts;

    private void SubscribeModelChanges()
    {
        _car.PropertyChanged += OnModelChanged;
        _car.PropertyChanged += OnCarPropertyChanged;
        _track.PropertyChanged += OnModelChanged;
    }

    private void OnCarPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CarCard.PowertrainType))
            OnPropertyChanged(nameof(MaxRPMFieldLabel));
        if (e.PropertyName == nameof(CarCard.MaxSpeedKmh))
            OnPropertyChanged(nameof(SpeedDisplay));
        if (e.PropertyName == nameof(CarCard.DriveType))
        {
            OnPropertyChanged(nameof(HasCenterDiffBias));
            OnPropertyChanged(nameof(HasAWDFrontDiff));
        }
        if (e.PropertyName == nameof(CarCard.DifferentialUpgrade))
            OnPropertyChanged(nameof(HasDiffDecel));
        if (e.PropertyName is nameof(CarCard.Make) or nameof(CarCard.Model) or nameof(CarCard.Year))
            OnPropertyChanged(nameof(SelectedCarDisplayText));
        if (e.PropertyName == nameof(CarCard.TotalMass))
        {
            OnPropertyChanged(nameof(MassDisplay));
            OnPropertyChanged(nameof(SpeedDisplay));
        }
        if (e.PropertyName == nameof(CarCard.PowerHP))
            OnPropertyChanged(nameof(PowerDisplay));
        if (e.PropertyName == nameof(CarCard.TorqueNm))
            OnPropertyChanged(nameof(TorqueDisplay));
        if (e.PropertyName == nameof(CarCard.Wheelbase))
            OnPropertyChanged(nameof(WheelbaseDisplay));
        if (e.PropertyName == nameof(CarCard.FrontTrack))
            OnPropertyChanged(nameof(FrontTrackDisplay));
        if (e.PropertyName == nameof(CarCard.RearTrack))
            OnPropertyChanged(nameof(RearTrackDisplay));
    }

    private void OnModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!IsAutoGenerate || _isGenerating) return;
        _lastInputChange = DateTime.Now;
        _ = DebounceGenerate();
    }

    private async Task DebounceGenerate()
    {
        var oldCts = _debounceCts;
        _debounceCts = new CancellationTokenSource();
        oldCts?.Cancel();
        oldCts?.Dispose();
        int myId = ++_pendingGenerationId;
        try
        {
            await Task.Delay(400, _debounceCts.Token).ConfigureAwait(true);
            if (!IsAutoGenerate || myId != _pendingGenerationId) return;
            BusyMessage = T("BusyGenerating");
            IsGenerating = true;
            GenerateTune();
        }
        catch (OperationCanceledException) { /* cancelled */ }
        finally { IsGenerating = false; }
    }

    // ── Status ───────────────────────────────────────────────────────────────
    private string _statusMessage = "";
    public string StatusMessage
    {
        get => string.IsNullOrEmpty(_statusMessage) ? T("StatusBarDefault") : _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    // ── Commands ─────────────────────────────────────────────────────────────
    public RelayCommand GenerateCommand       { get; }
    public RelayCommand SaveCommand           { get; }
    public RelayCommand LoadCommand           { get; }
    public RelayCommand DeleteProfileCommand  { get; }
    public RelayCommand DeleteAllProfilesCommand { get; }
    public RelayCommand NewProfileCommand     { get; }
    public RelayCommand ClearCarSelectionCommand { get; }

    public MainViewModel()
    {
        _profileService = new ProfileService(_storage);

        // Wire CarSpecController callbacks
        _carSpec.NotifyCarDisplayProperties = NotifyCarDisplayProperties;
        _carSpec.NotifyCarSelectionProperties = () =>
        {
            OnPropertyChanged(nameof(SelectedCar));
            OnPropertyChanged(nameof(HasSelectedCar));
            OnPropertyChanged(nameof(SelectedCarDisplayText));
            OnPropertyChanged(nameof(CarSearchText));
        };
        _carSpec.SetStatusMessage = msg => StatusMessage = msg;
        _carSpec.BusyFlagsChanged = () =>
        {
            OnPropertyChanged(nameof(IsLoadingCars));
            OnPropertyChanged(nameof(IsBusy));
        };

        GenerateCommand        = new RelayCommand(GenerateTune);
        SaveCommand            = new RelayCommand(SaveProfile);
        LoadCommand            = new RelayCommand(LoadProfile, () => SelectedProfile != null);
        DeleteProfileCommand   = new RelayCommand(DeleteProfile, () => SelectedProfile != null);
        DeleteAllProfilesCommand = new RelayCommand(DeleteAllProfiles);
        NewProfileCommand      = new RelayCommand(NewProfile);
        ClearCarSelectionCommand = new RelayCommand(() => _carSpec.ClearCarSelection(_car));

        ToggleUnitsCommand      = new RelayCommand(DoToggleUnits);
        TogglePowerUnitCommand  = new RelayCommand(DoTogglePowerUnit);
        ToggleSpringUnitCommand = new RelayCommand(DoToggleSpringUnit);
        SetLanguageCommand      = new RelayCommand(() => { }); // no-op: switching handled by SelectedLanguageItem setter

        _car.PropertyChanged += OnModelChanged;
        _car.PropertyChanged += OnCarPropertyChanged;
        _track.PropertyChanged += OnModelChanged;

        var svc = LocalizationService.Instance;
        svc.PropertyChanged += OnLanguageChanged;
        RefreshLanguageLabels();
        var currentLang = LanguageOptions.FirstOrDefault(l => l.Code == svc.CurrentLanguage)
            ?? LanguageOptions[0];
        _selectedLanguageItem = currentLang;
        OnPropertyChanged(nameof(SelectedLanguageItem));

        var tInit = LocalizationService.Instance;
        _unitSystemOptions = new()
        {
            new() { Value = UnitSystem.Metric, Label = tInit.T("UnitMetricLabel") },
            new() { Value = UnitSystem.Imperial, Label = tInit.T("UnitImperialLabel") },
        };
        _powerUnitOptions = new()
        {
            new() { Value = PowerUnit.HP, Label = $"⚡ {tInit.T("FieldPower_HP")}" },
            new() { Value = PowerUnit.PS, Label = $"⚡ {tInit.T("FieldPower_PS")}" },
            new() { Value = PowerUnit.KW, Label = $"⚡ {tInit.T("FieldPower_KW")}" },
        };
        _springUnitOptions = new()
        {
            new() { Value = SpringUnit.KgfMm, Label = tInit.T("SpringUnitKgfMmLabel") },
            new() { Value = SpringUnit.NMm, Label = tInit.T("SpringUnitNmmLabel") },
            new() { Value = SpringUnit.LbsIn, Label = tInit.T("SpringUnitLbsInLabel") },
        };
        OnPropertyChanged(nameof(UnitSystemOptions));
        OnPropertyChanged(nameof(PowerUnitOptions));
        OnPropertyChanged(nameof(SpringUnitOptions));

        _selectedUnitSystemItem  = UnitSystemOptions[0];
        _selectedPowerUnitItem   = PowerUnitOptions[0];
        _selectedSpringUnitItem  = SpringUnitOptions[1];
        OnPropertyChanged(nameof(SelectedUnitSystemItem));
        OnPropertyChanged(nameof(SelectedPowerUnitItem));
        OnPropertyChanged(nameof(SelectedSpringUnitItem));

        var (savedMs, savedPu, savedSu) = LocalizationService.Instance.LoadUnitSettings();
        if (savedMs != null && Enum.TryParse<UnitSystem>(savedMs, out var ms)) MeasurementSystem = ms;
        if (savedPu != null && Enum.TryParse<PowerUnit>(savedPu, out var pu)) PowerUnit = pu;
        // Spring unit always starts at N/mm to match the in-game tuning screen (e.g. 43-214.8),
        // so a previously persisted kgf/mm no longer overrides it. Users can still switch in-session.
        _ = savedSu;

        RefreshProfiles();
        RecalculateOutdatedProfiles();
        InvalidateAllLanguageDependent();
        _ = LoadCarDatabaseAsync();
    }

    private void LoadSubViewModels(CarCard car)
    {
        var parts = _isLoadingProfile ? _selectedParts : new SelectedParts();
        if (parts == null) parts = new SelectedParts();
        parts.SetCarData(car, !_isLoadingProfile);
        if (_isLoadingProfile)
            parts.ResolveEngineAndMotorIds();
        bool isElectric = Fh6DatabaseService.Instance.GetCar(car.CarDbId)?.AspirationTypeId == 8;
        IsElectricCar = isElectric;
        if (isElectric)
            ClearEnginePartIds(parts);
        SwapsVM.LoadForCar(car, parts);
    EngineVM.LoadForCar(car, parts);
        MotorVM.LoadForCar(car, parts);
        car.MotorDbId = parts.MotorId ?? 0;
    SuspensionVM.LoadForCar(car, parts);
    TransmissionVM.LoadForCar(car, parts);
    TiresWheelsVM.LoadForCar(car, parts);
    AeroVisualVM.LoadForCar(car, parts);
        SelectedParts = parts;
        // SetCarData/ResetAllToStock above fire CarMassUpdated before this VM subscribes
        // (subscription happens in the SelectedParts setter), so push the car's actual
        // mass now — otherwise TotalMass would stay at its 1400 kg default.
        Car.TotalMass = parts.ComputeTotalMass();
        _lastEngineSwapPartId = parts.EngineSwapPartId;
        _lastForcedInductionPartId = parts.ForcedInductionPartId;
        _lastMotorSwapPartId = parts.MotorSwapPartId;
        _lastDrivetrainSwapPartId = parts.DrivetrainSwapPartId;
        MapDbToOldEnums(car);
        Constraints.ApplyPhysicsBounds(car, parts, Fh6DatabaseService.Instance);
    }

    private static void ClearEnginePartIds(SelectedParts parts)
    {
        parts.EngineSwapPartId = null;
        parts.ForcedInductionPartId = null;
        parts.CamshaftPartId = null;
        parts.DisplacementPartId = null;
        parts.ValvesPartId = null;
        parts.PistonsPartId = null;
        parts.FuelSystemPartId = null;
        parts.IgnitionPartId = null;
        parts.ExhaustPartId = null;
        parts.IntakePartId = null;
        parts.FlywheelPartId = null;
        parts.ManifoldPartId = null;
        parts.OilCoolingPartId = null;
        parts.RestrictorPartId = null;
        parts.IntercoolerPartId = null;
    }

    private void MapDbToOldEnums(CarCard car)
    {
        var db = Fh6DatabaseService.Instance;
        var dbCar = db.GetCar(car.CarDbId);
        if (dbCar == null) return;

        // DriveType (overridden by drivetrain swap)
        int driveTypeId = dbCar.DriveTypeID;
        var drivetrainSwap = _selectedParts.DrivetrainSwapPartId != null ? db.GetDrivetrainSwapById(_selectedParts.DrivetrainSwapPartId.Value) : null;
        if (drivetrainSwap != null)
            driveTypeId = drivetrainSwap.DriveTypeID;
        car.DriveType = driveTypeId switch
        {
            0 => Forza_Horizon_6_Tune_Master.Models.DriveType.FWD,
            2 => Forza_Horizon_6_Tune_Master.Models.DriveType.AWD,
            _ => Forza_Horizon_6_Tune_Master.Models.DriveType.RWD
        };
        car.DriveTypeID = driveTypeId;

        // Engine type from stock engine's CylinderID + ConfigID
        var engine = db.GetEngine(car.EngineDbId);
        if (engine != null)
        {
            car.EngineType = MapEngineType(engine.CylinderID, engine.ConfigID);
            car.FuelType = engine.Diesel ? FuelType.Diesel : FuelType.Gasoline;
            car.AspirationType = dbCar.AspirationTypeId switch
            {
                2 => Models.AspirationType.SingleTurbo,
                3 => Models.AspirationType.TwinTurbo,
                5 => Models.AspirationType.PositiveDisplacement,
                6 => Models.AspirationType.Centrifugal,
                8 => Models.AspirationType.Electric,
                _ => Models.AspirationType.Natural
            };
            IsElectricCar = car.AspirationType == Models.AspirationType.Electric;
        }

        MapPartIdsToEnums(car);
    }

    private void MapPartIdsToEnums(CarCard car)
    {
        var db = Fh6DatabaseService.Instance;
        car.TireType = MapTierFromLevel<TireType>(_selectedParts.TireCompoundPartId,
            id => db.GetTireCompoundById(id)?.Level ?? 0);
        car.SuspensionUpgrade = MapTierFromLevel<SuspensionUpgrade>(_selectedParts.SpringDamperPartId,
            id => db.GetSpringDamperById(id)?.Level ?? 0);
        car.BrakesUpgrade = MapTierFromLevel<BrakesUpgrade>(_selectedParts.BrakePartId,
            id => db.GetBrakesById(id)?.Level ?? 0);
        car.DifferentialUpgrade = MapTierFromLevel<DifferentialUpgrade>(_selectedParts.DifferentialPartId,
            id => db.GetDifferentialById(id)?.Level ?? 0);

        car.HasRearAero = _selectedParts.RearWingPartId != null;
        // A non-stock front bumper/splitter with an aero physics profile adds front downforce.
        var frontBumper = _selectedParts.FrontBumperPartId != null
            ? db.GetFrontBumperById(_selectedParts.FrontBumperPartId.Value) : null;
        car.HasFrontAero = frontBumper is { IsStock: false, AeroPhysicsID: > 0 };
        car.HasFrontARB = _selectedParts.AntiSwayFrontPartId != null;
        car.HasRearARB = _selectedParts.AntiSwayRearPartId != null;
    }

    private void UpdateAspirationTypeFromForcedInduction()
    {
        var db = Fh6DatabaseService.Instance;

        // No forced-induction part = naturally aspirated (the synthetic "Stock / NA"
        // option on a naturally-aspirated engine maps back to a null part id).
        if (!_selectedParts.ForcedInductionPartId.HasValue)
        {
            Car.AspirationType = Models.AspirationType.Natural;
            Car.AntiLag = false;
            return;
        }

        var fi = db.GetForcedInductionById(_selectedParts.ForcedInductionPartId.Value);
        Car.AspirationType = fi switch
        {
            DbUpgradeTurboSingle => Models.AspirationType.SingleTurbo,
            DbUpgradeTurboTwin   => Models.AspirationType.TwinTurbo,
            DbUpgradeCSC         => Models.AspirationType.Centrifugal,
            DbUpgradeDSC         => Models.AspirationType.PositiveDisplacement,
            _                    => Car.AspirationType
        };

        // Tier 4 is the race turbo with anti-lag (absolute Level, like the resolver).
        Car.AntiLag = fi switch
        {
            DbUpgradeTurboSingle ts => ts.Level >= 4,
            DbUpgradeTurboTwin tt   => tt.Level >= 4,
            _                       => false
        };
    }

    private void UpdateTireAndWheelData()
    {
        var db = Fh6DatabaseService.Instance;

        var twf = _selectedParts.TireWidthFrontPartId != null
            ? db.GetTireWidthFrontById(_selectedParts.TireWidthFrontPartId.Value) : null;
        if (twf != null) Car.FrontTireWidth = twf.FrontTireWidth;

        var twr = _selectedParts.TireWidthRearPartId != null
            ? db.GetTireWidthRearById(_selectedParts.TireWidthRearPartId.Value) : null;
        if (twr != null) Car.RearTireWidth = twr.RearTireWidth;

        var rf = _selectedParts.RimFrontPartId != null
            ? db.GetRimFrontById(_selectedParts.RimFrontPartId.Value) : null;
        if (rf != null) Car.FrontRimDiameter = rf.FrontWheelDiameter;

        var rr = _selectedParts.RimRearPartId != null
            ? db.GetRimRearById(_selectedParts.RimRearPartId.Value) : null;
        if (rr != null) Car.RearRimDiameter = rr.RearWheelDiameter;

        var taf = _selectedParts.TireAspectRatioFrontPartId != null
            ? db.GetTireAspectRatioFrontById(_selectedParts.TireAspectRatioFrontPartId.Value) : null;
        if (taf != null) Car.FrontTireProfile = (int)(Car.StockFrontTireProfile + taf.FrontTireAspectRatioOffset);

        var tar = _selectedParts.TireAspectRatioRearPartId != null
            ? db.GetTireAspectRatioRearById(_selectedParts.TireAspectRatioRearPartId.Value) : null;
        if (tar != null) Car.RearTireProfile = (int)(Car.StockRearTireProfile + tar.RearTireAspectRatioOffset);
    }

    private static EngineType MapEngineType(int cylinderId, int configId)
    {
        return configId switch
        {
            5 => EngineType.V6, // Electric — no mapping, default V6
            3 => EngineType.Rotary, // Rotary
            2 => EngineType.Boxer, // Boxer
            4 => EngineType.W12, // W
            1 => cylinderId switch // V
            {
                5 => EngineType.V6,
                6 => EngineType.V8,
                7 => EngineType.V10,
                _ => EngineType.V12
            },
            _ => cylinderId switch // Inline (0)
            {
                0 => EngineType.I1,
                1 => EngineType.I2,
                2 => EngineType.I3,
                3 => EngineType.I4,
                4 => EngineType.I5,
                5 => EngineType.I6,
                6 => EngineType.I8,
                _ => EngineType.V6
            }
        };
    }

    private static T MapTierFromLevel<T>(int? partId, Func<int, int> getLevel) where T : Enum
    {
        if (partId == null) return (T)Enum.GetValues(typeof(T)).GetValue(0)!;
        int level = getLevel(partId.Value);
        var values = Enum.GetValues(typeof(T));
        int idx = Math.Clamp(level, 0, values.Length - 1);
        return (T)values.GetValue(idx)!;
    }

    private void NotifyPartSelectionProperties()
    {
    OnPropertyChanged(nameof(SwapsVM));
    OnPropertyChanged(nameof(EngineVM));
    OnPropertyChanged(nameof(MotorVM));
    OnPropertyChanged(nameof(SuspensionVM));
    OnPropertyChanged(nameof(TransmissionVM));
    OnPropertyChanged(nameof(TiresWheelsVM));
    OnPropertyChanged(nameof(AeroVisualVM));
    }

    private void NotifyCarDisplayProperties()
    {
        OnPropertyChanged(nameof(PowerDisplay));
        OnPropertyChanged(nameof(SpeedDisplay));
        OnPropertyChanged(nameof(MassDisplay));
        OnPropertyChanged(nameof(TorqueDisplay));
        OnPropertyChanged(nameof(WheelbaseDisplay));
        OnPropertyChanged(nameof(FrontTrackDisplay));
        OnPropertyChanged(nameof(RearTrackDisplay));
        OnPropertyChanged(nameof(SelectedCarDisplayText));
    }

    // ── Unit persistence ──────────────────────────────────────────────────────
    private void SaveUnitSettings()
    {
        LocalizationService.Instance.SaveUnitSettings(_measurementSystem, _powerUnit, _springUnit);
    }

    // ── Unit toggles ─────────────────────────────────────────────────────────
    private void DoToggleUnits()
    {
        MeasurementSystem = _measurementSystem == UnitSystem.Metric ? UnitSystem.Imperial : UnitSystem.Metric;
    }

    private void DoTogglePowerUnit()
    {
        PowerUnit = _powerUnit switch
        {
            PowerUnit.HP => PowerUnit.PS,
            PowerUnit.PS => PowerUnit.KW,
            _            => PowerUnit.HP
        };
    }

    private void DoToggleSpringUnit()
    {
        SpringUnit = _springUnit switch
        {
            SpringUnit.KgfMm => SpringUnit.NMm,
            SpringUnit.NMm   => SpringUnit.LbsIn,
            _                => SpringUnit.KgfMm
        };
    }

    // ── Tune generation ──────────────────────────────────────────────────────
    private void GenerateTune()
    {
        BusyMessage = T("BusyGenerating");
        IsGenerating = true;
        try
        {
            Car.Name = _profileService.AutoProfileName(Car, Track);
            TuneResult = _generator.Generate(Car, Track, _selectedParts, Fh6DatabaseService.Instance);
            var discLocalized = T($"Discipline{Track.Discipline}");
            StatusMessage = string.Format(T("StatusTuneGenerated"), Car.Make, Car.Model, $"{discLocalized}  •  {DateTime.Now:HH:mm}");

            _tuneGenerationCount++;
            if (_tuneGenerationCount % 100 == 0)
            {
                var owner = Application.Current?.MainWindow;
                if (owner != null)
                    new DonateWindow { Owner = owner }.ShowDialog();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(T("StatusGenerationError"), ex.Message);
            MessageBox.Show(ex.ToString(), T("ErrorCaption"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsGenerating = false;
        }
    }

    // ── Profile management ──────────────────────────────────────────────────
    private void SaveProfile()
    {
        try
        {
            // If the auto-name changed since the last generate (e.g. season switched without
            // regenerating), delete the stale file so it doesn't duplicate in the dropdown.
            if (!string.IsNullOrEmpty(Car.Name))
            {
                string newAutoName = _profileService.AutoProfileName(Car, Track);
                if (Car.Name != newAutoName)
                    _profileService.Delete(Car.Name);
            }
            string name = _profileService.Save(Car, Track, _selectedParts, TuneResult, Constraints);
            RefreshProfiles();
            SelectProfileSilently(name);
            StatusMessage = string.Format(T("StatusProfileSaved"), name);
        }
        catch (Exception ex) { StatusMessage = string.Format(T("StatusSaveError"), ex.Message); }
    }

    private void SelectProfileSilently(string name)
    {
        _selectedProfile = name;
        OnPropertyChanged(nameof(SelectedProfile));
        LoadCommand.Raise();
        DeleteProfileCommand.Raise();
        ProfileSearchText = name;
    }

    private void LoadProfile()
    {
        if (SelectedProfile == null) return;
        try
        {
            var p = _profileService.Load(SelectedProfile);
            if (p == null) { StatusMessage = T("StatusProfileNotFound"); return; }
            _isLoadingProfile = true;
            Car         = p.Car;
            Track       = p.Track;
            _selectedParts = p.Parts ?? new SelectedParts();
            _selectedParts.ResolveEngineAndMotorIds();
            Car.EngineDbId = _selectedParts.EngineId ?? Car.EngineDbId;
            Car.MotorDbId  = _selectedParts.MotorId ?? Car.MotorDbId;
            // Restore CarDbId from Make/Model/Year match first (old profiles may have CarDbId=0)
            _carSpec.SelectCarFromProfile(Car);
            // Now CarDbId is correct → load sub-VMs with correct parts
            LoadSubViewModels(Car);
            OnPropertyChanged(nameof(SelectedParts));
            var loadedResult = p.LastResult;
            if (loadedResult != null) { loadedResult.Car = Car; loadedResult.Track = Track; }
            TuneResult  = loadedResult;
            // Restore constraints from saved profile
            if (p.Constraints != null)
            {
                foreach (var prop in typeof(TuningConstraints).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                {
                    if (prop.CanWrite && prop.Name != nameof(TuningConstraints.CenterDiffBias))
                    {
                        var val = prop.GetValue(p.Constraints);
                        prop.SetValue(Constraints, val);
                    }
                }
                Constraints.CenterDiffBias = p.Constraints.CenterDiffBias;
            }
            MapPartIdsToEnums(Car);
            _isLoadingProfile = false;
            StatusMessage = string.Format(T("StatusProfileLoaded"), SelectedProfile);
        }
        catch (Exception ex) { StatusMessage = string.Format(T("StatusLoadError"), ex.Message); }
    }

    private void DeleteProfile()
    {
        if (SelectedProfile == null) return;
        if (MessageBox.Show(string.Format(T("DeleteProfileConfirm"), SelectedProfile), T("DeleteProfileTitle"),
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _profileService.Delete(SelectedProfile);
        RefreshProfiles();
        StatusMessage = T("StatusProfileDeleted");
    }

    private void DeleteAllProfiles()
    {
        if (MessageBox.Show(T("DeleteAllProfilesConfirm"), T("DeleteAllProfilesTitle"),
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        _profileService.DeleteAll();
        RefreshProfiles();
        StatusMessage = T("StatusAllProfilesDeleted");
    }

    private void NewProfile()
    {
        Car = new CarCard(); Track = new TrackInfo(); _selectedParts = new SelectedParts();
        OnPropertyChanged(nameof(SelectedParts));
        TuneResult  = null;
        _carSpec.ClearCarSelection(_car);
        SelectedProfile = null;
        ProfileSearchText = "";
        StatusMessage = T("StatusProfileCreated");
    }

    // ── Auto-recalculate profiles with outdated version ───────────────────
    private void RecalculateOutdatedProfiles()
    {
        var names = _profileService.GetProfileNames();
        if (names.Count == 0) return;
        int ok = 0;
        foreach (var name in names)
        {
            var p = _profileService.Load(name);
            if (p == null) continue;
            if (p.Version == SavedProfile.ProfileVersion) continue;
            try
            {
                // v1.x profiles had enums as [JsonIgnore] — restore from PartIds
                MapPartIdsToEnums(p.Car);
                p.LastResult = _generator.Generate(p.Car, p.Track, p.Parts ?? new SelectedParts(), Fh6DatabaseService.Instance);
                p.Version = SavedProfile.ProfileVersion;
                _storage.Save(name, p);
                ok++;
            }
            catch { }
        }
        if (ok > 0)
            StatusMessage = string.Format(T("StatusProfilesRecalculated"), ok);
    }

    private void RefreshProfiles()
    {
        Profiles = new ObservableCollection<string>(_profileService.GetProfileNames());
        ApplyProfileFilter();
        LoadCommand.Raise();
        DeleteProfileCommand.Raise();
    }

    // ── Car database ─────────────────────────────────────────────────────────
    private async Task LoadCarDatabaseAsync()
    {
        await _carSpec.LoadCarDatabaseAsync(Car);
        _carSpec.SelectCarFromProfile(Car);
    }

    public string AppVersion => SavedProfile.ProfileVersion;

}

public class UnitSystemOption : NotifyBase
{
    public UnitSystem Value { get; set; }

    private string _label = "";
    public string Label
    {
        get => _label;
        set { _label = value; OnPropertyChanged(); }
    }

    public override string ToString() => Label;
}

public class PowerUnitOption : NotifyBase
{
    public PowerUnit Value { get; set; }

    private string _label = "";
    public string Label
    {
        get => _label;
        set { _label = value; OnPropertyChanged(); }
    }

    public override string ToString() => Label;
}

public class SpringUnitOption : NotifyBase
{
    public SpringUnit Value { get; set; }

    private string _label = "";
    public string Label
    {
        get => _label;
        set { _label = value; OnPropertyChanged(); }
    }

    public override string ToString() => Label;
}
