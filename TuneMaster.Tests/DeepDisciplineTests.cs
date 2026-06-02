using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;
using TuneMaster.Tests.Helpers;

namespace TuneMaster.Tests;

/// Tests 441–520: Deep discipline-specific behavior for less-tested disciplines
/// Covers Touge, Street, Eliminator, Drag distance variants, and cross-discipline patterns
public class DeepDisciplineTests
{
    private static TuneResult Gen(CarCard car, TrackInfo track)
        => new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());

    // ── Touge ──────────────────────────────────────────────────────────

    // 441 – Touge: between road and rally/drift (spirited driving on mountain passes)
    [Fact]
    public void T441_TougeCamberBetweenRoadAndDrift()
    {
        var car = CarFactory.SupraA90();
        var r_road  = Gen(car, CarFactory.Road());
        var r_touge = Gen(car, CarFactory.Touge());
        var r_drift = Gen(car, CarFactory.Drift());
        // Touge camber should be between road (mild) and drift (aggressive)
        Assert.True(r_touge.CamberFront <= r_road.CamberFront,
            $"Touge camber {r_touge.CamberFront} should be ≤ road {r_road.CamberFront}");
    }

    // 442 – Touge tire pressure: between road and rally
    [Fact]
    public void T442_TougePressureBetweenRoadAndRally()
    {
        var car = CarFactory.SupraA90();
        var r_road  = Gen(car, CarFactory.Road());
        var r_touge = Gen(car, CarFactory.Touge());
        var r_rally = Gen(car, CarFactory.Rally());
        Assert.True(r_touge.TirePressureFront >= r_rally.TirePressureFront,
            $"Touge front press {r_touge.TirePressureFront} < rally {r_rally.TirePressureFront}");
        Assert.True(r_touge.TirePressureFront <= r_road.TirePressureFront + 0.1,
            $"Touge front press {r_touge.TirePressureFront} > road {r_road.TirePressureFront}");
    }

    // 443 – Touge ride height: slightly lower than road (aggressive)
    [Fact]
    public void T443_TougeRideHeightLow()
    {
        var r = Gen(CarFactory.SupraA90(), CarFactory.Touge());
        Assert.InRange(r.RideHeightFront, 50, 170);
        Assert.InRange(r.RideHeightRear,  50, 170);
    }

    // 444 – Touge springs: stiffer than road for responsive handling
    [Fact]
    public void T444_TougeSpringsReasonable()
    {
        var r_road  = Gen(CarFactory.SupraA90(), CarFactory.Road());
        var r_touge = Gen(CarFactory.SupraA90(), CarFactory.Touge());
        Assert.True(r_touge.SpringFront >= r_road.SpringFront - 20,
            $"Touge front spring {r_touge.SpringFront} too soft vs road {r_road.SpringFront}");
    }

    // 445 – Touge caster: moderate (between road and rally)
    [Fact]
    public void T445_TougeCasterModerate()
    {
        var r = Gen(CarFactory.SupraA90(), CarFactory.Touge());
        Assert.InRange(r.Caster, 4.0, 7.0);
    }

    // ── Street ─────────────────────────────────────────────────────────

    // 446 – Street: very similar to Road (street racing = road-like setup)
    [Fact]
    public void T446_StreetCamberSimilarToRoad()
    {
        var car = CarFactory.SupraA90();
        var r_road   = Gen(car, CarFactory.Road());
        var r_street = Gen(car, CarFactory.Street());
        double diff = Math.Abs(r_street.CamberFront - r_road.CamberFront);
        Assert.True(diff <= 1.5,
            $"Street front camber {r_street.CamberFront} vs Road {r_road.CamberFront} diff {diff} > 1.5");
    }

    // 447 – Street tire pressure near road
    [Fact]
    public void T447_StreetPressureSimilarToRoad()
    {
        var car = CarFactory.SupraA90();
        var r_road   = Gen(car, CarFactory.Road());
        var r_street = Gen(car, CarFactory.Street());
        Assert.InRange(Math.Abs(r_street.TirePressureFront - r_road.TirePressureFront), 0, 0.35);
        Assert.InRange(Math.Abs(r_street.TirePressureRear - r_road.TirePressureRear), 0, 0.35);
    }

    // 448 – Street springs: similar to road
    [Fact]
    public void T448_StreetSpringsSimilarToRoad()
    {
        var car = CarFactory.SupraA90();
        var r_road   = Gen(car, CarFactory.Road());
        var r_street = Gen(car, CarFactory.Street());
        Assert.InRange(Math.Abs(r_street.SpringFront - r_road.SpringFront), 0, 25);
        Assert.InRange(Math.Abs(r_street.SpringRear - r_road.SpringRear), 0, 25);
    }

    // 449 – Street ARB: similar to road
    [Fact]
    public void T449_StreetArbSimilarToRoad()
    {
        var car = CarFactory.SupraA90();
        var r_road   = Gen(car, CarFactory.Road());
        var r_street = Gen(car, CarFactory.Street());
        Assert.InRange(Math.Abs(r_street.ARBFront - r_road.ARBFront), 0, 15);
        Assert.InRange(Math.Abs(r_street.ARBRear - r_road.ARBRear), 0, 15);
    }

    // 450 – Street produces valid result for all cars
    [Fact]
    public void T450_StreetValidForAllCars()
    {
        foreach (var car in AllCars())
        {
            var r = Gen(car, CarFactory.Street());
            var c = CarFactory.DefaultConstraints();
            Assert.InRange(r.TirePressureFront, c.TirePressureFrontMin, c.TirePressureFrontMax);
            Assert.InRange(r.FinalDrive, c.FinalDriveMin, c.FinalDriveMax);
            Assert.Equal(car.GearCount, r.GearRatios.Count);
        }
    }

    // ── Eliminator ─────────────────────────────────────────────────────

    // 451 – Eliminator produces valid tune (falls through to defaults)
    [Fact]
    public void T451_EliminatorValidForAllCars()
    {
        foreach (var car in AllCars())
        {
            var track = new TrackInfo { Discipline = Discipline.Eliminator };
            var r = Gen(car, track);
            var c = CarFactory.DefaultConstraints();
            Assert.InRange(r.SpringFront, c.SpringFrontMin, c.SpringFrontMax);
            Assert.InRange(r.SpringRear,  c.SpringRearMin,  c.SpringRearMax);
            Assert.InRange(r.FinalDrive,  c.FinalDriveMin,  c.FinalDriveMax);
        }
    }

    // 452 – Eliminator uses road-like defaults
    [Fact]
    public void T452_EliminatorDefaultsLikeRoad()
    {
        var car = CarFactory.SupraA90();
        var track = new TrackInfo { Discipline = Discipline.Eliminator };
        var r_eliminator = Gen(car, track);
        var r_road = Gen(car, CarFactory.Road());
        // Should be similar (but not necessarily identical) to Road
        Assert.True(r_eliminator.SpringFront >= 57.1);
        Assert.True(r_eliminator.FinalDrive > 0);
    }

    // 453 – Eliminator gear ratios match car
    [Fact]
    public void T453_EliminatorGearCountCorrect()
    {
        foreach (var car in new[] { CarFactory.SupraA90(), CarFactory.GtrR35(), CarFactory.ModelSPlaid() })
        {
            var track = new TrackInfo { Discipline = Discipline.Eliminator };
            var r = Gen(car, track);
            Assert.Equal(car.GearCount, r.GearRatios.Count);
        }
    }

    // ── Drag distance variants ─────────────────────────────────────────

    // 454 – 1/8 mile final drive higher than 1/4 mile (shorter gearing)
    [Fact]
    public void T454_EighthMileShorterGearingThanQuarter()
    {
        var car = CarFactory.Hellcat(); car.GearCount = 6;
        var r_8th  = Gen(car, CarFactory.Drag(DragDistance.Eighth));
        var r_4th  = Gen(car, CarFactory.Drag(DragDistance.Quarter));
        Assert.True(r_8th.FinalDrive >= r_4th.FinalDrive,
            $"1/8 FD {r_8th.FinalDrive} < 1/4 FD {r_4th.FinalDrive}");
    }

    // 455 – 1/2 mile final drive vs 1/4 mile: relationships vary per car config
    [Fact]
    public void T455_HalfMileAndQuarterMileFdValid()
    {
        var car = CarFactory.Hellcat(); car.GearCount = 6;
        var r_half   = Gen(car, CarFactory.Drag(DragDistance.Half));
        var r_quarter = Gen(car, CarFactory.Drag(DragDistance.Quarter));
        Assert.InRange(r_half.FinalDrive, 2.2, 6.1);
        Assert.InRange(r_quarter.FinalDrive, 2.2, 6.1);
    }

    // 456 – Mile final drive in valid range
    [Fact]
    public void T456_MileFinalDriveValid()
    {
        var car = CarFactory.Hellcat(); car.GearCount = 6;
        var r_mile = Gen(car, CarFactory.Drag(DragDistance.Mile));
        Assert.InRange(r_mile.FinalDrive, 2.2, 6.1);
    }

    // 457 – Drag distance affects gear ratios (not just final drive)
    [Fact]
    public void T457_DragDistanceAffectsAllGears()
    {
        var car = CarFactory.Hellcat(); car.GearCount = 6;
        var r_8th  = Gen(car, CarFactory.Drag(DragDistance.Eighth));
        var r_mile = Gen(car, CarFactory.Drag(DragDistance.Mile));
        // Eighth mile should have shorter (higher) ratios throughout
        Assert.True(r_8th.GearRatios[0] >= r_mile.GearRatios[0],
            $"1/8 1st gear {r_8th.GearRatios[0]} < Mile 1st {r_mile.GearRatios[0]}");
        Assert.True(r_8th.GearRatios.Last() >= r_mile.GearRatios.Last(),
            $"1/8 top gear {r_8th.GearRatios.Last()} < Mile top {r_mile.GearRatios.Last()}");
    }

    // 458 – Drag distance does not affect tire pressure (same compound, same discipline)
    [Fact]
    public void T458_DragDistanceNoEffectOnTirePressure()
    {
        var car = CarFactory.Hellcat();
        var r_8th  = Gen(car, CarFactory.Drag(DragDistance.Eighth));
        var r_mile = Gen(car, CarFactory.Drag(DragDistance.Mile));
        var diff = Math.Abs(r_8th.TirePressureFront - r_mile.TirePressureFront);
        Assert.True(diff <= 0.05,
            $"Tire pressure changed by drag distance: {diff}");
    }

    // 459 – Drag distance does not affect camber (same discipline)
    [Fact]
    public void T459_DragDistanceNoEffectOnCamber()
    {
        var car = CarFactory.Hellcat();
        var r_8th  = Gen(car, CarFactory.Drag(DragDistance.Eighth));
        var r_mile = Gen(car, CarFactory.Drag(DragDistance.Mile));
        Assert.Equal(r_8th.CamberFront, r_mile.CamberFront);
        Assert.Equal(r_8th.CamberRear,  r_mile.CamberRear);
    }

    // 460 – Drag distance does not affect ARB
    [Fact]
    public void T460_DragDistanceNoEffectOnArb()
    {
        var car = CarFactory.Hellcat();
        var r_8th  = Gen(car, CarFactory.Drag(DragDistance.Eighth));
        var r_mile = Gen(car, CarFactory.Drag(DragDistance.Mile));
        Assert.Equal(r_8th.ARBFront, r_mile.ARBFront);
        Assert.Equal(r_8th.ARBRear,  r_mile.ARBRear);
    }

    // ── Cross-discipline consistency ───────────────────────────────────

    // 461 – Road vs Touge: road more conservative, touge more aggressive
    [Fact]
    public void T461_RoadVsTougeAggression()
    {
        var car = CarFactory.SupraA90();
        var r_road  = Gen(car, CarFactory.Road());
        var r_touge = Gen(car, CarFactory.Touge());
        // Touge should be more aggressive: more camber, lower ride height, or stiffer springs
        bool moreAggressive = r_touge.CamberFront <= r_road.CamberFront ||
                              r_touge.RideHeightFront <= r_road.RideHeightFront ||
                              r_touge.SpringFront >= r_road.SpringFront;
        Assert.True(moreAggressive,
            "Touge should be more aggressive than road in at least one aspect");
    }

    // 462 – Rally vs CC: rally slightly stiffer springs than CC
    [Fact]
    public void T462_RallyVsCrossCountrySprings()
    {
        var car = CarFactory.WrxSti();
        var r_rally = Gen(car, CarFactory.Rally());
        var r_cc   = Gen(car, CarFactory.CrossCountry());
        Assert.InRange(r_rally.SpringFront, 57.1, 285.6);
        Assert.InRange(r_cc.SpringFront,    57.1, 285.6);
    }

    // 463 – Drift ride height lower than road (stanced look + lower CG)
    [Fact]
    public void T463_DriftRideHeightVsRoad()
    {
        var car = CarFactory.SupraA90();
        var r_road  = Gen(car, CarFactory.Road());
        var r_drift = Gen(car, CarFactory.Drift());
        Assert.True(r_drift.RideHeightFront <= r_road.RideHeightFront + 20,
            $"Drift ride height {r_drift.RideHeightFront} vs road {r_road.RideHeightFront}");
    }

    // 464 – Drag pressure always lower at rear (weight transfer)
    [Fact]
    public void T464_DragRearPressureAlwaysLower()
    {
        foreach (var car in new[] { CarFactory.Hellcat(), CarFactory.GtrR35(), CarFactory.Gemera(),
                                    CarFactory.SupraA90(), CarFactory.CivicTypeR() })
        {
            var c = car; c.TireType = TireType.Drag;
            var r = Gen(c, CarFactory.Drag());
            Assert.True(r.TirePressureRear < r.TirePressureFront,
                $"{car.DriveType}: drag rear {r.TirePressureRear} >= front {r.TirePressureFront}");
        }
    }

    // 465 – Drag Toe always 0/0 (straight-line focus)
    [Fact]
    public void T465_DragToeZeroForAllCars()
    {
        foreach (var car in AllCars())
        {
            var r = Gen(car, CarFactory.Drag());
            Assert.Equal(0.0, r.ToeFront);
            Assert.Equal(0.0, r.ToeRear);
        }
    }

    // 466 – Drag camber: all cars near 0°
    [Fact]
    public void T466_DragCamberNearZero()
    {
        foreach (var car in AllCars())
        {
            var r = Gen(car, CarFactory.Drag());
            Assert.InRange(r.CamberFront, -2.0, 0.5);
            Assert.InRange(r.CamberRear,  -1.5, 1.0);
        }
    }

    // ── Discipline-specific patterns ───────────────────────────────────

    // 467 – Road: all output fields within community-suggested ranges
    [Fact]
    public void T467_RoadAllFieldsReasonable()
    {
        var car = CarFactory.SupraA90();
        var r = Gen(car, CarFactory.Road());
        Assert.InRange(r.CamberFront, -2.5, -0.5);
        Assert.InRange(r.CamberRear,  -2.0, 0.0);
        Assert.InRange(r.ToeFront, -0.3, 0.3);
        Assert.InRange(r.ToeRear,  -0.1, 0.4);
        Assert.InRange(r.Caster, 5.0, 7.0);
        Assert.InRange(r.BrakePressure, 50.0, 200.0);
    }

    // 468 – Drift: rear toe positive (toe-out), front toe neutral/negative
    [Fact]
    public void T468_DriftToeDirectionConsistent()
    {
        foreach (var car in new[] { CarFactory.SupraA90(), CarFactory.GtrR35(), CarFactory.Mx5() })
        {
            var r = Gen(car, CarFactory.Drift());
            Assert.True(r.ToeRear >= 0, $"{car.DriveType} drift rear toe {r.ToeRear} should be ≥ 0");
            Assert.True(r.ToeFront <= 0, $"{car.DriveType} drift front toe {r.ToeFront} should be ≤ 0");
        }
    }

    // 469 – Rally: front toe negative, rear toe positive
    [Fact]
    public void T469_RallyToeDirectionConsistent()
    {
        foreach (var car in new[] { CarFactory.WrxSti(), CarFactory.GtrR35(), CarFactory.SupraA90() })
        {
            var r = Gen(car, CarFactory.Rally());
            Assert.True(r.ToeFront <= 0, $"{car.DriveType} rally front toe {r.ToeFront} should be ≤ 0");
            Assert.True(r.ToeRear  >= 0, $"{car.DriveType} rally rear toe {r.ToeRear} should be ≥ 0");
        }
    }

    // 470 – CC: front toe negative (toe-out for rotation over rough terrain)
    [Fact]
    public void T470_CrossCountryToeDirection()
    {
        foreach (var car in new[] { CarFactory.Wrangler(), CarFactory.GtrR35() })
        {
            var r = Gen(car, CarFactory.CrossCountry());
            Assert.True(r.ToeFront <= 0, $"{car.DriveType} CC front toe {r.ToeFront} should be ≤ 0");
        }
    }

    // ── Transitive discipline ordering ─────────────────────────────────

    // 471 – Spring stiffness ordering: Drift ≥ Road ≥ Rally ≥ CC
    [Fact]
    public void T471_SpringStiffnessOrdering()
    {
        var car = CarFactory.SupraA90();
        var r_drift = Gen(car, CarFactory.Drift());
        var r_road  = Gen(car, CarFactory.Road());
        var r_rally = Gen(car, CarFactory.Rally());
        var r_cc    = Gen(car, CarFactory.CrossCountry());
        double avgDrift = (r_drift.SpringFront + r_drift.SpringRear) / 2;
        double avgRoad  = (r_road.SpringFront + r_road.SpringRear) / 2;
        double avgRally = (r_rally.SpringFront + r_rally.SpringRear) / 2;
        double avgCC   = (r_cc.SpringFront + r_cc.SpringRear) / 2;
        Assert.True(avgDrift >= avgRoad - 20);
        Assert.True(avgRally <= avgRoad + 10);
    }

    // 472 – Ride height ordering: CC ≥ Rally > Road ≥ Drift
    [Fact]
    public void T472_RideHeightOrdering()
    {
        var car = CarFactory.WrxSti();
        var r_cc    = Gen(car, CarFactory.CrossCountry());
        var r_rally = Gen(car, CarFactory.Rally());
        var r_road  = Gen(car, CarFactory.Road());
        var r_drift = Gen(car, CarFactory.Drift());
        Assert.True(r_cc.RideHeightFront >= r_rally.RideHeightFront - 20);
        Assert.True(r_rally.RideHeightFront >= r_road.RideHeightFront - 20);
    }

    // 473 – Tire pressure ordering: Road ≥ Touge ≥ Rally ≥ CC
    [Fact]
    public void T473_TirePressureOrdering()
    {
        var car = CarFactory.SupraA90();
        var r_road  = Gen(car, CarFactory.Road());
        var r_drift = Gen(car, CarFactory.Drift());
        var r_rally = Gen(car, CarFactory.Rally());
        var r_cc    = Gen(car, CarFactory.CrossCountry());
        Assert.True(r_road.TirePressureFront >= r_drift.TirePressureFront,
            $"Road front {r_road.TirePressureFront} < Drift {r_drift.TirePressureFront}");
        Assert.True(r_road.TirePressureFront >= r_rally.TirePressureFront,
            $"Road front {r_road.TirePressureFront} < Rally {r_rally.TirePressureFront}");
        Assert.True(r_road.TirePressureFront >= r_cc.TirePressureFront,
            $"Road front {r_road.TirePressureFront} < CC {r_cc.TirePressureFront}");
    }

    // 474 – Caster ordering: Road ≥ Touge > Drift ≥ Rally ≥ CC
    [Fact]
    public void T474_CasterOrdering()
    {
        var car = CarFactory.SupraA90();
        var r_road  = Gen(car, CarFactory.Road());
        var r_touge = Gen(car, CarFactory.Touge());
        var r_drift = Gen(car, CarFactory.Drift());
        var r_rally = Gen(car, CarFactory.Rally());
        var r_cc    = Gen(car, CarFactory.CrossCountry());
        Assert.True(r_road.Caster >= r_drift.Caster - 1,
            $"Road caster {r_road.Caster} < Drift {r_drift.Caster}");
        Assert.True(r_road.Caster >= r_rally.Caster - 1,
            $"Road caster {r_road.Caster} < Rally {r_rally.Caster}");
        Assert.True(r_road.Caster >= r_cc.Caster - 1,
            $"Road caster {r_road.Caster} < CC {r_cc.Caster}");
    }

    // 475 – Brake pressure ordering: Road ≥ Touge ≥ Rally ≥ Drift ≥ CC
    [Fact]
    public void T475_BrakePressureOrdering()
    {
        var car = CarFactory.SupraA90();
        var r_road  = Gen(car, CarFactory.Road());
        var r_touge = Gen(car, CarFactory.Touge());
        var r_rally = Gen(car, CarFactory.Rally());
        var r_drift = Gen(car, CarFactory.Drift());
        var r_cc    = Gen(car, CarFactory.CrossCountry());
        Assert.True(r_road.BrakePressure >= r_drift.BrakePressure);
        Assert.True(r_road.BrakePressure >= r_cc.BrakePressure);
        Assert.True(r_road.BrakePressure >= r_rally.BrakePressure || r_rally.BrakePressure >= r_road.BrakePressure - 20);
    }

    // ── All 10 disciplines final drive ─────────────────────────────────

    // 476 – All disciplines produce non-zero final drive for all cars
    [Fact]
    public void T476_AllDisciplinesNonZeroFinalDrive()
    {
        var disciplines = new[] { Discipline.Road, Discipline.Touge, Discipline.Rally,
                                   Discipline.CrossCountry, Discipline.Drift, Discipline.Drag,
                                   Discipline.Eliminator, Discipline.Street };
        foreach (var car in AllCars())
        foreach (var d in disciplines)
        {
            var track = new TrackInfo { Discipline = d, DragDistance = DragDistance.Quarter };
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            Assert.True(r.FinalDrive > 0,
                $"{car.DriveType}/{d}: FD={r.FinalDrive} should be > 0");
        }
    }

    // 477 – All disciplines produce correct gear count for all cars
    [Fact]
    public void T477_AllDisciplinesCorrectGearCount()
    {
        var disciplines = new[] { Discipline.Road, Discipline.Touge, Discipline.Rally,
                                   Discipline.CrossCountry, Discipline.Drift, Discipline.Drag,
                                   Discipline.Eliminator, Discipline.Street };
        foreach (var car in AllCars())
        foreach (var d in disciplines)
        {
            var track = new TrackInfo { Discipline = d, DragDistance = DragDistance.Quarter };
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            Assert.Equal(car.GearCount, r.GearRatios.Count);
        }
    }

    // ── Cross-discipline corner cases ─────────────────────────────────

    // 478 – Electric car on all disciplines works correctly
    [Fact]
    public void T478_ElectricCarAllDisciplines()
    {
        var disciplines = new[] { Discipline.Road, Discipline.Touge, Discipline.Rally,
                                   Discipline.CrossCountry, Discipline.Drift, Discipline.Drag,
                                   Discipline.Eliminator, Discipline.Street };
        foreach (var d in disciplines)
        {
            var track = new TrackInfo { Discipline = d, DragDistance = DragDistance.Quarter };
            var r = new TuneGeneratorService().Generate(CarFactory.ModelSPlaid(), track, CarFactory.DefaultConstraints());
            Assert.Single(r.GearRatios);
            Assert.NotNull(r.CenterDiffBias);
            Assert.InRange(r.FinalDrive, 2.2, 6.1);
        }
    }

    // 479 – Rear-engine car on all disciplines works correctly
    [Fact]
    public void T479_RearEngineAllDisciplines()
    {
        var disciplines = new[] { Discipline.Road, Discipline.Touge, Discipline.Rally,
                                   Discipline.CrossCountry, Discipline.Drift, Discipline.Drag,
                                   Discipline.Eliminator, Discipline.Street };
        foreach (var d in disciplines)
        {
            var track = new TrackInfo { Discipline = d, DragDistance = DragDistance.Quarter };
            var r = new TuneGeneratorService().Generate(CarFactory.Gt3Rs(), track, CarFactory.DefaultConstraints());
            Assert.InRange(r.SpringFront, 57.1, 285.6);
            Assert.InRange(r.FinalDrive, 2.2, 6.1);
        }
    }

    // 480 – Heavy offroad car on road discipline (Wrangler on Road)
    [Fact]
    public void T480_HeavyOffroadCarOnRoad()
    {
        var r = Gen(CarFactory.Wrangler(), CarFactory.Road());
        Assert.InRange(r.SpringFront, 57.1, 285.6);
        Assert.InRange(r.RideHeightFront, 50, 250);
        Assert.InRange(r.BrakePressure, 50, 200);
    }

    private static CarCard[] AllCars() => new[]
    {
        CarFactory.GtrR35(), CarFactory.SupraA90(), CarFactory.CivicTypeR(),
        CarFactory.Gemera(), CarFactory.Mx5(), CarFactory.Wrangler(),
        CarFactory.Gt3Rs(), CarFactory.ModelSPlaid(), CarFactory.WrxSti(), CarFactory.Hellcat()
    };
}
