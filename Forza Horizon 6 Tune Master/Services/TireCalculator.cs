using System;
using System.Collections.Generic;
using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

internal static class TireCalculator
{
    private const double PsiToBar = 0.0689476;

    public static void CalculateTirePressure(CarCard car, TrackInfo track, TuningConstraints _, TuneResult r, Dictionary<string, string> ex) =>
        CalculateTirePressure(car, track, new SelectedParts(), Fh6DatabaseService.Instance, r, ex);

    public static void CalculateTirePressure(CarCard car, TrackInfo track, SelectedParts parts, Fh6DatabaseService db, TuneResult r, Dictionary<string, string> ex)
    {
        var compound = TuningPhysicsContext.TireCompound(car, parts, db);
        var baseCompound = compound != null ? db.GetTireCompound(compound.TireCompoundID) : null;

        double baseF = compound?.FrontTirePressure ?? 30.0;
        double baseR = compound?.RearTirePressure  ?? 30.0;

        double barF = baseF * PsiToBar;
        double barR = baseR * PsiToBar;

        // Load compensation: the DB scaler tells us how aggressively pressure should rise with mass.
        double massScaler = baseCompound?.PressureIncreaseWithMassScaler > 0
            ? baseCompound.PressureIncreaseWithMassScaler
            : 1.0;
        double massRef = 1400.0;
        double massAdj = (car.TotalMass - massRef) / 1000.0 * massScaler * 0.12;

        double wdF = CalculationHelpers.EffectiveWtDist(car) / 100.0;
        barF += massAdj * wdF;
        barR += massAdj * (1.0 - wdF);

        // Width correction: wider tyres need marginally less pressure for the same contact patch.
        double widthAdjF = Math.Tanh((275 - car.FrontTireWidth) / 100.0) * 0.05;
        double widthAdjR = Math.Tanh((275 - car.RearTireWidth)  / 100.0) * 0.05;
        barF += widthAdjF;
        barR += widthAdjR;

        // Discipline offsets
        double discF = 0, discR = 0;
        string reason;
        switch (track.Discipline)
        {
            case Discipline.Drag:
            {
                discF = 0.35;
                double dragPtw = car.PowerHP / (car.TotalMass / 1000.0);
                double dragPtwFactor = CalculationHelpers.Clamp((dragPtw - 100) / 400, 0, 1);
                double dragMassFactor = CalculationHelpers.Clamp((car.TotalMass - 1000) / 1000, 0, 1);
                double dragDistFactor = track.DragDistance switch
                {
                    DragDistance.Quarter => 0.04,
                    DragDistance.Half    => 0.08,
                    DragDistance.Mile    => 0.14,
                    _                    => 0.00
                };
                discR = -0.10 - dragMassFactor * 0.10 + dragPtwFactor * 0.08 + dragDistFactor;
                reason = CalculationHelpers.L("Expl_TirePressureReason_Drag");
                break;
            }
            case Discipline.Drift:
                discF = -0.45;
                discR = -0.45;
                reason = CalculationHelpers.L("Expl_TirePressureReason_Drift");
                break;
            case Discipline.Rally:
                discF = -0.04; discR = -0.04;
                reason = CalculationHelpers.L("Expl_TirePressureReason_Rally");
                break;
            case Discipline.CrossCountry:
                discF = -0.04; discR = -0.04;
                reason = CalculationHelpers.L("Expl_TirePressureReason_CrossCountry");
                break;
            case Discipline.Touge:
                discF = -0.10; discR = -0.08;
                reason = CalculationHelpers.L("Expl_TirePressureReason_Touge");
                break;
            case Discipline.Street:
                discR = 0.04;
                reason = CalculationHelpers.L("Expl_TirePressureReason_Street");
                break;
            default:
                reason = CalculationHelpers.L("Expl_TirePressureReason_Road");
                break;
        }

        barF += discF;
        barR += discR;

        // Season
        double seasonPressAdj = track.Season switch
        {
            Season.Winter => +0.04,
            Season.Spring => +0.02,
            Season.Autumn => -0.02,
            Season.Summer => -0.08,
            _             =>  0.00
        };
        barF += seasonPressAdj;
        barR += seasonPressAdj;

        const double tpMin = 1.0, tpMax = 3.5;
        r.TirePressureFront = Math.Round(CalculationHelpers.Clamp(barF, tpMin, tpMax), 2);
        r.TirePressureRear  = Math.Round(CalculationHelpers.Clamp(barR, tpMin, tpMax), 2);
        ex["TirePressure"] = string.Format(CalculationHelpers.L("Expl_TirePressure_Fmt"), r.TirePressureFront, r.TirePressureRear, reason);
    }
}
