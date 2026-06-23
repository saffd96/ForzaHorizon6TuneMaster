using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;

namespace Forza_Horizon_6_Tune_Master.ViewModels;

internal class CarSpecController
{
    private readonly CarDatabaseService _carDbService = new();
    private List<CarData> _carDatabase = new();
    private bool _suppressFilter;
    private CarData? _selectedCar;
    private string _carSearchText = "";
    private readonly ObservableCollection<CarData> _filteredCarDatabase = new();
    private bool _isLoadingCars;

    // Callbacks set by MainViewModel for UI updates
    public Action? NotifyCarDisplayProperties { get; set; }
    public Action? NotifyCarSelectionProperties { get; set; }
    public Action<string>? SetStatusMessage { get; set; }
    public Action? BusyFlagsChanged { get; set; }

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
    public bool HasSelectedCar => _selectedCar != null;

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

    // ── DB Populate ────────────────────────────────────────────────────────

    public void PopulateCarFromDb(CarData carData, CarCard car)
    {
        var dbCar = Fh6DatabaseService.Instance.GetCar(carData.Id);
        if (dbCar == null) return;

        car.CarDbId = dbCar.Id;
        car.Year = dbCar.Year;
        car.EnginePosition = (EnginePosition)dbCar.EnginePlacementID;
        car.PowertrainType = dbCar.AspirationTypeId == 8
            ? PowertrainType.Electric
            : PowertrainType.ICE;
        car.GearCount = dbCar.NumGears;
        car.DriveTypeID = dbCar.DriveTypeID;

        // Stock engine
        var swaps = Fh6DatabaseService.Instance.GetEngineSwaps(dbCar.Id);
        var stock = swaps.FirstOrDefault(e => e.IsStock);
        if (stock != null) car.EngineDbId = stock.EngineID;

        // Geometry: Data_CarBody in METERS → mm
        car.CarBodyId = dbCar.Id * 1000;
        var body = Fh6DatabaseService.Instance.GetCarBody(car.CarBodyId);
        if (body != null)
        {
            car.Wheelbase = body.Wheelbase * 1000;
            car.FrontTrack = body.ModelFrontTrackOuter * 1000;
            car.RearTrack = body.ModelRearTrackOuter * 1000;
        }

        // Weight
        car.WeightDistributionFront = dbCar.WeightDistribution * 100;
        car.CurbWeightKg = dbCar.CurbWeight * 100;

        // Tires
        car.FrontTireWidth = dbCar.FrontTireWidthMM;
        car.FrontTireProfile = dbCar.FrontTireAspect;
        car.StockFrontTireProfile = dbCar.FrontTireAspect;
        car.FrontRimDiameter = dbCar.FrontWheelDiameterIN;
        car.RearTireWidth = dbCar.RearTireWidthMM;
        car.RearTireProfile = dbCar.RearTireAspect;
        car.StockRearTireProfile = dbCar.RearTireAspect;
        car.RearRimDiameter = dbCar.RearWheelDiameterIN;

        PowerCalculator.Calculate(car);

        NotifyCarDisplayProperties?.Invoke();
    }

    public void SelectCar(CarData? value, CarCard car, bool isLoadingProfile)
    {
        if (_selectedCar == value) return;
        _selectedCar = value;
        if (value != null)
        {
            car.Make = value.Make;
            car.Model = value.Model;
            car.Year = value.Year;
            _suppressFilter = true;
            CarSearchText = value.DisplayName;
            _suppressFilter = false;
            ApplyCarFilter();

            if (!isLoadingProfile)
                PopulateCarFromDb(value, car);
        }
        NotifyCarSelectionProperties?.Invoke();
    }

    // ── Car database ───────────────────────────────────────────────────────

    public void ApplyCarFilter()
    {
        var query = string.IsNullOrWhiteSpace(_carSearchText)
            ? null
            : _carSearchText.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var filtered = query == null
            ? _carDatabase
            : _carDatabase.Where(c => query.All(q => c.DisplayName.ToLowerInvariant().Contains(q)));
        _filteredCarDatabase.Clear();
        foreach (var car in filtered.OrderBy(c => c.Make).ThenBy(c => c.Year))
            _filteredCarDatabase.Add(car);
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
        if (match != null)
            PopulateCarFromDb(match, car);
        _carSearchText = "";
        ApplyCarFilter();
        NotifyCarSelectionProperties?.Invoke();
    }

    public async Task LoadCarDatabaseAsync(CarCard car)
    {
        IsLoadingCars = true;
        try
        {
            var cars = await _carDbService.LoadCarDatabaseAsync();
            _carDatabase = cars;
            ApplyCarFilter();
            SelectCarFromProfile(car);
            SetStatusMessage?.Invoke(string.Format(T("StatusCarsLoaded"), cars.Count));
        }
        catch (Exception ex)
        {
            SetStatusMessage?.Invoke($"[CarDB] {ex.Message}");
        }
        finally
        {
            IsLoadingCars = false;
        }
    }

    private static string T(string key) => LocalizationService.Instance.T(key);
}
