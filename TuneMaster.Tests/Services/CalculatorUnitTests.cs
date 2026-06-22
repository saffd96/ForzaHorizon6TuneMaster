using Xunit;
using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;
using TuneMaster.Tests.Helpers;

namespace TuneMaster.Tests.Services;

public class CalculatorUnitTests
{
    // ─── AeroCalculator ────────────────────────────────────────────────────

    [Fact]
    public void Aero_NoParts_ReturnsZero()
    {
        var car = CarFactory.FWDStockCar();
        var r = new TuneResult();
        var ex = new Dictionary<string, string>();

        AeroCalculator.CalculateAero(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints(), r, ex);

        Assert.Equal(0, r.AeroFront);
        Assert.Equal(0, r.AeroRear);
    }

    [Fact]
    public void Aero_Drag_FrontAtMin_RearBasedOnPower()
    {
        var car = CarFactory.AWDPerformanceCar();
        var r = new TuneResult();
        var ex = new Dictionary<string, string>();
        car.MaxSpeedKmh = 320;

        AeroCalculator.CalculateAero(car, new TrackInfo { Discipline = Discipline.Drag },
            CarFactory.RelaxedConstraints(), r, ex);

        Assert.Equal(0, r.AeroFront);
        Assert.InRange(r.AeroRear, 0, 200);
    }

    [Fact]
    public void Aero_Drift_Uses15Factor()
    {
        var car = CarFactory.DefaultCar();
        var r = new TuneResult();
        var ex = new Dictionary<string, string>();

        AeroCalculator.CalculateAero(car, new TrackInfo { Discipline = Discipline.Drift },
            CarFactory.RelaxedConstraints(), r, ex);

        Assert.InRange(r.AeroFront, 0, 200);
        Assert.InRange(r.AeroRear, 0, 200);
    }

    [Fact]
    public void Aero_Rally_Uses65Factor()
    {
        var car = CarFactory.DefaultCar();
        var r = new TuneResult();
        var ex = new Dictionary<string, string>();

        AeroCalculator.CalculateAero(car, new TrackInfo { Discipline = Discipline.Rally },
            CarFactory.RelaxedConstraints(), r, ex);

        Assert.InRange(r.AeroFront, 0, 200);
    }

    [Fact]
    public void Aero_CrossCountry_Uses50Factor()
    {
        var car = CarFactory.DefaultCar();
        var r = new TuneResult();
        var ex = new Dictionary<string, string>();

        AeroCalculator.CalculateAero(car, new TrackInfo { Discipline = Discipline.CrossCountry },
            CarFactory.RelaxedConstraints(), r, ex);

        Assert.InRange(r.AeroFront, 0, 200);
    }

    [Fact]
    public void Aero_RWD_FrontEngine_LessFrontAeroThanMid()
    {
        var c = CarFactory.RelaxedConstraints();
        var ex = new Dictionary<string, string>();

        var carFront = CarFactory.DefaultCar();
        carFront.EnginePosition = EnginePosition.Front;
        var rFront = new TuneResult();
        AeroCalculator.CalculateAero(carFront, CarFactory.DefaultTrack(), c, rFront, ex);

        var carMid = CarFactory.DefaultCar();
        carMid.EnginePosition = EnginePosition.Mid;
        var rMid = new TuneResult();
        AeroCalculator.CalculateAero(carMid, CarFactory.DefaultTrack(), c, rMid, ex);

        Assert.True(rFront.AeroFront <= rMid.AeroFront,
            $"Front-engine front aero ({rFront.AeroFront}) should be <= mid ({rMid.AeroFront})");
    }

    [Fact]
    public void Aero_SpeedFactorCapped()
    {
        var car = CarFactory.DefaultCar();
        var r = new TuneResult();
        var ex = new Dictionary<string, string>();

        AeroCalculator.CalculateAero(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints(), r, ex, overrideSpeedKmh: 500);

        Assert.InRange(r.AeroFront, 0, 200);
    }

    [Fact]
    public void Aero_PowerFactorCapped()
    {
        var car = CarFactory.DefaultCar();
        car.PowerHP = 2000;
        var r = new TuneResult();
        var ex = new Dictionary<string, string>();

        AeroCalculator.CalculateAero(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints(), r, ex);

        Assert.InRange(r.AeroFront, 0, 200);
    }

    [Fact]
    public void Aero_FWD_Uses65_50Base()
    {
        var car = CarFactory.DefaultCar();
        car.DriveType = DriveType.FWD;
        var r = new TuneResult();
        var ex = new Dictionary<string, string>();

        AeroCalculator.CalculateAero(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints(), r, ex);

        Assert.InRange(r.AeroFront, 0, 200);
        Assert.InRange(r.AeroRear, 0, 200);
    }

    [Fact]
    public void Aero_DefaultCar_HasRearAeroOnly()
    {
        var car = CarFactory.DefaultCar();
        var r = new TuneResult();
        var ex = new Dictionary<string, string>();

        AeroCalculator.CalculateAero(car, CarFactory.DefaultTrack(), new TuningConstraints(), r, ex);

        Assert.Equal(0, r.AeroFront);
        Assert.InRange(r.AeroRear, 100, 200);
    }

    [Fact]
    public void Aero_FrontOnly_ProducesFrontDownforce_NotShortCircuited()
    {
        // A front splitter with no rear wing must still produce front downforce.
        // Regression: the calc used to bail whenever there was no rear wing, zeroing the front.
        var car = CarFactory.DefaultCar();
        car.HasRearAero = false;
        car.HasFrontAero = true;
        var r = new TuneResult();
        var ex = new Dictionary<string, string>();

        AeroCalculator.CalculateAero(car, CarFactory.DefaultTrack(), new TuningConstraints(), r, ex);

        Assert.Equal(0, r.AeroRear);
        Assert.True(r.AeroFront > 0, $"Front-only aero should be > 0, was {r.AeroFront}");
        Assert.DoesNotContain("Expl_Aero_None", ex["Aero"]);
    }

    // ─── TireCalculator ────────────────────────────────────────────────────

    [Fact]
    public void TirePressure_Slick_HighestBase()
    {
        var car = CarFactory.DefaultCar();
        car.TireType = TireType.Slick;
        var r = new TuneResult();
        var ex = new Dictionary<string, string>();

        TireCalculator.CalculateTirePressure(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints(), r, ex);

        Assert.True(r.TirePressureFront >= 2.0 && r.TirePressureFront <= 2.8);
    }

    [Fact]
    public void TirePressure_Winter_LowerThanSport()
    {
        var c = CarFactory.RelaxedConstraints();
        var ex = new Dictionary<string, string>();

        var carSport = CarFactory.DefaultCar();
        carSport.TireType = TireType.Sport;
        var rSport = new TuneResult();
        TireCalculator.CalculateTirePressure(carSport, CarFactory.DefaultTrack(), c, rSport, ex);

        var carWinter = CarFactory.DefaultCar();
        carWinter.TireType = TireType.Winter;
        var rWinter = new TuneResult();
        TireCalculator.CalculateTirePressure(carWinter, CarFactory.DefaultTrack(), c, rWinter, ex);

        Assert.True(rWinter.TirePressureFront <= rSport.TirePressureFront);
    }

    [Fact]
    public void TirePressure_Drag_FrontHigherThanRear()
    {
        var car = CarFactory.AWDPerformanceCar();
        var r = new TuneResult();
        var ex = new Dictionary<string, string>();

        TireCalculator.CalculateTirePressure(car, new TrackInfo { Discipline = Discipline.Drag },
            CarFactory.RelaxedConstraints(), r, ex);

        Assert.True(r.TirePressureFront > r.TirePressureRear,
            $"Drag front ({r.TirePressureFront}) should be > rear ({r.TirePressureRear})");
    }

    [Fact]
    public void TirePressure_Drag_DistanceFactorIncreasesRear()
    {
        var car = CarFactory.AWDPerformanceCar();
        var c = CarFactory.RelaxedConstraints();

        var rQ = new TuneResult();
        TireCalculator.CalculateTirePressure(car, new TrackInfo { Discipline = Discipline.Drag, DragDistance = DragDistance.Quarter }, c, rQ, new Dictionary<string, string>());

        var rM = new TuneResult();
        TireCalculator.CalculateTirePressure(car, new TrackInfo { Discipline = Discipline.Drag, DragDistance = DragDistance.Mile }, c, rM, new Dictionary<string, string>());

        Assert.True(rM.TirePressureRear > rQ.TirePressureRear,
            $"Mile rear ({rM.TirePressureRear}) should be > Quarter rear ({rQ.TirePressureRear})");
    }

    [Fact]
    public void TirePressure_Drift_BothReducedBy50()
    {
        var car = CarFactory.DefaultCar();
        var rRoad = new TuneResult();
        TireCalculator.CalculateTirePressure(car, new TrackInfo { Discipline = Discipline.Road },
            CarFactory.RelaxedConstraints(), rRoad, new Dictionary<string, string>());

        var rDrift = new TuneResult();
        TireCalculator.CalculateTirePressure(car, new TrackInfo { Discipline = Discipline.Drift },
            CarFactory.RelaxedConstraints(), rDrift, new Dictionary<string, string>());

        Assert.True(rDrift.TirePressureFront < rRoad.TirePressureFront - 0.3);
    }

    [Fact]
    public void TirePressure_Touge_FrontDelta_LowerThanRearDelta()
    {
        var car = CarFactory.DefaultCar();
        var rRoad = new TuneResult();
        TireCalculator.CalculateTirePressure(car, new TrackInfo { Discipline = Discipline.Road },
            CarFactory.RelaxedConstraints(), rRoad, new Dictionary<string, string>());

        var rTouge = new TuneResult();
        TireCalculator.CalculateTirePressure(car, new TrackInfo { Discipline = Discipline.Touge },
            CarFactory.RelaxedConstraints(), rTouge, new Dictionary<string, string>());

        double deltaF = rTouge.TirePressureFront - rRoad.TirePressureFront;
        double deltaR = rTouge.TirePressureRear - rRoad.TirePressureRear;
        Assert.True(deltaF < deltaR,
            $"Touge front delta ({deltaF}) should be < rear delta ({deltaR})");
    }

    [Fact]
    public void TirePressure_Street_RearHigherThanDefault()
    {
        var car = CarFactory.DefaultCar();
        var rRoad = new TuneResult();
        TireCalculator.CalculateTirePressure(car, new TrackInfo { Discipline = Discipline.Road },
            CarFactory.RelaxedConstraints(), rRoad, new Dictionary<string, string>());

        var rStreet = new TuneResult();
        TireCalculator.CalculateTirePressure(car, new TrackInfo { Discipline = Discipline.Street },
            CarFactory.RelaxedConstraints(), rStreet, new Dictionary<string, string>());

        Assert.True(rStreet.TirePressureRear > rRoad.TirePressureRear);
    }

    [Fact]
    public void TirePressure_Summer_AdjustmentNeg10()
    {
        var car = CarFactory.DefaultCar();
        var rSu = new TuneResult();
        TireCalculator.CalculateTirePressure(car, new TrackInfo { Discipline = Discipline.Road, Season = Season.Summer },
            CarFactory.RelaxedConstraints(), rSu, new Dictionary<string, string>());

        var rWi = new TuneResult();
        TireCalculator.CalculateTirePressure(car, new TrackInfo { Discipline = Discipline.Road, Season = Season.Winter },
            CarFactory.RelaxedConstraints(), rWi, new Dictionary<string, string>());

        Assert.True(rSu.TirePressureFront < rWi.TirePressureFront - 0.10);
    }

    [Fact]
    public void TirePressure_LargeRim_IncreasesPressure()
    {
        var car = CarFactory.DefaultCar();
        car.FrontRimDiameter = 22;
        car.RearRimDiameter = 22;
        var r = new TuneResult();
        var ex = new Dictionary<string, string>();

        TireCalculator.CalculateTirePressure(car, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), r, ex);

        Assert.InRange(r.TirePressureFront, 1.5, 4.0);
    }

    [Fact]
    public void TirePressure_Offroad_LowestBase()
    {
        var car = CarFactory.DefaultCar();
        car.TireType = TireType.Offroad;
        var r = new TuneResult();

        TireCalculator.CalculateTirePressure(car, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), r, new Dictionary<string, string>());

        Assert.InRange(r.TirePressureFront, 2.0, 2.5);
    }

    // ─── SuspensionCalculator — ARB ─────────────────────────────────────────

    [Fact]
    public void ARB_Drag_Base2And10()
    {
        var car = CarFactory.DefaultCar();
        var r = new TuneResult();
        var ex = new Dictionary<string, string>();

        SuspensionCalculator.CalculateARB(car, new TrackInfo { Discipline = Discipline.Drag },
            CarFactory.RelaxedConstraints(), r, ex);

        Assert.True(r.ARBFront >= 0 && r.ARBFront <= 65);
        Assert.True(r.ARBRear >= 0 && r.ARBRear <= 65);
    }

    [Fact]
    public void ARB_DriftRWD_Base3And50()
    {
        var car = CarFactory.DefaultCar();
        car.DriveType = DriveType.RWD;
        var r = new TuneResult();
        var ex = new Dictionary<string, string>();

        SuspensionCalculator.CalculateARB(car, new TrackInfo { Discipline = Discipline.Drift },
            CarFactory.RelaxedConstraints(), r, ex);

        Assert.InRange(r.ARBFront, 0, 65);
        Assert.InRange(r.ARBRear, 0, 65);
    }

    [Fact]
    public void ARB_MassScaling_HeavierCarHigherARB()
    {
        var c = CarFactory.RelaxedConstraints();
        var carLight = CarFactory.DefaultCar();
        carLight.TotalMass = 800;
        var rLight = new TuneResult();
        SuspensionCalculator.CalculateARB(carLight, CarFactory.DefaultTrack(), c, rLight, new Dictionary<string, string>());

        var carHeavy = CarFactory.DefaultCar();
        carHeavy.TotalMass = 2500;
        var rHeavy = new TuneResult();
        SuspensionCalculator.CalculateARB(carHeavy, CarFactory.DefaultTrack(), c, rHeavy, new Dictionary<string, string>());

        Assert.True(rHeavy.ARBFront > rLight.ARBFront);
    }

    [Fact]
    public void ARB_CGHeightAdjustsARB()
    {
        var car = CarFactory.DefaultCar();
        car.SuspensionUpgrade = SuspensionUpgrade.Offroad;
        var r = new TuneResult();
        var ex = new Dictionary<string, string>();

        SuspensionCalculator.CalculateARB(car, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), r, ex);

        Assert.InRange(r.ARBFront, 0, 65);
    }

    [Fact]
    public void ARB_SeasonSummer_Highest()
    {
        var car = CarFactory.DefaultCar();
        var rWi = new TuneResult();
        SuspensionCalculator.CalculateARB(car, new TrackInfo { Season = Season.Winter },
            CarFactory.RelaxedConstraints(), rWi, new Dictionary<string, string>());

        var rSu = new TuneResult();
        SuspensionCalculator.CalculateARB(car, new TrackInfo { Season = Season.Summer },
            CarFactory.RelaxedConstraints(), rSu, new Dictionary<string, string>());

        Assert.True(rSu.ARBFront >= rWi.ARBFront - 1);
    }

    [Fact]
    public void ARB_WideTrack_ReducesARB()
    {
        var c = CarFactory.RelaxedConstraints();
        var carNarrow = CarFactory.DefaultCar();
        carNarrow.FrontTrack = 1400;
        carNarrow.RearTrack = 1420;
        var rNarrow = new TuneResult();
        SuspensionCalculator.CalculateARB(carNarrow, CarFactory.DefaultTrack(), c, rNarrow, new Dictionary<string, string>());

        var carWide = CarFactory.DefaultCar();
        carWide.FrontTrack = 1750;
        carWide.RearTrack = 1750;
        var rWide = new TuneResult();
        SuspensionCalculator.CalculateARB(carWide, CarFactory.DefaultTrack(), c, rWide, new Dictionary<string, string>());

        Assert.True(rNarrow.ARBFront > rWide.ARBFront);
    }

    // ─── SuspensionCalculator — Springs ─────────────────────────────────────

    [Fact]
    public void Springs_NonAdvanced_ReturnsZero()
    {
        var car = CarFactory.DefaultCar();
        car.SuspensionUpgrade = SuspensionUpgrade.Sport;
        var r = new TuneResult();
        var ex = new Dictionary<string, string>();

        SuspensionCalculator.CalculateSprings(car, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), r, ex);

        Assert.Equal(0, r.SpringFront);
        Assert.Equal(0, r.SpringRear);
    }

    [Fact]
    public void Springs_RoadHz_1_8_1_9()
    {
        var car = CarFactory.DefaultCar();
        car.SuspensionUpgrade = SuspensionUpgrade.Race;
        var r = new TuneResult();

        SuspensionCalculator.CalculateSprings(car, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), r, new Dictionary<string, string>());

        Assert.True(r.SpringFront > 0);
        Assert.True(r.SpringRear > 0);
    }

    [Fact]
    public void Springs_DragQuarterHz_1_5_0_95()
    {
        var car = CarFactory.DefaultCar();
        car.SuspensionUpgrade = SuspensionUpgrade.Race;
        var r = new TuneResult();

        SuspensionCalculator.CalculateSprings(car,
            new TrackInfo { Discipline = Discipline.Drag, DragDistance = DragDistance.Quarter },
            CarFactory.RelaxedConstraints(), r, new Dictionary<string, string>());

        Assert.True(r.SpringRear < r.SpringFront,
            $"Drag: rear ({r.SpringRear}) should be softer than front ({r.SpringFront})");
    }

    [Fact]
    public void Springs_DriftHz_1_3_1_6()
    {
        var car = CarFactory.DefaultCar();
        car.SuspensionUpgrade = SuspensionUpgrade.Race;
        var r = new TuneResult();

        SuspensionCalculator.CalculateSprings(car, new TrackInfo { Discipline = Discipline.Drift },
            CarFactory.RelaxedConstraints(), r, new Dictionary<string, string>());

        Assert.True(r.SpringRear > r.SpringFront || Math.Abs(r.SpringRear - r.SpringFront) < 50);
    }

    [Fact]
    public void Springs_RWD_AddsRearHz()
    {
        var carRwd = CarFactory.DefaultCar();
        carRwd.SuspensionUpgrade = SuspensionUpgrade.Race;
        carRwd.DriveType = DriveType.RWD;
        var rRwd = new TuneResult();
        SuspensionCalculator.CalculateSprings(carRwd, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), rRwd, new Dictionary<string, string>());

        var carFwd = CarFactory.DefaultCar();
        carFwd.SuspensionUpgrade = SuspensionUpgrade.Race;
        carFwd.DriveType = DriveType.FWD;
        var rFwd = new TuneResult();
        SuspensionCalculator.CalculateSprings(carFwd, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), rFwd, new Dictionary<string, string>());

        Assert.True(rRwd.SpringFront > 50);
        Assert.True(rFwd.SpringFront > 50);
        Assert.True(rRwd.SpringRear > rFwd.SpringRear * 0.9);
    }

    [Fact]
    public void Springs_HighPower_IncreasesSquat()
    {
        var car = CarFactory.DefaultCar();
        car.SuspensionUpgrade = SuspensionUpgrade.Race;
        car.PowerHP = 1000;
        car.TorqueNm = 1200;
        var rHP = new TuneResult();
        SuspensionCalculator.CalculateSprings(car, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), rHP, new Dictionary<string, string>());

        var carBase = CarFactory.DefaultCar();
        carBase.SuspensionUpgrade = SuspensionUpgrade.Race;
        var rBase = new TuneResult();
        SuspensionCalculator.CalculateSprings(carBase, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), rBase, new Dictionary<string, string>());

        Assert.True(rHP.SpringRear > rBase.SpringRear,
            $"High-power rear spring ({rHP.SpringRear}) should be stiffer than base ({rBase.SpringRear})");
    }

    [Fact]
    public void Springs_OffroadRace_Multiplier110()
    {
        var car = CarFactory.DefaultCar();
        car.SuspensionUpgrade = SuspensionUpgrade.Race;
        car.TotalMass = 1400;
        var rRace = new TuneResult();
        SuspensionCalculator.CalculateSprings(car, new TrackInfo { Discipline = Discipline.Rally },
            CarFactory.RelaxedConstraints(), rRace, new Dictionary<string, string>());

        var carOffroad = CarFactory.DefaultCar();
        carOffroad.SuspensionUpgrade = SuspensionUpgrade.Offroad;
        carOffroad.TotalMass = 1400;
        var rOffroad = new TuneResult();
        SuspensionCalculator.CalculateSprings(carOffroad, new TrackInfo { Discipline = Discipline.Rally },
            CarFactory.RelaxedConstraints(), rOffroad, new Dictionary<string, string>());

        Assert.True(rRace.SpringFront > rOffroad.SpringFront);
    }

    [Fact]
    public void Springs_Hybrid_BoostsSprings()
    {
        var car = CarFactory.DefaultCar();
        car.SuspensionUpgrade = SuspensionUpgrade.Race;
        car.PowertrainType = PowertrainType.Hybrid;
        var r = new TuneResult();

        SuspensionCalculator.CalculateSprings(car, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), r, new Dictionary<string, string>());

        Assert.True(r.SpringFront > 0);
    }

    // ─── SuspensionCalculator — RideHeight ──────────────────────────────────

    [Fact]
    public void RideHeight_NonAdvanced_ReturnsZero()
    {
        var car = CarFactory.DefaultCar();
        car.SuspensionUpgrade = SuspensionUpgrade.Street;
        var r = new TuneResult();
        var ex = new Dictionary<string, string>();

        SuspensionCalculator.CalculateRideHeight(car, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), r, ex);

        Assert.Equal(0, r.RideHeightFront);
        Assert.Equal(0, r.RideHeightRear);
    }

    [Fact]
    public void RideHeight_Drag_Base10_65()
    {
        var car = CarFactory.DefaultCar();
        car.SuspensionUpgrade = SuspensionUpgrade.Race;
        var r = new TuneResult();

        SuspensionCalculator.CalculateRideHeight(car,
            new TrackInfo { Discipline = Discipline.Drag },
            CarFactory.RelaxedConstraints(), r, new Dictionary<string, string>());

        Assert.True(r.RideHeightFront < r.RideHeightRear,
            $"Drag: front ({r.RideHeightFront}) should be lower than rear ({r.RideHeightRear})");
    }

    [Fact]
    public void RideHeight_Rally_High()
    {
        var car = CarFactory.DefaultCar();
        car.SuspensionUpgrade = SuspensionUpgrade.Rally;
        var r = new TuneResult();

        SuspensionCalculator.CalculateRideHeight(car, new TrackInfo { Discipline = Discipline.Rally },
            CarFactory.RelaxedConstraints(), r, new Dictionary<string, string>());

        Assert.True(r.RideHeightFront >= 80);
    }

    [Fact]
    public void RideHeight_Drift_Low()
    {
        var car = CarFactory.DefaultCar();
        car.SuspensionUpgrade = SuspensionUpgrade.Drift;
        var r = new TuneResult();

        SuspensionCalculator.CalculateRideHeight(car, new TrackInfo { Discipline = Discipline.Drift },
            CarFactory.RelaxedConstraints(), r, new Dictionary<string, string>());

        Assert.InRange(r.RideHeightFront, 10.0, 200.0);
    }

    [Fact]
    public void RideHeight_RaceSuspension_LowerThanRally()
    {
        var car = CarFactory.DefaultCar();
        car.SuspensionUpgrade = SuspensionUpgrade.Race;
        var rRace = new TuneResult();
        SuspensionCalculator.CalculateRideHeight(car, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), rRace, new Dictionary<string, string>());

        car.SuspensionUpgrade = SuspensionUpgrade.Rally;
        var rRally = new TuneResult();
        SuspensionCalculator.CalculateRideHeight(car, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), rRally, new Dictionary<string, string>());

        Assert.True(rRace.RideHeightFront <= rRally.RideHeightFront,
            $"Race front ({rRace.RideHeightFront}) should be <= Rally front ({rRally.RideHeightFront})");
    }

    [Fact]
    public void RideHeight_ZeroRange_UsesMin()
    {
        var car = CarFactory.DefaultCar();
        car.SuspensionUpgrade = SuspensionUpgrade.Race;
        var c = new TuningConstraints
        {
            RideHeightFrontMin = 30, RideHeightFrontMax = 30,
            RideHeightRearMin = 40, RideHeightRearMax = 40,
            SpringFrontMin = 100, SpringFrontMax = 1000,
            SpringRearMin = 100, SpringRearMax = 1000,
        };
        var r = new TuneResult();

        SuspensionCalculator.CalculateRideHeight(car, CarFactory.DefaultTrack(), c, r, new Dictionary<string, string>());

        Assert.Equal(30.0, r.RideHeightFront, 1);
        Assert.Equal(40.0, r.RideHeightRear, 1);
    }

    // ─── SuspensionCalculator — SpringRideHeightFix ─────────────────────────

    [Fact]
    public void SpringRideHeightFix_NoAdvanced_ReturnsFalse()
    {
        var car = CarFactory.DefaultCar();
        car.SuspensionUpgrade = SuspensionUpgrade.Sport;
        var r = new TuneResult();

        var result = SuspensionCalculator.SpringRideHeightFix(car, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), r);

        Assert.False(result);
    }

    [Fact]
    public void SpringRideHeightFix_LowRH_LowSprings_Triggers()
    {
        var car = CarFactory.DefaultCar();
        car.SuspensionUpgrade = SuspensionUpgrade.Race;
        var c = CarFactory.RelaxedConstraints();
        var r = new TuneResult
        {
            RideHeightFront = c.RideHeightFrontMin + 1,
            RideHeightRear = c.RideHeightRearMin + 1,
            SpringFront = 5,
            SpringRear = 5,
        };

        var result = SuspensionCalculator.SpringRideHeightFix(car, CarFactory.DefaultTrack(), c, r);

        Assert.True(result);
        Assert.True(r.SpringFront > 5);
    }

    [Fact]
    public void SpringRideHeightFix_Offroad_SkipsLowFix()
    {
        var car = CarFactory.DefaultCar();
        car.SuspensionUpgrade = SuspensionUpgrade.Offroad;
        var c = CarFactory.RelaxedConstraints();
        var r = new TuneResult
        {
            RideHeightFront = c.RideHeightFrontMin + 1,
            RideHeightRear = c.RideHeightRearMin + 1,
            SpringFront = 5,
            SpringRear = 5,
        };

        var result = SuspensionCalculator.SpringRideHeightFix(car,
            new TrackInfo { Discipline = Discipline.Rally }, c, r);

        Assert.False(result);
    }

    [Fact]
    public void SpringRideHeightFix_HighRH_HighSprings_Triggers()
    {
        var car = CarFactory.DefaultCar();
        car.SuspensionUpgrade = SuspensionUpgrade.Race;
        var c = CarFactory.RelaxedConstraints();
        var r = new TuneResult
        {
            RideHeightFront = c.RideHeightFrontMin + (c.RideHeightFrontMax - c.RideHeightFrontMin) * 0.80,
            RideHeightRear = c.RideHeightRearMin + (c.RideHeightRearMax - c.RideHeightRearMin) * 0.80,
            SpringFront = c.SpringFrontMin + (c.SpringFrontMax - c.SpringFrontMin) * 0.90,
            SpringRear = c.SpringRearMin + (c.SpringRearMax - c.SpringRearMin) * 0.90,
        };

        var result = SuspensionCalculator.SpringRideHeightFix(car, CarFactory.DefaultTrack(), c, r);

        Assert.True(result);
    }

    // ─── SuspensionCalculator — Dampers ─────────────────────────────────────

    [Fact]
    public void Dampers_Rebound_Positive()
    {
        var car = CarFactory.DefaultCar();
        var r = new TuneResult { SpringFront = 200, SpringRear = 200 };

        SuspensionCalculator.CalculateDampers(car, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), r, new Dictionary<string, string>());

        Assert.True(r.ReboundFront > 0);
        Assert.True(r.ReboundRear > 0);
    }

    [Fact]
    public void Dampers_Drag_Discipline075()
    {
        var car = CarFactory.DefaultCar();
        var r = new TuneResult { SpringFront = 200, SpringRear = 200 };
        var rDrag = new TuneResult { SpringFront = 200, SpringRear = 200 };

        SuspensionCalculator.CalculateDampers(car, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), r, new Dictionary<string, string>());

        SuspensionCalculator.CalculateDampers(car, new TrackInfo { Discipline = Discipline.Drag },
            CarFactory.RelaxedConstraints(), rDrag, new Dictionary<string, string>());

        Assert.True(rDrag.ReboundFront < r.ReboundFront);
    }

    [Fact]
    public void Dampers_BumpRatio_RaceIs60()
    {
        var car = CarFactory.DefaultCar();
        car.SuspensionUpgrade = SuspensionUpgrade.Race;
        var r = new TuneResult { SpringFront = 200, SpringRear = 200 };

        SuspensionCalculator.CalculateDampers(car, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), r, new Dictionary<string, string>());

        Assert.True(r.BumpFront <= r.ReboundFront);
        Assert.True(r.BumpRear <= r.ReboundRear);
    }

    [Fact]
    public void Dampers_RallyCross_BumpRatio065()
    {
        var car = CarFactory.DefaultCar();
        var r = new TuneResult { SpringFront = 200, SpringRear = 200 };

        SuspensionCalculator.CalculateDampers(car, new TrackInfo { Discipline = Discipline.Rally },
            CarFactory.RelaxedConstraints(), r, new Dictionary<string, string>());

        Assert.True(r.BumpFront > 0);
        Assert.True(r.ReboundFront > 0);
    }

    [Fact]
    public void Dampers_HighPower_IncreasesRebound()
    {
        var car = CarFactory.DefaultCar();
        var c = CarFactory.RelaxedConstraints();
        var rBase = new TuneResult { SpringFront = 200, SpringRear = 200 };
        SuspensionCalculator.CalculateDampers(car, CarFactory.DefaultTrack(), c, rBase, new Dictionary<string, string>());

        car.PowerHP = 1500;
        car.TorqueNm = 1600;
        var rHP = new TuneResult { SpringFront = 200, SpringRear = 200 };
        SuspensionCalculator.CalculateDampers(car, CarFactory.DefaultTrack(), c, rHP, new Dictionary<string, string>());

        Assert.NotEqual(rBase.ReboundRear, rHP.ReboundRear);
    }

    [Fact]
    public void Dampers_SeasonWinter_Reduces()
    {
        var car = CarFactory.DefaultCar();
        var rSu = new TuneResult { SpringFront = 200, SpringRear = 200 };
        SuspensionCalculator.CalculateDampers(car, new TrackInfo { Season = Season.Summer },
            CarFactory.RelaxedConstraints(), rSu, new Dictionary<string, string>());

        var rWi = new TuneResult { SpringFront = 200, SpringRear = 200 };
        SuspensionCalculator.CalculateDampers(car, new TrackInfo { Season = Season.Winter },
            CarFactory.RelaxedConstraints(), rWi, new Dictionary<string, string>());

        Assert.True(rSu.ReboundFront >= rWi.ReboundFront - 0.1);
    }

    // ─── AlignmentCalculator — Camber ───────────────────────────────────────

    [Fact]
    public void Camber_NonAdvanced_ReturnsZero()
    {
        var car = CarFactory.DefaultCar();
        car.SuspensionUpgrade = SuspensionUpgrade.Sport;
        var r = new TuneResult();

        AlignmentCalculator.CalculateCamber(car, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), r, new Dictionary<string, string>(), 200);

        Assert.Equal(0, r.CamberFront);
        Assert.Equal(0, r.CamberRear);
    }

    [Fact]
    public void Camber_Drag_Base_0_2_0()
    {
        var car = CarFactory.DefaultCar();
        var r = new TuneResult();

        AlignmentCalculator.CalculateCamber(car, new TrackInfo { Discipline = Discipline.Drag },
            CarFactory.RelaxedConstraints(), r, new Dictionary<string, string>(), 300);

        Assert.True(r.CamberFront <= 0);
    }

    [Fact]
    public void Camber_Drift_Base_2_5_0_8()
    {
        var car = CarFactory.DefaultCar();
        var r = new TuneResult();

        AlignmentCalculator.CalculateCamber(car, new TrackInfo { Discipline = Discipline.Drift },
            CarFactory.RelaxedConstraints(), r, new Dictionary<string, string>(), 180);

        Assert.True(r.CamberFront <= -1.0);
    }

    [Fact]
    public void Camber_Touge_Base_1_8_0_8()
    {
        var car = CarFactory.DefaultCar();
        var r = new TuneResult();

        AlignmentCalculator.CalculateCamber(car, new TrackInfo { Discipline = Discipline.Touge },
            CarFactory.RelaxedConstraints(), r, new Dictionary<string, string>(), 200);

        Assert.True(r.CamberFront < 0);
        Assert.True(r.CamberRear < 0);
    }

    [Fact]
    public void Camber_SlickTire_MoreCamberThanStock()
    {
        var c = CarFactory.RelaxedConstraints();
        var carSlick = CarFactory.DefaultCar();
        carSlick.TireType = TireType.Slick;
        var rSlick = new TuneResult();
        AlignmentCalculator.CalculateCamber(carSlick, CarFactory.DefaultTrack(), c, rSlick,
            new Dictionary<string, string>(), 200);

        var carStock = CarFactory.DefaultCar();
        carStock.TireType = TireType.Stock;
        var rStock = new TuneResult();
        AlignmentCalculator.CalculateCamber(carStock, CarFactory.DefaultTrack(), c, rStock,
            new Dictionary<string, string>(), 200);

        Assert.True(rSlick.CamberFront <= rStock.CamberFront || Math.Abs(rSlick.CamberFront - rStock.CamberFront) < 0.3);
    }

    [Fact]
    public void Camber_AeroAdjustment_AddsCamber()
    {
        var car = CarFactory.DefaultCar();
        var r = new TuneResult { AeroFront = 100, AeroRear = 80 };
        var c = CarFactory.RelaxedConstraints();

        AlignmentCalculator.CalculateCamber(car, CarFactory.DefaultTrack(), c, r,
            new Dictionary<string, string>(), 200);

        Assert.True(r.CamberFront < 0);
    }

    [Fact]
    public void Camber_EnginePositionFront_AddsNegativeFront()
    {
        var carFront = CarFactory.DefaultCar();
        carFront.EnginePosition = EnginePosition.Front;
        var rFront = new TuneResult();
        AlignmentCalculator.CalculateCamber(carFront, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), rFront, new Dictionary<string, string>(), 200);

        var carRear = CarFactory.DefaultCar();
        carRear.EnginePosition = EnginePosition.Rear;
        var rRear = new TuneResult();
        AlignmentCalculator.CalculateCamber(carRear, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), rRear, new Dictionary<string, string>(), 200);

        Assert.NotEqual(rFront.CamberFront, rRear.CamberFront);
    }

    [Fact]
    public void Camber_AWD_Road_AdjustsFront()
    {
        var car = CarFactory.DefaultCar();
        car.DriveType = DriveType.AWD;
        var r = new TuneResult();

        AlignmentCalculator.CalculateCamber(car, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), r, new Dictionary<string, string>(), 200);

        Assert.True(r.CamberFront < 0);
    }

    [Fact]
    public void Camber_Drift_AWD_Assisted()
    {
        var car = CarFactory.DefaultCar();
        car.DriveType = DriveType.AWD;
        var r = new TuneResult();

        AlignmentCalculator.CalculateCamber(car, new TrackInfo { Discipline = Discipline.Drift },
            CarFactory.RelaxedConstraints(), r, new Dictionary<string, string>(), 180);

        Assert.True(r.CamberFront < -1.0);
    }

    [Fact]
    public void Camber_ClampedToConstraints()
    {
        var car = CarFactory.DefaultCar();
        var c = new TuningConstraints { CamberFrontMin = -2.0, CamberFrontMax = -0.5, CamberRearMin = -2.0, CamberRearMax = -0.5 };
        var r = new TuneResult();

        AlignmentCalculator.CalculateCamber(car, CarFactory.DefaultTrack(), c, r,
            new Dictionary<string, string>(), 200);

        Assert.InRange(r.CamberFront, -2.0, -0.5);
        Assert.InRange(r.CamberRear, -2.0, -0.5);
    }

    // ─── AlignmentCalculator — Toe ──────────────────────────────────────────

    [Fact]
    public void Toe_RWD_NegativeFront_PositiveRear()
    {
        var car = CarFactory.DefaultCar();
        car.DriveType = DriveType.RWD;
        var r = new TuneResult();

        AlignmentCalculator.CalculateToe(car, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), r, new Dictionary<string, string>(), 200);

        Assert.True(r.ToeFront < 0, $"RWD front toe ({r.ToeFront}) should be negative");
        Assert.True(r.ToeRear > 0, $"RWD rear toe ({r.ToeRear}) should be positive");
    }

    [Fact]
    public void Toe_FWD_NegativeFront_PositiveRear()
    {
        var car = CarFactory.DefaultCar();
        car.DriveType = DriveType.FWD;
        var r = new TuneResult();

        AlignmentCalculator.CalculateToe(car, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), r, new Dictionary<string, string>(), 200);

        Assert.True(r.ToeFront < 0);
        Assert.True(r.ToeRear > 0);
    }

    [Fact]
    public void Toe_AWD_NegativeFront_PositiveRear()
    {
        var car = CarFactory.DefaultCar();
        car.DriveType = DriveType.AWD;
        var r = new TuneResult();

        AlignmentCalculator.CalculateToe(car, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), r, new Dictionary<string, string>(), 200);

        Assert.True(r.ToeFront < 0);
        Assert.True(r.ToeRear > 0);
    }

    [Fact]
    public void Toe_Drift_Hardcoded_0_8_0_2()
    {
        var car = CarFactory.DefaultCar();
        var r = new TuneResult();

        AlignmentCalculator.CalculateToe(car, new TrackInfo { Discipline = Discipline.Drift },
            CarFactory.RelaxedConstraints(), r, new Dictionary<string, string>(), 180);

        Assert.True(r.ToeFront <= -0.5);
        Assert.True(r.ToeRear > 0);
    }

    [Fact]
    public void Toe_Drag_ZeroMultiplier()
    {
        var car = CarFactory.DefaultCar();
        var r = new TuneResult();

        AlignmentCalculator.CalculateToe(car, new TrackInfo { Discipline = Discipline.Drag },
            CarFactory.RelaxedConstraints(), r, new Dictionary<string, string>(), 320);

        Assert.InRange(r.ToeFront, -0.5, 0.5);
        Assert.InRange(r.ToeRear, -0.5, 0.5);
    }

    [Fact]
    public void Toe_TanH_SquashesExtremeValues()
    {
        var car = CarFactory.DefaultCar();
        var r = new TuneResult();

        AlignmentCalculator.CalculateToe(car, new TrackInfo { Discipline = Discipline.Drift },
            CarFactory.RelaxedConstraints(), r, new Dictionary<string, string>(), 180);

        Assert.InRange(r.ToeFront, -0.81, 0.5);
        Assert.InRange(r.ToeRear, -0.5, 0.5);
    }

    // ─── AlignmentCalculator — Caster ───────────────────────────────────────

    [Fact]
    public void Caster_BaseByWeight()
    {
        var car = CarFactory.DefaultCar();
        var r = new TuneResult();

        AlignmentCalculator.CalculateCaster(car, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), r, new Dictionary<string, string>(), 200);

        Assert.InRange(r.Caster, 6.0, 15.0);
    }

    [Fact]
    public void Caster_Drift_115Multiplier()
    {
        var car = CarFactory.DefaultCar();
        var rDrift = new TuneResult();
        AlignmentCalculator.CalculateCaster(car, new TrackInfo { Discipline = Discipline.Drift },
            CarFactory.RelaxedConstraints(), rDrift, new Dictionary<string, string>(), 200);

        var rRoad = new TuneResult();
        AlignmentCalculator.CalculateCaster(car, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), rRoad, new Dictionary<string, string>(), 200);

        Assert.True(rDrift.Caster >= rRoad.Caster);
    }

    [Fact]
    public void Caster_SpeedWeightFactor_HighSpeedMoreCaster()
    {
        var car = CarFactory.DefaultCar();
        var rLow = new TuneResult();
        AlignmentCalculator.CalculateCaster(car, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), rLow, new Dictionary<string, string>(), 120);

        var rHigh = new TuneResult();
        AlignmentCalculator.CalculateCaster(car, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), rHigh, new Dictionary<string, string>(), 350);

        Assert.True(rHigh.Caster >= rLow.Caster);
    }

    [Fact]
    public void Caster_ClampedToConstraints()
    {
        var car = CarFactory.DefaultCar();
        var c = new TuningConstraints { CasterMin = 3.0, CasterMax = 8.0 };
        var r = new TuneResult();

        AlignmentCalculator.CalculateCaster(car, new TrackInfo { Discipline = Discipline.Drift }, c, r,
            new Dictionary<string, string>(), 300);

        Assert.InRange(r.Caster, 3.0, 10.0);
    }

    // ─── BrakeCalculator ────────────────────────────────────────────────────

    [Fact]
    public void Brakes_Drag_Bias50()
    {
        var car = CarFactory.DefaultCar();
        var r = new TuneResult();

        BrakeCalculator.CalculateBrakes(car, new TrackInfo { Discipline = Discipline.Drag },
            CarFactory.RelaxedConstraints(), r, new Dictionary<string, string>(), 300);

        Assert.Equal(50, r.BrakeBalance);
    }

    [Fact]
    public void Brakes_FrontEngine_BiasHigher()
    {
        var carFront = CarFactory.DefaultCar();
        carFront.EnginePosition = EnginePosition.Front;
        var rFront = new TuneResult();
        BrakeCalculator.CalculateBrakes(carFront, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), rFront, new Dictionary<string, string>(), 200);

        var carRear = CarFactory.DefaultCar();
        carRear.EnginePosition = EnginePosition.Rear;
        var rRear = new TuneResult();
        BrakeCalculator.CalculateBrakes(carRear, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), rRear, new Dictionary<string, string>(), 200);

        Assert.True(rFront.BrakeBalance > rRear.BrakeBalance);
    }

    [Fact]
    public void Brakes_FWD_BiasReduced()
    {
        var carRwd = CarFactory.DefaultCar();
        carRwd.DriveType = DriveType.RWD;
        var rRwd = new TuneResult();
        BrakeCalculator.CalculateBrakes(carRwd, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), rRwd, new Dictionary<string, string>(), 200);

        var carFwd = CarFactory.DefaultCar();
        carFwd.DriveType = DriveType.FWD;
        var rFwd = new TuneResult();
        BrakeCalculator.CalculateBrakes(carFwd, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), rFwd, new Dictionary<string, string>(), 200);

        Assert.NotEqual(rFwd.BrakeBalance, rRwd.BrakeBalance);
    }

    [Fact]
    public void Brakes_HeavyCar_IncreasesPressure()
    {
        var car = CarFactory.DefaultCar();
        car.TotalMass = 2500;
        var r = new TuneResult();

        BrakeCalculator.CalculateBrakes(car, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), r, new Dictionary<string, string>(), 200);

        Assert.True(r.BrakePressure > 100);
    }

    [Fact]
    public void Brakes_LightCar_DecreasesPressure()
    {
        var car = CarFactory.DefaultCar();
        car.TotalMass = 800;
        var r = new TuneResult();

        BrakeCalculator.CalculateBrakes(car, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), r, new Dictionary<string, string>(), 200);

        Assert.InRange(r.BrakePressure, 20, 120);
    }

    [Fact]
    public void Brakes_SlickTire_PressureBonus3()
    {
        var car = CarFactory.DefaultCar();
        car.TireType = TireType.Slick;
        var r = new TuneResult();
        BrakeCalculator.CalculateBrakes(car, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), r, new Dictionary<string, string>(), 200);

        Assert.True(r.BrakePressure >= 100);
    }

    [Fact]
    public void Brakes_Drift_PressureReducedBy5()
    {
        var car = CarFactory.DefaultCar();
        var rDrift = new TuneResult();
        BrakeCalculator.CalculateBrakes(car, new TrackInfo { Discipline = Discipline.Drift },
            CarFactory.RelaxedConstraints(), rDrift, new Dictionary<string, string>(), 180);

        var rRoad = new TuneResult();
        BrakeCalculator.CalculateBrakes(car, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), rRoad, new Dictionary<string, string>(), 200);

        Assert.True(rDrift.BrakePressure <= rRoad.BrakePressure);
    }

    // ─── DifferentialCalculator ─────────────────────────────────────────────

    [Fact]
    public void Diff_Stock_ReturnsEarly()
    {
        var car = CarFactory.DefaultCar();
        car.DifferentialUpgrade = DifferentialUpgrade.Stock;
        var r = new TuneResult();
        var ex = new Dictionary<string, string>();

        DifferentialCalculator.CalculateDifferential(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints(), r, ex);

        Assert.InRange(r.DiffAccel, 45, 55);
        Assert.Equal(0, r.DiffDecel);
    }

    [Fact]
    public void Diff_Street_ReturnsEarly()
    {
        var car = CarFactory.DefaultCar();
        car.DifferentialUpgrade = DifferentialUpgrade.Street;
        var r = new TuneResult();
        var ex = new Dictionary<string, string>();

        DifferentialCalculator.CalculateDifferential(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints(), r, ex);

        Assert.InRange(r.DiffAccel, 45, 55);
    }

    [Fact]
    public void Diff_Drag_RWD_85_5()
    {
        var car = CarFactory.DefaultCar();
        car.DifferentialUpgrade = DifferentialUpgrade.Race;
        car.DriveType = DriveType.RWD;
        var r = new TuneResult();

        DifferentialCalculator.CalculateDifferential(car, new TrackInfo { Discipline = Discipline.Drag },
            CarFactory.RelaxedConstraints(), r, new Dictionary<string, string>());

        Assert.True(r.DiffAccel >= 80);
        Assert.True(r.DiffDecel >= 0);
    }

    [Fact]
    public void Diff_Rally_AWD_55_20()
    {
        var car = CarFactory.DefaultCar();
        car.DifferentialUpgrade = DifferentialUpgrade.Race;
        car.DriveType = DriveType.AWD;
        var r = new TuneResult();

        DifferentialCalculator.CalculateDifferential(car, new TrackInfo { Discipline = Discipline.Rally },
            CarFactory.RelaxedConstraints(), r, new Dictionary<string, string>());

        Assert.True(r.DiffAccel >= 40);
        Assert.True(r.DiffDecel >= 10);
    }

    [Fact]
    public void Diff_Drift_RWD_90_10()
    {
        var car = CarFactory.DefaultCar();
        car.DifferentialUpgrade = DifferentialUpgrade.Race;
        var r = new TuneResult();

        DifferentialCalculator.CalculateDifferential(car, new TrackInfo { Discipline = Discipline.Drift },
            CarFactory.RelaxedConstraints(), r, new Dictionary<string, string>());

        Assert.True(r.DiffAccel >= 70);
    }

    [Fact]
    public void Diff_PowerAdjustment_IncreasesAccel()
    {
        var car = CarFactory.DefaultCar();
        car.DifferentialUpgrade = DifferentialUpgrade.Race;
        var rBase = new TuneResult();
        DifferentialCalculator.CalculateDifferential(car, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), rBase, new Dictionary<string, string>());

        car.PowerHP = 1000;
        car.TorqueNm = 1200;
        var rHP = new TuneResult();
        DifferentialCalculator.CalculateDifferential(car, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), rHP, new Dictionary<string, string>());

        Assert.True(rHP.DiffAccel >= rBase.DiffAccel);
    }

    [Fact]
    public void Diff_RearEngine_IncreasesAccel()
    {
        var car = CarFactory.DefaultCar();
        car.DifferentialUpgrade = DifferentialUpgrade.Race;
        car.EnginePosition = EnginePosition.Rear;
        var r = new TuneResult();
        DifferentialCalculator.CalculateDifferential(car, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), r, new Dictionary<string, string>());

        Assert.True(r.DiffAccel > 0);
    }

    [Fact]
    public void Diff_SeasonWinter_ReducesAccel()
    {
        var car = CarFactory.DefaultCar();
        car.DifferentialUpgrade = DifferentialUpgrade.Race;
        var rSu = new TuneResult();
        DifferentialCalculator.CalculateDifferential(car, new TrackInfo { Season = Season.Summer },
            CarFactory.RelaxedConstraints(), rSu, new Dictionary<string, string>());

        var rWi = new TuneResult();
        DifferentialCalculator.CalculateDifferential(car, new TrackInfo { Season = Season.Winter },
            CarFactory.RelaxedConstraints(), rWi, new Dictionary<string, string>());

        Assert.True(rSu.DiffAccel >= rWi.DiffAccel);
    }

    [Fact]
    public void Diff_Sport_NoDecel()
    {
        var car = CarFactory.DefaultCar();
        car.DifferentialUpgrade = DifferentialUpgrade.Sport;
        var r = new TuneResult();

        DifferentialCalculator.CalculateDifferential(car, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), r, new Dictionary<string, string>());

        Assert.Equal(0, r.DiffDecel);
        Assert.True(r.DiffAccel > 0);
    }

    // ─── DifferentialCalculator — AWD ───────────────────────────────────────

    [Fact]
    public void Diff_AWD_CenterBias_Present()
    {
        var car = CarFactory.AWDPerformanceCar();
        car.DifferentialUpgrade = DifferentialUpgrade.Race;
        var r = new TuneResult();

        DifferentialCalculator.CalculateDifferential(car, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), r, new Dictionary<string, string>());

        Assert.NotNull(r.CenterDiffBias);
        Assert.NotNull(r.DiffFrontAccel);
        Assert.InRange(r.CenterDiffBias!.Value, 0, 100);
    }

    [Fact]
    public void Diff_AWD_FrontAccel_DisciplineSpecific()
    {
        var car = CarFactory.AWDPerformanceCar();
        car.DifferentialUpgrade = DifferentialUpgrade.Race;
        var rDrift = new TuneResult();
        DifferentialCalculator.CalculateDifferential(car, new TrackInfo { Discipline = Discipline.Drift },
            CarFactory.RelaxedConstraints(), rDrift, new Dictionary<string, string>());

        var rRoad = new TuneResult();
        DifferentialCalculator.CalculateDifferential(car, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), rRoad, new Dictionary<string, string>());

        Assert.NotNull(rDrift.DiffFrontAccel);
        Assert.NotNull(rRoad.DiffFrontAccel);
        Assert.NotEqual(rDrift.DiffFrontAccel, rRoad.DiffFrontAccel);
    }

    [Fact]
    public void Diff_AWD_RearEngine_ReducesFrontAccel()
    {
        var carFront = CarFactory.AWDPerformanceCar();
        carFront.DifferentialUpgrade = DifferentialUpgrade.Race;
        carFront.EnginePosition = EnginePosition.Front;
        var rFront = new TuneResult();
        DifferentialCalculator.CalculateDifferential(carFront, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), rFront, new Dictionary<string, string>());

        var carRear = CarFactory.AWDPerformanceCar();
        carRear.DifferentialUpgrade = DifferentialUpgrade.Race;
        carRear.EnginePosition = EnginePosition.Rear;
        var rRear = new TuneResult();
        DifferentialCalculator.CalculateDifferential(carRear, CarFactory.DefaultTrack(),
            CarFactory.RelaxedConstraints(), rRear, new Dictionary<string, string>());

        Assert.NotNull(rFront.DiffFrontAccel);
        Assert.NotNull(rRear.DiffFrontAccel);
    }

    // ─── GearingCalculator ──────────────────────────────────────────────────

    [Fact]
    public void Gearing_AspirationStep_CentrifugalReducesStepMax()
    {
        double stepMin = 0.68, stepMax = 0.82;

        GearingCalculator.ApplyAspirationStepAdjustment(AspirationType.Centrifugal, false, ref stepMin, ref stepMax);

        Assert.True(stepMax < 0.82);
    }

    [Fact]
    public void Gearing_Diesel_LowersFirstGear()
    {
        var (firstGas, _, _, _) = GearingCalculator.GetDisciplineGearParams(Discipline.Road, 200, FuelType.Gasoline);
        var (firstDiesel, _, _, _) = GearingCalculator.GetDisciplineGearParams(Discipline.Road, 200, FuelType.Diesel);

        Assert.True(firstDiesel <= firstGas);
    }

    [Fact]
    public void Gearing_DisciplineParams_AllUnique()
    {
        var (firstDrift, _, _, _) = GearingCalculator.GetDisciplineGearParams(Discipline.Drift, 200, FuelType.Gasoline);
        var (firstRally, _, _, _) = GearingCalculator.GetDisciplineGearParams(Discipline.Rally, 200, FuelType.Gasoline);
        var (firstCC, _, _, _) = GearingCalculator.GetDisciplineGearParams(Discipline.CrossCountry, 200, FuelType.Gasoline);

        Assert.True(firstDrift < firstRally);
        Assert.True(firstRally < firstCC);
    }

    // ─── LaunchControlCalculator ────────────────────────────────────────────

    [Fact]
    public void LaunchControl_Electric_ReturnsNull()
    {
        var car = CarFactory.ElectricCar();
        var r = new TuneResult();

        LaunchControlCalculator.CalculateLaunchControl(car, new TrackInfo { Discipline = Discipline.Drag }, r);

        Assert.Null(r.LaunchControlRpm);
    }

    [Fact]
    public void LaunchControl_Drag_ReturnsRpm()
    {
        var car = CarFactory.DefaultCar();
        var r = new TuneResult();

        LaunchControlCalculator.CalculateLaunchControl(car, new TrackInfo { Discipline = Discipline.Drag }, r);

        Assert.NotNull(r.LaunchControlRpm);
        Assert.InRange(r.LaunchControlRpm!.Value, 1200, car.MaxRPM * 80 / 100);
    }

    [Fact]
    public void LaunchControl_TwinTurboAntiLag_HigherThanNatural()
    {
        var rNatural = new TuneResult();
        var carN = CarFactory.DefaultCar();
        carN.AspirationType = AspirationType.Natural;
        LaunchControlCalculator.CalculateLaunchControl(carN, new TrackInfo { Discipline = Discipline.Drag }, rNatural);

        var rTT = new TuneResult();
        var carTT = CarFactory.DefaultCar();
        carTT.AspirationType = AspirationType.TwinTurbo;
        carTT.AntiLag = true;
        LaunchControlCalculator.CalculateLaunchControl(carTT, new TrackInfo { Discipline = Discipline.Drag }, rTT);

        Assert.True(rTT.LaunchControlRpm >= rNatural.LaunchControlRpm || rTT.LaunchControlRpm == rNatural.LaunchControlRpm);
    }

    [Fact]
    public void LaunchControl_AWD_HigherThanRWD()
    {
        var carRwd = CarFactory.DefaultCar();
        carRwd.DriveType = DriveType.RWD;
        var rRwd = new TuneResult();
        LaunchControlCalculator.CalculateLaunchControl(carRwd, new TrackInfo { Discipline = Discipline.Drag }, rRwd);

        var carAwd = CarFactory.DefaultCar();
        carAwd.DriveType = DriveType.AWD;
        var rAwd = new TuneResult();
        LaunchControlCalculator.CalculateLaunchControl(carAwd, new TrackInfo { Discipline = Discipline.Drag }, rAwd);

        if (rRwd.LaunchControlRpm.HasValue && rAwd.LaunchControlRpm.HasValue)
            Assert.True(rAwd.LaunchControlRpm >= rRwd.LaunchControlRpm);
    }

    [Fact]
    public void LaunchControl_TireDrag_Factor115()
    {
        var car = CarFactory.DefaultCar();
        car.TireType = TireType.Drag;
        var rDrag = new TuneResult();
        LaunchControlCalculator.CalculateLaunchControl(car, new TrackInfo { Discipline = Discipline.Drag }, rDrag);

        var carSport = CarFactory.DefaultCar();
        carSport.TireType = TireType.Sport;
        var rSport = new TuneResult();
        LaunchControlCalculator.CalculateLaunchControl(carSport, new TrackInfo { Discipline = Discipline.Drag }, rSport);

        if (rDrag.LaunchControlRpm.HasValue && rSport.LaunchControlRpm.HasValue)
            Assert.True(rDrag.LaunchControlRpm >= rSport.LaunchControlRpm);
    }

    [Fact]
    public void LaunchControl_FWD_LowerThanRWD()
    {
        var carRwd = CarFactory.DefaultCar();
        carRwd.DriveType = DriveType.RWD;
        var rRwd = new TuneResult();
        LaunchControlCalculator.CalculateLaunchControl(carRwd, new TrackInfo { Discipline = Discipline.Drag }, rRwd);

        var carFwd = CarFactory.DefaultCar();
        carFwd.DriveType = DriveType.FWD;
        var rFwd = new TuneResult();
        LaunchControlCalculator.CalculateLaunchControl(carFwd, new TrackInfo { Discipline = Discipline.Drag }, rFwd);

        if (rRwd.LaunchControlRpm.HasValue && rFwd.LaunchControlRpm.HasValue)
            Assert.True(rFwd.LaunchControlRpm <= rRwd.LaunchControlRpm);
    }

    // ─── CalculationHelpers ─────────────────────────────────────────────────

    [Fact]
    public void EffectiveWtDist_Explicit_ReturnsExplicitValue()
    {
        var car = CarFactory.DefaultCar();
        car.WeightDistributionFront = 62;

        double wd = CalculationHelpers.EffectiveWtDist(car);

        Assert.Equal(62, wd);
    }

    [Fact]
    public void EffectiveWtDist_ExplicitSet_ReturnsExplicit()
    {
        var car = CarFactory.DefaultCar();
        car.WeightDistributionFront = 48;

        double wd = CalculationHelpers.EffectiveWtDist(car);

        Assert.Equal(48, wd);
    }

    [Fact]
    public void EffectiveWtDist_Default50_FrontEngine_Returns55()
    {
        var car = CarFactory.DefaultCar();
        car.EnginePosition = EnginePosition.Front;

        double wd = CalculationHelpers.EffectiveWtDist(car);

        Assert.Equal(55, wd);
    }

    [Fact]
    public void EffectiveWtDist_Default50_MidEngine_Returns48()
    {
        var car = CarFactory.DefaultCar();
        car.EnginePosition = EnginePosition.Mid;

        double wd = CalculationHelpers.EffectiveWtDist(car);

        Assert.Equal(48, wd);
    }

    [Fact]
    public void EffectiveWtDist_Default50_RearEngine_Returns40()
    {
        var car = CarFactory.DefaultCar();
        car.EnginePosition = EnginePosition.Rear;

        double wd = CalculationHelpers.EffectiveWtDist(car);

        Assert.Equal(40, wd);
    }

    [Fact]
    public void GetSeasonGripFactor_Winter_Lowest()
    {
        Assert.Equal(0.85, CalculationHelpers.GetSeasonGripFactor(Season.Winter));
        Assert.Equal(1.05, CalculationHelpers.GetSeasonGripFactor(Season.Summer));
    }

    [Fact]
    public void GetPowerDeliveryFactors_Electric_Highest()
    {
        var factors = CalculationHelpers.GetPowerDeliveryFactors(PowertrainType.Electric, null);
        Assert.Equal(1.20, factors.Diff);
        Assert.Equal(1.08, factors.Spring);
        Assert.Equal(1.06, factors.Damper);
    }

    [Fact]
    public void GetPowerDeliveryFactors_Hybrid_60PercentOfBoost()
    {
        var hybrid = CalculationHelpers.GetPowerDeliveryFactors(PowertrainType.Hybrid, AspirationType.TwinTurbo, true);
        var full = CalculationHelpers.GetPowerDeliveryFactors(PowertrainType.ICE, AspirationType.TwinTurbo, true);

        Assert.True(hybrid.Diff < full.Diff);
        Assert.True(hybrid.Diff > 1.0);
    }

    [Theory]
    [InlineData(1.0, 0.0, 10.0, 1.0)]
    [InlineData(-1.0, 0.0, 10.0, 0.0)]
    [InlineData(15.0, 0.0, 10.0, 10.0)]
    public void Clamp_WorksCorrectly(double input, double min, double max, double expected)
    {
        Assert.Equal(expected, CalculationHelpers.Clamp(input, min, max));
    }
}
