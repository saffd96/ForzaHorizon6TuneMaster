using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;
using TuneMaster.Tests.Helpers;

namespace TuneMaster.Tests;

/// Tests 091–100: Integration, edge cases, output completeness
/// Verifies: all fields populated, explanations present, determinism,
///           extreme car configs don't crash, result is self-consistent
public class IntegrationAndEdgeCaseTests
{
    private static TuneResult Gen(CarCard car, TrackInfo track)
        => new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());

    // 091 – All 12 explanation keys are populated after generation
    [Fact]
    public void T091_AllExplanationKeysPresent()
    {
        var r = Gen(CarFactory.GtrR35(), CarFactory.Road());
        var required = new[] { "TirePressure", "Camber", "Toe", "Caster", "ARB", "Springs", "RideHeight", "Dampers", "Aero", "Differential", "Brakes", "FinalDrive" };
        foreach (var key in required)
        {
            Assert.True(r.Explanations.ContainsKey(key) && !string.IsNullOrWhiteSpace(r.Explanations[key]),
                $"Explanation key '{key}' missing or empty");
        }
    }

    // 092 – Generation is deterministic: same inputs produce identical outputs
    [Fact]
    public void T092_GenerationIsDeterministic()
    {
        var car   = CarFactory.SupraA90();
        var track = CarFactory.Road();
        var c     = CarFactory.DefaultConstraints();
        var svc   = new TuneGeneratorService();
        var r1    = svc.Generate(car, track, c);
        var r2    = svc.Generate(car, track, c);
        Assert.Equal(r1.TirePressureFront, r2.TirePressureFront);
        Assert.Equal(r1.SpringFront,       r2.SpringFront);
        Assert.Equal(r1.ARBFront,          r2.ARBFront);
        Assert.Equal(r1.FinalDrive,        r2.FinalDrive);
        Assert.Equal(r1.DiffAccel,         r2.DiffAccel);
    }

    // 093 – Minimum mass car (500 kg) does not crash and produces valid output
    [Fact]
    public void T093_MinimumMassCarDoesNotCrash()
    {
        var car = CarFactory.Mx5();
        car.TotalMass = 500;
        car.PowerHP   = 50;
        var r = Gen(car, CarFactory.Road());
        Assert.True(r.SpringFront > 0);
        Assert.True(r.FinalDrive  > 0);
        Assert.NotEmpty(r.GearRatios);
    }

    // 094 – Maximum mass car (3000 kg) does not crash and produces valid output
    [Fact]
    public void T094_MaximumMassCarDoesNotCrash()
    {
        var car = CarFactory.Wrangler();
        car.TotalMass = 3000;
        car.PowerHP   = 150;
        var r = Gen(car, CarFactory.CrossCountry());
        var c = CarFactory.DefaultConstraints();
        Assert.InRange(r.SpringFront,        c.SpringFrontMin,        c.SpringFrontMax);
        Assert.InRange(r.TirePressureFront,  c.TirePressureFrontMin,  c.TirePressureFrontMax);
    }

    // 095 – Ultra-high power (3000 HP) car does not crash and all values in range
    [Fact]
    public void T095_UltraHighPowerDoesNotCrash()
    {
        var car = CarFactory.Gemera();
        car.PowerHP = 3000;
        var c = CarFactory.DefaultConstraints();
        var r = new TuneGeneratorService().Generate(car, CarFactory.Drag(), c);
        Assert.InRange(r.ARBFront,     c.ARBFrontMin,     c.ARBFrontMax);
        Assert.InRange(r.ARBRear,      c.ARBRearMin,      c.ARBRearMax);
        Assert.InRange(r.DiffAccel,    c.DiffAccelMin,    c.DiffAccelMax);
        Assert.InRange(r.BrakePressure,c.BrakePressureMin,c.BrakePressureMax);
    }

    // 096 – No-aero car: aero values both 0
    [Fact]
    public void T096_NoAeroCarAeroValuesZero()
    {
        var car = CarFactory.GtrR35(); // HasFrontAero=false, HasRearAero=false
        var r = Gen(car, CarFactory.Road());
        Assert.Equal(0.0, r.AeroFront);
        Assert.Equal(0.0, r.AeroRear);
    }

    // 097 – Car with aero: rear aero > 0 on Road high-speed car
    [Fact]
    public void T097_AeroCar_RearAeroPositive()
    {
        var car = CarFactory.Gt3Rs(); // HasFrontAero=true, HasRearAero=true, 296 km/h
        var r = Gen(car, CarFactory.Road());
        Assert.True(r.AeroRear > 0, $"Rear aero {r.AeroRear} should be > 0 with rear aero installed");
    }

    // 098 – Eliminator discipline handled without exception (falls through to default)
    [Fact]
    public void T098_EliminatorDisciplineHandled()
    {
        var car   = CarFactory.Mx5();
        var track = new TrackInfo { Discipline = Discipline.Eliminator };
        var ex    = Record.Exception(() => Gen(car, track));
        Assert.Null(ex);
    }

    // 099 – TuneResult.Car and TuneResult.Track are the passed-in instances
    [Fact]
    public void T099_ResultReferencesInputObjects()
    {
        var car   = CarFactory.SupraA90();
        var track = CarFactory.Drift();
        var r     = Gen(car, track);
        Assert.Same(car,   r.Car);
        Assert.Same(track, r.Track);
    }

    // 100 – All 10 disciplines produce non-zero FinalDrive for a 6-gear car
    [Fact]
    public void T100_AllDisciplinesProduceNonZeroFinalDrive()
    {
        var car = CarFactory.SupraA90(); car.GearCount = 6;
        var disciplines = new[]
        {
            Discipline.Road, Discipline.Touge, Discipline.Rally, Discipline.CrossCountry,
            Discipline.Drift, Discipline.Drag, Discipline.Eliminator, Discipline.Street
        };
        foreach (var d in disciplines)
        {
            var track = new TrackInfo { Discipline = d, DragDistance = DragDistance.Quarter };
            var r = new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());
            Assert.True(r.FinalDrive > 0,
                $"Discipline {d}: FinalDrive={r.FinalDrive} should be > 0");
            Assert.Equal(6, r.GearRatios.Count);
        }
    }
}
