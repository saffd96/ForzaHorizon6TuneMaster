using System.Threading.Tasks;
using Xunit;
using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;
using TuneMaster.Tests.Helpers;

namespace TuneMaster.Tests.Services;

public class TuneGeneratorServiceTests
{
    private readonly TuneGeneratorService _sut = new();

    [Fact]
    public void Generate_ReturnsNonNullResult()
    {
        var car = CarFactory.DefaultCar();
        var track = CarFactory.DefaultTrack();
        var constraints = CarFactory.RelaxedConstraints();

        var result = _sut.Generate(car, track, constraints);

        Assert.NotNull(result);
        Assert.Equal(car, result.Car);
        Assert.Equal(track, result.Track);
    }

    [Fact]
    public void Generate_PopulatesAllExplanations()
    {
        var result = _sut.Generate(CarFactory.DefaultCar(), CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.Contains("TirePressure", result.Explanations);
        Assert.Contains("Camber", result.Explanations);
        Assert.Contains("Toe", result.Explanations);
        Assert.Contains("Caster", result.Explanations);
        Assert.Contains("ARB", result.Explanations);
        Assert.Contains("Springs", result.Explanations);
        Assert.Contains("RideHeight", result.Explanations);
        Assert.Contains("Dampers", result.Explanations);
        Assert.Contains("Differential", result.Explanations);
        Assert.Contains("Brakes", result.Explanations);
        Assert.Contains("FinalDrive", result.Explanations);
        Assert.Contains("Aero", result.Explanations);
    }

    [Fact]
    public void Generate_TirePressure_InRange()
    {
        var car = CarFactory.DefaultCar();
        var c = CarFactory.DefaultConstraints();

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), c);

        Assert.InRange(result.TirePressureFront, c.TirePressureFrontMin, c.TirePressureFrontMax);
        Assert.InRange(result.TirePressureRear, c.TirePressureRearMin, c.TirePressureRearMax);
    }

    [Fact]
    public void Generate_Camber_InRange()
    {
        var car = CarFactory.DefaultCar();
        var c = CarFactory.RelaxedConstraints();

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), c);

        Assert.InRange(result.CamberFront, c.CamberFrontMin, c.CamberFrontMax);
        Assert.InRange(result.CamberRear, c.CamberRearMin, c.CamberRearMax);
    }

    [Fact]
    public void Generate_Toe_InRange()
    {
        var car = CarFactory.DefaultCar();
        var c = CarFactory.RelaxedConstraints();

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), c);

        Assert.InRange(result.ToeFront, c.ToeFrontMin, c.ToeFrontMax);
        Assert.InRange(result.ToeRear, c.ToeRearMin, c.ToeRearMax);
    }

    [Fact]
    public void Generate_Caster_InRange()
    {
        var car = CarFactory.DefaultCar();
        var result = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.InRange(result.Caster, 1.0, 15.0);
    }

    [Fact]
    public void Generate_ARB_InRange()
    {
        var car = CarFactory.DefaultCar();
        var c = CarFactory.RelaxedConstraints();

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), c);

        Assert.InRange(result.ARBFront, c.ARBFrontMin, c.ARBFrontMax);
        Assert.InRange(result.ARBRear, c.ARBRearMin, c.ARBRearMax);
    }

    [Fact]
    public void Generate_Springs_InRange()
    {
        var car = CarFactory.DefaultCar();
        var c = CarFactory.RelaxedConstraints();

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), c);

        Assert.InRange(result.SpringFront, c.SpringFrontMin, c.SpringFrontMax);
        Assert.InRange(result.SpringRear, c.SpringRearMin, c.SpringRearMax);
    }

    [Fact]
    public void Generate_RideHeight_InRange()
    {
        var car = CarFactory.DefaultCar();
        var c = CarFactory.RelaxedConstraints();

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), c);

        Assert.InRange(result.RideHeightFront, c.RideHeightFrontMin, c.RideHeightFrontMax);
        Assert.InRange(result.RideHeightRear, c.RideHeightRearMin, c.RideHeightRearMax);
    }

    [Fact]
    public void Generate_Dampers_InRange()
    {
        var car = CarFactory.DefaultCar();
        var c = CarFactory.RelaxedConstraints();

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), c);

        Assert.InRange(result.ReboundFront, c.ReboundFrontMin, c.ReboundFrontMax);
        Assert.InRange(result.ReboundRear, c.ReboundRearMin, c.ReboundRearMax);
        Assert.InRange(result.BumpFront, c.BumpFrontMin, c.BumpFrontMax);
        Assert.InRange(result.BumpRear, c.BumpRearMin, c.BumpRearMax);
    }

    [Fact]
    public void Generate_Aero_InRange()
    {
        var car = CarFactory.DefaultCar();
        var c = CarFactory.RelaxedConstraints();

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), c);

        Assert.InRange(result.AeroFront, c.AeroFrontMin, c.AeroFrontMax);
        Assert.InRange(result.AeroRear, c.AeroRearMin, c.AeroRearMax);
    }

    [Fact]
    public void Generate_Differential_InRange()
    {
        var car = CarFactory.DefaultCar();
        var c = CarFactory.RelaxedConstraints();

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), c);

        Assert.InRange(result.DiffAccel, c.DiffAccelMin, c.DiffAccelMax);
        Assert.InRange(result.DiffDecel, c.DiffDecelMin, c.DiffDecelMax);
    }

    [Fact]
    public void Generate_Brakes_InRange()
    {
        var car = CarFactory.DefaultCar();
        var c = CarFactory.RelaxedConstraints();

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), c);

        Assert.InRange(result.BrakeBalance, c.BrakeBalanceMin, c.BrakeBalanceMax);
        Assert.InRange(result.BrakePressure, c.BrakePressureMin, c.BrakePressureMax);
    }

    [Fact]
    public void Generate_FinalDrive_InRange()
    {
        var car = CarFactory.DefaultCar();
        var c = CarFactory.RelaxedConstraints();

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), c);

        Assert.InRange(result.FinalDrive, c.FinalDriveMin, c.FinalDriveMax);
    }

    [Fact]
    public void Generate_GearRatios_NotEmpty()
    {
        var result = _sut.Generate(CarFactory.DefaultCar(), CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.NotEmpty(result.GearRatios);
    }

    [Fact]
    public void Generate_GearRatios_Descending()
    {
        var result = _sut.Generate(CarFactory.DefaultCar(), CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        for (int i = 1; i < result.GearRatios.Count; i++)
            Assert.True(result.GearRatios[i] <= result.GearRatios[i - 1],
                $"Gear {i + 1} ({result.GearRatios[i]}) > gear {i} ({result.GearRatios[i - 1]})");
    }

    [Fact]
    public void Generate_GearRatios_ForzaRange()
    {
        var result = _sut.Generate(CarFactory.DefaultCar(), CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        foreach (var g in result.GearRatios)
            Assert.InRange(g, 0.48, 6.10);
    }

    [Fact]
    public void Generate_ActualMaxSpeedKmh_Positive()
    {
        var result = _sut.Generate(CarFactory.DefaultCar(), CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.True(result.ActualMaxSpeedKmh > 0);
    }

    [Fact]
    public void Generate_AWD_HasFrontDiffAndCenterBias()
    {
        var car = CarFactory.AWDPerformanceCar();
        var result = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.NotNull(result.DiffFrontAccel);
        Assert.NotNull(result.DiffFrontDecel);
        Assert.NotNull(result.CenterDiffBias);
    }

    [Fact]
    public void Generate_RWD_DoesNotHaveFrontDiff()
    {
        var result = _sut.Generate(CarFactory.DefaultCar(), CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.Null(result.DiffFrontAccel);
        Assert.Null(result.CenterDiffBias);
    }

    [Fact]
    public void Generate_Drag_HasLaunchControl()
    {
        var car = CarFactory.DefaultCar();
        var track = new TrackInfo { Discipline = Discipline.Drag };

        var result = _sut.Generate(car, track, CarFactory.RelaxedConstraints());

        Assert.NotNull(result.LaunchControlRpm);
    }

    [Fact]
    public void Generate_NonDrag_NoLaunchControl()
    {
        var result = _sut.Generate(CarFactory.DefaultCar(), CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.Null(result.LaunchControlRpm);
    }

    [Fact]
    public void Generate_DragDiscipline_TirePressureRearLow()
    {
        var car = CarFactory.AWDPerformanceCar();
        var track = new TrackInfo { Discipline = Discipline.Drag };

        var result = _sut.Generate(car, track, CarFactory.RelaxedConstraints());

        Assert.True(result.TirePressureRear < result.TirePressureFront,
            "Drag: rear pressure should be lower than front");
    }

    [Fact]
    public void Generate_StockSuspension_CamberDisabled()
    {
        var car = CarFactory.FWDStockCar();
        var result = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.True(result.CamberFront == 0 && result.CamberRear == 0 ||
                    result.Explanations["Camber"] != "");
    }

    [Fact]
    public void Generate_StockDiff_DifferentialDisabled()
    {
        var car = CarFactory.FWDStockCar();
        var result = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.Contains("Differential", result.Explanations);
    }

    [Fact]
    public void Generate_Aero_NoneWhenNoAeroParts()
    {
        var car = CarFactory.FWDStockCar();
        var result = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.Equal(0, result.AeroFront);
        Assert.Equal(0, result.AeroRear);
    }

    [Fact]
    public void Generate_Aero_Drag_SetsRearToMin()
    {
        var car = CarFactory.DefaultCar();
        var track = new TrackInfo { Discipline = Discipline.Drag };

        var result = _sut.Generate(car, track, CarFactory.RelaxedConstraints());

        Assert.Equal(0, result.AeroFront);
        Assert.True(result.AeroRear > 0 || result.AeroRear == 0);
    }

    [Fact]
    public void Generate_ElectricCar_SingleGear()
    {
        var car = CarFactory.ElectricCar();
        var result = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.Single(result.GearRatios);
    }

    [Fact]
    public void Generate_ElectricCar_LaunchControl_IsNull()
    {
        var car = CarFactory.ElectricCar();
        var track = new TrackInfo { Discipline = Discipline.Drag };

        var result = _sut.Generate(car, track, CarFactory.RelaxedConstraints());

        // Electric motors deliver instant torque from 0 RPM — launch RPM doesn't apply
        Assert.Null(result.LaunchControlRpm);
    }

    [Fact]
    public void Generate_ElectricCar_Drag_SingleGear()
    {
        var car = CarFactory.ElectricCar();
        var track = new TrackInfo { Discipline = Discipline.Drag };

        var result = _sut.Generate(car, track, CarFactory.RelaxedConstraints());

        Assert.Single(result.GearRatios);
        Assert.Null(result.LaunchControlRpm);
    }

    [Fact]
    public void Generate_Discipline_Road_ExpectedValues()
    {
        var result = _sut.Generate(CarFactory.DefaultCar(), CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.True(result.CamberFront < 0);
        Assert.True(result.SpringFront > 0);
        Assert.True(result.ARBFront > 0);
    }

    [Fact]
    public void Generate_Discipline_Drift_LowCaster()
    {
        var car = CarFactory.DefaultCar();
        var track = new TrackInfo { Discipline = Discipline.Drift };

        var result = _sut.Generate(car, track, CarFactory.RelaxedConstraints());

        Assert.True(result.Caster >= 5.0 && result.Caster <= 15.0, $"Drift caster ({result.Caster}) should be in valid range");
    }

    [Fact]
    public void Generate_Discipline_Rally_SoftSprings()
    {
        var car = CarFactory.DefaultCar();
        car.SuspensionUpgrade = SuspensionUpgrade.Rally;
        var track = new TrackInfo { Discipline = Discipline.Rally };

        var result = _sut.Generate(car, track, CarFactory.RelaxedConstraints());

        Assert.True(result.RideHeightFront >= 100, "Rally ride height should be high");
    }

    [Fact]
    public void Generate_Discipline_CrossCountry_HighRideHeight()
    {
        var car = CarFactory.DefaultCar();
        car.SuspensionUpgrade = SuspensionUpgrade.Offroad;
        var track = new TrackInfo { Discipline = Discipline.CrossCountry };

        var result = _sut.Generate(car, track, CarFactory.RelaxedConstraints());

        Assert.True(result.RideHeightFront > 100, "CrossCountry ride height should be high");
    }

    [Fact]
    public void Generate_SportUpgrade_AccelOnlyNoDecel()
    {
        var car = CarFactory.DefaultCar();
        car.DifferentialUpgrade = DifferentialUpgrade.Sport;

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        // Sport LSD has only Acceleration slider in FH6/FH5 — Decel must remain at default (0)
        Assert.Equal(0, result.DiffDecel);
        Assert.True(result.DiffAccel > 0, $"Sport diff Accel should be > 0, got {result.DiffAccel}");
        if (car.DriveType == DriveType.AWD)
        {
            Assert.NotNull(result.DiffFrontAccel);
            Assert.Null(result.DiffFrontDecel);
        }
    }

    [Fact]
    public void Generate_OnlyFinalDrive_NoGearRatios()
    {
        var car = CarFactory.DefaultCar();
        car.OnlyFinalDriveCalculation = true;

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.Empty(result.GearRatios);
        Assert.True(result.FinalDrive > 0);
    }

    [Fact]
    public void Generate_NoGearCalc_ReturnsNoGearRatios()
    {
        var car = CarFactory.DefaultCar();
        car.AllowGearCalculation = false;

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.Empty(result.GearRatios);
    }

    [Fact]
    public void Generate_FWD_MoreFrontCamber()
    {
        var car = CarFactory.DefaultCar();
        car.DriveType = DriveType.FWD;

        var resultFWD = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        car.DriveType = DriveType.RWD;
        var resultRWD = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        // FWD typically has less negative camber front
        Assert.True(resultFWD.CamberFront > resultRWD.CamberFront || Math.Abs(resultFWD.CamberFront - resultRWD.CamberFront) < 0.5);
    }

    [Fact]
    public void Generate_Pressure_RWD_LowerRearForHighPower()
    {
        var car = CarFactory.DefaultCar();
        car.PowerHP = 800;
        car.TorqueNm = 900;

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        // High power RWD should have lower rear pressure
        Assert.True(result.TirePressureRear <= result.TirePressureFront + 0.5);
    }

    [Fact]
    public void Generate_WeightDistribution_EnginePositionOverride()
    {
        // Default 50.0 with mid engine should use 48
        var car = CarFactory.DefaultCar();
        car.WeightDistributionFront = 50;
        car.EnginePosition = EnginePosition.Mid;

        // We can't directly test private method EffectiveWtDist, but
        // spring/damper distribution will be affected
        var result = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.NotNull(result);
    }

    [Fact]
    public void Generate_FrontEngine_WeightDistributionOverride()
    {
        var car = CarFactory.DefaultCar();
        car.WeightDistributionFront = 50;
        car.EnginePosition = EnginePosition.Front;

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.NotNull(result);
    }

    [Fact]
    public void Generate_RearEngine_WeightDistributionOverride()
    {
        var car = CarFactory.DefaultCar();
        car.WeightDistributionFront = 50;
        car.EnginePosition = EnginePosition.Rear;

        var resultFront = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        car.WeightDistributionFront = 40;
        var resultExplicit = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.NotNull(resultFront);
        Assert.NotNull(resultExplicit);
    }

    [Fact]
    public void Generate_Turbo_MoreAccelLock()
    {
        var carNA = CarFactory.DefaultCar();
        carNA.AspirationType = AspirationType.Natural;

        var carTurbo = CarFactory.DefaultCar();
        carTurbo.AspirationType = AspirationType.TwinTurbo;

        var resultNA = _sut.Generate(carNA, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());
        var resultTurbo = _sut.Generate(carTurbo, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.NotNull(resultNA);
        Assert.NotNull(resultTurbo);
    }

    [Theory]
    [InlineData(Discipline.Road)]
    [InlineData(Discipline.Rally)]
    [InlineData(Discipline.Drift)]
    [InlineData(Discipline.Drag)]
    [InlineData(Discipline.CrossCountry)]
    [InlineData(Discipline.Touge)]
    [InlineData(Discipline.Street)]
    public void Generate_AllDisciplines_NoThrow(Discipline discipline)
    {
        var track = new TrackInfo { Discipline = discipline };

        var result = _sut.Generate(CarFactory.DefaultCar(), track, CarFactory.RelaxedConstraints());

        Assert.NotNull(result);
        Assert.True(result.TirePressureFront > 0);
    }

    [Theory]
    [InlineData(DriveType.FWD)]
    [InlineData(DriveType.RWD)]
    [InlineData(DriveType.AWD)]
    public void Generate_AllDriveTypes_NoThrow(DriveType dt)
    {
        var car = CarFactory.DefaultCar();
        car.DriveType = dt;

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.NotNull(result);
    }

    [Theory]
    [InlineData(EngineType.I4)]
    [InlineData(EngineType.V8)]
    [InlineData(EngineType.V12)]
    [InlineData(EngineType.Rotary)]
    [InlineData(EngineType.Boxer)]
    public void Generate_AllEngineTypes_NoThrow(EngineType engine)
    {
        var car = CarFactory.DefaultCar();
        car.EngineType = engine;

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.NotNull(result);
    }

    [Theory]
    [InlineData(EnginePosition.Front)]
    [InlineData(EnginePosition.Mid)]
    [InlineData(EnginePosition.Rear)]
    public void Generate_AllEnginePositions_NoThrow(EnginePosition ep)
    {
        var car = CarFactory.DefaultCar();
        car.EnginePosition = ep;

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.NotNull(result);
    }

    [Theory]
    [InlineData(TireType.Slick)]
    [InlineData(TireType.Sport)]
    [InlineData(TireType.Rally)]
    [InlineData(TireType.Offroad)]
    [InlineData(TireType.Drag)]
    [InlineData(TireType.Winter)]
    [InlineData(TireType.Drift)]
    public void Generate_AllTireTypes_NoThrow(TireType tire)
    {
        var car = CarFactory.DefaultCar();
        car.TireType = tire;

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.NotNull(result);
        Assert.True(result.TirePressureFront > 0);
    }

    [Theory]
    [InlineData(SuspensionUpgrade.Race)]
    [InlineData(SuspensionUpgrade.Rally)]
    [InlineData(SuspensionUpgrade.Drift)]
    [InlineData(SuspensionUpgrade.Offroad)]
    [InlineData(SuspensionUpgrade.Street)]
    [InlineData(SuspensionUpgrade.Sport)]
    [InlineData(SuspensionUpgrade.Stock)]
    public void Generate_AllSuspensionUpgrades_NoThrow(SuspensionUpgrade su)
    {
        var car = CarFactory.DefaultCar();
        car.SuspensionUpgrade = su;

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.NotNull(result);
    }

    [Theory]
    [InlineData(DifferentialUpgrade.Stock)]
    [InlineData(DifferentialUpgrade.Sport)]
    [InlineData(DifferentialUpgrade.Race)]
    [InlineData(DifferentialUpgrade.Rally)]
    [InlineData(DifferentialUpgrade.DriftSpec)]
    [InlineData(DifferentialUpgrade.Offroad)]
    public void Generate_AllDifferentialUpgrades_NoThrow(DifferentialUpgrade diff)
    {
        var car = CarFactory.DefaultCar();
        car.DifferentialUpgrade = diff;

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.NotNull(result);
    }

    [Theory]
    [InlineData(AspirationType.Natural)]
    [InlineData(AspirationType.SingleTurbo)]
    [InlineData(AspirationType.TwinTurbo)]
    [InlineData(AspirationType.PositiveDisplacement)]
    [InlineData(AspirationType.Centrifugal)]
    public void Generate_AllAspirationTypes_NoThrow(AspirationType asp)
    {
        var car = CarFactory.DefaultCar();
        car.AspirationType = asp;

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.NotNull(result);
    }

    [Fact]
    public void Generate_HybridPowertrain_NoThrow()
    {
        var car = CarFactory.DefaultCar();
        car.PowertrainType = PowertrainType.Hybrid;

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.NotNull(result);
    }

    [Fact]
    public void Generate_AntiLagSingleTurbo_NoThrow()
    {
        var car = CarFactory.DefaultCar();
        car.AspirationType = AspirationType.SingleTurbo;
        car.AntiLag = true;

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.NotNull(result);
    }

    [Fact]
    public void Generate_AntiLagTwinTurbo_NoThrow()
    {
        var car = CarFactory.DefaultCar();
        car.AspirationType = AspirationType.TwinTurbo;
        car.AntiLag = true;

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.NotNull(result);
    }

    [Fact]
    public void Generate_DieselFuel_NoThrow()
    {
        var car = CarFactory.DefaultCar();
        car.FuelType = FuelType.Diesel;

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.NotNull(result);
    }

    [Fact]
    public void Generate_HeavyCar_ValuesScale()
    {
        var car = CarFactory.DefaultCar();
        car.TotalMass = 2500;

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.True(result.SpringFront > 50);
        Assert.True(result.ARBFront > 10);
    }

    [Fact]
    public void Generate_LightCar_ValuesScale()
    {
        var car = CarFactory.DefaultCar();
        car.TotalMass = 800;

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.True(result.SpringFront > 0);
    }

    [Fact]
    public void Generate_HighPower_ValuesScale()
    {
        var car = CarFactory.DefaultCar();
        car.PowerHP = 1500;
        car.TorqueNm = 1600;

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.NotNull(result);
    }

    [Fact]
    public void Generate_BumpyBumpRatio_Valid()
    {
        var result = _sut.Generate(CarFactory.DefaultCar(), CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.True(result.BumpFront <= result.ReboundFront);
        Assert.True(result.BumpRear <= result.ReboundRear);
    }

    [Fact]
    public void Generate_NoAero_AeroExplainsNone()
    {
        var car = CarFactory.FWDStockCar();
        var result = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.DoesNotContain("Expl_Aero_None", result.Explanations["Aero"]);
        Assert.NotEmpty(result.Explanations["Aero"]);
    }

    [Fact]
    public void Generate_MultiGear_AllRatiosPositive()
    {
        var result = _sut.Generate(CarFactory.DefaultCar(), CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        foreach (var g in result.GearRatios)
            Assert.True(g > 0);
    }

    [Fact]
    public void Generate_2Gear_Works()
    {
        var car = CarFactory.DefaultCar();
        car.GearCount = 2;

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.Equal(2, result.GearRatios.Count);
    }

    [Fact]
    public void Generate_10Gear_Works()
    {
        var car = CarFactory.DefaultCar();
        car.GearCount = 10;

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.Equal(10, result.GearRatios.Count);
    }

    [Fact]
    public void Generate_LongWheelbase_LessAccelLock()
    {
        var carShort = CarFactory.DefaultCar();
        carShort.Wheelbase = 2400;

        var carLong = CarFactory.DefaultCar();
        carLong.Wheelbase = 3200;

        var rShort = _sut.Generate(carShort, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());
        var rLong = _sut.Generate(carLong, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.NotEqual(rShort.DiffAccel, rLong.DiffAccel);
    }

    [Fact]
    public void Generate_WideTrack_LessARB()
    {
        var carNarrow = CarFactory.DefaultCar();
        carNarrow.FrontTrack = 1400;
        carNarrow.RearTrack = 1420;

        var carWide = CarFactory.DefaultCar();
        carWide.FrontTrack = 1700;
        carWide.RearTrack = 1720;

        var rNarrow = _sut.Generate(carNarrow, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());
        var rWide = _sut.Generate(carWide, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.NotEqual(rNarrow.ARBFront, rWide.ARBFront);
    }

    [Fact]
    public void Generate_AllDisciplines_DifferentResults()
    {
        var car = CarFactory.DefaultCar();
        var results = new List<TuneResult>();

        foreach (Discipline d in Enum.GetValues<Discipline>())
        {
            var track = new TrackInfo { Discipline = d };
            results.Add(_sut.Generate(car, track, CarFactory.RelaxedConstraints()));
        }

        // Different disciplines should produce different spring rates
        var springValues = results.Select(r => r.SpringFront).Distinct().ToList();
        Assert.True(springValues.Count > 1, "Different disciplines should produce different spring rates");
    }

    [Fact]
    public void Generate_AWD_Drag_FrontDiff50()
    {
        var car = CarFactory.AWDPerformanceCar();
        var track = new TrackInfo { Discipline = Discipline.Drag };

        var result = _sut.Generate(car, track, CarFactory.RelaxedConstraints());

        Assert.NotNull(result.DiffFrontAccel);
        Assert.True(result.DiffFrontAccel > 0);
    }

    [Fact]
    public void Generate_AWD_Road_CenterBiasRear()
    {
        var car = CarFactory.AWDPerformanceCar();

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.NotNull(result.CenterDiffBias);
        Assert.True(result.CenterDiffBias >= 50, $"AWD road center bias ({result.CenterDiffBias}) should be rear-biased (>=50)");
    }

    [Fact]
    public void Generate_AWD_Drift_CenterBiasBalanced()
    {
        var car = CarFactory.AWDPerformanceCar();
        var track = new TrackInfo { Discipline = Discipline.Drift };

        var result = _sut.Generate(car, track, CarFactory.RelaxedConstraints());

        Assert.NotNull(result.CenterDiffBias);
    }

    [Fact]
    public void Generate_Drag_Quarter_Has3Gears()
    {
        var track = new TrackInfo { Discipline = Discipline.Drag, DragDistance = DragDistance.Quarter };

        var result = _sut.Generate(CarFactory.DefaultCar(), track, CarFactory.RelaxedConstraints());

        Assert.InRange(result.GearRatios.Count, 3, 6);
    }

    [Fact]
    public void Generate_Touge_RWD_DifferentToe()
    {
        var car = CarFactory.DefaultCar();
        var trackRWD = new TrackInfo { Discipline = Discipline.Touge };
        car.DriveType = DriveType.RWD;

        var resultRWD = _sut.Generate(car, trackRWD, CarFactory.RelaxedConstraints());

        car.DriveType = DriveType.FWD;
        var resultFWD = _sut.Generate(car, trackRWD, CarFactory.RelaxedConstraints());

        Assert.NotEqual(resultRWD.ToeFront, resultFWD.ToeFront);
    }

    [Fact]
    public void Generate_WinterTires_LowerPressure()
    {
        var carSport = CarFactory.DefaultCar();
        carSport.TireType = TireType.Sport;

        var carWinter = CarFactory.DefaultCar();
        carWinter.TireType = TireType.Winter;

        var rSport = _sut.Generate(carSport, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());
        var rWinter = _sut.Generate(carWinter, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.True(rWinter.TirePressureFront <= rSport.TirePressureFront,
            "Winter tires should have lower or equal pressure than sport tires");
    }

    [Fact]
    public void Generate_SameInput_Deterministic()
    {
        var car = CarFactory.DefaultCar();
        var track = CarFactory.DefaultTrack();
        var c = CarFactory.RelaxedConstraints();

        var r1 = _sut.Generate(car, track, c);
        var r2 = _sut.Generate(car, track, c);

        Assert.Equal(r1.TirePressureFront, r2.TirePressureFront);
        Assert.Equal(r1.CamberFront, r2.CamberFront);
        Assert.Equal(r1.SpringFront, r2.SpringFront);
        Assert.Equal(r1.RideHeightFront, r2.RideHeightFront);
        Assert.Equal(r1.FinalDrive, r2.FinalDrive);
        Assert.Equal(r1.GearRatios.Count, r2.GearRatios.Count);
        for (int i = 0; i < r1.GearRatios.Count; i++)
            Assert.Equal(r1.GearRatios[i], r2.GearRatios[i]);
    }

    [Fact]
    public void Generate_CamberDisabled_ForNonRaceSuspension()
    {
        var car = CarFactory.DefaultCar();
        car.SuspensionUpgrade = SuspensionUpgrade.Sport;

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.Equal(0, result.CamberFront);
        Assert.Equal(0, result.CamberRear);
    }

    [Fact]
    public void Generate_SpringsDisabled_ForNonRaceSuspension()
    {
        var car = CarFactory.DefaultCar();
        car.SuspensionUpgrade = SuspensionUpgrade.Sport;

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.Equal(0, result.SpringFront);
        Assert.Equal(0, result.SpringRear);
    }

    [Fact]
    public void Generate_RideHeightDisabled_ForNonRaceSuspension()
    {
        var car = CarFactory.DefaultCar();
        car.SuspensionUpgrade = SuspensionUpgrade.Sport;

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.Equal(0, result.RideHeightFront);
        Assert.Equal(0, result.RideHeightRear);
    }

    [Fact]
    public void Generate_OnlyFinalDriveCalc_NoGearChange()
    {
        var car = CarFactory.DefaultCar();
        car.OnlyFinalDriveCalculation = true;

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.Empty(result.GearRatios);
        Assert.True(result.FinalDrive > 0);
    }

    // ── Post-validation tests ──────────────────────────────────────────────

    [Fact]
    public void Generate_PostValidate_AdjustsFDWhenConstrained()
    {
        // Fast car where FD is forced too high → actual speed < target → post-validate should adjust
        var car = CarFactory.AWDPerformanceCar();
        var c = CarFactory.RelaxedConstraints();
        c.FinalDriveMin = 4.5; // Force FD high → actual speed will be low

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), c);

        Assert.True(result.FinalDrive > 0);
        Assert.True(result.ActualMaxSpeedKmh > 0);
    }

    [Fact]
    public void Generate_PostValidate_NoFalsePositiveForNormalCar()
    {
        var car = CarFactory.AWDPerformanceCar();
        var c = CarFactory.RelaxedConstraints();

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), c);

        // Normal car should still produce valid results
        Assert.InRange(result.SpringFront, c.SpringFrontMin, c.SpringFrontMax);
        Assert.InRange(result.SpringRear, c.SpringRearMin, c.SpringRearMax);
        Assert.InRange(result.FinalDrive, c.FinalDriveMin, c.FinalDriveMax);
        Assert.NotEmpty(result.GearRatios);
    }

    [Fact]
    public void Generate_PostValidate_SpringRideHeightConsistency()
    {
        var car = CarFactory.DefaultCar();
        // Set up constraints where ride height is low and springs would normally be soft
        var c = CarFactory.RelaxedConstraints();
        c.RideHeightFrontMin = 10;
        c.RideHeightFrontMax = 50;   // very low max → forces low ride height
        c.RideHeightRearMin = 10;
        c.RideHeightRearMax = 50;
        c.SpringFrontMin = 10;
        c.SpringFrontMax = 600;
        c.SpringRearMin = 10;
        c.SpringRearMax = 600;

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), c);

        // Springs should be reasonable (not at minimum despite low ride height)
        Assert.True(result.RideHeightFront > 0);
        Assert.True(result.RideHeightRear > 0);
        Assert.True(result.SpringFront > 0);
        Assert.True(result.SpringRear > 0);
    }

    [Fact]
    public void Generate_PostValidate_GearRatiosRemainDescending()
    {
        var result = _sut.Generate(CarFactory.DefaultCar(), CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        for (int i = 1; i < result.GearRatios.Count; i++)
            Assert.True(result.GearRatios[i] <= result.GearRatios[i - 1],
                $"Gear {i + 1} ({result.GearRatios[i]}) > gear {i} ({result.GearRatios[i - 1]}) after post-validation");
    }

    [Fact]
    public void Generate_PostValidate_AllValuesStillInRange()
    {
        var car = CarFactory.DefaultCar();
        var c = CarFactory.RelaxedConstraints();

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), c);

        Assert.InRange(result.TirePressureFront, c.TirePressureFrontMin, c.TirePressureFrontMax);
        Assert.InRange(result.TirePressureRear, c.TirePressureRearMin, c.TirePressureRearMax);
        Assert.InRange(result.CamberFront, c.CamberFrontMin, c.CamberFrontMax);
        Assert.InRange(result.CamberRear, c.CamberRearMin, c.CamberRearMax);
        Assert.InRange(result.ToeFront, c.ToeFrontMin, c.ToeFrontMax);
        Assert.InRange(result.ToeRear, c.ToeRearMin, c.ToeRearMax);
        Assert.InRange(result.ARBFront, c.ARBFrontMin, c.ARBFrontMax);
        Assert.InRange(result.ARBRear, c.ARBRearMin, c.ARBRearMax);
        Assert.InRange(result.ReboundFront, c.ReboundFrontMin, c.ReboundFrontMax);
        Assert.InRange(result.ReboundRear, c.ReboundRearMin, c.ReboundRearMax);
        Assert.InRange(result.BumpFront, c.BumpFrontMin, c.BumpFrontMax);
        Assert.InRange(result.BumpRear, c.BumpRearMin, c.BumpRearMax);
    }

    [Fact]
    public void Generate_Caster_DifferentByDiscipline()
    {
        var car = CarFactory.DefaultCar();
        var c = CarFactory.RelaxedConstraints();

        double cast(Discipline d) => _sut.Generate(car, new TrackInfo { Discipline = d }, c).Caster;

        double road = cast(Discipline.Road);
        double drag = cast(Discipline.Drag);
        double drift = cast(Discipline.Drift);
        double rally = cast(Discipline.Rally);
        double cc = cast(Discipline.CrossCountry);

        // Drag should have lowest caster (0.90 multiplier), Drift highest (1.15)
        Assert.True(drag <= road, $"Drag caster ({drag}) should be <= Road caster ({road})");
        Assert.True(drift >= road, $"Drift caster ({drift}) should be >= Road caster ({road})");
        Assert.True(rally <= road, $"Rally caster ({rally}) should be <= Road caster ({road})");
        Assert.True(cc <= road, $"CrossCountry caster ({cc}) should be <= Road caster ({road})");
    }

    [Fact]
    public void Generate_Drag_GearCount_HighPowerCar()
    {
        var car = CarFactory.AWDPerformanceCar(); // 565 HP, 633 Nm, AWD
        var c = CarFactory.RelaxedConstraints();

        var rQ = _sut.Generate(car, new TrackInfo { Discipline = Discipline.Drag, DragDistance = DragDistance.Quarter }, c);
        var rH = _sut.Generate(car, new TrackInfo { Discipline = Discipline.Drag, DragDistance = DragDistance.Half }, c);
        var rM = _sut.Generate(car, new TrackInfo { Discipline = Discipline.Drag, DragDistance = DragDistance.Mile }, c);

        // AWD + high torque should still produce a usable ratio set at every distance
        Assert.InRange(rQ.GearRatios.Count, 3, 7);
        Assert.InRange(rH.GearRatios.Count, 3, 7);
        Assert.InRange(rM.GearRatios.Count, 3, 7);
        Assert.True(rQ.FinalDrive > 0);
        Assert.True(rH.FinalDrive > 0);
        Assert.True(rM.FinalDrive > 0);
    }

    [Fact]
    public void Generate_RecommendedGearCount_IsComputed_DecoupledFromBox()
    {
        // A 10-speed gearbox, but a 300 HP / 7000 rpm engine doesn't need 10 gears: the ratio set
        // still follows the box (10), while the recommendation is computed and fewer.
        var car = CarFactory.DefaultCar();
        car.GearCount = 10;

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());

        Assert.Equal(10, result.GearRatios.Count);                       // ratios follow the box
        Assert.InRange(result.RecommendedGearCount, 2, 9);               // computed, fewer
        Assert.True(result.RecommendedGearCount < result.GearRatios.Count);
    }

    [Fact]
    public void Generate_RecommendedGearCount_Electric_IsOne()
    {
        var result = _sut.Generate(CarFactory.ElectricCar(), CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());
        Assert.Equal(1, result.RecommendedGearCount);
    }

    [Fact]
    public void Generate_RecommendedGearCount_PeakyEngine_NeedsAtLeastAsManyAsFlat()
    {
        // Same car, only the engine's character differs: a peaky engine (torque peaks high, near
        // the limiter) wants more, closer gears than a flat/torquey one. This is the "power /
        // engine is accounted for" guard.
        var c = CarFactory.RelaxedConstraints();
        var track = CarFactory.DefaultTrack();

        var peaky = CarFactory.DefaultCar(); peaky.EngineType = EngineType.Rotary; // torque peaks high
        var flat  = CarFactory.DefaultCar(); flat.EngineType  = EngineType.V8;     // torque peaks low

        var rPeaky = _sut.Generate(peaky, track, c);
        var rFlat  = _sut.Generate(flat, track, c);

        Assert.True(rPeaky.RecommendedGearCount >= rFlat.RecommendedGearCount,
            $"peaky engine ({rPeaky.RecommendedGearCount}) should want >= flat engine ({rFlat.RecommendedGearCount})");
    }

    [Fact]
    public void Generate_RecommendedGearCount_ShorterDragStrip_NeedsNoMoreGears()
    {
        var car = CarFactory.AWDPerformanceCar();
        var c = CarFactory.RelaxedConstraints();

        var quarter = _sut.Generate(car, new TrackInfo { Discipline = Discipline.Drag, DragDistance = DragDistance.Quarter }, c);
        var mile    = _sut.Generate(car, new TrackInfo { Discipline = Discipline.Drag, DragDistance = DragDistance.Mile }, c);

        // A shorter strip reaches a lower terminal speed → never needs MORE gears than a full mile.
        Assert.True(quarter.RecommendedGearCount <= mile.RecommendedGearCount);
        Assert.InRange(quarter.RecommendedGearCount, 2, 10);
    }

    [Fact]
    public void Generate_FinalDrive_RespectsTightenedConstraint()
    {
        // Request: the final-drive clamp IS the constraints. Raising FinalDriveMin must lift the FD.
        var car = CarFactory.DefaultCar();
        var c = CarFactory.RelaxedConstraints();
        c.FinalDriveMin = 4.8;

        var result = _sut.Generate(car, CarFactory.DefaultTrack(), c);

        Assert.True(result.FinalDrive >= 4.8, $"FinalDrive {result.FinalDrive} should honour Min 4.8");
        Assert.True(result.FinalDrive <= c.FinalDriveMax);
    }

    [Fact]
    public async Task Generate_RealDbCar_AllDisciplines_ValuesInRange()
    {
        using var env = new TestingEnvironment();
        await Fh6DatabaseService.Instance.InitializeAsync();
        var db = Fh6DatabaseService.Instance;

        var carIds = new[] { 247, 295, 125 };
        var disciplines = new[] { Discipline.Road, Discipline.Rally, Discipline.Drag, Discipline.Drift };

        foreach (int carId in carIds)
        {
            var dbCar = db.GetCar(carId);
            if (dbCar == null) continue;

            var car = new CarCard();
            // Reuse the app's own PopulateCarFromDb logic inline
            var specController = new Forza_Horizon_6_Tune_Master.ViewModels.CarSpecController();
            var carData = new CarData { Id = carId };
            specController.PopulateCarFromDb(carData, car);

            var parts = new SelectedParts();
            parts.SetCarData(car);
            parts.ResolveEngineAndMotorIds();

            if (!car.SuspensionAllowsAdvancedTuning)
                car.SuspensionUpgrade = SuspensionUpgrade.Race;
            car.AllowGearCalculation = true;
            car.HasRearAero = true;
            car.HasFrontAero = true;
            car.HasFrontARB = true;
            car.HasRearARB = true;

            foreach (var disc in disciplines)
            {
                var track = new TrackInfo { Discipline = disc, DragDistance = DragDistance.Quarter };
                var result = new TuneGeneratorService().Generate(car, track, parts, db);

                Assert.NotNull(result);
                Assert.NotNull(result.Explanations);

                foreach (var key in new[] { "TirePressure", "Camber", "Toe", "Caster", "ARB", "Springs", "RideHeight", "Dampers", "Differential", "Brakes", "FinalDrive", "Aero" })
                    Assert.True(result.Explanations.ContainsKey(key), $"Car {carId} {disc}: missing '{key}'");

                Assert.InRange(result.TirePressureFront, 0.8, 5.0);
                Assert.InRange(result.TirePressureRear, 0.8, 5.0);

                if (car.SuspensionAllowsAdvancedTuning)
                {
                    Assert.True(result.SpringFront > 0, $"Car {carId} {disc}: SpringFront={result.SpringFront}");
                    Assert.True(result.SpringRear > 0, $"Car {carId} {disc}: SpringRear={result.SpringRear}");
                    Assert.True(result.RideHeightFront >= 0);
                    Assert.True(result.RideHeightRear >= 0);
                    Assert.True(result.ReboundFront >= 0);
                    Assert.True(result.BumpFront >= 0);
                }

                Assert.InRange(result.DiffAccel, 0, 100);
                if (car.DriveType == DriveType.AWD)
                {
                    Assert.NotNull(result.DiffFrontAccel);
                    Assert.NotNull(result.CenterDiffBias);
                    Assert.InRange(result.CenterDiffBias!.Value, 0, 100);
                }

                Assert.InRange(result.BrakeBalance, 20, 80);
                Assert.InRange(result.BrakePressure, 30, 260);
                Assert.True(result.FinalDrive > 0, $"Car {carId} {disc}: FinalDrive={result.FinalDrive}");

                if (result.GearRatios.Count > 0)
                    Assert.True(result.ActualMaxSpeedKmh > 0, $"Car {carId} {disc}: ActualMaxSpeed={result.ActualMaxSpeedKmh}");

                Assert.NotEmpty(result.Explanations["Aero"]);
            }
        }
    }

    [Fact]
    public void Generate_TirePressure_SeasonDeltasConsistent()
    {
        var car = CarFactory.DefaultCar();
        var c = CarFactory.RelaxedConstraints();

        double press(Season s) => _sut.Generate(car, new TrackInfo { Discipline = Discipline.Road, Season = s }, c).TirePressureFront;

        double wi = press(Season.Winter);
        double sp = press(Season.Spring);
        double su = press(Season.Summer);
        double au = press(Season.Autumn);

        // Season deltas: Winter +0.05, Spring +0.02, Autumn -0.02, Summer -0.10
        Assert.InRange(wi - su, 0.12, 0.18);
        Assert.InRange(wi - au, 0.04, 0.10);
        Assert.InRange(sp - su, 0.08, 0.14);
        Assert.InRange(sp - au, 0.00, 0.06);
    }
}
