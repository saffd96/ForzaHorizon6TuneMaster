using System;
using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

internal static class LaunchControlCalculator
{
    public static void CalculateLaunchControl(CarCard car, TrackInfo track, TuneResult r)
    {
        var db = Fh6DatabaseService.Instance;
        CalculateLaunchControl(car, track, new SelectedParts(), db, r);
    }

    public static void CalculateLaunchControl(CarCard car, TrackInfo track, SelectedParts parts, Fh6DatabaseService db, TuneResult r)
    {
        if (car.PowertrainType == PowertrainType.Electric)
        {
            r.LaunchControlRpm = null;
            return;
        }

        double torquePeak = car.TorquePeakRPM;
        double maxRpm = car.MaxRPM;

        double baseLaunch = car.AspirationType switch
        {
            AspirationType.TwinTurbo when car.AntiLag
                => Math.Max(maxRpm * 0.40, torquePeak * 0.65),
            AspirationType.TwinTurbo
                => Math.Max(maxRpm * 0.35, torquePeak * 0.60),
            AspirationType.SingleTurbo when car.AntiLag
                => Math.Max(maxRpm * 0.45, torquePeak * 0.70),
            AspirationType.SingleTurbo
                => Math.Max(maxRpm * 0.40, torquePeak * 0.65),
            AspirationType.PositiveDisplacement
                => Math.Max(maxRpm * 0.30, torquePeak * 0.65),
            AspirationType.Centrifugal
                => Math.Max(maxRpm * 0.30, torquePeak * 0.75),
            _   => Math.Max(maxRpm * 0.30, torquePeak * 0.75)
        };

        double driveAdj = car.DriveType switch
        {
            DriveType.AWD => 1.15,
            DriveType.RWD => 1.00,
            DriveType.FWD => 0.75,
            _             => 1.00
        };

        double excessTorque = Math.Max(0, car.TorqueNm - CalculationHelpers.TorqueBaselineNm);
        double torqueFactor;
        if (car.DriveType == DriveType.AWD)
            torqueFactor = Math.Clamp(1.0 - excessTorque / 4000.0, 0.85, 1.0);
        else
            torqueFactor = Math.Clamp(1.0 - excessTorque / 1200.0, 0.60, 1.0);

        // Use the selected tyre compound's longitudinal grip to scale launch RPM.
        var compound = TuningPhysicsContext.TireCompound(car, parts, db);
        var baseCompound = compound != null ? db.GetTireCompound(compound.TireCompoundID) : null;
        double longGrip = baseCompound?.TorqueFreeLongFrictionScaleAccel0 ?? 1.0;
        double tireFactor = 0.85 + longGrip * 0.15;

        double disciplineFactor = track.Discipline == Discipline.Drag ? 1.15 : 1.00;

        double launch = baseLaunch * driveAdj * torqueFactor * tireFactor * disciplineFactor;

        // ── Drivetrain physics ──────────────────────────────────────────
        // Clutch: more friction = can hold more torque without slip → launch harder.
        var clutch = TuningPhysicsContext.Clutch(car, parts, db);
        double clutchFriction = clutch.ClutchFriction;
        double clutchFactor = 0.85 + clutchFriction * 0.15;
        launch *= clutchFactor;

        // Transmission inertia: more rotational mass stores more energy → more sustained
        // torque when the clutch bites → wheelspin risk goes up, so lower the RPM.
        var trans = TuningPhysicsContext.Transmission(car, parts, db);
        double transMI = trans.MomentInertia;
        const double baselineTransMI = 0.3;
        double inertiaFactor = transMI > 0.001
            ? CalculationHelpers.Clamp(baselineTransMI / transMI, 0.90, 1.10)
            : 1.0;
        launch *= inertiaFactor;

        // Camshaft StallRPM: the engine cannot run cleanly below this — it is the
        // hard floor for launch RPM. A racing cam with high StallRPM forces the launch
        // higher, independent of the multiplicative factors.
        double stallFloor = GetStallFloor(car, parts, db);

        // Gearing factor: shorter effective 1st gear (higher numerical ratio → more torque
        // multiplication at the wheels) increases wheelspin risk, so lower the launch RPM.
        // Taller effective 1st gear (less mechanical advantage) increases bog risk, so raise it.
        double gearFactor = 1.0;
        if (r.GearRatios.Count > 0 && r.FinalDrive > 0)
        {
            double firstGear = r.GearRatios[0];
            double effFirst = firstGear * r.FinalDrive;
            const double baselineEffFirst = 12.0;
            gearFactor = CalculationHelpers.Clamp(baselineEffFirst / effFirst, 0.82, 1.18);
        }
        launch *= gearFactor;

        launch = CalculationHelpers.Clamp(launch, stallFloor, maxRpm * 0.80);

        r.LaunchControlRpm = (int)(Math.Round(launch / 100.0) * 100);
    }

    public static double GetStallFloor(CarCard car, SelectedParts parts, Fh6DatabaseService db)
    {
        var cam = TuningPhysicsContext.Camshaft(car, parts, db);
        return Math.Max(1200.0, cam.StallRPM);
    }
}
