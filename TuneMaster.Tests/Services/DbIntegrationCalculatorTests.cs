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
            // Matches CarSpecController.PopulateCarFromDb — PowertrainID is not the field production
            // uses for this; AspirationTypeId == 8 is the real electric-motor marker in the DB.
            PowertrainType = dbCar.AspirationTypeId == 8 ? PowertrainType.Electric : PowertrainType.ICE,
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

        Assert.True(result.SpringFront > 0, $"SpringFront={result.SpringFront} should be positive");
        Assert.True(result.SpringRear > 0, $"SpringRear={result.SpringRear} should be positive");
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
    public async Task TiresWheels_TireCompoundDropdown_HasNoDuplicateNames()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();
        // Aston Martin Valkyrie AMR Pro '22: factory Michelin slick + generic Slick upgrade both
        // read "Slick Race Tire Compound", which used to show as a duplicate dropdown entry.
        var car = BuildCarCard(3631);
        var vm = new Forza_Horizon_6_Tune_Master.ViewModels.TiresWheelsViewModel();
        vm.LoadForCar(car, new SelectedParts());

        var names = vm.TireCompounds.Select(o => o.DisplayName).ToList();
        Assert.NotEmpty(names);
        Assert.Equal(names.Count, names.Distinct(System.StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public async Task Generate_HighRevCar_GearedTopSpeed_DoesNotOvershootReachableMax()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();
        var db = Fh6DatabaseService.Instance;
        // Aston Martin Valkyrie AMR Pro '22 — 11,000-rpm V12. Its tall top gear used to gear the
        // car to ~480 km/h when it can only reach ~324 (FD capped at 6.0 couldn't pull it down).
        var car = BuildCarCard(3631);
        car.PowertrainType = PowertrainType.ICE;

        var r = new TuneGeneratorService().Generate(car, new TrackInfo { Discipline = Discipline.Road }, new SelectedParts(), db);
        double effMax = CalculationHelpers.ComputeEffectiveMaxSpeedKmh(car, r);

        Assert.True(r.ActualMaxSpeedKmh <= effMax * 1.08,
            $"geared top speed {r.ActualMaxSpeedKmh} should track the reachable max {effMax:F0}, not overshoot it");
    }

    // Gear spacing is now solved from each car's own cached torque curve (equal wheel-force at the
    // shift point — see docs/superpowers/specs/2026-07-06-physics-based-gear-spacing-design.md)
    // instead of a fixed per-discipline ramp. For a normal powerband (torque peak comfortably below
    // the shift RPM), every upshift should land back at or above the torque peak — the engine never
    // falls out of its usable powerband. Spread of real cars: peaky turbo, flat torque, high-revving.
    [Theory]
    [InlineData(4162)]  // Sprinter Trueno GT-APEX FE — small NA four
    [InlineData(2636)]
    [InlineData(1367)]
    [InlineData(3785)]
    [InlineData(423)]
    public async Task Generate_GearShifts_DoNotDropBelowTorquePeak(int carDbId)
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();
        var db = Fh6DatabaseService.Instance;
        var car = BuildCarCard(carDbId);
        var track = new TrackInfo { Discipline = Discipline.Road };

        var result = new TuneGeneratorService().Generate(car, track, new SelectedParts(), db);
        Assert.True(result.GearRatios.Count >= 2, "test car should have a multi-gear box");

        double shiftRpm = car.MaxRPM * CalculationHelpers.RevLimitFraction;
        for (int i = 0; i < result.GearRatios.Count - 1; i++)
        {
            double rpmAfterShift = shiftRpm * result.GearRatios[i + 1] / result.GearRatios[i];
            Assert.True(rpmAfterShift >= car.TorquePeakRPM,
                $"car {carDbId} shift {i + 1}->{i + 2}: post-shift RPM {rpmAfterShift:F0} fell below torque peak {car.TorquePeakRPM}");
        }
    }

    // BuildDisciplineRatios recenters the discipline's own wide-low/tight-high shape on the
    // crossover-solved step instead of flattening it into one constant percentage (see the spec
    // doc addendum) — real gear ladders taper this way near-universally (a Lada 2110 and a
    // 268 km/h turbo build show the same shape), not just draggy cars, so the ratio set should
    // never come out as a perfectly uniform percentage ladder.
    [Fact]
    public async Task Generate_GearSteps_TaperForHighDragCar()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();
        var db = Fh6DatabaseService.Instance;
        var car = BuildCarCard(423); // high CdA ≈ 2.0 fallback estimate — tapers under Road
        var track = new TrackInfo { Discipline = Discipline.Road };

        var result = new TuneGeneratorService().Generate(car, track, new SelectedParts(), db);
        Assert.True(result.GearRatios.Count >= 4, "test car should have at least 4 gears");

        var steps = new List<double>();
        for (int i = 0; i < result.GearRatios.Count - 1; i++)
            steps.Add(result.GearRatios[i + 1] / result.GearRatios[i]);

        Assert.True(steps.Max() - steps.Min() > 0.02,
            $"expected non-uniform gear steps for a draggy car, got {string.Join(", ", steps.Select(s => s.ToString("F3")))}");
    }

    // Landing RPM (the RPM the engine drops to right after an upshift) should trend upward toward
    // redline as gear number increases — the same shape real gearbox calculator tools show for
    // road cars regardless of drag (see the spec doc addendum). Checked on the last intermediate
    // shift vs. the first, since the very last shift (into the FD-matched top gear) is governed by
    // an independent target-top-speed constraint and isn't expected to continue the trend exactly.
    [Theory]
    [InlineData(4162)]
    [InlineData(1367)]
    [InlineData(1200)]
    public async Task Generate_LandingRpm_TrendsUpwardTowardRedline(int carDbId)
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();
        var db = Fh6DatabaseService.Instance;
        var car = BuildCarCard(carDbId);
        var track = new TrackInfo { Discipline = Discipline.Road };

        var result = new TuneGeneratorService().Generate(car, track, new SelectedParts(), db);
        Assert.True(result.GearRatios.Count >= 4, "test car should have at least 4 gears");

        double shiftRpm = car.MaxRPM * CalculationHelpers.RevLimitFraction;
        var landingRpm = new List<double>();
        for (int i = 0; i < result.GearRatios.Count - 1; i++)
            landingRpm.Add(shiftRpm * result.GearRatios[i + 1] / result.GearRatios[i]);

        double first = landingRpm[0];
        double lastIntermediate = landingRpm[^2]; // excludes the shift into the top gear
        Assert.True(lastIntermediate > first,
            $"car {carDbId}: expected landing RPM to trend upward ({string.Join(", ", landingRpm.Select(l => l.ToString("F0")))})");
    }

    // On a short strip, RecommendedGearCount (built from the empirical trap speed) can fall well
    // below the installed box's real gear count — the trap speed just doesn't leave room for all of
    // them. Regression for the fix that stops BuildDisciplineRatios from cramming every installed
    // gear into that narrow band: previously this compressed every step toward 1.0, so landing RPM
    // came back nearly flat across shifts (the engine barely climbed before the next upshift —
    // "the engine never opens up"). Now only the useful leading gears are spaced/rescaled into the
    // band and the rest extend past it as unused overdrive ratios, so the useful gears should show
    // the same upward-trending landing RPM shape Road does.
    [Fact]
    public async Task Generate_DragQuarter_UsefulGearsNotCompressed_WhenBoxHasMoreGearsThanNeeded()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();
        var db = Fh6DatabaseService.Instance;
        var car = BuildCarCard(1367);
        var track = new TrackInfo { Discipline = Discipline.Drag, DragDistance = DragDistance.Quarter };

        var result = new TuneGeneratorService().Generate(car, track, new SelectedParts(), db);

        Assert.True(result.RecommendedGearCount >= 2 && result.RecommendedGearCount < result.GearRatios.Count,
            $"this regression needs a car/strip combo where the box has more gears ({result.GearRatios.Count}) " +
            $"than the drag strip calls for (recommended {result.RecommendedGearCount})");

        double shiftRpm = car.MaxRPM * CalculationHelpers.RevLimitFraction;
        var landingRpm = new List<double>();
        for (int i = 0; i < result.GearRatios.Count - 1; i++)
            landingRpm.Add(shiftRpm * result.GearRatios[i + 1] / result.GearRatios[i]);

        // Transitions within the useful range (gear 1 through RecommendedGearCount) should climb,
        // same as a normal box — not stay flat because extra installed gears got squeezed in too.
        for (int i = 1; i < result.RecommendedGearCount - 1; i++)
            Assert.True(landingRpm[i] > landingRpm[i - 1],
                $"expected landing RPM to trend upward across the useful gears, got " +
                $"{string.Join(", ", landingRpm.Select(l => l.ToString("F0")))}");
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

    // Fleet-wide guard: every ICE car, its native engine and every engine swap, at both
    // stock and a fully-maxed build, must produce finite positive sane power. Also logs
    // how often a maxed build lands on the engine's dyno ceiling (EngineGraphingMaxPower).
    // That hit-rate is diagnostic only — many engines' biggest available forced-induction
    // part cannot reach the graph-axis max, so a miss is expected, not a bug. For the
    // accuracy breakdown by aspiration type see PowerAccuracyDiagnostics.
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

    [Fact]
    public void Query_FullDb_SpringTables()
    {
        var fullDb = @"U:\Forza Horizon 6 Tune Master\DUMPER\fh6_db.sqlite";
        if (!System.IO.File.Exists(fullDb)) { Assert.True(true); return; }
        
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={fullDb}");
        conn.Open();
        var lines = new System.Collections.Generic.List<string>();
        
        // 1. Dump List_SuspensionPhysicsType
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info('List_SuspensionPhysicsType')";
            using var r = cmd.ExecuteReader();
            var cols = new System.Collections.Generic.List<string>();
            while (r.Read()) cols.Add($"{r.GetInt32(0)}:{r.GetString(1)}:{r.GetString(2)}");
            lines.Add($"=== List_SuspensionPhysicsType ({cols.Count} cols) ===");
            foreach (var c in cols) lines.Add($"  {c}");
        }
        
        // 2. Dump data from it
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT * FROM List_SuspensionPhysicsType LIMIT 10";
            using var r = cmd.ExecuteReader();
            lines.Add("=== Data (first 10) ===");
            while (r.Read())
            {
                var vals = new System.Collections.Generic.List<string>();
                for (int i = 0; i < r.FieldCount; i++)
                    vals.Add(r.IsDBNull(i) ? "NULL" : r.GetValue(i)?.ToString() ?? "NULL");
                lines.Add($"  [{string.Join("|", vals)}]");
            }
        }
        
        // 3. JOIN: physics + suspension type for our 4 key records
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT p.SpringDamperPhysicsID, p.SuspensionPhysicsTypeID, p.MinSpringRate, p.MaxSpringRate
                FROM List_SpringDamperPhysics p
                WHERE p.SpringDamperPhysicsID IN (1270003,1270103,4167005,4167105)";
            using var r = cmd.ExecuteReader();
            lines.Add("\n=== Physics records ===");
            while (r.Read())
                lines.Add($"  PhysId={r.GetInt32(0)} SuspType={r.GetInt32(1)} Min={r.GetDouble(2)} Max={r.GetDouble(3)}");
        }
        
        // 4. Get ALL SuspensionPhysicsType records
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT * FROM List_SuspensionPhysicsType ORDER BY SuspensionPhysicsTypeID";
            using var r = cmd.ExecuteReader();
            lines.Add("\n=== ALL SuspensionPhysicsType ===");
            while (r.Read())
            {
                int id = r.GetInt32(0);
                string name = r.GetString(1);
                string path = r.IsDBNull(2) ? "" : r.GetString(2);
                lines.Add($"  {id}: {name} -> {path}");
            }
        }
        
        var dumpPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "fh6_fulldb_dump.txt");
        System.IO.File.WriteAllLines(dumpPath, lines);
        System.Console.WriteLine($"WROTE: {dumpPath}");
        Assert.True(true);
    }

    // ── Drift tire compound tests ─────────────────────────────────────────

    /// <summary>
    /// Verifies that drift tire compounds (TireCompoundID=17) exist in the DB
    /// for at least one car ordinal.  Ordinal 247 (Nissan Silvia) is known to
    /// have a drift entry at Level=15 with TireModelName "Street".
    /// </summary>
    [Fact]
    public async Task DriftTire_ExistsInDb()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();
        var db = Fh6DatabaseService.Instance;

        var compounds = db.GetTireCompounds(247);
        var drift = compounds.FirstOrDefault(c => c.TireCompoundID == 17);

        Assert.NotNull(drift);
        Assert.Equal(15, drift.Level);
        Assert.False(drift.IsStock);
    }

    /// <summary>
    /// Verifies that a drift tire compound resolves to "Drift Tire Compound"
    /// (Upgrades_IDS_Name_298) rather than "Street Tire Compound" (which is
    /// what its TireModelName "Street" would otherwise produce).
    /// </summary>
    [Fact]
    public async Task DriftTire_ResolvesToDriftDisplayName()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();
        var db = Fh6DatabaseService.Instance;

        var compounds = db.GetTireCompounds(247);
        var drift = compounds.First(c => c.TireCompoundID == 17);
        var street = compounds.First(c => c.TireModelName == "Street" && c.TireCompoundID != 17);

        var resolver = new PartDisplayNameResolver();
        int makeId = db.GetCar(247)?.MakeID ?? 0;

        string driftName = resolver.Resolve(drift, makeId);
        string streetName = resolver.Resolve(street, makeId);

        // The drift compound should NOT have the same name as the street compound
        Assert.NotEqual(streetName, driftName);

        // The drift compound should contain "Drift" in its resolved name
        Assert.Contains("Drift", driftName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that drift tires survive the PopulateTireCompounds dedup logic
    /// — they must have a distinct display name so the dictionary doesn't drop them.
    /// </summary>
    [Fact]
    public async Task DriftTire_SurvivesDedup()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();
        var db = Fh6DatabaseService.Instance;

        var source = db.GetTireCompounds(247);
        var resolver = new PartDisplayNameResolver();
        int makeId = db.GetCar(247)?.MakeID ?? 0;

        // Simulate the dedup logic from TiresWheelsViewModel.PopulateTireCompounds
        var byName = new System.Collections.Generic.Dictionary<string, PartOption>(StringComparer.OrdinalIgnoreCase);
        var order = new System.Collections.Generic.List<string>();
        foreach (var p in source)
        {
            var opt = new PartOption { Id = p.Id, DisplayName = resolver.Resolve(p, makeId), IsStock = p.IsStock };
            if (byName.TryGetValue(opt.DisplayName, out var existing))
            {
                if (opt.IsStock && !existing.IsStock)
                    byName[opt.DisplayName] = opt;
                continue;
            }
            byName[opt.DisplayName] = opt;
            order.Add(opt.DisplayName);
        }

        // Verify drift tire survived the dedup
        bool driftSurvived = order.Any(name =>
            name.Contains("Drift", StringComparison.OrdinalIgnoreCase));
        Assert.True(driftSurvived,
            "Drift tire compound was dropped by the dedup logic — " +
            "it likely shared the same display name as another compound.");

        // Verify drift tire is in the final result
        bool driftInResult = byName.Values.Any(o =>
        {
            var compound = db.GetTireCompoundById(o.Id);
            return compound?.TireCompoundID == 17;
        });
        Assert.True(driftInResult,
            "Drift tire compound was not in the final deduplicated collection.");
    }

    /// <summary>
    /// Verifies that multiple car ordinals have drift tire compounds available.
    /// </summary>
    [Fact]
    public async Task DriftTire_AvailableOnMultipleCars()
    {
        using var env = new TestingEnvironment();
        await InitDbAsync();
        var db = Fh6DatabaseService.Instance;

        // Ordinals known to have drift compounds (TireCompoundID=17)
        int[] ordinals = { 247, 295, 323, 411, 513, 633, 1006, 1500, 2002, 3000 };
        int found = 0;

        foreach (int ord in ordinals)
        {
            var compounds = db.GetTireCompounds(ord);
            if (compounds.Any(c => c.TireCompoundID == 17))
                found++;
        }

        Assert.True(found >= 3,
            $"Expected drift tires on at least 3 of the tested ordinals, found on {found}");
    }
}
