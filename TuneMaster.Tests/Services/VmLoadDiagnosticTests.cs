using System.Threading.Tasks;
using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;
using Forza_Horizon_6_Tune_Master.ViewModels;
using Xunit;

namespace TuneMaster.Tests.Services;

public class VmLoadDiagnosticTests
{
    [Fact]
    public async Task LoadRealCar_TransmissionHasOptions()
    {
        using var env = new TestingEnvironment();
        await Fh6DatabaseService.Instance.InitializeAsync();

        // Car 295 = 2003 Toyota Celica (FWD)
        var car = new CarCard { CarDbId = 295 };
        var dbCar = Fh6DatabaseService.Instance.GetCar(car.CarDbId);
        Assert.NotNull(dbCar);

        var parts = new SelectedParts();
        parts.SetCarData(car);

        var vm = new TransmissionViewModel();
        vm.LoadForCar(car, parts);

        Assert.True(vm.Transmissions.Count > 0, $"Expected transmissions for car {car.CarDbId}, got {vm.Transmissions.Count}");
        Assert.True(vm.Clutches.Count > 0, $"Expected clutches for car {car.CarDbId}, got {vm.Clutches.Count}");
        Assert.True(vm.Drivelines.Count > 0, $"Expected drivelines for car {car.CarDbId}, got {vm.Drivelines.Count}");
        Assert.True(vm.Differentials.Count > 0, $"Expected differentials for car {car.CarDbId}, got {vm.Differentials.Count}");
        Assert.NotNull(vm.SelectedTransmission);
        Assert.NotNull(vm.SelectedClutch);
        Assert.NotNull(vm.SelectedDriveline);
        Assert.NotNull(vm.SelectedDifferential);
    }

    [Fact]
    public async Task LoadRealCar_EnginePartsHaveOptions()
    {
        using var env = new TestingEnvironment();
        await Fh6DatabaseService.Instance.InitializeAsync();

        var car = new CarCard { CarDbId = 295 };
        var parts = new SelectedParts();
        parts.SetCarData(car);

        var swapsVm = new SwapsViewModel();
        swapsVm.LoadForCar(car, parts);

        var engineVm = new EnginePartsViewModel();
        engineVm.LoadForCar(car, parts);

        Assert.True(engineVm.Camshafts.Count > 0, "Camshafts empty");
        Assert.True(engineVm.Displacements.Count > 0, "Displacements empty");
        Assert.True(engineVm.Valves.Count > 0, "Valves empty");
        Assert.NotNull(parts.EngineSwapPartId);
        Assert.NotNull(parts.CamshaftPartId);

        // Simulate engine swap change
        var oldCamshaftId = parts.CamshaftPartId;
        var oldCamshaftLevel = Fh6DatabaseService.Instance.GetCamshaftById(oldCamshaftId ?? 0)?.Level ?? 0;
        var swaps = Fh6DatabaseService.Instance.GetEngineSwaps(car.CarDbId);
        var nonStockSwap = swaps.Find(s => !s.IsStock);
        if (nonStockSwap != null)
        {
            parts.EngineSwapPartId = nonStockSwap.Id;
            engineVm.ResetToStockForEngine(nonStockSwap.EngineID);

            Assert.NotNull(parts.CamshaftPartId);
            Assert.NotEqual(oldCamshaftId, parts.CamshaftPartId);
            if (oldCamshaftLevel > 0)
            {
                var newCamshaft = Fh6DatabaseService.Instance.GetCamshaftById(parts.CamshaftPartId.Value);
                Assert.NotNull(newCamshaft);
                Assert.Equal(oldCamshaftLevel, newCamshaft.Level);
            }
        }
    }

    [Fact]
    public async Task LoadRealCar_DrivetrainSwap_ChangesTransmissionParts()
    {
        using var env = new TestingEnvironment();
        await Fh6DatabaseService.Instance.InitializeAsync();

        // 1997 Honda Civic Type R (FWD stock)
        var car = new CarCard { CarDbId = 1273 };
        var parts = new SelectedParts();
        parts.SetCarData(car);

        var vm = new TransmissionViewModel();
        vm.LoadForCar(car, parts);

        Assert.True(vm.DrivetrainSwaps.Count > 1, $"Expected drivetrain swaps for car {car.CarDbId}, got {vm.DrivetrainSwaps.Count}");
        Assert.NotNull(parts.DrivetrainSwapPartId);
        var oldTransmissionId = parts.TransmissionPartId;
        var oldDifferentialId = parts.DifferentialPartId;

        var rwdSwap = vm.DrivetrainSwaps.Select(o => Fh6DatabaseService.Instance.GetDrivetrainSwapById(o.Id))
            .FirstOrDefault(s => s != null && s.DriveTypeID == 1); // RWD
        if (rwdSwap != null)
        {
            parts.DrivetrainSwapPartId = rwdSwap.Id;
            vm.ResetToStockForDrivetrain(rwdSwap.DrivetrainID);

        Assert.True(vm.Transmissions.Count > 0, "RWD transmissions empty");
        Assert.True(vm.Clutches.Count > 0, "RWD clutches empty");
        Assert.True(vm.Drivelines.Count > 0, "RWD drivelines empty");
        Assert.True(vm.Differentials.Count > 0, "RWD differentials empty");
        Assert.Equal(rwdSwap.DrivetrainID, parts.DrivetrainId);
    }
    }
}
