using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using Forza_Horizon_6_Tune_Master.Services;
using TuneMaster.Tests.Helpers;

namespace TuneMaster.Tests.Services;

public sealed class UpdateServiceTests : IDisposable
{
    private readonly TestingEnvironment _env = new();
    private readonly MockHttpMessageHandler _handler = new();

    public UpdateServiceTests()
    {
        UpdateService.SetHttpClient(new HttpClient(_handler));
    }

    public void Dispose()
    {
        UpdateService.SetHttpClient(new HttpClient());
    }

    [Fact]
    public async Task CheckAsync_RemoteHashMatchesLocalAppData_ReturnsFalse()
    {
        byte[] localBytes = [1, 2, 3, 4];
        string localHash = ComputeHexHash(localBytes);
        Directory.CreateDirectory(ForzaPaths.UpdateVersionDir);
        File.WriteAllBytes(ForzaPaths.UpdateDbPath, localBytes);

        _handler.Handler = req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("fh6_db.sha256"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(localHash) };
            if (req.RequestUri!.AbsolutePath.EndsWith("loc.sha256"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(localHash) };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };

        var (db, _) = await UpdateService.Instance.CheckAsync();

        Assert.False(db);
    }

    [Fact]
    public async Task CheckAsync_DbHashDiffers_ReturnsDbOutdated()
    {
        byte[] localBytes = [1, 2, 3, 4];
        string differentHash = "0000000000000000000000000000000000000000000000000000000000000000";
        Directory.CreateDirectory(ForzaPaths.UpdateVersionDir);
        File.WriteAllBytes(ForzaPaths.UpdateDbPath, localBytes);

        _handler.Handler = req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("fh6_db.sha256"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(differentHash) };
            if (req.RequestUri!.AbsolutePath.EndsWith("loc.sha256"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("e5f6a1b2") };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };

        var (db, _) = await UpdateService.Instance.CheckAsync();

        Assert.True(db);
    }

    [Fact]
    public async Task CheckAsync_HttpError_Throws()
    {
        Directory.CreateDirectory(ForzaPaths.UpdateVersionDir);
        File.WriteAllBytes(ForzaPaths.UpdateDbPath, [1, 2, 3, 4]);
        _handler.Handler = _ => throw new HttpRequestException("No network");

        await Assert.ThrowsAsync<HttpRequestException>(() => UpdateService.Instance.CheckAsync());
    }

    [Fact]
    public async Task DownloadDbAsync_WritesFile_AndVerifiesHash()
    {
        byte[] fakeDb = "fake sqlite content from supabase"u8.ToArray();
        string hash = ComputeHexHash(fakeDb);

        _handler.Handler = req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith(".sha256"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(hash) };
            if (req.RequestUri!.AbsolutePath.EndsWith(".sqlite"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(fakeDb) };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };

        await UpdateService.Instance.DownloadDbAsync();

        Assert.True(File.Exists(ForzaPaths.UpdateDbPath));
        Assert.Equal(fakeDb, File.ReadAllBytes(ForzaPaths.UpdateDbPath));
    }

    [Fact]
    public async Task DownloadDbAsync_CorruptedFile_Throws()
    {
        string remoteHash = "a".Repeat(64);
        byte[] fakeDb = "wrong content"u8.ToArray();

        _handler.Handler = req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith(".sha256"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(remoteHash) };
            if (req.RequestUri!.AbsolutePath.EndsWith(".sqlite"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(fakeDb) };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };

        await Assert.ThrowsAsync<InvalidDataException>(() => UpdateService.Instance.DownloadDbAsync());
    }

    [Fact]
    public async Task DownloadLocAsync_WritesFiles()
    {
        string uiJson = """{"key": "value"}""";
        string gameJson = """{}""";
        var names = GetBundledLocalizationNames();
        var contents = names.ToDictionary(
            name => name,
            name => name.StartsWith("GameStrings.", StringComparison.Ordinal) ? gameJson : uiJson,
            StringComparer.Ordinal);

        // Mirror DownloadLocAsync's dynamically discovered embedded-resource list.
        var combined = new List<byte>();
        foreach (var name in names)
            combined.AddRange(System.Text.Encoding.UTF8.GetBytes(contents[name]));
        string locHash = ComputeHexHash(combined.ToArray());

        _handler.Handler = req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.EndsWith("loc.sha256"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(locHash) };
            string name = Path.GetFileName(path);
            if (contents.TryGetValue(name, out string? content))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };

        await UpdateService.Instance.DownloadLocAsync();

        Assert.True(File.Exists(Path.Combine(ForzaPaths.UpdateLocDir, "en.json")));
        Assert.True(File.Exists(Path.Combine(ForzaPaths.UpdateLocDir, "ru.json")));
        Assert.True(File.Exists(Path.Combine(ForzaPaths.UpdateLocDir, "fr.json")));
        Assert.Equal(uiJson, File.ReadAllText(Path.Combine(ForzaPaths.UpdateLocDir, "en.json")));
    }

    [Fact]
    public async Task DownloadLocAsync_HashMismatch_ThrowsAndWritesNoFiles()
    {
        string enJson = """{"key": "value"}""";

        _handler.Handler = req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.EndsWith("loc.sha256"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("0".Repeat(64)) };
            if (path.EndsWith(".json"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(enJson) };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };

        await Assert.ThrowsAsync<InvalidDataException>(() => UpdateService.Instance.DownloadLocAsync());

        Assert.False(File.Exists(Path.Combine(ForzaPaths.UpdateLocDir, "en.json")));
        Assert.False(File.Exists(Path.Combine(ForzaPaths.UpdateLocDir, "ru.json")));
        Assert.False(Directory.Exists(ForzaPaths.UpdateLocDir) &&
            Directory.GetFiles(ForzaPaths.UpdateLocDir, "*.tmp").Length > 0);
    }

    [Fact]
    public void PendingUpdateMark_RoundTrips()
    {
        Assert.False(UpdateService.IsPendingUpdateMarked());

        UpdateService.MarkPendingUpdate();
        Assert.True(UpdateService.IsPendingUpdateMarked());

        UpdateService.ClearPendingUpdateMark();
        Assert.False(UpdateService.IsPendingUpdateMarked());
    }

    [Fact]
    public void UpdateDbPath_PointsToVersionedDir()
    {
        Assert.Equal(Path.Combine(ForzaPaths.UpdateVersionDir, "fh6_db.sqlite"), ForzaPaths.UpdateDbPath);
        Assert.Equal(Path.Combine(ForzaPaths.BaseDir, SavedProfile.ProfileVersion), ForzaPaths.UpdateVersionDir);
    }

    [Fact]
    public void UpdateLocDir_PointsToVersionedDir()
    {
        Assert.Equal(Path.Combine(ForzaPaths.UpdateVersionDir, "Localization"), ForzaPaths.UpdateLocDir);
    }

    // Cross-checks DUMPER/publish_update.ps1's hashing algorithm (loose Localization/*.json
    // files, concatenated in [StringComparer]::Ordinal filename order) against what
    // UpdateService actually computes at runtime from the assembly's embedded resources
    // (GetLocalizationFileNames, same Ordinal order). If these two ever diverge - a locale
    // added to one side but not the other, or a differently-cased sort - the bucket's
    // loc.sha256 stops matching what the app expects and CheckAsync reports every install as
    // outdated forever. Guards exactly the bug this pair was written to fix: French was
    // embedded in the .csproj but the publish script's sort silently produced a different file
    // order than the app on Object[] arrays from the pipeline, so the two hashes disagreed.
    [Fact]
    public async Task LocHash_MatchesWhatPublishScriptWouldComputeFromSourceFiles()
    {
        string repoRoot = FindRepoRoot();
        string locDir = Path.Combine(repoRoot, "Forza Horizon 6 Tune Master", "Localization");
        var names = Directory.GetFiles(locDir, "*.json")
            .Select(Path.GetFileName)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        Assert.True(names.Length >= 6, "Expected at least en/ru/fr + GameStrings.en/ru/fr in Localization/");

        var combined = new List<byte>();
        foreach (var name in names)
            combined.AddRange(File.ReadAllBytes(Path.Combine(locDir, name!)));
        string diskHash = ComputeHexHash(combined.ToArray());

        _handler.Handler = req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.EndsWith("loc.sha256"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(diskHash) };
            if (path.EndsWith("fh6_db.sha256"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("irrelevant") };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };

        var (_, locOutdated) = await UpdateService.Instance.CheckAsync();

        Assert.False(locOutdated);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && dir.GetFiles("*.sln").Length == 0)
            dir = dir.Parent;
        if (dir == null) throw new DirectoryNotFoundException("Could not find repo root (.sln) above " + AppContext.BaseDirectory);
        return dir.FullName;
    }

    private static string[] GetBundledLocalizationNames() =>
        typeof(UpdateService).Assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith("Forza_Horizon_6_Tune_Master.Localization.", StringComparison.Ordinal)
                           && name.EndsWith(".json", StringComparison.Ordinal))
            .Select(name => name["Forza_Horizon_6_Tune_Master.Localization.".Length..])
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

    private static string ComputeHexHash(byte[] data) =>
        Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
}

internal static class StringExtensions
{
    public static string Repeat(this string s, int count) =>
        string.Concat(Enumerable.Repeat(s, count));
}
