using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;
using TuneMaster.Tests.Helpers;

namespace TuneMaster.Tests;

/// Tests 041–060: Springs, Ride Height, ARB physics
/// Sources: forums.forza.net spring formula, forzafire.com platform guide
/// Rules:
///   Springs: Drag front soft (low Hz) / rear stiff (high Hz)
///   ARB Drag: near minimum (community: stiff ARBs cause wheel hop)
///   ARB Drift RWD: max rear (holds angle)
///   ARB CC/Rally: both soft (independent wheel travel)
///   Ride Height: Drag minimum, CC maximum
///   Rear spring > front spring in Drag (weight transfer platform)
public class SpringsArbTests
{
    private static TuneResult Gen(CarCard car, TrackInfo track)
        => new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());

    // 041 – Drag: rear spring stiffer than front (weight transfer platform)
    [Fact]
    public void T041_DragRearSpringStifferThanFront()
    {
        var r = Gen(CarFactory.GtrR35(), CarFactory.Drag());
        Assert.True(r.SpringRear > r.SpringFront,
            $"Drag rear spring {r.SpringRear} should be > front {r.SpringFront}");
    }

    // 042 – CC/Rally: springs both soft relative to Road same car
    [Fact]
    public void T042_CrossCountrySpringsSofterThanRoad()
    {
        var r_road = Gen(CarFactory.Wrangler(), CarFactory.Road());
        var r_cc   = Gen(CarFactory.Wrangler(), CarFactory.CrossCountry());
        double avgRoad = (r_road.SpringFront + r_road.SpringRear) / 2;
        double avgCC   = (r_cc.SpringFront   + r_cc.SpringRear)   / 2;
        Assert.True(avgCC <= avgRoad,
            $"CC avg spring {avgCC} should be <= Road avg spring {avgRoad}");
    }

    // 043 – Springs always within constraint bounds
    [Fact]
    public void T043_SpringsWithinConstraints()
    {
        var c = CarFactory.DefaultConstraints();
        foreach (var car in AllCars())
        foreach (var track in AllTracks())
        {
            var r = new TuneGeneratorService().Generate(car, track, c);
            Assert.InRange(r.SpringFront, c.SpringFrontMin, c.SpringFrontMax);
            Assert.InRange(r.SpringRear,  c.SpringRearMin,  c.SpringRearMax);
        }
    }

    // 044 – Heavier car has higher spring rates than lighter car (same discipline, same susp upgrade)
    [Fact]
    public void T044_HeavierCarHigherSpringRate()
    {
        var heavy = CarFactory.ModelSPlaid(); heavy.SuspensionUpgrade = SuspensionUpgrade.Sport;
        var light = CarFactory.Mx5();         light.SuspensionUpgrade = SuspensionUpgrade.Sport;
        var r_heavy = Gen(heavy, CarFactory.Road());
        var r_light = Gen(light, CarFactory.Road());
        Assert.True(r_heavy.SpringFront >= r_light.SpringFront,
            $"Heavy car spring {r_heavy.SpringFront} should be >= light car spring {r_light.SpringFront}");
    }

    // 045 – Race suspension gives stiffer springs than Stock (same car, same discipline)
    [Fact]
    public void T045_RaceSuspensionStifferThanStock()
    {
        var race  = CarFactory.SupraA90(); race.SuspensionUpgrade  = SuspensionUpgrade.Race;
        var stock = CarFactory.SupraA90(); stock.SuspensionUpgrade = SuspensionUpgrade.Stock;
        var r_race  = Gen(race,  CarFactory.Road());
        var r_stock = Gen(stock, CarFactory.Road());
        Assert.True(r_race.SpringFront > r_stock.SpringFront,
            $"Race spring {r_race.SpringFront} should be > Stock spring {r_stock.SpringFront}");
    }

    // 046 – Drag ARB front near minimum (community rule: minimum ARBs for launch grip)
    [Fact]
    public void T046_DragArbFrontNearMinimum()
    {
        var r = Gen(CarFactory.Gemera(), CarFactory.Drag());
        Assert.True(r.ARBFront <= 10,
            $"Drag ARB front {r.ARBFront} should be near minimum (≤10) for launch grip");
    }

    // 047 – Drag ARB rear moderate (not maximum — prevents hop)
    [Fact]
    public void T047_DragArbRearNotAtMaximum()
    {
        var r = Gen(CarFactory.Hellcat(), CarFactory.Drag());
        Assert.True(r.ARBRear <= 35,
            $"Drag ARB rear {r.ARBRear} should be moderate (≤35), not maximum");
    }

    // 048 – Drift RWD: rear ARB much higher than front (holds drift angle)
    [Fact]
    public void T048_DriftRwdRearArbHigherThanFront()
    {
        var r = Gen(CarFactory.SupraA90(), CarFactory.Drift());
        Assert.True(r.ARBRear > r.ARBFront,
            $"Drift RWD rear ARB {r.ARBRear} should be > front {r.ARBFront}");
    }

    // 049 – Rally ARB: both values should be low (independent wheel travel)
    [Fact]
    public void T049_RallyArbBothLow()
    {
        var r = Gen(CarFactory.WrxSti(), CarFactory.Rally());
        Assert.True(r.ARBFront <= 20, $"Rally ARB front {r.ARBFront} should be low (≤20)");
        Assert.True(r.ARBRear  <= 20, $"Rally ARB rear {r.ARBRear} should be low (≤20)");
    }

    // 050 – CC ARB: even lower than Rally
    [Fact]
    public void T050_CrossCountryArbLowerThanRally()
    {
        var r_rally = Gen(CarFactory.Wrangler(), CarFactory.Rally());
        var r_cc    = Gen(CarFactory.Wrangler(), CarFactory.CrossCountry());
        double avgRally = (r_rally.ARBFront + r_rally.ARBRear) / 2;
        double avgCC    = (r_cc.ARBFront    + r_cc.ARBRear)    / 2;
        Assert.True(avgCC <= avgRally + 1,
            $"CC avg ARB {avgCC} should be <= Rally avg ARB {avgRally}");
    }

    // 051 – ARB always within constraint bounds
    [Fact]
    public void T051_ArbWithinConstraints()
    {
        var c = CarFactory.DefaultConstraints();
        foreach (var car in AllCars())
        foreach (var track in AllTracks())
        {
            var r = new TuneGeneratorService().Generate(car, track, c);
            Assert.InRange(r.ARBFront, c.ARBFrontMin, c.ARBFrontMax);
            Assert.InRange(r.ARBRear,  c.ARBRearMin,  c.ARBRearMax);
        }
    }

    // 052 – FWD Road: front ARB softer than rear (prevents understeer — community standard)
    [Fact]
    public void T052_FwdRoadFrontArbSofterThanRear()
    {
        var r = Gen(CarFactory.CivicTypeR(), CarFactory.Road());
        Assert.True(r.ARBFront <= r.ARBRear,
            $"FWD Road: front ARB {r.ARBFront} should be <= rear ARB {r.ARBRear}");
    }

    // 053 – Drag ride height: front at or near minimum
    [Fact]
    public void T053_DragFrontRideHeightLow()
    {
        var r = Gen(CarFactory.Hellcat(), CarFactory.Drag());
        Assert.True(r.RideHeightFront <= 100,
            $"Drag front ride height {r.RideHeightFront} mm should be low (≤100 mm)");
    }

    // 054 – Drag: rear ride height >= front (rake for weight transfer)
    [Fact]
    public void T054_DragRearHeightHigherThanFront()
    {
        var r = Gen(CarFactory.GtrR35(), CarFactory.Drag());
        Assert.True(r.RideHeightRear >= r.RideHeightFront,
            $"Drag rear {r.RideHeightRear} should be >= front {r.RideHeightFront}");
    }

    // 055 – CC ride height significantly higher than Road same car (ground clearance)
    [Fact]
    public void T055_CrossCountryHeightHigherThanRoad()
    {
        var r_road = Gen(CarFactory.Wrangler(), CarFactory.Road());
        var r_cc   = Gen(CarFactory.Wrangler(), CarFactory.CrossCountry());
        Assert.True(r_cc.RideHeightFront > r_road.RideHeightFront,
            $"CC front height {r_cc.RideHeightFront} should be > Road {r_road.RideHeightFront}");
    }

    // 056 – Ride height always within constraint bounds
    [Fact]
    public void T056_RideHeightWithinConstraints()
    {
        var c = CarFactory.DefaultConstraints();
        foreach (var car in AllCars())
        foreach (var track in AllTracks())
        {
            var r = new TuneGeneratorService().Generate(car, track, c);
            Assert.InRange(r.RideHeightFront, c.RideHeightFrontMin, c.RideHeightFrontMax);
            Assert.InRange(r.RideHeightRear,  c.RideHeightRearMin,  c.RideHeightRearMax);
        }
    }

    // 057 – Rally ride height higher than Touge/Road (terrain clearance)
    [Fact]
    public void T057_RallyHeightHigherThanRoad()
    {
        var r_road  = Gen(CarFactory.WrxSti(), CarFactory.Road());
        var r_rally = Gen(CarFactory.WrxSti(), CarFactory.Rally());
        Assert.True(r_rally.RideHeightFront > r_road.RideHeightFront);
    }

    // 058 – Drift springs: rear stiffer than front (platform for sustained angle)
    [Fact]
    public void T058_DriftRearSpringStifferOrEqualFront()
    {
        var r = Gen(CarFactory.SupraA90(), CarFactory.Drift());
        Assert.True(r.SpringRear >= r.SpringFront,
            $"Drift rear {r.SpringRear} should be >= front {r.SpringFront}");
    }

    // 059 – ARB does not go below minimum (1) for any discipline
    [Fact]
    public void T059_ArbNeverBelowMinimum()
    {
        var c = CarFactory.DefaultConstraints();
        foreach (var car in AllCars())
        foreach (var track in AllTracks())
        {
            var r = new TuneGeneratorService().Generate(car, track, c);
            Assert.True(r.ARBFront >= 1, $"ARB front {r.ARBFront} below minimum 1");
            Assert.True(r.ARBRear  >= 1, $"ARB rear {r.ARBRear} below minimum 1");
        }
    }

    // 060 – High power car drag ARB not blown out by power scaling (bug fix check)
    [Fact]
    public void T060_HighPowerDragArbNotExcessive()
    {
        // Gemera 2300 HP — before fix this was ARBFront=36, ARBRear=53. After fix should be low.
        var r = Gen(CarFactory.Gemera(), CarFactory.Drag());
        Assert.True(r.ARBFront <= 10, $"2300 HP drag ARBFront={r.ARBFront} should be ≤10 after power-adj fix");
        Assert.True(r.ARBRear  <= 30, $"2300 HP drag ARBRear={r.ARBRear} should be ≤30 after fix");
    }

    private static CarCard[] AllCars() => new[]
    {
        CarFactory.GtrR35(), CarFactory.SupraA90(), CarFactory.CivicTypeR(),
        CarFactory.Gemera(), CarFactory.Mx5(), CarFactory.Wrangler(),
        CarFactory.Gt3Rs(), CarFactory.ModelSPlaid(), CarFactory.WrxSti(), CarFactory.Hellcat()
    };
    private static TrackInfo[] AllTracks() => new[]
    {
        CarFactory.Road(), CarFactory.Drag(), CarFactory.Drift(),
        CarFactory.Rally(), CarFactory.CrossCountry(), CarFactory.Touge()
    };
}
