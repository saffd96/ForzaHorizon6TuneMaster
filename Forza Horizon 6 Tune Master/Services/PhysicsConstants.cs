namespace Forza_Horizon_6_Tune_Master;

/// <summary>
/// Physical unit conversions and shared reference values used by the tune calculators and the
/// car model. Centralised so each value lives in exactly one place — these were previously
/// duplicated as bare numeric literals across <c>Services/</c> and <c>Models/</c>, where a fix
/// in one copy could silently diverge from the others.
///
/// Lives in the root namespace so both <c>Forza_Horizon_6_Tune_Master.Services</c> and
/// <c>Forza_Horizon_6_Tune_Master.Models</c> see it without a using directive.
///
/// NOTE: only genuinely shared / unit-conversion values belong here. Discipline-specific tuning
/// tables and one-off empirical coefficients stay local to their calculator — moving them here
/// would hurt readability, not help it.
/// </summary>
internal static class PhysicsConstants
{
    // ── Unit conversions ─────────────────────────────────────────────────────
    public const double InchToMeter = 0.0254;        // length: in → m
    public const double MmPerInch   = 25.4;          // length: mm → in (divide)
    public const double HpToWatt    = 745.7;         // power: mechanical HP → W
    public const double KwToHp      = 1.341;         // power: kW → HP
    public const double DeciKwToHp  = KwToHp * 0.1;  // power: game 0.1-kW units → HP (0.1341)
    public const double NmRpmToHp   = 7121.0;        // power: torque[Nm] × rpm → HP
    public const double MsToKmh     = 3.6;           // speed: m/s → km/h
    public const double NmmToNm     = 1000.0;        // spring rate: N/mm → N/m

    // ── Physical constants ───────────────────────────────────────────────────
    public const double GravityMs2  = 9.81;          // gravitational acceleration, m/s²
    public const double AirDensity  = 1.225;         // air density, kg/m³ (sea level, 15 °C)

    // ── Shared empirical reference points ────────────────────────────────────
    /// <summary>Power-to-weight (HP per kg) above which calculators add a power-related bias.</summary>
    public const double PtwReferenceHpPerKg = 0.25;
    /// <summary>Reference vehicle mass (kg) used to scale mass-dependent factors.</summary>
    public const double RefMassKg = 1500.0;
}
