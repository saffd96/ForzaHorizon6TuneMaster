using System;
using System.IO;

namespace Forza_Horizon_6_Tune_Master.Services;

public static class ForzaPaths
{
    private static string? _testRoot;

    public static IDisposable SetTestRoot(string root)
    {
        var previous = _testRoot;
        _testRoot = root;
        return new TestRootRestorer(previous);
    }

    private sealed class TestRootRestorer(string? previous) : IDisposable
    {
        public void Dispose() => _testRoot = previous;
    }

    private static string BasePath =>
        _testRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ForzaTuneMaster");

    public static string BaseDir => BasePath;
    public static string ProfilesDir => Path.Combine(BasePath, "profiles");
    public static string SpecsDir => Path.Combine(BasePath, "specs");
    public static string SpecsCacheDir => Path.Combine(BasePath, "specs_cache");
    public static string OverridesPath => Path.Combine(BasePath, "specs_overrides.json");
    public static string CachePath => Path.Combine(BasePath, "fh6_cars_fandom.json");
    public static string LegacyCachePath => Path.Combine(BasePath, "fh6_cars_all.json");
    public static string SettingsPath => Path.Combine(BasePath, "settings.json");
}
