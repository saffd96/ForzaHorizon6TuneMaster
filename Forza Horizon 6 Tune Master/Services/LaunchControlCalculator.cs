using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

internal static class LaunchControlCalculator
{
    public static void CalculateLaunchControl(CarCard car, TuneResult r)
    {
        if (car.PowertrainType == PowertrainType.Electric)
        {
            // Electric motors deliver full torque from 0 RPM — launch RPM doesn't apply
            r.LaunchControlRpm = null;
            return;
        }

        double torquePeak = car.TorquePeakRPM;

        double baseLaunch = car.AspirationType switch
        {
            AspirationType.TwinTurbo when car.AntiLag
                => Math.Max(car.MaxRPM * 0.37, torquePeak * 0.60),
            AspirationType.TwinTurbo
                => Math.Max(car.MaxRPM * 0.32, torquePeak * 0.55),
            AspirationType.SingleTurbo when car.AntiLag
                => Math.Max(car.MaxRPM * 0.42, torquePeak * 0.65),
            AspirationType.SingleTurbo
                => Math.Max(car.MaxRPM * 0.38, torquePeak * 0.60),
            AspirationType.PositiveDisplacement
                => Math.Max(car.MaxRPM * 0.28, torquePeak * 0.65),
            AspirationType.Centrifugal
                => Math.Max(car.MaxRPM * 0.25, torquePeak * 0.72),
            AspirationType.Electric
                => car.MaxRPM * 0.15,
            _   => Math.Max(car.MaxRPM * 0.20, torquePeak * 0.70)
        };

        double driveAdj = car.DriveType switch
        {
            DriveType.AWD => 1.10,
            DriveType.RWD => 1.00,
            DriveType.FWD => 0.80,
            _             => 1.00
        };

        double torqueFactor = Math.Clamp(1.0 - Math.Max(0, car.TorqueNm - CalculationHelpers.TorqueBaselineNm) / 1500.0, 0.65, 1.0);
        double launch = Math.Clamp(baseLaunch * driveAdj * torqueFactor, 1000, car.MaxRPM * 0.75);
        r.LaunchControlRpm = Math.Round(launch / 100.0) * 100;
    }
}
