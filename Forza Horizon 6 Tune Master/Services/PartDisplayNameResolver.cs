using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

public class PartDisplayNameResolver
{
    private readonly Fh6DatabaseService _db = Fh6DatabaseService.Instance;

    public int StockFrontTireProfile { get; set; }
    public int StockRearTireProfile { get; set; }

    // Stock tire width (mm), needed alongside StockFrontTireProfile/StockRearTireProfile to
    // work out the equivalent profile for each width tier (see FormatTireWidth).
    public int StockFrontTireWidth { get; set; }
    public int StockRearTireWidth { get; set; }

    // Imperial vs metric for unit-bearing option labels (e.g. track spacing). Static because
    // resolvers are created per sub-VM; the host VM keeps this in sync with the unit toggle.
    public static bool UseImperial { get; set; }

    // TireModelName root -> Upgrades IDS_Name_* key.
    private static readonly Dictionary<string, string> TireCompoundNameIds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Stock"] = "Upgrades_IDS_Name_78",
        ["Street"] = "Upgrades_IDS_Name_79",
        ["Sport"] = "Upgrades_IDS_Name_80",
        ["Semi_Slick"] = "Upgrades_IDS_Name_81",
        ["Semi_Slick_Horizon"] = "Upgrades_IDS_Name_275",
        ["Semi_Slick_ToyoProxes"] = "Upgrades_IDS_Name_81",
        ["Semi_Slick_YellowWall"] = "Upgrades_IDS_Name_300",
        ["Slick"] = "Upgrades_IDS_Name_299",
        ["Slick_Drag"] = "Upgrades_IDS_Name_280",
        ["Slick_F1"] = "Upgrades_IDS_Name_299",
        ["Slick_Dunlop"] = "Upgrades_IDS_Name_299",
        ["Slick_Goodyear"] = "Upgrades_IDS_Name_299",
        ["Slick_Michelin"] = "Upgrades_IDS_Name_299",
        ["Rally"] = "Upgrades_IDS_Name_273",
        ["Offroad"] = "Upgrades_IDS_Name_274",
        ["Snow"] = "Upgrades_IDS_Name_277",
        ["Vintage"] = "Upgrades_IDS_Name_281",
        ["Vintage_Race"] = "Upgrades_IDS_Name_281",
        ["Vintage_Race_Firestone"] = "Upgrades_IDS_Name_281",
        ["Vintage_WhiteWall"] = "Upgrades_IDS_Name_300",
        ["Drag"] = "Upgrades_IDS_Name_280",
        ["Drift"] = "Upgrades_IDS_Name_298",
    };

    // TireCompoundID override: some compounds share the same TireModelName as another type
    // (e.g. drift tires use TireModelName "Street" and TireCompoundID=17, while regular street
    // tires use TireModelName "Street" and TireCompoundID=6).  The TireModelName-based lookup
    // would give both the same display name, so we check TireCompoundID first.
    private static readonly Dictionary<int, string> TireCompoundIdOverrides = new()
    {
        [17] = "Upgrades_IDS_Name_298", // DriftL1 → "Drift Tire Compound"
        // On "Forza Edition" cars the drag row's TireModelName is mislabeled "Slick_FE" instead
        // of "*_Drag" (unlike the same car's non-FE trim), colliding with that car's other Slick
        // tiers and getting silently dropped by the compound dropdown's name-based dedup.
        [14] = "Upgrades_IDS_Name_280", // Drag → "Drag Tire Compound"
    };

    private static readonly Dictionary<Type, string> TableNameMap = new()
    {
        { typeof(DbUpgradeEngine),              "List_UpgradeEngine" },
        { typeof(DbUpgradeCamshaft),            "List_UpgradeEngineCamshaft" },
        { typeof(DbUpgradeDisplacement),        "List_UpgradeEngineDisplacement" },
        { typeof(DbUpgradeValves),              "List_UpgradeEngineValves" },
        { typeof(DbUpgradePistons),             "List_UpgradeEnginePistonsCompression" },
        { typeof(DbUpgradeFuelSystem),          "List_UpgradeEngineFuelSystem" },
        { typeof(DbUpgradeIgnition),            "List_UpgradeEngineIgnition" },
        { typeof(DbUpgradeExhaust),             "List_UpgradeEngineExhaust" },
        { typeof(DbUpgradeIntake),              "List_UpgradeEngineIntake" },
        { typeof(DbUpgradeFlywheel),            "List_UpgradeEngineFlywheel" },
        { typeof(DbUpgradeManifold),            "List_UpgradeEngineManifold" },
        { typeof(DbUpgradeOilCooling),          "List_UpgradeEngineOilCooling" },
        { typeof(DbUpgradeRestrictor),          "List_UpgradeEngineRestrictorPlate" },
        { typeof(DbUpgradeTurboSingle),         "List_UpgradeEngineTurboSingle" },
        { typeof(DbUpgradeTurboTwin),           "List_UpgradeEngineTurboTwin" },
        { typeof(DbUpgradeCSC),                 "List_UpgradeEngineCSC" },
        { typeof(DbUpgradeDSC),                 "List_UpgradeEngineDSC" },
        { typeof(DbUpgradeIntercooler),         "List_UpgradeEngineIntercooler" },
        { typeof(DbUpgradeTireCompound),        "List_UpgradeTireCompound" },
        { typeof(DbUpgradeSpringDamper),        "List_UpgradeSpringDamper" },
        { typeof(DbUpgradeBrakes),              "List_UpgradeBrakes" },
        { typeof(DbUpgradeTransmission),        "List_UpgradeDrivetrainTransmission" },
        { typeof(DbUpgradeClutch),              "List_UpgradeDrivetrainClutch" },
        { typeof(DbUpgradeDriveline),           "List_UpgradeDrivetrainDriveline" },
        { typeof(DbUpgradeDifferential),        "List_UpgradeDrivetrainDifferential" },
        { typeof(DbUpgradeAntiSwayFront),       "List_UpgradeAntiSwayFront" },
        { typeof(DbUpgradeAntiSwayRear),        "List_UpgradeAntiSwayRear" },
        { typeof(DbUpgradeCarBody),             "List_UpgradeCarBody" },
        { typeof(DbUpgradeRearWing),            "List_UpgradeRearWing" },
        { typeof(DbUpgradeFrontBumper),         "List_UpgradeCarBodyFrontBumper" },
        { typeof(DbUpgradeRearBumper),          "List_UpgradeCarBodyRearBumper" },
        { typeof(DbUpgradeSideSkirt),           "List_UpgradeCarBodySideSkirt" },
        { typeof(DbUpgradeHood),                "List_UpgradeCarBodyHood" },
        { typeof(DbUpgradeWeightReduction),     "List_UpgradeCarBodyWeight" },
        { typeof(DbUpgradeChassisStiffness),    "List_UpgradeCarBodyChassisStiffness" },
        { typeof(DbUpgradeTireWidthFront),      "List_UpgradeCarBodyTireWidthFront" },
        { typeof(DbUpgradeTireWidthRear),       "List_UpgradeCarBodyTireWidthRear" },
        { typeof(DbUpgradeRimFront),            "List_UpgradeRimSizeFront" },
        { typeof(DbUpgradeRimRear),             "List_UpgradeRimSizeRear" },
        { typeof(DbUpgradeTireAspectRatioFront),"List_UpgradeCarBodyTireAspectRatioFront" },
        { typeof(DbUpgradeTireAspectRatioRear), "List_UpgradeCarBodyTireAspectRatioRear" },
        { typeof(DbUpgradeTrackSpacingFront),   "List_UpgradeCarBodyTrackSpacingFront" },
        { typeof(DbUpgradeTrackSpacingRear),    "List_UpgradeCarBodyTrackSpacingRear" },
        { typeof(DbUpgradeMotorSwap),           "List_UpgradeMotor" },
        { typeof(DbUpgradeMotorPart),           "List_UpgradeMotorParts" },
        { typeof(DbUpgradeDrivetrain),          "List_UpgradeDrivetrain" },
    };

    public string Resolve(DbUpgradePart part, int makeId)
    {
        if (TryResolveGameString(part, out var gameName))
            return gameName;

        // Fallback: generic "Stock Category" / "Category Stage N".
        string category = GetLocalizedCategoryName(part);
        if (!part.IsStock)
            return $"{category} {T("Part_Stage")} {part.Level}";
        return $"{T("Part_Stock")} {category}";
    }

    public string Resolve<T>(T part, int makeId) where T : DbUpgradePart
        => Resolve((DbUpgradePart)part, makeId);

    private bool TryResolveGameString(DbUpgradePart part, out string name)
    {
        name = "";
        string? key = null;

        switch (part)
        {
            // ── Swaps ──────────────────────────────────────────────────────
            case DbUpgradeEngine eng:
                name = _db.GetEngine(eng.EngineID)?.EngineName ?? "";
                return !string.IsNullOrEmpty(name);

            case DbUpgradeMotorSwap mSwap:
                name = _db.GetMotor(mSwap.MotorID)?.MotorName ?? "";
                return !string.IsNullOrEmpty(name);

            case DbUpgradeDrivetrain drv:
                {
                    string baseName = drv.IsStock
                        ? T("Upgrades_IDS_Name_141") // Stock Drivetrain
                        : T("Upgrades_IDS_Name_142"); // Alternate Drivetrain
                    // DB drive-type IDs match the localization suffixes 1-to-1: 1 FWD / 2 RWD / 3 AWD.
                    string driveKey = $"List_DriveType_IDS_DisplayName_{drv.DriveTypeID}";
                    string driveType = T(driveKey);
                    if (driveType == driveKey) driveType = drv.DriveTypeID.ToString();
                    name = drv.IsStock ? baseName : $"{baseName} ({driveType})";
                    return true;
                }

            // ── Engine parts ───────────────────────────────────────────────
            case DbUpgradeCamshaft c:
                key = OffsetLevelKey(c, 3);
                break;
            case DbUpgradeValves v:
                key = OffsetLevelKey(v, 220);
                break;
            case DbUpgradeDisplacement d:
                key = OffsetLevelKey(d, 7); // Engine Block
                break;
            case DbUpgradePistons p:
                key = OffsetLevelKey(p, 224);
                break;
            case DbUpgradeFuelSystem fs:
                key = ResolveFuelSystemKey(fs);
                break;
            case DbUpgradeIgnition ig:
                key = ResolveIgnitionKey(ig);
                break;
            case DbUpgradeExhaust e:
                key = OffsetLevelKey(e, 19);
                break;
            case DbUpgradeIntake i:
                key = OffsetLevelKey(i, 23);
                break;
            case DbUpgradeManifold m:
                key = OffsetLevelKey(m, 208);
                break;
            case DbUpgradeFlywheel f:
                key = OffsetLevelKey(f, 63);
                break;
            case DbUpgradeOilCooling o:
                key = OffsetLevelKey(o, 232);
                break;
            case DbUpgradeRestrictor r:
                key = r.IsStock ? "Upgrades_IDS_Name_161" : "Upgrades_IDS_Name_270";
                break;
            case DbUpgradeTurboSingle ts:
                // Tier 4 is the race turbo with anti-lag.
                key = ts.Level >= 4 ? "Upgrades_IDS_Name_312" : OffsetLevelKey(ts, 27);
                break;
            case DbUpgradeTurboTwin tt:
                key = tt.Level >= 4 ? "Upgrades_IDS_Name_313" : OffsetLevelKey(tt, 236);
                break;
            case DbUpgradeCSC csc:
                key = OffsetLevelKey(csc, 35);
                break;
            case DbUpgradeDSC dsc:
                key = OffsetLevelKey(dsc, 31);
                break;
            case DbUpgradeIntercooler ic:
                key = OffsetLevelKey(ic, 39);
                break;

            // ── Chassis / suspension ───────────────────────────────────────
            case DbUpgradeTireCompound tc:
                key = ResolveTireCompoundKey(tc);
                break;
            case DbUpgradeSpringDamper sd:
                // Absolute tier: 0 Stock, 1 Street, 2 Sport, 3 Race, 4 Rally, 5 Drift.
                key = sd.IsStock
                    ? "Upgrades_IDS_Name_47"
                    : sd.Level switch
                    {
                        1 => "Upgrades_IDS_Name_48",  // Street
                        2 => "Upgrades_IDS_Name_49",  // Sport
                        3 => "Upgrades_IDS_Name_50",  // Race
                        4 => "Upgrades_IDS_Name_272", // Rally
                        5 => "Upgrades_IDS_Name_278", // Drift
                        _ => null
                    };
                break;
            case DbUpgradeBrakes b:
                key = OffsetLevelKey(b, 43);
                break;
            case DbUpgradeAntiSwayFront af:
                key = OffsetLevelKey(af, 51);
                break;
            case DbUpgradeAntiSwayRear ar:
                key = OffsetLevelKey(ar, 244);
                break;
            case DbUpgradeRearWing w:
                return ResolveBodyKit(w, 90, "Upgrades_IDS_Name_265", out name);

            // Body-kit conversion: stock keeps the plain "Stock Body Kit" label; swapped kits
            // are aftermarket widebody conversions disambiguated by their brand (manufacturer).
            case DbUpgradeCarBody bk:
            {
                string baseName = T("Part_BodyKit");
                name = bk.IsStock ? $"{T("Part_Stock")} {baseName}" : WithManufacturer(baseName, bk);
                return name != "";
            }

            // ── Body kits ──────────────────────────────────────────────────
            // Tiers 0-3 are Stock/Street/Sport/Race; tier 4 is the "Remove <part>" option
            // (always a negative mass delta in the DB). Multiple same-tier options differ
            // only by brand, so the manufacturer name disambiguates them in the dropdown.
            case DbUpgradeFrontBumper fb:
                return ResolveBodyKit(fb, 86, "Upgrades_IDS_Name_264", out name);
            case DbUpgradeRearBumper rb:
                return ResolveBodyKit(rb, 94, "Upgrades_IDS_Name_266", out name);
            case DbUpgradeSideSkirt ss:
                {
                    // Side skirts only have Stock (98), Street (99) and Remove (267) strings.
                    if (ss.Level >= 4) { name = T("Upgrades_IDS_Name_267"); return true; }
                    string baseName = T(ss.IsStock ? "Upgrades_IDS_Name_98" : "Upgrades_IDS_Name_99");
                    name = WithManufacturer(baseName, ss);
                    return name != "";
                }
            case DbUpgradeHood hd:
                {
                    // Hoods only have Stock (100) and Street (101) strings; higher tiers reuse
                    // the "Street Hood" label disambiguated by the manufacturer brand.
                    string baseName = T(hd.IsStock ? "Upgrades_IDS_Name_100" : "Upgrades_IDS_Name_101");
                    name = WithManufacturer(baseName, hd);
                    return name != "";
                }

            // ── Drivetrain parts ───────────────────────────────────────────
            case DbUpgradeTransmission t when t.IsStock:
                key = "Upgrades_IDS_Name_55"; // Stock Transmission
                break;
            case DbUpgradeTransmission t:
                // Map by absolute Level to match the game's Upgrades table (TypeId 15).
                key = t.Level switch
                {
                    0  => "Upgrades_IDS_Name_55",  // Stock Transmission
                    1  => "Upgrades_IDS_Name_56",  // Street Transmission
                    2  => "Upgrades_IDS_Name_57",  // Sport Transmission
                    3  => "Upgrades_IDS_Name_58",  // Race Transmission
                    4  => "Upgrades_IDS_Name_271", // Rally Transmission
                    6  => "Upgrades_IDS_Name_293", // Race Transmission: 6 Speed
                    7  => "Upgrades_IDS_Name_294", // Race Transmission: 7 Speed
                    8  => "Upgrades_IDS_Name_295", // Race Transmission: 8 Speed
                    9  => "Upgrades_IDS_Name_296", // Race Transmission: 9 Speed
                    10 => "Upgrades_IDS_Name_297", // Race Transmission: 10 Speed
                    11 => "Upgrades_IDS_Name_302", // Drift Transmission: 4 Speed
                    _  => null
                };
                break;
            case DbUpgradeClutch c:
                key = OffsetLevelKey(c, 59);
                break;
            case DbUpgradeDriveline d:
                key = OffsetLevelKey(d, 67);
                break;
            case DbUpgradeDifferential diff when diff.IsStock:
                key = "Upgrades_IDS_Name_71"; // Stock Diff
                break;
            case DbUpgradeDifferential diff:
                // Map by absolute Level to match the game's Upgrades table (TypeId 19).
                key = diff.Level switch
                {
                    0 => "Upgrades_IDS_Name_71",  // Stock Diff
                    1 => "Upgrades_IDS_Name_72",  // Street Diff
                    2 => "Upgrades_IDS_Name_73",  // Sport Diff
                    3 => "Upgrades_IDS_Name_74",  // Race Diff
                    5 => "Upgrades_IDS_Name_291", // Rally Diff
                    6 => "Upgrades_IDS_Name_292", // Drift Diff
                    7 => "Upgrades_IDS_Name_301", // Offroad Diff
                    _ => null
                };
                break;

            // ── Body / wheels ──────────────────────────────────────────────
            case DbUpgradeWeightReduction wr:
                key = OffsetLevelKey(wr, 75, 113); // 75-77 + 113 for race
                break;
            case DbUpgradeChassisStiffness cs:
                key = OffsetLevelKey(cs, 248);
                break;
            // Tire width / rim size: the game's generic "Modified tire width" / "Modified
            // rim size" strings are identical across every option, so show the actual
            // dimension instead. That is what disambiguates the choices in the dropdown.
            case DbUpgradeTireWidthFront twf:
                name = FormatTireWidth(twf.FrontTireWidth, StockFrontTireWidth, StockFrontTireProfile, twf.IsStock);
                return true;
            case DbUpgradeTireWidthRear twr:
                name = FormatTireWidth(twr.RearTireWidth, StockRearTireWidth, StockRearTireProfile, twr.IsStock);
                return true;
            case DbUpgradeRimFront rf:
                name = FormatRimSize(rf.FrontWheelDiameter, rf.IsStock);
                return true;
            case DbUpgradeRimRear rr:
                name = FormatRimSize(rr.RearWheelDiameter, rr.IsStock);
                return true;
            case DbUpgradeTireAspectRatioFront tarf:
            {
                int finalProfile = StockFrontTireProfile + (int)tarf.FrontTireAspectRatioOffset;
                string s = $"{finalProfile}";
                name = tarf.IsStock ? $"{s} ({T("Part_Stock")})" : s;
                return true;
            }
            case DbUpgradeTireAspectRatioRear tarr:
            {
                int finalProfile = StockRearTireProfile + (int)tarr.RearTireAspectRatioOffset;
                string s = $"{finalProfile}";
                name = tarr.IsStock ? $"{s} ({T("Part_Stock")})" : s;
                return true;
            }
            // Every non-stock option shares the same generic "Modified track width" string,
            // so append the actual spacing (DB stores metres) to tell the tiers apart.
            case DbUpgradeTrackSpacingFront tsf:
                name = FormatTrackSpacing(tsf.IsStock, tsf.Spacing, "Upgrades_IDS_Name_282", "Upgrades_IDS_Name_283");
                return name != "";
            case DbUpgradeTrackSpacingRear tsr:
                name = FormatTrackSpacing(tsr.IsStock, tsr.Spacing, "Upgrades_IDS_Name_284", "Upgrades_IDS_Name_285");
                return name != "";

            // ── Motor parts ─────────────────────────────────────────────────
            case DbUpgradeMotorPart mp:
                key = OffsetLevelKey(mp, 259);
                break;
        }

        if (key == null) return false;
        name = T(key);
        return name != key;
    }

    // The game stores an *absolute* tier in Level (0 = Stock, 1 = Street, 2 = Sport,
    // 3 = Race), independent of which tier the car happens to ship with. The stock part
    // for a given car may already sit at a non-zero tier (e.g. a Sport-grade engine
    // block), so we must not compute the tier relative to the stock entry — that shifts
    // every label down. The only special case is the stock entry itself, which the game
    // always presents as "Stock <category>" regardless of its physical tier.
    private string? OffsetLevelKey(DbUpgradePart part, int baseId, int raceId = -1)
    {
        int tier = part.IsStock ? 0 : part.Level;
        if (tier < 0 || tier > 3) return null;
        if (raceId > 0 && tier == 3)
            return $"Upgrades_IDS_Name_{raceId}";
        return LevelKey(baseId, tier);
    }

    private string? ResolveFuelSystemKey(DbUpgradeFuelSystem fs)
    {
        var engine = _db.GetEngine(fs.EngineID);
        int baseId = 11;
        if (engine?.Diesel == true)
        {
            // Diesel only has Stock/Street/Sport fuel system strings (212-214).
            int tier = fs.IsStock ? 0 : fs.Level;
            if (tier is >= 0 and <= 2)
                return LevelKey(212, tier);
            return null;
        }
        if (engine?.Carbureted == true)
            baseId = 216;
        return OffsetLevelKey(fs, baseId);
    }

    private string? ResolveIgnitionKey(DbUpgradeIgnition ig)
    {
        var engine = _db.GetEngine(ig.EngineID);
        if (engine?.Diesel == true)
        {
            // Only "Race Diesel Ignition" exists for diesel engines.
            return ig.IsStock ? "Upgrades_IDS_Name_15" : "Upgrades_IDS_Name_215";
        }
        return OffsetLevelKey(ig, 15);
    }

    private string? ResolveTireCompoundKey(DbUpgradeTireCompound tc)
    {
        // Check TireCompoundID override first: compounds like Drift share the same
        // TireModelName as another type (e.g. "Street") but have a distinct TireCompoundID,
        // so the name-based lookup would produce a collision.
        if (TireCompoundIdOverrides.TryGetValue(tc.TireCompoundID, out var compoundKey))
            return compoundKey;

        if (string.IsNullOrEmpty(tc.TireModelName)) return null;
        string root = NormalizeTireModelName(tc.TireModelName);
        return TireCompoundNameIds.TryGetValue(root, out var key) ? key : null;
    }

    // Body-kit parts (front/rear bumper, rear wing) share the same tier scheme: levels 0-3
    // map to baseId..baseId+3, level 4 is the brandless "Remove" option (removeKey).
    private bool ResolveBodyKit(DbUpgradePart part, int baseId, string removeKey, out string name)
    {
        if (part.Level >= 4) { name = T(removeKey); return true; }
        string? k = OffsetLevelKey(part, baseId);
        if (k == null) { name = ""; return false; }
        name = WithManufacturer(T(k), part);
        return name != "";
    }

    // Stock track width keeps its plain name; modified options append the spacing in mm
    // (DB stores metres) so the otherwise-identical tiers are distinguishable.
    private static string FormatTrackSpacing(bool isStock, double spacingMetres, string stockKey, string modKey)
    {
        if (isStock) return T(stockKey);
        double mm = spacingMetres * 1000;
        string amount = UseImperial
            ? $"{mm / 25.4:F2} {T("UnitInch")}"
            : $"{mm:F0} {T("UnitMm")}";
        return $"{T(modKey)} (+{amount})";
    }

    // Appends the manufacturer brand to a part name, so options that share a tier
    // (e.g. several street front bumpers) are distinguishable. Stock parts and the
    // generic "Stock" manufacturer (id 0/1) keep their plain tier name.
    private static string WithManufacturer(string baseName, DbUpgradePart part)
    {
        if (part.IsStock || part.ManufacturerID <= 1) return baseName;
        string m = T($"List_PartManufacturer_IDS_PartManufacturer_{part.ManufacturerID}");
        if (string.IsNullOrEmpty(m) || m.StartsWith("List_PartManufacturer")) return baseName;
        return $"{baseName} ({m})";
    }

    // The width table carries no profile column, but the game still shows an aspect ratio next
    // to every width tier — one that keeps the tire's overall (sidewall) diameter close to
    // stock as width increases, same as real-world "plus sizing" (wider tire → lower profile).
    // Verified against the in-game values for a Sprinter Trueno GT-APEX FE (stock 215/50):
    // front 225/50 235/45 245/45, rear 245/45 255/40 265/40 — matches round-to-nearest-5 of
    // (stockWidth * stockProfile / thisWidth).
    private static string FormatTireWidth(int widthMm, int stockWidthMm, int stockProfile, bool isStock)
    {
        int profile = EquivalentProfile(stockWidthMm, stockProfile, widthMm);
        string s = $"{widthMm}/{profile}";
        return isStock ? $"{s} ({T("Part_Stock")})" : s;
    }

    private static int EquivalentProfile(int stockWidthMm, int stockProfile, int widthMm)
    {
        if (widthMm <= 0 || stockWidthMm <= 0) return stockProfile;
        double sidewallMm = stockWidthMm * stockProfile / 100.0;
        double profile = sidewallMm / widthMm * 100.0;
        return (int)(Math.Round(profile / 5.0) * 5.0);
    }

    private static string FormatRimSize(int diameterIn, bool isStock)
    {
        string s = $"R{diameterIn}";
        return isStock ? $"{s} ({T("Part_Stock")})" : s;
    }

    private static string? LevelKey(int baseId, int offset)
    {
        if (offset < 0 || offset > 3) return null;
        return $"Upgrades_IDS_Name_{baseId + offset}";
    }

    private string GetLocalizedCategoryName(DbUpgradePart part)
    {
        string tableName = GetTableName(part);
        // Never look up an empty table name: Data_UpgradePart has a junk row with a blank
        // TableName whose PartName is "Aspiration", so GetUpgradePartInfo("") would mislabel
        // any unmapped part as "Aspiration".
        var info = string.IsNullOrEmpty(tableName) ? null : _db.GetUpgradePartInfo(tableName);
        string partName = info?.PartName ?? "";
        if (string.IsNullOrEmpty(partName))
            partName = part.GetType().Name.Replace("DbUpgrade", "");

        string key = $"Part_{partName}";
        string localized = T(key);
        return localized != key ? localized : partName;
    }

    private static string GetTableName(DbUpgradePart part)
    {
        var type = part.GetType();
        if (TableNameMap.TryGetValue(type, out var tableName))
            return tableName;
        if (type.BaseType != null && TableNameMap.TryGetValue(type.BaseType, out tableName))
            return tableName;
        return "";
    }

    private static string T(string key)
    {
        var loc = LocalizationService.Instance;
        if (loc.TryGet(key, out var value))
            return value;
        return key;
    }

    public ObservableCollection<PartOption> ToOptions<T>(List<T> parts, int makeId) where T : DbUpgradePart
    {
        var col = new ObservableCollection<PartOption>();
        foreach (var p in parts)
        {
            col.Add(new PartOption
            {
                Id = p.Id,
                DisplayName = Resolve(p, makeId),
                IsStock = p.IsStock
            });
        }
        return col;
    }

    private static string NormalizeTireModelName(string modelName)
    {
        var suffixes = new[] { "_Dually_FE_OW", "_Dually_OW", "_FE_OW", "_OW", "_FE", "_Dually" };
        foreach (var suffix in suffixes)
        {
            if (modelName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return modelName.Substring(0, modelName.Length - suffix.Length);
        }
        return modelName;
    }
}
