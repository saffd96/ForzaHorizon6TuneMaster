using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

internal static class AlignmentCalculator
{
    private const double CamberPhysicsLayerCapNominal   = 1.5;
    private const double CamberTuningLayerCapNominal    = 1.0;
    private const double CamberMaxTotalDeviationNominal = 3.0;

    private const double CamberPowerSigmoidK         = 0.025;
    private const double CamberTorqueSigmoidK        = 0.008;
    private const double CamberPowerMaxAdj           = 0.40;
    private const double CamberTorqueMaxAdj          = 0.25;

    private const double CamberWtSensitivity         = 0.40;
    private const double CamberCgFactorScale          = 0.30;
    private const double CamberSpeedScaleK            = 0.15;

    private const double CamberAeroMaxAdj             = 0.15;
    private const double CamberAeroSaturationRef      = 150.0;

    private const double CamberBrakingThreshold        = 3.0;
    private const double CamberBrakingPenalty           = 0.20;

    private const double CamberDriftRangeMax            = 1.0;

    public static void CalculateCamber(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r,
        Dictionary<string, string> ex, double effectiveMaxKmh)
    {
        if (!car.SuspensionAllowsAdvancedTuning)
        {
            r.CamberFront = 0;
            r.CamberRear = 0;
            ex["Camber"] = CalculationHelpers.L("Expl_Camber_Disabled");
            return;
        }

        string reason = CalculationHelpers.L(GetCamberReasonKey(track.Discipline));
        double baseF = GetCamberBaseF(track.Discipline);
        double baseR = GetCamberBaseR(track.Discipline);

        var (physicsCap, tuningCap, totalCap) = GetDynamicCamberCaps(car);

        var grip = GetTireGripModel(car.TireType, track.Discipline);

        double physF = 0, physR = 0;
        var (tireF, tireR) = GetCamberTireAdjustment(grip, track.Discipline);
        physF += tireF; physR += tireR;

        var (loadF, loadR) = GetCamberLoadAdjustment(car, track.Discipline, grip, effectiveMaxKmh);
        physF += loadF; physR += loadR;

        var (aeroF, aeroR) = GetCamberAeroAdjustment(car, r);
        physF += aeroF; physR += aeroR;

        (physF, physR) = SoftSquashCamber(physF, physR, physicsCap);

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

        if (track.Discipline == Discipline.Drift)
        {
            var (driftF, driftR) = GetCamberDriftAdjustment(car, grip);
            tuneF += driftF;
            tuneR += driftR;
        }

        (tuneF, tuneR) = SoftSquashCamber(tuneF, tuneR, tuningCap);

        double camF = baseF + physF + tuneF;
        double camR = baseR + physR + tuneR;

        double braking = 0;
        if (camF < -CamberBrakingThreshold)
            braking += -(camF + CamberBrakingThreshold) * CamberBrakingPenalty;
        if (camR < -CamberBrakingThreshold)
            braking += -(camR + CamberBrakingThreshold) * CamberBrakingPenalty;
        double brakingPenalty = braking * 0.3;
        camF += brakingPenalty;
        camR += brakingPenalty;

        r.CamberFront = Math.Round(CalculationHelpers.Clamp(camF, c.CamberFrontMin, c.CamberFrontMax), 1);
        r.CamberRear  = Math.Round(CalculationHelpers.Clamp(camR, c.CamberRearMin,  c.CamberRearMax),  1);
        ex["Camber"] = string.Format(CalculationHelpers.L("Expl_Camber_Fmt"), r.CamberFront, r.CamberRear, reason);
    }

    public static void CalculateToe(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r, Dictionary<string, string> ex, double effectiveMaxKmh)
    {
        double wbRatio = Math.Min(car.Wheelbase / CalculationHelpers.RefWheelbaseMm, 1.2);
        double massFactor = CalculationHelpers.Clamp(car.TotalMass / 1500.0, 0.7, 1.3);

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
            (Discipline.Touge, Models.DriveType.RWD)      => (1.4, 1.3),
            (Discipline.Touge, Models.DriveType.FWD)      => (1.2, 1.5),
            (Discipline.Touge, _)                         => (1.4, 1.4),
            _                                             => (1.0, 1.0)
        };

        double toeF = baseF * discMulF;
        double toeR = baseR * discMulR;

        double speedFactor = CalculationHelpers.Clamp((effectiveMaxKmh - 120.0) / 200.0, 0, 1);
        toeF *= (1.0 - speedFactor * 0.15);
        toeR *= (1.0 - speedFactor * 0.15);

        toeF = Math.Tanh(toeF / 0.5) * 0.5;
        toeR = Math.Tanh(toeR / 0.5) * 0.5;

        r.ToeFront = Math.Round(CalculationHelpers.Clamp(toeF, c.ToeFrontMin, c.ToeFrontMax), 1);
        r.ToeRear  = Math.Round(CalculationHelpers.Clamp(toeR, c.ToeRearMin,  c.ToeRearMax),  1);

        string fd = r.ToeFront < 0 ? CalculationHelpers.L("Expl_Toe_Out") : r.ToeFront > 0 ? CalculationHelpers.L("Expl_Toe_In") : CalculationHelpers.L("Expl_Toe_Zero");
        string rd = r.ToeRear  > 0 ? CalculationHelpers.L("Expl_Toe_In")   : r.ToeRear  < 0 ? CalculationHelpers.L("Expl_Toe_Out") : CalculationHelpers.L("Expl_Toe_Zero");
        ex["Toe"] = string.Format(CalculationHelpers.L("Expl_Toe_Fmt"), r.ToeFront, fd, r.ToeRear, rd);
    }

    public static void CalculateCaster(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r, Dictionary<string, string> ex, double effectiveMaxKmh)
    {
        double baseByWeight = CalculationHelpers.Clamp(5.0 + (car.TotalMass - 800.0) / 600.0, 5.0, 7.5);

        double discMul = track.Discipline switch
        {
            Discipline.Drag         => 0.90,
            Discipline.Drift        => 0.85,
            Discipline.Rally        => 0.92,
            Discipline.CrossCountry => 0.95,
            _                       => 1.0
        };

        double speedWeightFactor = Math.Max(0, (effectiveMaxKmh - CalculationHelpers.RefSpeedKmh) / 100.0 * 0.3 * (car.TotalMass / 1500.0));

        double caster = CalculationHelpers.Clamp(baseByWeight * discMul + speedWeightFactor, c.CasterMin, c.CasterMax);
        r.Caster = Math.Round(caster, 1);
        ex["Caster"] = string.Format(CalculationHelpers.L("Expl_Caster_Fmt"),
            r.Caster,
            r.Caster >= 6.5 ? CalculationHelpers.L("Expl_Caster_High") : CalculationHelpers.L("Expl_Caster_Std"),
            car.TotalMass,
            effectiveMaxKmh);
    }

    private static (double physicsCap, double tuningCap, double totalCap) GetDynamicCamberCaps(CarCard car)
    {
        double massRatio = CalculationHelpers.Clamp(car.TotalMass / 1500.0, 0.7, 1.3);
        double ptw = car.PowerHP / car.TotalMass;
        double ptwRatio = CalculationHelpers.Clamp(ptw / 0.2, 0.7, 1.3);
        double physicsCap = CamberPhysicsLayerCapNominal * massRatio;
        double tuningCap  = CamberTuningLayerCapNominal * ptwRatio;
        double totalCap   = CamberMaxTotalDeviationNominal * Math.Sqrt(massRatio * ptwRatio);
        return (physicsCap, tuningCap, totalCap);
    }

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

    private static double GetCamberCgReference(CarCard car)
    {
        return 350 + CalculationHelpers.Clamp((car.TotalMass - 900) / 1400, 0, 1) * 140;
    }

    private static double GetCamberAeroClassScale(CarCard car)
    {
        return 1.0 / CalculationHelpers.Clamp(car.TotalMass / 1500.0, 0.7, 1.4);
    }

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
        double thermal = tire switch
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
        double wear = tire switch
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
        if (offRoad) { grip *= 0.85; thermal *= 0.5; }
        return (grip, thermal, wear);
    }

    private static double GetGripPowerScale((double grip, double thermal, double wear) grip)
    {
        // Normalize over the actual grip range [0.55, 1.0] so scale spans full [0.5, 1.0]
        const double minGrip = 0.55;
        return 0.5 + (grip.grip - minGrip) / (1.0 - minGrip) * 0.5;
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

    private static (double f, double r) GetCamberTireAdjustment(
        (double grip, double thermal, double wear) grip, Discipline disc)
    {
        bool offRoad = disc is Discipline.Rally or Discipline.CrossCountry;
        double adj = (grip.grip - 0.7) * 0.5 - grip.thermal * 0.08 - grip.wear * 0.05;
        if (offRoad) adj *= 0.5;
        return (adj, adj);
    }

    private static (double f, double r) GetCamberLoadAdjustment(CarCard car, Discipline disc,
        (double grip, double thermal, double wear) grip, double effectiveMaxKmh)
    {
        double wdF = CalculationHelpers.EffectiveWtDist(car) / 100.0;
        double wdDev = wdF - 0.5;

        double camF = -wdDev * CamberWtSensitivity;
        double camR =  wdDev * CamberWtSensitivity;

        double cgH = CalculationHelpers.EstimateCGHeight(car);
        double cgRef = GetCamberCgReference(car);
        double cgFactor = (cgH - cgRef) / cgRef * CamberCgFactorScale;
        camF += cgFactor; camR += cgFactor;

        double speedFactor = CalculationHelpers.Clamp((effectiveMaxKmh - 120.0) / 300.0, 0, 1);
        camF -= speedFactor * CamberSpeedScaleK;
        camR -= speedFactor * CamberSpeedScaleK;

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

    private static (double f, double r) GetCamberAeroAdjustment(CarCard car, TuneResult r)
    {
        double aeroTotal = (car.HasFrontAero ? r.AeroFront : 0) + (car.HasRearAero ? r.AeroRear : 0);
        if (aeroTotal <= 0) return (0, 0);
        double saturation = Math.Tanh(aeroTotal / CamberAeroSaturationRef);
        double classScale = GetCamberAeroClassScale(car);
        double adj = -saturation * CamberAeroMaxAdj * classScale;
        return (adj, adj);
    }

    private static (double f, double r) GetCamberPowerAdjustment(CarCard car, Discipline disc, double gripPowerScale)
    {
        if (disc is Discipline.Drag or Discipline.CrossCountry)
            return (0, 0);

        double pwrExcess = Math.Max(0, car.PowerHP - CalculationHelpers.PowerBaselineHP);
        double trqExcess = Math.Max(0, car.TorqueNm - CalculationHelpers.TorqueBaselineNm);

        double torqueCurveFactor = 1.0;
        if (car.TorquePeakRPM > 0 && car.PowerPeakRPM > 0)
        {
            double spread = (double)(car.PowerPeakRPM - car.TorquePeakRPM) / car.PowerPeakRPM;
            torqueCurveFactor = 1.0 + Math.Max(0, (0.30 - spread)) * 1.2;
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

    private static (double f, double r) GetCamberAWDAdjustment(CarCard car,
        Discipline disc, (double grip, double thermal, double wear) grip)
    {
        if (disc is Discipline.Road or Discipline.Street or Discipline.Touge)
        {
            double ptwFactor = CalculationHelpers.Clamp((car.PowerHP / car.TotalMass) / 0.35, 0, 1);
            double frontAdj = -0.08 - ptwFactor * 0.06;
            double rearAdj  =  0.04 + ptwFactor * 0.04;
            return (frontAdj * grip.grip, rearAdj * grip.grip);
        }
        if (disc is Discipline.Rally or Discipline.CrossCountry)
            return (-0.10, 0.05);
        return (0, 0);
    }

    private static (double f, double r) GetCamberEnginePositionAdjustment(CarCard car)
    {
        return car.EnginePosition switch
        {
            EnginePosition.Front => (-0.12,  0.06),
            EnginePosition.Rear  => ( 0.06, -0.12),
            _                    => ( 0.0,   0.0)
        };
    }

    private static (double f, double r) GetCamberDriftAdjustment(CarCard car,
        (double grip, double thermal, double wear) grip)
    {
        double ptw = car.PowerHP / car.TotalMass;
        double ptwFactor = CalculationHelpers.Clamp((ptw * 1000 - 100) / 300, 0, 1);

        double assistFactor = car.DriveType switch
        {
            DriveType.AWD => 0.6 - CalculationHelpers.Clamp(ptw / 0.5, 0, 0.2),
            DriveType.FWD => 1.1 + CalculationHelpers.Clamp(ptw / 0.5, 0, 0.2),
            _             => 1.0
        };

        double driftF = (-0.5 - ptwFactor * 0.3) * assistFactor;
        double driftR = (-0.15 - ptwFactor * 0.1) * assistFactor;

        double tireFactor = -(grip.grip - 0.5) * 0.3;
        driftF += tireFactor;
        driftR += tireFactor * 0.5;

        return (driftF, driftR);
    }
}
