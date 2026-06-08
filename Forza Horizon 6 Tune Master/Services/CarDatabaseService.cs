using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

public record CarLoadResult(List<CarData> Cars, bool FromCache, string? WebErrorMessage);

public class CarDatabaseService
{
    private static string CachePath => ForzaPaths.CachePath;
    private static string LegacyCachePath => ForzaPaths.LegacyCachePath;

    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(20) };

    private static readonly Regex CommentRegex = new(
        @"<!--\s+(.+?)\s+-->", RegexOptions.Compiled);

    private static readonly Regex RowRegex = new(
        @"\{\{CarListStatsFH6\|([^}]+)\}\}", RegexOptions.Compiled | RegexOptions.Singleline);

    public static bool IsCacheStale =>
        !File.Exists(CachePath) || File.GetLastWriteTime(CachePath).Date < DateTime.Today;

    public static void DeleteCache()
    {
        try { if (File.Exists(CachePath)) File.Delete(CachePath); } catch { /* non-critical */ }
        try { if (File.Exists(LegacyCachePath)) File.Delete(LegacyCachePath); } catch { /* non-critical */ }
    }

    public Task<CarLoadResult> LoadCarDatabaseAsync()
    {
        try { if (File.Exists(LegacyCachePath)) File.Delete(LegacyCachePath); } catch { }

        if (File.Exists(CachePath))
            return Task.FromResult(new CarLoadResult(LoadCache(), true, null));

        return RefreshAsync();
    }

    public async Task<CarLoadResult> RefreshAsync()
    {
        try
        {
            var cars = await FetchFromWikiAsync();
            if (cars.Count >= 5)
            {
                SaveCache(cars);
                return new CarLoadResult(cars, false, null);
            }
            throw new InvalidDataException(LocalizationService.Instance.T("CarDbTooFewRecords"));
        }
        catch (Exception ex)
        {
            return new CarLoadResult(LoadCache(), true, ex.Message);
        }
    }

    private static async Task<List<CarData>> FetchFromWikiAsync()
    {
        const string apiUrl = "https://forza.fandom.com/api.php?action=parse&page=Forza_Horizon_6%2FCars&prop=wikitext&format=json";
        var json = await Client.GetStringAsync(apiUrl);

        using var doc = JsonDocument.Parse(json);
        var wikitext = doc.RootElement
            .GetProperty("parse")
            .GetProperty("wikitext")
            .GetProperty("*")
            .GetString() ?? "";

        return ParseWikitext(wikitext);
    }

    private static List<CarData> ParseWikitext(string wikitext)
    {
        var cars = new List<CarData>(650);
        var seen = new HashSet<string>();

        // Collect all token positions: section comments and car rows
        var comments = CommentRegex.Matches(wikitext);
        var rows = RowRegex.Matches(wikitext);

        // Build a sorted list of events by position
        // event: (position, isMake, make/rowContent)
        var events = new List<(int pos, bool isMake, string payload)>(comments.Count + rows.Count);
        foreach (Match m in comments)
            events.Add((m.Index, true, m.Groups[1].Value.Trim()));
        foreach (Match m in rows)
            events.Add((m.Index, false, m.Groups[1].Value));
        events.Sort((a, b) => a.pos.CompareTo(b.pos));

        var currentMake = "";
        foreach (var (_, isMake, payload) in events)
        {
            if (isMake)
            {
                currentMake = payload;
                continue;
            }

            var car = ParseCarRow(payload, currentMake);
            if (car == null) continue;

            var key = $"{car.Year}|{car.Make}|{car.Model}";
            if (seen.Add(key))
                cars.Add(car);
        }

        return cars;
    }

    private static CarData? ParseCarRow(string rowContent, string currentMake)
    {
        // Split positional params (skip named params containing '=')
        var parts = rowContent.Split('|');
        var positional = new List<string>();
        foreach (var p in parts)
        {
            if (!p.Contains('='))
                positional.Add(p.Trim());
        }

        if (positional.Count < 3) return null;

        var fullName = positional[0].Trim();
        if (string.IsNullOrWhiteSpace(fullName)) return null;

        // param[2] = year, param[12] = PI
        if (!int.TryParse(positional[2], out var year)) return null;

        var pi = 0;
        if (positional.Count > 12 && int.TryParse(positional[12], out var piParsed))
            pi = piParsed;

        // Split make and model using section comment make name
        string make = currentMake;
        string model;
        if (!string.IsNullOrEmpty(make) && fullName.StartsWith(make, StringComparison.OrdinalIgnoreCase))
        {
            model = fullName[make.Length..].TrimStart(' ', '.', '-');
            if (string.IsNullOrWhiteSpace(model))
                model = fullName;
        }
        else
        {
            // Fallback: first word is make
            var spaceIdx = fullName.IndexOf(' ');
            if (spaceIdx < 0) return null;
            make = fullName[..spaceIdx];
            model = fullName[(spaceIdx + 1)..];
        }

        return new CarData
        {
            Year = year,
            Make = make,
            Model = model,
            WikiPageTitle = fullName,
            PI = pi
        };
    }

    private static void SaveCache(List<CarData> cars)
    {
        try
        {
            Directory.CreateDirectory(ForzaPaths.BaseDir);
            File.WriteAllText(CachePath, JsonSerializer.Serialize(cars));
        }
        catch { /* non-critical */ }
    }

    private static List<CarData> LoadCache()
    {
        try
        {
            if (!File.Exists(CachePath)) return [];
            return JsonSerializer.Deserialize<List<CarData>>(File.ReadAllText(CachePath)) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
