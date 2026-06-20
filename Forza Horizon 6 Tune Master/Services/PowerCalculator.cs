using System;
using System.Linq;
using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

public static class PowerCalculator
{
    private const double RadSToRPM = 60.0 / (2.0 * Math.PI);

    public static void Calculate(CarCard car, SelectedParts? parts = null)
    {
        var db = Fh6DatabaseService.Instance;
        var dbCar = db.GetCar(car.CarDbId);
        if (dbCar == null) return;

        parts ??= new SelectedParts();
        // Electric cars are identified by aspiration type 8 (matches CarSpecController).
        // PowertrainID is an engine-layout category, NOT an electric flag — most ICE cars
        // have PowertrainID == 1, so keying off it sent V8s down the (motor-less) electric
        // path, which bailed out and left power/torque at their CarCard defaults (300 hp).
        if (dbCar.AspirationTypeId == 8)
            CalcElectric(car, dbCar, db, parts);
        else
            CalcIce(car, dbCar, db, parts);

        // Apply rotational inertia factor
        double inertiaFactor = TuningPhysicsContext.ComputeRotationalInertiaFactor(car, parts, db);
        car.RotationalInertiaFactor = inertiaFactor;
        if (car.CachedTorqueCurveNm is { Length: > 0 } && Math.Abs(inertiaFactor - 1.0) > 0.001)
        {
            for (int i = 0; i < car.CachedTorqueCurveNm.Length; i++)
                car.CachedTorqueCurveNm[i] = Math.Round(car.CachedTorqueCurveNm[i] * inertiaFactor, 1);
        }

        car.CachedPowerCurveHP = ComputePowerCurveFromTorque(car.CachedTorqueCurveNm, car.MaxRPM);

        // Power is derived purely from the (already forced-induction-scaled) torque curve.
        // Forced induction must NOT apply a second, separate power multiplier — doing so
        // double-counts the boost and inflates power roughly 5× (turbo torque scale + a
        // bogus power scale). See CalcIce / ApplyForcedInduction.
        if (car.CachedPowerCurveHP is { Length: > 0 })
            car.PowerHP = Math.Round(car.CachedPowerCurveHP.Max(), 1);
    }

    private static void CalcElectric(CarCard car, DbCar dbCar, Fh6DatabaseService db, SelectedParts? parts = null)
    {
        int motorId = ResolveEffectiveMotorId(car, dbCar, db, parts);
        var motor = db.GetMotor(motorId);
        if (motor == null) return;

        double maxRpm = motor.RedlineRPM;
        double peakTorqueNm = motor.MotorGraphingMaxTorque;
        double peakPowerW = motor.MotorGraphingMaxPower;

        double torqueScale = 1.0;
        if (parts?.MotorPartId != null)
        {
            var part = db.GetMotorPartById(parts.MotorPartId.Value);
            if (part is { IsStock: false, TorqueScale: not null } && Math.Abs(part.TorqueScale.Value - 1.0) > 0.005)
                torqueScale = part.TorqueScale.Value;
        }
        peakTorqueNm *= torqueScale;
        peakPowerW *= torqueScale;

        car.MaxRPM = (int)Math.Round(maxRpm);
        car.TorqueNm = Math.Round(peakTorqueNm);
        car.PowerHP = Math.Round(peakPowerW / 745.7, 1);

        car.CachedTorqueCurveNm = LoadTorqueCurve(motor.TorqueCurveFullThrottleID, db, torqueScale, maxRpm, maxRpm)
            ?? GenerateElectricTorqueCurve(peakTorqueNm, (int)Math.Round(maxRpm));
    }

    private static void CalcIce(CarCard car, DbCar dbCar, Fh6DatabaseService db, SelectedParts parts)
    {
        // Resolve the effective engine (stock or swap) so we use its true torque/power baseline.
        int effectiveEngineId = ResolveEffectiveEngineId(car, dbCar, db, parts);
        var effectiveEngine = db.GetEngine(effectiveEngineId);

        double redlineRPM = dbCar.SimRedlineAngVel * RadSToRPM;
        double peakTorqueNm = effectiveEngine?.EngineGraphingMaxTorque ?? dbCar.SimPeakTorque;
        double peakPowerW = effectiveEngine?.EngineGraphingMaxPower ?? dbCar.SimPeakPower;
        // GameTorqueScale is a stock-engine correction for the original car.
        // Engine swaps bring their own torque curves with their natural scale — no correction needed.
        double torqueScale = parts.EngineSwapPartId != null ? 1.0 : dbCar.GameTorqueScale;

        double partRedlineRPM = redlineRPM;
        int? torqueCurveId = null;
        double torqueCurveMaxRPM = redlineRPM;

        if (parts.CamshaftPartId != null)
        {
            var cam = db.GetCamshaftById(parts.CamshaftPartId.Value);
            if (cam != null)
            {
                partRedlineRPM = cam.RedlineRPM > 0 ? cam.RedlineRPM : redlineRPM;
                torqueCurveId = cam.TorqueCurveFullThrottleID > 0 ? cam.TorqueCurveFullThrottleID : null;
                torqueCurveMaxRPM = cam.TorqueCurveMaxRPM > 0 ? cam.TorqueCurveMaxRPM : partRedlineRPM;
                torqueScale *= cam.TorqueScale ?? 1.0;
            }
        }

        car.MaxRPM = (int)Math.Round(partRedlineRPM);

        double partScale = AccumulatePartTorqueScales(parts, db);

        // Base (naturally-aspirated) torque scale: engine baseline × bolt-on engine parts.
        // Forced induction is applied separately, AS AN RPM-DEPENDENT CURVE (see below), so the
        // boost characteristic (turbo lag low down, supercharger building with RPM, …) actually
        // reshapes the graph instead of just lifting the whole curve by a flat factor.
        double baseScale = torqueScale * Math.Max(0.1, partScale);
        baseScale = Math.Min(baseScale, 20.0);

        // An intercooler raises the FI's peak boost; folded into the FI curve's top end.
        double intercoolerMaxScale = 1.0;
        if (parts.ForcedInductionPartId != null && parts.IntercoolerPartId != null)
        {
            var ic = db.GetIntercoolerById(parts.IntercoolerPartId.Value);
            if (ic != null && ic.MaxScaleScale > 0.001)
                intercoolerMaxScale = ic.MaxScaleScale;
        }

        double[] torqueCurve;
        if (torqueCurveId != null)
        {
            torqueCurve = LoadTorqueCurve(torqueCurveId.Value, db, baseScale, torqueCurveMaxRPM, partRedlineRPM)
                ?? GenerateIceTorqueCurve(dbCar, peakTorqueNm * baseScale, partRedlineRPM);
        }
        else
        {
            torqueCurve = GenerateIceTorqueCurve(dbCar, peakTorqueNm * baseScale, partRedlineRPM);
        }

        ApplyForcedInductionCurve(torqueCurve, car.MaxRPM, parts, db, intercoolerMaxScale);

        car.CachedTorqueCurveNm = torqueCurve;
        car.TorqueNm = Math.Round(torqueCurve.Max());
    }

    private static int ResolveEffectiveEngineId(CarCard car, DbCar dbCar, Fh6DatabaseService db, SelectedParts? parts = null)
    {
        int? swapId = parts?.EngineSwapPartId;
        if (swapId != null)
        {
            var swap = db.GetEngineSwapById(swapId.Value);
            if (swap != null) return swap.EngineID;
        }
        if (car.EngineDbId > 0) return car.EngineDbId;

        var swaps = db.GetEngineSwaps(dbCar.Id);
        return swaps?.FirstOrDefault(e => e.IsStock)?.EngineID ?? 0;
    }

    private static int ResolveEffectiveMotorId(CarCard car, DbCar dbCar, Fh6DatabaseService db, SelectedParts? parts = null)
    {
        if (parts?.MotorSwapPartId != null)
        {
            var swap = db.GetMotorSwapById(parts.MotorSwapPartId.Value);
            if (swap != null) return swap.MotorID;
        }
        if (car.MotorDbId > 0) return car.MotorDbId;

        var swaps = db.GetMotorSwaps(dbCar.Id);
        return swaps?.FirstOrDefault(s => s.IsStock)?.MotorID ?? 0;
    }

    private static double AccumulatePartTorqueScales(SelectedParts parts, Fh6DatabaseService db)
    {
        var scales = new System.Collections.Generic.List<double>();
        AddTorqueScaleIfNotStock(db, parts.DisplacementPartId, id => db.GetDisplacementById(id), scales);
        AddTorqueScaleIfNotStock(db, parts.ValvesPartId, id => db.GetValvesById(id), scales);
        AddTorqueScaleIfNotStock(db, parts.PistonsPartId, id => db.GetPistonsById(id), scales);
        AddTorqueScaleIfNotStock(db, parts.FuelSystemPartId, id => db.GetFuelSystemById(id), scales);
        AddTorqueScaleIfNotStock(db, parts.IgnitionPartId, id => db.GetIgnitionById(id), scales);
        AddTorqueScaleIfNotStock(db, parts.ExhaustPartId, id => db.GetExhaustById(id), scales);
        AddTorqueScaleIfNotStock(db, parts.IntakePartId, id => db.GetIntakeById(id), scales);
        AddTorqueScaleIfNotStock(db, parts.ManifoldPartId, id => db.GetManifoldById(id), scales);
        AddTorqueScaleIfNotStock(db, parts.OilCoolingPartId, id => db.GetOilCoolingById(id), scales);
        AddTorqueScaleIfNotStock(db, parts.RestrictorPartId, id => db.GetRestrictorById(id), scales);

        if (scales.Count == 0) return 1.0;

        double product = scales.Aggregate(1.0, (p, s) => p * s);
        return product;
    }

    private static void AddTorqueScaleIfNotStock<T>(Fh6DatabaseService db, int? partId, Func<int, T?> getter, System.Collections.Generic.List<double> scales)
        where T : DbUpgradePart
    {
        if (partId == null) return;
        var part = getter(partId.Value);
        if (part == null) return;
        if (part.IsStock) return;
        double s = part.TorqueScale ?? 1.0;
        if (Math.Abs(s - 1.0) < 0.005) return;
        scales.Add(s);
    }

    // Forced induction reshapes the torque curve across RPM rather than lifting it by a flat
    // factor — that is what makes each FI type look different on the graph. Power is computed
    // downstream from the resulting torque curve, so FI must NOT be scaled into power again.
    // Note: turbo PowerMaxScale is NOT a multiplier (DB values range ~15–1000); it is the
    // turbo's rated power and must never be applied as a scale factor.
    private static void ApplyForcedInductionCurve(double[] curve, int maxRPM, SelectedParts parts, Fh6DatabaseService db, double intercoolerMaxScale)
    {
        if (parts.ForcedInductionPartId == null || curve.Length == 0 || maxRPM <= 0) return;

        var fi = db.GetForcedInductionById(parts.ForcedInductionPartId.Value);
        if (fi is not DbUpgradeForcedInduction fiPart) return;

        int n = curve.Length;
        for (int i = 0; i < n; i++)
        {
            double rpm = maxRPM * i / (double)(n - 1);
            double m = FiScaleAtRpm(fiPart, rpm, maxRPM, intercoolerMaxScale);
            curve[i] = Math.Round(curve[i] * m, 1);
        }
    }

    private static double FiScaleAtRpm(DbUpgradeForcedInduction fi, double rpm, double maxRPM, double intercoolerMaxScale)
    {
        double frac = CalculationHelpers.Clamp(rpm / maxRPM, 0.0, 1.0);
        return fi switch
        {
            DbUpgradeTurboSingle ts => TurboScaleAtRpm(ts.MinScale, ts.MaxScale * intercoolerMaxScale, ts, rpm, frac),
            DbUpgradeTurboTwin   tt => TurboScaleAtRpm(tt.MinScale, tt.MaxScale * intercoolerMaxScale, tt, rpm, frac),
            // Centrifugal blowers make boost roughly with the square of impeller (≈ engine) speed,
            // so almost nothing low down and a steep climb up top.
            DbUpgradeCSC csc => SuperchargerScaleAtRpm(csc.ZeroRPMScale, csc.RedlineRPMScale * intercoolerMaxScale, frac, centrifugal: true),
            // Positive-displacement (Roots) blowers give near-constant boost from just off idle.
            DbUpgradeDSC dsc => SuperchargerScaleAtRpm(dsc.ZeroRPMScale, dsc.RedlineRPMScale * intercoolerMaxScale, frac, centrifugal: false),
            _ => 1.0
        };
    }

    // Turbo: little/negative help while it spools (MinScale), rising to full boost (MaxScale)
    // once on song, then tapering at very high RPM per the part's TorqueDropOff points. The
    // spool fraction (~where full boost arrives) is the one modelled assumption — MinScale,
    // MaxScale and the drop-off come straight from the DB.
    private const double TurboSpoolFraction = 0.35;        // full boost by ~35% of redline
    private const double TurboSpoolFractionAntiLag = 0.15; // anti-lag keeps it spooled → boost comes in much earlier
    private static double TurboScaleAtRpm(double minScale, double maxScale, DbUpgradeForcedInduction fi, double rpm, double frac)
    {
        if (minScale <= 0.0) minScale = maxScale;
        // Anti-lag (OffThrottleMomentInertia > 0) effectively eliminates turbo lag, so full boost
        // arrives at lower RPM — i.e. the spool ramp shifts left on the graph.
        double spool = fi.HasAntiLag ? TurboSpoolFractionAntiLag : TurboSpoolFraction;
        double s;
        if (frac <= spool && spool > 0.0)
        {
            double t = frac / spool;                  // 0..1 across the spool region
            t = t * t * (3.0 - 2.0 * t);              // smoothstep: lag, then a quick build
            s = minScale + (maxScale - minScale) * t;
        }
        else s = maxScale;
        return s * FiDropOff(fi, rpm);
    }

    private static double SuperchargerScaleAtRpm(double zeroScale, double redlineScale, double frac, bool centrifugal)
    {
        double z = zeroScale <= 0.0 ? 1.0 : zeroScale;
        double r = redlineScale <= 0.0 ? z : redlineScale;
        double t = centrifugal ? frac * frac : frac;
        return z + (r - z) * t;
    }

    // High-RPM boost taper from the part's TorqueDropOffRPM/Scale points. For most street
    // engines the drop-off RPMs sit above redline, so this is a no-op there.
    private static double FiDropOff(DbUpgradeForcedInduction fi, double rpm)
    {
        double rpm0 = fi.TorqueDropOffRPM0, rpm1 = fi.TorqueDropOffRPM1;
        double s0 = fi.TorqueDropOffScale0 <= 0.0 ? 1.0 : fi.TorqueDropOffScale0;
        double s1 = fi.TorqueDropOffScale1 <= 0.0 ? s0 : fi.TorqueDropOffScale1;
        if (rpm1 <= rpm0 || rpm <= rpm0) return s0;
        double t = CalculationHelpers.Clamp((rpm - rpm0) / (rpm1 - rpm0), 0.0, 1.0);
        return s0 + (s1 - s0) * t;
    }

    // DB torque curves are sampled uniformly over [0, curveMaxRPM], where the trailing
    // entries sit past the redline and the final value is a negative fuel-cut sentinel.
    // Consumers (power calc + chart) assume the array spans 0..MaxRPM, so we resample onto
    // a uniform [0, targetMaxRPM] grid — dropping the over-rev tail and the sentinel that
    // would otherwise plot as a spike crashing to the floor at the right edge of the graph.
    private static double[]? LoadTorqueCurve(int curveId, Fh6DatabaseService db, double scale, double curveMaxRPM, double targetMaxRPM)
    {
        var tc = db.GetTorqueCurve(curveId);
        if (tc?.V == null || tc.V.Length == 0) return null;

        double[] raw = tc.V.Select(v => v * tc.TorqueScale * scale).ToArray();
        int rawN = raw.Length;
        // Replace any negative fuel-cut sentinels with the previous valid value.
        for (int i = 0; i < rawN; i++)
            if (raw[i] < 0) raw[i] = i > 0 ? raw[i - 1] : 0.0;

        if (curveMaxRPM <= 0) curveMaxRPM = targetMaxRPM;
        if (targetMaxRPM <= 0 || rawN < 2) return raw.Select(v => Math.Round(v, 1)).ToArray();

        const int outN = 24;
        double[] outc = new double[outN];
        for (int i = 0; i < outN; i++)
        {
            double rpm = targetMaxRPM * i / (outN - 1);
            double pos = CalculationHelpers.Clamp(rpm / curveMaxRPM, 0.0, 1.0) * (rawN - 1);
            int lo = (int)Math.Floor(pos);
            int hi = Math.Min(lo + 1, rawN - 1);
            double val = raw[lo] + (raw[hi] - raw[lo]) * (pos - lo);
            outc[i] = Math.Round(Math.Max(val, 0.0), 1);
        }
        return outc;
    }

    private static double[] GenerateIceTorqueCurve(DbCar dbCar, double peakTorque, double redlineRPM)
    {
        int points = 20;
        double[] curve = new double[points];
        double torquePeakRPM = dbCar.SimPeakTorqueAngVel * RadSToRPM;
        double powerPeakRPM = dbCar.SimPeakAngVel * RadSToRPM;

        for (int i = 0; i < points; i++)
        {
            double rpm = redlineRPM * i / (points - 1);
            if (rpm <= torquePeakRPM)
                curve[i] = peakTorque * (0.60 + 0.40 * rpm / Math.Max(torquePeakRPM, 1));
            else if (rpm <= powerPeakRPM)
                curve[i] = peakTorque * (1.0 - 0.08 * (rpm - torquePeakRPM) / Math.Max(powerPeakRPM - torquePeakRPM, 1));
            else
                curve[i] = peakTorque * 0.92 * (1.0 - (rpm - powerPeakRPM) / Math.Max(redlineRPM - powerPeakRPM, 1) * 0.35);
            curve[i] = Math.Round(Math.Max(curve[i], peakTorque * 0.20), 1);
        }
        return curve;
    }

    private static double[] GenerateElectricTorqueCurve(double peakTorque, int maxRPM)
    {
        int points = 20;
        double[] curve = new double[points];
        for (int i = 0; i < points; i++)
        {
            double pct = (double)i / (points - 1);
            curve[i] = pct < 0.9
                ? peakTorque
                : peakTorque * (1.0 - (pct - 0.9) / 0.1 * 0.5);
            curve[i] = Math.Round(curve[i], 1);
        }
        return curve;
    }

    private static double[]? ComputePowerCurveFromTorque(double[]? torqueCurve, int maxRPM)
    {
        if (torqueCurve == null || torqueCurve.Length == 0) return null;
        int points = torqueCurve.Length;
        double[] power = new double[points];
        for (int i = 0; i < points; i++)
        {
            double rpm = maxRPM * i / (double)(points - 1);
            power[i] = Math.Round(torqueCurve[i] * rpm / 7121.0, 1);
        }
        return power;
    }
}
