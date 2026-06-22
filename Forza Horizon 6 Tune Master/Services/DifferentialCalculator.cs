using System;
using System.Collections.Generic;
using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

internal static class DifferentialCalculator
{
    public static void CalculateDifferential(CarCard car, TrackInfo track, TuningConstraints _, TuneResult r, Dictionary<string, string> ex) =>
        CalculateDifferential(car, track, new SelectedParts(), Fh6DatabaseService.Instance, r, ex);

    public static void CalculateDifferential(CarCard car, TrackInfo track, SelectedParts parts, Fh6DatabaseService db, TuneResult r, Dictionary<string, string> ex)
    {
        // Differential() always resolves a part (falls back to a synthetic stock diff), so no
        // null check is needed here.
        var diff = TuningPhysicsContext.Differential(car, parts, db);

        // Decel lock is tunable whenever the fitted differential actually supports it.
        // Deriving this from the resolved part (rather than the DifferentialUpgrade enum)
        // is robust to the DB Level→enum mapping, which is not 1:1 (DB levels skip 4),
        // so e.g. a race diff would otherwise be misread as "Sport" and hide decel.
        bool hasDecel = Math.Max(diff.RearLimitedSlipTorqueDecel, diff.FrontLimitedSlipTorqueDecel) > 0.01;

        // Adjust for power delivery: more torque needs more lock, but very high torque
        // on a light car is easier to break loose, so we cap the increase.
        double ptw = car.PowerHP / Math.Max(car.TotalMass, 1.0);
        double powerMul = 1.0 + Math.Min(0.25, (ptw - 0.20) * 0.25);

        // Longer wheelbase makes the car more stable, so we can run slightly less lock.
        double wheelbaseFactor = 1.0 - CalculationHelpers.Clamp((car.Wheelbase - 2600.0) / 1000.0, -0.2, 0.3) * 0.10;

        // Less grip in winter -> less diff lock to avoid understeer/power-on instability.
        double seasonFactor = CalculationHelpers.GetSeasonGripFactor(track.Season);
        double seasonMul = 0.85 + seasonFactor * 0.15;

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

        if (car.DriveType == DriveType.AWD)
        {
            // ── AWD: front + rear + center ──────────────────────────────────
            (double rearAccelTarget, double rearDecelTarget) = RearLockTargets(track.Discipline, car.DriveType);
            rearAccelTarget *= powerMul * wheelbaseFactor * seasonMul;
            rearDecelTarget *= powerMul * wheelbaseFactor * seasonMul;

            double rearAccel = ClampToMax(rearAccelTarget, diff.RearLimitedSlipTorqueAccel);
            double rearDecel = hasDecel ? ClampToMax(rearDecelTarget, diff.RearLimitedSlipTorqueDecel) : 0.0;

            (double frontAccelTarget, double frontDecelTarget) = FrontLockTargets(track.Discipline);
            frontAccelTarget *= wheelbaseFactor * seasonMul;
            frontDecelTarget *= wheelbaseFactor * seasonMul;
            double frontAccel = ClampToMax(frontAccelTarget, diff.FrontLimitedSlipTorqueAccel);
            double frontDecel = hasDecel ? ClampToMax(frontDecelTarget, diff.FrontLimitedSlipTorqueDecel) : 0.0;

            // Centre bias: start from the part's rear torque split, then nudge per discipline.
            double centerBias = diff.RearToqueSplit;
            centerBias += track.Discipline switch
            {
                Discipline.Drag         => 0.12,
                Discipline.Drift        => 0.05,
                Discipline.Rally        => 0.08,
                Discipline.CrossCountry => 0.06,
                _                       => 0.03
            };
            if (car.EnginePosition == EnginePosition.Rear) centerBias += 0.03;
            centerBias = CalculationHelpers.Clamp(centerBias, 0.30, 0.85);

            // Re-bias the rear diff output to account for the centre split.
            double rearFactor = 0.85 + centerBias * 0.15;
            r.DiffAccel = Math.Round(CalculationHelpers.Clamp(rearAccel * rearFactor, 0.0, diff.RearLimitedSlipTorqueAccel) * 100.0);
            if (hasDecel)
                r.DiffDecel = Math.Round(CalculationHelpers.Clamp(rearDecel * rearFactor, 0.0, diff.RearLimitedSlipTorqueDecel) * 100.0);

            r.DiffFrontAccel = Math.Round(frontAccel * 100.0);
            if (hasDecel)
                r.DiffFrontDecel = Math.Round(frontDecel * 100.0);
            r.CenterDiffBias = Math.Round(centerBias * 100.0);

            ex["Differential"] = hasDecel
                ? string.Format(CalculationHelpers.L("Expl_Differential_AWDFmt"), r.DiffAccel, r.DiffDecel, r.DiffFrontAccel, r.DiffFrontDecel, r.CenterDiffBias, diag)
                : string.Format(CalculationHelpers.L("Expl_Differential_AWDFmtAccelOnly"), r.DiffAccel, r.DiffFrontAccel, r.CenterDiffBias, diag);
        }
        else if (car.DriveType == DriveType.FWD)
        {
            // ── FWD: main diff is on the front axle ─────────────────────────
            (double accelTarget, double decelTarget) = FrontLockTargets(track.Discipline);
            accelTarget *= powerMul * wheelbaseFactor * seasonMul;
            decelTarget *= powerMul * wheelbaseFactor * seasonMul;

            double accel = ClampToMax(accelTarget, diff.FrontLimitedSlipTorqueAccel);
            double decel = hasDecel ? ClampToMax(decelTarget, diff.FrontLimitedSlipTorqueDecel) : 0.0;

            r.DiffAccel = Math.Round(accel * 100.0);
            if (hasDecel)
                r.DiffDecel = Math.Round(decel * 100.0);

            ex["Differential"] = hasDecel
                ? string.Format(CalculationHelpers.L("Expl_Differential_Fmt"), r.DiffAccel, r.DiffDecel, diag)
                : string.Format(CalculationHelpers.L("Expl_Differential_FmtAccelOnly"), r.DiffAccel, diag);
        }
        else
        {
            // ── RWD: main diff is on the rear axle ──────────────────────────
            (double accelTarget, double decelTarget) = RearLockTargets(track.Discipline, car.DriveType);
            accelTarget *= powerMul * wheelbaseFactor * seasonMul;
            decelTarget *= powerMul * wheelbaseFactor * seasonMul;

            double accel = ClampToMax(accelTarget, diff.RearLimitedSlipTorqueAccel);
            double decel = hasDecel ? ClampToMax(decelTarget, diff.RearLimitedSlipTorqueDecel) : 0.0;

            r.DiffAccel = Math.Round(accel * 100.0);
            if (hasDecel)
                r.DiffDecel = Math.Round(decel * 100.0);

            ex["Differential"] = hasDecel
                ? string.Format(CalculationHelpers.L("Expl_Differential_Fmt"), r.DiffAccel, r.DiffDecel, diag)
                : string.Format(CalculationHelpers.L("Expl_Differential_FmtAccelOnly"), r.DiffAccel, diag);
        }
    }

    private static double ClampToMax(double target, double max)
    {
        if (max <= 0) return CalculationHelpers.Clamp(target, 0.0, 1.0);
        return CalculationHelpers.Clamp(target, 0.0, max);
    }

    private static (double accel, double decel) RearLockTargets(Discipline d, DriveType drive) => d switch
    {
        Discipline.Drag => drive switch
        {
            DriveType.RWD => (0.85, 0.05),
            DriveType.AWD => (0.75, 0.0),
            _             => (0.70, 0.0)
        },
        Discipline.Drift => drive switch
        {
            DriveType.AWD => (0.65, 0.08),
            DriveType.FWD => (0.35, 0.05),
            _             => (0.90, 0.10)
        },
        Discipline.Rally => drive switch
        {
            DriveType.AWD => (0.55, 0.20),
            _             => (0.45, 0.20)
        },
        Discipline.CrossCountry => drive switch
        {
            DriveType.AWD => (0.50, 0.25),
            _             => (0.40, 0.25)
        },
        _ => drive switch
        {
            DriveType.RWD => (0.50, 0.20),
            DriveType.FWD => (0.60, 0.05),
            _             => (0.55, 0.30)
        }
    };

    private static (double accel, double decel) FrontLockTargets(Discipline d) => d switch
    {
        Discipline.Drag         => (0.45, 0.03),
        Discipline.Drift        => (0.30, 0.05),
        Discipline.Rally        => (0.40, 0.20),
        Discipline.CrossCountry => (0.45, 0.15),
        _                       => (0.45, 0.10)
    };
}
