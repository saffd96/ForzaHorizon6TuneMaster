using Forza_Horizon_6_Tune_Master.Models;

namespace TuneMaster.Tests.Models;

public class CarDataTests
{
    [Fact]
    public void DisplayName_FormatsCorrectly()
    {
        var data = new CarData { Year = 2020, Make = "Toyota", Model = "Supra" };
        Assert.Equal("2020 Toyota Supra", data.DisplayName);
    }

    [Fact]
    public void DisplayName_TrimsWhenMakeEmpty()
    {
        var data = new CarData { Year = 2024, Make = "", Model = "Model 3" };
        Assert.Equal("2024  Model 3", data.DisplayName);
    }

    [Fact]
    public void DisplayName_HandlesModelOnly()
    {
        var data = new CarData { Year = 2024, Make = "", Model = "" };
        Assert.Equal("2024", data.DisplayName);
    }
}
