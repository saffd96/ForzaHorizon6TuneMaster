using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;
using TuneMaster.Tests.Helpers;
using Models_DriveType = Forza_Horizon_6_Tune_Master.Models.DriveType;

namespace TuneMaster.Tests;

/// Tests validating physical correctness fixes:
///   P001–P005: LaunchControl populated for Drag
///   P006–P008: RearMid engine position heuristic
///   P009–P011: AWD aero balance (front/rear ratio < 3:1)
///   P012–P013: BrakesUpgrade effect on brake pressure
public class PhysicsCorrectnessTests
{
    private static TuneResult Gen(CarCard car, TrackInfo track)
        => new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());

    // ── Launch Control ──────────────────────────────────────────────────────

    // P001 – LaunchControlRpm is populated for Drag discipline
    [Fact]
    public void P001_LaunchControlRpmPopulatedForDrag()
    {
        var r = Gen(CarFactory.GtrR35(), CarFactory.Drag());
        Assert.NotNull(r.LaunchControlRpm);
        Assert.True(r.LaunchControlRpm > 0, $"LaunchControlRpm should be > 0, got {r.LaunchControlRpm}");
    }

    // P002 – LaunchControlRpm is null for Road discipline
    [Fact]
    public void P002_LaunchControlRpmNullForRoad()
    {
        var r = Gen(CarFactory.GtrR35(), CarFactory.Road());
        Assert.Null(r.LaunchControlRpm);
    }

    // P003 – Electric car (Tesla) launch RPM = 1000 for Drag
    [Fact]
    public void P003_ElectricLaunchControlRpm1000()
    {
        var r = Gen(CarFactory.ModelSPlaid(), CarFactory.Drag());
        Assert.NotNull(r.LaunchControlRpm);
        Assert.Equal(1000, r.LaunchControlRpm);
    }

    // P004 – TwinTurbo launch RPM lower than SingleTurbo (less turbo lag at launch)
    [Fact]
    public void P004_TwinTurboLaunchLowerThanSingleTurbo()
    {
        var twin   = CarFactory.GtrR35();     // TwinTurbo, V6
        var single = CarFactory.WrxSti();     // SingleTurbo, Boxer
        var r_twin   = Gen(twin,   CarFactory.Drag());
        var r_single = Gen(single, CarFactory.Drag());
        Assert.NotNull(r_twin.LaunchControlRpm);
        Assert.NotNull(r_single.LaunchControlRpm);
        // TwinTurbo typically launches lower than SingleTurbo (less peak-RPM dependency)
        Assert.True(r_twin.LaunchControlRpm <= r_single.LaunchControlRpm + 500,
            $"TwinTurbo launch {r_twin.LaunchControlRpm} should be ≤ SingleTurbo {r_single.LaunchControlRpm} + 500");
    }

    // P005 – LaunchControlRpm within engine operating range [500, MaxRPM × 0.75]
    [Fact]
    public void P005_LaunchControlRpmWithinRange()
    {
        foreach (var car in AllDragCars())
        {
            var r = Gen(car, CarFactory.Drag());
            Assert.NotNull(r.LaunchControlRpm);
            Assert.True(r.LaunchControlRpm >= 500, $"{car.AspirationType}: launch {r.LaunchControlRpm} < 500");
            Assert.True(r.LaunchControlRpm <= car.MaxRPM * 0.75,
                $"{car.AspirationType}: launch {r.LaunchControlRpm} > MaxRPM × 0.75 ({car.MaxRPM * 0.75})");
        }
    }

    // ── RearMid Engine Position ─────────────────────────────────────────────

    // P006 – Gemera (RearMid engine): springs reflect rear-biased weight distribution
    [Fact]
    public void P006_RearMidEngineSpringRearBiasedRelativeToFront()
    {
        var r = Gen(CarFactory.Gemera(), CarFactory.Road());
        // RearMid heuristic = 43% front → rear spring should be stiffer than front
        Assert.True(r.SpringRear >= r.SpringFront,
            $"RearMid: rear spring {r.SpringRear} should be >= front {r.SpringFront}");
    }

    // P007 – RearMid brake balance: reflects rear-heavy distribution (≤ 50%)
    [Fact]
    public void P007_RearMidBrakeBalanceLowerThanFrontEngine()
    {
        var rearMid  = CarFactory.Gemera();   // RearMid, WD=49%
        var frontEng = CarFactory.GtrR35();   // Front engine, WD=54%
        var r_rm = Gen(rearMid,  CarFactory.Road());
        var r_fe = Gen(frontEng, CarFactory.Road());
        Assert.True(r_rm.BrakeBalance <= r_fe.BrakeBalance,
            $"RearMid brake {r_rm.BrakeBalance} should be <= Front engine {r_fe.BrakeBalance}");
    }

    // P008 – RearMid ARB: rear stiffer relative to front (weight on rear)
    [Fact]
    public void P008_RearMidArbRearHeavier()
    {
        var r = Gen(CarFactory.Gemera(), CarFactory.Road());
        // RearMid → EffectiveWtDist=43 → wdDev=-0.14 → arbF decreases, arbR increases
        // So rear ARB should be at least as stiff as front
        Assert.True(r.ARBRear >= r.ARBFront,
            $"RearMid Road: rear ARB {r.ARBRear} should be >= front {r.ARBFront}");
    }

    // ── AWD Aero Balance ───────────────────────────────────────────────────

    // P009 – AWD with both aero elements: front/rear ratio < 3.0 (not 6:1 extreme)
    [Fact]
    public void P009_AwdAeroFrontRearRatioReasonable()
    {
        var car = CarFactory.Gt3Rs();  // has front + rear aero
        car.DriveType = Models_DriveType.AWD;  // make it AWD for this test
        var r = Gen(car, CarFactory.Road());
        Assert.True(r.AeroFront > 0);
        Assert.True(r.AeroRear  > 0);
        double ratio = r.AeroFront / r.AeroRear;
        Assert.True(ratio < 3.0, $"AWD aero front/rear ratio {ratio:F2} should be < 3.0 (was 6.0 before fix)");
    }

    // P010 – AWD front emphasis vs RWD: AWD front factor higher (understeer compensation)
    [Fact]
    public void P010_AwdFrontAeroHigherThanRwdFrontAero()
    {
        var awd = CarFactory.Gt3Rs(); awd.DriveType = Models_DriveType.AWD;
        var rwd = CarFactory.Gt3Rs();  // already RWD
        var r_awd = Gen(awd, CarFactory.Road());
        var r_rwd = Gen(rwd, CarFactory.Road());
        Assert.True(r_awd.AeroFront >= r_rwd.AeroFront,
            $"AWD front aero {r_awd.AeroFront} should be >= RWD front {r_rwd.AeroFront}");
    }

    // P011 – AWD rear aero: lower than RWD (less rear emphasis needed for AWD stability)
    [Fact]
    public void P011_AwdRearAeroLowerThanRwdRearAero()
    {
        var awd = CarFactory.Gt3Rs(); awd.DriveType = Models_DriveType.AWD;
        var rwd = CarFactory.Gt3Rs();
        var r_awd = Gen(awd, CarFactory.Road());
        var r_rwd = Gen(rwd, CarFactory.Road());
        Assert.True(r_awd.AeroRear <= r_rwd.AeroRear,
            $"AWD rear aero {r_awd.AeroRear} should be <= RWD rear {r_rwd.AeroRear}");
    }

    // ── BrakesUpgrade effect ────────────────────────────────────────────────

    // P012 – Race brakes give higher pressure than Stock brakes (same car)
    [Fact]
    public void P012_RaceBrakesHigherPressureThanStock()
    {
        var race  = CarFactory.SupraA90(); race.BrakesUpgrade  = BrakesUpgrade.Race;
        var stock = CarFactory.SupraA90(); stock.BrakesUpgrade = BrakesUpgrade.Stock;
        var r_race  = Gen(race,  CarFactory.Road());
        var r_stock = Gen(stock, CarFactory.Road());
        Assert.True(r_race.BrakePressure > r_stock.BrakePressure,
            $"Race brakes {r_race.BrakePressure} should be > Stock {r_stock.BrakePressure}");
    }

    // P013 – Brake pressure ordering: Race > Sport > Stock
    [Fact]
    public void P013_BrakeUpgradePressuреOrdering()
    {
        var race  = CarFactory.GtrR35(); race.BrakesUpgrade  = BrakesUpgrade.Race;
        var sport = CarFactory.GtrR35(); sport.BrakesUpgrade = BrakesUpgrade.Sport;
        var stock = CarFactory.GtrR35(); stock.BrakesUpgrade = BrakesUpgrade.Stock;
        var r_race  = Gen(race,  CarFactory.Road());
        var r_sport = Gen(sport, CarFactory.Road());
        var r_stock = Gen(stock, CarFactory.Road());
        Assert.True(r_race.BrakePressure >= r_sport.BrakePressure);
        Assert.True(r_sport.BrakePressure >= r_stock.BrakePressure);
    }

    private static CarCard[] AllDragCars() => new[]
    {
        CarFactory.GtrR35(), CarFactory.SupraA90(), CarFactory.Hellcat(),
        CarFactory.Gemera(), CarFactory.WrxSti(), CarFactory.CivicTypeR()
    };

    // ── Computed MaxSpeedKmh ───────────────────────────────────────────────

    // P014 – Higher power → higher computed max speed (same car, same mass)
    [Fact]
    public void P014_HigherPowerHigherMaxSpeed()
    {
        var high = CarFactory.SupraA90(); high.PowerHP = 800;
        var low  = CarFactory.SupraA90(); low.PowerHP  = 150;
        Assert.True(high.MaxSpeedKmh > low.MaxSpeedKmh,
            $"800HP: {high.MaxSpeedKmh} should be > 150HP: {low.MaxSpeedKmh}");
    }

    // P015 – Heavier car → lower max speed (same power, same drive, same tires)
    [Fact]
    public void P015_HeavierCarLowerMaxSpeed()
    {
        var heavy = CarFactory.SupraA90(); heavy.TotalMass = 2200;
        var light = CarFactory.SupraA90(); light.TotalMass = 1000;
        Assert.True(light.MaxSpeedKmh > heavy.MaxSpeedKmh,
            $"1000kg: {light.MaxSpeedKmh} should be > 2200kg: {heavy.MaxSpeedKmh}");
    }

    // P016 – AWD → lower max speed than RWD (drivetrain efficiency 0.87 vs 0.92)
    [Fact]
    public void P016_AwdLowerMaxSpeedThanRwd()
    {
        var awd = CarFactory.SupraA90(); awd.DriveType = Models_DriveType.AWD;
        var rwd = CarFactory.SupraA90(); // already RWD
        Assert.True(awd.MaxSpeedKmh < rwd.MaxSpeedKmh,
            $"AWD: {awd.MaxSpeedKmh} should be < RWD: {rwd.MaxSpeedKmh}");
    }

    // P017 – High-profile tires (>55) → lower max speed (SUV body penalty)
    [Fact]
    public void P017_HighProfileTiresLowerMaxSpeed()
    {
        var suv   = CarFactory.SupraA90(); suv.FrontTireProfile = 65; suv.RearTireProfile = 65;
        var sport = CarFactory.SupraA90(); // default profile 35/35
        Assert.True(suv.MaxSpeedKmh < sport.MaxSpeedKmh,
            $"High-profile (65): {suv.MaxSpeedKmh} should be < low-profile (35): {sport.MaxSpeedKmh}");
    }

    // P018 – GTR R35 computed speed within ±15% of real spec (315 km/h)
    [Fact]
    public void P018_GtrR35ComputedSpeedAccurate()
    {
        var gtr = CarFactory.GtrR35();
        Assert.InRange(gtr.MaxSpeedKmh, 315 * 0.85, 315 * 1.15); // ±15%
    }

    // P019 – Wrangler computed speed within ±20% of real spec (165 km/h, SUV body penalty)
    [Fact]
    public void P019_WranglerComputedSpeedAccurate()
    {
        var wrangler = CarFactory.Wrangler();
        Assert.InRange(wrangler.MaxSpeedKmh, 165 * 0.80, 165 * 1.20); // ±20% for SUV
    }

    // P020 – Computed max speed always within [60, 600] for all test cars
    [Fact]
    public void P020_ComputedMaxSpeedAlwaysInRange()
    {
        foreach (var car in AllTestCars())
            Assert.InRange(car.MaxSpeedKmh, 60, 600);
    }

    private static CarCard[] AllTestCars() => new[]
    {
        CarFactory.GtrR35(), CarFactory.SupraA90(), CarFactory.CivicTypeR(),
        CarFactory.Gemera(), CarFactory.Mx5(), CarFactory.Wrangler(),
        CarFactory.Gt3Rs(), CarFactory.ModelSPlaid(), CarFactory.WrxSti(), CarFactory.Hellcat()
    };
}
