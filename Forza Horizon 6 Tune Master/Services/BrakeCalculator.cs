using System;
using System.Collections.Generic;
using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

internal static class BrakeCalculator
{
    public static void CalculateBrakes(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r, Dictionary<string, string> ex, double effectiveMaxKmh)
    {
        double bias = CalculationHelpers.EffectiveWtDist(car);

        double cgShift = car.EnginePosition switch
        {
            EnginePosition.Front => 2.5,   
            EnginePosition.Mid   => -2.5,  
            EnginePosition.Rear  => -1.5,  
            _                    => 0.0
        };
        bias += cgShift;

        if (track.Discipline == Discipline.Drag)
        {
            bias = 50.0; 
        }
        else
        {
            double discAdj = track.Discipline switch
            {
                Discipline.Drift        => -5.0,
                Discipline.Rally        => -1.5,
                Discipline.CrossCountry => -2.5,
                _                       => 0.0
            };
            bias += discAdj;
        }

        if (car.DriveType == Models.DriveType.FWD)
            bias -= 3.0;

        double pressure = 100.0;

        if (car.TotalMass > 1500)
            pressure += (car.TotalMass - 1500) / 250.0 * 4.0;
        else if (car.TotalMass < 1100)
            pressure -= 4.0;

        if (car.TireType is TireType.Slick or TireType.SemiSlick or TireType.Drag)
            pressure += 3.0;

        if (track.Discipline is Discipline.Rally or Discipline.CrossCountry)
            pressure -= 10.0;

        if (track.Discipline == Discipline.Drift)
            pressure -= 5.0;

        r.BrakeBalance  = Math.Round(CalculationHelpers.Clamp(bias,  c.BrakeBalanceMin,  c.BrakeBalanceMax));
        r.BrakePressure = Math.Round(CalculationHelpers.Clamp(pressure, c.BrakePressureMin, c.BrakePressureMax));

        string reason = track.Discipline switch
        {
            Discipline.Drift   => CalculationHelpers.L("Expl_BrakesReason_Drift"),
            Discipline.Drag    => CalculationHelpers.L("Expl_BrakesReason_Drag"),
            _                  => CalculationHelpers.L("Expl_BrakesReason_Default")
        };
        ex["Brakes"] = string.Format(CalculationHelpers.L("Expl_Brakes_Fmt"), r.BrakeBalance, r.BrakePressure, reason);
    }
}