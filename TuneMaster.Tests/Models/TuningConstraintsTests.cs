using Forza_Horizon_6_Tune_Master.Models;

namespace TuneMaster.Tests.Models;

public class TuningConstraintsTests
{
    [Fact]
    public void DefaultValues_AreReasonable()
    {
        var c = new TuningConstraints();

        Assert.Equal(1.0, c.TirePressureFrontMin);
        Assert.Equal(3.8, c.TirePressureFrontMax);
        Assert.Equal(-5.0, c.CamberFrontMin);
        Assert.Equal(5.0, c.CamberFrontMax);
        Assert.Equal(560, c.SpringFrontMin);
        Assert.Equal(2800, c.SpringFrontMax);
        Assert.Equal(50.0, c.RideHeightFrontMin);
        Assert.Equal(250.0, c.RideHeightFrontMax);
        Assert.Equal(1.0, c.CasterMin);
        Assert.Equal(7.0, c.CasterMax);
    }

    [Fact]
    public void SetMinMax_EnforcesMinLessThanMax_Pressure()
    {
        var c = new TuningConstraints();
        c.TirePressureFrontMin = 3.0;
        c.TirePressureFrontMax = 2.0; // should push max up
        Assert.True(c.TirePressureFrontMax >= c.TirePressureFrontMin);
    }

    [Fact]
    public void SetMaxMin_EnforcesMaxGreaterThanMin_Pressure()
    {
        var c = new TuningConstraints();
        c.TirePressureFrontMax = 1.5;
        c.TirePressureFrontMin = 2.0; // should push min down
        Assert.True(c.TirePressureFrontMax >= c.TirePressureFrontMin);
    }

    [Fact]
    public void SetMinMax_EnforcesMinLessThanMax_Camber()
    {
        var c = new TuningConstraints();
        c.CamberFrontMin = 1.0;
        c.CamberFrontMax = 0.5;
        Assert.True(c.CamberFrontMax >= c.CamberFrontMin);
    }

    [Fact]
    public void SetMinMax_EnforcesMinLessThanMax_Spring()
    {
        var c = new TuningConstraints();
        c.SpringFrontMin = 200;
        c.SpringFrontMax = 150;
        Assert.True(c.SpringFrontMax >= c.SpringFrontMin);
    }

    [Fact]
    public void SetMinMax_EnforcesMinLessThanMax_RideHeight()
    {
        var c = new TuningConstraints();
        c.RideHeightFrontMin = 200;
        c.RideHeightFrontMax = 100;
        Assert.True(c.RideHeightFrontMax >= c.RideHeightFrontMin);
    }

    [Fact]
    public void SetMinMax_EnforcesMinLessThanMax_ARB()
    {
        var c = new TuningConstraints();
        c.ARBFrontMin = 50;
        c.ARBFrontMax = 30;
        Assert.True(c.ARBFrontMax >= c.ARBFrontMin);
    }

    [Fact]
    public void SetMinMax_EnforcesMinLessThanMax_Damper()
    {
        var c = new TuningConstraints();
        c.ReboundFrontMin = 15;
        c.ReboundFrontMax = 10;
        Assert.True(c.ReboundFrontMax >= c.ReboundFrontMin);
    }

    [Fact]
    public void SetMinMax_EnforcesMinLessThanMax_Brake()
    {
        var c = new TuningConstraints();
        c.BrakeBalanceMin = 60;
        c.BrakeBalanceMax = 55;
        Assert.True(c.BrakeBalanceMax >= c.BrakeBalanceMin);
    }

    [Fact]
    public void SetMinMax_EnforcesMinLessThanMax_FinalDrive()
    {
        var c = new TuningConstraints();
        c.FinalDriveMin = 5.0;
        c.FinalDriveMax = 4.0;
        Assert.True(c.FinalDriveMax >= c.FinalDriveMin);
    }

    [Fact]
    public void SetMinMax_EnforcesMinLessThanMax_Aero()
    {
        var c = new TuningConstraints();
        c.AeroFrontMin = 120;
        c.AeroFrontMax = 100;
        Assert.True(c.AeroFrontMax >= c.AeroFrontMin);
    }

    [Fact]
    public void SetMinMax_Differential()
    {
        var c = new TuningConstraints();
        c.DiffAccelMin = 60;
        c.DiffAccelMax = 50;
        Assert.True(c.DiffAccelMax >= c.DiffAccelMin);
    }

    [Fact]
    public void CenterDiffBias_Default50()
    {
        var c = new TuningConstraints();
        Assert.Equal(50, c.CenterDiffBias);
    }

    [Fact]
    public void PropertyChanged_Raised()
    {
        var c = new TuningConstraints();
        var changed = new List<string>();
        c.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        c.TirePressureFrontMin = 1.5;

        Assert.Contains(nameof(TuningConstraints.TirePressureFrontMin), changed);
    }

    [Fact]
    public void RearProperties_IndependentFromFront()
    {
        var c = new TuningConstraints();
        c.TirePressureFrontMin = 2.5;
        c.TirePressureRearMin = 1.5;
        Assert.Equal(2.5, c.TirePressureFrontMin);
        Assert.Equal(1.5, c.TirePressureRearMin);
    }

    [Fact]
    public void FinalDriveMin_ClampedAboveZero()
    {
        var c = new TuningConstraints();
        c.FinalDriveMin = -1.0;
        Assert.True(c.FinalDriveMin >= 0.1);
    }

    [Fact]
    public void JsonPropertyOrder_PressureFrontMax_BeforeMin()
    {
        var c = new TuningConstraints();

        var props = typeof(TuningConstraints).GetProperties();
        var maxProp = props.First(p => p.Name == "TirePressureFrontMax");
        var minProp = props.First(p => p.Name == "TirePressureFrontMin");

        var maxOrder = maxProp.GetCustomAttributes(typeof(System.Text.Json.Serialization.JsonPropertyOrderAttribute), false)
            .Cast<System.Text.Json.Serialization.JsonPropertyOrderAttribute>().First().Order;
        var minOrder = minProp.GetCustomAttributes(typeof(System.Text.Json.Serialization.JsonPropertyOrderAttribute), false)
            .Cast<System.Text.Json.Serialization.JsonPropertyOrderAttribute>().First().Order;

        Assert.True(maxOrder < minOrder);
    }
}
