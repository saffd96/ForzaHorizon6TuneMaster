using System;
using System.Collections.Generic;
using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

internal static class AlignmentCalculator
{
    private const double CamberMin = -5.0, CamberMax = 0.0;
    private const double ToeMin = -1.0, ToeMax = 1.0;
    private const double CasterMin = 3.0, CasterMax = 10.0;

    public static void CalculateCamber(CarCard car, TrackInfo track, TuningConstraints _, TuneResult r,
        Dictionary<string, string> ex, double effectiveMaxKmh) =>
        CalculateCamber(car, track, new SelectedParts(), Fh6DatabaseService.Instance, r, ex, effectiveMaxKmh);

    public static void CalculateCamber(CarCard car, TrackInfo track, SelectedParts parts, TuneResult r,
        Dictionary<string, string> ex, double effectiveMaxKmh) =>
        CalculateCamber(car, track, parts, Fh6DatabaseService.Instance, r, ex, effectiveMaxKmh);

    public static void CalculateCamber(CarCard car, TrackInfo track, SelectedParts parts, Fh6DatabaseService db, TuneResult r,
        Dictionary<string, string> ex, double effectiveMaxKmh)
    {
        DbSpringDamperPhysics? frontPhys = TuningPhysicsContext.FrontSpringDamper(car, parts, db);
        DbSpringDamperPhysics? rearPhys  = TuningPhysicsContext.RearSpringDamper(car, parts, db);

        if (!car.SuspensionAllowsAdvancedTuning)
        {
            r.CamberFront = 0;
            r.CamberRear  = 0;
            ex["Camber"] = CalculationHelpers.L("Expl_Camber_Disabled");
            return;
        }

        string reason = CalculationHelpers.L(GetCamberReasonKey(track.Discipline));
        (double baseF, double baseR) = CamberBaseOffsets(track.Discipline, car.DriveType);

        double staticF = frontPhys?.StaticCamber ?? 0.0;
        double staticR = rearPhys?.StaticCamber  ?? 0.0;

        // Aero downforce pushes the tyre outward -> a touch more negative camber.
        double aeroTotal = (car.HasFrontAero ? r.AeroFront : 0) + (car.HasRearAero ? r.AeroRear : 0);
        double aeroAdj = -Math.Tanh(aeroTotal / 250.0) * 0.25;

        // Power: add negative camber to the driven axle.
        double ptw = car.PowerHP / Math.Max(car.TotalMass, 1.0);
        double powerAdj = -Math.Min(0.6, Math.Max(0, ptw - 0.25) * 0.4);

        double camF = staticF + baseF + aeroAdj + (car.DriveType == DriveType.FWD ? powerAdj : car.DriveType == DriveType.AWD ? powerAdj * 0.5 : 0.0);
        double camR = staticR + baseR + aeroAdj + (car.DriveType == DriveType.RWD ? powerAdj : car.DriveType == DriveType.AWD ? powerAdj * 0.5 : 0.0);

        // Engine position adds a static load bias.
        double engOffsetF = car.EnginePosition switch
        {
            EnginePosition.Front => -0.20,
            EnginePosition.Rear  => +0.15,
            _                    =>  0.00
        };
        camF += engOffsetF;
        camR -= engOffsetF * 0.5;

        // Weight distribution: heavier axle needs slightly more negative camber.
        double wd = CalculationHelpers.EffectiveWtDist(car) / 100.0 - 0.5;
        camF += wd * 0.20;
        camR -= wd * 0.20;

        r.CamberFront = Math.Round(CalculationHelpers.Clamp(camF, CamberMin, CamberMax), 1);
        r.CamberRear  = Math.Round(CalculationHelpers.Clamp(camR, CamberMin, CamberMax), 1);
        ex["Camber"] = string.Format(CalculationHelpers.L("Expl_Camber_Fmt"), r.CamberFront, r.CamberRear, reason);
    }

    public static void CalculateToe(CarCard car, TrackInfo track, TuningConstraints _, TuneResult r, Dictionary<string, string> ex, double effectiveMaxKmh) =>
        CalculateToe(car, track, new SelectedParts(), Fh6DatabaseService.Instance, r, ex, effectiveMaxKmh);

    public static void CalculateToe(CarCard car, TrackInfo track, SelectedParts parts, TuneResult r, Dictionary<string, string> ex, double effectiveMaxKmh) =>
        CalculateToe(car, track, parts, Fh6DatabaseService.Instance, r, ex, effectiveMaxKmh);

    public static void CalculateToe(CarCard car, TrackInfo track, SelectedParts parts, Fh6DatabaseService db, TuneResult r, Dictionary<string, string> ex, double effectiveMaxKmh)
    {
        var (frontPhys, rearPhys) = (
            TuningPhysicsContext.FrontSpringDamper(car, parts, db),
            TuningPhysicsContext.RearSpringDamper(car, parts, db));

        double staticF = frontPhys?.StaticToe ?? 0.0;
        double staticR = rearPhys?.StaticToe  ?? 0.0;

        (double baseF, double baseR) = track.Discipline switch
        {
            Discipline.Drag         => (0.00,  0.00),
            Discipline.Drift        => (-0.80, 0.20),
            Discipline.Rally        => (-0.25, 0.15),
            Discipline.CrossCountry => (-0.15, 0.20),
            Discipline.Touge        => (-0.15, 0.12),
            Discipline.Street       => (-0.12, 0.12),
            _                       => (-0.10, 0.10)
        };

        // Drive-type bias: RWD likes a bit of rear toe-in for stability, FWD a bit more front toe-out.
        if (car.DriveType == DriveType.RWD) { baseR += 0.05; baseF -= 0.02; }
        if (car.DriveType == DriveType.FWD) { baseF -= 0.05; baseR += 0.02; }

        double toeF = staticF + baseF;
        double toeR = staticR + baseR;

        // High-speed cars need less aggressive toe to avoid instability.
        double speedFactor = CalculationHelpers.Clamp((effectiveMaxKmh - 120.0) / 250.0, 0, 1);
        toeF *= (1.0 - speedFactor * 0.25);
        toeR *= (1.0 - speedFactor * 0.15);

        r.ToeFront = Math.Round(CalculationHelpers.Clamp(toeF, ToeMin, ToeMax), 2);
        r.ToeRear  = Math.Round(CalculationHelpers.Clamp(toeR, ToeMin, ToeMax), 2);

        string fd = r.ToeFront < 0 ? CalculationHelpers.L("Expl_Toe_Out") : r.ToeFront > 0 ? CalculationHelpers.L("Expl_Toe_In") : CalculationHelpers.L("Expl_Toe_Zero");
        string rd = r.ToeRear  > 0 ? CalculationHelpers.L("Expl_Toe_In")   : r.ToeRear  < 0 ? CalculationHelpers.L("Expl_Toe_Out") : CalculationHelpers.L("Expl_Toe_Zero");
        ex["Toe"] = string.Format(CalculationHelpers.L("Expl_Toe_Fmt"), r.ToeFront, fd, r.ToeRear, rd);
    }

    public static void CalculateCaster(CarCard car, TrackInfo track, TuningConstraints _, TuneResult r, Dictionary<string, string> ex, double effectiveMaxKmh) =>
        CalculateCaster(car, track, new SelectedParts(), Fh6DatabaseService.Instance, r, ex, effectiveMaxKmh);

    public static void CalculateCaster(CarCard car, TrackInfo track, SelectedParts parts, TuneResult r, Dictionary<string, string> ex, double effectiveMaxKmh) =>
        CalculateCaster(car, track, parts, Fh6DatabaseService.Instance, r, ex, effectiveMaxKmh);

    public static void CalculateCaster(CarCard car, TrackInfo track, SelectedParts parts, Fh6DatabaseService db, TuneResult r, Dictionary<string, string> ex, double effectiveMaxKmh)
    {
        var frontPhys = TuningPhysicsContext.FrontSpringDamper(car, parts, db);
        double caster = frontPhys?.Caster ?? 6.0;

        // Heavier cars benefit from a little more caster for self-centering.
        caster += (car.TotalMass - 1500.0) / 1000.0 * 0.3;

        // Discipline tuning.
        caster += track.Discipline switch
        {
            Discipline.Drag         => -0.3,
            Discipline.Drift        => +0.8,
            Discipline.Rally        => -0.4,
            Discipline.CrossCountry => -0.5,
            Discipline.Touge        => +0.4,
            _                       => 0.0
        };

        // High-speed builds need a touch more caster for straight-line stability.
        caster += CalculationHelpers.Clamp((effectiveMaxKmh - 200.0) / 200.0, 0, 1) * 0.4;

        r.Caster = Math.Round(CalculationHelpers.Clamp(caster, CasterMin, CasterMax), 1);
        ex["Caster"] = string.Format(CalculationHelpers.L("Expl_Caster_Fmt"),
            r.Caster,
            r.Caster >= 6.5 ? CalculationHelpers.L("Expl_Caster_High") : CalculationHelpers.L("Expl_Caster_Std"),
            car.TotalMass,
            effectiveMaxKmh);
    }

    private static (double f, double r) CamberBaseOffsets(Discipline d, DriveType drive) => d switch
    {
        Discipline.Drag         => (-0.2,  0.0),
        Discipline.Drift        => (-2.5, -0.8),
        Discipline.Rally        => (-0.8, -0.5),
        Discipline.CrossCountry => (-0.4, -0.4),
        Discipline.Touge        => (-1.8, -0.8),
        Discipline.Street       => drive == DriveType.FWD ? (-1.4, -0.6) : (-1.2, -0.9),
        _                       => drive == DriveType.FWD ? (-1.4, -0.6) : (-1.2, -0.9)
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
}
