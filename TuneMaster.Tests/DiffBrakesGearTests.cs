using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;
using TuneMaster.Tests.Helpers;

namespace TuneMaster.Tests;

/// Tests 071–090: Differential, Brakes, Gearing physics
/// Sources: forzafire.com, forums.forza.net, ApexSpeedCraft, ForzaTune
/// Rules:
///   Drag decel = 0% (community universal), drag accel high
///   AWD center bias: more rear for Road (community: 60–90% rear)
///   AWD: front diff and center bias fields populated
///   RWD/FWD: no front diff / center bias fields
///   Brake balance shifted rear for Drag (less front braking on launch)
///   Final drive: must target MaxSpeedKmh @ 95% MaxRPM
///   Gear ratios: descending geometric progression, count matches GearCount
public class DiffBrakesGearTests
{
    private static TuneResult Gen(CarCard car, TrackInfo track)
        => new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());

    // 071 – Drag AWD decel = 0% (no brake locking on launch — bug fix verification)
    [Fact]
    public void T071_DragAwdDiffDecelZero()
    {
        var r = Gen(CarFactory.Gemera(), CarFactory.Drag());
        Assert.Equal(0.0, r.DiffDecel);
    }

    // 072 – Drag RWD decel stays low (no rear brake lock)
    [Fact]
    public void T072_DragRwdDiffDecelLow()
    {
        var r = Gen(CarFactory.Hellcat(), CarFactory.Drag());
        Assert.True(r.DiffDecel <= 25,
            $"Drag RWD diff decel {r.DiffDecel} should be low (≤25)");
    }

    // 073 – Drag accel significantly higher than decel for all drivetrains
    [Fact]
    public void T073_DragAccelHigherThanDecel()
    {
        foreach (var car in new[] { CarFactory.Gemera(), CarFactory.Hellcat(), CarFactory.CivicTypeR() })
        {
            var r = Gen(car, CarFactory.Drag());
            Assert.True(r.DiffAccel > r.DiffDecel,
                $"{car.DriveType} drag: accel {r.DiffAccel} should be > decel {r.DiffDecel}");
        }
    }

    // 074 – AWD: CenterDiffBias and front diff are populated
    [Fact]
    public void T074_AwdHasFrontDiffAndCenterBias()
    {
        var r = Gen(CarFactory.GtrR35(), CarFactory.Road());
        Assert.NotNull(r.CenterDiffBias);
        Assert.NotNull(r.DiffFrontAccel);
        Assert.NotNull(r.DiffFrontDecel);
    }

    // 075 – RWD/FWD: center diff fields are null (no AWD center differential)
    [Fact]
    public void T075_RwdFwdNoCenterDiff()
    {
        var r_rwd = Gen(CarFactory.SupraA90(), CarFactory.Road());
        var r_fwd = Gen(CarFactory.CivicTypeR(), CarFactory.Road());
        Assert.Null(r_rwd.CenterDiffBias);
        Assert.Null(r_fwd.CenterDiffBias);
        Assert.Null(r_rwd.DiffFrontAccel);
        Assert.Null(r_fwd.DiffFrontAccel);
    }

    // 076 – AWD center bias: Road/Touge skewed rear (community: 60–90% rear = 60–90 value)
    [Fact]
    public void T076_AwdCenterBiasRearSkewedForRoad()
    {
        var r = Gen(CarFactory.GtrR35(), CarFactory.Road());
        Assert.True(r.CenterDiffBias >= 55,
            $"AWD center bias {r.CenterDiffBias} should be >= 55 (rear-skewed for Road)");
    }

    // 077 – Diff accel/decel always within [0, 100]
    [Fact]
    public void T077_DiffWithinRange()
    {
        var c = CarFactory.DefaultConstraints();
        foreach (var car in AllCars())
        foreach (var track in AllTracks())
        {
            var r = new TuneGeneratorService().Generate(car, track, c);
            Assert.InRange(r.DiffAccel, 0, 100);
            Assert.InRange(r.DiffDecel, 0, 100);
        }
    }

    // 078 – Drift RWD: diff accel very high (holds wheelspin for angle)
    [Fact]
    public void T078_DriftRwdDiffAccelHigh()
    {
        var r = Gen(CarFactory.SupraA90(), CarFactory.Drift());
        Assert.True(r.DiffAccel >= 40,
            $"Drift RWD diff accel {r.DiffAccel} should be >= 40");
    }

    // 079 – Stock diff caps result significantly below Race diff (same car, same discipline)
    [Fact]
    public void T079_StockDiffLowerThanRaceDiff()
    {
        var stock = CarFactory.GtrR35(); stock.DifferentialUpgrade = DifferentialUpgrade.Stock;
        var race  = CarFactory.GtrR35(); race.DifferentialUpgrade  = DifferentialUpgrade.Race;
        var r_stock = Gen(stock, CarFactory.Road());
        var r_race  = Gen(race,  CarFactory.Road());
        Assert.True(r_race.DiffAccel >= r_stock.DiffAccel,
            $"Race diff accel {r_race.DiffAccel} should be >= Stock {r_stock.DiffAccel}");
    }

    // 080 – Brake balance: drag shifts balance rearward vs road (less front engagement)
    [Fact]
    public void T080_DragBrakeBalanceLowerThanRoad()
    {
        var r_road = Gen(CarFactory.GtrR35(), CarFactory.Road());
        var r_drag = Gen(CarFactory.GtrR35(), CarFactory.Drag());
        Assert.True(r_drag.BrakeBalance <= r_road.BrakeBalance,
            $"Drag brake balance {r_drag.BrakeBalance} should be <= Road {r_road.BrakeBalance}");
    }

    // 081 – Brake balance always within constraint [40–70]
    [Fact]
    public void T081_BrakeBalanceWithinConstraints()
    {
        var c = CarFactory.DefaultConstraints();
        foreach (var car in AllCars())
        foreach (var track in AllTracks())
        {
            var r = new TuneGeneratorService().Generate(car, track, c);
            Assert.InRange(r.BrakeBalance, c.BrakeBalanceMin, c.BrakeBalanceMax);
        }
    }

    // 082 – Brake pressure always within [50–200]
    [Fact]
    public void T082_BrakePressureWithinConstraints()
    {
        var c = CarFactory.DefaultConstraints();
        foreach (var car in AllCars())
        foreach (var track in AllTracks())
        {
            var r = new TuneGeneratorService().Generate(car, track, c);
            Assert.InRange(r.BrakePressure, c.BrakePressureMin, c.BrakePressureMax);
        }
    }

    // 083 – Gear count matches GearCount (correct number of ratios generated)
    [Fact]
    public void T083_GearCountMatchesCarConfig()
    {
        foreach (var car in AllCars())
        foreach (var track in AllTracks())
        {
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            Assert.Equal(car.GearCount, r.GearRatios.Count);
        }
    }

    // 084 – Gear ratios are strictly descending (each gear is shorter than previous)
    [Fact]
    public void T084_GearRatiosDescending()
    {
        foreach (var car in AllCars())
        foreach (var track in AllTracks())
        {
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            for (int i = 1; i < r.GearRatios.Count; i++)
            {
                Assert.True(r.GearRatios[i] < r.GearRatios[i - 1],
                    $"{car.DriveType}/{track.Discipline}: gear {i+1}={r.GearRatios[i]} not < gear {i}={r.GearRatios[i-1]}");
            }
        }
    }

    // 085 – All gear ratios are positive
    [Fact]
    public void T085_GearRatiosAllPositive()
    {
        foreach (var car in AllCars())
        foreach (var track in AllTracks())
        {
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            foreach (var ratio in r.GearRatios)
                Assert.True(ratio > 0, $"Ratio {ratio} is not positive");
        }
    }

    // 086 – Final drive within constraint [2.2–6.1]
    [Fact]
    public void T086_FinalDriveWithinConstraints()
    {
        var c = CarFactory.DefaultConstraints();
        foreach (var car in AllCars())
        foreach (var track in AllTracks())
        {
            var r = new TuneGeneratorService().Generate(car, track, c);
            Assert.InRange(r.FinalDrive, c.FinalDriveMin, c.FinalDriveMax);
        }
    }

    // 087 – Electric car (single gear): exactly 1 gear ratio
    [Fact]
    public void T087_ElectricSingleGear()
    {
        var car = CarFactory.ModelSPlaid(); // GearCount = 1
        var r   = Gen(car, CarFactory.Road());
        Assert.Single(r.GearRatios);
    }

    // 088 – High-power car first gear shorter (higher ratio) than low-power car (same layout)
    [Fact]
    public void T088_HighPowerShorterFirstGear()
    {
        var high = CarFactory.SupraA90(); high.PowerHP = 900; high.GearCount = 6;
        var low  = CarFactory.SupraA90(); low.PowerHP  = 150; low.GearCount  = 6;
        var r_high = Gen(high, CarFactory.Road());
        var r_low  = Gen(low,  CarFactory.Road());
        Assert.True(r_high.GearRatios[0] >= r_low.GearRatios[0],
            $"High power 1st gear {r_high.GearRatios[0]} should be >= low power {r_low.GearRatios[0]}");
    }

    // 089 – Drag mile: final drive produces long top gear (low ratio) vs 1/8 mile
    [Fact]
    public void T089_DragMileLongerTopGearThanEighthMile()
    {
        var car1 = CarFactory.Hellcat(); car1.GearCount = 6;
        var car2 = CarFactory.Hellcat(); car2.GearCount = 6;
        var r_mile   = Gen(car1, CarFactory.Drag(DragDistance.Mile));
        var r_eighth = Gen(car2, CarFactory.Drag(DragDistance.Eighth));
        // Mile drag top gear ratio should be lower (longer) than 1/8 mile
        double topMile   = r_mile.GearRatios.Last();
        double topEighth = r_eighth.GearRatios.Last();
        Assert.True(topMile <= topEighth,
            $"Mile top gear {topMile} should be <= eighth-mile top {topEighth} (longer ratio)");
    }

    // 090 – FWD high brake balance: front-biased braking (>= 50%)
    [Fact]
    public void T090_FwdBrakeBalanceFrontBiased()
    {
        var r = Gen(CarFactory.CivicTypeR(), CarFactory.Road());
        Assert.True(r.BrakeBalance >= 50,
            $"FWD brake balance {r.BrakeBalance} should be >= 50 (front bias)");
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
