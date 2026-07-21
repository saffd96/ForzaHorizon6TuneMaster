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

        AeroCalculator.CalculateAero(car, track, parts, db, r, ex, null, c);
        double effectiveMaxKmh = CalculationHelpers.ComputeEffectiveMaxSpeedKmh(car, r);
        AeroCalculator.CalculateAero(car, track, parts, db, r, ex, effectiveMaxKmh, c);
        double prevMaxKmh = effectiveMaxKmh;
        effectiveMaxKmh = CalculationHelpers.ComputeEffectiveMaxSpeedKmh(car, r);
        if (Math.Abs(effectiveMaxKmh - prevMaxKmh) > 1)
        {
            AeroCalculator.CalculateAero(car, track, parts, db, r, ex, effectiveMaxKmh, c);
            effectiveMaxKmh = CalculationHelpers.ComputeEffectiveMaxSpeedKmh(car, r);
        }

        TireCalculator.CalculateTirePressure(car, track, parts, db, r, ex, c);
        AlignmentCalculator.CalculateCamber(car, track, parts, db, r, ex, effectiveMaxKmh, c);
        AlignmentCalculator.CalculateToe(car, track, parts, db, r, ex, effectiveMaxKmh, c);
        AlignmentCalculator.CalculateCaster(car, track, parts, db, r, ex, effectiveMaxKmh, c);
        SuspensionCalculator.CalculateARB(car, track, parts, db, r, ex, c);
        SuspensionCalculator.CalculateSprings(car, track, parts, db, r, ex, c);
        SuspensionCalculator.CalculateRideHeight(car, track, parts, db, r, ex, c);
        SuspensionCalculator.CalculateDampers(car, track, parts, db, r, ex, c);
        BrakeCalculator.CalculateBrakes(car, track, parts, db, r, ex, effectiveMaxKmh, c);
        DifferentialCalculator.CalculateDifferential(car, track, parts, db, r, ex, c);

        if (track.Discipline == Discipline.Drag)
        {
            // This loop nudges the first-gear anchor to pull LaunchControlRpm toward an
            // "optimal" launch band (45% between the stall floor and 80% of MaxRPM). It works
            // when the anchor actually moves that RPM — but CalculateGearing's own final-drive
            // fit re-solves FinalDrive from scratch every call to keep the SAME top gear hitting
            // the SAME target top speed, so for most cars FD moves opposite to whatever
            // firstGear is fed in and effFirst (= GearRatios[0] × FinalDrive, what actually
            // drives LaunchControlRpm) barely changes — the lever is self-cancelling. Left
            // unguarded, that made the loop blindly repeat `newFirst = firstGear / ratio` against
            // a `ratio` that never improved, walking first gear to its 5.5 clamp (and FinalDrive
            // to its own floor/ceiling) for zero actual benefit — an artificially short first
            // gear no grip-based physics called for (roadmap #19: "gearing feels forced to hit
            // a mark" — this was it, just not the trap-speed target). Track the best (closest to
            // the optimal launch RPM) firstGear seen and stop as soon as a pass fails to improve
            // on the previous one meaningfully, then commit to the best rather than the last.
            double firstGear = GearingCalculator.CalcDragInitialFirstGear(car, parts, db);
            double bestFirst = firstGear;
            double bestDeviation = double.MaxValue;
            double prevDeviation = double.MaxValue;

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
                double deviation = Math.Abs(ratio - 1.0);
                if (deviation < bestDeviation)
                {
                    bestDeviation = deviation;
                    bestFirst = firstGear;
                }

                if (deviation < 0.04) break;

                // No real progress since the last pass — the final-drive fit absorbed the change
                // instead of the engine's actual launch RPM moving. Stop chasing; the best first
                // gear seen so far (possibly this iteration's, possibly an earlier one) is
                // reinstated below.
                if (iter > 0 && prevDeviation - deviation < 0.02)
                    break;
                prevDeviation = deviation;

                double newFirst = CalculationHelpers.Clamp(firstGear / ratio, 1.5, 5.5);
                if (Math.Abs(newFirst - firstGear) < 0.05)
                    break;
                firstGear = newFirst;
            }

            // The loop may have exited on a worse attempt than an earlier one (or on one that
            // never improved at all) — recompute gearing/launch control for whichever first gear
            // actually came closest to the optimal launch RPM.
            if (Math.Abs(bestFirst - firstGear) > 0.001)
            {
                GearingCalculator.CalculateGearing(car, track, parts, db, r, ex, effectiveMaxKmh, c, bestFirst);
                LaunchControlCalculator.CalculateLaunchControl(car, track, parts, db, r);
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
