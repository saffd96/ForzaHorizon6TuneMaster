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

        // Compute everything into locals first, write to car at the end.
        if (dbCar.AspirationTypeId == 8)
        {
            var (maxRPM, peakTorqueNm, targetPower, targetTorque, torqueCurve, estimated) = CalcElectric(car, dbCar, db, parts);
            ApplyResults(car, parts, db, maxRPM, peakTorqueNm, targetPower, targetTorque, torqueCurve, estimated);
        }
        else
        {
            var (maxRPM, peakTorqueNm, targetPower, targetTorque, torqueCurve, estimated) = CalcIce(car, dbCar, db, parts);
            ApplyResults(car, parts, db, maxRPM, peakTorqueNm, targetPower, targetTorque, torqueCurve, estimated);
        }
    }

    private static void ApplyResults(CarCard car, SelectedParts parts, Fh6DatabaseService db,
        int maxRPM, double peakTorqueNm, double targetPowerHP, double targetTorqueNm, double[]? torqueCurve,
        bool estimated)
    {
        double inertiaFactor = TuningPhysicsContext.ComputeRotationalInertiaFactor(car, parts, db);
        if (torqueCurve is { Length: > 0 } && Math.Abs(inertiaFactor - 1.0) > 0.001)
        {
            torqueCurve = torqueCurve.Select(t => Math.Round(t * inertiaFactor, 1)).ToArray();
            peakTorqueNm *= inertiaFactor;
        }

        var powerCurve = ComputePowerCurveFromTorque(torqueCurve, maxRPM);
        double curvePowerPeak = powerCurve is { Length: > 0 } ? powerCurve.Max() : 0;
        double curveTorquePeak = torqueCurve is { Length: > 0 } ? torqueCurve.Max() : peakTorqueNm;

        // Anchor the absolute PEAK power to the engine's dyno data instead of the raw curve
        // (whose stacked multipliers over/undershoot). targetPowerHP is the dyno-calibrated
        // value for this exact build: stock power at no upgrades, the engine ceiling
        // (EngineGraphingMaxPower) at a maxed build, interpolated in between. The power
        // curve is rescaled so its peak lands there while keeping its shape.
        double finalPower;
        if (targetPowerHP > 0.1 && curvePowerPeak > 0.1)
        {
            double kp = targetPowerHP / curvePowerPeak;
            powerCurve = powerCurve!.Select(p => Math.Round(p * kp, 1)).ToArray();
            finalPower = Math.Round(targetPowerHP, 1);
        }
        else
        {
            // No dyno anchor (electric, or no engine row) → trust the curve as-is.
            finalPower = curvePowerPeak > 0.1 ? Math.Round(curvePowerPeak, 1) : 0;
        }

        // Peak torque is anchored the SAME way but to its OWN dyno pair (stock SimPeakTorque
        // -> EngineGraphingMaxTorque ceiling) — peak torque and peak power occur at different
        // RPM, so one can't be derived from the other; deriving torque from the power-scaled
        // curve left it ~10% off the game. The torque and power curves are plotted on
        // independent axes (each rescales to its own max), so scaling them by different
        // factors is visually consistent.
        double finalTorque;
        if (targetTorqueNm > 0.1 && curveTorquePeak > 0.1)
        {
            double kt = targetTorqueNm / curveTorquePeak;
            if (torqueCurve is { Length: > 0 })
                torqueCurve = torqueCurve.Select(t => Math.Round(t * kt, 1)).ToArray();
            finalTorque = Math.Round(targetTorqueNm);
        }
        else
        {
            finalTorque = curveTorquePeak > 0.1 ? Math.Round(curveTorquePeak) : 0;
        }

        // All writes together — no partial mutation
        car.MaxRPM = maxRPM;
        car.TorqueNm = finalTorque;
        car.PowerHP = finalPower;
        car.PowerIsEstimated = estimated;
        car.RotationalInertiaFactor = inertiaFactor;
        car.CachedTorqueCurveNm = torqueCurve;
        car.CachedPowerCurveHP = powerCurve;
    }

    private static (int MaxRPM, double PeakTorqueNm, double TargetPowerHP, double TargetTorqueNm, double[]? TorqueCurve, bool Estimated) CalcElectric(
        CarCard car, DbCar dbCar, Fh6DatabaseService db, SelectedParts? parts = null)
    {
        int motorId = ResolveEffectiveMotorId(car, dbCar, db, parts);
        var motor = db.GetMotor(motorId);
        if (motor == null) return (0, 0, 0, 0, null, false);

        double maxRpm = motor.RedlineRPM;
        double peakTorqueNm = motor.MotorGraphingMaxTorque;

        double torqueScale = 1.0;
        if (parts?.MotorPartId != null)
        {
            var part = db.GetMotorPartById(parts.MotorPartId.Value);
            if (part is { IsStock: false, TorqueScale: not null } && Math.Abs(part.TorqueScale.Value - 1.0) > 0.005)
                torqueScale = part.TorqueScale.Value;
        }
        peakTorqueNm *= torqueScale;

        var torqueCurve = LoadTorqueCurve(motor.TorqueCurveFullThrottleID, db, torqueScale, maxRpm, maxRpm)
            ?? GenerateElectricTorqueCurve(peakTorqueNm, (int)Math.Round(maxRpm));

        // No reliable HP anchor for motors (MotorGraphingMaxPower units are unverified),
        // so return 0 for both anchors and let ApplyResults derive power/torque from the
        // curve (whose peak is MotorGraphingMaxTorque, already a usable torque figure).
        // Not flagged as "estimated" — the motor torque curve is genuine game data.
        return ((int)Math.Round(maxRpm), peakTorqueNm, 0, 0, torqueCurve, false);
    }

    private static (int MaxRPM, double PeakTorqueNm, double TargetPowerHP, double TargetTorqueNm, double[]? TorqueCurve, bool Estimated) CalcIce(
        CarCard car, DbCar dbCar, Fh6DatabaseService db, SelectedParts parts)
    {
        int effectiveEngineId = ResolveEffectiveEngineId(car, dbCar, db, parts);
        var effectiveEngine = db.GetEngine(effectiveEngineId);

        // A *stock* engine swap is not a real swap — the engine is still the car's native
        // engine, so the native dyno figures (GameTorqueScale, SimPeakPower) still apply.
        // The UI always sets EngineSwapPartId (it defaults to the stock swap), so keying
        // off "EngineSwapPartId != null" alone would wrongly treat every stock car as
        // swapped and derive stock power from the ceiling instead of SimPeakPower.
        var swapPart = parts.EngineSwapPartId != null ? db.GetEngineSwapById(parts.EngineSwapPartId.Value) : null;
        bool isEngineSwapped = swapPart is { IsStock: false };

        double redlineRPM = dbCar.SimRedlineAngVel * RadSToRPM;
        double peakTorqueNm = effectiveEngine?.EngineGraphingMaxTorque ?? dbCar.SimPeakTorque;
        double torqueScale = isEngineSwapped ? 1.0 : dbCar.GameTorqueScale;

        double partScale = AccumulatePartTorqueScales(parts, db);
        double baseScale = Math.Min(torqueScale * Math.Max(0.1, partScale), 20.0);

        // Camshaft sets the rev range and torque-curve shape (it has no TorqueScale), so
        // the selected cam — not a scalar — defines the base curve.
        var selectedCam = parts.CamshaftPartId != null ? db.GetCamshaftById(parts.CamshaftPartId.Value) : null;
        double[] torqueCurve = BuildCamTorqueCurve(dbCar, db, baseScale, redlineRPM, peakTorqueNm, selectedCam, out int maxRPM);

        double intercoolerMaxScale = 1.0;
        if (parts.ForcedInductionPartId != null && parts.IntercoolerPartId != null)
        {
            var ic = db.GetIntercoolerById(parts.IntercoolerPartId.Value);
            if (ic != null && ic.MaxScaleScale > 0.001)
                intercoolerMaxScale = ic.MaxScaleScale;
        }

        double[] fiCurve = ApplyForcedInductionCurve(torqueCurve, maxRPM, parts, db, intercoolerMaxScale);

        // Power ceiling = engine dyno max. EngineGraphingMaxPower is kW (verified:
        // engine 2794 = 1011 kW -> 1356 HP, matching the in-game full build). Only
        // valid when an engine row resolves; SimPeakPower is *stock* power, not a
        // ceiling, so we don't cap with it -- fall back to the curve instead.
        double powerCapHP = effectiveEngine != null
            ? effectiveEngine.EngineGraphingMaxPower * 1.341
            : 0;
        if (powerCapHP <= 0.1)
            return (maxRPM, fiCurve.Max(), 0, 0, fiCurve, true); // no engine row -> curve, flagged estimate

        // Each part scales torque by its TorqueScale; the build's combined multiplier is
        // their product. Forced induction is measured EMPIRICALLY from the curve (peak
        // power post-FI / pre-FI) for both the current and the best-available turbo, so
        // the boost's real peak gain — after spool and torque drop-off — is used instead
        // of its flat torque MaxScale (which overstates power, e.g. 1.4 torque -> ~1.45
        // power not 1.53). The MAX build uses the best part in every category.
        //
        // The game does NOT turn the torque-multiplier product straight into power — it
        // saturates hard toward the engine's dyno ceiling (native NSX V6 multiplies to
        // x2.8 yet only reaches x1.70 power). So we map the multiplier into power in LOG
        // space between the two dyno anchors:
        //   progress = ln(currentMult) / ln(maxMult)   (0 at stock, 1 at a maxed build)
        // which keeps each part's contribution PROPORTIONAL to its multiplier (a turbo
        // adds far more than ignition — not a flat per-part step) while guaranteeing the
        // max build lands exactly on the ceiling and there is no dead zone.
        double basePeak = PowerPeak(torqueCurve, maxRPM); // pre-FI, current part-scaled curve
        double currentFiPeak = (parts.ForcedInductionPartId != null && basePeak > 0.0)
            ? PowerPeak(fiCurve, maxRPM) / basePeak
            : 1.0;
        var (bestFi, bestIc) = BestForcedInduction(effectiveEngineId, db);
        double maxFiPeak = (bestFi != null && basePeak > 0.0)
            ? PowerPeak(ApplyFiCore(torqueCurve, maxRPM, bestFi, bestIc), maxRPM) / basePeak
            : 1.0;

        // Some engines ship with FACTORY forced induction (a stock turbo/SC that can't be
        // removed, only upgraded). That boost is already part of the stock dyno figure the
        // power is anchored to, so FI build-progress must be measured RELATIVE to the stock
        // FI (current/stock, best/stock) — otherwise the factory boost is double-counted as
        // an upgrade and inflates stock power (e.g. Civic Type R '23 392 HP instead of 315).
        // NA engines have no stock FI -> stockFiPeak = 1.0 -> behaviour unchanged.
        var stockFi = StockForcedInduction(effectiveEngineId, db);
        double stockFiPeak = (stockFi != null && basePeak > 0.0)
            ? PowerPeak(ApplyFiCore(torqueCurve, maxRPM, stockFi, 1.0), maxRPM) / basePeak
            : 1.0;
        if (stockFiPeak > 1.0001)
        {
            if (parts.ForcedInductionPartId != null) currentFiPeak /= stockFiPeak;
            maxFiPeak = Math.Max(maxFiPeak / stockFiPeak, 1.0);
        }

        // Camshaft has no TorqueScale — its power gain comes from rev range / curve shape,
        // so it's measured empirically too: peak power with this cam vs the stock cam, and
        // with the best available cam vs stock.
        var (camFactor, maxCamFactor) = ComputeCamFactors(
            dbCar, db, effectiveEngineId, baseScale, redlineRPM, peakTorqueNm, selectedCam);

        // Best build uses forced induction when available, which excludes the manifold.
        double currentBuildScale = partScale * camFactor * currentFiPeak;
        double maxBuildScale = ComputeMaxPartScale(effectiveEngineId, db, includeManifold: bestFi == null)
                               * maxCamFactor * maxFiPeak;
        double progress = (maxBuildScale > 1.0001 && currentBuildScale > 1.0)
            ? Math.Clamp(Math.Log(currentBuildScale) / Math.Log(maxBuildScale), 0.0, 1.0)
            : 0.0;

        // Low anchor = stock power. For the car's native engine that's the verified
        // SimPeakPower (0.1 kW units -> *0.1341, e.g. NSX 4473.8 -> 600 HP). A swapped
        // engine has its own stock figure on the car it is native to: the engine's
        // MediaName identifies that donor car, so we anchor to its SimPeakPower (e.g.
        // 2.0L I4-VVT swap -> donor HON_CivicR_04 -> 212 HP).
        bool nativeEngine = !isEngineSwapped;
        var donor = nativeEngine ? null
            : (effectiveEngine != null ? db.GetCarByMediaName(effectiveEngine.MediaName) : null);

        // ~30 race/rally swap engines have no donor car in the game DB at all. For those we
        // estimate stock from the engine's REAL stock-configuration dyno curve (stock cam's
        // torque curve + factory FI), whose magnitude is genuine game data — far better than
        // ceiling/maxMult, which the game's power saturation makes much too low (Turbo Rally:
        // ~300 HP real vs ~200 from ceiling/maxMult). Only used when no anchor figure exists.
        double curveStockHp = 0, curveStockTq = 0;
        bool noAnchorFigure = !(nativeEngine && dbCar.SimPeakPower > 0) && donor is not { SimPeakPower: > 0 };
        if (noAnchorFigure)
            (curveStockHp, curveStockTq) = EstimateStockFromCurve(
                dbCar, db, effectiveEngineId, torqueScale, redlineRPM, peakTorqueNm, stockFi);

        double torqueCapNm = effectiveEngine?.EngineGraphingMaxTorque ?? 0;

        double stockHP;
        if (nativeEngine && dbCar.SimPeakPower > 0)
            stockHP = dbCar.SimPeakPower * 0.1341;
        else if (donor is { SimPeakPower: > 0 })
            stockHP = donor.SimPeakPower * 0.1341;
        else
            stockHP = curveStockHp > 0.1 ? curveStockHp : powerCapHP / Math.Max(maxBuildScale, 1.0);
        stockHP = Math.Min(stockHP, powerCapHP);

        double targetPowerHP = stockHP > 0.1
            ? stockHP * Math.Pow(powerCapHP / stockHP, progress)
            : powerCapHP * progress;

        // Peak TORQUE is anchored exactly like power but to its own dyno pair: stock torque
        // (native SimPeakTorque, or the donor car's for a swap; *100 -> Nm, verified e.g.
        // Civic'97 1.59922 -> 160, Civic'04 2.02 -> 202) interpolated to the engine's
        // EngineGraphingMaxTorque ceiling by the same build progress. 0 when no figure is
        // available -> ApplyResults falls back to the curve peak.
        double stockTorqueNm;
        if (nativeEngine && dbCar.SimPeakTorque > 0)
            stockTorqueNm = dbCar.SimPeakTorque * 100.0;
        else if (donor is { SimPeakTorque: > 0 })
            stockTorqueNm = donor.SimPeakTorque * 100.0;
        else
            stockTorqueNm = curveStockTq > 0.1 ? curveStockTq
                : (torqueCapNm > 0.1 ? torqueCapNm / Math.Max(maxBuildScale, 1.0) : 0);
        if (torqueCapNm > 0.1) stockTorqueNm = Math.Min(stockTorqueNm, torqueCapNm);

        double targetTorqueNm = (stockTorqueNm > 0.1 && torqueCapNm > 0.1)
            ? stockTorqueNm * Math.Pow(torqueCapNm / stockTorqueNm, progress)
            : 0;

        // "Estimated" = no exact stock figure existed (donorless swap engine) — power/torque
        // are derived from the stock curve, not anchored to verified dyno data.
        return (maxRPM, fiCurve.Max(), Math.Round(targetPowerHP, 1), Math.Round(targetTorqueNm, 1), fiCurve, noAnchorFigure);
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
        // Manifold (headers) is mutually exclusive with forced induction — the turbo/SC
        // manifold replaces it — so it doesn't contribute when boost is installed.
        if (parts.ForcedInductionPartId == null)
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

    // Largest combined TORQUE-PART multiplier (no forced induction) an engine can reach
    // with the best part in every category. FI is handled separately and empirically
    // (MaxFiPowerPeak) because a turbo's power gain at peak ≠ its flat torque MaxScale.
    private static double ComputeMaxPartScale(int engineId, Fh6DatabaseService db, bool includeManifold)
    {
        if (engineId <= 0) return 1.0;

        double product = 1.0;
        product *= MaxTorqueScale(db.GetDisplacement(engineId));
        product *= MaxTorqueScale(db.GetValves(engineId));
        product *= MaxTorqueScale(db.GetPistons(engineId));
        product *= MaxTorqueScale(db.GetFuelSystems(engineId));
        product *= MaxTorqueScale(db.GetIgnition(engineId));
        product *= MaxTorqueScale(db.GetExhaust(engineId));
        product *= MaxTorqueScale(db.GetIntake(engineId));
        // Manifold is mutually exclusive with forced induction; when the engine's best
        // build uses boost, the headers upgrade isn't part of the max.
        if (includeManifold)
            product *= MaxTorqueScale(db.GetManifolds(engineId));
        product *= MaxTorqueScale(db.GetOilCooling(engineId));
        product *= MaxTorqueScale(db.GetRestrictors(engineId));
        // Camshaft handled separately (ComputeCamFactors) — it has no TorqueScale.
        return product;
    }

    // Builds the pre-FI torque curve for a given camshaft (or stock rev range when null),
    // mirroring the engine's curve/redline resolution. Shared so cam contribution can be
    // measured by rebuilding the curve for the stock / current / best cam.
    private static double[] BuildCamTorqueCurve(DbCar dbCar, Fh6DatabaseService db, double baseScale,
        double baseRedlineRPM, double peakTorqueNm, DbUpgradeCamshaft? cam, out int maxRPM)
    {
        double partRedlineRPM = baseRedlineRPM;
        int? torqueCurveId = null;
        double torqueCurveMaxRPM = baseRedlineRPM;
        if (cam != null)
        {
            partRedlineRPM = cam.RedlineRPM > 0 ? cam.RedlineRPM : baseRedlineRPM;
            torqueCurveId = cam.TorqueCurveFullThrottleID > 0 ? cam.TorqueCurveFullThrottleID : null;
            torqueCurveMaxRPM = cam.TorqueCurveMaxRPM > 0 ? cam.TorqueCurveMaxRPM : partRedlineRPM;
        }
        maxRPM = (int)Math.Round(partRedlineRPM);
        if (torqueCurveId != null)
            return LoadTorqueCurve(torqueCurveId.Value, db, baseScale, torqueCurveMaxRPM, partRedlineRPM)
                ?? GenerateIceTorqueCurve(dbCar, peakTorqueNm * baseScale, partRedlineRPM);
        return GenerateIceTorqueCurve(dbCar, peakTorqueNm * baseScale, partRedlineRPM);
    }

    // (current cam peak / stock cam peak, best cam peak / stock cam peak), all pre-FI on
    // the same baseScale so the camshaft's curve/rev-range effect on power is isolated.
    private static (double Current, double Max) ComputeCamFactors(DbCar dbCar, Fh6DatabaseService db,
        int engineId, double baseScale, double baseRedlineRPM, double peakTorqueNm, DbUpgradeCamshaft? selectedCam)
    {
        var cams = db.GetCamshafts(engineId);
        if (cams.Count == 0) return (1.0, 1.0);

        var stockCam = cams.FirstOrDefault(c => c.IsStock) ?? cams[0];
        double refPeak = PowerPeak(
            BuildCamTorqueCurve(dbCar, db, baseScale, baseRedlineRPM, peakTorqueNm, stockCam, out int rRpm), rRpm);
        if (refPeak <= 0.0) return (1.0, 1.0);

        var effCam = selectedCam ?? stockCam;
        double curPeak = PowerPeak(
            BuildCamTorqueCurve(dbCar, db, baseScale, baseRedlineRPM, peakTorqueNm, effCam, out int eRpm), eRpm);

        double bestPeak = refPeak;
        foreach (var c in cams)
        {
            double pk = PowerPeak(
                BuildCamTorqueCurve(dbCar, db, baseScale, baseRedlineRPM, peakTorqueNm, c, out int cRpm), cRpm);
            if (pk > bestPeak) bestPeak = pk;
        }
        return (curPeak / refPeak, bestPeak / refPeak);
    }

    private static double MaxTorqueScale<T>(System.Collections.Generic.List<T> parts) where T : DbUpgradePart
    {
        double max = 1.0;
        foreach (var p in parts)
        {
            if (p.IsStock) continue;
            double s = p.TorqueScale ?? 1.0;
            if (s > max) max = s;
        }
        return max;
    }

    // Best forced-induction part (highest peak scale, any type) and best intercooler
    // scale available for the engine. Used to estimate the maxed build's FI power gain.
    private static (DbUpgradeForcedInduction? Fi, double IcScale) BestForcedInduction(int engineId, Fh6DatabaseService db)
    {
        DbUpgradeForcedInduction? best = null;
        double bestScale = 1.0;
        void Consider(DbUpgradeForcedInduction p, double s) { if (!p.IsStock && s > bestScale) { bestScale = s; best = p; } }
        foreach (var t in db.GetTurbosSingle(engineId)) Consider(t, t.MaxScale);
        foreach (var t in db.GetTurbosTwin(engineId)) Consider(t, t.MaxScale);
        foreach (var c in db.GetCSC(engineId)) Consider(c, c.RedlineRPMScale);
        foreach (var d in db.GetDSC(engineId)) Consider(d, d.RedlineRPMScale);

        double ic = 1.0;
        if (best != null)
            foreach (var x in db.GetIntercoolers(engineId)) if (!x.IsStock && x.MaxScaleScale > ic) ic = x.MaxScaleScale;
        return (best, ic);
    }

    // The engine's factory (stock) forced-induction part, if it ships with one. Such a
    // turbo/SC is marked IsStock in its upgrade table and can't be removed in-game.
    private static DbUpgradeForcedInduction? StockForcedInduction(int engineId, Fh6DatabaseService db)
    {
        if (engineId <= 0) return null;
        return db.GetTurbosSingle(engineId).FirstOrDefault(p => p.IsStock) as DbUpgradeForcedInduction
            ?? db.GetTurbosTwin(engineId).FirstOrDefault(p => p.IsStock)
            ?? db.GetCSC(engineId).FirstOrDefault(p => p.IsStock)
            ?? (DbUpgradeForcedInduction?)db.GetDSC(engineId).FirstOrDefault(p => p.IsStock);
    }

    // Stock power/torque estimate from the engine's real stock-configuration dyno curve:
    // the stock camshaft's torque curve (genuine game magnitude) at stock part scales, plus
    // the factory forced induction if any. Used only when there's no donor car to anchor to.
    // Returns (0,0) when the stock cam has no real curve (would otherwise synthesise one at
    // ceiling magnitude) so the caller keeps the ceiling/maxMult fallback.
    private static (double Hp, double TorqueNm) EstimateStockFromCurve(
        DbCar dbCar, Fh6DatabaseService db, int engineId, double torqueScale,
        double redlineRPM, double peakTorqueNm, DbUpgradeForcedInduction? stockFi)
    {
        var cams = db.GetCamshafts(engineId);
        var stockCam = cams.FirstOrDefault(c => c.IsStock) ?? cams.FirstOrDefault();
        if (stockCam == null || stockCam.TorqueCurveFullThrottleID <= 0) return (0, 0);

        // Stock build: no part upgrades (partScale = 1), so baseScale is just torqueScale.
        double[] curve = BuildCamTorqueCurve(dbCar, db, torqueScale, redlineRPM, peakTorqueNm, stockCam, out int rpm);
        if (stockFi != null) curve = ApplyFiCore(curve, rpm, stockFi, 1.0);
        if (curve.Length == 0) return (0, 0);
        // PowerPeak returns Nm*rpm (a relative proxy); /7121 converts it to HP.
        return (PowerPeak(curve, rpm) / 7121.0, curve.Max());
    }

    // Peak engine power implied by a torque curve (max of torque*rpm over the sweep).
    private static double PowerPeak(double[] torque, int maxRPM)
    {
        if (torque.Length == 0 || maxRPM <= 0) return 0;
        int n = torque.Length;
        double best = 0;
        for (int i = 0; i < n; i++)
        {
            double p = torque[i] * (maxRPM * i / (double)(n - 1));
            if (p > best) best = p;
        }
        return best;
    }

    private static double[] ApplyForcedInductionCurve(double[] curve, int maxRPM, SelectedParts parts, Fh6DatabaseService db, double intercoolerMaxScale)
    {
        if (parts.ForcedInductionPartId == null) return curve;
        var fi = db.GetForcedInductionById(parts.ForcedInductionPartId.Value);
        if (fi is not DbUpgradeForcedInduction fiPart) return curve;
        return ApplyFiCore(curve, maxRPM, fiPart, intercoolerMaxScale);
    }

    private static double[] ApplyFiCore(double[] curve, int maxRPM, DbUpgradeForcedInduction fiPart, double intercoolerMaxScale)
    {
        if (curve.Length == 0 || maxRPM <= 0) return curve;
        double[] result = new double[curve.Length];
        int n = curve.Length;
        for (int i = 0; i < n; i++)
        {
            double rpm = maxRPM * i / (double)(n - 1);
            double m = FiScaleAtRpm(fiPart, rpm, maxRPM, intercoolerMaxScale);
            result[i] = Math.Round(curve[i] * m, 1);
        }
        return result;
    }

    private static double FiScaleAtRpm(DbUpgradeForcedInduction fi, double rpm, double maxRPM, double intercoolerMaxScale)
    {
        double frac = CalculationHelpers.Clamp(rpm / maxRPM, 0.0, 1.0);
        return fi switch
        {
            DbUpgradeTurboSingle ts => TurboScaleAtRpm(ts.MinScale, ts.MaxScale * intercoolerMaxScale, ts, rpm, frac),
            DbUpgradeTurboTwin   tt => TurboScaleAtRpm(tt.MinScale, tt.MaxScale * intercoolerMaxScale, tt, rpm, frac),
            DbUpgradeCSC csc => SuperchargerScaleAtRpm(csc.ZeroRPMScale, csc.RedlineRPMScale * intercoolerMaxScale, frac, centrifugal: true),
            DbUpgradeDSC dsc => SuperchargerScaleAtRpm(dsc.ZeroRPMScale, dsc.RedlineRPMScale * intercoolerMaxScale, frac, centrifugal: false),
            _ => 1.0
        };
    }

    private const double TurboSpoolFraction = 0.35;
    private const double TurboSpoolFractionAntiLag = 0.15;
    private static double TurboScaleAtRpm(double minScale, double maxScale, DbUpgradeForcedInduction fi, double rpm, double frac)
    {
        if (minScale <= 0.0) minScale = maxScale;
        double spool = fi.HasAntiLag ? TurboSpoolFractionAntiLag : TurboSpoolFraction;
        double s;
        if (frac <= spool && spool > 0.0)
        {
            double t = frac / spool;
            t = t * t * (3.0 - 2.0 * t);
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

    private static double FiDropOff(DbUpgradeForcedInduction fi, double rpm)
    {
        double rpm0 = fi.TorqueDropOffRPM0, rpm1 = fi.TorqueDropOffRPM1;
        double s0 = fi.TorqueDropOffScale0 <= 0.0 ? 1.0 : fi.TorqueDropOffScale0;
        double s1 = fi.TorqueDropOffScale1 <= 0.0 ? s0 : fi.TorqueDropOffScale1;
        if (rpm1 <= rpm0 || rpm <= rpm0) return s0;
        double t = CalculationHelpers.Clamp((rpm - rpm0) / (rpm1 - rpm0), 0.0, 1.0);
        return s0 + (s1 - s0) * t;
    }

    private static double[]? LoadTorqueCurve(int curveId, Fh6DatabaseService db, double scale, double curveMaxRPM, double targetMaxRPM)
    {
        var tc = db.GetTorqueCurve(curveId);
        if (tc?.V == null || tc.V.Length == 0) return null;

        double[] raw = tc.V.Select(v => v * tc.TorqueScale * scale).ToArray();
        int rawN = raw.Length;
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
