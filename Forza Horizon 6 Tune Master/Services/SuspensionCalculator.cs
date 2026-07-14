using System;
using System.Collections.Generic;
using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

internal static class SuspensionCalculator
{
    // Spring rates in the DB (MinSpringRate / DefSpringRate / MaxSpringRate) are stored in
    // N/mm — the same unit the tune result and the constraints use internally. Earlier code
    // mistook them for kgf/mm and multiplied by 9.807, inflating every spring ~9.8x and
    // pinning the result to the minimum. No spring-rate unit conversion is needed here.
    private const double MmToM = 0.001;

    public static void CalculateARB(CarCard car, TrackInfo track, TuningConstraints constraints, TuneResult r, Dictionary<string, string> ex) =>
        CalculateARB(car, track, new SelectedParts(), Fh6DatabaseService.Instance, r, ex, constraints);

    public static void CalculateARB(CarCard car, TrackInfo track, SelectedParts parts, TuneResult r, Dictionary<string, string> ex) =>
        CalculateARB(car, track, parts, Fh6DatabaseService.Instance, r, ex);

    public static void CalculateARB(CarCard car, TrackInfo track, SelectedParts parts, Fh6DatabaseService db, TuneResult r, Dictionary<string, string> ex, TuningConstraints? constraints = null)
    {
        var frontPhys = TuningPhysicsContext.FrontArb(car, parts, db);
        var rearPhys = TuningPhysicsContext.RearArb(car, parts, db);

        bool hasFront = frontPhys != null && (car.HasFrontARB || frontPhys.MaxSwaybarStiffness > frontPhys.MinSwaybarStiffness);
        bool hasRear = rearPhys != null && (car.HasRearARB || rearPhys.MaxSwaybarStiffness > rearPhys.MinSwaybarStiffness);

        if (!hasFront && !hasRear)
        {
            ex["ARB"] = CalculationHelpers.L("Expl_ARB_None");
            return;
        }

        double wdF = CalculationHelpers.EffectiveWtDist(car) / 100.0;
        double wdR = 1.0 - wdF;

        // Base ARB balance: RWD likes stiffer rear, FWD stiffer front, AWD balanced.
        (double baseFrontFraction, double baseRearFraction, string noteKey) = (track.Discipline, car.DriveType) switch
        {
            (Discipline.Drag, _)             => (0.85, 0.15, "Expl_ARBNote_Drag"),
            (Discipline.Drift, DriveType.RWD) => (0.55, 0.95, "Expl_ARBNote_DriftRWD"),
            (Discipline.Drift, DriveType.AWD) => (0.45, 0.85, "Expl_ARBNote_DriftAWD"),
            (Discipline.Drift, _)            => (0.40, 0.80, "Expl_ARBNote_DriftFWD"),
            (Discipline.Rally, _)            => (0.40, 0.45, "Expl_ARBNote_Rally"),
            (Discipline.CrossCountry, _)     => (0.30, 0.40, "Expl_ARBNote_CrossCountry"),
            (Discipline.Touge, DriveType.RWD) => (0.60, 0.85, "Expl_ARBNote_TougeRWD"),
            (Discipline.Touge, DriveType.FWD) => (0.70, 0.60, "Expl_ARBNote_TougeFWD"),
            (Discipline.Touge, _)            => (0.60, 0.75, "Expl_ARBNote_TougeAWD"),
            (Discipline.Street, DriveType.RWD) => (0.55, 0.80, "Expl_ARBNote_StreetRWD"),
            (Discipline.Street, DriveType.FWD) => (0.75, 0.60, "Expl_ARBNote_StreetFWD"),
            (_, DriveType.RWD)               => (0.50, 0.80, "Expl_ARBNote_RoadRWD"),
            (_, DriveType.FWD)               => (0.75, 0.55, "Expl_ARBNote_RoadFWD"),
            (_, _)                           => (0.55, 0.70, "Expl_ARBNote_RoadAWD")
        };

        // Driving-style bias: +1 (Agile) softens the front / stiffens the rear (relatively) so
        // the rear lets go first and the car rotates more readily; -1 (Stable) does the
        // opposite. Applies in Drag too — a softer front / stiffer rear balance there shifts
        // weight transfer at launch, not cornering rotation, but it's the same lever.
        double chassisRotation = constraints?.ChassisRotation ?? 0.0;
        if (chassisRotation != 0.0)
        {
            baseFrontFraction = CalculationHelpers.Clamp(baseFrontFraction - chassisRotation * 0.15, 0.0, 1.0);
            baseRearFraction = CalculationHelpers.Clamp(baseRearFraction + chassisRotation * 0.15, 0.0, 1.0);
        }

        // Adjust balance for weight distribution: move stiffness toward the heavier axle.
        double wdShiftF = (wdF - 0.5) * 0.20;
        double wdShiftR = (wdR - 0.5) * 0.20;

        // Lower grip -> slightly softer ARB to let the tire work.
        double seasonMul = CalculationHelpers.SeasonGripMultiplier(track.Season, 0.85, 0.15);

        // Wider track reduces the need for stiff anti-roll bars.
        double avgTrack = (car.FrontTrack + car.RearTrack) / 2.0;
        double trackMul = 1.0 - CalculationHelpers.Clamp((avgTrack - 1500.0) / 1000.0, -0.2, 0.3) * 0.15;

        // Heavier cars need a little more roll resistance.
        double massMul = 1.0 + (car.TotalMass - PhysicsConstants.RefMassKg) / 1000.0 * 0.10;

        double ComputeArb(DbAntiSwayPhysics phys, double baseFrac, double wdShift)
        {
            double frac = CalculationHelpers.Clamp(baseFrac + wdShift, 0.0, 1.0);
            double val = phys.MinSwaybarStiffness + (phys.MaxSwaybarStiffness - phys.MinSwaybarStiffness) * frac;
            return Math.Round(CalculationHelpers.Clamp(val * seasonMul * trackMul * massMul, phys.MinSwaybarStiffness, phys.MaxSwaybarStiffness), 1);
        }

        if (hasFront)
            r.ARBFront = ComputeArb(frontPhys!, baseFrontFraction, wdShiftF);
        if (hasRear)
            r.ARBRear  = ComputeArb(rearPhys!,  baseRearFraction,  wdShiftR);

        string note = CalculationHelpers.L(noteKey);
        ex["ARB"] = string.Format(CalculationHelpers.L("Expl_ARB_Fmt"), r.ARBFront, r.ARBRear, note);
    }

    public static void CalculateSprings(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r, Dictionary<string, string> ex) =>
        CalculateSprings(car, track, new SelectedParts(), Fh6DatabaseService.Instance, r, ex, c);

    public static void CalculateSprings(CarCard car, TrackInfo track, SelectedParts parts, TuneResult r, Dictionary<string, string> ex) =>
        CalculateSprings(car, track, parts, Fh6DatabaseService.Instance, r, ex);

    public static void CalculateSprings(CarCard car, TrackInfo track, SelectedParts parts, Fh6DatabaseService db, TuneResult r, Dictionary<string, string> ex, TuningConstraints? constraints = null)
    {
        var frontPhys = TuningPhysicsContext.FrontSpringDamper(car, parts, db);
        var rearPhys = TuningPhysicsContext.RearSpringDamper(car, parts, db);
        if (frontPhys == null || rearPhys == null)
        {
            ex["Springs"] = CalculationHelpers.L("Expl_Springs_Disabled");
            return;
        }

        if (!car.SuspensionAllowsAdvancedTuning)
        {
            r.SpringFront = 0;
            r.SpringRear  = 0;
            ex["Springs"] = CalculationHelpers.L("Expl_Springs_Disabled");
            return;
        }

        double wdF = CalculationHelpers.EffectiveWtDist(car) / 100.0;
        double massF = car.TotalMass * wdF / 2.0;
        double massR = car.TotalMass * (1.0 - wdF) / 2.0;

        (double hzF, double hzR) = TargetNaturalFrequency(track.Discipline, car.DriveType);

        // Heavier or more powerful cars can tolerate slightly higher frequencies.
        double massRef = PhysicsConstants.RefMassKg;
        double ptw = car.PowerHP / Math.Max(car.TotalMass, 1.0);
        double ptwRef = PhysicsConstants.PtwReferenceHpPerKg;
        double massFactor = Math.Pow(Math.Max(car.TotalMass / massRef, 0.5), 0.15); 
        
        double basePower = 1.0 + Math.Min(0.30, Math.Max(0, (ptw - ptwRef) / ptwRef) * 0.10);

        // Power-delivery character (turbo lag, electric instant torque) adds a small
        // frequency bias independent of raw power-to-weight.
        var delivery = CalculationHelpers.GetPowerDeliveryFactors(
            car.PowertrainType, car.AspirationType, car.AntiLag);

        // Drift: keep the rear softer so it breaks loose under power. The front
        // still gets the full power stiffening for turn-in precision.
        double powerFactorR = basePower;
        if (track.Discipline == Discipline.Drift)
            powerFactorR = 1.0 + (basePower - 1.0) * 0.55;

        hzF *= massFactor * basePower * delivery.Spring;
        hzR *= massFactor * powerFactorR * delivery.Spring;

        // Softer springs in low-grip seasons and with off-road suspension.
        double seasonMul = CalculationHelpers.SeasonGripMultiplier(track.Season, 0.70, 0.30);
        if (car.SuspensionUpgrade == SuspensionUpgrade.Offroad)
            seasonMul *= 0.90;
        hzF *= seasonMul;
        hzR *= seasonMul;

        // Driving-style bias: same lever as ARB/dampers — +1 (Agile) softens the front spring /
        // stiffens the rear (relatively) so the front lets go of load faster and the car rotates
        // more readily, -1 (Stable) does the opposite.
        double chassisRotation = constraints?.ChassisRotation ?? 0.0;
        hzF *= 1.0 - chassisRotation * 0.06;
        hzR *= 1.0 + chassisRotation * 0.06;

        double springF_nmm = SpringFromHz(hzF, massF);
        double springR_nmm = SpringFromHz(hzR, massR);

        // Clamp to the spring's physical bounds — the game's tuning slider uses
        // [MinSpringRate, MaxSpringRate] as its range (linear interpolation).
        //springF_nmm = CalculationHelpers.Clamp(springF_nmm, frontPhys.MinSpringRate, frontPhys.MaxSpringRate);
        //springR_nmm = CalculationHelpers.Clamp(springR_nmm, rearPhys.MinSpringRate, rearPhys.MaxSpringRate);

        r.SpringFront = Math.Round(springF_nmm, 1);
        r.SpringRear  = Math.Round(springR_nmm, 1);

        ex["Springs"] = string.Format(CalculationHelpers.L("Expl_Springs_Fmt"),
            r.SpringFront, r.SpringRear, hzF, hzR, CalculationHelpers.L($"Enum_SuspensionUpgrade_{car.SuspensionUpgrade}"));
    }

    public static void CalculateRideHeight(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r, Dictionary<string, string> ex) =>
        CalculateRideHeight(car, track, new SelectedParts(), Fh6DatabaseService.Instance, r, ex, c);

    public static void CalculateRideHeight(CarCard car, TrackInfo track, SelectedParts parts, TuneResult r, Dictionary<string, string> ex) =>
        CalculateRideHeight(car, track, parts, Fh6DatabaseService.Instance, r, ex);

    public static void CalculateRideHeight(CarCard car, TrackInfo track, SelectedParts parts, Fh6DatabaseService db, TuneResult r, Dictionary<string, string> ex, TuningConstraints? constraints = null)
    {
        var c = constraints ?? new TuningConstraints();
        var frontPhys = TuningPhysicsContext.FrontSpringDamper(car, parts, db);
        var rearPhys = TuningPhysicsContext.RearSpringDamper(car, parts, db);
        if (frontPhys == null || rearPhys == null)
        {
            ex["RideHeight"] = CalculationHelpers.L("Expl_RideHeight_Disabled");
            return;
        }

        if (!car.SuspensionAllowsAdvancedTuning)
        {
            r.RideHeightFront = 0;
            r.RideHeightRear  = 0;
            ex["RideHeight"] = CalculationHelpers.L("Expl_RideHeight_Disabled");
            return;
        }

        (double fFrac, double rFrac, string noteKey) = RideHeightFractions(track.Discipline, car.DriveType);

        // Rake: front engine cars keep a little nose-down attitude for turn-in.
        double rake = car.EnginePosition switch
        {
            EnginePosition.Front => -0.03,
            EnginePosition.Rear  =>  0.03,
            _                    =>  0.0
        };
        fFrac += rake;
        rFrac -= rake;

        // Larger diameter wheels need more ride height to keep the same suspension geometry.
        double avgRim = (car.FrontRimDiameter + car.RearRimDiameter) / 2.0;
        double rimOffset = (avgRim - 19.0) * 0.005;
        fFrac += rimOffset;
        rFrac += rimOffset;

        // Softer disciplines (dirt) run higher; lower for road.
        double seasonFactor = CalculationHelpers.GetSeasonGripFactor(track.Season);
        double seasonOffset = (1.0 - seasonFactor) * 0.08;
        fFrac += seasonOffset;
        rFrac += seasonOffset;

        // Driving-style bias: same lever as the engine-position rake above — +1 (Agile) lowers
        // the front / raises the rear (more rake -> more front bite, more rotation), -1 (Stable)
        // does the opposite (less/reverse rake -> more front grip retention, more understeer).
        double chassisRotation = c.ChassisRotation;
        fFrac -= chassisRotation * 0.04;
        rFrac += chassisRotation * 0.04;

        double rhF = InterpolateHeight(frontPhys, CalculationHelpers.Clamp(fFrac, 0.0, 1.0));
        double rhR = InterpolateHeight(rearPhys,  CalculationHelpers.Clamp(rFrac, 0.0, 1.0));

        // Clamp to the user's adjustable constraints if they are provided.
        rhF = CalculationHelpers.Clamp(rhF, c.RideHeightFrontMin, c.RideHeightFrontMax);
        rhR = CalculationHelpers.Clamp(rhR, c.RideHeightRearMin,  c.RideHeightRearMax);

        r.RideHeightFront = Math.Round(rhF, 1);
        r.RideHeightRear  = Math.Round(rhR, 1);
        ex["RideHeight"] = string.Format(CalculationHelpers.L("Expl_RideHeight_Fmt"), r.RideHeightFront, r.RideHeightRear, CalculationHelpers.L(noteKey));
    }

    public static void CalculateDampers(CarCard car, TrackInfo track, TuningConstraints constraints, TuneResult r, Dictionary<string, string> ex) =>
        CalculateDampers(car, track, new SelectedParts(), Fh6DatabaseService.Instance, r, ex, constraints);

    public static void CalculateDampers(CarCard car, TrackInfo track, SelectedParts parts, TuneResult r, Dictionary<string, string> ex) =>
        CalculateDampers(car, track, parts, Fh6DatabaseService.Instance, r, ex);

    public static void CalculateDampers(CarCard car, TrackInfo track, SelectedParts parts, Fh6DatabaseService db, TuneResult r, Dictionary<string, string> ex, TuningConstraints? constraints = null)
    {
        var frontPhys = TuningPhysicsContext.FrontSpringDamper(car, parts, db);
        var rearPhys = TuningPhysicsContext.RearSpringDamper(car, parts, db);
        if (frontPhys == null || rearPhys == null)
        {
            ex["Dampers"] = CalculationHelpers.L("Expl_Dampers_Disabled");
            return;
        }

        if (!car.SuspensionAllowsAdvancedTuning)
        {
            r.ReboundFront = 0;
            r.ReboundRear  = 0;
            r.BumpFront    = 0;
            r.BumpRear     = 0;
            ex["Dampers"] = CalculationHelpers.L("Expl_Dampers_Disabled");
            return;
        }

        double wdF = CalculationHelpers.EffectiveWtDist(car) / 100.0;
        double massF = car.TotalMass * wdF / 2.0;
        double massR = car.TotalMass * (1.0 - wdF) / 2.0;

        // Spring rates are N/mm; *NmmToNm -> N/m for the critical-damping math.
        double springF_Nm = r.SpringFront > 0 ? r.SpringFront * PhysicsConstants.NmmToNm : frontPhys.DefSpringRate * PhysicsConstants.NmmToNm;
        double springR_Nm = r.SpringRear  > 0 ? r.SpringRear  * PhysicsConstants.NmmToNm : rearPhys.DefSpringRate  * PhysicsConstants.NmmToNm;

        double dampingRatio = track.Discipline switch
        {
            Discipline.Drag         => 0.55,
            Discipline.Drift        => 0.55,
            Discipline.Rally        => 0.75,
            Discipline.CrossCountry => 0.80,
            Discipline.Touge        => 0.70,
            Discipline.Street       => 0.65,
            _                       => 0.68
        };

        double bumpRatio = track.Discipline switch
        {
            Discipline.Drag         => 0.45,
            Discipline.Drift        => 0.50,
            Discipline.Rally        => 0.65,
            Discipline.CrossCountry => 0.70,
            Discipline.Touge        => 0.58,
            Discipline.Street       => 0.56,
            _                       => 0.58
        };

        // High-power cars need a touch more damping to control weight transfer.
        double ptwD = car.PowerHP / Math.Max(car.TotalMass, 1.0);
        
        // Cap the power factor - same rationale as CalculateSprings (line 122).
        double powerFactorD = 1.0 + Math.Min(0.40, Math.Max(0, ptwD - PhysicsConstants.PtwReferenceHpPerKg) * 0.20);
        dampingRatio *= powerFactorD;

        double seasonMul = CalculationHelpers.SeasonGripMultiplier(track.Season, 0.70, 0.35);

        // Driving-style bias: same lever as ARB — +1 (Agile) softens the front / stiffens the
        // rear (relatively) so weight transfers off the front faster on turn-in (or at launch),
        // -1 (Stable) does the opposite. Applied to both rebound and bump so the front/rear
        // balance shifts consistently, not just one damping phase.
        double chassisRotation = constraints?.ChassisRotation ?? 0.0;
        double dampFrontMul = 1.0 - chassisRotation * 0.12;
        double dampRearMul = 1.0 + chassisRotation * 0.12;

        double rebF = DampingFromSpringAndMass(springF_Nm, massF, frontPhys, dampingRatio, true) * seasonMul * dampFrontMul;
        double rebR = DampingFromSpringAndMass(springR_Nm, massR, rearPhys,  dampingRatio, true) * seasonMul * dampRearMul;
        double bmpF = DampingFromSpringAndMass(springF_Nm, massF, frontPhys, dampingRatio * bumpRatio, false) * seasonMul * dampFrontMul;
        double bmpR = DampingFromSpringAndMass(springR_Nm, massR, rearPhys,  dampingRatio * bumpRatio, false) * seasonMul * dampRearMul;

        r.ReboundFront = Math.Round(CalculationHelpers.Clamp(rebF, frontPhys.MinDampenReboundRate, frontPhys.MaxDampenReboundRate), 1);
        r.ReboundRear  = Math.Round(CalculationHelpers.Clamp(rebR, rearPhys.MinDampenReboundRate,  rearPhys.MaxDampenReboundRate), 1);
        r.BumpFront    = Math.Round(CalculationHelpers.Clamp(bmpF, frontPhys.MinDampenBumpRate,    frontPhys.MaxDampenBumpRate), 1);
        r.BumpRear     = Math.Round(CalculationHelpers.Clamp(bmpR, rearPhys.MinDampenBumpRate,     rearPhys.MaxDampenBumpRate), 1);

        ex["Dampers"] = string.Format(CalculationHelpers.L("Expl_Dampers_Fmt"),
            r.ReboundFront, r.ReboundRear, r.BumpFront, r.BumpRear,
            rebF > 0 ? bmpF / rebF * 100 : 0,
            rebR > 0 ? bmpR / rebR * 100 : 0);
    }

    // Spring / ride-height safety fix: if the selected spring is far outside the usable
    // range for the selected ride height, push it back into the workable band.
    public static bool SpringRideHeightFix(CarCard car, TrackInfo track, TuningConstraints _, TuneResult r) =>
        SpringRideHeightFix(car, track, new SelectedParts(), Fh6DatabaseService.Instance, r);

    public static bool SpringRideHeightFix(CarCard car, TrackInfo track, SelectedParts parts, TuneResult r) =>
        SpringRideHeightFix(car, track, parts, Fh6DatabaseService.Instance, r);

    public static bool SpringRideHeightFix(CarCard car, TrackInfo track, SelectedParts parts, Fh6DatabaseService db, TuneResult r)
    {
        var frontPhys = TuningPhysicsContext.FrontSpringDamper(car, parts, db);
        var rearPhys = TuningPhysicsContext.RearSpringDamper(car, parts, db);
        if (frontPhys == null || rearPhys == null) return false;
        if (!car.SuspensionAllowsAdvancedTuning) return false;

        bool changed = false;
        bool offRoad = track.Discipline is Discipline.Rally or Discipline.CrossCountry;

        // If the car is very low and the spring is near the soft end, stiffen it to avoid bottoming.
        double rhMinF = frontPhys.MinRideHeight / MmToM;
        double rhMaxF = frontPhys.MaxRideHeight / MmToM;
        double rhMinR = rearPhys.MinRideHeight / MmToM;
        double rhMaxR = rearPhys.MaxRideHeight / MmToM;

        double rhFracF = (r.RideHeightFront - rhMinF) / Math.Max(1.0, rhMaxF - rhMinF);
        double rhFracR = (r.RideHeightRear  - rhMinR) / Math.Max(1.0, rhMaxR - rhMinR);

        double sprMinF = frontPhys.MinSpringRate;
        double sprMaxF = frontPhys.MaxSpringRate;
        double sprMinR = rearPhys.MinSpringRate;
        double sprMaxR = rearPhys.MaxSpringRate;

        double sprFracF = (r.SpringFront - sprMinF) / Math.Max(1.0, sprMaxF - sprMinF);
        double sprFracR = (r.SpringRear  - sprMinR) / Math.Max(1.0, sprMaxR - sprMinR);

        if (!offRoad && rhFracF < 0.10 && sprFracF < 0.15)
        {
            r.SpringFront = Math.Round(sprMinF + (sprMaxF - sprMinF) * 0.45, 1);
            changed = true;
        }
        if (!offRoad && rhFracR < 0.10 && sprFracR < 0.15)
        {
            r.SpringRear = Math.Round(sprMinR + (sprMaxR - sprMinR) * 0.45, 1);
            changed = true;
        }
        if (rhFracF > 0.90 && sprFracF > 0.85)
        {
            r.SpringFront = Math.Round(sprMinF + (sprMaxF - sprMinF) * 0.65, 1);
            changed = true;
        }
        if (rhFracR > 0.90 && sprFracR > 0.85)
        {
            r.SpringRear = Math.Round(sprMinR + (sprMaxR - sprMinR) * 0.65, 1);
            changed = true;
        }
        return changed;
    }

    private static double SpringFromHz(double hz, double cornerMassKg)
    {
        // k [N/m] = (2*pi*f)^2 * m, converted to N/mm (the DB / tune unit) by /1000.
        double kNpm = Math.Pow(2.0 * Math.PI * hz, 2.0) * cornerMassKg;
        return Math.Round(kNpm / PhysicsConstants.NmmToNm, 2);
    }

    private static double DampingFromSpringAndMass(double springNm, double cornerMassKg, DbSpringDamperPhysics phys, double dampingRatio, bool rebound)
    {
        // critical damping cc = 2 * sqrt(k * m)   [N/(m/s)]
        double cc = 2.0 * Math.Sqrt(springNm * cornerMassKg);
        double desiredForce = dampingRatio * cc;

        // Derive a game-unit scale: at the default spring and the same corner mass the
        // default damper represents a reference damping ratio (0.65 rebound, 0.40 bump).
        double defaultSpringNm = phys.DefSpringRate * PhysicsConstants.NmmToNm;
        double defaultCc = defaultSpringNm > 0 ? 2.0 * Math.Sqrt(defaultSpringNm * cornerMassKg) : 1.0;
        double referenceRatio = rebound ? 0.65 : 0.40;
        double referenceValue = rebound ? phys.DefDampenReboundRate : phys.DefDampenBumpRate;
        double unitsPerNs = referenceValue / (referenceRatio * defaultCc);

        return desiredForce * unitsPerNs;
    }

    private static double InterpolateHeight(DbSpringDamperPhysics phys, double fraction)
    {
        double minM = phys.MinRideHeight;
        double maxM = phys.MaxRideHeight;
        return (minM + (maxM - minM) * fraction) / MmToM;
    }

    private static (double f, double r, string noteKey) RideHeightFractions(Discipline d, DriveType dt) => d switch
    {
        Discipline.Drag when dt == DriveType.RWD => (0.10, 0.55, "Expl_RideHeightNote_Drag"),
        Discipline.Drag                          => (0.12, 0.45, "Expl_RideHeightNote_Drag"),
        Discipline.Drift                         => (0.18, 0.22, "Expl_RideHeightNote_Drift"),
        Discipline.Rally                         => (0.50, 0.55, "Expl_RideHeightNote_Rally"),
        Discipline.CrossCountry                  => (0.65, 0.70, "Expl_RideHeightNote_CrossCountry"),
        Discipline.Touge                         => (0.18, 0.22, "Expl_RideHeightNote_Touge"),
        Discipline.Street                        => (0.15, 0.20, "Expl_RideHeightNote_Street"),
        _                                        => (0.18, 0.22, "Expl_RideHeightNote_Road")
    };

    private static (double f, double r) TargetNaturalFrequency(Discipline d, DriveType dt) => d switch
    {
        Discipline.Drag when dt == DriveType.RWD => (2.8, 1.5),
        Discipline.Drag                          => (2.8, 1.6),
        Discipline.Drift                         => (2.4, 2.0),
        Discipline.Rally                         => (2.3, 2.1),
        Discipline.CrossCountry                  => (2.1, 1.9),
        Discipline.Touge                         => (2.6, 2.7),
        Discipline.Street                        => (2.5, 2.6),
        _                                        => (2.5, 2.6)
    };
}
