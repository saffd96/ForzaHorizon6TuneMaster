using System;
using System.Linq;
using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

public static class PowerCalculator
{
    public static bool VerboseLogging { get; set; }

    private const double RadSToRPM = 60.0 / (2.0 * Math.PI);

    // The game's actual rev limiter sits ~10.8 % ABOVE the data "redline"
    // (SimRedlineAngVel / camshaft RedlineRPM). Measured from in-game dyno graphs
    // across 3 cam tiers on the Supra RZ (6800→7530, 7900→8750, 8500→9410 — a
    // constant ×1.108, not an additive offset). DB verification: for all 620 cars,
    // SimRedlineAngVel×60/2π == cam.RedlineRPM exactly — the 1.108 is a GAME-ENGINE
    // behaviour (rev limiter sits above the data redline), not a per-car DB value.
    // Applied universally on the assumption that the game engine scales all engines
    // uniformly, but has only been validated on one car.
    private const double GameRedlineScale = 1.108;

    // Epsilon below which a power/torque figure is treated as "not a real value".
    private const double MinValidValue = 0.1;
    private const int TorqueCurvePoints = 96;
    private const int ResampledCurvePoints = 96;

    public static void Calculate(CarCard car, SelectedParts? parts = null)
    {
        var db = Fh6DatabaseService.Instance;
        var dbCar = db.GetCar(car.CarDbId);
        if (dbCar == null) return;

        parts ??= new SelectedParts();

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

        double curveTorquePeak = torqueCurve is { Length: > 0 } ? torqueCurve.Max() : peakTorqueNm;

        double finalPower, finalTorque;
        if (targetTorqueNm > MinValidValue && curveTorquePeak > MinValidValue)
        {
            double kt = targetTorqueNm / curveTorquePeak;
            var tqScaled = torqueCurve!.Select(t => t * kt).ToArray();
            var pwFromTq = ComputePowerCurveFromTorque(tqScaled, maxRPM);
            double pwFromTqPeak = pwFromTq is { Length: > 0 } ? pwFromTq.Max() : 0;
            if (targetPowerHP > MinValidValue && pwFromTqPeak > MinValidValue && targetPowerHP > pwFromTqPeak)
                kt *= targetPowerHP / pwFromTqPeak;
            torqueCurve = torqueCurve!.Select(t => Math.Round(t * kt, 1)).ToArray();
            finalTorque = Math.Round(torqueCurve.Max());
        }
        else
        {
            finalTorque = curveTorquePeak > MinValidValue ? Math.Round(curveTorquePeak) : 0;
        }
        var powerCurve = ComputePowerCurveFromTorque(torqueCurve, maxRPM);
        finalPower = powerCurve is { Length: > 0 } ? Math.Round(powerCurve.Max(), 1) : 0;

        car.MaxRPM = maxRPM;
        car.TorqueNm = finalTorque;
        car.PowerHP = finalPower;
        car.PowerIsEstimated = estimated;
        car.RotationalInertiaFactor = inertiaFactor;
        car.CachedTorqueCurveNm = torqueCurve;
        car.CachedPowerCurveHP = powerCurve;
    }

    // ── Electric ──────────────────────────────────────────────────────────────

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

        return ((int)Math.Round(maxRpm), peakTorqueNm, 0, 0, torqueCurve, false);
    }

    // ── ICE ───────────────────────────────────────────────────────────────────

    private static (int MaxRPM, double PeakTorqueNm, double TargetPowerHP, double TargetTorqueNm, double[]? TorqueCurve, bool Estimated) CalcIce(
        CarCard car, DbCar dbCar, Fh6DatabaseService db, SelectedParts parts)
    {
        int effectiveEngineId = ResolveEffectiveEngineId(car, dbCar, db, parts);
        var effectiveEngine = db.GetEngine(effectiveEngineId);

        var swapPart = parts.EngineSwapPartId != null ? db.GetEngineSwapById(parts.EngineSwapPartId.Value) : null;
        bool isEngineSwapped = swapPart is { IsStock: false };

        double redlineRPM = dbCar.SimRedlineAngVel * RadSToRPM;
        // EngineGraphingMaxTorque is the engine's *fully-upgraded ceiling*,
        // NOT the stock torque.  For cars with a real cam torque curve (all 1639
        // cams in the DB have one) it is never used — the real curve carries its
        // own magnitude.  The fallback (GenerateIceTorqueCurve) would be fed this
        // ceiling value and produce a curve 2–7× too high, but is never reached.
        // SimPeakTorque is in deci-Nm (×100 → Nm), used only when engine==null.
        double peakTorqueNm = effectiveEngine?.EngineGraphingMaxTorque
                              ?? (dbCar.SimPeakTorque > 0 ? dbCar.SimPeakTorque * 100.0 : 0);
        double torqueScale = isEngineSwapped ? 1.0 : dbCar.GameTorqueScale;

        double partScale = AccumulatePartTorqueScales(parts, db);
        double baseScale = Math.Min(torqueScale * Math.Max(0.1, partScale), 20.0);

        var selectedCam = parts.CamshaftPartId != null ? db.GetCamshaftById(parts.CamshaftPartId.Value) : null;
        selectedCam ??= db.GetCamshafts(effectiveEngineId).FirstOrDefault(cc => cc.IsStock)
                        ?? db.GetCamshafts(effectiveEngineId).FirstOrDefault();
        double[] torqueCurve = BuildCamTorqueCurve(dbCar, db, baseScale, redlineRPM, peakTorqueNm, selectedCam, out int maxRPM);

        double intercoolerMaxScale = 1.0;
        if (parts.ForcedInductionPartId != null && parts.IntercoolerPartId != null)
        {
            var ic = db.GetIntercoolerById(parts.IntercoolerPartId.Value);
            if (ic != null && ic.MaxScaleScale > 0.001)
                intercoolerMaxScale = ic.MaxScaleScale;
        }

        // FI curve for the multiplicative fallback (mulPower/mulTorque).
        double[] fiCurveFull = ApplyForcedInductionCurve(torqueCurve, maxRPM, parts, db, intercoolerMaxScale);

        double powerCapHP = effectiveEngine != null
            ? effectiveEngine.EngineGraphingMaxPower * PhysicsConstants.KwToHp
            : 0;
        if (powerCapHP <= MinValidValue)
            return (maxRPM, fiCurveFull.Max(), 0, 0, fiCurveFull, true);

        var stockCam = db.GetCamshafts(effectiveEngineId).FirstOrDefault(c => c.IsStock)
                       ?? db.GetCamshafts(effectiveEngineId).FirstOrDefault();
        double[] stockCurve = BuildCamTorqueCurve(dbCar, db, torqueScale, redlineRPM, peakTorqueNm, stockCam, out int stockMaxRPM);

        var selectedFi = parts.ForcedInductionPartId != null
            ? db.GetForcedInductionById(parts.ForcedInductionPartId.Value) as DbUpgradeForcedInduction
            : null;
        var stockFi = StockForcedInduction(effectiveEngineId, db);
        var currentFi = selectedFi ?? stockFi;

        // ── Peak curve magnitudes (Nm×rpm proxy — divide by NmRpmToHp for HP) ──
        double naCurPeakProxy = torqueCurve.Length > 0 ? TorqueRpmPeak(torqueCurve, maxRPM) : 0;
        double naStkPeakProxy = stockCurve.Length > 0 ? TorqueRpmPeak(stockCurve, stockMaxRPM) : 0;
        double naCurPeakHP = naCurPeakProxy / PhysicsConstants.NmRpmToHp;
        double naStkPeakHP = naStkPeakProxy / PhysicsConstants.NmRpmToHp;

        // ── Base breathing (cam + torqueScale only, NO partScale) ──────────────
        double[] naCurveNoParts = partScale > 1.001
            ? BuildCamTorqueCurve(dbCar, db, torqueScale, redlineRPM, peakTorqueNm, selectedCam, out _)
            : torqueCurve;
        double naBaseProxy = naCurveNoParts.Length > 0 ? TorqueRpmPeak(naCurveNoParts, maxRPM) : naCurPeakProxy;
        double naBaseHP = naBaseProxy / PhysicsConstants.NmRpmToHp;

        // ── Part gains (additive — turbo does NOT multiply bolt-on parts) ──────
        double partHP = Math.Max(naCurPeakHP - naBaseHP, 0);
        double partTq = Math.Max((torqueCurve.Length > 0 ? torqueCurve.Max() : 0)
                                 - (naCurveNoParts.Length > 0 ? naCurveNoParts.Max() : 0), 0);

        // ── Stock turbo effective multiplier (asymptotic, no breathing clamp) ──
        double stockTurboMultEff = StockTurboMultiplier(stockFi);

        // ── Pure NA breathing: deboost cam curve by stock FI ──────────────────
        // Cam torque curves include the stock FI.  To avoid double-counting when
        // computing the new turbo multiplier, divide out the stock FI first.
        double naPureBaseHP = stockTurboMultEff > 1.001
            ? naBaseHP / stockTurboMultEff
            : naBaseHP;

        // ── Current turbo multiplier (with breathing) ─────────────────────────
        double curTurboMult = TurboMult(currentFi, naPureBaseHP);

        // ── Primary power: (pure NA breathing × current turbo) + additive parts
        double addPower = naPureBaseHP * curTurboMult + partHP;
        double naPureBaseTq = stockTurboMultEff > 1.001
            ? (naCurveNoParts.Length > 0 ? naCurveNoParts.Max() : 0) / stockTurboMultEff
            : (naCurveNoParts.Length > 0 ? naCurveNoParts.Max() : 0);
        double addTorque = naPureBaseTq * curTurboMult + partTq;

        // ── Output FI curve (used for shape only — magnitude re-scaled in ApplyResults)
        double[] fiCurve = ApplyForcedInductionCurve(naCurveNoParts, maxRPM, parts, db, intercoolerMaxScale);

        // ── Stock anchor ─────────────────────────────────────────────────────
        bool nativeEngine = !isEngineSwapped;
        var donor = nativeEngine ? null
            : (effectiveEngine != null ? db.GetCarByMediaName(effectiveEngine.MediaName) : null);
        double curveStockHp = 0, curveStockTq = 0;
        bool noAnchorFigure = !(nativeEngine && dbCar.SimPeakPower > 0) && donor is not { SimPeakPower: > 0 };
        if (noAnchorFigure)
            (curveStockHp, curveStockTq) = EstimateStockFromCurve(
                dbCar, db, effectiveEngineId, torqueScale, redlineRPM, peakTorqueNm, stockFi);

        double torqueCapNm = effectiveEngine?.EngineGraphingMaxTorque ?? 0;

        double stockHP;
        if (nativeEngine && dbCar.SimPeakPower > 0)
            stockHP = dbCar.SimPeakPower * PhysicsConstants.DeciKwToHp;
        else if (donor is { SimPeakPower: > 0 })
            stockHP = donor.SimPeakPower * PhysicsConstants.DeciKwToHp;
        else
            stockHP = curveStockHp > MinValidValue ? curveStockHp : powerCapHP * 0.3;
        stockHP = Math.Min(stockHP, powerCapHP);

        double stockTorqueNm;
        if (nativeEngine && dbCar.SimPeakTorque > 0)
            stockTorqueNm = dbCar.SimPeakTorque * 100.0;
        else if (donor is { SimPeakTorque: > 0 })
            stockTorqueNm = donor.SimPeakTorque * 100.0;
        else
            stockTorqueNm = curveStockTq > MinValidValue ? curveStockTq
                : (torqueCapNm > MinValidValue ? torqueCapNm * 0.5 : 0);
        if (torqueCapNm > MinValidValue) stockTorqueNm = Math.Min(stockTorqueNm, torqueCapNm);

        // ── Multiplicative fallback ──────────────────────────────────────────
        double[] stockFiCurve = stockFi != null ? ApplyFiCore(stockCurve, stockMaxRPM, stockFi, 1.0) : stockCurve;
        double mulRefPow = TorqueRpmPeak(stockFiCurve, stockMaxRPM);
        double mulPower = mulRefPow > MinValidValue ? stockHP * TorqueRpmPeak(fiCurveFull, maxRPM) / mulRefPow : 0;
        double mulRefTq = stockFiCurve.Length > 0 ? stockFiCurve.Max() : 0;
        double mulTorque = mulRefTq > MinValidValue ? stockTorqueNm * (fiCurveFull.Length > 0 ? fiCurveFull.Max() : 0) / mulRefTq : 0;

        // ── When FI output barely changes, anchor to stock + additive cam delta ──
        bool fiChanged = currentFi != stockFi || (currentFi != null && stockFi != null && currentFi.Id != stockFi.Id);
        if (!fiChanged)
        {
            // Cam-only (or pure stock): FI didn't change, so the breathing model
            // should produce naPureBaseHP*stockTurboMult+partHP ≈ naCurPeakHP+partHP.
            // Keep the additive anchor fallback for cases where the curve-to-anchor
            // ratio drifts with cam-only changes.
            double naDeltaPowerHp = (naCurPeakProxy - naStkPeakProxy) / PhysicsConstants.NmRpmToHp;
            double naDeltaTorqueNm = (torqueCurve.Length > 0 ? torqueCurve.Max() : 0)
                                     - (stockCurve.Length > 0 ? stockCurve.Max() : 0);
            addPower = stockHP + naDeltaPowerHp;
            addTorque = stockTorqueNm + naDeltaTorqueNm;
        }
        else if (stockFi == null || Ms(stockFi) <= 0)
        {
            // NA engine receiving its FIRST forced induction.
            double pressureScale = Ms(currentFi);
            double baseEff = TurboBaseEff;
            if (pressureScale <= 0)
            {
                pressureScale = currentFi switch { DbUpgradeCSC csc => csc.RedlineRPMScale, DbUpgradeDSC dsc => dsc.RedlineRPMScale, _ => 1.0 };
                baseEff = SCBaseEff;
            }
            if (pressureScale > 1.0)
            {
                addPower = stockHP * (1.0 + torqueScale * baseEff * (pressureScale - 1.0));
                addTorque = stockTorqueNm * (1.0 + torqueScale * baseEff * (pressureScale - 1.0));
            }
            mulPower = addPower;
            mulTorque = addTorque;
        }
        else
        {
            // Boosted engine, FI upgrade: anchor to the stock dyno figure (SimPeakPower),
            // NOT the raw DB torque-curve magnitude. On some engines the curve under-reports
            // stock (e.g. a 2.0L I6-TT: curve ~136 hp vs SimPeakPower 206 hp), so deboosting
            // the curve then reboosting it could land a BIGGER turbo BELOW stock. Re-deriving
            // the pure-NA base from the stock anchor keeps the same MaxMult breathing model
            // but guarantees output scales from the real stock figure for every car.
            double naAnchorHP = stockTurboMultEff > 1.001 ? stockHP / stockTurboMultEff : stockHP;
            double naAnchorTq = stockTurboMultEff > 1.001 ? stockTorqueNm / stockTurboMultEff : stockTorqueNm;
            double anchorMult = TurboMult(currentFi, naAnchorHP);     // breath from anchored NA base
            double anchorRatio = naBaseHP > MinValidValue ? stockHP / naBaseHP : 1.0;
            addPower = naAnchorHP * anchorMult + partHP * anchorRatio;
            addTorque = naAnchorTq * anchorMult + partTq * anchorRatio;
            // Keep the multiplicative estimate (mulPower/mulTorque from above) alive so
            // Math.Max can pick it: the breathing model (addPower) under-reports big
            // SINGLE turbos by ~25 % vs the engine's dyno ceiling, while the full-MaxScale
            // curve ratio reaches it. addPower still acts as a floor (never below stock).
        }

        // When FI is actually upgraded, the reported peak must not fall below the peak of
        // the forced-induction torque curve the model itself builds (and hands to the UI):
        // the breathing/anchor scalar under-reports big SINGLE turbos by ~25 %, leaving the
        // headline power BELOW its own power curve. Stock / cam-only builds keep the exact
        // SimPeak anchor (fiChanged == false), so they are untouched. Still clamped to the cap.
        if (fiChanged)
        {
            double fiPeakHp = TorqueRpmPeak(fiCurveFull, maxRPM) / PhysicsConstants.NmRpmToHp;
            addPower = Math.Max(addPower, fiPeakHp);
            if (fiCurveFull.Length > 0) addTorque = Math.Max(addTorque, fiCurveFull.Max());
        }

        double targetPowerHP = Math.Clamp(Math.Max(addPower, mulPower), MinValidValue, powerCapHP);
        double targetTorqueNm = Math.Max(Math.Max(addTorque, mulTorque), MinValidValue);
        if (torqueCapNm > MinValidValue) targetTorqueNm = Math.Min(targetTorqueNm, torqueCapNm);

        var finalPower = Math.Round(targetPowerHP, 1);
        var finalTorque = Math.Round(targetTorqueNm, 1);

        if (VerboseLogging)
        {
            double stkPeakPower = naStkPeakProxy / PhysicsConstants.NmRpmToHp;
            var fiPart = selectedFi;
            Console.WriteLine("=== CalcIce Diagnostics ===");
            Console.WriteLine($"  stockHP={stockHP:F1}  stockTorqueNm={stockTorqueNm:F1}");
            Console.WriteLine($"  torqueScale={torqueScale:F3}  partScale={partScale:F3}  baseScale={baseScale:F3}");
            Console.WriteLine($"  cam RedlineRPM={(selectedCam?.RedlineRPM ?? redlineRPM):F0}  maxRPM={maxRPM}  stockMaxRPM={stockMaxRPM}");
            Console.WriteLine($"  naCurvePeakTorque={torqueCurve.Max():F1}  naCurvePeakPower={naCurPeakHP:F1}");
            Console.WriteLine($"  stkCurvePeakTorque={stockCurve.Max():F1}  stkCurvePeakPower={stkPeakPower:F1}");
            double fiPms = fiPart switch { DbUpgradeTurboSingle ts => ts.PowerMaxScale, DbUpgradeTurboTwin tt => tt.PowerMaxScale, _ => -1 };
            double fiMs = fiPart switch { DbUpgradeTurboSingle ts => ts.MaxScale, DbUpgradeTurboTwin tt => tt.MaxScale, _ => -1 };
            Console.WriteLine($"  selectedFi PowerMaxScale={fiPms}  MaxScale={fiMs}  HasAntiLag={fiPart?.HasAntiLag ?? false}");
            Console.WriteLine($"  stockTurboMultEff={stockTurboMultEff:F3}  curTurboMult={curTurboMult:F3}");
            Console.WriteLine($"  naPureBaseHP={naPureBaseHP:F1}  naBaseHP={naBaseHP:F1}  partHP={partHP:F1}");
            Console.WriteLine($"  intercoolerMaxScale={intercoolerMaxScale:F3}");
            Console.WriteLine($"  addPower={addPower:F1}  mulPower={mulPower:F1}  winner={Math.Max(addPower, mulPower):F1}");
            Console.WriteLine($"  powerCapHP={powerCapHP:F1}  torqueCapNm={torqueCapNm:F1}");
            Console.WriteLine($"  targetPowerHP={finalPower:F1}  targetTorqueNm={finalTorque:F1}");
            Console.WriteLine($"  fiCurvePeak={fiCurve.Max():F1}  fiCurvePeakPower={TorqueRpmPeak(fiCurve, maxRPM) / PhysicsConstants.NmRpmToHp:F1}");
        }

        return (maxRPM, fiCurve.Max(), finalPower, finalTorque, fiCurve, noAnchorFigure);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

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
        AddPart(db, parts.DisplacementPartId, id => db.GetDisplacementById(id), scales);
        AddPart(db, parts.ValvesPartId, id => db.GetValvesById(id), scales);
        AddPart(db, parts.PistonsPartId, id => db.GetPistonsById(id), scales);
        AddPart(db, parts.FuelSystemPartId, id => db.GetFuelSystemById(id), scales);
        AddPart(db, parts.IgnitionPartId, id => db.GetIgnitionById(id), scales);
        AddPart(db, parts.ExhaustPartId, id => db.GetExhaustById(id), scales);
        AddPart(db, parts.IntakePartId, id => db.GetIntakeById(id), scales);
        if (parts.ForcedInductionPartId == null)
            AddPart(db, parts.ManifoldPartId, id => db.GetManifoldById(id), scales);
        AddPart(db, parts.OilCoolingPartId, id => db.GetOilCoolingById(id), scales);
        AddPart(db, parts.RestrictorPartId, id => db.GetRestrictorById(id), scales);

        if (scales.Count == 0) return 1.0;

        double product = scales.Aggregate(1.0, (p, s) => p * s);
        return product;
    }

    private static void AddPart<T>(Fh6DatabaseService db, int? partId, Func<int, T?> getter, System.Collections.Generic.List<double> scales)
        where T : DbUpgradePart
    {
        if (partId == null) return;
        var part = getter(partId.Value);
        if (part == null || part.IsStock) return;
        double s = part.TorqueScale ?? 1.0;
        if (Math.Abs(s - 1.0) < 0.005) return;
        scales.Add(s);
    }

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
        partRedlineRPM *= GameRedlineScale;
        maxRPM = (int)Math.Round(partRedlineRPM);
        if (torqueCurveId != null && torqueCurveId.HasValue)
            return LoadTorqueCurve(torqueCurveId.Value, db, baseScale, torqueCurveMaxRPM, partRedlineRPM)
                ?? GenerateIceTorqueCurve(dbCar, peakTorqueNm * baseScale, partRedlineRPM);
        return GenerateIceTorqueCurve(dbCar, peakTorqueNm * baseScale, partRedlineRPM);
    }

    // ── Turbo multiplier model ──────────────────────────────────────────────────
    // Calibrated on Supra RZ (2JZ-GTE, twin-turbo stock, single-turbo upgrades).
    // MaxScale → asymptotic multiplier:  maxMult = MaxMultA × MaxScale + MaxMultB.
    // PowerMaxScale controls *breathing* — whether the turbo can reach its MaxScale
    // ceiling at the current NA peak.  Anti-lag halves PMS resistance.
    //
    // NOTE: all four constants are empirical from one engine family.  31 single-turbo
    // upgrades across the DB have MaxScale > 3.0 — the linear extrapolation into that
    // range has not been validated against in-game measurements.
    private const double MaxMultA = 2.15;
    private const double MaxMultB = -1.085;
    private const double BreathK = 0.2109;
    private const double BreathAlFactor = 0.5;

    // NA engine receiving its FIRST forced induction: power = stockHP × (1 + GTS × Eff × (Ms−1)).
    private const double TurboBaseEff = 0.855;
    private const double SCBaseEff = 0.52;

    /// <summary>Asymptotic turbo multiplier (no breathing clamp).</summary>
    private static double StockTurboMultiplier(DbUpgradeForcedInduction? fi)
    {
        double ms = Ms(fi);
        return ms > 0 ? Math.Max(MaxMultA * ms + MaxMultB, 1.0) : 1.0;
    }

    /// <summary>Effective turbo multiplier at the current breathing level.</summary>
    private static double TurboMult(DbUpgradeForcedInduction? fi, double naPurePeakHP)
    {
        double ms = Ms(fi);
        if (ms <= 0) return 1.0;
        double pms = Pm(fi);
        if (pms <= 0) return Math.Max(MaxMultA * ms + MaxMultB, 1.0);
        double tau = BreathK * pms * (fi?.HasAntiLag == true ? BreathAlFactor : 1.0);
        double breath = Math.Clamp(naPurePeakHP / tau, 0.05, 1.0);
        double effectiveMs = ms * breath;
        return Math.Max(MaxMultA * effectiveMs + MaxMultB, 1.0);
    }

    private static double Pm(DbUpgradeForcedInduction? fi) => fi switch
    {
        DbUpgradeTurboSingle ts => ts.PowerMaxScale,
        DbUpgradeTurboTwin tt => tt.PowerMaxScale,
        _ => 0
    };

    private static double Ms(DbUpgradeForcedInduction? fi) => fi switch
    {
        DbUpgradeTurboSingle ts => ts.MaxScale,
        DbUpgradeTurboTwin tt => tt.MaxScale,
        _ => 0
    };

    private static DbUpgradeForcedInduction? StockForcedInduction(int engineId, Fh6DatabaseService db)
    {
        if (engineId <= 0) return null;
        return db.GetTurbosSingle(engineId).FirstOrDefault(p => p.IsStock) as DbUpgradeForcedInduction
            ?? db.GetTurbosTwin(engineId).FirstOrDefault(p => p.IsStock)
            ?? db.GetCSC(engineId).FirstOrDefault(p => p.IsStock)
            ?? (DbUpgradeForcedInduction?)db.GetDSC(engineId).FirstOrDefault(p => p.IsStock);
    }

    private static (double Hp, double TorqueNm) EstimateStockFromCurve(
        DbCar dbCar, Fh6DatabaseService db, int engineId, double torqueScale,
        double redlineRPM, double peakTorqueNm, DbUpgradeForcedInduction? stockFi)
    {
        var cams = db.GetCamshafts(engineId);
        var stockCam = cams.FirstOrDefault(c => c.IsStock) ?? cams.FirstOrDefault();
        if (stockCam == null || stockCam.TorqueCurveFullThrottleID <= 0) return (0, 0);

        double[] curve = BuildCamTorqueCurve(dbCar, db, torqueScale, redlineRPM, peakTorqueNm, stockCam, out int rpm);
        if (stockFi != null) curve = ApplyFiCore(curve, rpm, stockFi, 1.0);
        if (curve.Length == 0) return (0, 0);
        return (TorqueRpmPeak(curve, rpm) / PhysicsConstants.NmRpmToHp, curve.Max());
    }

    /// <summary>
    /// Returns max(torque[i] × rpm) over the sweep — a proxy for power in Nm×RPM units.
    /// Callers must divide by <see cref="PhysicsConstants.NmRpmToHp"/> to get HP.
    /// </summary>
    private static double TorqueRpmPeak(double[] torque, int maxRPM)
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

    // ── Forced-induction curve helpers ─────────────────────────────────────────

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

    // ── Torque curve loading / generation ─────────────────────────────────────

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

        const int outN = ResampledCurvePoints;
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

    /// <summary>
    /// Synthetic ICE torque curve (fallback — never called for the current DB
    /// because all 1639 camshafts have a real TorqueCurveFullThrottleID).
    /// </summary>
    private static double[] GenerateIceTorqueCurve(DbCar dbCar, double peakTorque, double redlineRPM)
    {
        int points = TorqueCurvePoints;
        double[] curve = new double[points];
        double torquePeakRPM = dbCar.SimPeakTorqueAngVel * RadSToRPM;
        double powerPeakRPM = dbCar.SimPeakAngVel * RadSToRPM;

        // Torque drop from peak to power-peak RPM varies 27–100 % across cars;
        // 0.08 is a mid-range approximation that covers the common case (~6200 RPM)
        // but understates the drop for high-rev engines.
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
        int points = TorqueCurvePoints;
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
            power[i] = Math.Round(torqueCurve[i] * rpm / PhysicsConstants.NmRpmToHp, 1);
        }
        return power;
    }
}
