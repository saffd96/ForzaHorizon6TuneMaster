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

        double speed = overrideSpeedKmh ?? car.MaxSpeedKmh;

        double speedFactor = Math.Min(1.2, Math.Pow(speed / 380.0, 1.1)); 
        double pwrFactor = 1.0 + Math.Max(0, (car.PowerHP - CalculationHelpers.PowerBaselineHP) / CalculationHelpers.PowerStepHP * 0.12);
        pwrFactor = Math.Min(1.5, pwrFactor); 
        
        double aeroMultiplier = speedFactor * pwrFactor;

        double fwBase, rwBase;
        switch (car.DriveType)
        {
            case Models.DriveType.RWD:
                bool isFrontEngineRwd = car.EnginePosition == Models.EnginePosition.Front; 
                fwBase = isFrontEngineRwd ? 0.45 : 0.55;
                rwBase = isFrontEngineRwd ? 0.75 : 0.65;
                break;
            case Models.DriveType.FWD:
                fwBase = 0.65; rwBase = 0.50;
                break;
            case Models.DriveType.AWD:
                fwBase = 0.55; 
                rwBase = 0.75; 
                break;
            default:
                fwBase = 0.55; rwBase = 0.60;
                break;
        }

        double aeroF = 0, aeroR = 0;

        switch (track.Discipline)
        {
            case Discipline.Drag:
                aeroF = c.AeroFrontMin;
                double dragRearFactor = Math.Min(0.45, 0.05 + (pwrFactor - 1.0) * 0.80); 
                aeroR = car.HasRearAero ? c.AeroRearMin + (c.AeroRearMax - c.AeroRearMin) * dragRearFactor : 0;
                break;

            case Discipline.Drift:
                aeroF = car.HasFrontAero ? c.AeroFrontMin + (c.AeroFrontMax - c.AeroFrontMin) * 0.15 * aeroMultiplier : 0;
                aeroR = car.HasRearAero  ? c.AeroRearMin  + (c.AeroRearMax  - c.AeroRearMin)  * 0.15 * aeroMultiplier : 0;
                break;

            case Discipline.CrossCountry:
            case Discipline.Rally:
                double offroadFactor = track.Discipline == Discipline.Rally ? 0.65 : 0.50;
                aeroF = car.HasFrontAero ? c.AeroFrontMin + (c.AeroFrontMax - c.AeroFrontMin) * offroadFactor * aeroMultiplier : 0;
                aeroR = car.HasRearAero  ? c.AeroRearMin  + (c.AeroRearMax  - c.AeroRearMin)  * (offroadFactor + 0.10) * aeroMultiplier : 0;
                break;

            default:
                aeroF = car.HasFrontAero ? c.AeroFrontMin + (c.AeroFrontMax - c.AeroFrontMin) * fwBase * aeroMultiplier : 0;
                aeroR = car.HasRearAero  ? c.AeroRearMin  + (c.AeroRearMax  - c.AeroRearMin)  * rwBase * aeroMultiplier : 0;
                break;
        }

        r.AeroFront = car.HasFrontAero ? Math.Round(CalculationHelpers.Clamp(aeroF, c.AeroFrontMin, c.AeroFrontMax)) : 0;
        r.AeroRear  = car.HasRearAero  ? Math.Round(CalculationHelpers.Clamp(aeroR, c.AeroRearMin,  c.AeroRearMax))  : 0;
        
        ex["Aero"] = string.Format(CalculationHelpers.L("Expl_Aero_Fmt"), r.AeroFront, r.AeroRear, car.MaxSpeedKmh, car.PowerHP);
    }
}