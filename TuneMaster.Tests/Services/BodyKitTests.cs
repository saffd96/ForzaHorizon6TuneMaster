using System.Linq;
using System.Threading.Tasks;
using Forza_Horizon_6_Tune_Master.Services;
using Xunit;

namespace TuneMaster.Tests.Services;

// 2022 Acura NSX Type S (ordinal 3767): a body-kit conversion exists. The stock kit lets you
// pick a front bumper AND a rear wing; the swapped (Liberty Walk widebody) kit has only one
// front bumper, so effectively only the rear wing is choosable. Front bumpers key off the
// kit's CarBodyID; rear wings key off the ordinal and are shared across kits.
[Collection("FileSystem")]
public class BodyKitTests
{
    private const int NsxOrdinal = 3767;

    private static async Task<Fh6DatabaseService> DbAsync()
    {
        await Fh6DatabaseService.Instance.InitializeAsync();
        return Fh6DatabaseService.Instance;
    }

    [Fact]
    public async Task Nsx_HasStockAndSwappedBodyKit()
    {
        var db = await DbAsync();
        var kits = db.GetCarBodies(NsxOrdinal);

        Assert.Equal(2, kits.Count);
        Assert.Single(kits, k => k.IsStock);
        // Stock kit keeps the ordinal×1000 CarBodyID; the swap re-keys to a different body.
        var stock = kits.Single(k => k.IsStock);
        var swap = kits.Single(k => !k.IsStock);
        Assert.Equal(NsxOrdinal * 1000, stock.CarBodyId);
        Assert.NotEqual(stock.CarBodyId, swap.CarBodyId);
    }

    [Fact]
    public async Task Nsx_StockKit_OffersFrontBumperChoice_SwappedKitDoesNot()
    {
        var db = await DbAsync();
        var kits = db.GetCarBodies(NsxOrdinal);
        int stockBody = kits.Single(k => k.IsStock).CarBodyId;
        int swapBody = kits.Single(k => !k.IsStock).CarBodyId;

        // Stock kit: more than one front bumper → a real choice.
        Assert.True(db.GetFrontBumpers(stockBody).Count > 1);
        // Swapped widebody: a single fixed front bumper → no choice.
        Assert.Single(db.GetFrontBumpers(swapBody));
    }

    [Fact]
    public async Task Nsx_RearWings_AreSharedAcrossKits()
    {
        var db = await DbAsync();
        // Rear wings are keyed by ordinal, not CarBodyID, so they stay available on both kits.
        Assert.True(db.GetRearWings(NsxOrdinal).Count > 1);
    }

    // 1998 Toyota Supra RZ (ordinal 461): the widebody conversion itself has MassDiff 0, but it
    // re-keys to a body whose STOCK hood is 5 kg lighter than the original — so switching kits
    // changes the car's mass through the auto-fitted hood, not through the conversion row.
    [Fact]
    public async Task SupraRz_WidebodyStockHood_Is5KgLighterThanStockBodyHood()
    {
        const int supraOrdinal = 461;
        var db = await DbAsync();
        var kits = db.GetCarBodies(supraOrdinal);
        int stockBody = kits.Single(k => k.IsStock).CarBodyId;
        int swapBody = kits.Single(k => !k.IsStock).CarBodyId;

        var stockHood = db.GetHoods(stockBody).Single(h => h.IsStock);
        var swapHood = db.GetHoods(swapBody).Single(h => h.IsStock);

        Assert.Equal(0.0, stockHood.MassDiff);
        Assert.Equal(-5.0, swapHood.MassDiff);
    }

    [Fact]
    public async Task Nsx_SwappedKit_HasWiderTrackGeometry()
    {
        var db = await DbAsync();
        var kits = db.GetCarBodies(NsxOrdinal);
        var stockBody = db.GetCarBody(kits.Single(k => k.IsStock).CarBodyId);
        var swapBody = db.GetCarBody(kits.Single(k => !k.IsStock).CarBodyId);

        Assert.NotNull(stockBody);
        Assert.NotNull(swapBody);
        // The Liberty Walk widebody conversion widens the track on both axles.
        Assert.True(swapBody!.ModelFrontTrackOuter > stockBody!.ModelFrontTrackOuter);
        Assert.True(swapBody.ModelRearTrackOuter > stockBody.ModelRearTrackOuter);
    }
}
