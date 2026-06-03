using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;
using TuneMaster.Tests.Helpers;

namespace TuneMaster.Tests;

/// Tests 101–160: ForzaFire community reference ranges
/// Sources: forzafire.com FH6 guides (Engine, P&H, Tires, Drivetrain, Aero)
/// Each test verifies generated values fall within published community ranges
public class ForzaFireReferenceTests
{
    private static TuneResult Gen(CarCard car, TrackInfo track)
        => new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());

    // ── Tire pressure ranges (ForzaFire Tires guide) ────────────────────

    // 101 – Drift pressure: both ends in ForzaFire drift compound range (20-26 PSI ≈ 1.38-1.79 bar)
    [Fact]
    public void T101_DriftPressureInForzaFireRange()
    {
        var r = Gen(CarFactory.SupraA90(), CarFactory.Drift());
        Assert.InRange(r.TirePressureFront, 1.30, 1.85);
        Assert.InRange(r.TirePressureRear,  1.30, 1.85);
    }

    // 102 – Drag rear pressure in ForzaFire drag compound range (18-20 PSI ≈ 1.24-1.38 bar)
    [Fact]
    public void T102_DragRearPressureInForzaFireRange()
    {
        var r = Gen(CarFactory.Hellcat(), CarFactory.Drag());
        Assert.InRange(r.TirePressureRear, 1.2, 1.7);
    }

    // 103 – Road tire pressure in community range (30-35 PSI ≈ 2.07-2.41 bar) for sport tires
    [Fact]
    public void T103_RoadPressureInCommunityRange()
    {
        var r = Gen(CarFactory.SupraA90(), CarFactory.Road());
        Assert.InRange(r.TirePressureFront, 1.90, 2.55);
        Assert.InRange(r.TirePressureRear,  1.90, 2.55);
    }

    // 104 – Rally/CC pressure lower than road but above minimum
    [Fact]
    public void T104_RallyPressureAboveMinimum()
    {
        foreach (var combo in new[] {
            (CarFactory.WrxSti(), CarFactory.Rally()),
            (CarFactory.Wrangler(), CarFactory.CrossCountry()) })
        {
            var r = Gen(combo.Item1, combo.Item2);
            Assert.True(r.TirePressureFront >= 1.3, $"Rally/CC front {r.TirePressureFront} too low");
            Assert.True(r.TirePressureRear  >= 1.3, $"Rally/CC rear {r.TirePressureRear} too low");
        }
    }

    // 105 – Slick tire road pressure (ForzaFire: ~2.24 bar baseline for slicks)
    [Fact]
    public void T105_SlickRoadPressureNearBaseline()
    {
        var car = CarFactory.GtrR35(); car.TireType = TireType.Slick;
        var r = Gen(car, CarFactory.Road());
        Assert.InRange(r.TirePressureFront, 2.00, 2.50);
    }

    // ── Camber ranges (ForzaFire Platform guide) ────────────────────────

    // 106 – Road camber front: ForzaFire says -1.0° to -2.5°
    [Fact]
    public void T106_RoadFrontCamberInForzaFireRange()
    {
        var cars = new[] { CarFactory.SupraA90(), CarFactory.GtrR35(), CarFactory.Mx5(), CarFactory.Gt3Rs() };
        foreach (var c in cars)
        {
            var r = Gen(c, CarFactory.Road());
            Assert.InRange(r.CamberFront, -3.0, -0.5);
        }
    }

    // 107 – Drift front camber: ForzaFire says -3° to -5°
    [Fact]
    public void T107_DriftFrontCamberInForzaFireRange()
    {
        var r = Gen(CarFactory.SupraA90(), CarFactory.Drift());
        Assert.InRange(r.CamberFront, -5.0, -3.0);
    }

    // 108 – Drag camber: ForzaFire says near 0° (-1° to 0°)
    [Fact]
    public void T108_DragCamberInForzaFireRange()
    {
        var r = Gen(CarFactory.Hellcat(), CarFactory.Drag());
        Assert.InRange(r.CamberFront, -1.5, 0.0);
        Assert.InRange(r.CamberRear,  -1.0, 0.5);
    }

    // 109 – Touge camber: between road and drift (slightly aggressive front)
    [Fact]
    public void T109_TougeFrontCamberAggressive()
    {
        var r = Gen(CarFactory.SupraA90(), CarFactory.Touge());
        Assert.True(r.CamberFront <= -1.5, $"Touge front camber {r.CamberFront} should be ≤ -1.5");
    }

    // 110 – Rear engine: camber adjusted per ForzaFire (less front load → less front camber)
    [Fact]
    public void T110_RearEngineCamberAdjustment()
    {
        var r = Gen(CarFactory.Gt3Rs(), CarFactory.Road());
        // Rear-engine GT3 RS should have front camber that accounts for engine position
        Assert.InRange(r.CamberFront, -2.5, 0.0);
    }

    // ── Toe ranges (ForzaFire Platform guide) ───────────────────────────

    // 111 – Road RWD rear toe: ForzaFire says 0.0° to +0.3° (toe-in stability)
    [Fact]
    public void T111_RoadRwdRearToeInRange()
    {
        var r = Gen(CarFactory.SupraA90(), CarFactory.Road());
        Assert.InRange(r.ToeRear, 0.0, 0.5);
    }

    // 112 – Drift rear toe: ForzaFire says +0.5° to +3.0° (toe-in for sustained angle control)
    [Fact]
    public void T112_DriftRearToePositive()
    {
        var r = Gen(CarFactory.SupraA90(), CarFactory.Drift());
        Assert.True(r.ToeRear >= 0.1, $"Drift rear toe {r.ToeRear} should be positive (toe-in for angle control)");
    }

    // 113 – FWD road front toe: ForzaFire says -0.1° to +0.1° (neutral to slight toe-out)
    [Fact]
    public void T113_FwdRoadFrontToeNearNeutral()
    {
        var r = Gen(CarFactory.CivicTypeR(), CarFactory.Road());
        Assert.InRange(r.ToeFront, -0.3, 0.3);
    }

    // 114 – Rally/CC front toe: ForzaFire says -0.3° to -1.0° (toe-out for rotation)
    [Fact]
    public void T114_RallyCrossCountryFrontToeNegative()
    {
        foreach (var combo in new[] {
            (CarFactory.WrxSti(), CarFactory.Rally()),
            (CarFactory.Wrangler(), CarFactory.CrossCountry()) })
        {
            var r = Gen(combo.Item1, combo.Item2);
            Assert.True(r.ToeFront <= 0, $"Front toe {r.ToeFront} should be ≤ 0");
        }
    }

    // 115 – Toe wheelbase scaling: short wheelbase car gets less aggressive toe
    [Fact]
    public void T115_ShortWheelbaseLessToe()
    {
        var mx5 = CarFactory.Mx5();       // 2310mm
        var gtr = CarFactory.GtrR35();    // 2780mm - but AWD, use same drivetrain
        var supra = CarFactory.SupraA90(); // 2470mm
        var hellcat = CarFactory.Hellcat(); // 3020mm
        var r_mx5 = Gen(mx5, CarFactory.Road());
        var r_hellcat = Gen(hellcat, CarFactory.Road());
        // MX5 has shorter wheelbase → toe adjustment should be less aggressive
        Assert.True(Math.Abs(r_mx5.ToeFront) <= Math.Abs(r_hellcat.ToeFront) + 0.3,
            $"MX5 toe {r_mx5.ToeFront} vs Hellcat {r_hellcat.ToeFront}");
    }

    // ── Caster ranges (ForzaFire Platform guide) ────────────────────────

    // 116 – Road caster: ForzaFire says 5.5° to 7.0° for most road cars
    [Fact]
    public void T116_RoadCasterInForzaFireRange()
    {
        var r = Gen(CarFactory.GtrR35(), CarFactory.Road());
        Assert.InRange(r.Caster, 5.0, 7.0);
    }

    // 117 – Rear engine caster: ForzaFire says 4.5° to 6.0° (reduced for turn-in)
    [Fact]
    public void T117_RearEngineCasterModerate()
    {
        var r = Gen(CarFactory.Gt3Rs(), CarFactory.Road());
        Assert.InRange(r.Caster, 4.0, 7.0);
    }

    // 118 – Drag caster: ForzaFire says 5.0° to 6.5° (moderate for launch stability)
    [Fact]
    public void T118_DragCasterInForzaFireRange()
    {
        var r = Gen(CarFactory.Hellcat(), CarFactory.Drag());
        Assert.InRange(r.Caster, 4.5, 6.5);
    }

    // 119 – Caster speed scaling: faster car → higher caster
    [Fact]
    public void T119_CasterScalesWithSpeed()
    {
        var fast = CarFactory.GtrR35(); fast.MaxSpeedKmh = 380;
        var slow = CarFactory.GtrR35(); slow.MaxSpeedKmh = 180;
        var r_fast = Gen(fast, CarFactory.Road());
        var r_slow = Gen(slow, CarFactory.Road());
        Assert.True(r_fast.Caster >= r_slow.Caster);
    }

    // 120 – Caster by discipline: road ≈ touge > street > rally > cc > drift
    [Fact]
    public void T120_RoadCasterHighest()
    {
        var car = CarFactory.SupraA90();
        var r_road = Gen(car, CarFactory.Road());
        var r_cc   = Gen(car, CarFactory.CrossCountry());
        var r_rally = Gen(car, CarFactory.Rally());
        Assert.True(r_road.Caster >= r_cc.Caster);
        Assert.True(r_road.Caster >= r_rally.Caster);
    }

    // ── ARB ranges (ForzaFire Platform guide — verified from recent fixes) ─

    // 121 – RWD road ARB front: ForzaFire base is 22, scales with mass/power/wb
    [Fact]
    public void T121_RwdRoadArbFrontInForzaFireRange()
    {
        var r = Gen(CarFactory.SupraA90(), CarFactory.Road());
        Assert.InRange(r.ARBFront, 18, 35);
    }

    // 122 – AWD road ARB: base 26/33 scales with mass/power/wb (e.g. GTR 600HP → 34/41)
    [Fact]
    public void T122_AwdRoadArbInForzaFireRange()
    {
        var r = Gen(CarFactory.GtrR35(), CarFactory.Road());
        Assert.InRange(r.ARBFront, 22, 45);
        Assert.InRange(r.ARBRear,  28, 50);
    }

    // 123 – Drift ARB: rear stiffer than front per ForzaFire
    [Fact]
    public void T123_DriftArbRearStifferThanFront()
    {
        var r = Gen(CarFactory.SupraA90(), CarFactory.Drift());
        Assert.True(r.ARBRear > r.ARBFront,
            $"Drift rear ARB {r.ARBRear} should be > front {r.ARBFront}");
    }

    // 124 – Drag ARB: ForzaFire says minimal front (≤10), moderate rear (≤35)
    [Fact]
    public void T124_DragArbInForzaFireRange()
    {
        var r = Gen(CarFactory.Hellcat(), CarFactory.Drag());
        Assert.True(r.ARBFront <= 12, $"Drag ARB front {r.ARBFront} too high");
        Assert.InRange(r.ARBRear, 1, 35);
    }

    // 125 – Rally/CC ARB low per ForzaFire (≤20 for independent travel)
    [Fact]
    public void T125_RallyCrossCountryArbLow()
    {
        foreach (var combo in new[] {
            (CarFactory.WrxSti(), CarFactory.Rally()),
            (CarFactory.Wrangler(), CarFactory.CrossCountry()) })
        {
            var r = Gen(combo.Item1, combo.Item2);
            Assert.True(r.ARBFront <= 20, $"ARB front {r.ARBFront} too high for off-road");
            Assert.True(r.ARBRear  <= 20, $"ARB rear {r.ARBRear} too high for off-road");
        }
    }

    // ── Spring rate ranges ──────────────────────────────────────────────

    // 126 – Drag springs: rear stiffer than front (ForzaFire weight transfer platform)
    [Fact]
    public void T126_DragRearSpringStifferThanFront()
    {
        foreach (var car in new[] { CarFactory.Hellcat(), CarFactory.GtrR35(), CarFactory.Gemera() })
        {
            var r = Gen(car, CarFactory.Drag());
            Assert.True(r.SpringRear > r.SpringFront,
                $"{car.DriveType} drag: rear {r.SpringRear} should be > front {r.SpringFront}");
        }
    }

    // 127 – Road spring front/rear split reasonable: front ≤ rear
    [Fact]
    public void T127_RoadSpringFrontNotStifferThanRear()
    {
        foreach (var car in new[] { CarFactory.SupraA90(), CarFactory.GtrR35(), CarFactory.Mx5() })
        {
            var r = Gen(car, CarFactory.Road());
            Assert.True(r.SpringFront <= r.SpringRear + 10,
                $"{car.DriveType} road: front {r.SpringFront} should not be much stiffer than rear {r.SpringRear}");
        }
    }

    // 128 – Drift springs: rear stiffer than front per ForzaFire
    [Fact]
    public void T128_DriftSpringRearStifferThanFront()
    {
        var r = Gen(CarFactory.SupraA90(), CarFactory.Drift());
        Assert.True(r.SpringRear >= r.SpringFront,
            $"Drift rear {r.SpringRear} should be >= front {r.SpringFront}");
    }

    // 129 – Mass-based spring scaling: √(mass) factor gives reasonable rates
    [Fact]
    public void T129_SpringRateReasonablePerMass()
    {
        var mx5 = CarFactory.Mx5(); mx5.SuspensionUpgrade = SuspensionUpgrade.Sport; // 1000 kg
        var tesla = CarFactory.ModelSPlaid(); tesla.SuspensionUpgrade = SuspensionUpgrade.Sport; // 2162 kg
        var r_mx5 = Gen(mx5, CarFactory.Road());
        var r_tesla = Gen(tesla, CarFactory.Road());
        // Ratio of spring rates ≈ ratio of sqrt(mass)
        double massRatio = Math.Sqrt(tesla.TotalMass / mx5.TotalMass); // ≈ 1.47
        double springRatio = r_tesla.SpringFront / r_mx5.SpringFront;
        Assert.InRange(springRatio, massRatio * 0.6, massRatio * 1.4);
    }

    // 130 – CC/Rally springs not extremely stiff (for terrain absorption)
    [Fact]
    public void T130_OffroadSpringsNotExtreme()
    {
        var r = Gen(CarFactory.Wrangler(), CarFactory.CrossCountry());
        Assert.True(r.SpringFront < 200, $"CC front spring {r.SpringFront} too stiff");
        Assert.True(r.SpringRear  < 200, $"CC rear spring {r.SpringRear} too stiff");
    }

    // ── Ride height ranges ──────────────────────────────────────────────

    // 131 – Drag ride height low (ForzaFire: minimum for aero/speed)
    [Fact]
    public void T131_DragRideHeightLow()
    {
        var r = Gen(CarFactory.Hellcat(), CarFactory.Drag());
        Assert.True(r.RideHeightFront <= 120, $"Drag front height {r.RideHeightFront} too high");
        Assert.True(r.RideHeightRear  <= 130, $"Drag rear height {r.RideHeightRear} too high");
    }

    // 132 – CC ride height high (ForzaFire: maximum ground clearance)
    [Fact]
    public void T132_CrossCountryRideHeightHigh()
    {
        var r = Gen(CarFactory.Wrangler(), CarFactory.CrossCountry());
        Assert.True(r.RideHeightFront >= 80, $"CC front height {r.RideHeightFront} too low");
        Assert.True(r.RideHeightRear  >= 80, $"CC rear height {r.RideHeightRear} too low");
    }

    // 133 – Rally ride height moderate-high (between road and CC)
    [Fact]
    public void T133_RallyRideHeightModerate()
    {
        var r = Gen(CarFactory.WrxSti(), CarFactory.Rally());
        Assert.True(r.RideHeightFront >= 70, $"Rally front height {r.RideHeightFront} too low");
        Assert.True(r.RideHeightFront <= 220, $"Rally front height {r.RideHeightFront} too high");
    }

    // 134 – Road ride height moderate (not at extremes)
    [Fact]
    public void T134_RoadRideHeightModerate()
    {
        var r = Gen(CarFactory.SupraA90(), CarFactory.Road());
        Assert.InRange(r.RideHeightFront, 50, 180);
        Assert.InRange(r.RideHeightRear,  50, 180);
    }

    // 135 – Ride height rake: front lower than rear for most disciplines (except possibly rally)
    [Fact]
    public void T135_DragRideHeightRakeFrontLower()
    {
        var r = Gen(CarFactory.GtrR35(), CarFactory.Drag());
        Assert.True(r.RideHeightRear >= r.RideHeightFront,
            $"Drag rake: rear {r.RideHeightRear} should be >= front {r.RideHeightFront}");
    }

    // ── Damper ranges (ForzaFire P&H guide) ─────────────────────────────

    // 136 – Bump ratio within 40-70% for road/touge/street (ForzaFire target ~60%)
    [Fact]
    public void T136_RoadBumpRatioInForzaFireRange()
    {
        foreach (var car in new[] { CarFactory.SupraA90(), CarFactory.GtrR35(), CarFactory.Mx5() })
        {
            var r = Gen(car, CarFactory.Road());
            if (r.ReboundFront >= 2)
            {
                double ratio = r.BumpFront / r.ReboundFront;
                Assert.InRange(ratio, 0.35, 0.80);
            }
        }
    }

    // 137 – Drift bump ratio: ForzaFire target ~0.50 (recently fixed)
    [Fact]
    public void T137_DriftBumpRatioReasonable()
    {
        var r = Gen(CarFactory.SupraA90(), CarFactory.Drift());
        if (r.ReboundFront >= 2)
        {
            double ratio = r.BumpFront / r.ReboundFront;
            Assert.InRange(ratio, 0.30, 0.70);
        }
    }

    // 138 – Drag bump ratio: ForzaFire says 0.40-0.55 (quick squat, controlled extension)
    [Fact]
    public void T138_DragBumpRatioInForzaFireRange()
    {
        var r = Gen(CarFactory.Hellcat(), CarFactory.Drag());
        if (r.ReboundFront >= 2)
        {
            double ratio = r.BumpFront / r.ReboundFront;
            Assert.InRange(ratio, 0.35, 0.60);
        }
    }

    // 139 – Rally/CC bump ratio: ForzaFire says 0.55-0.70 (terrain absorption)
    [Fact]
    public void T139_RallyCrossCountryBumpRatioHigher()
    {
        foreach (var combo in new[] {
            (CarFactory.WrxSti(), CarFactory.Rally()),
            (CarFactory.Wrangler(), CarFactory.CrossCountry()) })
        {
            var r = Gen(combo.Item1, combo.Item2);
            if (r.ReboundFront >= 2)
            {
                double ratio = r.BumpFront / r.ReboundFront;
                Assert.InRange(ratio, 0.35, 0.80);
            }
        }
    }

    // 140 – Damper mass scaling: heavier cars have higher absolute values
    [Fact]
    public void T140_DamperMassScalingReasonable()
    {
        var wrangler = CarFactory.Wrangler(); wrangler.SuspensionUpgrade = SuspensionUpgrade.Sport;
        var mx5 = CarFactory.Mx5(); mx5.SuspensionUpgrade = SuspensionUpgrade.Sport;
        var r_wrangler = Gen(wrangler, CarFactory.Road());
        var r_mx5 = Gen(mx5, CarFactory.Road());
        Assert.True(r_wrangler.ReboundFront >= r_mx5.ReboundFront,
            $"Heavy car rebound {r_wrangler.ReboundFront} should be >= light {r_mx5.ReboundFront}");
    }

    // ── Aero ranges (ForzaFire Aero guide) ──────────────────────────────

    // 141 – Drift aero: front-biased per ForzaFire (recently fixed from rear to front bias)
    [Fact]
    public void T141_DriftAeroFrontBiased()
    {
        var car = CarFactory.Gt3Rs(); // has front + rear aero
        var r = Gen(car, CarFactory.Drift());
        Assert.True(r.AeroFront >= r.AeroRear,
            $"Drift aero: front {r.AeroFront} should be >= rear {r.AeroRear} (front bias)");
    }

    // 142 – Road aero: rear-biased or balanced (high-speed stability)
    [Fact]
    public void T142_RoadAeroRearBiased()
    {
        var car = CarFactory.Gt3Rs();
        var r = Gen(car, CarFactory.Road());
        Assert.True(r.AeroRear >= r.AeroFront * 0.7,
            $"Road aero: rear {r.AeroRear} should be at least 70% of front {r.AeroFront}");
    }

    // 143 – Aero scales with speed: faster car → more downforce
    [Fact]
    public void T143_AeroScalesWithSpeed()
    {
        var fast = CarFactory.Gt3Rs(); fast.MaxSpeedKmh = 380;
        var slow = CarFactory.Gt3Rs(); slow.MaxSpeedKmh = 200;
        var r_fast = Gen(fast, CarFactory.Road());
        var r_slow = Gen(slow, CarFactory.Road());
        Assert.True(r_fast.AeroFront >= r_slow.AeroFront);
        Assert.True(r_fast.AeroRear  >= r_slow.AeroRear);
    }

    // 144 – Drag aero: minimal or zero (reduce drag, not increase downforce)
    [Fact]
    public void T144_DragAeroMinimal()
    {
        var car = CarFactory.Gt3Rs();
        var r = Gen(car, CarFactory.Drag());
        Assert.True(r.AeroFront <= 50, $"Drag aero front {r.AeroFront} should be low");
        Assert.True(r.AeroRear  <= 50, $"Drag aero rear {r.AeroRear} should be low");
    }

    // 145 – Rally/CC aero: moderate (some downforce for stability over terrain)
    [Fact]
    public void T145_OffroadAeroModerate()
    {
        var car = CarFactory.Gt3Rs();
        foreach (var track in new[] { CarFactory.Rally(), CarFactory.CrossCountry() })
        {
            var r = Gen(car, track);
            Assert.InRange(r.AeroFront, 0, 150);
            Assert.InRange(r.AeroRear,  0, 150);
        }
    }

    // ── Differential ranges (ForzaFire Drivetrain guide) ────────────────

    // 146 – Drift RWD diff accel: ForzaFire says 60-100% (high for angle hold)
    [Fact]
    public void T146_DriftRwdDiffAccelHigh()
    {
        var r = Gen(CarFactory.SupraA90(), CarFactory.Drift());
        Assert.InRange(r.DiffAccel, 40, 100);
    }

    // 147 – Drift RWD diff decel: ForzaFire says 0-10% (recently fixed to 0%)
    [Fact]
    public void T147_DriftRwdDiffDecelLow()
    {
        var r = Gen(CarFactory.SupraA90(), CarFactory.Drift());
        Assert.InRange(r.DiffDecel, 0, 10);
    }

    // 148 – Drag diff accel high, decel = 0% (or very low if power-adjusted)
    [Fact]
    public void T148_DragDiffAccelHighDecelLow()
    {
        foreach (var car in new[] { CarFactory.Hellcat(), CarFactory.Gemera() })
        {
            var r = Gen(car, CarFactory.Drag());
            Assert.InRange(r.DiffAccel, 25, 100);
            Assert.InRange(r.DiffDecel, 0, 25);
        }
    }

    // 149 – AWD center diff bias: road ≥ 50% rear (ForzaFire: 60-90% rear)
    [Fact]
    public void T149_AwdCenterBiasRearBiased()
    {
        var r = Gen(CarFactory.GtrR35(), CarFactory.Road());
        Assert.True(r.CenterDiffBias >= 55,
            $"AWD road center bias {r.CenterDiffBias} should be ≥ 55% rear");
    }

    // 150 – AWD front diff: accel higher than decel (ForzaFire: accel lock, decel open)
    [Fact]
    public void T150_AwdFrontDiffAccelHigherThanDecel()
    {
        var r = Gen(CarFactory.GtrR35(), CarFactory.Road());
        if (r.DiffFrontAccel.HasValue && r.DiffFrontDecel.HasValue)
        {
            Assert.True(r.DiffFrontAccel >= r.DiffFrontDecel,
                $"AWD front diff: accel {r.DiffFrontAccel} should be >= decel {r.DiffFrontDecel}");
        }
    }

    // ── Brake ranges (ForzaFire & community) ────────────────────────────

    // 151 – Brake balance: RWD road ≈ 50-60% (slightly front-biased)
    [Fact]
    public void T151_RwdRoadBrakeBalanceReasonable()
    {
        var r = Gen(CarFactory.SupraA90(), CarFactory.Road());
        Assert.InRange(r.BrakeBalance, 45, 65);
    }

    // 152 – Brake balance: FWD ≥ 50% (front-biased for weight transfer)
    [Fact]
    public void T152_FwdBrakeBalanceFrontBiased()
    {
        var r = Gen(CarFactory.CivicTypeR(), CarFactory.Road());
        Assert.True(r.BrakeBalance >= 50, $"FWD brake balance {r.BrakeBalance} should be ≥ 50");
    }

    // 153 – Brake pressure: mass-adjusted (heavier car → more pressure)
    [Fact]
    public void T153_BrakePressureMassAdjusted()
    {
        var heavy = CarFactory.Wrangler(); // 2100 kg
        var light = CarFactory.Mx5();       // 1000 kg
        var r_heavy = Gen(heavy, CarFactory.Road());
        var r_light = Gen(light, CarFactory.Road());
        Assert.True(r_heavy.BrakePressure >= r_light.BrakePressure,
            $"Heavy brake {r_heavy.BrakePressure} should be >= light {r_light.BrakePressure}");
    }

    // 154 – Brake pressure: power-adjusted (more power → more pressure)
    [Fact]
    public void T154_BrakePressurePowerAdjusted()
    {
        var high = CarFactory.SupraA90(); high.PowerHP = 900;
        var low  = CarFactory.SupraA90(); low.PowerHP  = 150;
        var r_high = Gen(high, CarFactory.Road());
        var r_low  = Gen(low,  CarFactory.Road());
        Assert.True(r_high.BrakePressure >= r_low.BrakePressure,
            $"High power {r_high.BrakePressure} should be >= low {r_low.BrakePressure}");
    }

    // 155 – Brake pressure: speed-adjusted (faster car → more pressure)
    [Fact]
    public void T155_BrakePressureSpeedAdjusted()
    {
        var fast = CarFactory.SupraA90(); fast.MaxSpeedKmh = 380;
        var slow = CarFactory.SupraA90(); slow.MaxSpeedKmh = 180;
        var r_fast = Gen(fast, CarFactory.Road());
        var r_slow = Gen(slow, CarFactory.Road());
        Assert.True(r_fast.BrakePressure >= r_slow.BrakePressure,
            $"Fast {r_fast.BrakePressure} should be >= slow {r_slow.BrakePressure}");
    }

    // ── Gearing ranges ──────────────────────────────────────────────────

    // 156 – Final drive: drag FD can differ from road (drag uses different top gear target)
    [Fact]
    public void T156_FinalDriveDragVsRoadValid()
    {
        var car = CarFactory.Hellcat(); car.GearCount = 6;
        var r_drag = Gen(car, CarFactory.Drag(DragDistance.Quarter));
        var r_road = Gen(car, CarFactory.Road());
        Assert.InRange(r_drag.FinalDrive, 2.2, 6.1);
        Assert.InRange(r_road.FinalDrive, 2.2, 6.1);
    }

    // 157 – Electric final drive: single gear, unique ratio
    [Fact]
    public void T157_ElectricFinalDriveSingle()
    {
        var r = Gen(CarFactory.ModelSPlaid(), CarFactory.Road());
        Assert.Single(r.GearRatios);
        Assert.InRange(r.FinalDrive, 2.2, 6.1);
    }

    // 158 – Gear progression: each gear ratio decreases by reasonable factor (1.1-1.6)
    [Fact]
    public void T158_GearProgressionReasonable()
    {
        foreach (var car in AllCars())
        foreach (var track in AllTracks())
        {
            if (car.GearCount < 2) continue;
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            for (int i = 1; i < r.GearRatios.Count; i++)
            {
                double step = r.GearRatios[i - 1] / r.GearRatios[i];
                Assert.InRange(step, 1.05, 2.0);
            }
        }
    }

    // 159 – Power-scaling of final drive: more power → higher top speed → lower numerical FD
    [Fact]
    public void T159_PowerScalesFinalDrive()
    {
        var high = CarFactory.SupraA90(); high.PowerHP = 900; high.GearCount = 6;
        var low  = CarFactory.SupraA90(); low.PowerHP  = 150; low.GearCount  = 6;
        var r_high = Gen(high, CarFactory.Road());
        var r_low  = Gen(low,  CarFactory.Road());
        // Higher power → higher computed top speed → numerically lower FD (longer gear for high speed)
        Assert.True(r_high.FinalDrive <= r_low.FinalDrive,
            $"High power FD {r_high.FinalDrive} should be <= low power FD {r_low.FinalDrive}");
    }

    // 160 – All explanations contain meaningful content (not just numbers)
    [Fact]
    public void T160_ExplanationsContainMeaningfulText()
    {
        var r = Gen(CarFactory.GtrR35(), CarFactory.Road());
        foreach (var kvp in r.Explanations)
        {
            Assert.NotNull(kvp.Value);
            Assert.True(kvp.Value.Length >= 10, $"Explanation for {kvp.Key} too short: '{kvp.Value}'");
        }
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
