using System;
using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

public class TuneGeneratorService
{
    public TuneResult Generate(CarCard car, TrackInfo track, SelectedParts parts, Fh6DatabaseService db,
        TuningConstraints? constraints = null)
    {
        var r  = new TuneResult { Car = car, Track = track };
        var ex = r.Explanations;
        var c  = constraints ?? new TuningConstraints();

        // Refresh power/torque/RPM to reflect currently selected engine parts.
        PowerCalculator.Calculate(car, parts);
        double inertiaFactor = car.RotationalInertiaFactor;
        if (Math.Abs(inertiaFactor - 1.0) > 0.005)
            ex["Inertia"] = string.Format(CalculationHelpers.L("Expl_Inertia"), inertiaFactor);

        AeroCalculator.CalculateAero(car, track, parts, db, r, ex);
        double effectiveMaxKmh = CalculationHelpers.ComputeEffectiveMaxSpeedKmh(car, r);
        AeroCalculator.CalculateAero(car, track, parts, db, r, ex, effectiveMaxKmh);
        double prevMaxKmh = effectiveMaxKmh;
        effectiveMaxKmh = CalculationHelpers.ComputeEffectiveMaxSpeedKmh(car, r);
        if (Math.Abs(effectiveMaxKmh - prevMaxKmh) > 1)
        {
            AeroCalculator.CalculateAero(car, track, parts, db, r, ex, effectiveMaxKmh);
            effectiveMaxKmh = CalculationHelpers.ComputeEffectiveMaxSpeedKmh(car, r);
        }

        TireCalculator.CalculateTirePressure(car, track, parts, db, r, ex);
        AlignmentCalculator.CalculateCamber(car, track, parts, r, ex, effectiveMaxKmh);
        AlignmentCalculator.CalculateToe(car, track, parts, r, ex, effectiveMaxKmh);
        AlignmentCalculator.CalculateCaster(car, track, parts, r, ex, effectiveMaxKmh);
        SuspensionCalculator.CalculateARB(car, track, parts, r, ex);
        SuspensionCalculator.CalculateSprings(car, track, parts, r, ex);
        SuspensionCalculator.CalculateRideHeight(car, track, parts, r, ex);
        SuspensionCalculator.CalculateDampers(car, track, parts, r, ex);
        BrakeCalculator.CalculateBrakes(car, track, parts, db, r, ex, effectiveMaxKmh);
        DifferentialCalculator.CalculateDifferential(car, track, parts, db, r, ex);

        if (track.Discipline == Discipline.Drag)
        {
            double firstGear = GearingCalculator.CalcDragInitialFirstGear(car, parts, db);
            for (int iter = 0; iter < 3; iter++)
            {
                GearingCalculator.CalculateGearing(car, track, parts, db, r, ex, effectiveMaxKmh, c, firstGear);
                LaunchControlCalculator.CalculateLaunchControl(car, track, parts, db, r);

                if (!r.LaunchControlRpm.HasValue || r.GearRatios.Count == 0)
                    break;

                double launchRpm = r.LaunchControlRpm.Value;
                double stallFloor = LaunchControlCalculator.GetStallFloor(car, parts, db);
                double ceiling = car.MaxRPM * 0.80;
                double optimal = stallFloor + (ceiling - stallFloor) * 0.45;

                double ratio = optimal / launchRpm;
                if (Math.Abs(ratio - 1.0) < 0.04)
                    break;

                double newFirst = CalculationHelpers.Clamp(firstGear / ratio, 1.5, 5.5);
                if (Math.Abs(newFirst - firstGear) < 0.05)
                    break;
                firstGear = newFirst;
            }
        }
        else
        {
            GearingCalculator.CalculateGearing(car, track, parts, db, r, ex, effectiveMaxKmh, c);
        }

        GearingCalculator.PostValidateAndRecalculate(car, track, parts, db, r, ex, ref effectiveMaxKmh, c);

        return r;
    }

    // Backward-compat overload for tests that pass TuningConstraints — now forwards them so the
    // final-drive bounds are actually honoured.
    [Obsolete("Use Generate(CarCard, TrackInfo, SelectedParts, Fh6DatabaseService, TuningConstraints)")]
    public TuneResult Generate(CarCard car, TrackInfo track, TuningConstraints constraints) =>
        Generate(car, track, new SelectedParts(), Fh6DatabaseService.Instance, constraints);
}
