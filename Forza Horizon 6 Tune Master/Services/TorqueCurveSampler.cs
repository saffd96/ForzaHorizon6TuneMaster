using System;

namespace Forza_Horizon_6_Tune_Master.Services;

// Samples a car's cached torque curve (CarCard.CachedTorqueCurveNm — 96 points evenly spaced
// over 0..MaxRPM) and solves the "equal wheel-force at the shift point" gear-step equation:
// no shift should lose tractive force at the wheels. See
// docs/superpowers/specs/2026-07-06-physics-based-gear-spacing-design.md for the derivation.
internal static class TorqueCurveSampler
{
    // Linear interpolation of the torque curve at an arbitrary RPM. Returns 0 for missing/too-short
    // curve data or a non-positive maxRpm — callers treat that as "no curve available".
    internal static double SampleTorqueNm(double[]? curve, double maxRpm, double rpm)
    {
        if (curve == null || curve.Length < 2 || maxRpm <= 0) return 0;

        double idx = Math.Clamp(rpm, 0, maxRpm) / maxRpm * (curve.Length - 1);
        int i0 = (int)Math.Floor(idx);
        int i1 = Math.Min(i0 + 1, curve.Length - 1);
        double frac = idx - i0;
        return curve[i0] + (curve[i1] - curve[i0]) * frac;
    }

    // Solves for the widest gear step x = ratioNext/ratioCurrent (0 < x < 1) such that shifting
    // at engine RPM `shiftRpm` doesn't drop wheel force: Torque(shiftRpm) == Torque(shiftRpm*x) * x.
    // The result is clamped into [stepMin, stepMax] — the discipline's own practical envelope —
    // so it never needs to search outside that range. Returns null when there isn't enough curve
    // data to compute a meaningful answer; callers fall back to their existing behavior.
    internal static double? SolveCrossoverStep(double[]? curve, double maxRpm, double shiftRpm,
        double stepMin, double stepMax)
    {
        if (curve == null || curve.Length < 2 || maxRpm <= 0 || shiftRpm <= 0) return null;
        if (stepMin >= stepMax) return null;

        double target = SampleTorqueNm(curve, maxRpm, shiftRpm);
        if (target <= 0) return null;

        double G(double x) => SampleTorqueNm(curve, maxRpm, shiftRpm * x) * x - target;

        double lo = stepMin, hi = stepMax;
        double gLo = G(lo), gHi = G(hi);

        // No sign change inside the envelope: g(x) >= 0 means "no force dip" (safe), g(x) < 0 means
        // a dip. If the WHOLE envelope is safe, the widest step (stepMin) is the most gear-efficient
        // choice that's still safe. If the whole envelope dips, the tightest step (stepMax) is the
        // least-bad choice (minimal RPM drop = minimal force loss). Only when the envelope straddles
        // the boundary (opposite signs) does the exact crossover lie inside it — bisect for that.
        if (gLo == 0) return lo;
        if (gHi == 0) return hi;
        if (Math.Sign(gLo) == Math.Sign(gHi))
            return gLo > 0 ? lo : hi;

        for (int i = 0; i < 40 && hi - lo > 1e-4; i++)
        {
            double mid = (lo + hi) / 2.0;
            double gMid = G(mid);
            if (gMid == 0) return mid;
            if (Math.Sign(gMid) == Math.Sign(gLo)) { lo = mid; gLo = gMid; }
            else { hi = mid; gHi = gMid; }
        }
        return (lo + hi) / 2.0;
    }
}
