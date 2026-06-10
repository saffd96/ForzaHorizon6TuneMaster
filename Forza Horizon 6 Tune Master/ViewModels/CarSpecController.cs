using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;

namespace Forza_Horizon_6_Tune_Master.ViewModels;

internal class CarSpecController
{
    private readonly CarDatabaseService _carDbService = new();
    private readonly WikiCarSpecService _wikiSpecService = new();
    private readonly AiCarSpecService _aiCarSpecService = new();
    private CancellationTokenSource? _wikiSpecsCts;
    private List<CarData> _carDatabase = new();
    private bool _suppressFilter;
    private CarData? _selectedCar;
    private string _carSearchText = "";
    private readonly ObservableCollection<CarData> _filteredCarDatabase = new();
    private readonly HashSet<string> _aiEstimatedFields = new();
    private bool _isSettingAiSpecs;
    private string _aiSpecStatusMessage = "";
    private bool _isFetchingAiSpecs;
    private bool _isLoadingCars;
    private bool _isLoadingCarSpecs;

    // Callbacks set by MainViewModel for UI updates
    public Action? NotifyCarDisplayProperties { get; set; }
    public Action? NotifyAiEstimatedFieldProperties { get; set; }
    public Action? NotifyCarSelectionProperties { get; set; }
    public Action? RaiseRefreshCarDbCommand { get; set; }
    public Action? RaiseFetchAiSpecsCommand { get; set; }
    public Action<string>? SetBusyMessage { get; set; }
    public Action<string>? SetStatusMessage { get; set; }
    public Func<bool>? IsLoadingProfile { get; set; }
    public Action? NotifyAiSpecStatusChanged { get; set; }

    // ── Car selection ──────────────────────────────────────────────────────

    public CarData? SelectedCar
    {
        get => _selectedCar;
        set
        {
            if (_selectedCar == value) return;
            _selectedCar = value;
        }
    }

    public string CarSearchText
    {
        get => _carSearchText;
        set
        {
            if (_carSearchText == value) return;
            _carSearchText = value;
            if (!_suppressFilter) ApplyCarFilter();
        }
    }

    public ObservableCollection<CarData> FilteredCarDatabase => _filteredCarDatabase;
    public HashSet<string> AiEstimatedFields => _aiEstimatedFields;
    public bool HasSelectedCar => _selectedCar != null;

    public Action? BusyFlagsChanged { get; set; }

    public bool IsFetchingAiSpecs
    {
        get => _isFetchingAiSpecs;
        set
        {
            if (_isFetchingAiSpecs == value) return;
            _isFetchingAiSpecs = value;
            RaiseFetchAiSpecsCommand?.Invoke();
            BusyFlagsChanged?.Invoke();
        }
    }

    public bool IsLoadingCars
    {
        get => _isLoadingCars;
        set
        {
            if (_isLoadingCars == value) return;
            _isLoadingCars = value;
            BusyFlagsChanged?.Invoke();
        }
    }

    public bool IsLoadingCarSpecs
    {
        get => _isLoadingCarSpecs;
        set
        {
            if (_isLoadingCarSpecs == value) return;
            _isLoadingCarSpecs = value;
            BusyFlagsChanged?.Invoke();
        }
    }

    public string AiSpecStatusMessage
    {
        get => _aiSpecStatusMessage;
        set
        {
            _aiSpecStatusMessage = value;
            NotifyAiSpecStatusChanged?.Invoke();
        }
    }

    public bool HasAiSpecStatus => !string.IsNullOrEmpty(_aiSpecStatusMessage);
    public bool NeedsCarSelectionHighlight { get; private set; }

    public void ClearAiSpecStatus()
    {
        NeedsCarSelectionHighlight = false;
        _aiSpecStatusMessage = "";
        NotifyAiSpecStatusChanged?.Invoke();
    }

    // ── AI-estimated field tracking ────────────────────────────────────────

    public bool IsWheelbaseAiEstimated  => _aiEstimatedFields.Contains("Wheelbase");
    public bool IsFrontTrackAiEstimated => _aiEstimatedFields.Contains("FrontTrack");
    public bool IsRearTrackAiEstimated  => _aiEstimatedFields.Contains("RearTrack");
    public bool IsCdAiEstimated         => _aiEstimatedFields.Contains("Cd");
    public bool IsFrontalAreaAiEstimated => _aiEstimatedFields.Contains("FrontalArea");
    public bool HasAnyAiEstimatedField => _aiEstimatedFields.Count > 0;

    public void ClearAiEstimatedFields()
    {
        _aiEstimatedFields.Clear();
        NotifyAiEstimatedFieldProperties?.Invoke();
    }

    public bool IsSettingAiSpecs { get => _isSettingAiSpecs; set => _isSettingAiSpecs = value; }

    public void OnCarPropertyChanged(string? propertyName)
    {
        if (!_isSettingAiSpecs && propertyName != null)
        {
            bool removed = false;
            if (propertyName == nameof(CarCard.Wheelbase) && _aiEstimatedFields.Remove("Wheelbase"))
            { removed = true; }
            else if (propertyName == nameof(CarCard.FrontTrack) && _aiEstimatedFields.Remove("FrontTrack"))
            { removed = true; }
            else if (propertyName == nameof(CarCard.RearTrack) && _aiEstimatedFields.Remove("RearTrack"))
            { removed = true; }
            else if (propertyName == nameof(CarCard.Cd) && _aiEstimatedFields.Remove("Cd"))
            { removed = true; }
            else if (propertyName == nameof(CarCard.FrontalAreaM2) && _aiEstimatedFields.Remove("FrontalArea"))
            { removed = true; }
            if (removed)
                NotifyAiEstimatedFieldProperties?.Invoke();
        }
    }

    public void SelectCar(CarData? value, CarCard car, bool isLoadingProfile)
    {
        if (_selectedCar == value) return;
        _selectedCar = value;
        if (value != null)
        {
            if (!isLoadingProfile) { ClearAiEstimatedFields(); ClearAiSpecStatus(); }
            car.Make = value.Make;
            car.Model = value.Model;
            car.Year = value.Year;
            _suppressFilter = true;
            CarSearchText = value.DisplayName;
            _suppressFilter = false;
            ApplyCarFilter();

            _wikiSpecsCts?.Cancel();
            _wikiSpecsCts?.Dispose();
            _wikiSpecsCts = new CancellationTokenSource();
            if (!isLoadingProfile)
                _ = FetchAndApplyWikiSpecsAsync(value, car, _wikiSpecsCts.Token);
        }
        NotifyCarSelectionProperties?.Invoke();
    }

    // ── Car database ───────────────────────────────────────────────────────

    public void ApplyCarFilter()
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

    public void ClearCarSelection(CarCard car)
    {
        _selectedCar = null;
        car.Make = "";
        car.Model = "";
        car.Year = 0;
        _carSearchText = "";
        ApplyCarFilter();
        NotifyCarSelectionProperties?.Invoke();
    }

    public void SelectCarFromProfile(CarCard car)
    {
        if (string.IsNullOrEmpty(car.Make) && string.IsNullOrEmpty(car.Model))
        {
            _selectedCar = null;
            NotifyCarSelectionProperties?.Invoke();
            return;
        }
        var match = _carDatabase.FirstOrDefault(c =>
            c.Make == car.Make && c.Model == car.Model && c.Year == car.Year);
        _selectedCar = match;
        _carSearchText = "";
        ApplyCarFilter();
        NotifyCarSelectionProperties?.Invoke();
    }

    public async Task LoadCarDatabaseAsync(CarCard car)
    {
        SetBusyMessage?.Invoke(T("BusyLoadingCars"));
        IsLoadingCars = true;
        RaiseRefreshCarDbCommand?.Invoke();
        try
        {
            var result = await _carDbService.LoadCarDatabaseAsync();
            _carDatabase = result.Cars;
            ApplyCarFilter();
            SelectCarFromProfile(car);

            if (result.FromCache && result.WebErrorMessage != null)
                SetStatusMessage?.Invoke(string.Format(T("StatusCarsNoConnection"), result.Cars.Count, result.WebErrorMessage));
            else if (result.FromCache)
                SetStatusMessage?.Invoke(string.Format(T("StatusCarsLoadedFromCache"), result.Cars.Count));
            else
                SetStatusMessage?.Invoke(string.Format(T("StatusCarsLoaded"), result.Cars.Count));

            if (result.FromCache && CarDatabaseService.IsCacheStale)
                _ = AutoRefreshCarDatabaseAsync();
        }
        catch (Exception ex)
        {
            SetStatusMessage?.Invoke(string.Format(T("CarsLoadingError"), ex.Message));
        }
        finally
        {
            IsLoadingCars = false;
            RaiseRefreshCarDbCommand?.Invoke();
        }
    }

    private async Task AutoRefreshCarDatabaseAsync()
    {
        await Task.Delay(500);
        SetBusyMessage?.Invoke(T("BusyRefreshingCars"));
        try
        {
            var result = await _carDbService.RefreshAsync();
            if (!result.FromCache)
            {
                _carDatabase = result.Cars;
                ApplyCarFilter();
                SetStatusMessage?.Invoke(string.Format(T("StatusDbRefreshed"), result.Cars.Count));
            }
            else if (result.WebErrorMessage != null)
            {
                SetStatusMessage?.Invoke(string.Format(T("StatusAutoUpdateFailed"), result.WebErrorMessage));
            }
        }
        catch (Exception ex) { SetStatusMessage?.Invoke($"[AutoRefresh] {ex.Message}"); }
    }

    public async Task RefreshCarDatabaseAsync()
    {
        SetBusyMessage?.Invoke(T("BusyRefreshingCars"));
        IsLoadingCars = true;
        RaiseRefreshCarDbCommand?.Invoke();
        try
        {
            var result = await _carDbService.RefreshAsync();
            _carDatabase = result.Cars;
            ApplyCarFilter();

            if (result.FromCache && result.WebErrorMessage != null)
                SetStatusMessage?.Invoke(string.Format(T("StatusDbRefreshError"), result.WebErrorMessage, result.Cars.Count));
            else
                SetStatusMessage?.Invoke(string.Format(T("StatusDbRefreshed"), result.Cars.Count));
        }
        catch (Exception ex)
        {
            SetStatusMessage?.Invoke(string.Format(T("StatusDbUpdateError"), ex.Message));
        }
        finally
        {
            IsLoadingCars = false;
            RaiseRefreshCarDbCommand?.Invoke();
        }
    }

    public void ClearCache()
    {
        CarDatabaseService.DeleteCache();
        WikiCarSpecService.DeleteCache();
        _carDatabase.Clear();
        _filteredCarDatabase.Clear();
        SetStatusMessage?.Invoke(T("StatusCacheCleared"));
    }

    public void ClearAiCache()
    {
        AiCarSpecService.DeleteCache();
        SetStatusMessage?.Invoke(T("StatusCacheCleared"));
    }

    // ── Wiki specs ─────────────────────────────────────────────────────────

    private async Task FetchAndApplyWikiSpecsAsync(CarData carData, CarCard car, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(carData.WikiPageTitle)) return;

        SetBusyMessage?.Invoke(T("BusyLoadingSpecs"));
        IsLoadingCarSpecs = true;
        try
        {
            var specs = await _wikiSpecService.FetchSpecsAsync(carData.WikiPageTitle, ct);
            if (ct.IsCancellationRequested || specs == null) return;

            if (specs.EngineType.HasValue)             car.EngineType = specs.EngineType.Value;
            if (specs.AspirationType.HasValue)         car.AspirationType = specs.AspirationType.Value;
            if (specs.PowertrainType.HasValue)         car.PowertrainType = specs.PowertrainType.Value;
            if (specs.PowerHP is > 0)                  car.PowerHP = specs.PowerHP.Value;
            if (specs.TorqueNm is > 0)                 car.TorqueNm = specs.TorqueNm.Value;
            if (specs.DriveType.HasValue)              car.DriveType = specs.DriveType.Value;
            if (specs.EnginePosition.HasValue)         car.EnginePosition = specs.EnginePosition.Value;
            if (specs.WeightDistributionFront is > 0)  car.WeightDistributionFront = specs.WeightDistributionFront.Value;
            if (specs.TotalMassKg is > 0)              car.TotalMass = specs.TotalMassKg.Value;
            if (specs.GearCount is > 0)                car.GearCount = specs.GearCount.Value;

            NotifyCarDisplayProperties?.Invoke();
            SetStatusMessage?.Invoke(string.Format(T("StatusSpecsLoaded"), carData.Make, carData.Model));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!ct.IsCancellationRequested)
                SetStatusMessage?.Invoke(string.Format(T("StatusSpecsError"), ex.Message));
        }
        finally
        {
            IsLoadingCarSpecs = false;
        }
    }

    // ── AI specs ───────────────────────────────────────────────────────────

    public async Task FetchAiCarSpecsAsync(CarCard car)
    {
        if (_isFetchingAiSpecs) return;
        ClearAiSpecStatus();
        if (string.IsNullOrWhiteSpace(car.Make) && string.IsNullOrWhiteSpace(car.Model))
        {
            NeedsCarSelectionHighlight = true;
            _aiSpecStatusMessage = T("StatusFirstSelectCar");
            NotifyAiSpecStatusChanged?.Invoke();
            SetStatusMessage?.Invoke(T("StatusFirstSelectCar"));
            return;
        }

        SetBusyMessage?.Invoke(T("BusyFetchingAi"));
        IsFetchingAiSpecs = true;
        try
        {
            var carName = $"{car.Year} {car.Make} {car.Model}".Trim();
            SetStatusMessage?.Invoke(string.Format(T("StatusAiRequested"), carName));

            var specs = await _aiCarSpecService.FetchCarSpecsAsync(carName);

            _isSettingAiSpecs = true;
            _aiEstimatedFields.Clear();
            if (specs.WheelbaseMm > 0) { car.Wheelbase = specs.WheelbaseMm; _aiEstimatedFields.Add("Wheelbase"); }
            if (specs.FrontTrackMm > 0) { car.FrontTrack = specs.FrontTrackMm; _aiEstimatedFields.Add("FrontTrack"); }
            if (specs.RearTrackMm > 0) { car.RearTrack = specs.RearTrackMm; _aiEstimatedFields.Add("RearTrack"); }
            if (specs.DragCoefficientCd > 0) { car.Cd = specs.DragCoefficientCd; _aiEstimatedFields.Add("Cd"); }
            if (specs.FrontalAreaM2 > 0) { car.FrontalAreaM2 = specs.FrontalAreaM2; _aiEstimatedFields.Add("FrontalArea"); }
            _isSettingAiSpecs = false;

            NotifyCarDisplayProperties?.Invoke();
            NotifyAiEstimatedFieldProperties?.Invoke();

            var estimated = specs.EstimatedFields.Count > 0
                ? string.Format(T("AiSpecEstimate"), string.Join(", ", specs.EstimatedFields))
                : "";
            var rawValues = $"WB={specs.WheelbaseMm} Trk={specs.FrontTrackMm}/{specs.RearTrackMm} Cd={specs.DragCoefficientCd} A={specs.FrontalAreaM2}m²";
            SetStatusMessage?.Invoke($"{string.Format(T("StatusAiReceived"), carName, estimated)} [{rawValues}]");
        }
        catch (Exception ex)
        {
            _aiSpecStatusMessage = string.Format(T("StatusAiError"), ex.Message);
            NotifyAiSpecStatusChanged?.Invoke();
            SetStatusMessage?.Invoke(string.Format(T("StatusAiError"), ex.Message));
        }
        finally
        {
            IsFetchingAiSpecs = false;
        }
    }

    private static string T(string key) => LocalizationService.Instance.T(key);
}
