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

    // Downloaded update files (from Supabase), scoped per app version so an upgrade
    // never inherits an older version's cached DB/localization override, and so an
    // older still-running exe never picks up data shaped for a newer version's schema.
    public static string UpdateVersionDir => Path.Combine(BasePath, SavedProfile.ProfileVersion);
    public static string UpdateDbPath => Path.Combine(UpdateVersionDir, "fh6_db.sqlite");
    public static string UpdateLocDir => Path.Combine(UpdateVersionDir, "Localization");

    // Marker: set when the user agreed to restart-and-update; consumed on next launch.
    public static string PendingUpdatePath => Path.Combine(BasePath, ".pending_update");
}
