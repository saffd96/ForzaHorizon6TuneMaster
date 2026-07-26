using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Forza_Horizon_6_Tune_Master.Services;

public static class SupabaseConfig
{
    public static string ProjectRef { get; } = "ncshtediehlmickwsiwa";
    public static string BucketName { get; } = "fh6-tune-master";
    public static string AnonKey { get; } = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Im5jc2h0ZWRpZWhsbWlja3dzaXdhIiwicm9sZSI6ImFub24iLCJpYXQiOjE3ODMyNDY4MjgsImV4cCI6MjA5ODgyMjgyOH0.yJFRFAptTZbznFf526dO-6V54nTRi_Z8cnp1SZ_l_7I";
    public static string StorageUrl => $"https://{ProjectRef}.supabase.co/storage/v1/object/public/{BucketName}/";
}

public sealed class UpdateService
{
    public static UpdateService Instance { get; } = new();

    private static HttpClient _http = CreateDefaultClient();

    internal static void SetHttpClient(HttpClient client) => _http = client;
    private static readonly Assembly _assembly = Assembly.GetExecutingAssembly();

    private UpdateService() { }

    // Every remote path is scoped under the running exe's own version, so an exe
    // only ever pulls DB/localization content published for its exact schema -
    // never a newer version's (incompatible) content, and never leaves an older
    // version's content stuck around after an upgrade.
    private static string VersionPrefix => $"{SavedProfile.ProfileVersion}/";

    public static bool IsPendingUpdateMarked() => File.Exists(ForzaPaths.PendingUpdatePath);

    public static void MarkPendingUpdate()
    {
        try
        {
            Directory.CreateDirectory(ForzaPaths.BaseDir);
            File.WriteAllText(ForzaPaths.PendingUpdatePath, DateTime.UtcNow.ToString("O"));
        }
        catch { }
    }

    public static void ClearPendingUpdateMark()
    {
        try { if (File.Exists(ForzaPaths.PendingUpdatePath)) File.Delete(ForzaPaths.PendingUpdatePath); }
        catch { }
    }

    private static HttpClient CreateDefaultClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("apikey", SupabaseConfig.AnonKey);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {SupabaseConfig.AnonKey}");
        return client;
    }

    public async Task<(bool DbOutdated, bool LocOutdated)> CheckAsync(CancellationToken ct = default)
    {
        var dbOutdated = await CheckHashAsync($"{VersionPrefix}fh6_db.sha256", GetCurrentDbHash(), ct).ConfigureAwait(false);
        var locOutdated = await CheckHashAsync($"{VersionPrefix}loc.sha256", GetCurrentLocHash(), ct).ConfigureAwait(false);
        return (dbOutdated, locOutdated);
    }

    private async Task<bool> CheckHashAsync(string remoteFile, string localHash, CancellationToken ct)
    {
        string remote = await _http.GetStringAsync(SupabaseConfig.StorageUrl + remoteFile, ct).ConfigureAwait(false);
        return remote?.Trim() != localHash;
    }

    private static string GetCurrentDbHash()
    {
        string? appDataPath = ForzaPaths.UpdateDbPath;
        if (File.Exists(appDataPath))
        {
            try
            {
                return HexHash(File.ReadAllBytes(appDataPath));
            }
            catch { }
        }
        return HexHash(Fh6DatabaseService.LoadEmbeddedDbBytes());
    }

    // Namespace prefix under which every Localization/*.json file is embedded (see the
    // .csproj's EmbeddedResource entries) - the single source of truth for "which locale
    // files exist", so adding a new language (embed it + drop it in DUMPER/publish_update.ps1's
    // folder) never needs a matching hardcoded list here.
    private const string LocalizationResourcePrefix = "Forza_Horizon_6_Tune_Master.Localization.";

    private static string[] GetLocalizationFileNames() =>
        _assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(LocalizationResourcePrefix, StringComparison.Ordinal)
                        && n.EndsWith(".json", StringComparison.Ordinal))
            .Select(n => n[LocalizationResourcePrefix.Length..])
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

    private static string GetCurrentLocHash()
    {
        var combined = new List<byte>();
        bool anyAppData = false;

        string locDir = ForzaPaths.UpdateLocDir;
        var names = GetLocalizationFileNames();
        foreach (var name in names)
        {
            string path = Path.Combine(locDir, name);
            if (File.Exists(path))
            {
                anyAppData = true;
                combined.AddRange(File.ReadAllBytes(path));
            }
        }

        if (anyAppData)
            return HexHash(combined.ToArray());

        foreach (var name in names)
        {
            using var stream = _assembly.GetManifestResourceStream($"{LocalizationResourcePrefix}{name}");
            if (stream != null)
            {
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                combined.AddRange(ms.ToArray());
            }
        }
        return HexHash(combined.ToArray());
    }

    public async Task DownloadDbAsync(IProgress<double>? progress = null, CancellationToken ct = default)
    {
        string tmpPath = ForzaPaths.UpdateDbPath + ".tmp";
        try
        {
            await DownloadFileAsync($"{VersionPrefix}fh6_db.sqlite", tmpPath, progress, ct).ConfigureAwait(false);

            if (!await ValidateDownloadedHash(tmpPath, $"{VersionPrefix}fh6_db.sha256").ConfigureAwait(false))
                throw new InvalidDataException("Database download corrupted: SHA256 mismatch");

            if (File.Exists(ForzaPaths.UpdateDbPath))
                File.Delete(ForzaPaths.UpdateDbPath);
            File.Move(tmpPath, ForzaPaths.UpdateDbPath);
        }
        finally
        {
            try { if (File.Exists(tmpPath)) File.Delete(tmpPath); } catch { }
        }
    }

    public async Task DownloadLocAsync(IProgress<double>? progress = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(ForzaPaths.UpdateLocDir);

        var files = GetLocalizationFileNames();
        var tmpPaths = new string[files.Length];
        double step = 1.0 / files.Length;
        try
        {
            for (int i = 0; i < files.Length; i++)
            {
                string local = Path.Combine(ForzaPaths.UpdateLocDir, files[i]);
                tmpPaths[i] = local + ".tmp";
                await DownloadFileAsync($"{VersionPrefix}loc/{files[i]}", tmpPaths[i], null, ct).ConfigureAwait(false);
                progress?.Report((i + 1) * step);
            }

            if (!await ValidateDownloadedLocHash(tmpPaths).ConfigureAwait(false))
                throw new InvalidDataException("Locale download corrupted: SHA256 mismatch");

            for (int i = 0; i < files.Length; i++)
            {
                string local = Path.Combine(ForzaPaths.UpdateLocDir, files[i]);
                if (File.Exists(local)) File.Delete(local);
                File.Move(tmpPaths[i], local);
            }
        }
        finally
        {
            foreach (var tmp in tmpPaths)
            {
                try { if (tmp != null && File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }
    }

    private async Task DownloadFileAsync(string remotePath, string localPath,
        IProgress<double>? progress, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(localPath);
        if (dir != null) Directory.CreateDirectory(dir);

        using var response = await _http.GetAsync(
            SupabaseConfig.StorageUrl + remotePath,
            HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1;
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write,
            FileShare.None, 81920, useAsync: true);

        if (total > 0)
        {
            var buffer = new byte[81920];
            long read = 0;
            int bytes;
            while ((bytes = await stream.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytes), ct).ConfigureAwait(false);
                read += bytes;
                progress?.Report((double)read / total);
            }
        }
        else
        {
            await stream.CopyToAsync(fileStream, ct).ConfigureAwait(false);
        }
    }

    private async Task<bool> ValidateDownloadedHash(string filePath, string hashFileName)
    {
        try
        {
            string remoteHash = await _http.GetStringAsync(SupabaseConfig.StorageUrl + hashFileName).ConfigureAwait(false);
            string fileHashHex = HexHash(File.ReadAllBytes(filePath));
            return fileHashHex == remoteHash?.Trim();
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> ValidateDownloadedLocHash(string[] tmpPaths)
    {
        try
        {
            string remoteHash = await _http.GetStringAsync(SupabaseConfig.StorageUrl + $"{VersionPrefix}loc.sha256").ConfigureAwait(false);
            var combined = new List<byte>();
            foreach (var path in tmpPaths)
                combined.AddRange(File.ReadAllBytes(path));
            string localHash = HexHash(combined.ToArray());
            return localHash == remoteHash?.Trim();
        }
        catch
        {
            return false;
        }
    }

    private static string HexHash(byte[] data)
    {
        return Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
    }
}
