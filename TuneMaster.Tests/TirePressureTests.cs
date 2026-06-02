using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;
using TuneMaster.Tests.Helpers;

namespace TuneMaster.Tests;

/// Tests 001–020: Tire pressure physics
/// Sources: forzafire.com FH6 tires guide, forums.forza.net, SimRacingSetup
/// Rules:
///   Slick=2.24 / SemiSlick=2.21 / Sport=2.17 / Stock=2.14 bar baseline
///   Drag rear target ≈ 1.72–1.85 bar (~25 PSI); front stays near 2.21
///   Rally/CC: both ends lower than Road baseline
///   Drift: both ends low (~1.4–1.9 bar)
///   Pressure must stay within [TirePressureMin, TirePressureMax]
public class TirePressureTests
{
    private static TuneResult Gen(CarCard car, TrackInfo track)
        => new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());

    // 001 – Slick tires baseline is higher than Sport tires on same car/discipline
    [Fact]
    public void T001_SlickPressureHigherThanSport()
    {
        var car1 = CarFactory.GtrR35(); car1.TireType = TireType.Slick;
        var car2 = CarFactory.GtrR35(); car2.TireType = TireType.Sport;
        var r1 = Gen(car1, CarFactory.Road());
        var r2 = Gen(car2, CarFactory.Road());
        Assert.True(r1.TirePressureFront >= r2.TirePressureFront,
            $"Slick front {r1.TirePressureFront} should be >= Sport front {r2.TirePressureFront}");
    }

    // 002 – Drag rear pressure is significantly lower than drag front (weight transfer)
    [Fact]
    public void T002_DragRearLowerThanFront()
    {
        var r = Gen(CarFactory.GtrR35(), CarFactory.Drag());
        Assert.True(r.TirePressureRear < r.TirePressureFront,
            $"Drag rear {r.TirePressureRear} should be < front {r.TirePressureFront}");
    }

    // 003 – Drag rear pressure near community target (~1.72–1.90 bar ≈ 25–27.5 PSI)
    [Fact]
    public void T003_DragRearPressureInCommunityRange()
    {
        var r = Gen(CarFactory.GtrR35(), CarFactory.Drag());
        Assert.InRange(r.TirePressureRear, 1.65, 1.95);
    }

    // 004 – Drag rear pressure with stock diff on Gemera still in valid range
    [Fact]
    public void T004_GemeraRearDragPressureValid()
    {
        var r = Gen(CarFactory.Gemera(), CarFactory.Drag(DragDistance.Mile));
        Assert.InRange(r.TirePressureRear, 1.0, 3.8);
        Assert.True(r.TirePressureRear < r.TirePressureFront);
    }

    // 005 – Rally pressure lower than Road pressure (both ends, same car)
    [Fact]
    public void T005_RallyPressureLowerThanRoad()
    {
        var r_road  = Gen(CarFactory.WrxSti(), CarFactory.Road());
        var r_rally = Gen(CarFactory.WrxSti(), CarFactory.Rally());
        Assert.True(r_rally.TirePressureFront < r_road.TirePressureFront,
            $"Rally front {r_rally.TirePressureFront} should be < Road front {r_road.TirePressureFront}");
        Assert.True(r_rally.TirePressureRear < r_road.TirePressureRear,
            $"Rally rear {r_rally.TirePressureRear} should be < Road rear {r_road.TirePressureRear}");
    }

    // 006 – CrossCountry pressure lower than Road pressure
    [Fact]
    public void T006_CrossCountryPressureLowerThanRoad()
    {
        var r_road = Gen(CarFactory.Wrangler(), CarFactory.Road());
        var r_cc   = Gen(CarFactory.Wrangler(), CarFactory.CrossCountry());
        Assert.True(r_cc.TirePressureFront < r_road.TirePressureFront);
    }

    // 007 – Drift pressure: both ends below Road pressure for same car
    [Fact]
    public void T007_DriftPressureBelowRoad()
    {
        var r_road  = Gen(CarFactory.SupraA90(), CarFactory.Road());
        var r_drift = Gen(CarFactory.SupraA90(), CarFactory.Drift());
        Assert.True(r_drift.TirePressureFront <= r_road.TirePressureFront);
        Assert.True(r_drift.TirePressureRear  <= r_road.TirePressureRear);
    }

    // 008 – Heavy car (>1800 kg) gets higher base pressure than 1000 kg car (Road)
    [Fact]
    public void T008_HeavierCarHigherPressure()
    {
        var heavy = CarFactory.ModelSPlaid(); heavy.TireType = TireType.Sport;
        var light = CarFactory.Mx5();        light.TireType = TireType.Sport;
        var r_heavy = Gen(heavy, CarFactory.Road());
        var r_light = Gen(light, CarFactory.Road());
        Assert.True(r_heavy.TirePressureFront >= r_light.TirePressureFront,
            $"Heavy {r_heavy.TirePressureFront} should be >= Light {r_light.TirePressureFront}");
    }

    // 009 – Offroad tires have lower pressure than Sport tires (same car, Road discipline)
    [Fact]
    public void T009_OffroadPressureLowerThanSport()
    {
        var sport = CarFactory.Wrangler(); sport.TireType = TireType.Sport;
        var off   = CarFactory.Wrangler(); off.TireType   = TireType.Offroad;
        var r_sport = Gen(sport, CarFactory.Road());
        var r_off   = Gen(off,   CarFactory.Road());
        Assert.True(r_off.TirePressureFront < r_sport.TirePressureFront);
    }

    // 010 – All pressures stay within constraint bounds
    [Fact]
    public void T010_PressuresWithinConstraints()
    {
        var c = CarFactory.DefaultConstraints();
        foreach (var (car, track) in AllCombos())
        {
            var r = new TuneGeneratorService().Generate(car, track, c);
            Assert.InRange(r.TirePressureFront, c.TirePressureFrontMin, c.TirePressureFrontMax);
            Assert.InRange(r.TirePressureRear,  c.TirePressureRearMin,  c.TirePressureRearMax);
        }
    }

    // 011 – FWD drag: front tire under more stress, front pressure must NOT be below rear
    [Fact]
    public void T011_FwdDragFrontNotLowerThanRear()
    {
        var civic = CarFactory.CivicTypeR();
        civic.TireType = TireType.Drag;
        var r = Gen(civic, CarFactory.Drag());
        Assert.True(r.TirePressureFront >= r.TirePressureRear,
            $"FWD drag: front {r.TirePressureFront} should be >= rear {r.TirePressureRear}");
    }

    // 012 – SemiSlick drag rear stays lower than SemiSlick road rear
    [Fact]
    public void T012_SemiSlickDragRearLowerThanRoadRear()
    {
        var car1 = CarFactory.GtrR35(); car1.TireType = TireType.SemiSlick;
        var r_road = Gen(car1, CarFactory.Road());
        var car2 = CarFactory.GtrR35(); car2.TireType = TireType.SemiSlick;
        var r_drag = Gen(car2, CarFactory.Drag());
        Assert.True(r_drag.TirePressureRear < r_road.TirePressureRear);
    }

    // 013 – Rally tires on Rally discipline: both ends ≤ 2.10 bar
    [Fact]
    public void T013_RallyTiresRallyDisciplinePressureLow()
    {
        var car = CarFactory.WrxSti(); car.TireType = TireType.Rally;
        var r = Gen(car, CarFactory.Rally());
        Assert.True(r.TirePressureFront <= 2.10, $"Front: {r.TirePressureFront}");
        Assert.True(r.TirePressureRear  <= 2.10, $"Rear:  {r.TirePressureRear}");
    }

    // 014 – Drag with NO power adjustment: RWD high-power front should not drop excessively
    [Fact]
    public void T014_DragRwdFrontPressureReasonable()
    {
        var car = CarFactory.Hellcat();
        var r = Gen(car, CarFactory.Drag());
        // Front must stay near 2.21 ± 0.4 bar (community: keep front firm for drag)
        Assert.InRange(r.TirePressureFront, 1.80, 2.65);
    }

    // 015 – Hellcat drag rear pressure lower than front (RWD launches on rear)
    [Fact]
    public void T015_HellcatDragRearLowerThanFront()
    {
        var r = Gen(CarFactory.Hellcat(), CarFactory.Drag());
        Assert.True(r.TirePressureRear < r.TirePressureFront);
    }

    // 016 – Touge pressure: between Road and Rally (moderate)
    [Fact]
    public void T016_TougePressureBetweenRoadAndRally()
    {
        var r_road  = Gen(CarFactory.SupraA90(), CarFactory.Road());
        var r_touge = Gen(CarFactory.SupraA90(), CarFactory.Touge());
        var r_rally = Gen(CarFactory.SupraA90(), CarFactory.Rally());
        // Touge rear should be between rally and road (or equal)
        Assert.True(r_touge.TirePressureRear >= r_rally.TirePressureRear,
            $"Touge {r_touge.TirePressureRear} should be >= Rally {r_rally.TirePressureRear}");
        Assert.True(r_touge.TirePressureRear <= r_road.TirePressureRear + 0.05,
            $"Touge {r_touge.TirePressureRear} should be near/below Road {r_road.TirePressureRear}");
    }

    // 017 – Electric car (Tesla) drag pressure rear still low (instant torque = more stress)
    [Fact]
    public void T017_ElectricDragRearPressureCorrect()
    {
        var car = CarFactory.ModelSPlaid();
        car.TireType = TireType.Drag;
        var r = Gen(car, CarFactory.Drag());
        Assert.True(r.TirePressureRear < r.TirePressureFront);
    }

    // 018 – Street discipline pressure similar to Road (should not differ much)
    [Fact]
    public void T018_StreetPressureSimilarToRoad()
    {
        var r_road   = Gen(CarFactory.GtrR35(), CarFactory.Road());
        var r_street = Gen(CarFactory.GtrR35(), CarFactory.Street());
        Assert.InRange(Math.Abs(r_street.TirePressureFront - r_road.TirePressureFront), 0, 0.25);
    }

    // 019 – Pressure is always positive (no negative values under any config)
    [Fact]
    public void T019_PressureAlwaysPositive()
    {
        foreach (var (car, track) in AllCombos())
        {
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            Assert.True(r.TirePressureFront > 0);
            Assert.True(r.TirePressureRear  > 0);
        }
    }

    // 020 – GT3 RS (rear-engine RWD): rear pressure >= front (heavier rear)
    [Fact]
    public void T020_RearEngineRwdRearPressureNotLessThanFront_Road()
    {
        var r = Gen(CarFactory.Gt3Rs(), CarFactory.Road());
        // Rear-engine cars have more weight on rear → rear pressure should be higher
        Assert.True(r.TirePressureRear >= r.TirePressureFront,
            $"Rear-engine RWD: rear {r.TirePressureRear} should be >= front {r.TirePressureFront}");
    }

    private static IEnumerable<(CarCard, TrackInfo)> AllCombos()
    {
        var cars   = new[] { CarFactory.GtrR35(), CarFactory.SupraA90(), CarFactory.CivicTypeR(), CarFactory.Gemera(), CarFactory.Mx5() };
        var tracks = new[] { CarFactory.Road(), CarFactory.Drag(), CarFactory.Drift(), CarFactory.Rally(), CarFactory.CrossCountry() };
        foreach (var c in cars) foreach (var t in tracks) yield return (c, t);
    }
}
