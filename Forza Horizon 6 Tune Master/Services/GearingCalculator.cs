using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

internal static class GearingCalculator
{
    public static int CalcRecommendedGearCount(CarCard car, TrackInfo track, double effectiveMaxKmh)
    {
        if (car.PowertrainType == PowertrainType.Electric)
            return track.Discipline == Discipline.Drag ? 1 : 2;

        if (track.Discipline == Discipline.Drag)
        {
            return track.DragDistance switch
            {
                DragDistance.Eighth  => 4,
                DragDistance.Quarter => 4,
                DragDistance.Half    => 4,
                _                    => 5,
            };
        }

        double pwRatio = car.PowerHP / (car.TotalMass / 1000.0);
        (double first, double stepMin, double stepMax, _) = GetDisciplineGearParams(track.Discipline, pwRatio, car.FuelType);
        ApplyAspirationStepAdjustment(car.AspirationType, car.AntiLag, ref stepMin, ref stepMax);
        stepMin = Math.Max(0.50, stepMin);
        stepMax = CalculationHelpers.Clamp(stepMax, stepMin + 0.05, 0.95);

        double stepIdeal = car.TorquePeakRPM > 0 && car.PowerPeakRPM > 0
            ? (double)car.TorquePeakRPM / car.PowerPeakRPM
            : (stepMin + stepMax) / 2.0;
        double step = CalculationHelpers.Clamp(stepIdeal, stepMin, stepMax);

        double tireCirc = Math.PI * car.DrivenWheelDiameterInch * 0.0254;
        double targetKmh = Math.Min(effectiveMaxKmh, CalculationHelpers.TargetSpeedCapKmh);
        double targetMs = targetKmh / 3.6;

        double totalRatio = targetMs > 0 && car.MaxRPM > 0 && tireCirc > 0
            ? car.MaxRPM * CalculationHelpers.RevLimitFraction * tireCirc / (60.0 * targetMs)
            : 9.0;
        double estFd = track.Discipline switch
        {
            Discipline.Drift        => 2.8,
            Discipline.Rally        => 4.0,
            Discipline.CrossCountry => 4.3,
            _                       => 3.5
        };
        double topEstimate = CalculationHelpers.Clamp(totalRatio / estFd, CalculationHelpers.GearRatioMin, first);

        double spread = Math.Max(topEstimate / first, 0.01);
        int rec = (int)Math.Round(1.0 + Math.Log(spread) / Math.Log(step));

        return Math.Clamp(rec, 4, 10);
    }

    public static (double first, double stepMin, double stepMax, string note) GetDisciplineGearParams(
        Discipline discipline, double pwRatio, FuelType fuelType)
    {
        (double first, double stepMin, double stepMax, string noteKey) = discipline switch
        {
            Discipline.Drift        => (3.0, 0.70, 0.88, "Expl_GearNote_Drift"),
            Discipline.Rally        => (4.0, 0.68, 0.78, "Expl_GearNote_Rally"),
            Discipline.CrossCountry => (4.5, 0.66, 0.75, "Expl_GearNote_CrossCountry"),
            Discipline.Touge        => (3.8, 0.70, 0.84, "Expl_GearNote_Touge"),
            _                       => (3.5, 0.68, 0.82, "Expl_GearNote_Road")
        };
        string note = CalculationHelpers.L(noteKey);

        first += CalculationHelpers.Clamp((pwRatio - 150.0) / 100.0 * 0.30, -0.45, 0.50);

        if (fuelType == FuelType.Diesel)
            first = Math.Max(first - 0.45, 1.5);

        return (first, stepMin, stepMax, note);
    }

    public static void ApplyAspirationStepAdjustment(AspirationType? aspiration, bool antiLag, ref double stepMin, ref double stepMax)
    {
        switch (aspiration ?? AspirationType.Natural)
        {
            case AspirationType.Centrifugal:            stepMax -= 0.08; break;
            case AspirationType.SingleTurbo when !antiLag: stepMax -= 0.04; break;
            case AspirationType.SingleTurbo:            stepMax -= 0.02; break;
            case AspirationType.TwinTurbo when !antiLag: stepMax -= 0.02; break;
            case AspirationType.TwinTurbo:              stepMax -= 0.01; break;
            case AspirationType.Electric:               stepMin += 0.05; stepMax += 0.05; break;
        }
    }

    public static void CalculateGearing(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r, Dictionary<string, string> ex, double effectiveMaxKmh)
    {
        r.RecommendedGearCount = CalcRecommendedGearCount(car, track, effectiveMaxKmh);

        if (!car.AllowGearCalculation)
        {
            ex["FinalDrive"] = string.Format(CalculationHelpers.L("Expl_FinalDrive_NoCalc"), r.RecommendedGearCount);
            return;
        }

        int n = Math.Max(1, Math.Min(car.GearCount, 10));

        double pwRatio = car.PowerHP / (car.TotalMass / 1000.0);
        double tireCirc = Math.PI * car.DrivenWheelDiameterInch * 0.0254;

        double targetKmh = track.Discipline == Discipline.Drag
            ? effectiveMaxKmh
            : Math.Min(effectiveMaxKmh, CalculationHelpers.TargetSpeedCapKmh);
        double targetMs = targetKmh / 3.6;

        if (n == 1)
        {
            double total = targetMs > 0 && car.MaxRPM > 0
                ? car.MaxRPM * CalculationHelpers.RevLimitFraction * tireCirc / (60.0 * targetMs)
                : 9.0;

            double g1 = track.Discipline switch
            {
                Discipline.Drag         => CalculationHelpers.Clamp(4.5 - (car.TorqueNm / Math.Max(car.TotalMass, 500.0) - 0.25) * 1.40, 2.0, 5.5),
                Discipline.CrossCountry => 4.5,
                Discipline.Rally        => 4.0,
                Discipline.Touge        => 3.8,
                Discipline.Drift        => 3.0,
                _                       => 3.5
            };
            g1 += CalculationHelpers.Clamp((pwRatio - 150.0) / 100.0 * 0.30, -0.45, 0.50);
            g1 = Math.Max(1.0, g1);

            double fd1 = CalculationHelpers.Clamp(total / g1, c.FinalDriveMin, c.FinalDriveMax);
            g1 = CalculationHelpers.Clamp(total / fd1, CalculationHelpers.GearRatioMin, CalculationHelpers.GearRatioMax);
            fd1 = CalculationHelpers.Clamp(total / g1, c.FinalDriveMin, c.FinalDriveMax);

            g1  = Math.Round(g1,  2);
            fd1 = Math.Round(fd1, 2);
            r.FinalDrive = fd1;
            if (car.OnlyFinalDriveCalculation)
            {
                ex["FinalDrive"] = string.Format(CalculationHelpers.L("Expl_FinalDrive_OnlyFD"), fd1, r.RecommendedGearCount);
            }
            else
            {
                r.GearRatios = new List<double> { g1 };
                ex["FinalDrive"] = string.Format(CalculationHelpers.L("Expl_FinalDrive_SingleGear"),
                    fd1, g1, g1, fd1, g1 * fd1, effectiveMaxKmh, r.ActualMaxSpeedKmh, car.MaxRPM, r.RecommendedGearCount);
            }
            return;
        }

        double first, top;
        string note;

        if (track.Discipline == Discipline.Drag)
        {
            (first, top, note) = GetDragRatios(car, track.DragDistance);
        }
        else
        {
            double stepMin, stepMax;
            (first, stepMin, stepMax, note) = GetDisciplineGearParams(track.Discipline, pwRatio, car.FuelType);

            ApplyAspirationStepAdjustment(car.AspirationType, car.AntiLag, ref stepMin, ref stepMax);
            stepMin = Math.Max(0.50, stepMin);
            if (n <= 4) stepMax -= 0.04;
            else if (n <= 5) stepMax -= 0.02;
            stepMax = CalculationHelpers.Clamp(stepMax, stepMin + 0.05, 0.95);

            double stepIdeal = car.PowerPeakRPM > 0
                ? (double)car.TorquePeakRPM / car.PowerPeakRPM
                : (stepMin + stepMax) / 2.0;
            double step = CalculationHelpers.Clamp(stepIdeal, stepMin, stepMax);
            top = first * Math.Pow(step, n - 1);
        }

        first = CalculationHelpers.Clamp(first, CalculationHelpers.GearRatioMin, CalculationHelpers.GearRatioMax);
        top   = CalculationHelpers.Clamp(top,   CalculationHelpers.GearRatioMin, first);

        double degFactor = track.Discipline switch
        {
            Discipline.Drift        => 1.02,
            Discipline.Rally        => 1.05,
            Discipline.CrossCountry => 1.05,
            _                       => 1.04,
        };

        double bandWidth = car.MaxRPM > 0 && car.PowerPeakRPM > 0 && car.TorquePeakRPM > 0
            ? (double)(car.PowerPeakRPM - car.TorquePeakRPM) / car.MaxRPM
            : 0.28;
        degFactor += (0.28 - bandWidth) * 0.20;

        degFactor += (car.AspirationType, car.AntiLag) switch
        {
            (AspirationType.Centrifugal, _)      =>  0.01,
            (AspirationType.SingleTurbo, false)  =>  0.01,
            (AspirationType.Electric, _)         => -0.02,
            _                                    =>  0.00,
        };
        if (car.FuelType == FuelType.Diesel) degFactor -= 0.01;

        degFactor = CalculationHelpers.Clamp(degFactor, 1.01, 1.07);

        var ratios = new List<double>(n);
        if (n <= 2)
        {
            if (n == 1)
            {
                ratios.Add(Math.Round(CalculationHelpers.Clamp(first, CalculationHelpers.GearRatioMin, CalculationHelpers.GearRatioMax), 2));
            }
            else
            {
                for (int i = 0; i < n; i++)
                {
                    double t = i;
                    ratios.Add(Math.Round(CalculationHelpers.Clamp(first * Math.Pow(top / first, t), CalculationHelpers.GearRatioMin, CalculationHelpers.GearRatioMax), 2));
                }
            }
        }
        else
        {
            double spread  = top / first;
            double degExp  = (n - 1) * (n - 2) / 2.0;
            double s0      = Math.Pow(spread / Math.Pow(degFactor, degExp), 1.0 / (n - 1));
            double ratio   = first;
            ratios.Add(Math.Round(CalculationHelpers.Clamp(ratio, CalculationHelpers.GearRatioMin, CalculationHelpers.GearRatioMax), 2));
            double stepCur = s0;
            for (int i = 1; i < n; i++)
            {
                ratio *= stepCur;
                ratios.Add(Math.Round(CalculationHelpers.Clamp(ratio, CalculationHelpers.GearRatioMin, CalculationHelpers.GearRatioMax), 2));
                stepCur *= degFactor;
            }
        }

        double fd = targetMs > 0 && car.MaxRPM > 0
            ? car.MaxRPM * CalculationHelpers.RevLimitFraction * tireCirc / (60.0 * targetMs * top)
            : 3.50;
        if (track.Discipline != Discipline.Drag)
            fd *= 1.0 + Math.Max(0, (pwRatio - 150) / 200.0 * 0.05);

        r.FinalDrive = Math.Round(CalculationHelpers.Clamp(fd, c.FinalDriveMin, c.FinalDriveMax), 2);

        if (car.OnlyFinalDriveCalculation)
        {
            ex["FinalDrive"] = string.Format(CalculationHelpers.L("Expl_FinalDrive_OnlyFD"), r.FinalDrive, r.RecommendedGearCount) + " " + note;
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
                ex["FinalDrive"] += string.Format(CalculationHelpers.L("Expl_FinalDrive_Warning"), minSafeRpm, string.Join(", ", dropWarnings));
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
            }

            if (RpmDropFix(car, r))
                changed = true;

            if (SpringRideHeightFix(car, track, c, r))
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
            string gearStr = string.Join("  ", r.GearRatios.Select((g, i) => $"{i + 1}: {g:F2}"));
            double actualSpd = r.ActualMaxSpeedKmh;
            double tgt = track.Discipline == Discipline.Drag
                ? effectiveMaxKmh
                : Math.Min(effectiveMaxKmh, CalculationHelpers.TargetSpeedCapKmh);
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
            for (int i = 0; i < r.GearRatios.Count - 1; i++)
            {
                double rpmAfter = shiftRpm * r.GearRatios[i + 1] / r.GearRatios[i];
                if (rpmAfter < minSafeRpm)
                {
                    double minRatio = r.GearRatios[i + 1] * (minSafeRpm / rpmAfter);
                    r.GearRatios[i + 1] = Math.Round(Math.Min(r.GearRatios[i],
                        CalculationHelpers.Clamp(minRatio, CalculationHelpers.GearRatioMin, CalculationHelpers.GearRatioMax)), 2);
                    anyDrop = true;
                    anyFixed = true;
                }
            }
            if (!anyDrop) break;
        }

        return anyFixed;
    }

    private static bool SpringRideHeightFix(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r)
    {
        if (!car.SuspensionAllowsAdvancedTuning) return false;

        // Off-road disciplines intentionally run high ride height with compliant springs — don't override
        bool offRoad = track.Discipline is Discipline.Rally or Discipline.CrossCountry;

        double rhRangeF = Math.Max(1.0, c.RideHeightFrontMax - c.RideHeightFrontMin);
        double rhRangeR = Math.Max(1.0, c.RideHeightRearMax - c.RideHeightRearMin);
        double rhFracF = (r.RideHeightFront - c.RideHeightFrontMin) / rhRangeF;
        double rhFracR = (r.RideHeightRear - c.RideHeightRearMin) / rhRangeR;

        double sprRangeF = Math.Max(1.0, c.SpringFrontMax - c.SpringFrontMin);
        double sprRangeR = Math.Max(1.0, c.SpringRearMax - c.SpringRearMin);
        double sprFracF = (r.SpringFront - c.SpringFrontMin) / sprRangeF;
        double sprFracR = (r.SpringRear - c.SpringRearMin) / sprRangeR;

        bool changed = false;

        if (!offRoad && rhFracF < 0.25 && sprFracF < 0.40)
        {
            double newSpr = CalculationHelpers.Clamp(c.SpringFrontMin + sprRangeF * 0.50, c.SpringFrontMin, c.SpringFrontMax);
            r.SpringFront = Math.Round(newSpr);
            changed = true;
        }
        if (!offRoad && rhFracR < 0.25 && sprFracR < 0.40)
        {
            double newSpr = CalculationHelpers.Clamp(c.SpringRearMin + sprRangeR * 0.50, c.SpringRearMin, c.SpringRearMax);
            r.SpringRear = Math.Round(newSpr);
            changed = true;
        }

        if (rhFracF > 0.75 && sprFracF > 0.85)
        {
            double newSpr = CalculationHelpers.Clamp(c.SpringFrontMin + sprRangeF * 0.65, c.SpringFrontMin, c.SpringFrontMax);
            r.SpringFront = Math.Round(newSpr);
            changed = true;
        }
        if (rhFracR > 0.75 && sprFracR > 0.85)
        {
            double newSpr = CalculationHelpers.Clamp(c.SpringRearMin + sprRangeR * 0.65, c.SpringRearMin, c.SpringRearMax);
            r.SpringRear = Math.Round(newSpr);
            changed = true;
        }

        return changed;
    }

    private static (double first, double top, string note) GetDragRatios(CarCard car, DragDistance dist)
    {
        double tqPerKg = car.TorqueNm / Math.Max(car.TotalMass, 500.0);
        double first   = CalculationHelpers.Clamp(4.5 - (tqPerKg - 0.25) * 1.40, 2.2, 5.5);
        first *= car.DriveType switch
        {
            DriveType.AWD => 1.05,
            DriveType.FWD => 0.92,
            _             => 1.0
        };
        first = Math.Round(CalculationHelpers.Clamp(first, 2.0, 5.5), 2);

        double top = dist switch
        {
            DragDistance.Eighth  => 1.80,
            DragDistance.Quarter => 1.30,
            DragDistance.Half    => 1.00,
            DragDistance.Mile    => 0.85,
            _                    => 1.30
        };
        string distLabel = CalculationHelpers.L($"Expl_DragDistStr_{(int)dist}");
        string note = string.Format(CalculationHelpers.L("Expl_DragNote"), distLabel, first, tqPerKg, CalculationHelpers.L($"Enum_DriveType_{car.DriveType}"));
        return (first, top, note);
    }
}
