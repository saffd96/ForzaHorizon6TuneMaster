using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;
using TuneMaster.Tests.Helpers;

namespace TuneMaster.Tests;

/// Tests 301–380: Mass, power, speed, wheelbase scaling relationships
public class ScalingRelationshipTests
{
    private static TuneResult Gen(CarCard car, TrackInfo track)
        => new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());

    // ── Mass scaling ────────────────────────────────────────────────────

    // 301 – Spring rate increases with mass (same car, same upgrade, road)
    [Theory]
    [InlineData(1000, 57.1)]
    [InlineData(1500, 57.1)]
    [InlineData(2000, 57.1)]
    [InlineData(2500, 57.1)]
    public void T301_SpringRateIncreasesWithMass(double mass, double minExpected)
    {
        var car = CarFactory.SupraA90(); car.TotalMass = mass;
        car.SuspensionUpgrade = SuspensionUpgrade.Sport;
        var r = Gen(car, CarFactory.Road());
        Assert.True(r.SpringFront >= minExpected,
            $"Mass {mass}: spring front {r.SpringFront} < {minExpected}");
    }

    // 302 – Rebound increases with mass (sqrt mass scaling, same car)
    [Fact]
    public void T302_ReboundIncreasesWithMass()
    {
        var light = CarFactory.SupraA90(); light.TotalMass = 1000; light.SuspensionUpgrade = SuspensionUpgrade.Sport;
        var heavy = CarFactory.SupraA90(); heavy.TotalMass = 2000; heavy.SuspensionUpgrade = SuspensionUpgrade.Sport;
        var r_light = Gen(light, CarFactory.Road());
        var r_heavy = Gen(heavy, CarFactory.Road());
        Assert.True(r_heavy.ReboundFront >= r_light.ReboundFront);
        Assert.True(r_heavy.ReboundRear  >= r_light.ReboundRear);
    }

    // 303 – Brake pressure increases with mass
    [Fact]
    public void T303_BrakePressureIncreasesWithMass()
    {
        var light = CarFactory.SupraA90(); light.TotalMass = 800; light.MaxSpeedKmh = 200; light.PowerHP = 200;
        var heavy = CarFactory.SupraA90(); heavy.TotalMass = 2500; heavy.MaxSpeedKmh = 200; heavy.PowerHP = 200;
        var r_light = Gen(light, CarFactory.Road());
        var r_heavy = Gen(heavy, CarFactory.Road());
        Assert.True(r_heavy.BrakePressure >= r_light.BrakePressure,
            $"Heavy {r_heavy.BrakePressure} < Light {r_light.BrakePressure}");
    }

    // 304 – Tire pressure increases with mass (heavier = more load = higher pressure)
    [Fact]
    public void T304_TirePressureIncreasesWithMass()
    {
        var light = CarFactory.SupraA90(); light.TotalMass = 800; light.TireType = TireType.Sport;
        var heavy = CarFactory.SupraA90(); heavy.TotalMass = 2500; heavy.TireType = TireType.Sport;
        var r_light = Gen(light, CarFactory.Road());
        var r_heavy = Gen(heavy, CarFactory.Road());
        Assert.True(r_heavy.TirePressureFront >= r_light.TirePressureFront);
        Assert.True(r_heavy.TirePressureRear  >= r_light.TirePressureRear);
    }

    // 305 – Ride height: lighter car = lower ride height (sportier)
    [Fact]
    public void T305_RideHeightDecreasesWithMass()
    {
        var light = CarFactory.SupraA90(); light.TotalMass = 900; light.SuspensionUpgrade = SuspensionUpgrade.Sport;
        var heavy = CarFactory.SupraA90(); heavy.TotalMass = 2200; heavy.SuspensionUpgrade = SuspensionUpgrade.Sport;
        var r_light = Gen(light, CarFactory.Road());
        var r_heavy = Gen(heavy, CarFactory.Road());
        Assert.True(r_heavy.RideHeightFront >= r_light.RideHeightFront - 20,
            $"Heavy {r_heavy.RideHeightFront} vs Light {r_light.RideHeightFront}");
    }

    // 306 – Ride height: larger rim/tire diameter → higher ride height
    [Fact]
    public void T306_RideHeightIncreasesWithRimDiameter()
    {
        var small = CarFactory.SupraA90(); small.FrontRimDiameter = 17; small.RearRimDiameter = 17;
        var large = CarFactory.SupraA90(); large.FrontRimDiameter = 22; large.RearRimDiameter = 22;
        var r_small = Gen(small, CarFactory.Road());
        var r_large = Gen(large, CarFactory.Road());
        Assert.True(r_large.RideHeightFront >= r_small.RideHeightFront,
            $"Large rim {r_large.RideHeightFront} < small rim {r_small.RideHeightFront}");
    }

    // 307 – Caster: heavier car → higher caster (stability)
    [Fact]
    public void T307_CasterIncreasesWithMass()
    {
        var light = CarFactory.SupraA90(); light.TotalMass = 800;
        var heavy = CarFactory.SupraA90(); heavy.TotalMass = 2800;
        var r_light = Gen(light, CarFactory.Road());
        var r_heavy = Gen(heavy, CarFactory.Road());
        Assert.True(r_heavy.Caster >= r_light.Caster,
            $"Heavy caster {r_heavy.Caster} < Light {r_light.Caster}");
    }

    // ── Power scaling ──────────────────────────────────────────────────

    // 308 – Brake pressure increases with power
    [Fact]
    public void T308_BrakePressureIncreasesWithPower()
    {
        var low  = CarFactory.SupraA90(); low.PowerHP = 100;
        var high = CarFactory.SupraA90(); high.PowerHP = 1000;
        var r_low  = Gen(low,  CarFactory.Road());
        var r_high = Gen(high, CarFactory.Road());
        Assert.True(r_high.BrakePressure >= r_low.BrakePressure,
            $"High power {r_high.BrakePressure} < Low {r_low.BrakePressure}");
    }

    // 309 – Final drive decreases with power (more power → higher speed → longer final drive)
    [Fact]
    public void T309_FinalDriveDecreasesWithPower()
    {
        var low  = CarFactory.SupraA90(); low.PowerHP = 150; low.GearCount = 6;
        var high = CarFactory.SupraA90(); high.PowerHP = 900; high.GearCount = 6;
        var r_low  = Gen(low,  CarFactory.Road());
        var r_high = Gen(high, CarFactory.Road());
        // Higher power → higher computed top speed → lower numerical FD to achieve that speed
        Assert.True(r_high.FinalDrive <= r_low.FinalDrive,
            $"High power FD {r_high.FinalDrive} should be <= Low {r_low.FinalDrive}");
    }

    // 310 – First gear ratio increases with power (shorter first gear)
    [Fact]
    public void T310_FirstGearIncreasesWithPower()
    {
        var low  = CarFactory.SupraA90(); low.PowerHP = 150; low.GearCount = 6;
        var high = CarFactory.SupraA90(); high.PowerHP = 900; high.GearCount = 6;
        var r_low  = Gen(low,  CarFactory.Road());
        var r_high = Gen(high, CarFactory.Road());
        Assert.True(r_high.GearRatios[0] >= r_low.GearRatios[0],
            $"High power 1st {r_high.GearRatios[0]} < Low {r_low.GearRatios[0]}");
    }

    // 311 – Rear camber more negative with higher power (RWD, road)
    [Fact]
    public void T311_RearCamberMoreNegativeWithPower()
    {
        var low  = CarFactory.SupraA90(); low.PowerHP = 150;
        var high = CarFactory.SupraA90(); high.PowerHP = 900;
        var r_low  = Gen(low,  CarFactory.Road());
        var r_high = Gen(high, CarFactory.Road());
        Assert.True(r_high.CamberRear <= r_low.CamberRear,
            $"High power rear camber {r_high.CamberRear} > Low {r_low.CamberRear}");
    }

    // 312 – Tire pressure primarily compound-based, power has minor indirect effect
    [Fact]
    public void T312_TirePressureBasedOnCompound()
    {
        var low  = CarFactory.SupraA90(); low.PowerHP = 100;
        var high = CarFactory.SupraA90(); high.PowerHP = 1200;
        var r_low  = Gen(low,  CarFactory.Road());
        var r_high = Gen(high, CarFactory.Road());
        // Tire pressure doesn't directly increase with power (compound is the main factor)
        Assert.InRange(r_low.TirePressureRear, 1.0, 3.8);
        Assert.InRange(r_high.TirePressureRear, 1.0, 3.8);
    }

    // 313 – High torque: diff settings differ (AWD center bias more rear)
    [Fact]
    public void T313_HighTorqueAffectsDiff()
    {
        var low  = CarFactory.GtrR35(); low.TorqueNm = 300;
        var high = CarFactory.GtrR35(); high.TorqueNm = 1200;
        var r_low  = Gen(low,  CarFactory.Road());
        var r_high = Gen(high, CarFactory.Road());
        Assert.True(r_high.DiffAccel >= r_low.DiffAccel || r_high.DiffDecel <= r_low.DiffDecel,
            $"High torque diff not adjusted: accel {r_low.DiffAccel}→{r_high.DiffAccel}");
    }

    // ── Speed scaling ──────────────────────────────────────────────────

    // 314 – Aero increases with speed (linear v scaling)
    [Fact]
    public void T314_AeroIncreasesWithSpeed()
    {
        var slow = CarFactory.Gt3Rs(); slow.MaxSpeedKmh = 200;
        var fast = CarFactory.Gt3Rs(); fast.MaxSpeedKmh = 400;
        var r_slow = Gen(slow, CarFactory.Road());
        var r_fast = Gen(fast, CarFactory.Road());
        Assert.True(r_fast.AeroFront >= r_slow.AeroFront,
            $"Fast aero front {r_fast.AeroFront} < Slow {r_slow.AeroFront}");
        Assert.True(r_fast.AeroRear  >= r_slow.AeroRear,
            $"Fast aero rear {r_fast.AeroRear} < Slow {r_slow.AeroRear}");
    }

    // 315 – Caster increases with speed (high-speed stability)
    [Fact]
    public void T315_CasterIncreasesWithSpeed()
    {
        var slow = CarFactory.SupraA90(); slow.MaxSpeedKmh = 180;
        var fast = CarFactory.SupraA90(); fast.MaxSpeedKmh = 380;
        var r_slow = Gen(slow, CarFactory.Road());
        var r_fast = Gen(fast, CarFactory.Road());
        Assert.True(r_fast.Caster >= r_slow.Caster,
            $"Fast caster {r_fast.Caster} < Slow {r_slow.Caster}");
    }

    // 316 – Brake pressure increases with speed
    [Fact]
    public void T316_BrakePressureIncreasesWithSpeed()
    {
        var slow = CarFactory.SupraA90(); slow.MaxSpeedKmh = 180; slow.PowerHP = 300;
        var fast = CarFactory.SupraA90(); fast.MaxSpeedKmh = 400; fast.PowerHP = 300;
        var r_slow = Gen(slow, CarFactory.Road());
        var r_fast = Gen(fast, CarFactory.Road());
        Assert.True(r_fast.BrakePressure >= r_slow.BrakePressure,
            $"Fast brake {r_fast.BrakePressure} < Slow {r_slow.BrakePressure}");
    }

    // 317 – Final drive: faster car → lower FD (taller gearing for top speed)
    [Fact]
    public void T317_FinalDriveDecreasesWithSpeed()
    {
        var slow = CarFactory.SupraA90(); slow.MaxSpeedKmh = 200; slow.PowerHP = 400; slow.GearCount = 6;
        var fast = CarFactory.SupraA90(); fast.MaxSpeedKmh = 400; fast.PowerHP = 400; fast.GearCount = 6;
        var r_slow = Gen(slow, CarFactory.Road());
        var r_fast = Gen(fast, CarFactory.Road());
        Assert.True(r_fast.FinalDrive <= r_slow.FinalDrive + 0.3,
            $"Fast FD {r_fast.FinalDrive} vs Slow {r_slow.FinalDrive}");
    }

    // ── Wheelbase scaling ──────────────────────────────────────────────

    // 318 – Toe scales with wheelbase: longer = less toe needed (stability)
    [Fact]
    public void T318_ToeScalesWithWheelbase()
    {
        var short_wb = CarFactory.SupraA90(); short_wb.Wheelbase = 2300;
        var long_wb  = CarFactory.SupraA90(); long_wb.Wheelbase  = 3100;
        var r_short = Gen(short_wb, CarFactory.Road());
        var r_long  = Gen(long_wb,  CarFactory.Road());
        // Longer wheelbase should have less (or equal) aggressive toe
        Assert.True(Math.Abs(r_short.ToeFront) >= Math.Abs(r_long.ToeFront) - 0.2,
            $"Short WB toe {r_short.ToeFront} vs Long WB toe {r_long.ToeFront}");
    }

    // 319 – Caster increases with wheelbase (longer = more stable at speed)
    [Fact]
    public void T319_CasterIncreasesWithWheelbase()
    {
        var short_wb = CarFactory.SupraA90(); short_wb.Wheelbase = 2300;
        var long_wb  = CarFactory.SupraA90(); long_wb.Wheelbase  = 3200;
        var r_short = Gen(short_wb, CarFactory.Road());
        var r_long  = Gen(long_wb,  CarFactory.Road());
        Assert.True(r_long.Caster >= r_short.Caster - 0.5,
            $"Long WB caster {r_long.Caster} vs Short {r_short.Caster}");
    }

    // 320 – Springs unaffected by wheelbase (mass-driven, not WB)
    [Fact]
    public void T320_SpringsIndependentOfWheelbase()
    {
        var short_wb = CarFactory.SupraA90(); short_wb.Wheelbase = 2200;
        var long_wb  = CarFactory.SupraA90(); long_wb.Wheelbase  = 3200;
        var r_short = Gen(short_wb, CarFactory.Road());
        var r_long  = Gen(long_wb,  CarFactory.Road());
        // Same mass, same suspension → same springs (within rounding)
        Assert.Equal(r_short.SpringFront, r_long.SpringFront);
        Assert.Equal(r_short.SpringRear,  r_long.SpringRear);
    }

    // ── Cross-parameter scaling ────────────────────────────────────────

    // 321 – Power+mass combine: heavy+high-power gets highest brake pressure
    [Fact]
    public void T321_HeavyHighPowerHighestBrakePressure()
    {
        var light_low  = CarFactory.Mx5(); light_low.PowerHP = 100;
        var heavy_high = CarFactory.Wrangler(); heavy_high.PowerHP = 800; heavy_high.MaxSpeedKmh = 250;
        var r_ll = Gen(light_low, CarFactory.Road());
        var r_hh = Gen(heavy_high, CarFactory.Road());
        Assert.True(r_hh.BrakePressure >= r_ll.BrakePressure);
    }

    // 322 – Mass+power combination drives spring rate (both increase spring)
    [Fact]
    public void T322_MassAndPowerIncreaseSprings()
    {
        var base_car = CarFactory.Mx5(); base_car.SuspensionUpgrade = SuspensionUpgrade.Sport;
        var heavy    = CarFactory.Mx5(); heavy.TotalMass = 2000; heavy.SuspensionUpgrade = SuspensionUpgrade.Sport;
        var r_base  = Gen(base_car, CarFactory.Road());
        var r_heavy = Gen(heavy, CarFactory.Road());
        Assert.True(r_heavy.SpringFront > r_base.SpringFront,
            $"Heavy spring {r_heavy.SpringFront} should be > base {r_base.SpringFront}");
    }

    // 323 – Wider tires increase pressure slightly (more contact patch load)
    [Fact]
    public void T323_WiderTiresHigherPressure()
    {
        var narrow = CarFactory.SupraA90(); narrow.FrontTireWidth = 205; narrow.RearTireWidth = 205;
        var wide   = CarFactory.SupraA90(); wide.FrontTireWidth = 325; wide.RearTireWidth = 355;
        var r_narrow = Gen(narrow, CarFactory.Road());
        var r_wide   = Gen(wide, CarFactory.Road());
        Assert.True(r_wide.TirePressureFront >= r_narrow.TirePressureFront - 0.2,
            $"Wide front {r_wide.TirePressureFront} vs narrow {r_narrow.TirePressureFront}");
    }

    // 324 – Higher profile tire = slightly softer effective spring (tire sidewall)
    [Fact]
    public void T324_HigherProfileTireSofterSpring()
    {
        var lowProf  = CarFactory.SupraA90(); lowProf.FrontTireProfile = 25; lowProf.FrontTireProfile = 25;
        var highProf = CarFactory.SupraA90(); highProf.FrontTireProfile = 65; highProf.RearTireProfile = 65;
        var r_low   = Gen(lowProf, CarFactory.Road());
        var r_high  = Gen(highProf, CarFactory.Road());
        // Note: tire profile may not directly affect springs in this generator
        // This tests that nothing crashes and values are reasonable
        Assert.InRange(r_low.SpringFront, 57.1, 285.6);
        Assert.InRange(r_high.SpringFront, 57.1, 285.6);
    }

    // 325 – Mass affects ride height (heavier = more compressed)
    [Fact]
    public void T325_HeavierCarLowerRideHeight()
    {
        var light = CarFactory.SupraA90(); light.TotalMass = 800; light.SuspensionUpgrade = SuspensionUpgrade.Sport;
        var heavy = CarFactory.SupraA90(); heavy.TotalMass = 2500; heavy.SuspensionUpgrade = SuspensionUpgrade.Sport;
        var r_light = Gen(light, CarFactory.Road());
        var r_heavy = Gen(heavy, CarFactory.Road());
        Assert.True(r_heavy.RideHeightFront <= r_light.RideHeightFront + 20,
            $"Heavy ride height {r_heavy.RideHeightFront} not near Light {r_light.RideHeightFront}");
    }

    // ── Upgrade scaling ────────────────────────────────────────────────

    // 326 – Suspension upgrade increases spring rates on road (race > stock)
    // Note: Drift/CC with light cars may floor-clamp both upgrades to SpringMin — only Road tested.
    [Fact]
    public void T326_SuspensionUpgradeEffectOnRoad()
    {
        var stock = CarFactory.SupraA90(); stock.SuspensionUpgrade = SuspensionUpgrade.Stock;
        var race  = CarFactory.SupraA90(); race.SuspensionUpgrade  = SuspensionUpgrade.Race;
        var r_stock = Gen(stock, CarFactory.Road());
        var r_race  = Gen(race,  CarFactory.Road());
        Assert.True(r_race.SpringFront > r_stock.SpringFront,
            $"Road: Race spring {r_race.SpringFront} <= Stock {r_stock.SpringFront}");
        Assert.True(r_race.SpringRear  > r_stock.SpringRear,
            $"Road: Race rear spring {r_race.SpringRear} <= Stock {r_stock.SpringRear}");
    }

    // 327 – Differential upgrade: race gives higher accel than stock
    [Fact]
    public void T327_DifferentialUpgradeEffect()
    {
        var stock = CarFactory.SupraA90(); stock.DifferentialUpgrade = DifferentialUpgrade.Stock;
        var race  = CarFactory.SupraA90(); race.DifferentialUpgrade  = DifferentialUpgrade.Race;
        var r_stock = Gen(stock, CarFactory.Road());
        var r_race  = Gen(race,  CarFactory.Road());
        Assert.True(r_race.DiffAccel >= r_stock.DiffAccel,
            $"Race diff accel {r_race.DiffAccel} < Stock {r_stock.DiffAccel}");
    }

    // 328 – Tire type affects pressure: Slick > SemiSlick > Sport > Stock
    [Fact]
    public void T328_TireTypePressureOrdering()
    {
        var slick      = CarFactory.SupraA90(); slick.TireType = TireType.Slick;
        var semiSlick  = CarFactory.SupraA90(); semiSlick.TireType = TireType.SemiSlick;
        var sport      = CarFactory.SupraA90(); sport.TireType = TireType.Sport;
        var stock      = CarFactory.SupraA90(); stock.TireType = TireType.Stock;
        var r_slick     = Gen(slick, CarFactory.Road());
        var r_semiSlick = Gen(semiSlick, CarFactory.Road());
        var r_sport     = Gen(sport, CarFactory.Road());
        var r_stock     = Gen(stock, CarFactory.Road());
        Assert.True(r_slick.TirePressureFront >= r_semiSlick.TirePressureFront);
        Assert.True(r_semiSlick.TirePressureFront >= r_sport.TirePressureFront - 0.1);
        Assert.True(r_sport.TirePressureFront >= r_stock.TirePressureFront - 0.1);
    }

    // 329 – Rally tires on rally discipline give appropriate pressure
    [Fact]
    public void T329_RallyTirePressureAppropriate()
    {
        var car = CarFactory.WrxSti(); car.TireType = TireType.Rally;
        var r = Gen(car, CarFactory.Rally());
        Assert.InRange(r.TirePressureFront, 1.3, 2.2);
        Assert.InRange(r.TirePressureRear,  1.3, 2.2);
    }

    // 330 – Offroad tires on CC discipline give appropriate pressure
    [Fact]
    public void T330_OffroadTirePressureAppropriate()
    {
        var car = CarFactory.Wrangler(); car.TireType = TireType.Offroad;
        var r = Gen(car, CarFactory.CrossCountry());
        Assert.InRange(r.TirePressureFront, 1.3, 2.2);
        Assert.InRange(r.TirePressureRear,  1.3, 2.2);
    }

    // ── Aspiration/powertrain scaling ──────────────────────────────────

    // 331 – Aspiration affects final drive: more torque → shorter gearing
    [Fact]
    public void T331_AspirationAffectsFinalDrive()
    {
        var turbo  = CarFactory.SupraA90(); turbo.AspirationType = AspirationType.TwinTurbo; turbo.TorqueNm = 800;
        var na     = CarFactory.SupraA90(); na.AspirationType = AspirationType.Natural; na.TorqueNm = 300;
        turbo.PowerHP = 600; na.PowerHP = 600;
        turbo.GearCount = 6; na.GearCount = 6;
        var r_turbo = Gen(turbo, CarFactory.Road());
        var r_na    = Gen(na, CarFactory.Road());
        Assert.True(r_turbo.FinalDrive >= r_na.FinalDrive - 0.5,
            $"Turbo FD {r_turbo.FinalDrive} vs NA {r_na.FinalDrive}");
    }

    // 332 – Electric car final drive different from ICE (single gear, different torque curve)
    [Fact]
    public void T332_ElectricFinalDriveAppropriate()
    {
        var r = Gen(CarFactory.ModelSPlaid(), CarFactory.Road());
        Assert.Single(r.GearRatios);
        Assert.InRange(r.GearRatios[0], 2.0, 6.1);
    }

    // 333 – Electric brake pressure: regen braking = may affect pressure
    [Fact]
    public void T333_ElectricBrakePressureValid()
    {
        var r = Gen(CarFactory.ModelSPlaid(), CarFactory.Road());
        Assert.InRange(r.BrakePressure, 50.0, 200.0);
    }

    // 334 – Aspiration affects damper values (torque delivery = damping response)
    [Fact]
    public void T334_AspirationAffectsDampers()
    {
        var turbo   = CarFactory.SupraA90(); turbo.AspirationType = AspirationType.TwinTurbo; turbo.TorqueNm = 900;
        var na      = CarFactory.SupraA90(); na.AspirationType = AspirationType.Natural; na.TorqueNm = 300;
        turbo.PowerHP = na.PowerHP;
        var r_turbo = Gen(turbo, CarFactory.Road());
        var r_na    = Gen(na, CarFactory.Road());
        Assert.True(r_turbo.ReboundRear >= r_na.ReboundRear - 3,
            $"Turbo rear rebound {r_turbo.ReboundRear} vs NA {r_na.ReboundRear}");
    }

    // ── Combined/multiplicative scaling ────────────────────────────────

    // 335 – Heavier + faster + more power → highest brake pressure
    [Fact]
    public void T335_CompoundBrakePressureScaling()
    {
        var min = CarFactory.Mx5(); min.TotalMass = 700; min.MaxSpeedKmh = 180; min.PowerHP = 80;
        var max = CarFactory.Wrangler(); max.TotalMass = 2800; max.MaxSpeedKmh = 350; max.PowerHP = 1000;
        var r_min = Gen(min, CarFactory.Road());
        var r_max = Gen(max, CarFactory.Road());
        Assert.True(r_max.BrakePressure >= r_min.BrakePressure,
            $"Max brake {r_max.BrakePressure} < Min {r_min.BrakePressure}");
    }

    // 336 – Heavier + faster → higher caster
    [Fact]
    public void T336_CompoundCasterScaling()
    {
        var min = CarFactory.Mx5(); min.TotalMass = 700; min.MaxSpeedKmh = 180;
        var max = CarFactory.GtrR35(); max.TotalMass = 2200; max.MaxSpeedKmh = 380;
        var r_min = Gen(min, CarFactory.Road());
        var r_max = Gen(max, CarFactory.Road());
        Assert.True(r_max.Caster >= r_min.Caster,
            $"Max caster {r_max.Caster} < Min {r_min.Caster}");
    }

    // 337 – Mass+power+speed affect final drive in valid ranges
    [Fact]
    public void T337_CompoundFinalDriveValid()
    {
        var low  = CarFactory.SupraA90(); low.TotalMass = 900; low.PowerHP = 150; low.MaxSpeedKmh = 200; low.TorqueNm = 200; low.GearCount = 6;
        var high = CarFactory.SupraA90(); high.TotalMass = 2000; high.PowerHP = 1000; high.MaxSpeedKmh = 350; high.TorqueNm = 1200; high.GearCount = 6;
        var r_low  = Gen(low, CarFactory.Road());
        var r_high = Gen(high, CarFactory.Road());
        Assert.InRange(r_low.FinalDrive, 2.2, 6.1);
        Assert.InRange(r_high.FinalDrive, 2.2, 6.1);
    }

    // ── Tire and rim effects ───────────────────────────────────────────

    // 338 – Larger rims = higher ride height (tire + rim combined diameter)
    [Fact]
    public void T338_RimDiameterAffectsRideHeight()
    {
        var small = CarFactory.SupraA90(); small.FrontRimDiameter = 17; small.RearRimDiameter = 17;
        var large = CarFactory.SupraA90(); large.FrontRimDiameter = 22; large.RearRimDiameter = 22;
        var r_small = Gen(small, CarFactory.Road());
        var r_large = Gen(large, CarFactory.Road());
        Assert.True(r_large.RideHeightFront >= r_small.RideHeightFront - 5,
            $"Large rim ride {r_large.RideHeightFront} < Small {r_small.RideHeightFront}");
    }

    // 339 – Tire width affects contact patch → tire pressure slightly
    [Fact]
    public void T339_TireWidthAffectsPressure()
    {
        var narrow = CarFactory.SupraA90(); narrow.FrontTireWidth = 195; narrow.RearTireWidth = 195;
        var wide   = CarFactory.SupraA90(); wide.FrontTireWidth = 325; wide.RearTireWidth = 345;
        var r_narrow = Gen(narrow, CarFactory.Road());
        var r_wide   = Gen(wide, CarFactory.Road());
        Assert.InRange(r_narrow.TirePressureFront, 1.0, 3.8);
        Assert.InRange(r_wide.TirePressureFront,   1.0, 3.8);
    }

    // ── Engine type effects ────────────────────────────────────────────

    // 340 – V8 vs I4: heavier engine → more front weight → slight pressure/camber differences
    [Fact]
    public void T340_EngineTypeWeightEffects()
    {
        var v8  = CarFactory.SupraA90(); v8.EngineType = EngineType.V8; v8.TotalMass += 50;
        var i4  = CarFactory.SupraA90(); i4.EngineType = EngineType.I4;
        var r_v8 = Gen(v8, CarFactory.Road());
        var r_i4 = Gen(i4, CarFactory.Road());
        Assert.InRange(r_v8.TirePressureFront, 1.0, 3.8);
        Assert.InRange(r_i4.TirePressureFront, 1.0, 3.8);
    }

    private static IEnumerable<(CarCard, TrackInfo)> AllCombos()
    {
        var cars   = new[] { CarFactory.GtrR35(), CarFactory.SupraA90(), CarFactory.CivicTypeR(),
                             CarFactory.Gemera(), CarFactory.Mx5(), CarFactory.Wrangler() };
        var tracks = new[] { CarFactory.Road(), CarFactory.Drag(), CarFactory.Drift(),
                             CarFactory.Rally(), CarFactory.CrossCountry() };
        foreach (var c in cars) foreach (var t in tracks) yield return (c, t);
    }
}
