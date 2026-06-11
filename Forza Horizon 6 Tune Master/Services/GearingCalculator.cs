using System;
using System.Collections.Generic;
using System.Linq;
using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

internal static class GearingCalculator
{
    public static int CalcRecommendedGearCount(CarCard car, TrackInfo track, double effectiveMaxKmh)
    {
        if (car.PowertrainType == PowertrainType.Electric)
            return track.Discipline == Discipline.Drag ? 1 : 2;

        double pwRatio = car.PowerHP / (Math.Max(car.TotalMass, 1.0) / 1000.0);

        double first, top;
        if (track.Discipline == Discipline.Drag)
        {
            (first, _) = GetDragRatios(car, track.DragDistance, pwRatio);
            double targetKmh = effectiveMaxKmh;
            double targetMs = targetKmh / 3.6;
            double targetRpmFraction =
                CalculationHelpers.RevLimitFraction > 0 ? CalculationHelpers.RevLimitFraction : 0.95;
            double totalRatio = CalcTotalRatio(car, targetMs, targetRpmFraction);
            double estFd = 3.5;
            estFd *= 1.0 + Math.Max(0, (pwRatio - 150) / 200.0 * 0.05);
            top = CalculationHelpers.Clamp(totalRatio / estFd, CalculationHelpers.GearRatioMin, first);
        }
        else
        {
            (first, _, _, _) = GetDisciplineGearParams(track.Discipline, pwRatio, car.FuelType);

            double targetKmh = Math.Min(effectiveMaxKmh, CalculationHelpers.TargetSpeedCapKmh);
            double targetMs = targetKmh / 3.6;

            double targetRpmFraction =
                CalculationHelpers.RevLimitFraction > 0 ? CalculationHelpers.RevLimitFraction : 0.95;
            double totalRatio = CalcTotalRatio(car, targetMs, targetRpmFraction);

            double estFd = track.Discipline switch
            {
                Discipline.Drift => 4.2,
                Discipline.Rally => 4.0,
                Discipline.CrossCountry => 4.3,
                _ => 3.5
            };

            estFd *= 1.0 + Math.Max(0, (pwRatio - 150) / 200.0 * 0.05);

            top = CalculationHelpers.Clamp(totalRatio / estFd, CalculationHelpers.GearRatioMin, first);
        }

        double idealStep;

        if (track.Discipline == Discipline.Drag)
        {
            double tqPerKg = car.TorqueNm / Math.Max(car.TotalMass, 500.0);
            if (tqPerKg > 0.80) idealStep = 0.70;
            else if (tqPerKg < 0.40) idealStep = 0.58;
            else idealStep = 0.65;
        }
        else
        {
            (_, double stepMin, double stepMax, _) = GetDisciplineGearParams(track.Discipline, pwRatio, car.FuelType);
            ApplyAspirationStepAdjustment(car.AspirationType, car.AntiLag, ref stepMin, ref stepMax);

            idealStep = (stepMin + stepMax) / 2.0;

            if (car.AspirationType == AspirationType.TwinTurbo ||
                car.AspirationType == AspirationType.SingleTurbo ||
                car.AspirationType == AspirationType.Electric)
            {
                idealStep = Math.Max(idealStep, 0.76);
            }
        }

        double ratioSpread = first / Math.Max(top, 0.01);

        int rec;
        if (idealStep >= 1.0 || ratioSpread <= 1.0)
        {
            rec = 1;
        }
        else
        {
            double logSpread = Math.Log(ratioSpread);
            double logStep = Math.Log(1.0 / idealStep);
            rec = (int)Math.Round(1.0 + logSpread / logStep);
        }

        int maxGears = Math.Max(1, Math.Min(car.MaxAvailableGearCount, 10));
        return Math.Clamp(rec, 1, maxGears);
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

    public static void CalculateGearing(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r,
        Dictionary<string, string> ex, double effectiveMaxKmh)
    {
        r.RecommendedGearCount = CalcRecommendedGearCount(car, track, effectiveMaxKmh);

        if (!car.AllowGearCalculation)
        {
            ex["FinalDrive"] = string.Format(CalculationHelpers.L("Expl_FinalDrive_NoCalc"), r.RecommendedGearCount);
            return;
        }

        int n = Math.Max(1, Math.Min(car.GearCount, 10));

        double pwRatio = car.PowerHP / (Math.Max(car.TotalMass, 1.0) / 1000.0);
        double tireCirc = Math.PI * car.DrivenWheelDiameterInch * 0.0254;

        double targetRpmFraction = CalculationHelpers.RevLimitFraction > 0 
            ? CalculationHelpers.RevLimitFraction 
            : 0.95;
        
        double targetKmh = track.Discipline == Discipline.Drag
            ? effectiveMaxKmh
            : Math.Min(effectiveMaxKmh, CalculationHelpers.TargetSpeedCapKmh);
        double targetMs = targetKmh / 3.6;

        if (n == 1)
        {
            double total = CalcTotalRatio(car, targetMs, targetRpmFraction);

            double g1 = track.Discipline switch
            {
                Discipline.Drag => GetDragRatios(car, track.DragDistance, pwRatio).first,
                Discipline.CrossCountry => 4.5,
                Discipline.Rally => 4.0,
                Discipline.Touge => 3.8,
                Discipline.Drift => 3.0,
                _ => 3.5
            };
            g1 = Math.Max(1.0, g1);

            double fd1 = CalculationHelpers.Clamp(total / g1, c.FinalDriveMin, c.FinalDriveMax);
            g1 = CalculationHelpers.Clamp(total / fd1, CalculationHelpers.GearRatioMin,
                CalculationHelpers.GearRatioMax);
            fd1 = CalculationHelpers.Clamp(total / g1, c.FinalDriveMin, c.FinalDriveMax);

            g1 = Math.Round(g1, 2);
            fd1 = Math.Round(fd1, 2);
            r.FinalDrive = fd1;
            if (car.OnlyFinalDriveCalculation)
            {
                ex["FinalDrive"] = string.Format(CalculationHelpers.L("Expl_FinalDrive_OnlyFD"), fd1,
                    r.RecommendedGearCount);
            }
            else
            {
                r.GearRatios = new List<double> { g1 };
                ex["FinalDrive"] = string.Format(CalculationHelpers.L("Expl_FinalDrive_SingleGear"),
                    fd1, g1, g1, fd1, g1 * fd1, effectiveMaxKmh, r.ActualMaxSpeedKmh, car.MaxRPM,
                    r.RecommendedGearCount);
            }

            return;
        }

        double first, targetTop;
        string note;

        if (track.Discipline == Discipline.Drag)
        {
            (first, note) = GetDragRatios(car, track.DragDistance, pwRatio);
            double totalRatio = CalcTotalRatio(car, targetMs, targetRpmFraction);
            double estFd = 3.5;
            estFd *= 1.0 + Math.Max(0, (pwRatio - 150) / 200.0 * 0.05);
            estFd = CalculationHelpers.Clamp(estFd, c.FinalDriveMin, c.FinalDriveMax);
            targetTop = totalRatio / estFd;
        }
        else
        {
            double stepMin, stepMax;
            (first, stepMin, stepMax, note) = GetDisciplineGearParams(track.Discipline, pwRatio, car.FuelType);

            ApplyAspirationStepAdjustment(car.AspirationType, car.AntiLag, ref stepMin, ref stepMax);
            stepMin = Math.Max(0.50, stepMin);
            if (n <= 4) stepMax -= 0.04;
            else if (n <= 5) stepMax -= 0.02;
            double minLimit = stepMin + 0.05;
            double maxLimit = Math.Max(minLimit + 0.01, 0.95);
            stepMax = CalculationHelpers.Clamp(stepMax, minLimit, maxLimit);

            double totalRatio = CalcTotalRatio(car, targetMs, targetRpmFraction);

            double estFd = track.Discipline switch
            {
                Discipline.Drift => 4.2,
                Discipline.Rally => 4.0,
                Discipline.CrossCountry => 4.3,
                _ => 3.5
            };
            estFd *= 1.0 + Math.Max(0, (pwRatio - 150) / 200.0 * 0.05);
            estFd = CalculationHelpers.Clamp(estFd, c.FinalDriveMin, c.FinalDriveMax);

            targetTop = totalRatio / estFd;
        }

        first = CalculationHelpers.Clamp(first, CalculationHelpers.GearRatioMin, CalculationHelpers.GearRatioMax);
        targetTop = CalculationHelpers.Clamp(targetTop, CalculationHelpers.GearRatioMin, first);

        double degFactor = track.Discipline switch
        {
            Discipline.Drift => 1.02,
            Discipline.Rally => CalculationHelpers.GearDegradeRallyFactor,
            Discipline.CrossCountry => CalculationHelpers.GearDegradeRallyFactor,
            _ => 1.04,
        };

        double bandWidth = car.MaxRPM > 0 && car.PowerPeakRPM > 0 && car.TorquePeakRPM > 0
            ? (double)(car.PowerPeakRPM - car.TorquePeakRPM) / car.MaxRPM
            : 0.28;
        degFactor += (0.28 - bandWidth) * 0.20;

        degFactor += (car.AspirationType, car.AntiLag) switch
        {
            (AspirationType.Centrifugal, _) => 0.01,
            (AspirationType.SingleTurbo, false) => 0.01,
            (AspirationType.Electric, _) => -0.02,
            _ => 0.00,
        };
        if (car.FuelType == FuelType.Diesel) degFactor -= 0.01;

        degFactor = CalculationHelpers.Clamp(degFactor, 1.01, 1.07);

        var ratios = new List<double>(n);
        if (n <= 2)
        {
            for (int i = 0; i < n; i++)
            {
                double t = (n == 1) ? 0 : (double)i / (n - 1);
                double ratio = first * Math.Pow(targetTop / first, t);
                ratios.Add(Math.Round(
                    CalculationHelpers.Clamp(ratio, CalculationHelpers.GearRatioMin, CalculationHelpers.GearRatioMax),
                    2));
            }
        }
        else
        {
            double spread = targetTop / first;
            double degExp = (n - 1) * (n - 2) / 2.0;
            double s0 = Math.Pow(spread / Math.Pow(degFactor, degExp), 1.0 / (n - 1));
            double ratio = first;
            ratios.Add(Math.Round(
                CalculationHelpers.Clamp(ratio, CalculationHelpers.GearRatioMin, CalculationHelpers.GearRatioMax), 2));
            double stepCur = s0;
            for (int i = 1; i < n; i++)
            {
                ratio *= stepCur;
                ratios.Add(Math.Round(
                    CalculationHelpers.Clamp(ratio, CalculationHelpers.GearRatioMin, CalculationHelpers.GearRatioMax),
                    2));
                stepCur *= degFactor;
            }

            ratios[n - 1] =
                Math.Round(
                    CalculationHelpers.Clamp(targetTop, CalculationHelpers.GearRatioMin,
                        CalculationHelpers.GearRatioMax), 2);
        }

        double actualTop = ratios[n - 1];
        double fd = targetMs > 0 && car.MaxRPM > 0 && actualTop > 0
            ? car.MaxRPM * targetRpmFraction * tireCirc / (60.0 * targetMs * actualTop)
            : 3.50;

        r.FinalDrive = Math.Round(CalculationHelpers.Clamp(fd, c.FinalDriveMin, c.FinalDriveMax), 2);

        if (car.OnlyFinalDriveCalculation)
        {
            ex["FinalDrive"] =
                string.Format(CalculationHelpers.L("Expl_FinalDrive_OnlyFD"), r.FinalDrive, r.RecommendedGearCount) +
                " " + note;
            return;
        }

        r.GearRatios = ratios;
        string gearStr = string.Join("  ", ratios.Select((g, i) => $"{i + 1}: {g:F2}"));
        double actualKmhMulti = r.ActualMaxSpeedKmh;
        ex["FinalDrive"] = string.Format(CalculationHelpers.L("Expl_FinalDrive_MultiGear"),
            r.FinalDrive, r.RecommendedGearCount, gearStr, effectiveMaxKmh, actualKmhMulti, car.MaxRPM, note);

        if (car.MaxRPM > 0 && car.TorquePeakRPM > 0 && car.PowerPeakRPM > 0 && ratios.Count > 1)
        {
            double shiftRpm = car.PowerPeakRPM;
            double minSafeRpm = car.TorquePeakRPM * 0.90;
            var dropWarnings = new List<string>();
            for (int i = 0; i < ratios.Count - 1; i++)
            {
                double rpmAfterShift = shiftRpm * ratios[i + 1] / ratios[i];
                if (rpmAfterShift < minSafeRpm)
                    dropWarnings.Add($"{i + 1}→{i + 2}: {rpmAfterShift:F0} {CalculationHelpers.L("Expl_RpmAbbr")}");
            }

            if (dropWarnings.Count > 0)
                ex["FinalDrive"] += string.Format(CalculationHelpers.L("Expl_FinalDrive_Warning"), minSafeRpm,
                    string.Join(", ", dropWarnings));
        }
    }

    public static void PostValidateAndRecalculate(CarCard car, TrackInfo track, TuningConstraints c,
        TuneResult r, Dictionary<string, string> ex, ref double effectiveMaxKmh)
    {
        bool anyChange = false;
        const int maxIter = 3;

        for (int iter = 0; iter < maxIter; iter++)
        {
            bool changed = false;

            if (car.AllowGearCalculation && r.GearRatios.Count > 0 && r.FinalDrive > 0)
            {
                double actual = r.ActualMaxSpeedKmh;
                double target = track.Discipline == Discipline.Drag
                    ? effectiveMaxKmh
                    : Math.Min(effectiveMaxKmh, CalculationHelpers.TargetSpeedCapKmh);

                if (actual > 0 && target > 0 && actual < target * 0.97)
                {
                    double ratio = target / actual;
                    double newFd = CalculationHelpers.Clamp(r.FinalDrive / ratio, c.FinalDriveMin, c.FinalDriveMax);
                    if (Math.Abs(newFd - r.FinalDrive) > 0.01)
                    {
                        r.FinalDrive = Math.Round(newFd, 2);
                        changed = true;
                    }
                }
                else if (actual > target * 1.05 && track.Discipline != Discipline.Drag)
                {
                    double ratio = target / actual;
                    double newFd = CalculationHelpers.Clamp(r.FinalDrive * ratio, c.FinalDriveMin, c.FinalDriveMax);
                    if (Math.Abs(newFd - r.FinalDrive) > 0.01)
                    {
                        r.FinalDrive = Math.Round(newFd, 2);
                        changed = true;
                    }
                }
            }

            if (RpmDropFix(car, r))
                changed = true;

            if (SuspensionCalculator.SpringRideHeightFix(car, track, c, r))
                changed = true;

            if (changed)
            {
                double newEff = CalculationHelpers.ComputeEffectiveMaxSpeedKmh(car, r);
                if (Math.Abs(newEff - effectiveMaxKmh) > 1)
                {
                    effectiveMaxKmh = newEff;
                    AeroCalculator.CalculateAero(car, track, c, r, ex, effectiveMaxKmh);
                    effectiveMaxKmh = CalculationHelpers.ComputeEffectiveMaxSpeedKmh(car, r);
                }
            }

            if (!changed) break;
            anyChange = true;
        }

        if (anyChange)
        {
            string gearStr = r.GearRatios != null
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

    private static double CalcTotalRatio(CarCard car, double targetMs, double revFraction)
    {
        double tireCirc = Math.PI * car.DrivenWheelDiameterInch * 0.0254;
        return targetMs > 0 && car.MaxRPM > 0 && tireCirc > 0
            ? car.MaxRPM * revFraction * tireCirc / (60.0 * targetMs)
            : 9.0;
    }

    private static (double first, string note) GetDragRatios(CarCard car, DragDistance dist, double pwRatio)
    {
        double tqPerKg = car.TorqueNm / Math.Max(car.TotalMass, 500.0);
    
        // Базовый расчет БЕЗ промежуточного клемпа - пусть математика "дышит"
        double first = CalculationHelpers.DragFirstGearBaseline - (tqPerKg - 0.25) * 1.40;

        // Множитель типа привода
        first *= car.DriveType switch
        {
            DriveType.AWD => 1.05,
            DriveType.FWD => 0.92,
            _ => 1.0
        };

        double pwAdjustment;
        if (pwRatio > 500)
        {
            double excess = (pwRatio - 500) / 500.0;
            pwAdjustment = Math.Min(0.30, 0.50 / (1.0 + excess * 0.5));
        }
        else
        {
            pwAdjustment = CalculationHelpers.Clamp((pwRatio - 150.0) / 100.0 * 0.30, -0.45, 0.50);
        }
        first -= pwAdjustment;

        // Множитель дистанции драга
        double distFactor = dist switch
        {
            DragDistance.Quarter => 1.00,
            DragDistance.Half => 0.88,
            DragDistance.Mile => 0.70,
            _ => 1.0
        };
        first *= distFactor;

        first = Math.Round(CalculationHelpers.Clamp(first, 1.5, 5.5), 2);

        string distLabel = CalculationHelpers.L($"Expl_DragDistStr_{(int)dist}");
        string note = string.Format(CalculationHelpers.L("Expl_DragNote"), distLabel, first, tqPerKg,
            CalculationHelpers.L($"Enum_DriveType_{car.DriveType}"));
        
        return (first, note);
    }}