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

    private static readonly string OverridesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ForzaTuneMaster", "specs_overrides.json");

    private static readonly JsonSerializerOptions CacheJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private static Dictionary<string, AiCarSpecsResponse>? _overrides;

    private static Dictionary<string, AiCarSpecsResponse> LoadOverrides()
    {
        if (_overrides != null) return _overrides;
        _overrides = new Dictionary<string, AiCarSpecsResponse>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (File.Exists(OverridesPath))
            {
                var raw = File.ReadAllText(OverridesPath);
                var parsed = JsonSerializer.Deserialize<Dictionary<string, AiCarSpecsResponse>>(raw, CacheJsonOptions);
                if (parsed != null)
                    _overrides = parsed;
            }
        }
        catch { }
        return _overrides;
    }

    private static void ApplyOverrides(string carName, AiCarSpecsResponse result)
    {
        var overrides = LoadOverrides();
        if (overrides.TryGetValue(carName, out var ov) && ov != null)
        {
            if (ov.DragCoefficientCd > 0) { result.DragCoefficientCd = ov.DragCoefficientCd; result.EstimatedFields.Remove("drag_coefficient_cd"); }
            if (ov.FrontalAreaM2 > 0) { result.FrontalAreaM2 = ov.FrontalAreaM2; result.EstimatedFields.Remove("frontal_area_m2"); }
            if (ov.WheelbaseMm > 0) { result.WheelbaseMm = ov.WheelbaseMm; result.EstimatedFields.Remove("wheelbase_mm"); }
            if (ov.FrontTrackMm > 0) { result.FrontTrackMm = ov.FrontTrackMm; result.EstimatedFields.Remove("front_track_mm"); }
            if (ov.RearTrackMm > 0) { result.RearTrackMm = ov.RearTrackMm; result.EstimatedFields.Remove("rear_track_mm"); }
        }
    }

    public static void DeleteCache()
    {
        try { if (Directory.Exists(SpecsCacheDir)) Directory.Delete(SpecsCacheDir, recursive: true); } catch { }
        try { Directory.CreateDirectory(SpecsCacheDir); } catch { }
    }

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
                if (cached != null)
                {
                    ApplyOverrides(carName, cached);
                    return true;
                }
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
        var list = new List<ProviderEntry>();

        list.Add(new("https://openrouter.ai/api/v1/chat/completions", "openrouter/auto", ApiKeys.OpenRouter!));
        list.Add(new("https://api.cerebras.ai/v1/chat/completions", "gpt-oss-120b", ApiKeys.Cerebras));
        list.Add(new("https://api.cerebras.ai/v1/chat/completions", "zai-glm-4.7", ApiKeys.Cerebras));

        return list;
    }

    public async Task<AiCarSpecsResponse> FetchCarSpecsAsync(string carName)
    {
        if (TryGetCached(carName, out var cached))
        {
            System.Diagnostics.Debug.WriteLine($"[AiCarSpec] Cache hit for '{carName}'");
            return cached!;
        }

        var prompt = $@"Search in google for {carName}.

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
- Prefer official factory/Forza data over estimates.
- If exact values are unavailable, cross-reference multiple sources (similar-year vehicles, same chassis/platform, engineering estimates) and list ALL estimated fields in ""estimated_fields"".
- For **frontal_area_m2**: if no exact value found, search for the vehicle's width and height (in mm or m), then calculate: FrontalArea = Width_m × Height_m × 0.85 (typical reduction factor for passenger cars). Use actual width (mirrors excluded) and roofline height. List ""frontal_area_m2"" in estimated_fields.
- For **drag_coefficient_cd**: if no exact value found, search for Cd of sibling models (same platform, generation, body style — e.g., sedan, hatchback, SUV). If unavailable, estimate from body-style typical range (sedan ~0.26–0.31, hatchback ~0.30–0.36, SUV ~0.33–0.40, sports car ~0.30–0.38, supercar ~0.32–0.36). List ""drag_coefficient_cd"" in estimated_fields.
- Output ONLY the JSON object — no markdown, no code fences, no explanations.
- Use the exact units: mm for lengths, m² for area, dimensionless for Cd.";

        var providers = BuildProviders();
        List<Exception> errors = [];
        foreach (var p in providers)
        {
            try
            {
                var result = await CallModelAsync(p.Url, p.Model, p.ApiKey, prompt);
                ApplyOverrides(carName, result);
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
