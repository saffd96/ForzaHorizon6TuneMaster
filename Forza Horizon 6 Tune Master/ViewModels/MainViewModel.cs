using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    private List<CarData> _carDatabase = new();

    private bool _suppressFilter;

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
                _selectedUnitSystemItem = UnitSystemOptions.FirstOrDefault(o => o.Value == value);
                OnPropertyChanged(nameof(SelectedUnitSystemItem));
                _syncingUnitSystem = false;
            }
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
    public string TirePressureUnitLabel => UseImperial ? "ДАВЛЕНИЕ ШИН (PSI)" : "ДАВЛЕНИЕ ШИН (БАР)";
    public string SpringUnitLabel => _springUnit switch
    {
        SpringUnit.NMm   => "ПРУЖИНЫ (Н/ММ)",
        SpringUnit.LbsIn => "ПРУЖИНЫ (ФУНТ/ДЮЙМ)",
        _                => "ПРУЖИНЫ (КГС/ММ)"
    };
    public string RideHeightUnitLabel => UseImperial ? "КЛИРЕНС (ДЮЙМ)" : "КЛИРЕНС (ММ)";

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
                _selectedPowerUnitItem = PowerUnitOptions.FirstOrDefault(o => o.Value == value);
                OnPropertyChanged(nameof(SelectedPowerUnitItem));
                _syncingPowerUnit = false;
            }
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
                _selectedSpringUnitItem = SpringUnitOptions.FirstOrDefault(o => o.Value == value);
                OnPropertyChanged(nameof(SelectedSpringUnitItem));
                _syncingSpringUnit = false;
            }
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
    public string PowerFieldLabel => _powerUnit switch
    {
        PowerUnit.KW => "Мощность (кВт)",
        PowerUnit.PS => "Мощность (PS)",
        _            => "Мощность (л.с.)"
    };
    public string SpeedFieldLabel => _measurementSystem == UnitSystem.Imperial
        ? "Макс. скорость (миль/ч, расч.)" : "Макс. скорость (км/ч, расч.)";
    public string MassFieldLabel  => _measurementSystem == UnitSystem.Imperial
        ? "Полная масса (фнт)" : "Полная масса (кг)";
    public string TorqueFieldLabel => _measurementSystem == UnitSystem.Imperial
        ? "Момент (фнт·фут)" : "Момент (Нм)";
    public string WheelbaseFieldLabel => _measurementSystem == UnitSystem.Imperial
        ? "Колёсная база (дюйм)" : "Колёсная база (мм)";
    public string FrontTrackFieldLabel => _measurementSystem == UnitSystem.Imperial
        ? "Колея перед (дюйм)" : "Колея перед (мм)";
    public string RearTrackFieldLabel => _measurementSystem == UnitSystem.Imperial
        ? "Колея зад (дюйм)" : "Колея зад (мм)";
    public string MaxRPMFieldLabel => Car.PowertrainType == PowertrainType.Electric
        ? "Макс. об/мин мотора" : "Макс. об/мин";

    // ── Unit option lists + selected items for dropdowns ─────────────────────
    public List<UnitSystemOption> UnitSystemOptions { get; } = new()
    {
        new() { Value = UnitSystem.Metric, Label = "📐 Метрические" },
        new() { Value = UnitSystem.Imperial, Label = "📐 Имперские" }
    };

    public List<PowerUnitOption> PowerUnitOptions { get; } = new()
    {
        new() { Value = PowerUnit.HP, Label = "⚡ л.с." },
        new() { Value = PowerUnit.PS, Label = "⚡ PS" },
        new() { Value = PowerUnit.KW, Label = "⚡ кВт" }
    };

    public List<SpringUnitOption> SpringUnitOptions { get; } = new()
    {
        new() { Value = SpringUnit.KgfMm, Label = "Ⓜ кгс/мм" },
        new() { Value = SpringUnit.NMm, Label = "Ⓜ Н/мм" },
        new() { Value = SpringUnit.LbsIn, Label = "Ⓜ фунт/дюйм" }
    };

    private UnitSystemOption? _selectedUnitSystemItem;
    public UnitSystemOption? SelectedUnitSystemItem
    {
        get => _selectedUnitSystemItem;
        set
        {
            if (value == null || _selectedUnitSystemItem?.Value == value.Value) return;
            if (_syncingUnitSystem) return;
            _syncingUnitSystem = true;
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
            _syncingUnitSystem = false;
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
            _selectedPowerUnitItem = value;
            OnPropertyChanged();
            _powerUnit = value.Value;
            OnPropertyChanged(nameof(PowerUnit));
            OnPropertyChanged(nameof(PowerDisplay));
            OnPropertyChanged(nameof(PowerFieldLabel));
            OnPropertyChanged(nameof(PowerUnitToggleLabel));
            _syncingPowerUnit = false;
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
            _syncingSpringUnit = false;
        }
    }

    // ── Unit toggle labels ───────────────────────────────────────────────────
    public string UnitToggleLabel => _measurementSystem == UnitSystem.Imperial
        ? "📐 Имперские" : "📐 Метрические";

    public string PowerUnitToggleLabel => _powerUnit switch
    {
        PowerUnit.PS => "⚡ PS",
        PowerUnit.KW => "⚡ кВт",
        _            => "⚡ л.с."
    };

    public string SpringUnitToggleLabel => _springUnit switch
    {
        SpringUnit.NMm   => "Ⓜ Н/мм",
        SpringUnit.LbsIn => "Ⓜ фунт/дюйм",
        _                => "Ⓜ кгс/мм"
    };

    // ── Unit toggle commands ─────────────────────────────────────────────────
    public RelayCommand ToggleUnitsCommand      { get; }
    public RelayCommand TogglePowerUnitCommand  { get; }
    public RelayCommand ToggleSpringUnitCommand { get; }

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

    public bool IsBusy => _isGenerating || _isFetchingAiSpecs || _isLoadingCars;

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
        try
        {
            await Task.Delay(400, _debounceCts.Token);
            if (!_isAutoGenerate) return;
            if ((DateTime.Now - _lastInputChange).TotalMilliseconds < 350) return;
            BusyMessage = "Расчёт тюнинга...";
            IsGenerating = true;
            GenerateTune();
        }
        catch (OperationCanceledException) { /* cancelled */ }
        finally { IsGenerating = false; }
    }

    // ── Status ───────────────────────────────────────────────────────────────
    private string _statusMessage = "Готов • Заполните данные автомобиля и нажмите «Сгенерировать тюн»";
    public string StatusMessage { get => _statusMessage; set { _statusMessage = value; OnPropertyChanged(); } }

    // ── Commands ─────────────────────────────────────────────────────────────
    public RelayCommand GenerateCommand       { get; }
    public RelayCommand SaveCommand           { get; }
    public RelayCommand LoadCommand           { get; }
    public RelayCommand DeleteProfileCommand  { get; }
    public RelayCommand NewProfileCommand     { get; }
    public RelayCommand FetchAiCarSpecsCommand { get; }
    public RelayCommand ClearCarSelectionCommand { get; }
    public RelayCommand RefreshCarDatabaseCommand { get; }

    public MainViewModel()
    {
        GenerateCommand        = new RelayCommand(GenerateTune);
        SaveCommand            = new RelayCommand(SaveProfile);
        LoadCommand            = new RelayCommand(LoadProfile);
        DeleteProfileCommand   = new RelayCommand(DeleteProfile);
        NewProfileCommand      = new RelayCommand(NewProfile);
        FetchAiCarSpecsCommand = new RelayCommand(FetchAiCarSpecs, () => !IsFetchingAiSpecs);
        ClearCarSelectionCommand = new RelayCommand(ClearCarSelection);
        RefreshCarDatabaseCommand = new RelayCommand(RefreshCarDatabase, () => !IsLoadingCars);

        ToggleUnitsCommand      = new RelayCommand(DoToggleUnits);
        TogglePowerUnitCommand  = new RelayCommand(DoTogglePowerUnit);
        ToggleSpringUnitCommand = new RelayCommand(DoToggleSpringUnit);

        _car.PropertyChanged += OnModelChanged;
        _track.PropertyChanged += OnModelChanged;
        _constraints.PropertyChanged += OnModelChanged;

        _selectedUnitSystemItem  = UnitSystemOptions[0];
        _selectedPowerUnitItem   = PowerUnitOptions[0];
        _selectedSpringUnitItem  = SpringUnitOptions[1];
        OnPropertyChanged(nameof(SelectedUnitSystemItem));
        OnPropertyChanged(nameof(SelectedPowerUnitItem));
        OnPropertyChanged(nameof(SelectedSpringUnitItem));
        RefreshProfiles();
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
        BusyMessage = "Расчёт тюнинга...";
        IsGenerating = true;
        try
        {
            Car.Name = SelectedProfile ?? AutoProfileName();
            TuneResult = _generator.Generate(Car, Track, Constraints);
            StatusMessage = $"Тюнинг сгенерирован  •  {Car.Make} {Car.Model}  •  {Track.Discipline}  •  {DateTime.Now:HH:mm}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка генерации: {ex.Message}";
            MessageBox.Show(ex.ToString(), "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsGenerating = false;
        }
    }

    // ── AI car specs fetch ──────────────────────────────────────────────────
    private async void FetchAiCarSpecs()
    {
        if (IsFetchingAiSpecs) return;
        if (string.IsNullOrWhiteSpace(Car.Make) && string.IsNullOrWhiteSpace(Car.Model))
        {
            StatusMessage = "Сначала выберите автомобиль";
            return;
        }

        BusyMessage = "Запрос характеристик через AI...";
        IsFetchingAiSpecs = true;
        try
        {
            var service = new AiCarSpecService();
            var carName = $"{Car.Year} {Car.Make} {Car.Model}".Trim();
            StatusMessage = $"Запрос характеристик {carName} через AI...";

            var specs = await service.FetchCarSpecsAsync(carName);

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
                ? $" (оценено: {string.Join(", ", specs.EstimatedFields)})"
                : "";
            StatusMessage = $"Характеристики {carName} получены{estimated}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка AI: {ex.Message}";
        }
        finally
        {
            IsFetchingAiSpecs = false;
        }
    }

    // ── Profile management ──────────────────────────────────────────────────
    private string AutoProfileName() => $"{Car.Make} {Car.Model} {Car.Year} {Car.DriveType} {Car.EngineType} {Track.Discipline}";

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
            StatusMessage = $"Профиль «{name}» сохранён";
        }
        catch (Exception ex) { StatusMessage = $"Ошибка сохранения: {ex.Message}"; }
    }

    private void LoadProfile()
    {
        if (SelectedProfile == null) return;
        try
        {
            var p = _storage.Load(SelectedProfile);
            if (p == null) { StatusMessage = "Профиль не найден"; return; }
            Car         = p.Car;
            Track       = p.Track;
            Constraints = p.Constraints;
            TuneResult  = p.LastResult;
            SelectCarFromProfile();
            StatusMessage = $"Загружен профиль «{SelectedProfile}»";
        }
        catch (Exception ex) { StatusMessage = $"Ошибка загрузки: {ex.Message}"; }
    }

    private void DeleteProfile()
    {
        if (SelectedProfile == null) return;
        if (MessageBox.Show($"Удалить профиль «{SelectedProfile}»?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _storage.Delete(SelectedProfile);
        RefreshProfiles();
        StatusMessage = "Профиль удалён";
    }

    private void NewProfile()
    {
        Car = new CarCard(); Track = new TrackInfo(); Constraints = new TuningConstraints();
        TuneResult  = null;
        SelectedCar = null;
        StatusMessage = "Новый профиль создан";
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
        BusyMessage = "Загрузка списка автомобилей...";
        try
        {
            var result = await _carDbService.LoadCarDatabaseAsync();
            _carDatabase = result.Cars;
            ApplyCarFilter();
            SelectCarFromProfile();

            if (result.FromCache && result.WebErrorMessage != null)
                StatusMessage = $"Загружено {result.Cars.Count} авт. из кеша — нет соединения: {result.WebErrorMessage}";
            else if (result.FromCache)
                StatusMessage = $"Загружено {result.Cars.Count} авт. из кеша";
            else
                StatusMessage = $"Загружено {result.Cars.Count} автомобилей";

            if (result.FromCache && CarDatabaseService.IsCacheStale)
                _ = AutoRefreshCarDatabaseAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Не удалось загрузить список авто: {ex.Message}";
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
        StatusMessage = "Обновление базы автомобилей...";
        try
        {
            var result = await _carDbService.RefreshAsync();
            if (!result.FromCache)
            {
                _carDatabase = result.Cars;
                ApplyCarFilter();
                StatusMessage = $"База обновлена: {result.Cars.Count} автомобилей";
            }
            else if (result.WebErrorMessage != null)
            {
                StatusMessage = $"Авто-обновление не удалось: {result.WebErrorMessage}";
            }
        }
        catch { }
    }

    private async void RefreshCarDatabase()
    {
        IsLoadingCars = true;
        RefreshCarDatabaseCommand.Raise();
        BusyMessage = "Обновление базы автомобилей...";
        try
        {
            var result = await _carDbService.RefreshAsync();
            _carDatabase = result.Cars;
            ApplyCarFilter();

            if (result.FromCache && result.WebErrorMessage != null)
                StatusMessage = $"Не удалось обновить: {result.WebErrorMessage}. Используется кеш ({result.Cars.Count} авт.)";
            else
                StatusMessage = $"База обновлена: {result.Cars.Count} автомобилей";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка обновления: {ex.Message}";
        }
        finally
        {
            IsLoadingCars = false;
            RefreshCarDatabaseCommand.Raise();
        }
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

public class UnitSystemOption
{
    public UnitSystem Value { get; set; }
    public string Label { get; set; } = "";
    public override string ToString() => Label;
}

public class PowerUnitOption
{
    public PowerUnit Value { get; set; }
    public string Label { get; set; } = "";
    public override string ToString() => Label;
}

public class SpringUnitOption
{
    public SpringUnit Value { get; set; }
    public string Label { get; set; } = "";
    public override string ToString() => Label;
}
