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

    // Cache of sorted distinct levels for each (part type, parent id) group.
    // Used to map a part's Level to Stock/Street/Sport/Rank regardless of gaps
    // or whether the stock entry starts at Level 0 or Level 1.
    private readonly Dictionary<(Type Type, int ParentId), List<int>> _levelCache = new();

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
        { typeof(DbUpgradeRearWing),            "List_UpgradeRearWing" },
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
                    string driveKey = drv.DriveTypeID switch
                    {
                        0 => "List_DriveType_IDS_DisplayName_1",
                        2 => "List_DriveType_IDS_DisplayName_3",
                        _ => "List_DriveType_IDS_DisplayName_2",
                    };
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
                {
                    string? k = OffsetLevelKey(w, 90);
                    if (k == null) return false;
                    name = WithManufacturer(T(k), w);
                    return name != "";
                }

            // ── Body kits ──────────────────────────────────────────────────
            // Multiple options share the same tier and differ only by brand, so the
            // manufacturer name is what disambiguates them in the dropdown.
            case DbUpgradeFrontBumper fb:
                {
                    string? k = OffsetLevelKey(fb, 86);
                    if (k == null) return false;
                    name = WithManufacturer(T(k), fb);
                    return name != "";
                }
            case DbUpgradeRearBumper rb:
                {
                    string? k = OffsetLevelKey(rb, 94);
                    if (k == null) return false;
                    name = WithManufacturer(T(k), rb);
                    return name != "";
                }
            case DbUpgradeSideSkirt ss:
                {
                    // Side skirts only have Stock (98) and Street (99) game strings.
                    string baseName = T(ss.IsStock ? "Upgrades_IDS_Name_98" : "Upgrades_IDS_Name_99");
                    name = WithManufacturer(baseName, ss);
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
                name = FormatTireWidth(twf.FrontTireWidth, StockFrontTireProfile, twf.IsStock);
                return true;
            case DbUpgradeTireWidthRear twr:
                name = FormatTireWidth(twr.RearTireWidth, StockRearTireProfile, twr.IsStock);
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
            case DbUpgradeTrackSpacingFront tsf:
                key = tsf.IsStock ? "Upgrades_IDS_Name_282" : "Upgrades_IDS_Name_283";
                break;
            case DbUpgradeTrackSpacingRear tsr:
                key = tsr.IsStock ? "Upgrades_IDS_Name_284" : "Upgrades_IDS_Name_285";
                break;

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

    private int GetStockLevel(DbUpgradePart part)
    {
        int parentId = GetParentId(part);
        if (parentId < 0) return -1;
        var list = GetPartList(part, parentId);
        return list.FirstOrDefault(p => p.IsStock)?.Level ?? list.Min(p => (int?)p.Level) ?? -1;
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
        if (string.IsNullOrEmpty(tc.TireModelName)) return null;
        string root = NormalizeTireModelName(tc.TireModelName);
        return TireCompoundNameIds.TryGetValue(root, out var key) ? key : null;
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

    private static string FormatTireWidth(int widthMm, int profile, bool isStock)
    {
        string s = $"{widthMm}/{profile}";
        return isStock ? $"{s} ({T("Part_Stock")})" : s;
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

    private string? RankKey(int baseId, DbUpgradePart part)
    {
        int rank = GetRank(part);
        return rank >= 0 ? LevelKey(baseId, rank) : null;
    }

    private int GetRank(DbUpgradePart part)
    {
        int parentId = GetParentId(part);
        if (parentId < 0) return -1;

        var key = (part.GetType(), parentId);
        if (!_levelCache.TryGetValue(key, out var levels))
        {
            levels = GetSortedLevels(part, parentId);
            _levelCache[key] = levels;
        }
        return levels.IndexOf(part.Level);
    }

    private static List<int> GetSortedLevels(DbUpgradePart part, int parentId)
    {
        return GetPartList(part, parentId).Select(p => p.Level).Distinct().OrderBy(l => l).ToList();
    }

    private static List<DbUpgradePart> GetPartList(DbUpgradePart part, int parentId)
    {
        IEnumerable<DbUpgradePart> all = part switch
        {
            // Engine parts
            DbUpgradeCamshaft => Fh6DatabaseService.Instance.GetCamshafts(parentId).Cast<DbUpgradePart>(),
            DbUpgradeValves => Fh6DatabaseService.Instance.GetValves(parentId).Cast<DbUpgradePart>(),
            DbUpgradeDisplacement => Fh6DatabaseService.Instance.GetDisplacement(parentId).Cast<DbUpgradePart>(),
            DbUpgradePistons => Fh6DatabaseService.Instance.GetPistons(parentId).Cast<DbUpgradePart>(),
            DbUpgradeFuelSystem => Fh6DatabaseService.Instance.GetFuelSystems(parentId).Cast<DbUpgradePart>(),
            DbUpgradeIgnition => Fh6DatabaseService.Instance.GetIgnition(parentId).Cast<DbUpgradePart>(),
            DbUpgradeExhaust => Fh6DatabaseService.Instance.GetExhaust(parentId).Cast<DbUpgradePart>(),
            DbUpgradeIntake => Fh6DatabaseService.Instance.GetIntake(parentId).Cast<DbUpgradePart>(),
            DbUpgradeManifold => Fh6DatabaseService.Instance.GetManifolds(parentId).Cast<DbUpgradePart>(),
            DbUpgradeFlywheel => Fh6DatabaseService.Instance.GetFlywheels(parentId).Cast<DbUpgradePart>(),
            DbUpgradeOilCooling => Fh6DatabaseService.Instance.GetOilCooling(parentId).Cast<DbUpgradePart>(),
            DbUpgradeRestrictor => Fh6DatabaseService.Instance.GetRestrictors(parentId).Cast<DbUpgradePart>(),
            DbUpgradeTurboSingle => Fh6DatabaseService.Instance.GetTurbosSingle(parentId).Cast<DbUpgradePart>(),
            DbUpgradeTurboTwin => Fh6DatabaseService.Instance.GetTurbosTwin(parentId).Cast<DbUpgradePart>(),
            DbUpgradeCSC => Fh6DatabaseService.Instance.GetCSC(parentId).Cast<DbUpgradePart>(),
            DbUpgradeDSC => Fh6DatabaseService.Instance.GetDSC(parentId).Cast<DbUpgradePart>(),
            DbUpgradeIntercooler => Fh6DatabaseService.Instance.GetIntercoolers(parentId).Cast<DbUpgradePart>(),

            // Drivetrain parts
            DbUpgradeTransmission => Fh6DatabaseService.Instance.GetTransmissions(parentId).Cast<DbUpgradePart>(),
            DbUpgradeClutch => Fh6DatabaseService.Instance.GetClutches(parentId).Cast<DbUpgradePart>(),
            DbUpgradeDriveline => Fh6DatabaseService.Instance.GetDrivelines(parentId).Cast<DbUpgradePart>(),
            DbUpgradeDifferential => Fh6DatabaseService.Instance.GetDifferentials(parentId).Cast<DbUpgradePart>(),

            // Ordinal parts
            DbUpgradeTireCompound => Fh6DatabaseService.Instance.GetTireCompounds(parentId).Cast<DbUpgradePart>(),
            DbUpgradeSpringDamper => Fh6DatabaseService.Instance.GetSpringDampers(parentId).Cast<DbUpgradePart>(),
            DbUpgradeBrakes => Fh6DatabaseService.Instance.GetBrakes(parentId).Cast<DbUpgradePart>(),
            DbUpgradeAntiSwayFront => Fh6DatabaseService.Instance.GetAntiSwayFront(parentId).Cast<DbUpgradePart>(),
            DbUpgradeAntiSwayRear => Fh6DatabaseService.Instance.GetAntiSwayRear(parentId).Cast<DbUpgradePart>(),
            DbUpgradeRearWing => Fh6DatabaseService.Instance.GetRearWings(parentId).Cast<DbUpgradePart>(),
            DbUpgradeRimFront => Fh6DatabaseService.Instance.GetRimsFront(parentId).Cast<DbUpgradePart>(),
            DbUpgradeRimRear => Fh6DatabaseService.Instance.GetRimsRear(parentId).Cast<DbUpgradePart>(),

            // CarBody parts
            DbUpgradeWeightReduction => Fh6DatabaseService.Instance.GetWeightReductions(parentId).Cast<DbUpgradePart>(),
            DbUpgradeChassisStiffness => Fh6DatabaseService.Instance.GetChassisStiffness(parentId).Cast<DbUpgradePart>(),
            DbUpgradeTireWidthFront => Fh6DatabaseService.Instance.GetTireWidthsFront(parentId).Cast<DbUpgradePart>(),
            DbUpgradeTireWidthRear => Fh6DatabaseService.Instance.GetTireWidthsRear(parentId).Cast<DbUpgradePart>(),
            DbUpgradeTireAspectRatioFront => Fh6DatabaseService.Instance.GetTireAspectRatiosFront(parentId).Cast<DbUpgradePart>(),
            DbUpgradeTireAspectRatioRear => Fh6DatabaseService.Instance.GetTireAspectRatiosRear(parentId).Cast<DbUpgradePart>(),
            DbUpgradeTrackSpacingFront => Fh6DatabaseService.Instance.GetTrackSpacingsFront(parentId).Cast<DbUpgradePart>(),
            DbUpgradeTrackSpacingRear => Fh6DatabaseService.Instance.GetTrackSpacingsRear(parentId).Cast<DbUpgradePart>(),

            // Motor parts
            DbUpgradeMotorPart => Fh6DatabaseService.Instance.GetMotorParts(parentId).Cast<DbUpgradePart>(),

            _ => Enumerable.Empty<DbUpgradePart>()
        };

        return all.ToList();
    }

    private static int GetParentId(DbUpgradePart part)
    {
        return part switch
        {
            DbUpgradeCamshaft p => p.EngineID,
            DbUpgradeValves p => p.EngineID,
            DbUpgradeDisplacement p => p.EngineID,
            DbUpgradePistons p => p.EngineID,
            DbUpgradeFuelSystem p => p.EngineID,
            DbUpgradeIgnition p => p.EngineID,
            DbUpgradeExhaust p => p.EngineID,
            DbUpgradeIntake p => p.EngineID,
            DbUpgradeManifold p => p.EngineID,
            DbUpgradeFlywheel p => p.EngineID,
            DbUpgradeOilCooling p => p.EngineID,
            DbUpgradeRestrictor p => p.EngineID,
            DbUpgradeTurboSingle p => p.EngineID,
            DbUpgradeTurboTwin p => p.EngineID,
            DbUpgradeCSC p => p.EngineID,
            DbUpgradeDSC p => p.EngineID,
            DbUpgradeIntercooler p => p.EngineID,

            DbUpgradeTransmission p => p.DrivetrainID,
            DbUpgradeClutch p => p.DrivetrainID,
            DbUpgradeDriveline p => p.DrivetrainId,
            DbUpgradeDifferential p => p.DrivetrainID,

            DbUpgradeTireCompound p => p.Ordinal,
            DbUpgradeSpringDamper p => p.Ordinal,
            DbUpgradeBrakes p => p.Ordinal,
            DbUpgradeAntiSwayFront p => p.Ordinal,
            DbUpgradeAntiSwayRear p => p.Ordinal,
            DbUpgradeRearWing p => p.Ordinal,
            DbUpgradeRimFront p => p.Ordinal,
            DbUpgradeRimRear p => p.Ordinal,

            DbUpgradeWeightReduction p => p.CarBodyId,
            DbUpgradeChassisStiffness p => p.CarbodyId,
            DbUpgradeTireWidthFront p => p.CarBodyId,
            DbUpgradeTireWidthRear p => p.CarBodyId,
            DbUpgradeTireAspectRatioFront p => p.CarBodyId,
            DbUpgradeTireAspectRatioRear p => p.CarBodyId,
            DbUpgradeTrackSpacingFront p => p.CarBodyId,
            DbUpgradeTrackSpacingRear p => p.CarBodyId,

            DbUpgradeMotorPart p => p.MotorID,

            _ => -1
        };
    }

    private string GetLocalizedCategoryName(DbUpgradePart part)
    {
        string tableName = GetTableName(part);
        var info = _db.GetUpgradePartInfo(tableName);
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
