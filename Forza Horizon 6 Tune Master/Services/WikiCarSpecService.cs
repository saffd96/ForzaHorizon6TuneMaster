using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

public class WikiCarSpecs
{
    public EngineType? EngineType { get; set; }
    public AspirationType? AspirationType { get; set; }
    public PowertrainType? PowertrainType { get; set; }
    public double? PowerHP { get; set; }
    public double? TorqueNm { get; set; }
    public Models.DriveType? DriveType { get; set; }
    public EnginePosition? EnginePosition { get; set; }
    public double? WeightDistributionFront { get; set; }
    public double? TotalMassKg { get; set; }
    public int? GearCount { get; set; }
}

public class WikiCarSpecService
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(15) };

    private static string CacheDir => ForzaPaths.SpecsDir;

    public static void DeleteCache()
    {
        try
        {
            if (Directory.Exists(CacheDir))
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (!CacheDir.StartsWith(appData, StringComparison.Ordinal))
                    return;
                Directory.Delete(CacheDir, recursive: true);
            }
        }
        catch { /* non-critical */ }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly Regex FieldLineRegex = new(
        @"^\|\s*(\w+)\s*=\s*(.+)$", RegexOptions.Compiled | RegexOptions.Multiline);

    public async Task<WikiCarSpecs?> FetchSpecsAsync(string wikiPageTitle, CancellationToken ct = default)
    {
        var cacheFile = Path.Combine(CacheDir, SanitizeFileName(wikiPageTitle) + ".json");
        if (File.Exists(cacheFile))
        {
            try
            {
                var cached = JsonSerializer.Deserialize<WikiCarSpecs>(File.ReadAllText(cacheFile), JsonOptions);
                if (cached != null) return cached;
            }
            catch
            {
                try { File.Delete(cacheFile); } catch { }
            }
        }

        var url = $"https://forza.fandom.com/api.php?action=parse&page={Uri.EscapeDataString(wikiPageTitle)}&prop=wikitext&format=json";
        var json = await Client.GetStringAsync(url, ct);

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("parse", out var parse)) return null;
        if (!parse.TryGetProperty("wikitext", out var wtProp) ||
            !wtProp.TryGetProperty("*", out var wtStar)) return null;
        var wikitext = wtStar.GetString() ?? "";

        var specs = ParseInfobox(wikitext);
        if (specs != null)
        {
            try
            {
                Directory.CreateDirectory(CacheDir);
                File.WriteAllText(cacheFile, JsonSerializer.Serialize(specs, JsonOptions));
            }
            catch { /* non-critical */ }
        }
        return specs;
    }

    private static WikiCarSpecs? ParseInfobox(string wikitext)
    {
        var start = wikitext.IndexOf("{{CarInfobox", StringComparison.Ordinal);
        if (start < 0) return null;

        var end = wikitext.IndexOf("\n}}", start);
        if (end < 0) return null;

        var block = wikitext.Substring(start, end - start + 3);

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in FieldLineRegex.Matches(block))
            fields[m.Groups[1].Value] = m.Groups[2].Value.Trim().TrimEnd('.');

        var specs = new WikiCarSpecs();
        bool hasData = false;

        if (fields.TryGetValue("engine", out var eng) && ParseEngineType(eng) is { } et)
        {
            specs.EngineType = et;
            hasData = true;
        }

        bool hasIceEngine = fields.ContainsKey("engine");
        bool hasElecMotor = fields.ContainsKey("elec");

        if (fields.TryGetValue("aspiration", out var asp))
        {
            var (aspt, ptt) = ParseAspiration(asp);
            if (aspt.HasValue)
            {
                specs.AspirationType = aspt;
                // elec field present alongside ICE engine → hybrid
                specs.PowertrainType = (hasElecMotor && hasIceEngine) ? PowertrainType.Hybrid : ptt;
                hasData = true;
            }
        }

        // Pure electric: elec field but no ICE engine field
        if (hasElecMotor && !hasIceEngine)
        {
            specs.EngineType = null;
            specs.AspirationType = null;
            specs.PowertrainType = Models.PowertrainType.Electric;
        }

        if (fields.TryGetValue("power", out var pow) &&
            double.TryParse(pow, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var hp) && hp > 0)
        {
            specs.PowerHP = hp;
            hasData = true;
        }

        if (fields.TryGetValue("torque", out var trq) &&
            double.TryParse(trq, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lbft) && lbft > 0)
        {
            specs.TorqueNm = Math.Round(lbft * 1.35582, 0);
            hasData = true;
        }

        if (fields.TryGetValue("layout", out var layout))
        {
            var (dt, ep) = ParseLayout(layout.ToLowerInvariant().Trim());
            if (dt.HasValue) { specs.DriveType = (Models.DriveType)dt; specs.EnginePosition = ep; hasData = true; }
        }

        if (fields.TryGetValue("front", out var front) &&
            double.TryParse(front, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var frontPct) && frontPct > 0)
        {
            specs.WeightDistributionFront = frontPct;
            hasData = true;
        }

        if (fields.TryGetValue("weight", out var wt) &&
            double.TryParse(wt, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lbs) && lbs > 0)
        {
            specs.TotalMassKg = Math.Round(lbs / 2.20462, 0);
            hasData = true;
        }

        if (fields.TryGetValue("gears", out var gears) && int.TryParse(gears, out var gearCount) && gearCount > 0)
        {
            specs.GearCount = gearCount;
            hasData = true;
        }

        return hasData ? specs : null;
    }

    private static EngineType? ParseEngineType(string raw)
    {
        // Ignore malformed values that contain field separators
        var engine = raw.ToUpperInvariant().Trim();
        if (engine.Contains('|') || engine.Contains('=')) return null;

        // Strip trailing extras like "|extra = (Suzuki F6A)"
        var pipe = engine.IndexOf('|');
        if (pipe > 0) engine = engine[..pipe].Trim();

        return engine switch
        {
            "I1"                                         => EngineType.I1,
            "I2"                                         => EngineType.I2,
            "I3"                                         => EngineType.I3,
            "I4"                                         => EngineType.I4,
            "I5"                                         => EngineType.I5,
            "I6"                                         => EngineType.I6,
            "I8"                                         => EngineType.I8,
            "V6"                                         => EngineType.V6,
            "V8"                                         => EngineType.V8,
            "V10"                                        => EngineType.V10,
            "V12"                                        => EngineType.V12,
            "W12"                                        => EngineType.W12,
            "H2" or "F2" or "H4" or "F4"
                or "H6" or "F6" or "F12" or "BOXER"     => EngineType.Boxer,
            "2-ROTOR ROTARY" or "3-ROTOR ROTARY"
                or "4-ROTOR ROTARY" or "ROTARY"
                or "WANKEL"                              => EngineType.Rotary,
            "ELECTRIC"                                   => null,
            _                                            => null
        };
    }

    private static (AspirationType? asp, PowertrainType? pt) ParseAspiration(string asp) =>
        asp.ToLowerInvariant().Trim() switch
        {
            // ICE-only
            "na"                             => (AspirationType.Natural,              PowertrainType.ICE),
            "t" or "st" or "t2" or "tc"      => (AspirationType.SingleTurbo,         PowertrainType.ICE),
            "tt" or "biturbo" or "tw"        => (AspirationType.TwinTurbo,           PowertrainType.ICE),
            "pds" or "s" or "sc"             => (AspirationType.PositiveDisplacement,PowertrainType.ICE),
            "cfsc" or "cs"                   => (AspirationType.Centrifugal,         PowertrainType.ICE),
            "e" or "electric"                => (AspirationType.Electric,            PowertrainType.Electric),
            // Electric hybrid (electric motor as primary, small ICE range extender)
            "elech"                          => (AspirationType.Electric,            PowertrainType.Hybrid),
            // ICE hybrids — suffix "h" = hybrid; ICE aspiration type is preserved
            "nah"                            => (AspirationType.Natural,             PowertrainType.Hybrid),
            "th" or "t2h" or "tch"           => (AspirationType.SingleTurbo,        PowertrainType.Hybrid),
            "tth"                            => (AspirationType.TwinTurbo,           PowertrainType.Hybrid),
            "pdsh" or "sch"                  => (AspirationType.PositiveDisplacement,PowertrainType.Hybrid),
            _                                => (null, null)
        };

    private static (Models.DriveType? dt, EnginePosition? ep) ParseLayout(string layout) =>
        layout switch
        {
            "fa" or "ffa"   => (Models.DriveType.AWD, EnginePosition.Front),
            "fr"            => (Models.DriveType.RWD, EnginePosition.Front),
            "ff"            => (Models.DriveType.FWD, EnginePosition.Front),
            "mr"            => (Models.DriveType.RWD, EnginePosition.Mid),
            "ma" or "m4"    => (Models.DriveType.AWD, EnginePosition.Mid),
            "mf"            => (Models.DriveType.FWD, EnginePosition.Mid),
            "rr"            => (Models.DriveType.RWD, EnginePosition.Rear),
            "ra"            => (Models.DriveType.AWD, EnginePosition.Rear),
            _               => (null, null)
        };

    private static string SanitizeFileName(string name) =>
        Regex.Replace(name, @"[<>:""/\\|?*\s]", "_");
}
