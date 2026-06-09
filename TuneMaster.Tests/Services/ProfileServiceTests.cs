using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;
using TuneMaster.Tests.Helpers;

namespace TuneMaster.Tests.Services;

[Collection("FileSystem")]
public class ProfileServiceTests : IDisposable
{
    private readonly TestingEnvironment _testEnv = new();
    private readonly ProfileService _service = new(new StorageService());

    public void Dispose()
    {
        _testEnv.Dispose();
    }

    [Fact]
    public void Save_SetsVersion()
    {
        var car = CarFactory.DefaultCar();
        var track = CarFactory.DefaultTrack();
        var constraints = CarFactory.RelaxedConstraints();

        string name = _service.Save(car, track, constraints, null, new List<string>());

        var loaded = _service.Load(name);
        Assert.NotNull(loaded);
        Assert.Equal(SavedProfile.ProfileVersion, loaded.Version);
    }

    [Fact]
    public void Save_SetsVersion_WithResult()
    {
        var car = CarFactory.DefaultCar();
        var track = CarFactory.DefaultTrack();
        var constraints = CarFactory.RelaxedConstraints();
        var result = new TuneResult();

        string name = _service.Save(car, track, constraints, result, new List<string>());

        var loaded = _service.Load(name);
        Assert.NotNull(loaded);
        Assert.Equal(SavedProfile.ProfileVersion, loaded.Version);
        Assert.NotNull(loaded.LastResult);
    }

    [Fact]
    public void Load_OldProfile_ReturnsNullVersion()
    {
        var storage = new StorageService();
        var old = new SavedProfile { Car = CarFactory.DefaultCar() };
        storage.Save("OldFormat", old);

        var loaded = _service.Load("OldFormat");
        Assert.NotNull(loaded);
        Assert.Null(loaded.Version);
    }
}
