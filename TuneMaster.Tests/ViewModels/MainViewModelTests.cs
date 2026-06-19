using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;
using Forza_Horizon_6_Tune_Master.ViewModels;
using TuneMaster.Tests;
using TuneMaster.Tests.Helpers;

namespace TuneMaster.Tests.ViewModels;

[Collection("FileSystem")]
public class MainViewModelTests : IDisposable
{
    private readonly TestingEnvironment _testEnv = new();

    public void Dispose()
    {
        _testEnv.Dispose();
    }

    [Fact]
    public void DefaultViewModel_HasDefaultCar()
    {
        var vm = new MainViewModel();
        Assert.NotNull(vm.Car);
        Assert.Equal(1400, vm.Car.TotalMass);
    }

    [Fact]
    public void DefaultViewModel_HasDefaultTrack()
    {
        var vm = new MainViewModel();
        Assert.NotNull(vm.Track);
        Assert.Equal(Discipline.Road, vm.Track.Discipline);
    }

    [Fact]
    public void DefaultViewModel_HasDefaultConstraints()
    {
        var vm = new MainViewModel();
        Assert.NotNull(vm.Constraints);
        Assert.Equal(1.0, vm.Constraints.TirePressureFrontMin);
    }

    [Fact]
    public void TuneResult_InitiallyNull()
    {
        var vm = new MainViewModel();
        Assert.Null(vm.TuneResult);
        Assert.False(vm.HasResult);
    }

    [Fact]
    public void SettingTuneResult_UpdatesHasResult()
    {
        var vm = new MainViewModel();
        vm.TuneResult = new TuneResult();
        Assert.True(vm.HasResult);
    }

    [Fact]
    public void ToggleUnits_SwitchesMetricToImperial()
    {
        var vm = new MainViewModel();
        vm.MeasurementSystem = UnitSystem.Metric;

        vm.ToggleUnitsCommand.Execute(null);

        Assert.Equal(UnitSystem.Imperial, vm.MeasurementSystem);
    }

    [Fact]
    public void ToggleUnits_SwitchesImperialToMetric()
    {
        var vm = new MainViewModel();
        vm.MeasurementSystem = UnitSystem.Imperial;

        vm.ToggleUnitsCommand.Execute(null);

        Assert.Equal(UnitSystem.Metric, vm.MeasurementSystem);
    }

    [Fact]
    public void TogglePowerUnit_CyclesCorrectly()
    {
        var vm = new MainViewModel();
        vm.PowerUnit = PowerUnit.HP;

        vm.TogglePowerUnitCommand.Execute(null);
        Assert.Equal(PowerUnit.PS, vm.PowerUnit);

        vm.TogglePowerUnitCommand.Execute(null);
        Assert.Equal(PowerUnit.KW, vm.PowerUnit);

        vm.TogglePowerUnitCommand.Execute(null);
        Assert.Equal(PowerUnit.HP, vm.PowerUnit);
    }

    [Fact]
    public void ToggleSpringUnit_CyclesCorrectly()
    {
        var vm = new MainViewModel();
        vm.SpringUnit = SpringUnit.KgfMm;

        vm.ToggleSpringUnitCommand.Execute(null);
        Assert.Equal(SpringUnit.NMm, vm.SpringUnit);

        vm.ToggleSpringUnitCommand.Execute(null);
        Assert.Equal(SpringUnit.LbsIn, vm.SpringUnit);

        vm.ToggleSpringUnitCommand.Execute(null);
        Assert.Equal(SpringUnit.KgfMm, vm.SpringUnit);
    }

    [Fact]
    public void UseImperial_MatchesMeasurementSystem()
    {
        var vm = new MainViewModel();
        vm.MeasurementSystem = UnitSystem.Imperial;
        Assert.True(vm.UseImperial);

        vm.MeasurementSystem = UnitSystem.Metric;
        Assert.False(vm.UseImperial);
    }

    [Fact]
    public void HasCenterDiffBias_FalseForRWD()
    {
        var vm = new MainViewModel();
        vm.Car.DriveType = DriveType.RWD;
        Assert.False(vm.HasCenterDiffBias);
    }

    [Fact]
    public void HasCenterDiffBias_TrueForAWD()
    {
        var vm = new MainViewModel();
        vm.Car.DriveType = DriveType.AWD;
        Assert.True(vm.HasCenterDiffBias);
    }

    [Fact]
    public void DefaultMeasurementSystem_HasValidValue()
    {
        var vm = new MainViewModel();
        Assert.True(vm.MeasurementSystem is UnitSystem.Metric or UnitSystem.Imperial);
    }

    [Fact]
    public void DefaultPowerUnit_IsHP()
    {
        var vm = new MainViewModel();
        Assert.Equal(PowerUnit.HP, vm.PowerUnit);
    }

    [Fact]
    public void StatusMessage_NotEmpty()
    {
        var vm = new MainViewModel();
        Assert.NotNull(vm.StatusMessage);
        Assert.NotEmpty(vm.StatusMessage);
    }

    [Fact]
    public void IsBusy_ReturnsBool()
    {
        var vm = new MainViewModel();
        Assert.IsType<bool>(vm.IsBusy);
    }

    [Fact]
    public void PowerDisplay_UpdatesWithPowerUnit()
    {
        var vm = new MainViewModel();
        vm.Car.PowerHP = 300;

        vm.PowerUnit = PowerUnit.HP;
        Assert.Equal(300, vm.PowerDisplay);

        vm.PowerUnit = PowerUnit.KW;
        Assert.Equal(Math.Round(300 * 0.7457, 1), vm.PowerDisplay);

        vm.PowerUnit = PowerUnit.PS;
        Assert.Equal(Math.Round(300 * 1.01387, 1), vm.PowerDisplay);
    }

    [Fact]
    public void SpeedDisplay_UpdatesWithUnitSystem()
    {
        var vm = new MainViewModel();
        vm.Car.PowerHP = 400;
        vm.Car.Cd = 0.30;
        vm.Car.FrontalAreaM2 = 2.0;

        vm.MeasurementSystem = UnitSystem.Metric;
        Assert.True(vm.SpeedDisplay > 0);

        vm.MeasurementSystem = UnitSystem.Imperial;
        var expectedMph = Math.Round(vm.Car.MaxSpeedKmh * 0.6214, 1);
        Assert.Equal(expectedMph, vm.SpeedDisplay);
    }

    [Fact]
    public void MassDisplay_UpdatesWithUnitSystem()
    {
        var vm = new MainViewModel();
        vm.Car.TotalMass = 1400;

        vm.MeasurementSystem = UnitSystem.Metric;
        Assert.Equal(1400, vm.MassDisplay);

        vm.MeasurementSystem = UnitSystem.Imperial;
        Assert.Equal(Math.Round(1400 * 2.2046, 1), vm.MassDisplay);
    }

    [Fact]
    public void TorqueDisplay_UpdatesWithUnitSystem()
    {
        var vm = new MainViewModel();
        vm.Car.TorqueNm = 400;

        vm.MeasurementSystem = UnitSystem.Metric;
        Assert.Equal(400, vm.TorqueDisplay);

        vm.MeasurementSystem = UnitSystem.Imperial;
        Assert.Equal(Math.Round(400 * 0.73756, 1), vm.TorqueDisplay);
    }

    [Fact]
    public void WheelbaseDisplay_UpdatesWithUnitSystem()
    {
        var vm = new MainViewModel();
        vm.Car.Wheelbase = 2700;

        vm.MeasurementSystem = UnitSystem.Metric;
        Assert.Equal(2700, vm.WheelbaseDisplay);

        vm.MeasurementSystem = UnitSystem.Imperial;
        Assert.Equal(Math.Round(2700 / 25.4, 1), vm.WheelbaseDisplay);
    }

    [Fact]
    public void SetMassDisplay_Imperial_ConvertsBack()
    {
        var vm = new MainViewModel();
        vm.MeasurementSystem = UnitSystem.Imperial;
        vm.MassDisplay = 3086; // ~1400 kg

        Assert.Equal(1400, Math.Round(vm.Car.TotalMass));
    }

    [Fact]
    public void UnitToggleLabel_SwitchesWithUnitSystem()
    {
        var vm = new MainViewModel();
        vm.MeasurementSystem = UnitSystem.Metric;
        var labelMetric = vm.UnitToggleLabel;

        vm.MeasurementSystem = UnitSystem.Imperial;
        Assert.NotEqual(labelMetric, vm.UnitToggleLabel);
    }

    [Fact]
    public void GenerateCommand_UpdatesStatus()
    {
        var vm = new MainViewModel();
        vm.GenerateCommand.Execute(null);

        Assert.NotNull(vm.TuneResult);
        Assert.True(vm.HasResult);
    }

    [Fact]
    public void NewProfileCommand_ResetsAll()
    {
        var vm = new MainViewModel();
        vm.Car.Make = "TestMake";
        vm.Car.Model = "TestModel";
        vm.TuneResult = new TuneResult();

        vm.NewProfileCommand.Execute(null);

        Assert.Empty(vm.Car.Make);
        Assert.Null(vm.TuneResult);
    }

    [Fact]
    public void ClearCarSelection_ClearsCarFields()
    {
        var vm = new MainViewModel();
        vm.Car.Make = "Toyota";
        vm.Car.Model = "Supra";
        vm.Car.Year = 2020;

        vm.ClearCarSelectionCommand.Execute(null);

        Assert.Empty(vm.Car.Make);
        Assert.Empty(vm.Car.Model);
        Assert.Equal(0, vm.Car.Year);
    }

    [Fact]
    public void HasLaunchControl_FalseByDefault()
    {
        var vm = new MainViewModel();
        Assert.False(vm.HasLaunchControl);
    }

    [Fact]
    public void HasLaunchControl_TrueWithDragResult()
    {
        var vm = new MainViewModel();
        vm.TuneResult = new TuneResult { LaunchControlRpm = 3500 };
        Assert.True(vm.HasLaunchControl);
    }

    [Fact]
    public void HasAWDFrontDiff_FalseForRWD()
    {
        var vm = new MainViewModel();
        vm.Car.DriveType = DriveType.RWD;
        vm.TuneResult = new TuneResult { CenterDiffBias = 50 };
        Assert.False(vm.HasAWDFrontDiff);
    }

    [Fact]
    public void HasAWDFrontDiff_TrueForAWDWithCenterBias()
    {
        var vm = new MainViewModel();
        vm.Car.DriveType = DriveType.AWD;
        vm.TuneResult = new TuneResult { CenterDiffBias = 50 };
        Assert.True(vm.HasAWDFrontDiff);
    }

    [Fact]
    public void TirePressureDisplay_Metric_Roundtrips()
    {
        var vm = new MainViewModel();
        vm.MeasurementSystem = UnitSystem.Metric;

        vm.TirePressureFrontMinDisplay = 1.5;
        Assert.Equal(1.5, vm.TirePressureFrontMinDisplay);
        Assert.True(Math.Abs(vm.Constraints.TirePressureFrontMin - 1.5) < 0.01);
    }

    [Fact]
    public void TirePressureDisplay_Imperial_Roundtrips()
    {
        var vm = new MainViewModel();
        vm.MeasurementSystem = UnitSystem.Imperial;

        vm.TirePressureFrontMinDisplay = 29.0; // ~2.0 bar
        var barValue = 29.0 / 14.504;
        Assert.True(Math.Abs(vm.Constraints.TirePressureFrontMin - barValue) < 0.1);

        var displayBack = Math.Round(barValue * 14.504, 1);
        Assert.Equal(29.0, displayBack);
    }

    [Fact]
    public void MaxRPMFieldLabel_ChangesWithPowertrain()
    {
        var vm = new MainViewModel();
        vm.Car.PowertrainType = PowertrainType.ICE;
        var iceLabel = vm.MaxRPMFieldLabel;

        vm.Car.PowertrainType = PowertrainType.Electric;
        Assert.NotEqual(iceLabel, vm.MaxRPMFieldLabel);
    }

    [Fact]
    public void LanguageOptions_HaveRussianAndEnglish()
    {
        var vm = new MainViewModel();
        Assert.Contains(vm.LanguageOptions, l => l.Code == "ru");
        Assert.Contains(vm.LanguageOptions, l => l.Code == "en");
    }

    [Fact]
    public void UnitSystemOptions_HaveAllThree()
    {
        var vm = new MainViewModel();
        Assert.Contains(vm.UnitSystemOptions, o => o.Value == UnitSystem.Metric);
        Assert.Contains(vm.UnitSystemOptions, o => o.Value == UnitSystem.Imperial);
    }

    [Fact]
    public void PowerUnitOptions_HaveAllThree()
    {
        var vm = new MainViewModel();
        Assert.Contains(vm.PowerUnitOptions, o => o.Value == PowerUnit.HP);
        Assert.Contains(vm.PowerUnitOptions, o => o.Value == PowerUnit.PS);
        Assert.Contains(vm.PowerUnitOptions, o => o.Value == PowerUnit.KW);
    }

    [Fact]
    public void SpringUnitOptions_HaveAllThree()
    {
        var vm = new MainViewModel();
        Assert.Contains(vm.SpringUnitOptions, o => o.Value == SpringUnit.KgfMm);
        Assert.Contains(vm.SpringUnitOptions, o => o.Value == SpringUnit.NMm);
        Assert.Contains(vm.SpringUnitOptions, o => o.Value == SpringUnit.LbsIn);
    }

    [Fact]
    public void RideHeightDisplay_Metric_Roundtrips()
    {
        var vm = new MainViewModel();
        vm.MeasurementSystem = UnitSystem.Metric;

        vm.RideHeightFrontMinDisplay = 50;
        Assert.Equal(50, vm.RideHeightFrontMinDisplay);
    }

    [Fact]
    public void RideHeightDisplay_Imperial_Roundtrips()
    {
        var vm = new MainViewModel();
        vm.MeasurementSystem = UnitSystem.Imperial;

        vm.RideHeightFrontMinDisplay = 2.0; // ~50.8 mm
        var mmValue = Math.Round(2.0 * 25.4, 0);
        Assert.Equal(mmValue, vm.Constraints.RideHeightFrontMin);
    }

    [Fact]
    public void SpringDisplay_NMm_Roundtrips()
    {
        var vm = new MainViewModel();
        vm.SpringUnit = SpringUnit.NMm;

        vm.SpringFrontMinDisplay = 100.0;
        Assert.True(Math.Abs(vm.Constraints.SpringFrontMin - 100.0) < 0.01);
    }

    [Fact]
    public void SpringDisplay_KgfMm_Roundtrips()
    {
        var vm = new MainViewModel();
        vm.SpringUnit = SpringUnit.KgfMm;

        vm.SpringFrontMinDisplay = 100.0; // kgf/mm display is scaled x10 => 100/10*9.807 = 98.07 N/mm
        Assert.True(Math.Abs(vm.Constraints.SpringFrontMin - 98.07) < 0.1);
    }

    [Fact]
    public void SelectedProfile_NullByDefault()
    {
        var vm = new MainViewModel();
        Assert.Null(vm.SelectedProfile);
    }

    [Fact]
    public void Profiles_NotNull()
    {
        var vm = new MainViewModel();
        Assert.NotNull(vm.Profiles);
    }

    [Fact]
    public void FilteredCarDatabase_NotNull()
    {
        var vm = new MainViewModel();
        Assert.NotNull(vm.FilteredCarDatabase);
    }

    [Fact]
    public void IsAutoGenerate_AlwaysTrue()
    {
        var vm = new MainViewModel();
        Assert.True(vm.IsAutoGenerate);
    }

    [Fact]
    public void IsGenerating_FalseByDefault()
    {
        var vm = new MainViewModel();
        Assert.False(vm.IsGenerating);
    }

    [Fact]
    public void IsGenerating_True_SetsIsBusy()
    {
        var vm = new MainViewModel();
        vm.IsLoadingCars = false;
        vm.IsGenerating = true;
        Assert.True(vm.IsBusy);
        vm.IsGenerating = false;
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public void IsLoadingCars_True_SetsIsBusy()
    {
        var vm = new MainViewModel();
        vm.IsLoadingCars = true;
        Assert.True(vm.IsBusy);
        vm.IsLoadingCars = false;
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public void BusyMessage_DefaultEmpty()
    {
        var vm = new MainViewModel();
        Assert.Equal("", vm.BusyMessage);
    }

    [Fact]
    public void BusyMessage_Roundtrips()
    {
        var vm = new MainViewModel();
        vm.BusyMessage = "Loading...";
        Assert.Equal("Loading...", vm.BusyMessage);
    }

    [Fact]
    public void StatusMessage_DefaultNotEmpty()
    {
        var vm = new MainViewModel();
        Assert.NotNull(vm.StatusMessage);
        Assert.NotEmpty(vm.StatusMessage);
    }

    [Fact]
    public void StatusMessage_Roundtrips()
    {
        var vm = new MainViewModel();
        vm.StatusMessage = "Custom status";
        Assert.Equal("Custom status", vm.StatusMessage);
    }

    [Fact]
    public void SelectedCarDisplayText_MatchesCar()
    {
        var vm = new MainViewModel();
        vm.Car.Make = "Toyota";
        vm.Car.Model = "Supra";
        vm.Car.Year = 2024;

        Assert.Equal("2024 Toyota Supra", vm.SelectedCarDisplayText);
    }

    [Fact]
    public void SelectedCarDisplayText_WithSelectedCar()
    {
        var vm = new MainViewModel();
        vm.Car.Make = "Manual";
        vm.Car.Model = "Entry";
        vm.Car.Year = 0;

        Assert.Equal("0 Manual Entry", vm.SelectedCarDisplayText);
    }

    [Fact]
    public void CarSearchText_DefaultEmpty()
    {
        var vm = new MainViewModel();
        Assert.Equal("", vm.CarSearchText);
    }

    [Fact]
    public void CarSearchText_Roundtrips()
    {
        var vm = new MainViewModel();
        vm.CarSearchText = "Supra";
        Assert.Equal("Supra", vm.CarSearchText);
    }

    [Fact]
    public void MaxRPMFieldLabel_NotNull()
    {
        var vm = new MainViewModel();
        Assert.NotNull(vm.MaxRPMFieldLabel);
        Assert.NotEmpty(vm.MaxRPMFieldLabel);
    }

    [Fact]
    public void SaveCommand_SavesProfile()
    {
        var vm = new MainViewModel();
        vm.Car.Make = "TestMake";
        vm.Car.Model = "TestModel";
        vm.Car.Year = 2024;

        vm.SaveCommand.Execute(null);

        Assert.NotEmpty(vm.Profiles);
        Assert.Contains(vm.Profiles, p => p.Contains("TestMake") && p.Contains("TestModel"));
    }

    [Fact]
    public void SaveCommand_SavesAndStatusUpdated()
    {
        var vm = new MainViewModel();
        vm.Car.Make = "StatusTest";
        vm.Car.Model = "Car";

        vm.SaveCommand.Execute(null);

        Assert.NotEqual("", vm.StatusMessage);
    }

    [Fact]
    public void SaveProfile_AutoName()
    {
        var vm = new MainViewModel();
        vm.Car.Make = "SaveNameMake";
        vm.Car.Model = "SaveNameModel";
        vm.Car.Year = 2024;

        vm.SaveCommand.Execute(null);

        var name = vm.Car.Name;
        Assert.Contains("SaveNameMake", name);
        Assert.Contains("SaveNameModel", name);
        Assert.Contains("2024", name);
    }

    [Fact]
    public void SetSelectedProfile_ToNull_DoesNotThrow()
    {
        var vm = new MainViewModel();
        vm.SelectedProfile = null;
    }

    [Fact]
    public void ToggleUnits_SwitchesLabel()
    {
        var vm = new MainViewModel();
        vm.MeasurementSystem = UnitSystem.Metric;
        var metricLabel = vm.UnitToggleLabel;

        vm.MeasurementSystem = UnitSystem.Imperial;
        var imperialLabel = vm.UnitToggleLabel;

        Assert.NotEqual(metricLabel, imperialLabel);
    }

    [Fact]
    public void SaveCommand_SavesProfileWithVersion()
    {
        var vm = new MainViewModel();
        vm.Car.Make = "VersionCheck";
        vm.Car.Model = "Car";
        vm.Car.Year = 2024;

        vm.SaveCommand.Execute(null);

        var storage = new StorageService();
        var name = vm.Profiles.FirstOrDefault(p => p.Contains("VersionCheck"));
        Assert.NotNull(name);
        var loaded = storage.Load(name);
        Assert.NotNull(loaded);
        Assert.Equal(SavedProfile.ProfileVersion, loaded.Version);
    }

    [Fact]
    public void RecalculateOutdatedProfiles_OnConstruction_UpdatesOldProfile()
    {
        // Seed a profile with no Version (simulating old format)
        var storage = new StorageService();
        storage.Save("OldProfileNoVersion", new SavedProfile
        {
            Car = CarFactory.DefaultCar(),
            Track = CarFactory.DefaultTrack(),
            Constraints = CarFactory.RelaxedConstraints(),
        });

        var vm = new MainViewModel();

        var loaded = storage.Load("OldProfileNoVersion");
        Assert.NotNull(loaded);
        Assert.Equal(SavedProfile.ProfileVersion, loaded.Version);
        Assert.NotNull(loaded.LastResult);
    }

    [Fact]
    public void RecalculateOutdatedProfiles_OnConstruction_SkipsCurrentVersion()
    {
        // Seed a profile already at current version
        var storage = new StorageService();
        storage.Save("CurrentVersionProfile", new SavedProfile
        {
            Car = CarFactory.DefaultCar(),
            Track = CarFactory.DefaultTrack(),
            Constraints = CarFactory.RelaxedConstraints(),
            Version = SavedProfile.ProfileVersion,
        });

        var vm = new MainViewModel();

        var loaded = storage.Load("CurrentVersionProfile");
        Assert.NotNull(loaded);
        Assert.Equal(SavedProfile.ProfileVersion, loaded.Version);
        // LastResult should still be null because it wasn't recalculated
        Assert.Null(loaded.LastResult);
    }

    [Fact]
    public void RecalculateOutdatedProfiles_OnConstruction_UpdatesMultipleOldProfiles()
    {
        var storage = new StorageService();
        var car = CarFactory.DefaultCar();
        var track = CarFactory.DefaultTrack();
        var constraints = CarFactory.RelaxedConstraints();

        storage.Save("Old1", new SavedProfile { Car = car, Track = track, Constraints = constraints });
        storage.Save("Old2", new SavedProfile { Car = car, Track = track, Constraints = constraints });
        storage.Save("Current", new SavedProfile { Car = car, Track = track, Constraints = constraints, Version = SavedProfile.ProfileVersion });

        var vm = new MainViewModel();

        var old1 = storage.Load("Old1");
        var old2 = storage.Load("Old2");
        var current = storage.Load("Current");

        Assert.Equal(SavedProfile.ProfileVersion, old1!.Version);
        Assert.Equal(SavedProfile.ProfileVersion, old2!.Version);
        Assert.NotNull(old1.LastResult);
        Assert.NotNull(old2.LastResult);

        // Current version profile should NOT have been touched
        Assert.Null(current!.LastResult);
        Assert.Equal(SavedProfile.ProfileVersion, current.Version);
    }
}


