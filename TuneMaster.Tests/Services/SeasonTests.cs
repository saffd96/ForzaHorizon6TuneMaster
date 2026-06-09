using Xunit;
using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;
using TuneMaster.Tests.Helpers;

namespace TuneMaster.Tests.Services;

public class SeasonTests
{
    private readonly TuneGeneratorService _sut = new();

    private static TrackInfo Track(Season s) => new() { Discipline = Discipline.Road, Season = s };


    // ── GetSeasonGripFactor ───────────────────────────────────────────────────

    [Theory]
    [InlineData(Season.Summer, 1.00)]
    [InlineData(Season.Spring, 0.93)]
    [InlineData(Season.Autumn, 0.88)]
    [InlineData(Season.Winter, 0.78)]
    public void GetSeasonGripFactor_ReturnsExpected(Season season, double expected)
    {
        Assert.Equal(expected, CalculationHelpers.GetSeasonGripFactor(season));
    }

    // ── Grip factor ordering ──────────────────────────────────────────────────

    [Fact]
    public void GetSeasonGripFactor_Order_SummerHighestWinterLowest()
    {
        double summer = CalculationHelpers.GetSeasonGripFactor(Season.Summer);
        double spring = CalculationHelpers.GetSeasonGripFactor(Season.Spring);
        double autumn = CalculationHelpers.GetSeasonGripFactor(Season.Autumn);
        double winter = CalculationHelpers.GetSeasonGripFactor(Season.Winter);

        Assert.True(summer > spring && spring > autumn && autumn > winter,
            $"Expected Summer({summer}) > Spring({spring}) > Autumn({autumn}) > Winter({winter})");
    }

    // ── Spring rates ──────────────────────────────────────────────────────────

    [Fact]
    public void Generate_SpringRates_WinterSofterThanSummer()
    {
        var c = CarFactory.RelaxedConstraints();

        var summer = _sut.Generate(CarFactory.DefaultCar(), Track(Season.Summer), c);
        var winter = _sut.Generate(CarFactory.DefaultCar(), Track(Season.Winter), c);

        Assert.True(winter.SpringFront < summer.SpringFront,
            $"SpringFront: Summer={summer.SpringFront:F4}, Winter={winter.SpringFront:F4}");
        Assert.True(winter.SpringRear < summer.SpringRear,
            $"SpringRear: Summer={summer.SpringRear:F4}, Winter={winter.SpringRear:F4}");
    }

    [Fact]
    public void Generate_SpringRates_SeasonOrderCorrect()
    {
        var c = CarFactory.RelaxedConstraints();

        double sprF(Season s) => _sut.Generate(CarFactory.DefaultCar(), Track(s), c).SpringFront;

        double su = sprF(Season.Summer);
        double sp = sprF(Season.Spring);
        double au = sprF(Season.Autumn);
        double wi = sprF(Season.Winter);

        Assert.True(su >= sp && sp >= au && au >= wi,
            $"SpringFront order: Summer({su}) >= Spring({sp}) >= Autumn({au}) >= Winter({wi})");
    }

    // ── ARB ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Generate_ARB_WinterSofterThanSummer()
    {
        var car = CarFactory.DefaultCar();
        var c   = CarFactory.RelaxedConstraints();

        var summer = _sut.Generate(car, Track(Season.Summer), c);
        var winter = _sut.Generate(car, Track(Season.Winter), c);

        Assert.True(winter.ARBFront < summer.ARBFront,
            $"ARBFront: Winter({winter.ARBFront}) should be < Summer({summer.ARBFront})");
        Assert.True(winter.ARBRear < summer.ARBRear,
            $"ARBRear: Winter({winter.ARBRear}) should be < Summer({summer.ARBRear})");
    }

    // ── Dampers ───────────────────────────────────────────────────────────────

    [Fact]
    public void Generate_Dampers_WinterSofterThanSummer()
    {
        var car = CarFactory.DefaultCar();
        var c   = CarFactory.RelaxedConstraints();

        var summer = _sut.Generate(car, Track(Season.Summer), c);
        var winter = _sut.Generate(car, Track(Season.Winter), c);

        Assert.True(winter.ReboundFront < summer.ReboundFront,
            $"ReboundFront: Winter({winter.ReboundFront}) < Summer({summer.ReboundFront})");
        Assert.True(winter.ReboundRear < summer.ReboundRear,
            $"ReboundRear: Winter({winter.ReboundRear}) < Summer({summer.ReboundRear})");
        Assert.True(winter.BumpFront < summer.BumpFront,
            $"BumpFront: Winter({winter.BumpFront}) < Summer({summer.BumpFront})");
        Assert.True(winter.BumpRear < summer.BumpRear,
            $"BumpRear: Winter({winter.BumpRear}) < Summer({summer.BumpRear})");
    }

    // ── Differential ─────────────────────────────────────────────────────────

    [Fact]
    public void Generate_DiffAccel_WinterLowerLockThanSummer()
    {
        var car = CarFactory.DefaultCar(); // RWD
        var c   = CarFactory.RelaxedConstraints();

        var summer = _sut.Generate(car, Track(Season.Summer), c);
        var winter = _sut.Generate(car, Track(Season.Winter), c);

        Assert.True(winter.DiffAccel < summer.DiffAccel,
            $"DiffAccel: Winter({winter.DiffAccel}) should be < Summer({summer.DiffAccel})");
    }

    // ── Tire pressure ─────────────────────────────────────────────────────────

    [Fact]
    public void Generate_TirePressure_WinterHigherThanSummer()
    {
        var car = CarFactory.DefaultCar();
        var c   = CarFactory.RelaxedConstraints();

        var summer = _sut.Generate(car, Track(Season.Summer), c);
        var winter = _sut.Generate(car, Track(Season.Winter), c);

        Assert.True(winter.TirePressureFront > summer.TirePressureFront,
            $"TirePressureFront: Winter({winter.TirePressureFront}) > Summer({summer.TirePressureFront})");
        Assert.True(winter.TirePressureRear > summer.TirePressureRear,
            $"TirePressureRear: Winter({winter.TirePressureRear}) > Summer({summer.TirePressureRear})");
    }

    [Fact]
    public void Generate_TirePressure_WinterSpringHigherThanAutumnSummer()
    {
        var car = CarFactory.DefaultCar();
        var c   = CarFactory.RelaxedConstraints();

        double pressF(Season s) => _sut.Generate(car, Track(s), c).TirePressureFront;

        double wi = pressF(Season.Winter);
        double sp = pressF(Season.Spring);
        double au = pressF(Season.Autumn);
        double su = pressF(Season.Summer);

        // Winter and Spring use +0.034; Autumn and Summer use -0.034 → diff = 0.068
        Assert.True(wi > su, $"Winter({wi}) > Summer({su})");
        Assert.True(sp > su, $"Spring({sp}) > Summer({su})");
        Assert.True(wi > au, $"Winter({wi}) > Autumn({au})");
        Assert.True(sp > au, $"Spring({sp}) > Autumn({au})");
    }

    [Fact]
    public void Generate_TirePressure_WinterSpringDeltaVsAutumnSummerIs0_068()
    {
        var car = CarFactory.DefaultCar();
        var c   = CarFactory.RelaxedConstraints();

        double pressF(Season s) => _sut.Generate(car, Track(s), c).TirePressureFront;

        // Winter +0.034 vs Summer -0.034 → delta = 0.068 (before rounding)
        double delta = pressF(Season.Winter) - pressF(Season.Summer);
        Assert.InRange(delta, 0.060, 0.076); // tight around 0.068, rounding-safe
    }

    // ── All seasons in range ──────────────────────────────────────────────────

    [Theory]
    [InlineData(Season.Summer)]
    [InlineData(Season.Spring)]
    [InlineData(Season.Autumn)]
    [InlineData(Season.Winter)]
    public void Generate_AllSeasons_TuneValuesWithinConstraints(Season season)
    {
        var car = CarFactory.DefaultCar();
        var c   = CarFactory.RelaxedConstraints();
        var r   = _sut.Generate(car, Track(season), c);

        Assert.InRange(r.TirePressureFront, c.TirePressureFrontMin, c.TirePressureFrontMax);
        Assert.InRange(r.TirePressureRear,  c.TirePressureRearMin,  c.TirePressureRearMax);
        Assert.InRange(r.SpringFront,       c.SpringFrontMin,        c.SpringFrontMax);
        Assert.InRange(r.SpringRear,        c.SpringRearMin,         c.SpringRearMax);
        Assert.InRange(r.ARBFront,          c.ARBFrontMin,           c.ARBFrontMax);
        Assert.InRange(r.ARBRear,           c.ARBRearMin,            c.ARBRearMax);
        Assert.InRange(r.DiffAccel,         c.DiffAccelMin,          c.DiffAccelMax);
    }

    // Checks spring constraint invariant with the DEFAULT (tight) range where
    // SpringFrontMax = 285.6 — Math.Round(285.6) = 286 was a latent overflow bug.
    [Theory]
    [InlineData(Season.Summer)]
    [InlineData(Season.Spring)]
    [InlineData(Season.Autumn)]
    [InlineData(Season.Winter)]
    public void Generate_AllSeasons_SpringWithinDefaultConstraints(Season season)
    {
        var c = CarFactory.DefaultConstraints();
        var r = _sut.Generate(CarFactory.DefaultCar(), Track(season), c);

        Assert.InRange(r.SpringFront, c.SpringFrontMin, c.SpringFrontMax);
        Assert.InRange(r.SpringRear,  c.SpringRearMin,  c.SpringRearMax);
    }
}
