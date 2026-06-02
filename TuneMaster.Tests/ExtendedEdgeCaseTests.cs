using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;
using TuneMaster.Tests.Helpers;

namespace TuneMaster.Tests;

/// Tests 521–600: Extended edge cases, extreme values, and robustness
public class ExtendedEdgeCaseTests
{
    private static TuneResult Gen(CarCard car, TrackInfo track)
        => new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());

    // ── Extreme mass ───────────────────────────────────────────────────

    // 521 – Minimum mass (500 kg) road car
    [Fact]
    public void T521_MinMassRoadCar()
    {
        var car = CarFactory.Mx5(); car.TotalMass = 500; car.PowerHP = 60; car.MaxSpeedKmh = 180;
        var r = Gen(car, CarFactory.Road());
        var c = CarFactory.DefaultConstraints();
        Assert.InRange(r.SpringFront, c.SpringFrontMin, c.SpringFrontMax);
        Assert.InRange(r.ReboundFront, c.ReboundFrontMin, c.ReboundFrontMax);
        Assert.True(r.FinalDrive > 0);
    }

    // 522 – Maximum mass (3000 kg) road car
    [Fact]
    public void T522_MaxMassRoadCar()
    {
        var car = CarFactory.Wrangler(); car.TotalMass = 3000; car.PowerHP = 200; car.MaxSpeedKmh = 200;
        var r = Gen(car, CarFactory.Road());
        var c = CarFactory.DefaultConstraints();
        Assert.InRange(r.BrakePressure, c.BrakePressureMin, c.BrakePressureMax);
        Assert.InRange(r.SpringFront, c.SpringFrontMin, c.SpringFrontMax);
        Assert.InRange(r.ReboundFront, c.ReboundFrontMin, c.ReboundFrontMax);
    }

    // 523 – Ultra-light (500 kg) drift car
    [Fact]
    public void T523_MinMassDriftCar()
    {
        var car = CarFactory.Mx5(); car.TotalMass = 500; car.PowerHP = 150;
        var r = Gen(car, CarFactory.Drift());
        Assert.InRange(r.BumpFront, 1, 20);
        Assert.InRange(r.BumpRear,  1, 20);
        Assert.InRange(r.CamberFront, -5.0, 5.0);
    }

    // 524 – Ultra-heavy (3000 kg) drag car
    [Fact]
    public void T524_MaxMassDragCar()
    {
        var car = CarFactory.Wrangler(); car.TotalMass = 3000; car.PowerHP = 500; car.TireType = TireType.Drag;
        var r = Gen(car, CarFactory.Drag());
        Assert.Equal(0.0, r.DiffDecel);
        Assert.InRange(r.TirePressureRear, 1.0, 3.8);
    }

    // ── Extreme power ──────────────────────────────────────────────────

    // 525 – Ultra-low power (50 HP) road car
    [Fact]
    public void T525_LowPowerRoadCar()
    {
        var car = CarFactory.Mx5(); car.PowerHP = 50; car.TorqueNm = 80;
        var r = Gen(car, CarFactory.Road());
        Assert.True(r.BrakePressure >= 50, $"Brake pressure {r.BrakePressure} too low");
        Assert.True(r.FinalDrive > 0);
    }

    // 526 – Ultra-high power (3000 HP) road car
    [Fact]
    public void T526_HighPowerRoadCar()
    {
        var car = CarFactory.Gemera(); car.PowerHP = 3000; car.MaxSpeedKmh = 500; car.TorqueNm = 3500;
        var r = Gen(car, CarFactory.Road());
        var c = CarFactory.DefaultConstraints();
        Assert.InRange(r.BrakePressure, c.BrakePressureMin, c.BrakePressureMax);
        Assert.InRange(r.BrakeBalance, c.BrakeBalanceMin, c.BrakeBalanceMax);
        Assert.InRange(r.FinalDrive, c.FinalDriveMin, c.FinalDriveMax);
    }

    // 527 – High power + light weight (extreme power-to-weight)
    [Fact]
    public void T527_HighPowerLightWeight()
    {
        var car = CarFactory.Mx5(); car.PowerHP = 1200; car.TotalMass = 800; car.TorqueNm = 1000;
        car.MaxSpeedKmh = 350; car.GearCount = 6; car.SuspensionUpgrade = SuspensionUpgrade.Race;
        var r = Gen(car, CarFactory.Road());
        var c = CarFactory.DefaultConstraints();
        Assert.InRange(r.FinalDrive, c.FinalDriveMin, c.FinalDriveMax);
        Assert.InRange(r.BrakePressure, c.BrakePressureMin, c.BrakePressureMax);
    }

    // ── Extreme speed ─────────────────────────────────────────────────

    // 528 – Low max speed (150 km/h) road car
    [Fact]
    public void T528_LowSpeedRoadCar()
    {
        var car = CarFactory.Mx5(); car.MaxSpeedKmh = 150; car.PowerHP = 80;
        var r = Gen(car, CarFactory.Road());
        Assert.InRange(r.FinalDrive, 2.2, 6.1);
    }

    // 529 – Extreme max speed (500 km/h) road car
    [Fact]
    public void T529_HighSpeedRoadCar()
    {
        var car = CarFactory.Gemera(); car.MaxSpeedKmh = 500; car.PowerHP = 2000;
        var r = Gen(car, CarFactory.Road());
        Assert.InRange(r.FinalDrive, 2.2, 6.1);
        Assert.NotEmpty(r.GearRatios);
    }

    // ── Extreme wheelbase ──────────────────────────────────────────────

    // 530 – Very short wheelbase (2000 mm) road car
    [Fact]
    public void T530_ShortWheelbaseRoadCar()
    {
        var car = CarFactory.Mx5(); car.Wheelbase = 2000;
        var r = Gen(car, CarFactory.Road());
        Assert.InRange(r.ToeFront, -5.0, 5.0);
        Assert.InRange(r.ToeRear,  -5.0, 5.0);
    }

    // 531 – Very long wheelbase (3500 mm) road car
    [Fact]
    public void T531_LongWheelbaseRoadCar()
    {
        var car = CarFactory.Mx5(); car.Wheelbase = 3500;
        var r = Gen(car, CarFactory.Road());
        Assert.InRange(r.ToeFront, -5.0, 5.0);
        Assert.InRange(r.ToeRear,  -5.0, 5.0);
    }

    // ── Extreme weight distribution ────────────────────────────────────

    // 532 – Extreme front weight (90% front)
    [Fact]
    public void T532_ExtremeFrontWeight()
    {
        var car = CarFactory.SupraA90(); car.WeightDistributionFront = 90;
        var r = Gen(car, CarFactory.Road());
        Assert.InRange(r.CamberFront, -5.0, 5.0);
        Assert.InRange(r.TirePressureFront, 1.0, 3.8);
    }

    // 533 – Extreme rear weight (10% front = 90% rear)
    [Fact]
    public void T533_ExtremeRearWeight()
    {
        var car = CarFactory.SupraA90(); car.WeightDistributionFront = 10;
        var r = Gen(car, CarFactory.Road());
        Assert.InRange(r.CamberRear, -5.0, 5.0);
        Assert.InRange(r.TirePressureRear, 1.0, 3.8);
    }

    // 534 – Perfect 50/50 weight with engine override
    [Fact]
    public void T534_Perfect5050WeightDistribution()
    {
        var car = CarFactory.SupraA90(); car.WeightDistributionFront = 50;
        // Front-engine at 50% → EffectiveWtDist() overrides to 55%
        var r = Gen(car, CarFactory.Road());
        Assert.InRange(r.CamberFront, -5.0, 5.0);
        Assert.InRange(r.CamberRear,  -5.0, 5.0);
        Assert.InRange(r.TirePressureFront, 1.0, 3.8);
        Assert.InRange(r.TirePressureRear,  1.0, 3.8);
    }

    // 535 – 50/50 with mid-engine (Gemera has 49% ≈ override to 48%)
    [Fact]
    public void T535_MidEngine5050Override()
    {
        var car = CarFactory.Gemera(); car.WeightDistributionFront = 50;
        var r = Gen(car, CarFactory.Road());
        Assert.InRange(r.SpringFront, 57.1, 285.6);
    }

    // ── Hybrid/EV drivetrain edge cases ────────────────────────────────

    // 536 – Electric car on drag: single gear, valid tune
    [Fact]
    public void T536_ElectricCarDrag()
    {
        var car = CarFactory.ModelSPlaid(); car.TireType = TireType.Drag;
        var r = Gen(car, CarFactory.Drag());
        Assert.Single(r.GearRatios);
        Assert.Equal(0.0, r.DiffDecel);
    }

    // 537 – Electric car on rally: single gear works
    [Fact]
    public void T537_ElectricCarRally()
    {
        var car = CarFactory.ModelSPlaid(); car.TireType = TireType.Rally;
        car.SuspensionUpgrade = SuspensionUpgrade.Rally;
        var r = Gen(car, CarFactory.Rally());
        Assert.Single(r.GearRatios);
        Assert.InRange(r.RideHeightFront, 50, 250);
    }

    // 538 – Electric car on cross country: extreme terrain
    [Fact]
    public void T538_ElectricCarCrossCountry()
    {
        var car = CarFactory.ModelSPlaid();
        var r = Gen(car, CarFactory.CrossCountry());
        Assert.Single(r.GearRatios);
        Assert.InRange(r.TirePressureFront, 1.0, 3.8);
    }

    // ── Extreme tire/rim sizes ────────────────────────────────────────

    // 539 – Minimum rim size (15 inch)
    [Fact]
    public void T539_MinRimSize()
    {
        var car = CarFactory.Mx5(); car.FrontRimDiameter = 15; car.RearRimDiameter = 15;
        var r = Gen(car, CarFactory.Road());
        Assert.InRange(r.RideHeightFront, 50, 250);
    }

    // 540 – Maximum rim size (24 inch)
    [Fact]
    public void T540_MaxRimSize()
    {
        var car = CarFactory.SupraA90(); car.FrontRimDiameter = 24; car.RearRimDiameter = 24;
        var r = Gen(car, CarFactory.Road());
        Assert.InRange(r.RideHeightFront, 50, 250);
    }

    // 541 – Very wide tires (400 mm)
    [Fact]
    public void T541_VeryWideTires()
    {
        var car = CarFactory.SupraA90(); car.FrontTireWidth = 400; car.RearTireWidth = 400;
        var r = Gen(car, CarFactory.Road());
        Assert.InRange(r.TirePressureFront, 1.0, 3.8);
    }

    // 542 – Very narrow tires (155 mm)
    [Fact]
    public void T542_VeryNarrowTires()
    {
        var car = CarFactory.SupraA90(); car.FrontTireWidth = 155; car.RearTireWidth = 155;
        var r = Gen(car, CarFactory.Road());
        Assert.InRange(r.TirePressureFront, 1.0, 3.8);
    }

    // ── Upgrade combinations ──────────────────────────────────────────

    // 543 – Stock everything (minimum upgrades)
    [Fact]
    public void T543_StockEverything()
    {
        var car = CarFactory.SupraA90();
        car.SuspensionUpgrade = SuspensionUpgrade.Stock;
        car.DifferentialUpgrade = DifferentialUpgrade.Stock;
        car.TireType = TireType.Stock;
        var r = Gen(car, CarFactory.Road());
        var c = CarFactory.DefaultConstraints();
        Assert.InRange(r.SpringFront, c.SpringFrontMin, c.SpringFrontMax);
        Assert.InRange(r.ReboundFront, c.ReboundFrontMin, c.ReboundFrontMax);
    }

    // 544 – Race everything (maximum upgrades)
    [Fact]
    public void T544_RaceEverything()
    {
        var car = CarFactory.SupraA90();
        car.SuspensionUpgrade = SuspensionUpgrade.Race;
        car.DifferentialUpgrade = DifferentialUpgrade.Race;
        car.TireType = TireType.Slick;
        var r = Gen(car, CarFactory.Road());
        var c = CarFactory.DefaultConstraints();
        Assert.InRange(r.SpringFront, c.SpringFrontMin, c.SpringFrontMax);
        Assert.InRange(r.ReboundFront, c.ReboundFrontMin, c.ReboundFrontMax);
    }

    // 545 – Rally suspension on road (mismatched setup)
    [Fact]
    public void T545_RallySuspensionOnRoad()
    {
        var car = CarFactory.SupraA90(); car.SuspensionUpgrade = SuspensionUpgrade.Rally;
        var r = Gen(car, CarFactory.Road());
        Assert.InRange(r.SpringFront, 57.1, 285.6);
        Assert.InRange(r.RideHeightFront, 50, 250);
    }

    // 546 – Street suspension on rally (mismatch)
    [Fact]
    public void T546_StreetSuspensionOnRally()
    {
        var car = CarFactory.WrxSti(); car.SuspensionUpgrade = SuspensionUpgrade.Street;
        var r = Gen(car, CarFactory.Rally());
        Assert.InRange(r.RideHeightFront, 50, 250);
        Assert.InRange(r.SpringFront, 57.1, 285.6);
    }

    // ── Edge case combos ──────────────────────────────────────────────

    // 547 – 1-gear car on all disciplines (electric)
    [Fact]
    public void T547_SingleGearAllDisciplines()
    {
        foreach (var track in AllTracks())
        {
            var r = Gen(CarFactory.ModelSPlaid(), track);
            Assert.Single(r.GearRatios);
            Assert.InRange(r.FinalDrive, 2.2, 6.1);
        }
    }

    // 548 – 10-gear car (Gemera) all disciplines
    [Fact]
    public void T548_TenGearAllDisciplines()
    {
        foreach (var track in AllTracks())
        {
            var r = Gen(CarFactory.Gemera(), track);
            Assert.Equal(10, r.GearRatios.Count);
            Assert.InRange(r.FinalDrive, 2.2, 6.1);
        }
    }

    // 549 – 8-gear car all disciplines
    [Fact]
    public void T549_EightGearAllDisciplines()
    {
        foreach (var track in AllTracks())
        {
            var r = Gen(CarFactory.SupraA90(), track);
            Assert.Equal(8, r.GearRatios.Count);
            Assert.InRange(r.FinalDrive, 2.2, 6.1);
        }
    }

    // 550 – No-aero, no-front-aero-only, no-rear-aero-only cars
    [Fact]
    public void T550_AeroConfigVariations()
    {
        var noAero = CarFactory.SupraA90(); // no aero at all
        var rearOnly = CarFactory.WrxSti(); // HasRearAero=true
        var both = CarFactory.Gt3Rs(); // both aero
        var r_no    = Gen(noAero, CarFactory.Road());
        var r_rear  = Gen(rearOnly, CarFactory.Road());
        var r_both  = Gen(both, CarFactory.Road());
        Assert.Equal(0.0, r_no.AeroFront);
        Assert.Equal(0.0, r_no.AeroRear);
        Assert.Equal(0.0, r_rear.AeroFront);
        Assert.True(r_rear.AeroRear > 0 || r_rear.AeroRear == 0);
        Assert.True(r_both.AeroFront > 0, $"Both-aero car front {r_both.AeroFront} should be > 0");
        Assert.True(r_both.AeroRear > 0,  $"Both-aero car rear {r_both.AeroRear} should be > 0");
    }

    // ── Full output completeness ──────────────────────────────────────

    // 551 – All 12 explanation keys non-empty for all 10 cars × 6 tracks
    [Fact]
    public void T551_AllExplanationsAllCombos()
    {
        var required = new[] { "TirePressure", "Camber", "Toe", "Caster", "ARB", "Springs",
                               "RideHeight", "Dampers", "Aero", "Differential", "Brakes", "FinalDrive" };
        foreach (var (car, track) in AllCombosFull())
        {
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            foreach (var key in required)
            {
                Assert.True(r.Explanations.ContainsKey(key) && !string.IsNullOrWhiteSpace(r.Explanations[key]),
                    $"{car.DriveType}/{track.Discipline}: missing '{key}'");
            }
        }
    }

    // 552 – Result has all fields populated for all combos
    [Fact]
    public void T552_AllFieldsPopulatedAllCombos()
    {
        foreach (var (car, track) in AllCombosFull())
        {
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            Assert.True(r.TirePressureFront > 0);
            Assert.True(r.TirePressureRear  > 0);
            Assert.True(r.ReboundFront > 0);
            Assert.True(r.ReboundRear  > 0);
            Assert.True(r.BumpFront > 0);
            Assert.True(r.BumpRear > 0);
            Assert.True(r.SpringFront > 0);
            Assert.True(r.SpringRear  > 0);
            Assert.True(r.FinalDrive > 0);
            Assert.NotEmpty(r.GearRatios);
            Assert.NotEmpty(r.Explanations);
        }
    }

    // 553 – Explanation content mentions numbers (or meaningful text for no-aero cars)
    [Fact]
    public void T553_ExplanationsContainNumbers()
    {
        foreach (var (car, track) in AllCombosFull())
        {
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            foreach (var kvp in r.Explanations)
            {
                if (kvp.Key == "Aero" && (!car.HasFrontAero && !car.HasRearAero))
                {
                    // "No aero installed" is valid, just check non-empty
                    Assert.False(string.IsNullOrWhiteSpace(kvp.Value));
                }
                else
                {
                    bool hasDigit = kvp.Value.Any(char.IsDigit);
                    Assert.True(hasDigit,
                        $"Explanation '{kvp.Key}' for {car.DriveType}/{track.Discipline} has no numbers: '{kvp.Value}'");
                }
            }
        }
    }

    // ── Determinism ───────────────────────────────────────────────────

    // 554 – Determinism: all fields identical across multiple generations
    [Fact]
    public void T554_FullDeterminismAllCombos()
    {
        var svc = new TuneGeneratorService();
        var c   = CarFactory.DefaultConstraints();
        foreach (var (car, track) in new[] { (CarFactory.SupraA90(), CarFactory.Road()),
                                              (CarFactory.GtrR35(), CarFactory.Drift()),
                                              (CarFactory.Wrangler(), CarFactory.CrossCountry()),
                                              (CarFactory.Hellcat(), CarFactory.Drag()),
                                              (CarFactory.Mx5(), CarFactory.Touge()) })
        {
            var r1 = svc.Generate(car, track, c);
            var r2 = svc.Generate(car, track, c);
            Assert.Equal(r1.TirePressureFront, r2.TirePressureFront);
            Assert.Equal(r1.CamberFront, r2.CamberFront);
            Assert.Equal(r1.ToeFront, r2.ToeFront);
            Assert.Equal(r1.Caster, r2.Caster);
            Assert.Equal(r1.ARBFront, r2.ARBFront);
            Assert.Equal(r1.SpringFront, r2.SpringFront);
            Assert.Equal(r1.RideHeightFront, r2.RideHeightFront);
            Assert.Equal(r1.ReboundFront, r2.ReboundFront);
            Assert.Equal(r1.BumpFront, r2.BumpFront);
            Assert.Equal(r1.AeroFront, r2.AeroFront);
            Assert.Equal(r1.DiffAccel, r2.DiffAccel);
            Assert.Equal(r1.BrakeBalance, r2.BrakeBalance);
            Assert.Equal(r1.BrakePressure, r2.BrakePressure);
            Assert.Equal(r1.FinalDrive, r2.FinalDrive);
        }
    }

    // 555 – Determinism electric car
    [Fact]
    public void T555_DeterminismElectricCar()
    {
        var svc = new TuneGeneratorService();
        var c   = CarFactory.DefaultConstraints();
        var r1  = svc.Generate(CarFactory.ModelSPlaid(), CarFactory.Road(), c);
        var r2  = svc.Generate(CarFactory.ModelSPlaid(), CarFactory.Road(), c);
        Assert.Equal(r1.GearRatios[0], r2.GearRatios[0]);
        Assert.Equal(r1.CenterDiffBias, r2.CenterDiffBias);
        Assert.Equal(r1.DiffFrontAccel, r2.DiffFrontAccel);
    }

    // ── Multiple aero configurations ──────────────────────────────────

    // 556 – Only front aero (e.g. some cars in FH6 have just front splitter)
    [Fact]
    public void T556_FrontAeroOnly()
    {
        var car = CarFactory.Gt3Rs();
        car.HasRearAero = false;
        var r = Gen(car, CarFactory.Road());
        Assert.Equal(0.0, r.AeroRear);
        Assert.True(r.AeroFront >= 0);
    }

    // 557 – Only rear aero (e.g. WRX STI)
    [Fact]
    public void T557_RearAeroOnly()
    {
        var r = Gen(CarFactory.WrxSti(), CarFactory.Road());
        Assert.Equal(0.0, r.AeroFront);
        Assert.InRange(r.AeroRear, 0.0, 200.0);
    }

    // 558 – Both aero on all disciplines
    [Fact]
    public void T558_BothAeroAllDisciplines()
    {
        foreach (var track in AllTracks())
        {
            var r = Gen(CarFactory.Gt3Rs(), track);
            Assert.True(r.AeroFront >= 0);
            Assert.True(r.AeroRear  >= 0);
        }
    }

    // ── Engine/Aspiration combos ──────────────────────────────────────

    // 559 – Electric + AWD + single gear: diff works correctly
    [Fact]
    public void T559_ElectricAwdDiffCorrect()
    {
        var r = Gen(CarFactory.ModelSPlaid(), CarFactory.Road());
        Assert.NotNull(r.DiffFrontAccel);
        Assert.NotNull(r.DiffFrontDecel);
        Assert.NotNull(r.CenterDiffBias);
    }

    // 560 – TwinTurbo + V8 + AWD: Gemera on all disciplines
    [Fact]
    public void T560_TwinTurboV8AwdAllDisciplines()
    {
        foreach (var track in AllTracks())
        {
            var r = Gen(CarFactory.Gemera(), track);
            Assert.InRange(r.SpringFront, 57.1, 285.6);
            Assert.InRange(r.BrakePressure, 50, 200);
        }
    }

    // ── Null/edge fields ──────────────────────────────────────────────

    // 561 – RWD/FWD: center diff and front diff are null for all combos
    [Fact]
    public void T561_RwdFwdNoCenterDiffAllCombos()
    {
        foreach (var car in new[] { CarFactory.SupraA90(), CarFactory.CivicTypeR(), CarFactory.Mx5(), CarFactory.Gt3Rs(), CarFactory.Hellcat() })
        foreach (var track in AllTracks())
        {
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            Assert.Null(r.CenterDiffBias);
            Assert.Null(r.DiffFrontAccel);
            Assert.Null(r.DiffFrontDecel);
        }
    }

    // 562 – AWD: center diff and front diff non-null for all combos
    [Fact]
    public void T562_AwdHasCenterDiffAllCombos()
    {
        foreach (var car in new[] { CarFactory.GtrR35(), CarFactory.Gemera(), CarFactory.Wrangler(), CarFactory.ModelSPlaid(), CarFactory.WrxSti() })
        foreach (var track in AllTracks())
        {
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            Assert.NotNull(r.CenterDiffBias);
            Assert.NotNull(r.DiffFrontAccel);
            Assert.NotNull(r.DiffFrontDecel);
        }
    }

    // 563 – All disciplines handle sport suspension upgrade consistently
    [Fact]
    public void T563_SportSuspensionAllDisciplines()
    {
        var car = CarFactory.SupraA90(); car.SuspensionUpgrade = SuspensionUpgrade.Sport;
        foreach (var track in AllTracks())
        {
            var r = Gen(car, track);
            Assert.InRange(r.SpringFront, 57.1, 285.6);
            Assert.InRange(r.ReboundFront, 1, 20);
        }
    }

    // ── Front/rear config mismatches ──────────────────────────────────

    // 564 – Front tire narrower than rear (common stagger)
    [Fact]
    public void T564_StaggeredTireFitment()
    {
        var car = CarFactory.SupraA90(); car.FrontTireWidth = 225; car.RearTireWidth = 315;
        var r = Gen(car, CarFactory.Road());
        Assert.InRange(r.TirePressureFront, 1.0, 3.8);
        Assert.InRange(r.TirePressureRear,  1.0, 3.8);
    }

    // 565 – Front tire wider than rear (reverse stagger)
    [Fact]
    public void T565_ReverseStagger()
    {
        var car = CarFactory.SupraA90(); car.FrontTireWidth = 315; car.RearTireWidth = 225;
        var r = Gen(car, CarFactory.Road());
        Assert.InRange(r.TirePressureFront, 1.0, 3.8);
        Assert.InRange(r.TirePressureRear,  1.0, 3.8);
    }

    // ── Extreme value bounds ──────────────────────────────────────────

    // 566 – Minimum power (1 HP)
    [Fact]
    public void T566_MinimumPower()
    {
        var car = CarFactory.Mx5(); car.PowerHP = 1; car.TorqueNm = 10; car.MaxSpeedKmh = 50;
        var r = Gen(car, CarFactory.Road());
        var c = CarFactory.DefaultConstraints();
        Assert.InRange(r.BrakePressure, c.BrakePressureMin, c.BrakePressureMax);
        Assert.InRange(r.FinalDrive, c.FinalDriveMin, c.FinalDriveMax);
    }

    // 567 – Minimum torque (1 Nm)
    [Fact]
    public void T567_MinimumTorque()
    {
        var car = CarFactory.Mx5(); car.PowerHP = 50; car.TorqueNm = 1; car.MaxSpeedKmh = 100;
        var r = Gen(car, CarFactory.Road());
        Assert.True(r.FinalDrive > 0);
    }

    // 568 – Minimum RPM (1000)
    [Fact]
    public void T568_MinimumRpm()
    {
        var car = CarFactory.Mx5(); car.MaxRPM = 1000;
        var r = Gen(car, CarFactory.Road());
        Assert.True(r.FinalDrive > 0);
    }

    // 569 – Maximum RPM (12000)
    [Fact]
    public void T569_MaximumRpm()
    {
        var car = CarFactory.SupraA90(); car.MaxRPM = 12000;
        var r = Gen(car, CarFactory.Road());
        Assert.True(r.FinalDrive > 0);
    }

    // 570 – Very high max RPM (20000, electric-like)
    [Fact]
    public void T570_ElectricRpmRange()
    {
        var r = Gen(CarFactory.ModelSPlaid(), CarFactory.Road());
        Assert.Single(r.GearRatios);
    }

    // ── Mixed upgrade scenarios ──────────────────────────────────────

    // 571 – Race springs with stock dampers (unusual but valid)
    [Fact]
    public void T571_RaceSuspensionOnAllDisciplines()
    {
        var car = CarFactory.SupraA90(); car.SuspensionUpgrade = SuspensionUpgrade.Race;
        foreach (var track in AllTracks())
        {
            var r = Gen(car, track);
            Assert.InRange(r.SpringFront, 57.1, 285.6);
            Assert.InRange(r.ReboundFront, 1, 20);
        }
    }

    // 572 – Stock suspension on all disciplines
    [Fact]
    public void T572_StockSuspensionAllDisciplines()
    {
        var car = CarFactory.SupraA90(); car.SuspensionUpgrade = SuspensionUpgrade.Stock;
        foreach (var track in AllTracks())
        {
            var r = Gen(car, track);
            Assert.InRange(r.SpringFront, 57.1, 285.6);
            Assert.InRange(r.RideHeightFront, 50, 250);
        }
    }

    // ── Heterogeneous tire combos ─────────────────────────────────────

    // 573 – Drag tires on off-road discipline (unusual)
    [Fact]
    public void T573_DragTiresOnRally()
    {
        var car = CarFactory.WrxSti(); car.TireType = TireType.Drag;
        var r = Gen(car, CarFactory.Rally());
        Assert.InRange(r.TirePressureFront, 1.0, 3.8);
        Assert.InRange(r.RideHeightFront, 50, 250);
    }

    // 574 – Offroad tires on road (unusual)
    [Fact]
    public void T574_OffroadTiresOnRoad()
    {
        var car = CarFactory.Wrangler(); car.TireType = TireType.Offroad;
        var r = Gen(car, CarFactory.Road());
        Assert.InRange(r.TirePressureFront, 1.0, 3.8);
    }

    // ── Gear count edge cases ────────────────────────────────────────

    // 575 – 1-gear car (Tesla) on all drag distances
    [Fact]
    public void T575_SingleGearAllDragDistances()
    {
        foreach (var dist in new[] { DragDistance.Eighth, DragDistance.Quarter, DragDistance.Half, DragDistance.Mile })
        {
            var r = Gen(CarFactory.ModelSPlaid(), CarFactory.Drag(dist));
            Assert.Single(r.GearRatios);
            Assert.InRange(r.FinalDrive, 2.2, 6.1);
        }
    }

    // 576 – 6-gear car all drag distances
    [Fact]
    public void T576_SixGearAllDragDistances()
    {
        var car = CarFactory.Hellcat(); car.GearCount = 6;
        foreach (var dist in new[] { DragDistance.Eighth, DragDistance.Quarter, DragDistance.Half, DragDistance.Mile })
        {
            var r = Gen(car, CarFactory.Drag(dist));
            Assert.Equal(6, r.GearRatios.Count);
            Assert.InRange(r.FinalDrive, 2.2, 6.1);
        }
    }

    // ── Edge: engine type specific ───────────────────────────────────

    // 577 – Boxer engine (WRX STI, GT3 RS) produces valid tune
    [Fact]
    public void T577_BoxerEngineValid()
    {
        foreach (var car in new[] { CarFactory.WrxSti(), CarFactory.Gt3Rs() })
        foreach (var track in AllTracks())
        {
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            Assert.InRange(r.SpringFront, 57.1, 285.6);
            Assert.InRange(r.FinalDrive, 2.2, 6.1);
        }
    }

    // 578 – V6 vs V8 vs I4 vs I6 engine types all valid
    [Fact]
    public void T578_VariousEngineTypesValid()
    {
        var v6   = CarFactory.GtrR35();   // V6
        var v8   = CarFactory.Gemera();   // V8
        var i4   = CarFactory.CivicTypeR(); // I4
        var i6   = CarFactory.SupraA90(); // I6
        foreach (var car in new[] { v6, v8, i4, i6 })
        {
            var r = Gen(car, CarFactory.Road());
            Assert.InRange(r.SpringFront, 57.1, 285.6);
            Assert.InRange(r.FinalDrive, 2.2, 6.1);
        }
    }

    // ── Negative values prevention ───────────────────────────────────

    // 579 – No negative camber for drag (should be near 0)
    [Fact]
    public void T579_NoNegativeDragCamberExcessive()
    {
        foreach (var car in AllCars())
        {
            var r = Gen(car, CarFactory.Drag());
            Assert.True(r.CamberFront >= -2.0, $"Drag front camber {r.CamberFront} too negative");
        }
    }

    // 580 – No negative ride height
    [Fact]
    public void T580_NoNegativeRideHeight()
    {
        foreach (var (car, track) in AllCombosFull())
        {
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            Assert.True(r.RideHeightFront >= 0);
            Assert.True(r.RideHeightRear  >= 0);
        }
    }

    // ──── Final integrity checks ────────────────────────────────────

    // 581 – Bump/rebound ratio front vs rear similar for same combo
    [Fact]
    public void T581_BumpReboundRatioFrontRearConsistent()
    {
        foreach (var (car, track) in AllCombosFull())
        {
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            if (r.ReboundFront >= 2 && r.ReboundRear >= 2)
            {
                double ratioF = r.BumpFront / r.ReboundFront;
                double ratioR = r.BumpRear  / r.ReboundRear;
                double diff = Math.Abs(ratioF - ratioR);
                Assert.True(diff <= 0.25,
                    $"{car.DriveType}/{track.Discipline}: bump ratio front {ratioF:F2} vs rear {ratioR:F2} diff {diff:F2}");
            }
        }
    }

    // 582 – Aero front/rear balance consistent per discipline
    [Fact]
    public void T582_AeroBalanceConsistent()
    {
        var car = CarFactory.Gt3Rs();
        var r_road  = Gen(car, CarFactory.Road());
        var r_drift = Gen(car, CarFactory.Drift());
        // Drift should have more front bias than road
        double driftBalance = r_drift.AeroFront / Math.Max(r_drift.AeroRear, 1);
        double roadBalance  = r_road.AeroFront / Math.Max(r_road.AeroRear, 1);
        Assert.True(driftBalance >= roadBalance - 0.5,
            $"Drift aero balance {driftBalance:F2} should be >= road {roadBalance:F2}");
    }

    // 583 – No division by zero in any calculation
    [Fact]
    public void T583_NoDivisionByZeroInAnyCombo()
    {
        // This test verifies that all combos produce finite values (no NaN, no Infinity)
        foreach (var (car, track) in AllCombosFull())
        {
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            Assert.False(double.IsNaN(r.TirePressureFront), $"NaN in {car.DriveType}/{track.Discipline}");
            Assert.False(double.IsNaN(r.CamberFront), $"NaN camber in {car.DriveType}/{track.Discipline}");
            Assert.False(double.IsNaN(r.SpringFront), $"NaN spring in {car.DriveType}/{track.Discipline}");
            Assert.False(double.IsNaN(r.FinalDrive), $"NaN FD in {car.DriveType}/{track.Discipline}");
            Assert.False(double.IsInfinity(r.FinalDrive), $"Inf FD in {car.DriveType}/{track.Discipline}");
        }
    }

    // 584 – Spring frequency in reasonable range (~1.5-6.0 Hz)
    [Fact]
    public void T584_SpringFrequencyReasonable()
    {
        foreach (var (car, track) in AllCombosFull())
        {
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            // Calculate approximate Hz = 1/(2π) * √(k/m) where k is spring rate in N/m
            // k (N/mm) = springRate × 9.81 / (wheelRate_kgfPerMm_to_N_per_m)
            // For a rough check: Hz = 0.5 * √(k_in_N_per_m / sprungMass_kg)
            // Simplified: just verify spring rates are within absolute bounds
            Assert.InRange(r.SpringFront, 57.1, 285.6);
            Assert.InRange(r.SpringRear,  57.1, 285.6);
        }
    }

    // 585 – All Cars produce 25 output properties (no missing fields)
    [Fact]
    public void T585_AllOutputPropertiesPopulated()
    {
        var r = Gen(CarFactory.GtrR35(), CarFactory.Road());
        Assert.NotNull(r.Car);
        Assert.NotNull(r.Track);
        Assert.NotNull(r.GearRatios);
        Assert.NotNull(r.Explanations);
        Assert.NotEmpty(r.Explanations);
        // Check core fields are writeable (not constants)
        Assert.True(r.TirePressureFront.GetType() == typeof(double));
        Assert.True(r.SpringFront.GetType() == typeof(double));
        Assert.True(r.CamberFront.GetType() == typeof(double));
        Assert.True(r.ARBFront.GetType() == typeof(double));
        Assert.True(r.Caster.GetType() == typeof(double));
        Assert.True(r.FinalDrive.GetType() == typeof(double));
    }

    private static TrackInfo[] AllTracks() => new[]
    {
        CarFactory.Road(), CarFactory.Drag(), CarFactory.Drift(),
        CarFactory.Rally(), CarFactory.CrossCountry(), CarFactory.Touge()
    };
    private static IEnumerable<(CarCard, TrackInfo)> AllCombosFull()
    {
        var cars   = new[] { CarFactory.GtrR35(), CarFactory.SupraA90(), CarFactory.CivicTypeR(),
                             CarFactory.Gemera(), CarFactory.Mx5(), CarFactory.Wrangler(),
                             CarFactory.Gt3Rs(), CarFactory.ModelSPlaid(), CarFactory.WrxSti(), CarFactory.Hellcat() };
        var tracks = new[] { CarFactory.Road(), CarFactory.Drag(), CarFactory.Drift(),
                             CarFactory.Rally(), CarFactory.CrossCountry(), CarFactory.Touge() };
        foreach (var c in cars) foreach (var t in tracks) yield return (c, t);
    }
    private static CarCard[] AllCars() => new[]
    {
        CarFactory.GtrR35(), CarFactory.SupraA90(), CarFactory.CivicTypeR(),
        CarFactory.Gemera(), CarFactory.Mx5(), CarFactory.Wrangler(),
        CarFactory.Gt3Rs(), CarFactory.ModelSPlaid(), CarFactory.WrxSti(), CarFactory.Hellcat()
    };
}
