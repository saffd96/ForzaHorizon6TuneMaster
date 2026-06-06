using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;

namespace Forza_Horizon_6_Tune_Master.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly TuneGeneratorService _generator = new();
    private readonly StorageService _storage = new();

    // ── Models ──────────────────────────────────────────────────────────────
    private CarCard _car = new();
    public CarCard Car
    {
        get => _car;
        set
        {
            if (_car != null) _car.PropertyChanged -= OnModelChanged;
            if (_car != null) _car.PropertyChanged -= OnCarPropertyChanged;
            _car = value;
            _car.PropertyChanged += OnModelChanged;
            _car.PropertyChanged += OnCarPropertyChanged;
            OnPropertyChanged();
            NotifyCarDisplayProperties();
            OnPropertyChanged(nameof(HasCenterDiffBias));
            OnPropertyChanged(nameof(SelectedCarDisplayText));
            ClearAiEstimatedFields();
            OnModelChanged(null, null!);
        }
    }

    private TrackInfo _track = new();
    public TrackInfo Track
    {
        get => _track;
        set
        {
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

    // ── Car database ──────────────────────────────────────────────────────────
    private readonly CarDatabaseService _carDbService = new();
    private readonly WikiCarSpecService _wikiSpecService = new();
    private CancellationTokenSource? _wikiSpecsCts;
    private List<CarData> _carDatabase = new();

    private bool _suppressFilter;
    private bool _isLoadingProfile;

    private CarData? _selectedCar;
    public CarData? SelectedCar
    {
        get => _selectedCar;
        set
        {
            if (_selectedCar == value) return;
            _selectedCar = value;
            if (value != null)
            {
                _car.Make = value.Make;
                _car.Model = value.Model;
                _car.Year = value.Year;
                _suppressFilter = true;
                CarSearchText = value.DisplayName;
                _suppressFilter = false;
                ApplyCarFilter();

                _wikiSpecsCts?.Cancel();
                _wikiSpecsCts = new CancellationTokenSource();
                if (!_isLoadingProfile)
                    _ = FetchAndApplyWikiSpecsAsync(value, _wikiSpecsCts.Token);
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedCarDisplayText));
        }
    }

    private string _carSearchText = "";
    public string CarSearchText
    {
        get => _carSearchText;
        set
        {
            if (_carSearchText == value) return;
            _carSearchText = value;
            OnPropertyChanged();
            if (!_suppressFilter) ApplyCarFilter();
        }
    }

    private readonly ObservableCollection<CarData> _filteredCarDatabase = new();
    public ObservableCollection<CarData> FilteredCarDatabase => _filteredCarDatabase;

    private void ApplyCarFilter()
    {
        var query = string.IsNullOrWhiteSpace(_carSearchText)
            ? null
            : _carSearchText.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        _filteredCarDatabase.Clear();
        foreach (var car in _carDatabase)
        {
            if (query == null || query.All(q => car.DisplayName.ToLowerInvariant().Contains(q)))
                _filteredCarDatabase.Add(car);
        }
    }

    public string SelectedCarDisplayText => _selectedCar?.DisplayName
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
    // Tire Pressure
    private double TirePressureToDisplay(double bar) => UseImperial ? Math.Round(bar * 14.504, 1) : bar;
    private double TirePressureFromDisplay(double val) => UseImperial ? Math.Round(val / 14.504, 2) : val;

    public double TirePressureFrontMinDisplay
    {
        get => TirePressureToDisplay(Constraints.TirePressureFrontMin);
        set { Constraints.TirePressureFrontMin = TirePressureFromDisplay(value); OnPropertyChanged(); }
    }
    public double TirePressureFrontMaxDisplay
    {
        get => TirePressureToDisplay(Constraints.TirePressureFrontMax);
        set { Constraints.TirePressureFrontMax = TirePressureFromDisplay(value); OnPropertyChanged(); }
    }
    public double TirePressureRearMinDisplay
    {
        get => TirePressureToDisplay(Constraints.TirePressureRearMin);
        set { Constraints.TirePressureRearMin = TirePressureFromDisplay(value); OnPropertyChanged(); }
    }
    public double TirePressureRearMaxDisplay
    {
        get => TirePressureToDisplay(Constraints.TirePressureRearMax);
        set { Constraints.TirePressureRearMax = TirePressureFromDisplay(value); OnPropertyChanged(); }
    }

    // Springs
    private double SpringToDisplay(double nmm) => _springUnit switch
    {
        SpringUnit.KgfMm => Math.Round(nmm / 9.807, 2),
        SpringUnit.LbsIn => Math.Round(nmm * 5.710, 1),
        _                => Math.Round(nmm, 1)
    };
    private double SpringFromDisplay(double val) => _springUnit switch
    {
        SpringUnit.KgfMm => val * 9.807,
        SpringUnit.LbsIn => val / 5.710,
        _                => val
    };

    public double SpringFrontMinDisplay
    {
        get => SpringToDisplay(Constraints.SpringFrontMin);
        set { Constraints.SpringFrontMin = SpringFromDisplay(value); OnPropertyChanged(); }
    }
    public double SpringFrontMaxDisplay
    {
        get => SpringToDisplay(Constraints.SpringFrontMax);
        set { Constraints.SpringFrontMax = SpringFromDisplay(value); OnPropertyChanged(); }
    }
    public double SpringRearMinDisplay
    {
        get => SpringToDisplay(Constraints.SpringRearMin);
        set { Constraints.SpringRearMin = SpringFromDisplay(value); OnPropertyChanged(); }
    }
    public double SpringRearMaxDisplay
    {
        get => SpringToDisplay(Constraints.SpringRearMax);
        set { Constraints.SpringRearMax = SpringFromDisplay(value); OnPropertyChanged(); }
    }

    // Ride Height
    public double RideHeightFrontMinDisplay
    {
        get => UseImperial ? Math.Round(Constraints.RideHeightFrontMin / 25.4, 1) : Constraints.RideHeightFrontMin;
        set { Constraints.RideHeightFrontMin = UseImperial ? Math.Round(value * 25.4, 0) : value; OnPropertyChanged(); }
    }
    public double RideHeightFrontMaxDisplay
    {
        get => UseImperial ? Math.Round(Constraints.RideHeightFrontMax / 25.4, 1) : Constraints.RideHeightFrontMax;
        set { Constraints.RideHeightFrontMax = UseImperial ? Math.Round(value * 25.4, 0) : value; OnPropertyChanged(); }
    }
    public double RideHeightRearMinDisplay
    {
        get => UseImperial ? Math.Round(Constraints.RideHeightRearMin / 25.4, 1) : Constraints.RideHeightRearMin;
        set { Constraints.RideHeightRearMin = UseImperial ? Math.Round(value * 25.4, 0) : value; OnPropertyChanged(); }
    }
    public double RideHeightRearMaxDisplay
    {
        get => UseImperial ? Math.Round(Constraints.RideHeightRearMax / 25.4, 1) : Constraints.RideHeightRearMax;
        set { Constraints.RideHeightRearMax = UseImperial ? Math.Round(value * 25.4, 0) : value; OnPropertyChanged(); }
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
        get => _powerUnit switch
        {
            PowerUnit.KW => Math.Round(_car.PowerHP * 0.7457, 1),
            PowerUnit.PS => Math.Round(_car.PowerHP * 1.01387, 1),
            _            => _car.PowerHP
        };
        set
        {
            _car.PowerHP = _powerUnit switch
            {
                PowerUnit.KW => value / 0.7457,
                PowerUnit.PS => value / 1.01387,
                _            => value
            };
            OnPropertyChanged();
        }
    }

    public double SpeedDisplay
    {
        get => _measurementSystem == UnitSystem.Imperial
            ? Math.Round(_car.MaxSpeedKmh * 0.6214, 1)
            : _car.MaxSpeedKmh;
        set { /* computed — read-only */ OnPropertyChanged(); }
    }

    public double MassDisplay
    {
        get => _measurementSystem == UnitSystem.Imperial
            ? Math.Round(_car.TotalMass * 2.2046, 1)
            : _car.TotalMass;
        set
        {
            _car.TotalMass = _measurementSystem == UnitSystem.Imperial ? value / 2.2046 : value;
            OnPropertyChanged();
        }
    }

    public double TorqueDisplay
    {
        get => _measurementSystem == UnitSystem.Imperial
            ? Math.Round(_car.TorqueNm * 0.73756, 1)
            : _car.TorqueNm;
        set
        {
            _car.TorqueNm = _measurementSystem == UnitSystem.Imperial ? value / 0.73756 : value;
            OnPropertyChanged();
        }
    }

    public double WheelbaseDisplay
    {
        get => _measurementSystem == UnitSystem.Imperial
            ? Math.Round(_car.Wheelbase / 25.4, 1)
            : _car.Wheelbase;
        set
        {
            _car.Wheelbase = _measurementSystem == UnitSystem.Imperial ? value * 25.4 : value;
            OnPropertyChanged();
        }
    }

    public double FrontTrackDisplay
    {
        get => _measurementSystem == UnitSystem.Imperial
            ? Math.Round(_car.FrontTrack / 25.4, 1)
            : _car.FrontTrack;
        set
        {
            _car.FrontTrack = _measurementSystem == UnitSystem.Imperial ? value * 25.4 : value;
            OnPropertyChanged();
        }
    }

    public double RearTrackDisplay
    {
        get => _measurementSystem == UnitSystem.Imperial
            ? Math.Round(_car.RearTrack / 25.4, 1)
            : _car.RearTrack;
        set
        {
            _car.RearTrack = _measurementSystem == UnitSystem.Imperial ? value * 25.4 : value;
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
        // force tune-result multi-bindings to re-evaluate (speed via UnitValueConverter uses loc.T())
        OnPropertyChanged(nameof(TuneResult));
        // force all ComboBox selection boxes to re-render with new labels
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
        // Force all Car-bound ComboBox DataTemplates to re-evaluate converters with new language.
        // Direct field access + OnPropertyChanged avoids the Car setter (no event resubscribe).
        var carrier = _car;
        _car = null!;
        OnPropertyChanged(nameof(Car));
        _car = carrier;
        OnPropertyChanged(nameof(Car));
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
            if (value != null) LoadProfile();
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

    private bool _isFetchingAiSpecs;
    public bool IsFetchingAiSpecs
    {
        get => _isFetchingAiSpecs;
        set
        {
            _isFetchingAiSpecs = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsBusy));
            FetchAiCarSpecsCommand.Raise();
        }
    }

    private bool _isLoadingCars;
    public bool IsLoadingCars
    {
        get => _isLoadingCars;
        set
        {
            _isLoadingCars = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsBusy));
        }
    }

    private bool _isLoadingCarSpecs;
    public bool IsLoadingCarSpecs
    {
        get => _isLoadingCarSpecs;
        set
        {
            _isLoadingCarSpecs = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsBusy));
        }
    }

    public bool IsBusy => _isGenerating || _isFetchingAiSpecs || _isLoadingCars || _isLoadingCarSpecs;

    private string _busyMessage = "";
    public string BusyMessage
    {
        get => _busyMessage;
        set { _busyMessage = value; OnPropertyChanged(); }
    }

    // ── AI-estimated field tracking ─────────────────────────────────────────
    private readonly HashSet<string> _aiEstimatedFields = new();

    public bool IsWheelbaseAiEstimated  => _aiEstimatedFields.Contains("Wheelbase");
    public bool IsFrontTrackAiEstimated => _aiEstimatedFields.Contains("FrontTrack");
    public bool IsRearTrackAiEstimated  => _aiEstimatedFields.Contains("RearTrack");
    public bool IsCdAiEstimated         => _aiEstimatedFields.Contains("Cd");
    public bool IsFrontalAreaAiEstimated => _aiEstimatedFields.Contains("FrontalArea");

    private void ClearAiEstimatedFields()
    {
        _aiEstimatedFields.Clear();
        OnPropertyChanged(nameof(IsWheelbaseAiEstimated));
        OnPropertyChanged(nameof(IsFrontTrackAiEstimated));
        OnPropertyChanged(nameof(IsRearTrackAiEstimated));
        OnPropertyChanged(nameof(IsCdAiEstimated));
        OnPropertyChanged(nameof(IsFrontalAreaAiEstimated));
    }

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
    public RelayCommand ClearCarSelectionCommand { get; }
    public RelayCommand RefreshCarDatabaseCommand { get; }
    public RelayCommand ClearCacheCommand { get; }
    public RelayCommand ClearAiCacheCommand { get; }

    public MainViewModel()
    {
        GenerateCommand        = new RelayCommand(GenerateTune);
        SaveCommand            = new RelayCommand(SaveProfile);
        LoadCommand            = new RelayCommand(LoadProfile);
        DeleteProfileCommand   = new RelayCommand(DeleteProfile);
        NewProfileCommand      = new RelayCommand(NewProfile);
        FetchAiCarSpecsCommand = new RelayCommand(() => _ = FetchAiCarSpecsAsync(), () => !IsFetchingAiSpecs);
        ClearCarSelectionCommand = new RelayCommand(ClearCarSelection);
        RefreshCarDatabaseCommand = new RelayCommand(RefreshCarDatabase, () => !IsLoadingCars);
        ClearCacheCommand = new RelayCommand(ClearCache);
        ClearAiCacheCommand = new RelayCommand(ClearAiCache);

        ToggleUnitsCommand      = new RelayCommand(DoToggleUnits);
        TogglePowerUnitCommand  = new RelayCommand(DoTogglePowerUnit);
        ToggleSpringUnitCommand = new RelayCommand(DoToggleSpringUnit);
        SetLanguageCommand      = new RelayCommand(() => { }); // no-op: switching handled by SelectedLanguageItem setter

        _car.PropertyChanged += OnModelChanged;
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
        _ = LoadCarDatabaseAsync();

        InvalidateAllLanguageDependent();
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
            Car.Name = SelectedProfile ?? AutoProfileName();
            TuneResult = _generator.Generate(Car, Track, Constraints);
            var discLocalized = T($"Discipline{Track.Discipline}");
            StatusMessage = string.Format(T("StatusTuneGenerated"), Car.Make, Car.Model, $"{discLocalized}  •  {DateTime.Now:HH:mm}");
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

    // ── Wiki car specs fetch (auto on car select) ────────────────────────────
    private async Task FetchAndApplyWikiSpecsAsync(CarData car, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(car.WikiPageTitle)) return;

        IsLoadingCarSpecs = true;
        BusyMessage = T("BusyLoadingSpecs");
        try
        {
            var specs = await _wikiSpecService.FetchSpecsAsync(car.WikiPageTitle, ct);
            if (ct.IsCancellationRequested || specs == null) return;

            if (specs.EngineType.HasValue)             Car.EngineType = specs.EngineType.Value;
            if (specs.AspirationType.HasValue)         Car.AspirationType = specs.AspirationType.Value;
            if (specs.PowertrainType.HasValue)         Car.PowertrainType = specs.PowertrainType.Value;
            if (specs.PowerHP is > 0)                  Car.PowerHP = specs.PowerHP.Value;
            if (specs.TorqueNm is > 0)                 Car.TorqueNm = specs.TorqueNm.Value;
            if (specs.DriveType.HasValue)              Car.DriveType = specs.DriveType.Value;
            if (specs.EnginePosition.HasValue)         Car.EnginePosition = specs.EnginePosition.Value;
            if (specs.WeightDistributionFront is > 0)  Car.WeightDistributionFront = specs.WeightDistributionFront.Value;
            if (specs.TotalMassKg is > 0)              Car.TotalMass = specs.TotalMassKg.Value;
            if (specs.GearCount is > 0)                Car.GearCount = specs.GearCount.Value;

            NotifyCarDisplayProperties();
            StatusMessage = string.Format(T("StatusSpecsLoaded"), car.Make, car.Model);
        }
        catch (OperationCanceledException) { /* другой автомобиль выбран */ }
        catch (Exception ex)
        {
            if (!ct.IsCancellationRequested)
                StatusMessage = string.Format(T("StatusSpecsError"), ex.Message);
        }
        finally
        {
            IsLoadingCarSpecs = false;
        }
    }

    // ── AI car specs fetch ──────────────────────────────────────────────────
    private readonly AiCarSpecService _aiCarSpecService = new();
    private async Task FetchAiCarSpecsAsync()
    {
        if (IsFetchingAiSpecs) return;
        if (string.IsNullOrWhiteSpace(Car.Make) && string.IsNullOrWhiteSpace(Car.Model))
        {
            StatusMessage = T("StatusFirstSelectCar");
            return;
        }

        BusyMessage = T("BusyFetchingAi");
        IsFetchingAiSpecs = true;
        try
        {
            var carName = $"{Car.Year} {Car.Make} {Car.Model}".Trim();
            StatusMessage = string.Format(T("StatusAiRequested"), carName);

            var specs = await _aiCarSpecService.FetchCarSpecsAsync(carName);

            _aiEstimatedFields.Clear();
            if (specs.WheelbaseMm > 0) Car.Wheelbase = specs.WheelbaseMm;
            if (specs.FrontTrackMm > 0) Car.FrontTrack = specs.FrontTrackMm;
            if (specs.RearTrackMm > 0) Car.RearTrack = specs.RearTrackMm;
            if (specs.DragCoefficientCd > 0) Car.Cd = specs.DragCoefficientCd;
            if (specs.FrontalAreaM2 > 0) Car.FrontalAreaM2 = specs.FrontalAreaM2;
            if (specs.EstimatedFields.Contains("wheelbase_mm")) _aiEstimatedFields.Add("Wheelbase");
            if (specs.EstimatedFields.Contains("front_track_mm")) _aiEstimatedFields.Add("FrontTrack");
            if (specs.EstimatedFields.Contains("rear_track_mm")) _aiEstimatedFields.Add("RearTrack");
            if (specs.EstimatedFields.Contains("drag_coefficient_cd")) _aiEstimatedFields.Add("Cd");
            if (specs.EstimatedFields.Contains("frontal_area_m2")) _aiEstimatedFields.Add("FrontalArea");

            OnPropertyChanged(nameof(WheelbaseDisplay));
            OnPropertyChanged(nameof(FrontTrackDisplay));
            OnPropertyChanged(nameof(RearTrackDisplay));
            OnPropertyChanged(nameof(IsWheelbaseAiEstimated));
            OnPropertyChanged(nameof(IsFrontTrackAiEstimated));
            OnPropertyChanged(nameof(IsRearTrackAiEstimated));
            OnPropertyChanged(nameof(IsCdAiEstimated));
            OnPropertyChanged(nameof(IsFrontalAreaAiEstimated));

            var estimated = specs.EstimatedFields.Count > 0
                ? string.Format(T("AiSpecEstimate"), string.Join(", ", specs.EstimatedFields))
                : "";
            var rawValues = $"WB={specs.WheelbaseMm} Trk={specs.FrontTrackMm}/{specs.RearTrackMm} Cd={specs.DragCoefficientCd} A={specs.FrontalAreaM2}m²";
            StatusMessage = $"{string.Format(T("StatusAiReceived"), carName, estimated)} [{rawValues}]";
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(T("StatusAiError"), ex.Message);
        }
        finally
        {
            IsFetchingAiSpecs = false;
        }
    }

    // ── Profile management ──────────────────────────────────────────────────
    private string AutoProfileName()
    {
        var dt = T($"Enum_DriveType_{Car.DriveType}");
        var et = T($"Enum_EngineType_{Car.EngineType}");
        var disc = T($"Discipline{Track.Discipline}");
        return $"{Car.Make} {Car.Model} {Car.Year} {dt} {et} {disc}";
    }

    private void SaveProfile()
    {
        try
        {
            string name = AutoProfileName();
            Car.Name = name;
            _storage.Save(name, new SavedProfile
            {
                Car = Car, Track = Track, Constraints = Constraints, LastResult = TuneResult
            });
            RefreshProfiles();
            StatusMessage = string.Format(T("StatusProfileSaved"), name);
        }
        catch (Exception ex) { StatusMessage = string.Format(T("StatusSaveError"), ex.Message); }
    }

    private void LoadProfile()
    {
        if (SelectedProfile == null) return;
        try
        {
            var p = _storage.Load(SelectedProfile);
            if (p == null) { StatusMessage = T("StatusProfileNotFound"); return; }
            Car         = p.Car;
            Track       = p.Track;
            Constraints = p.Constraints;
            NotifyConstraintDisplayProperties();
            var loadedResult = p.LastResult;
            if (loadedResult != null) loadedResult.Car = Car;
            TuneResult  = loadedResult;
            _isLoadingProfile = true;
            SelectCarFromProfile();
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
        _storage.Delete(SelectedProfile);
        RefreshProfiles();
        StatusMessage = T("StatusProfileDeleted");
    }

    private void NewProfile()
    {
        Car = new CarCard(); Track = new TrackInfo(); Constraints = new TuningConstraints();
        NotifyConstraintDisplayProperties();
        TuneResult  = null;
        SelectedCar = null;
        StatusMessage = T("StatusProfileCreated");
    }

    private void RefreshProfiles()
    {
        Profiles = new ObservableCollection<string>(_storage.GetProfileNames());
        LoadCommand.Raise();
        DeleteProfileCommand.Raise();
    }

    // ── Car database ─────────────────────────────────────────────────────────
    private void ClearCarSelection()
    {
        SelectedCar = null;
        _car.Make = "";
        _car.Model = "";
        _car.Year = 0;
        CarSearchText = "";
    }

    private async Task LoadCarDatabaseAsync()
    {
        IsLoadingCars = true;
        RefreshCarDatabaseCommand.Raise();
        BusyMessage = T("BusyLoadingCars");
        try
        {
            var result = await _carDbService.LoadCarDatabaseAsync();
            _carDatabase = result.Cars;
            ApplyCarFilter();
            SelectCarFromProfile();

            if (result.FromCache && result.WebErrorMessage != null)
                StatusMessage = string.Format(T("StatusCarsNoConnection"), result.Cars.Count, result.WebErrorMessage);
            else if (result.FromCache)
                StatusMessage = string.Format(T("StatusCarsLoadedFromCache"), result.Cars.Count);
            else
                StatusMessage = string.Format(T("StatusCarsLoaded"), result.Cars.Count);

            if (result.FromCache && CarDatabaseService.IsCacheStale)
                _ = AutoRefreshCarDatabaseAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(T("CarsLoadingError"), ex.Message);
        }
        finally
        {
            IsLoadingCars = false;
            RefreshCarDatabaseCommand.Raise();
        }
    }

    private async Task AutoRefreshCarDatabaseAsync()
    {
        await Task.Delay(500);
        StatusMessage = T("BusyRefreshingCars");
        try
        {
            var result = await _carDbService.RefreshAsync();
            if (!result.FromCache)
            {
                _carDatabase = result.Cars;
                ApplyCarFilter();
                StatusMessage = string.Format(T("StatusDbRefreshed"), result.Cars.Count);
            }
            else if (result.WebErrorMessage != null)
            {
                StatusMessage = string.Format(T("StatusAutoUpdateFailed"), result.WebErrorMessage);
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[AutoRefresh] {ex.Message}"); }
    }

    private async void RefreshCarDatabase()
    {
        IsLoadingCars = true;
        RefreshCarDatabaseCommand.Raise();
        BusyMessage = T("BusyRefreshingCars");
        try
        {
            var result = await _carDbService.RefreshAsync();
            _carDatabase = result.Cars;
            ApplyCarFilter();

            if (result.FromCache && result.WebErrorMessage != null)
                StatusMessage = string.Format(T("StatusDbRefreshError"), result.WebErrorMessage, result.Cars.Count);
            else
                StatusMessage = string.Format(T("StatusDbRefreshed"), result.Cars.Count);
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(T("StatusDbUpdateError"), ex.Message);
        }
        finally
        {
            IsLoadingCars = false;
            RefreshCarDatabaseCommand.Raise();
        }
    }

    private void ClearCache()
    {
        CarDatabaseService.DeleteCache();
        WikiCarSpecService.DeleteCache();
        _carDatabase.Clear();
        _filteredCarDatabase.Clear();
        StatusMessage = T("StatusCacheCleared");
    }

    private void ClearAiCache()
    {
        AiCarSpecService.DeleteCache();
        StatusMessage = T("StatusCacheCleared");
    }

    private void SelectCarFromProfile()
    {
        if (string.IsNullOrEmpty(_car.Make) && string.IsNullOrEmpty(_car.Model))
        {
            SelectedCar = null;
            return;
        }
        SelectedCar = _carDatabase.FirstOrDefault(c =>
            c.Make == _car.Make && c.Model == _car.Model && c.Year == _car.Year);
    }

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
