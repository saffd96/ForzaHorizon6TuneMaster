using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;
using TuneMaster.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace TuneMaster.Tests.Services;

// Offline accuracy diagnostic (no in-game data): compares PowerCalculator output
// against the DB's own anchors — stock vs SimPeakPower/SimPeakTorque, and a fully
// maxed build vs EngineGraphingMaxPower/Torque (the engine's dyno-graph ceiling).
// Pure reporting: no asserts that can fail the suite. Run with:
//   dotnet test ... --filter FullyQualifiedName~PowerAccuracyDiagnostics --logger "console;verbosity=detailed"
[Collection("FileSystem")]
public class PowerAccuracyDiagnostics
{
    private readonly ITestOutputHelper _out;
    public PowerAccuracyDiagnostics(ITestOutputHelper o) => _out = o;

    private static CarCard MinimalCard(int carDbId, int engineId) => new()
    {
        CarDbId = carDbId,
        CarBodyId = carDbId * 1000,
        EngineDbId = engineId,
        Name = "probe",
        PowertrainType = PowertrainType.ICE,
    };

    private static int StockEngineId(Fh6DatabaseService db, int carDbId)
    {
        var swaps = db.GetEngineSwaps(carDbId);
        var stock = swaps.FirstOrDefault(s => s.IsStock);
        return stock?.EngineID ?? swaps.FirstOrDefault()?.EngineID ?? 0;
    }

    [Fact]
    public async Task Report_StockAndMaxed_VsDbAnchors()
    {
        using var env = new TestingEnvironment();
        await Fh6DatabaseService.Instance.InitializeAsync();
        var db = Fh6DatabaseService.Instance;

        // per-aspiration accumulators
        var byAsp = new Dictionary<int, (int n, double sumAbsStock, int stockIn3, int stockChecked,
                                         double sumMaxedShort, int maxedIn1, int maxedChecked)>();
        var worstStock = new List<(double resid, int car, int asp, double calc, double sim)>();
        var worstMaxed = new List<(double shortHp, int eng, int asp, double maxed, double ceil)>();

        foreach (var dbCar in db.GetAllCars())
        {
            if (dbCar.AspirationTypeId == 8 || dbCar.PowertrainID == 1) continue; // electric
            int eng = StockEngineId(db, dbCar.Id);
            if (eng <= 0) continue;
            var e = db.GetEngine(eng);
            if (e == null) continue;
            int asp = dbCar.AspirationTypeId;

            // ── Stock vs SimPeakPower ──
            double simHp = dbCar.SimPeakPower * 0.1341;
            double stockResid = double.NaN;
            int stockChecked = 0, stockIn3 = 0;
            if (simHp > 1)
            {
                var c = MinimalCard(dbCar.Id, eng);
                try { PowerCalculator.Calculate(c, new SelectedParts()); } catch { c.PowerHP = 0; }
                stockResid = (c.PowerHP - simHp) / simHp * 100.0;
                stockChecked = 1;
                if (Math.Abs(stockResid) <= 3.0) stockIn3 = 1;
                worstStock.Add((stockResid, dbCar.Id, asp, c.PowerHP, simHp));
            }

            // ── Maxed vs EngineGraphingMaxPower ──
            double ceil = e.EngineGraphingMaxPower * 1.341;
            double maxedShort = double.NaN;
            int maxedChecked = 0, maxedIn1 = 0;
            if (ceil > 1)
            {
                var maxed = BuildMaxedParts(null, eng, db);
                var cams = db.GetCamshafts(eng);
                double maxedPower = 0;
                if (cams.Count == 0)
                {
                    var cm = MinimalCard(dbCar.Id, eng);
                    try { PowerCalculator.Calculate(cm, maxed); maxedPower = cm.PowerHP; } catch { }
                }
                else foreach (var cam in cams)
                {
                    maxed.CamshaftPartId = cam.Id;
                    var cm = MinimalCard(dbCar.Id, eng);
                    try { PowerCalculator.Calculate(cm, maxed); } catch { continue; }
                    if (cm.PowerHP > maxedPower) maxedPower = cm.PowerHP;
                }
                maxedShort = (maxedPower - ceil) / ceil * 100.0; // negative = under ceiling
                maxedChecked = 1;
                if (Math.Abs(maxedPower - ceil) <= Math.Max(1.0, ceil * 0.01)) maxedIn1 = 1;
                worstMaxed.Add((maxedPower - ceil, eng, asp, maxedPower, ceil));
            }

            var a = byAsp.TryGetValue(asp, out var v) ? v : default;
            a.n++;
            if (stockChecked == 1) { a.sumAbsStock += Math.Abs(stockResid); a.stockIn3 += stockIn3; a.stockChecked++; }
            if (maxedChecked == 1) { a.sumMaxedShort += maxedShort; a.maxedIn1 += maxedIn1; a.maxedChecked++; }
            byAsp[asp] = a;
        }

        string AspName(int id) => id switch
        {
            0 => "NA", 1 => "SingleTurbo", 2 => "TwinTurbo",
            3 => "PD-SC", 4 => "Centri-SC", 5 => "Electric", _ => $"asp{id}"
        };

        _out.WriteLine("=== Stock vs SimPeakPower & Maxed vs EngineGraphingMaxPower (per aspiration) ===");
        _out.WriteLine($"{"Asp",-12} {"cars",5} {"stock|resid|%",13} {"stock±3%",9} {"maxedShort%",12} {"maxed±1%",9}");
        foreach (var kv in byAsp.OrderBy(k => k.Key))
        {
            var a = kv.Value;
            double mAbsStock = a.stockChecked > 0 ? a.sumAbsStock / a.stockChecked : 0;
            double mMaxedShort = a.maxedChecked > 0 ? a.sumMaxedShort / a.maxedChecked : 0;
            _out.WriteLine($"{AspName(kv.Key),-12} {a.n,5} {mAbsStock,12:F1}% {a.stockIn3,4}/{a.stockChecked,-4} {mMaxedShort,11:F1}% {a.maxedIn1,4}/{a.maxedChecked,-4}");
        }

        _out.WriteLine("\n--- Worst 15 stock residuals (|%|) ---");
        foreach (var w in worstStock.OrderByDescending(x => Math.Abs(x.resid)).Take(15))
            _out.WriteLine($"car {w.car,5} {AspName(w.asp),-12} calc={w.calc,7:F0} sim={w.sim,7:F0}  resid={w.resid,7:F1}%");

        _out.WriteLine("\n--- Worst 15 maxed shortfalls (hp under ceiling) ---");
        foreach (var w in worstMaxed.OrderBy(x => x.shortHp).Take(15))
            _out.WriteLine($"eng {w.eng,5} {AspName(w.asp),-12} maxed={w.maxed,7:F0} ceil={w.ceil,7:F0}  {w.shortHp,7:F0}hp");

        // ── Regression guards ───────────────────────────────────────────────────
        // Stock must stay anchored to SimPeakPower for every aspiration class.
        foreach (var kv in byAsp.Where(k => k.Value.stockChecked > 0))
        {
            double meanAbs = kv.Value.sumAbsStock / kv.Value.stockChecked;
            Assert.True(meanAbs < 3.0,
                $"{AspName(kv.Key)} stock mean |resid| {meanAbs:F1}% — stock anchoring drifted from SimPeakPower");
        }
        // Single-turbo maxed builds must not systematically under-shoot the dyno ceiling
        // (the bug this test was written for: was -25.6 %, fixed to ~+0.3 %).
        if (byAsp.TryGetValue(1, out var st) && st.maxedChecked > 0)
        {
            double meanShort = st.sumMaxedShort / st.maxedChecked;
            Assert.True(meanShort > -10.0,
                $"SingleTurbo maxed mean shortfall {meanShort:F1}% vs EngineGraphingMaxPower — under-production regression");
        }
    }

    [Fact]
    public async Task Diagnostic_BoltOnPartTorqueScales()
    {
        using var env = new TestingEnvironment();
        await Fh6DatabaseService.Instance.InitializeAsync();
        var db = Fh6DatabaseService.Instance;

        int oilWithScale = 0, exhaustTurboWithScale = 0, manifoldTurboWithScale = 0, icWithScale = 0;

        foreach (var dbCar in db.GetAllCars())
        {
            if (dbCar.AspirationTypeId == 8 || dbCar.PowertrainID == 1 || dbCar.SimPeakPower <= 0) continue;
            var swaps = db.GetEngineSwaps(dbCar.Id);
            var stockSwap = swaps.FirstOrDefault(s => s.IsStock);
            if (stockSwap == null) continue;
            int eid = stockSwap.EngineID;

            bool hasTurbo = db.GetTurbosSingle(eid).Any(t => t.IsStock)
                         || db.GetTurbosTwin(eid).Any(t => t.IsStock);

            var oilParts = db.GetOilCooling(eid);
            if (oilParts.Any(p => !p.IsStock && (p.TorqueScale ?? 1.0) > 1.0)) oilWithScale++;

            if (hasTurbo)
            {
                var exhaustList = db.GetExhaust(eid);
                if (exhaustList.Any(e => !e.IsStock && (e.TorqueScale ?? 1.0) > 1.0)) exhaustTurboWithScale++;

                var manifolds = db.GetManifolds(eid);
                if (manifolds.Any(m => !m.IsStock && (m.TorqueScale ?? 1.0) > 1.0)) manifoldTurboWithScale++;

                var ics = db.GetIntercoolers(eid);
                if (ics.Any(i => !i.IsStock && i.MaxScaleScale > 1.0)) icWithScale++;
            }
        }

        _out.WriteLine($"Cars with OilCooling TorqueScale>1: {oilWithScale}");
        _out.WriteLine($"Turbo cars with Exhaust TorqueScale>1: {exhaustTurboWithScale}");
        _out.WriteLine($"Turbo cars with Manifold TorqueScale>1: {manifoldTurboWithScale}");
        _out.WriteLine($"Turbo cars with Intercooler MaxScaleScale>1: {icWithScale}");

        // not a hard assertion — just diagnostic output
        Assert.True(true);
    }

    [Fact]
    public async Task Report_BigTurbo_ConstantCalibration()
    {
        using var env = new TestingEnvironment();
        await Fh6DatabaseService.Instance.InitializeAsync();
        var db = Fh6DatabaseService.Instance;

        _out.WriteLine("=== Big Single-Turbo builds (MaxScale > 3.0) vs EngineGraphingMaxPower ===");
        _out.WriteLine($"{"Car",5} {"Eng",5} {"MaxScale",9} {"calcHP",7} {"ceilHP",7} {"ratio%",8}");

        foreach (var dbCar in db.GetAllCars().OrderBy(c => c.Id))
        {
            if (dbCar.AspirationTypeId == 8 || dbCar.PowertrainID == 1) continue;
            var swaps = db.GetEngineSwaps(dbCar.Id);
            var stockSwap = swaps.FirstOrDefault(s => s.IsStock);
            if (stockSwap == null) continue;
            int eid = stockSwap.EngineID;

            var bigTurbos = db.GetTurbosSingle(eid).Where(t => !t.IsStock && t.MaxScale > 3.0).ToList();
            if (bigTurbos.Count == 0) continue;

            var engine = db.GetEngine(eid);
            if (engine == null || engine.EngineGraphingMaxPower <= 0) continue;

            double ceilHP = engine.EngineGraphingMaxPower * 1.341; // kW → HP
            var bestTurbo = bigTurbos.OrderByDescending(t => t.MaxScale).First();

            var c = new CarCard
            {
                CarDbId = dbCar.Id, CarBodyId = dbCar.Id * 1000,
                EngineDbId = eid, PowertrainType = PowertrainType.ICE,
            };
            var p = new SelectedParts { ForcedInductionPartId = bestTurbo.Id };
            try { PowerCalculator.Calculate(c, p); } catch { continue; }

            double ratio = (c.PowerHP - ceilHP) / ceilHP * 100;
            _out.WriteLine($"{dbCar.Id,5} {eid,5} {bestTurbo.MaxScale,9:F2} {c.PowerHP,7:F0} {ceilHP,7:F0} {ratio,7:F1}%");
        }

        _out.WriteLine("\nДля перекалибровки: сравнить с реальным HP из игры для машин выше.");
        _out.WriteLine("Цель: ratio% близко к 0 (в пределах ±5%).");
    }

    [Fact]
    public async Task Report_RedlineScale_ByEngineType()
    {
        using var env = new TestingEnvironment();
        await Fh6DatabaseService.Instance.InitializeAsync();
        var db = Fh6DatabaseService.Instance;

        const double RadSToRPM = 60.0 / (2.0 * Math.PI);

        _out.WriteLine("=== Cam RedlineRPM vs SimRedlineAngVel×60/2π — расхождения > 2% ===");
        _out.WriteLine($"{"Car",5} {"Eng",5} {"CamRPM",8} {"SimRPM",8} {"ratio",7} {"Notes"}");

        int checked_ = 0, flagged = 0;
        foreach (var dbCar in db.GetAllCars())
        {
            if (dbCar.AspirationTypeId == 8 || dbCar.SimRedlineAngVel <= 0) continue;
            var swaps = db.GetEngineSwaps(dbCar.Id);
            var stockSwap = swaps.FirstOrDefault(s => s.IsStock);
            if (stockSwap == null) continue;
            int eid = stockSwap.EngineID;

            var engine = db.GetEngine(eid);
            if (engine == null) continue;

            var cams = db.GetCamshafts(eid);
            var stockCam = cams.FirstOrDefault(c => c.IsStock) ?? cams.FirstOrDefault();
            if (stockCam == null || stockCam.RedlineRPM <= 0) continue;

            double simRPM = dbCar.SimRedlineAngVel * RadSToRPM;
            double ratio = stockCam.RedlineRPM / simRPM;
            checked_++;

            if (Math.Abs(ratio - 1.0) > 0.02)
            {
                flagged++;
                _out.WriteLine($"{dbCar.Id,5} {eid,5} {stockCam.RedlineRPM,8:F0} {simRPM,8:F0} {ratio,7:F3}");
            }
        }

        _out.WriteLine($"\nПроверено: {checked_}  Флагов (ratio отклонение >2%): {flagged}");
        _out.WriteLine("Ожидаемое ratio ≈ 1.000. Отклонения указывают на нестандартный GameRedlineScale.");
    }

    [Fact]
    public async Task Diagnose_BmwM5_1995_EngineSwaps()
    {
        using var env = new TestingEnvironment();
        await Fh6DatabaseService.Instance.InitializeAsync();
        var db = Fh6DatabaseService.Instance;

        // MediaName follows pattern MAKE_MODEL_YEAR, e.g. BMW_M5_95
        var m5 = db.GetAllCars().FirstOrDefault(c =>
            (c.MediaName ?? "").Contains("M5", StringComparison.OrdinalIgnoreCase) && c.Year == 1995);
        if (m5 == null) { _out.WriteLine("BMW M5 1995 not found in DB"); Assert.True(true); return; }

        _out.WriteLine($"Car: {m5.Id}  CurbWeight={m5.CurbWeight * 100:F0}kg  SimPeakPower={m5.SimPeakPower * 0.1341:F0}hp");

        // Section 1: FI bolt-on to stock engine (no engine swap)
        var stockSwap = db.GetEngineSwaps(m5.Id).FirstOrDefault(s => s.IsStock);
        int stockEng = StockEngineId(db, m5.Id);
        _out.WriteLine($"\n--- Stock engine {stockEng} bolt-on FI options ---");
        PowerCalculator.VerboseLogging = true;
        var captureOut = Console.Out;
        var sw = new System.IO.StringWriter();
        Console.SetOut(sw);
        // Prints all FI parts for engine, including non-stock (what user picks via FI type dropdown)
        _out.WriteLine($"  TurboSingle parts: {string.Join(", ", db.GetTurbosSingle(stockEng).Select(p => $"Id={p.Id} Lv={p.Level} IsStock={p.IsStock} MassDiff={p.MassDiff:F0} MaxScale={((DbUpgradeTurboSingle)p).MaxScale:F2}"))}");
        _out.WriteLine($"  TurboTwin parts:   {string.Join(", ", db.GetTurbosTwin(stockEng).Select(p => $"Id={p.Id} Lv={p.Level} IsStock={p.IsStock} MassDiff={p.MassDiff:F0} MaxScale={((DbUpgradeTurboTwin)p).MaxScale:F2}"))}");
        _out.WriteLine($"  DSC parts:         {string.Join(", ", db.GetDSC(stockEng).Select(p => $"Id={p.Id} Lv={p.Level} IsStock={p.IsStock} MassDiff={p.MassDiff:F0} RedlineScale={((DbUpgradeDSC)p).RedlineRPMScale:F2}"))}");
        _out.WriteLine($"  CSC parts:         {string.Join(", ", db.GetCSC(stockEng).Select(p => $"Id={p.Id} Lv={p.Level} IsStock={p.IsStock} MassDiff={p.MassDiff:F0}"))}");
        var manifolds = db.GetManifolds(stockEng);
        _out.WriteLine($"  Manifolds:         {string.Join(", ", manifolds.Select(m => $"Id={m.Id} Lv={m.Level} IsStock={m.IsStock} MassDiff={m.MassDiff:F0}"))}");
        var intercoolers = db.GetIntercoolers(stockEng);
        _out.WriteLine($"  Intercoolers:      {string.Join(", ", intercoolers.Select(ic => $"Id={ic.Id} Lv={ic.Level} IsStock={ic.IsStock} MassDiff={ic.MassDiff:F0} MaxScaleScale={ic.MaxScaleScale:F3}"))}");
        int? stockManifoldId = manifolds.FirstOrDefault(m => m.IsStock)?.Id;
        int? stockIntercoolerid = intercoolers.FirstOrDefault(ic => ic.IsStock)?.Id;

        // Use first (lowest level) part per kind — same as SelectedForcedInductionType setter
        // Also set ManifoldPartId and IntercoolerPartId as the app does after FI type change.
        foreach (var fiParts in new[] {
            ("ST",  db.GetTurbosSingle(stockEng).OrderBy(p=>p.Level).FirstOrDefault()?.Id),
            ("TT",  db.GetTurbosTwin(stockEng).OrderBy(p=>p.Level).FirstOrDefault()?.Id),
            ("DSC", db.GetDSC(stockEng).OrderBy(p=>p.Level).FirstOrDefault()?.Id),
            ("CSC", db.GetCSC(stockEng).OrderBy(p=>p.Level).FirstOrDefault()?.Id),
        }.Select(x => (x.Item1, x.Item2)))
        {
            if (fiParts.Item2 == null) continue;
            var p = new SelectedParts();
            if (stockSwap != null) p.EngineSwapPartId = stockSwap.Id;
            p.ManifoldPartId = stockManifoldId;
            p.ForcedInductionPartId = fiParts.Item2;
            p.IntercoolerPartId = stockIntercoolerid; // app auto-sets stock IC when FI installed
            var fi = db.GetForcedInductionById(fiParts.Item2.Value);
            var icPart = stockIntercoolerid.HasValue ? db.GetIntercoolerById(stockIntercoolerid.Value) : null;
            double mass = m5.CurbWeight * 100
                + (stockSwap?.MassDiff ?? 0)
                + (manifolds.FirstOrDefault(m => m.Id == stockManifoldId)?.MassDiff ?? 0)
                + (fi?.MassDiff ?? 0)
                + (icPart?.MassDiff ?? 0);
            sw.GetStringBuilder().Clear();
            var card = new CarCard { CarDbId = m5.Id, EngineDbId = stockEng, PowertrainType = PowertrainType.ICE };
            try { PowerCalculator.Calculate(card, p); } catch { continue; }
            var vb = sw.ToString();
            _out.WriteLine($"  {fiParts.Item1}  FI_id={fiParts.Item2}  IsStock={fi?.IsStock}  fiMass={fi?.MassDiff:F0}  icMass={icPart?.MassDiff:F0}  totMass={mass:F0}  →  CalcHP={card.PowerHP:F0}  CalcNm={card.TorqueNm:F0}");
            foreach (var line in vb.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                _out.WriteLine("    " + line.TrimEnd());
        }
        Console.SetOut(captureOut);
        PowerCalculator.VerboseLogging = false;

        _out.WriteLine($"\n--- Engine swaps ---");
        foreach (var swap in db.GetEngineSwaps(m5.Id))
        {
            var eng = db.GetEngine(swap.EngineID);
            if (eng == null) continue;

            var donor = db.GetCarByMediaName(eng.MediaName);
            double donorHp = donor?.SimPeakPower > 0 ? donor.SimPeakPower * 0.1341 : -1;

            var parts = new SelectedParts();
            if (!swap.IsStock) parts.EngineSwapPartId = swap.Id;
            parts.ForcedInductionPartId =
                db.GetTurbosSingle(swap.EngineID).FirstOrDefault(p => p.IsStock)?.Id
                ?? db.GetTurbosTwin(swap.EngineID).FirstOrDefault(p => p.IsStock)?.Id
                ?? db.GetCSC(swap.EngineID).FirstOrDefault(p => p.IsStock)?.Id
                ?? db.GetDSC(swap.EngineID).FirstOrDefault(p => p.IsStock)?.Id;

            string fiKind = db.GetTurbosSingle(swap.EngineID).Any(p => p.IsStock) ? "ST"
                          : db.GetTurbosTwin(swap.EngineID).Any(p => p.IsStock) ? "TT"
                          : db.GetCSC(swap.EngineID).Any(p => p.IsStock) ? "CSC"
                          : db.GetDSC(swap.EngineID).Any(p => p.IsStock) ? "DSC" : "NA";

            var card = new CarCard { CarDbId = m5.Id, EngineDbId = swap.EngineID, PowertrainType = PowertrainType.ICE };
            try { PowerCalculator.Calculate(card, parts); }
            catch (Exception ex) { _out.WriteLine($"  Swap {swap.Id} ERROR: {ex.Message}"); continue; }
            _out.WriteLine($"  Lv={swap.Level} {fiKind} DonorHP={donorHp:F0} SwapMass={swap.MassDiff:F0}kg  →  CalcHP={card.PowerHP:F0} CalcNm={card.TorqueNm:F0}  [{eng.MediaName}]");
        }

        Assert.True(true);
    }

    private static SelectedParts BuildMaxedParts(int? swapId, int engineId, Fh6DatabaseService db)
    {
        int? BestTs<T>(List<T> list) where T : DbUpgradePart =>
            list.Where(x => !x.IsStock && (x.TorqueScale ?? 1.0) > 1.0)
                .OrderByDescending(x => x.TorqueScale ?? 1.0).FirstOrDefault()?.Id;

        var p = new SelectedParts
        {
            EngineSwapPartId = swapId,
            DisplacementPartId = BestTs(db.GetDisplacement(engineId)),
            ValvesPartId = BestTs(db.GetValves(engineId)),
            PistonsPartId = BestTs(db.GetPistons(engineId)),
            FuelSystemPartId = BestTs(db.GetFuelSystems(engineId)),
            IgnitionPartId = BestTs(db.GetIgnition(engineId)),
            ExhaustPartId = BestTs(db.GetExhaust(engineId)),
            IntakePartId = BestTs(db.GetIntake(engineId)),
            ManifoldPartId = BestTs(db.GetManifolds(engineId)),
            OilCoolingPartId = BestTs(db.GetOilCooling(engineId)),
            RestrictorPartId = BestTs(db.GetRestrictors(engineId)),
        };

        DbUpgradePart? bestFi = null; double bestScale = 1.0;
        foreach (var t in db.GetTurbosSingle(engineId)) if (!t.IsStock && t.MaxScale > bestScale) { bestScale = t.MaxScale; bestFi = t; }
        foreach (var t in db.GetTurbosTwin(engineId)) if (!t.IsStock && t.MaxScale > bestScale) { bestScale = t.MaxScale; bestFi = t; }
        foreach (var cc in db.GetCSC(engineId)) if (!cc.IsStock && cc.RedlineRPMScale > bestScale) { bestScale = cc.RedlineRPMScale; bestFi = cc; }
        foreach (var d in db.GetDSC(engineId)) if (!d.IsStock && d.RedlineRPMScale > bestScale) { bestScale = d.RedlineRPMScale; bestFi = d; }
        if (bestFi != null)
        {
            p.ForcedInductionPartId = bestFi.Id;
            p.IntercoolerPartId = db.GetIntercoolers(engineId).Where(x => !x.IsStock)
                .OrderByDescending(x => x.MaxScaleScale).FirstOrDefault()?.Id;
        }
        return p;
    }
}
