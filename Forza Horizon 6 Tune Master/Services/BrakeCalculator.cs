using System;
using System.Collections.Generic;
using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

internal static class BrakeCalculator
{
    public static void CalculateBrakes(CarCard car, TrackInfo track, TuningConstraints _, TuneResult r, Dictionary<string, string> ex, double effectiveMaxKmh = 250) =>
        CalculateBrakes(car, track, new SelectedParts(), Fh6DatabaseService.Instance, r, ex, effectiveMaxKmh);

    public static void CalculateBrakes(CarCard car, TrackInfo track, SelectedParts parts, Fh6DatabaseService db, TuneResult r, Dictionary<string, string> ex, double effectiveMaxKmh)
    {
        var brakes = TuningPhysicsContext.Brakes(car, parts, db);
        if (brakes == null)
        {
            // Fallback when no brake part is available (synthetic / test cars).
            double fbWtDistFront = CalculationHelpers.EffectiveWtDist(car);
            double fbBias = fbWtDistFront;
            fbBias += car.EnginePosition switch
            {
                EnginePosition.Front => +2.5,
                EnginePosition.Mid   =>  0.0,
                EnginePosition.Rear  => -2.5,
                _                    =>  0.0
            };
            fbBias += car.DriveType switch
            {
                DriveType.FWD => +3.0,
                DriveType.RWD => -3.0,
                _             =>  0.0
            };
            if (track.Discipline == Discipline.Drag)
                fbBias = 50.0;

            double fbMassFactor = Math.Pow(car.TotalMass / 1500.0, 0.55);
            double fbSpeedFactor = 1.0 + Math.Max(0, effectiveMaxKmh - 200.0) / 400.0 * 0.10;
            double fbPressure = 110.0 * fbMassFactor * fbSpeedFactor;

            r.BrakeBalance = Math.Round(CalculationHelpers.Clamp(fbBias, 30.0, 70.0));
            r.BrakePressure = Math.Round(CalculationHelpers.Clamp(fbPressure, 50.0, 200.0));
            ex["Brakes"] = string.Format(CalculationHelpers.L("Expl_Brakes_Fmt"), r.BrakeBalance, r.BrakePressure, CalculationHelpers.L("Expl_BrakesReason_Default"));
            return;
        }

        // Base balance from the part (0..1 => front percentage)
        double bias = brakes.BrakeBiasSlider * 100.0;

        // Shift toward the heavier axle under braking (weight transfer goes forward).
        double wtDistFront = CalculationHelpers.EffectiveWtDist(car);
        double wtShift = (wtDistFront - 50.0) * 0.45;
        bias += wtShift;

        // Engine position shifts static load.
        double cgShift = car.EnginePosition switch
        {
            EnginePosition.Front => +2.5,
            EnginePosition.Mid   => -1.0,
            EnginePosition.Rear  => -2.5,
            _                    => 0.0
        };
        bias += cgShift;

        // Drive type changes available grip front/rear.
        bias += car.DriveType switch
        {
            DriveType.FWD => +3.0,
            DriveType.RWD => -3.0,
            _             => 0.0
        };

        // Brake rotor size ratio: larger front rotors can take more bias.
        double sizeRatio = brakes.FrontBrakeSizeMM > 0 && brakes.RearBrakeSizeMM > 0
            ? brakes.FrontBrakeSizeMM / brakes.RearBrakeSizeMM
            : 1.0;
        bias += (sizeRatio - 1.0) * 3.0;

        // Discipline-specific bias.
        if (track.Discipline == Discipline.Drag)
        {
            bias = 50.0;
        }
        else
        {
            bias += track.Discipline switch
            {
                Discipline.Drift        => -4.0,
                Discipline.Rally        => -1.5,
                Discipline.CrossCountry => -2.5,
                Discipline.Touge        => +1.0,
                Discipline.Street       => +1.0,
                _                       => 0.0
            };
        }

        // Pressure: in-game brake pressure defaults to 100% and a firm race tune sits a bit
        // above that. Base 125 keeps the typical car around 110-130%. Brake friction only
        // mildly reduces the needed pedal effort (sqrt, not full division — full division
        // pushed race brakes well under 100% which is too soft), and heavier cars need more.
        double frictionScale = brakes.GameFrictionScaleBraking > 0.1 ? brakes.GameFrictionScaleBraking : 1.0;
        double massRef = 1500.0;
        double massFactor = Math.Pow(car.TotalMass / massRef, 0.55);

        // Faster cars / longer straights can use a touch more pressure for repeatability.
        double speedFactor = 1.0 + Math.Max(0, effectiveMaxKmh - 200.0) / 400.0 * 0.10;

        // BrakeTorqueSlider is the in-game brake-torque slider; its neutral/default position
        // is 0.5 (= 100% torque), so normalise against 0.5 rather than inverting it directly.
        // A raw 1.0/slider treated 0.5 as a 2x multiplier and inflated every car's pressure
        // toward the 200 cap. Normalised, the common 0.5 value yields a neutral factor of 1.
        double torqueSliderFactor = brakes.BrakeTorqueSlider > 0.01 ? 0.5 / brakes.BrakeTorqueSlider : 1.0;

        double pressure = 125.0 / Math.Sqrt(frictionScale) * massFactor * speedFactor * torqueSliderFactor;

        // Dynamic balance limits from the brake's physical torque capacity. The torque clamps
        // are symmetric (250/250) for almost every car, so a tight +/-2 window around the
        // front-torque share collapsed the whole band to ~48-52% and erased the weight- and
        // drivetrain-based bias above. Use a generous half-width so the band only narrows for
        // genuinely lopsided hardware while leaving the normal 30-70 range intact.
        double balanceMin = 30.0, balanceMax = 70.0;
        if (brakes.FrontBrakeTorqueClamp > 0 && brakes.RearBrakeTorqueClamp > 0)
        {
            double totalClamp = brakes.FrontBrakeTorqueClamp + brakes.RearBrakeTorqueClamp;
            double frontRatio = brakes.FrontBrakeTorqueClamp / totalClamp * 100.0;
            balanceMax = CalculationHelpers.Clamp(frontRatio + 20.0, 30.0, 70.0);
            balanceMin = CalculationHelpers.Clamp(100.0 - frontRatio - 20.0, 30.0, 70.0);
        }

        const double pressureMin = 50.0, pressureMax = 200.0;

        r.BrakeBalance  = Math.Round(CalculationHelpers.Clamp(bias,  balanceMin, balanceMax));
        r.BrakePressure = Math.Round(CalculationHelpers.Clamp(pressure, pressureMin, pressureMax));

        string reason = track.Discipline switch
        {
            Discipline.Drift   => CalculationHelpers.L("Expl_BrakesReason_Drift"),
            Discipline.Drag    => CalculationHelpers.L("Expl_BrakesReason_Drag"),
            _                  => CalculationHelpers.L("Expl_BrakesReason_Default")
        };
        ex["Brakes"] = string.Format(CalculationHelpers.L("Expl_Brakes_Fmt"), r.BrakeBalance, r.BrakePressure, reason);
    }
}
