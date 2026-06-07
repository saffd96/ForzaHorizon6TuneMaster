using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

public class TuneGeneratorService
{
    private static string L(string key) => LocalizationService.Instance.T(key);

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
    private const double RefTireWidth        = 275; // reference width in mm for pressure adjustment
    // 0.85 = base log slope — multiplied by massWeight (0.3–1.0) for weight-class sensitivity
    private const double MassLogFactor       = 0.85;

    // ── Camber calibration constants ──
    private const double CamberPhysicsLayerCapNominal   = 1.5;
    private const double CamberTuningLayerCapNominal    = 1.0;
    private const double CamberMaxTotalDeviationNominal = 3.0;


    // Power/torque → camber
    private const double CamberPowerSigmoidK         = 0.025;
    private const double CamberTorqueSigmoidK        = 0.008;
    private const double CamberPowerMaxAdj           = 0.40;
    private const double CamberTorqueMaxAdj          = 0.25;

    // Load: separate factors (weight dist, CG, speed)
    private const double CamberWtSensitivity         = 0.40;
    private const double CamberCgFactorScale          = 0.30;
    private const double CamberSpeedScaleK            = 0.15;

    // Aero: saturation + class scale
    private const double CamberAeroMaxAdj             = 0.15;
    private const double CamberAeroSaturationRef      = 150.0;

    // Braking penalty (additive after normalization)
    private const double CamberBrakingThreshold        = 3.0;
    private const double CamberBrakingPenalty           = 0.20;

    // Drift: continuous assist
    private const double CamberDriftRangeMax            = 1.0;
    // 4π²/2000 = 0.019739 — includes ÷1000 (N/m→N/mm) and ÷2 (motion-ratio/half-axle calibration for FH6)
    private const double SpringHzToNmm      = 0.019739;
    private const double RevLimitFraction   = 0.95;
    private const double TargetSpeedCapKmh  = 700;

    private static (double Diff, double Spring, double Damper) GetPowerDeliveryFactors(
        PowertrainType pt, AspirationType? asp, bool antiLag = false)
    {
        if (pt == PowertrainType.Electric)
            return (1.20, 1.08, 1.06);

        var (d, s, dmpr) = (asp ?? AspirationType.Natural) switch
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
            d    = 1.0 + (d    - 1.0) * 0.60;
            s    = 1.0 + (s    - 1.0) * 0.60;
            dmpr = 1.0 + (dmpr - 1.0) * 0.60;
        }

        return (d, s, dmpr);
    }

    // Estimates CG height (mm) from suspension, engine position, and mass.
    private static double EstimateCGHeight(CarCard car)
    {
        double h = car.SuspensionUpgrade switch
        {
            SuspensionUpgrade.Race    => 345,
            SuspensionUpgrade.Sport   => 385,
            SuspensionUpgrade.Drift   => 375,
            SuspensionUpgrade.Street  => 415,
            SuspensionUpgrade.Rally   => 480,
            SuspensionUpgrade.Offroad => 560,
            _                         => 415
        };
        h += car.EnginePosition switch
        {
            EnginePosition.Rear    => -30,
            EnginePosition.Mid     => -15,
            _                      => 0
        };
        h += Math.Max(0, (car.TotalMass - 1200.0) / 200.0) * 8.0;
        return Math.Clamp(h, 280, 700);
    }

    // Drag-limited speed: body CdA + wing-induced drag + rolling resistance.
    // Forza uses HP at-wheel with no drivetrain loss (forums.forza.net confirmed).
    // Rolling resistance coefficients fitted to FH6 observed top speeds per tire type.
    private static double ComputeEffectiveMaxSpeedKmh(CarCard car, TuneResult r)
    {
        double cdABody = car.CdABodyEstimate;
        const double AeroDragFactor = 0.001787;
        double cdATotal = cdABody + (r.AeroFront + r.AeroRear) * AeroDragFactor;

        // FH6 already models a baseline tire-rolling loss internally.
        // CRR here represents only the DIFFERENTIAL between tire types so we don't double-count
        // the baseline. Values are ~50 % of real-world Crr to avoid over-penalising speed.
        double crr = car.TireType switch
        {
            TireType.Slick     => 0.004,
            TireType.SemiSlick => 0.005,
            TireType.Sport     => 0.005,
            TireType.Street    => 0.006,
            TireType.Stock     => 0.007,
            TireType.Rally     => 0.008,
            TireType.Winter    => 0.009, // soft compound + tread pattern = more rolling resistance
            TireType.Offroad   => 0.011,
            TireType.Drag      => 0.005,
            _                  => 0.006
        };

        double powerW = car.PowerHP * 745.7;
        const double rho = 1.225;
        double fRR = crr * car.TotalMass * 9.81;

        // Newton-Raphson: solve P = 0.5*rho*CdA*v³ + fRR*v
        double v = Math.Pow(powerW / (0.5 * rho * cdATotal), 1.0 / 3.0);
        for (int i = 0; i < 15; i++)
        {
            double fv = 0.5 * rho * cdATotal * v * v * v + fRR * v - powerW;
            double dv = 1.5 * rho * cdATotal * v * v + fRR;
            if (Math.Abs(dv) < 1e-10) break;
            double step = fv / dv;
            v = Math.Max(v - step, 1.0);
            if (Math.Abs(step) < 1e-4) break;
        }
        return Math.Round(Math.Clamp(v * 3.6, 60.0, TargetSpeedCapKmh));
    }

    public TuneResult Generate(CarCard car, TrackInfo track, TuningConstraints c)
    {
        var r  = new TuneResult { Car = car, Track = track };
        var ex = r.Explanations;

        CalculateAero(car, track, c, r, ex);
        double effectiveMaxKmh = ComputeEffectiveMaxSpeedKmh(car, r);
        CalculateAero(car, track, c, r, ex, effectiveMaxKmh);
        double prevMaxKmh = effectiveMaxKmh;
        effectiveMaxKmh = ComputeEffectiveMaxSpeedKmh(car, r);
        if (Math.Abs(effectiveMaxKmh - prevMaxKmh) > 1)
        {
            CalculateAero(car, track, c, r, ex, effectiveMaxKmh);
            effectiveMaxKmh = ComputeEffectiveMaxSpeedKmh(car, r);
        }

        CalculateTirePressure(car, track, c, r, ex);
        CalculateCamber(car, track, c, r, ex, effectiveMaxKmh);
        CalculateToe(car, track, c, r, ex, effectiveMaxKmh);
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
        // Trust any value the user explicitly set (anything other than the unmodified default 50.0).
        // Engine-position fallback only applies when the field was never filled in.
        if (car.WeightDistributionFront != 50.0)
            return car.WeightDistributionFront;
        return car.EnginePosition switch
        {
            EnginePosition.Front   => 55,
            EnginePosition.Mid     => 48,
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
        // Slick=32.5 PSI, SemiSlick=32.0, Sport=30.0, Stock/Street=31.0, Rally=29.5, Offroad=29.0
        double baseBar = car.TireType switch
        {
            TireType.Slick     => 2.24, // 32.5 PSI
            TireType.SemiSlick => 2.21, // 32.0 PSI
            TireType.Sport     => 2.07, // 30.0 PSI
            TireType.Street    => 2.14,
            TireType.Stock     => 2.14,
            TireType.Rally     => 2.03,
            TireType.Winter    => 1.97, // 28.5 PSI — soft compound needs lower pressure for footprint
            TireType.Offroad   => 2.00,
            TireType.Drag      => 2.21,
            _                  => 2.14
        };

        // Mass: log scaling with weight-class sensitivity
        // Light cars (<1000kg): ~0.3x — minimal pressure drop for being light
        // Heavy cars (>2300kg): ~1.0x — full log effect for SUVs
        double massRatio = car.TotalMass / MassBaselineKg;
        double massWeight = 0.30 + 0.70 * Math.Clamp((car.TotalMass - 900) / 1400, 0, 1);
        double massAdj = Math.Log(massRatio) * MassLogFactor * massWeight;

        // Weight distribution: nonlinear — soft deadzone near 50%, amplification at extremes
        // FH6 behaviour: 50/50 to 55/45 barely changes grip balance, 70/30+ dramatically does
        double wd = EffectiveWtDist(car);
        double wdDev = (wd - 50) / 50.0;
        double wdNonlinear = Math.Sign(wdDev) * Math.Pow(Math.Abs(wdDev), 1.5);
        double wdAdjF = wdNonlinear * 0.62;
        double wdAdjR = -wdNonlinear * 0.62;

        // Profile: lower profile → stiffer sidewall → slightly higher pressure
        double profile = (car.FrontTireProfile + car.RearTireProfile) / 2.0;
        double profileAdj = Math.Clamp((ProfileBaseline - profile) * 0.004, -0.15, 0.15);

        // Rim diameter: negligible in FH6 (profile covers sidewall stiffness)
        double rimAdj = 0;

        // Tire width: tanh saturation — diminishing returns at extreme widths
        // Drive-type weighting: RWD cares more about rear width, FWD about front
        double frontWidthWeight = car.DriveType == DriveType.FWD ? 1.30 : car.DriveType == DriveType.RWD ? 0.80 : 1.00;
        double rearWidthWeight  = car.DriveType == DriveType.RWD ? 1.30 : car.DriveType == DriveType.FWD ? 0.80 : 1.00;
        double widthAdjF = Math.Tanh((RefTireWidth - car.FrontTireWidth) / 100.0 * 0.5) * 0.10 * frontWidthWeight;
        double widthAdjR = Math.Tanh((RefTireWidth - car.RearTireWidth)  / 100.0 * 0.5) * 0.10 * rearWidthWeight;

        // Power-to-weight: split curve — below/above baseline behave differently
        // Below 1.0: gentle linear (underpowered cars need minimal adjustment)
        // Above 1.0: tanh saturation (powerful cars get diminishing returns)
        // Drag skipped — handled by discipline override below
        double powerAdjF = 0, powerAdjR = 0;
        if (track.Discipline != Discipline.Drag)
        {
            double ptwRatio = car.PowerHP / car.TotalMass / (PowerBaselineHP / MassBaselineKg);
            double ptwFactor = ptwRatio < 1.0
                ? (ptwRatio - 1.0) * 0.30
                : Math.Tanh((ptwRatio - 1.0) * 1.5) * 0.85;
            if (car.DriveType == Models.DriveType.RWD)
            {
                powerAdjR = -ptwFactor * 0.15;
                powerAdjF =  ptwFactor * 0.07;
            }
            else if (car.DriveType == Models.DriveType.FWD)
            {
                powerAdjF = -ptwFactor * 0.15;
                powerAdjR =  ptwFactor * 0.07;
            }
            else
            {
                powerAdjF = ptwFactor * 0.05;
                powerAdjR = ptwFactor * 0.05;
            }
        }

        double tpF = baseBar + massAdj + wdAdjF + profileAdj + rimAdj + widthAdjF + powerAdjF;
        double tpR = baseBar + massAdj + wdAdjR + profileAdj + rimAdj + widthAdjR + powerAdjR;

        double discF = 0, discR = 0;
        string reason;
        switch (track.Discipline)
        {
            case Discipline.Drag:
            {
                // Drag: rear pressure drops for launch grip, scaled by PTW and mass
                // High PTW → already launches well → less reduction
                // Heavy → more weight transfer benefit → more reduction
                double dragPtw = car.PowerHP / car.TotalMass;
                double dragPtwFactor = Math.Clamp((dragPtw - 100) / 400, 0, 1);
                double dragMassFactor = Math.Clamp((car.TotalMass - 1000) / 1000, 0, 1);
                discR = -0.50 - dragMassFactor * 0.60 + dragPtwFactor * 0.20;
                reason = L("Expl_TirePressureReason_Drag");
                break;
            }
            case Discipline.Drift:
            {
                // Drift: adjustment varies with base grip level
                // High-grip builds (sticky tires + wide + low PTW) need more rear pressure to slide
                // Low-grip builds already slide easily → milder adjustment
                double tireGrip = car.TireType switch
                {
                    TireType.Slick     => 1.00,
                    TireType.SemiSlick => 0.80,
                    TireType.Sport     => 0.60,
                    TireType.Street    => 0.50,
                    TireType.Stock     => 0.40,
                    TireType.Rally     => 0.30,
                    TireType.Offroad   => 0.20,
                    TireType.Drag      => 0.70,
                    _                  => 0.50
                };
                double avgWidth = (car.FrontTireWidth + car.RearTireWidth) / 2.0;
                double widthFactor = (avgWidth - 205) / 200;
                double ptwActual = car.PowerHP / car.TotalMass;
                double ptwFactor = Math.Clamp((ptwActual - 100) / 400, 0, 1);
                double gripMod = Math.Clamp(tireGrip * 0.60 + widthFactor * 0.30 - ptwFactor * 0.20, 0, 1);
                discF = -0.05 + gripMod * 0.02;
                discR =  0.05 + gripMod * 0.12;
                reason = L("Expl_TirePressureReason_Drift");
                break;
            }
            case Discipline.Rally:
                discF = -0.20; discR = -0.20;
                reason = L("Expl_TirePressureReason_Rally");
                break;
            case Discipline.CrossCountry:
                discF = -0.20; discR = -0.20;
                reason = L("Expl_TirePressureReason_CrossCountry");
                break;
            case Discipline.Touge:
                discF = -0.12; discR = -0.10;
                reason = L("Expl_TirePressureReason_Touge");
                break;
            case Discipline.Street:
                discR = 0.05;
                reason = L("Expl_TirePressureReason_Street");
                break;
            default:
                reason = L("Expl_TirePressureReason_Road");
                break;
        }

        r.TirePressureFront = Math.Round(Clamp(tpF + discF, c.TirePressureFrontMin, c.TirePressureFrontMax), 2);
        r.TirePressureRear  = Math.Round(Clamp(tpR + discR, c.TirePressureRearMin,  c.TirePressureRearMax),  2);
        ex["TirePressure"] = string.Format(L("Expl_TirePressure_Fmt"), r.TirePressureFront, r.TirePressureRear, reason);
    }

    // ── Camber ───────────────────────────────────────────────────────────────
    // Architecture:
    //   Physics → base discipline + tire grip (dynamic) + load/speed + aero     [capped per layer]
    //   Tuning  → power/grip + AWD/stability + engine/wt + drift               [capped per layer]
    //   Balance → penalty → normalize → clamp

    // Dynamic caps from car mass/PTW (🔴 #3)
    private static (double physicsCap, double tuningCap, double totalCap) GetDynamicCamberCaps(CarCard car)
    {
        double massRatio = Math.Clamp(car.TotalMass / 1500.0, 0.7, 1.3);
        double ptw = car.PowerHP / car.TotalMass;
        double ptwRatio = Math.Clamp(ptw / 0.2, 0.7, 1.3);
        double physicsCap = CamberPhysicsLayerCapNominal * massRatio;
        double tuningCap  = CamberTuningLayerCapNominal * ptwRatio;
        double totalCap   = CamberMaxTotalDeviationNominal * Math.Sqrt(massRatio * ptwRatio);
        return (physicsCap, tuningCap, totalCap);
    }

    // Hard squash: values exceeding cap are projected onto cap boundary
    private static (double f, double r) SoftSquashCamber(double f, double r, double cap)
    {
        double mag = Math.Sqrt(f * f + r * r);
        if (mag > cap)
        {
            double scale = cap / mag;
            return (f * scale, r * scale);
        }
        return (f, r);
    }

    // CG reference normalised by mass/class (🟡 #8)
    private static double GetCamberCgReference(CarCard car)
    {
        return 350 + Math.Clamp((car.TotalMass - 900) / 1400, 0, 1) * 140;
    }

    // Aero effect class scaling (🟡 #9)
    private static double GetCamberAeroClassScale(CarCard car)
    {
        return 1.0 / Math.Clamp(car.TotalMass / 1500.0, 0.7, 1.4);
    }

    private static void CalculateCamber(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r,
        Dictionary<string, string> ex, double effectiveMaxKmh)
    {
        if (!car.SuspensionAllowsAdvancedTuning)
        {
            r.CamberFront = 0;
            r.CamberRear = 0;
            ex["Camber"] = L("Expl_Camber_Disabled");
            return;
        }

        string reason = L(GetCamberReasonKey(track.Discipline));
        double baseF = GetCamberBaseF(track.Discipline);
        double baseR = GetCamberBaseR(track.Discipline);

        var (physicsCap, tuningCap, totalCap) = GetDynamicCamberCaps(car);

        // ── Physics layer (tire + load + aero) ──
        var grip = GetTireGripModel(car.TireType, track.Discipline);

        double physF = 0, physR = 0;
        var (tireF, tireR) = GetCamberTireAdjustment(grip, track.Discipline);
        physF += tireF; physR += tireR;

        var (loadF, loadR) = GetCamberLoadAdjustment(car, track.Discipline, grip, effectiveMaxKmh);
        physF += loadF; physR += loadR;

        var (aeroF, aeroR) = GetCamberAeroAdjustment(car, r);
        physF += aeroF; physR += aeroR;

        (physF, physR) = SoftSquashCamber(physF, physR, physicsCap);

        // ── Tuning layer (power + AWD + engine) ──
        double tuneF = 0, tuneR = 0;

        double powerScale = GetGripPowerScale(grip);
        var (pwrF, pwrR) = GetCamberPowerAdjustment(car, track.Discipline, powerScale);
        tuneF += pwrF; tuneR += pwrR;

        if (car.DriveType == DriveType.AWD)
        {
            var (awdF, awdR) = GetCamberAWDAdjustment(car, track.Discipline, grip);
            tuneF += awdF; tuneR += awdR;
        }

        var (epF, epR) = GetCamberEnginePositionAdjustment(car);
        tuneF += epF; tuneR += epR;

        // Drift: now in tuning layer (same constraint system)
        if (track.Discipline == Discipline.Drift)
        {
            var (driftF, driftR) = GetCamberDriftAdjustment(car, grip);
            tuneF += driftF;
            tuneR += driftR;
        }

        (tuneF, tuneR) = SoftSquashCamber(tuneF, tuneR, tuningCap);

        // Combine physics + tuning
        double camF = baseF + physF + tuneF;
        double camR = baseR + physR + tuneR;

        // ── Braking penalty (modifies camber directly, then normalize ensures caps) ──
        double braking = 0;
        if (camF < -CamberBrakingThreshold)
            braking += -(camF + CamberBrakingThreshold) * CamberBrakingPenalty;
        if (camR < -CamberBrakingThreshold)
            braking += -(camR + CamberBrakingThreshold) * CamberBrakingPenalty;
        double brakingPenalty = -braking * 0.3;
        camF += brakingPenalty;
        camR += brakingPenalty;

        r.CamberFront = Math.Round(Clamp(camF, c.CamberFrontMin, c.CamberFrontMax), 1);
        r.CamberRear  = Math.Round(Clamp(camR, c.CamberRearMin,  c.CamberRearMax),  1);
        ex["Camber"] = string.Format(L("Expl_Camber_Fmt"), r.CamberFront, r.CamberRear, reason);
    }

    // ── Helper: dynamic tire grip model (🔴 #1) ──
    // Returns (baseGrip, thermalSensitivity, wearResistance) as dimensionless 0..1 values.
    private static (double grip, double thermal, double wear) GetTireGripModel(TireType tire, Discipline disc)
    {
        bool offRoad = disc is Discipline.Rally or Discipline.CrossCountry;
        double grip = tire switch
        {
            TireType.Slick     => 1.00,
            TireType.SemiSlick => 0.90,
            TireType.Sport     => 0.80,
            TireType.Street    => 0.70,
            TireType.Stock     => 0.60,
            TireType.Winter    => 0.55,
            TireType.Rally     => 0.75,
            TireType.Offroad   => 0.65,
            TireType.Drag      => 0.85,
            _                  => 0.70
        };
        double thermal = tire switch // higher = more sensitive to temp (narrow operating window)
        {
            TireType.Slick     => 0.30,
            TireType.SemiSlick => 0.20,
            TireType.Sport     => 0.10,
            TireType.Street    => 0.05,
            TireType.Stock     => 0.05,
            TireType.Winter    => 0.40,
            TireType.Rally     => 0.15,
            TireType.Offroad   => 0.10,
            TireType.Drag      => 0.25,
            _                  => 0.10
        };
        double wear = tire switch // higher = wears faster under camber
        {
            TireType.Slick     => 0.40,
            TireType.SemiSlick => 0.30,
            TireType.Sport     => 0.20,
            TireType.Street    => 0.12,
            TireType.Stock     => 0.08,
            TireType.Winter    => 0.25,
            TireType.Rally     => 0.15,
            TireType.Offroad   => 0.10,
            TireType.Drag      => 0.20,
            _                  => 0.12
        };
        if (offRoad) { grip *= 0.85; thermal *= 0.5; } // loose surface reduces tire-dependent effects
        return (grip, thermal, wear);
    }

    // Grip-dependent power scale — how much camber the tire can use for power (🔴 #5)
    private static double GetGripPowerScale((double grip, double thermal, double wear) grip)
    {
        // Higher grip = tire can support more camber for power delivery
        return 0.5 + grip.grip * 0.5; // 0.5 (Stock) .. 1.0 (Slick)
    }

    private static double GetCamberBaseF(Discipline d) => d switch
    {
        Discipline.Drag         => -0.2,
        Discipline.Drift        => -2.5,
        Discipline.Rally        => -0.8,
        Discipline.CrossCountry => -0.4,
        Discipline.Touge        => -1.8,
        _                       => -1.3
    };

    private static double GetCamberBaseR(Discipline d) => d switch
    {
        Discipline.Drag         => 0.0,
        Discipline.Drift        => -0.8,
        Discipline.Rally        => -0.5,
        Discipline.CrossCountry => -0.4,
        Discipline.Touge        => -0.8,
        _                       => -0.7
    };

    private static string GetCamberReasonKey(Discipline d) => d switch
    {
        Discipline.Drag         => "Expl_CamberReason_Drag",
        Discipline.Drift        => "Expl_CamberReason_Drift",
        Discipline.Rally        => "Expl_CamberReason_Rally",
        Discipline.CrossCountry => "Expl_CamberReason_CrossCountry",
        Discipline.Touge        => "Expl_CamberReason_Touge",
        _                       => "Expl_CamberReason_Road"
    };

    // Tire compound: additive thermal/wear (simpler, more predictable)
    private static (double f, double r) GetCamberTireAdjustment(
        (double grip, double thermal, double wear) grip, Discipline disc)
    {
        bool offRoad = disc is Discipline.Rally or Discipline.CrossCountry;
        double adj = (grip.grip - 0.7) * 0.5 - grip.thermal * 0.08 - grip.wear * 0.05;
        if (offRoad) adj *= 0.5;
        return (adj, adj);
    }

    // Load: separate weight dist, CG, speed factors (simpler, separate concerns)
    private static (double f, double r) GetCamberLoadAdjustment(CarCard car, Discipline disc,
        (double grip, double thermal, double wear) grip, double effectiveMaxKmh)
    {
        double wdF = EffectiveWtDist(car) / 100.0;
        double wdDev = wdF - 0.5;

        double camF = -wdDev * CamberWtSensitivity;
        double camR =  wdDev * CamberWtSensitivity;

        // CG height → body roll → more camber
        double cgH = EstimateCGHeight(car);
        double cgRef = GetCamberCgReference(car);
        double cgFactor = (cgH - cgRef) / cgRef * CamberCgFactorScale;
        camF += cgFactor; camR += cgFactor;

        // Speed: less camber needed at high speed (aero loads suspension)
        double speedFactor = Math.Clamp((effectiveMaxKmh - 120.0) / 300.0, 0, 1);
        camF -= speedFactor * CamberSpeedScaleK;
        camR -= speedFactor * CamberSpeedScaleK;

        // Drivetrain squat: driven axle needs less static camber
        if (disc is Discipline.Road or Discipline.Street or Discipline.Touge)
        {
            if (car.DriveType == DriveType.RWD)
            {
                camF += -0.20; camR += 0.10;
            }
            else if (car.DriveType == DriveType.FWD)
            {
                camF += 0.15; camR += -0.15;
            }
            else
            {
                camF += -0.05; camR += -0.05;
            }
        }

        return (camF, camR);
    }

    // Aero: tanh saturation + class scale only (🟠 #6 simplified)
    private static (double f, double r) GetCamberAeroAdjustment(CarCard car, TuneResult r)
    {
        double aeroTotal = (car.HasFrontAero ? r.AeroFront : 0) + (car.HasRearAero ? r.AeroRear : 0);
        if (aeroTotal <= 0) return (0, 0);
        double saturation = Math.Tanh(aeroTotal / CamberAeroSaturationRef);
        double classScale = GetCamberAeroClassScale(car);
        double adj = -saturation * CamberAeroMaxAdj * classScale;
        return (adj, adj);
    }

    // Power/torque with sigmoid × grip capacity scaling (🔴 #5)
    private static (double f, double r) GetCamberPowerAdjustment(CarCard car, Discipline disc, double gripPowerScale)
    {
        if (disc is Discipline.Drag or Discipline.CrossCountry)
            return (0, 0);

        double pwrExcess = Math.Max(0, car.PowerHP - PowerBaselineHP);
        double trqExcess = Math.Max(0, car.TorqueNm - TorqueBaselineNm);

        // Torque curve: rpm spread proxy for power band width (🟡 #7)
        // Small spread = peaky engine → more camber help for drivability
        // Large spread = broad torque → less camber needed
        double torqueCurveFactor = 1.0;
        if (car.TorquePeakRPM > 0 && car.PowerPeakRPM > 0)
        {
            double spread = (double)(car.PowerPeakRPM - car.TorquePeakRPM) / car.PowerPeakRPM;
            torqueCurveFactor = 1.0 + Math.Max(0, (0.30 - spread)) * 1.2; // 1.0 (broad) .. 1.36 (peaky)
        }

        double pwrAdj = Math.Tanh(pwrExcess * CamberPowerSigmoidK) * CamberPowerMaxAdj * gripPowerScale;
        double trqAdj = Math.Tanh(trqExcess * CamberTorqueSigmoidK) * CamberTorqueMaxAdj * torqueCurveFactor * gripPowerScale;
        double total = -(pwrAdj + trqAdj);

        return car.DriveType switch
        {
            DriveType.RWD => (total * 0.30, total * 0.70),
            DriveType.FWD => (total * 0.70, total * 0.30),
            _             => (total * 0.50, total * 0.50)
        };
    }

    // AWD as stability vs grip trade-off (🔴 #4)
    private static (double f, double r) GetCamberAWDAdjustment(CarCard car,
        Discipline disc, (double grip, double thermal, double wear) grip)
    {
        // AWD inherently understeers → mitigate with slightly more front camber.
        // But too much front camber on AWD causes unstable rear → limit by grip.
        if (disc is Discipline.Road or Discipline.Street or Discipline.Touge)
        {
            double ptwFactor = Math.Clamp((car.PowerHP / car.TotalMass) / 0.35, 0, 1);
            double frontAdj = -0.08 - ptwFactor * 0.06; // more front camber with power
            double rearAdj  =  0.04 + ptwFactor * 0.04; // slightly less rear camber for stability
            return (frontAdj * grip.grip, rearAdj * grip.grip);
        }
        if (disc is Discipline.Rally or Discipline.CrossCountry)
            return (-0.10, 0.05); // AWD offroad: front bias for turn-in
        return (0, 0);
    }

    // Engine position → yaw inertia effect (per-layout values)
    private static (double f, double r) GetCamberEnginePositionAdjustment(CarCard car)
    {
        return car.EnginePosition switch
        {
            EnginePosition.Front => (-0.12,  0.06),
            EnginePosition.Rear  => ( 0.06, -0.12),
            _                    => ( 0.0,   0.0)
        };
    }

    // Drift: tuning layer input (same constraint system, smaller values)
    private static (double f, double r) GetCamberDriftAdjustment(CarCard car,
        (double grip, double thermal, double wear) grip)
    {
        double ptw = car.PowerHP / car.TotalMass;
        double ptwFactor = Math.Clamp((ptw * 1000 - 100) / 300, 0, 1);

        double assistFactor = car.DriveType switch
        {
            DriveType.AWD => 0.6 - Math.Clamp(ptw / 0.5, 0, 0.2),
            DriveType.FWD => 1.1 + Math.Clamp(ptw / 0.5, 0, 0.2),
            _             => 1.0
        };

        double driftF = (-0.5 - ptwFactor * 0.3) * assistFactor;
        double driftR = (-0.15 - ptwFactor * 0.1) * assistFactor;

        double tireFactor = -(grip.grip - 0.5) * 0.3;
        driftF += tireFactor;
        driftR += tireFactor * 0.5;

        return (driftF, driftR);
    }

    // Normalize total deviation from base (#7)
    // ── Toe ──────────────────────────────────────────────────────────────────
    // Community: default 0.0°, slight front toe-out (-0.1° to -0.2°) for turn-in,
    // rear toe-in (+0.1° to +0.3°) for stability (forzafire, forza.guide).

    private static void CalculateToe(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r, Dictionary<string, string> ex, double effectiveMaxKmh)
    {
        double wbRatio = Math.Min(car.Wheelbase / RefWheelbaseMm, 1.2);
        double massFactor = Math.Clamp(car.TotalMass / 1500.0, 0.7, 1.3);

        (double baseF, double baseR) = car.DriveType switch
        {
            Models.DriveType.RWD => (-0.15, 0.15),
            Models.DriveType.FWD => (-0.10, 0.18),
            _                    => (-0.08, 0.12)
        };

        baseF *= wbRatio * massFactor;
        baseR *= wbRatio * massFactor;

        (double discMulF, double discMulR) = (track.Discipline, car.DriveType) switch
        {
            (Discipline.Drag, _)                          => (0.0, 0.0),
            (Discipline.Drift, _)                         => (2.5, 2.5),
            (Discipline.Rally, _)                         => (1.3, 0.9),
            (Discipline.CrossCountry, _)                  => (0.8, 1.5),
            (Discipline.Touge, Models.DriveType.RWD)      => (1.4, 1.0),
            (Discipline.Touge, Models.DriveType.FWD)      => (1.2, 1.2),
            (Discipline.Touge, _)                         => (1.4, 1.1),
            _                                             => (1.0, 1.0)
        };

        double toeF = baseF * discMulF;
        double toeR = baseR * discMulR;

        double speedFactor = Math.Clamp((effectiveMaxKmh - 120.0) / 200.0, 0, 1);
        toeF *= (1.0 - speedFactor * 0.15);
        toeR *= (1.0 - speedFactor * 0.15);

        toeF = Math.Tanh(toeF / 0.5) * 0.5;
        toeR = Math.Tanh(toeR / 0.5) * 0.5;

        r.ToeFront = Math.Round(Clamp(toeF, c.ToeFrontMin, c.ToeFrontMax), 1);
        r.ToeRear  = Math.Round(Clamp(toeR, c.ToeRearMin,  c.ToeRearMax),  1);

        string fd = r.ToeFront < 0 ? L("Expl_Toe_Out") : r.ToeFront > 0 ? L("Expl_Toe_In") : L("Expl_Toe_Zero");
        string rd = r.ToeRear  > 0 ? L("Expl_Toe_In")   : r.ToeRear  < 0 ? L("Expl_Toe_Out") : L("Expl_Toe_Zero");
        ex["Toe"] = string.Format(L("Expl_Toe_Fmt"), r.ToeFront, fd, r.ToeRear, rd);
    }

    // ── Caster ───────────────────────────────────────────────────────────────
    // Community (forzafire): 5.0°–7.0° range.
    //   Light/agile: 5.0°–5.5°
    //   Mid-weight road: 5.5°–6.5°
    //   Heavy/high-speed: 6.5°–7.0°

    private static void CalculateCaster(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r, Dictionary<string, string> ex, double effectiveMaxKmh)
    {
        double baseByWeight = Math.Clamp(5.0 + (car.TotalMass - 800.0) / 600.0, 5.0, 7.5);

        double discMul = track.Discipline switch
        {
            Discipline.Drag         => 0.90,
            Discipline.Drift        => 0.85,
            Discipline.Rally        => 0.92,
            Discipline.CrossCountry => 0.95,
            _                       => 1.0
        };

        double speedWeightFactor = Math.Max(0, (effectiveMaxKmh - RefSpeedKmh) / 100.0 * 0.3 * (car.TotalMass / 1500.0));

        double caster = Math.Clamp(baseByWeight * discMul + speedWeightFactor, c.CasterMin, c.CasterMax);
        r.Caster = Math.Round(caster, 1);
        ex["Caster"] = string.Format(L("Expl_Caster_Fmt"),
            r.Caster,
            r.Caster >= 6.5 ? L("Expl_Caster_High") : L("Expl_Caster_Std"),
            car.TotalMass,
            effectiveMaxKmh);
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
        (double baseF, double baseR, string arbNoteKey) = (track.Discipline, car.DriveType) switch
        {
            (Discipline.Drag, _)             => (2.0, 18.0, "Expl_ARBNote_Drag"),
            (Discipline.Drift, Models.DriveType.RWD) => (5.0, 22.0, "Expl_ARBNote_DriftRWD"),
            (Discipline.Drift, _)            => (20.0, 40.0, "Expl_ARBNote_DriftAWD"),
            (Discipline.Rally, _)            => (14.0, 12.0, "Expl_ARBNote_Rally"),
            (Discipline.CrossCountry, _)     => (10.0, 10.0, "Expl_ARBNote_CrossCountry"),
            (Discipline.Touge, Models.DriveType.RWD) => (30.0, 26.0, "Expl_ARBNote_TougeRWD"),
            (Discipline.Touge, Models.DriveType.FWD) => (10.0, 32.0, "Expl_ARBNote_TougeFWD"),
            (Discipline.Touge, _)            => (34.0, 28.0, "Expl_ARBNote_TougeAWD"),
            (Discipline.Street, Models.DriveType.RWD) => (28.0, 24.0, "Expl_ARBNote_StreetRWD"),
            (Discipline.Street, Models.DriveType.FWD) => (10.0, 30.0, "Expl_ARBNote_StreetFWD"),
            (_, Models.DriveType.RWD)        => (28.0, 20.0, "Expl_ARBNote_RoadRWD"),
            (_, Models.DriveType.FWD)        => (12.0, 28.0, "Expl_ARBNote_RoadFWD"),
            (_, _)                           => (26.0, 33.0, "Expl_ARBNote_RoadAWD")
        };
        string note = L(arbNoteKey);

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

        // CG height → roll moment → ARB stiffness correction.
        // Conservative magnitude (-3..+8) — FH6 already smooths some CG effects internally.
        // Drag skipped — soft ARB preserves forward weight transfer for launch.
        if (track.Discipline != Discipline.Drag)
        {
            double cgH = EstimateCGHeight(car);
            double rollAdj = Math.Clamp((cgH - 420.0) / 420.0 * 6.0, -3.0, 8.0);
            arbF += rollAdj;
            arbR += rollAdj;
        }

        r.ARBFront = Math.Round(Clamp(arbF, c.ARBFrontMin, c.ARBFrontMax), 1);
        r.ARBRear  = Math.Round(Clamp(arbR, c.ARBRearMin,  c.ARBRearMax), 1);
        double cgH_arb = EstimateCGHeight(car);
        ex["ARB"] = string.Format(L("Expl_ARB_Fmt"), r.ARBFront, r.ARBRear, note);
    }

    // ── Springs ──────────────────────────────────────────────────────────────

    private static void CalculateSprings(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r, Dictionary<string, string> ex)
    {
        if (!car.SuspensionAllowsAdvancedTuning)
        {
            ex["Springs"] = L("Expl_Springs_Disabled");
            return;
        }

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

        // K (Н/мм) per spring = SpringHzToNmm × f² × m_corner
        double sprF = SpringHzToNmm * hzF * hzF * car.TotalMass * wdF;
        double sprR = SpringHzToNmm * hzR * hzR * car.TotalMass * wdR;

        // CG height: higher CG → larger roll moment → stiffer springs required.
        // Magnitude is conservative (0.35) because FH6 already smooths some CG effects internally.
        // Track width: wider track → smaller roll arm → slightly softer springs.
        double cgH_spr    = EstimateCGHeight(car);
        double cgFactor   = Math.Clamp(1.0 + (cgH_spr - 420.0) / 700.0 * 0.35, 0.90, 1.25);
        double avgTrack_s = car.FrontTrack > 0 && car.RearTrack > 0
            ? (car.FrontTrack + car.RearTrack) / 2.0 : 1600.0;
        double trackRollF = Math.Pow(Math.Max(1100.0, avgTrack_s) / 1600.0, -0.35);
        sprF *= cgFactor * trackRollF;
        sprR *= cgFactor * trackRollF;

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
        ex["Springs"] = string.Format(L("Expl_Springs_Fmt"), r.SpringFront, r.SpringRear, hzF, hzR, L($"Enum_SuspensionUpgrade_{car.SuspensionUpgrade}"));
    }

    // ── Ride Height ──────────────────────────────────────────────────────────
    // Community (forzafire): road = minimum, rally = 70-80%, CC = max, drag = min front / sl higher rear

    private static void CalculateRideHeight(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r, Dictionary<string, string> ex)
    {
        if (!car.SuspensionAllowsAdvancedTuning)
        {
            ex["RideHeight"] = L("Expl_RideHeight_Disabled");
            return;
        }

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
                rhF = 65; rhR = 68;
                note = L("Expl_RideHeightNote_Drag");
                break;
            case Discipline.Drift:
                rhF = 80; rhR = 88;
                note = L("Expl_RideHeightNote_Drift");
                break;
            case Discipline.Rally:
                rhF = 130; rhR = 140;
                note = L("Expl_RideHeightNote_Rally");
                break;
            case Discipline.CrossCountry:
                rhF = 170; rhR = 180;
                note = L("Expl_RideHeightNote_CrossCountry");
                break;
            default:
                rhF = 68; rhR = 74;
                note = L("Expl_RideHeightNote_Road");
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
        ex["RideHeight"] = string.Format(L("Expl_RideHeight_Fmt"), r.RideHeightFront, r.RideHeightRear, note);
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

        r.ReboundFront = Math.Round(Clamp(rebF, c.ReboundFrontMin, c.ReboundFrontMax), 1);
        r.ReboundRear  = Math.Round(Clamp(rebR, c.ReboundRearMin,  c.ReboundRearMax), 1);
        r.BumpFront    = Math.Round(Clamp(bmpF, c.BumpFrontMin,    c.BumpFrontMax), 1);
        r.BumpRear     = Math.Round(Clamp(bmpR, c.BumpRearMin,     c.BumpRearMax), 1);
        ex["Dampers"] = string.Format(L("Expl_Dampers_Fmt"),
            r.ReboundFront, r.ReboundRear, r.BumpFront, r.BumpRear,
            rebF > 0 ? bmpF / rebF * 100 : 0,
            rebR > 0 ? bmpR / rebR * 100 : 0);
    }

    // ── Aero ─────────────────────────────────────────────────────────────────
    // Community (forza.guide): max front, rear for stability.
    // RWD → rear emphasis, FWD/AWD → front emphasis.

    private static void CalculateAero(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r, Dictionary<string, string> ex, double? overrideSpeedKmh = null)
    {
        if (!car.HasFrontAero && !car.HasRearAero)
        {
            r.AeroFront = 0; r.AeroRear = 0;
            ex["Aero"] = L("Expl_Aero_None");
            return;
        }

        // Use effective speed (including wing drag) when available for accurate downforce calibration
        double speedFactor = (overrideSpeedKmh ?? car.MaxSpeedKmh) / 280.0;
        double pwrFactor = Math.Min(1.5, 1.0 + Math.Max(0, (car.PowerHP - PowerBaselineHP) / PowerStepHP * 0.15));

        var (fwFactor, rwFactor) = car.DriveType switch
        {
            Models.DriveType.RWD => (0.55, 0.70),  // rear-biased for RWD stability
            Models.DriveType.FWD => (0.65, 0.55),  // slight front emphasis for FWD
            Models.DriveType.AWD => (0.65, 0.55),  // front emphasis to mitigate AWD understeer
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
        ex["Aero"] = string.Format(L("Expl_Aero_Fmt"), r.AeroFront, r.AeroRear, car.MaxSpeedKmh, car.PowerHP);
    }

    // ── Differential ────────────────────────────────────────────────────────
    // Community (forzafire.com FH6 drivetrain guide):
    //   RWD: Accel 40-65%, Decel 15-30% (road)
    //   FWD: Accel 80-95%, Decel 0-10%
    //   AWD road: Front Accel 28%, Decel 0%; Rear Accel 100%, Decel 45%; Center 70-85% rear
    //   AWD rally: Front 35-40%, Rear 55-70%, Center 65-75% rear

    private static void CalculateDifferential(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r, Dictionary<string, string> ex)
    {
        if (car.DifferentialUpgrade == DifferentialUpgrade.Stock)
        {
            ex["Differential"] = L("Expl_Differential_Stock");
            return;
        }

        // Upgrade hardware fraction: scales the calculated optimal lock value, preserving
        // the relative character of each build rather than hard-clamping to a flat maximum.
        // A well-calculated 65 % on Sport diff stays ~57 %; the same on Race stays 65 %.
        double hwFraction = car.DifferentialUpgrade switch
        {
            DifferentialUpgrade.Stock     => 0.62,
            DifferentialUpgrade.Street    => 0.76,
            DifferentialUpgrade.Sport     => 0.88,
            DifferentialUpgrade.Rally     => 0.94,
            DifferentialUpgrade.Offroad   => 0.90, // between Sport and Rally — optimised for traction, not precision lock
            DifferentialUpgrade.Race      => 1.00,
            DifferentialUpgrade.DriftSpec => 1.00,
            _                             => 0.88
        };
        // Keep maxAccel/maxDecel as absolute safety ceilings only (not primary limiter)
        const double maxAccel = 100.0, maxDecel = 100.0;

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

        accel = Math.Min(accel * hwFraction, maxAccel);
        decel = Math.Min(decel * hwFraction, maxDecel);

        r.DiffAccel = Math.Round(Clamp(accel, c.DiffAccelMin, c.DiffAccelMax));
        r.DiffDecel = car.DifferentialUpgrade == DifferentialUpgrade.Sport
            ? 0
            : Math.Round(Clamp(decel, c.DiffDecelMin, c.DiffDecelMax));

        string aspLabel = car.AspirationType switch
        {
            AspirationType.SingleTurbo          => L("Enum_AspirationType_SingleTurbo"),
            AspirationType.TwinTurbo            => L("Enum_AspirationType_TwinTurbo"),
            AspirationType.PositiveDisplacement => L("Enum_AspirationType_PositiveDisplacement"),
            AspirationType.Centrifugal          => L("Enum_AspirationType_Centrifugal"),
            AspirationType.Electric             => L("Enum_AspirationType_Electric"),
            _                                   => L("Enum_AspirationType_Natural")
        };
        string engPosLabel = car.EnginePosition switch
        {
            EnginePosition.Front   => L("Enum_EnginePosition_Front"),
            EnginePosition.Mid     => L("Enum_EnginePosition_Mid"),
            EnginePosition.Rear    => L("Enum_EnginePosition_Rear"),
            _                      => car.EnginePosition.ToString()
        };
        string diag = string.Format(L("Expl_Differential_DiagFmt"), car.PowerHP, aspLabel, engPosLabel, car.Wheelbase);

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

            // Individualize front diff by weight distribution and engine position.
            // Front-heavy AWD (Audi/VW) → more front lock to counter understeer.
            // Rear-biased AWD (GT-R, Huracán, Evo with custom distribution) → less front lock.
            double wdFront_awd = EffectiveWtDist(car);
            fAccel += (wdFront_awd - 50.0) * 0.5;   // +5 for 60/40, -5 for 40/60
            fAccel += car.EnginePosition switch
            {
                EnginePosition.Front   =>  5.0,   // front-engine: more understeer tendency
                EnginePosition.Mid     =>  0.0,
                EnginePosition.Rear    => -8.0,
                _                      =>  0.0
            };
            fAccel = Math.Clamp(fAccel, 5.0, 60.0);

            // bias → rear-biased: reduce front lock, increase rear lock
            double frontFactor = 1.2 - bias * 0.4;
            double rearFactor  = 0.8 + bias * 0.4;

            // Apply rearFactor to rear diff (accel already capped by maxAccel)
            r.DiffAccel = Math.Round(Clamp(accel * rearFactor, c.DiffAccelMin, c.DiffAccelMax));
            r.DiffDecel = car.DifferentialUpgrade == DifferentialUpgrade.Sport
                ? 0
                : Math.Round(Clamp(decel * rearFactor, c.DiffDecelMin, c.DiffDecelMax));

            r.DiffFrontAccel = Math.Round(Clamp(Math.Min(fAccel * hwFraction, maxAccel) * frontFactor, c.DiffAccelMin, c.DiffAccelMax));
            r.DiffFrontDecel = car.DifferentialUpgrade == DifferentialUpgrade.Sport
                ? 0
                : Math.Round(Clamp(Math.Min(fDecel * hwFraction, maxDecel) * frontFactor, c.DiffDecelMin, c.DiffDecelMax));
            r.CenterDiffBias = Math.Round(bias * 100);
        }

        if (r.DiffFrontAccel.HasValue)
            ex["Differential"] = string.Format(L("Expl_Differential_AWDFmt"), r.DiffAccel, r.DiffDecel, r.DiffFrontAccel, r.DiffFrontDecel, r.CenterDiffBias, diag);
        else
            ex["Differential"] = string.Format(L("Expl_Differential_Fmt"), r.DiffAccel, r.DiffDecel, diag);
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

        // FWD: structural front bias (driven wheels + weight over front axle)
        if (car.DriveType == Models.DriveType.FWD)
            bias += 4.0;

        bias = Clamp(bias, 45, 62);

        double pressure = track.Discipline switch
        {
            Discipline.Drift  => 85,
            Discipline.Rally  => 90,
            Discipline.CrossCountry => 85,
            _                 => 100
        };
        // Pressure scales with kinetic energy at max speed: KE ∝ mass × v².
        // High-speed and heavy cars need more braking force and therefore more line pressure.
        pressure += (car.TotalMass - MassBaselineKg) / 200.0 * 2.5;
        pressure += Math.Max(0, (effectiveMaxKmh - RefSpeedKmh) / 100.0 * 5.0);
        if (car.DriveType == Models.DriveType.AWD) pressure += 5;
        // Brake upgrade: better calipers/pads → higher effective bite
        pressure += car.BrakesUpgrade switch
        {
            BrakesUpgrade.Race   => 5.0,
            BrakesUpgrade.Sport  => 2.5,
            BrakesUpgrade.Street => 1.0,
            _                    => 0.0
        };

        r.BrakeBalance  = Math.Round(Clamp(bias,  c.BrakeBalanceMin,  c.BrakeBalanceMax));
        r.BrakePressure = Math.Round(Clamp(pressure, c.BrakePressureMin, c.BrakePressureMax));

        string reason = track.Discipline switch
        {
            Discipline.Drift   => L("Expl_BrakesReason_Drift"),
            Discipline.Drag    => L("Expl_BrakesReason_Drag"),
            _                  => L("Expl_BrakesReason_Default")
        };
        ex["Brakes"] = string.Format(L("Expl_Brakes_Fmt"), r.BrakeBalance, r.BrakePressure, reason);
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
        double targetKmh = Math.Min(effectiveMaxKmh, TargetSpeedCapKmh);
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
        (double first, double stepMin, double stepMax, string noteKey) = discipline switch
        {
            Discipline.Drift        => (3.0, 0.70, 0.88, "Expl_GearNote_Drift"),
            Discipline.Rally        => (4.0, 0.68, 0.78, "Expl_GearNote_Rally"),
            Discipline.CrossCountry => (4.5, 0.66, 0.75, "Expl_GearNote_CrossCountry"),
            Discipline.Touge        => (3.8, 0.70, 0.84, "Expl_GearNote_Touge"),
            _                       => (3.5, 0.68, 0.82, "Expl_GearNote_Road")
        };
        string note = L(noteKey);

        // Continuous P/W adjustment — higher P/W → longer first gear to keep engine in power band longer
        first += Math.Clamp((pwRatio - 150.0) / 100.0 * 0.30, -0.45, 0.50);

        if (fuelType == FuelType.Diesel)
            first = Math.Max(first - 0.45, 1.5);

        return (first, stepMin, stepMax, note);
    }

    private static void ApplyAspirationStepAdjustment(AspirationType? aspiration, bool antiLag, ref double stepMin, ref double stepMax)
    {
        switch (aspiration ?? AspirationType.Natural)
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
        r.RecommendedGearCount = CalcRecommendedGearCount(car, track, effectiveMaxKmh);

        if (!car.AllowGearCalculation)
        {
            ex["FinalDrive"] = string.Format(L("Expl_FinalDrive_NoCalc"), r.RecommendedGearCount);
            return;
        }

        int n = Math.Max(1, Math.Min(car.GearCount, 10));

        double pwRatio = car.PowerHP / (car.TotalMass / 1000.0);
        double tireCirc = Math.PI * car.RearWheelDiameterInch * 0.0254;

double targetKmh = track.Discipline == Discipline.Drag
    ? effectiveMaxKmh
    : Math.Min(effectiveMaxKmh, TargetSpeedCapKmh);
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
                Discipline.Drag         => Math.Clamp(4.5 - (car.TorqueNm / Math.Max(car.TotalMass, 500.0) - 0.25) * 1.40, 2.0, 5.5),
                Discipline.CrossCountry => 4.5,
                Discipline.Rally        => 4.0,
                Discipline.Touge        => 3.8,
                Discipline.Drift        => 3.0,
                _                       => 3.5
            };
            g1 += Math.Clamp((pwRatio - 150.0) / 100.0 * 0.30, -0.45, 0.50);
            g1 = Math.Max(1.0, g1);

            // Resolve both constraint ranges: FinalDrive first, then GearRatio.
            // If both clamp simultaneously the product g1×fd1 may not equal total — accept best fit.
            double fd1 = Math.Clamp(total / g1, c.FinalDriveMin, c.FinalDriveMax);
            g1 = Math.Clamp(total / fd1, GearRatioMin, GearRatioMax);
            fd1 = Math.Clamp(total / g1, c.FinalDriveMin, c.FinalDriveMax);

            g1  = Math.Round(g1,  2);
            fd1 = Math.Round(fd1, 2);
            r.FinalDrive = fd1;
            if (car.OnlyFinalDriveCalculation)
            {
                ex["FinalDrive"] = string.Format(L("Expl_FinalDrive_OnlyFD"), fd1, r.RecommendedGearCount);
            }
            else
            {
                r.GearRatios = new List<double> { g1 };
                ex["FinalDrive"] = string.Format(L("Expl_FinalDrive_SingleGear"),
                    fd1, g1, g1, fd1, g1 * fd1, effectiveMaxKmh, r.ActualMaxSpeedKmh, car.MaxRPM, r.RecommendedGearCount);
            }
            return;
        }

        double first, top;
        string note;

        if (track.Discipline == Discipline.Drag)
        {
            (first, top, note) = GetDragRatios(car, track.DragDistance);
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

        // Band width as fraction of MaxRPM — physically meaningful and not confused by redline level.
        // V8 ≈ 0.30, I4/Boxer ≈ 0.27, Rotary ≈ 0.22 (narrow → tighter upper gears → higher degFactor).
        // Reference 0.28 is the I4/V6 midpoint; engines narrower than that get a higher degFactor.
        double bandWidth = car.MaxRPM > 0 && car.PowerPeakRPM > 0 && car.TorquePeakRPM > 0
            ? (double)(car.PowerPeakRPM - car.TorquePeakRPM) / car.MaxRPM
            : 0.28;
        degFactor += (0.28 - bandWidth) * 0.20;

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
            if (n == 1)
            {
                ratios.Add(Math.Round(Math.Clamp(first, GearRatioMin, GearRatioMax), 2));
            }
            else
            {
                for (int i = 0; i < n; i++)
                {
                    double t = i;
                    ratios.Add(Math.Round(Math.Clamp(first * Math.Pow(top / first, t), GearRatioMin, GearRatioMax), 2));
                }
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

        r.FinalDrive = Math.Round(Clamp(fd, c.FinalDriveMin, c.FinalDriveMax), 2);

        if (car.OnlyFinalDriveCalculation)
        {
            ex["FinalDrive"] = string.Format(L("Expl_FinalDrive_OnlyFD"), r.FinalDrive, r.RecommendedGearCount) + " " + note;
            return;
        }

        r.GearRatios = ratios;
        string gearStr = string.Join("  ", ratios.Select((g, i) => $"{i + 1}: {g:F2}"));
        double actualKmhMulti = r.ActualMaxSpeedKmh;
        ex["FinalDrive"] = string.Format(L("Expl_FinalDrive_MultiGear"),
            r.FinalDrive, r.RecommendedGearCount, gearStr, effectiveMaxKmh, actualKmhMulti, car.MaxRPM, note);

        // Validate RPM drop after each shift — warn if engine falls below torque peak on any shift
        if (car.MaxRPM > 0 && car.TorquePeakRPM > 0 && car.PowerPeakRPM > 0 && ratios.Count > 1)
        {
            double shiftRpm = car.PowerPeakRPM;
            double minSafeRpm = car.TorquePeakRPM * 0.90;
            var dropWarnings = new List<string>();
            for (int i = 0; i < ratios.Count - 1; i++)
            {
                double rpmAfterShift = shiftRpm * ratios[i + 1] / ratios[i];
                if (rpmAfterShift < minSafeRpm)
                    dropWarnings.Add($"{i + 1}→{i + 2}: {rpmAfterShift:F0} {L("Expl_RpmAbbr")}");
            }
            if (dropWarnings.Count > 0)
                ex["FinalDrive"] += string.Format(L("Expl_FinalDrive_Warning"), minSafeRpm, string.Join(", ", dropWarnings));
        }
    }

    private static (double first, double top, string note) GetDragRatios(CarCard car, DragDistance dist)
    {
        // First gear from torque-to-weight: high torque per kg → shorter (numerically smaller) gear
        // to keep launches manageable without excessive wheelspin.
        // Reference: 0.25 Nm/kg (economy) → first≈4.5; 0.9 Nm/kg (muscle/turbo) → first≈3.5
        double tqPerKg = car.TorqueNm / Math.Max(car.TotalMass, 500.0);
        double first   = Math.Clamp(4.5 - (tqPerKg - 0.25) * 1.40, 2.2, 5.5);
        first *= car.DriveType switch
        {
            DriveType.AWD => 1.05,  // better traction → can use slightly shorter first
            DriveType.FWD => 0.92,  // limited traction → longer first to reduce wheelspin
            _             => 1.0
        };
        first = Math.Round(Math.Clamp(first, 2.0, 5.5), 2);

        double top = dist switch
        {
            DragDistance.Eighth  => 1.80,
            DragDistance.Quarter => 1.30,
            DragDistance.Half    => 1.00,
            DragDistance.Mile    => 0.85,
            _                    => 1.30
        };
        string distLabel = L($"Expl_DragDistStr_{(int)dist}");
        string note = string.Format(L("Expl_DragNote"), distLabel, first, tqPerKg, L($"Enum_DriveType_{car.DriveType}"));
        return (first, top, note);
    }
}
