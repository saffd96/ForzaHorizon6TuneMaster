using System;
using System.IO;
using System.Threading;

namespace Forza_Horizon_6_Tune_Master.Services;

public static class ForzaPaths
{
    private static readonly AsyncLocal<string?> _testRoot = new();

    public static IDisposable SetTestRoot(string root)
    {
        var previous = _testRoot.Value;
        _testRoot.Value = root;
        return new TestRootRestorer(previous);
    }

    private sealed class TestRootRestorer(string? previous) : IDisposable
    {
        public void Dispose() => _testRoot.Value = previous;
    }

    private static string BasePath =>
        _testRoot.Value ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ForzaTuneMaster");

    public static string BaseDir => BasePath;
    public static string ProfilesDir => Path.Combine(BasePath, "profiles");
    public static string CachePath => Path.Combine(BasePath, "fh6_cars_fandom.json");
    public static string SettingsPath => Path.Combine(BasePath, "settings.json");
}
