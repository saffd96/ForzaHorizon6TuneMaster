using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

internal static class BrakeCalculator
{
    public static void CalculateBrakes(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r, Dictionary<string, string> ex, double effectiveMaxKmh)
    {
        double bias = CalculationHelpers.EffectiveWtDist(car);

        double discAdj = track.Discipline switch
        {
            Discipline.Drift        => -5.0,
            Discipline.Drag         => -4.0,
            Discipline.Rally        => -2.0,
            Discipline.CrossCountry => -3.0,
            _                       => 0.0
        };
        bias += discAdj;

        if (car.DriveType == Models.DriveType.FWD)
            bias += 4.0;

        double pressure = track.Discipline switch
        {
            Discipline.Drift  => 85,
            Discipline.Rally  => 90,
            Discipline.CrossCountry => 85,
            _                 => 100
        };
        pressure += (car.TotalMass - CalculationHelpers.MassBaselineKg) / 200.0 * 2.5;
        pressure += Math.Max(0, (effectiveMaxKmh - CalculationHelpers.RefSpeedKmh) / 100.0 * 5.0);
        if (car.DriveType == Models.DriveType.AWD) pressure += 5;
        if (car.TireType is TireType.Slick or TireType.SemiSlick) pressure += 5.0;

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
