using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

internal static class AeroCalculator
{
    public static void CalculateAero(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r, Dictionary<string, string> ex, double? overrideSpeedKmh = null)
    {
        if (!car.HasFrontAero && !car.HasRearAero)
        {
            r.AeroFront = 0; r.AeroRear = 0;
            ex["Aero"] = CalculationHelpers.L("Expl_Aero_None");
            return;
        }

        double speedFactor = (overrideSpeedKmh ?? car.MaxSpeedKmh) / 280.0;
        double pwrFactor = Math.Min(1.5, 1.0 + Math.Max(0, (car.PowerHP - CalculationHelpers.PowerBaselineHP) / CalculationHelpers.PowerStepHP * 0.15));

        var (fwFactor, rwFactor) = car.DriveType switch
        {
            Models.DriveType.RWD => (0.55, 0.70),
            Models.DriveType.FWD => (0.65, 0.55),
            Models.DriveType.AWD => (0.65, 0.55),
            _                    => (0.55, 0.60)
        };
        double aeroBase = Math.Min(1.0, speedFactor * pwrFactor);
        double aeroF = car.HasFrontAero ? c.AeroFrontMin + (c.AeroFrontMax - c.AeroFrontMin) * fwFactor * aeroBase : 0;
        double aeroR = car.HasRearAero  ? c.AeroRearMin  + (c.AeroRearMax  - c.AeroRearMin)  * rwFactor * aeroBase : 0;

        switch (track.Discipline)
        {
            case Discipline.Drag:
                // Scale rear aero with PTW/speed so a 900 HP car gets more downforce than a 300 HP car
                double dragAeroFactor = Math.Min(0.15, speedFactor * pwrFactor * 0.15);
                aeroF = 0;
                aeroR = car.HasRearAero ? c.AeroRearMin + (c.AeroRearMax - c.AeroRearMin) * (0.10 + dragAeroFactor) : 0;
                break;
            case Discipline.Drift:
                aeroF *= 0.35; aeroR *= 0.3;
                break;
            case Discipline.CrossCountry:
                aeroF = car.HasFrontAero ? CalculationHelpers.Clamp(c.AeroFrontMax * 0.40 * speedFactor * pwrFactor, c.AeroFrontMin, c.AeroFrontMax) : 0;
                aeroR = car.HasRearAero  ? CalculationHelpers.Clamp(c.AeroRearMax  * 0.55 * speedFactor * pwrFactor, c.AeroRearMin,  c.AeroRearMax)  : 0;
                break;
            case Discipline.Rally:
                aeroF = car.HasFrontAero ? CalculationHelpers.Clamp(c.AeroFrontMax * 0.60 * speedFactor * pwrFactor, c.AeroFrontMin, c.AeroFrontMax) : 0;
                aeroR = car.HasRearAero  ? CalculationHelpers.Clamp(c.AeroRearMax  * 0.75 * speedFactor * pwrFactor, c.AeroRearMin,  c.AeroRearMax)  : 0;
                break;
        }

        r.AeroFront = car.HasFrontAero ? Math.Round(CalculationHelpers.Clamp(aeroF, c.AeroFrontMin, c.AeroFrontMax)) : 0;
        r.AeroRear  = car.HasRearAero  ? Math.Round(CalculationHelpers.Clamp(aeroR, c.AeroRearMin,  c.AeroRearMax))  : 0;
        ex["Aero"] = string.Format(CalculationHelpers.L("Expl_Aero_Fmt"), r.AeroFront, r.AeroRear, car.MaxSpeedKmh, car.PowerHP);
    }
}
