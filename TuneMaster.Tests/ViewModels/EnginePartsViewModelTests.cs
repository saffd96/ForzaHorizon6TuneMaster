using System.Threading.Tasks;
using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;
using Forza_Horizon_6_Tune_Master.ViewModels;
using Xunit;

namespace TuneMaster.Tests.ViewModels;

// Covers a bug reported for the 1984 DeTomaso Pantera GT5 84 (Data_Car.Id 3198, stock engine
// 3330, Data_Engine.Carbureted = 1): the fuel-system slot doubles as the carburetor slot for
// carbureted engines (PartDisplayNameResolver.ResolveFuelSystemKey already renames its options
// "Stock/Street/Sport/Race Carburetor"), but the row label in the UI always read "Fuel System",
// making it look like there was no carburetor option at all.
[Collection("FileSystem")]
public class EnginePartsViewModelTests
{
    private static async Task InitDbAsync() => await Fh6DatabaseService.Instance.InitializeAsync();

    [Fact]
    public async Task CarburetedEngine_LabelsFuelSystemRowAsCarburetor()
    {
        await InitDbAsync();

        var car = new CarCard { CarDbId = 3198, CarBodyId = 3198000 };
        var parts = new SelectedParts { EngineId = 3330 };

        var vm = new EnginePartsViewModel();
        vm.LoadForCar(car, parts);

        Assert.True(vm.IsCarbureted);
        Assert.Equal(LocalizationService.Instance.T("Part_Carburetor"), vm.FuelSystemLabel);
        Assert.NotEmpty(vm.FuelSystems);
        Assert.Contains(vm.FuelSystems, o => o.DisplayName.Contains("Carburetor"));
    }

    [Fact]
    public async Task NonCarburetedEngine_LabelsFuelSystemRowAsFuelSystem()
    {
        await InitDbAsync();

        // A Pantera swap engine with Data_Engine.Carbureted = 0 (queried directly against the
        // DB earlier alongside the stock 3330 above), so this exercises the same car/UI path
        // with the opposite flag rather than depending on an arbitrary unrelated car.
        var car = new CarCard { CarDbId = 3198, CarBodyId = 3198000 };
        var parts = new SelectedParts { EngineId = 579 };

        var vm = new EnginePartsViewModel();
        vm.LoadForCar(car, parts);

        Assert.False(vm.IsCarbureted);
        Assert.Equal(LocalizationService.Instance.T("Part_FuelSystem"), vm.FuelSystemLabel);
    }
}
