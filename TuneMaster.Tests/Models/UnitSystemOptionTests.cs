using Forza_Horizon_6_Tune_Master.ViewModels;
using Forza_Horizon_6_Tune_Master.Models;

namespace TuneMaster.Tests.Models;

public class UnitSystemOptionTests
{
    [Fact]
    public void ToString_ReturnsLabel()
    {
        var opt = new UnitSystemOption { Value = UnitSystem.Metric, Label = "Metric" };
        Assert.Equal("Metric", opt.ToString());
    }

    [Fact]
    public void PropertyChanged_RaisedOnLabelChange()
    {
        var opt = new UnitSystemOption();
        var changed = false;
        opt.PropertyChanged += (_, e) => { if (e.PropertyName == "Label") changed = true; };

        opt.Label = "New";

        Assert.True(changed);
    }
}

public class PowerUnitOptionTests
{
    [Fact]
    public void ToString_ReturnsLabel()
    {
        var opt = new PowerUnitOption { Value = PowerUnit.HP, Label = "HP" };
        Assert.Equal("HP", opt.ToString());
    }
}

public class SpringUnitOptionTests
{
    [Fact]
    public void ToString_ReturnsLabel()
    {
        var opt = new SpringUnitOption { Value = SpringUnit.NMm, Label = "N/mm" };
        Assert.Equal("N/mm", opt.ToString());
    }
}
