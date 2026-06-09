using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

internal static class TireCalculator
{
    public static void CalculateTirePressure(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r, Dictionary<string, string> ex)
    {
        double baseBar = car.TireType switch
        {
            TireType.Slick     => 2.24,
            TireType.SemiSlick => 2.21,
            TireType.Sport     => 2.07,
            TireType.Street    => 2.14,
            TireType.Stock     => 2.14,
            TireType.Rally     => 2.03,
            TireType.Winter    => 1.97,
            TireType.Offroad   => 2.00,
            TireType.Drag      => 2.21,
            _                  => 2.14
        };

        double massRatio = car.TotalMass / CalculationHelpers.MassBaselineKg;
        double massWeight = 0.30 + 0.70 * CalculationHelpers.Clamp((car.TotalMass - 900) / 1400, 0, 1);
        double massAdj = Math.Log(massRatio) * CalculationHelpers.MassLogFactor * massWeight;

        double wd = CalculationHelpers.EffectiveWtDist(car);
        double wdDev = (wd - 50) / 50.0;
        double wdNonlinear = Math.Sign(wdDev) * Math.Pow(Math.Abs(wdDev), 1.5);
        double wdAdjF = wdNonlinear * 0.62;
        double wdAdjR = -wdNonlinear * 0.62;

        double profile = (car.FrontTireProfile + car.RearTireProfile) / 2.0;
        double profileAdj = CalculationHelpers.Clamp((CalculationHelpers.ProfileBaseline - profile) * 0.004, -0.15, 0.15);

        double frontWidthWeight = car.DriveType == DriveType.FWD ? 1.30 : car.DriveType == DriveType.RWD ? 0.80 : 1.00;
        double rearWidthWeight  = car.DriveType == DriveType.RWD ? 1.30 : car.DriveType == DriveType.FWD ? 0.80 : 1.00;
        double widthAdjF = Math.Tanh((CalculationHelpers.RefTireWidth - car.FrontTireWidth) / 100.0 * 0.5) * 0.10 * frontWidthWeight;
        double widthAdjR = Math.Tanh((CalculationHelpers.RefTireWidth - car.RearTireWidth)  / 100.0 * 0.5) * 0.10 * rearWidthWeight;

        double powerAdjF = 0, powerAdjR = 0;
        if (track.Discipline != Discipline.Drag)
        {
            double ptwRatio = car.PowerHP / car.TotalMass / (CalculationHelpers.PowerBaselineHP / CalculationHelpers.MassBaselineKg);
            // Both branches have matching slope (~0.30) at ptwRatio=1.0 to avoid a discontinuity
            double ptwFactor = ptwRatio < 1.0
                ? (ptwRatio - 1.0) * 0.30
                : Math.Tanh((ptwRatio - 1.0) * 0.35) * 0.85;
            if (car.DriveType == Models.DriveType.RWD)
            {
                powerAdjR = -ptwFactor * 0.15;
                powerAdjF =  ptwFactor * 0.07;
            }
            else if (car.DriveType == Models.DriveType.FWD)
            {
                powerAdjF = -ptwFactor * 0.15;
                powerAdjR =  ptwFactor * 0.07;
            }
            else
            {
                powerAdjF = ptwFactor * 0.05;
                powerAdjR = ptwFactor * 0.05;
            }
        }

        double tpF = baseBar + massAdj + wdAdjF + profileAdj + widthAdjF + powerAdjF;
        double tpR = baseBar + massAdj + wdAdjR + profileAdj + widthAdjR + powerAdjR;

        double discF = 0, discR = 0;
        string reason;
        switch (track.Discipline)
        {
            case Discipline.Drag:
            {
                double dragPtw = car.PowerHP / car.TotalMass;
                double dragPtwFactor = CalculationHelpers.Clamp((dragPtw - 100) / 400, 0, 1);
                double dragMassFactor = CalculationHelpers.Clamp((car.TotalMass - 1000) / 1000, 0, 1);
                discR = -0.50 - dragMassFactor * 0.60 + dragPtwFactor * 0.20;
                reason = CalculationHelpers.L("Expl_TirePressureReason_Drag");
                break;
            }
            case Discipline.Drift:
            {
                double tireGrip = car.TireType switch
                {
                    TireType.Slick     => 1.00,
                    TireType.SemiSlick => 0.80,
                    TireType.Sport     => 0.60,
                    TireType.Street    => 0.50,
                    TireType.Stock     => 0.40,
                    TireType.Rally     => 0.30,
                    TireType.Offroad   => 0.20,
                    TireType.Drag      => 0.70,
                    _                  => 0.50
                };
                double avgWidth = (car.FrontTireWidth + car.RearTireWidth) / 2.0;
                double widthFactor = (avgWidth - 205) / 200;
                double ptwActual = car.PowerHP / car.TotalMass;
                double ptwFactor = CalculationHelpers.Clamp((ptwActual - 100) / 400, 0, 1);
                double gripMod = CalculationHelpers.Clamp(tireGrip * 0.60 + widthFactor * 0.30 - ptwFactor * 0.20, 0, 1);
                discF = -0.05 + gripMod * 0.02;
                discR =  0.05 + gripMod * 0.12;
                reason = CalculationHelpers.L("Expl_TirePressureReason_Drift");
                break;
            }
            case Discipline.Rally:
                discF = -0.20; discR = -0.20;
                reason = CalculationHelpers.L("Expl_TirePressureReason_Rally");
                break;
            case Discipline.CrossCountry:
                discF = -0.20; discR = -0.20;
                reason = CalculationHelpers.L("Expl_TirePressureReason_CrossCountry");
                break;
            case Discipline.Touge:
                discF = -0.12; discR = -0.10;
                reason = CalculationHelpers.L("Expl_TirePressureReason_Touge");
                break;
            case Discipline.Street:
                discR = 0.05;
                reason = CalculationHelpers.L("Expl_TirePressureReason_Street");
                break;
            default:
                reason = CalculationHelpers.L("Expl_TirePressureReason_Road");
                break;
        }

        double rimCorrF = (car.FrontRimDiameter - 19) * 0.018;
        double rimCorrR = (car.RearRimDiameter  - 19) * 0.018;

        r.TirePressureFront = Math.Round(CalculationHelpers.Clamp(tpF + discF + rimCorrF, c.TirePressureFrontMin, c.TirePressureFrontMax), 2);
        r.TirePressureRear  = Math.Round(CalculationHelpers.Clamp(tpR + discR + rimCorrR, c.TirePressureRearMin,  c.TirePressureRearMax),  2);
        ex["TirePressure"] = string.Format(CalculationHelpers.L("Expl_TirePressure_Fmt"), r.TirePressureFront, r.TirePressureRear, reason);
    }
}
