using System;
using System.Collections.Generic;
using System.Linq;
using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

internal static class GearingCalculator
{
    private const double FdMin = 2.2, FdMax = 6.0;

    public static int CalcRecommendedGearCount(CarCard car, TrackInfo track, double effectiveMaxKmh) =>
        CalcRecommendedGearCount(car, track, new SelectedParts(), Fh6DatabaseService.Instance, effectiveMaxKmh);

    public static int CalcRecommendedGearCount(CarCard car, TrackInfo track, SelectedParts parts, Fh6DatabaseService db, double effectiveMaxKmh)
    {
        if (car.PowertrainType == PowertrainType.Electric)
            return track.Discipline == Discipline.Drag ? 1 : 2;

        var trans = TuningPhysicsContext.Transmission(car, parts, db);
        if (trans == null) return 1;

        int maxGears = parts.TransmissionPartId != null ? trans.NumGears : car.GearCount;
        if (track.Discipline == Discipline.Drag && maxGears > 4)
            maxGears = 4;

        return Math.Clamp(maxGears, 1, car.MaxAvailableGearCount > 0 ? car.MaxAvailableGearCount : 10);
    }

    public static (double first, double stepMin, double stepMax, string note) GetDisciplineGearParams(
        Discipline discipline, double pwRatio, FuelType fuelType)
    {
        (double first, double stepMin, double stepMax, string noteKey) = discipline switch
        {
            Discipline.Drift => (3.0, 0.70, 0.88, "Expl_GearNote_Drift"),
            Discipline.Rally => (4.0, 0.68, 0.78, "Expl_GearNote_Rally"),
            Discipline.CrossCountry => (4.5, 0.66, 0.75, "Expl_GearNote_CrossCountry"),
            Discipline.Touge => (3.8, 0.70, 0.84, "Expl_GearNote_Touge"),
            _ => (3.5, 0.68, 0.82, "Expl_GearNote_Road")
        };
        string note = CalculationHelpers.L(noteKey);
        first -= CalculationHelpers.Clamp((pwRatio - 150.0) / 100.0 * 0.30, -0.45, 0.50);
        if (fuelType == FuelType.Diesel)
            first = Math.Max(first - 0.45, 1.5);
        return (first, stepMin, stepMax, note);
    }

    public static void ApplyAspirationStepAdjustment(AspirationType? aspiration, bool antiLag, ref double stepMin,
        ref double stepMax)
    {
        switch (aspiration ?? AspirationType.Natural)
        {
            case AspirationType.Centrifugal: stepMax -= 0.08; break;
            case AspirationType.SingleTurbo when !antiLag: stepMax -= 0.04; break;
            case AspirationType.SingleTurbo: stepMax -= 0.02; break;
            case AspirationType.TwinTurbo when !antiLag: stepMax -= 0.02; break;
            case AspirationType.TwinTurbo: stepMax -= 0.01; break;
            case AspirationType.Electric:
                stepMin += 0.05;
                stepMax += 0.05;
                break;
        }
    }

    public static void CalculateGearing(CarCard car, TrackInfo track, SelectedParts parts, Fh6DatabaseService db, TuneResult r,
        Dictionary<string, string> ex, double effectiveMaxKmh)
    {
        r.RecommendedGearCount = CalcRecommendedGearCount(car, track, parts, db, effectiveMaxKmh);

        if (!car.AllowGearCalculation)
        {
            ex["FinalDrive"] = string.Format(CalculationHelpers.L("Expl_FinalDrive_NoCalc"), r.RecommendedGearCount);
            return;
        }

        var trans = TuningPhysicsContext.Transmission(car, parts, db);
        if (trans == null)
        {
            ex["FinalDrive"] = string.Format(CalculationHelpers.L("Expl_FinalDrive_NoCalc"), r.RecommendedGearCount);
            return;
        }

        double targetRpmFraction = CalculationHelpers.RevLimitFraction > 0
            ? CalculationHelpers.RevLimitFraction
            : 0.95;

        double targetKmh = track.Discipline == Discipline.Drag
            ? effectiveMaxKmh
            : Math.Min(effectiveMaxKmh, CalculationHelpers.TargetSpeedCapKmh);
        double targetMs = targetKmh / 3.6;

        double tireCirc = Math.PI * car.DrivenWheelDiameterInch * 0.0254;

        // Build the ratio list from the selected transmission, skipping invalid entries.
        var ratios = trans.GearRatios
            .Take(trans.NumGears)
            .Where(g => g > 0)
            .Select(g => Math.Round(CalculationHelpers.Clamp(g, CalculationHelpers.GearRatioMin, CalculationHelpers.GearRatioMax), 2))
            .ToList();

        if (ratios.Count == 0)
        {
            r.FinalDrive = Math.Round(CalculationHelpers.Clamp(trans.FinalDriveRatio, FdMin, FdMax), 2);
            ex["FinalDrive"] = string.Format(CalculationHelpers.L("Expl_FinalDrive_OnlyFD"), r.FinalDrive, r.RecommendedGearCount);
            return;
        }

        double topGear = ratios[^1];
        double currentFd = trans.FinalDriveRatio > 0 ? trans.FinalDriveRatio : 3.5;

        // Actual top speed with the stock/default final drive.
        double actualTopKmh = car.MaxRPM > 0 && topGear > 0
            ? car.MaxRPM * targetRpmFraction * tireCirc / (60.0 * currentFd * topGear) * 3.6
            : targetKmh;

        // Adjust final drive so the car reaches the target top speed in top gear.
        double newFd = actualTopKmh > 0 && targetKmh > 0 ? currentFd * actualTopKmh / targetKmh : currentFd;
        newFd = CalculationHelpers.Clamp(newFd, FdMin, FdMax);
        r.FinalDrive = Math.Round(newFd, 2);

        // For a single-gear transmission (or final-drive-only mode) keep the list empty.
        if (car.OnlyFinalDriveCalculation || ratios.Count == 1)
        {
            ex["FinalDrive"] = string.Format(CalculationHelpers.L("Expl_FinalDrive_OnlyFD"), r.FinalDrive, r.RecommendedGearCount)
                + " " + CalculationHelpers.L("Expl_GearNote_Road");
            if (!car.OnlyFinalDriveCalculation)
                r.GearRatios = ratios;
            return;
        }

        r.GearRatios = ratios;
        string gearStr = string.Join("  ", ratios.Select((g, i) => $"{i + 1}: {g:F2}"));
        ex["FinalDrive"] = string.Format(CalculationHelpers.L("Expl_FinalDrive_MultiGear"),
            r.FinalDrive, r.RecommendedGearCount, gearStr, effectiveMaxKmh, r.ActualMaxSpeedKmh, car.MaxRPM, CalculationHelpers.L("Expl_GearNote_Road"));
    }

    public static void PostValidateAndRecalculate(CarCard car, TrackInfo track, SelectedParts parts, Fh6DatabaseService db,
        TuneResult r, Dictionary<string, string> ex, ref double effectiveMaxKmh)
    {
        bool anyChange = false;

        for (int iter = 0; iter < 2; iter++)
        {
            bool changed = false;

            if (car.AllowGearCalculation && r.GearRatios.Count > 0 && r.FinalDrive > 0)
            {
                double actual = r.ActualMaxSpeedKmh;
                double target = track.Discipline == Discipline.Drag
                    ? effectiveMaxKmh
                    : Math.Min(effectiveMaxKmh, CalculationHelpers.TargetSpeedCapKmh);

                if (actual > 0 && target > 0 && (actual < target * 0.97 || actual > target * 1.05))
                {
                    double ratio = target / actual;
                    double newFd = CalculationHelpers.Clamp(r.FinalDrive * ratio, FdMin, FdMax);
                    if (Math.Abs(newFd - r.FinalDrive) > 0.01)
                    {
                        r.FinalDrive = Math.Round(newFd, 2);
                        changed = true;
                    }
                }
            }

            if (RpmDropFix(car, r))
                changed = true;

            if (SuspensionCalculator.SpringRideHeightFix(car, track, parts, r))
                changed = true;

            if (changed)
            {
                double newEff = CalculationHelpers.ComputeEffectiveMaxSpeedKmh(car, r);
                if (Math.Abs(newEff - effectiveMaxKmh) > 1)
                {
                    effectiveMaxKmh = newEff;
                    AeroCalculator.CalculateAero(car, track, parts, db, r, ex, effectiveMaxKmh);
                    effectiveMaxKmh = CalculationHelpers.ComputeEffectiveMaxSpeedKmh(car, r);
                }
            }

            if (!changed) break;
            anyChange = true;
        }

        if (anyChange)
        {
            string gearStr = r.GearRatios.Count > 0
                ? string.Join("  ", r.GearRatios.Select((g, i) => $"{i + 1}: {g:F2}"))
                : "N/A";
            double actualSpd = r.ActualMaxSpeedKmh;
            ex["FinalDrive"] = string.Format(CalculationHelpers.L("Expl_FinalDrive_Verified"),
                r.FinalDrive, r.RecommendedGearCount, gearStr, effectiveMaxKmh, actualSpd, car.MaxRPM);
        }
    }

    private static bool RpmDropFix(CarCard car, TuneResult r)
    {
        if (!car.AllowGearCalculation || r.GearRatios.Count < 2) return false;
        if (car.MaxRPM <= 0 || car.TorquePeakRPM <= 0 || car.PowerPeakRPM <= 0) return false;

        double shiftRpm = car.PowerPeakRPM;
        double minSafeRpm = car.TorquePeakRPM * 0.90;
        bool anyFixed = false;

        for (int pass = 0; pass < 5; pass++)
        {
            bool anyDrop = false;
            for (int i = r.GearRatios.Count - 2; i >= 0; i--)
            {
                if (r.GearRatios[i] == 0) continue;
                double rpmAfter = shiftRpm * r.GearRatios[i + 1] / r.GearRatios[i];
                if (rpmAfter < minSafeRpm)
                {
                    double minRatio = r.GearRatios[i + 1] * (minSafeRpm / rpmAfter);
                    double maxAllowedRatio = r.GearRatios[i] - 0.01;
                    double clampedRatio = CalculationHelpers.Clamp(minRatio,
                        CalculationHelpers.GearRatioMin, maxAllowedRatio);
                    double newRatio = Math.Round(clampedRatio, 2);

                    if (newRatio != r.GearRatios[i + 1])
                    {
                        r.GearRatios[i + 1] = newRatio;
                        anyDrop = true;
                        anyFixed = true;
                    }
                }
            }

            if (!anyDrop) break;
        }

        return anyFixed;
    }
}
