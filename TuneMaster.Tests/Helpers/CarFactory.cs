using Forza_Horizon_6_Tune_Master.Models;
using Models_DriveType = Forza_Horizon_6_Tune_Master.Models.DriveType;

namespace TuneMaster.Tests.Helpers;

/// <summary>
/// Factory methods producing realistic CarCard + TrackInfo + TuningConstraints
/// for use across all 100 tests.
/// </summary>
internal static class CarFactory
{
    // ── Default wide-open constraints (simulate game slider range) ────────────
    public static TuningConstraints DefaultConstraints() => new()
    {
        TirePressureFrontMin = 1.0,  TirePressureFrontMax = 3.8,
        TirePressureRearMin  = 1.0,  TirePressureRearMax  = 3.8,
        CamberFrontMin = -5, CamberFrontMax = 5,
        CamberRearMin  = -5, CamberRearMax  = 5,
        ToeFrontMin = -5, ToeFrontMax = 5,
        ToeRearMin  = -5, ToeRearMax  = 5,
        CasterMin = 1, CasterMax = 7,
        ARBFrontMin = 1, ARBFrontMax = 65,
        ARBRearMin  = 1, ARBRearMax  = 65,
        SpringFrontMin = 57.1,  SpringFrontMax = 285.6,
        SpringRearMin  = 57.1,  SpringRearMax  = 285.6,
        RideHeightFrontMin = 50,  RideHeightFrontMax = 250,
        RideHeightRearMin  = 50,  RideHeightRearMax  = 250,
        ReboundFrontMin = 1, ReboundFrontMax = 20,
        ReboundRearMin  = 1, ReboundRearMax  = 20,
        BumpFrontMin = 1, BumpFrontMax = 20,
        BumpRearMin  = 1, BumpRearMax  = 20,
        AeroFrontMin = 0, AeroFrontMax = 200,
        AeroRearMin  = 0, AeroRearMax  = 200,
        DiffAccelMin = 0, DiffAccelMax = 100,
        DiffDecelMin = 0, DiffDecelMax = 100,
        CenterDiffBias = 50,
        BrakeBalanceMin = 40, BrakeBalanceMax = 70,
        BrakePressureMin = 50, BrakePressureMax = 200,
        FinalDriveMin = 2.2, FinalDriveMax = 6.1,
    };

    // ── Preset cars ──────────────────────────────────────────────────────────

    /// Nissan GT-R R35 — AWD, TwinTurbo, front-heavy
    public static CarCard GtrR35() => new()
    {
        TotalMass = 1740, WeightDistributionFront = 54,
        PowerHP = 600, TorqueNm = 650, MaxRPM = 7000,
        DriveType = Models_DriveType.AWD, EnginePosition = EnginePosition.Front,
        AspirationType = AspirationType.TwinTurbo, PowertrainType = PowertrainType.ICE,
        EngineType = EngineType.V6, GearCount = 6,
        TireType = TireType.Sport, SuspensionUpgrade = SuspensionUpgrade.Sport,
        DifferentialUpgrade = DifferentialUpgrade.Sport,
        FrontTireWidth = 255, FrontTireProfile = 40, FrontRimDiameter = 20,
        RearTireWidth  = 285, RearTireProfile  = 35, RearRimDiameter  = 20,
        Wheelbase = 2780,
        HasFrontAero = false, HasRearAero = false,
    };

    /// Toyota Supra A90 — RWD, SingleTurbo, rear-heavy-ish
    public static CarCard SupraA90() => new()
    {
        TotalMass = 1570, WeightDistributionFront = 50,
        PowerHP = 450, TorqueNm = 500, MaxRPM = 7000,
        DriveType = Models_DriveType.RWD, EnginePosition = EnginePosition.Front,
        AspirationType = AspirationType.SingleTurbo, PowertrainType = PowertrainType.ICE,
        EngineType = EngineType.I6, GearCount = 8,
        TireType = TireType.Sport, SuspensionUpgrade = SuspensionUpgrade.Sport,
        DifferentialUpgrade = DifferentialUpgrade.Sport,
        FrontTireWidth = 255, FrontTireProfile = 35, FrontRimDiameter = 19,
        RearTireWidth  = 275, RearTireProfile  = 35, RearRimDiameter  = 19,
        Wheelbase = 2470,
        HasFrontAero = false, HasRearAero = false,
    };

    /// Honda Civic Type R FN2 — FWD, NA, front-heavy
    public static CarCard CivicTypeR() => new()
    {
        TotalMass = 1380, WeightDistributionFront = 62,
        PowerHP = 200, TorqueNm = 193, MaxRPM = 8000,
        DriveType = Models_DriveType.FWD, EnginePosition = EnginePosition.Front,
        AspirationType = AspirationType.Natural, PowertrainType = PowertrainType.ICE,
        EngineType = EngineType.I4, GearCount = 6,
        TireType = TireType.Sport, SuspensionUpgrade = SuspensionUpgrade.Race,
        DifferentialUpgrade = DifferentialUpgrade.Race,
        FrontTireWidth = 235, FrontTireProfile = 35, FrontRimDiameter = 18,
        RearTireWidth  = 235, RearTireProfile  = 35, RearRimDiameter  = 18,
        Wheelbase = 2620,
        HasFrontAero = false, HasRearAero = false,
    };

    /// Koenigsegg Gemera — AWD, TwinTurbo, mid-engine drag beast
    public static CarCard Gemera() => new()
    {
        TotalMass = 1401, WeightDistributionFront = 49,
        PowerHP = 2300, TorqueNm = 2750, MaxRPM = 9500,
        DriveType = Models_DriveType.AWD, EnginePosition = EnginePosition.Mid,
        AspirationType = AspirationType.TwinTurbo, PowertrainType = PowertrainType.ICE,
        EngineType = EngineType.V8, GearCount = 10,
        TireType = TireType.Drag,
        SuspensionUpgrade = SuspensionUpgrade.Stock,
        DifferentialUpgrade = DifferentialUpgrade.Stock,
        FrontTireWidth = 315, FrontTireProfile = 30, FrontRimDiameter = 19,
        RearTireWidth  = 265, RearTireProfile  = 35, RearRimDiameter  = 19,
        Wheelbase = 3000,
        HasFrontAero = false, HasRearAero = false,
    };

    /// Mazda MX-5 ND — RWD, NA, lightweight
    public static CarCard Mx5() => new()
    {
        TotalMass = 1000, WeightDistributionFront = 52,
        PowerHP = 132, TorqueNm = 150, MaxRPM = 7500,
        DriveType = Models_DriveType.RWD, EnginePosition = EnginePosition.Front,
        AspirationType = AspirationType.Natural, PowertrainType = PowertrainType.ICE,
        EngineType = EngineType.I4, GearCount = 6,
        TireType = TireType.Sport, SuspensionUpgrade = SuspensionUpgrade.Sport,
        DifferentialUpgrade = DifferentialUpgrade.Stock,
        FrontTireWidth = 205, FrontTireProfile = 45, FrontRimDiameter = 17,
        RearTireWidth  = 205, RearTireProfile  = 45, RearRimDiameter  = 17,
        Wheelbase = 2310,
        HasFrontAero = false, HasRearAero = false,
    };

    /// Jeep Wrangler — AWD, Offroad, CC setup
    public static CarCard Wrangler() => new()
    {
        TotalMass = 2100, WeightDistributionFront = 52,
        PowerHP = 285, TorqueNm = 347, MaxRPM = 6400,
        DriveType = Models_DriveType.AWD, EnginePosition = EnginePosition.Front,
        AspirationType = AspirationType.Natural, PowertrainType = PowertrainType.ICE,
        EngineType = EngineType.V6, GearCount = 6,
        TireType = TireType.Offroad, SuspensionUpgrade = SuspensionUpgrade.Rally,
        DifferentialUpgrade = DifferentialUpgrade.Sport,
        FrontTireWidth = 285, FrontTireProfile = 70, FrontRimDiameter = 17,
        RearTireWidth  = 285, RearTireProfile  = 70, RearRimDiameter  = 17,
        Wheelbase = 2949,
        HasFrontAero = false, HasRearAero = false,
    };

    /// Porsche 911 GT3 RS — RWD, NA, rear-engine
    public static CarCard Gt3Rs() => new()
    {
        TotalMass = 1450, WeightDistributionFront = 38,
        PowerHP = 525, TorqueNm = 465, MaxRPM = 9000,
        DriveType = Models_DriveType.RWD, EnginePosition = EnginePosition.Rear,
        AspirationType = AspirationType.Natural, PowertrainType = PowertrainType.ICE,
        EngineType = EngineType.Boxer, GearCount = 7,
        TireType = TireType.SemiSlick, SuspensionUpgrade = SuspensionUpgrade.Race,
        DifferentialUpgrade = DifferentialUpgrade.Race,
        FrontTireWidth = 265, FrontTireProfile = 35, FrontRimDiameter = 21,
        RearTireWidth  = 325, RearTireProfile  = 30, RearRimDiameter  = 21,
        Wheelbase = 2457,
        HasFrontAero = true, HasRearAero = true,
    };

    /// Tesla Model S Plaid — AWD, Electric
    public static CarCard ModelSPlaid() => new()
    {
        TotalMass = 2162, WeightDistributionFront = 48,
        PowerHP = 1020, TorqueNm = 1420, MaxRPM = 20000,
        DriveType = Models_DriveType.AWD, EnginePosition = EnginePosition.Mid,
        AspirationType = null, PowertrainType = PowertrainType.Electric,
        EngineType = EngineType.V8, GearCount = 1,
        TireType = TireType.Sport, SuspensionUpgrade = SuspensionUpgrade.Sport,
        DifferentialUpgrade = DifferentialUpgrade.Race,
        FrontTireWidth = 245, FrontTireProfile = 45, FrontRimDiameter = 21,
        RearTireWidth  = 265, RearTireProfile  = 45, RearRimDiameter  = 21,
        Wheelbase = 2960,
        HasFrontAero = false, HasRearAero = false,
    };

    /// Subaru WRX STI — AWD, SingleTurbo, rally setup
    public static CarCard WrxSti() => new()
    {
        TotalMass = 1510, WeightDistributionFront = 56,
        PowerHP = 310, TorqueNm = 422, MaxRPM = 6000,
        DriveType = Models_DriveType.AWD, EnginePosition = EnginePosition.Front,
        AspirationType = AspirationType.SingleTurbo, PowertrainType = PowertrainType.ICE,
        EngineType = EngineType.Boxer, GearCount = 6,
        TireType = TireType.Rally, SuspensionUpgrade = SuspensionUpgrade.Rally,
        DifferentialUpgrade = DifferentialUpgrade.Sport,
        FrontTireWidth = 245, FrontTireProfile = 45, FrontRimDiameter = 17,
        RearTireWidth  = 245, RearTireProfile  = 45, RearRimDiameter  = 17,
        Wheelbase = 2650,
        HasFrontAero = false, HasRearAero = true,
    };

    /// Dodge Challenger SRT Hellcat — RWD, PositiveDisplacement, drag
    public static CarCard Hellcat() => new()
    {
        TotalMass = 2025, WeightDistributionFront = 57,
        PowerHP = 717, TorqueNm = 881, MaxRPM = 6200,
        DriveType = Models_DriveType.RWD, EnginePosition = EnginePosition.Front,
        AspirationType = AspirationType.PositiveDisplacement, PowertrainType = PowertrainType.ICE,
        EngineType = EngineType.V8, GearCount = 8,
        TireType = TireType.Drag,
        SuspensionUpgrade = SuspensionUpgrade.Street,
        DifferentialUpgrade = DifferentialUpgrade.Race,
        FrontTireWidth = 275, FrontTireProfile = 40, FrontRimDiameter = 20,
        RearTireWidth  = 305, RearTireProfile  = 35, RearRimDiameter  = 20,
        Wheelbase = 3020,
        HasFrontAero = false, HasRearAero = false,
    };

    /// Porsche 918 Spyder — Hybrid, AWD, Mid, 887 HP
    public static CarCard Porsche918() => new()
    {
        TotalMass = 1674, WeightDistributionFront = 43,
        PowerHP = 887, TorqueNm = 1000, MaxRPM = 9150,
        DriveType = Models_DriveType.AWD, EnginePosition = EnginePosition.Mid,
        AspirationType = AspirationType.Natural, PowertrainType = PowertrainType.Hybrid,
        EngineType = EngineType.V8, GearCount = 7,
        TireType = TireType.SemiSlick, SuspensionUpgrade = SuspensionUpgrade.Race,
        DifferentialUpgrade = DifferentialUpgrade.Race,
        FrontTireWidth = 265, FrontTireProfile = 35, FrontRimDiameter = 21,
        RearTireWidth  = 325, RearTireProfile  = 30, RearRimDiameter  = 21,
        Wheelbase = 2730,
        HasFrontAero = true, HasRearAero = true,
    };

    /// Mitsubishi Lancer Evo X — AWD, SingleTurbo, AntiLag
    public static CarCard LancerEvoXAntiLag() => new()
    {
        TotalMass = 1600, WeightDistributionFront = 58,
        PowerHP = 350, TorqueNm = 470, MaxRPM = 7000,
        DriveType = Models_DriveType.AWD, EnginePosition = EnginePosition.Front,
        AspirationType = AspirationType.SingleTurbo, PowertrainType = PowertrainType.ICE,
        AntiLag = true,
        EngineType = EngineType.I4, GearCount = 6,
        TireType = TireType.Sport, SuspensionUpgrade = SuspensionUpgrade.Race,
        DifferentialUpgrade = DifferentialUpgrade.Race,
        FrontTireWidth = 245, FrontTireProfile = 40, FrontRimDiameter = 18,
        RearTireWidth  = 245, RearTireProfile  = 40, RearRimDiameter  = 18,
        Wheelbase = 2650,
        HasFrontAero = false, HasRearAero = false,
    };

    /// McLaren 720S — RWD, TwinTurbo, Mid
    public static CarCard McLaren720S() => new()
    {
        TotalMass = 1283, WeightDistributionFront = 42,
        PowerHP = 710, TorqueNm = 770, MaxRPM = 8500,
        DriveType = Models_DriveType.RWD, EnginePosition = EnginePosition.Mid,
        AspirationType = AspirationType.TwinTurbo, PowertrainType = PowertrainType.ICE,
        EngineType = EngineType.V8, GearCount = 7,
        TireType = TireType.SemiSlick, SuspensionUpgrade = SuspensionUpgrade.Race,
        DifferentialUpgrade = DifferentialUpgrade.Race,
        FrontTireWidth = 245, FrontTireProfile = 35, FrontRimDiameter = 20,
        RearTireWidth  = 305, RearTireProfile  = 30, RearRimDiameter  = 21,
        Wheelbase = 2670,
        HasFrontAero = true, HasRearAero = true,
    };

    /// Bugatti Chiron — AWD, TwinTurbo (quad-turbo mapped), Mid, extreme power
    public static CarCard BugattiChiron() => new()
    {
        TotalMass = 1997, WeightDistributionFront = 46,
        PowerHP = 1479, TorqueNm = 1600, MaxRPM = 6700,
        DriveType = Models_DriveType.AWD, EnginePosition = EnginePosition.Mid,
        AspirationType = AspirationType.TwinTurbo, PowertrainType = PowertrainType.ICE,
        EngineType = EngineType.V8, GearCount = 7,
        TireType = TireType.SemiSlick, SuspensionUpgrade = SuspensionUpgrade.Race,
        DifferentialUpgrade = DifferentialUpgrade.Race,
        FrontTireWidth = 285, FrontTireProfile = 30, FrontRimDiameter = 21,
        RearTireWidth  = 355, RearTireProfile  = 25, RearRimDiameter  = 21,
        Wheelbase = 2711,
        HasFrontAero = true, HasRearAero = true,
    };

    /// Honda S2000 AP2 — RWD, NA, high-rev front-engine
    public static CarCard HondaS2000() => new()
    {
        TotalMass = 1250, WeightDistributionFront = 52,
        PowerHP = 237, TorqueNm = 220, MaxRPM = 9000,
        DriveType = Models_DriveType.RWD, EnginePosition = EnginePosition.Front,
        AspirationType = AspirationType.Natural, PowertrainType = PowertrainType.ICE,
        EngineType = EngineType.I4, GearCount = 6,
        TireType = TireType.Sport, SuspensionUpgrade = SuspensionUpgrade.Race,
        DifferentialUpgrade = DifferentialUpgrade.Sport,
        FrontTireWidth = 205, FrontTireProfile = 55, FrontRimDiameter = 17,
        RearTireWidth  = 225, RearTireProfile  = 50, RearRimDiameter  = 17,
        Wheelbase = 2400,
        HasFrontAero = false, HasRearAero = false,
    };

    // ── Tracks ───────────────────────────────────────────────────────────────
    public static TrackInfo Road()         => new() { Discipline = Discipline.Road };
    public static TrackInfo Drag(DragDistance d = DragDistance.Quarter) => new() { Discipline = Discipline.Drag, DragDistance = d };
    public static TrackInfo Drift()        => new() { Discipline = Discipline.Drift };
    public static TrackInfo Rally()        => new() { Discipline = Discipline.Rally };
    public static TrackInfo CrossCountry() => new() { Discipline = Discipline.CrossCountry };
    public static TrackInfo Touge()        => new() { Discipline = Discipline.Touge };
    public static TrackInfo Street()       => new() { Discipline = Discipline.Street };
}
