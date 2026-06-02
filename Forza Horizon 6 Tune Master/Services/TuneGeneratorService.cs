using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

public class TuneGeneratorService
{
    private static double Clamp(double v, double min, double max) => Math.Max(min, Math.Min(max, v));

    private const double LbPerKg = 2.20462;

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
            AspirationType.TwinTurbo            => (1.07, 1.04, 1.03),
            AspirationType.PositiveDisplacement => (1.05, 1.03, 1.03),
            AspirationType.Centrifugal          => (1.03, 1.02, 1.01),
            AspirationType.Electric             => (1.20, 1.08, 1.06),
            _                                   => (1.00, 1.00, 1.00),
        };

        if (pt == PowertrainType.Hybrid)
        {
            d  = 1.0 + (d  - 1.0) * 0.60;
            s  = 1.0 + (s  - 1.0) * 0.60;
            dm = 1.0 + (dm - 1.0) * 0.60;
        }

        return (d, s, dm);
    }

    public TuneResult Generate(CarCard car, TrackInfo track, TuningConstraints c)
    {
        var r  = new TuneResult { Car = car, Track = track };
        var ex = r.Explanations;

        CalculateTirePressure(car, track, c, r, ex);
        CalculateCamber(car, track, c, r, ex);
        CalculateToe(car, track, c, r, ex);
        CalculateCaster(car, track, c, r, ex);
        CalculateARB(car, track, c, r, ex);
        CalculateSprings(car, track, c, r, ex);
        CalculateRideHeight(car, track, c, r, ex);
        CalculateDampers(car, track, c, r, ex);
        CalculateAero(car, track, c, r, ex);
        CalculateDifferential(car, track, c, r, ex);
        CalculateBrakes(car, track, c, r, ex);
        CalculateGearing(car, track, c, r, ex);

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

        double baseLaunch = car.AspirationType switch
        {
            AspirationType.TwinTurbo            => Math.Max(2500, torquePeak * 0.55),
            AspirationType.SingleTurbo          => car.AntiLag
                                                   ? Math.Max(3000, torquePeak * 0.65)
                                                   : Math.Max(2800, torquePeak * 0.60),
            AspirationType.PositiveDisplacement => Math.Max(2200, torquePeak * 0.65),
            _                                   => torquePeak * 0.70   // NA
        };

        double driveAdj = car.DriveType switch
        {
            DriveType.AWD => 1.10,
            DriveType.FWD => 0.95,
            DriveType.RWD => 0.85,
            _             => 1.00
        };

        double launch = Math.Clamp(baseLaunch * driveAdj, 1000, car.MaxRPM * 0.75);
        r.LaunchControlRpm = Math.Round(launch / 100.0) * 100;
    }

    private static double EffectiveWtDist(CarCard car)
    {
        if (Math.Abs(car.WeightDistributionFront - 50) > 2)
            return car.WeightDistributionFront;
        return car.EnginePosition switch
        {
            EnginePosition.Front => 55,
            EnginePosition.Mid   => 48,
            EnginePosition.Rear  => 40,
            _                    => 50
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
            TireType.Slick     => 2.24,
            TireType.SemiSlick => 2.21,
            TireType.Sport     => 2.17,
            TireType.Street    => 2.14,
            TireType.Stock     => 2.14,
            TireType.Rally     => 2.03,
            TireType.Offroad   => 2.00,
            TireType.Drag      => 2.21,
            _                  => 2.14
        };

        // Mass: +0.05 bar per 200 kg over 1400
        double massAdj = (car.TotalMass - 1400) / 200.0 * 0.05;

        // Weight distribution: heavier end gets more pressure
        double wd = EffectiveWtDist(car);
        double wdDev = (wd - 50) / 50.0;
        double wdAdjF = wdDev * 0.25;
        double wdAdjR = -wdDev * 0.25;

        // Profile: lower profile → stiffer sidewall → slightly higher pressure
        double profile = (car.FrontTireProfile + car.RearTireProfile) / 2.0;
        double profileAdj = Math.Clamp((45 - profile) * 0.004, -0.15, 0.15);

        // Rim diameter: bigger rim → lower sidewall → slightly higher pressure
        double rim = (car.FrontRimDiameter + car.RearRimDiameter) / 2.0;
        double rimAdj = Math.Clamp((rim - 19) * 0.02, -0.10, 0.15);

        // Power-based adjustments
        // RWD: more power needs lower rear pressure for traction, slightly higher front for stability
        // FWD: more power needs lower front pressure for traction
        // AWD: balanced increase both axles
        // Drag skipped — rear pressure is intentionally minimised for maximum launch grip
        double powerAdjF = 0, powerAdjR = 0;
        if (track.Discipline != Discipline.Drag)
        {
            double hpOver = Math.Max(0, car.PowerHP - 300);
            if (car.DriveType == Models.DriveType.RWD)
            {
                powerAdjR = -(hpOver / 300.0 * 0.06);
                powerAdjF = hpOver / 300.0 * 0.03;
            }
            else if (car.DriveType == Models.DriveType.FWD)
            {
                powerAdjF = -(hpOver / 300.0 * 0.06);
                powerAdjR = hpOver / 300.0 * 0.03;
            }
            else
            {
                powerAdjF = hpOver / 300.0 * 0.03;
                powerAdjR = hpOver / 300.0 * 0.03;
            }
        }

        double tpF = baseBar + massAdj + wdAdjF + profileAdj + rimAdj + powerAdjF;
        double tpR = baseBar + massAdj + wdAdjR + profileAdj + rimAdj + powerAdjR;

        double discF = 0, discR = 0;
        string reason;
        switch (track.Discipline)
        {
            case Discipline.Drag:
                // Target: ~32 PSI front / ~25 PSI rear. Large rear drop for launch grip.
                discF = 0.00; discR = -0.48;
                reason = "Drag: нейтральное спереди (~32 PSI), пониженное сзади (~25 PSI) для максимального зацепа на старте.";
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
        double epF = car.EnginePosition switch { EnginePosition.Front => -0.2, EnginePosition.Rear => 0.1, _ => 0.0 };
        double epR = car.EnginePosition switch { EnginePosition.Front => 0.1, EnginePosition.Rear => -0.2, _ => 0.0 };

        // Drivetrain camber bias (ForzaFire): road only
        if (track.Discipline is Discipline.Road or Discipline.Street or Discipline.Touge)
        {
            camF += car.DriveType switch { DriveType.RWD => -0.3, DriveType.FWD => 0.3, _ => 0.0 };
            camR += car.DriveType switch { DriveType.RWD => 0.2, DriveType.FWD => -0.2, _ => 0.0 };
        }

        // Power: more power → more rear camber for exit grip (not for drag/CC)
        double pwrR = track.Discipline is Discipline.Drag or Discipline.CrossCountry
            ? 0
            : -Math.Max(0, (car.PowerHP - 300) / 200.0 * 0.15);

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
        double wbNorm = Math.Min(car.Wheelbase / 2700.0, 1.2);

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

        // More power on RWD → more rear toe-in for exit stability (skip drag — 0° is mandatory)
        if (car.DriveType == Models.DriveType.RWD && track.Discipline != Discipline.Drag)
            toeR += Math.Max(0, (car.PowerHP - 300) / 200.0 * 0.05);

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

    private static void CalculateCaster(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r, Dictionary<string, string> ex)
    {
        double baseByWeight = car.TotalMass switch
        {
            < 1100 => 5.5,
            < 1500 => 6.0,
            _      => 6.5
        };

        double speedAdj = Math.Max(0, (car.MaxSpeedKmh - 200) / 100.0 * 0.5);
        if (track.Discipline == Discipline.Drag)
            speedAdj = Math.Min(speedAdj, 0.3); // drag: light steering preferred, cap speed bonus

        double discAdj = track.Discipline switch
        {
            Discipline.Drag  => -0.5,
            Discipline.Rally => -0.5,
            Discipline.CrossCountry => -1.0,
            _                => 0.0
        };

        double caster = Clamp(baseByWeight + speedAdj + discAdj, c.CasterMin, c.CasterMax);
        r.Caster = Math.Round(caster, 1);
        ex["Caster"] = $"{r.Caster}° — " +
            $"{(r.Caster >= 6.5 ? "увеличенный — самовозврат и стабильность на скорости" : "стандартный")}. " +
            $"Масса {car.TotalMass} кг, макс. {car.MaxSpeedKmh} км/ч.";
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
        double refMass = 1500.0;
        double massScale = Math.Pow(car.TotalMass / refMass, 0.6);

        double wd = EffectiveWtDist(car);
        double wdDev = (wd - 50) / 50.0; // -1..+1

        // Base discipline values at refMass (from community reference tunes)
        (double baseF, double baseR, string note) = (track.Discipline, car.DriveType) switch
        {
            (Discipline.Drag, _)             => (2.0, 18.0, "Drag: мин. перед для переноса веса; жёсткий зад для платформы."),
            (Discipline.Drift, Models.DriveType.RWD) => (18.0, 55.0, "Drift: мягкий перед для завязки; макс. зад для удержания угла."),
            (Discipline.Drift, _)            => (20.0, 40.0, "Drift AWD: умеренные стабилизаторы."),
            (Discipline.Rally, _)            => (14.0, 12.0, "Ралли: мягкие — независимая работа колёс на грунте."),
            (Discipline.CrossCountry, _)     => (10.0, 10.0, "CC: мин. жёсткость для артикуляции подвески."),
            (Discipline.Touge, Models.DriveType.RWD) => (30.0, 26.0, "Тоге RWD: жёстче для точного управления."),
            (Discipline.Touge, Models.DriveType.FWD) => (10.0, 32.0, "Тоге FWD: мягкий перед для зацепа, жёсткий зад для ротации."),
            (Discipline.Touge, _)            => (34.0, 28.0, "Тоге AWD: сбалансированные."),
            (Discipline.Street, Models.DriveType.RWD) => (28.0, 24.0, "Стрит RWD: средняя жёсткость."),
            (Discipline.Street, Models.DriveType.FWD) => (10.0, 30.0, "Стрит FWD: мягкий перед для зацепа, жёстче зад против сноса."),
            (_, Models.DriveType.RWD)        => (22.0, 28.0, "Road RWD: классический баланс (ForzaFire F18-25)."),
            // FWD: soft front for grip, stiff rear for rotation — prevents understeer (forzafire.com)
            (_, Models.DriveType.FWD)        => (12.0, 28.0, "Road FWD: мягкий перед (зацеп), жёстче зад (ротация)."),
            (_, _)                           => (26.0, 33.0, "Road AWD: F26/R33 — сбалансированные стабилизаторы (ForzaFire F22-30/R28-38).")
        };

        // Wheelbase: longer → slightly stiffer (applied to base first, then add fixed offsets)
        double wbFactor = car.Wheelbase / 2700.0;
        double arbF = baseF * massScale * wbFactor;
        double arbR = baseR * massScale * wbFactor;

        // Weight distribution: shift after wbFactor so offset isn't scaled by wheelbase
        arbF += wdDev * 4.0;
        arbR -= wdDev * 4.0;

        // Power: more power → stiffer to control roll (drag skipped — stiff ARBs kill launch grip via wheel hop)
        double pwrAdj = track.Discipline == Discipline.Drag
            ? 0.0
            : Math.Max(0, (car.PowerHP - 300) / 200.0 * 3.0);
        arbF += pwrAdj;
        arbR += pwrAdj;

        r.ARBFront = Math.Round(Clamp(arbF, c.ARBFrontMin, c.ARBFrontMax));
        r.ARBRear  = Math.Round(Clamp(arbR, c.ARBRearMin,  c.ARBRearMax));
        ex["ARB"] = $"Стаб.: П {r.ARBFront} / З {r.ARBRear} (диап. 1–65). " +
            $"{car.DriveType}, база {car.Wheelbase} мм, {car.PowerHP} л.с. {note}";
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

        // Power: more power → stiffer rear to control squat
        double pwrHz = Math.Max(0, (car.PowerHP - 300) / 300.0 * 0.25);
        hzR += pwrHz;

        // K (kгс/мм) = (2π)² / (2 × 981) × f² × mass_kg × dist  (g=981 cm/s²)
        double sprF = 0.02012 * hzF * hzF * car.TotalMass * wdF;
        double sprR = 0.02012 * hzR * hzR * car.TotalMass * wdR;

        // Suspension upgrade multiplier.
        // Rally/CC disciplines already use soft Hz targets — Rally upgrade keeps them neutral (0.85)
        // rather than halving again (0.55). On road disciplines, 0.55 correctly softens road springs.
        bool offRoadDisc = track.Discipline is Discipline.Rally or Discipline.CrossCountry;
        double suspMul = car.SuspensionUpgrade switch
        {
            SuspensionUpgrade.Race   => 1.10,
            SuspensionUpgrade.Sport  => 1.00,
            SuspensionUpgrade.Street => 0.88,
            SuspensionUpgrade.Rally  => offRoadDisc ? 0.85 : 0.55,
            SuspensionUpgrade.Drift  => 0.85,
            _                        => 0.72
        };
        sprF *= suspMul;
        sprR *= suspMul;

        // Aspiration: more sudden power delivery → stiffer rear spring to control squat
        if (car.PowertrainType == PowertrainType.Hybrid)
        { sprF *= 1.05; sprR *= 1.05; }
        sprR *= GetPowerDeliveryFactors(car.PowertrainType, car.AspirationType, car.AntiLag).Spring;

        r.SpringFront = Clamp(Math.Round(sprF), c.SpringFrontMin, c.SpringFrontMax);
        r.SpringRear  = Clamp(Math.Round(sprR), c.SpringRearMin,  c.SpringRearMax);
        ex["Springs"] = $"Пружины: П {r.SpringFront} / З {r.SpringRear} кгс/мм " +
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
            SuspensionUpgrade.Race  => -5,
            SuspensionUpgrade.Sport => 0,
            SuspensionUpgrade.Street => 5,
            SuspensionUpgrade.Rally => 15,
            SuspensionUpgrade.Drift => -5,
            _                       => 0
        };

        switch (track.Discipline)
        {
            case Discipline.Drag:
                rhF = 60; rhR = 75;
                note = "Drag: мин. перед / повыш. зад для переноса веса на старте.";
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
        rhF += (avgRim - 19) * 1.5;
        rhR += (avgRim - 19) * 1.5;

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
        double refMass = 1500.0;
        double massScale = Math.Sqrt(car.TotalMass / refMass);

        double wdF = EffectiveWtDist(car) / 100.0;
        double wdDev = wdF - 0.5;

        // Base rebound for reference car, per discipline
        double baseReb = (track.Discipline, car.DriveType) switch
        {
            (Discipline.Drag, _)             => 10.0,
            (Discipline.Drift, _)            => 4.0,
            (Discipline.Rally, _)            => 9.0,
            (Discipline.CrossCountry, _)     => 8.0,
            (Discipline.Touge, _)            => 13.0,
            (Discipline.Street, _)           => 12.0,
            (_, DriveType.AWD)               => 18.0,
            (_, _)                           => 14.0
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

        // Power: more power → stiffer rear rebound
        rebR += Math.Max(0, (car.PowerHP - 300) / 200.0 * 0.5);

        double bmpF = rebF * bumpRatio;
        double bmpR = rebR * bumpRatio;

        // Suspension upgrade range modifier
        double suspMul = car.SuspensionUpgrade switch
        {
            SuspensionUpgrade.Race  => 1.10,
            SuspensionUpgrade.Sport => 1.00,
            SuspensionUpgrade.Rally => 1.05,
            SuspensionUpgrade.Drift => 0.95,
            SuspensionUpgrade.Street => 0.90,
            _                       => 0.85
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
            $"сжатие: П {r.BumpFront} / З {r.BumpRear}. (Bump ~{bmpF / rebF * 100:F0}% от Rebound)";
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

        double speedFactor = Math.Min(1.0, car.MaxSpeedKmh / 280.0);
        double pwrFactor = Math.Min(1.5, 1.0 + Math.Max(0, (car.PowerHP - 300) / 200.0 * 0.15));

        var (fwFactor, rwFactor) = car.DriveType switch
        {
            Models.DriveType.RWD => (0.55, 0.70),
            Models.DriveType.FWD => (0.65, 0.55),
            Models.DriveType.AWD => (0.90, 0.15),
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
                aeroF = car.HasFrontAero ? 30 : 0; aeroR = car.HasRearAero ? 50 : 0;
                break;
            case Discipline.Rally:
                aeroF = car.HasFrontAero ? 50 : 0; aeroR = car.HasRearAero ? 65 : 0;
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
        accel += Math.Max(0, (car.PowerHP - 300) / 200.0 * 5.0);
        // Weight: heavier → more accel lock
        accel += (car.TotalMass - 1400) / 100.0 * 1.5;
        // Engine position: rear/mid → more accel lock
        accel += car.EnginePosition switch { EnginePosition.Rear => 8.0, EnginePosition.Mid => 4.0, _ => 0.0 };
        // Wheelbase: longer → less accel lock (stable by design)
        accel -= (car.Wheelbase - 2700) / 500.0 * 3.0;
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
                Discipline.Drift  => 0.50,
                Discipline.Drag   => 0.60,
                Discipline.Rally  => 0.70,
                Discipline.CrossCountry => 0.60,
                _                 => 0.78
            };
            // Blend user preference toward community target
            bias = bias * 0.4 + targetBias * 0.6;
            // Wheelbase: longer → more rear bias
            bias += (car.Wheelbase - 2700) / 500.0 * 0.03;
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

            double frontFactor = 1.2 - bias * 0.4;
            double rearFactor  = 0.8 + bias * 0.4;

            double pwrF = Math.Max(0, (car.PowerHP - 300) / 200.0 * 3.0);

            r.DiffFrontAccel = Math.Round(Clamp((fAccel + pwrF) * cap * frontFactor, c.DiffAccelMin, c.DiffAccelMax));
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

    private static void CalculateBrakes(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r, Dictionary<string, string> ex)
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

        // FWD needs more front bias
        if (car.DriveType == Models.DriveType.FWD) bias += 4;

        bias = Clamp(bias, 45, 62);

        double pressure = track.Discipline switch
        {
            Discipline.Drift  => 85,
            Discipline.Rally  => 90,
            Discipline.CrossCountry => 85,
            _                 => 100
        };
        // Mass: lighter cars need less pressure, heavier need more (ForzaFire: 85-115%)
        pressure += (car.TotalMass - 1400) / 200.0 * 2.5;
        pressure += Math.Max(0, (car.PowerHP - 300) / 200.0 * 5.0);
        pressure += Math.Max(0, (car.MaxSpeedKmh - 200) / 100.0 * 5.0);
        if (car.DriveType == Models.DriveType.AWD) pressure += 5;

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

    private static int CalcRecommendedGearCount(CarCard car, TrackInfo track)
    {
        double pwRatio = car.PowerHP / (car.TotalMass / 1000.0);

        int rec = track.Discipline switch
        {
            Discipline.Drag when track.DragDistance == DragDistance.Eighth  => 2,
            Discipline.Drag when track.DragDistance == DragDistance.Quarter => 2,
            Discipline.Drag when track.DragDistance == DragDistance.Half    => 3,
            Discipline.Drag                                                  => 4,
            Discipline.Drift                                                 => 5,
            Discipline.Rally                                                 => 5,
            Discipline.CrossCountry                                          => 5,
            _                                                                => 6,
        };

        if (pwRatio > 250) rec++;
        if (car.MaxSpeedKmh > 280) rec++;
        if (car.EngineType == EngineType.I3) rec--;

        return Math.Clamp(rec, 1, 10);
    }

    private static void CalculateGearing(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r, Dictionary<string, string> ex)
    {
        int n = Math.Max(1, Math.Min(car.GearCount, 10));
        r.RecommendedGearCount = CalcRecommendedGearCount(car, track);

        double pwRatio = car.PowerHP / (car.TotalMass / 1000.0);
        double tireCirc = Math.PI * car.RearWheelDiameterInch * 0.0254;
        double targetKmh = track.Discipline == Discipline.Drag
            ? car.MaxSpeedKmh
            : Math.Min(car.MaxSpeedKmh, 400);
        double targetMs = targetKmh / 3.6;

        // Single-speed: Gear 1 × FinalDrive = total reduction (same Forza mechanics, one gear).
        // Gear 1 is discipline-based like ICE first gear; FD is back-calculated from the remainder.
        // If FD falls outside constraint range, gear 1 is adjusted to compensate.
        if (n == 1)
        {
            double total = targetMs > 0 && car.MaxRPM > 0
                ? car.MaxRPM * 0.95 * tireCirc / (60.0 * targetMs)
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

            double fd1 = total / g1;
            if (fd1 < c.FinalDriveMin) { fd1 = c.FinalDriveMin; g1 = total / fd1; }
            else if (fd1 > c.FinalDriveMax) { fd1 = c.FinalDriveMax; g1 = total / fd1; }

            g1  = Math.Round(g1,  2);
            fd1 = Math.Round(fd1, 2);
            r.GearRatios = new List<double> { g1 };
            r.FinalDrive = fd1;
            ex["FinalDrive"] = $"ГП {fd1}. Передача 1: {g1} (одна ступень). " +
                $"Суммарное передаточное: {g1} × {fd1} = {g1 * fd1:F2}. " +
                $"{car.MaxSpeedKmh} км/ч @ {car.MaxRPM} об/мин. Рек. передач: {r.RecommendedGearCount}.";
            return;
        }

        (double first, double top, string note) = track.Discipline switch
        {
            Discipline.Drag => GetDragRatios(track.DragDistance, n),
            Discipline.Drift    => (3.0, 0.85, "Удлинённые передачи для контроля в заносе."),
            Discipline.Rally    => (4.0, 0.70, "Короткий ряд для быстрого разгона на грунте."),
            Discipline.CrossCountry => (4.5, 0.65, "Макс. ускорение на бездорожье."),
            Discipline.Touge    => (3.8, 0.82, "Короткие передачи для горных серпантинов."),
            _                   => (3.5, 0.78, "Ряд под дорожные дисциплины.")
        };

        if (pwRatio > 200) { first += 0.3; top -= 0.05; }
        else if (pwRatio < 100) { first -= 0.3; top += 0.05; }

        var ratios = new List<double>(n);
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / (n - 1);
            ratios.Add(Math.Round(first * Math.Pow(top / first, t), 2));
        }

        double fd = targetMs > 0 && car.MaxRPM > 0
            ? car.MaxRPM * 0.95 * tireCirc / (60.0 * targetMs * top)
            : 3.50;
        if (track.Discipline != Discipline.Drag)
            fd *= 1.0 + Math.Max(0, (pwRatio - 150) / 200.0 * 0.05);

        r.GearRatios = ratios;
        r.FinalDrive = Math.Round(Clamp(fd, c.FinalDriveMin, c.FinalDriveMax), 2);

        string gearStr = string.Join("  ", ratios.Select((g, i) => $"{i + 1}: {g:F2}"));
        ex["FinalDrive"] = $"ГП {r.FinalDrive}. Рек. передач: {r.RecommendedGearCount}. Ряд: {gearStr}. " +
            $"{car.MaxSpeedKmh} км/ч @ {car.MaxRPM} об/мин, P/W {pwRatio:F1} л.с./т. {note}";
    }

    private static (double first, double top, string note) GetDragRatios(DragDistance dist, int gears)
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
