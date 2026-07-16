using System.Threading.Tasks;
using Forza_Horizon_6_Tune_Master;
using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;
using Forza_Horizon_6_Tune_Master.ViewModels;
using TuneMaster.Tests.Helpers;
using Xunit;

namespace TuneMaster.Tests.ViewModels;

[Collection("FileSystem")]
public class CarSpecControllerTests
{
    // Regression for the CdA back-solve ignoring a gearing-limited stock reference speed.
    // The 1985 Sprinter FE (CarDbId 4162)'s stock SimTopSpeed (226 km/h) sits right at its own
    // stock 6-speed's kinematic top-gear-at-redline ceiling (~239 km/h) — i.e. the stock car
    // never actually explored its aero ceiling, it just ran out of gears. Confirmed against a
    // real in-game reading: a Mile-strip, max-upgrade build of this car (1065.7 HP, 879 kg)
    // reaches ~500 km/h, far past what a CdA solved from 226 km/h alone would ever predict
    // (~424 km/h). Folding the stock kinematic ceiling into the anchor should pull CdA down
    // and the predicted top speed up notably, even if not all the way to 500 (no DB field
    // captures the swapped body's real drag, so this is a partial, principled correction).
    [Fact]
    public async Task PopulateCarFromDb_Sprinter4162_CdAAccountsForGearingLimitedStockSpeed()
    {
        using var env = new TestingEnvironment();
        await Fh6DatabaseService.Instance.InitializeAsync();

        var controller = new CarSpecController();
        var car = new CarCard();
        controller.PopulateCarFromDb(new CarData { Id = 4162 }, car);

        // Before this fix, CdA solved to ~0.79 (from SimTopSpeed=226 km/h alone), predicting
        // only ~424 km/h at 1065.7 HP. The stock kinematic ceiling (~239 km/h) is higher, so it
        // should now be the anchor, pulling CdA down measurably.
        Assert.True(car.Cd < 0.75,
            $"expected the stock kinematic ceiling to pull CdA below the old ~0.79 anchor, got {car.Cd}");

        double vKmh = System.Math.Pow(1065.7 * PhysicsConstants.HpToWatt / (0.5 * PhysicsConstants.AirDensity * car.Cd), 1.0 / 3.0)
            * PhysicsConstants.MsToKmh;
        Assert.True(vKmh > 440,
            $"expected predicted top speed at 1065.7 HP to rise notably above the old ~424 km/h estimate, got {vKmh:F1}");
    }
}
