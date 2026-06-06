using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;

namespace TuneMaster.Tests.Services;

[Collection("StorageService")]
public class StorageServiceTests : IDisposable
{
    private readonly StorageService _storage = new();
    private readonly List<string> _existingNames;

    public StorageServiceTests()
    {
        _existingNames = _storage.GetProfileNames();
    }

    public void Dispose()
    {
        foreach (var name in _storage.GetProfileNames())
        {
            if (!_existingNames.Contains(name))
            {
                _storage.Delete(name);
            }
        }
    }

    [Fact]
    public void SaveAndLoad_Roundtrip_ViaGetProfileNames()
    {
        var profile = new SavedProfile { Car = new CarCard { Make = "RoundtripMake" } };
        _storage.Save("RoundtripTest", profile);

        var names = _storage.GetProfileNames();
        var loaded = _storage.Load(names[0]);

        Assert.NotNull(loaded);
        Assert.Equal("RoundtripMake", loaded.Car.Make);
    }

    [Fact]
    public void GetProfileNames_UnderscoreName_FileNamePreserved()
    {
        _storage.Save("My_Profile", new SavedProfile { Car = new CarCard { Make = "Test" } });
        var names = _storage.GetProfileNames();

        Assert.Contains("My_Profile", names);
    }

    [Fact]
    public void GetProfileNames_UnderscoreName_LoadWorks()
    {
        _storage.Save("My_Profile", new SavedProfile { Car = new CarCard { Make = "UnderscoreTest" } });
        var names = _storage.GetProfileNames();
        var loaded = _storage.Load(names[0]);

        Assert.NotNull(loaded);
        Assert.Equal("UnderscoreTest", loaded.Car.Make);
    }

    [Fact]
    public void GetProfileNames_UnderscoreName_DeleteWorks()
    {
        _storage.Save("My_Profile", new SavedProfile());
        var names = _storage.GetProfileNames();
        _storage.Delete(names[0]);

        Assert.Null(_storage.Load("My_Profile"));
    }

    [Fact]
    public void Save_DoubleUnderscore_Roundtrips()
    {
        _storage.Save("A__B", new SavedProfile { Car = new CarCard { Make = "Double" } });
        var names = _storage.GetProfileNames();

        Assert.Contains("A__B", names);
        var loaded = _storage.Load(names[0]);
        Assert.Equal("Double", loaded!.Car.Make);
    }

    [Fact]
    public void Save_InvalidChars_ViaGetProfileNames_LoadWorks()
    {
        _storage.Save("Test:Profile/With*Bad?Chars",
            new SavedProfile { Car = new CarCard { Make = "InvalidChars" } });
        var names = _storage.GetProfileNames();
        // After fix: filename is "Test_Profile_With_Bad_Chars.json" → listed as "Test_Profile_With_Bad_Chars"
        var loaded = _storage.Load(names[0]);

        Assert.NotNull(loaded);
        Assert.Equal("InvalidChars", loaded.Car.Make);
    }

    [Fact]
    public void Save_InvalidChars_DeleteWorks()
    {
        _storage.Save("Bad:Name?", new SavedProfile());
        var names = _storage.GetProfileNames();
        _storage.Delete(names[0]);

        Assert.Null(_storage.Load("Bad:Name?"));
    }

    [Fact]
    public void Save_UnicodeName_Roundtrips()
    {
        _storage.Save("Тестовый_Профиль", new SavedProfile { Car = new CarCard { Make = "Unicode" } });
        var names = _storage.GetProfileNames();

        Assert.Contains("Тестовый_Профиль", names);
        var loaded = _storage.Load(names[0]);
        Assert.Equal("Unicode", loaded!.Car.Make);
    }

    [Fact]
    public void Save_SpacesInName_Roundtrips()
    {
        _storage.Save("My Profile With Spaces", new SavedProfile { Car = new CarCard { Make = "Spaces" } });
        var names = _storage.GetProfileNames();

        Assert.Contains("My Profile With Spaces", names);
        var loaded = _storage.Load(names[0]);
        Assert.Equal("Spaces", loaded!.Car.Make);
    }

    [Fact]
    public void Save_MultipleProfiles_GetProfileNames_ReturnsAll()
    {
        _storage.Save("Profile_A", new SavedProfile());
        _storage.Save("Profile_B", new SavedProfile());
        _storage.Save("Profile_C", new SavedProfile());

        var names = _storage.GetProfileNames();

        Assert.Contains("Profile_A", names);
        Assert.Contains("Profile_B", names);
        Assert.Contains("Profile_C", names);
    }

    [Fact]
    public void Delete_Nonexistent_DoesNotThrow()
    {
        _storage.Delete("DoesNotExist_" + Guid.NewGuid());
    }

    [Fact]
    public void Save_Then_GetProfileNames_ReturnsExactMatch()
    {
        var name = "ExactMatch_" + Guid.NewGuid();
        _storage.Save(name, new SavedProfile { Car = new CarCard { Make = "Exact" } });

        var names = _storage.GetProfileNames();
        var match = names.FirstOrDefault(n => n == name);

        Assert.NotNull(match);
        Assert.Equal(name, match);
    }

    [Fact]
    public void Save_LeadingTrailingSpaces_Preserved()
    {
        _storage.Save("  Spaced  ", new SavedProfile { Car = new CarCard { Make = "Spaced" } });
        var names = _storage.GetProfileNames();

        Assert.Contains("  Spaced  ", names);
        var loaded = _storage.Load("  Spaced  ");
        Assert.Equal("Spaced", loaded!.Car.Make);
    }

    [Fact]
    public void SaveAndLoad_Roundtrip()
    {
        var car = new CarCard { Make = "Test", Model = "Car", Year = 2024 };
        var profile = new SavedProfile
        {
            Car = car,
            Track = new TrackInfo { Discipline = Discipline.Road },
            Constraints = new TuningConstraints(),
            LastResult = new TuneResult { TirePressureFront = 2.5 },
            AiEstimatedFields = new List<string> { "Wheelbase" },
        };

        _storage.Save("TestProfile", profile);
        var loaded = _storage.Load("TestProfile");

        Assert.NotNull(loaded);
        Assert.Equal("Test", loaded.Car.Make);
        Assert.Equal("Car", loaded.Car.Model);
        Assert.Equal(Discipline.Road, loaded.Track.Discipline);
        Assert.Equal(2.5, loaded.LastResult!.TirePressureFront);
        Assert.Contains("Wheelbase", loaded.AiEstimatedFields);
    }

    [Fact]
    public void Load_NonExistent_ReturnsNull()
    {
        var loaded = _storage.Load("NonExistentProfile_" + Guid.NewGuid());
        Assert.Null(loaded);
    }

    [Fact]
    public void GetProfileNames_EmptyInitially()
    {
        var names = _storage.GetProfileNames();
        Assert.NotNull(names);
    }

    [Fact]
    public void Save_Then_GetProfileNames_Contains()
    {
        _storage.Save("UniqueProfile_" + Guid.NewGuid(),
            new SavedProfile { Car = new CarCard { Make = "Test" } });

        var names = _storage.GetProfileNames();
        Assert.NotEmpty(names);
    }

    [Fact]
    public void Delete_RemovesProfile()
    {
        var name = "ToDelete_" + Guid.NewGuid();
        _storage.Save(name, new SavedProfile());
        _storage.Delete(name);

        var loaded = _storage.Load(name);
        Assert.Null(loaded);
    }

    [Fact]
    public void Save_HandlesInvalidFileNameChars()
    {
        _storage.Save("Test:Profile/With*Bad?Chars",
            new SavedProfile { Car = new CarCard { Make = "Test" } });

        var loaded = _storage.Load("Test:Profile/With*Bad?Chars");
        Assert.NotNull(loaded);
    }

    [Fact]
    public void MultipleProfiles_Independent()
    {
        var p1 = new SavedProfile { Car = new CarCard { Make = "Make1" } };
        var p2 = new SavedProfile { Car = new CarCard { Make = "Make2" } };

        _storage.Save("Profile1", p1);
        _storage.Save("Profile2", p2);

        var l1 = _storage.Load("Profile1");
        var l2 = _storage.Load("Profile2");

        Assert.NotNull(l1);
        Assert.NotNull(l2);
        Assert.Equal("Make1", l1.Car.Make);
        Assert.Equal("Make2", l2.Car.Make);
    }
}

[CollectionDefinition("StorageService", DisableParallelization = true)]
public class StorageServiceTestCollection { }
