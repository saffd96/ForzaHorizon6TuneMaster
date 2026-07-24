using System.Linq;
using System.Threading.Tasks;
using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;
using Xunit;

namespace TuneMaster.Tests.Services;

[Collection("FileSystem")]
public class PowerCalculatorBoltOnTests
{
    private static CarCard MakeCard(DbCar dbCar, int engineId) => new()
    {
        CarDbId = dbCar.Id,
        CarBodyId = dbCar.Id * 1000,
        EngineDbId = engineId,
        PowertrainType = PowertrainType.ICE,
        Name = dbCar.DisplayName,
    };

    // ── Task 1: КРИТ #3 — OilCooling не влияет на пиковую мощность ──────────
    // Поиск по всем машинам (не только первой), иначе early return.

    [Fact]
    public async Task OilCooling_DoesNotChangePeakPower()
    {
        using var env = new TestingEnvironment();
        await Fh6DatabaseService.Instance.InitializeAsync();
        var db = Fh6DatabaseService.Instance;

        DbCar? testCar = null;
        int testEngineId = 0, stockOilId = 0, raceOilId = 0;

        foreach (var dbCar in db.GetAllCars())
        {
            if (dbCar.AspirationTypeId == 8 || dbCar.PowertrainID == 1 || dbCar.SimPeakPower <= 0) continue;
            var swaps = db.GetEngineSwaps(dbCar.Id);
            var stockSwap = swaps.FirstOrDefault(s => s.IsStock);
            if (stockSwap == null) continue;
            int eid = stockSwap.EngineID;

            var oilParts = db.GetOilCooling(eid);
            var stockOil = oilParts.FirstOrDefault(p => p.IsStock);
            var raceOil = oilParts.Where(p => !p.IsStock && (p.TorqueScale ?? 1.0) > 1.0)
                                  .OrderByDescending(p => p.TorqueScale ?? 1.0).FirstOrDefault();
            if (stockOil == null || raceOil == null) continue;

            testCar = dbCar; testEngineId = eid; stockOilId = stockOil.Id; raceOilId = raceOil.Id;
            break;
        }

        if (testCar == null) return; // нет подходящей машины с OilCooling TorqueScale

        var card1 = MakeCard(testCar, testEngineId);
        PowerCalculator.Calculate(card1, new SelectedParts { OilCoolingPartId = stockOilId });

        var card2 = MakeCard(testCar, testEngineId);
        PowerCalculator.Calculate(card2, new SelectedParts { OilCoolingPartId = raceOilId });

        Assert.Equal(card1.PowerHP, card2.PowerHP, precision: 0);
    }

    // ── Task 2: КРИТ #1 — bolt-on части × FI ────────────────────────────────

    [Fact]
    public async Task TurboEngine_FiCurve_ReflectsBoltOnParts()
    {
        using var env = new TestingEnvironment();
        var db = Fh6DatabaseService.Instance;
        await db.InitializeAsync();

        DbCar? testCar = null;
        int testEngineId = 0, stockFiId = 0, stockExId = 0, raceExId = 0;

        foreach (var dbCar in db.GetAllCars())
        {
            if (dbCar.AspirationTypeId == 8 || dbCar.SimPeakPower <= 0) continue;
            var swaps = db.GetEngineSwaps(dbCar.Id);
            var stockSwap = swaps.FirstOrDefault(s => s.IsStock);
            if (stockSwap == null) continue;
            int eid = stockSwap.EngineID;

            var stockFi = (DbUpgradePart?)db.GetTurbosSingle(eid).FirstOrDefault(t => t.IsStock)
                       ?? db.GetTurbosTwin(eid).FirstOrDefault(t => t.IsStock);
            if (stockFi == null) continue;

            var exhaustList = db.GetExhaust(eid);
            var stockEx = exhaustList.FirstOrDefault(e => e.IsStock);
            // Расширяем порог: любой exhaust с TorqueScale > 1.0
            var raceEx = exhaustList.Where(e => !e.IsStock && (e.TorqueScale ?? 1.0) > 1.0)
                                    .OrderByDescending(e => e.TorqueScale ?? 1.0).FirstOrDefault();
            if (stockEx == null || raceEx == null) continue;

            testCar = dbCar; testEngineId = eid;
            stockFiId = stockFi.Id; stockExId = stockEx.Id; raceExId = raceEx.Id;
            break;
        }

        if (testCar == null) return;

        var cardStock = MakeCard(testCar, testEngineId);
        PowerCalculator.Calculate(cardStock, new SelectedParts { ForcedInductionPartId = stockFiId, ExhaustPartId = stockExId });

        var cardParts = MakeCard(testCar, testEngineId);
        PowerCalculator.Calculate(cardParts, new SelectedParts { ForcedInductionPartId = stockFiId, ExhaustPartId = raceExId });

        double peakStock = cardStock.CachedTorqueCurveNm?.Max() ?? 0;
        double peakParts = cardParts.CachedTorqueCurveNm?.Max() ?? 0;

        Assert.True(peakParts > peakStock,
            $"Curve peak with race exhaust ({peakParts:F0} Nm) should exceed stock ({peakStock:F0} Nm). " +
            "fiCurve is built from naCurveNoParts without partScale.");
    }

    [Fact]
    public async Task BoostedEngine_FiUpgrade_BoltOnsAreMultiplicative()
    {
        using var env = new TestingEnvironment();
        var db = Fh6DatabaseService.Instance;
        await db.InitializeAsync();

        DbCar? testCar = null;
        int testEngineId = 0, stockFiId = 0, upgradeFiId = 0, stockExId = 0, raceExId = 0;

        foreach (var dbCar in db.GetAllCars())
        {
            if (dbCar.AspirationTypeId == 8 || dbCar.SimPeakPower <= 0) continue;
            var swaps = db.GetEngineSwaps(dbCar.Id);
            var stockSwap = swaps.FirstOrDefault(s => s.IsStock);
            if (stockSwap == null) continue;
            int eid = stockSwap.EngineID;

            var stockFi = (DbUpgradePart?)db.GetTurbosSingle(eid).FirstOrDefault(t => t.IsStock)
                       ?? db.GetTurbosTwin(eid).FirstOrDefault(t => t.IsStock);
            var upgradeFi = (DbUpgradePart?)db.GetTurbosSingle(eid).Where(t => !t.IsStock)
                              .OrderByDescending(t => t.MaxScale).FirstOrDefault()
                         ?? db.GetTurbosTwin(eid).Where(t => !t.IsStock)
                              .OrderByDescending(t => t.MaxScale).FirstOrDefault();
            if (stockFi == null || upgradeFi == null) continue;

            var exhaustList = db.GetExhaust(eid);
            var stockEx = exhaustList.FirstOrDefault(e => e.IsStock);
            var raceEx = exhaustList.Where(e => !e.IsStock && (e.TorqueScale ?? 1.0) > 1.0)
                                    .OrderByDescending(e => e.TorqueScale ?? 1.0).FirstOrDefault();
            if (stockEx == null || raceEx == null) continue;

            testCar = dbCar; testEngineId = eid;
            stockFiId = stockFi.Id; upgradeFiId = upgradeFi.Id;
            stockExId = stockEx.Id; raceExId = raceEx.Id;
            break;
        }

        if (testCar == null) return;

        var cardBase = MakeCard(testCar, testEngineId);
        PowerCalculator.Calculate(cardBase, new SelectedParts { ForcedInductionPartId = stockFiId, ExhaustPartId = stockExId });
        double hpBase = cardBase.PowerHP;

        var cardParts = MakeCard(testCar, testEngineId);
        PowerCalculator.Calculate(cardParts, new SelectedParts { ForcedInductionPartId = stockFiId, ExhaustPartId = raceExId });
        double hpParts = cardParts.PowerHP;

        var cardFi = MakeCard(testCar, testEngineId);
        PowerCalculator.Calculate(cardFi, new SelectedParts { ForcedInductionPartId = upgradeFiId, ExhaustPartId = stockExId });
        double hpFi = cardFi.PowerHP;

        var cardBoth = MakeCard(testCar, testEngineId);
        PowerCalculator.Calculate(cardBoth, new SelectedParts { ForcedInductionPartId = upgradeFiId, ExhaustPartId = raceExId });
        double hpBoth = cardBoth.PowerHP;

        // После исправления: турбо должен умножать bolt-on прирост.
        // Минимальное требование: hpBoth >= аддитивной оценки (−1 допуск на округление).
        double additiveEstimate = hpFi + (hpParts - hpBase);
        Assert.True(hpBoth >= additiveEstimate - 1.0,
            $"HP both ({hpBoth:F0}) >= additive estimate ({additiveEstimate:F0}). " +
            $"base={hpBase:F0} parts={hpParts:F0} fi={hpFi:F0}");
    }

    // ── Task 3: КРИТ #4 — Интеркулер работает со стоковым турбо ─────────────

    [Fact]
    public async Task Intercooler_AffectsPower_WithStockTurbo()
    {
        using var env = new TestingEnvironment();
        var db = Fh6DatabaseService.Instance;
        await db.InitializeAsync();

        DbCar? testCar = null;
        int testEngineId = 0, stockFiId = 0, stockIcId = 0, raceIcId = 0;

        foreach (var dbCar in db.GetAllCars())
        {
            if (dbCar.AspirationTypeId == 8 || dbCar.SimPeakPower <= 0) continue;
            var swaps = db.GetEngineSwaps(dbCar.Id);
            var stockSwap = swaps.FirstOrDefault(s => s.IsStock);
            if (stockSwap == null) continue;
            int eid = stockSwap.EngineID;

            var stockFi = (DbUpgradePart?)db.GetTurbosSingle(eid).FirstOrDefault(t => t.IsStock)
                       ?? db.GetTurbosTwin(eid).FirstOrDefault(t => t.IsStock);
            if (stockFi == null) continue;

            var intercoolers = db.GetIntercoolers(eid);
            var stockIc = intercoolers.FirstOrDefault(i => i.IsStock);
            var raceIc = intercoolers.Where(i => !i.IsStock && i.MaxScaleScale > 1.0)
                                     .OrderByDescending(i => i.MaxScaleScale).FirstOrDefault();
            if (stockIc == null || raceIc == null) continue;

            testCar = dbCar; testEngineId = eid;
            stockFiId = stockFi.Id; stockIcId = stockIc.Id; raceIcId = raceIc.Id;
            break;
        }

        if (testCar == null) return;

        var cardStock = MakeCard(testCar, testEngineId);
        PowerCalculator.Calculate(cardStock, new SelectedParts
            { ForcedInductionPartId = stockFiId, IntercoolerPartId = stockIcId });

        var cardRace = MakeCard(testCar, testEngineId);
        PowerCalculator.Calculate(cardRace, new SelectedParts
            { ForcedInductionPartId = stockFiId, IntercoolerPartId = raceIcId });

        Assert.True(cardRace.PowerHP > cardStock.PowerHP,
            $"Race intercooler ({cardRace.PowerHP:F0} HP) should give more power than stock ({cardStock.PowerHP:F0} HP) with stock turbo.");
    }

    // ── СРЕД #5: Closed-throttle curve ──────────────────────────────────────

    [Fact]
    public async Task ICE_ClosedThrottleCurve_IsPopulatedAndSmallerThanFullThrottle()
    {
        using var env = new TestingEnvironment();
        var db = Fh6DatabaseService.Instance;
        await db.InitializeAsync();

        DbCar? testCar = null;
        int testEngineId = 0;

        foreach (var dbCar in db.GetAllCars())
        {
            if (dbCar.AspirationTypeId == 8 || dbCar.PowertrainID == 1 || dbCar.SimPeakPower <= 0) continue;
            var swaps = db.GetEngineSwaps(dbCar.Id);
            var stockSwap = swaps.FirstOrDefault(s => s.IsStock);
            if (stockSwap == null) continue;
            var cams = db.GetCamshafts(stockSwap.EngineID);
            if (cams.Count == 0) continue;
            var stockCam = cams.FirstOrDefault(c => c.IsStock) ?? cams.First();
            var tc = db.GetTorqueCurve(stockCam.TorqueCurveFullThrottleID);
            if (tc == null || tc.TorqueScale <= 0.001 || tc.ZeroThrottleTorqueScale <= 0) continue;

            testCar = dbCar; testEngineId = stockSwap.EngineID;
            break;
        }

        if (testCar == null) return;

        var card = MakeCard(testCar, testEngineId);
        PowerCalculator.Calculate(card, new SelectedParts());

        Assert.NotNull(card.CachedClosedThrottleCurveNm);
        Assert.True(card.CachedClosedThrottleCurveNm!.Length > 0);
        double maxFull = card.CachedTorqueCurveNm?.Max() ?? 0;
        double maxClosed = card.CachedClosedThrottleCurveNm.Max();
        Assert.True(maxClosed < maxFull,
            $"Closed-throttle peak ({maxClosed:F0} Nm) should be less than full-throttle ({maxFull:F0} Nm).");
    }

    // ── Баг-репорт 2026-07: интеркулер не влияет на мощность при первой
    // установке наддува на NA-двигатель (isNaToFi-ветка addPower игнорировала
    // intercoolerMaxScale) ────────────────────────────────────────────────────

    [Fact]
    public async Task Intercooler_AffectsPower_WhenFirstFiInstalledOnNaEngine()
    {
        using var env = new TestingEnvironment();
        var db = Fh6DatabaseService.Instance;
        await db.InitializeAsync();

        DbCar? testCar = null;
        int testEngineId = 0, turboPart = 0, lowIcId = 0, highIcId = 0;

        foreach (var dbCar in db.GetAllCars())
        {
            if (dbCar.AspirationTypeId == 8 || dbCar.SimPeakPower <= 0) continue;
            var swaps = db.GetEngineSwaps(dbCar.Id);
            var stockSwap = swaps.FirstOrDefault(s => s.IsStock);
            if (stockSwap == null) continue;
            int eid = stockSwap.EngineID;

            // Только NA двигатели (нет стокового турбо/CSC/DSC) — воспроизводит
            // isNaToFi-ветку (stockFi == null).
            bool hasStockFi = db.GetTurbosSingle(eid).Any(t => t.IsStock)
                            || db.GetTurbosTwin(eid).Any(t => t.IsStock)
                            || db.GetCSC(eid).Any(t => t.IsStock)
                            || db.GetDSC(eid).Any(t => t.IsStock);
            if (hasStockFi) continue;

            var turboUpgrade = (DbUpgradePart?)db.GetTurbosSingle(eid).FirstOrDefault(t => !t.IsStock)
                            ?? db.GetTurbosTwin(eid).FirstOrDefault(t => !t.IsStock);
            if (turboUpgrade == null) continue;

            var ics = db.GetIntercoolers(eid);
            var lowIc = ics.OrderBy(i => i.MaxScaleScale).FirstOrDefault();
            var highIc = ics.OrderByDescending(i => i.MaxScaleScale).FirstOrDefault();
            if (lowIc == null || highIc == null || highIc.MaxScaleScale <= lowIc.MaxScaleScale + 0.001) continue;

            testCar = dbCar; testEngineId = eid;
            turboPart = turboUpgrade.Id; lowIcId = lowIc.Id; highIcId = highIc.Id;
            break;
        }

        if (testCar == null) return;

        var cardLow = MakeCard(testCar, testEngineId);
        PowerCalculator.Calculate(cardLow, new SelectedParts
            { ForcedInductionPartId = turboPart, IntercoolerPartId = lowIcId });

        var cardHigh = MakeCard(testCar, testEngineId);
        PowerCalculator.Calculate(cardHigh, new SelectedParts
            { ForcedInductionPartId = turboPart, IntercoolerPartId = highIcId });

        Assert.True(cardHigh.PowerHP > cardLow.PowerHP,
            $"Better intercooler ({cardHigh.PowerHP:F0} HP) should give more power than a weaker one ({cardLow.PowerHP:F0} HP) " +
            "when forced induction is installed for the first time on an NA engine (isNaToFi path).");
    }

    // ── Баг-репорт 2026-07: апгрейд распредвала не влияет на мощность при
    // первой установке наддува на NA-двигатель (anchorRatio считался от кривой
    // с ТЕКУЩИМ распредвалом на обеих сторонах вычитания — прирост от самого
    // распредвала сокращался и никогда не попадал в addPower) ────────────────

    [Fact]
    public async Task Camshaft_AffectsPower_WhenFirstFiInstalledOnNaEngine()
    {
        using var env = new TestingEnvironment();
        var db = Fh6DatabaseService.Instance;
        await db.InitializeAsync();

        DbCar? testCar = null;
        int testEngineId = 0, turboPart = 0, stockCamId = 0, raceCamId = 0;

        foreach (var dbCar in db.GetAllCars())
        {
            if (dbCar.AspirationTypeId == 8 || dbCar.SimPeakPower <= 0) continue;
            var swaps = db.GetEngineSwaps(dbCar.Id);
            var stockSwap = swaps.FirstOrDefault(s => s.IsStock);
            if (stockSwap == null) continue;
            int eid = stockSwap.EngineID;

            // Только NA двигатели (нет стокового турбо/CSC/DSC) — воспроизводит
            // isNaToFi-ветку (stockFi == null).
            bool hasStockFi = db.GetTurbosSingle(eid).Any(t => t.IsStock)
                            || db.GetTurbosTwin(eid).Any(t => t.IsStock)
                            || db.GetCSC(eid).Any(t => t.IsStock)
                            || db.GetDSC(eid).Any(t => t.IsStock);
            if (hasStockFi) continue;

            var turboUpgrade = (DbUpgradePart?)db.GetTurbosSingle(eid).FirstOrDefault(t => !t.IsStock)
                            ?? db.GetTurbosTwin(eid).FirstOrDefault(t => !t.IsStock);
            if (turboUpgrade == null) continue;

            var cams = db.GetCamshafts(eid);
            var stockCam = cams.FirstOrDefault(c => c.IsStock);
            var raceCam = cams.Where(c => !c.IsStock)
                              .OrderByDescending(c => c.RedlineRPM).FirstOrDefault();
            if (stockCam == null || raceCam == null || raceCam.RedlineRPM <= stockCam.RedlineRPM) continue;

            testCar = dbCar; testEngineId = eid;
            turboPart = turboUpgrade.Id; stockCamId = stockCam.Id; raceCamId = raceCam.Id;
            break;
        }

        if (testCar == null) return;

        var cardStock = MakeCard(testCar, testEngineId);
        PowerCalculator.Calculate(cardStock, new SelectedParts
            { ForcedInductionPartId = turboPart, CamshaftPartId = stockCamId });

        var cardRace = MakeCard(testCar, testEngineId);
        PowerCalculator.Calculate(cardRace, new SelectedParts
            { ForcedInductionPartId = turboPart, CamshaftPartId = raceCamId });

        Assert.True(cardRace.PowerHP > cardStock.PowerHP,
            $"Race camshaft ({cardRace.PowerHP:F0} HP) should give more power than stock ({cardStock.PowerHP:F0} HP) " +
            "when forced induction is installed for the first time on an NA engine (isNaToFi path).");
    }

    // ── КРИТ #2: Manifold на NA-двигателе при добавлении FI-апгрейда ─────────
    // Турбированные двигатели в БД не имеют Manifold с TorqueScale > 1.
    // Тест проверяет NA-двигатель у которого есть manifold + доступный turbo-апгрейд.

    [Fact]
    public async Task Manifold_AffectsPower_WhenTurboInstalledOnNaEngine()
    {
        using var env = new TestingEnvironment();
        var db = Fh6DatabaseService.Instance;
        await db.InitializeAsync();

        DbCar? testCar = null;
        int testEngineId = 0, turboPart = 0, stockManifoldId = 0, raceManifoldId = 0;

        foreach (var dbCar in db.GetAllCars())
        {
            if (dbCar.AspirationTypeId == 8 || dbCar.SimPeakPower <= 0) continue;
            var swaps = db.GetEngineSwaps(dbCar.Id);
            var stockSwap = swaps.FirstOrDefault(s => s.IsStock);
            if (stockSwap == null) continue;
            int eid = stockSwap.EngineID;

            // Только NA двигатели (нет стокового турбо)
            bool hasStockTurbo = db.GetTurbosSingle(eid).Any(t => t.IsStock)
                               || db.GetTurbosTwin(eid).Any(t => t.IsStock);
            if (hasStockTurbo) continue;

            // Нужен доступный турбо-апгрейд
            var turboUpgrade = (DbUpgradePart?)db.GetTurbosSingle(eid).FirstOrDefault(t => !t.IsStock)
                            ?? db.GetTurbosTwin(eid).FirstOrDefault(t => !t.IsStock);
            if (turboUpgrade == null) continue;

            var manifolds = db.GetManifolds(eid);
            var stockM = manifolds.FirstOrDefault(m => m.IsStock);
            var raceM = manifolds.Where(m => !m.IsStock && (m.TorqueScale ?? 1.0) > 1.0)
                                 .OrderByDescending(m => m.TorqueScale ?? 1.0).FirstOrDefault();
            if (stockM == null || raceM == null) continue;

            testCar = dbCar; testEngineId = eid;
            turboPart = turboUpgrade.Id;
            stockManifoldId = stockM.Id; raceManifoldId = raceM.Id;
            break;
        }

        if (testCar == null) return;

        // С турбо + стоковый manifold
        var cardStock = MakeCard(testCar, testEngineId);
        PowerCalculator.Calculate(cardStock, new SelectedParts
            { ForcedInductionPartId = turboPart, ManifoldPartId = stockManifoldId });

        // С турбо + race manifold — должен давать больше мощности
        var cardRace = MakeCard(testCar, testEngineId);
        PowerCalculator.Calculate(cardRace, new SelectedParts
            { ForcedInductionPartId = turboPart, ManifoldPartId = raceManifoldId });

        Assert.True(cardRace.PowerHP > cardStock.PowerHP,
            $"Race manifold ({cardRace.PowerHP:F0} HP) should give more power than stock ({cardStock.PowerHP:F0} HP) even with turbo installed.");
    }
}
