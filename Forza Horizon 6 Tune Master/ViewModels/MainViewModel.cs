using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;
using Forza_Horizon_6_Tune_Master.Views;

namespace Forza_Horizon_6_Tune_Master.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly TuneGeneratorService _generator = new();
    private readonly StorageService _storage = new();
    private readonly ProfileService _profileService;
    private int _tuneGenerationCount;

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
            _carSpec.ClearAiEstimatedFields();
            _carSpec.ClearAiSpecStatus();
            OnPropertyChanged(nameof(AiSpecStatusMessage));
            OnPropertyChanged(nameof(HasAiSpecStatus));
            OnModelChanged(null, null!);
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

    private TuningConstraints _constraints = new();
    public TuningConstraints Constraints
    {
        get => _constraints;
        set
        {
            if (value == null) return;
            if (_constraints != null) _constraints.PropertyChanged -= OnModelChanged;
            _constraints = value;
            _constraints.PropertyChanged += OnModelChanged;
            OnPropertyChanged();
            OnModelChanged(null, null!);
        }
    }

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
    public bool HasLaunchControl => _tuneResult?.LaunchControlRpm.HasValue == true;

    // ── Car database + specs ──────────────────────────────────────────────────
    private readonly CarSpecController _carSpec = new();
    private bool _isLoadingProfile;

    public CarData? SelectedCar
    {
        get => _carSpec.SelectedCar;
        set
        {
            _carSpec.SelectCar(value, _car, _isLoadingProfile);
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
            NotifyConstraintDisplayProperties();
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

    // ── Constraint display properties (unit-aware) ───────────────────────────
    public double TirePressureFrontMinDisplay
    {
        get => UnitConverter.TirePressureToDisplay(Constraints.TirePressureFrontMin, UseImperial);
        set { Constraints.TirePressureFrontMin = UnitConverter.TirePressureFromDisplay(value, UseImperial); OnPropertyChanged(); }
    }
    public double TirePressureFrontMaxDisplay
    {
        get => UnitConverter.TirePressureToDisplay(Constraints.TirePressureFrontMax, UseImperial);
        set { Constraints.TirePressureFrontMax = UnitConverter.TirePressureFromDisplay(value, UseImperial); OnPropertyChanged(); }
    }
    public double TirePressureRearMinDisplay
    {
        get => UnitConverter.TirePressureToDisplay(Constraints.TirePressureRearMin, UseImperial);
        set { Constraints.TirePressureRearMin = UnitConverter.TirePressureFromDisplay(value, UseImperial); OnPropertyChanged(); }
    }
    public double TirePressureRearMaxDisplay
    {
        get => UnitConverter.TirePressureToDisplay(Constraints.TirePressureRearMax, UseImperial);
        set { Constraints.TirePressureRearMax = UnitConverter.TirePressureFromDisplay(value, UseImperial); OnPropertyChanged(); }
    }

    // Springs
    public double SpringFrontMinDisplay
    {
        get => UnitConverter.SpringToDisplay(Constraints.SpringFrontMin, _springUnit);
        set { Constraints.SpringFrontMin = UnitConverter.SpringFromDisplay(value, _springUnit); OnPropertyChanged(); }
    }
    public double SpringFrontMaxDisplay
    {
        get => UnitConverter.SpringToDisplay(Constraints.SpringFrontMax, _springUnit);
        set { Constraints.SpringFrontMax = UnitConverter.SpringFromDisplay(value, _springUnit); OnPropertyChanged(); }
    }
    public double SpringRearMinDisplay
    {
        get => UnitConverter.SpringToDisplay(Constraints.SpringRearMin, _springUnit);
        set { Constraints.SpringRearMin = UnitConverter.SpringFromDisplay(value, _springUnit); OnPropertyChanged(); }
    }
    public double SpringRearMaxDisplay
    {
        get => UnitConverter.SpringToDisplay(Constraints.SpringRearMax, _springUnit);
        set { Constraints.SpringRearMax = UnitConverter.SpringFromDisplay(value, _springUnit); OnPropertyChanged(); }
    }

    // Ride Height
    public double RideHeightFrontMinDisplay
    {
        get => UnitConverter.RideHeightToDisplay(Constraints.RideHeightFrontMin, UseImperial);
        set { Constraints.RideHeightFrontMin = UnitConverter.RideHeightFromDisplay(value, UseImperial); OnPropertyChanged(); }
    }
    public double RideHeightFrontMaxDisplay
    {
        get => UnitConverter.RideHeightToDisplay(Constraints.RideHeightFrontMax, UseImperial);
        set { Constraints.RideHeightFrontMax = UnitConverter.RideHeightFromDisplay(value, UseImperial); OnPropertyChanged(); }
    }
    public double RideHeightRearMinDisplay
    {
        get => UnitConverter.RideHeightToDisplay(Constraints.RideHeightRearMin, UseImperial);
        set { Constraints.RideHeightRearMin = UnitConverter.RideHeightFromDisplay(value, UseImperial); OnPropertyChanged(); }
    }
    public double RideHeightRearMaxDisplay
    {
        get => UnitConverter.RideHeightToDisplay(Constraints.RideHeightRearMax, UseImperial);
        set { Constraints.RideHeightRearMax = UnitConverter.RideHeightFromDisplay(value, UseImperial); OnPropertyChanged(); }
    }

    // ── Constraint unit labels ────────────────────────────────────────────────
    public string TirePressureUnitLabel => UseImperial ? T("UnitPressure_Imperial") : T("UnitPressure_Metric");
    public string SpringUnitLabel => _springUnit switch
    {
        SpringUnit.NMm   => T("UnitSpring_NMm"),
        SpringUnit.LbsIn => T("UnitSpring_LbsIn"),
        _                => T("UnitSpring_KgfMm")
    };
    public string RideHeightUnitLabel => UseImperial ? T("UnitRideHeight_Imperial") : T("UnitRideHeight_Metric");

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
            OnPropertyChanged(nameof(SpringUnitLabel));
            OnPropertyChanged(nameof(SpringFrontMinDisplay));
            OnPropertyChanged(nameof(SpringFrontMaxDisplay));
            OnPropertyChanged(nameof(SpringRearMinDisplay));
            OnPropertyChanged(nameof(SpringRearMaxDisplay));
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
    public double PowerDisplay
    {
        get => UnitConverter.PowerToDisplay(_car.PowerHP, _powerUnit);
        set
        {
            _car.PowerHP = UnitConverter.PowerFromDisplay(value, _powerUnit);
            OnPropertyChanged();
        }
    }

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

    public double TorqueDisplay
    {
        get => UnitConverter.TorqueToDisplay(_car.TorqueNm, UseImperial);
        set
        {
            _car.TorqueNm = UnitConverter.TorqueFromDisplay(value, UseImperial);
            OnPropertyChanged();
        }
    }

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
                NotifyConstraintDisplayProperties();
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
                OnPropertyChanged(nameof(SpringUnitLabel));
                OnPropertyChanged(nameof(SpringFrontMinDisplay));
                OnPropertyChanged(nameof(SpringFrontMaxDisplay));
                OnPropertyChanged(nameof(SpringRearMinDisplay));
                OnPropertyChanged(nameof(SpringRearMaxDisplay));
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
            Application.Current?.Dispatcher.Invoke(InvalidateAllLanguageDependent);
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
        OnPropertyChanged(nameof(TirePressureUnitLabel));
        OnPropertyChanged(nameof(SpringUnitLabel));
        OnPropertyChanged(nameof(RideHeightUnitLabel));
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
    private bool _isAutoGenerate;
    public bool IsAutoGenerate
    {
        get => _isAutoGenerate;
        set
        {
            _isAutoGenerate = value;
            OnPropertyChanged();
            if (value) _ = DebounceGenerate();
        }
    }

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

    public bool IsFetchingAiSpecs
    {
        get => _carSpec.IsFetchingAiSpecs;
        set => _carSpec.IsFetchingAiSpecs = value;
    }

    public bool IsLoadingCars
    {
        get => _carSpec.IsLoadingCars;
        set => _carSpec.IsLoadingCars = value;
    }

    public bool IsLoadingCarSpecs
    {
        get => _carSpec.IsLoadingCarSpecs;
        set => _carSpec.IsLoadingCarSpecs = value;
    }

    public bool IsBusy => _isGenerating || _carSpec.IsFetchingAiSpecs || _carSpec.IsLoadingCars || _carSpec.IsLoadingCarSpecs;

    private string _busyMessage = "";
    public string BusyMessage
    {
        get => _busyMessage;
        set { _busyMessage = value; OnPropertyChanged(); }
    }

    // ── AI spec status overlay ──────────────────────────────────────────────
    public string AiSpecStatusMessage
    {
        get => _carSpec.AiSpecStatusMessage;
        set { OnPropertyChanged(); OnPropertyChanged(nameof(HasAiSpecStatus)); }
    }
    public bool HasAiSpecStatus => _carSpec.HasAiSpecStatus;
    public bool NeedsCarSelectionHighlight => _carSpec.NeedsCarSelectionHighlight;

    // ── AI-estimated field tracking ─────────────────────────────────────────
    public bool IsWheelbaseAiEstimated  => _carSpec.IsWheelbaseAiEstimated;
    public bool IsFrontTrackAiEstimated => _carSpec.IsFrontTrackAiEstimated;
    public bool IsRearTrackAiEstimated  => _carSpec.IsRearTrackAiEstimated;
    public bool IsCdAiEstimated         => _carSpec.IsCdAiEstimated;
    public bool IsFrontalAreaAiEstimated => _carSpec.IsFrontalAreaAiEstimated;
    public bool HasAnyAiEstimatedField => _carSpec.HasAnyAiEstimatedField;

    private int _pendingGenerationId;
    private DateTime _lastInputChange = DateTime.MinValue;
    private CancellationTokenSource? _debounceCts;

    private void SubscribeModelChanges()
    {
        _car.PropertyChanged += OnModelChanged;
        _car.PropertyChanged += OnCarPropertyChanged;
        _track.PropertyChanged += OnModelChanged;
        _constraints.PropertyChanged += OnModelChanged;
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
        if (e.PropertyName is nameof(CarCard.Make) or nameof(CarCard.Model) or nameof(CarCard.Year))
            OnPropertyChanged(nameof(SelectedCarDisplayText));

        _carSpec.OnCarPropertyChanged(e.PropertyName);
    }

    private void OnModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!_isAutoGenerate || _isGenerating) return;
        _lastInputChange = DateTime.Now;
        _ = DebounceGenerate();
    }

    private async Task DebounceGenerate()
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = new CancellationTokenSource();
        int myId = ++_pendingGenerationId;
        try
        {
            await Task.Delay(400, _debounceCts.Token);
            if (!_isAutoGenerate || myId != _pendingGenerationId) return;
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
    public RelayCommand NewProfileCommand     { get; }
    public RelayCommand FetchAiCarSpecsCommand { get; }
    public RelayCommand DismissAiSpecStatusCommand { get; }
    public RelayCommand ClearCarSelectionCommand { get; }
    public RelayCommand RefreshCarDatabaseCommand { get; }
    public RelayCommand ClearCacheCommand { get; }
    public RelayCommand ClearAiCacheCommand { get; }

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
        _carSpec.NotifyAiEstimatedFieldProperties = () =>
        {
            OnPropertyChanged(nameof(IsWheelbaseAiEstimated));
            OnPropertyChanged(nameof(IsFrontTrackAiEstimated));
            OnPropertyChanged(nameof(IsRearTrackAiEstimated));
            OnPropertyChanged(nameof(IsCdAiEstimated));
            OnPropertyChanged(nameof(IsFrontalAreaAiEstimated));
            OnPropertyChanged(nameof(HasAnyAiEstimatedField));
        };
        _carSpec.RaiseRefreshCarDbCommand = () => RefreshCarDatabaseCommand?.Raise();
        _carSpec.RaiseFetchAiSpecsCommand = () => FetchAiCarSpecsCommand?.Raise();
        _carSpec.SetBusyMessage = msg => BusyMessage = msg;
        _carSpec.SetStatusMessage = msg => StatusMessage = msg;
        _carSpec.BusyFlagsChanged = () =>
        {
            OnPropertyChanged(nameof(IsFetchingAiSpecs));
            OnPropertyChanged(nameof(IsLoadingCars));
            OnPropertyChanged(nameof(IsLoadingCarSpecs));
            OnPropertyChanged(nameof(IsBusy));
        };
        _carSpec.NotifyAiSpecStatusChanged = () =>
        {
            OnPropertyChanged(nameof(AiSpecStatusMessage));
            OnPropertyChanged(nameof(HasAiSpecStatus));
        };

        GenerateCommand        = new RelayCommand(GenerateTune);
        SaveCommand            = new RelayCommand(SaveProfile);
        LoadCommand            = new RelayCommand(LoadProfile, () => SelectedProfile != null);
        DeleteProfileCommand   = new RelayCommand(DeleteProfile, () => SelectedProfile != null);
        NewProfileCommand      = new RelayCommand(NewProfile);
        FetchAiCarSpecsCommand = new RelayCommand(() => _ = _carSpec.FetchAiCarSpecsAsync(Car), () => !IsFetchingAiSpecs);
        DismissAiSpecStatusCommand = new RelayCommand(() =>
        {
            _carSpec.ClearAiSpecStatus();
            OnPropertyChanged(nameof(AiSpecStatusMessage));
            OnPropertyChanged(nameof(HasAiSpecStatus));
        });
        ClearCarSelectionCommand = new RelayCommand(() => _carSpec.ClearCarSelection(_car));
        RefreshCarDatabaseCommand = new RelayCommand(() => _ = _carSpec.RefreshCarDatabaseAsync(), () => !IsLoadingCars);
        ClearCacheCommand = new RelayCommand(() => _carSpec.ClearCache());
        ClearAiCacheCommand = new RelayCommand(() => _carSpec.ClearAiCache());

        ToggleUnitsCommand      = new RelayCommand(DoToggleUnits);
        TogglePowerUnitCommand  = new RelayCommand(DoTogglePowerUnit);
        ToggleSpringUnitCommand = new RelayCommand(DoToggleSpringUnit);
        SetLanguageCommand      = new RelayCommand(() => { }); // no-op: switching handled by SelectedLanguageItem setter

        _car.PropertyChanged += OnModelChanged;
        _car.PropertyChanged += OnCarPropertyChanged;
        _track.PropertyChanged += OnModelChanged;
        _constraints.PropertyChanged += OnModelChanged;

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
        if (savedSu != null && Enum.TryParse<SpringUnit>(savedSu, out var su)) SpringUnit = su;

        RefreshProfiles();
        RecalculateOutdatedProfiles();
        InvalidateAllLanguageDependent();
        _ = LoadCarDatabaseAsync();
    }

    private void NotifyConstraintDisplayProperties()
    {
        OnPropertyChanged(nameof(TirePressureFrontMinDisplay));
        OnPropertyChanged(nameof(TirePressureFrontMaxDisplay));
        OnPropertyChanged(nameof(TirePressureRearMinDisplay));
        OnPropertyChanged(nameof(TirePressureRearMaxDisplay));
        OnPropertyChanged(nameof(SpringFrontMinDisplay));
        OnPropertyChanged(nameof(SpringFrontMaxDisplay));
        OnPropertyChanged(nameof(SpringRearMinDisplay));
        OnPropertyChanged(nameof(SpringRearMaxDisplay));
        OnPropertyChanged(nameof(RideHeightFrontMinDisplay));
        OnPropertyChanged(nameof(RideHeightFrontMaxDisplay));
        OnPropertyChanged(nameof(RideHeightRearMinDisplay));
        OnPropertyChanged(nameof(RideHeightRearMaxDisplay));
        OnPropertyChanged(nameof(TirePressureUnitLabel));
        OnPropertyChanged(nameof(SpringUnitLabel));
        OnPropertyChanged(nameof(RideHeightUnitLabel));
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
            TuneResult = _generator.Generate(Car, Track, Constraints);
            var discLocalized = T($"Discipline{Track.Discipline}");
            StatusMessage = string.Format(T("StatusTuneGenerated"), Car.Make, Car.Model, $"{discLocalized}  •  {DateTime.Now:HH:mm}");

            _tuneGenerationCount++;
            if (_tuneGenerationCount % 25 == 0)
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
            string name = _profileService.Save(Car, Track, Constraints, TuneResult, _carSpec.AiEstimatedFields.ToList());
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
            Car         = p.Car;
            Track       = p.Track;
            Constraints = p.Constraints;
            NotifyConstraintDisplayProperties();
            var loadedResult = p.LastResult;
            if (loadedResult != null) { loadedResult.Car = Car; loadedResult.Track = Track; }
            TuneResult  = loadedResult;
            _carSpec.AiEstimatedFields.Clear();
            foreach (var f in p.AiEstimatedFields)
                _carSpec.AiEstimatedFields.Add(f);
            OnPropertyChanged(nameof(IsWheelbaseAiEstimated));
            OnPropertyChanged(nameof(IsFrontTrackAiEstimated));
            OnPropertyChanged(nameof(IsRearTrackAiEstimated));
            OnPropertyChanged(nameof(IsCdAiEstimated));
            OnPropertyChanged(nameof(IsFrontalAreaAiEstimated));
            OnPropertyChanged(nameof(HasAnyAiEstimatedField));
            _isLoadingProfile = true;
            _carSpec.SelectCarFromProfile(Car);
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

    private void NewProfile()
    {
        Car = new CarCard(); Track = new TrackInfo(); Constraints = new TuningConstraints();
        NotifyConstraintDisplayProperties();
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
                p.LastResult = _generator.Generate(p.Car, p.Track, p.Constraints);
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

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
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
