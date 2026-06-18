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
        launch = Math.Clamp(launch, 1200, maxRpm * 0.80);

        r.LaunchControlRpm = (int)(Math.Round(launch / 100.0) * 100);
    }
}
