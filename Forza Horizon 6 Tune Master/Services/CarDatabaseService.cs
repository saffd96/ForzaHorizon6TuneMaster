using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

public class CarDatabaseService
{
    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ForzaTuneMaster");

    private static readonly string CachePath = Path.Combine(CacheDir, "fh6_cars_all.json");

    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    public async Task<List<CarData>> LoadCarDatabaseAsync()
    {
        try
        {
            var cars = await FetchFromWebAsync();
            if (cars.Count > 0)
            {
                SaveCache(cars);
                return cars;
            }
        }
        catch
        {
            // fall through to cache
        }

        return LoadCache();
    }

    private static async Task<List<CarData>> FetchFromWebAsync()
    {
        var html = await Client.GetStringAsync("https://forza.net/fh6cars");

        var rows = Regex.Matches(html,
            @"<tr>\s*<td>(.*?)</td>\s*<td>(.*?)</td>\s*<td>(.*?)</td>\s*<td>(.*?)</td>\s*<td>(.*?)</td>\s*<td>(.*?)</td>\s*<td>(.*?)</td>\s*</tr>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        var cars = new List<CarData>(rows.Count);
        var seen = new HashSet<string>();

        foreach (Match m in rows)
        {
            var make = WebUtility(m.Groups[1].Value);
            var fullName = WebUtility(m.Groups[2].Value);

            int year = 0;
            string model = fullName;

            var yearMatch = Regex.Match(fullName, @"^(\d{4})\s+(.*)");
            if (yearMatch.Success)
            {
                year = int.Parse(yearMatch.Groups[1].Value);
                model = yearMatch.Groups[2].Value;
            }

            model = StripMakePrefix(model, make);

            var key = $"{year}|{make}|{model}";
            if (seen.Add(key))
            {
                cars.Add(new CarData
                {
                    Year = year,
                    Make = make,
                    Model = model
                });
            }
        }

        return cars;
    }

    private static string StripMakePrefix(string model, string make)
    {
        if (model.StartsWith(make, StringComparison.OrdinalIgnoreCase))
        {
            var rest = model[make.Length..].TrimStart('.', ' ');
            if (rest.Length > 0) return rest;
        }
        return model;
    }

    private static string WebUtility(string s)
        => s.Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&#39;", "'").Replace("&quot;", "\"");

    private static void SaveCache(List<CarData> cars)
    {
        try
        {
            Directory.CreateDirectory(CacheDir);
            var json = JsonSerializer.Serialize(cars);
            File.WriteAllText(CachePath, json);
        }
        catch
        {
            // non-critical
        }
    }

    private static List<CarData> LoadCache()
    {
        try
        {
            if (!File.Exists(CachePath)) return new List<CarData>();
            var json = File.ReadAllText(CachePath);
            return JsonSerializer.Deserialize<List<CarData>>(json) ?? new List<CarData>();
        }
        catch
        {
            return new List<CarData>();
        }
    }
}
