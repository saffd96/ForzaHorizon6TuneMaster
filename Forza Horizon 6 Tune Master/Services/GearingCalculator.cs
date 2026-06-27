using System;
using System.Collections.Generic;
using System.Linq;
using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

internal static class GearingCalculator
{
    // Hardware ceiling on gear count (the in-game gearbox tops out at 10 speeds).
    private const int HardMaxGears = 10;

    // Final-drive min/max is no longer a hard-coded constant here — it comes from
    // TuningConstraints.FinalDriveMin/Max (single source of truth, defaults 2.2..6.0), threaded
    // in through CalculateGearing / PostValidateAndRecalculate.

    // Recommended number of gears — computed from physics, NOT copied from the fitted gearbox.
    // It answers "how many ratios does this engine want to stay in its strong powerband from a
    // standing pull to the speed it actually reaches here?" so it can differ from the box the
    // user has installed (advice: fit an N-speed for best use of the engine).
    //
    // Model: the ratio set steps DOWN at the discipline's gear spacing; each gear then covers a
    // SPEED factor of band = 1/avgStep while the engine sweeps its rev range. The gear count is
    // how many such steps span first-gear top speed → the speed the car actually reaches (the drag
    // terminal speed on a strip, so a short ¼-mile naturally wants fewer gears than a full mile —
    // distance enters through physics, not a hard cap). Using the SAME spacing the ratios are
    // built from keeps the count and the listed ratios coherent — there are no separate
    // gear-count magic numbers, and it no longer collapses to "10 for everything" the way an
    // engine-powerband band did on engines whose power peaks near the limiter.
    public static int CalcRecommendedGearCount(CarCard car, TrackInfo track, SelectedParts parts,
        Fh6DatabaseService db, double effectiveMaxKmh, TuningConstraints constraints)
    {
        int hardMax = car.MaxAvailableGearCount > 0 ? Math.Min(car.MaxAvailableGearCount, HardMaxGears) : HardMaxGears;

        // Electric motors hold near-flat torque to the limiter, so one ratio already keeps them
        // in their efficient band across the whole speed range — a single gear is optimal.
        if (car.PowertrainType == PowertrainType.Electric)
            return 1;

        double rpmShift = Math.Max(car.MaxRPM * CalculationHelpers.RevLimitFraction, 1000.0);

        double pwRatio = car.PowerHP / Math.Max(car.TotalMass / 1000.0, 0.1);
        var (firstGear, stepMin, stepMax, _) = GetDisciplineGearParams(track.Discipline, pwRatio, car.FuelType);

        // Speed factor a gear covers. Start from the discipline's own ratio spacing (the per-shift
        // RPM drop a tuner accepts → keeps the count coherent with the ratios that get built), then
        // let the ENGINE'S character nudge it: a PEAKY engine (torque peaks high, near the limiter)
        // only pulls up top and wants closer gears (smaller band → more gears); a FLAT, torquey one
        // pulls from low RPM and can run wider gears (fewer). torquePeakRatio ≈ 0.45 (flat) … 0.78
        // (peaky); the effect is bounded so it refines the count by ~a gear rather than dominating
        // (an unbounded powerband band collapsed to "10 for everything" / "2 for everything").
        double avgStep = CalculationHelpers.Clamp((stepMin + stepMax) / 2.0, 0.55, 0.90);
        double bandDisc = 1.0 / avgStep;
        double tqRatio = car.MaxRPM > 0 && car.TorquePeakRPM > 0
            ? (double)car.TorquePeakRPM / car.MaxRPM : 0.55;
        double engineFactor = CalculationHelpers.Clamp(1.0 + (0.55 - tqRatio) * 0.45, 0.90, 1.12);
        double band = CalculationHelpers.Clamp(bandDisc * engineFactor, 1.28, 1.62);

        double tireCirc = Math.PI * car.DrivenWheelDiameterInch * PhysicsConstants.InchToMeter;

        // Speed the car actually reaches here — empirical trap speed for drag strips,
        // terminal velocity for all other disciplines.
        double vTop = track.Discipline == Discipline.Drag
            ? CalculateDragTrapSpeedKmh(car, track.DragDistance, effectiveMaxKmh)
            : effectiveMaxKmh;
        if (vTop < 1.0) return Math.Clamp(6, 2, hardMax);

        // First-gear top speed at the shift point, from the gearbox's stock final drive (the
        // actual FD is optimised later but doesn't change the gear COUNT for normal cars).
        var trans = TuningPhysicsContext.Transmission(car, parts, db);
        double fdNom = CalculationHelpers.Clamp(
            trans.FinalDriveRatio > 0 ? trans.FinalDriveRatio : 3.5,
            constraints.FinalDriveMin, constraints.FinalDriveMax);
        // First-gear top speed. A very high redline doesn't earn a proportionally taller 1st gear
        // in practice — the box is geared shorter — so measuring 1st gear at the full shift RPM
        // makes an 11,000-rpm V12 read a ~140 km/h 1st gear, shrinking the speed span and
        // under-counting gears. Reference the 1st-gear speed at a typical redline so only genuinely
        // high-revving engines are affected; normal cars (≤ ~7,900 rpm) are unchanged.
        double rpmForFirst = Math.Min(rpmShift, 7500.0);
        double kFirst = rpmForFirst * tireCirc * PhysicsConstants.MsToKmh / 60.0;
        double vFirstKmh = Math.Max(kFirst / (fdNom * Math.Max(firstGear, 0.1)), 1.0);

        vTop = Math.Max(vTop, vFirstKmh * 1.05);
        double n = 1.0 + Math.Log(vTop / vFirstKmh) / Math.Log(band);
        return Math.Clamp((int)Math.Round(n), 2, hardMax);
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

        // Power-to-weight adjustment: more power → taller first gear (lower numerical ratio).
        // Linear ramp up to pwRatio ≈ 317 (where the old cap of 0.50 was reached), then
        // logarithmic continuation so extreme builds (2500 HP / 1700 kg → pwRatio ≈ 1470)
        // get meaningful further reduction instead of being clipped at the same 0.50 cap
        // as a modest 350 HP build.
        double pwAdj;
        const double linearLimit = 0.50;
        const double linearPwCeiling = 150.0 + linearLimit / 0.30 * 100.0; // ≈ 317
        if (pwRatio <= linearPwCeiling)
            pwAdj = (pwRatio - 150.0) / 100.0 * 0.30;
        else
            pwAdj = linearLimit + Math.Log(pwRatio / linearPwCeiling) * 0.35;

        first -= CalculationHelpers.Clamp(pwAdj, -0.45, 1.20);

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

    // Physics-based initial first gear for drag — replaces the old fixed table value.
    // Shorter first gear = higher wheel torque = faster launch IF grip permits;
    // too short → wheelspin. The iterative loop adjusts this further.
    internal static double CalcDragInitialFirstGear(CarCard car, SelectedParts parts, Fh6DatabaseService db)
    {
        var clutch = TuningPhysicsContext.Clutch(car, parts, db);
        var compound = TuningPhysicsContext.TireCompound(car, parts, db);
        var tireData = compound != null ? db.GetTireCompound(compound.TireCompoundID) : null;
        double tireGrip = tireData?.TorqueFreeLongFrictionScaleAccel0 ?? 1.0;

        double gripScore = clutch.ClutchFriction * tireGrip;

        double driveMul = car.DriveType switch
        {
            DriveType.AWD => 1.25,
            DriveType.FWD => 0.70,
            _ => 1.00
        };

        double pwRatio = car.PowerHP / Math.Max(car.TotalMass / 1000.0, 0.1);
        double pwFactor = Math.Clamp(1.0 + (pwRatio - 150.0) / 300.0, 0.50, 1.80);
        if (car.FuelType == FuelType.Diesel)
            pwFactor = Math.Max(pwFactor, 1.20);

        double first = 3.5 * driveMul / (gripScore * pwFactor);
        return Math.Clamp(first, 1.5, 5.5);
    }

    // Fraction of v-max actually reached at the end of the strip — used to target the final drive.
    // A quarter mile tops out well short of v-max; a full mile nearly reaches it.
    // ⚠️  DEPRECATED in favour of CalculateDragTrapSpeedKmh for the *target speed* feed —
    // kept for CalcRecommendedGearCount's vTop (still terminal-velocity-based for gear-count
    // estimation) and forward-compat.
    internal static double DragSpeedFactor(DragDistance dist) => dist switch
    {
        DragDistance.Quarter => 0.82,
        DragDistance.Half    => 0.91,
        _                    => 1.00, // Mile
    };

    // Empirical drag-strip trap speed using the Hale formula:
    //   trap_mph ≈ 234 × (HP / weight_lb) ^ ⅓
    // Converted to km/h and scaled by strip distance. This replaces the old
    // terminal-velocity × DragSpeedFactor approach which produced absurd target
    // speeds (~490 km/h for a 2500 HP car on a quarter mile — no car traps that
    // fast), cascading into too-tall final drives and unusable gearing.
    //
    // The formula is capped at 500 km/h (the absolute quarter-mile trap-speed
    // ceiling of any real vehicle) and floored at 80 km/h.
    internal static double CalculateDragTrapSpeedKmh(CarCard car, DragDistance distance, double effectiveMaxKmh)
    {
        double weightLb = car.TotalMass * 2.20462;
        double powerPerLb = car.PowerHP / Math.Max(weightLb, 1.0);
        double trapMph = 234.0 * Math.Pow(powerPerLb, 1.0 / 3.0);
        double trapKmhQ = trapMph * 1.60934; // quarter-mile baseline

        // Scale for longer strips. These factors are calibrated to keep the FD
        // reasonable across all three distances while staying within the physically
        // reachable range for each distance.
        double distanceFactor = distance switch
        {
            DragDistance.Quarter => 1.00,
            DragDistance.Half    => 1.25,
            DragDistance.Mile    => 1.50,
            _                    => 1.00
        };

        double trapKmh = trapKmhQ * distanceFactor;

        // Sanity cap: the trap speed must not exceed what the car can physically
        // reach (the stock top speed with this power), and the absolute floor is
        // 80 km/h for drivability.
        double physCeiling = Math.Max(effectiveMaxKmh * DragSpeedFactor(distance), trapKmh);
        return Math.Clamp(Math.Min(trapKmh, physCeiling), 80.0, 500.0);
    }

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
    internal static List<double> BuildDisciplineRatios(double first, double stepMin, double stepMax, int count, double topFloor = 0, double topCeiling = 0)
    {
        var list = new List<double>();
        if (count <= 0) return list;

        first = CalculationHelpers.Clamp(first, CalculationHelpers.GearRatioMin, CalculationHelpers.GearRatioMax);
        if (count == 1) { list.Add(Math.Round(first, 2)); return list; }

        // The top gear must stay within the range the final drive can gear to the target top speed:
        //   minTop  — tallest top gear (FD at its MAX); below it the geared top speed overshoots the
        //             car's real max (a high-revving engine never hits the limiter in top gear).
        //   maxTop  — shortest top gear (FD at its MIN); above it the car can't reach its top speed
        //             (a few-gear box undershoots). Both keep the geared max on the real max.
        double minTop = Math.Min(Math.Max(CalculationHelpers.GearRatioMin, topFloor), first - 0.01);
        double maxTop = topCeiling > 0 ? Math.Min(topCeiling, first - 0.01) : first - 0.01;
        if (maxTop < minTop) maxTop = minTop;

        // Raw ramped progression (tighter steps up top).
        var raw = new double[count];
        raw[0] = first;
        for (int i = 1; i < count; i++)
        {
            double t = count > 2 ? (double)(i - 1) / (count - 2) : 0.5;
            double step = stepMin + (stepMax - stepMin) * CalculationHelpers.Clamp(t, 0.0, 1.0);
            raw[i] = raw[i - 1] * step;
        }

        // Target top gear: the raw endpoint, pulled into the FD-reachable [minTop, maxTop] band.
        double rawTop = raw[count - 1];
        double top = CalculationHelpers.Clamp(rawTop, minTop, maxTop);

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
        Dictionary<string, string> ex, double effectiveMaxKmh, TuningConstraints? constraints = null, double? forceFirstGear = null)
    {
        if (!car.AllowGearCalculation)
        {
            ex["FinalDrive"] = CalculationHelpers.L("Expl_FinalDrive_NoCalc");
            return;
        }

        // Final-drive bounds come from the user constraints (single source of truth) rather than a
        // duplicated hard-coded range; the defaults match the old [2.2, 6.0].
        var c = constraints ?? new TuningConstraints();

        // Transmission() always resolves a part (synthetic fallback when none in DB), so it is
        // never null here.
        var trans = TuningPhysicsContext.Transmission(car, parts, db);

        double targetRpmFraction = CalculationHelpers.RevLimitFraction > 0
            ? CalculationHelpers.RevLimitFraction
            : 0.95;

        // On the drag strip the gearing targets an EMPIRICAL trap speed (Hale formula),
        // NOT the full terminal velocity × strip factor. The old terminal-velocity approach
        // produced absurd targets (~490 km/h for a 2500 HP car on a quarter mile), cascading
        // into too-tall final drives that made the car bog off the line.
        double targetKmh = track.Discipline == Discipline.Drag
            ? CalculateDragTrapSpeedKmh(car, track.DragDistance, effectiveMaxKmh)
            : Math.Min(effectiveMaxKmh, CalculationHelpers.TargetSpeedCapKmh);
        double targetMs = targetKmh / PhysicsConstants.MsToKmh;

        double tireCirc = Math.PI * car.DrivenWheelDiameterInch * PhysicsConstants.InchToMeter;

        // The ratio SET follows the FITTED gearbox's gear count (these ratios apply directly to
        // the box the user actually has), tailored to the DISCIPLINE (first-gear height + spacing)
        // rather than copied from stock — so e.g. rally/cross-country run short, close gears while
        // road/drag run a taller, wider spread. RecommendedGearCount is computed independently from
        // the engine's powerband (advisory: "the engine is happiest with N gears") and may
        // deliberately differ from the installed box.
        r.RecommendedGearCount = CalcRecommendedGearCount(car, track, parts, db, effectiveMaxKmh, c);
        int gearCount = c.ForceRecommendedGears
            ? r.RecommendedGearCount
            : trans.GearRatios.Take(trans.NumGears).Count(g => g > 0);

        double pwRatio = car.PowerHP / Math.Max(car.TotalMass / 1000.0, 0.1);
        var (firstGear, stepMin, stepMax, gearNote) =
            GetDisciplineGearParams(track.Discipline, pwRatio, car.FuelType);
        ApplyAspirationStepAdjustment(car.AspirationType, car.AntiLag, ref stepMin, ref stepMax);
        if (track.Discipline == Discipline.Drag)
            ApplyDragDistanceSpacing(track.DragDistance, ref stepMin, ref stepMax);
        if (forceFirstGear.HasValue)
            firstGear = forceFirstGear.Value;

        // Bound the top gear to what the final drive (within [Min, Max]) can gear to the target top
        // speed, so the geared top speed lands ON the car's real max — no overshoot for very
        // high-revving engines (FD would need to exceed its max), no undershoot for few-gear boxes
        // (FD would need to drop below its min). top gear = kFd / (FD × targetKmh).
        double kFd = car.MaxRPM * targetRpmFraction * tireCirc * PhysicsConstants.MsToKmh / 60.0;
        double topGearFloor = targetKmh > 1.0 ? kFd / (c.FinalDriveMax * targetKmh) : 0.0;
        double topGearCeiling = targetKmh > 1.0 && c.FinalDriveMin > 0.01 ? kFd / (c.FinalDriveMin * targetKmh) : 0.0;

        var ratios = BuildDisciplineRatios(firstGear, stepMin, stepMax, gearCount, topGearFloor, topGearCeiling);

        if (ratios.Count == 0)
        {
            r.FinalDrive = Math.Round(CalculationHelpers.Clamp(trans.FinalDriveRatio, c.FinalDriveMin, c.FinalDriveMax), 2);
            ex["FinalDrive"] = string.Format(CalculationHelpers.L("Expl_FinalDrive_OnlyFD"), r.FinalDrive);
            return;
        }

        double topGear = ratios[^1];
        double currentFd = trans.FinalDriveRatio > 0 ? trans.FinalDriveRatio : 3.5;

        // Actual top speed with the stock/default final drive.
        double actualTopKmh = car.MaxRPM > 0 && topGear > 0
            ? car.MaxRPM * targetRpmFraction * tireCirc / (60.0 * currentFd * topGear) * PhysicsConstants.MsToKmh
            : targetKmh;

        // Adjust final drive so the car reaches the target top speed in top gear.
        double newFd = actualTopKmh > 0 && targetKmh > 0 ? currentFd * actualTopKmh / targetKmh : currentFd;
        newFd = CalculationHelpers.Clamp(newFd, c.FinalDriveMin, c.FinalDriveMax);
        r.FinalDrive = Math.Round(newFd, 2);

        // For a single-gear transmission (or final-drive-only mode) keep the list empty.
        if (car.OnlyFinalDriveCalculation || ratios.Count == 1)
        {
            ex["FinalDrive"] = string.Format(CalculationHelpers.L("Expl_FinalDrive_OnlyFD"), r.FinalDrive)
                + " " + gearNote;
            if (!car.OnlyFinalDriveCalculation)
                r.GearRatios = ratios;
            return;
        }

        r.GearRatios = ratios;
        string gearStr = string.Join("  ", ratios.Select((g, i) => $"{i + 1}: {g:F2}"));
        ex["FinalDrive"] = string.Format(CalculationHelpers.L("Expl_FinalDrive_MultiGear"),
            r.FinalDrive, gearStr, effectiveMaxKmh, r.ActualMaxSpeedKmh, car.MaxRPM, gearNote);
    }

    public static void PostValidateAndRecalculate(CarCard car, TrackInfo track, SelectedParts parts, Fh6DatabaseService db,
        TuneResult r, Dictionary<string, string> ex, ref double effectiveMaxKmh, TuningConstraints? constraints = null)
    {
        var c = constraints ?? new TuningConstraints();
        bool anyChange = false;

        for (int iter = 0; iter < 2; iter++)
        {
            bool changed = false;

            if (car.AllowGearCalculation && r.GearRatios.Count > 0 && r.FinalDrive > 0)
            {
                double actual = r.ActualMaxSpeedKmh;
                double target = track.Discipline == Discipline.Drag
                    ? CalculateDragTrapSpeedKmh(car, track.DragDistance, effectiveMaxKmh)
                    : Math.Min(effectiveMaxKmh, CalculationHelpers.TargetSpeedCapKmh);

                if (actual > 0 && target > 0 && (actual < target * 0.97 || actual > target * 1.05))
                {
                    // Top speed ∝ 1/FinalDrive, so to move `actual` toward `target` the
                    // final drive scales by actual/target (too fast → raise FD to shorten).
                    double ratio = actual / target;
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
                    AlignmentCalculator.CalculateCamber(car, track, parts, db, r, ex, effectiveMaxKmh);
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
                r.FinalDrive, gearStr, effectiveMaxKmh, actualSpd, car.MaxRPM);
        }
    }

    private static bool RpmDropFix(CarCard car, TuneResult r)
    {
        if (!car.AllowGearCalculation || r.GearRatios.Count < 2) return false;
        if (car.MaxRPM <= 0 || car.TorquePeakRPM <= 0 || car.PowerPeakRPM <= 0) return false;

        double shiftRpm = car.PowerPeakRPM;

        // Cap minSafeRpm: for engines where TorquePeakRPM is very high (highly-tuned
        // turbos, peaky NA builds), TorquePeakRPM × 0.90 can approach the shift RPM
        // itself, causing the fix to compress EVERY gear ratio into a narrow band and
        // produce "ужасные коротки передачи" (terribly short gears).  The cap at 65 %
        // of shiftRPM means the fix only activates when the RPM drop is genuinely
        // excessive (engine falls below 65 % of its power peak), not when it merely
        // dips below an unusually high torque peak.
        double minSafeRpm = Math.Min(car.TorquePeakRPM * 0.90, shiftRpm * 0.65);

        // Per-gear ratio increase cap: prevent the fix from inflating a single gear
        // by more than 25 % in one pass — more than that signals a discipline-spacing
        // mismatch rather than a true RPM-drop problem, and the fix would cascade
        // through the whole set.
        const double maxRatioIncrease = 1.25;

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
                    double maxAllowedRatio = Math.Min(
                        r.GearRatios[i] - 0.01,
                        r.GearRatios[i + 1] * maxRatioIncrease);
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
