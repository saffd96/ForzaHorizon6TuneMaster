using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;
using TuneMaster.Tests.Helpers;

namespace TuneMaster.Tests;

/// Tests 161–220: Comprehensive constraint and combinatorial coverage
/// Verifies every output field stays within bounds for all car×track combos
public class ComprehensiveConstraintTests
{
    // 161 – All output values within constraints for every combination
    [Fact]
    public void T161_AllValuesWithinConstraints_AllCombos()
    {
        var c = CarFactory.DefaultConstraints();
        foreach (var (car, track) in AllCombos())
        {
            var r = new TuneGeneratorService().Generate(car, track, c);
            Assert.InRange(r.TirePressureFront, c.TirePressureFrontMin, c.TirePressureFrontMax);
            Assert.InRange(r.TirePressureRear,  c.TirePressureRearMin,  c.TirePressureRearMax);
            Assert.InRange(r.CamberFront, c.CamberFrontMin, c.CamberFrontMax);
            Assert.InRange(r.CamberRear,  c.CamberRearMin,  c.CamberRearMax);
            Assert.InRange(r.ToeFront, c.ToeFrontMin, c.ToeFrontMax);
            Assert.InRange(r.ToeRear,  c.ToeRearMin,  c.ToeRearMax);
            Assert.InRange(r.Caster, c.CasterMin, c.CasterMax);
            Assert.InRange(r.ARBFront, c.ARBFrontMin, c.ARBFrontMax);
            Assert.InRange(r.ARBRear,  c.ARBRearMin,  c.ARBRearMax);
            Assert.InRange(r.SpringFront, c.SpringFrontMin, c.SpringFrontMax);
            Assert.InRange(r.SpringRear,  c.SpringRearMin,  c.SpringRearMax);
            Assert.InRange(r.RideHeightFront, c.RideHeightFrontMin, c.RideHeightFrontMax);
            Assert.InRange(r.RideHeightRear,  c.RideHeightRearMin,  c.RideHeightRearMax);
            Assert.InRange(r.ReboundFront, c.ReboundFrontMin, c.ReboundFrontMax);
            Assert.InRange(r.ReboundRear,  c.ReboundRearMin,  c.ReboundRearMax);
            Assert.InRange(r.BumpFront, c.BumpFrontMin, c.BumpFrontMax);
            Assert.InRange(r.BumpRear,  c.BumpRearMin,  c.BumpRearMax);
            Assert.InRange(r.AeroFront, c.AeroFrontMin, c.AeroFrontMax);
            Assert.InRange(r.AeroRear,  c.AeroRearMin,  c.AeroRearMax);
            Assert.InRange(r.DiffAccel, c.DiffAccelMin, c.DiffAccelMax);
            Assert.InRange(r.DiffDecel, c.DiffDecelMin, c.DiffDecelMax);
            Assert.InRange(r.BrakeBalance, c.BrakeBalanceMin, c.BrakeBalanceMax);
            Assert.InRange(r.BrakePressure, c.BrakePressureMin, c.BrakePressureMax);
            Assert.InRange(r.FinalDrive, c.FinalDriveMin, c.FinalDriveMax);
            Assert.Equal(car.GearCount, r.GearRatios.Count);
            if (r.DiffFrontAccel.HasValue) Assert.InRange(r.DiffFrontAccel.Value, 0.0, 100.0);
            if (r.DiffFrontDecel.HasValue) Assert.InRange(r.DiffFrontDecel.Value, 0.0, 100.0);
            if (r.CenterDiffBias.HasValue) Assert.InRange(r.CenterDiffBias.Value, 0.0, 100.0);
        }
    }

    // 162 – No null values for non-nullable fields across all combos
    [Fact]
    public void T162_NoNullValuesForNonNullableFields()
    {
        foreach (var (car, track) in AllCombos())
        {
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            Assert.NotNull(r.Car);
            Assert.NotNull(r.Track);
            Assert.NotNull(r.GearRatios);
            Assert.NotNull(r.Explanations);
            Assert.NotEmpty(r.GearRatios);
            Assert.NotEmpty(r.Explanations);
        }
    }

    // 163 – All 12 explanation keys present for every combination
    [Fact]
    public void T163_AllExplanationKeysPresent_AllCombos()
    {
        var required = new[] { "TirePressure", "Camber", "Toe", "Caster", "ARB", "Springs",
                               "RideHeight", "Dampers", "Aero", "Differential", "Brakes", "FinalDrive" };
        foreach (var (car, track) in AllCombos())
        {
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            foreach (var key in required)
            {
                Assert.True(r.Explanations.ContainsKey(key) && !string.IsNullOrWhiteSpace(r.Explanations[key]),
                    $"{car.DriveType}/{track.Discipline}: missing explanation key '{key}'");
            }
        }
    }

    // 164 – AWD-specific fields null/non-null correctly per drivetrain
    [Fact]
    public void T164_AwdSpecificFieldsCorrectForAllCombos()
    {
        foreach (var (car, track) in AllCombos())
        {
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            if (car.DriveType == Forza_Horizon_6_Tune_Master.Models.DriveType.AWD)
            {
                Assert.NotNull(r.CenterDiffBias);
                Assert.NotNull(r.DiffFrontAccel);
                Assert.NotNull(r.DiffFrontDecel);
            }
            else
            {
                Assert.Null(r.CenterDiffBias);
                Assert.Null(r.DiffFrontAccel);
                Assert.Null(r.DiffFrontDecel);
            }
        }
    }

    // 165 – Explanations content non-empty and meaningful for all combos
    [Fact]
    public void T165_ExplanationsContentNonEmpty_AllCombos()
    {
        foreach (var (car, track) in AllCombos())
        {
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            foreach (var kvp in r.Explanations)
            {
                Assert.False(string.IsNullOrWhiteSpace(kvp.Value),
                    $"{car.DriveType}/{track.Discipline}: explanation '{kvp.Key}' is empty");
                Assert.True(kvp.Value.Length >= 5,
                    $"{car.DriveType}/{track.Discipline}: explanation '{kvp.Key}' too short: '{kvp.Value}'");
            }
        }
    }

    // 166 – Gear ratios always positive for all combos
    [Fact]
    public void T166_GearRatiosAlwaysPositive_AllCombos()
    {
        foreach (var (car, track) in AllCombos())
        {
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            foreach (var g in r.GearRatios)
                Assert.True(g > 0, $"{car.DriveType}/{track.Discipline}: gear ratio {g} ≤ 0");
        }
    }

    // 167 – Gear ratios strictly descending for all combos
    [Fact]
    public void T167_GearRatiosDescending_AllCombos()
    {
        foreach (var (car, track) in AllCombos())
        {
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            for (int i = 1; i < r.GearRatios.Count; i++)
                Assert.True(r.GearRatios[i] < r.GearRatios[i - 1],
                    $"{car.DriveType}/{track.Discipline}: gear {i+1}={r.GearRatios[i]} not < gear {i}={r.GearRatios[i-1]}");
        }
    }

    // 168 – Gear count matches for all combos (including electric single gear)
    [Fact]
    public void T168_GearCountMatches_AllCombos()
    {
        foreach (var (car, track) in AllCombos())
        {
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            Assert.Equal(car.GearCount, r.GearRatios.Count);
        }
    }

    // 169 – No negative tire pressure for any combo
    [Fact]
    public void T169_TirePressureAlwaysPositive_AllCombos()
    {
        foreach (var (car, track) in AllCombos())
        {
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            Assert.True(r.TirePressureFront > 0,
                $"{car.DriveType}/{track.Discipline}: front pressure {r.TirePressureFront} ≤ 0");
            Assert.True(r.TirePressureRear > 0,
                $"{car.DriveType}/{track.Discipline}: rear pressure {r.TirePressureRear} ≤ 0");
        }
    }

    // 170 – No negative damper values
    [Fact]
    public void T170_DamperValuesPositive_AllCombos()
    {
        foreach (var (car, track) in AllCombos())
        {
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            Assert.True(r.ReboundFront > 0); Assert.True(r.ReboundRear > 0);
            Assert.True(r.BumpFront > 0);    Assert.True(r.BumpRear > 0);
        }
    }

    // 171 – Spring rates always positive
    [Fact]
    public void T171_SpringRatesPositive_AllCombos()
    {
        foreach (var (car, track) in AllCombos())
        {
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            Assert.True(r.SpringFront > 0, $"{car.DriveType}/{track.Discipline}: front spring {r.SpringFront} ≤ 0");
            Assert.True(r.SpringRear > 0,  $"{car.DriveType}/{track.Discipline}: rear spring {r.SpringRear} ≤ 0");
        }
    }

    // 172 – Bump < Rebound invariant for all combos
    [Fact]
    public void T172_BumpLessThanRebound_AllCombos()
    {
        foreach (var (car, track) in AllCombos())
        {
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            Assert.True(r.BumpFront <= r.ReboundFront,
                $"{car.DriveType}/{track.Discipline}: bump front {r.BumpFront} > rebound front {r.ReboundFront}");
            Assert.True(r.BumpRear <= r.ReboundRear,
                $"{car.DriveType}/{track.Discipline}: bump rear {r.BumpRear} > rebound rear {r.ReboundRear}");
        }
    }

    // 173 – ARB minimum 1 for all combos
    [Fact]
    public void T173_ArbMinimumOne_AllCombos()
    {
        foreach (var (car, track) in AllCombos())
        {
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            Assert.True(r.ARBFront >= 1, $"{car.DriveType}/{track.Discipline}: ARB front {r.ARBFront} < 1");
            Assert.True(r.ARBRear >= 1,  $"{car.DriveType}/{track.Discipline}: ARB rear {r.ARBRear} < 1");
        }
    }

    // 174 – Final drive positive for all combos
    [Fact]
    public void T174_FinalDrivePositive_AllCombos()
    {
        foreach (var (car, track) in AllCombos())
        {
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            Assert.True(r.FinalDrive > 0, $"{car.DriveType}/{track.Discipline}: FD {r.FinalDrive} ≤ 0");
        }
    }

    // 175 – Diff accel and decel within 0-100 for all combos
    [Fact]
    public void T175_DiffWithinRange_AllCombos()
    {
        foreach (var (car, track) in AllCombos())
        {
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            Assert.InRange(r.DiffAccel, 0.0, 100.0);
            Assert.InRange(r.DiffDecel, 0.0, 100.0);
            if (r.DiffFrontAccel.HasValue) Assert.InRange(r.DiffFrontAccel.Value, 0.0, 100.0);
            if (r.DiffFrontDecel.HasValue) Assert.InRange(r.DiffFrontDecel.Value, 0.0, 100.0);
        }
    }

    // 176 – Aero does not exceed maximum for any combo (no-aero cars = 0)
    [Fact]
    public void T176_AeroBoundsRespected_AllCombos()
    {
        foreach (var (car, track) in AllCombos())
        {
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            Assert.InRange(r.AeroFront, 0.0, 200.0);
            Assert.InRange(r.AeroRear,  0.0, 200.0);
            // Cars without aero must have 0
            if (!car.HasFrontAero) Assert.Equal(0.0, r.AeroFront);
            if (!car.HasRearAero)  Assert.Equal(0.0, r.AeroRear);
        }
    }

    // 177 – No-aero cars always produce zero aero regardless of discipline
    [Fact]
    public void T177_NoAeroCarsAlwaysZeroAero()
    {
        var noAeroCars = new[] { CarFactory.GtrR35(), CarFactory.SupraA90(), CarFactory.Mx5() };
        var disciplines = new[] { CarFactory.Road(), CarFactory.Drag(), CarFactory.Drift(),
                                  CarFactory.Rally(), CarFactory.CrossCountry(), CarFactory.Touge() };
        foreach (var car in noAeroCars)
        foreach (var track in disciplines)
        {
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            Assert.Equal(0.0, r.AeroFront);
            Assert.Equal(0.0, r.AeroRear);
        }
    }

    // 178 – Aero car produces positive aero on road (both ends)
    [Fact]
    public void T178_AeroCarPositiveAeroOnRoad()
    {
        var r = Gen(CarFactory.Gt3Rs(), CarFactory.Road());
        Assert.True(r.AeroFront > 0, $"GT3 RS road aero front {r.AeroFront} should be > 0");
        Assert.True(r.AeroRear > 0,  $"GT3 RS road aero rear {r.AeroRear} should be > 0");
    }

    // 179 – Diff accel > decel for road/touge/street (stability)
    [Fact]
    public void T179_DiffAccelGreaterThanDecel_RoadTougeStreet()
    {
        foreach (var car in new[] { CarFactory.GtrR35(), CarFactory.SupraA90(), CarFactory.Mx5() })
        foreach (var track in new[] { CarFactory.Road(), CarFactory.Touge(), CarFactory.Street() })
        {
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            Assert.True(r.DiffAccel >= r.DiffDecel,
                $"{car.DriveType}/{track.Discipline}: accel {r.DiffAccel} < decel {r.DiffDecel}");
        }
    }

    // 180 – Bump ratio front/rear similar (not drastically different)
    [Fact]
    public void T180_BumpRatioFrontRearSimilar_AllCombos()
    {
        foreach (var (car, track) in AllCombos())
        {
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            if (r.ReboundFront >= 2 && r.ReboundRear >= 2)
            {
                double ratioF = r.BumpFront / r.ReboundFront;
                double ratioR = r.BumpRear  / r.ReboundRear;
                double diff = Math.Abs(ratioF - ratioR);
                Assert.True(diff <= 0.30,
                    $"{car.DriveType}/{track.Discipline}: bump ratio front {ratioF:F2} vs rear {ratioR:F2} diff {diff:F2} > 0.30");
            }
        }
    }

    private static TuneResult Gen(CarCard car, TrackInfo track)
        => new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
    private static IEnumerable<(CarCard, TrackInfo)> AllCombos()
    {
        var cars   = new[] { CarFactory.GtrR35(), CarFactory.SupraA90(), CarFactory.CivicTypeR(),
                             CarFactory.Gemera(), CarFactory.Mx5(), CarFactory.Wrangler(),
                             CarFactory.Gt3Rs(), CarFactory.ModelSPlaid(), CarFactory.WrxSti(), CarFactory.Hellcat() };
        var tracks = new[] { CarFactory.Road(), CarFactory.Drag(), CarFactory.Drift(),
                             CarFactory.Rally(), CarFactory.CrossCountry(), CarFactory.Touge() };
        foreach (var c in cars) foreach (var t in tracks) yield return (c, t);
    }
}
