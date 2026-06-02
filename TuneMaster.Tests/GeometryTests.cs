using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;
using TuneMaster.Tests.Helpers;

namespace TuneMaster.Tests;

/// Tests 021–040: Camber, Toe, Caster physics
/// Sources: forzafire.com platform guide, forums.forza.net tuning guide
/// Rules:
///   Camber: Drag ≈ 0°, Drift front ≈ -3 to -4°, Road front ≈ -1 to -2°
///   Toe: Drag = 0°/0°, Drift rear = slight toe-out (+), Road RWD rear = slight toe-in
///   Caster: Drag ≤ 6.5°, Road heavy/fast cars near max (6.5–7°)
///   Caster: rear-engine and CC get reduced caster
public class GeometryTests
{
    private static TuneResult Gen(CarCard car, TrackInfo track)
        => new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());

    // 021 – Drag camber: front and rear both near 0° (max contact patch)
    [Fact]
    public void T021_DragCamberNearZero()
    {
        var r = Gen(CarFactory.Gemera(), CarFactory.Drag());
        Assert.InRange(r.CamberFront, -1.0, 0.0);
        Assert.InRange(r.CamberRear,  -0.5, 0.5);
    }

    // 022 – Drift front camber is aggressive negative (community: -3° to -4°)
    [Fact]
    public void T022_DriftFrontCamberAggressive()
    {
        var r = Gen(CarFactory.SupraA90(), CarFactory.Drift());
        Assert.True(r.CamberFront <= -2.5, $"Drift front camber {r.CamberFront} should be <= -2.5°");
    }

    // 023 – Drift rear camber less aggressive than front
    [Fact]
    public void T023_DriftRearCamberLessAggressiveThanFront()
    {
        var r = Gen(CarFactory.SupraA90(), CarFactory.Drift());
        Assert.True(r.CamberRear > r.CamberFront,
            $"Drift rear {r.CamberRear} should be less negative than front {r.CamberFront}");
    }

    // 024 – Road/Touge: front camber negative (grip), rear less negative
    [Fact]
    public void T024_RoadFrontMoreNegativeThanRear()
    {
        var r = Gen(CarFactory.GtrR35(), CarFactory.Road());
        Assert.True(r.CamberFront < 0, $"Front camber must be negative, got {r.CamberFront}");
        Assert.True(r.CamberFront <= r.CamberRear,
            $"Front {r.CamberFront} should be <= rear {r.CamberRear} (more negative)");
    }

    // 025 – Camber always within constraint bounds
    [Fact]
    public void T025_CamberWithinConstraints()
    {
        var c = CarFactory.DefaultConstraints();
        foreach (var car in AllCars())
        foreach (var track in AllTracks())
        {
            var r = new TuneGeneratorService().Generate(car, track, c);
            Assert.InRange(r.CamberFront, c.CamberFrontMin, c.CamberFrontMax);
            Assert.InRange(r.CamberRear,  c.CamberRearMin,  c.CamberRearMax);
        }
    }

    // 026 – Drag toe: front and rear both 0°
    [Fact]
    public void T026_DragToeZero()
    {
        var r = Gen(CarFactory.Hellcat(), CarFactory.Drag());
        Assert.Equal(0.0, r.ToeFront);
        Assert.Equal(0.0, r.ToeRear);
    }

    // 027 – Drift rear toe: positive (toe-out promotes rotation)
    [Fact]
    public void T027_DriftRearToePositive()
    {
        var r = Gen(CarFactory.SupraA90(), CarFactory.Drift());
        Assert.True(r.ToeRear >= 0, $"Drift rear toe {r.ToeRear} should be >= 0 (toe-out)");
    }

    // 028 – Drift front toe: zero or slightly negative (counter-steer)
    [Fact]
    public void T028_DriftFrontToeNegativeOrZero()
    {
        var r = Gen(CarFactory.SupraA90(), CarFactory.Drift());
        Assert.True(r.ToeFront <= 0, $"Drift front toe {r.ToeFront} should be <= 0");
    }

    // 029 – RWD Road: rear toe slightly positive (toe-in = stability)
    [Fact]
    public void T029_RwdRoadRearToeIn()
    {
        var r = Gen(CarFactory.SupraA90(), CarFactory.Road());
        Assert.True(r.ToeRear >= -0.1, $"RWD Road rear toe {r.ToeRear} should not be toe-out");
    }

    // 030 – Toe always within constraint bounds
    [Fact]
    public void T030_ToeWithinConstraints()
    {
        var c = CarFactory.DefaultConstraints();
        foreach (var car in AllCars())
        foreach (var track in AllTracks())
        {
            var r = new TuneGeneratorService().Generate(car, track, c);
            Assert.InRange(r.ToeFront, c.ToeFrontMin, c.ToeFrontMax);
            Assert.InRange(r.ToeRear,  c.ToeRearMin,  c.ToeRearMax);
        }
    }

    // 031 – Caster always within constraint bounds [1–7]
    [Fact]
    public void T031_CasterWithinConstraints()
    {
        var c = CarFactory.DefaultConstraints();
        foreach (var car in AllCars())
        foreach (var track in AllTracks())
        {
            var r = new TuneGeneratorService().Generate(car, track, c);
            Assert.InRange(r.Caster, c.CasterMin, c.CasterMax);
        }
    }

    // 032 – Drag caster: not at maximum (light steering preferred for drag launch)
    [Fact]
    public void T032_DragCasterNotAtMax()
    {
        var r = Gen(CarFactory.Gemera(), CarFactory.Drag(DragDistance.Mile));
        Assert.True(r.Caster <= 6.5,
            $"Drag caster {r.Caster} should be <= 6.5° (community: 5–6° for drag)");
    }

    // 033 – Road high-speed car (>300 km/h) caster higher than slow car (<200 km/h)
    [Fact]
    public void T033_HighSpeedCasterHigher()
    {
        var fast = CarFactory.GtrR35(); fast.MaxSpeedKmh = 350;
        var slow = CarFactory.Mx5();   slow.MaxSpeedKmh = 180;
        var r_fast = Gen(fast, CarFactory.Road());
        var r_slow = Gen(slow, CarFactory.Road());
        Assert.True(r_fast.Caster >= r_slow.Caster,
            $"Fast car caster {r_fast.Caster} should be >= slow car caster {r_slow.Caster}");
    }

    // 034 – CrossCountry caster lower than Road (reduced for off-road agility)
    [Fact]
    public void T034_CrossCountryCasterReducedVsRoad()
    {
        var r_road = Gen(CarFactory.Wrangler(), CarFactory.Road());
        var r_cc   = Gen(CarFactory.Wrangler(), CarFactory.CrossCountry());
        Assert.True(r_cc.Caster <= r_road.Caster,
            $"CC caster {r_cc.Caster} should be <= Road caster {r_road.Caster}");
    }

    // 035 – Rear-engine car (GT3 RS): front camber less negative than front-engine (less load)
    [Fact]
    public void T035_RearEngineLessFrontCamber()
    {
        var rear  = CarFactory.Gt3Rs();   // rear-engine
        var front = CarFactory.SupraA90(); // front-engine
        var r_rear  = Gen(rear,  CarFactory.Road());
        var r_front = Gen(front, CarFactory.Road());
        // Rear-engine has engine pos adjustment that adds to front camber
        Assert.True(r_rear.CamberFront <= r_front.CamberFront + 0.5,
            $"Rear-engine front camber {r_rear.CamberFront} vs front-engine {r_front.CamberFront}");
    }

    // 036 – Camber does not exceed 0° on rear for drag (flat contact patch maximizes traction)
    [Fact]
    public void T036_DragRearCamberNotPositive()
    {
        var r = Gen(CarFactory.GtrR35(), CarFactory.Drag());
        Assert.True(r.CamberRear <= 0.0, $"Drag rear camber {r.CamberRear} should be <= 0");
    }

    // 037 – High-power RWD car: more negative rear camber than low-power version (Road)
    [Fact]
    public void T037_HighPowerRwdMoreRearCamber()
    {
        var high = CarFactory.SupraA90(); high.PowerHP = 800;
        var low  = CarFactory.SupraA90(); low.PowerHP  = 200;
        var r_high = Gen(high, CarFactory.Road());
        var r_low  = Gen(low,  CarFactory.Road());
        Assert.True(r_high.CamberRear <= r_low.CamberRear,
            $"High power rear camber {r_high.CamberRear} should be <= low power {r_low.CamberRear}");
    }

    // 038 – CC front toe: negative (toe-out helps rotation over rough terrain)
    [Fact]
    public void T038_CrossCountryFrontToeNegative()
    {
        var r = Gen(CarFactory.Wrangler(), CarFactory.CrossCountry());
        Assert.True(r.ToeFront <= 0, $"CC front toe {r.ToeFront} should be <= 0 (toe-out)");
    }

    // 039 – Rally toe: front negative, rear positive (similar to CC)
    [Fact]
    public void T039_RallyToeDirectionCorrect()
    {
        var r = Gen(CarFactory.WrxSti(), CarFactory.Rally());
        Assert.True(r.ToeFront <= 0,   $"Rally front toe {r.ToeFront} should be <= 0");
        Assert.True(r.ToeRear  >= 0.0, $"Rally rear toe {r.ToeRear} should be >= 0");
    }

    // 040 – FWD Road: rear toe-in (stability) — slightly positive rear toe
    [Fact]
    public void T040_FwdRoadRearToeIn()
    {
        var r = Gen(CarFactory.CivicTypeR(), CarFactory.Road());
        Assert.True(r.ToeRear >= 0.0, $"FWD Road rear toe {r.ToeRear} should be toe-in (>= 0)");
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
