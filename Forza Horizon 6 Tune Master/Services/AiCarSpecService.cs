using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Forza_Horizon_6_Tune_Master.Services;

public class AiCarSpecsResponse
{
    public double WheelbaseMm { get; set; }
    public double FrontTrackMm { get; set; }
    public double RearTrackMm { get; set; }
    public double DragCoefficientCd { get; set; }
    public double FrontalAreaM2 { get; set; }
    public List<string> EstimatedFields { get; set; } = new();
}

public class AiCarSpecService
{
    private static readonly string SpecsCacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ForzaTuneMaster", "specs_cache");

    private static readonly JsonSerializerOptions CacheJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    static AiCarSpecService()
    {
        try { Directory.CreateDirectory(SpecsCacheDir); } catch { }
    }

    private static string GetCachePath(string carName)
    {
        var safeName = string.Join("_", carName.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(SpecsCacheDir, $"{safeName}.json");
    }

    private static bool TryGetCached(string carName, out AiCarSpecsResponse? cached)
    {
        var path = GetCachePath(carName);
        if (File.Exists(path) && File.GetLastWriteTime(path).Date >= DateTime.Today)
        {
            try
            {
                cached = JsonSerializer.Deserialize<AiCarSpecsResponse>(
                    File.ReadAllText(path), CacheJsonOptions);
                return cached != null;
            }
            catch { }
        }
        cached = null;
        return false;
    }

    private static void SaveCache(string carName, AiCarSpecsResponse specs)
    {
        try
        {
            File.WriteAllText(GetCachePath(carName),
                JsonSerializer.Serialize(specs, CacheJsonOptions));
        }
        catch { }
    }

    private record ProviderEntry(string Url, string Model, string ApiKey);

    private static List<ProviderEntry> BuildProviders()
    {
        var list = new List<ProviderEntry>
        {
            new("https://api.cerebras.ai/v1/chat/completions", "gpt-oss-120b", ApiKeys.Cerebras),
            new("https://api.cerebras.ai/v1/chat/completions", "zai-glm-4.7",  ApiKeys.Cerebras),
        };

        if (!string.IsNullOrEmpty(ApiKeys.OpenRouter))
            list.Add(new("https://openrouter.ai/api/v1/chat/completions", "openrouter/auto", ApiKeys.OpenRouter!));

        return list;
    }

    public async Task<AiCarSpecsResponse> FetchCarSpecsAsync(string carName)
    {
        if (TryGetCached(carName, out var cached))
        {
            System.Diagnostics.Debug.WriteLine($"[AiCarSpec] Cache hit for '{carName}'");
            return cached!;
        }

        var prompt = $@"{carName}.

Return only JSON:

{{
  ""wheelbase_mm"": number,
  ""front_track_mm"": number,
  ""rear_track_mm"": number,
  ""drag_coefficient_cd"": number,
  ""frontal_area_m2"": number,
  ""estimated_fields"": []
}}

Requirements:
- Use factory specs where available.
- If exact value is not found, estimate from reliable sources, similar models, or engineering calculations.
- List all estimated fields in the estimated_fields array.
- Values only in the specified units.
- No explanations, comments, markdown, or extra text.";

        var providers = BuildProviders();
        List<Exception> errors = [];
        foreach (var p in providers)
        {
            try
            {
                var result = await CallModelAsync(p.Url, p.Model, p.ApiKey, prompt);
                SaveCache(carName, result);
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AiCarSpec] Provider '{p.Model}' failed: {ex.Message}");
                errors.Add(ex);
            }
        }

        throw new AggregateException(
            LocalizationService.Instance.T("AiAllModelsFailed"), errors);
    }

    private static async Task<AiCarSpecsResponse> CallModelAsync(string url, string model, string apiKey, string prompt)
    {
        var requestBody = JsonSerializer.Serialize(new
        {
            model,
            temperature = 0,
            top_p = 1,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

        using var response = await Client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        System.Diagnostics.Debug.WriteLine($"[AiCarSpec] Raw API response ({model}): {json}");

        using var doc = JsonDocument.Parse(json);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";

        var clean = content.Trim();
        if (clean.StartsWith("```json")) clean = clean["```json".Length..];
        if (clean.StartsWith("```")) clean = clean["```".Length..];
        if (clean.EndsWith("```")) clean = clean[..^"```".Length];
        clean = clean.Trim();
        System.Diagnostics.Debug.WriteLine($"[AiCarSpec] Extracted content ({model}): {clean}");

        var result = JsonSerializer.Deserialize<AiCarSpecsResponse>(clean, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        if (result != null)
            System.Diagnostics.Debug.WriteLine(
                $"[AiCarSpec] Parsed ({model}): wb={result.WheelbaseMm} ft={result.FrontTrackMm} rt={result.RearTrackMm} " +
                $"cd={result.DragCoefficientCd} fa={result.FrontalAreaM2} est=[{string.Join(",", result.EstimatedFields)}]");
        else
            System.Diagnostics.Debug.WriteLine($"[AiCarSpec] Deserialize returned null ({model})");

        return result ?? throw new InvalidOperationException(
            string.Format(LocalizationService.Instance.T("AiResponseParseError"), model));
    }
}
