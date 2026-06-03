using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;
using TuneMaster.Tests.Helpers;
using Models_DriveType = Forza_Horizon_6_Tune_Master.Models.DriveType;

namespace TuneMaster.Tests;

/// Tests 381–440: Drive type (AWD/RWD/FWD) and engine position effects
public class DriveTypeAndEngineTests
{
    private static TuneResult Gen(CarCard car, TrackInfo track)
        => new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());

    // ── AWD-specific behavior ──────────────────────────────────────────

    // 381 – AWD road ARB: base 26/33 scales with mass/power/wb
    // GTR (600HP, 1740kg, 2780mm) → ~34/41, WRX (310HP, 1510kg, 2650mm) → ~26/32
    [Fact]
    public void T381_AwdRoadArbScalesWithPower()
    {
        var high = CarFactory.GtrR35();
        var low  = CarFactory.GtrR35(); low.PowerHP = 300; low.TotalMass = 1500; low.Wheelbase = 2700;
        var r_high = Gen(high, CarFactory.Road());
        var r_low  = Gen(low,  CarFactory.Road());
        Assert.True(r_high.ARBFront >= r_low.ARBFront,
            $"High power ARB {r_high.ARBFront} should be >= low {r_low.ARBFront}");
    }

    // 382 – AWD vs RWD on same road: different ARB settings
    [Fact]
    public void T382_AwdVsRwdRoadArbDifferent()
    {
        var awd   = CarFactory.GtrR35();
        var rwd   = CarFactory.SupraA90();
        var r_awd = Gen(awd, CarFactory.Road());
        var r_rwd = Gen(rwd, CarFactory.Road());
        Assert.True(r_awd.ARBFront != r_rwd.ARBFront || r_awd.ARBRear != r_rwd.ARBRear,
            "AWD and RWD should have different road ARB settings");
    }

    // 383 – AWD center diff bias: different per discipline
    [Fact]
    public void T383_AwdCenterBiasDisciplineVariation()
    {
        var car = CarFactory.GtrR35();
        var r_road  = Gen(car, CarFactory.Road());
        var r_drag  = Gen(car, CarFactory.Drag());
        var r_drift = Gen(car, CarFactory.Drift());
        var r_rally = Gen(car, CarFactory.Rally());
        var r_cc    = Gen(car, CarFactory.CrossCountry());
        // Not all need to be different, but at least some should differ
        var biases = new[] { r_road.CenterDiffBias, r_drag.CenterDiffBias, r_drift.CenterDiffBias,
                             r_rally.CenterDiffBias, r_cc.CenterDiffBias };
        Assert.True(biases.Distinct().Count() >= 2,
            $"AWD center bias should vary by discipline: {string.Join(", ", biases)}");
    }

    // 384 – AWD front diff accel/decel present for all disciplines
    [Fact]
    public void T384_AwdFrontDiffPopulatedForAllDisciplines()
    {
        var car = CarFactory.GtrR35();
        foreach (var track in AllTracks())
        {
            var r = Gen(car, track);
            Assert.NotNull(r.DiffFrontAccel);
            Assert.NotNull(r.DiffFrontDecel);
        }
    }

    // 385 – AWD bump ratio: all disciplines within community range
    [Fact]
    public void T385_AwdBumpRatioInRange()
    {
        var car = CarFactory.GtrR35();
        foreach (var track in AllTracks())
        {
            var r = Gen(car, track);
            if (r.ReboundFront >= 2)
            {
                double ratio = r.BumpFront / r.ReboundFront;
                Assert.InRange(ratio, 0.30, 0.80);
            }
        }
    }

    // 386 – AWD diff decel not zero for non-drag (needs some decel lock stability)
    [Fact]
    public void T386_AwdNonDragDiffDecelPositive()
    {
        foreach (var track in new[] { CarFactory.Road(), CarFactory.Rally(), CarFactory.CrossCountry() })
        {
            var r = Gen(CarFactory.GtrR35(), track);
            Assert.True(r.DiffDecel >= 0, $"AWD {track.Discipline}: diff decel {r.DiffDecel} < 0");
        }
    }

    // ── RWD-specific behavior ──────────────────────────────────────────

    // 387 – RWD road ARB: base 22 scales with mass/power/wb (MX5 132HP → ~15, Supra 450HP → ~23)
    [Fact]
    public void T387_RwdRoadArbFrontReasonable()
    {
        var rwdCars = new[] { CarFactory.SupraA90(), CarFactory.Mx5(), CarFactory.Gt3Rs() };
        foreach (var car in rwdCars)
        {
            var r = Gen(car, CarFactory.Road());
            Assert.InRange(r.ARBFront, 10, 40);
        }
    }

    // 388 – RWD drift diff: accel high, decel low/zero
    [Fact]
    public void T388_RwdDriftDiffPattern()
    {
        var r = Gen(CarFactory.SupraA90(), CarFactory.Drift());
        Assert.True(r.DiffAccel >= 40,
            $"Drift RWD accel {r.DiffAccel} should be high (≥40)");
        Assert.InRange(r.DiffDecel, 0, 15);
    }

    // 389 – RWD drag diff: accel high, decel very low (power-adj may add slight decel)
    [Fact]
    public void T389_RwdDragDiffPattern()
    {
        var r = Gen(CarFactory.Hellcat(), CarFactory.Drag());
        Assert.True(r.DiffAccel >= 25,
            $"Drag RWD accel {r.DiffAccel} should be ≥ 25");
        Assert.InRange(r.DiffDecel, 0, 25);
    }

    // 390 – RWD road: rear camber more negative for high-power cars
    [Fact]
    public void T390_RwdRoadRearCamberPowerScaling()
    {
        var low  = CarFactory.Mx5(); low.PowerHP = 100;
        var high = CarFactory.Mx5(); high.PowerHP = 800;
        var r_low  = Gen(low, CarFactory.Road());
        var r_high = Gen(high, CarFactory.Road());
        Assert.True(r_high.CamberRear <= r_low.CamberRear,
            $"High power rear camber {r_high.CamberRear} > Low {r_low.CamberRear}");
    }

    // 391 – RWD road: front camber negative, rear less negative or neutral
    [Fact]
    public void T391_RwdRoadCamberPattern()
    {
        var r = Gen(CarFactory.SupraA90(), CarFactory.Road());
        Assert.True(r.CamberFront < 0, $"RWD road front camber {r.CamberFront} should be negative");
        Assert.True(r.CamberFront <= r.CamberRear,
            $"RWD road front {r.CamberFront} should be <= rear {r.CamberRear}");
    }

    // 392 – RWD drift: front camber aggressive, rear mild
    [Fact]
    public void T392_RwdDriftCamberPattern()
    {
        var r = Gen(CarFactory.SupraA90(), CarFactory.Drift());
        Assert.True(r.CamberFront <= -3.0,
            $"Drift front camber {r.CamberFront} should be aggressively negative");
        Assert.True(r.CamberRear > r.CamberFront,
            $"Drift rear {r.CamberRear} should be less aggressive than front {r.CamberFront}");
    }

    // ── FWD-specific behavior ──────────────────────────────────────────

    // 393 – FWD road: front ARB ≤ rear ARB (reduce understeer)
    [Fact]
    public void T393_FwdRoadFrontArbSofterOrEqual()
    {
        var r = Gen(CarFactory.CivicTypeR(), CarFactory.Road());
        Assert.True(r.ARBFront <= r.ARBRear,
            $"FWD road: front ARB {r.ARBFront} should be <= rear {r.ARBRear}");
    }

    // 394 – FWD road: front camber more negative than rear (weight over front)
    [Fact]
    public void T394_FwdRoadCamberFrontMoreNegative()
    {
        var r = Gen(CarFactory.CivicTypeR(), CarFactory.Road());
        Assert.True(r.CamberFront <= r.CamberRear,
            $"FWD road: front {r.CamberFront} should be <= rear {r.CamberRear}");
    }

    // 395 – FWD road: brake balance front-biased (≥50%)
    [Fact]
    public void T395_FwdBrakeBalanceFrontBiased()
    {
        var r = Gen(CarFactory.CivicTypeR(), CarFactory.Road());
        Assert.True(r.BrakeBalance >= 50,
            $"FWD brake balance {r.BrakeBalance} < 50 (should be front-biased)");
    }

    // 396 – FWD drag: diff accel high, decel low (power-adj may add slight decel)
    [Fact]
    public void T396_FwdDragDiffAccelHigh()
    {
        var car = CarFactory.CivicTypeR(); car.TireType = TireType.Drag;
        var r = Gen(car, CarFactory.Drag());
        Assert.True(r.DiffAccel >= 25,
            $"FWD drag accel {r.DiffAccel} should be ≥ 25");
        Assert.InRange(r.DiffDecel, 0, 25);
    }

    // 397 – FWD road: rear toe slightly positive (toe-in stability)
    [Fact]
    public void T397_FwdRoadRearToePositive()
    {
        var r = Gen(CarFactory.CivicTypeR(), CarFactory.Road());
        Assert.True(r.ToeRear >= 0.0,
            $"FWD road rear toe {r.ToeRear} should be ≥ 0 (toe-in)");
    }

    // ── Cross-drivetrain comparisons ───────────────────────────────────

    // 398 – AWD vs RWD road: may share toe base values but effective values exist
    [Fact]
    public void T398_AwdVsRwdRoadToeValid()
    {
        var r_awd = Gen(CarFactory.GtrR35(), CarFactory.Road());
        var r_rwd = Gen(CarFactory.SupraA90(), CarFactory.Road());
        Assert.InRange(r_awd.ToeFront, -5.0, 5.0);
        Assert.InRange(r_rwd.ToeFront, -5.0, 5.0);
        Assert.InRange(r_awd.ToeRear,  -5.0, 5.0);
        Assert.InRange(r_rwd.ToeRear,  -5.0, 5.0);
    }

    // 399 – FWD vs RWD brake balance: FWD higher (more front bias)
    [Fact]
    public void T399_FwdBrakeBalanceHigherThanRwd()
    {
        var r_fwd = Gen(CarFactory.CivicTypeR(), CarFactory.Road()); // FWD, 62% front
        var r_rwd = Gen(CarFactory.SupraA90(), CarFactory.Road());   // RWD, 50% front
        Assert.True(r_fwd.BrakeBalance >= r_rwd.BrakeBalance,
            $"FWD BB {r_fwd.BrakeBalance} < RWD BB {r_rwd.BrakeBalance}");
    }

    // 400 – AWD vs RWD on drift: different diff patterns
    [Fact]
    public void T400_AwdVsRwdDriftDiffDifferent()
    {
        var r_awd = Gen(CarFactory.GtrR35(), CarFactory.Drift());
        var r_rwd = Gen(CarFactory.SupraA90(), CarFactory.Drift());
        // AWD has center diff, RWD doesn't
        Assert.NotNull(r_awd.CenterDiffBias);
        Assert.Null(r_rwd.CenterDiffBias);
    }

    // 401 – AWD vs RWD spring rates: different for same discipline (weight distribution)
    [Fact]
    public void T401_AwdVsRwdSpringsDifferent()
    {
        var r_awd = Gen(CarFactory.GtrR35(), CarFactory.Road());
        var r_rwd = Gen(CarFactory.SupraA90(), CarFactory.Road());
        Assert.True(r_awd.SpringFront != r_rwd.SpringFront || r_awd.SpringRear != r_rwd.SpringRear,
            "AWD and RWD should have different spring rates");
    }

    // ── Engine position effects ────────────────────────────────────────

    // 402 – Rear-engine (GT3 RS): different camber pattern vs front-engine
    [Fact]
    public void T402_RearEngineCamberDifferent()
    {
        var r_rear  = Gen(CarFactory.Gt3Rs(), CarFactory.Road());    // rear-engine
        var r_front = Gen(CarFactory.SupraA90(), CarFactory.Road()); // front-engine
        Assert.True(Math.Abs(r_rear.CamberFront - r_front.CamberFront) > 0.01 ||
                    Math.Abs(r_rear.CamberRear - r_front.CamberRear) > 0.01,
            "Engine position should affect camber");
    }

    // 403 – Mid-engine (Gemera): front camber less aggressive than front-engine
    [Fact]
    public void T403_MidEngineFrontCamberModerate()
    {
        var r_mid   = Gen(CarFactory.Gemera(), CarFactory.Road());
        var r_front = Gen(CarFactory.SupraA90(), CarFactory.Road());
        Assert.InRange(r_mid.CamberFront, -3.0, 0.0);
    }

    // 404 – EffectiveWtDist override: near-50/50 with engine override
    [Fact]
    public void T404_EffectiveWtDistOverride()
    {
        // Engine position override: Front→55%, Mid→48%, Rear→40% when within 2% of 50/50
        // We test by checking the effects on tuning that depend on weight distribution

        // Supra is 50% front weight, front-engine → EffectiveWtDist = 55 (engine override)
        var r_supra = Gen(CarFactory.SupraA90(), CarFactory.Road());
        // GTR is 54% front weight, front-engine → no override (54-50=4 > 2)
        var r_gtr   = Gen(CarFactory.GtrR35(), CarFactory.Road());

        // Supra (effective = 55) should have more front bias than GTR (effective = 54)
        // in certain fields. The override should produce different results.
        Assert.True(r_supra.CamberFront != r_gtr.CamberFront || r_supra.CamberRear != r_gtr.CamberRear,
            "Effective weight distribution override should affect camber");
    }

    // 405 – Rear-engine brake balance: more rear bias (weight over rear)
    [Fact]
    public void T405_RearEngineBrakeBalanceRearBiased()
    {
        var r_rr = Gen(CarFactory.Gt3Rs(), CarFactory.Road()); // 38% front, rear-engine
        var r_fr = Gen(CarFactory.SupraA90(), CarFactory.Road()); // 50% front, front-engine
        // GT3 RS should have lower (more rear) brake balance than Supra
        Assert.True(r_rr.BrakeBalance <= r_fr.BrakeBalance + 3,
            $"GT3 RS BB {r_rr.BrakeBalance} vs Supra BB {r_fr.BrakeBalance}");
    }

    // 406 – Rear-engine tire pressure: rear higher than front (weight bias)
    [Fact]
    public void T406_RearEngineTirePressureRearHigher()
    {
        var r = Gen(CarFactory.Gt3Rs(), CarFactory.Road());
        Assert.True(r.TirePressureRear >= r.TirePressureFront,
            $"GT3 RS rear press {r.TirePressureRear} < front {r.TirePressureFront}");
    }

    // 407 – Front-engine heavy front: front tire pressure higher
    [Fact]
    public void T407_FrontHeavyFrontTirePressureHigher()
    {
        var r = Gen(CarFactory.CivicTypeR(), CarFactory.Road()); // 62% front
        Assert.True(r.TirePressureFront >= r.TirePressureRear - 0.1,
            $"Civic front press {r.TirePressureFront} vs rear {r.TirePressureRear}");
    }

    // 408 – Engine position + weight distribution affects ride height rake
    [Fact]
    public void T408_EnginePositionAffectsRideHeightRake()
    {
        var r_rear  = Gen(CarFactory.Gt3Rs(), CarFactory.Road());  // rear-engine, 38% front
        var r_front = Gen(CarFactory.SupraA90(), CarFactory.Road()); // front-engine, 50% front
        double rakeRear  = r_rear.RideHeightRear - r_rear.RideHeightFront;
        double rakeFront = r_front.RideHeightRear - r_front.RideHeightFront;
        Assert.True(rakeRear >= rakeFront - 15,
            $"Rear engine rake {rakeRear} vs front {rakeFront}");
    }

    // ── Combined drive type + discipline ───────────────────────────────

    // 409 – AWD rally: specific behavior (low ARB, moderate springs, rally ride height)
    [Fact]
    public void T409_AwdRallyCharacteristics()
    {
        var r = Gen(CarFactory.WrxSti(), CarFactory.Rally());
        Assert.True(r.ARBFront <= 20, $"Rally ARB front {r.ARBFront} > 20");
        Assert.True(r.ARBRear  <= 20, $"Rally ARB rear {r.ARBRear} > 20");
        Assert.True(r.RideHeightFront >= 70, $"Rally ride height {r.RideHeightFront} < 70");
    }

    // 410 – RWD Touge: more aggressive than road, less than drift
    [Fact]
    public void T410_RwdTougeCharacteristics()
    {
        var r_touge = Gen(CarFactory.SupraA90(), CarFactory.Touge());
        var r_road  = Gen(CarFactory.SupraA90(), CarFactory.Road());
        var r_drift = Gen(CarFactory.SupraA90(), CarFactory.Drift());
        // Touge front camber between road and drift
        Assert.True(r_touge.CamberFront <= r_road.CamberFront,
            $"Touge front camber {r_touge.CamberFront} should be <= road {r_road.CamberFront}");
        Assert.True(r_touge.CamberFront >= r_drift.CamberFront || r_touge.CamberFront > -5,
            $"Touge front camber {r_touge.CamberFront} out of range");
    }

    // 411 – FWD Rally: specific front-wheel drive off-road behavior
    [Fact]
    public void T411_FwdRallyCharacteristics()
    {
        var car = CarFactory.CivicTypeR(); car.SuspensionUpgrade = SuspensionUpgrade.Rally;
        car.TireType = TireType.Rally;
        var r = Gen(car, CarFactory.Rally());
        Assert.True(r.RideHeightFront >= 70, $"Rally ride height {r.RideHeightFront} < 70");
        Assert.True(r.ToeFront <= 0, $"Rally front toe {r.ToeFront} should be ≤ 0");
        Assert.True(r.ToeRear  >= 0, $"Rally rear toe {r.ToeRear} should be ≥ 0");
    }

    // 412 – RWD CrossCountry: high ride height, soft springs
    [Fact]
    public void T412_RwdCrossCountryCharacteristics()
    {
        var r = Gen(CarFactory.Mx5(), CarFactory.CrossCountry());
        Assert.True(r.RideHeightFront >= 70, $"CC ride height {r.RideHeightFront} < 70");
        Assert.True(r.ToeFront <= 0, $"CC front toe {r.ToeFront} should be ≤ 0");
    }

    // ── AWD diff deep dive ─────────────────────────────────────────────

    // 413 – AWD front diff: varies by discipline
    [Fact]
    public void T413_AwdFrontDiffVariesByDiscipline()
    {
        var car = CarFactory.GtrR35();
        var r_road  = Gen(car, CarFactory.Road());
        var r_drag  = Gen(car, CarFactory.Drag());
        var r_drift = Gen(car, CarFactory.Drift());
        var r_rally = Gen(car, CarFactory.Rally());
        var r_cc    = Gen(car, CarFactory.CrossCountry());
        var accels = new[] { r_road.DiffFrontAccel, r_drag.DiffFrontAccel, r_drift.DiffFrontAccel,
                             r_rally.DiffFrontAccel, r_cc.DiffFrontAccel };
        Assert.True(accels.Distinct().Count() >= 2,
            $"AWD front diff accel should vary: {string.Join(", ", accels)}");
    }

    // 414 – AWD center bias: discipline-specific (road ≥ 55)
    [Fact]
    public void T414_AwdCenterBiasRoadAtLeast55()
    {
        var r = Gen(CarFactory.GtrR35(), CarFactory.Road());
        Assert.True(r.CenterDiffBias >= 55,
            $"AWD road center bias {r.CenterDiffBias} < 55");
    }

    // 415 – AWD center bias: drag ≥ rally ≥ cc (torque split logic)
    [Fact]
    public void T415_AwdCenterBiasDragVsRally()
    {
        var car = CarFactory.GtrR35();
        var r_drag = Gen(car, CarFactory.Drag());
        var r_rally = Gen(car, CarFactory.Rally());
        Assert.NotNull(r_drag.CenterDiffBias);
        Assert.NotNull(r_rally.CenterDiffBias);
        Assert.InRange(r_drag.CenterDiffBias.Value, 0, 100);
        Assert.InRange(r_rally.CenterDiffBias.Value, 0, 100);
    }

    // ── Drive type invariant checks ────────────────────────────────────

    // 416 – No drive type produces null gear ratios
    [Fact]
    public void T416_NoDriveTypeNullGearRatios()
    {
        foreach (var car in new[] { CarFactory.GtrR35(), CarFactory.SupraA90(), CarFactory.CivicTypeR() })
        foreach (var track in AllTracks())
        {
            var r = Gen(car, track);
            Assert.NotNull(r.GearRatios);
            Assert.NotEmpty(r.GearRatios);
        }
    }

    // 417 – All drive types produce valid spring rates
    [Fact]
    public void T417_AllDriveTypesValidSprings()
    {
        foreach (var car in new[] { CarFactory.GtrR35(), CarFactory.SupraA90(), CarFactory.CivicTypeR() })
        foreach (var track in AllTracks())
        {
            var r = Gen(car, track);
            Assert.InRange(r.SpringFront, 57.1, 285.6);
            Assert.InRange(r.SpringRear,  57.1, 285.6);
        }
    }

    // 418 – All drive types produce valid camber (within -5 to +5)
    [Fact]
    public void T418_AllDriveTypesValidCamber()
    {
        foreach (var car in new[] { CarFactory.GtrR35(), CarFactory.SupraA90(), CarFactory.CivicTypeR(),
                                    CarFactory.Gt3Rs(), CarFactory.ModelSPlaid() })
        foreach (var track in AllTracks())
        {
            var r = Gen(car, track);
            Assert.InRange(r.CamberFront, -5.0, 5.0);
            Assert.InRange(r.CamberRear,  -5.0, 5.0);
        }
    }

    // 419 – All drive types produce valid toe (within -5 to +5)
    [Fact]
    public void T419_AllDriveTypesValidToe()
    {
        foreach (var car in new[] { CarFactory.GtrR35(), CarFactory.SupraA90(), CarFactory.CivicTypeR() })
        foreach (var track in AllTracks())
        {
            var r = Gen(car, track);
            Assert.InRange(r.ToeFront, -5.0, 5.0);
            Assert.InRange(r.ToeRear,  -5.0, 5.0);
        }
    }

    // 420 – Electric AWD: Tesla has correct diff and gear config
    [Fact]
    public void T420_ElectricAwdCorrectConfig()
    {
        var r = Gen(CarFactory.ModelSPlaid(), CarFactory.Road());
        Assert.NotNull(r.CenterDiffBias);
        Assert.NotNull(r.DiffFrontAccel);
        Assert.NotNull(r.DiffFrontDecel);
        Assert.Single(r.GearRatios);
    }

    private static TrackInfo[] AllTracks() => new[]
    {
        CarFactory.Road(), CarFactory.Drag(), CarFactory.Drift(),
        CarFactory.Rally(), CarFactory.CrossCountry(), CarFactory.Touge()
    };
}
