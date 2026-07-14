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
            ApplyClosedThrottleCurve(car, dbCar, db, parts);
        }
    }

    private static void ApplyResults(CarCard car, SelectedParts parts, Fh6DatabaseService db,
        int maxRPM, double peakTorqueNm, double targetPowerHP, double targetTorqueNm, double[]? torqueCurve,
        bool estimated)
    {
        double inertiaFactor = TuningPhysicsContext.ComputeRotationalInertiaFactor(car, parts, db);

        double curveTorquePeak = torqueCurve is { Length: > 0 } ? torqueCurve.Max() : peakTorqueNm;

        double finalPower, finalTorque;
        // Two-pass scaling: match target power, then independently match target torque.
        // The old single-kt approach let kt_power cancel kt_torque, so trqRatio had no effect.
        // powerCurve is snapped from the step-1 curve so its peak == targetPowerHP regardless
        // of step 2 (e.g. CSC trqRatio=0.426 gives tqFix>1, inflating the curve above target).
        double[]? powerCurve = null;
        if (torqueCurve is { Length: > 0 })
        {
            // Step 1: scale torque curve so its power peak matches targetPowerHP
            var origPw = ComputePowerCurveFromTorque(torqueCurve, maxRPM);
            double origPwPeak = origPw is { Length: > 0 } ? origPw.Max() : 0;
            double kt = targetPowerHP > MinValidValue && origPwPeak > MinValidValue
                ? targetPowerHP / origPwPeak : 1.0;
            torqueCurve = torqueCurve.Select(t => Math.Round(t * kt, 1)).ToArray();

            powerCurve = ComputePowerCurveFromTorque(torqueCurve, maxRPM);

            // Step 2: fine-tune so peak torque matches targetTorqueNm
            if (targetTorqueNm > MinValidValue && torqueCurve.Max() > MinValidValue)
            {
                double tqFix = targetTorqueNm / torqueCurve.Max();
                if (Math.Abs(tqFix - 1.0) > 0.0001)
                    torqueCurve = torqueCurve.Select(t => Math.Round(t * tqFix, 1)).ToArray();
            }
        }

        finalTorque = targetTorqueNm > MinValidValue ? Math.Round(targetTorqueNm)
                      : (curveTorquePeak > MinValidValue ? Math.Round(curveTorquePeak) : 0);
        powerCurve ??= ComputePowerCurveFromTorque(torqueCurve, maxRPM);
        finalPower = targetPowerHP > MinValidValue ? Math.Round(targetPowerHP, 1)
                     : (powerCurve is { Length: > 0 } ? Math.Round(powerCurve.Max(), 1) : 0);

        // Rotational inertia (RotationalInertiaFactor, below) intentionally does NOT scale
        // PowerHP/TorqueNm/the cached curves — it was tried (2026-07) and reverted: the one
        // real-game calibration point that seemed to require it turned out to be explained by
        // a mismatched forced-induction level in a saved profile, not a missing effect, and
        // applying it let a maxed-out build's power exceed the engine's own documented ceiling
        // (EngineGraphingMaxPower) after the ceiling clamp above had already been applied.
        car.MaxRPM = maxRPM;
        car.TorqueNm = finalTorque;
        car.PowerHP = finalPower;
        car.PowerIsEstimated = estimated;
        car.RotationalInertiaFactor = inertiaFactor;
        car.CachedTorqueCurveNm = torqueCurve;
        car.CachedPowerCurveHP = powerCurve;
    }

    private static void ApplyClosedThrottleCurve(CarCard car, DbCar dbCar, Fh6DatabaseService db, SelectedParts parts)
    {
        if (car.CachedTorqueCurveNm == null) return;
        int effectiveEngineId = ResolveEffectiveEngineId(car, dbCar, db, parts);
        if (effectiveEngineId <= 0) return;

        var cam = parts.CamshaftPartId != null
            ? db.GetCamshaftById(parts.CamshaftPartId.Value)
            : db.GetCamshafts(effectiveEngineId).FirstOrDefault(c => c.IsStock)
              ?? db.GetCamshafts(effectiveEngineId).FirstOrDefault();
        if (cam == null) return;

        var tc = db.GetTorqueCurve(cam.TorqueCurveFullThrottleID);
        if (tc == null || tc.TorqueScale <= 0.001 || tc.ZeroThrottleTorqueScale <= 0) return;

        double closeRatio = tc.ZeroThrottleTorqueScale / tc.TorqueScale;
        car.CachedClosedThrottleCurveNm = car.CachedTorqueCurveNm.Select(t => Math.Round(t * closeRatio, 1)).ToArray();
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
        // SimPeakTorque is in centi-Nm (×100 → Nm), used only when engine==null.
        double peakTorqueNm = effectiveEngine?.EngineGraphingMaxTorque
                              ?? (dbCar.SimPeakTorque > 0 ? dbCar.SimPeakTorque * 100.0 : 0);
        double torqueScale = isEngineSwapped ? 1.0 : dbCar.GameTorqueScale;

        double partScale = AccumulatePartTorqueScales(parts, db);
        double baseScale = Math.Min(torqueScale * Math.Max(0.1, partScale), 20.0);

        var selectedCam = parts.CamshaftPartId != null ? db.GetCamshaftById(parts.CamshaftPartId.Value) : null;
        selectedCam ??= db.GetCamshafts(effectiveEngineId).FirstOrDefault(cc => cc.IsStock)
                        ?? db.GetCamshafts(effectiveEngineId).FirstOrDefault();
        double[] torqueCurve = BuildCamTorqueCurve(dbCar, db, baseScale, redlineRPM, peakTorqueNm, selectedCam, out int maxRPM);

        var selectedFi = parts.ForcedInductionPartId != null
            ? db.GetForcedInductionById(parts.ForcedInductionPartId.Value) as DbUpgradeForcedInduction
            : null;
        var stockFi = StockForcedInduction(effectiveEngineId, db);
        var currentFi = selectedFi ?? stockFi;

        double intercoolerMaxScale = 1.0;
        if (currentFi != null && parts.IntercoolerPartId != null)
        {
            var ic = db.GetIntercoolerById(parts.IntercoolerPartId.Value);
            if (ic != null && ic.MaxScaleScale > 0.001)
            {
                // The base (Lv=1) intercooler is always bundled with the FI kit; its effect is
                // already captured in the stock power anchor (SimPeakPower) or FI MassDiff.
                // Only count the scale DELTA above the base intercooler so that auto-installing
                // the base IC does not add spurious power.
                var allIcs = db.GetIntercoolers(effectiveEngineId);
                double icBaseScale = allIcs.Count > 0
                    ? allIcs.OrderBy(x => x.Level).First().MaxScaleScale
                    : 1.0;
                double icDelta = icBaseScale > 0.001 ? ic.MaxScaleScale / icBaseScale : ic.MaxScaleScale;
                intercoolerMaxScale = icDelta > 1.0001 ? icDelta : 1.0;
            }
        }

        // FI curve for the multiplicative fallback (mulPower/mulTorque).
        // Only apply the stock FI directly when an intercooler is actually selected without
        // an explicit FI upgrade — this captures the intercooler gain on factory-turbocharged
        // engines while leaving the pure-stock path unchanged (avoids asp6 shape regression).
        double[] fiCurveFull = parts.ForcedInductionPartId != null
            ? ApplyForcedInductionCurve(torqueCurve, maxRPM, parts, db, intercoolerMaxScale)
            : (stockFi != null && intercoolerMaxScale > 1.001)
                ? ApplyFiCore(torqueCurve, maxRPM, stockFi, intercoolerMaxScale)
                : torqueCurve;

        double powerCapHP = effectiveEngine != null
            ? effectiveEngine.EngineGraphingMaxPower * PhysicsConstants.KwToHp
            : 0;
        if (powerCapHP <= MinValidValue)
            return (maxRPM, fiCurveFull.Max(), 0, 0, fiCurveFull, true);

        var stockCam = db.GetCamshafts(effectiveEngineId).FirstOrDefault(c => c.IsStock)
                       ?? db.GetCamshafts(effectiveEngineId).FirstOrDefault();
        double[] stockCurve = BuildCamTorqueCurve(dbCar, db, torqueScale, redlineRPM, peakTorqueNm, stockCam, out int stockMaxRPM);

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

        // ── Part HP/torque gains (game applies partScale multiplicatively at curve level) ──
        double partHP = Math.Max(naCurPeakHP - naBaseHP, 0);
        double partTq = Math.Max((torqueCurve.Length > 0 ? torqueCurve.Max() : 0)
                                 - (naCurveNoParts.Length > 0 ? naCurveNoParts.Max() : 0), 0);

        // ── Scalar power/torque estimates — each branch below overwrites these ──
        // The authoritative result is mulPower (line below) which implements the
        // exact game formula: stockHP × peak(baseCurve×parts×FiScale) / peak(baseCurve×stockFi).
        double addPower = 0;
        double addTorque = 0;

        // ── Output FI curve: use fiCurveFull (already includes partScale) for correct curve shape
        double[] fiCurve = fiCurveFull;

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

        // ── Curve-to-real-HP bridge: naBaseHP is the cam-only curve peak (curve-space);
        //     stockHP is the real dyno figure.  anchorRatio rescales bolt-on HP/Tq gains
        //     from curve-space into real-HP-space so they can mix with stockHP. ──
        double anchorRatio = naBaseHP > MinValidValue ? stockHP / naBaseHP : 1.0;

        // ── Multiplicative fallback ──────────────────────────────────────────
        double[] stockFiCurve = stockFi != null ? ApplyFiCore(stockCurve, stockMaxRPM, stockFi, 1.0) : stockCurve;
        double mulRefPow = TorqueRpmPeak(stockFiCurve, stockMaxRPM);
        double mulPower = mulRefPow > MinValidValue ? stockHP * TorqueRpmPeak(fiCurveFull, maxRPM) / mulRefPow : 0;
        double mulRefTq = stockFiCurve.Length > 0 ? stockFiCurve.Max() : 0;
        double mulTorque = mulRefTq > MinValidValue ? stockTorqueNm * (fiCurveFull.Length > 0 ? fiCurveFull.Max() : 0) / mulRefTq : 0;

        // ── When FI output barely changes, anchor to stock + additive cam delta ──
        bool fiChanged = (intercoolerMaxScale > 1.001 && currentFi != null)
            || currentFi != stockFi || (currentFi != null && stockFi != null && currentFi.Id != stockFi.Id);
        if (!fiChanged)
        {
            // Cam-only (or pure stock): FI didn't change — anchor to SimPeakPower plus
            // the cam-upgrade delta read directly off the curve. Keeps exact stock figure.
            double naDeltaPowerHp = (naCurPeakProxy - naStkPeakProxy) / PhysicsConstants.NmRpmToHp;
            double naDeltaTorqueNm = (torqueCurve.Length > 0 ? torqueCurve.Max() : 0)
                                     - (stockCurve.Length > 0 ? stockCurve.Max() : 0);
            addPower = stockHP + naDeltaPowerHp;
            addTorque = stockTorqueNm + naDeltaTorqueNm;
        }
        else if (stockFi == null || Ms(stockFi) <= 0)
        {
            // NA engine receiving its FIRST forced induction.
            // Pressure scale and efficiency depend on FI type:
            //   turbos: MaxScale (pressure ceiling)
            //   superchargers: RedlineRPMScale (boost at redline, crank-driven)
            // Efficiencies calibrated per-type on Nissan Fairlady Z '03.
            double pressureScale;
            double baseEff;
            double trqRatio;
            switch (currentFi)
            {
                case DbUpgradeTurboSingle ts: pressureScale = ts.MaxScale;         baseEff = STBaseEff;  trqRatio = 1.0;   break;
                case DbUpgradeTurboTwin   tt: pressureScale = tt.MaxScale;         baseEff = TTBaseEff;  trqRatio = 1.0;   break;
                case DbUpgradeCSC        csc: pressureScale = csc.RedlineRPMScale; baseEff = CSCBaseEff; trqRatio = 0.426; break;
                case DbUpgradeDSC        dsc: pressureScale = dsc.RedlineRPMScale; baseEff = DSCBaseEff; trqRatio = 1.0;   break;
                default:                       pressureScale = 1.0;                baseEff = 0;          trqRatio = 0;     break;
            }
            if (pressureScale > 1.0)
            {
                // Bolt-on parts (cam, exhaust, intake, etc.) increase NA breathing
                // before the turbo multiplies it.  Scale part gains from curve-space
                // to real-HP-space via the same anchorRatio used in the FI-upgrade path.
                double naPowerWithParts = stockHP + partHP * anchorRatio;
                double naTorqueWithParts = stockTorqueNm + partTq * anchorRatio;
                addPower = naPowerWithParts * (1.0 + torqueScale * baseEff * (pressureScale - 1.0));
                addTorque = naTorqueWithParts * (1.0 + torqueScale * baseEff * trqRatio * (pressureScale - 1.0));
            }
        }
        else
        {
            // Boosted engine, FI upgrade: deboost the stock anchor to its pure-NA base
            // using the same linear pressure-efficiency model as the NA→first-FI path,
            // then reboost with the new FI.  The old asymptotic model (MaxMultA×Ms+MaxMultB,
            // calibrated on one engine) overestimated power for most other engines — e.g.
            // Syclone 4.3L V6 TT gave 410 hp vs in-game 362 hp (+13 %).  The linear
            // efficiency model matches measured in-game power across all engine families.
            double stockFiMult = FiEfficiencyMultiplier(stockFi, torqueScale);
            double naAnchorHP = stockFiMult > 1.001 ? stockHP / stockFiMult : stockHP;
            double naAnchorTq = stockFiMult > 1.001 ? stockTorqueNm / stockFiMult : stockTorqueNm;
            double currentFiMult = FiEfficiencyMultiplier(currentFi, torqueScale);
            double currentFiTqMult = FiTorqueMultiplier(currentFi, torqueScale);
            addPower = (naAnchorHP + partHP * anchorRatio) * currentFiMult;
            addTorque = (naAnchorTq + partTq * anchorRatio) * currentFiTqMult;
        }

        // For NA→any-FI, the calibrated pressure-efficiency formula (addPower) is
        // authoritative at EVERY level — not just Lv1.  mulPower uses the raw FI
        // curve ratio without efficiency losses and overshoots at higher boost
        // (e.g. Fairlady Z '03 DSC: Lv1=317✓, Lv2=350→337✗, Lv3=365→350✗).
        // Boosted-engine FI upgrades keep max(addPower, mulPower) as before —
        // the ratio of two FI curves partially cancels the efficiency error.
        bool isNaToFi = stockFi == null && selectedFi != null;

        // FI curve floor guard: ensures addPower isn't below what the forced-induction
        // torque curve suggests.  Only applies to boosted-engine FI upgrades — for
        // NA→FI the curve peak includes the raw pressure multiplier without efficiency
        // and would overshoot (especially CSC/DSC with quadratic/linear ramp).
        if (fiChanged && !isNaToFi)
        {
            double fiPeakHp = TorqueRpmPeak(fiCurveFull, maxRPM) / PhysicsConstants.NmRpmToHp;
            addPower = Math.Max(addPower, fiPeakHp);
            if (fiCurveFull.Length > 0) addTorque = Math.Max(addTorque, fiCurveFull.Max());
        }
        double targetPowerHP = Math.Clamp(isNaToFi ? addPower : Math.Max(addPower, mulPower), MinValidValue, powerCapHP);
        double targetTorqueNm = Math.Max(isNaToFi ? addTorque : Math.Max(addTorque, mulTorque), MinValidValue);
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
            Console.WriteLine($"  stockFiMult={FiEfficiencyMultiplier(stockFi, torqueScale):F3}  currentFiMult={FiEfficiencyMultiplier(currentFi, torqueScale):F3}");
            Console.WriteLine($"  naBaseHP={naBaseHP:F1}  partHP={partHP:F1}");
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
        AddPart(db, parts.ManifoldPartId, id => db.GetManifoldById(id), scales);
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

    // ── Unified FI efficiency model ────────────────────────────────────────────
    // All three CalcIce paths now use the same linear pressure-efficiency formula:
    //   multiplier = 1 + torqueScale × baseEff × (pressureScale − 1)
    // where pressureScale = MaxScale (turbos) or RedlineRPMScale (superchargers).
    // Efficiencies calibrated per-type on Nissan Fairlady Z '03 (CarId=344).
    // The old asymptotic model (MaxMultA×Ms+MaxMultB, calibrated on Supra RZ only)
    // overestimated power for most other engines — e.g. Syclone 4.3L V6 TT gave
    // 410 hp vs in-game 362 hp (+13 %).

    // NA→first FI: addPower = stockHP × (1 + GTS × Eff × (Ms−1)).
    // ST(Ms=1.250→344hp) TT(Ms=1.250→344hp) CSC(Red=1.136→316hp) DSC(Red=1.136→317hp)
    private const double STBaseEff  = 0.935;
    private const double TTBaseEff  = 0.935;
    private const double CSCBaseEff = 0.874;
    private const double DSCBaseEff = 0.904;

    /// <summary>Unified FI power multiplier — linear pressure-efficiency model,
    /// consistent across all three CalcIce paths.</summary>
    private static double FiEfficiencyMultiplier(DbUpgradeForcedInduction? fi, double torqueScale)
    {
        if (fi == null) return 1.0;
        double pressureScale;
        double baseEff;
        switch (fi)
        {
            case DbUpgradeTurboSingle ts: pressureScale = ts.MaxScale;         baseEff = STBaseEff;  break;
            case DbUpgradeTurboTwin   tt: pressureScale = tt.MaxScale;         baseEff = TTBaseEff;  break;
            case DbUpgradeCSC        csc: pressureScale = csc.RedlineRPMScale; baseEff = CSCBaseEff; break;
            case DbUpgradeDSC        dsc: pressureScale = dsc.RedlineRPMScale; baseEff = DSCBaseEff; break;
            default: return 1.0;
        }
        if (pressureScale <= 1.0) return 1.0;
        return 1.0 + torqueScale * baseEff * (pressureScale - 1.0);
    }

    /// <summary>Torque-specific FI multiplier.  CSC (centrifugal supercharger)
    /// produces less low-end torque than peak power (trqRatio = 0.426).</summary>
    private static double FiTorqueMultiplier(DbUpgradeForcedInduction? fi, double torqueScale)
    {
        if (fi == null) return 1.0;
        double pressureScale;
        double baseEff;
        double trqRatio;
        switch (fi)
        {
            case DbUpgradeTurboSingle ts: pressureScale = ts.MaxScale;         baseEff = STBaseEff;  trqRatio = 1.0;   break;
            case DbUpgradeTurboTwin   tt: pressureScale = tt.MaxScale;         baseEff = TTBaseEff;  trqRatio = 1.0;   break;
            case DbUpgradeCSC        csc: pressureScale = csc.RedlineRPMScale; baseEff = CSCBaseEff; trqRatio = 0.426; break;
            case DbUpgradeDSC        dsc: pressureScale = dsc.RedlineRPMScale; baseEff = DSCBaseEff; trqRatio = 1.0;   break;
            default: return 1.0;
        }
        if (pressureScale <= 1.0) return 1.0;
        return 1.0 + torqueScale * baseEff * trqRatio * (pressureScale - 1.0);
    }

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
