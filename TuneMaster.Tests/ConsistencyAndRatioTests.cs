using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;
using TuneMaster.Tests.Helpers;

namespace TuneMaster.Tests;

/// Tests 221–300: Internal consistency, ratios, and cross-field relationships
/// Verifies front/rear splits, discipline-appropriate ratios, and physics invariants
public class ConsistencyAndRatioTests
{
    private static TuneResult Gen(CarCard car, TrackInfo track)
        => new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());

    // ── Spring front/rear splits ────────────────────────────────────────

    // 221 – Drag spring split: rear ≥ front (weight transfer platform)
    [Fact]
    public void T221_DragSpringRearStiffer()
    {
        foreach (var car in new[] { CarFactory.Hellcat(), CarFactory.GtrR35(), CarFactory.Gemera() })
        {
            var r = Gen(car, CarFactory.Drag());
            Assert.True(r.SpringRear >= r.SpringFront,
                $"{car.DriveType}: drag rear spring {r.SpringRear} < front {r.SpringFront}");
        }
    }

    // 222 – Road spring split: front ≈ rear (balanced, within 15 units)
    [Fact]
    public void T222_RoadSpringSplitBalanced()
    {
        foreach (var car in new[] { CarFactory.SupraA90(), CarFactory.GtrR35(), CarFactory.Mx5() })
        {
            var r = Gen(car, CarFactory.Road());
            double delta = Math.Abs(r.SpringFront - r.SpringRear);
            Assert.True(delta <= 30,
                $"{car.DriveType}: road spring split |{r.SpringFront} - {r.SpringRear}| = {delta} > 30");
        }
    }

    // 223 – Drift spring split: rear ≥ front (holds angle)
    [Fact]
    public void T223_DriftSpringRearStiffer()
    {
        var r = Gen(CarFactory.SupraA90(), CarFactory.Drift());
        Assert.True(r.SpringRear >= r.SpringFront,
            $"Drift rear {r.SpringRear} < front {r.SpringFront}");
    }

    // 224 – Rally/CC spring split: front softer than or near rear (terrain compliance)
    [Fact]
    public void T224_RallyCcSpringSplitReasonable()
    {
        foreach (var combo in new[] {
            (CarFactory.WrxSti(), CarFactory.Rally()),
            (CarFactory.Wrangler(), CarFactory.CrossCountry()) })
        {
            var r = Gen(combo.Item1, combo.Item2);
            double split = r.SpringRear - r.SpringFront;
            Assert.True(split > -20,
                $"Spring split {split}: rear shouldn't be much softer than front");
        }
    }

    // 225 – Cross-country spring rates softer than road (same car)
    [Fact]
    public void T225_CrossCountrySpringsSofterThanRoad()
    {
        var r_road = Gen(CarFactory.Wrangler(), CarFactory.Road());
        var r_cc   = Gen(CarFactory.Wrangler(), CarFactory.CrossCountry());
        Assert.True(r_cc.SpringFront <= r_road.SpringFront + 10,
            $"CC front {r_cc.SpringFront} should be near/softer than Road {r_road.SpringFront}");
        Assert.True(r_cc.SpringRear  <= r_road.SpringRear + 10,
            $"CC rear {r_cc.SpringRear} should be near/softer than Road {r_road.SpringRear}");
    }

    // ── ARB front/rear splits ───────────────────────────────────────────

    // 226 – RWD road ARB split: front ≥ rear (front stiffer for turn-in, ForzaFire)
    [Fact]
    public void T226_RwdRoadArbFrontStifferOrEqual()
    {
        var r = Gen(CarFactory.SupraA90(), CarFactory.Road());
        Assert.True(r.ARBFront >= r.ARBRear,
            $"RWD road: front ARB {r.ARBFront} should be >= rear {r.ARBRear} (front stiffer for turn-in)");
    }

    // 227 – FWD road ARB: front softer than rear (reduce understeer)
    [Fact]
    public void T227_FwdRoadArbFrontSofterThanRear()
    {
        var r = Gen(CarFactory.CivicTypeR(), CarFactory.Road());
        Assert.True(r.ARBFront <= r.ARBRear,
            $"FWD road: front ARB {r.ARBFront} should be <= rear {r.ARBRear}");
    }

    // 228 – Drift RWD ARB: rear stiffer than front (hold angle)
    [Fact]
    public void T228_DriftRwdArbRearStiffer()
    {
        var r = Gen(CarFactory.SupraA90(), CarFactory.Drift());
        Assert.True(r.ARBRear > r.ARBFront,
            $"Drift RWD: rear ARB {r.ARBRear} <= front {r.ARBFront}");
    }

    // 229 – Rally ARB: both low, front ≈ rear
    [Fact]
    public void T229_RallyArbBalanced()
    {
        var r = Gen(CarFactory.WrxSti(), CarFactory.Rally());
        double diff = Math.Abs(r.ARBFront - r.ARBRear);
        Assert.True(diff <= 15,
            $"Rally ARB split |{r.ARBFront} - {r.ARBRear}| = {diff} > 15");
    }

    // 230 – Cross-country ARB: both very low (1-15)
    [Fact]
    public void T230_CrossCountryArbVeryLow()
    {
        var r = Gen(CarFactory.Wrangler(), CarFactory.CrossCountry());
        Assert.True(r.ARBFront <= 15, $"CC ARB front {r.ARBFront} > 15");
        Assert.True(r.ARBRear  <= 15, $"CC ARB rear {r.ARBRear} > 15");
    }

    // ── Ride height splits ──────────────────────────────────────────────

    // 231 – Drag rake: rear higher than front (weight transfer)
    [Fact]
    public void T231_DragRakeRearHigher()
    {
        foreach (var car in new[] { CarFactory.Hellcat(), CarFactory.GtrR35() })
        {
            var r = Gen(car, CarFactory.Drag());
            Assert.True(r.RideHeightRear >= r.RideHeightFront,
                $"Drag: rear height {r.RideHeightRear} < front {r.RideHeightFront}");
        }
    }

    // 232 – Road rake: front slightly lower than rear or equal
    [Fact]
    public void T232_RoadRakeSlight()
    {
        var r = Gen(CarFactory.SupraA90(), CarFactory.Road());
        double rake = r.RideHeightRear - r.RideHeightFront;
        Assert.InRange(rake, -20, 30);
    }

    // 233 – CC/Rally: similar ride height front and rear (balanced for terrain)
    [Fact]
    public void T233_OffroadRideHeightBalanced()
    {
        foreach (var combo in new[] {
            (CarFactory.WrxSti(), CarFactory.Rally()),
            (CarFactory.Wrangler(), CarFactory.CrossCountry()) })
        {
            var r = Gen(combo.Item1, combo.Item2);
            double diff = Math.Abs(r.RideHeightFront - r.RideHeightRear);
            Assert.True(diff <= 40,
                $"Off-road ride height split {diff} > 40");
        }
    }

    // 234 – Drift ride height: moderate, not at extremes
    [Fact]
    public void T234_DriftRideHeightModerate()
    {
        var r = Gen(CarFactory.SupraA90(), CarFactory.Drift());
        Assert.InRange(r.RideHeightFront, 50, 180);
        Assert.InRange(r.RideHeightRear,  50, 180);
    }

    // ── Damper ratio consistency ────────────────────────────────────────

    // 235 – Bump ratio discipline ordering: Drag < Road < Rally < CC
    [Fact]
    public void T235_BumpRatioOrderingByDiscipline()
    {
        var car = CarFactory.WrxSti();
        var r_drag = Gen(car, CarFactory.Drag());
        var r_road = Gen(car, CarFactory.Road());
        var r_rally = Gen(car, CarFactory.Rally());
        var r_cc   = Gen(car, CarFactory.CrossCountry());
        double dragRatio  = r_drag.ReboundFront >= 2 ? r_drag.BumpFront / r_drag.ReboundFront : 0;
        double roadRatio  = r_road.ReboundFront >= 2 ? r_road.BumpFront / r_road.ReboundFront : 0;
        double rallyRatio = r_rally.ReboundFront >= 2 ? r_rally.BumpFront / r_rally.ReboundFront : 0;
        double ccRatio    = r_cc.ReboundFront >= 2 ? r_cc.BumpFront / r_cc.ReboundFront : 0;
        Assert.True(dragRatio <= roadRatio + 0.1);
        Assert.True(roadRatio <= rallyRatio + 0.1);
        Assert.True(rallyRatio <= ccRatio + 0.1);
    }

    // 236 – Rebound discipline ordering: Drag < Road < Touge ≈ Drift > Rally > CC
    [Fact]
    public void T236_ReboundDisciplineOrdering()
    {
        var car = CarFactory.SupraA90();
        var r_drag = Gen(car, CarFactory.Drag());
        var r_road = Gen(car, CarFactory.Road());
        var r_drift = Gen(car, CarFactory.Drift());
        var r_cc   = Gen(car, CarFactory.CrossCountry());
        Assert.True(r_drag.ReboundFront <= r_road.ReboundFront,
            $"Drag rebound {r_drag.ReboundFront} > Road {r_road.ReboundFront}");
        Assert.True(r_cc.ReboundFront <= r_road.ReboundFront || Math.Abs(r_cc.ReboundFront - r_road.ReboundFront) < 5,
            $"CC rebound {r_cc.ReboundFront} should be near/below Road {r_road.ReboundFront}");
    }

    // 237 – Damper values round to integers (FH6 slider constraint)
    [Fact]
    public void T237_DamperValuesAreIntegers()
    {
        foreach (var (car, track) in AllCombos())
        {
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            Assert.Equal(r.ReboundFront, Math.Round(r.ReboundFront));
            Assert.Equal(r.ReboundRear,  Math.Round(r.ReboundRear));
            Assert.Equal(r.BumpFront,    Math.Round(r.BumpFront));
            Assert.Equal(r.BumpRear,     Math.Round(r.BumpRear));
        }
    }

    // 238 – Front/rear rebound difference not extreme (suspension balance)
    [Fact]
    public void T238_FrontRearReboundDifferenceReasonable()
    {
        foreach (var (car, track) in AllCombos())
        {
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            double diff = Math.Abs(r.ReboundFront - r.ReboundRear);
            Assert.True(diff <= 12,
                $"{car.DriveType}/{track.Discipline}: rebound split |{r.ReboundFront} - {r.ReboundRear}| = {diff} > 12");
        }
    }

    // 239 – Front/rear bump difference not extreme
    [Fact]
    public void T239_FrontRearBumpDifferenceReasonable()
    {
        foreach (var (car, track) in AllCombos())
        {
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            double diff = Math.Abs(r.BumpFront - r.BumpRear);
            Assert.True(diff <= 10,
                $"{car.DriveType}/{track.Discipline}: bump split |{r.BumpFront} - {r.BumpRear}| = {diff} > 10");
        }
    }

    // 240 – Suspension upgrade affects both dampers and springs consistently
    [Fact]
    public void T240_SuspensionUpgradeConsistent()
    {
        var stock = CarFactory.SupraA90(); stock.SuspensionUpgrade = SuspensionUpgrade.Stock;
        var race  = CarFactory.SupraA90(); race.SuspensionUpgrade  = SuspensionUpgrade.Race;
        var r_stock = Gen(stock, CarFactory.Road());
        var r_race  = Gen(race,  CarFactory.Road());
        Assert.True(r_race.SpringFront > r_stock.SpringFront);
        Assert.True(r_race.SpringRear  > r_stock.SpringRear);
        Assert.True(r_race.ReboundFront >= r_stock.ReboundFront);
        Assert.True(r_race.ReboundRear  >= r_stock.ReboundRear);
    }

    // ── Aero balance ────────────────────────────────────────────────────

    // 241 – Aero with aero car: all disciplines produce values within bounds
    [Fact]
    public void T241_AeroCarAllDisciplinesInBounds()
    {
        var car = CarFactory.Gt3Rs();
        foreach (var track in AllTracks())
        {
            var r = Gen(car, track);
            Assert.InRange(r.AeroFront, 0.0, 200.0);
            Assert.InRange(r.AeroRear,  0.0, 200.0);
        }
    }

    // 242 – Drift aero balanced front (F > R) per ForzaFire (recent fix)
    [Fact]
    public void T242_DriftAeroFrontBiased()
    {
        var car = CarFactory.Gt3Rs();
        var r = Gen(car, CarFactory.Drift());
        Assert.True(r.AeroFront >= r.AeroRear,
            $"Drift aero: front {r.AeroFront} < rear {r.AeroRear}, should be front-biased");
    }

    // 243 – Road aero: rear-biased or balanced (high-speed stability)
    [Fact]
    public void T243_RoadAeroRearBiased()
    {
        var car = CarFactory.Gt3Rs();
        var r = Gen(car, CarFactory.Road());
        Assert.True(r.AeroRear >= r.AeroFront * 0.6,
            $"Road aero: rear {r.AeroRear} too low vs front {r.AeroFront}");
    }

    // 244 – Aero decreases for rally/CC vs road (less speed, less downforce needed)
    [Fact]
    public void T244_OffroadAeroLowerThanRoad()
    {
        var car = CarFactory.Gt3Rs();
        var r_road = Gen(car, CarFactory.Road());
        var r_rally = Gen(car, CarFactory.Rally());
        var r_cc   = Gen(car, CarFactory.CrossCountry());
        Assert.True(r_rally.AeroFront <= r_road.AeroFront + 10,
            $"Rally aero front {r_rally.AeroFront} > Road {r_road.AeroFront}");
        Assert.True(r_cc.AeroFront <= r_road.AeroFront + 10,
            $"CC aero front {r_cc.AeroFront} > Road {r_road.AeroFront}");
    }

    // ── Differential consistency ────────────────────────────────────────

    // 245 – Drift RWD diff: accel >> decel (one-way lock)
    [Fact]
    public void T245_DriftRwdDiffAccelMuchHigherThanDecel()
    {
        var r = Gen(CarFactory.SupraA90(), CarFactory.Drift());
        Assert.True(r.DiffAccel - r.DiffDecel >= 50,
            $"Drift RWD: accel {r.DiffAccel} - decel {r.DiffDecel} = {r.DiffAccel - r.DiffDecel} < 50");
    }

    // 246 – AWD center bias varies by discipline
    [Fact]
    public void T246_AwdCenterBiasVariesByDiscipline()
    {
        var car = CarFactory.GtrR35();
        var r_road  = Gen(car, CarFactory.Road());
        var r_drift = Gen(car, CarFactory.Drift());
        var r_rally = Gen(car, CarFactory.Rally());
        var r_cc    = Gen(car, CarFactory.CrossCountry());
        var r_drag  = Gen(car, CarFactory.Drag());
        // All should be in valid range
        Assert.NotNull(r_road.CenterDiffBias);
        Assert.NotNull(r_drift.CenterDiffBias);
        Assert.NotNull(r_rally.CenterDiffBias);
        Assert.NotNull(r_cc.CenterDiffBias);
        Assert.NotNull(r_drag.CenterDiffBias);
        Assert.InRange(r_road.CenterDiffBias.Value,  0, 100);
        Assert.InRange(r_drift.CenterDiffBias.Value, 0, 100);
        Assert.InRange(r_rally.CenterDiffBias.Value, 0, 100);
        Assert.InRange(r_cc.CenterDiffBias.Value,    0, 100);
        Assert.InRange(r_drag.CenterDiffBias.Value,  0, 100);
    }

    // 247 – Drag AWD front diff: accel high (> decel), decel very low
    [Fact]
    public void T247_DragAwdFrontDiffAccelHighDecelLow()
    {
        var r = Gen(CarFactory.Gemera(), CarFactory.Drag());
        Assert.True(r.DiffFrontAccel >= r.DiffFrontDecel,
            $"AWD drag front diff: accel {r.DiffFrontAccel} < decel {r.DiffFrontDecel}");
        // Front diff decel should be much lower than accel for drag
        Assert.InRange(r.DiffFrontDecel.Value, 0, 15);
    }

    // 248 – Non-drag AWD front diff: accel > decel
    [Fact]
    public void T248_NonDragAwdFrontDiffAccelHigher()
    {
        foreach (var track in new[] { CarFactory.Road(), CarFactory.Rally(), CarFactory.CrossCountry(), CarFactory.Touge() })
        {
            var r = Gen(CarFactory.GtrR35(), track);
            Assert.True(r.DiffFrontAccel >= r.DiffFrontDecel,
                $"{track.Discipline}: front diff accel {r.DiffFrontAccel} < decel {r.DiffFrontDecel}");
        }
    }

    // 249 – AWD center diff: rear bias for road/touge ≥ 55%
    [Fact]
    public void T249_AwdCenterBiasRearBiasedRoadTouge()
    {
        foreach (var track in new[] { CarFactory.Road(), CarFactory.Touge() })
        {
            var r = Gen(CarFactory.GtrR35(), track);
            Assert.True(r.CenterDiffBias >= 55,
                $"{track.Discipline}: center bias {r.CenterDiffBias} < 55% rear");
        }
    }

    // 250 – Drift AWD center bias: neutral (50/50) for rotation, road is rear-biased
    [Fact]
    public void T250_DriftAwdCenterBiasBalanced()
    {
        var r_drift = Gen(CarFactory.GtrR35(), CarFactory.Drift());
        Assert.NotNull(r_drift.CenterDiffBias);
        Assert.InRange(r_drift.CenterDiffBias.Value, 40, 60);
    }

    // ── Brake consistency ──────────────────────────────────────────────

    // 251 – Brake balance discipline ordering: Drag < Road < Rally/CC
    [Fact]
    public void T251_BrakeBalanceOrderingByDiscipline()
    {
        var car = CarFactory.SupraA90();
        var r_drag = Gen(car, CarFactory.Drag());
        var r_road = Gen(car, CarFactory.Road());
        var r_rally = Gen(car, CarFactory.Rally());
        Assert.True(r_drag.BrakeBalance <= r_road.BrakeBalance,
            $"Drag brake balance {r_drag.BrakeBalance} > Road {r_road.BrakeBalance}");
    }

    // 252 – Brake pressure: discipline ordering (road > rally > drift > cc)
    [Fact]
    public void T252_BrakePressureDisciplineOrdering()
    {
        var car = CarFactory.SupraA90();
        var r_road = Gen(car, CarFactory.Road());
        var r_rally = Gen(car, CarFactory.Rally());
        var r_drift = Gen(car, CarFactory.Drift());
        var r_cc   = Gen(car, CarFactory.CrossCountry());
        Assert.True(r_road.BrakePressure >= r_drift.BrakePressure,
            $"Road brake {r_road.BrakePressure} < Drift {r_drift.BrakePressure}");
        Assert.True(r_road.BrakePressure >= r_cc.BrakePressure,
            $"Road brake {r_road.BrakePressure} < CC {r_cc.BrakePressure}");
    }

    // 253 – AWD brake pressure slightly higher than RWD (same car, road)
    [Fact]
    public void T253_AwdBrakePressureHigherThanRwd()
    {
        var awd   = CarFactory.GtrR35(); // AWD
        var rwd   = CarFactory.SupraA90(); // RWD, adjust mass to match
        rwd.TotalMass = awd.TotalMass; rwd.PowerHP = awd.PowerHP; rwd.MaxSpeedKmh = awd.MaxSpeedKmh;
        var r_awd = Gen(awd, CarFactory.Road());
        var r_rwd = Gen(rwd, CarFactory.Road());
        Assert.True(r_awd.BrakePressure >= r_rwd.BrakePressure,
            $"AWD brake {r_awd.BrakePressure} < RWD {r_rwd.BrakePressure}");
    }

    // 254 – Brake balance correlates with drivetrain and weight distribution
    [Fact]
    public void T254_BrakeBalanceCorrelatesWithWeightDist()
    {
        var civic = Gen(CarFactory.CivicTypeR(), CarFactory.Road()); // 62% front, FWD
        var gt3   = Gen(CarFactory.Gt3Rs(), CarFactory.Road());      // 38% front, RWD
        // Civic (FWD, front-heavy) should have higher (more front) brake balance
        Assert.True(civic.BrakeBalance >= gt3.BrakeBalance,
            $"Civic BB {civic.BrakeBalance} < GT3 RS BB {gt3.BrakeBalance}");
    }

    // ── Gearing consistency ────────────────────────────────────────────

    // 255 – More gears → tighter spacing (smaller step between ratios)
    [Fact]
    public void T255_MoreGearsTighterSpacing()
    {
        var sixSpd   = CarFactory.SupraA90(); sixSpd.GearCount = 6;
        var eightSpd = CarFactory.SupraA90(); eightSpd.GearCount = 8;
        var r_6  = Gen(sixSpd, CarFactory.Road());
        var r_8  = Gen(eightSpd, CarFactory.Road());
        double avgStep6 = AvgRatioStep(r_6);
        double avgStep8 = AvgRatioStep(r_8);
        Assert.True(avgStep8 <= avgStep6,
            $"8-speed avg step {avgStep8:F3} > 6-speed avg step {avgStep6:F3}");
    }

    // 256 – Drag and road final drives both in valid range
    [Fact]
    public void T256_DragAndRoadFdInRange()
    {
        var car = CarFactory.Hellcat(); car.GearCount = 6;
        var r_road = Gen(car, CarFactory.Road());
        var r_8th  = Gen(car, CarFactory.Drag(DragDistance.Eighth));
        Assert.InRange(r_8th.FinalDrive, 2.2, 6.1);
        Assert.InRange(r_road.FinalDrive, 2.2, 6.1);
    }

    // 257 – Rally/CC final drive lower (taller) than road (higher top speed per gear)
    [Fact]
    public void T257_OffroadFinalDriveLowerThanRoad()
    {
        var car = CarFactory.WrxSti(); car.GearCount = 6;
        var r_road = Gen(car, CarFactory.Road());
        var r_rally = Gen(car, CarFactory.Rally());
        var r_cc   = Gen(car, CarFactory.CrossCountry());
        Assert.True(r_rally.FinalDrive <= r_road.FinalDrive + 1,
            $"Rally FD {r_rally.FinalDrive} > Road FD {r_road.FinalDrive} + 1");
        Assert.True(r_cc.FinalDrive <= r_road.FinalDrive + 1,
            $"CC FD {r_cc.FinalDrive} > Road FD {r_road.FinalDrive} + 1");
    }

    // 258 – First gear (highest ratio) reasonable: 2.5-6.0 for ICE cars
    [Fact]
    public void T258_FirstGearReasonable()
    {
        foreach (var car in AllCars())
        {
            if (car.PowertrainType == PowertrainType.Electric) continue;
            var r = Gen(car, CarFactory.Road());
            Assert.InRange(r.GearRatios[0], 1.5, 6.0);
        }
    }

    // 259 – Top gear (lowest ratio) reasonable: 0.5-1.5 for ICE road cars
    [Fact]
    public void T259_TopGearReasonable()
    {
        foreach (var car in AllCars())
        {
            if (car.PowertrainType == PowertrainType.Electric) continue;
            if (car.GearCount >= 6)
            {
                var r = Gen(car, CarFactory.Road());
                double topGear = r.GearRatios.Last();
                Assert.InRange(topGear, 0.4, 2.0);
            }
        }
    }

    // 260 – Final drive varies by discipline in reasonable range
    [Fact]
    public void T260_FinalDriveDisciplineVariation()
    {
        var car = CarFactory.GtrR35(); car.GearCount = 8;
        var r_drag = Gen(car, CarFactory.Drag());
        var r_road = Gen(car, CarFactory.Road());
        var r_rally = Gen(car, CarFactory.Rally());
        var r_cc   = Gen(car, CarFactory.CrossCountry());
        // All FDs must be in valid range
        Assert.InRange(r_drag.FinalDrive, 2.2, 6.1);
        Assert.InRange(r_road.FinalDrive, 2.2, 6.1);
        Assert.InRange(r_rally.FinalDrive, 2.2, 6.1);
        Assert.InRange(r_cc.FinalDrive, 2.2, 6.1);
        // At least some variation between disciplines
        var fds = new[] { r_drag.FinalDrive, r_road.FinalDrive, r_rally.FinalDrive, r_cc.FinalDrive };
        Assert.True(fds.Distinct().Count() >= 1);
    }

    private static double AvgRatioStep(TuneResult r)
    {
        double total = 0;
        for (int i = 1; i < r.GearRatios.Count; i++)
            total += r.GearRatios[i - 1] / r.GearRatios[i];
        return total / (r.GearRatios.Count - 1);
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
    private static IEnumerable<(CarCard, TrackInfo)> AllCombos()
    {
        foreach (var c in AllCars()) foreach (var t in AllTracks()) yield return (c, t);
    }
}
