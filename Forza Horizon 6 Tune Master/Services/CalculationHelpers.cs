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

    internal const double PowerBaselineHP   = 300;
    internal const double PowerStepHP        = 200;
    internal const double TorqueBaselineNm   = 400;
    internal const double MassBaselineKg     = 1400;
    internal const double RefMassKg          = 1500;
    internal const double RefWheelbaseMm     = 2700;
    internal const double RefRimDiameterInch = 19;
    internal const double ProfileBaseline    = 45;
    internal const double RefFrontTrackMm    = 1550;
    internal const double RefSpeedKmh        = 200;
    internal const double RefTireWidth       = 275;
    internal const double MassLogFactor      = 1.0;

    internal const double SpringHzToNmm      = 0.019739;
    internal const double GameSpringUnitToNmm = 9.807; // FH6 spring display unit ("kgf/mm") → canonical N/mm
    internal const double RevLimitFraction   = 0.95;
    internal const double TargetSpeedCapKmh  = 700;

    internal const double SpringPhysicalFloorFactor = 0.55;
    internal const double DragFirstGearBaseline = 4.5;
    internal const double GearDegradeRallyFactor = 1.05;

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
        // absurd top speeds (~78 km/h), which the gearing pass then amplified. Use the same
        // CdABodyEstimate that CarCard.MaxSpeedKmh uses so both speed figures agree.
        double cdABody = car.CdABodyEstimate;
        const double AeroDragFactor = 0.001787;
        double cdATotal = cdABody + (r.AeroFront + r.AeroRear) * AeroDragFactor;

        if (cdATotal < 0.3) cdATotal = 0.3; 

        double crr = car.TireType switch
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

        double powerW = car.PowerHP * 745.7;
        const double rho = 1.225;
        double fRR = crr * car.TotalMass * 9.81;

        double v = Math.Pow(powerW / (0.5 * rho * cdATotal), 1.0 / 3.0);
        for (int i = 0; i < 15; i++)
        {
            double fv = 0.5 * rho * cdATotal * v * v * v + fRR * v - powerW;
            double dv = 1.5 * rho * cdATotal * v * v + fRR;
            if (Math.Abs(dv) < 1e-10) break;
            double step = fv / dv;
            v = Math.Max(v - step, 1.0);
            if (Math.Abs(step) < 1e-4) break;
        }
        return Math.Round(Math.Clamp(v * 3.6, 60.0, TargetSpeedCapKmh));
    }

    internal static double GetSeasonGripFactor(Season s) => s switch
    {
        Season.Winter => 0.85,
        Season.Autumn => 0.95,
        Season.Spring => 1.00,
        _             => 1.05
    };

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
