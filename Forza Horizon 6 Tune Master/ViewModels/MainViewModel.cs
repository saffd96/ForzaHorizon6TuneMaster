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
            ExportTextCommand?.Raise();
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
    public RelayCommand ExportTextCommand     { get; }
    public RelayCommand ImportTextCommand     { get; }
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
        ExportTextCommand      = new RelayCommand(ExportText, () => HasResult);
        ImportTextCommand      = new RelayCommand(ImportText);
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

    // ── Export / Import as text ─────────────────────────────────────────────
    private static readonly string ImportDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "ForzaTuneMaster");

    private void ExportText()
    {
        if (TuneResult == null) return;
        try
        {
            var sb = new StringBuilder();
            var savedCulture = System.Threading.Thread.CurrentThread.CurrentCulture;
            System.Threading.Thread.CurrentThread.CurrentCulture =
                System.Globalization.CultureInfo.InvariantCulture;
            try
            {
            sb.AppendLine();

            sb.AppendLine("[Car]");
            sb.AppendLine($"Make={Car.Make}");
            sb.AppendLine($"Model={Car.Model}");
            sb.AppendLine($"Year={Car.Year}");
            sb.AppendLine($"TotalMass={Car.TotalMass}");
            sb.AppendLine($"WeightDistributionFront={Car.WeightDistributionFront}");
            sb.AppendLine($"PowerHP={Car.PowerHP}");
            sb.AppendLine($"TorqueNm={Car.TorqueNm}");
            sb.AppendLine($"MaxRPM={Car.MaxRPM}");
            sb.AppendLine($"AspirationType={Car.AspirationType}");
            sb.AppendLine($"EngineType={Car.EngineType}");
            sb.AppendLine($"EnginePosition={Car.EnginePosition}");
            sb.AppendLine($"DriveType={Car.DriveType}");
            sb.AppendLine($"GearCount={Car.GearCount}");
            sb.AppendLine($"FrontTireWidth={Car.FrontTireWidth}");
            sb.AppendLine($"FrontTireProfile={Car.FrontTireProfile}");
            sb.AppendLine($"RearTireWidth={Car.RearTireWidth}");
            sb.AppendLine($"RearTireProfile={Car.RearTireProfile}");
            sb.AppendLine($"FrontRimDiameter={Car.FrontRimDiameter}");
            sb.AppendLine($"RearRimDiameter={Car.RearRimDiameter}");
            sb.AppendLine($"TireType={Car.TireType}");
            sb.AppendLine($"Wheelbase={Car.Wheelbase}");
            sb.AppendLine($"FrontTrack={Car.FrontTrack}");
            sb.AppendLine($"RearTrack={Car.RearTrack}");
            sb.AppendLine($"MaxSpeedKmh={Car.MaxSpeedKmh:F0}"); // computed from physics
            sb.AppendLine($"HasFrontAero={Car.HasFrontAero}");
            sb.AppendLine($"HasRearAero={Car.HasRearAero}");
            sb.AppendLine($"SuspensionUpgrade={Car.SuspensionUpgrade}");
            sb.AppendLine($"DifferentialUpgrade={Car.DifferentialUpgrade}");
            sb.AppendLine($"BrakesUpgrade={Car.BrakesUpgrade}");
            sb.AppendLine();

            sb.AppendLine("[Track]");
            sb.AppendLine($"Discipline={Track.Discipline}");
            sb.AppendLine($"DragDistance={Track.DragDistance}");
            sb.AppendLine();

            sb.AppendLine("[Constraints]");
            foreach (var p in typeof(TuningConstraints).GetProperties())
                sb.AppendLine($"{p.Name}={p.GetValue(Constraints)}");
            sb.AppendLine();

            sb.AppendLine("[Result]");
            sb.AppendLine($"TirePressureFront={TuneResult.TirePressureFront}");
            sb.AppendLine($"TirePressureRear={TuneResult.TirePressureRear}");
            sb.AppendLine($"CamberFront={TuneResult.CamberFront}");
            sb.AppendLine($"CamberRear={TuneResult.CamberRear}");
            sb.AppendLine($"ToeFront={TuneResult.ToeFront}");
            sb.AppendLine($"ToeRear={TuneResult.ToeRear}");
            sb.AppendLine($"Caster={TuneResult.Caster}");
            sb.AppendLine($"ARBFront={TuneResult.ARBFront}");
            sb.AppendLine($"ARBRear={TuneResult.ARBRear}");
            sb.AppendLine($"SpringFront={TuneResult.SpringFront}");
            sb.AppendLine($"SpringRear={TuneResult.SpringRear}");
            sb.AppendLine($"RideHeightFront={TuneResult.RideHeightFront}");
            sb.AppendLine($"RideHeightRear={TuneResult.RideHeightRear}");
            sb.AppendLine($"ReboundFront={TuneResult.ReboundFront}");
            sb.AppendLine($"ReboundRear={TuneResult.ReboundRear}");
            sb.AppendLine($"BumpFront={TuneResult.BumpFront}");
            sb.AppendLine($"BumpRear={TuneResult.BumpRear}");
            sb.AppendLine($"AeroFront={TuneResult.AeroFront}");
            sb.AppendLine($"AeroRear={TuneResult.AeroRear}");
            sb.AppendLine($"DiffAccel={TuneResult.DiffAccel}");
            sb.AppendLine($"DiffDecel={TuneResult.DiffDecel}");
            sb.AppendLine($"DiffFrontAccel={TuneResult.DiffFrontAccel}");
            sb.AppendLine($"DiffFrontDecel={TuneResult.DiffFrontDecel}");
            sb.AppendLine($"CenterDiffBias={TuneResult.CenterDiffBias}");
            sb.AppendLine($"BrakeBalance={TuneResult.BrakeBalance}");
            sb.AppendLine($"BrakePressure={TuneResult.BrakePressure}");
            sb.AppendLine($"FinalDrive={TuneResult.FinalDrive}");
            sb.AppendLine($"GearRatios={string.Join(";", TuneResult.GearRatios)}");

            sb.AppendLine();

            sb.AppendLine("── Пояснения ──────────────────────────────────────");
            foreach (var kv in TuneResult.Explanations)
                sb.AppendLine($"{kv.Key}: {kv.Value}");
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = savedCulture;
            }

            var dlg = new SaveFileDialog
            {
                Title = "Экспорт тюнинга",
                Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*",
                FileName = $"{AutoProfileName().Replace(' ', '_')}.txt",
                InitialDirectory = ImportDir
            };
            if (dlg.ShowDialog() == true)
            {
                File.WriteAllText(dlg.FileName, sb.ToString());
                StatusMessage = $"Тюнинг экспортирован: {dlg.FileName}";
            }
        }
        catch (Exception ex) { StatusMessage = $"Ошибка экспорта: {ex.Message}"; }
    }

    private void ImportText()
    {
        try
        {
            var dlg = new OpenFileDialog
            {
                Title = "Импорт автомобиля и ограничений",
                Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*",
                InitialDirectory = ImportDir
            };
            if (dlg.ShowDialog() != true) return;

            string text = File.ReadAllText(dlg.FileName);

            var lines = text.Split('\n', '\r')
                .Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("─") && !l.StartsWith("═") && !l.StartsWith("──"))
                .ToList();

            string section = "";
            var parsedCar = new CarCard();
            var parsedConstraints = new TuningConstraints();
            var parsedTrack = new TrackInfo();
            bool hasCar = false, hasConstraints = false, hasTrack = false;

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    section = line[1..^1].Trim();
                    continue;
                }

                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                string key = line[..eq].Trim();
                string val = line[(eq + 1)..].Trim();

                switch (section)
                {
                    case "Car":
                        SetCarProperty(parsedCar, key, val);
                        hasCar = true;
                        break;
                    case "Constraints":
                        SetConstraintProperty(parsedConstraints, key, val);
                        hasConstraints = true;
                        break;
                    case "Track":
                        SetTrackProperty(parsedTrack, key, val);
                        hasTrack = true;
                        break;
                }
            }

            if (!hasCar && !hasConstraints)
            { StatusMessage = "Не удалось распознать данные автомобиля/ограничений в файле"; return; }

            if (hasCar) Car = parsedCar;
            if (hasConstraints) Constraints = parsedConstraints;
            if (hasTrack) Track = parsedTrack;

            GenerateTune();

            StatusMessage = $"Импортировано: {Path.GetFileName(dlg.FileName)} • тюнинг пересчитан";
        }
        catch (Exception ex) { StatusMessage = $"Ошибка импорта: {ex.Message}"; }
    }

    private static void SetCarProperty(CarCard car, string key, string val)
    {
        switch (key)
        {
            case "Make":              car.Make = val; break;
            case "Model":             car.Model = val; break;
            case "Year":              car.Year = SafeInt(val, 2024); break;
            case "TotalMass":         car.TotalMass = SafeDouble(val, 1400); break;
            case "WeightDistributionFront": car.WeightDistributionFront = SafeDouble(val, 50); break;
            case "PowerHP":           car.PowerHP = SafeDouble(val, 300); break;
            case "TorqueNm":          car.TorqueNm = SafeDouble(val, 400); break;
            case "MaxRPM":            car.MaxRPM = SafeInt(val, 7000); break;
            case "AspirationType":    car.AspirationType = SafeEnum<AspirationType>(val); break;
            case "EngineType":        car.EngineType = SafeEnum<EngineType>(val); break;
            case "EnginePosition":    car.EnginePosition = SafeEnum<EnginePosition>(val); break;
            case "DriveType":         car.DriveType = SafeEnum<Models.DriveType>(val); break;
            case "GearCount":         car.GearCount = SafeInt(val, 6); break;
            case "FrontTireWidth":    car.FrontTireWidth = SafeInt(val, 235); break;
            case "FrontTireProfile":  car.FrontTireProfile = SafeInt(val, 40); break;
            case "RearTireWidth":     car.RearTireWidth = SafeInt(val, 265); break;
            case "RearTireProfile":   car.RearTireProfile = SafeInt(val, 35); break;
            case "FrontRimDiameter":  car.FrontRimDiameter = SafeInt(val, 19); break;
            case "RearRimDiameter":   car.RearRimDiameter = SafeInt(val, 19); break;
            case "TireType":          car.TireType = SafeEnum<TireType>(val); break;
            case "Wheelbase":         car.Wheelbase = SafeDouble(val, 2700); break;
            case "FrontTrack":        car.FrontTrack = SafeDouble(val, 1550); break;
            case "RearTrack":         car.RearTrack = SafeDouble(val, 1570); break;
            case "MaxSpeedKmh":       /* legacy: now computed from physics */ break;
            case "HasFrontAero":      car.HasFrontAero = SafeBool(val); break;
            case "HasRearAero":       car.HasRearAero = SafeBool(val); break;
            case "SuspensionUpgrade": car.SuspensionUpgrade = SafeEnum<SuspensionUpgrade>(val); break;
            case "DifferentialUpgrade": car.DifferentialUpgrade = SafeEnum<DifferentialUpgrade>(val); break;
            case "BrakesUpgrade":     car.BrakesUpgrade = SafeEnum<BrakesUpgrade>(val); break;
        }
    }

    private static void SetTrackProperty(TrackInfo track, string key, string val)
    {
        switch (key)
        {
            case "Discipline":   track.Discipline = SafeEnum<Discipline>(val); break;
            case "DragDistance": track.DragDistance = SafeEnum<DragDistance>(val); break;
        }
    }

    private static void SetConstraintProperty(TuningConstraints c, string key, string val)
    {
        switch (key)
        {
            case "TirePressureFrontMin":  c.TirePressureFrontMin = SafeDouble(val); break;
            case "TirePressureFrontMax":  c.TirePressureFrontMax = SafeDouble(val); break;
            case "TirePressureRearMin":   c.TirePressureRearMin = SafeDouble(val); break;
            case "TirePressureRearMax":   c.TirePressureRearMax = SafeDouble(val); break;
            case "CamberFrontMin":        c.CamberFrontMin = SafeDouble(val); break;
            case "CamberFrontMax":        c.CamberFrontMax = SafeDouble(val); break;
            case "CamberRearMin":         c.CamberRearMin = SafeDouble(val); break;
            case "CamberRearMax":         c.CamberRearMax = SafeDouble(val); break;
            case "ToeFrontMin":           c.ToeFrontMin = SafeDouble(val); break;
            case "ToeFrontMax":           c.ToeFrontMax = SafeDouble(val); break;
            case "ToeRearMin":            c.ToeRearMin = SafeDouble(val); break;
            case "ToeRearMax":            c.ToeRearMax = SafeDouble(val); break;
            case "CasterMin":             c.CasterMin = SafeDouble(val); break;
            case "CasterMax":             c.CasterMax = SafeDouble(val); break;
            case "ARBFrontMin":           c.ARBFrontMin = SafeDouble(val); break;
            case "ARBFrontMax":           c.ARBFrontMax = SafeDouble(val); break;
            case "ARBRearMin":            c.ARBRearMin = SafeDouble(val); break;
            case "ARBRearMax":            c.ARBRearMax = SafeDouble(val); break;
            case "SpringFrontMin":        c.SpringFrontMin = SafeDouble(val); break;
            case "SpringFrontMax":        c.SpringFrontMax = SafeDouble(val); break;
            case "SpringRearMin":         c.SpringRearMin = SafeDouble(val); break;
            case "SpringRearMax":         c.SpringRearMax = SafeDouble(val); break;
            case "RideHeightFrontMin":    c.RideHeightFrontMin = SafeDouble(val); break;
            case "RideHeightFrontMax":    c.RideHeightFrontMax = SafeDouble(val); break;
            case "RideHeightRearMin":     c.RideHeightRearMin = SafeDouble(val); break;
            case "RideHeightRearMax":     c.RideHeightRearMax = SafeDouble(val); break;
            case "ReboundFrontMin":       c.ReboundFrontMin = SafeDouble(val); break;
            case "ReboundFrontMax":       c.ReboundFrontMax = SafeDouble(val); break;
            case "ReboundRearMin":        c.ReboundRearMin = SafeDouble(val); break;
            case "ReboundRearMax":        c.ReboundRearMax = SafeDouble(val); break;
            case "BumpFrontMin":          c.BumpFrontMin = SafeDouble(val); break;
            case "BumpFrontMax":          c.BumpFrontMax = SafeDouble(val); break;
            case "BumpRearMin":           c.BumpRearMin = SafeDouble(val); break;
            case "BumpRearMax":           c.BumpRearMax = SafeDouble(val); break;
            case "AeroFrontMin":          c.AeroFrontMin = SafeDouble(val); break;
            case "AeroFrontMax":          c.AeroFrontMax = SafeDouble(val); break;
            case "AeroRearMin":           c.AeroRearMin = SafeDouble(val); break;
            case "AeroRearMax":           c.AeroRearMax = SafeDouble(val); break;
            case "DiffAccelMin":          c.DiffAccelMin = SafeDouble(val); break;
            case "DiffAccelMax":          c.DiffAccelMax = SafeDouble(val); break;
            case "DiffDecelMin":          c.DiffDecelMin = SafeDouble(val); break;
            case "DiffDecelMax":          c.DiffDecelMax = SafeDouble(val); break;
            case "CenterDiffBias":        c.CenterDiffBias = SafeDouble(val); break;
            case "BrakeBalanceMin":       c.BrakeBalanceMin = SafeDouble(val); break;
            case "BrakeBalanceMax":       c.BrakeBalanceMax = SafeDouble(val); break;
            case "BrakePressureMin":      c.BrakePressureMin = SafeDouble(val); break;
            case "BrakePressureMax":      c.BrakePressureMax = SafeDouble(val); break;
            case "FinalDriveMin":         c.FinalDriveMin = SafeDouble(val); break;
            case "FinalDriveMax":         c.FinalDriveMax = SafeDouble(val); break;
        }
    }

    private static void SetResultProperty(TuneResult r, string key, string val)
    {
        switch (key)
        {
            case "TirePressureFront":  r.TirePressureFront = SafeDouble(val); break;
            case "TirePressureRear":   r.TirePressureRear = SafeDouble(val); break;
            case "CamberFront":        r.CamberFront = SafeDouble(val); break;
            case "CamberRear":         r.CamberRear = SafeDouble(val); break;
            case "ToeFront":           r.ToeFront = SafeDouble(val); break;
            case "ToeRear":            r.ToeRear = SafeDouble(val); break;
            case "Caster":             r.Caster = SafeDouble(val); break;
            case "ARBFront":           r.ARBFront = SafeDouble(val); break;
            case "ARBRear":            r.ARBRear = SafeDouble(val); break;
            case "SpringFront":        r.SpringFront = SafeDouble(val); break;
            case "SpringRear":         r.SpringRear = SafeDouble(val); break;
            case "RideHeightFront":    r.RideHeightFront = SafeDouble(val); break;
            case "RideHeightRear":     r.RideHeightRear = SafeDouble(val); break;
            case "ReboundFront":       r.ReboundFront = SafeDouble(val); break;
            case "ReboundRear":        r.ReboundRear = SafeDouble(val); break;
            case "BumpFront":          r.BumpFront = SafeDouble(val); break;
            case "BumpRear":           r.BumpRear = SafeDouble(val); break;
            case "AeroFront":          r.AeroFront = SafeDouble(val); break;
            case "AeroRear":           r.AeroRear = SafeDouble(val); break;
            case "DiffAccel":          r.DiffAccel = SafeDouble(val); break;
            case "DiffDecel":          r.DiffDecel = SafeDouble(val); break;
            case "DiffFrontAccel":     r.DiffFrontAccel = SafeDoubleOrNull(val); break;
            case "DiffFrontDecel":     r.DiffFrontDecel = SafeDoubleOrNull(val); break;
            case "CenterDiffBias":     r.CenterDiffBias = SafeDoubleOrNull(val); break;
            case "BrakeBalance":       r.BrakeBalance = SafeDouble(val); break;
            case "BrakePressure":      r.BrakePressure = SafeDouble(val); break;
            case "FinalDrive":         r.FinalDrive = SafeDouble(val); break;
            case "GearRatios":         r.GearRatios = val.Split(';').Select(v => SafeDouble(v)).ToList(); break;
        }
    }

    // ── Safe parsing helpers ──────────────────────────────────────────────────
    private static int SafeInt(string s, int def = 0)
        => int.TryParse(s, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out int r) ? r : def;

    private static double SafeDouble(string s, double def = 0)
    {
        if (double.TryParse(s, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double r))
            return r;
        if (double.TryParse(s, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.CurrentCulture, out r))
            return r;
        return def;
    }

    private static double? SafeDoubleOrNull(string s)
    {
        if (double.TryParse(s, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double r))
            return r;
        if (double.TryParse(s, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.CurrentCulture, out r))
            return r;
        return null;
    }

    private static bool SafeBool(string s)
        => bool.TryParse(s, out bool r) && r;

    private static T SafeEnum<T>(string s) where T : struct
        => Enum.TryParse<T>(s, true, out T r) ? r : default;

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
