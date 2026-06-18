using System.Text.Json;
using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;
using System.IO;
using TuneMaster.Tests;

namespace TuneMaster.Tests.Services;

[Collection("FileSystem")]
public class CarDatabaseServiceTests : IDisposable
{
    private readonly TestingEnvironment _testEnv = new();

    public void Dispose()
    {
        _testEnv.Dispose();
    }

    private static void WriteCache(List<CarData> cars)
    {
        var path = ForzaPaths.CachePath;
        Directory.CreateDirectory(ForzaPaths.BaseDir);
        File.WriteAllText(path, JsonSerializer.Serialize(new List<CarData>(cars)));
    }

    [Fact(Skip = "CarDatabaseService replaced")]
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

    [Fact(Skip = "CarDatabaseService replaced")]
    public async Task LoadCarDatabase_FromCache_CarPropertiesPreserved()
    {
        WriteCache(new List<CarData>
        {
            new() { Year = 2020, Make = "Honda", Model = "Civic Type R", PI = 650 },
        });

        var svc = new CarDatabaseService();
        var result = await svc.LoadCarDatabaseAsync();
        var car = result.Cars[0];

        Assert.Equal(2020, car.Year);
        Assert.Equal("Honda", car.Make);
        Assert.Equal("Civic Type R", car.Model);
        Assert.Equal(650, car.PI);
    }

    [Fact(Skip = "CarDatabaseService replaced")]
    public async Task LoadCarDatabase_EmptyCache_ReturnsEmpty()
    {
        WriteCache(new List<CarData>());
        var svc = new CarDatabaseService();
        var result = await svc.LoadCarDatabaseAsync();

        Assert.Empty(result.Cars);
        Assert.True(result.FromCache);
    }

    [Fact(Skip = "CarDatabaseService replaced")]
    public async Task LoadCarDatabase_InvalidCache_ReturnsEmpty()
    {
        Directory.CreateDirectory(ForzaPaths.BaseDir);
        File.WriteAllText(ForzaPaths.CachePath, "invalid json content");
        var svc = new CarDatabaseService();
        var result = await svc.LoadCarDatabaseAsync();

        Assert.Empty(result.Cars);
    }

    [Fact(Skip = "CarDatabaseService replaced")]
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

    [Fact(Skip = "CarDatabaseService replaced")]
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
    }

    [Fact(Skip = "CarDatabaseService replaced")]
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


