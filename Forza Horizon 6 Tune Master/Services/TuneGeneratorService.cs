using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

public class TuneGeneratorService
{
    private static double Clamp(double v, double min, double max) => Math.Max(min, Math.Min(max, v));

    private const double GearRatioMin = 0.48;
    private const double GearRatioMax = 6.10;

    // Named calibration constants (FH6 community baselines)
    private const double PowerBaselineHP    = 300;
    private const double PowerStepHP        = 200;
    private const double TorqueBaselineNm   = 400;
    private const double MassBaselineKg     = 1400;
    private const double RefMassKg          = 1500;
    private const double RefWheelbaseMm     = 2700;
    private const double RefRimDiameterInch = 19;
    private const double ProfileBaseline    = 45;
    private const double RefFrontTrackMm    = 1550;
    private const double RefSpeedKmh        = 200;
    // 4π²/2000 = 0.019739 — includes ÷1000 (N/m→N/mm) and ÷2 (motion-ratio/half-axle calibration for FH6)
    private const double SpringHzToNmm      = 0.019739;
    private const double RevLimitFraction   = 0.95;

    // Returns (diffFactor, springRearFactor, damperRearFactor) based on powertrain and aspiration type.
    // Electric BEV always uses max instant-torque factors regardless of stored AspirationType.
    // Hybrid moderates aspiration factors by 40% (electric fill smooths power delivery spikes).
    private static (double Diff, double Spring, double Damper) GetPowerDeliveryFactors(
        PowertrainType pt, AspirationType asp, bool antiLag = false)
    {
        if (pt == PowertrainType.Electric)
            return (1.20, 1.08, 1.06);

        var (d, s, dm) = asp switch
        {
            AspirationType.SingleTurbo          => antiLag ? (1.12, 1.05, 1.05) : (1.10, 1.05, 1.04),
            AspirationType.TwinTurbo            => antiLag ? (1.09, 1.04, 1.04) : (1.07, 1.04, 1.03),
            AspirationType.PositiveDisplacement => (1.05, 1.03, 1.03),
            AspirationType.Centrifugal          => (1.03, 1.02, 1.01),
            AspirationType.Electric             => (1.20, 1.08, 1.06),
            _                                   => (1.00, 1.00, 1.00),
        };

        if (pt == PowertrainType.Hybrid)
        {
            // Hybrid: electric motor smooths ICE torque spikes → 40% reduction in power-delivery factor amplitude
            d  = 1.0 + (d  - 1.0) * 0.60;
            s  = 1.0 + (s  - 1.0) * 0.60;
            dm = 1.0 + (dm - 1.0) * 0.60;
        }

        return (d, s, dm);
    }

    // Drag-limited speed: body CdA + wing-induced drag from computed aero.
    // AeroDragFactor = 2g / (ρ × v_ref² × L/D) — empirically fitted from FH community data.
    // Forza uses HP at-wheel with no drivetrain loss (forums.forza.net confirmed).
    private static double ComputeEffectiveMaxSpeedKmh(CarCard car, TuneResult r)
    {
        double avgProfile = (car.FrontTireProfile + car.RearTireProfile) / 2.0;
        double bodyFactor = Math.Clamp(1.0 + Math.Max(0, (avgProfile - 45.0) / 20.0) * 2.0, 1.0, 3.5);
        double cdABody    = car.Cd > 0 && car.FrontalAreaM2 > 0
            ? car.Cd * car.FrontalAreaM2
            : (0.50 + car.TotalMass / 2500.0) * bodyFactor;
        const double AeroDragFactor = 0.001787; // m² per kg of displayed downforce — fitted from FH5/FH6 community data
        double cdAWing    = (r.AeroFront + r.AeroRear) * AeroDragFactor;
        double vMs        = Math.Pow(car.PowerHP * 745.7 / (0.5 * 1.225 * (cdABody + cdAWing)), 1.0 / 3.0);
        return Math.Round(Math.Clamp(vMs * 3.6, 60.0, 600.0));
    }

    public TuneResult Generate(CarCard car, TrackInfo track, TuningConstraints c)
    {
        var r  = new TuneResult { Car = car, Track = track };
        var ex = r.Explanations;

        // Aero runs first so effectiveMaxKmh (which uses r.AeroFront/Rear) is ready for all other methods
        CalculateAero(car, track, c, r, ex);
        double effectiveMaxKmh = ComputeEffectiveMaxSpeedKmh(car, r);

        CalculateTirePressure(car, track, c, r, ex);
        CalculateCamber(car, track, c, r, ex);
        CalculateToe(car, track, c, r, ex);
        CalculateCaster(car, track, c, r, ex, effectiveMaxKmh);
        CalculateARB(car, track, c, r, ex);
        CalculateSprings(car, track, c, r, ex);
        CalculateRideHeight(car, track, c, r, ex);
        CalculateDampers(car, track, c, r, ex);
        CalculateDifferential(car, track, c, r, ex);
        CalculateBrakes(car, track, c, r, ex, effectiveMaxKmh);
        CalculateGearing(car, track, c, r, ex, effectiveMaxKmh);
        if (track.Discipline == Discipline.Drag)
            CalculateLaunchControl(car, r);

        return r;
    }

    private static void CalculateLaunchControl(CarCard car, TuneResult r)
    {
        if (car.PowertrainType == PowertrainType.Electric)
        {
            r.LaunchControlRpm = 1000;
            return;
        }

        double torquePeak = car.TorquePeakRPM;

        // Floors are proportional to MaxRPM so low-redline engines (diesel, small-displacement)
        // don't get pushed to 70%+ of their redline by a floor calibrated for high-rev gasoline.
        double baseLaunch = car.AspirationType switch
        {
            AspirationType.TwinTurbo            => car.AntiLag
                                                   ? Math.Max(car.MaxRPM * 0.37, torquePeak * 0.60)
                                                   : Math.Max(car.MaxRPM * 0.32, torquePeak * 0.55),
            AspirationType.SingleTurbo          => car.AntiLag
                                                   ? Math.Max(car.MaxRPM * 0.42, torquePeak * 0.65)
                                                   : Math.Max(car.MaxRPM * 0.38, torquePeak * 0.60),
            AspirationType.PositiveDisplacement => Math.Max(car.MaxRPM * 0.28, torquePeak * 0.65),
            AspirationType.Centrifugal          => Math.Max(car.MaxRPM * 0.25, torquePeak * 0.72),
            AspirationType.Electric             => Math.Max(car.MaxRPM * 0.15, torquePeak * 0.50), // electric boost: instant torque, low launch RPM
            _                                   => Math.Max(car.MaxRPM * 0.20, torquePeak * 0.70)
        };

        double driveAdj = car.DriveType switch
        {
            DriveType.AWD => 1.10,  // AWD: best launch traction across all 4 wheels
            DriveType.RWD => 1.00,  // RWD: weight transfers TO rear → aids traction
            DriveType.FWD => 0.80,  // FWD: weight transfers AWAY from driven wheels → hurts traction
            _             => 1.00
        };

        // Torque: high-torque engines need lower launch RPM to avoid spinning wheels
        // Factor scales from 1.0 (400 Nm baseline) down to 0.65 (extreme torque)
        double torqueFactor = Math.Clamp(1.0 - Math.Max(0, car.TorqueNm - TorqueBaselineNm) / 1500.0, 0.65, 1.0);
        double launch = Math.Clamp(baseLaunch * driveAdj * torqueFactor, 1000, car.MaxRPM * 0.75);
        r.LaunchControlRpm = Math.Round(launch / 100.0) * 100;
    }

    private static double EffectiveWtDist(CarCard car)
    {
        if (Math.Abs(car.WeightDistributionFront - 50) > 2)
            return car.WeightDistributionFront;
        return car.EnginePosition switch
        {
            EnginePosition.Front   => 55,
            EnginePosition.Mid     => 48,
            EnginePosition.RearMid => 43,  // between Mid and Rear: rear-biased dynamic load
            EnginePosition.Rear    => 40,
            _                      => 50
        };
    }

    // ── Tire Pressure ────────────────────────────────────────────────────────
    // Community baselines (forzafire, forza.guide, fh6tune):
    //   Road: 28–32 PSI (1.93–2.21 bar)
    //   Drift: raise rear (35–40 PSI = 2.41–2.76 bar)
    //   CC/Rally: drop 2–4 PSI (0.14–0.28 bar)
    //   Drag: max front, min rear

    private static void CalculateTirePressure(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r, Dictionary<string, string> ex)
    {
        // Tire-type base (bar). Source: ForzaFire FH6 tires guide (forzafire.com)
        // Slick=32.5 PSI, SemiSlick=32.0, Sport=31.5, Stock/Street=31.0, Rally=29.5, Offroad=29.0
        double baseBar = car.TireType switch
        {
            TireType.Slick     => 2.24, // 32.5 PSI
            TireType.SemiSlick => 2.21, // 32.0 PSI
            TireType.Sport     => 2.07, // 30.0 PSI
            TireType.Street    => 2.14,
            TireType.Stock     => 2.14,
            TireType.Rally     => 2.03,
            TireType.Offroad   => 2.00,
            TireType.Drag      => 2.21,
            _                  => 2.14
        };

        // Mass: +0.05 bar per 200 kg over 1400
        double massAdj = (car.TotalMass - MassBaselineKg) / 200.0 * 0.05;

        // Weight distribution: heavier end gets more pressure
        double wd = EffectiveWtDist(car);
        double wdDev = (wd - 50) / 50.0;
        double wdAdjF = wdDev * 0.25;
        double wdAdjR = -wdDev * 0.25;

        // Profile: lower profile → stiffer sidewall → slightly higher pressure
        double profile = (car.FrontTireProfile + car.RearTireProfile) / 2.0;
        double profileAdj = Math.Clamp((ProfileBaseline - profile) * 0.004, -0.15, 0.15);

        // Rim diameter: bigger rim → lower sidewall → slightly higher pressure
        double rim = (car.FrontRimDiameter + car.RearRimDiameter) / 2.0;
        double rimAdj = Math.Clamp((rim - RefRimDiameterInch) * 0.02, -0.10, 0.15);

        // Power-based adjustments
        // RWD: more power needs lower rear pressure for traction, slightly higher front for stability
        // FWD: more power needs lower front pressure for traction
        // AWD: balanced increase both axles
        // Drag skipped — rear pressure is intentionally minimised for maximum launch grip
        double powerAdjF = 0, powerAdjR = 0;
        if (track.Discipline != Discipline.Drag)
        {
            double hpOver = Math.Max(0, car.PowerHP  - PowerBaselineHP);
            double tqOver = Math.Max(0, car.TorqueNm - TorqueBaselineNm);
            if (car.DriveType == Models.DriveType.RWD)
            {
                powerAdjR = -(hpOver / 300.0 * 0.06) - (tqOver / 600.0 * 0.03);
                powerAdjF =   hpOver / 300.0 * 0.03;
            }
            else if (car.DriveType == Models.DriveType.FWD)
            {
                powerAdjF = -(hpOver / 300.0 * 0.06) - (tqOver / 600.0 * 0.03);
                powerAdjR =   hpOver / 300.0 * 0.03;
            }
            else
            {
                powerAdjF = hpOver / 300.0 * 0.03 - tqOver / 600.0 * 0.015;
                powerAdjR = hpOver / 300.0 * 0.03 - tqOver / 600.0 * 0.015;
            }
        }

        double tpF = baseBar + massAdj + wdAdjF + profileAdj + rimAdj + powerAdjF;
        double tpR = baseBar + massAdj + wdAdjR + profileAdj + rimAdj + powerAdjR;

        double discF = 0, discR = 0;
        string reason;
        switch (track.Discipline)
        {
            case Discipline.Drag:
                // Target: ~32 PSI front / ~18-20 PSI rear. Aggressive rear drop for launch grip (ForzaFire).
                discF = 0.00; discR = -1.00;
                reason = "Drag: нейтральное спереди (~32 PSI), мин. сзади (~18-20 PSI) для макс. зацепа на старте.";
                break;
            case Discipline.Drift:
                // Community: 20–26 PSI for drift. Lower both ends, rear more so.
                discF = -0.52; discR = -0.72;
                reason = "Drift: ~25 PSI перед / ~22 PSI зад для предсказуемого скольжения.";
                break;
            case Discipline.Rally or Discipline.CrossCountry:
                discF = -0.20; discR = -0.20;
                reason = "Грунт/внедорожье: пониженное давление для максимального пятна контакта.";
                break;
            case Discipline.Touge:
                discF = -0.05; discR = -0.03;
                reason = "Тоге: чуть мягче базы для сцепления в горных поворотах.";
                break;
            case Discipline.Street:
                discR = 0.05;
                reason = "Стрит: стандартное давление, небольшой запас сзади для манёвров.";
                break;
            default:
                reason = "Давление по умолчанию для асфальтовых дисциплин.";
                break;
        }

        r.TirePressureFront = Math.Round(Clamp(tpF + discF, c.TirePressureFrontMin, c.TirePressureFrontMax), 2);
        r.TirePressureRear  = Math.Round(Clamp(tpR + discR, c.TirePressureRearMin,  c.TirePressureRearMax),  2);
        ex["TirePressure"] = $"Давление: П {r.TirePressureFront:F2} / З {r.TirePressureRear:F2} бар. {reason}";
    }

    // ── Camber ───────────────────────────────────────────────────────────────
    // Community ranges (forzafire):
    //   Road:  Front -1.0° to -2.0°, Rear -0.5° to -1.0°
    //   Drift: Front -3.0° to -5.0°, Rear -1.0°
    //   Dirt:  Front -0.8° to -1.2°, Rear -0.5° to -0.8°
    //   CC:    Front -0.5°,         Rear -0.5°

    private static void CalculateCamber(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r, Dictionary<string, string> ex)
    {
        double camF, camR;
        string reason;
        switch (track.Discipline)
        {
            case Discipline.Drag:
                camF = -0.2; camR = 0.0;
                reason = "Drag: минимальный развал — максимальное пятно контакта на старте.";
                break;
            case Discipline.Drift:
                camF = -5.0; camR = -1.0;
                reason = "Drift: агрессивный передний развал для контроля при больших углах поворота.";
                break;
            case Discipline.Rally:
                camF = -1.0; camR = -0.6;
                reason = "Ралли: умеренный развал — плоское пятно контакта на грунте.";
                break;
            case Discipline.CrossCountry:
                camF = -0.5; camR = -0.5;
                reason = "CC: минимальный развал — максимальный контакт на неровностях.";
                break;
            case Discipline.Touge:
                camF = -2.0; camR = -1.0;
                reason = "Тоге: усиленный развал для точного входа в крутые повороты.";
                break;
            default:
                camF = -1.5; camR = -0.8;
                reason = "Дорога: классический отрицательный развал для компенсации крена в повороте.";
                break;
        }

        // Engine position bias
        double epF = car.EnginePosition switch { EnginePosition.Front => -0.2, EnginePosition.RearMid => 0.05, EnginePosition.Rear => 0.1, _ => 0.0 };
        double epR = car.EnginePosition switch { EnginePosition.Front => 0.1, EnginePosition.RearMid => -0.1, EnginePosition.Rear => -0.2, _ => 0.0 };

        // Drivetrain camber bias (ForzaFire): road only
        if (track.Discipline is Discipline.Road or Discipline.Street or Discipline.Touge)
        {
            camF += car.DriveType switch { DriveType.RWD => -0.3, DriveType.FWD => 0.3, _ => 0.0 };
            camR += car.DriveType switch { DriveType.RWD => 0.2, DriveType.FWD => -0.2, _ => 0.0 };
        }

        // AWD: additional rear camber for corner exit grip
        if (car.DriveType == Models.DriveType.AWD && track.Discipline is Discipline.Road or Discipline.Street or Discipline.Touge)
            camR += -0.2;

        // Power + torque → more rear camber for exit grip (not for drag/CC where flat patch is priority)
        double pwrR = 0.0;
        if (track.Discipline is not Discipline.Drag and not Discipline.CrossCountry)
        {
            pwrR -= Math.Max(0, (car.PowerHP  - PowerBaselineHP)  / PowerStepHP * 0.15);
            pwrR -= Math.Max(0, (car.TorqueNm - TorqueBaselineNm) / 500.0       * 0.08);
        }

        camF = Clamp(camF + epF, -5.0, 0.0);
        camR = Clamp(camR + epR + pwrR, -5.0, 0.0);

        r.CamberFront = Math.Round(Clamp(camF, c.CamberFrontMin, c.CamberFrontMax), 1);
        r.CamberRear  = Math.Round(Clamp(camR, c.CamberRearMin,  c.CamberRearMax),  1);
        ex["Camber"] = $"Развал: П {r.CamberFront}° / З {r.CamberRear}°. {reason}";
    }

    // ── Toe ──────────────────────────────────────────────────────────────────
    // Community: default 0.0°, slight front toe-out (-0.1° to -0.2°) for turn-in,
    // rear toe-in (+0.1° to +0.3°) for stability (forzafire, forza.guide).

    private static void CalculateToe(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r, Dictionary<string, string> ex)
    {
        double toeF, toeR;
        double wbNorm = Math.Min(car.Wheelbase / RefWheelbaseMm, 1.2);

        switch (track.Discipline)
        {
            case Discipline.Drag:
                toeF = 0.0; toeR = 0.0; break;
            case Discipline.Drift:
                toeF = -0.5; toeR = 0.5; break;
            case Discipline.Rally:
                toeF = -0.2; toeR = 0.1; break;  // грунт/гравий: больше расхождения для реакции
            case Discipline.CrossCountry:
                toeF = -0.1; toeR = 0.3; break;  // бездорожье: меньше расхождения, больше схождения для устойчивости
            case Discipline.Touge:
                toeF = car.DriveType == Models.DriveType.RWD ? -0.2 : -0.1;
                toeR = car.DriveType == Models.DriveType.FWD ? 0.2 : 0.1;
                break;
            default:
                toeF = car.DriveType == Models.DriveType.RWD ? -0.15 : -0.1;
                toeR = car.DriveType == Models.DriveType.FWD ? 0.2 : 0.1;
                break;
        }

        toeF *= wbNorm;
        toeR *= wbNorm;

        // More power/torque on RWD → more rear toe-in for exit stability (skip drag — 0° is mandatory)
        if (car.DriveType == Models.DriveType.RWD && track.Discipline != Discipline.Drag)
        {
            toeR += Math.Max(0, (car.PowerHP  - PowerBaselineHP)  / PowerStepHP * 0.05);
            toeR += Math.Max(0, (car.TorqueNm - TorqueBaselineNm) / 500.0       * 0.03);
        }

        r.ToeFront = Math.Round(Clamp(toeF, c.ToeFrontMin, c.ToeFrontMax), 1);
        r.ToeRear  = Math.Round(Clamp(toeR, c.ToeRearMin,  c.ToeRearMax),  1);

        string fd = r.ToeFront < 0 ? "расхождение" : r.ToeFront > 0 ? "схождение" : "0";
        string rd = r.ToeRear  > 0 ? "схождение"   : r.ToeRear  < 0 ? "расхождение" : "0";
        ex["Toe"] = $"Схождение: П {r.ToeFront}° ({fd}), З {r.ToeRear}° ({rd}).";
    }

    // ── Caster ───────────────────────────────────────────────────────────────
    // Community (forzafire): 5.0°–7.0° range.
    //   Light/agile: 5.0°–5.5°
    //   Mid-weight road: 5.5°–6.5°
    //   Heavy/high-speed: 6.5°–7.0°

    private static void CalculateCaster(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r, Dictionary<string, string> ex, double effectiveMaxKmh)
    {
        // Linear interpolation: 800 kg → 5.0°, 2000 kg → 7.0°, clamped to [5.0, 7.5].
        // Matches community anchors (light car ~5.5°, typical ~6.0°, heavy ~6.5°) without step jumps.
        double baseByWeight = Math.Clamp(5.0 + (car.TotalMass - 800.0) / 600.0, 5.0, 7.5);

        double speedAdj = Math.Max(0, (effectiveMaxKmh - RefSpeedKmh) / 100.0 * 0.5);
        if (track.Discipline == Discipline.Drag)
            speedAdj = Math.Min(speedAdj, 0.3); // drag: light steering preferred, cap speed bonus

        double discAdj = track.Discipline switch
        {
            Discipline.Drag          => -0.5,
            Discipline.Drift         => -1.0, // light steering for drift initiation (community: 5–6°)
            Discipline.Rally         => -0.5,
            Discipline.CrossCountry  => -0.3, // offroad needs self-centering; softer than road, not as light as drift
            _                        => 0.0
        };

        double caster = Clamp(baseByWeight + speedAdj + discAdj, c.CasterMin, c.CasterMax);
        r.Caster = Math.Round(caster, 1);
        ex["Caster"] = $"{r.Caster}° — " +
            $"{(r.Caster >= 6.5 ? "увеличенный — самовозврат и стабильность на скорости" : "стандартный")}. " +
            $"Масса {car.TotalMass} кг, макс. {effectiveMaxKmh} км/ч.";
    }

    // ── ARB ──────────────────────────────────────────────────────────────────
    // Reference-anchored to community tunes (ForzaFire FH6 guide):
    //   RWD: Front 18–25, Rear 25–35. AWD: Front 22–30, Rear 28–38.
    //   FWD: Front 8–15 (soft for grip), Rear 25–40 (stiff for rotation).
    //   Dirt RWD → 14/12, Drift RWD → 20/55.
    // Formula: ARB = ref_axis × (mass/ref_mass)^0.6 × wdAdj + pwrAdj
    // Weight exponent 0.6 ≈ ARB scales with roll moment ~ mass × latG × CG_height

    private static void CalculateARB(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r, Dictionary<string, string> ex)
    {
        double massScale = Math.Pow(car.TotalMass / RefMassKg, 0.6);

        double wd = EffectiveWtDist(car);
        double wdDev = (wd - 50) / 50.0; // -1..+1

        // Base discipline values at refMass (from community reference tunes)
        (double baseF, double baseR, string note) = (track.Discipline, car.DriveType) switch
        {
            (Discipline.Drag, _)             => (2.0, 18.0, "Drag: мин. перед для переноса веса; жёсткий зад для платформы."),
            (Discipline.Drift, Models.DriveType.RWD) => (5.0, 22.0, "Drift: мягкий перед для завязки; умеренный зад для удержания угла (ForzaFire R20-25)."),
            (Discipline.Drift, _)            => (20.0, 40.0, "Drift AWD: умеренные стабилизаторы."),
            (Discipline.Rally, _)            => (14.0, 12.0, "Ралли: мягкие — независимая работа колёс на грунте."),
            (Discipline.CrossCountry, _)     => (10.0, 10.0, "CC: мин. жёсткость для артикуляции подвески."),
            (Discipline.Touge, Models.DriveType.RWD) => (30.0, 26.0, "Тоге RWD: жёстче для точного управления."),
            (Discipline.Touge, Models.DriveType.FWD) => (10.0, 32.0, "Тоге FWD: мягкий перед для зацепа, жёсткий зад для ротации."),
            (Discipline.Touge, _)            => (34.0, 28.0, "Тоге AWD: сбалансированные."),
            (Discipline.Street, Models.DriveType.RWD) => (28.0, 24.0, "Стрит RWD: средняя жёсткость."),
            (Discipline.Street, Models.DriveType.FWD) => (10.0, 30.0, "Стрит FWD: мягкий перед для зацепа, жёстче зад против сноса."),
            (_, Models.DriveType.RWD)        => (28.0, 20.0, "Road RWD: перед жёстче зада для точного входа (ForzaFire F18-28/R12-20)."),
            // FWD: soft front for grip, stiff rear for rotation — prevents understeer (forzafire.com)
            (_, Models.DriveType.FWD)        => (12.0, 28.0, "Road FWD: мягкий перед (зацеп), жёстче зад (ротация)."),
            (_, _)                           => (26.0, 33.0, "Road AWD: F26/R33 — сбалансированные стабилизаторы (ForzaFire F22-30/R28-38).")
        };

        // Track width: wider track → less roll tendency → softer ARB needed (average front+rear)
        double avgTrack   = (car.FrontTrack > 0 && car.RearTrack > 0)
            ? (car.FrontTrack + car.RearTrack) / 2.0
            : Math.Max(car.FrontTrack, car.RearTrack);
        double trackFactor = avgTrack > 0 ? RefFrontTrackMm / avgTrack : 1.0;
        double arbF = baseF * massScale * trackFactor;
        double arbR = baseR * massScale * trackFactor;

        // Weight distribution: shift after trackFactor so offset isn't scaled
        arbF += wdDev * 4.0;
        arbR -= wdDev * 4.0;

        // Power + torque → stiffer ARB on driven axle(s) to control squat/torque steer.
        // RWD: rear only; FWD: front only; AWD: both. Drag skipped — low ARB keeps weight transfer free.
        double pwrAdj = Math.Max(0, (car.PowerHP  - PowerBaselineHP)  / PowerStepHP * 3.0);
        double tqAdj  = Math.Max(0, (car.TorqueNm - TorqueBaselineNm) / 300.0       * 1.5);
        double arbAdj = track.Discipline == Discipline.Drag ? 0.0 : pwrAdj + tqAdj;
        if (car.DriveType == Models.DriveType.RWD)       arbR += arbAdj;
        else if (car.DriveType == Models.DriveType.FWD)  arbF += arbAdj;
        else { arbF += arbAdj; arbR += arbAdj; }

        r.ARBFront = Math.Round(Clamp(arbF, c.ARBFrontMin, c.ARBFrontMax));
        r.ARBRear  = Math.Round(Clamp(arbR, c.ARBRearMin,  c.ARBRearMax));
        ex["ARB"] = $"Стаб.: П {r.ARBFront} / З {r.ARBRear} (диап. 1–65). " +
            $"{car.DriveType}, колея {car.FrontTrack} мм, {car.PowerHP} л.с. {note}";
    }

    // ── Springs ──────────────────────────────────────────────────────────────

    private static void CalculateSprings(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r, Dictionary<string, string> ex)
    {
        double wdF = EffectiveWtDist(car) / 100.0;
        double wdR = 1.0 - wdF;

        (double hzF, double hzR) = track.Discipline switch
        {
            Discipline.Drag         => (2.0, 4.5),
            Discipline.Drift        => (1.8, 2.2),
            Discipline.Rally        => (1.5, 1.7),
            Discipline.CrossCountry => (1.3, 1.5),
            Discipline.Touge        => (2.3, 2.2),
            Discipline.Street       => (2.1, 2.0),
            _                       => (2.1, 2.2) // road — rear stiffer for RWD by default
        };

        // Drivetrain bias: RWD → rear stiffer, FWD → front stiffer
        if (car.DriveType == Models.DriveType.RWD) { hzR += 0.15; hzF -= 0.05; }
        if (car.DriveType == Models.DriveType.FWD) { hzF += 0.15; hzR -= 0.05; }

        // Power/torque under acceleration: driven axle squats → stiffer on that axle.
        // FWD: front dives (front-wheel drive pulls forward), RWD/AWD: rear squats.
        double pwrHz    = Math.Max(0, (car.PowerHP - PowerBaselineHP) / 300.0 * 0.25);
        double torqueHz = Math.Min(0.4, Math.Max(0, (car.TorqueNm - TorqueBaselineNm) / 600.0 * 0.25));
        double squat    = pwrHz + torqueHz;
        if (car.DriveType == Models.DriveType.FWD)
            hzF += squat;
        else
            hzR += squat;

        // K (Н/мм) per spring = 4π²/2000 × f² × m_corner; exact constant = 0.019739 (= 4π²/2000)
        double sprF = 0.019739 * hzF * hzF * car.TotalMass * wdF;
        double sprR = 0.019739 * hzR * hzR * car.TotalMass * wdR;

        // Suspension upgrade multiplier.
        // Rally/CC disciplines already use soft Hz targets — Rally upgrade keeps them neutral (0.85)
        // rather than halving again (0.55). On road disciplines, 0.55 correctly softens road springs.
        bool offRoadDisc = track.Discipline is Discipline.Rally or Discipline.CrossCountry;
        double suspMul = car.SuspensionUpgrade switch
        {
            SuspensionUpgrade.Race    => 1.10,
            SuspensionUpgrade.Sport   => 1.00,
            SuspensionUpgrade.Street  => 0.88,
            SuspensionUpgrade.Rally   => offRoadDisc ? 0.85 : 0.55,
            SuspensionUpgrade.Drift   => 0.85,
            SuspensionUpgrade.Offroad => offRoadDisc ? 0.80 : 0.50,
            _                         => 0.72
        };
        sprF *= suspMul;
        sprR *= suspMul;

        // Aspiration: more sudden power delivery → stiffer rear spring to control squat
        if (car.PowertrainType == PowertrainType.Hybrid)
        { sprF *= 1.05; sprR *= 1.05; }
        sprR *= GetPowerDeliveryFactors(car.PowertrainType, car.AspirationType, car.AntiLag).Spring;

        r.SpringFront = Clamp(Math.Round(sprF), c.SpringFrontMin, c.SpringFrontMax);
        r.SpringRear  = Clamp(Math.Round(sprR), c.SpringRearMin,  c.SpringRearMax);
        ex["Springs"] = $"Пружины: П {r.SpringFront} / З {r.SpringRear} Н/мм " +
            $"({hzF:F2}/{hzR:F2} Гц, подвеска {car.SuspensionUpgrade}).";
    }

    // ── Ride Height ──────────────────────────────────────────────────────────
    // Community (forzafire): road = minimum, rally = 70-80%, CC = max, drag = min front / sl higher rear

    private static void CalculateRideHeight(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r, Dictionary<string, string> ex)
    {
        double rhF, rhR;
        string note;

        double suspOff = car.SuspensionUpgrade switch
        {
            SuspensionUpgrade.Race    => -5,
            SuspensionUpgrade.Sport   => 0,
            SuspensionUpgrade.Street  => 5,
            SuspensionUpgrade.Rally   => 15,
            SuspensionUpgrade.Drift   => -5,
            SuspensionUpgrade.Offroad => 25,
            _                         => 0
        };

        switch (track.Discipline)
        {
            case Discipline.Drag:
                rhF = 100; rhR = 110;
                note = "Drag: макс. клиренс — перенос веса на заднюю ось для старта (ForzaFire).";
                break;
            case Discipline.Drift:
                rhF = 80; rhR = 88;
                note = "Drift: низкий клиренс для устойчивости в скольжении.";
                break;
            case Discipline.Rally:
                rhF = 130; rhR = 140;
                note = "Ралли: высокий клиренс для проезда неровностей.";
                break;
            case Discipline.CrossCountry:
                rhF = 170; rhR = 180;
                note = "CC: макс. клиренс для прыжков и ухабов.";
                break;
            default:
                rhF = 68; rhR = 74;
                note = "Дорога: минимальный клиренс для низкого ЦТ.";
                break;
        }

        rhF += suspOff;
        rhR += suspOff;

        // Rake by engine position
        double rake = car.EnginePosition switch { EnginePosition.Front => 3, EnginePosition.Rear => -2, _ => 0 };
        rhF -= rake * 0.3;
        rhR += rake * 0.3;

        // Rim diameter adjustment
        double avgRim = (car.FrontRimDiameter + car.RearRimDiameter) / 2.0;
        rhF += (avgRim - RefRimDiameterInch) * 1.5;
        rhR += (avgRim - RefRimDiameterInch) * 1.5;

        // Tire profile: taller sidewall raises the car → less ride height needed to achieve same clearance.
        // Profile 25 (racing) → +10 mm; profile 45 (baseline) → 0; profile 65 (SUV) → -10 mm.
        double avgProfile = (car.FrontTireProfile + car.RearTireProfile) / 2.0;
        double profileRhAdj = (ProfileBaseline - avgProfile) * 0.5;
        rhF += profileRhAdj;
        rhR += profileRhAdj;

        r.RideHeightFront = Math.Round(Clamp(rhF, c.RideHeightFrontMin, c.RideHeightFrontMax));
        r.RideHeightRear  = Math.Round(Clamp(rhR, c.RideHeightRearMin,  c.RideHeightRearMax));
        ex["RideHeight"] = $"Клиренс: П {r.RideHeightFront} / З {r.RideHeightRear} мм. {note}";
    }

    // ── Dampers ──────────────────────────────────────────────────────────────
    // Reference-anchored to community tunes (GamerStation GR Supra, forzafire):
    //   Road ~1540kg → reb 14/14, bump 8/8  (bump ~57% of rebound)
    //   Dirt ~1540kg → reb 9/10, bump 6/6   (bump ~63% of rebound)
    // Formula: reb = baseReb_ref × (mass/refMass)^0.5  (sqrt = damping ∝ √spring ∝ √weight)
    //   bump = reb × bumpRatio
    //   bumpRatio target: road 57%, dirt 63%, rally 65%, CC 67%, drift 50%, drag 45%

    private static void CalculateDampers(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r, Dictionary<string, string> ex)
    {
        double massScale = Math.Sqrt(car.TotalMass / RefMassKg);

        double wdF = EffectiveWtDist(car) / 100.0;
        double wdDev = wdF - 0.5;

        // Base rebound for reference car, per discipline. Unified road base=15 — mass scaling handles weight.
        double baseReb = track.Discipline switch
        {
            Discipline.Drag          => 10.0,
            Discipline.Drift         => 4.0,
            Discipline.Rally         => 9.0,
            Discipline.CrossCountry  => 8.0,
            Discipline.Touge         => 13.0,
            Discipline.Street        => 12.0,
            _                        => 15.0  // road + eliminator: unified base, √mass scales AWD/RWD/FWD
        };

        // Bump ratio: bump / rebound
        double bumpRatio = (track.Discipline, car.DriveType) switch
        {
            (Discipline.Drag, _)             => 0.45,
            (Discipline.Drift, _)            => 0.50,
            (Discipline.Rally, _)            => 0.65,
            (Discipline.CrossCountry, _)     => 0.67,
            (_, _)                           => 0.57
        };

        // Weight distribution: heavier end gets stiffer damping
        double wdAdj = wdDev * 4.0;
        double rebF = baseReb * massScale + wdAdj;
        double rebR = baseReb * massScale - wdAdj;

        // RWD: rear rebound slightly higher for squat control
        if (car.DriveType == Models.DriveType.RWD) rebR += 0.5;

        // Power/torque under acceleration: driven axle squats → stiffer rebound on that axle.
        // FWD: front dives under power; RWD/AWD: rear squats.
        double powerReb  = Math.Max(0, (car.PowerHP  - PowerBaselineHP)  / PowerStepHP * 0.5);
        double torqueReb = Math.Max(0, (car.TorqueNm - TorqueBaselineNm) / 500.0       * 0.5);
        double squatReb  = powerReb + torqueReb;
        if (car.DriveType == Models.DriveType.FWD) rebF += squatReb;
        else                                        rebR += squatReb;

        double bmpF = rebF * bumpRatio;
        double bmpR = rebR * bumpRatio;

        // Suspension upgrade range modifier
        double suspMul = car.SuspensionUpgrade switch
        {
            SuspensionUpgrade.Race    => 1.10,
            SuspensionUpgrade.Sport   => 1.00,
            SuspensionUpgrade.Rally   => 1.05,
            SuspensionUpgrade.Drift   => 0.95,
            SuspensionUpgrade.Street  => 0.90,
            SuspensionUpgrade.Offroad => 0.85,
            _                         => 0.85
        };
        rebF *= suspMul; rebR *= suspMul;
        bmpF *= suspMul; bmpR *= suspMul;

        // Aspiration: sudden power onset → stiffer rear damping to resist squat/extension
        double aspDamper = GetPowerDeliveryFactors(car.PowertrainType, car.AspirationType, car.AntiLag).Damper;
        rebR *= aspDamper;
        bmpR *= aspDamper;

        r.ReboundFront = Math.Round(Clamp(rebF, c.ReboundFrontMin, c.ReboundFrontMax));
        r.ReboundRear  = Math.Round(Clamp(rebR, c.ReboundRearMin,  c.ReboundRearMax));
        r.BumpFront    = Math.Round(Clamp(bmpF, c.BumpFrontMin,    c.BumpFrontMax));
        r.BumpRear     = Math.Round(Clamp(bmpR, c.BumpRearMin,     c.BumpRearMax));
        ex["Dampers"] = $"Отбой: П {r.ReboundFront} / З {r.ReboundRear}, " +
            $"сжатие: П {r.BumpFront} / З {r.BumpRear}. " +
            $"(сжатие/отбой: П {(rebF > 0 ? bmpF / rebF * 100 : 0):F0}%, З {(rebR > 0 ? bmpR / rebR * 100 : 0):F0}%)";
    }

    // ── Aero ─────────────────────────────────────────────────────────────────
    // Community (forza.guide): max front, rear for stability.
    // RWD → rear emphasis, FWD/AWD → front emphasis.

    private static void CalculateAero(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r, Dictionary<string, string> ex)
    {
        if (!car.HasFrontAero && !car.HasRearAero)
        {
            r.AeroFront = 0; r.AeroRear = 0;
            ex["Aero"] = "Аэродинамические элементы не установлены.";
            return;
        }

        double speedFactor = car.MaxSpeedKmh / 280.0; // scales up beyond 280 km/h; clamped by AeroMax constraints
        double pwrFactor = Math.Min(1.5, 1.0 + Math.Max(0, (car.PowerHP - PowerBaselineHP) / PowerStepHP * 0.15));

        var (fwFactor, rwFactor) = car.DriveType switch
        {
            Models.DriveType.RWD => (0.55, 0.70),  // rear-biased for RWD stability
            Models.DriveType.FWD => (0.65, 0.55),  // slight front emphasis for FWD
            Models.DriveType.AWD => (0.70, 0.45),  // front emphasis to mitigate AWD understeer, balanced
            _                    => (0.55, 0.60)
        };
        double aeroF = car.HasFrontAero ? c.AeroFrontMax * fwFactor * speedFactor * pwrFactor : 0;
        double aeroR = car.HasRearAero  ? c.AeroRearMax  * rwFactor * speedFactor * pwrFactor : 0;

        // Discipline
        switch (track.Discipline)
        {
            case Discipline.Drag:
                aeroF = 0; aeroR = car.HasRearAero ? 15 : 0;
                break;
            case Discipline.Drift:
                aeroF *= 0.8; aeroR *= 0.3;
                break;
            case Discipline.CrossCountry:
                aeroF = car.HasFrontAero ? Clamp(c.AeroFrontMax * 0.40 * speedFactor * pwrFactor, c.AeroFrontMin, c.AeroFrontMax) : 0;
                aeroR = car.HasRearAero  ? Clamp(c.AeroRearMax  * 0.55 * speedFactor * pwrFactor, c.AeroRearMin,  c.AeroRearMax)  : 0;
                break;
            case Discipline.Rally:
                aeroF = car.HasFrontAero ? Clamp(c.AeroFrontMax * 0.60 * speedFactor * pwrFactor, c.AeroFrontMin, c.AeroFrontMax) : 0;
                aeroR = car.HasRearAero  ? Clamp(c.AeroRearMax  * 0.75 * speedFactor * pwrFactor, c.AeroRearMin,  c.AeroRearMax)  : 0;
                break;
        }

        r.AeroFront = Math.Round(Clamp(aeroF, c.AeroFrontMin, c.AeroFrontMax));
        r.AeroRear  = Math.Round(Clamp(aeroR, c.AeroRearMin,  c.AeroRearMax));
        ex["Aero"] = $"Прижим: П {r.AeroFront} / З {r.AeroRear} кг. " +
            $"{car.MaxSpeedKmh} км/ч, {car.PowerHP} л.с.";
    }

    // ── Differential ────────────────────────────────────────────────────────
    // Community (forzafire.com FH6 drivetrain guide):
    //   RWD: Accel 40-65%, Decel 15-30% (road)
    //   FWD: Accel 80-95%, Decel 0-10%
    //   AWD road: Front Accel 28%, Decel 0%; Rear Accel 100%, Decel 45%; Center 70-85% rear
    //   AWD rally: Front 35-40%, Rear 55-70%, Center 65-75% rear

    private static void CalculateDifferential(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r, Dictionary<string, string> ex)
    {
        double cap = car.DifferentialUpgrade switch
        {
            DifferentialUpgrade.Stock    => 0.40,
            DifferentialUpgrade.Street   => 0.60,
            DifferentialUpgrade.Sport    => 0.80,
            DifferentialUpgrade.Rally    => 0.90,
            DifferentialUpgrade.Race     => 1.00,
            DifferentialUpgrade.DriftSpec => 1.00,
            _                            => 0.80
        };

        double accel, decel;
        switch (track.Discipline)
        {
            case Discipline.Drag:
                (accel, decel) = car.DriveType switch
                {
                    Models.DriveType.RWD => (70.0, 20.0),
                    Models.DriveType.AWD => (85.0, 0.0),
                    _                    => (80.0, 10.0)
                };
                break;
            case Discipline.Drift:
                (accel, decel) = car.DriveType switch
                {
                    Models.DriveType.RWD => (95.0, 0.0),
                    Models.DriveType.AWD => (80.0, 10.0),
                    Models.DriveType.FWD => (30.0, 5.0),  // FWD drift: slight accel lock for rotation
                    _                    => (0.0, 0.0)
                };
                break;
            case Discipline.Rally:
                (accel, decel) = car.DriveType switch
                {
                    Models.DriveType.AWD => (65.0, 20.0),
                    _                    => (55.0, 20.0)
                };
                break;
            case Discipline.CrossCountry:
                (accel, decel) = car.DriveType switch
                {
                    Models.DriveType.AWD => (60.0, 25.0),
                    _                    => (50.0, 25.0)
                };
                break;
            default:
                // Road / Touge / Street / Eliminator
                // AWD rear diff: high lock for traction; front diff set separately below (~28%)
                (accel, decel) = car.DriveType switch
                {
                    Models.DriveType.RWD => (55.0, 20.0),
                    Models.DriveType.FWD => (85.0, 5.0),
                    _                    => (90.0, 40.0)  // AWD rear diff (community: ~100%/45%)
                };
                break;
        }

        // Power: more power → more accel lock
        accel += Math.Max(0, (car.PowerHP - PowerBaselineHP) / PowerStepHP * 5.0);
        // Torque: more torque → higher wheel slip tendency → more accel lock (direct traction factor)
        accel += Math.Max(0, (car.TorqueNm - TorqueBaselineNm) / 300.0 * 3.0);
        // Weight: heavier → more accel lock
        accel += (car.TotalMass - MassBaselineKg) / 100.0 * 1.5;
        // Engine position: rear/mid → more accel lock
        accel += car.EnginePosition switch { EnginePosition.Rear => 8.0, EnginePosition.Mid => 4.0, _ => 0.0 };
        // Wheelbase: longer → less accel lock (stable by design)
        accel -= (car.Wheelbase - RefWheelbaseMm) / 500.0 * 3.0;
        // Aspiration: sudden/peak power delivery → more accel lock to prevent wheelspin
        accel *= GetPowerDeliveryFactors(car.PowertrainType, car.AspirationType, car.AntiLag).Diff;

        accel *= cap;
        decel *= cap;

        r.DiffAccel = Math.Round(Clamp(accel, c.DiffAccelMin, c.DiffAccelMax));
        r.DiffDecel = Math.Round(Clamp(decel, c.DiffDecelMin, c.DiffDecelMax));

        string aspLabel = car.AspirationType switch
        {
            AspirationType.SingleTurbo          => car.AntiLag ? "антилаг" : "одиноч. турбо",
            AspirationType.TwinTurbo            => "двойн. турбо",
            AspirationType.PositiveDisplacement => "объём. компр.",
            AspirationType.Centrifugal          => "центроб. компр.",
            AspirationType.Electric             => "электро",
            _                                   => "атмосф."
        };
        string engPosLabel = car.EnginePosition switch
        {
            EnginePosition.Front   => "переднее",
            EnginePosition.Mid     => "среднее",
            EnginePosition.RearMid => "заднее-среднее",
            EnginePosition.Rear    => "заднее",
            _                      => car.EnginePosition.ToString()
        };
        string diag = $"{car.PowerHP} л.с. ({aspLabel}), двиг. {engPosLabel}, база {car.Wheelbase} мм.";

        if (car.DriveType == Models.DriveType.AWD)
        {
            double bias = c.CenterDiffBias / 100.0; // user-set (50 default)
            // Community: road AWD target 70-85% rear bias
            double targetBias = track.Discipline switch
            {
                Discipline.Drift        => 0.50, // AWD drift: balanced 50/50 — allows equal-slip rotation
                Discipline.Drag         => 0.60,
                Discipline.Rally        => 0.70,
                Discipline.CrossCountry => 0.60,
                _                       => 0.78
            };
            // Blend user preference toward community target (70% target weight to meet ≥70% minimum)
            bias = bias * 0.3 + targetBias * 0.7;
            // Wheelbase: longer → more rear bias
            bias += (car.Wheelbase - RefWheelbaseMm) / 500.0 * 0.03;
            bias = Clamp(bias, 0.0, 1.0);

            // AWD front diff: road = 28% (open for rotation), off-road higher for traction
            // Source: forzafire.com FH6 drivetrain guide — AWD road front accel ~28%
            double fAccel = track.Discipline switch
            {
                Discipline.Drag         => 50,
                Discipline.Drift        => 30,
                Discipline.Rally        => 35,
                Discipline.CrossCountry => 40,
                _                       => 28  // road/street/touge
            };
            double fDecel = track.Discipline switch
            {
                Discipline.Drift        => 5,
                Discipline.Rally        => 20,
                Discipline.CrossCountry => 15,
                _                       => 0   // road: open front decel
            };

            // bias → rear-biased: reduce front lock, increase rear lock
            double frontFactor = 1.2 - bias * 0.4;
            double rearFactor  = 0.8 + bias * 0.4;

            // Apply rearFactor to rear diff (accel already has cap applied)
            r.DiffAccel = Math.Round(Clamp(accel * rearFactor, c.DiffAccelMin, c.DiffAccelMax));
            r.DiffDecel = Math.Round(Clamp(decel * rearFactor, c.DiffDecelMin, c.DiffDecelMax));

            double pwrF  = Math.Max(0, (car.PowerHP  - PowerBaselineHP)  / PowerStepHP * 3.0);
            double tqF   = Math.Max(0, (car.TorqueNm - TorqueBaselineNm) / 300.0       * 1.5);
            double adjF  = pwrF + tqF;

            r.DiffFrontAccel = Math.Round(Clamp((fAccel + adjF) * cap * frontFactor, c.DiffAccelMin, c.DiffAccelMax));
            r.DiffFrontDecel = Math.Round(Clamp(fDecel * cap * frontFactor, c.DiffDecelMin, c.DiffDecelMax));
            r.CenterDiffBias = Math.Round(bias * 100);
        }

        ex["Differential"] = $"Осн. дифф.: разгон {r.DiffAccel}%, торм. {r.DiffDecel}%." +
            (r.DiffFrontAccel.HasValue
                ? $" Передний: разгон {r.DiffFrontAccel}%, торм. {r.DiffFrontDecel}%, центр {r.CenterDiffBias}% (зад)."
                : "") +
            $" {diag}";
    }

    // ── Brakes ──────────────────────────────────────────────────────────────
    // Community (forzafire):
    //   RWD: 50-55% front bias
    //   AWD: 52-56% front bias
    //   FWD: 55-62% front bias
    //   Drift/Dirt: 45-50%

    private static void CalculateBrakes(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r, Dictionary<string, string> ex, double effectiveMaxKmh)
    {
        double bias = EffectiveWtDist(car);

        double discAdj = track.Discipline switch
        {
            Discipline.Drift        => -5.0,
            Discipline.Drag         => -4.0,
            Discipline.Rally        => -2.0,
            Discipline.CrossCountry => -3.0,
            _                       => 0.0
        };
        bias += discAdj;

        // FWD: more front bias; scales up with power since driven front wheels carry more load
        if (car.DriveType == Models.DriveType.FWD)
            bias += 4.0 + Math.Max(0, (car.PowerHP - PowerBaselineHP) / PowerStepHP * 1.0);

        bias = Clamp(bias, 45, 62);

        double pressure = track.Discipline switch
        {
            Discipline.Drift  => 85,
            Discipline.Rally  => 90,
            Discipline.CrossCountry => 85,
            _                 => 100
        };
        // Mass: lighter cars need less pressure, heavier need more (ForzaFire: 85-115%)
        pressure += (car.TotalMass - MassBaselineKg) / 200.0 * 2.5;
        pressure += Math.Max(0, (car.PowerHP  - PowerBaselineHP)  / PowerStepHP * 5.0);
        pressure += Math.Max(0, (car.TorqueNm - TorqueBaselineNm) / 500.0       * 3.0);
        pressure += Math.Max(0, (effectiveMaxKmh - RefSpeedKmh) / 100.0 * 5.0);
        if (car.DriveType == Models.DriveType.AWD) pressure += 5;
        // Brake upgrade: better calipers/pads → higher effective bite
        pressure += car.BrakesUpgrade switch
        {
            BrakesUpgrade.Race  => 5.0,
            BrakesUpgrade.Sport => 2.5,
            _                   => 0.0
        };

        r.BrakeBalance  = Math.Round(Clamp(bias,  c.BrakeBalanceMin,  c.BrakeBalanceMax));
        r.BrakePressure = Math.Round(Clamp(pressure, c.BrakePressureMin, c.BrakePressureMax));

        string reason = track.Discipline switch
        {
            Discipline.Drift   => "Сдвиг назад — занос при торможении.",
            Discipline.Drag    => "Сдвиг назад — минимум вмешательства на старте.",
            _                  => "Соответствует развесовке."
        };
        ex["Brakes"] = $"Тормоза: баланс {r.BrakeBalance}% (П), давление {r.BrakePressure}%. {reason}";
    }

    // ── Gearing ──────────────────────────────────────────────────────────────
    // Community (forza.guide): adjust final drive only, leave individual gears.
    // Target: max speed at 95% redline in top gear.

    private static int CalcRecommendedGearCount(CarCard car, TrackInfo track, double effectiveMaxKmh)
    {
        // Electric drivetrains use 1–2 gears regardless of discipline
        if (car.PowertrainType == PowertrainType.Electric)
            return track.Discipline == Discipline.Drag ? 1 : 2;

        // Drag: distance-based defaults (community convention)
        if (track.Discipline == Discipline.Drag)
        {
            return track.DragDistance switch
            {
                DragDistance.Eighth  => 2,
                DragDistance.Quarter => 3,
                DragDistance.Half    => 4,
                _                    => 5,
            };
        }

        // ICE non-Drag: physics-based count from TorquePeakRPM / MaxRPM
        double pwRatio = car.PowerHP / (car.TotalMass / 1000.0);
        (double first, double stepMin, double stepMax, _) = GetDisciplineGearParams(track.Discipline, pwRatio, car.FuelType);
        ApplyAspirationStepAdjustment(car.AspirationType, car.AntiLag, ref stepMin, ref stepMax);
        stepMin = Math.Max(0.50, stepMin);
        stepMax = Math.Clamp(stepMax, stepMin + 0.05, 0.95);

        double stepIdeal = car.TorquePeakRPM > 0 && car.PowerPeakRPM > 0
            ? (double)car.TorquePeakRPM / car.PowerPeakRPM
            : (stepMin + stepMax) / 2.0;
        double step = Math.Clamp(stepIdeal, stepMin, stepMax);

        // Estimate top gear ratio needed to hit target speed at redline (assume FD ≈ 3.5)
        double tireCirc = Math.PI * car.RearWheelDiameterInch * 0.0254;
        double targetKmh = Math.Min(effectiveMaxKmh, 400);
        double targetMs = targetKmh / 3.6;

        double totalRatio = targetMs > 0 && car.MaxRPM > 0 && tireCirc > 0
            ? car.MaxRPM * RevLimitFraction * tireCirc / (60.0 * targetMs)
            : 9.0;
        double topEstimate = Math.Clamp(totalRatio / 3.5, GearRatioMin, first);

        double spread = Math.Max(topEstimate / first, 0.01);
        int rec = (int)Math.Round(1.0 + Math.Log(spread) / Math.Log(step));

        return Math.Clamp(rec, 4, 10);
    }

    private static (double first, double stepMin, double stepMax, string note) GetDisciplineGearParams(
        Discipline discipline, double pwRatio, FuelType fuelType)
    {
        (double first, double stepMin, double stepMax, string note) = discipline switch
        {
            Discipline.Drift        => (3.0, 0.70, 0.88, "Удлинённые передачи для контроля в заносе."),
            Discipline.Rally        => (4.0, 0.68, 0.78, "Короткий ряд для быстрого разгона на грунте."),
            Discipline.CrossCountry => (4.5, 0.66, 0.75, "Макс. ускорение на бездорожье."),
            Discipline.Touge        => (3.8, 0.70, 0.84, "Короткие передачи для горных серпантинов."),
            _                       => (3.5, 0.68, 0.82, "Ряд под дорожные дисциплины.")
        };

        if (pwRatio > 200) first += 0.3;
        else if (pwRatio < 100) first -= 0.3;

        if (fuelType == FuelType.Diesel)
            first = Math.Max(first - 0.45, 1.5);

        return (first, stepMin, stepMax, note);
    }

    private static void ApplyAspirationStepAdjustment(AspirationType aspiration, bool antiLag, ref double stepMin, ref double stepMax)
    {
        switch (aspiration)
        {
            case AspirationType.Centrifugal:            stepMax -= 0.08; break;
            case AspirationType.SingleTurbo when !antiLag: stepMax -= 0.04; break;
            case AspirationType.SingleTurbo:            stepMax -= 0.02; break;
            case AspirationType.TwinTurbo when !antiLag: stepMax -= 0.02; break;
            case AspirationType.TwinTurbo:              stepMax -= 0.01; break;
            case AspirationType.Electric:               stepMin += 0.05; stepMax += 0.05; break;
        }
    }

    private static void CalculateGearing(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r, Dictionary<string, string> ex, double effectiveMaxKmh)
    {
        int n = Math.Max(1, Math.Min(car.GearCount, 10));
        r.RecommendedGearCount = CalcRecommendedGearCount(car, track, effectiveMaxKmh);

        double pwRatio = car.PowerHP / (car.TotalMass / 1000.0);
        double tireCirc = Math.PI * car.RearWheelDiameterInch * 0.0254;

        double targetKmh = track.Discipline == Discipline.Drag
            ? effectiveMaxKmh
            : Math.Min(effectiveMaxKmh, 400);
        double targetMs = targetKmh / 3.6;

        // Single-speed: Gear 1 × FinalDrive = total reduction (same Forza mechanics, one gear).
        // Gear 1 is discipline-based like ICE first gear; FD is back-calculated from the remainder.
        // If FD falls outside constraint range, gear 1 is adjusted to compensate.
        if (n == 1)
        {
            double total = targetMs > 0 && car.MaxRPM > 0
                ? car.MaxRPM * RevLimitFraction * tireCirc / (60.0 * targetMs)
                : 9.0;

            double g1 = track.Discipline switch
            {
                Discipline.Drag         => 4.0,
                Discipline.CrossCountry => 4.5,
                Discipline.Rally        => 4.0,
                Discipline.Touge        => 3.8,
                Discipline.Drift        => 3.0,
                _                       => 3.5
            };
            if (pwRatio > 200) g1 += 0.3;
            else if (pwRatio < 100) g1 -= 0.3;
            g1 = Math.Max(1.0, g1);

            // Resolve both constraint ranges: FinalDrive first, then GearRatio.
            // If both clamp simultaneously the product g1×fd1 may not equal total — accept best fit.
            double fd1 = Math.Clamp(total / g1, c.FinalDriveMin, c.FinalDriveMax);
            g1 = Math.Clamp(total / fd1, GearRatioMin, GearRatioMax);
            fd1 = Math.Clamp(total / g1, c.FinalDriveMin, c.FinalDriveMax);

            g1  = Math.Round(g1,  2);
            fd1 = Math.Round(fd1, 2);
            r.GearRatios = new List<double> { g1 };
            r.FinalDrive = fd1;
            ex["FinalDrive"] = $"ГП {fd1}. Передача 1: {g1} (одна ступень). " +
                $"Суммарное передаточное: {g1} × {fd1} = {g1 * fd1:F2}. " +
                $"{effectiveMaxKmh} км/ч @ {car.MaxRPM} об/мин. Рек. передач: {r.RecommendedGearCount}.";
            return;
        }

        double first, top;
        string note;

        if (track.Discipline == Discipline.Drag)
        {
            (first, top, note) = GetDragRatios(track.DragDistance);
        }
        else
        {
            double stepMin, stepMax;
            (first, stepMin, stepMax, note) = GetDisciplineGearParams(track.Discipline, pwRatio, car.FuelType);

            ApplyAspirationStepAdjustment(car.AspirationType, car.AntiLag, ref stepMin, ref stepMax);
            stepMin = Math.Max(0.50, stepMin);
            stepMax = Math.Clamp(stepMax, stepMin + 0.05, 0.95);

            double stepIdeal = car.PowerPeakRPM > 0
                ? (double)car.TorquePeakRPM / car.PowerPeakRPM
                : (stepMin + stepMax) / 2.0;
            double step = Math.Clamp(stepIdeal, stepMin, stepMax);
            top = first * Math.Pow(step, n - 1);
        }

        // Clamp endpoints to Forza's physical gear ratio range before generating the sequence
        first = Math.Clamp(first, GearRatioMin, GearRatioMax);
        top   = Math.Clamp(top,   GearRatioMin, first);

        // Degressiv factor: base from discipline, then adjusted by engine power-band width.
        // Higher = more degressiv (upper gears closer together).
        double degFactor = track.Discipline switch
        {
            Discipline.Drift        => 1.02,
            Discipline.Rally        => 1.05,
            Discipline.CrossCountry => 1.05,
            _                       => 1.04,
        };

        // Narrow power band → upper gears must be tighter → increase degFactor.
        // TorquePeak/PowerPeak is the direct measure of useful band width: V8≈0.63 (wide), Rotary≈0.76 (narrow).
        double bandRatio = car.PowerPeakRPM > 0 ? (double)car.TorquePeakRPM / car.PowerPeakRPM : 0.70;
        degFactor += (bandRatio - 0.70) * 0.15;

        // Aspiration: centrifugal and single-turbo (no antilag) have narrower usable band.
        // Electric has flat torque — nearly geometric is ideal.
        degFactor += (car.AspirationType, car.AntiLag) switch
        {
            (AspirationType.Centrifugal, _)      =>  0.01,
            (AspirationType.SingleTurbo, false)  =>  0.01,
            (AspirationType.Electric, _)         => -0.02,
            _                                    =>  0.00,
        };
        if (car.FuelType == FuelType.Diesel) degFactor -= 0.01;

        degFactor = Math.Clamp(degFactor, 1.01, 1.07);

        var ratios = new List<double>(n);
        if (n <= 2)
        {
            // 1- and 2-speed: no intermediate gears, geometric and degressiv are identical
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / (n - 1);
                ratios.Add(Math.Round(Math.Clamp(first * Math.Pow(top / first, t), GearRatioMin, GearRatioMax), 2));
            }
        }
        else
        {
            // Degressiv progression: s0^(n-1) * degFactor^((n-1)(n-2)/2) = top/first  →  solve for s0.
            double spread  = top / first;
            double degExp  = (n - 1) * (n - 2) / 2.0;
            double s0      = Math.Pow(spread / Math.Pow(degFactor, degExp), 1.0 / (n - 1));
            double ratio   = first;
            ratios.Add(Math.Round(Math.Clamp(ratio, GearRatioMin, GearRatioMax), 2));
            double stepCur = s0;
            for (int i = 1; i < n; i++)
            {
                ratio *= stepCur;
                ratios.Add(Math.Round(Math.Clamp(ratio, GearRatioMin, GearRatioMax), 2));
                stepCur *= degFactor;
            }
        }

        double fd = targetMs > 0 && car.MaxRPM > 0
            ? car.MaxRPM * RevLimitFraction * tireCirc / (60.0 * targetMs * top)
            : 3.50;
        if (track.Discipline != Discipline.Drag)
            fd *= 1.0 + Math.Max(0, (pwRatio - 150) / 200.0 * 0.05);

        r.GearRatios = ratios;
        r.FinalDrive = Math.Round(Clamp(fd, c.FinalDriveMin, c.FinalDriveMax), 2);

        string gearStr = string.Join("  ", ratios.Select((g, i) => $"{i + 1}: {g:F2}"));
        ex["FinalDrive"] = $"ГП {r.FinalDrive}. Рек. передач: {r.RecommendedGearCount}. Ряд: {gearStr}. " +
            $"{effectiveMaxKmh} км/ч @ {car.MaxRPM} об/мин, P/W {pwRatio:F1} л.с./т. {note}";
    }

    private static (double first, double top, string note) GetDragRatios(DragDistance dist)
    {
        return dist switch
        {
            DragDistance.Eighth  => (4.5, 1.80, "1/8 мили: очень короткий ряд — пик мощности на финише."),
            DragDistance.Quarter => (4.0, 1.30, "1/4 мили: сбалансированный ряд, пик на 3-й передаче."),
            DragDistance.Half    => (4.0, 1.00, "1/2 мили: нужна 4-я передача на финиш."),
            DragDistance.Mile    => (4.0, 0.85, "Миля: длинные передачи для высокой скорости."),
            _                    => (4.0, 1.30, "")
        };
    }
}
