using System;
using System.Diagnostics;
using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

internal static class CalculationHelpers
{
    internal static string L(string key) => LocalizationService.Instance.T(key);

    internal static double Clamp(double v, double min, double max)
    {
        if (v < min || v > max)
            Debug.WriteLine($"[Clamp] {v} clamped to [{min}, {max}]");
        return Math.Max(min, Math.Min(max, v));
    }

    internal const double GearRatioMin = 0.48;
    internal const double GearRatioMax = 6.00;

    // Launch-control torque baseline (used by LaunchControlCalculator). The other ~13
    // reference constants here were unused dead weight and were removed — notably the
    // kgf/mm→N/mm factor (9.807), whose accidental use once inflated every spring ~9.8×.
    internal const double TorqueBaselineNm   = 400;

    internal const double RevLimitFraction   = 0.95;
    internal const double TargetSpeedCapKmh  = 700;
    internal const int    MaxNewtonIterations = 15;

    internal static double EffectiveWtDist(CarCard car)
    {
        if (car.HasExplicitWeightDistribution)
            return car.WeightDistributionFront;
        return car.EnginePosition switch
        {
            EnginePosition.Front   => 55,
            EnginePosition.Mid     => 48,
            EnginePosition.Rear    => 40,
            _                      => 50
        };
    }

    internal static double ComputeEffectiveMaxSpeedKmh(CarCard car, TuneResult r)
    {
        // Body drag must be a Cd×A (~0.3–2.0 m²). Data_Car.BodyAeroLongitudinalDrag is in
        // game-internal units (52–1700, avg ~210) — NOT Cd×A — so feeding it here produced
        // absurd top speeds (~78 km/h), which the gearing pass then amplified. We reuse the
        // SAME CdABodyEstimate as CarCard.MaxSpeedKmh as the body term; this effective figure
        // then ADDITIONALLY accounts for aero-downforce drag and rolling resistance, so it
        // reads a little lower than CarCard.MaxSpeedKmh (which is a quick body-drag-only label).
        double cdABody = car.CdABodyEstimate;
        const double AeroDragFactor = 0.001787;
        // GameDragScale is the DB's own per-car aero-drag scale (Data_Car.GameDragScale) — the
        // sibling of GameDownforceScale, which AeroCalculator already applies to wing downforce.
        // It was previously loaded and never used, so every car's wing-induced drag was computed
        // as if this scale were 1.0 regardless of the car's real value (0.8–0.95 for the two
        // profiles that surfaced this). Applied only to the wing/downforce-derived term, not
        // cdABody — the body figure is back-solved per-car from that car's own recorded top
        // speed, which already reflects whatever drag scale the game itself used.
        double cdATotal = cdABody + (r.AeroFront + r.AeroRear) * AeroDragFactor * car.GameDragScale;

        if (cdATotal < 0.3) cdATotal = 0.3; 

        double crr = RollingResistanceCoefficient(car.TireType);

        double powerW = car.PowerHP * PhysicsConstants.HpToWatt;
        const double rho = PhysicsConstants.AirDensity;
        double fRR = crr * car.TotalMass * PhysicsConstants.GravityMs2;

        double v = Math.Pow(powerW / (0.5 * rho * cdATotal), 1.0 / 3.0);
        for (int i = 0; i < MaxNewtonIterations; i++)
        {
            double fv = 0.5 * rho * cdATotal * v * v * v + fRR * v - powerW;
            double dv = 1.5 * rho * cdATotal * v * v + fRR;
            if (Math.Abs(dv) < 1e-10) break;
            double step = fv / dv;
            v = Math.Max(v - step, 1.0);
            if (Math.Abs(step) < 1e-4) break;
        }
        return Math.Round(Math.Clamp(v * PhysicsConstants.MsToKmh, 60.0, TargetSpeedCapKmh));
    }

    // Rolling-resistance coefficient by tire compound. Shared between ComputeEffectiveMaxSpeedKmh
    // (top-speed solve) and GearingCalculator's drag-taper gear fix — same physics, one table.
    internal static double RollingResistanceCoefficient(TireType tireType) => tireType switch
    {
        TireType.Slick     => 0.004,
        TireType.SemiSlick => 0.005,
        TireType.Sport     => 0.005,
        TireType.Street    => 0.006,
        TireType.Stock     => 0.007,
        TireType.Rally     => 0.008,
        TireType.Winter    => 0.009,
        TireType.Offroad   => 0.011,
        TireType.Drag      => 0.005,
        _                  => 0.006
    };

    internal static double GetSeasonGripFactor(Season s) => s switch
    {
        Season.Winter => 0.85,
        Season.Autumn => 0.95,
        Season.Spring => 1.00,
        _             => 1.05
    };

    // Season grip mapped to a multiplier: `floor` at the low-grip end, `floor + span` at the
    // high. Centralises the repeated "floor + GetSeasonGripFactor(season) * span" pattern.
    internal static double SeasonGripMultiplier(Season s, double floor, double span)
        => floor + GetSeasonGripFactor(s) * span;

    internal static (double Diff, double Spring, double Damper) GetPowerDeliveryFactors(
        PowertrainType pt, AspirationType? asp, bool antiLag = false)
    {
        if (pt == PowertrainType.Electric)
            return (1.20, 1.08, 1.06);

        var (d, s, dmpr) = (asp ?? AspirationType.Natural) switch
        {
            AspirationType.SingleTurbo          => antiLag ? (1.12, 1.05, 1.05) : (1.10, 1.05, 1.04),
            AspirationType.TwinTurbo            => antiLag ? (1.09, 1.04, 1.04) : (1.07, 1.04, 1.03),
            AspirationType.PositiveDisplacement => (1.05, 1.03, 1.03),
            AspirationType.Centrifugal          => (1.03, 1.02, 1.01),
            AspirationType.Electric             => (1.20, 1.08, 1.06),
            _                                   => (1.00, 1.00, 1.00),
        };

        if (pt == PowertrainType.Hybrid)
        {
            d    = 1.0 + (d    - 1.0) * 0.60;
            s    = 1.0 + (s    - 1.0) * 0.60;
            dmpr = 1.0 + (dmpr - 1.0) * 0.60;
        }

        return (d, s, dmpr);
    }
}
