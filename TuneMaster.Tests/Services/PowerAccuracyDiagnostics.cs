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
