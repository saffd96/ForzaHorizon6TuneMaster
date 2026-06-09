using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

public class TuneGeneratorService
{
    public TuneResult Generate(CarCard car, TrackInfo track, TuningConstraints c)
    {
        var r  = new TuneResult { Car = car, Track = track };
        var ex = r.Explanations;

        AeroCalculator.CalculateAero(car, track, c, r, ex);
        double effectiveMaxKmh = CalculationHelpers.ComputeEffectiveMaxSpeedKmh(car, r);
        AeroCalculator.CalculateAero(car, track, c, r, ex, effectiveMaxKmh);
        double prevMaxKmh = effectiveMaxKmh;
        effectiveMaxKmh = CalculationHelpers.ComputeEffectiveMaxSpeedKmh(car, r);
        if (Math.Abs(effectiveMaxKmh - prevMaxKmh) > 1)
        {
            AeroCalculator.CalculateAero(car, track, c, r, ex, effectiveMaxKmh);
            effectiveMaxKmh = CalculationHelpers.ComputeEffectiveMaxSpeedKmh(car, r);
        }

        TireCalculator.CalculateTirePressure(car, track, c, r, ex);
        AlignmentCalculator.CalculateCamber(car, track, c, r, ex, effectiveMaxKmh);
        AlignmentCalculator.CalculateToe(car, track, c, r, ex, effectiveMaxKmh);
        AlignmentCalculator.CalculateCaster(car, track, c, r, ex, effectiveMaxKmh);
        SuspensionCalculator.CalculateARB(car, track, c, r, ex);
        SuspensionCalculator.CalculateSprings(car, track, c, r, ex);
        SuspensionCalculator.CalculateRideHeight(car, track, c, r, ex);
        SuspensionCalculator.CalculateDampers(car, track, c, r, ex);
        DifferentialCalculator.CalculateDifferential(car, track, c, r, ex);
        BrakeCalculator.CalculateBrakes(car, track, c, r, ex, effectiveMaxKmh);
        GearingCalculator.CalculateGearing(car, track, c, r, ex, effectiveMaxKmh);
        if (track.Discipline == Discipline.Drag)
            LaunchControlCalculator.CalculateLaunchControl(car, r);

        GearingCalculator.PostValidateAndRecalculate(car, track, c, r, ex, ref effectiveMaxKmh);

        return r;
    }
}
