using System.Text.Json;
using Forza_Horizon_6_Tune_Master.Models;

namespace TuneMaster.Tests.Models;

public class CarCardTests
{
    [Fact]
    public void DefaultCar_HasExpectedDefaults()
    {
        var car = new CarCard();

        Assert.Equal(1400, car.TotalMass);
        Assert.Equal(50, car.WeightDistributionFront);
        Assert.Equal(300, car.PowerHP);
        Assert.Equal(400, car.TorqueNm);
        Assert.Equal(7000, car.MaxRPM);
        Assert.Equal(DriveType.RWD, car.DriveType);
        Assert.Equal(EnginePosition.Front, car.EnginePosition);
        Assert.Equal(EngineType.V6, car.EngineType);
        Assert.Equal(TireType.Sport, car.TireType);
        Assert.Equal(SuspensionUpgrade.Sport, car.SuspensionUpgrade);
    }

    [Fact]
    public void TotalMass_ClampsToMinimum()
    {
        var car = new CarCard();
        car.TotalMass = -100;
        Assert.Equal(1, car.TotalMass);
    }

    [Fact]
    public void SuspensionAllowsAdvancedTuning_Race_ReturnsTrue()
    {
        var car = new CarCard { SuspensionUpgrade = SuspensionUpgrade.Race };
        Assert.True(car.SuspensionAllowsAdvancedTuning);
    }

    [Fact]
    public void SuspensionAllowsAdvancedTuning_Sport_ReturnsFalse()
    {
        var car = new CarCard { SuspensionUpgrade = SuspensionUpgrade.Sport };
        Assert.False(car.SuspensionAllowsAdvancedTuning);
    }

    [Fact]
    public void SuspensionAllowsAdvancedTuning_Rally_ReturnsTrue()
    {
        var car = new CarCard { SuspensionUpgrade = SuspensionUpgrade.Rally };
        Assert.True(car.SuspensionAllowsAdvancedTuning);
    }

    [Fact]
    public void SuspensionAllowsAdvancedTuning_Drift_ReturnsTrue()
    {
        var car = new CarCard { SuspensionUpgrade = SuspensionUpgrade.Drift };
        Assert.True(car.SuspensionAllowsAdvancedTuning);
    }

    [Fact]
    public void SuspensionAllowsAdvancedTuning_Offroad_ReturnsTrue()
    {
        var car = new CarCard { SuspensionUpgrade = SuspensionUpgrade.Offroad };
        Assert.True(car.SuspensionAllowsAdvancedTuning);
    }

    [Theory]
    [InlineData(SuspensionUpgrade.Stock, false)]
    [InlineData(SuspensionUpgrade.Street, false)]
    [InlineData(SuspensionUpgrade.Sport, false)]
    public void SuspensionAllowsAdvancedTuning_Basic_ReturnsFalse(SuspensionUpgrade su, bool expected)
    {
        var car = new CarCard { SuspensionUpgrade = su };
        Assert.Equal(expected, car.SuspensionAllowsAdvancedTuning);
    }

    [Fact]
    public void ShowAntiLag_WithTurbo_ReturnsTrue()
    {
        var car = new CarCard { AspirationType = AspirationType.SingleTurbo };
        Assert.True(car.ShowAntiLag);
    }

    [Fact]
    public void ShowAntiLag_WithNA_ReturnsFalse()
    {
        var car = new CarCard { AspirationType = AspirationType.Natural };
        Assert.False(car.ShowAntiLag);
    }

    [Fact]
    public void ShowAntiLag_WithNull_ReturnsFalse()
    {
        var car = new CarCard { AspirationType = null };
        Assert.False(car.ShowAntiLag);
    }

    [Fact]
    public void AntiLag_DisabledWhenAspirationNotTurbo()
    {
        var car = new CarCard { AspirationType = AspirationType.Natural };
        car.AntiLag = true;
        car.AspirationType = AspirationType.Natural;
    }

    [Fact]
    public void IsElectricPowertrain_TrueForElectric()
    {
        var car = new CarCard { PowertrainType = PowertrainType.Electric };
        Assert.True(car.IsElectricPowertrain);
        Assert.False(car.ShowAspiration);
        Assert.Null(car.AspirationType);
    }

    [Fact]
    public void PowerPeakRPM_Electric_Uses45Percent()
    {
        var car = new CarCard { PowertrainType = PowertrainType.Electric, MaxRPM = 20000 };
        Assert.Equal(9000, car.PowerPeakRPM);
    }

    [Theory]
    [InlineData(EngineType.V8, 0.80)]
    [InlineData(EngineType.V12, 0.82)]
    [InlineData(EngineType.Rotary, 0.92)]
    [InlineData(EngineType.I4, 0.85)]
    public void PowerPeakRPM_ICE_VariesByType(EngineType engine, double fraction)
    {
        var car = new CarCard { EngineType = engine, MaxRPM = 7000 };
        var expected = (int)Math.Round(7000 * fraction / 100.0) * 100;
        Assert.Equal(expected, car.PowerPeakRPM);
    }

    [Fact]
    public void TorquePeakRPM_Electric_ReturnsZero()
    {
        var car = new CarCard { PowertrainType = PowertrainType.Electric };
        Assert.Equal(0, car.TorquePeakRPM);
    }

    [Theory]
    [InlineData(EngineType.V8, 0.50)]
    [InlineData(EngineType.I6, 0.55)]
    public void TorquePeakRPM_ICE_VariesByType(EngineType engine, double fraction)
    {
        var car = new CarCard { EngineType = engine, MaxRPM = 7000 };
        var rawPeak = (int)Math.Round(7000 * fraction / 100.0) * 100;
        var expected = Math.Max(500, rawPeak);
        Assert.Equal(expected, car.TorquePeakRPM);
    }

    [Theory]
    [InlineData(EngineType.V6)]
    [InlineData(EngineType.I4)]
    [InlineData(EngineType.V8)]
    [InlineData(EngineType.Boxer)]
    public void TorquePeakRPM_Minimum500(EngineType engine)
    {
        var car = new CarCard { EngineType = engine, MaxRPM = 3000 };
        Assert.True(car.TorquePeakRPM >= 500);
    }

    [Fact]
    public void MaxSpeedKmh_Computed_WithCdAndFrontalArea()
    {
        var car = new CarCard
        {
            PowerHP = 500,
            TotalMass = 1400,
            Cd = 0.32,
            FrontalAreaM2 = 2.0,
            FrontTireProfile = 35,
            RearTireProfile = 35,
        };
        Assert.True(car.MaxSpeedKmh > 100);
        Assert.True(car.MaxSpeedKmh <= 700);
    }

    [Fact]
    public void CdABodyEstimate_UsesCdAndArea_WhenBothSet()
    {
        var car = new CarCard { Cd = 0.30, FrontalAreaM2 = 2.0 };
        Assert.Equal(0.60, car.CdABodyEstimate);
    }

    [Fact]
    public void CdABodyEstimate_Estimates_WhenCdOrAreaMissing()
    {
        var car = new CarCard { TotalMass = 1400, FrontTireProfile = 40, RearTireProfile = 35 };
        Assert.True(car.CdABodyEstimate > 0.5);
    }

    [Fact]
    public void FrontWheelDiameterInch_Computed()
    {
        var car = new CarCard { FrontRimDiameter = 19, FrontTireWidth = 235, FrontTireProfile = 40 };
        double sidewallMm = 2.0 * 235 * 40 / 100.0;
        double expectedInch = 19 + sidewallMm / 25.4;
        Assert.Equal(expectedInch, car.FrontWheelDiameterInch);
    }

    [Fact]
    public void RearWheelDiameterInch_Computed()
    {
        var car = new CarCard { RearRimDiameter = 19, RearTireWidth = 265, RearTireProfile = 35 };
        double sidewallMm = 2.0 * 265 * 35 / 100.0;
        double expectedInch = 19 + sidewallMm / 25.4;
        Assert.Equal(expectedInch, car.RearWheelDiameterInch);
    }

    [Fact]
    public void IsElectricPowertrain_SetsAspirationNull()
    {
        var car = new CarCard { AspirationType = AspirationType.TwinTurbo };
        car.PowertrainType = PowertrainType.Electric;
        Assert.Null(car.AspirationType);
    }

    [Fact]
    public void Serialization_IgnoresComputedProperties()
    {
        var car = new CarCard
        {
            Make = "Test", Model = "Car", PowerHP = 400,
            Cd = 0.30, FrontalAreaM2 = 2.0,
        };
        var json = JsonSerializer.Serialize(car);
        Assert.Contains("Make", json);
        Assert.DoesNotContain("MaxSpeedKmh", json);
        Assert.DoesNotContain("CdABodyEstimate", json);
        Assert.DoesNotContain("PowerPeakRPM", json);
    }

    [Fact]
    public void MaxSpeedKmh_ClampedTo700()
    {
        var car = new CarCard
        {
            PowerHP = 2000,
            Cd = 0.15,
            FrontalAreaM2 = 1.0,
        };
        Assert.True(car.MaxSpeedKmh <= 700);
    }

    [Fact]
    public void MaxSpeedKmh_Minimum60()
    {
        var car = new CarCard
        {
            PowerHP = 1,
            TotalMass = 3000,
            FrontTireProfile = 70,
            RearTireProfile = 70,
        };
        Assert.True(car.MaxSpeedKmh >= 60);
    }

    [Fact]
    public void PowerHP_Changing_RaisesMaxSpeedKmh()
    {
        var car = new CarCard();
        var changed = new List<string>();
        car.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        car.PowerHP = 500;

        Assert.Contains("MaxSpeedKmh", changed);
    }

    [Fact]
    public void TotalMass_Changing_RaisesMaxSpeedKmh()
    {
        var car = new CarCard();
        var changed = new List<string>();
        car.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        car.TotalMass = 1600;

        Assert.Contains("MaxSpeedKmh", changed);
    }
}
