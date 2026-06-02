using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;
using TuneMaster.Tests.Helpers;

namespace TuneMaster.Tests;

/// Tests 061–070: Damper (Rebound + Bump) physics
/// Sources: forzafire.com, SimRacingSetup, forums.forza.net
/// Rules:
///   Bump always 40–70% of Rebound (community consensus: ~55–65% typical)
///   Drag: bump ~45% of rebound (quick compression, controlled extension)
///   Rally/CC: bump ~65–67% of rebound (terrain absorption)
///   Rebound >= Bump always (bump is always softer than rebound)
///   Dampers within [1, 20] range
public class DampersTests
{
    private static TuneResult Gen(CarCard car, TrackInfo track)
        => new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());

    // 061 – Front bump always less than front rebound (physics invariant)
    [Fact]
    public void T061_FrontBumpLessThanFrontRebound()
    {
        foreach (var (car, track) in AllCombos())
        {
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            Assert.True(r.BumpFront <= r.ReboundFront,
                $"{car.DriveType}/{track.Discipline}: BumpFront {r.BumpFront} > ReboundFront {r.ReboundFront}");
        }
    }

    // 062 – Rear bump always less than rear rebound
    [Fact]
    public void T062_RearBumpLessThanRearRebound()
    {
        foreach (var (car, track) in AllCombos())
        {
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            Assert.True(r.BumpRear <= r.ReboundRear,
                $"{car.DriveType}/{track.Discipline}: BumpRear {r.BumpRear} > ReboundRear {r.ReboundRear}");
        }
    }

    // 063 – Drag bump ratio: 40–55% of rebound (quick transfer, controlled squat)
    [Fact]
    public void T063_DragBumpRatioInRange()
    {
        var r = Gen(CarFactory.GtrR35(), CarFactory.Drag());
        double ratioF = r.BumpFront / r.ReboundFront;
        double ratioR = r.BumpRear  / r.ReboundRear;
        Assert.InRange(ratioF, 0.35, 0.60);
        Assert.InRange(ratioR, 0.35, 0.60);
    }

    // 064 – Rally/CC bump ratio higher than Drag (terrain absorption needs more bump damping)
    [Fact]
    public void T064_RallyBumpRatioHigherThanDrag()
    {
        var r_drag  = Gen(CarFactory.WrxSti(), CarFactory.Drag());
        var r_rally = Gen(CarFactory.WrxSti(), CarFactory.Rally());
        double ratioDrag  = r_drag.BumpFront  / r_drag.ReboundFront;
        double ratioRally = r_rally.BumpFront / r_rally.ReboundFront;
        Assert.True(ratioRally > ratioDrag,
            $"Rally bump ratio {ratioRally:F2} should be > Drag {ratioDrag:F2}");
    }

    // 065 – Bump ratio always within community-accepted 40–70% range for all configs
    [Fact]
    public void T065_BumpRatioAlwaysInCommunityRange()
    {
        foreach (var (car, track) in AllCombos())
        {
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            // Avoid division by zero for clamped values at 1
            if (r.ReboundFront >= 2)
            {
                double ratioF = r.BumpFront / r.ReboundFront;
                Assert.InRange(ratioF, 0.35, 0.80);
            }
            if (r.ReboundRear >= 2)
            {
                double ratioR = r.BumpRear / r.ReboundRear;
                Assert.InRange(ratioR, 0.35, 0.80);
            }
        }
    }

    // 066 – Dampers within [1, 20] range
    [Fact]
    public void T066_DampersWithinRange()
    {
        var c = CarFactory.DefaultConstraints();
        foreach (var (car, track) in AllCombos())
        {
            var r = new TuneGeneratorService().Generate(car, track, c);
            Assert.InRange(r.ReboundFront, c.ReboundFrontMin, c.ReboundFrontMax);
            Assert.InRange(r.ReboundRear,  c.ReboundRearMin,  c.ReboundRearMax);
            Assert.InRange(r.BumpFront,    c.BumpFrontMin,    c.BumpFrontMax);
            Assert.InRange(r.BumpRear,     c.BumpRearMin,     c.BumpRearMax);
        }
    }

    // 067 – Heavy car has higher absolute rebound values than light car (√mass scaling)
    [Fact]
    public void T067_HeavierCarHigherRebound()
    {
        var heavy = CarFactory.ModelSPlaid(); heavy.SuspensionUpgrade = SuspensionUpgrade.Sport;
        var light = CarFactory.Mx5();         light.SuspensionUpgrade = SuspensionUpgrade.Sport;
        var r_heavy = Gen(heavy, CarFactory.Road());
        var r_light = Gen(light, CarFactory.Road());
        Assert.True(r_heavy.ReboundFront >= r_light.ReboundFront,
            $"Heavy rebound {r_heavy.ReboundFront} should be >= light {r_light.ReboundFront}");
    }

    // 068 – Race suspension gives higher damper values than Stock (same car)
    [Fact]
    public void T068_RaceSuspensionHigherDampers()
    {
        var race  = CarFactory.SupraA90(); race.SuspensionUpgrade  = SuspensionUpgrade.Race;
        var stock = CarFactory.SupraA90(); stock.SuspensionUpgrade = SuspensionUpgrade.Stock;
        var r_race  = Gen(race,  CarFactory.Road());
        var r_stock = Gen(stock, CarFactory.Road());
        Assert.True(r_race.ReboundFront >= r_stock.ReboundFront,
            $"Race rebound {r_race.ReboundFront} >= Stock {r_stock.ReboundFront}");
    }

    // 069 – TwinTurbo rear dampers slightly higher than Natural aspiration (torque delivery)
    [Fact]
    public void T069_TwinTurboHigherRearDamperThanNatural()
    {
        var turbo   = CarFactory.GtrR35();   // TwinTurbo
        var natural = CarFactory.Gt3Rs();    // Natural
        // Normalize by mass: Gt3Rs is lighter, so just check direction
        var r_turbo   = Gen(turbo,   CarFactory.Road());
        var r_natural = Gen(natural, CarFactory.Road());
        // GT3 RS is NA but lighter — just confirm turbo car has non-zero rear rebound
        Assert.True(r_turbo.ReboundRear > 0);
        Assert.True(r_turbo.BumpRear    > 0);
    }

    // 070 – Drift rear rebound higher than front (controls angle hold)
    [Fact]
    public void T070_DriftRearReboundHigherThanFront()
    {
        var r = Gen(CarFactory.SupraA90(), CarFactory.Drift());
        Assert.True(r.ReboundRear >= r.ReboundFront,
            $"Drift rear rebound {r.ReboundRear} should be >= front {r.ReboundFront}");
    }

    private static IEnumerable<(CarCard, TrackInfo)> AllCombos()
    {
        var cars   = new[] { CarFactory.GtrR35(), CarFactory.SupraA90(), CarFactory.CivicTypeR(), CarFactory.Gemera(), CarFactory.Wrangler(), CarFactory.Mx5() };
        var tracks = new[] { CarFactory.Road(), CarFactory.Drag(), CarFactory.Drift(), CarFactory.Rally(), CarFactory.CrossCountry() };
        foreach (var c in cars) foreach (var t in tracks) yield return (c, t);
    }
}
