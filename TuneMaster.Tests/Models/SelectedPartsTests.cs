using System.Reflection;
using Forza_Horizon_6_Tune_Master.Models;

namespace TuneMaster.Tests.Models;

public class SelectedPartsTests
{
    [Fact]
    public void ResetToStock_ResetsAllPartProperties()
    {
        var parts = new SelectedParts();

        parts.CamshaftPartId = 1;
        parts.DisplacementPartId = 2;
        parts.ValvesPartId = 3;

        parts.ResetAllToStock();

        Assert.Null(parts.CamshaftPartId);
        Assert.Null(parts.DisplacementPartId);
        Assert.Null(parts.ValvesPartId);
    }

    [Fact]
    public void ResetToStock_MarksExactly46Properties()
    {
        var count = typeof(SelectedParts)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Count(p => p.IsDefined(typeof(ResetToStockAttribute), false));

        // 39 original + FrontBumper/RearBumper/SideSkirt/Hood (body kits) + RimMassFront/RimMassRear
        // + BodyKit (body-kit conversion).
        Assert.Equal(46, count);
    }
}
