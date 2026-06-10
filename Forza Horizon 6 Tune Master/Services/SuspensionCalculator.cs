using System;
using System.Collections.Generic;
using System.Diagnostics;
using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

internal static class SuspensionCalculator
{
    public static void CalculateARB(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r, Dictionary<string, string> ex)
    {
        double massScale = Math.Pow(car.TotalMass / CalculationHelpers.RefMassKg, 0.6);

        double wd = CalculationHelpers.EffectiveWtDist(car);
        double wdDev = (wd - 50) / 50.0;

        double wdWeight = (track.Discipline, car.DriveType) switch
        {
            (Discipline.Drag, _)             => 0.0,
            (Discipline.Drift, _)            => 2.0,
            (Discipline.Rally, _)            => 6.0,
            (Discipline.CrossCountry, _)     => 5.0,
            (Discipline.Touge, _)            => 5.0,
            (Discipline.Street, Models.DriveType.FWD) => 5.0,
            (Discipline.Street, _)           => 4.0,
            (_, Models.DriveType.FWD)        => 5.0,
            (_, _)                           => 4.0
        };

        (double baseF, double baseR, string arbNoteKey) = (track.Discipline, car.DriveType) switch
        {
            (Discipline.Drag, _)             => (2.0, 3.0, "Expl_ARBNote_Drag"),
            (Discipline.Drift, Models.DriveType.RWD) => (5.0, 22.0, "Expl_ARBNote_DriftRWD"),
            (Discipline.Drift, _)            => (20.0, 40.0, "Expl_ARBNote_DriftAWD"),
            (Discipline.Rally, _)            => (14.0, 12.0, "Expl_ARBNote_Rally"),
            (Discipline.CrossCountry, _)     => (10.0, 10.0, "Expl_ARBNote_CrossCountry"),
            (Discipline.Touge, Models.DriveType.RWD) => (22.0, 25.0, "Expl_ARBNote_TougeRWD"),
            (Discipline.Touge, Models.DriveType.FWD) => (7.0,  31.0, "Expl_ARBNote_TougeFWD"),
            (Discipline.Touge, _)            => (25.0, 29.0, "Expl_ARBNote_TougeAWD"),
            (Discipline.Street, Models.DriveType.RWD) => (25.0, 22.0, "Expl_ARBNote_StreetRWD"),
            (Discipline.Street, Models.DriveType.FWD) => (9.0,  27.0, "Expl_ARBNote_StreetFWD"),
            (_, Models.DriveType.RWD)        => (28.0, 20.0, "Expl_ARBNote_RoadRWD"),
            (_, Models.DriveType.FWD)        => (12.0, 28.0, "Expl_ARBNote_RoadFWD"),
            (_, _)                           => (26.0, 33.0, "Expl_ARBNote_RoadAWD")
        };
        string note = CalculationHelpers.L(arbNoteKey);

        double avgTrack   = (car.FrontTrack > 0 && car.RearTrack > 0)
            ? (car.FrontTrack + car.RearTrack) / 2.0
            : Math.Max(car.FrontTrack, car.RearTrack);
        double trackFactor = avgTrack > 0 ? CalculationHelpers.RefFrontTrackMm / avgTrack : 1.0;
        double arbF = baseF * massScale * trackFactor;
        double arbR = baseR * massScale * trackFactor;

        arbF += wdDev * wdWeight;
        arbR -= wdDev * wdWeight;

        if (track.Discipline != Discipline.Drag)
        {
            double cgH = CalculationHelpers.EstimateCGHeight(car);
            double rollAdj = CalculationHelpers.Clamp((cgH - 420.0) / 420.0 * 6.0, -3.0, 8.0);
            arbF += rollAdj;
            arbR += rollAdj;
        }

        double seasonFactorArb = CalculationHelpers.GetSeasonGripFactor(track.Season);
        double arbSeasonMul = 1.0 - (1.0 - seasonFactorArb) * 0.65;
        arbF *= arbSeasonMul;
        arbR *= arbSeasonMul;

        bool hasFront = car.HasFrontARB;
        bool hasRear  = car.HasRearARB;
        if (hasFront)
            r.ARBFront = Math.Round(CalculationHelpers.Clamp(arbF, c.ARBFrontMin, c.ARBFrontMax), 1);
        if (hasRear)
            r.ARBRear  = Math.Round(CalculationHelpers.Clamp(arbR, c.ARBRearMin,  c.ARBRearMax), 1);
        if (hasFront || hasRear)
            ex["ARB"] = string.Format(CalculationHelpers.L("Expl_ARB_Fmt"), r.ARBFront, r.ARBRear, note);
    }

    public static void CalculateSprings(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r, Dictionary<string, string> ex)
    {
        if (!car.SuspensionAllowsAdvancedTuning)
        {
            ex["Springs"] = CalculationHelpers.L("Expl_Springs_Disabled");
            return;
        }

        double wdF = CalculationHelpers.EffectiveWtDist(car) / 100.0;
        double wdR = 1.0 - wdF;

        (double hzF, double hzR) = track.Discipline switch
        {
            Discipline.Drag         => (2.0, 1.0),
            Discipline.Drift        => (1.8, 2.2),
            Discipline.Rally        => (2.0, 2.2),
            Discipline.CrossCountry => (1.3, 1.5),
            Discipline.Touge        => (2.3, 2.2),
            Discipline.Street       => (2.5, 2.4),
            _                       => (2.5, 2.6)
        };

        if (car.DriveType == Models.DriveType.RWD) { hzR += 0.15; hzF -= 0.05; }
        if (car.DriveType == Models.DriveType.FWD) { hzF += 0.15; hzR -= 0.05; }

        double pwrHz    = Math.Max(0, (car.PowerHP - CalculationHelpers.PowerBaselineHP) / 300.0 * 0.25);
        double torqueHz = Math.Min(0.4, Math.Max(0, (car.TorqueNm - CalculationHelpers.TorqueBaselineNm) / 600.0 * 0.25));
        double squat    = Math.Min(0.40, pwrHz + torqueHz);
        if (car.DriveType == Models.DriveType.FWD)
            hzF += squat;
        else
            hzR += squat;

        const double CorrectSpringHzToNmm = 0.039478; 

        double sprF = CorrectSpringHzToNmm * hzF * hzF * car.TotalMass * wdF;
        double sprR = CorrectSpringHzToNmm * hzR * hzR * car.TotalMass * wdR;

        double cgH_spr    = CalculationHelpers.EstimateCGHeight(car);
        double avgProfile_spr = (car.FrontTireProfile + car.RearTireProfile) / 2.0;
        cgH_spr = Math.Max(250.0, cgH_spr - (45.0 - avgProfile_spr) * 0.5);
        double cgFactor   = CalculationHelpers.Clamp(1.0 + (cgH_spr - 420.0) / 700.0 * 0.22, 0.90, 1.25);
        double avgTrack_s = car.FrontTrack > 0 && car.RearTrack > 0
            ? (car.FrontTrack + car.RearTrack) / 2.0 : 1600.0;
        double trackRollF = Math.Pow(Math.Max(1100.0, avgTrack_s) / 1600.0, -0.18);
        sprF *= cgFactor * trackRollF;
        sprR *= cgFactor * trackRollF;

        bool offRoadDisc = track.Discipline is Discipline.Rally or Discipline.CrossCountry;
        double suspMul = offRoadDisc
            ? car.SuspensionUpgrade switch
            {
                SuspensionUpgrade.Race    => 1.10,
                SuspensionUpgrade.Sport   => 1.00,
                SuspensionUpgrade.Street  => 0.88,
                SuspensionUpgrade.Rally   => 0.85,
                SuspensionUpgrade.Drift   => 0.85,
                SuspensionUpgrade.Offroad => 0.80,
                _                         => 0.72
            }
            : car.SuspensionUpgrade switch
            {
                SuspensionUpgrade.Race    => 1.08,
                SuspensionUpgrade.Sport   => 1.04,
                SuspensionUpgrade.Street  => 0.96,
                SuspensionUpgrade.Rally   => 0.92,
                SuspensionUpgrade.Drift   => 0.90,
                SuspensionUpgrade.Offroad => 0.85,
                _                         => 0.85
            };
        sprF *= suspMul;
        sprR *= suspMul;

        if (car.PowertrainType == PowertrainType.Hybrid)
        { sprF *= 1.05; sprR *= 1.05; }
        double aspSpring = CalculationHelpers.GetPowerDeliveryFactors(car.PowertrainType, car.AspirationType, car.AntiLag).Spring;
        if (car.DriveType == Models.DriveType.FWD)
            sprF *= aspSpring;
        else
            sprR *= aspSpring;

        double seasonFactorSpr = CalculationHelpers.GetSeasonGripFactor(track.Season);
        double springSeasonMul = 0.65 + seasonFactorSpr * 0.35;
        sprF *= springSeasonMul;
        sprR *= springSeasonMul;

        r.SpringFront = Math.Round(CalculationHelpers.Clamp(sprF, c.SpringFrontMin, c.SpringFrontMax), 1);
        r.SpringRear  = Math.Round(CalculationHelpers.Clamp(sprR, c.SpringRearMin,  c.SpringRearMax),  1);
        ex["Springs"] = string.Format(CalculationHelpers.L("Expl_Springs_Fmt"), r.SpringFront, r.SpringRear, hzF, hzR, CalculationHelpers.L($"Enum_SuspensionUpgrade_{car.SuspensionUpgrade}"));
    }

    public static void CalculateRideHeight(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r, Dictionary<string, string> ex)
    {
        if (!car.SuspensionAllowsAdvancedTuning)
        {
            ex["RideHeight"] = CalculationHelpers.L("Expl_RideHeight_Disabled");
            return;
        }

        double rangeF = c.RideHeightFrontMax - c.RideHeightFrontMin;
        double rangeR = c.RideHeightRearMax - c.RideHeightRearMin;
        if (rangeF <= 0 || rangeR <= 0)
        {
            r.RideHeightFront = c.RideHeightFrontMin;
            r.RideHeightRear = c.RideHeightRearMin;
            ex["RideHeight"] = string.Format(CalculationHelpers.L("Expl_RideHeight_Fmt"), r.RideHeightFront, r.RideHeightRear, "-");
            return;
        }

        (double baseFFactor, double baseRFactor, string note) = track.Discipline switch
        {
            Discipline.Drag         => (0.05, 0.80, CalculationHelpers.L("Expl_RideHeightNote_Drag")),
            Discipline.Drift        => (0.15, 0.19, CalculationHelpers.L("Expl_RideHeightNote_Drift")),
            Discipline.Rally        => (0.40, 0.45, CalculationHelpers.L("Expl_RideHeightNote_Rally")),
            Discipline.CrossCountry => (0.60, 0.65, CalculationHelpers.L("Expl_RideHeightNote_CrossCountry")),
            _                       => (0.22, 0.28, CalculationHelpers.L("Expl_RideHeightNote_Road"))
        };

        double rhFFactor = baseFFactor;
        double rhRFactor = baseRFactor;

        double suspOffFrac = car.SuspensionUpgrade switch
        {
            SuspensionUpgrade.Race    => -0.12,
            SuspensionUpgrade.Sport   =>  0.00,
            SuspensionUpgrade.Street  =>  0.08,
            SuspensionUpgrade.Rally   =>  0.30,
            SuspensionUpgrade.Drift   => -0.10,
            SuspensionUpgrade.Offroad =>  0.40,
            _                         =>  0.00
        };
        rhFFactor += suspOffFrac;
        rhRFactor += suspOffFrac;

        double rake = car.EnginePosition switch { EnginePosition.Front => 2, EnginePosition.Rear => -1, _ => 0 };
        rhFFactor -= rake * 0.2 / rangeF;
        rhRFactor += rake * 0.2 / rangeR;

        double avgRim = (car.FrontRimDiameter + car.RearRimDiameter) / 2.0;
        double rimAdj = (avgRim - CalculationHelpers.RefRimDiameterInch) * 0.8;
        rhFFactor += rimAdj / rangeF;
        rhRFactor += rimAdj / rangeR;

        double avgProfile = (car.FrontTireProfile + car.RearTireProfile) / 2.0;
        double profileAdj = (CalculationHelpers.ProfileBaseline - avgProfile) * 0.3;
        rhFFactor += profileAdj / rangeF;
        rhRFactor += profileAdj / rangeR;

        rhFFactor = CalculationHelpers.Clamp(rhFFactor, 0.0, 1.0);
        rhRFactor = CalculationHelpers.Clamp(rhRFactor, 0.0, 1.0);

        double rhF = c.RideHeightFrontMin + rangeF * rhFFactor;
        double rhR = c.RideHeightRearMin + rangeR * rhRFactor;

        r.RideHeightFront = Math.Round(rhF, 1);
        r.RideHeightRear  = Math.Round(rhR, 1);
        ex["RideHeight"] = string.Format(CalculationHelpers.L("Expl_RideHeight_Fmt"), r.RideHeightFront, r.RideHeightRear, note);
    }

    public static void CalculateDampers(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r, Dictionary<string, string> ex)
    {
        double sprF = r.SpringFront > 0 ? r.SpringFront : 100.0;
        double sprR = r.SpringRear > 0 ? r.SpringRear : 100.0;

        double wdF = CalculationHelpers.EffectiveWtDist(car) / 100.0;
        double massF = car.TotalMass * wdF;
        double massR = car.TotalMass * (1.0 - wdF);

        double rebF = 6.0 + Math.Sqrt(sprF * massF) / 45.0;
        double rebR = 6.0 + Math.Sqrt(sprR * massR) / 45.0;

        double discRebMul = track.Discipline switch
        {
            Discipline.Drag => 0.75,
            Discipline.Drift => 0.65,
            Discipline.Rally => 1.15,
            Discipline.CrossCountry => 1.20,
            Discipline.Touge => 1.10,
            Discipline.Street => 0.95,
            _ => 1.0
        };
        rebF *= discRebMul;
        rebR *= discRebMul;

        double powerReb  = Math.Max(0, (car.PowerHP  - CalculationHelpers.PowerBaselineHP)  / CalculationHelpers.PowerStepHP * 0.5);
        double torqueReb = Math.Max(0, (car.TorqueNm - CalculationHelpers.TorqueBaselineNm) / 500.0       * 0.5);
        double squatReb  = Math.Min(3.0, powerReb + torqueReb);
        if (car.DriveType == Models.DriveType.FWD) rebF += squatReb;
        else                                        rebR += squatReb;

        if (car.DriveType == Models.DriveType.RWD) rebR += 0.5;

        double bumpRatio = (track.Discipline, car.SuspensionUpgrade) switch
        {
            (Discipline.Rally or Discipline.CrossCountry, _) => 0.65,
            (Discipline.Drag, _) => 0.45,
            (Discipline.Drift, _) => 0.50,
            (_, SuspensionUpgrade.Race) => 0.60,
            (_, SuspensionUpgrade.Sport) => 0.55,
            _ => 0.50
        };

        double bmpF = rebF * bumpRatio;
        double bmpR = rebR * bumpRatio;

        double seasonFactorDmp = CalculationHelpers.GetSeasonGripFactor(track.Season);
        double dampSeasonMul = 1.0 - (1.0 - seasonFactorDmp) * 0.25;
        rebF *= dampSeasonMul; rebR *= dampSeasonMul;
        bmpF *= dampSeasonMul; bmpR *= dampSeasonMul;

        double aspDamper = CalculationHelpers.GetPowerDeliveryFactors(car.PowertrainType, car.AspirationType, car.AntiLag).Damper;
        if (car.DriveType == Models.DriveType.FWD)
        { rebF *= aspDamper; bmpF *= aspDamper; }
        else
        { rebR *= aspDamper; bmpR *= aspDamper; }

        r.ReboundFront = Math.Round(CalculationHelpers.Clamp(rebF, c.ReboundFrontMin, c.ReboundFrontMax), 1);
        r.ReboundRear  = Math.Round(CalculationHelpers.Clamp(rebR, c.ReboundRearMin,  c.ReboundRearMax), 1);
        r.BumpFront    = Math.Round(CalculationHelpers.Clamp(bmpF, c.BumpFrontMin,    c.BumpFrontMax), 1);
        r.BumpRear     = Math.Round(CalculationHelpers.Clamp(bmpR, c.BumpRearMin,     c.BumpRearMax), 1);
        
        ex["Dampers"] = string.Format(CalculationHelpers.L("Expl_Dampers_Fmt"),
            r.ReboundFront, r.ReboundRear, r.BumpFront, r.BumpRear,
            rebF > 0 ? bmpF / rebF * 100 : 0,
            rebR > 0 ? bmpR / rebR * 100 : 0);
    }
}