using Forza_Horizon_6_Tune_Master.Models;

namespace TuneMaster.Tests.Models;

public class TuneResultTests
{
    [Fact]
    public void DefaultConstructor_SetsGuidAndDate()
    {
        var r = new TuneResult();
        Assert.NotEqual(Guid.Empty, r.Id);
        Assert.NotEqual(default, r.CreatedAt);
    }

    [Fact]
    public void DefaultResult_HasEmptyGearsAndEmptyExplanations()
    {
        var r = new TuneResult();
        Assert.Empty(r.GearRatios);
        Assert.Empty(r.Explanations);
        Assert.Null(r.LaunchControlRpm);
        Assert.Null(r.DiffFrontAccel);
        Assert.Null(r.CenterDiffBias);
    }

    [Fact]
    public void ActualMaxSpeedKmh_ZeroWhenNoCar()
    {
        var r = new TuneResult();
        Assert.Equal(0, r.ActualMaxSpeedKmh);
    }

    [Fact]
    public void ActualMaxSpeedKmh_ZeroWhenNoGears()
    {
        var r = new TuneResult { Car = new CarCard() };
        Assert.Equal(0, r.ActualMaxSpeedKmh);
    }

    [Fact]
    public void ActualMaxSpeedKmh_ComputedCorrectly()
    {
        var car = new CarCard
        {
            MaxRPM = 7000,
            RearRimDiameter = 19,
            RearTireWidth = 265,
            RearTireProfile = 35,
        };
        var r = new TuneResult
        {
            Car = car,
            GearRatios = new List<double> { 3.5, 2.5, 1.8, 1.3, 1.0, 0.75 },
            FinalDrive = 3.5,
        };

        double tireCirc = Math.PI * car.RearWheelDiameterInch * 0.0254;
        double expected = Math.Round(7000 * 0.95 * tireCirc / (60.0 * 3.5 * 0.75) * 3.6);

        Assert.Equal(expected, r.ActualMaxSpeedKmh);
    }

    [Fact]
    public void ActualMaxSpeedKmh_ZeroWhenFinalDriveZero()
    {
        var r = new TuneResult
        {
            Car = new CarCard { MaxRPM = 7000 },
            GearRatios = new List<double> { 1.0 },
            FinalDrive = 0,
        };
        Assert.Equal(0, r.ActualMaxSpeedKmh);
    }

    [Fact]
    public void ActualMaxSpeedKmh_ZeroWhenTopGearZero()
    {
        var r = new TuneResult
        {
            Car = new CarCard { MaxRPM = 7000 },
            GearRatios = new List<double> { 0 },
            FinalDrive = 3.5,
        };
        Assert.Equal(0, r.ActualMaxSpeedKmh);
    }

    [Fact]
    public void LaunchControlRpm_NullForNonDrag()
    {
        var r = new TuneResult();
        Assert.Null(r.LaunchControlRpm);
    }

    [Fact]
    public void Explanations_InitiallyEmpty()
    {
        var r = new TuneResult();
        Assert.Empty(r.Explanations);
    }

    [Fact]
    public void Explanations_CanBePopulated()
    {
        var r = new TuneResult();
        r.Explanations["Camber"] = "Test explanation";
        Assert.Equal("Test explanation", r.Explanations["Camber"]);
    }
}
