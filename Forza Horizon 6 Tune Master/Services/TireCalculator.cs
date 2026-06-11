using System;
using System.Collections.Generic;
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
            TireType.Sport     => 1.93,
            TireType.Street    => 1.90,
            TireType.Stock     => 1.90,
            TireType.Rally     => 1.59,
            TireType.Winter    => 1.55,
            TireType.Offroad   => 1.38,
            TireType.Drag      => 1.80,
            _                  => 2.14
        };

        double massRatio = car.TotalMass / CalculationHelpers.MassBaselineKg;
        double massAdj = Math.Log((massRatio + 1.0) / 2.0) * 0.50;

        double wd = CalculationHelpers.EffectiveWtDist(car);
        double wdDev = CalculationHelpers.Clamp((wd - 50) / 50.0, -1.0, 1.0);
        double wdAdjF = wdDev * 0.40;
        double wdAdjR = -wdDev * 0.40;

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
            double clampedRatio = CalculationHelpers.Clamp(ptwRatio, 0.5, 2.0);
            double ptwFactor = (clampedRatio - 1.0) / (1.0 + Math.Abs(clampedRatio - 1.0));
            if (car.DriveType == Models.DriveType.RWD)
            {
                powerAdjR = -ptwFactor * 0.07;
                powerAdjF =  ptwFactor * 0.07;
            }
            else if (car.DriveType == Models.DriveType.FWD)
            {
                powerAdjF = -ptwFactor * 0.07;
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
                discF = 0.40;

                double dragPtw = car.PowerHP / (car.TotalMass / 1000.0);
                double dragPtwFactor = CalculationHelpers.Clamp((dragPtw - 100) / 400, 0, 1);
                double dragMassFactor = CalculationHelpers.Clamp((car.TotalMass - 1000) / 1000, 0, 1);

                double dragDistFactor = track.DragDistance switch
                {
                    DragDistance.Quarter => 0.06,
                    DragDistance.Half => 0.12,
                    DragDistance.Mile => 0.18,
                    _ => 0.00
                };
                discR = -0.10 - dragMassFactor * 0.10 + dragPtwFactor * 0.10 + dragDistFactor;
                reason = CalculationHelpers.L("Expl_TirePressureReason_Drag");
                break;
            }
            case Discipline.Drift:
                discF = -0.50;
                discR = -0.50;
                reason = CalculationHelpers.L("Expl_TirePressureReason_Drift");
                break;
            case Discipline.Rally:
                discF = -0.05; discR = -0.05;
                reason = CalculationHelpers.L("Expl_TirePressureReason_Rally");
                break;
            case Discipline.CrossCountry:
                discF = -0.05; discR = -0.05;
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

        double seasonPressAdj = track.Season switch
        {
            Season.Winter => +0.05,
            Season.Spring => +0.02,
            Season.Autumn => -0.02,
            Season.Summer => -0.10,
            _             =>  0.00
        };
        tpF += seasonPressAdj;
        tpR += seasonPressAdj;

        double rimCorrF = CalculationHelpers.Clamp((car.FrontRimDiameter - 19) * 0.018, -0.10, 0.10);
        double rimCorrR = CalculationHelpers.Clamp((car.RearRimDiameter  - 19) * 0.018, -0.10, 0.10);

        r.TirePressureFront = Math.Round(CalculationHelpers.Clamp(tpF + discF + rimCorrF, c.TirePressureFrontMin, c.TirePressureFrontMax), 2);
        r.TirePressureRear  = Math.Round(CalculationHelpers.Clamp(tpR + discR + rimCorrR, c.TirePressureRearMin,  c.TirePressureRearMax),  2);
        ex["TirePressure"] = string.Format(CalculationHelpers.L("Expl_TirePressure_Fmt"), r.TirePressureFront, r.TirePressureRear, reason);
    }
}