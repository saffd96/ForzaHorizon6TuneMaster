using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;
using Forza_Horizon_6_Tune_Master.ViewModels;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace TuneMaster.Tests.Services;

// Covers two bugs reported for the 1985 Toyota Sprinter Trueno GT-APEX Forza Edition (CarDbId
// 4162): (1) every tire-width option showed the same stock profile suffix instead of the
// in-game "plus sizing" profile for that width tier, (2) the Drag tire compound was silently
// dropped from the compound dropdown because its TireModelName ("Slick_FE") collides with other
// Slick tiers.
[Collection("FileSystem")]
public class TireOptionsTests
{
    private static async Task InitDbAsync() => await Fh6DatabaseService.Instance.InitializeAsync();

    // Sprinter Trueno GT-APEX Forza Edition and a spread of other "_FE" cars sharing the same
    // TireModelName collision, per DUMPER/fh6_db.sqlite List_UpgradeTireCompound.
    [Theory]
    [InlineData(4162)]
    [InlineData(4158)]
    [InlineData(4163)]
    [InlineData(4160)]
    [InlineData(4165)]
    [InlineData(4166)]
    [InlineData(4167)]
    [InlineData(4168)]
    [InlineData(4169)]
    [InlineData(4171)]
    [InlineData(4197)]
    [InlineData(4198)]
    [InlineData(4199)]
    [InlineData(4287)]
    [InlineData(4313)]
    public async Task DragTireCompound_IsPresent_ForForzaEditionCars(int carDbId)
    {
        await InitDbAsync();
        var car = BuildMinimalCar(carDbId);

        var vm = new TiresWheelsViewModel();
        vm.LoadForCar(car, new SelectedParts());

        Assert.Contains(vm.TireCompounds, o => o.DisplayName == "Drag Tire Compound");
    }

    // Reported values for the Sprinter FE 85 (stock 215/50 both axles): front width tiers read
    // 215/50 (stock) 225/50 235/45 245/45; rear reads 215/50 245/45 255/40 265/40. This matches
    // real-world "plus sizing" — profile shrinks as width grows to keep the tire's overall
    // (sidewall) diameter close to stock — round-to-nearest-5 of (stockWidth*stockProfile/width).
    [Theory]
    [InlineData(215, "50 (")] // stock
    [InlineData(225, "50")]
    [InlineData(235, "45")]
    [InlineData(245, "45")]
    public async Task TireWidthLabel_FrontProfile_MatchesInGamePlusSizing(int widthMm, string expectedProfileSuffix)
    {
        await InitDbAsync();
        var car = BuildMinimalCar(4162);

        var vm = new TiresWheelsViewModel();
        vm.LoadForCar(car, new SelectedParts());

        var option = vm.TireWidthsFront.Single(o => o.DisplayName.StartsWith($"{widthMm}/"));
        Assert.Contains($"/{expectedProfileSuffix}", option.DisplayName);
    }

    [Theory]
    [InlineData(215, "50 (")] // stock
    [InlineData(245, "45")]
    [InlineData(255, "40")]
    [InlineData(265, "40")]
    public async Task TireWidthLabel_RearProfile_MatchesInGamePlusSizing(int widthMm, string expectedProfileSuffix)
    {
        await InitDbAsync();
        var car = BuildMinimalCar(4162);

        var vm = new TiresWheelsViewModel();
        vm.LoadForCar(car, new SelectedParts());

        var option = vm.TireWidthsRear.Single(o => o.DisplayName.StartsWith($"{widthMm}/"));
        Assert.Contains($"/{expectedProfileSuffix}", option.DisplayName);
    }

    private static CarCard BuildMinimalCar(int carDbId)
    {
        var db = Fh6DatabaseService.Instance;
        var dbCar = db.GetCar(carDbId)!;
        return new CarCard
        {
            CarDbId = carDbId,
            CarBodyId = carDbId * 1000,
            StockFrontTireProfile = dbCar.FrontTireAspect,
            StockRearTireProfile = dbCar.RearTireAspect,
            FrontTireWidth = dbCar.FrontTireWidthMM,
            RearTireWidth = dbCar.RearTireWidthMM,
        };
    }
}
