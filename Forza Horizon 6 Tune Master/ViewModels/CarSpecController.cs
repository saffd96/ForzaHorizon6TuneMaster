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
        car.EnginePosition = EnginePositionFromPlacement(dbCar.EnginePlacementID);
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

        
        // Back-calculate body CdxA from the game's rated top speed (TopSpeedMph).
        // The mass-based CdABody Estimate fallback (CarCard.cs:271-274) overestimates
        // drag ~2x for aerodynamic cars, producing artificially low effective top
        // speeds and cascading into too-short gearing + wrong alignment/brake values.
        //
        // The raw P = ½ρCdAv³ solve attributes ALL engine power to aero drag, ignoring
        // rolling resistance (~10–20 % at top speed) and drivetrain losses (~12–15 %).
        // We apply a 0.80 correction factor to strip out these non-aero power sinks —
        // calibrated against known CdA values (GT-R real CdA≈0.56, raw solve gives
        // ~1.02; with 0.80 → 0.82, close enough for physics purposes).
        //
        // Tried lowering this to 0.55 (2026-07) after a second real reference (1985 Sprinter
        // FE, CarDbId 4162: two real in-game Mile-drag top speeds — 500 km/h @ 1065.7 HP,
        // 597 km/h @ 1599.1 HP — implied CdA≈0.43–0.48) converged closely with a tighter GT-R
        // fit. Reverted: this constant feeds CdABodyEstimate for EVERY car and discipline (it's
        // the effectiveMaxKmh anchor for Road/Rally/etc. gearing targets too, not just Drag), so
        // a ~13% roster-wide top-speed inflation to fix one car's Mile-drag figure was too broad
        // a trade-off. See CalculateDragTrapSpeedKmh's Mile distanceFactor (recalibrated to 1.65
        // against the same two Sprinter readings) for the narrower fix that was kept — it only
        // touches Drag+Mile. With 0.80 + the distanceFactor fix, the Sprinter still lands at
        // ~513 km/h (vs the true 500/597 at the two power levels) — better than the original
        // ~424 km/h, not a full match; no further general fix without per-body-kit drag data
        // the DB doesn't have (see CLAUDE.md's Sprinter-FE wheel-mass outlier for the same car).
        if (car.PowerHP > 0 && (dbCar.TopSpeedMph > 10 || dbCar.SimTopSpeed > 5))
        {
            // TopSpeedMph is the game's advertised top speed, which for many cars is capped by
            // the STOCK gearbox rather than by aerodynamics/power (e.g. an economy coupe never
            // geared to actually reach its aero-limited speed) — back-solving CdA from a
            // gearing-limited figure fabricates far too much drag. SimTopSpeed (already m/s) is
            // an independent sim-derived figure that on 636 DB cars agrees with TopSpeedMph
            // within ~2% on average, but for the gearing-limited outliers it is up to 2.4×
            // higher — i.e. closer to the true aero ceiling. Whichever of the two is LOWER is
            // the one more likely explained by a non-aero limiter (gearing, stale data), so the
            // higher of the two is the safer anchor for a body-drag solve.
            //
            // Even SimTopSpeed can share the same gearing-limited flaw when the STOCK gearbox's
            // OWN top-gear-at-redline ceiling sits close to (or below) it — that means the sim
            // never actually explored the car's real aero ceiling either, it just ran out of
            // gears too. (Confirmed against a real car: a 1985 Sprinter FE's SimTopSpeed of
            // 226 km/h sits right at its stock 6-speed's kinematic 239 km/h redline ceiling — a
            // mile-long full-upgrade build of that car reaches ~500 km/h in-game, far past what a
            // CdA solved from 226 km/h would ever predict.) Folding the stock kinematic ceiling
            // into the anchor (when it's the highest of the three) stops that case from capping
            // CdA on a limit the car never actually tested aerodynamically.
            double stockKinematicMs = ComputeStockTopGearRedlineSpeedMs(car, dbCar);
            double vMaxMs = new[] { dbCar.TopSpeedMph * 0.44704, dbCar.SimTopSpeed, stockKinematicMs }.Max(); // mph → m/s
            double powerW = car.PowerHP * PhysicsConstants.HpToWatt;
            // Solve P = ½ρCdAv³ for raw CdA, then correct for non-aero losses:
            double cdA = 2.0 * powerW / (PhysicsConstants.AirDensity * vMaxMs * vMaxMs * vMaxMs);
            cdA *= 0.80; // strip rolling-resistance + drivetrain losses
            cdA = Math.Clamp(cdA, 0.15, 3.0);
            car.Cd = cdA;
            car.FrontalAreaM2 = 1.0; // CdABodyEstimate → cdA × 1.0
        }

        car.GameDragScale = dbCar.GameDragScale > 0 ? dbCar.GameDragScale : 1.0;

        NotifyCarDisplayProperties?.Invoke();
    }

    // The stock gearbox's own top-gear speed at the stock engine's redline (car.MaxRPM is
    // already the stock figure here — PowerCalculator.Calculate ran above with no swaps
    // applied yet). Purely kinematic (gear ratio × final drive × tire circumference), no
    // aero involved — used only as a floor to detect when the DB's recorded top speed was
    // itself gearing-limited (see caller).
    private static double ComputeStockTopGearRedlineSpeedMs(CarCard car, DbCar dbCar)
    {
        if (car.MaxRPM <= 0) return 0;
        var db = Fh6DatabaseService.Instance;

        var stockDrivetrain = db.GetDrivetrainSwaps(dbCar.Id).FirstOrDefault(p => p.IsStock);
        int drivetrainId = stockDrivetrain?.DrivetrainID ?? 0;
        int driveTypeId = stockDrivetrain?.DriveTypeID ?? dbCar.DriveTypeID;

        var trans = db.GetTransmissions(drivetrainId).FirstOrDefault(p => p.IsStock);
        if (trans == null || trans.FinalDriveRatio <= 0) return 0;

        double topGear = trans.GearRatios.Take(Math.Max(trans.NumGears, 1))
            .Where(g => g > 0).DefaultIfEmpty(0).Min();
        if (topGear <= 0) return 0;

        // List_DriveType: 1 = FWD, 2 = RWD, 3 = AWD — FWD is driven by the front wheels.
        bool fwd = driveTypeId == 1;
        double diameterIn = fwd
            ? dbCar.FrontWheelDiameterIN + 2.0 * dbCar.FrontTireWidthMM * dbCar.FrontTireAspect / 100.0 / PhysicsConstants.MmPerInch
            : dbCar.RearWheelDiameterIN + 2.0 * dbCar.RearTireWidthMM * dbCar.RearTireAspect / 100.0 / PhysicsConstants.MmPerInch;
        double tireCirc = Math.PI * diameterIn * PhysicsConstants.InchToMeter;

        return car.MaxRPM * tireCirc / (60.0 * trans.FinalDriveRatio * topGear);
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

    internal static EnginePosition EnginePositionFromPlacement(int placementId) => placementId switch
    {
        1 => EnginePosition.Front,
        2 => EnginePosition.Mid,
        3 => EnginePosition.Rear,
        _ => EnginePosition.Front
    };

    private static string T(string key) => LocalizationService.Instance.T(key);
}
