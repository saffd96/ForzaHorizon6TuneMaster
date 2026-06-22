using System;
using System.Collections.Generic;
using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

internal static class AeroCalculator
{
    public static void CalculateAero(CarCard car, TrackInfo track, TuningConstraints _, TuneResult r, Dictionary<string, string> ex, double? overrideSpeedKmh = null) =>
        CalculateAero(car, track, new SelectedParts(), Fh6DatabaseService.Instance, r, ex, overrideSpeedKmh);

    public static void CalculateAero(CarCard car, TrackInfo track, SelectedParts parts, Fh6DatabaseService db, TuneResult r, Dictionary<string, string> ex, double? overrideSpeedKmh = null)
    {
        var rearWing  = TuningPhysicsContext.RearWingAero(car, parts, db);
        var frontWing = TuningPhysicsContext.FrontBumperAero(car, parts, db);
        var dbCar = db.GetCar(car.CarDbId);

        // Front and rear are independent aero devices: a car can carry a front splitter with
        // no rear wing (or vice-versa). Only bail when NEITHER axle has a usable aero profile;
        // otherwise compute each axle on its own. (Previously the whole calc short-circuited
        // whenever there was no rear wing, silently zeroing a fitted front splitter.)
        bool hasRear  = car.HasRearAero  && rearWing  != null;
        bool hasFront = car.HasFrontAero && frontWing != null;

        if (!hasRear && !hasFront)
        {
            r.AeroFront = 0;
            r.AeroRear = 0;
            ex["Aero"] = CalculationHelpers.L("Expl_Aero_None");
            return;
        }

        double aeroScale = dbCar?.GameDownforceScale > 0 ? dbCar.GameDownforceScale : 1.0;
        double rearClampKg  = dbCar?.RearDownforceClampKG ?? 0.0;
        double frontClampKg = dbCar?.FrontDownforceClampKG ?? 0.0;

        (double rearFraction, double frontFraction, string noteKey) = AeroFractions(track.Discipline, car.DriveType);

        double ptw = car.PowerHP / Math.Max(car.TotalMass, 1.0);
        double ptwRef = 0.25;
        double powerFactor = 1.0 - CalculationHelpers.Clamp((ptw - ptwRef) / ptwRef, -0.5, 0.5) * 0.20;
        rearFraction *= powerFactor;
        frontFraction *= powerFactor;

        // Rear axle — from the rear wing's own downforce range, capped by the body's rear clamp.
        if (hasRear)
        {
            double rearMin = rearWing!.Downforce0 * aeroScale;
            double rearMax = rearWing.Downforce1 * aeroScale;
            if (rearClampKg > 0 && rearMax > rearClampKg) rearMax = rearClampKg;
            if (rearMax < rearMin) rearMax = rearMin;
            double rearAero = rearMin + (rearMax - rearMin) * CalculationHelpers.Clamp(rearFraction, 0.0, 1.0);
            r.AeroRear = Math.Round(CalculationHelpers.Clamp(rearAero, rearMin, rearMax));
        }
        else r.AeroRear = 0;

        // Front axle — from the front bumper's OWN aero profile (not tied to the rear value),
        // capped by the body's front clamp.
        if (hasFront)
        {
            double frontMin = frontWing!.Downforce0 * aeroScale;
            double frontMax = frontWing.Downforce1 * aeroScale;
            if (frontClampKg > 0 && frontMax > frontClampKg) frontMax = frontClampKg;
            if (frontMax < frontMin) frontMax = frontMin;
            double frontAero = frontMin + (frontMax - frontMin) * CalculationHelpers.Clamp(frontFraction, 0.0, 1.0);
            r.AeroFront = Math.Round(CalculationHelpers.Clamp(frontAero, frontMin, frontMax));
        }
        else r.AeroFront = 0;

        double speed = overrideSpeedKmh ?? car.MaxSpeedKmh;
        ex["Aero"] = string.Format(CalculationHelpers.L("Expl_Aero_Fmt"), r.AeroFront, r.AeroRear, speed, car.PowerHP);
    }

    private static (double rearFraction, double frontFraction, string noteKey) AeroFractions(Discipline d, DriveType drive) => d switch
    {
        Discipline.Drag         => (0.10, 0.00, "Expl_AeroNote_Drag"),
        Discipline.Drift        => (0.15, 0.05, "Expl_AeroNote_Drift"),
        Discipline.Rally        => (0.25, 0.10, "Expl_AeroNote_Rally"),
        Discipline.CrossCountry => (0.15, 0.05, "Expl_AeroNote_CrossCountry"),
        Discipline.Touge        => (0.90, 0.70, "Expl_AeroNote_Touge"),
        Discipline.Street       => (0.70, 0.50, "Expl_AeroNote_Street"),
        _ => drive switch
        {
            DriveType.RWD => (0.80, 0.45, "Expl_AeroNote_RoadRWD"),
            DriveType.FWD => (0.55, 0.70, "Expl_AeroNote_RoadFWD"),
            _             => (0.70, 0.60, "Expl_AeroNote_RoadAWD")
        }
    };
}
