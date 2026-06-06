using System.Text.Json;
using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;
using System.IO;

namespace TuneMaster.Tests.Services;

[Collection("CarDatabase")]
public class CarDatabaseServiceTests : IDisposable
{
    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ForzaTuneMaster");
    private static readonly string CachePath = Path.Combine(CacheDir, "fh6_cars_fandom.json");
    private static readonly string LegacyPath = Path.Combine(CacheDir, "fh6_cars_all.json");
    private static readonly object Lock = new();

    public CarDatabaseServiceTests()
    {
        Cleanup();
    }

    public void Dispose()
    {
        Cleanup();
    }

    private static void Cleanup()
    {
        try { if (File.Exists(CachePath)) File.Delete(CachePath); } catch { }
        try { if (File.Exists(LegacyPath)) File.Delete(LegacyPath); } catch { }
    }

    private static void WriteCache(List<CarData> cars)
    {
        Directory.CreateDirectory(CacheDir);
        File.WriteAllText(CachePath, JsonSerializer.Serialize(new List<CarData>(cars)));
    }

    [Fact]
    public async Task LoadCarDatabase_FromCache_ReturnsCars()
    {
        WriteCache(new List<CarData>
        {
            new() { Year = 2024, Make = "Toyota", Model = "Supra", PI = 800 },
            new() { Year = 2023, Make = "Nissan", Model = "GT-R", PI = 850 },
        });

        var svc = new CarDatabaseService();
        var result = await svc.LoadCarDatabaseAsync();

        Assert.True(result.FromCache);
        Assert.Null(result.WebErrorMessage);
        Assert.Equal(2, result.Cars.Count);
        Assert.Contains(result.Cars, c => c.Make == "Toyota" && c.Model == "Supra");
        Assert.Contains(result.Cars, c => c.Make == "Nissan" && c.Model == "GT-R");
    }

    [Fact]
    public async Task LoadCarDatabase_FromCache_CarPropertiesPreserved()
    {
        WriteCache(new List<CarData>
        {
            new() { Year = 2020, Make = "Honda", Model = "Civic Type R", PI = 650, WikiPageTitle = "Honda Civic Type R" },
        });

        var svc = new CarDatabaseService();
        var result = await svc.LoadCarDatabaseAsync();
        var car = result.Cars[0];

        Assert.Equal(2020, car.Year);
        Assert.Equal("Honda", car.Make);
        Assert.Equal("Civic Type R", car.Model);
        Assert.Equal(650, car.PI);
        Assert.Equal("Honda Civic Type R", car.WikiPageTitle);
    }

    [Fact]
    public async Task LoadCarDatabase_EmptyCache_ReturnsEmpty()
    {
        WriteCache(new List<CarData>());
        var svc = new CarDatabaseService();
        var result = await svc.LoadCarDatabaseAsync();

        Assert.Empty(result.Cars);
        Assert.True(result.FromCache);
    }

    [Fact]
    public async Task LoadCarDatabase_InvalidCache_ReturnsEmpty()
    {
        Directory.CreateDirectory(CacheDir);
        File.WriteAllText(CachePath, "invalid json content");
        var svc = new CarDatabaseService();
        var result = await svc.LoadCarDatabaseAsync();

        Assert.Empty(result.Cars);
    }

    [Fact]
    public void DeleteCache_RemovesFiles()
    {
        Directory.CreateDirectory(CacheDir);
        File.WriteAllText(CachePath, "test");
        File.WriteAllText(LegacyPath, "test");

        CarDatabaseService.DeleteCache();

        Assert.False(File.Exists(CachePath));
        Assert.False(File.Exists(LegacyPath));
    }

    [Fact]
    public void DeleteCache_NoFiles_DoesNotThrow()
    {
        Cleanup();
        CarDatabaseService.DeleteCache();
    }

    [Fact]
    public void IsCacheStale_WhenNoCache()
    {
        Cleanup();
        Assert.True(CarDatabaseService.IsCacheStale);
    }

    [Fact]
    public void IsCacheStale_WhenFileExists_ReturnsBasedOnDate()
    {
        WriteCache(new List<CarData>());
        var stale = CarDatabaseService.IsCacheStale;
        Assert.False(stale);
    }

    [Fact]
    public async Task LoadCarDatabase_LargeCache_AllItemsLoaded()
    {
        var cars = new List<CarData>();
        for (int i = 0; i < 100; i++)
            cars.Add(new() { Year = 2000 + i, Make = "Make" + i, Model = "Model" + i });
        WriteCache(cars);

        var svc = new CarDatabaseService();
        var result = await svc.LoadCarDatabaseAsync();

        Assert.Equal(100, result.Cars.Count);
    }

    [Fact]
    public async Task LoadCarDatabase_MinimalCarData_Handled()
    {
        WriteCache(new List<CarData>
        {
            new() { Year = 2024, Make = "Test", Model = "Car" },
        });

        var svc = new CarDatabaseService();
        var result = await svc.LoadCarDatabaseAsync();

        Assert.Single(result.Cars);
        Assert.Equal(0, result.Cars[0].PI);
        Assert.Empty(result.Cars[0].WikiPageTitle);
    }

    [Fact]
    public async Task LoadCarDatabase_MultipleEntries_SortedByListOrder()
    {
        var cars = new List<CarData>
        {
            new() { Year = 2024, Make = "Zebra", Model = "Car" },
            new() { Year = 2024, Make = "Alpha", Model = "Car" },
        };
        WriteCache(cars);

        var svc = new CarDatabaseService();
        var result = await svc.LoadCarDatabaseAsync();

        Assert.Equal(2, result.Cars.Count);
        Assert.Equal("Zebra", result.Cars[0].Make);
        Assert.Equal("Alpha", result.Cars[1].Make);
    }
}

[CollectionDefinition("CarDatabase", DisableParallelization = true)]
public class CarDatabaseTestCollection { }
