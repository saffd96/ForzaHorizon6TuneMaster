using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Forza_Horizon_6_Tune_Master.Services;

namespace Forza_Horizon_6_Tune_Master.Models;

public class TuningConstraints : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ── Tire Pressure ────────────────────────────────────────────────────
    // Single source of truth for the tire-pressure slider range (front and rear share it).
    public const double TirePressureAbsoluteMin = 1.0;
    public const double TirePressureAbsoluteMax = 3.8;
    private double _tirePressureFrontMin = TirePressureAbsoluteMin;
    private double _tirePressureFrontMax = TirePressureAbsoluteMax;
    [JsonPropertyOrder(1)] public double TirePressureFrontMin { get => _tirePressureFrontMin; set { if (SetMin(ref _tirePressureFrontMin, ref _tirePressureFrontMax, value)) { Raise(); Raise(nameof(TirePressureFrontMax)); } } }
    [JsonPropertyOrder(0)] public double TirePressureFrontMax { get => _tirePressureFrontMax; set { if (SetMax(ref _tirePressureFrontMin, ref _tirePressureFrontMax, value)) { Raise(); Raise(nameof(TirePressureFrontMin)); } } }
    private double _tirePressureRearMin = TirePressureAbsoluteMin;
    private double _tirePressureRearMax = TirePressureAbsoluteMax;
    public double TirePressureRearMin { get => _tirePressureRearMin; set { if (SetMin(ref _tirePressureRearMin, ref _tirePressureRearMax, value)) { Raise(); Raise(nameof(TirePressureRearMax)); } } }
    public double TirePressureRearMax { get => _tirePressureRearMax; set { if (SetMax(ref _tirePressureRearMin, ref _tirePressureRearMax, value)) { Raise(); Raise(nameof(TirePressureRearMin)); } } }

    // ── Camber ──────────────────────────────────────────────────────────
    private double _camberFrontMin = -5;
    private double _camberFrontMax = 5;
    public double CamberFrontMin { get => _camberFrontMin; set { if (SetMin(ref _camberFrontMin, ref _camberFrontMax, value)) { Raise(); Raise(nameof(CamberFrontMax)); } } }
    public double CamberFrontMax { get => _camberFrontMax; set { if (SetMax(ref _camberFrontMin, ref _camberFrontMax, value)) { Raise(); Raise(nameof(CamberFrontMin)); } } }
    private double _camberRearMin = -5;
    private double _camberRearMax = 5;
    public double CamberRearMin { get => _camberRearMin; set { if (SetMin(ref _camberRearMin, ref _camberRearMax, value)) { Raise(); Raise(nameof(CamberRearMax)); } } }
    public double CamberRearMax { get => _camberRearMax; set { if (SetMax(ref _camberRearMin, ref _camberRearMax, value)) { Raise(); Raise(nameof(CamberRearMin)); } } }

    // ── Toe ──────────────────────────────────────────────────────────────
    private double _toeFrontMin = -1;
    private double _toeFrontMax = 1;
    public double ToeFrontMin { get => _toeFrontMin; set { if (SetMin(ref _toeFrontMin, ref _toeFrontMax, value)) { Raise(); Raise(nameof(ToeFrontMax)); } } }
    public double ToeFrontMax { get => _toeFrontMax; set { if (SetMax(ref _toeFrontMin, ref _toeFrontMax, value)) { Raise(); Raise(nameof(ToeFrontMin)); } } }
    private double _toeRearMin = -1;
    private double _toeRearMax = 1;
    public double ToeRearMin { get => _toeRearMin; set { if (SetMin(ref _toeRearMin, ref _toeRearMax, value)) { Raise(); Raise(nameof(ToeRearMax)); } } }
    public double ToeRearMax { get => _toeRearMax; set { if (SetMax(ref _toeRearMin, ref _toeRearMax, value)) { Raise(); Raise(nameof(ToeRearMin)); } } }

    // ── Caster ──────────────────────────────────────────────────────────
    private double _casterMin = 1.0;
    private double _casterMax = 7.0;
    public double CasterMin { get => _casterMin; set { if (SetMin(ref _casterMin, ref _casterMax, value)) { Raise(); Raise(nameof(CasterMax)); } } }
    public double CasterMax { get => _casterMax; set { if (SetMax(ref _casterMin, ref _casterMax, value)) { Raise(); Raise(nameof(CasterMin)); } } }

    // ── ARB ──────────────────────────────────────────────────────────
    private double _arbFrontMin;
    private double _arbFrontMax = 100;
    public double ARBFrontMin { get => _arbFrontMin; set { if (SetMin(ref _arbFrontMin, ref _arbFrontMax, value)) { Raise(); Raise(nameof(ARBFrontMax)); } } }
    public double ARBFrontMax { get => _arbFrontMax; set { if (SetMax(ref _arbFrontMin, ref _arbFrontMax, value)) { Raise(); Raise(nameof(ARBFrontMin)); } } }
    private double _arbRearMin;
    private double _arbRearMax = 100;
    public double ARBRearMin { get => _arbRearMin; set { if (SetMin(ref _arbRearMin, ref _arbRearMax, value)) { Raise(); Raise(nameof(ARBRearMax)); } } }
    public double ARBRearMax { get => _arbRearMax; set { if (SetMax(ref _arbRearMin, ref _arbRearMax, value)) { Raise(); Raise(nameof(ARBRearMin)); } } }

    // ── Spring ──────────────────────────────────────────────────────────
    // Bounds are in N/mm (the canonical spring unit). DB spring rates span ~20-200 N/mm, so
    // these defaults are a wide safety band; per-car bounds come from ApplyPhysicsBounds.
    private double _springFrontMin = 10;
    private double _springFrontMax = 300;
    public double SpringFrontMin { get => _springFrontMin; set { if (SetMin(ref _springFrontMin, ref _springFrontMax, value)) { Raise(); Raise(nameof(SpringFrontMax)); } } }
    public double SpringFrontMax { get => _springFrontMax; set { if (SetMax(ref _springFrontMin, ref _springFrontMax, value)) { Raise(); Raise(nameof(SpringFrontMin)); } } }
    private double _springRearMin = 10;
    private double _springRearMax = 300;
    public double SpringRearMin { get => _springRearMin; set { if (SetMin(ref _springRearMin, ref _springRearMax, value)) { Raise(); Raise(nameof(SpringRearMax)); } } }
    public double SpringRearMax { get => _springRearMax; set { if (SetMax(ref _springRearMin, ref _springRearMax, value)) { Raise(); Raise(nameof(SpringRearMin)); } } }

    // ── Ride Height ──────────────────────────────────────────────────────
    private double _rideHeightFrontMin = 50.0;
    private double _rideHeightFrontMax = 250.0;
    public double RideHeightFrontMin { get => _rideHeightFrontMin; set { if (SetMin(ref _rideHeightFrontMin, ref _rideHeightFrontMax, value)) { Raise(); Raise(nameof(RideHeightFrontMax)); } } }
    public double RideHeightFrontMax { get => _rideHeightFrontMax; set { if (SetMax(ref _rideHeightFrontMin, ref _rideHeightFrontMax, value)) { Raise(); Raise(nameof(RideHeightFrontMin)); } } }
    private double _rideHeightRearMin = 10;
    private double _rideHeightRearMax = 400;
    public double RideHeightRearMin { get => _rideHeightRearMin; set { if (SetMin(ref _rideHeightRearMin, ref _rideHeightRearMax, value)) { Raise(); Raise(nameof(RideHeightRearMax)); } } }
    public double RideHeightRearMax { get => _rideHeightRearMax; set { if (SetMax(ref _rideHeightRearMin, ref _rideHeightRearMax, value)) { Raise(); Raise(nameof(RideHeightRearMin)); } } }

    // ── Rebound ───────────────────────────────────────────────────────
    private double _reboundFrontMin;
    private double _reboundFrontMax = 30;
    public double ReboundFrontMin { get => _reboundFrontMin; set { if (SetMin(ref _reboundFrontMin, ref _reboundFrontMax, value)) { Raise(); Raise(nameof(ReboundFrontMax)); } } }
    public double ReboundFrontMax { get => _reboundFrontMax; set { if (SetMax(ref _reboundFrontMin, ref _reboundFrontMax, value)) { Raise(); Raise(nameof(ReboundFrontMin)); } } }
    private double _reboundRearMin;
    private double _reboundRearMax = 30;
    public double ReboundRearMin { get => _reboundRearMin; set { if (SetMin(ref _reboundRearMin, ref _reboundRearMax, value)) { Raise(); Raise(nameof(ReboundRearMax)); } } }
    public double ReboundRearMax { get => _reboundRearMax; set { if (SetMax(ref _reboundRearMin, ref _reboundRearMax, value)) { Raise(); Raise(nameof(ReboundRearMin)); } } }

    // ── Bump ─────────────────────────────────────────────────────────
    private double _bumpFrontMin;
    private double _bumpFrontMax = 30;
    public double BumpFrontMin { get => _bumpFrontMin; set { if (SetMin(ref _bumpFrontMin, ref _bumpFrontMax, value)) { Raise(); Raise(nameof(BumpFrontMax)); } } }
    public double BumpFrontMax { get => _bumpFrontMax; set { if (SetMax(ref _bumpFrontMin, ref _bumpFrontMax, value)) { Raise(); Raise(nameof(BumpFrontMin)); } } }
    private double _bumpRearMin;
    private double _bumpRearMax = 30;
    public double BumpRearMin { get => _bumpRearMin; set { if (SetMin(ref _bumpRearMin, ref _bumpRearMax, value)) { Raise(); Raise(nameof(BumpRearMax)); } } }
    public double BumpRearMax { get => _bumpRearMax; set { if (SetMax(ref _bumpRearMin, ref _bumpRearMax, value)) { Raise(); Raise(nameof(BumpRearMin)); } } }

    // ── Aero ────────────────────────────────────────────────────────
    private double _aeroFrontMin;
    private double _aeroFrontMax = 300;
    public double AeroFrontMin { get => _aeroFrontMin; set { if (SetMin(ref _aeroFrontMin, ref _aeroFrontMax, value)) { Raise(); Raise(nameof(AeroFrontMax)); } } }
    public double AeroFrontMax { get => _aeroFrontMax; set { if (SetMax(ref _aeroFrontMin, ref _aeroFrontMax, value)) { Raise(); Raise(nameof(AeroFrontMin)); } } }
    private double _aeroRearMin;
    private double _aeroRearMax = 300;
    public double AeroRearMin { get => _aeroRearMin; set { if (SetMin(ref _aeroRearMin, ref _aeroRearMax, value)) { Raise(); Raise(nameof(AeroRearMax)); } } }
    public double AeroRearMax { get => _aeroRearMax; set { if (SetMax(ref _aeroRearMin, ref _aeroRearMax, value)) { Raise(); Raise(nameof(AeroRearMin)); } } }

    // ── Differential ──────────────────────────────────────────────────
    private double _diffAccelMin;
    private double _diffAccelMax = 100;
    public double DiffAccelMin { get => _diffAccelMin; set { if (SetMin(ref _diffAccelMin, ref _diffAccelMax, value)) { Raise(); Raise(nameof(DiffAccelMax)); } } }
    public double DiffAccelMax { get => _diffAccelMax; set { if (SetMax(ref _diffAccelMin, ref _diffAccelMax, value)) { Raise(); Raise(nameof(DiffAccelMin)); } } }
    private double _diffDecelMin;
    private double _diffDecelMax = 100;
    public double DiffDecelMin { get => _diffDecelMin; set { if (SetMin(ref _diffDecelMin, ref _diffDecelMax, value)) { Raise(); Raise(nameof(DiffDecelMax)); } } }
    public double DiffDecelMax { get => _diffDecelMax; set { if (SetMax(ref _diffDecelMin, ref _diffDecelMax, value)) { Raise(); Raise(nameof(DiffDecelMin)); } } }

    // ── Brake ────────────────────────────────────────────────────────
    private double _brakeBalanceMin = 20;
    private double _brakeBalanceMax = 80;
    public double BrakeBalanceMin { get => _brakeBalanceMin; set { if (SetMin(ref _brakeBalanceMin, ref _brakeBalanceMax, value)) { Raise(); Raise(nameof(BrakeBalanceMax)); } } }
    public double BrakeBalanceMax { get => _brakeBalanceMax; set { if (SetMax(ref _brakeBalanceMin, ref _brakeBalanceMax, value)) { Raise(); Raise(nameof(BrakeBalanceMin)); } } }
    private double _brakePressureMin = 20;
    private double _brakePressureMax = 300;
    public double BrakePressureMin { get => _brakePressureMin; set { if (SetMin(ref _brakePressureMin, ref _brakePressureMax, value)) { Raise(); Raise(nameof(BrakePressureMax)); } } }
    public double BrakePressureMax { get => _brakePressureMax; set { if (SetMax(ref _brakePressureMin, ref _brakePressureMax, value)) { Raise(); Raise(nameof(BrakePressureMin)); } } }

    // ── Final Drive ───────────────────────────────────────────────────
    private double _finalDriveMin = 2.2;
    private double _finalDriveMax = 6.1;
    public double FinalDriveMin { get => _finalDriveMin; set { if (value < 0.1) value = 0.1; if (SetMin(ref _finalDriveMin, ref _finalDriveMax, value)) { Raise(); Raise(nameof(FinalDriveMax)); } } }
    public double FinalDriveMax { get => _finalDriveMax; set { if (SetMax(ref _finalDriveMin, ref _finalDriveMax, value)) { Raise(); Raise(nameof(FinalDriveMin)); } } }

    // ── Gearing behaviour ─────────────────────────────────────────────
    private bool _forceRecommendedGears;
    [JsonPropertyOrder(99)]
    public bool ForceRecommendedGears { get => _forceRecommendedGears; set { if (_forceRecommendedGears == value) return; _forceRecommendedGears = value; Raise(); } }

    // ── Center Diff Bias ──────────────────────────────────────────────
    public double CenterDiffBias { get; set; } = 50;

    // ── Driving style preferences ──────────────────────────────────────
    // -1 = max grip/stability, 0 = discipline default (unchanged behaviour), +1 = max
    // speed/agility. These bias each calculator's own discipline baseline by a bounded amount
    // rather than replacing the calculation, so they compose with everything else already
    // driving that baseline (power, mass, season, weight distribution, etc).
    private double _aeroBalance;
    public double AeroBalance
    {
        get => _aeroBalance;
        set { value = CalculationHelpers.Clamp(value, -1.0, 1.0); if (Math.Abs(_aeroBalance - value) < 0.0001) return; _aeroBalance = value; Raise(); }
    }

    private double _chassisRotation;
    public double ChassisRotation
    {
        get => _chassisRotation;
        set { value = CalculationHelpers.Clamp(value, -1.0, 1.0); if (Math.Abs(_chassisRotation - value) < 0.0001) return; _chassisRotation = value; Raise(); }
    }

    // ── Data-driven bounds refresh ────────────────────────────────────
    public void ApplyPhysicsBounds(CarCard car, SelectedParts parts, Fh6DatabaseService db)
    {
        // Fixed, car-independent range (see TirePressureAbsoluteMin/Max). Always reset here
        // so stale saved profiles can't keep carrying an out-of-range bound forward.
        TirePressureFrontMin = TirePressureAbsoluteMin;
        TirePressureFrontMax = TirePressureAbsoluteMax;
        TirePressureRearMin = TirePressureAbsoluteMin;
        TirePressureRearMax = TirePressureAbsoluteMax;

        var fSp = TuningPhysicsContext.FrontSpringDamper(car, parts, db);
        var rSp = TuningPhysicsContext.RearSpringDamper(car, parts, db);

        // DB spring rates are already in N/mm (the unit the constraints store internally).
        // A user-entered override (SelectedParts.SpringFrontMinOverride etc.) wins over the
        // DB-derived bound, same escape-hatch pattern as SelectedParts.MaxRpmOverride. Ride height
        // does NOT use a min/max override — see SelectedParts.RideHeightFrontOverride, it's a
        // direct-value override applied in SuspensionCalculator.CalculateRideHeight instead.
        if (fSp != null)
        {
            SpringFrontMin = parts.SpringFrontMinOverride ?? fSp.MinSpringRate;
            SpringFrontMax = parts.SpringFrontMaxOverride ?? fSp.MaxSpringRate;
            RideHeightFrontMin = fSp.MinRideHeight * 1000.0;
            RideHeightFrontMax = fSp.MaxRideHeight * 1000.0;
            ReboundFrontMin = parts.ReboundFrontMinOverride ?? fSp.MinDampenReboundRate;
            ReboundFrontMax = parts.ReboundFrontMaxOverride ?? fSp.MaxDampenReboundRate;
            BumpFrontMin = parts.BumpFrontMinOverride ?? fSp.MinDampenBumpRate;
            BumpFrontMax = parts.BumpFrontMaxOverride ?? fSp.MaxDampenBumpRate;
        }
        if (rSp != null)
        {
            SpringRearMin = parts.SpringRearMinOverride ?? rSp.MinSpringRate;
            SpringRearMax = parts.SpringRearMaxOverride ?? rSp.MaxSpringRate;
            RideHeightRearMin = rSp.MinRideHeight * 1000.0;
            RideHeightRearMax = rSp.MaxRideHeight * 1000.0;
            ReboundRearMin = parts.ReboundRearMinOverride ?? rSp.MinDampenReboundRate;
            ReboundRearMax = parts.ReboundRearMaxOverride ?? rSp.MaxDampenReboundRate;
            BumpRearMin = parts.BumpRearMinOverride ?? rSp.MinDampenBumpRate;
            BumpRearMax = parts.BumpRearMaxOverride ?? rSp.MaxDampenBumpRate;
        }

        var fArb = TuningPhysicsContext.FrontArb(car, parts, db);
        var rArb = TuningPhysicsContext.RearArb(car, parts, db);
        if (fArb != null)
        {
            ARBFrontMin = fArb.MinSwaybarStiffness;
            ARBFrontMax = fArb.MaxSwaybarStiffness;
        }
        if (rArb != null)
        {
            ARBRearMin = rArb.MinSwaybarStiffness;
            ARBRearMax = rArb.MaxSwaybarStiffness;
        }

        var dbCar = db.GetCar(car.CarDbId);
        if (dbCar != null)
        {
            AeroFrontMin = 0;
            AeroFrontMax = Math.Max(0, dbCar.FrontDownforceClampKG);
            AeroRearMin = 0;
            AeroRearMax = Math.Max(0, dbCar.RearDownforceClampKG);
        }

        var brakes = TuningPhysicsContext.Brakes(car, parts, db);
        if (brakes != null)
        {
            double frictionScale = brakes.GameFrictionScaleBraking > 0.1 ? brakes.GameFrictionScaleBraking : 1.0;
            // Normalise the brake-torque slider against its 0.5 default (see BrakeCalculator).
            double torqueSliderFactor = brakes.BrakeTorqueSlider > 0.01 ? 0.5 / brakes.BrakeTorqueSlider : 1.0;
            BrakePressureMin = 20.0;
            BrakePressureMax = Math.Round(200.0 / frictionScale * torqueSliderFactor);

            if (brakes.FrontBrakeTorqueClamp > 0 && brakes.RearBrakeTorqueClamp > 0)
            {
                double totalClamp = brakes.FrontBrakeTorqueClamp + brakes.RearBrakeTorqueClamp;
                double maxFrontBias = brakes.FrontBrakeTorqueClamp / totalClamp * 100.0;
                // Symmetric clamps (the norm) should leave a wide adjustable band, not pin it near 50%.
                BrakeBalanceMin = Math.Max(20.0, 100.0 - maxFrontBias - 20.0);
                BrakeBalanceMax = Math.Min(80.0, maxFrontBias + 20.0);
            }
        }
    }

    // ── Pair correction helpers ───────────────────────────────────────
    private static bool SetMin(ref double min, ref double max, double value)
    {
        if (Math.Abs(min - value) < 0.0001) return false;
        min = value;
        if (min > max) max = min;
        return true;
    }

    private static bool SetMax(ref double min, ref double max, double value)
    {
        if (Math.Abs(max - value) < 0.0001) return false;
        max = value;
        if (min > max) min = max;
        return true;
    }
}
