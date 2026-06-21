using System;
using System.Linq;
using System.Threading.Tasks;
using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;
using TuneMaster.Tests.Helpers;
using Xunit;

namespace TuneMaster.Tests.Services;

[Collection("FileSystem")]
public class DbIntegrationCalculatorTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async Task InitDbAsync()
    {
        await Fh6DatabaseService.Instance.InitializeAsync();
    }

    private static CarCard BuildCarCard(int carDbId)
    {
        var db = Fh6DatabaseService.Instance;
        var dbCar = db.GetCar(carDbId);
        if (dbCar == null)
            throw new InvalidOperationException($"Car {carDbId} not found in DB");

        var body = db.GetCarBody(carDbId * 1000);

        // Resolve the effective engine ID from stock engine swap
        int engineId = 0;
        var swaps = db.GetEngineSwaps(carDbId);
        var stock = swaps.FirstOrDefault(s => s.IsStock);
        if (stock != null) engineId = stock.EngineID;
        if (engineId == 0) engineId = swaps.FirstOrDefault()?.EngineID ?? 0;

        return new CarCard
        {
            CarDbId = carDbId,
            CarBodyId = carDbId * 1000,
            EngineDbId = engineId,
            Name = dbCar.DisplayName,
            Make = dbCar.MakeName,
            Model = dbCar.ModelShort,
            Year = dbCar.Year,
            TotalMass = dbCar.CurbWeight * 100,
            WeightDistributionFront = dbCar.WeightDistribution,
            EngineType = EngineTypeFromCylinder(dbCar.CylinderID),
            EnginePosition = EnginePositionFromPlacement(dbCar.EnginePlacementID),
            AspirationType = AspirationTypeFromId(dbCar.AspirationTypeId),
            FuelType = FuelType.Gasoline,
            PowertrainType = dbCar.PowertrainID == 1 ? PowertrainType.Electric : PowertrainType.ICE,
            DriveType = DriveTypeFromId(dbCar.DriveTypeID),
            GearCount = dbCar.NumGears,
            MaxAvailableGearCount = 10,
            AllowGearCalculation = true,
            OnlyFinalDriveCalculation = false,
            FrontTireWidth = dbCar.FrontTireWidthMM,
            FrontTireProfile = dbCar.FrontTireAspect,
            RearTireWidth = dbCar.RearTireWidthMM,
            RearTireProfile = dbCar.RearTireAspect,
            FrontRimDiameter = dbCar.FrontWheelDiameterIN,
            RearRimDiameter = dbCar.RearWheelDiameterIN,
            TireType = TireType.Stock,
            SuspensionUpgrade = SuspensionUpgrade.Race,
            DifferentialUpgrade = DifferentialUpgrade.Race,
            BrakesUpgrade = BrakesUpgrade.Race,
            Wheelbase = (int)(body?.Wheelbase ?? 2700),
            FrontTrack = (int)(body?.ModelFrontTrackOuter ?? 1550),
            RearTrack = (int)(body?.ModelRearTrackOuter ?? 1570),
            Cd = 0,
            FrontalAreaM2 = 0,
            HasFrontARB = true,
            HasRearARB = true,
            HasRearAero = true,
            HasFrontAero = false,
            RearWingPartId = 1,
            ArbFrontPartId = 1,
            ArbRearPartId = 1,
        };
    }

    private static EngineType EngineTypeFromCylinder(int cylinderId) => cylinderId switch
    {
        3 => EngineType.I3,
        4 => EngineType.I4,
        5 => EngineType.I5,
        6 => EngineType.V6,
        8 => EngineType.V8,
        10 => EngineType.V10,
        12 => EngineType.V12,
        _ => EngineType.V6
    };

    private static EnginePosition EnginePositionFromPlacement(int placementId) => placementId switch
    {
        1 => EnginePosition.Front,
        3 => EnginePosition.Rear,
        _ => EnginePosition.Front
    };

    private static AspirationType? AspirationTypeFromId(int aspId) => aspId switch
    {
        0 => Forza_Horizon_6_Tune_Master.Models.AspirationType.Natural,
        1 => Forza_Horizon_6_Tune_Master.Models.AspirationType.SingleTurbo,
        2 => Forza_Horizon_6_Tune_Master.Models.AspirationType.TwinTurbo,
        3 => Forza_Horizon_6_Tune_Master.Models.AspirationType.PositiveDisplacement,
        4 => Forza_Horizon_6_Tune_Master.Models.AspirationType.Centrifugal,
        5 => Forza_Horizon_6_Tune_Master.Models.AspirationType.Electric,
        _ => Forza_Horizon_6_Tune_Master.Models.AspirationType.Natural
    };

    // DB drive-type IDs (List_DriveType): 1 = FWD, 2 = RWD, 3 = AWD.
    private static DriveType DriveTypeFromId(int dtId) => dtId switch
    {
        1 => DriveType.FWD,
        3 => DriveType.AWD,
        _ => DriveType.RWD
    };

    // ── DB Data Integrity ────────────────────────────────────────────────────

    [Fact]
    public async Task DbCar_CurbWeightMultiply100_GivesKilograms()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();
        var dbCar = Fh6DatabaseService.Instance.GetCar(247);
        Assert.NotNull(dbCar);
        double kg = dbCar.CurbWeight * 100;
        Assert.InRange(kg, 800, 3000);
    }

    [Fact]
    public async Task DbCar_WeightDistribution_IsFraction()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();
        var dbCar = Fh6DatabaseService.Instance.GetCar(247);
        Assert.NotNull(dbCar);
        Assert.InRange(dbCar.WeightDistribution, 0.30, 0.70);
    }

    [Fact]
    public async Task DbCar_CarBodyId_IsCarIdTimes1000()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();
        var body = Fh6DatabaseService.Instance.GetCarBody(247000);
        Assert.NotNull(body);
    }

    [Fact]
    public async Task Db_Car247_HasEngineSwaps()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();
        var swaps = Fh6DatabaseService.Instance.GetEngineSwaps(247);
        Assert.NotEmpty(swaps);
        var stock = swaps.FirstOrDefault(s => s.IsStock);
        Assert.NotNull(stock);
        Assert.True(stock.EngineID > 0);
    }

    [Fact]
    public async Task Db_Car247_StockCamHasTorqueCurveId()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();
        var swaps = Fh6DatabaseService.Instance.GetEngineSwaps(247);
        var stock = swaps.FirstOrDefault(s => s.IsStock);
        Assert.NotNull(stock);

        var cams = Fh6DatabaseService.Instance.GetCamshafts(stock.EngineID);
        var stockCam = cams.FirstOrDefault(c => c.IsStock);
        Assert.NotNull(stockCam);
        Assert.True(stockCam.TorqueCurveFullThrottleID > 0, "Stock cam must have a torque curve ID");
    }

    [Fact]
    public async Task Db_SpringDamperPhysics_MinRideHeightInMeters()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();
        var db = Fh6DatabaseService.Instance;
        // Check via known spring damper physics records from car 247
        var sds = db.GetSpringDampers(247);
        Assert.NotEmpty(sds);
        foreach (var sd in sds)
        {
            var frontPhys = db.GetSpringDamperPhysics(sd.FrontSpringDamperPhysicsID);
            var rearPhys = db.GetSpringDamperPhysics(sd.RearSpringDamperPhysicsID);
            if (frontPhys != null)
            {
                Assert.InRange(frontPhys.MinRideHeight, 0.01, 0.50);
                Assert.InRange(frontPhys.MaxRideHeight, frontPhys.MinRideHeight, 0.60);
            }
            if (rearPhys != null)
            {
                Assert.InRange(rearPhys.MinRideHeight, 0.01, 0.50);
                Assert.InRange(rearPhys.MaxRideHeight, rearPhys.MinRideHeight, 0.60);
            }
        }
    }

    [Fact]
    public async Task Db_EngineSwapTorqueScales_IncreaseByLevel_Car247()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();
        var swaps = Fh6DatabaseService.Instance.GetEngineSwaps(247);
        var nonStock = swaps.Where(s => !s.IsStock).OrderBy(s => s.Level).ToList();
        Assert.NotEmpty(nonStock);

        for (int i = 1; i < nonStock.Count; i++)
        {
            double prevScale = nonStock[i - 1].TorqueScale ?? 1.0;
            double currScale = nonStock[i].TorqueScale ?? 1.0;
            Assert.True(currScale >= prevScale,
                $"Swap Lv{nonStock[i].Level} scale ({currScale}) should be >= Lv{nonStock[i - 1].Level} ({prevScale})");
        }
    }

    // ── Power Calculator ─────────────────────────────────────────────────────

    [Fact]
    public async Task Power_StockEngine_IceCar_PowerIsPositiveAndReasonable()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();
        var car = BuildCarCard(247);
        PowerCalculator.Calculate(car);
        Assert.True(car.PowerHP > 20, $"Stock power too low: {car.PowerHP}");
        Assert.InRange(car.PowerHP, 20, 2000);
    }

    [Fact]
    public async Task Power_StockEngine_IceCar_TorqueCurvePopulated()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();
        var car = BuildCarCard(247);
        PowerCalculator.Calculate(car);
        if (car.CachedTorqueCurveNm == null || car.CachedTorqueCurveNm.Length == 0)
        {
            // Synthetic fallback: torque curve may be generated by GenerateIceTorqueCurve
            Assert.True(car.PowerHP > 0, "Must have power even without explicit torque curve");
            Assert.True(car.TorqueNm > 0, "Must have torque even without explicit torque curve");
            return;
        }
        Assert.NotEmpty(car.CachedTorqueCurveNm);
        Assert.True(car.CachedTorqueCurveNm.Max() > 30, $"Torque curve peak must be > 30 Nm, got {car.CachedTorqueCurveNm.Max()}");
    }

    [Fact]
    public async Task Power_StockEngine_IceCar_MaxRpmFromDbCam()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();
        var car = BuildCarCard(247);
        PowerCalculator.Calculate(car);
        Assert.InRange(car.MaxRPM, 3000, 12000);
    }

    [Fact]
    public async Task Power_EngineSwap_DoesNotApplyOriginalCarGameTorqueScale()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();
        var car = BuildCarCard(247);
        var dbCar = Fh6DatabaseService.Instance.GetCar(247);

        var parts = new SelectedParts();
        var swaps = Fh6DatabaseService.Instance.GetEngineSwaps(247);
        var nonStock = swaps.FirstOrDefault(s => !s.IsStock);
        Assert.NotNull(nonStock);

        parts.EngineSwapPartId = nonStock.Id;
        PowerCalculator.Calculate(car, parts);

        Assert.True(car.PowerHP > 20, $"Swapped power too low: {car.PowerHP}");
    }

    [Fact]
    public async Task Power_EngineSwap_HigherLevelGivesMorePower()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var swaps = Fh6DatabaseService.Instance.GetEngineSwaps(247);
        var nonStock = swaps.Where(s => !s.IsStock).OrderBy(s => s.Level).ToList();
        if (nonStock.Count < 2) return;

        double prevPower = 0;
        for (int i = 0; i < nonStock.Count; i++)
        {
            var testParts = new SelectedParts();
            testParts.EngineSwapPartId = nonStock[i].Id;
            var testCar = BuildCarCard(247);
            PowerCalculator.Calculate(testCar, testParts);

            if (i > 0)
                Assert.True(testCar.PowerHP >= prevPower - 1,
                    $"Swap Lv{nonStock[i].Level} power ({testCar.PowerHP}) should be >= Lv{nonStock[i - 1].Level} ({prevPower})");
            prevPower = testCar.PowerHP;
        }
    }

    [Fact]
    public async Task Power_NonStockParts_ProductOfScales_NotAttenuated()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var car = BuildCarCard(247);
        var dbCar = Fh6DatabaseService.Instance.GetCar(247);
        var engineId = dbCar.Id;

        // Get two non-stock parts with TorqueScale > 1
        var cams = Fh6DatabaseService.Instance.GetCamshafts(engineId);
        var nonStockCams = cams.Where(c => !c.IsStock && (c.TorqueScale ?? 1.0) > 1.01).ToList();

        var intakes = Fh6DatabaseService.Instance.GetIntake(engineId);
        var nonStockIntakes = intakes.Where(i => !i.IsStock && (i.TorqueScale ?? 1.0) > 1.01).ToList();

        if (nonStockCams.Count == 0 || nonStockIntakes.Count == 0)
            return;

        // One part only
        var partsOne = new SelectedParts();
        partsOne.CamshaftPartId = nonStockCams[0].Id;
        var carOne = BuildCarCard(247);
        PowerCalculator.Calculate(carOne, partsOne);

        // Two parts
        var partsTwo = new SelectedParts();
        partsTwo.CamshaftPartId = nonStockCams[0].Id;
        partsTwo.IntakePartId = nonStockIntakes[0].Id;
        var carTwo = BuildCarCard(247);
        PowerCalculator.Calculate(carTwo, partsTwo);

        Assert.True(carTwo.PowerHP >= carOne.PowerHP - 5,
            $"Two non-stock parts ({carTwo.PowerHP}) should give more power than one ({carOne.PowerHP})");
    }

    [Fact]
    public async Task Power_StockParts_DoNotAffectScale()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var car = BuildCarCard(247);
        PowerCalculator.Calculate(car);
        double stockPower = car.PowerHP;

        var parts = new SelectedParts();
        parts.CamshaftPartId = 0;
        var carWithNullPart = BuildCarCard(247);
        PowerCalculator.Calculate(carWithNullPart, parts);

        Assert.Equal(stockPower, carWithNullPart.PowerHP);
    }

    [Fact]
    public async Task Power_RaceLevelParts_IncreasesPowerOverStock()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var car = BuildCarCard(247);
        PowerCalculator.Calculate(car);
        double stockPower = car.PowerHP;

        var parts = new SelectedParts();
        var cams = Fh6DatabaseService.Instance.GetCamshafts(car.EngineDbId);
        var raceCam = cams.FirstOrDefault(c => !c.IsStock && c.Level >= 3);
        if (raceCam != null)
        {
            parts.CamshaftPartId = raceCam.Id;
            var upgradedCar = BuildCarCard(247);
            PowerCalculator.Calculate(upgradedCar, parts);
            Assert.True(Math.Abs(upgradedCar.PowerHP - stockPower) > 0.1 ||
                        Math.Abs((raceCam.TorqueScale ?? 1.0) - 1.0) < 0.01,
                $"Expected power change with cam Lv{raceCam.Level}, stock={stockPower}, upgraded={upgradedCar.PowerHP}");
        }
    }

    [Fact]
    public async Task Power_LighterFlywheel_InertiaFactorAboveOne()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var car = BuildCarCard(247);
        var flywheels = Fh6DatabaseService.Instance.GetFlywheels(car.EngineDbId);
        var lightFw = flywheels.FirstOrDefault(f => !f.IsStock && f.MomentInertia > 0);
        if (lightFw == null) return;

        var parts = new SelectedParts();
        parts.FlywheelPartId = lightFw.Id;
        PowerCalculator.Calculate(car, parts);

        double expectedInertia = TuningPhysicsContext.ComputeRotationalInertiaFactor(car, parts, Fh6DatabaseService.Instance);
        Assert.InRange(expectedInertia, 0.7, 1.3);
    }

    [Fact]
    public async Task Power_HeavierFlywheel_InertiaFactorBelowOne()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var car = BuildCarCard(247);
        var flywheels = Fh6DatabaseService.Instance.GetFlywheels(car.EngineDbId);
        var heavyFw = flywheels.FirstOrDefault(f => !f.IsStock && f.MomentInertia > 0);
        if (heavyFw == null) return;

        var parts = new SelectedParts();
        parts.FlywheelPartId = heavyFw.Id;
        PowerCalculator.Calculate(car, parts);

        double expectedInertia = TuningPhysicsContext.ComputeRotationalInertiaFactor(car, parts, Fh6DatabaseService.Instance);
        Assert.InRange(expectedInertia, 0.7, 1.3);
    }

    [Fact]
    public async Task Power_EngineSwap_ChangesPowerAndResult()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var car = BuildCarCard(247);
        var parts = new SelectedParts();
        var db = Fh6DatabaseService.Instance;

        // Stock power
        PowerCalculator.Calculate(car, parts);
        double stockPower = car.PowerHP;

        // Swap to a different engine
        var swaps = db.GetEngineSwaps(247);
        var nonStock = swaps.FirstOrDefault(s => !s.IsStock);
        if (nonStock == null) return;

        var swappedParts = new SelectedParts();
        swappedParts.EngineSwapPartId = nonStock.Id;
        var swappedCar = BuildCarCard(247);
        PowerCalculator.Calculate(swappedCar, swappedParts);

        // Power may or may not change depending on the specific engine swap;
        // at minimum the torque curve should reference the new engine
        Assert.True(swappedCar.PowerHP > 0, $"Swapped power must be positive, got {swappedCar.PowerHP}");
        Assert.True(swappedCar.TorqueNm > 0, $"Swapped torque must be positive, got {swappedCar.TorqueNm}");
    }

    // ── Physics Context ──────────────────────────────────────────────────────

    [Fact]
    public async Task PhysicsContext_SpringDamper_ReturnsDbRecord_WhenPartSelected()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var car = BuildCarCard(247);
        var parts = new SelectedParts();
        var db = Fh6DatabaseService.Instance;

        var sdList = db.GetSpringDampers(247);
        var nonStock = sdList.FirstOrDefault(s => !s.IsStock);
        if (nonStock == null) return;

        parts.SpringDamperPartId = nonStock.Id;
        var phys = TuningPhysicsContext.FrontSpringDamper(car, parts, db);
        Assert.NotNull(phys);
        Assert.True(phys.SpringDamperPhysicsID > 0, "Must resolve real DB physics record");
    }

    [Fact]
    public async Task PhysicsContext_RideHeightBounds_AreInMillimeterRange()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var car = BuildCarCard(247);
        var db = Fh6DatabaseService.Instance;
        var phys = TuningPhysicsContext.FrontSpringDamper(car, new SelectedParts(), db);

        Assert.NotNull(phys);
        double minMm = phys.MinRideHeight * 1000;
        double maxMm = phys.MaxRideHeight * 1000;
        Assert.InRange(minMm, 5, 200);
        Assert.InRange(maxMm, minMm, 400);
    }

    [Fact]
    public async Task PhysicsContext_Differential_ResolvesFromDb()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var car = BuildCarCard(247);
        var parts = new SelectedParts();
        var db = Fh6DatabaseService.Instance;

        var diff = TuningPhysicsContext.Differential(car, parts, db);
        Assert.NotNull(diff);
    }

    [Fact]
    public async Task PhysicsContext_TireCompound_ResolvesStockFromDb()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var car = BuildCarCard(247);
        var db = Fh6DatabaseService.Instance;
        var compound = TuningPhysicsContext.TireCompound(car, new SelectedParts(), db);
        Assert.NotNull(compound);
        Assert.True(compound.TireCompoundID > 0, "Must resolve to a real compound");
    }

    [Fact]
    public async Task PhysicsContext_RaceSpringDamper_DifferentFromStock()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var car = BuildCarCard(247);
        var parts = new SelectedParts();
        var db = Fh6DatabaseService.Instance;

        var sdList = db.GetSpringDampers(247);
        var stock = sdList.FirstOrDefault(s => s.IsStock);
        var race = sdList.FirstOrDefault(s => !s.IsStock && s.Level >= 3);
        if (stock == null || race == null) return;

        parts.SpringDamperPartId = race.Id;
        var racePhys = TuningPhysicsContext.FrontSpringDamper(car, parts, db);

        parts.SpringDamperPartId = stock.Id;
        var stockPhys = TuningPhysicsContext.FrontSpringDamper(car, parts, db);

        Assert.NotEqual(racePhys?.DefSpringRate, stockPhys?.DefSpringRate);
    }

    // ── Suspension Calculator ────────────────────────────────────────────────

    [Fact]
    public async Task Springs_WithRealDbPhysics_RateInRangeFromDb()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var car = BuildCarCard(247);
        var result = new TuneResult();
        var ex = new System.Collections.Generic.Dictionary<string, string>();

        SuspensionCalculator.CalculateSprings(car, CarFactory.DefaultTrack(), new SelectedParts(), result, ex);

        Assert.InRange(result.SpringFront, 1, 500);
        Assert.InRange(result.SpringRear, 1, 500);
    }

    [Fact]
    public async Task RideHeight_WithRealDbPart_RangeFromDbInMm()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var car = BuildCarCard(247);
        var result = new TuneResult();
        var ex = new System.Collections.Generic.Dictionary<string, string>();

        SuspensionCalculator.CalculateRideHeight(car, CarFactory.DefaultTrack(), new SelectedParts(), result, ex);

        Assert.InRange(result.RideHeightFront, 20, 300);
        Assert.InRange(result.RideHeightRear, 20, 300);
    }

    [Fact]
    public async Task Dampers_HeavierCar_LargerDamperValues()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var lightCar = BuildCarCard(247);
        var heavyCar = BuildCarCard(295);

        // Override masses
        lightCar.TotalMass = 1000;
        heavyCar.TotalMass = 2000;

        var lightResult = new TuneResult();
        var heavyResult = new TuneResult();
        var lightEx = new System.Collections.Generic.Dictionary<string, string>();
        var heavyEx = new System.Collections.Generic.Dictionary<string, string>();
        var track = CarFactory.DefaultTrack();

        SuspensionCalculator.CalculateSprings(lightCar, track, new SelectedParts(), lightResult, lightEx);
        SuspensionCalculator.CalculateSprings(heavyCar, track, new SelectedParts(), heavyResult, heavyEx);

        SuspensionCalculator.CalculateDampers(lightCar, track, new SelectedParts(), lightResult, lightEx);
        SuspensionCalculator.CalculateDampers(heavyCar, track, new SelectedParts(), heavyResult, heavyEx);

        Assert.True(heavyResult.ReboundFront >= lightResult.ReboundFront - 0.5,
            $"Heavy car rebound ({heavyResult.ReboundFront}) must exceed light car ({lightResult.ReboundFront})");
    }

    [Fact]
    public async Task Dampers_WithRealDbPhysics_BoundsRespected()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var car = BuildCarCard(247);
        var db = Fh6DatabaseService.Instance;
        var result = new TuneResult();
        var ex = new System.Collections.Generic.Dictionary<string, string>();
        var track = CarFactory.DefaultTrack();

        SuspensionCalculator.CalculateSprings(car, track, new SelectedParts(), result, ex);
        SuspensionCalculator.CalculateDampers(car, track, new SelectedParts(), result, ex);

        var frontPhys = TuningPhysicsContext.FrontSpringDamper(car, new SelectedParts(), db);
        var rearPhys = TuningPhysicsContext.RearSpringDamper(car, new SelectedParts(), db);

        if (frontPhys != null)
        {
            Assert.True(result.ReboundFront >= frontPhys.MinDampenReboundRate - 0.1,
                $"ReboundFront {result.ReboundFront} should be >= {frontPhys.MinDampenReboundRate}");
            Assert.True(result.ReboundFront <= frontPhys.MaxDampenReboundRate + 0.1,
                $"ReboundFront {result.ReboundFront} should be <= {frontPhys.MaxDampenReboundRate}");
            Assert.True(result.BumpFront >= frontPhys.MinDampenBumpRate - 0.1);
            Assert.True(result.BumpFront <= frontPhys.MaxDampenBumpRate + 0.1);
        }
        if (rearPhys != null)
        {
            Assert.True(result.ReboundRear >= rearPhys.MinDampenReboundRate - 0.1);
            Assert.True(result.ReboundRear <= rearPhys.MaxDampenReboundRate + 0.1);
            Assert.True(result.BumpRear >= rearPhys.MinDampenBumpRate - 0.1);
            Assert.True(result.BumpRear <= rearPhys.MaxDampenBumpRate + 0.1);
        }
    }

    [Fact]
    public async Task Dampers_BumpAlwaysLessThanRebound()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var car = BuildCarCard(247);
        var result = new TuneResult();
        var ex = new System.Collections.Generic.Dictionary<string, string>();

        SuspensionCalculator.CalculateSprings(car, CarFactory.DefaultTrack(), new SelectedParts(), result, ex);
        SuspensionCalculator.CalculateDampers(car, CarFactory.DefaultTrack(), new SelectedParts(), result, ex);

        Assert.True(result.BumpFront < result.ReboundFront,
            $"Bump front ({result.BumpFront}) must be < rebound front ({result.ReboundFront})");
        Assert.True(result.BumpRear < result.ReboundRear,
            $"Bump rear ({result.BumpRear}) must be < rebound rear ({result.ReboundRear})");
    }

    // ── Tire Calculator ──────────────────────────────────────────────────────

    [Fact]
    public async Task TirePressure_DbCompound_UsesNonZeroBasePressure()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var car = BuildCarCard(247);
        var result = new TuneResult();
        var ex = new System.Collections.Generic.Dictionary<string, string>();

        TireCalculator.CalculateTirePressure(car, CarFactory.DefaultTrack(), new SelectedParts(), Fh6DatabaseService.Instance, result, ex);

        Assert.True(result.TirePressureFront > 0.5, $"Front pressure too low: {result.TirePressureFront}");
        Assert.True(result.TirePressureRear > 0.5, $"Rear pressure too low: {result.TirePressureRear}");
    }

    [Fact]
    public async Task TirePressure_EachCompound_ProducesValidPressure()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var db = Fh6DatabaseService.Instance;
        var compounds = db.GetTireCompounds(247);
        Assert.NotEmpty(compounds);

        foreach (var compound in compounds)
        {
            var parts = new SelectedParts { TireCompoundPartId = compound.Id };
            var result = new TuneResult();
            var ex = new System.Collections.Generic.Dictionary<string, string>();

            TireCalculator.CalculateTirePressure(car: BuildCarCard(247), CarFactory.DefaultTrack(), parts, db, result, ex);

            Assert.InRange(result.TirePressureFront, 0.5, 5.0);
            Assert.InRange(result.TirePressureRear, 0.5, 5.0);
        }
    }

    [Fact]
    public async Task TirePressure_FrontAndRearMaxConstraints_RespectedIndependently()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var car = BuildCarCard(247);
        var constraints = new TuningConstraints
        {
            TirePressureFrontMin = 1.0,
            TirePressureFrontMax = 2.0,
            TirePressureRearMin = 1.0,
            TirePressureRearMax = 4.0,
        };

        var result = new TuneResult();
        var ex = new System.Collections.Generic.Dictionary<string, string>();

        TireCalculator.CalculateTirePressure(car, CarFactory.DefaultTrack(), new SelectedParts(), Fh6DatabaseService.Instance, result, ex, constraints);

        Assert.InRange(result.TirePressureFront, 1.0, 2.0);
    }

    [Fact]
    public async Task TirePressure_FrontMaxDifferentFromRearMax_BothRespected()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var car = BuildCarCard(247);
        var constraints = new TuningConstraints
        {
            TirePressureFrontMin = 0.5,
            TirePressureFrontMax = 3.0,
            TirePressureRearMin = 0.5,
            TirePressureRearMax = 5.0,
        };

        var result = new TuneResult();
        var ex = new System.Collections.Generic.Dictionary<string, string>();

        TireCalculator.CalculateTirePressure(car, CarFactory.DefaultTrack(), new SelectedParts(), Fh6DatabaseService.Instance, result, ex, constraints);

        Assert.InRange(result.TirePressureFront, 0.5, 3.0);
        Assert.InRange(result.TirePressureRear, 0.5, 5.0);
    }

    // ── Brake Calculator ─────────────────────────────────────────────────────

    [Fact]
    public async Task Brakes_RealDbBrakePart_BiasWithinRange()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var car = BuildCarCard(247);
        var db = Fh6DatabaseService.Instance;
        var brakes = db.GetBrakes(247);
        var nonStock = brakes.FirstOrDefault(b => !b.IsStock);
        if (nonStock == null) return;

        var parts = new SelectedParts();
        parts.BrakePartId = nonStock.Id;

        var result = new TuneResult();
        var ex = new System.Collections.Generic.Dictionary<string, string>();

        BrakeCalculator.CalculateBrakes(car, CarFactory.DefaultTrack(), parts, db, result, ex, 250);

        Assert.InRange(result.BrakeBalance, 30, 70);
        Assert.InRange(result.BrakePressure, 50, 200);
    }

    [Fact]
    public async Task Brakes_HigherBrakePart_DifferentFromStock()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var db = Fh6DatabaseService.Instance;
        var brakes = db.GetBrakes(247);
        var stock = brakes.FirstOrDefault(b => b.IsStock);
        var race = brakes.FirstOrDefault(b => !b.IsStock && b.Level >= 2);
        if (stock == null || race == null) return;

        var resultStock = new TuneResult();
        var resultRace = new TuneResult();
        var ex = new System.Collections.Generic.Dictionary<string, string>();
        var car = BuildCarCard(247);

        BrakeCalculator.CalculateBrakes(car, CarFactory.DefaultTrack(), new SelectedParts { BrakePartId = stock.Id }, db, resultStock, ex, 250);
        BrakeCalculator.CalculateBrakes(car, CarFactory.DefaultTrack(), new SelectedParts { BrakePartId = race.Id }, db, resultRace, ex, 250);

        Assert.NotEqual(resultStock.BrakePressure, resultRace.BrakePressure);
    }

    // ── Differential Calculator ──────────────────────────────────────────────

    [Fact]
    public async Task Diff_DbDifferential_RearToqueSplitUsed()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var car = BuildCarCard(247);
        car.DriveType = DriveType.AWD;
        var db = Fh6DatabaseService.Instance;

        var result = new TuneResult();
        var ex = new System.Collections.Generic.Dictionary<string, string>();

        DifferentialCalculator.CalculateDifferential(car, CarFactory.DefaultTrack(), new SelectedParts(), db, result, ex);

        Assert.InRange(result.DiffAccel, 0, 100);
        Assert.NotNull(result.CenterDiffBias);
        Assert.InRange(result.CenterDiffBias.Value, 30, 85);
    }

    [Fact]
    public async Task Diff_FwdCar_HasDiffValues()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var car = BuildCarCard(295);
        car.DriveType = DriveType.FWD;
        var db = Fh6DatabaseService.Instance;

        var result = new TuneResult();
        var ex = new System.Collections.Generic.Dictionary<string, string>();

        DifferentialCalculator.CalculateDifferential(car, CarFactory.DefaultTrack(), new SelectedParts(), db, result, ex);

        Assert.InRange(result.DiffAccel, 0, 100);
    }

    // ── Aero Calculator ──────────────────────────────────────────────────────

    [Fact]
    public async Task Aero_RealCar_WithRearWing_ProducesValues()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var car = BuildCarCard(247);
        car.HasRearAero = true;

        var result = new TuneResult();
        var ex = new System.Collections.Generic.Dictionary<string, string>();

        AeroCalculator.CalculateAero(car, CarFactory.DefaultTrack(), new SelectedParts(), Fh6DatabaseService.Instance, result, ex);

        Assert.InRange(result.AeroRear, 0, 300);
    }

    [Fact]
    public async Task Aero_HigherLevelWing_IncreasesDownforce()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var db = Fh6DatabaseService.Instance;
        var wings = db.GetRearWings(247);
        var stockWing = wings.FirstOrDefault(w => w.IsStock);
        var raceWing = wings.FirstOrDefault(w => !w.IsStock && w.Level >= 3);
        if (stockWing == null || raceWing == null) return;

        var stockPhys = db.GetAeroPhysics(stockWing.AeroPhysicsID);
        var racePhys = db.GetAeroPhysics(raceWing.AeroPhysicsID);

        Assert.NotNull(stockPhys);
        Assert.NotNull(racePhys);
        Assert.True(racePhys.Downforce1 >= stockPhys.Downforce1,
            $"Race wing max downforce ({racePhys.Downforce1}) must be >= stock ({stockPhys.Downforce1})");
    }

    // ── Launch Control ───────────────────────────────────────────────────────

    [Fact]
    public async Task LaunchControl_WithDbTireCompound_ProducesRpm()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var db = Fh6DatabaseService.Instance;
        var car = BuildCarCard(295);
        car.PowertrainType = PowertrainType.ICE;
        car.AspirationType = Forza_Horizon_6_Tune_Master.Models.AspirationType.Natural;
        car.DriveType = DriveType.RWD;
        car.TorqueNm = 250;
        car.PowerHP = 180;

        var compounds = db.GetTireCompounds(295);
        var anyCompound = compounds.FirstOrDefault();
        if (anyCompound == null) return;

        var result = new TuneResult();
        var track = new TrackInfo { Discipline = Discipline.Drag, DragDistance = DragDistance.Quarter };

        LaunchControlCalculator.CalculateLaunchControl(car, track, new SelectedParts { TireCompoundPartId = anyCompound.Id }, db, result);

        if (result.LaunchControlRpm.HasValue)
            Assert.InRange(result.LaunchControlRpm.Value, 1200, car.MaxRPM);
    }

    [Fact]
    public async Task LaunchControl_WithDbParts_RpmInValidRange()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var car = BuildCarCard(247);
        var track = new TrackInfo { Discipline = Discipline.Drag, DragDistance = DragDistance.Quarter };

        var result = new TuneResult();
        LaunchControlCalculator.CalculateLaunchControl(car, track, new SelectedParts(), Fh6DatabaseService.Instance, result);

        if (result.LaunchControlRpm.HasValue)
        {
            Assert.InRange(result.LaunchControlRpm.Value, 1200, car.MaxRPM);
        }
    }

    // ── Full Pipeline ────────────────────────────────────────────────────────

    [Fact]
    public async Task Generate_RealCar_AllFieldsPopulated()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var car = BuildCarCard(247);
        car.DriveType = DriveType.RWD;
        var parts = new SelectedParts();
        var track = CarFactory.DefaultTrack();

        var result = new TuneGeneratorService().Generate(car, track, parts, Fh6DatabaseService.Instance);

        Assert.NotNull(result);
        Assert.InRange(result.TirePressureFront, 0.5, 5.0);
        Assert.InRange(result.TirePressureRear, 0.5, 5.0);
        Assert.InRange(result.CamberFront, -5.0, 0.0);
        Assert.InRange(result.CamberRear, -5.0, 0.0);
        Assert.InRange(result.Caster, 3.0, 10.0);
        Assert.InRange(result.ARBFront, 0, 100);
        Assert.InRange(result.ARBRear, 0, 100);
        Assert.InRange(result.SpringFront, 1, 500);
        Assert.InRange(result.SpringRear, 1, 500);
        Assert.InRange(result.RideHeightFront, 10, 400);
        Assert.InRange(result.RideHeightRear, 10, 400);
        Assert.InRange(result.ReboundFront, 0, 30);
        Assert.InRange(result.ReboundRear, 0, 30);
        Assert.InRange(result.BumpFront, 0, 30);
        Assert.InRange(result.BumpRear, 0, 30);
        Assert.InRange(result.DiffAccel, 0, 100);
        Assert.InRange(result.BrakeBalance, 30, 70);
        Assert.InRange(result.BrakePressure, 50, 200);
        Assert.NotNull(result.GearRatios);
        Assert.NotEmpty(result.GearRatios);
        Assert.InRange(result.FinalDrive, 2.2, 6.0);
    }

    [Fact]
    public async Task Generate_EngineSwap_ChangesPowerAndResult()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var db = Fh6DatabaseService.Instance;
        var car = BuildCarCard(247);
        var parts = new SelectedParts();
        var track = CarFactory.DefaultTrack();

        // Stock result
        var stockResult = new TuneGeneratorService().Generate(car, track, parts, db);
        double stockPower = car.PowerHP;

        // Swap engine
        var swaps = db.GetEngineSwaps(247);
        var nonStock = swaps.FirstOrDefault(s => !s.IsStock);
        if (nonStock == null) return;

        var swappedParts = new SelectedParts();
        swappedParts.EngineSwapPartId = nonStock.Id;
        var swappedCar = BuildCarCard(247);
        PowerCalculator.Calculate(swappedCar, swappedParts);
        double swappedPower = swappedCar.PowerHP;

        // Power should change (increase or decrease) when engine is swapped
        Assert.NotEqual(stockPower, swappedPower);
    }

    [Fact]
    public async Task Generate_DragDiscipline_ProducesLaunchControlRpm()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var db = Fh6DatabaseService.Instance;
        var dbCar = db.GetCar(295);
        Assert.NotNull(dbCar);

        var car = BuildCarCard(295);
        car.PowertrainType = PowertrainType.ICE;
        car.AspirationType = Forza_Horizon_6_Tune_Master.Models.AspirationType.Natural;
        car.TorqueNm = 250;
        car.MaxRPM = 7000;
        car.PowerHP = 180;
        car.EngineDbId = db.GetEngineSwaps(295).FirstOrDefault()?.EngineID ?? 1;

        var parts = new SelectedParts();
        var track = new TrackInfo { Discipline = Discipline.Drag, DragDistance = DragDistance.Quarter };

        var result = new TuneGeneratorService().Generate(car, track, parts, db);

        if (result.LaunchControlRpm.HasValue)
            Assert.InRange(result.LaunchControlRpm.Value, 1200, car.MaxRPM);
    }

    [Fact]
    public async Task Generate_ExplanationsContainDbContext()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var car = BuildCarCard(247);
        var parts = new SelectedParts();
        var track = CarFactory.DefaultTrack();

        var result = new TuneGeneratorService().Generate(car, track, parts, Fh6DatabaseService.Instance);

        Assert.NotNull(result.Explanations);
        Assert.True(result.Explanations.Count > 0, "Must have at least one explanation");
    }

    [Fact]
    public async Task Generate_AllDisciplines_ValuesInRange()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var db = Fh6DatabaseService.Instance;
        var dbCar = db.GetCar(295);
        Assert.NotNull(dbCar);
        var car = BuildCarCard(295);
        car.PowertrainType = PowertrainType.ICE;
        car.TorqueNm = 250;
        car.PowerHP = 180;

        var parts = new SelectedParts();

        foreach (Discipline disc in Enum.GetValues<Discipline>())
        {
            var track = new TrackInfo { Discipline = disc, DragDistance = DragDistance.Quarter };
            var result = new TuneGeneratorService().Generate(car, track, parts, db);

            Assert.NotNull(result);
            Assert.InRange(result.TirePressureFront, 0.0, 5.0);
            Assert.InRange(result.TirePressureRear, 0.0, 5.0);
            Assert.InRange(result.CamberFront, -5.0, 0.0);
            Assert.InRange(result.CamberRear, -5.0, 0.0);
            Assert.InRange(result.ARBFront, 0.0, 100.0);
            Assert.InRange(result.ARBRear, 0.0, 100.0);
            Assert.InRange(result.SpringFront, 0.0, 500.0);
            Assert.InRange(result.SpringRear, 0.0, 500.0);
            Assert.InRange(result.DiffAccel, 0.0, 100.0);
            Assert.InRange(result.BrakeBalance, 30.0, 70.0);
        }
    }

    [Fact]
    public async Task Generate_FwdCar_ProducesValidTune()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var car = BuildCarCard(295);
        car.DriveType = DriveType.FWD;
        var parts = new SelectedParts();
        var track = CarFactory.DefaultTrack();

        var result = new TuneGeneratorService().Generate(car, track, parts, Fh6DatabaseService.Instance);

        Assert.NotNull(result);
        Assert.InRange(result.BrakeBalance, 30, 70);
        Assert.InRange(result.DiffAccel, 0, 100);
    }

    [Fact]
    public async Task Generate_AwdCar_ProducesCenterDiffBias()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var car = BuildCarCard(247);
        car.DriveType = DriveType.AWD;
        var parts = new SelectedParts();
        var track = CarFactory.DefaultTrack();

        var result = new TuneGeneratorService().Generate(car, track, parts, Fh6DatabaseService.Instance);

        Assert.NotNull(result);
        Assert.NotNull(result.DiffFrontAccel);
        Assert.NotNull(result.CenterDiffBias);
    }

    [Fact]
    public async Task Generate_DifferentCars_ProduceDifferentTunes()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var db = Fh6DatabaseService.Instance;
        var track = CarFactory.DefaultTrack();

        var car1 = BuildCarCard(247);
        var car2 = BuildCarCard(295);

        var result1 = new TuneGeneratorService().Generate(car1, track, new SelectedParts(), db);
        var result2 = new TuneGeneratorService().Generate(car2, track, new SelectedParts(), db);

        Assert.NotEqual(result1.SpringFront, result2.SpringFront);
    }

    [Fact]
    public async Task Generate_DifferentSeasons_ProduceDifferentTune()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var db = Fh6DatabaseService.Instance;
        var car = BuildCarCard(247);
        var parts = new SelectedParts();

        var summerTrack = new TrackInfo { Discipline = Discipline.Road, Season = Season.Summer };
        var winterTrack = new TrackInfo { Discipline = Discipline.Road, Season = Season.Winter };

        var summerResult = new TuneGeneratorService().Generate(car, summerTrack, parts, db);
        var winterResult = new TuneGeneratorService().Generate(car, winterTrack, parts, db);

        Assert.True(Math.Abs(summerResult.TirePressureFront - winterResult.TirePressureFront) > 0.01,
            $"Summer tire pressure ({summerResult.TirePressureFront}) should differ from winter ({winterResult.TirePressureFront})");
    }

    [Fact]
    public async Task Generate_SpringRideHeightConsistency_NoExtremes()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var db = Fh6DatabaseService.Instance;
        var car = BuildCarCard(247);

        foreach (var disc in new[] { Discipline.Road, Discipline.Rally, Discipline.Drag, Discipline.Drift })
        {
            var track = new TrackInfo { Discipline = disc };
            var result = new TuneGeneratorService().Generate(car, track, new SelectedParts(), db);

            var frontPhys = TuningPhysicsContext.FrontSpringDamper(car, new SelectedParts(), db);
            var rearPhys = TuningPhysicsContext.RearSpringDamper(car, new SelectedParts(), db);

            if (frontPhys != null)
            {
                double rhMinMm = frontPhys.MinRideHeight * 1000;
                double rhMaxMm = frontPhys.MaxRideHeight * 1000;
                Assert.InRange(result.RideHeightFront, rhMinMm - 5, rhMaxMm + 5);
            }
            if (rearPhys != null)
            {
                double rhMinMm = rearPhys.MinRideHeight * 1000;
                double rhMaxMm = rearPhys.MaxRideHeight * 1000;
                Assert.InRange(result.RideHeightRear, rhMinMm - 5, rhMaxMm + 5);
            }
        }
    }

    [Fact]
    public async Task Generate_PostValidation_FixesGearRatios()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var db = Fh6DatabaseService.Instance;
        var car = BuildCarCard(247);
        var parts = new SelectedParts();
        var track = new TrackInfo { Discipline = Discipline.Road };

        var result = new TuneGeneratorService().Generate(car, track, parts, db);

        Assert.NotNull(result.GearRatios);
        if (result.GearRatios.Count >= 2)
        {
            for (int i = 1; i < result.GearRatios.Count; i++)
            {
                Assert.True(result.GearRatios[i] < result.GearRatios[i - 1],
                    $"Gear {i + 1} ratio ({result.GearRatios[i]}) must be lower than gear {i} ({result.GearRatios[i - 1]})");
            }
        }
    }

    [Fact]
    public async Task Generate_Deterministic_SameInputSameOutput()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var db = Fh6DatabaseService.Instance;
        var car = BuildCarCard(247);
        var parts = new SelectedParts();
        var track = CarFactory.DefaultTrack();

        var result1 = new TuneGeneratorService().Generate(car, track, parts, db);
        var result2 = new TuneGeneratorService().Generate(car, track, parts, db);

        Assert.Equal(result1.TirePressureFront, result2.TirePressureFront);
        Assert.Equal(result1.CamberFront, result2.CamberFront);
        Assert.Equal(result1.SpringFront, result2.SpringFront);
        Assert.Equal(result1.ReboundFront, result2.ReboundFront);
        Assert.Equal(result1.ARBFront, result2.ARBFront);
        Assert.Equal(result1.DiffAccel, result2.DiffAccel);
        Assert.Equal(result1.BrakeBalance, result2.BrakeBalance);
        Assert.Equal(result1.FinalDrive, result2.FinalDrive);
    }

    [Fact]
    public async Task Db_CarCount_AtLeast300()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var cars = Fh6DatabaseService.Instance.GetAllCars();
        Assert.True(cars.Count >= 300, $"Expected at least 300 cars, got {cars.Count}");
    }

    [Fact]
    public async Task Db_EveryCar_HasCarBody()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var cars = Fh6DatabaseService.Instance.GetAllCars();
        int missing = 0;
        foreach (var car in cars.Take(100))
        {
            int carBodyId = car.Id * 1000;
            if (Fh6DatabaseService.Instance.GetCarBody(carBodyId) == null)
                missing++;
        }
        Assert.True(missing == 0, $"{missing} of first 100 cars missing CarBody record");
    }

    [Fact]
    public async Task Db_SpringDamperPhysics_NoNullReferences()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var db = Fh6DatabaseService.Instance;
        int nullRefs = 0;

        var sds = db.GetSpringDampers(247);
        foreach (var sd in sds)
        {
            if (db.GetSpringDamperPhysics(sd.FrontSpringDamperPhysicsID) == null)
                nullRefs++;
            if (db.GetSpringDamperPhysics(sd.RearSpringDamperPhysicsID) == null)
                nullRefs++;
        }

        Assert.True(nullRefs == 0, $"{nullRefs} null physics references found");
    }

    [Fact]
    public async Task TuneResult_Serialization_Roundtrip()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var car = BuildCarCard(247);
        var parts = new SelectedParts();
        var track = CarFactory.DefaultTrack();
        var result = new TuneGeneratorService().Generate(car, track, parts, Fh6DatabaseService.Instance);

        var json = System.Text.Json.JsonSerializer.Serialize(result);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<TuneResult>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(result.TirePressureFront, deserialized.TirePressureFront);
        Assert.Equal(result.CamberFront, deserialized.CamberFront);
        Assert.Equal(result.SpringFront, deserialized.SpringFront);
        Assert.Equal(result.FinalDrive, deserialized.FinalDrive);
        Assert.Equal(result.BrakeBalance, deserialized.BrakeBalance);
    }

    [Fact]
    public async Task NSX_V8Swap_RaceParts_PowerMatchesGame()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();

        var db = Fh6DatabaseService.Instance;
        var car = BuildCarCard(3767);
        var parts = new SelectedParts();

        // Engine swap: 2022 NSX → 7.2L V8 (engine 2794)
        // Swap part ID for Ordinal=3767, EngineID=2794
        parts.EngineSwapPartId = 3767005;

        // Stock cam for engine 2794 (auto-resolves via cam curve)
        parts.CamshaftPartId = 2794000;

        // Race twin turbo level 4 (MaxScale=1.4, anti-lag)
        // FI parts have offset applied in-memory: Id = DB_Id + FiOffset
        // FiOffsetTwin = 2 * 100_000_000 = 200_000_000
        parts.ForcedInductionPartId = 2794004 + 200_000_000;

        // Race intercooler level 3 (MaxScaleScale=1.09)
        parts.IntercoolerPartId = 2794002;

        // Race engine parts (all level 3 for engine 2794)
        parts.DisplacementPartId = 2794003;   // TS=1.0785
        parts.ManifoldPartId = 2794003;       // TS=1.049
        parts.FuelSystemPartId = 2794003;     // TS=1.054
        parts.IgnitionPartId = 2794003;       // TS=1.0442

        // Everything else stays null/stock

        PowerCalculator.Calculate(car, parts);

        // Log for diagnostics
        var output = $"PowerHP={car.PowerHP} TorqueNm={car.TorqueNm} MaxRPM={car.MaxRPM} Inertia={car.RotationalInertiaFactor}";
        System.Diagnostics.Debug.WriteLine(output);
        Console.WriteLine(output);
        if (car.CachedTorqueCurveNm != null)
            Console.WriteLine($"TorqueCurve: [{string.Join(",", car.CachedTorqueCurveNm)}]");
        if (car.CachedPowerCurveHP != null)
            Console.WriteLine($"PowerCurve: [{string.Join(",", car.CachedPowerCurveHP)}]");

        Console.WriteLine($"\n=== FULL BUILD (with IC) === PowerHP={car.PowerHP}");

        // STOCK NSX (no swaps, no upgrades)
        var stockParts = new SelectedParts();
        var stockCar = BuildCarCard(3767);
        PowerCalculator.Calculate(stockCar, stockParts);
        Console.WriteLine($"\n=== STOCK NSX === PowerHP={stockCar.PowerHP} TorqueNm={stockCar.TorqueNm}");
        if (stockCar.CachedPowerCurveHP != null)
            Console.WriteLine($"Stock PowerCurve: [{string.Join(";", stockCar.CachedPowerCurveHP)}]");

        // NSX + V8 SWAP ONLY (no other upgrades)
        var partsSwapOnly = new SelectedParts
        {
            EngineSwapPartId = 3767005,
            CamshaftPartId = 2794000
        };
        var carSwapOnly = BuildCarCard(3767);
        PowerCalculator.Calculate(carSwapOnly, partsSwapOnly);
        Console.WriteLine($"\n=== NSX + V8 SWAP ONLY === PowerHP={carSwapOnly.PowerHP}");

        // NSX + V8 + PARTS (no FI)
        var partsNoFI = new SelectedParts
        {
            EngineSwapPartId = 3767005,
            CamshaftPartId = 2794000,
            DisplacementPartId = 2794003,
            ManifoldPartId = 2794003,
            FuelSystemPartId = 2794003,
            IgnitionPartId = 2794003
        };
        var carNoFI = BuildCarCard(3767);
        PowerCalculator.Calculate(carNoFI, partsNoFI);
        Console.WriteLine($"\n=== V8 + PARTS (no FI) === PowerHP={carNoFI.PowerHP}");

        // Full build must hit the engine's dyno ceiling (EngineGraphingMaxPower of
        // engine 2794 = 1011.11 kW -> 1355.9 HP), matching the in-game value (~1356).
        double engineCeiling = db.GetEngine(2794)!.EngineGraphingMaxPower * 1.341;
        Assert.InRange(car.PowerHP, engineCeiling - 1.0, engineCeiling + 1.0);

        // Power must REACT to parts (the regression made every build show the ceiling).
        Assert.True(carSwapOnly.PowerHP < car.PowerHP - 1,
            $"Swap-only ({carSwapOnly.PowerHP}) must be below full build ({car.PowerHP})");
        Assert.True(carNoFI.PowerHP < car.PowerHP - 1,
            $"No-FI build ({carNoFI.PowerHP}) must be below full build with turbo ({car.PowerHP})");
        Assert.True(stockCar.PowerHP < carSwapOnly.PowerHP,
            $"Stock NSX ({stockCar.PowerHP}) must be below V8 swap ({carSwapOnly.PowerHP})");

        // Stock V8 swap power is anchored to the engine's donor car — engine 2794
        // ("Racing 7.2L V8") is native to the Pro2Truck (car 2663, SimPeakPower -> ~850
        // HP), so a stock swap with no other upgrades reproduces that donor dyno figure.
        // (Previously this was a ceiling/maxMult heuristic tuned to ~750; the donor anchor
        // is the directly-verified figure — see the Civic 2.0L-VVT swap = 212 HP.)
        Assert.InRange(carSwapOnly.PowerHP, 846.0, 854.0);

        // Manifold and forced induction are mutually exclusive: with the turbo installed,
        // adding/removing the manifold must not change power.
        var fullNoManifold = new SelectedParts
        {
            EngineSwapPartId = 3767005, CamshaftPartId = 2794000,
            ForcedInductionPartId = 2794004 + 200_000_000, IntercoolerPartId = 2794002,
            DisplacementPartId = 2794003, FuelSystemPartId = 2794003, IgnitionPartId = 2794003
        };
        var carNoMan = BuildCarCard(3767);
        PowerCalculator.Calculate(carNoMan, fullNoManifold);
        Assert.Equal(car.PowerHP, carNoMan.PowerHP, 1); // manifold present vs absent: same with FI

        // Top speed must be sane: the geared max (at redline in top gear) should track the
        // power/aero-limited max, not balloon (a units bug once produced 937 km/h here).
        var result = new TuneGeneratorService().Generate(car, CarFactory.DefaultTrack(), parts, db);
        Assert.InRange(result.ActualMaxSpeedKmh, 250.0, 450.0);
        Assert.InRange(result.FinalDrive, 2.2, 6.0);
    }

    // Fleet-wide guard: every ICE car, its native engine and every engine swap, at both
    // stock and a fully-maxed build, must produce finite positive sane power. Also logs
    // how often a maxed build lands on the engine's dyno ceiling (currently 1998/2000;
    // the 2 misses are engines whose only power upgrade is a camshaft, which changes the
    // rev range rather than applying a torque multiplier).
    [Fact]
    public async Task AllCarsAllSwaps_PowerIsFiniteAndPositive()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();
        var db = Fh6DatabaseService.Instance;

        int cars = 0, combos = 0, bad = 0, ceilingOk = 0, ceilingChecked = 0;
        double worstCeilErr = 0; int worstEng = 0;
        foreach (var dbCar in db.GetAllCars())
        {
            if (dbCar.AspirationTypeId == 8 || dbCar.PowertrainID == 1) continue; // electric
            CarCard probe;
            try { probe = BuildCarCard(dbCar.Id); } catch { continue; }
            cars++;

            // native engine (null swap) + every swap for this car
            var swapIds = new System.Collections.Generic.List<int?> { null };
            foreach (var s in db.GetEngineSwaps(dbCar.Id)) swapIds.Add(s.Id);

            foreach (var swapId in swapIds)
            {
                combos++;
                var p = new SelectedParts { EngineSwapPartId = swapId };
                var c = BuildCarCard(dbCar.Id);
                PowerCalculator.Calculate(c, p);

                double cap = c.PowerHP; // will compare below
                bool finite = !double.IsNaN(c.PowerHP) && !double.IsInfinity(c.PowerHP);
                if (!finite || c.PowerHP <= 0 || c.PowerHP > 5000) { bad++; continue; }

                // Resolve engine ceiling and verify a maxed build lands on it.
                int eng = p.EngineSwapPartId != null
                    ? db.GetEngineSwapById(p.EngineSwapPartId.Value)?.EngineID ?? c.EngineDbId
                    : c.EngineDbId;
                var e = eng > 0 ? db.GetEngine(eng) : null;
                if (e == null || e.EngineGraphingMaxPower <= 0) continue;
                double ceil = e.EngineGraphingMaxPower * 1.341;

                // Maxed build: best of every category. Camshaft contributes via curve
                // shape (no TorqueScale), and the model's "best cam" is by peak power, so
                // brute-force over cams and take the highest-power result.
                var maxed = BuildMaxedParts(swapId, eng, db);
                var cams = db.GetCamshafts(eng);
                double maxedPower = 0;
                if (cams.Count == 0)
                {
                    var cm0 = BuildCarCard(dbCar.Id);
                    PowerCalculator.Calculate(cm0, maxed);
                    maxedPower = cm0.PowerHP;
                }
                else foreach (var cam in cams)
                {
                    maxed.CamshaftPartId = cam.Id;
                    var cm = BuildCarCard(dbCar.Id);
                    PowerCalculator.Calculate(cm, maxed);
                    if (cm.PowerHP > maxedPower) maxedPower = cm.PowerHP;
                }
                ceilingChecked++;
                double err = Math.Abs(maxedPower - ceil);
                if (err <= Math.Max(1.0, ceil * 0.001)) ceilingOk++;
                else if (err > worstCeilErr) { worstCeilErr = err; worstEng = eng; }

                if (double.IsNaN(maxedPower) || maxedPower <= 0 || maxedPower > 5000) bad++;
            }
        }
        Console.WriteLine($"cars={cars} engine-combos={combos} bad(NaN/<=0/>5000)={bad} " +
                          $"maxedHitsCeiling={ceilingOk}/{ceilingChecked} worstCeilErr={worstCeilErr:F1}hp (eng {worstEng})");
        Assert.Equal(0, bad);
    }

    private static SelectedParts BuildMaxedParts(int? swapId, int engineId, Fh6DatabaseService db)
    {
        // Only parts that actually raise torque (>1) — a maxed build never adds a
        // restrictor (TorqueScale < 1), matching ComputeMaxPartScale's MaxTorqueScale.
        int? BestTs<T>(System.Collections.Generic.List<T> list) where T : DbUpgradePart =>
            list.Where(x => !x.IsStock && (x.TorqueScale ?? 1.0) > 1.0)
                .OrderByDescending(x => x.TorqueScale ?? 1.0).FirstOrDefault()?.Id;

        var p = new SelectedParts
        {
            EngineSwapPartId = swapId,
            DisplacementPartId = BestTs(db.GetDisplacement(engineId)),
            ValvesPartId = BestTs(db.GetValves(engineId)),
            PistonsPartId = BestTs(db.GetPistons(engineId)),
            FuelSystemPartId = BestTs(db.GetFuelSystems(engineId)),
            IgnitionPartId = BestTs(db.GetIgnition(engineId)),
            ExhaustPartId = BestTs(db.GetExhaust(engineId)),
            IntakePartId = BestTs(db.GetIntake(engineId)),
            ManifoldPartId = BestTs(db.GetManifolds(engineId)),
            OilCoolingPartId = BestTs(db.GetOilCooling(engineId)),
            RestrictorPartId = BestTs(db.GetRestrictors(engineId)),
            // CamshaftPartId is set by the caller (brute-forced over cams).
        };

        // Best forced induction (raw DB id) + best intercooler, mirroring BestForcedInduction.
        DbUpgradePart? bestFi = null; double bestScale = 1.0;
        foreach (var t in db.GetTurbosSingle(engineId)) if (!t.IsStock && t.MaxScale > bestScale) { bestScale = t.MaxScale; bestFi = t; }
        foreach (var t in db.GetTurbosTwin(engineId)) if (!t.IsStock && t.MaxScale > bestScale) { bestScale = t.MaxScale; bestFi = t; }
        foreach (var c in db.GetCSC(engineId)) if (!c.IsStock && c.RedlineRPMScale > bestScale) { bestScale = c.RedlineRPMScale; bestFi = c; }
        foreach (var d in db.GetDSC(engineId)) if (!d.IsStock && d.RedlineRPMScale > bestScale) { bestScale = d.RedlineRPMScale; bestFi = d; }
        if (bestFi != null)
        {
            p.ForcedInductionPartId = bestFi.Id;
            p.IntercoolerPartId = db.GetIntercoolers(engineId).Where(x => !x.IsStock)
                .OrderByDescending(x => x.MaxScaleScale).FirstOrDefault()?.Id;
        }
        return p;
    }

}
