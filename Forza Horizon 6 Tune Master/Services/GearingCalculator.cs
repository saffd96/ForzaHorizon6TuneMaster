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
            Discipline.Drag => (3.2, 0.74, 0.86, "Expl_GearNote_Drag"),
            Discipline.Street => (3.6, 0.69, 0.83, "Expl_GearNote_Street"),
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

    // Fraction of v-max actually reached at the end of the strip — used to target the final drive.
    // A quarter mile tops out well short of v-max; a full mile nearly reaches it.
    internal static double DragSpeedFactor(DragDistance dist) => dist switch
    {
        DragDistance.Quarter => 0.82,
        DragDistance.Half    => 0.91,
        _                    => 1.00, // Mile
    };

    // Shorter strips keep the gears closer together (less drop) so the car stays in the meat of its
    // power band to the trap; longer strips spread them out for terminal speed.
    internal static void ApplyDragDistanceSpacing(DragDistance dist, ref double stepMin, ref double stepMax)
    {
        switch (dist)
        {
            case DragDistance.Quarter: stepMin += 0.03; stepMax += 0.03; break;
            case DragDistance.Mile:    stepMin -= 0.03; stepMax -= 0.03; break;
            // Half = baseline
        }
    }

    // Build a descending ratio set from the discipline's first-gear height and gear spacing:
    // gear 1 = first, each next gear = previous × step, where step ramps from stepMin (big drops
    // between the low gears) up to stepMax (close ratios up top).
    //
    // Naively multiplying down can underflow the legal minimum on cars with many gears, which used
    // to leave the top gears all piled up on GearRatioMin (several identical 0.48 gears). To avoid
    // that we build the raw ramped shape, then — only if it underflows — re-fit it in log space
    // between the first gear and the floor, keeping every gear strictly descending and distinct.
    internal static List<double> BuildDisciplineRatios(double first, double stepMin, double stepMax, int count)
    {
        var list = new List<double>();
        if (count <= 0) return list;

        first = CalculationHelpers.Clamp(first, CalculationHelpers.GearRatioMin, CalculationHelpers.GearRatioMax);
        if (count == 1) { list.Add(Math.Round(first, 2)); return list; }

        // Raw ramped progression (tighter steps up top).
        var raw = new double[count];
        raw[0] = first;
        for (int i = 1; i < count; i++)
        {
            double t = count > 2 ? (double)(i - 1) / (count - 2) : 0.5;
            double step = stepMin + (stepMax - stepMin) * CalculationHelpers.Clamp(t, 0.0, 1.0);
            raw[i] = raw[i - 1] * step;
        }

        // Target top gear: the raw endpoint, but never below the floor and always below first.
        double rawTop = raw[count - 1];
        double top = Math.Min(Math.Max(rawTop, CalculationHelpers.GearRatioMin), first - 0.01);

        double logFirst = Math.Log(first);
        double denom = logFirst - Math.Log(rawTop);

        for (int i = 0; i < count; i++)
        {
            // f: 0 at the first gear, 1 at the top gear — preserves the ramp's spacing shape.
            double ratio = Math.Abs(denom) < 1e-9
                ? first
                : Math.Exp(logFirst - (logFirst - Math.Log(raw[i])) / denom * (logFirst - Math.Log(top)));
            list.Add(Math.Round(CalculationHelpers.Clamp(ratio, CalculationHelpers.GearRatioMin, CalculationHelpers.GearRatioMax), 2));
        }
        return list;
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

        // On the drag strip the gearing targets the TERMINAL speed reached at that distance
        // (a quarter mile tops out well short of v-max, a full mile nearly reaches it), which is
        // what makes the strip length actually change the final drive.
        double targetKmh = track.Discipline == Discipline.Drag
            ? effectiveMaxKmh * DragSpeedFactor(track.DragDistance)
            : Math.Min(effectiveMaxKmh, CalculationHelpers.TargetSpeedCapKmh);
        double targetMs = targetKmh / 3.6;

        double tireCirc = Math.PI * car.DrivenWheelDiameterInch * 0.0254;

        // The number of usable gears comes from the transmission, but the ratios themselves are
        // tailored to the DISCIPLINE (first-gear height + gear spacing) rather than copied from the
        // stock box — so e.g. rally/cross-country run short, close gears while road/drag run a
        // taller, wider spread. The final drive (below) then dials in the actual top speed.
        int gearCount = trans.GearRatios.Take(trans.NumGears).Count(g => g > 0);

        double pwRatio = car.PowerHP / Math.Max(car.TotalMass / 1000.0, 0.1);
        var (firstGear, stepMin, stepMax, gearNote) =
            GetDisciplineGearParams(track.Discipline, pwRatio, car.FuelType);
        ApplyAspirationStepAdjustment(car.AspirationType, car.AntiLag, ref stepMin, ref stepMax);
        if (track.Discipline == Discipline.Drag)
            ApplyDragDistanceSpacing(track.DragDistance, ref stepMin, ref stepMax);

        var ratios = BuildDisciplineRatios(firstGear, stepMin, stepMax, gearCount);

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
                + " " + gearNote;
            if (!car.OnlyFinalDriveCalculation)
                r.GearRatios = ratios;
            return;
        }

        r.GearRatios = ratios;
        string gearStr = string.Join("  ", ratios.Select((g, i) => $"{i + 1}: {g:F2}"));
        ex["FinalDrive"] = string.Format(CalculationHelpers.L("Expl_FinalDrive_MultiGear"),
            r.FinalDrive, r.RecommendedGearCount, gearStr, effectiveMaxKmh, r.ActualMaxSpeedKmh, car.MaxRPM, gearNote);
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
                    ? effectiveMaxKmh * DragSpeedFactor(track.DragDistance)
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
                if (r.GearRatios[i] == 0 || r.GearRatios[i + 1] == 0) continue;
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
