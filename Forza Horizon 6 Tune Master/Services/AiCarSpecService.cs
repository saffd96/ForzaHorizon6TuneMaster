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
    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private static readonly string[] Models = ["gpt-oss-120b", "zai-glm-4.7"];

    public async Task<AiCarSpecsResponse> FetchCarSpecsAsync(string carName)
    {
        var apiKey = ApiKeys.Cerebras;
        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException(
                LocalizationService.Instance.T("AiApiKeyMissing"));

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

        List<Exception> errors = [];
        foreach (var model in Models)
        {
            try
            {
                return await CallModelAsync(model, apiKey, prompt);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AiCarSpec] Model '{model}' failed: {ex.Message}");
                errors.Add(ex);
            }
        }

        throw new AggregateException(
            LocalizationService.Instance.T("AiAllModelsFailed"), errors);
    }

    private static async Task<AiCarSpecsResponse> CallModelAsync(string model, string apiKey, string prompt)
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

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.cerebras.ai/v1/chat/completions");
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
