using System;
using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

public class TuneGeneratorService
{
    public TuneResult Generate(CarCard car, TrackInfo track, SelectedParts parts, Fh6DatabaseService db)
    {
        var r  = new TuneResult { Car = car, Track = track };
        var ex = r.Explanations;

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
        GearingCalculator.CalculateGearing(car, track, parts, db, r, ex, effectiveMaxKmh);
        if (track.Discipline == Discipline.Drag)
            LaunchControlCalculator.CalculateLaunchControl(car, track, parts, db, r);

        GearingCalculator.PostValidateAndRecalculate(car, track, parts, db, r, ex, ref effectiveMaxKmh);

        return r;
    }

    // Backward-compat overload for tests that pass TuningConstraints
    [Obsolete("Use Generate(CarCard, TrackInfo, SelectedParts, Fh6DatabaseService)")]
    public TuneResult Generate(CarCard car, TrackInfo track, TuningConstraints _) =>
        Generate(car, track, new SelectedParts(), Fh6DatabaseService.Instance);
}
