using System;
using System.Collections.Generic;
using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

internal static class DifferentialCalculator
{
    public static void CalculateDifferential(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r, Dictionary<string, string> ex)
    {
        if (car.DifferentialUpgrade == DifferentialUpgrade.Stock ||
            car.DifferentialUpgrade == DifferentialUpgrade.Street)
        {
            ex["Differential"] = CalculationHelpers.L("Expl_Differential_Stock");
            return;
        }

        const double maxAccel = 100.0, maxDecel = 100.0;

        double accel, decel;
        switch (track.Discipline)
        {
            case Discipline.Drag:
                (accel, decel) = car.DriveType switch
                {
                    Models.DriveType.RWD => (85.0, 5.0),
                    Models.DriveType.AWD => (78.0, 0.0),
                    _                    => (70.0, 0.0)
                };
                break;
            case Discipline.Drift:
                bool isElectricDrift = car.PowertrainType == PowertrainType.Electric;
                (accel, decel) = (car.DriveType, isElectricDrift) switch
                {
                    (Models.DriveType.RWD, true)  => (72.0, 0.0),
                    (Models.DriveType.RWD, false)  => (90.0, 10.0),
                    (Models.DriveType.AWD, true)   => (65.0, 8.0),
                    (Models.DriveType.AWD, false)  => (75.0, 10.0),
                    (Models.DriveType.FWD, _)      => (30.0, 5.0),
                    _                              => (0.0, 0.0)
                };
                break;
            case Discipline.Rally:
                (accel, decel) = car.DriveType switch
                {
                    Models.DriveType.AWD => (55.0, 20.0),
                    _                    => (45.0, 20.0)
                };
                break;
            case Discipline.CrossCountry:
                (accel, decel) = car.DriveType switch
                {
                    Models.DriveType.AWD => (50.0, 25.0),
                    _                    => (40.0, 25.0)
                };
                break;
            default:
                (accel, decel) = car.DriveType switch
                {
                    Models.DriveType.RWD => (50.0, 20.0),
                    Models.DriveType.FWD => (60.0, 5.0),
                    _                    => (55.0, 30.0)
                };
                break;
        }

        accel += Math.Max(0, (car.PowerHP - CalculationHelpers.PowerBaselineHP) / CalculationHelpers.PowerStepHP * 2.5);
        accel += Math.Max(0, (car.TorqueNm - CalculationHelpers.TorqueBaselineNm) / 300.0 * 1.5);
        accel += (car.TotalMass - CalculationHelpers.MassBaselineKg) / 100.0 * 0.5;
        accel += car.EnginePosition switch { EnginePosition.Rear => 4.0, EnginePosition.Mid => 2.0, _ => 0.0 };
        accel -= (car.Wheelbase - CalculationHelpers.RefWheelbaseMm) / 500.0 * 1.5;
        
        accel *= CalculationHelpers.GetPowerDeliveryFactors(car.PowertrainType, car.AspirationType, car.AntiLag).Diff;

        double seasonFactorDiff = CalculationHelpers.GetSeasonGripFactor(track.Season);
        double seasonPenalty = (1.0 - seasonFactorDiff) * 15.0;
        accel -= seasonPenalty;
        decel -= seasonPenalty * 0.6;

        accel = Math.Min(accel, maxAccel);
        decel = Math.Min(decel, maxDecel);

        bool hasDecel = car.DifferentialUpgrade != DifferentialUpgrade.Sport;

        r.DiffAccel = Math.Round(CalculationHelpers.Clamp(accel, c.DiffAccelMin, c.DiffAccelMax));
        if (hasDecel)
            r.DiffDecel = Math.Round(CalculationHelpers.Clamp(decel, c.DiffDecelMin, c.DiffDecelMax));

        string aspLabel = car.AspirationType switch
        {
            AspirationType.SingleTurbo          => CalculationHelpers.L("Enum_AspirationType_SingleTurbo"),
            AspirationType.TwinTurbo            => CalculationHelpers.L("Enum_AspirationType_TwinTurbo"),
            AspirationType.PositiveDisplacement => CalculationHelpers.L("Enum_AspirationType_PositiveDisplacement"),
            AspirationType.Centrifugal          => CalculationHelpers.L("Enum_AspirationType_Centrifugal"),
            AspirationType.Electric             => CalculationHelpers.L("Enum_AspirationType_Electric"),
            _                                   => CalculationHelpers.L("Enum_AspirationType_Natural")
        };
        string engPosLabel = car.EnginePosition switch
        {
            EnginePosition.Front   => CalculationHelpers.L("Enum_EnginePosition_Front"),
            EnginePosition.Mid     => CalculationHelpers.L("Enum_EnginePosition_Mid"),
            EnginePosition.Rear    => CalculationHelpers.L("Enum_EnginePosition_Rear"),
            _                      => car.EnginePosition.ToString()
        };
        string diag = string.Format(CalculationHelpers.L("Expl_Differential_DiagFmt"), car.PowerHP, aspLabel, engPosLabel, car.Wheelbase);

        if (car.DriveType == Models.DriveType.AWD)
        {
            double bias = (100.0 - CalculationHelpers.EffectiveWtDist(car)) / 100.0;
            
            double biasAdj = track.Discipline switch
            {
                Discipline.Drag         => 0.15,
                Discipline.Drift        => 0.05,
                Discipline.Rally        => 0.10,
                Discipline.CrossCountry => 0.08,
                _                       => 0.05
            };
            
            bias += biasAdj;
            bias += (car.Wheelbase - CalculationHelpers.RefWheelbaseMm) / 500.0 * 0.03;
            bias = CalculationHelpers.Clamp(bias, 0.30, 0.85);

            double fAccel = track.Discipline switch
            {
                Discipline.Drag         => 55,
                Discipline.Drift        => 30,
                Discipline.Rally        => 40,
                Discipline.CrossCountry => 45,
                _                       => 45
            };
            double fDecel = track.Discipline switch
            {
                Discipline.Drag         => 3,
                Discipline.Drift        => 5,
                Discipline.Rally        => 20,
                Discipline.CrossCountry => 15,
                _                       => 10
            };

            double wdFront_awd = CalculationHelpers.EffectiveWtDist(car);
            fAccel += (wdFront_awd - 50.0) * 0.5;
            fAccel += car.EnginePosition switch
            {
                EnginePosition.Front   =>  5.0,
                EnginePosition.Mid     =>  0.0,
                EnginePosition.Rear    => -8.0,
                _                      =>  0.0
            };
            fAccel = Math.Clamp(fAccel, 10.0, 65.0);

            double frontFactor = 1.2 - bias * 0.4;
            double rearFactor  = 0.8 + bias * 0.4;

            r.DiffAccel = Math.Round(CalculationHelpers.Clamp(accel * rearFactor, c.DiffAccelMin, c.DiffAccelMax));
            if (hasDecel)
                r.DiffDecel = Math.Round(CalculationHelpers.Clamp(decel * rearFactor, c.DiffDecelMin, c.DiffDecelMax));

            r.DiffFrontAccel = Math.Round(CalculationHelpers.Clamp(Math.Min(fAccel, maxAccel) * frontFactor, c.DiffAccelMin, c.DiffAccelMax));
            if (hasDecel)
                r.DiffFrontDecel = Math.Round(CalculationHelpers.Clamp(Math.Min(fDecel, maxDecel) * frontFactor, c.DiffDecelMin, c.DiffDecelMax));
            r.CenterDiffBias = Math.Round(bias * 100);
        }

        if (r.DiffFrontAccel.HasValue)
            ex["Differential"] = hasDecel
                ? string.Format(CalculationHelpers.L("Expl_Differential_AWDFmt"), r.DiffAccel, r.DiffDecel, r.DiffFrontAccel, r.DiffFrontDecel, r.CenterDiffBias, diag)
                : string.Format(CalculationHelpers.L("Expl_Differential_AWDFmtAccelOnly"), r.DiffAccel, r.DiffFrontAccel, r.CenterDiffBias, diag);
        else
            ex["Differential"] = hasDecel
                ? string.Format(CalculationHelpers.L("Expl_Differential_Fmt"), r.DiffAccel, r.DiffDecel, diag)
                : string.Format(CalculationHelpers.L("Expl_Differential_FmtAccelOnly"), r.DiffAccel, diag);
    }
}